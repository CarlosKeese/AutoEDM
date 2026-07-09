using System;
using System.Globalization;
using System.Text;
using AutoEDM.Electrode;
using AutoEDM.Model;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Folha de dados dos eletrodos (spec-sheet): formata um
    /// <see cref="ElectrodeBuildPlan"/> num relatório por DETALHE (= por eletrodo),
    /// com Ra, pegada, blank recomendado, os passes (desbaste/acabamento) com o
    /// offset por Ra e o nome do arquivo .par, além da fixação padrão.
    ///
    /// SOMENTE LEITURA / formatação — reusa o plano não-destrutivo do
    /// <see cref="ElectrodeBuilder.PlanFromAssemblyDocument"/>. É o "irmão" do
    /// relatório de coordenadas (<see cref="BurnReportFormatter"/>), voltado à
    /// preparação/produção de cada eletrodo em vez das coordenadas de queima.
    /// </summary>
    public static class ElectrodeSpecSheet
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Descrição da fixação padrão (M6 central + 2×Ø4 entre centros).</summary>
        private static string FixationText(FixationPattern fx) =>
            $"{fx.CenterThread} central (broca {Mm(fx.CenterTapDrillDiameter)} × prof {Mm(fx.CenterHoleDepth)}) + " +
            $"2×Ø{Mm(fx.DowelDiameter)} × prof {Mm(fx.DowelDepth)} @ {Mm(fx.DowelCenterDistance)} entre centros";

        /// <summary>Relatório legível para log/tela.</summary>
        public static string ToText(ElectrodeBuildPlan plan, ElectrodeParams p, FixationPattern fx = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (p == null) throw new ArgumentNullException(nameof(p));
            fx = fx ?? new FixationPattern();
            var sb = new StringBuilder();

            sb.AppendLine("FOLHA DE DADOS DE ELETRODOS (spec-sheet)");
            sb.AppendLine($"Montagem  : {plan.AssemblyName ?? "—"}");
            sb.AppendLine($"Cavidade  : {plan.TargetOccurrenceName ?? "—"}");
            sb.AppendLine($"Material  : {p.Material ?? "—"}");
            sb.AppendLine($"Margem blank : {Mm(p.BlankMargin)} por lado");
            sb.AppendLine($"Fixação   : {FixationText(fx)}");
            sb.AppendLine($"Eletrodos a gerar : {plan.Regions.Count}");
            sb.AppendLine();

            if (plan.Regions.Count == 0)
                sb.AppendLine("(nenhum detalhe de queima encontrado)");

            foreach (var r in plan.Regions)
            {
                string footprint = r.BoundingBoxKnown
                    ? $"pegada {Mm(r.BurnBox.SizeX)} × {Mm(r.BurnBox.SizeY)} × {Mm(r.BurnBox.SizeZ)} (prof.)"
                    : "pegada não lida";
                sb.AppendLine($"── Detalhe {r.DetailIndex}  |  Ra {Ra(r.Ra)} µm  |  {r.FaceCount} face(s)  |  {footprint}");
                sb.AppendLine($"     Cor de queima : RGB({r.Color.R},{r.Color.G},{r.Color.B})");
                sb.AppendLine($"     Blank         : {(r.SelectedBlank != null ? r.SelectedBlank.Describe() : "— (nenhum do catálogo comporta / bbox não lido)")}");

                sb.AppendLine("     Passes (offset é para DENTRO — eletrodo encolhe):");
                foreach (var pass in r.Passes)
                {
                    string suffix = string.IsNullOrEmpty(pass.Pass.Suffix) ? "ÚNICO" : pass.Pass.Suffix;
                    sb.AppendLine(string.Format(Inv,
                        "        {0,-5}  Ra {1,4} µm   offset {2,5} mm p/ dentro   {3}",
                        suffix, Ra(pass.Pass.Ra), Mm(pass.InwardOffsetMm), pass.ElectrodeFileName));
                }

                foreach (var w in r.Warnings)
                    sb.AppendLine($"     ⚠ {w}");
                sb.AppendLine();
            }

            if (plan.Warnings.Count > 0)
            {
                sb.AppendLine("Avisos:");
                foreach (var w in plan.Warnings) sb.AppendLine(" • " + w);
            }

            return sb.ToString();
        }

        /// <summary>
        /// CSV (ponto e vírgula): UMA linha por PASSE = um arquivo .par a produzir.
        /// Serve como lista de trabalho para a bancada/CAM.
        /// </summary>
        public static string ToCsv(ElectrodeBuildPlan plan, ElectrodeParams p, FixationPattern fx = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (p == null) throw new ArgumentNullException(nameof(p));
            fx = fx ?? new FixationPattern();
            var sb = new StringBuilder();

            sb.AppendLine("Detalhe;Ra_detalhe_um;Faces;PegadaX_mm;PegadaY_mm;Prof_mm;Cor_RGB;" +
                          "Blank;Blank_cod;Margem_mm;Passe;Passe_Ra_um;Offset_mm_p_dentro;Arquivo;Fixacao;Obs");

            foreach (var r in plan.Regions)
            {
                bool bb = r.BoundingBoxKnown;
                string blankName = r.SelectedBlank != null ? r.SelectedBlank.Name : "";
                string blankCode = r.SelectedBlank != null ? r.SelectedBlank.Code : "";
                string obs = string.Join(" | ", r.Warnings);

                foreach (var pass in r.Passes)
                {
                    string suffix = string.IsNullOrEmpty(pass.Pass.Suffix) ? "UNICO" : pass.Pass.Suffix;
                    sb.AppendLine(string.Join(";",
                        r.DetailIndex.ToString(Inv),
                        Ra(r.Ra),
                        r.FaceCount.ToString(Inv),
                        Num(r.BurnBox.SizeX, bb), Num(r.BurnBox.SizeY, bb), Num(r.BurnBox.SizeZ, bb),
                        $"{r.Color.R},{r.Color.G},{r.Color.B}",
                        blankName, blankCode,
                        Mm(p.BlankMargin),
                        suffix, Ra(pass.Pass.Ra), Mm(pass.InwardOffsetMm),
                        pass.ElectrodeFileName,
                        FixationText(fx),
                        obs));
                }
            }
            return sb.ToString();
        }

        private static string Ra(double v) => v.ToString("0.0", Inv);
        private static string Mm(double v) => v.ToString("0.0", Inv);
        private static string Num(double v, bool known) => known ? v.ToString("0.0", Inv) : "";
    }
}
