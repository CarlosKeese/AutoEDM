using System;
using System.Globalization;
using System.Text;

namespace AutoEDM.Reverse
{
    /// <summary>
    /// Texto do plano 2,5D (e da construção, quando houve) para o agente e para o usuário.
    /// PURO, como o <see cref="SurfaceReport"/>: o formato é o que mais muda.
    ///
    /// A ordem é a da conferência: primeiro o que foi medido (níveis, faixas, contornos), depois
    /// os três volumes lado a lado — malha, plano pela malha e EXATO — e por fim os avisos. Quem
    /// lê tem de poder concluir sozinho se o sólido representa a malha.
    /// </summary>
    public static class PrismaticReport
    {
        public static string Format(PrismaticPlan plan, PrismaticBuildResult build, string bodyName)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var sb = new StringBuilder();

            sb.AppendLine("Malha → sólido, rota prismática (faixas Z)" +
                          (string.IsNullOrEmpty(bodyName) ? "" : " — corpo '" + bodyName + "'"));
            if (plan.Bands.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Nenhuma faixa: a malha está vazia ou não tem secção fechada em Z.");
                return sb.ToString();
            }

            sb.Append("Níveis Z (mm):");
            foreach (double z in plan.Levels) sb.Append(' ').Append(F2(z));
            sb.AppendLine();
            sb.AppendLine();

            int approx = 0;
            for (int i = 0; i < plan.Bands.Count; i++)
            {
                SectionBand b = plan.Bands[i];
                int lines = 0, arcs = 0, circles = 0, rough = 0;
                foreach (SectionLoop l in b.Loops)
                    foreach (SectionElement e in l.Elements)
                    {
                        if (!e.Exact) rough++;
                        else if (e.Kind == "reta") lines++;
                        else if (e.Kind == "arco") arcs++;
                        else circles++;
                    }
                approx += rough;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  Faixa {0}: Z {1} → {2}  |  {3} contorno(s): {4} reta(s), {5} arco(s), {6} círculo(s){7}",
                    i + 1, F2(b.Z0), F2(b.Z1), b.Loops.Count, lines, arcs, circles,
                    rough > 0 ? ", " + rough + " trecho(s) APROXIMADO(S) pela malha" : ""));
            }

            sb.AppendLine();
            sb.AppendLine("Volumes (mm³):");
            sb.AppendLine("  malha ...................... " + F3(plan.MeshVolumeMm3));
            sb.AppendLine("  plano, pelo polígono da malha " + F3(plan.PlanVolumeMm3) + Diff(plan.PlanVolumeMm3, plan.MeshVolumeMm3, "da malha"));
            sb.AppendLine("  plano EXATO (arcos reais) .. " + F3(plan.ExactVolumeMm3));
            sb.AppendLine("  (a malha tem furo e raio como polígono INSCRITO: o exato difere dela por isso, e é contra o");
            sb.AppendLine("   EXATO que o sólido se confere)");

            if (build != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Construído em '{build.DocumentName}' (peça nova, ordenada, NÃO salva): " +
                              $"{build.BandsBuilt} faixa(s) ok, {build.BandsFailed} com falha.");
                foreach (string m in build.Messages) sb.AppendLine("  " + m);
                if (!double.IsNaN(build.BodyVolumeMm3))
                {
                    sb.AppendLine("  volume do corpo construído .. " + F3(build.BodyVolumeMm3) +
                                  Diff(build.BodyVolumeMm3, plan.ExactVolumeMm3, "do exato"));
                    if (build.BodyMinMm != null)
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "  caixa: ({0}; {1}; {2}) → ({3}; {4}; {5})",
                            F2(build.BodyMinMm[0]), F2(build.BodyMinMm[1]), F2(build.BodyMinMm[2]),
                            F2(build.BodyMaxMm[0]), F2(build.BodyMaxMm[1]), F2(build.BodyMaxMm[2])));
                }
            }

            sb.AppendLine();
            if (plan.Warnings.Count == 0 && approx == 0)
            {
                sb.AppendLine("Sem avisos: toda faixa é prismática e todo contorno saiu em geometria exata.");
            }
            else
            {
                sb.AppendLine($"Avisos ({plan.Warnings.Count}):");
                foreach (string w in plan.Warnings) sb.AppendLine("  ! " + w);
            }
            return sb.ToString();
        }

        private static string Diff(double value, double reference, string what)
        {
            if (Math.Abs(reference) < 1e-9) return "";
            double d = value - reference;
            return string.Format(CultureInfo.InvariantCulture, "   ({0}{1:0.###} mm³ = {0}{2:0.####}% {3})",
                d >= 0 ? "+" : "−", Math.Abs(d), Math.Abs(100 * d / reference), what);
        }

        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
    }
}
