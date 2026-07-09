using System;
using System.IO;
using System.Linq;
using System.Text;
using AutoEDM.Electrode;
using AutoEDM.Model;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Grava a folha de dados de eletrodos em .txt (relatório) e .csv (lista de
    /// trabalho, 1 linha por passe/arquivo). Reutilizado pela GUI e pela ribbon do
    /// add-in para não duplicar I/O. Espelha <see cref="BurnReportWriter"/>.
    /// </summary>
    public static class ElectrodeSpecSheetWriter
    {
        /// <summary>Pasta padrão (%LOCALAPPDATA%\AutoEDM\reports).</summary>
        public static string DefaultFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoEDM", "reports");

        /// <summary>
        /// Grava .txt e .csv em <paramref name="folder"/> (ou a pasta padrão) e
        /// devolve o caminho do .txt.
        /// </summary>
        public static string Save(ElectrodeBuildPlan plan, ElectrodeParams p,
            FixationPattern fx = null, string folder = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (p == null) throw new ArgumentNullException(nameof(p));
            folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;
            Directory.CreateDirectory(folder);

            string baseName = Sanitize(plan.AssemblyName ?? "montagem");
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string txt = Path.Combine(folder, $"{baseName}_eletrodos_{stamp}.txt");
            string csv = Path.Combine(folder, $"{baseName}_eletrodos_{stamp}.csv");

            File.WriteAllText(txt, ElectrodeSpecSheet.ToText(plan, p, fx), new UTF8Encoding(true));
            File.WriteAllText(csv, ElectrodeSpecSheet.ToCsv(plan, p, fx), new UTF8Encoding(true));
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
