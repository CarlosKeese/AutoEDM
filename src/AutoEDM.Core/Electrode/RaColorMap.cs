using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Mapeia a cor pintada na face -> Ra alvo (definido pela engenharia). Também
    /// fornece a "escada" de Ra usada para derivar o passe de desbaste: o desbaste
    /// usa a faixa de Ra imediatamente acima (ex.: acabamento 0,8 -> desbasta 1,6).
    ///
    /// As cores vêm do padrão da empresa em RGB normalizado (0–1); aqui já estão
    /// convertidas para 0–255. Ajuste as entradas se a engenharia mudar a paleta.
    /// </summary>
    public sealed class RaColorMap
    {
        public sealed class Entry
        {
            public Color Color { get; }
            public double Ra { get; }
            public Entry(Color color, double ra) { Color = color; Ra = ra; }
        }

        private readonly List<Entry> _entries;
        private readonly List<double> _ladder; // Ra distintos, crescente

        /// <summary>Tolerância por canal (0–255) ao casar a cor da face.</summary>
        public int Tolerance { get; set; } = 8;

        public RaColorMap(IEnumerable<Entry> entries = null)
        {
            _entries = (entries ?? DefaultEntries()).ToList();
            _ladder = _entries.Select(e => e.Ra).Distinct().OrderBy(r => r).ToList();
        }

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>Casa a cor da face com uma entrada e devolve o Ra alvo.</summary>
        public bool TryGetRa(Color faceColor, out double ra, out Color matched)
        {
            foreach (var e in _entries)
            {
                if (OleColor.Matches(faceColor, e.Color, Tolerance))
                {
                    ra = e.Ra;
                    matched = e.Color;
                    return true;
                }
            }
            ra = 0;
            matched = default(Color);
            return false;
        }

        /// <summary>
        /// Ra de desbaste = próxima faixa acima do Ra de acabamento. No topo da
        /// escada, mantém o mesmo (não há mais grosso).
        /// </summary>
        public double RoughingRaFor(double finishRa)
        {
            foreach (var r in _ladder)
                if (r > finishRa)
                    return r;
            return finishRa;
        }

        private static Color N(double r, double g, double b) =>
            Color.FromArgb(
                (int)System.Math.Round(r * 255),
                (int)System.Math.Round(g * 255),
                (int)System.Math.Round(b * 255));

        private static IEnumerable<Entry> DefaultEntries() => new[]
        {
            new Entry(N(0.00, 0.50, 1.00), 6.3), // azul
            new Entry(N(1.00, 0.35, 0.39), 3.2), // vermelho
            new Entry(N(0.50, 0.25, 0.00), 1.6), // marrom
            new Entry(N(0.23, 0.50, 0.00), 0.8), // verde
            new Entry(N(1.00, 1.00, 0.50), 0.1), // amarelo
        };
    }
}
