using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AutoEDM.Wedm
{
    /// <summary>Um nível de corte: as curvas que estão (dentro da tolerância) na mesma altura Z.</summary>
    public sealed class WedmLevel
    {
        /// <summary>Z médio das curvas do nível (mm). Só nomeia o arquivo — as curvas mantêm o Z real delas.</summary>
        public double Z { get; set; }

        /// <summary>Z como vai no nome do arquivo ("12.50").</summary>
        public string Label { get; set; }

        public List<WireCurve> Curves { get; } = new List<WireCurve>();
    }

    /// <summary>
    /// Separa as curvas de perfil por altura Z e dá o nome de cada arquivo (Carlos, 2026-09-14): um
    /// .igs por nível, com o nome da peça + "Z = XX.XX". Curva que não está num plano horizontal
    /// não pertence a nível nenhum e volta à parte, para o botão avisar em vez de exportar torta.
    /// Lógica pura, sem COM.
    /// </summary>
    public static class WedmLevels
    {
        /// <summary>Variação máxima de Z (mm) dentro de UMA curva para ela contar como horizontal.</summary>
        public const double PlanarToleranceMm = 0.001;

        /// <summary>Curvas a até esta distância em Z (mm) do início do nível entram no mesmo nível.</summary>
        public const double LevelToleranceMm = 0.005;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Níveis em ordem crescente de Z; as curvas não horizontais vão para <paramref name="notHorizontal"/>.</summary>
        public static List<WedmLevel> Group(IEnumerable<WireCurve> curves, out List<WireCurve> notHorizontal,
            double planarToleranceMm = PlanarToleranceMm, double levelToleranceMm = LevelToleranceMm)
        {
            notHorizontal = new List<WireCurve>();
            var flat = new List<KeyValuePair<double, WireCurve>>();
            foreach (WireCurve c in curves ?? Enumerable.Empty<WireCurve>())
            {
                c.GetZRange(out double min, out double max);
                if (max - min > planarToleranceMm) notHorizontal.Add(c);
                else flat.Add(new KeyValuePair<double, WireCurve>((min + max) / 2.0, c));
            }

            var levels = new List<WedmLevel>();
            double levelStart = double.NaN, zSum = 0;
            foreach (var zc in flat.OrderBy(x => x.Key))
            {
                if (levels.Count == 0 || zc.Key - levelStart > levelToleranceMm)
                {
                    levels.Add(new WedmLevel());
                    levelStart = zc.Key;
                    zSum = 0;
                }
                WedmLevel level = levels[levels.Count - 1];
                level.Curves.Add(zc.Value);
                zSum += zc.Key;
                level.Z = zSum / level.Curves.Count;
            }

            // Dois níveis que arredondam para o mesmo nome iriam para o mesmo arquivo: viram um só.
            var merged = new List<WedmLevel>();
            foreach (WedmLevel level in levels)
            {
                level.Label = FormatZ(level.Z);
                WedmLevel same = merged.FirstOrDefault(m => m.Label == level.Label);
                if (same == null) { merged.Add(level); continue; }
                int n = same.Curves.Count;
                same.Curves.AddRange(level.Curves);
                same.Z = (same.Z * n + level.Z * level.Curves.Count) / same.Curves.Count;
            }
            return merged;
        }

        /// <summary>Z com 2 casas e ponto decimal; nunca "-0.00".</summary>
        public static string FormatZ(double z)
        {
            double r = Math.Round(z, 2, MidpointRounding.AwayFromZero);
            if (r == 0) r = 0; // -0.0 == 0: troca pelo zero positivo
            return r.ToString("0.00", Inv);
        }

        /// <summary>"PUNCAO 01.par", 12.5 → "PUNCAO 01 Z = 12.50.igs".</summary>
        public static string FileName(string partPathOrName, double z) =>
            $"{Path.GetFileNameWithoutExtension(partPathOrName)} Z = {FormatZ(z)}.igs";

        /// <summary>Máscara dos arquivos de nível desta peça (para achar os de uma exportação anterior).</summary>
        public static string FilePattern(string partPathOrName) =>
            $"{Path.GetFileNameWithoutExtension(partPathOrName)} Z = *.igs";
    }
}
