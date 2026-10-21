using System;
using System.Globalization;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Afastamentos ISO 286-2 para os ajustes que um alojamento de O'ring usa (furo H, eixo
    /// h/g/f, graus IT6 a IT8), até Ø 500 mm. Só o que o pior caso do canal precisa: dado o
    /// Ø nominal e a zona ("H7", "g6"…), o afastamento inferior e superior em mm.
    ///
    /// Os números são a transcrição da tabela da norma (µm), por faixa de diâmetro "acima de …
    /// até e inclusive …" — por isso o Ø 6 cai na faixa 3–6, e não na 6–10.
    /// </summary>
    public static class IsoFits
    {
        /// <summary>Limite SUPERIOR de cada faixa de diâmetro (mm), inclusive.</summary>
        private static readonly double[] RangeTop = { 3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500 };

        // Tolerância fundamental (µm) por faixa.
        private static readonly int[] It6 = { 6, 8, 9, 11, 13, 16, 19, 22, 25, 29, 32, 36, 40 };
        private static readonly int[] It7 = { 10, 12, 15, 18, 21, 25, 30, 35, 40, 46, 52, 57, 63 };
        private static readonly int[] It8 = { 14, 18, 22, 27, 33, 39, 46, 54, 63, 72, 81, 89, 97 };

        // Afastamento fundamental SUPERIOR (es, µm) dos eixos g e f.
        private static readonly int[] EsG = { -2, -4, -5, -6, -7, -9, -10, -12, -14, -15, -17, -18, -20 };
        private static readonly int[] EsF = { -6, -10, -13, -16, -20, -25, -30, -36, -43, -50, -56, -62, -68 };

        /// <summary>Ajuste padrão do AutoEDM quando o modelo não informa (escolha do Carlos, 2026-10-21).</summary>
        public const string DefaultFit = "H7/g6";

        /// <summary>
        /// Afastamentos (mm) da zona pedida no Ø nominal. Falso quando a zona não é uma das
        /// cobertas (H/h/g/f nos graus 6, 7 e 8) ou o Ø está fora de 0–500 mm.
        /// </summary>
        public static bool TryDeviations(string zone, double nominalMm, out double lowerMm, out double upperMm)
        {
            lowerMm = upperMm = 0;
            if (string.IsNullOrWhiteSpace(zone) || nominalMm <= 0 || nominalMm > 500) return false;
            zone = zone.Trim();
            char letter = zone[0];
            int grade;
            if (!int.TryParse(zone.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out grade)) return false;

            int i = Array.FindIndex(RangeTop, top => nominalMm <= top + 1e-9);
            int[] it = grade == 6 ? It6 : grade == 7 ? It7 : grade == 8 ? It8 : null;
            if (i < 0 || it == null) return false;
            double tol = it[i] / 1000.0;

            switch (letter)
            {
                case 'H': lowerMm = 0; upperMm = tol; return true;
                case 'h': upperMm = 0; lowerMm = -tol; return true;
                case 'g': upperMm = EsG[i] / 1000.0; lowerMm = upperMm - tol; return true;
                case 'f': upperMm = EsF[i] / 1000.0; lowerMm = upperMm - tol; return true;
                default: return false;
            }
        }

        /// <summary>Separa "H7/g6" em furo e eixo. Falso se o texto não tiver as duas zonas.</summary>
        public static bool TrySplitFit(string fit, out string hole, out string shaft)
        {
            hole = shaft = null;
            if (string.IsNullOrWhiteSpace(fit)) return false;
            var parts = fit.Split('/');
            if (parts.Length != 2) return false;
            hole = parts[0].Trim();
            shaft = parts[1].Trim();
            return hole.Length >= 2 && shaft.Length >= 2;
        }
    }
}
