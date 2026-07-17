using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Lista PRONTA de combinações Ra→GAP→cor p/ a lista suspensa do "Unir superfícies"
    /// (Carlos, 2026-07-17: escolher Ra numa lista já aplica o GAP e a cor certos — sem
    /// reinventar a tabela). Reusa DIRETO <see cref="RaOffsetTablePolicy"/> (GAP) e
    /// <see cref="RaColorMap"/> (cor) — fonte única de verdade, já confirmadas com o Carlos.
    /// </summary>
    public static class RaGapPresets
    {
        public sealed class Choice
        {
            public double Ra { get; }
            public double GapMm { get; }
            public Color Color { get; }
            public string Label => $"Ra {Ra:0.0} µm  —  GAP {GapMm:0.00} mm";
            public Choice(double ra, double gapMm, Color color) { Ra = ra; GapMm = gapMm; Color = color; }
            public override string ToString() => Label;
        }

        /// <summary>Uma combinação por Ra da escada de cores (<see cref="RaColorMap"/>), do
        /// mais grosso (maior Ra/GAP) para o mais fino, com o GAP tirado de
        /// <see cref="RaOffsetTablePolicy"/> (mesma tabela usada em "Criar eletrodos").</summary>
        public static IReadOnlyList<Choice> All(string material = "Cobre")
        {
            var colorMap = new RaColorMap();
            var offsetPolicy = new RaOffsetTablePolicy();
            return colorMap.Entries
                .OrderByDescending(e => e.Ra)
                .Select(e => new Choice(e.Ra,
                    offsetPolicy.GetInwardOffsetMm(new ElectrodePass("", e.Ra), material),
                    e.Color))
                .ToList();
        }
    }
}
