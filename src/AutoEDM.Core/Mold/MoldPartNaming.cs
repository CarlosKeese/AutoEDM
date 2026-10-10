using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoEDM.Mold
{
    /// <summary>
    /// De que parte do molde a peça é. O valor é o PRIMEIRO número da série do código:
    /// "XXXXX.100.par" em diante é a parte fixa, ".200" a móvel e ".300" a extração (Carlos,
    /// 2026-09-24). A série do MD-15335 começa no próprio 200 (15335.200.par existe).
    /// </summary>
    public enum MoldSection
    {
        Fixed = 100,
        Moving = 200,
        Ejection = 300,
    }

    /// <summary>
    /// Codificação das peças do molde: "{código do molde}.{NNN}.par". Puro (só texto), para caber
    /// em teste: quem chama junta os nomes de arquivo que existem (pasta + ocorrências da montagem).
    /// </summary>
    public static class MoldPartNaming
    {
        /// <summary>Quantos números cabem numa série (100…199, 200…299, 300…399).</summary>
        public const int SeriesSize = 100;

        // "15335.212.par", "15335.212.dft", "15335.212 - rev1.par"... o que vale é o começo.
        private static readonly Regex CodeRx = new Regex(@"^(?<prefix>\d+)\.(?<num>\d{3})(?!\d)", RegexOptions.CultureInvariant);

        public static string SectionLabel(MoldSection s)
        {
            switch (s)
            {
                case MoldSection.Fixed: return "Parte fixa (.100)";
                case MoldSection.Moving: return "Parte móvel (.200)";
                default: return "Extração (.300)";
            }
        }

        /// <summary>
        /// O código do molde das peças: o prefixo mais frequente entre os arquivos já codificados;
        /// sem nenhum, o do nome da pasta (MD-15335 → "15335"). Null se nenhum dos dois existir.
        /// </summary>
        public static string GuessPrefix(IEnumerable<string> fileNames, string moldCodeFromFolder)
        {
            string best = (fileNames ?? Enumerable.Empty<string>())
                .Select(n => CodeRx.Match(Path.GetFileName(n ?? "")))
                .Where(m => m.Success)
                .GroupBy(m => m.Groups["prefix"].Value)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
            return best ?? (string.IsNullOrWhiteSpace(moldCodeFromFolder) ? null : moldCodeFromFolder.Trim());
        }

        /// <summary>
        /// O próximo número livre da série: o MAIOR já usado + 1; série vazia começa no próprio
        /// início (100/200/300). Conta qualquer arquivo com o código (.par, .asm, .dft…) — um
        /// desenho "15335.212.dft" já ocupa o 212. Devolve -1 se a série estiver cheia (x99).
        /// </summary>
        public static int NextNumber(IEnumerable<string> fileNames, string prefix, MoldSection section)
        {
            int first = (int)section, last = first + SeriesSize - 1;
            int max = -1;
            foreach (string n in fileNames ?? Enumerable.Empty<string>())
            {
                Match m = CodeRx.Match(Path.GetFileName(n ?? ""));
                if (!m.Success || m.Groups["prefix"].Value != prefix) continue;
                int num = int.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture);
                if (num >= first && num <= last && num > max) max = num;
            }
            if (max < 0) return first;
            return max >= last ? -1 : max + 1;
        }

        public static string FileName(string prefix, int number) =>
            string.Format(CultureInfo.InvariantCulture, "{0}.{1:000}.par", prefix, number);
    }

    /// <summary>Eixo da montagem que é a "altura" do molde — cada projetista orienta de um jeito.</summary>
    public enum HeightAxis { X = 0, Y = 1, Z = 2 }

    /// <summary>
    /// Onde nasce a origem da peça nova: centro das faces nos dois eixos de planta, e no eixo de
    /// altura o ponto mais BAIXO ou mais ALTO delas (Carlos, 2026-09-24). Puro.
    /// </summary>
    public static class NewPartPlacement
    {
        /// <summary>Origem (mm, coordenadas da MONTAGEM) a partir da caixa das faces.</summary>
        public static double[] OriginMm(double[] minMm, double[] maxMm, HeightAxis axis, bool atTop)
        {
            var o = new double[3];
            for (int k = 0; k < 3; k++)
                o[k] = k == (int)axis
                    ? (atTop ? maxMm[k] : minMm[k])
                    : (minMm[k] + maxMm[k]) / 2.0;
            return o;
        }
    }
}
