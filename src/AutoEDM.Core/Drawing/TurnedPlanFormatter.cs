using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Drawing
{
    /// <summary>Texto legível do plano de cotas — para o log, a janela de conferência e o agente.</summary>
    public static class TurnedPlanFormatter
    {
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

        public static string Describe(TurnedProfile profile, TurnedDimensionPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Peça torneada: comprimento {N(profile.Length)} mm, Ø máx {N(2 * profile.RMax)} mm, {profile.Faces.Count} faces.");

            double origin = plan.Stations.Count > 0 ? plan.Stations[0].Z : 0;
            sb.AppendLine($"Cotas no eixo (a partir da ponta Z={N(origin)}): {plan.Stations.Count}");
            foreach (AxialStation s in plan.Stations)
                sb.AppendLine($"  {N(s.Z - origin),8}{Tol(s.Finish, s.External)}{Why(s.Plane == null ? "transição" : "plano", s.Finish)}");

            sb.AppendLine($"Diâmetros: {plan.Diameters.Count}");
            foreach (DiameterSpec d in plan.Diameters)
                sb.AppendLine($"  {(d.Count > 1 ? d.Count + "x " : "")}Ø{N(d.Diameter)}{Tol(d.Finish, d.Face.External)}" +
                              Why(d.Face.Kind == RevolvedKind.Cone ? "ponta do cone" : (d.Face.External ? "eixo" : "furo"), d.Finish));

            sb.AppendLine($"Raios: {plan.Radii.Count}");
            foreach (RadiusSpec r in plan.Radii)
                sb.AppendLine($"  {(r.Count > 1 ? r.Count + "x " : "")}R{N(r.Radius)}{Tol(r.Finish, r.Face.External)}{Why("arredondamento", r.Finish)}");

            sb.AppendLine($"Chanfros: {plan.Chamfers.Count}");
            foreach (ChamferSpec c in plan.Chamfers) sb.AppendLine($"  {(c.Count > 1 ? c.Count + "x " : "")}{N(c.Leg)} x 45°");

            sb.AppendLine($"Ângulos de cone: {plan.Angles.Count}");
            foreach (AngleSpec a in plan.Angles) sb.AppendLine($"  {N(a.IncludedDeg)}°{Why("cone de saída", a.Face.Finish)}");

            GdtPlan gdt = GdtPlan.Build(profile);
            sb.AppendLine($"GD&T (posição 0,01 — nota da tabela): {gdt.Frames.Count} quadro(s)");
            if (gdt.DatumA != null) sb.AppendLine($"  referência A = eixo do Ø{N(2 * gdt.DatumA.R0)} ({gdt.DatumA.Finish.ColorName})");
            if (gdt.DatumB != null) sb.AppendLine($"  referência B = face plana em Z={N(gdt.DatumB.Z0)} ({gdt.DatumB.Finish.ColorName})");
            foreach (GdtFrame f in gdt.Frames) sb.AppendLine($"  {Feature(f.Face)}: {f}");

            if (plan.Skipped.Count > 0)
            {
                sb.AppendLine($"Sem regra (ficam para cotar à mão): {plan.Skipped.Count}");
                foreach (RevolvedFace f in plan.Skipped) sb.AppendLine("  " + f);
            }

            var colors = profile.Faces.Where(f => f.Finish != null).GroupBy(f => f.Finish.ToString());
            sb.AppendLine("Cores encontradas:");
            foreach (var g in colors) sb.AppendLine($"  {g.Key}: {g.Count()} face(s)");
            int plain = profile.Faces.Count(f => f.Finish == null);
            if (plain > 0) sb.AppendLine($"  sem cor da tabela: {plain} face(s) — tolerância geral do carimbo");
            return sb.ToString();
        }

        private static string N(double v) => v.ToString("0.###", Br);

        private static string Feature(RevolvedFace f) =>
            f.Kind == RevolvedKind.Plane ? $"face Z={N(f.Z0)}" : $"Ø{N(2 * f.RMax)} ({(f.External ? "eixo" : "furo")})";

        private static string Tol(FinishClass f, bool external)
        {
            if (f == null) return "";
            if (f.NeedsReview) return "  [VERIFICAR]";
            ToleranceBand b = f.For(external);
            return b == null ? "" : "  " + b;
        }

        private static string Why(string what, FinishClass f) =>
            "   (" + what + (f != null ? ", " + f.ColorName : "") + ")";
    }
}
