using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// Lê as ROSCAS DE TUBO da base de furos do Solid Edge — <c>Preferences\Holes\&lt;Norma&gt;.xlsx</c>,
    /// aba "Threaded", linhas com Sub Type "Straight Pipe Thread" / "Tapered Pipe Thread".
    ///
    /// Por que ler a planilha em vez de escrever as strings: o <c>HoleDataCollection.AddEx</c> só
    /// liga a rosca se Standard/SubType/Size casarem EXATAMENTE com a planilha, e ela tem
    /// armadilhas que ninguém acertaria de cabeça — a NPT vem " 1/8-27 NPT      ", com espaços
    /// dos dois lados; a paralela BSP é "G1/4", sem espaço; a cônica BSPT é "R 1/4-19", com espaço.
    /// Lida em 2026-09-24 no SE 2023: ISO Metric traz G, Rp, Rd, R e M cônica; ANSI Inch, NPT e NPSM.
    ///
    /// O .xlsx é um zip: <c>xl/workbook.xml</c> → aba → <c>xl/worksheets/sheetN.xml</c> contra
    /// <c>xl/sharedStrings.xml</c>. Nada de Excel. Só leitura.
    /// </summary>
    public static class HoleDatabase
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// Pasta da base: a que a SE informar (<c>seApplicationGlobalHolesDatabaseFolder</c>, lida por
        /// quem chama), senão a instalação padrão mais nova em Program Files.
        /// </summary>
        public static string FindFolder(string fromApplication = null)
        {
            if (!string.IsNullOrWhiteSpace(fromApplication) && Directory.Exists(fromApplication)) return fromApplication;
            foreach (string pf in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                     }.Where(p => !string.IsNullOrEmpty(p)))
            {
                string siemens = Path.Combine(pf, "Siemens");
                if (!Directory.Exists(siemens)) continue;
                string best = Directory.GetDirectories(siemens, "Solid Edge*")
                    .Select(d => Path.Combine(d, "Preferences", "Holes"))
                    .Where(Directory.Exists)
                    .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>Todas as roscas de tubo das planilhas da pasta (ISO primeiro). Nunca lança.</summary>
        public static List<PipeThread> ReadPipeThreads(string folder)
        {
            var all = new List<PipeThread>();
            if (folder == null || !Directory.Exists(folder)) return all;
            foreach (string xlsx in Directory.GetFiles(folder, "*.xlsx")
                         .OrderBy(f => Path.GetFileNameWithoutExtension(f).StartsWith("ISO", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try { all.AddRange(ReadPipeThreadsFromFile(xlsx)); }
                catch (Exception e) { Log.Warn($"Base de furos: '{Path.GetFileName(xlsx)}' ilegível — " + e.GetBaseException().Message); }
            }
            return all;
        }

        /// <summary>Roscas de tubo de UMA planilha; Standard = nome do arquivo sem extensão.</summary>
        public static List<PipeThread> ReadPipeThreadsFromFile(string xlsxPath)
        {
            string standard = Path.GetFileNameWithoutExtension(xlsxPath);
            var result = new List<PipeThread>();
            using (ZipArchive zip = ZipFile.OpenRead(xlsxPath))
            {
                List<List<string>> rows = ReadSheet(zip, "Threaded");
                if (rows == null || rows.Count < 2) return result;

                List<string> head = rows[0];
                int Col(string startsWith, int fallback)
                {
                    int i = head.FindIndex(h => (h ?? "").Trim().StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));
                    return i >= 0 ? i : fallback;
                }
                int cSub = Col("Sub Type", 1), cFam = Col("ThreadFamily", 2), cSize = Col("Size", 3),
                    cNom = Col("Nominal Diameter", 4), cTap = Col("Tap Drill", 5), cLenInt = Col("Length For Internal", 11);

                foreach (List<string> r in rows.Skip(1))
                {
                    string sub = At(r, cSub);
                    if (sub == null || sub.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string size = At(r, cSize);
                    if (string.IsNullOrWhiteSpace(size)) continue;
                    result.Add(new PipeThread
                    {
                        Standard = standard,
                        SubType = sub,
                        Family = (At(r, cFam) ?? "").Trim(),
                        Size = size,                                   // EXATO — com os espaços
                        NominalMm = Num(At(r, cNom)) * UnitFactor(standard),
                        TapDrillMm = Num(At(r, cTap)) * UnitFactor(standard),
                        InternalLengthMm = Num(At(r, cLenInt)) * UnitFactor(standard),
                    });
                }
            }
            return result;
        }

        /// <summary>A planilha ANSI Inch está em polegadas; as métricas, em mm.</summary>
        private static double UnitFactor(string standard) =>
            standard != null && standard.IndexOf("Inch", StringComparison.OrdinalIgnoreCase) >= 0 ? 25.4 : 1.0;

        private static List<List<string>> ReadSheet(ZipArchive zip, string sheetName)
        {
            var shared = new List<string>();
            ZipArchiveEntry sst = zip.GetEntry("xl/sharedStrings.xml");
            if (sst != null)
                using (Stream s = sst.Open())
                    shared = XDocument.Load(s).Root.Elements(Main + "si")
                        .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))).ToList();

            XDocument wb, rels;
            using (Stream s = zip.GetEntry("xl/workbook.xml").Open()) wb = XDocument.Load(s);
            using (Stream s = zip.GetEntry("xl/_rels/workbook.xml.rels").Open()) rels = XDocument.Load(s);

            XElement sheet = wb.Descendants(Main + "sheet")
                .FirstOrDefault(e => string.Equals((string)e.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null) return null;
            string rid = (string)sheet.Attribute(Rel + "id");
            string target = rels.Root.Elements(PkgRel + "Relationship")
                .Where(e => (string)e.Attribute("Id") == rid).Select(e => (string)e.Attribute("Target")).FirstOrDefault();
            if (target == null) return null;
            target = target.TrimStart('/');
            if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) target = "xl/" + target;

            ZipArchiveEntry entry = zip.GetEntry(target);
            if (entry == null) return null;
            XDocument ws;
            using (Stream s = entry.Open()) ws = XDocument.Load(s);

            var rows = new List<List<string>>();
            foreach (XElement row in ws.Descendants(Main + "row"))
            {
                var cells = new List<string>();
                foreach (XElement c in row.Elements(Main + "c"))
                {
                    int col = ColumnIndex((string)c.Attribute("r"));
                    while (col >= 0 && cells.Count < col) cells.Add(null);   // célula vazia pulada no XML
                    string v = (string)c.Element(Main + "v");
                    string t = (string)c.Attribute("t");
                    if (t == "s" && v != null && int.TryParse(v, out int si) && si < shared.Count) v = shared[si];
                    else if (t == "inlineStr") v = string.Concat(c.Descendants(Main + "t").Select(x => x.Value));
                    cells.Add(v);
                }
                rows.Add(cells);
            }
            return rows;
        }

        /// <summary>"C12" → 2 (0-based). Sem referência → -1 (célula entra na sequência).</summary>
        private static int ColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int col = 0, i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i])) { col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1); i++; }
            return col - 1;
        }

        private static string At(List<string> r, int i) => i >= 0 && i < r.Count ? r[i] : null;

        private static double Num(string s) =>
            double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
    }
}
