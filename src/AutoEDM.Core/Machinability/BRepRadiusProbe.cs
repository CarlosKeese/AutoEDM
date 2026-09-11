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

                // Profundidade: do topo do corpo até o ponto mais fundo da face. Sem range,
                // julga só o raio (profundidade 0 nunca reprova por alcance).
                double minZ = 0, maxZ = 0, depth = 0;
                if (FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx))
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

                into.Add(new SmallRadiusHit
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
                        : null
                });
            }
            return measured;
        }

        /// <summary>
        /// Raio da face em mm, pela PROPRIEDADE da geometria (ver a nota da classe). No toro vale
        /// o raio MENOR: é ele o arredondamento que a fresa tem de caber, não a volta que ele dá.
        /// A API devolve METROS.
        /// </summary>
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
