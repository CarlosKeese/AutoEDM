using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Machinability
{
    /// <summary>Uma face cuja curvatura está fora do que a ferramentaria produz.</summary>
    public sealed class SmallRadiusHit
    {
        /// <summary><c>Face.ID</c>, quando legível (0 = não foi possível ler). Serve para achar a face de novo.</summary>
        public int FaceId { get; set; }

        /// <summary>"furo/pino", "canto" (cilindro parcial) ou "raio de canto" (toro).</summary>
        public string Kind { get; set; }

        /// <summary>
        /// A face dá a volta completa (2π no parâmetro angular) — é um FURO ou um pino, não um
        /// canto de fresa. Muda o veredito de profundidade: furo se faz com broca.
        /// </summary>
        public bool IsFullRevolution { get; set; }

        /// <summary>Raio que a face exige da fresa (mm). No toro é o raio MENOR — o do arredondamento.</summary>
        public double RadiusMm { get; set; }

        /// <summary>Profundidade (mm) do ponto mais fundo da face abaixo do topo do corpo.</summary>
        public double DepthMm { get; set; }

        public double MinZmm { get; set; }
        public double MaxZmm { get; set; }

        public MachinabilityVerdict Verdict { get; set; }

        /// <summary>A fresa que o raio aceitaria, quando o problema é só de alcance. Null nos demais casos.</summary>
        public MillingTool RadiusWouldAccept { get; set; }

        /// <summary>Eixo do cilindro/toro (vetor unitário, referencial do corpo). Null se ilegível.</summary>
        public double[] AxisDir { get; set; }

        /// <summary>Ponto do eixo (mm, referencial do corpo): base do cilindro ou centro do toro. Null se ilegível.</summary>
        public double[] AxisPointMm { get; set; }

        /// <summary>Raio MAIOR do toro (mm); 0 no cilindro.</summary>
        public double MajorRadiusMm { get; set; }

        /// <summary>Caixa da face (mm, referencial do corpo). Null se ilegível.</summary>
        public double[] FaceMinMm { get; set; }
        public double[] FaceMaxMm { get; set; }

        /// <summary>A face é côncava (material por fora do raio)? Null quando não deu para decidir.</summary>
        public bool? Concave { get; set; }

        /// <summary>Linha pronta para o usuário (pt-BR, vírgula decimal e unidade explícita).</summary>
        public string Describe()
        {
            string where = FaceId != 0 ? $"face {FaceId}" : "face";
            string size = IsFullRevolution
                ? $"Ø{RadiusMm * 2:0.000} mm"
                : $"R{RadiusMm:0.000} mm";
            string what = $"{Kind} {size}, Z de {MinZmm:0.0} a {MaxZmm:0.0} mm " +
                          $"({DepthMm:0.0} mm abaixo do topo)";
            switch (Verdict)
            {
                case MachinabilityVerdict.BelowMinimumRadius:
                    return $"{where}: {what} — abaixo do raio mínimo da ferramentaria. Só EDM.";
                case MachinabilityVerdict.BeyondReach:
                    string tool = RadiusWouldAccept != null ? RadiusWouldAccept.Label : "a fresa do raio";
                    return $"{where}: {what} — o raio aceita {tool}, mas ela não chega tão fundo. Só EDM (ou uma fresa mais longa).";
                case MachinabilityVerdict.HoleBeyondMillReach:
                    return $"{where}: {what} — é furo, e nem fresa nem broca do jogo chegam lá. Só EDM.";
                default:
                    return $"{where}: {what} — usinável.";
            }
        }
    }

    /// <summary>
    /// NÍVEL 1 da análise de usinabilidade: o raio EXATO, direto do B-Rep, sem malha nenhuma.
    ///
    /// Pergunta uma coisa só e responde com o número certo: existe face cuja curvatura seja mais
    /// fechada do que a ferramentaria consegue produzir, ou funda demais para a fresa que aquele
    /// raio aceita? Roda em milissegundos, não discretiza nada e não tem limiar para calibrar —
    /// o oposto do nível 2 (rasterização por fatia), que ACHA e DELIMITA região mas com a
    /// precisão do passo do raster. Os dois se completam: <b>o 2 acha, o 1 quantifica.</b>
    ///
    /// **Por que propriedade e não <c>GetCylinderData</c>:** <c>Cylinder.Radius</c>,
    /// <c>Torus.MinorRadius</c> e <c>Torus.MajorRadius</c> são PROPRIEDADES get no dump da
    /// typelib. Os métodos <c>GetCylinderData</c>/<c>GetTorusData</c> devolvem o raio por [out]
    /// ESCALAR — e escalar [out] em late binding é exatamente o que não popula confiável neste
    /// runtime (a mesma armadilha já anotada em <see cref="SectionAreaCalculator"/>, onde o
    /// <c>FacetCount</c> teve de ser ignorado em favor do tamanho do array). Propriedade não tem
    /// marshaling de saída, então é o caminho seguro.
    ///
    /// **Limite CONHECIDO desta rodada:** não distingue face CÔNCAVA de CONVEXA. Na prática isso
    /// não gera falso positivo, porque os dois casos são problema de fabricação — um canto
    /// côncavo de R0,3 não se fresa, e um pino de Ø0,6 em pé também não (quebra) —, mas o MOTIVO
    /// relatado sai genérico. A separação vem no nível 2, que enxerga o vazio e portanto sabe de
    /// que lado está o material.
    ///
    /// SOMENTE LEITURA: nenhuma geometria é criada. Nunca lança — face ilegível vira aviso no log.
    /// </summary>
    public static class BRepRadiusProbe
    {
        // FeatureTopologyQueryTypeConstants (dump da typelib)
        private const int igQueryTorus = 8;
        private const int igQueryCylinder = 10;

        /// <summary>
        /// Varre as faces cilíndricas e tóricas de <paramref name="comBody"/> e devolve as que a
        /// <paramref name="ladder"/> não produz. Lista vazia = nenhuma curvatura fora de alcance.
        /// </summary>
        /// <param name="comBody">Um <c>SolidEdgeGeometry.Body</c> vivo (ex.: <c>Models.Item(1).Body</c>).</param>
        public static IReadOnlyList<SmallRadiusHit> Probe(object comBody, ToolLadder ladder, DrillSet drills = null)
        {
            var hits = new List<SmallRadiusHit>();
            if (comBody == null) { Log.Warn("Raios mínimos: corpo nulo — análise não executada."); return hits; }
            ladder = ladder ?? ToolLadder.Shop();
            drills = drills ?? DrillSet.Din338();

            // Topo do corpo: a profundidade de cada face é medida A PARTIR DAQUI, porque é de cima
            // que a fresa desce. Sem o topo não dá para julgar alcance — só raio.
            bool hasTop = FaceGeometry.TryGetBodyRangeMm(comBody, out double[] _, out double[] bodyMax);
            double topZmm = hasTop ? bodyMax[2] : 0.0;
            if (!hasTop)
                Log.Warn("Raios mínimos: range do corpo ilegível — a profundidade não entra no veredito (só o raio).");

            Log.Info(ladder.Describe());
            Log.Info(drills.Describe());

            int scanned = 0, unreadable = 0;
            scanned += Scan(comBody, igQueryCylinder, "cilindro", false, ladder, drills, topZmm, hasTop, hits, ref unreadable);
            scanned += Scan(comBody, igQueryTorus, "raio de canto", true, ladder, drills, topZmm, hasTop, hits, ref unreadable);

            int below = hits.Count(h => h.Verdict == MachinabilityVerdict.BelowMinimumRadius);
            int far = hits.Count(h => h.Verdict == MachinabilityVerdict.BeyondReach);
            int holes = hits.Count(h => h.Verdict == MachinabilityVerdict.HoleBeyondMillReach);
            Log.Info($"Raios mínimos: {scanned} face(s) curva(s) medida(s) — {below} abaixo do raio mínimo, " +
                     $"{far} fora de alcance em profundidade, {holes} furo(s) fora de broca também" +
                     (unreadable > 0 ? $", {unreadable} ilegível(is)" : "") + ".");
            foreach (var h in hits.OrderBy(h => h.RadiusMm)) Log.Info("  • " + h.Describe());

            return hits;
        }

        /// <summary>Varre uma família de faces (cilindro ou toro) e acumula as reprovadas. Devolve quantas mediu.</summary>
        private static int Scan(object comBody, int queryType, string kind, bool torus,
            ToolLadder ladder, DrillSet drills, double topZmm, bool hasTop, List<SmallRadiusHit> into, ref int unreadable)
        {
            int measured = 0;
            dynamic faces;
            try { faces = ((dynamic)comBody).Faces[queryType]; }
            catch (Exception ex)
            {
                Log.Warn($"Raios mínimos: consulta de '{kind}' indisponível — {ex.GetBaseException().Message}");
                return 0;
            }

            int n = 0;
            try { n = (int)faces.Count; } catch { }
            for (int i = 1; i <= n; i++) // coleções COM da SE são 1-based
            {
                object face;
                try { face = faces.Item(i); } catch { unreadable++; continue; }

                double radiusMm;
                string err;
                if (!TryRadiusMm(face, torus, out radiusMm, out err))
                {
                    unreadable++;
                    Log.Warn($"Raios mínimos: raio de um(a) {kind} ilegível — {err}");
                    continue;
                }
                measured++;

                // Só curvatura CÔNCAVA limita a fresa (canto interno, furo, raio de fundo). Um raio
                // CONVEXO — quina externa arredondada, pino — é feito pelo flanco da ferramenta, com
                // qualquer diâmetro. Sem este filtro o NEGATIVO de uma cavidade (o eletrodo) saía
                // reprovado em todos os cantos que na cavidade eram internos e nele são externos
                // (medido em 2026-11-11: 84 "raios" reprovados num eletrodo do 15428.100).
                bool? concave = IsConcave(face, torus);
                if (concave == false) continue;

                // Profundidade: do topo do corpo até o ponto mais fundo da face. Sem range,
                // julga só o raio (profundidade 0 nunca reprova por alcance).
                double minZ = 0, maxZ = 0, depth = 0;
                bool hasRange = FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx);
                if (hasRange)
                {
                    minZ = mn[2]; maxZ = mx[2];
                    if (hasTop) depth = Math.Max(0.0, topZmm - minZ);
                }

                bool fullTurn = !torus && IsFullRevolution(face);

                MillingTool tool;
                MachinabilityVerdict verdict = ladder.Classify(radiusMm, depth, out tool);
                if (verdict == MachinabilityVerdict.Millable) continue;

                // Furo fundo NÃO é EDM (achado do 1º run ao vivo, 2026-09-11): um Ø8 × 25 mm saía
                // como "só EDM" porque a fresa mais longa da escada para em 20 mm — e aquilo é um
                // furo trivial (broca DIN 338 de Ø8 tem 75 mm de canal). Quando a face dá a volta
                // completa, quem julga a PROFUNDIDADE é a broca; o raio pequeno demais continua
                // reprovando de qualquer jeito, porque nem broca nem fresa daquele tamanho existem.
                if (fullTurn && verdict == MachinabilityVerdict.BeyondReach)
                {
                    Drill drill;
                    if (drills != null && drills.CanDrill(radiusMm * 2.0, depth, out drill))
                    {
                        Log.Info($"Raios mínimos: furo Ø{radiusMm * 2:0.000} mm a {depth:0.0} mm é fundo demais " +
                                 $"para fresa, mas a {drill.Label} alcança — não é EDM.");
                        continue;
                    }
                    verdict = MachinabilityVerdict.HoleBeyondMillReach;
                }

                var hit = new SmallRadiusHit
                {
                    FaceId = TryFaceId(face),
                    Kind = torus ? kind : (fullTurn ? "furo/pino" : "canto"),
                    IsFullRevolution = fullTurn,
                    RadiusMm = radiusMm,
                    DepthMm = depth,
                    MinZmm = minZ,
                    MaxZmm = maxZ,
                    Verdict = verdict,
                    RadiusWouldAccept = verdict == MachinabilityVerdict.BeyondReach
                        ? ladder.BestFor(radiusMm, 0)
                        : null,
                    Concave = concave,
                    FaceMinMm = hasRange ? mn : null,
                    FaceMaxMm = hasRange ? mx : null
                };
                FillAxis(face, torus, hit);
                into.Add(hit);
            }
            return measured;
        }

        /// <summary>
        /// As faces de cilindro/toro CONVEXAS (material por dentro do raio: quina externa arredondada) com raio
        /// abaixo de <paramref name="belowRadiusMm"/>. Na PEÇA a erodir, são exatamente as que viram raio INTERNO
        /// no eletrodo (o negativo dela): servem para prever, antes de construir, onde o eletrodo não é fresável.
        /// Furo/pino de volta completa fica de fora (na peça, pino convexo não vira canto do eletrodo: vira furo).
        /// SOMENTE LEITURA; nunca lança.
        /// </summary>
        public static IReadOnlyList<SmallRadiusHit> ProbeConvex(object comBody, double belowRadiusMm)
        {
            var hits = new List<SmallRadiusHit>();
            if (comBody == null) return hits;
            foreach (bool torus in new[] { false, true })
            {
                dynamic faces;
                try { faces = ((dynamic)comBody).Faces[torus ? igQueryTorus : igQueryCylinder]; }
                catch (Exception ex) { Log.Warn("Raios convexos: consulta indisponível — " + ex.GetBaseException().Message); continue; }
                int n = 0;
                try { n = (int)faces.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object face;
                    try { face = faces.Item(i); } catch { continue; }
                    if (!TryRadiusMm(face, torus, out double radiusMm, out string _)) continue;
                    if (radiusMm >= belowRadiusMm - 1e-6) continue;
                    if (IsConcave(face, torus) != false) continue;
                    if (!torus && IsFullRevolution(face)) continue;
                    var hit = new SmallRadiusHit
                    {
                        FaceId = TryFaceId(face),
                        Kind = torus ? "raio de canto" : "canto",
                        RadiusMm = radiusMm,
                        Verdict = MachinabilityVerdict.BelowMinimumRadius,
                        Concave = false
                    };
                    // Caixa EXATA: a T2c tira o enchimento do raio pela caixa da face, rente a ela (a GetRange é folgada).
                    if (FaceGeometry.TryGetExactRangeMm(face, out double[] mn, out double[] mx))
                    { hit.MinZmm = mn[2]; hit.MaxZmm = mx[2]; hit.FaceMinMm = mn; hit.FaceMaxMm = mx; }
                    FillAxis(face, torus, hit);
                    hits.Add(hit);
                }
            }
            return hits;
        }

        /// <summary>Eixo, ponto do eixo e raio maior (toro), pelas mesmas leituras do <see cref="IsConcave"/>.</summary>
        private static void FillAxis(object face, bool torus, SmallRadiusHit hit)
        {
            try
            {
                object geometry = (object)((dynamic)face).Geometry;
                if (!FaceGeometry.TryOneArrayOut(geometry, "GetAxisVector", out double[] a, out string _)) return;
                double al = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
                if (al < 1e-12) return;
                if (!FaceGeometry.TryOneArrayOut(geometry, torus ? "GetCenterPoint" : "GetBasePoint", out double[] o, out string _)) return;
                hit.AxisDir = new[] { a[0] / al, a[1] / al, a[2] / al };
                hit.AxisPointMm = new[] { Units.MToMm(o[0]), Units.MToMm(o[1]), Units.MToMm(o[2]) };
                if (torus) hit.MajorRadiusMm = Units.MToMm((double)((dynamic)geometry).MajorRadius);
            }
            catch { }
        }

        /// <summary>
        /// Raio da face em mm, pela PROPRIEDADE da geometria (ver a nota da classe). No toro vale
        /// o raio MENOR: é ele o arredondamento que a fresa tem de caber, não a volta que ele dá.
        /// A API devolve METROS.
        /// </summary>
        /// <summary>
        /// A face curva é CÔNCAVA (material por fora do raio: canto interno, furo)? Compara a normal
        /// para fora do material, no meio da face, com a direção que sai do eixo (cilindro) ou do
        /// centro do tubo (toro). null quando a geometria não deixa decidir — quem chama trata como
        /// côncava, que é o lado conservador.
        /// </summary>
        internal static bool? IsConcave(object comFace, bool torus)
        {
            try
            {
                if (!TryMidPointAndNormal(comFace, out double[] p, out double[] n)) return null;
                object geometry = (object)((dynamic)comFace).Geometry;
                if (!FaceGeometry.TryOneArrayOut(geometry, "GetAxisVector", out double[] a, out string _)) return null;
                double al = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
                if (al < 1e-12) return null;
                a = new[] { a[0] / al, a[1] / al, a[2] / al };

                double[] origin;
                if (!FaceGeometry.TryOneArrayOut(geometry, torus ? "GetCenterPoint" : "GetBasePoint", out origin, out string _)) return null;
                double[] v = { p[0] - origin[0], p[1] - origin[1], p[2] - origin[2] };
                double along = v[0] * a[0] + v[1] * a[1] + v[2] * a[2];
                double[] radial = { v[0] - along * a[0], v[1] - along * a[1], v[2] - along * a[2] };

                double[] outward;
                if (!torus) outward = radial;
                else
                {
                    double rl = Math.Sqrt(radial[0] * radial[0] + radial[1] * radial[1] + radial[2] * radial[2]);
                    if (rl < 1e-12) return null;
                    double major = (double)((dynamic)geometry).MajorRadius;
                    double[] tube = { origin[0] + major * radial[0] / rl, origin[1] + major * radial[1] / rl, origin[2] + major * radial[2] / rl };
                    outward = new[] { p[0] - tube[0], p[1] - tube[1], p[2] - tube[2] };
                }
                double dot = n[0] * outward[0] + n[1] * outward[1] + n[2] * outward[2];
                double ol = Math.Sqrt(outward[0] * outward[0] + outward[1] * outward[1] + outward[2] * outward[2]);
                if (ol < 1e-12) return null;
                return dot < 0;   // a normal aponta PARA o eixo: o material está por fora — côncava
            }
            catch { return null; }
        }

        /// <summary>Ponto (metros) e normal para fora do material no meio do domínio paramétrico da face.</summary>
        private static bool TryMidPointAndNormal(object face, out double[] point, out double[] normal)
        {
            point = null; normal = null;
            object[] range = { new double[2], new double[2] };
            var m2 = new System.Reflection.ParameterModifier(2); m2[0] = true; m2[1] = true;
            face.GetType().InvokeMember("GetParamRange", System.Reflection.BindingFlags.InvokeMethod, null, face, range,
                new[] { m2 }, System.Globalization.CultureInfo.InvariantCulture, null);
            double[] lo = (double[])range[0], hi = (double[])range[1];
            double[] mid = { (lo[0] + hi[0]) / 2.0, (lo[1] + hi[1]) / 2.0 };

            var m3 = new System.Reflection.ParameterModifier(3); m3[1] = true; m3[2] = true;
            object[] pa = { 1, (double[])mid.Clone(), new double[3] };
            face.GetType().InvokeMember("GetPointAtParam", System.Reflection.BindingFlags.InvokeMethod, null, face, pa,
                new[] { m3 }, System.Globalization.CultureInfo.InvariantCulture, null);
            object[] na = { 1, (double[])mid.Clone(), new double[3] };
            face.GetType().InvokeMember("GetNormal", System.Reflection.BindingFlags.InvokeMethod, null, face, na,
                new[] { m3 }, System.Globalization.CultureInfo.InvariantCulture, null);
            point = (double[])pa[2]; normal = (double[])na[2];
            return point != null && normal != null && point.Length >= 3 && normal.Length >= 3;
        }

        private static bool TryRadiusMm(object comFace, bool torus, out double radiusMm, out string error)
        {
            radiusMm = 0; error = null;
            try
            {
                dynamic geometry = ((dynamic)comFace).Geometry;
                double m = torus ? (double)geometry.MinorRadius : (double)geometry.Radius;
                radiusMm = Units.MToMm(m);
                if (radiusMm <= 0) { error = "raio não positivo"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        /// <summary>
        /// A face cilíndrica dá a VOLTA COMPLETA? Se sim é furo (ou pino), não canto de fresa.
        ///
        /// <c>Face.GetParamRange</c> tem a MESMA forma de <c>GetRange</c> — dois
        /// <c>[in,out] SAFEARRAY(double)</c>, confirmada na introspecção ao vivo de 2026-09-11 —
        /// então reusa o executor de <see cref="FaceGeometry.TryTwoArrayOut"/>. Mas o conteúdo
        /// NÃO é comprimento: num cilindro um dos parâmetros é o ÂNGULO, em radianos, e passá-lo
        /// por metros→mm daria 1000× sem estourar nada. Daí a variante crua.
        ///
        /// QUAL dos dois componentes é o angular não está documentado, então não se chuta: dá
        /// volta completa se QUALQUER um deles abrir ≈2π. O outro é o eixo do cilindro, em
        /// metros — cairia em 2π só numa peça de 6,28 m. O vão medido vai para o log para a
        /// convenção ser CONFIRMADA na próxima rodada em vez de assumida.
        /// </summary>
        private static bool IsFullRevolution(object comFace)
        {
            if (!FaceGeometry.TryTwoArrayOut(comFace, "GetParamRange",
                    out double[] min, out double[] max, out string error))
            {
                Log.Warn($"Raios mínimos: GetParamRange indisponível ({error}) — a face não pôde ser " +
                         "classificada como furo; segue julgada como canto (conservador).");
                return false;
            }

            const double TwoPi = 2.0 * Math.PI;
            const double Tol = 1e-4; // radianos
            int n = Math.Min(min.Length, max.Length);
            for (int k = 0; k < n; k++)
            {
                double span = Math.Abs(max[k] - min[k]);
                if (Math.Abs(span - TwoPi) <= Tol)
                {
                    Log.Info($"Raios mínimos: face dá a volta completa (parâmetro {k} abriu {span:0.0000} rad ≈ 2π) — tratada como furo.");
                    return true;
                }
            }
            return false;
        }

        private static int TryFaceId(object comFace)
        {
            try { return (int)((dynamic)comFace).ID; } catch { return 0; }
        }
    }
}
