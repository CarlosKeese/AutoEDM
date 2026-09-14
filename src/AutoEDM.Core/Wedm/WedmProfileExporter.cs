using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoEDM.Diagnostics;

namespace AutoEDM.Wedm
{
    /// <summary>Um arquivo .igs gravado.</summary>
    public sealed class WedmLevelFile
    {
        public string Path { get; set; }
        public string Label { get; set; }
        public int CurveCount { get; set; }
        /// <summary>Já existia um arquivo com esse nome e foi substituído.</summary>
        public bool Replaced { get; set; }
    }

    public sealed class WedmExportResult
    {
        public bool Ok { get; set; }
        /// <summary>Motivo, quando não exportou.</summary>
        public string Message { get; set; }
        public string Folder { get; set; }
        public List<WedmLevelFile> Files { get; } = new List<WedmLevelFile>();
        public WedmCurveReadResult Read { get; set; }
        public List<WireCurve> NotHorizontal { get; set; } = new List<WireCurve>();
        /// <summary>Arquivos de nível desta peça que já estavam na pasta e não correspondem a nenhum nível atual.</summary>
        public List<string> StaleFiles { get; } = new List<string>();
    }

    /// <summary>
    /// Botão "Exportar perfis (IGES)" do grupo WEDM (Carlos, 2026-09-14): lê as curvas de construção
    /// da peça (<see cref="WedmCurveReader"/>), separa por altura Z (<see cref="WedmLevels"/>) e grava
    /// um .igs por nível (<see cref="IgesWriter"/>) na pasta da própria peça, com o nome dela +
    /// "Z = XX.XX". Não altera o modelo; só escreve os arquivos.
    /// </summary>
    public static class WedmProfileExporter
    {
        public static WedmExportResult Export(object partDoc)
        {
            var res = new WedmExportResult();
            string fullName = null;
            try { fullName = (string)((dynamic)partDoc).FullName; } catch { }
            if (string.IsNullOrWhiteSpace(fullName) || !File.Exists(fullName))
            {
                res.Message = "Salve a peça (.par) antes de exportar: os arquivos .igs são gravados na pasta dela, com o nome dela.";
                return res;
            }
            res.Folder = Path.GetDirectoryName(fullName);

            res.Read = WedmCurveReader.Read(partDoc);
            if (res.Read.Curves.Count == 0)
            {
                res.Message = "Nenhuma curva de construção visível foi encontrada na peça." +
                              (res.Read.FeaturesHidden > 0 ? $"\n\n{res.Read.FeaturesHidden} curva(s) oculta(s) ou suprimida(s) ficaram de fora — mostre as que devem ser exportadas." : "") +
                              (res.Read.EdgesFailed > 0 ? $"\n\n{res.Read.EdgesFailed} aresta(s) não puderam ser lidas — veja o log." : "");
                return res;
            }

            List<WedmLevel> levels = WedmLevels.Group(res.Read.Curves, out List<WireCurve> notHorizontal);
            res.NotHorizontal = notHorizontal;
            if (levels.Count == 0)
            {
                res.Message = $"As {notHorizontal.Count} curva(s) encontradas não estão em planos horizontais (Z constante) — nada a exportar por nível.";
                return res;
            }

            DateTime now = DateTime.Now;
            string partFile = Path.GetFileName(fullName);
            foreach (WedmLevel level in levels)
            {
                string name = WedmLevels.FileName(fullName, level.Z);
                string path = Path.Combine(res.Folder, name);
                bool existed = File.Exists(path);
                string iges = IgesWriter.Write(level.Curves, name, $"AutoEDM - perfil WEDM de {partFile}, Z = {level.Label} mm", now);
                File.WriteAllText(path, iges, Encoding.ASCII);

                res.Files.Add(new WedmLevelFile { Path = path, Label = level.Label, CurveCount = level.Curves.Count, Replaced = existed });
                Log.Info($"WEDM: '{name}' — {level.Curves.Count} curva(s) " +
                         $"({string.Join(", ", level.Curves.GroupBy(c => c.Kind).Select(g => $"{g.Count()} {g.Key}"))}){(existed ? ", substituído" : "")}.");
            }

            foreach (string f in Directory.GetFiles(res.Folder, WedmLevels.FilePattern(fullName)))
                if (!res.Files.Any(x => string.Equals(x.Path, f, StringComparison.OrdinalIgnoreCase)))
                    res.StaleFiles.Add(f);

            foreach (WireCurve c in notHorizontal)
            {
                c.GetZRange(out double min, out double max);
                Log.Warn($"WEDM: curva de '{c.Source}' fora de plano horizontal (Z {min:0.000}→{max:0.000}) — não exportada.");
            }

            res.Ok = true;
            return res;
        }
    }
}
