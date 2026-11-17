using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>O que se mediu de um furo da fixação no corpo pronto (mm, mm²).</summary>
    public sealed class HoleMeasure
    {
        public double ConeAreaMm2;
        public int Cones;
        /// <summary>Faces de parede agrupadas por raio: (área, raio, zMin, zMax).</summary>
        public readonly List<double[]> Walls = new List<double[]>();
    }

    /// <summary>
    /// T2b (resposta do Carlos, 2026-11-12): o furo da fixação pode descer da faixa para dentro do pescoço e da ponta,
    /// mas não pode ESTOURAR o cobre. Lógica pura: um furo está inteiro quando a face cônica da ponta da broca tem a
    /// área do cone teórico e cada parede cilíndrica tem a área de um cilindro cheio entre o seu Z mínimo e máximo
    /// (uma janela na parede, ou a ponta saindo por baixo, tira área).
    /// </summary>
    public static class HoleIntegrity
    {
        /// <summary>Área lateral do cone da ponta da broca: π r² / sen(α), α = metade do ângulo da ponta.</summary>
        public static double ConeAreaMm2(double radiusMm, double tipAngleDeg) =>
            Math.PI * radiusMm * radiusMm / Math.Sin(tipAngleDeg / 2.0 * Math.PI / 180.0);

        /// <summary>Inteiro: cone completo e paredes cheias, com <paramref name="tol"/> de folga relativa.</summary>
        public static bool IsIntact(HoleMeasure m, double radiusMm, double tipAngleDeg, out string why, double tol = 0.005)
        {
            double cone = ConeAreaMm2(radiusMm, tipAngleDeg);
            if (m.Cones == 0) { why = "a ponta da broca saiu do cobre (sem face cônica)"; return false; }
            if (m.ConeAreaMm2 < cone * (1 - tol))
            {
                why = string.Format(CultureInfo.InvariantCulture, "ponta da broca incompleta ({0:0.##} de {1:0.##} mm²)", m.ConeAreaMm2, cone);
                return false;
            }
            foreach (double[] w in m.Walls)
            {
                double full = 2 * Math.PI * w[1] * (w[3] - w[2]);
                if (full > 1e-6 && w[0] < full * (1 - tol))
                {
                    why = string.Format(CultureInfo.InvariantCulture, "parede Ø{0:0.##} cortada ({1:0.##} de {2:0.##} mm²)", 2 * w[1], w[0], full);
                    return false;
                }
            }
            why = null;
            return true;
        }

        /// <summary>A próxima profundidade a tentar, encurtando de <paramref name="stepMm"/>; null abaixo do mínimo.</summary>
        public static double? Shorten(double depthMm, double stepMm, double minMm) =>
            depthMm - stepMm >= minMm - 1e-9 ? depthMm - stepMm : (double?)null;
    }

    /// <summary>
    /// Confere, no eletrodo pronto (síncrono), cada furo da fixação; se estourou, encurta pela <c>Hole.Depth</c> (escrita
    /// aceita em síncrono: medido em 2026-11-13, o volume variou π r² × Δ) até ficar inteiro; abaixo do mínimo, apaga o
    /// furo. Devolve uma linha por furo para o relatório. Nunca lança.
    /// </summary>
    public static class FixationHoleGuard
    {
        private const int igQueryCone = 7, igQueryCylinder = 10;

        /// <summary>Mínimos (decisão do Carlos a confirmar): o M6 precisa da rosca inteira; o pino, 1 × Ø.</summary>
        public static double MinCenterDepthMm(FixationPattern fix) => fix.CenterThreadDepth + 0.5;
        public static double MinDowelDepthMm(FixationPattern fix) => fix.DowelDiameter;

        public static List<string> Ensure(dynamic app, FixationPattern fix, double blockXmm, double blockYmm, double centerXmm, double centerYmm,
            double stepMm = 0.5)
        {
            var lines = new List<string>();
            try
            {
                double[][] d = BlankModeler.DowelOffsets(blockXmm, blockYmm, fix, out string _);
                var specs = new[]
                {
                    new Spec { Label = "M6", X = centerXmm, Y = centerYmm, R = fix.CenterTapDrillDiameter / 2, Tip = fix.CenterBottomAngle, Depth = fix.CenterHoleDepth, Min = MinCenterDepthMm(fix), Center = true },
                    new Spec { Label = "Ø4 #1", X = centerXmm + d[0][0], Y = centerYmm + d[0][1], R = fix.DowelDiameter / 2, Tip = fix.DowelBottomAngle, Depth = fix.DowelDepth, Min = MinDowelDepthMm(fix) },
                    new Spec { Label = "Ø4 #2", X = centerXmm + d[1][0], Y = centerYmm + d[1][1], R = fix.DowelDiameter / 2, Tip = fix.DowelBottomAngle, Depth = fix.DowelDepth, Min = MinDowelDepthMm(fix) }
                };

                // Primeiro mede todos; os pinos são iguais (um grupo): encurta-se os dois juntos.
                foreach (Spec s in specs) s.Intact = Measure(app, s, out s.Why);
                if (specs.All(s => s.Intact))
                {
                    lines.Add("M6 e 2×Ø4 inteiros (cone da ponta e parede conferidos).");
                    return lines;
                }
                Fix(app, specs.Where(s => s.Center).ToArray(), stepMm, fix.DowelDiameter, lines);
                Fix(app, specs.Where(s => !s.Center).ToArray(), stepMm, fix.DowelDiameter, lines);
            }
            catch (Exception ex) { lines.Add("conferência dos furos falhou: " + ex.GetBaseException().Message); }
            return lines;
        }

        private sealed class Spec
        {
            public string Label; public double X, Y, R, Tip, Depth, Min; public bool Center, Intact; public string Why;
        }

        /// <summary>Encurta (todos juntos) os furos do grupo que estourou; devolve no relatório a profundidade final.</summary>
        private static void Fix(dynamic app, Spec[] group, double stepMm, double dowelDiaMm, List<string> lines)
        {
            if (group.All(s => s.Intact)) { lines.Add($"{string.Join(" e ", group.Select(s => s.Label))}: inteiro(s)."); return; }
            string first = string.Join("; ", group.Where(s => !s.Intact).Select(s => s.Label + ": " + s.Why));
            List<dynamic> feats = HoleFeatures(app, group[0], dowelDiaMm);
            if (feats.Count == 0) { lines.Add($"{first} — e não achei a feature do furo para encurtar."); return; }

            double depth = group[0].Depth;
            while (true)
            {
                double? next = HoleIntegrity.Shorten(depth, stepMm, group[0].Min);
                if (next == null)
                {
                    foreach (dynamic h in feats) { try { h.Delete(); } catch (Exception ex) { Log.Warn("Furo: apagar falhou — " + ex.GetBaseException().Message); } }
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} ESTOURAVA ({1}) e nem com {2:0.#} mm (mínimo) ficava inteiro: furo retirado.", group[0].Label, first, group[0].Min));
                    Log.Warn("Furo " + lines[lines.Count - 1]);
                    return;
                }
                depth = next.Value;
                foreach (dynamic h in feats) h.Depth = Units.MmToM(depth);
                bool ok = true;
                foreach (Spec s in group) { s.Intact = Measure(app, s, out s.Why); ok &= s.Intact; }
                if (ok)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} ESTOURAVA com {1:0.#} mm ({2}): encurtado para {3:0.#} mm, inteiro.", string.Join(" e ", group.Select(s => s.Label)), group[0].Depth, first, depth));
                    Log.Info("Furo " + lines[lines.Count - 1]);
                    return;
                }
            }
        }

        /// <summary>
        /// As features de furo de um grupo, pelo diâmetro do <c>HoleData</c> (o M6 tem broca Ø5, os pinos Ø4). Em
        /// síncrono <c>Hole.Faces</c> dá E_FAIL (medido), então a posição não serve para separar: os pinos andam juntos.
        /// </summary>
        private static List<dynamic> HoleFeatures(dynamic app, Spec s, double dowelDiaMm)
        {
            var list = new List<dynamic>();
            dynamic holes = app.ActiveDocument.Models.Item(1).Holes;
            for (int i = 1; i <= (int)holes.Count; i++)
            {
                dynamic h = holes.Item(i);
                double dia = double.NaN;
                try { dia = Units.MToMm((double)h.HoleData.HoleDiameter); } catch { }
                // O M6 roscado pode declarar Ø6 (nominal) ou Ø5 (broca): o que não é pino é dele.
                bool dowel = !double.IsNaN(dia) && Math.Abs(dia - dowelDiaMm) < 0.05;
                if (s.Center ? !dowel : dowel) list.Add(h);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [furo] feature {0}: Ø{1:0.##} profundidade {2:0.##} mm", i, dia,
                    Units.MToMm((double)h.Depth)));
            }
            return list;
        }

        /// <summary>Mede o furo no corpo do documento ativo e diz se está inteiro.</summary>
        private static bool Measure(dynamic app, Spec s, out string why)
        {
            object body = (object)app.ActiveDocument.Models.Item(1).Body;
            var m = new HoleMeasure();
            dynamic cones = ((dynamic)body).Faces(igQueryCone);
            for (int i = 1; i <= (int)cones.Count; i++)
            {
                object f = cones.Item(i);
                if (!OnAxis(f, s, false)) continue;
                double half = (double)((dynamic)f).Geometry.HalfAngle * 180.0 / Math.PI;
                if (Math.Abs(half - s.Tip / 2) > 0.5) continue;   // o chanfro de 45° fica de fora
                m.Cones++;
                m.ConeAreaMm2 += (double)((dynamic)f).Area * 1e6;
            }
            var walls = new Dictionary<int, double[]>();
            dynamic cyl = ((dynamic)body).Faces(igQueryCylinder);
            for (int i = 1; i <= (int)cyl.Count; i++)
            {
                object f = cyl.Item(i);
                double r = Units.MToMm((double)((dynamic)f).Geometry.Radius);
                if (r > s.R + 1.05 || !OnAxis(f, s, true)) continue;
                if (!FaceGeometry.TryGetExactRangeMm(f, out double[] mn, out double[] mx)) continue;
                int key = (int)Math.Round(r * 1000);
                if (!walls.TryGetValue(key, out double[] w)) walls[key] = w = new[] { 0.0, r, double.MaxValue, double.MinValue };
                w[0] += (double)((dynamic)f).Area * 1e6; w[2] = Math.Min(w[2], mn[2]); w[3] = Math.Max(w[3], mx[2]);
            }
            m.Walls.AddRange(walls.Values);
            bool ok = HoleIntegrity.IsIntact(m, s.R, s.Tip, out why);
            Log.Info(string.Format(CultureInfo.InvariantCulture, "  [furo] {0} em ({1:0.##}, {2:0.##}): cone {3} face(s) {4:0.###} mm² (teórico {5:0.###}); paredes {6} → {7}",
                s.Label, s.X, s.Y, m.Cones, m.ConeAreaMm2, HoleIntegrity.ConeAreaMm2(s.R, s.Tip),
                string.Join(", ", m.Walls.Select(w => string.Format(CultureInfo.InvariantCulture, "Ø{0:0.##} {1:0.##}/{2:0.##}", 2 * w[1], w[0], 2 * Math.PI * w[1] * (w[3] - w[2])))),
                ok ? "inteiro" : "ESTOUROU: " + why));
            return ok;
        }

        /// <summary>A face (cone ou cilindro) tem eixo vertical passando pelo centro do furo.</summary>
        private static bool OnAxis(object face, Spec s, bool cylinder)
        {
            try
            {
                object geo = (object)((dynamic)face).Geometry;
                if (!FaceGeometry.TryOneArrayOut(geo, "GetAxisVector", out double[] a, out _)) return false;
                double al = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
                if (al < 1e-12 || Math.Abs(a[2]) / al < 0.9999) return false;
                if (!FaceGeometry.TryOneArrayOut(geo, "GetBasePoint", out double[] p, out _)) return false;
                return Math.Abs(Units.MToMm(p[0]) - s.X) < 0.02 && Math.Abs(Units.MToMm(p[1]) - s.Y) < 0.02;
            }
            catch { return false; }
        }
    }
}
