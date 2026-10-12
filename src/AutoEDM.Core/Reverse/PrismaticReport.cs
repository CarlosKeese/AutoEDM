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
            return Format(plan, build, bodyName, recognition, corrections, null);
        }

        /// <summary>O plano da fachada (<see cref="MeshToSolidPlanner"/>): diz também a direção de
        /// extrusão escolhida, por quê, e a previsão do sólido contra a malha.</summary>
        public static string Format(MeshToSolidPlan whole, PrismaticBuildResult build, string bodyName,
            RecognitionResult recognition)
        {
            if (whole == null) throw new ArgumentNullException(nameof(whole));
            return Format(whole.Plan, build, bodyName, recognition, whole.Corrections, whole);
        }

        /// <summary>
        /// O RESUMO para uma janela de diálogo: direção, faixas, correções, volumes, previsão e,
        /// construído, o resultado e o desvio — o relatório inteiro vai para o log. Na vector_v2-6 o
        /// relatório tem ~1.300 linhas (quase tudo aviso); dentro de um MessageBox a janela passava
        /// da tela e os botões Sim/Não ficavam fora do alcance (Carlos, 2026-10-11: "não funciona").
        /// </summary>
        public static string Summary(MeshToSolidPlan whole, PrismaticBuildResult build, string bodyName)
        {
            if (whole == null) throw new ArgumentNullException(nameof(whole));
            PrismaticPlan plan = whole.Plan;
            var sb = new StringBuilder();
            string axis = (plan.Frame ?? ExtrusionFrame.Z).AxisName;
            sb.AppendLine("Malha → sólido" + (string.IsNullOrEmpty(bodyName) ? "" : " — " + bodyName));
            if (plan.Bands.Count == 0)
            {
                sb.AppendLine("Nenhuma faixa: a malha não fecha secção em direção nenhuma das tentadas.");
                return sb.ToString();
            }
            if (whole.OutOfScope && whole.Evaluation != null && whole.Evaluation.Samples > 0)
                sb.AppendLine("FORA DO ALCANCE da rota 2,5D: mesmo na melhor direção, o sólido diverge da malha em ~" +
                    whole.Evaluation.FarErrorMm3.ToString("0", CultureInfo.InvariantCulture) + " mm³ (" +
                    (100 * whole.Evaluation.FarErrorMm3 / Math.Abs(plan.MeshVolumeMm3)).ToString("0.#", CultureInfo.InvariantCulture) +
                    "% do volume). A peça tem detalhes em várias direções; o sólido NÃO vai se parecer com ela.").AppendLine();
            sb.AppendLine("Direção de extrusão: " + axis + (whole.DirectionNotes.Count > 1 ? " (Z não servia; a de menor erro previsto)" : ""));
            int approx = 0;
            foreach (SectionBand b in plan.Bands) foreach (SectionLoop l in b.Loops) foreach (SectionElement e in l.Elements) if (!e.Exact) approx++;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} faixa(s), {1} correção(ões){2}.",
                plan.Bands.Count, whole.Corrections != null ? whole.Corrections.Regions.Count : 0,
                approx > 0 ? ", " + approx + " trecho(s) de contorno copiados da malha" : ""));
            sb.AppendLine("Volume da malha " + F3(whole.RealMeshVolumeMm3) + " mm³, do plano exato " + F3(plan.ExactVolumeMm3) + " mm³.");
            if (whole.OverlappingShells)
                sb.AppendLine("A malha tem cascas SOBREPOSTAS ou invertidas: a fórmula dá " + F3(plan.MeshVolumeMm3) +
                              " mm³, mas medido por pontos ela tem " + F3(whole.RealMeshVolumeMm3) + " mm³ — é esse o que vale.");
            if (whole.Evaluation != null && whole.Evaluation.Samples > 0)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Erro de forma previsto: {0:0.#} mm³ ({1:0.##}% do volume).",
                    whole.Evaluation.FarErrorMm3, Math.Abs(plan.MeshVolumeMm3) > 0 ? 100 * whole.Evaluation.FarErrorMm3 / Math.Abs(plan.MeshVolumeMm3) : 0));
            if (build != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Construído em '{build.DocumentName}': {build.BandsBuilt} faixa(s) ok, {build.BandsFailed} com falha; " +
                              $"{build.CorrectionsBuilt} correção(ões) ok, {build.CorrectionsFailed} com falha.");
                if (!double.IsNaN(build.BodyVolumeMm3))
                    sb.AppendLine("Volume do corpo construído " + F3(build.BodyVolumeMm3) + " mm³" + Diff(build.BodyVolumeMm3, whole.RealMeshVolumeMm3, "da malha"));
                if (build.MeshToSolid != null && build.SolidToMesh != null)
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Desvio máximo {0:0.###} mm (RMS {1:0.###} mm).",
                        Math.Max(build.MeshToSolid.MaxMm, build.SolidToMesh.MaxMm), Math.Max(build.MeshToSolid.RmsMm, build.SolidToMesh.RmsMm)));
            }
            int warnings = plan.Warnings.Count + (whole.Corrections != null ? whole.Corrections.Warnings.Count : 0);
            if (warnings > 0) sb.AppendLine().AppendLine(warnings + " aviso(s) — o relatório completo está no log do AutoEDM.");
            return sb.ToString();
        }

        private static string Format(PrismaticPlan plan, PrismaticBuildResult build, string bodyName,
            RecognitionResult recognition, CorrectionPlan corrections, MeshToSolidPlan whole)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var sb = new StringBuilder();
            string axis = (plan.Frame ?? ExtrusionFrame.Z).AxisName;

            sb.AppendLine("Malha → sólido, rota prismática (faixas " + axis + ")" +
                          (string.IsNullOrEmpty(bodyName) ? "" : " — corpo '" + bodyName + "'"));
            if (whole != null && whole.OutOfScope && whole.Evaluation != null && whole.Evaluation.Samples > 0 && plan.Bands.Count > 0)
                sb.AppendLine("FORA DO ALCANCE da rota 2,5D: mesmo na melhor direção, o sólido diverge da malha em ~" +
                    whole.Evaluation.FarErrorMm3.ToString("0", CultureInfo.InvariantCulture) + " mm³ (" +
                    (100 * whole.Evaluation.FarErrorMm3 / Math.Abs(plan.MeshVolumeMm3)).ToString("0.#", CultureInfo.InvariantCulture) +
                    "% do volume). A peça tem detalhes em várias direções; o sólido NÃO vai se parecer com ela.");
            if (whole != null)
            {
                sb.AppendLine("Direção de extrusão: " + axis + (whole.Forced ? " (imposta)" :
                              whole.DirectionNotes.Count > 1 ? " — Z não servia; escolhida pelo menor erro previsto:" : ""));
                if (whole.DirectionNotes.Count > 1) foreach (string n in whole.DirectionNotes) sb.AppendLine("  " + n);
            }
            if (plan.Bands.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Nenhuma faixa: a malha está vazia ou não tem secção fechada em " + axis + ".");
                return sb.ToString();
            }

            if (recognition != null)
            {
                sb.Append(SurfaceReport.Identify(recognition));
                sb.AppendLine();
            }

            sb.Append("Níveis " + axis + " (mm):");
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
                    "  Faixa {0}: {8} {1} → {2}  |  {3} contorno(s): {4} reta(s), {5} arco(s), {6} círculo(s){7}",
                    i + 1, F2(b.Z0), F2(b.Z1), b.Loops.Count, lines, arcs, circles,
                    rough > 0 ? ", " + rough + " trecho(s) APROXIMADO(S) pela malha" : "", axis));
            }

            sb.AppendLine();
            sb.AppendLine("Volumes (mm³):");
            sb.AppendLine("  malha ...................... " + F3(plan.MeshVolumeMm3));
            if (whole != null && whole.OverlappingShells)
                sb.AppendLine("  malha, medida por pontos ... " + F3(whole.RealMeshVolumeMm3) +
                              "   (a malha tem cascas SOBREPOSTAS ou invertidas: a fórmula acima erra, este é o volume de verdade)");
            sb.AppendLine("  plano, pelo polígono da malha " + F3(plan.PlanVolumeMm3) + Diff(plan.PlanVolumeMm3, plan.MeshVolumeMm3, "da malha"));
            sb.AppendLine("  plano EXATO (arcos reais) .. " + F3(plan.ExactVolumeMm3));
            sb.AppendLine("  (a malha tem furo e raio como polígono INSCRITO: o exato difere dela por isso, e é contra o");
            sb.AppendLine("   EXATO que o sólido se confere)");

            if (whole != null && whole.Evaluation != null && whole.Evaluation.Samples > 0)
            {
                PlanEvaluation ev = whole.Evaluation;
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Previsão do sólido contra a malha ({0} pontos, antes de construir): erro de forma {1:0.#} mm³ " +
                    "(discordância a mais de {2:0.0#} mm da superfície); na casca da superfície, {3:0.#} mm³ a menos e {4:0.#} mm³ a mais " +
                    "(flecha da tesselação e raios).",
                    ev.Samples, ev.FarErrorMm3, PlanEvaluator.FarMm, ev.MissingMm3, ev.ExtraMm3));
                ExtrusionFrame f = plan.Frame ?? ExtrusionFrame.Z;
                int shown = 0;
                foreach (var c in ev.FarClusters)
                {
                    if (shown++ == 5) break;
                    double[] w = f.ToWorld(c.Item1);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  erro de ~{0:0.#} mm³ perto de ({1:0.#}; {2:0.#}; {3:0.#})",
                        c.Item2, w[0], w[1], w[2]));
                }
            }

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
