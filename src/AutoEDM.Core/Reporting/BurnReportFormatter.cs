using System;
using System.Globalization;
using System.Text;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Formata um <see cref="BurnCoordinateReport"/> em texto monoespaçado (para o
    /// log/tela) e em CSV (para importar em planilha ou anexar ao desenho).
    /// </summary>
    public static class BurnReportFormatter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Tabela legível para log/tela.</summary>
        public static string ToText(BurnCoordinateReport r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var sb = new StringBuilder();

            sb.AppendLine("RELATÓRIO DE COORDENADAS DE QUEIMA (zero-máquina = origem da montagem)");
            sb.AppendLine($"Montagem : {r.AssemblyName ?? "—"}");
            sb.AppendLine($"Cavidade : {r.TargetOccurrenceName ?? "—"}");
            if (r.OriginKnown)
                sb.AppendLine($"Origem da cavidade (mm): X={F(r.OriginX)}  Y={F(r.OriginY)}  Z={F(r.OriginZ)}");
            else
                sb.AppendLine("Origem da cavidade: não lida — coordenadas em sistema LOCAL da peça.");
            sb.AppendLine();

            // Cabeçalho da tabela.
            sb.AppendLine("Det  Ra    Faces        X          Y          Z       TamX    TamY    Prof");
            sb.AppendLine("---  ----  -----  ---------  ---------  ---------  ------  ------  ------");
            foreach (var c in r.Coordinates)
            {
                if (c.CoordinateKnown)
                {
                    sb.AppendLine(string.Format(Inv,
                        "{0,3}  {1,4}  {2,5}  {3,9}  {4,9}  {5,9}  {6,6}  {7,6}  {8,6}",
                        c.DetailIndex, c.Ra.ToString("0.0", Inv), c.FaceCount,
                        F(c.X), F(c.Y), F(c.Z), F(c.SizeX), F(c.SizeY), F(c.SizeZ)));
                }
                else
                {
                    sb.AppendLine(string.Format(Inv,
                        "{0,3}  {1,4}  {2,5}  (coordenada não lida)",
                        c.DetailIndex, c.Ra.ToString("0.0", Inv), c.FaceCount));
                }
                foreach (var note in c.Notes)
                    sb.AppendLine($"       ↳ {note}");
            }

            if (r.Coordinates.Count == 0)
                sb.AppendLine("(nenhum detalhe de queima encontrado)");

            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Avisos:");
                foreach (var w in r.Warnings) sb.AppendLine(" • " + w);
            }

            return sb.ToString();
        }

        /// <summary>CSV (ponto e vírgula) para planilha/desenho.</summary>
        public static string ToCsv(BurnCoordinateReport r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var sb = new StringBuilder();
            sb.AppendLine("Detalhe;Ra_um;Faces;X_mm;Y_mm;Z_mm;TamX_mm;TamY_mm;Prof_mm;Cor_RGB;Obs");
            foreach (var c in r.Coordinates)
            {
                sb.AppendLine(string.Join(";",
                    c.DetailIndex.ToString(Inv),
                    c.Ra.ToString("0.0", Inv),
                    c.FaceCount.ToString(Inv),
                    Num(c.X, c.CoordinateKnown), Num(c.Y, c.CoordinateKnown), Num(c.Z, c.CoordinateKnown),
                    Num(c.SizeX, c.CoordinateKnown), Num(c.SizeY, c.CoordinateKnown), Num(c.SizeZ, c.CoordinateKnown),
                    $"{c.Color.R},{c.Color.G},{c.Color.B}",
                    string.Join(" | ", c.Notes)));
            }
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("0.000", Inv);
        private static string Num(double v, bool known) => known ? v.ToString("0.000", Inv) : "";
    }
}
