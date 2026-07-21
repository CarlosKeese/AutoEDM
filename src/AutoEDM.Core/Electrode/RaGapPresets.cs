using System;
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
        /// <summary>
        /// Nome da variável gravada na peça do eletrodo com o Ra detectado (Carlos,
        /// 2026-07-21) — escrita por "Criar eletrodo (manual)" (cor lida na seleção,
        /// ainda na montagem, antes de qualquer cópia) e por "Aplicar GAP" (depois de
        /// escolher o Ra na lista). Lida de volta por "Aplicar GAP" (pré-seleciona a
        /// combinação certa) e por "Duplicar eletrodo" (sabe de qual Ra partir para achar
        /// o PRÓXIMO da tabela). Guardada via <c>Variable.Formula</c> (string), NÃO
        /// <c>Variable.Value</c> — Value é escalado por unidade (a peça pode não ser mm),
        /// Formula é o texto cru que a gente mesmo escreveu, sem conversão nenhuma.
        /// </summary>
        public const string RaVariableName = "AutoEDM_Ra";

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

        /// <summary>A combinação cujo Ra mais se aproxima de <paramref name="ra"/> (tolerância
        /// 0,05 µm — ponto flutuante indo e vindo de uma string gravada na peça). Null se
        /// nenhuma bater (tabela vazia ou Ra fora de qualquer entrada conhecida).</summary>
        public static Choice ClosestTo(double ra, string material = "Cobre")
        {
            Choice best = null; double bestDiff = double.MaxValue;
            foreach (var c in All(material))
            {
                double diff = Math.Abs(c.Ra - ra);
                if (diff < bestDiff) { bestDiff = diff; best = c; }
            }
            return bestDiff <= 0.05 ? best : null;
        }

        /// <summary>Combinação do PRÓXIMO Ra da escada acima de <paramref name="finishRa"/>
        /// (desbaste = Ra maior que o acabamento, confirmado com o Carlos) — usado por
        /// "Duplicar eletrodo". Null se já é o Ra mais grosso da tabela (não há próximo).</summary>
        public static Choice NextCoarser(double finishRa, string material = "Cobre")
        {
            double nextRa = new RaColorMap().RoughingRaFor(finishRa);
            if (Math.Abs(nextRa - finishRa) < 1e-6) return null; // já é o topo da escada
            return ClosestTo(nextRa, material);
        }
    }
}
