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
            return Format(plan, build, bodyName, null, null);
        }

        /// <param name="recognition">Com ele, o relatório abre pela identificação (furos, raios,
        /// chanfros, faces inclinadas).</param>
        /// <param name="corrections">Com ele, lista as correções de fronteira planejadas.</param>
        public static string Format(PrismaticPlan plan, PrismaticBuildResult build, string bodyName,
            RecognitionResult recognition, CorrectionPlan corrections)
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

            if (recognition != null)
            {
                sb.Append(SurfaceReport.Identify(recognition));
                sb.AppendLine();
            }

            sb.Append("Níveis Z (mm):");
            foreach (double z in plan.Levels) sb.Append(' ').Append(F2(z));
            sb.AppendLine();
            sb.AppendLine();

            int approx = 0, uncovered = 0;
            var corrected = new System.Collections.Generic.HashSet<int>();
            if (corrections != null) foreach (CorrectionRegion c in corrections.Regions) corrected.Add(c.Surface);
            for (int i = 0; i < plan.Bands.Count; i++)
            {
                SectionBand b = plan.Bands[i];
                int lines = 0, arcs = 0, circles = 0, rough = 0;
                foreach (SectionLoop l in b.Loops)
                    foreach (SectionElement e in l.Elements)
                    {
                        if (!e.Exact) { rough++; if (!corrected.Contains(e.Region)) uncovered++; }
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

            if (corrections != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Correções de fronteira (o que a extrusão reta não faz): {corrections.Regions.Count} feature(s)" +
                              (corrections.AlreadyRight > 0 ? $", {corrections.AlreadyRight} região(ões) em que o prismático já concorda" : "") + ".");
                var grouped = new System.Collections.Generic.SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (CorrectionRegion c in corrections.Regions)
                {
                    // Agrupa pelo tipo de superfície, sem a posição: 24 chanfros iguais são uma linha.
                    string label = c.Label;
                    int at = label.IndexOf(" em (", StringComparison.Ordinal);
                    if (at > 0) label = label.Substring(0, at);
                    string key = (c.Add ? "+ protrusão " : "− recorte ") + (c.Kind == "revolucao" ? "revolvido, " : "extrudado, ") + label;
                    int n;
                    grouped[key] = grouped.TryGetValue(key, out n) ? n + 1 : 1;
                }
                foreach (var kv in grouped) sb.AppendLine($"  {kv.Value,3}× {kv.Key}");
                foreach (string w in corrections.Warnings) sb.AppendLine("  ! " + w);
            }

            if (build != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Construído em '{build.DocumentName}' (peça nova, ordenada, NÃO salva): " +
                              $"{build.BandsBuilt} faixa(s) ok, {build.BandsFailed} com falha" +
                              (build.CorrectionsBuilt + build.CorrectionsFailed > 0
                                  ? $"; {build.CorrectionsBuilt} correção(ões) ok, {build.CorrectionsFailed} com falha." : "."));
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
                if (build.MeshToSolid != null && build.SolidToMesh != null)
                {
                    sb.AppendLine("  desvio malha → sólido ...... " + Dev(build.MeshToSolid));
                    sb.AppendLine("  desvio sólido → malha ...... " + Dev(build.SolidToMesh));
                    double worst = Math.Max(build.MeshToSolid.MaxMm, build.SolidToMesh.MaxMm);
                    // Desvio pequeno NÃO prova geometria exata: trecho copiado da malha (raio que
                    // virou retas) coincide com ela por construção. Na Peça18 (alça da malha 2) as
                    // pontas saíram em 26 retas cada — o veredito tem de cobrar as duas coisas.
                    if (worst > PrismaticBuilder.DeviationLimitMm)
                        sb.AppendLine($"  O SÓLIDO DIVERGE DA MALHA em {F3(worst)} mm no pior ponto — veja onde, acima, e os avisos.");
                    else if (uncovered > 0)
                        sb.AppendLine($"  O sólido encosta na malha (≤ {F2(PrismaticBuilder.DeviationLimitMm)} mm), MAS {uncovered} trecho(s) do contorno " +
                                      "são o POLÍGONO da malha, não geometria exata — raio ou curva que virou retas. Não é exato.");
                    else
                        sb.AppendLine($"  O SÓLIDO É A MALHA, EM GEOMETRIA EXATA: nenhum ponto a mais de {F2(PrismaticBuilder.DeviationLimitMm)} mm nos " +
                                      "dois sentidos e nenhum trecho copiado da malha.");
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

        private static string Dev(DeviationResult d)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "máx {0:0.###} mm, RMS {1:0.####} mm, {2} de {3} ponto(s) acima de {4:0.00} mm{5}",
                d.MaxMm, d.RmsMm, d.Above, d.Points, PrismaticBuilder.DeviationLimitMm,
                d.Above > 0 ? string.Format(CultureInfo.InvariantCulture, " (pior em ({0:0.##}; {1:0.##}; {2:0.##}))",
                    d.WorstAtMm[0], d.WorstAtMm[1], d.WorstAtMm[2]) : "");
        }

        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
    }
}
