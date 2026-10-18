using System;
using System.Collections.Generic;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Import
{
    /// <summary>
    /// Tudo o que se lê de um corpo sólido por COM para decidir se duas peças são a mesma, em
    /// DOIS níveis de custo, de propósito.
    ///
    /// POR QUE DOIS NÍVEIS. Uma montagem importada de outro CAD traz dezenas ou centenas de
    /// peças. Ler os vértices de todas seria uma chamada COM por vértice — dezenas de milhares
    /// de travessias — e a maior parte do trabalho jogada fora, porque peças de volumes
    /// diferentes nunca iam ser comparadas. Então:
    ///
    ///   1. <see cref="ReadCoarse"/>  — 4 leituras por corpo (volume + 3 contagens). Agrupa.
    ///   2. <see cref="ReadFine"/>    — áreas de face e raios de cilindro. Só dentro de grupo.
    ///   3. <see cref="ReadVertices"/> — a nuvem de pontos. Só para confirmar o encaixe.
    ///
    /// Tudo em coordenadas da PEÇA (locais) e em MILÍMETROS. As coordenadas locais são o que
    /// importa: é entre dois espaços locais que o <see cref="RigidFit"/> acha o movimento, e é
    /// esse movimento que depois entra na matriz da ocorrência.
    ///
    /// API confirmada (dump da typelib SE 2026 + leitura ao vivo):
    ///   Body.Volume              -> double   (m³)                          dispid 59
    ///   Body.Faces[queryType]    -> Faces                                  dispid 53
    ///   Body.Edges[queryType]    -> Edges                                  dispid 52
    ///   Face.Area                -> double   (m²)                          dispid 224
    ///   Face.Geometry.Radius     -> double   (m)   só em cilindro/cone/etc.
    ///   Face.Vertices            -> Vertices                               dispid 49
    ///   Vertex.GetPointData([in,out] SAFEARRAY(double))                    dispid 124
    ///   Vertex.ID                -> int                                    dispid 136
    /// </summary>
    public static class BodySampler
    {
        /// <summary>igQueryAll — vale 1, e NÃO 0 (regra do projeto).</summary>
        private const int igQueryAll = 1;

        /// <summary>igQueryCylinder na indexação de <c>Body.Faces[...]</c>
        /// (FeatureTopologyQueryTypeConstants — os inteiros pequenos, não o enum de geometria).</summary>
        private const int igQueryCylinder = 10;

        /// <summary>Teto de vértices lidos por corpo. Uma peça de molde tem centenas; uma malha
        /// convertida ou um corpo cheio de raios pode ter milhares, e aí a leitura vira o gargalo
        /// sem melhorar a decisão — 3 pontos já fixam a rotação, e o encaixe é conferido em
        /// TODOS os que foram lidos. Estourar o teto marca a leitura como parcial, e peça com
        /// leitura parcial não é unificada: o corpo entra no relatório como "não confirmado".</summary>
        public const int MaxVertices = 4000;

        /// <summary>
        /// O corpo sólido de um documento de peça, ou null. Pega o PRIMEIRO modelo sólido e
        /// ignora corpos de FACETAS (malha) — malha não tem face nem vértice de B-rep, e tentar
        /// unificá-la por este caminho daria leitura vazia.
        /// </summary>
        public static object SolidBodyOf(object partDocument, out string why)
        {
            why = null;
            dynamic doc = partDocument;
            int count = 0;
            try { count = (int)doc.Models.Count; }
            catch (Exception ex) { why = "Models ilegível: " + ex.GetBaseException().Message; return null; }
            if (count == 0) { why = "a peça não tem corpo (nenhum Model)."; return null; }

            for (int i = 1; i <= count; i++)
            {
                dynamic m;
                try { m = doc.Models.Item(i); } catch { continue; }

                bool facet = false;
                try { facet = (bool)m.IsFacetBody; } catch { }
                if (facet) continue;                     // malha: outro caminho (Eng. Reversa)

                try
                {
                    dynamic b = m.Body;
                    if (b != null) return (object)b;
                }
                catch { }
            }
            why = count > 0 ? "só há corpo de MALHA nesta peça (nada de B-rep para comparar)." : "sem corpo sólido.";
            return null;
        }

        /// <summary>
        /// Peneira grossa: volume + contagens. Quatro leituras por corpo — é o que torna
        /// possível varrer uma montagem inteira num clique.
        /// </summary>
        public static GeometrySignature ReadCoarse(object comBody)
        {
            var sig = new GeometrySignature();
            if (comBody == null) { sig.Partial = true; sig.ReadError = "corpo nulo."; return sig; }
            dynamic body = comBody;

            try
            {
                // Body.Volume vem em m³ (a API é toda em metros) -> mm³ é x1e9.
                double v = Convert.ToDouble(body.Volume);
                sig.VolumeMm3 = v * 1e9;
            }
            catch (Exception ex) { sig.Partial = true; sig.ReadError = "Body.Volume: " + ex.GetBaseException().Message; }

            sig.FaceCount = CountOf(() => body.Faces[igQueryAll], sig, "Body.Faces");
            sig.EdgeCount = CountOf(() => body.Edges[igQueryAll], sig, "Body.Edges");

            double[] min, max;
            if (FaceGeometry.TryGetBodyRangeMm(comBody, out min, out max))
            {
                var box = new[] { max[0] - min[0], max[1] - min[1], max[2] - min[2] };
                Array.Sort(box);
                sig.BoxMm = box;
            }

            // A contagem de vértices sai da varredura das faces (não há Body.Vertices na API —
            // confirmado no dump: Body expõe Faces, Edges e Shells, não Vertices).
            var ids = new HashSet<int>();
            sig.VertexCount = CountVertices(comBody, ids, sig);
            return sig;
        }

        /// <summary>
        /// Segundo crivo: as listas ordenadas de áreas de face e de raios de cilindro. Caro
        /// (uma leitura por face) e forte — só vale rodar dentro de um grupo que já empatou na
        /// peneira grossa.
        /// </summary>
        public static void ReadFine(object comBody, GeometrySignature sig)
        {
            if (comBody == null || sig == null) return;
            dynamic body = comBody;

            var areas = new List<double>();
            double total = 0;
            try
            {
                dynamic faces = body.Faces[igQueryAll];
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    try
                    {
                        // Face.Area vem em m² -> mm² é x1e6.
                        double a = Convert.ToDouble(faces.Item(i).Area) * 1e6;
                        areas.Add(a);
                        total += a;
                    }
                    catch { sig.Partial = true; }
                }
            }
            catch (Exception ex) { sig.Partial = true; sig.ReadError = "áreas das faces: " + ex.GetBaseException().Message; }

            sig.SortedFaceAreasMm2 = GeometrySignature.Sorted(areas);
            sig.AreaMm2 = total;

            var radii = new List<double>();
            try
            {
                dynamic cyls = body.Faces[igQueryCylinder];
                int n = (int)cyls.Count;
                for (int i = 1; i <= n; i++)
                {
                    double r = RadiusMm(cyls.Item(i));
                    if (r > 0) radii.Add(r);
                }
            }
            catch { /* corpo sem cilindro nenhum: caminho normal, não é erro */ }
            sig.SortedCylinderRadiiMm = GeometrySignature.Sorted(radii);
        }

        /// <summary>
        /// A nuvem de VÉRTICES do corpo, em coordenadas da peça e em mm, sem repetição.
        ///
        /// Por que varrer por FACE e deduplicar por <c>Vertex.ID</c>: a API não expõe
        /// <c>Body.Vertices</c>, então o único caminho é <c>Face.Vertices</c> — e cada vértice
        /// pertence a 3 ou mais faces, logo apareceria 3+ vezes. Vértice repetido estraga o
        /// encaixe duas vezes: infla a contagem (que é critério de igualdade) e desloca o
        /// centroide para o lado dos cantos com mais faces.
        /// </summary>
        public static List<double[]> ReadVertices(object comBody, out bool partial, out string why)
        {
            partial = false;
            why = null;
            var pts = new List<double[]>();
            if (comBody == null) { partial = true; why = "corpo nulo."; return pts; }

            dynamic body = comBody;
            var seen = new HashSet<int>();
            bool anyIdMissing = false;

            try
            {
                dynamic faces = body.Faces[igQueryAll];
                int nf = (int)faces.Count;
                for (int i = 1; i <= nf; i++)
                {
                    dynamic face;
                    try { face = faces.Item(i); } catch { partial = true; continue; }

                    dynamic verts;
                    // Face.Vertices não existe em todo corpo (já visto em corpo de superfície
                    // neste projeto) — é recusa esperada, não erro de código.
                    try { verts = face.Vertices; } catch { partial = true; continue; }
                    if (verts == null) continue;

                    int nv;
                    try { nv = (int)verts.Count; } catch { partial = true; continue; }

                    for (int k = 1; k <= nv; k++)
                    {
                        dynamic v;
                        try { v = verts.Item(k); } catch { partial = true; continue; }

                        int id;
                        try { id = Convert.ToInt32(v.ID); }
                        catch { id = int.MinValue; anyIdMissing = true; }

                        if (id != int.MinValue && !seen.Add(id)) continue;   // já lido por outra face

                        double[] p;
                        if (!TryPoint((object)v, out p)) { partial = true; continue; }

                        if (id == int.MinValue && AlreadyThere(pts, p)) continue;  // sem ID: dedup por posição
                        pts.Add(p);

                        if (pts.Count >= MaxVertices)
                        {
                            partial = true;
                            why = $"corpo com mais de {MaxVertices} vértices — leitura interrompida.";
                            return pts;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                partial = true;
                why = "varredura de vértices: " + ex.GetBaseException().Message;
            }

            if (anyIdMissing)
                Log.Warn("[IMPORT] algum vértice não expôs ID — deduplicado por posição (mais lento).");
            if (pts.Count == 0 && why == null) why = "o corpo não expôs vértice nenhum.";
            return pts;
        }

        /// <summary>igCircle no <c>Geometry.Type</c> de uma aresta (enum de GEOMETRIA, não de
        /// topologia — <c>Edge.Type</c> responde sempre igEdge).</summary>
        private const int igCircle = 167551105;

        /// <summary>
        /// Os ANÉIS do corpo: toda aresta cuja geometria é um círculo, com centro, eixo e raio
        /// exatos. Em coordenadas da PEÇA e em mm.
        ///
        /// POR QUE EXISTE. O primeiro run real (base de molde Polimold, 2026-10-18) mostrou que
        /// bucha, pino e coluna têm <b>zero vértices</b> — numa peça torneada toda aresta é
        /// círculo. Sem vértice o encaixe por nuvem de pontos não tem o que casar, e eram
        /// justamente as peças mais repetidas de um molde. Os anéis são a informação que essas
        /// peças TÊM, e <see cref="AxisProfileFit"/> é quem a usa.
        ///
        /// <paramref name="allCircular"/> sai falso se existir QUALQUER aresta não circular: a
        /// conclusão "é sólido de revolução" depende disso, e uma aresta reta esquecida a
        /// invalidaria.
        /// </summary>
        public static List<Ring> ReadRings(object comBody, out bool allCircular, out string why)
        {
            allCircular = false;
            why = null;
            var rings = new List<Ring>();
            if (comBody == null) { why = "corpo nulo."; return rings; }

            int other = 0, unreadable = 0;
            try
            {
                dynamic edges = ((dynamic)comBody).Edges[igQueryAll];
                int n = (int)edges.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic geom;
                    try { geom = edges.Item(i).Geometry; } catch { unreadable++; continue; }
                    if (geom == null) { unreadable++; continue; }

                    int t;
                    try { t = Convert.ToInt32(geom.Type); } catch { unreadable++; continue; }
                    if (t != igCircle) { other++; continue; }

                    double rM;
                    try { rM = Convert.ToDouble(geom.Radius); } catch { unreadable++; continue; }

                    double[] center = TryVec3((object)geom, isCenter: true);
                    double[] axis = TryVec3((object)geom, isCenter: false);
                    if (center == null || axis == null) { unreadable++; continue; }

                    rings.Add(new Ring
                    {
                        CenterMm = new[] { Units.MToMm(center[0]), Units.MToMm(center[1]), Units.MToMm(center[2]) },
                        AxisDir = axis,
                        RadiusMm = Units.MToMm(rM)
                    });
                }
            }
            catch (Exception ex)
            {
                why = "varredura de arestas: " + ex.GetBaseException().Message;
                return rings;
            }

            allCircular = other == 0 && unreadable == 0 && rings.Count > 0;
            if (!allCircular)
                why = other > 0
                    ? $"{other} aresta(s) não circular(es) — o corpo não é só de revolução."
                    : unreadable > 0 ? $"{unreadable} aresta(s) ilegível(eis)."
                    : "nenhuma aresta circular.";
            return rings;
        }

        /// <summary><c>Circle.GetCenterPoint</c> / <c>Circle.GetAxisVector</c> — ambos
        /// <c>[in,out] SAFEARRAY(double)</c>, então o array tem de chegar ALOCADO (<c>ref null</c>
        /// dá E_INVALIDARG). Confirmados ao vivo em 21 de 21 arestas de duas buchas e duas
        /// colunas reais (SE 2026, 2026-10-18).</summary>
        private static double[] TryVec3(object comCircle, bool isCenter)
        {
            try
            {
                object buf = new double[3];
                if (isCenter) ((dynamic)comCircle).GetCenterPoint(ref buf);
                else ((dynamic)comCircle).GetAxisVector(ref buf);
                var raw = buf as double[];
                return raw != null && raw.Length >= 3 ? raw : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// <c>Vertex.GetPointData([in,out] SAFEARRAY(double))</c> — o array tem de chegar
        /// ALOCADO. Passar <c>ref null</c> dá E_INVALIDARG (a mesma armadilha de
        /// <c>Face.GetRange</c>, registrada na skill). Com <c>dynamic</c> + <c>ref</c> o
        /// marshaling funciona sem <c>ParameterModifier</c>.
        /// </summary>
        private static bool TryPoint(object comVertex, out double[] pointMm)
        {
            pointMm = null;
            try
            {
                object buf = new double[3];
                ((dynamic)comVertex).GetPointData(ref buf);
                var raw = buf as double[];
                if (raw == null || raw.Length < 3) return false;

                // A API devolve METROS; o domínio deste projeto é mm (ver Model.Units — fator
                // 1000 trocado aqui não estoura nada, só produz uma peça "igual" que não é).
                pointMm = new[] { Units.MToMm(raw[0]), Units.MToMm(raw[1]), Units.MToMm(raw[2]) };
                return true;
            }
            catch { return false; }
        }

        private static bool AlreadyThere(List<double[]> pts, double[] p)
        {
            foreach (double[] q in pts)
            {
                if (Math.Abs(q[0] - p[0]) < 1e-6 && Math.Abs(q[1] - p[1]) < 1e-6 && Math.Abs(q[2] - p[2]) < 1e-6)
                    return true;
            }
            return false;
        }

        private static int CountVertices(object comBody, HashSet<int> ids, GeometrySignature sig)
        {
            dynamic body = comBody;
            int noId = 0;
            try
            {
                dynamic faces = body.Faces[igQueryAll];
                int nf = (int)faces.Count;
                for (int i = 1; i <= nf; i++)
                {
                    dynamic verts;
                    try { verts = faces.Item(i).Vertices; } catch { continue; }
                    if (verts == null) continue;

                    int nv;
                    try { nv = (int)verts.Count; } catch { continue; }
                    for (int k = 1; k <= nv; k++)
                    {
                        try
                        {
                            int id = Convert.ToInt32(verts.Item(k).ID);
                            ids.Add(id);
                        }
                        catch { noId++; }
                    }
                }
            }
            catch (Exception ex)
            {
                sig.Partial = true;
                sig.ReadError = "contagem de vértices: " + ex.GetBaseException().Message;
            }
            // Sem ID não há como deduplicar barato: conta como parcial para a peça não ser
            // unificada com base numa contagem inflada.
            if (noId > 0) sig.Partial = true;
            return ids.Count;
        }

        private static int CountOf(Func<dynamic> get, GeometrySignature sig, string what)
        {
            try
            {
                dynamic col = get();
                return col == null ? 0 : (int)col.Count;
            }
            catch (Exception ex)
            {
                sig.Partial = true;
                sig.ReadError = what + ": " + ex.GetBaseException().Message;
                return 0;
            }
        }

        /// <summary>Raio (mm) de uma face cilíndrica, pela GEOMETRIA e não pela caixa envolvente
        /// — numa face cilíndrica parcial a caixa mede a corda, não o diâmetro. Zero quando a
        /// face não tem raio (face plana: recusa esperada, não erro).</summary>
        public static double RadiusMm(object comFace)
        {
            try
            {
                dynamic geom = ((dynamic)comFace).Geometry;
                if (geom == null) return 0;
                double r = Convert.ToDouble(geom.Radius);
                return r > 0 ? Units.MToMm(r) : 0;
            }
            catch { return 0; }
        }
    }
}
