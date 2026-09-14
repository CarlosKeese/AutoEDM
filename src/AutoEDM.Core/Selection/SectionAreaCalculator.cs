using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Área da SECÇÃO DE QUEIMA (cm²) — a coluna nova da janela "Coordenadas"
    /// (Carlos, 2026-08-12). É a área do contorno fechado que um plano HORIZONTAL
    /// no MEIO DA ALTURA das faces de queima recorta: Z = (Zmin+Zmax)/2 do conjunto
    /// de faces onde o GAP foi aplicado. Medida no corpo JÁ COM o offset do GAP (o
    /// eletrodo final, subdimensionado), que é o que a peça .par realmente tem.
    ///
    /// Como (sem criar geometria nenhuma — SOMENTE-LEITURA):
    /// 1. <c>Face.GetFacetData(tolerância, [out] nFacetas, [in,out] pontos)</c> devolve
    ///    a MALHA de triângulos da face (metros, 9 doubles por faceta = 3 vértices ×
    ///    XYZ) — confirmado no dump da typelib (<c>_IDMDFace</c>, 5 params). Os args
    ///    [out]/[in,out] precisam de <see cref="ParameterModifier"/> by-ref, mesma
    ///    armadilha de <see cref="FaceGeometry.TryGetRangeMm"/>.
    /// 2. Cada triângulo é cortado pelo plano Z=meio, gerando um SEGMENTO no plano XY.
    /// 3. A área do contorno formado por esses segmentos sai por VARREDURA (scanline)
    ///    com regra par-ímpar: nada de encadear os segmentos em loops nem depender da
    ///    orientação (winding) da malha — assim ilhas e furos internos entram certo, e
    ///    uma faceta com normal invertida não zera a conta.
    ///
    /// **DE ONDE SAI A MALHA (correção 2026-09-02, Carlos: "em alguns casos a área não
    /// aparece").** A 1ª versão malhava as FACES DA FEATURE DE GAP. Mas esse conjunto é o que o
    /// usuário selecionou (ou o heurístico "Z ≤ base do bloco" pegou) em "Aplicar GAP" — não há
    /// nenhuma garantia de que ele dê a volta completa no eletrodo na altura do corte. Faltando
    /// um pedaço da parede, o contorno não fecha e a varredura ou recusa ("não achou área
    /// fechada" = coluna vazia) ou, pior, devolve um número plausível e ERRADO. Agora as faces de
    /// queima definem só ONDE cortar (o meio da altura delas) e quem é malhado é o CORPO SÓLIDO,
    /// que é fechado por construção — o corte de um sólido sempre dá contorno fechado. Na altura
    /// da queima o único material é a própria forma (o bloco/faixa ficam acima), então a secção
    /// medida continua sendo a da queima, já com o offset do GAP aplicado.
    ///
    /// NUNCA lança: devolve false + motivo. O botão é só leitura, uma área ausente
    /// vira "—" na grade, não um erro.
    /// </summary>
    public static class SectionAreaCalculator
    {
        /// <summary>Tolerância de corda da malha (metros) — 0,02mm dá contorno fiel sem explodir o nº de facetas.</summary>
        private const double FacetToleranceM = 0.00002;

        /// <summary>Linhas de varredura. 512 mantém o erro bem abaixo de 1% mesmo em contorno curvo.</summary>
        private const int ScanLines = 512;

        /// <summary>
        /// Elevação (mm) do plano de corte quando as faces de queima são PLANAS em Z: sem altura
        /// não há "meio", então corta-se logo acima do plano da queima — a secção do corpo ali é
        /// a própria pegada da queima. Antes esse caso devolvia "—".
        /// </summary>
        private const double FlatBurnLiftMm = 0.05;

        /// <summary>
        /// Área (cm²) da secção horizontal no meio da altura das <paramref name="comBurnFaces"/>.
        ///
        /// As faces de queima definem só ONDE cortar (o meio da altura delas). O que é MALHADO é
        /// o <paramref name="comSolidBody"/> — ver a nota "de onde sai a malha" na classe. Sem
        /// corpo (null) cai no comportamento antigo, malhando as próprias faces de queima.
        /// </summary>
        /// <param name="areaCm2">Área da secção, em cm².</param>
        /// <param name="zMidMm">Z do plano de corte usado (mm, sistema local da peça) — vai para o log.</param>
        /// <param name="error">Motivo, quando devolve false.</param>
        public static bool TryMidSectionAreaCm2(IReadOnlyList<object> comBurnFaces, object comSolidBody,
            out double areaCm2, out double zMidMm, out string error)
        {
            areaCm2 = 0; zMidMm = 0; error = null;
            if (comBurnFaces == null || comBurnFaces.Count == 0) { error = "nenhuma face de queima"; return false; }

            if (!TryZExtentMm(comBurnFaces, out double zMinMm, out double zMaxMm, out error)) return false;

            bool flat = (zMaxMm - zMinMm) < 1e-6;
            zMidMm = flat ? zMinMm + FlatBurnLiftMm : 0.5 * (zMinMm + zMaxMm);
            if (flat)
                Log.Info($"Secção de queima: faces de queima planas em Z ({zMinMm:0.000}mm) — cortando {FlatBurnLiftMm:0.00}mm acima (a secção ali é a pegada da queima).");

            // De onde sai a MALHA: o corpo sólido, não as faces de queima (ver nota na classe).
            IReadOnlyList<object> meshFaces = comBurnFaces;
            string faceSource = "faces da feature de GAP";
            if (comSolidBody != null)
            {
                List<object> crossing = FacesCrossingZ(comSolidBody, zMidMm);
                if (crossing.Count > 0)
                {
                    meshFaces = crossing;
                    faceSource = "corpo sólido";
                }
                else
                {
                    Log.Warn($"Secção de queima: nenhuma face do corpo cruza Z={zMidMm:0.000}mm — malhando as faces de queima (contorno pode não fechar).");
                }
            }

            var meshes = new List<double[]>(meshFaces.Count);
            int noMesh = 0;
            foreach (object face in meshFaces)
            {
                if (!TryGetFacetPointsM(face, out double[] pts, out string ferr))
                {
                    Log.Warn($"Secção de queima: malha de uma face indisponível — {ferr}");
                    noMesh++;
                    continue;
                }
                meshes.Add(pts);
            }
            if (meshes.Count == 0) { error = "nenhuma face devolveu malha (GetFacetData)"; return false; }
            if (noMesh > 0) Log.Warn($"Secção de queima: {noMesh} face(s) sem malha — o contorno pode ficar aberto e a medição é recusada se ficar.");

            if (!TryAreaFromMeshesCm2(meshes, zMidMm, out areaCm2, out int segmentCount, out int badLines, out error))
                return false;

            Log.Info($"Secção de queima: Z={zMidMm:0.000}mm (queima entre {zMinMm:0.000} e {zMaxMm:0.000}), " +
                     $"malha de {meshes.Count} face(s) ({faceSource}), {segmentCount} segmento(s), área {areaCm2:0.000} cm²" +
                     (badLines > 0 ? $" ({badLines} linha(s) de varredura degenerada(s), ignorada(s))" : ""));
            return true;
        }

        /// <summary>
        /// Faces do CORPO cujo bbox contém o plano Z — as únicas que podem gerar segmento no
        /// corte. Malhar só elas mantém o custo perto do de antes mesmo num corpo com muitas
        /// faces. Best-effort: face sem bbox legível fica de fora (e o cruzamento X/Y da
        /// varredura acusa se isso abriu o contorno).
        /// </summary>
        private static List<object> FacesCrossingZ(object comSolidBody, double zMm, double tolMm = 1e-6)
        {
            var hits = new List<object>();
            try
            {
                dynamic all = ((dynamic)comSolidBody).Faces[1]; // 1 = igQueryAll
                int n = 0; try { n = (int)all.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f; try { f = all.Item(i); } catch { continue; }
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mn[2] - tolMm <= zMm && mx[2] + tolMm >= zMm) hits.Add(f);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Secção de queima: faces do corpo inacessíveis — " + ex.GetBaseException().Message);
            }
            return hits;
        }

        /// <summary>
        /// Núcleo geométrico, SEM COM: recebe as malhas já lidas (uma por face, 9 doubles por
        /// faceta, em METROS) e o Z do plano de corte (mm) e devolve a área (cm²). Separado do
        /// caminho COM para poder ser testado sem Solid Edge — <c>SectionAreaCalculatorTests</c>.
        /// </summary>
        public static bool TryAreaFromMeshesCm2(IReadOnlyList<double[]> meshesM, double zMidMm,
            out double areaCm2, out int segmentCount, out int badScanLines, out string error)
        {
            areaCm2 = 0; segmentCount = 0; badScanLines = 0; error = null;
            double zMidM = Units.MmToM(zMidMm);

            var segments = new List<Segment>();
            foreach (double[] pts in meshesM ?? Array.Empty<double[]>())
            {
                if (pts == null || pts.Length < 9) continue;
                CollectPlaneCrossings(pts, zMidM, segments);
            }
            segmentCount = segments.Count;
            if (segmentCount < 3)
            {
                error = $"o plano Z={zMidMm:0.000}mm cortou só {segmentCount} segmento(s) — contorno não fechou";
                return false;
            }

            // Varre nas DUAS direções (linhas horizontais e verticais). Num contorno FECHADO as
            // duas áreas batem; num contorno ABERTO não — e é justamente o caso perigoso, porque
            // uma varredura só pode devolver um número plausível e errado (faltando a parede de
            // cima, as linhas horizontais ainda cruzam 2 paredes e "fecham" a conta sozinhas).
            int badY, badX;
            double areaYMm2 = ScanlineArea(segments, swapAxes: false, badLines: out badY);
            double areaXMm2 = ScanlineArea(segments, swapAxes: true, badLines: out badX);
            badScanLines = badY + badX;

            if (areaYMm2 <= 0 || areaXMm2 <= 0)
            {
                // Falta uma parede TRANSVERSAL à direção varrida: todas as linhas daquela
                // direção cruzam o contorno um nº ímpar de vezes e nenhuma faixa fecha.
                error = string.Format(CultureInfo.InvariantCulture,
                    "contorno ABERTO no plano Z={0:0.000}mm — a varredura em {1} não fechou nenhuma faixa (malha incompleta)",
                    zMidMm, areaYMm2 <= 0 ? "Y" : "X");
                return false;
            }

            double diff = Math.Abs(areaYMm2 - areaXMm2) / Math.Max(areaYMm2, areaXMm2);
            if (diff > MaxAxisMismatch)
            {
                error = string.Format(CultureInfo.InvariantCulture,
                    "contorno ABERTO no plano Z={0:0.000}mm (varredura em Y deu {1:0.00} mm² e em X {2:0.00} mm², {3:0.0}% de diferença) — malha incompleta",
                    zMidMm, areaYMm2, areaXMm2, diff * 100);
                return false;
            }

            areaCm2 = 0.5 * (areaYMm2 + areaXMm2) / 100.0; // 1 cm² = 100 mm²
            return true;
        }

        /// <summary>
        /// Diferença máxima tolerada entre a varredura em Y e a em X (fração). Num contorno
        /// fechado a diferença é só discretização (bem abaixo de 1% com 512 linhas); acima disso
        /// o contorno está aberto e a medição é recusada em vez de sair errada.
        /// </summary>
        private const double MaxAxisMismatch = 0.05;

        // ------------------------------------------------------------------

        private struct Segment
        {
            public double X1, Y1, X2, Y2; // mm
        }

        /// <summary>Extensão Z (mm) do conjunto de faces, via <see cref="FaceGeometry.TryGetRangeMm"/>.</summary>
        private static bool TryZExtentMm(IReadOnlyList<object> comFaces,
            out double zMinMm, out double zMaxMm, out string error)
        {
            zMinMm = double.MaxValue; zMaxMm = double.MinValue; error = null;
            int read = 0;
            foreach (object face in comFaces)
            {
                if (!FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx)) continue;
                if (mn[2] < zMinMm) zMinMm = mn[2];
                if (mx[2] > zMaxMm) zMaxMm = mx[2];
                read++;
            }
            if (read == 0) { error = "range (Z) de nenhuma face de queima foi lido"; return false; }
            return true;
        }

        /// <summary>
        /// Malha da face em METROS: 9 doubles por faceta (v0.xyz, v1.xyz, v2.xyz).
        /// </summary>
        private static bool TryGetFacetPointsM(object comFace, out double[] pointsM, out string error) =>
            TryGetFacetPointsM(comFace, FacetToleranceM, out pointsM, out error);

        /// <summary>
        /// Mesma leitura com tolerância de corda escolhida (metros) — a miniatura da Lista de corte
        /// usa uma bem mais grossa que a da secção: numa imagem de 1 pol. o detalhe não aparece e a
        /// malha fica leve.
        /// </summary>
        public static bool TryGetFacetPointsM(object comFace, double toleranceM, out double[] pointsM, out string error)
        {
            pointsM = null; error = null;
            try
            {
                // (Tolerance, [out] FacetCount, [in,out] Points, [opt][out] Normals, [opt][out] TextureCoords)
                object[] args = { toleranceM, 0, new double[0] };
                var mod = new ParameterModifier(3);
                mod[1] = true; // FacetCount [out]
                mod[2] = true; // Points [in,out]

                comFace.GetType().InvokeMember(
                    "GetFacetData", BindingFlags.InvokeMethod, null, comFace, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                pointsM = ToDoubles(args[2]);
                if (pointsM == null || pointsM.Length < 9)
                {
                    error = $"malha vazia ({pointsM?.Length ?? 0} ponto(s))";
                    return false;
                }
                // O nº de facetas SAI do tamanho do array, não de FacetCount: aquele é um
                // `[out] int*` no MEIO da assinatura e o late binding nem sempre popula
                // [out] escalares (o array [in,out] é o que vem confiável). FacetCount só
                // serve de conferência, e quando diverge quem manda é o array.
                int declared = 0; try { declared = Convert.ToInt32(args[1]); } catch { }
                int facets = pointsM.Length / 9;
                if (declared > 0 && declared != facets)
                    Log.Warn($"Secção de queima: GetFacetData declarou {declared} faceta(s) mas o array tem {facets} — usando o array.");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        /// <summary>
        /// Corta cada triângulo da malha pelo plano Z=<paramref name="zM"/> e acumula o
        /// segmento resultante (em mm). Triângulo que não atravessa o plano é ignorado.
        /// </summary>
        private static void CollectPlaneCrossings(double[] ptsM, double zM, List<Segment> into)
        {
            int facets = ptsM.Length / 9;
            for (int f = 0; f < facets; f++)
            {
                int b = f * 9;
                var hits = new List<double[]>(2);
                for (int e = 0; e < 3; e++)
                {
                    int i = b + e * 3;
                    int j = b + ((e + 1) % 3) * 3;
                    double za = ptsM[i + 2], zb = ptsM[j + 2];
                    // Estritamente atravessando: aresta EM CIMA do plano (za==zb==z) ou
                    // tocando só num vértice não fecha contorno e só geraria ruído.
                    if ((za < zM && zb < zM) || (za > zM && zb > zM)) continue;
                    double d = zb - za;
                    if (Math.Abs(d) < 1e-15) continue;
                    double t = (zM - za) / d;
                    if (t < 0.0 || t > 1.0) continue;
                    hits.Add(new[]
                    {
                        Units.MToMm(ptsM[i]     + t * (ptsM[j]     - ptsM[i])),
                        Units.MToMm(ptsM[i + 1] + t * (ptsM[j + 1] - ptsM[i + 1])),
                    });
                    if (hits.Count == 2) break;
                }
                if (hits.Count != 2) continue;
                into.Add(new Segment { X1 = hits[0][0], Y1 = hits[0][1], X2 = hits[1][0], Y2 = hits[1][1] });
            }
        }

        /// <summary>
        /// Área (mm²) do contorno por VARREDURA com regra par-ímpar. Independe da
        /// orientação dos segmentos e trata ilhas/furos sem precisar encadear loops.
        /// </summary>
        /// <param name="swapAxes">true = varre com linhas VERTICAIS (X e Y trocados) — a segunda
        /// leitura que denuncia contorno aberto, ver <see cref="MaxAxisMismatch"/>.</param>
        private static double ScanlineArea(List<Segment> segsIn, bool swapAxes, out int badLines)
        {
            badLines = 0;
            List<Segment> segs = segsIn;
            if (swapAxes)
            {
                segs = new List<Segment>(segsIn.Count);
                foreach (var s in segsIn)
                    segs.Add(new Segment { X1 = s.Y1, Y1 = s.X1, X2 = s.Y2, Y2 = s.X2 });
            }

            double yMin = double.MaxValue, yMax = double.MinValue;
            foreach (var s in segs)
            {
                yMin = Math.Min(yMin, Math.Min(s.Y1, s.Y2));
                yMax = Math.Max(yMax, Math.Max(s.Y1, s.Y2));
            }
            double height = yMax - yMin;
            if (height <= 0) return 0;

            double dy = height / ScanLines;
            double area = 0;
            var xs = new List<double>(32);

            for (int k = 0; k < ScanLines; k++)
            {
                // Meio da faixa: evita cair exatamente sobre um vértice da malha
                // (empate que quebraria a contagem par-ímpar).
                double y = yMin + (k + 0.5) * dy;
                xs.Clear();
                foreach (var s in segs)
                {
                    double ya = s.Y1, yb = s.Y2;
                    if ((ya <= y && yb <= y) || (ya > y && yb > y)) continue; // regra semiaberta
                    double d = yb - ya;
                    if (Math.Abs(d) < 1e-15) continue;
                    double t = (y - ya) / d;
                    xs.Add(s.X1 + t * (s.X2 - s.X1));
                }
                if (xs.Count < 2) continue;
                if (xs.Count % 2 != 0) { badLines++; continue; } // contorno aberto nessa altura
                xs.Sort();
                double span = 0;
                for (int i = 0; i + 1 < xs.Count; i += 2) span += xs[i + 1] - xs[i];
                area += span * dy;
            }
            return area;
        }

        private static double[] ToDoubles(object arr)
        {
            if (arr is double[] d) return d;
            if (arr is Array a)
            {
                var list = new List<double>(a.Length);
                foreach (var v in a) list.Add(Convert.ToDouble(v));
                return list.ToArray();
            }
            return null;
        }
    }
}
