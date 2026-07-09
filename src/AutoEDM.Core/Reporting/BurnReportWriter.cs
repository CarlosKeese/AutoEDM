using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Grava o relatório de coordenadas em .txt (tabela) e .csv (planilha) numa
    /// pasta. Reutilizado pela GUI de debug e pela ribbon do add-in para evitar
    /// duplicar I/O.
    /// </summary>
    public static class BurnReportWriter
    {
        /// <summary>Pasta padrão de relatórios (%LOCALAPPDATA%\AutoEDM\reports).</summary>
        public static string DefaultFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoEDM", "reports");

        /// <summary>
        /// Grava .txt e .csv em <paramref name="folder"/> (ou a pasta padrão) e
        /// devolve o caminho do .txt.
        /// </summary>
        public static string Save(BurnCoordinateReport r, string folder = null)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;
            Directory.CreateDirectory(folder);

            string baseName = Sanitize(r.AssemblyName ?? "montagem");
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string txt = Path.Combine(folder, $"{baseName}_coordenadas_{stamp}.txt");
            string csv = Path.Combine(folder, $"{baseName}_coordenadas_{stamp}.csv");

            File.WriteAllText(txt, BurnReportFormatter.ToText(r), new UTF8Encoding(true));
            File.WriteAllText(csv, BurnReportFormatter.ToCsv(r), new UTF8Encoding(true));
            return txt;
        }

        private static string Sanitize(string name)
        {
            string trimmed = Path.GetFileNameWithoutExtension(name);
            string safe = new string(trimmed.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "montagem" : safe;
        }
    }
}
