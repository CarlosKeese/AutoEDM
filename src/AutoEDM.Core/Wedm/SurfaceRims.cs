using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;

namespace AutoEDM.Wedm
{
    /// <summary>Uma EXTREMIDADE da superfície: as arestas horizontais que estão na mesma altura Z.</summary>
    public sealed class SurfaceRim
    {
        /// <summary>Z médio das arestas do rim (mm).</summary>
        public double Z { get; set; }

        /// <summary>Z como vai no nome da curva ("12.50") — mesmo formato do nome do .igs.</summary>
        public string Label { get; set; }

        /// <summary>Rim de TOPO (Z máximo da superfície); false = rim de FUNDO.</summary>
        public bool IsTop { get; set; }

        public List<OpenEdgeSegment> Edges { get; } = new List<OpenEdgeSegment>();
    }

    /// <summary>O que a varredura das arestas de UMA superfície achou.</summary>
    public sealed class SurfaceRimPick
    {
        /// <summary>Rim de fundo e rim de topo (0, 1 ou 2 itens; 1 quando a superfície é plana).</summary>
        public List<SurfaceRim> Rims { get; } = new List<SurfaceRim>();

        /// <summary>A superfície inteira está num plano horizontal — fundo e topo são o mesmo rim.</summary>
        public bool Flat { get; set; }

        /// <summary>Quantos níveis horizontais a superfície tem ao todo (≥ 2 ⇒ houve descarte de intermediários).</summary>
        public int LevelCount { get; set; }

        /// <summary>Arestas horizontais em níveis INTERMEDIÁRIOS — descartadas (Carlos: só Z mín. e máx.).</summary>
        public int EdgesDropped { get; set; }

        /// <summary>Arestas que sobem em Z (paredes) — não são extremidade de nada.</summary>
        public int EdgesNotHorizontal { get; set; }
    }

    /// <summary>
    /// Acha as EXTREMIDADES PARALELAS AO PLANO XY de uma superfície (Carlos, 2026-09-15): das
    /// arestas dela, ficam as horizontais (Z constante) e, dessas, só as do Z MÍNIMO e do Z
    /// MÁXIMO — o rim de fundo e o de topo, que é o que o fio percorre. Aresta horizontal em
    /// altura intermediária (degrau no meio da parede) é descartada de propósito.
    ///
    /// Mesmas tolerâncias do agrupamento por nível da exportação (<see cref="WedmLevels"/>), para
    /// que uma curva criada aqui não seja recusada lá como "fora de plano horizontal".
    /// Lógica PURA, sem COM — testável sem Solid Edge.
    /// </summary>
    public static class SurfaceRims
    {
        public static SurfaceRimPick Pick(IEnumerable<OpenEdgeSegment> edges,
            double planarToleranceMm = WedmLevels.PlanarToleranceMm,
            double levelToleranceMm = WedmLevels.LevelToleranceMm)
        {
            var pick = new SurfaceRimPick();
            var flat = new List<KeyValuePair<double, OpenEdgeSegment>>();
            foreach (OpenEdgeSegment e in edges ?? Enumerable.Empty<OpenEdgeSegment>())
            {
                if (e == null) continue;
                if (e.ZMaxMm - e.ZMinMm > planarToleranceMm) { pick.EdgesNotHorizontal++; continue; }
                flat.Add(new KeyValuePair<double, OpenEdgeSegment>((e.ZMinMm + e.ZMaxMm) / 2.0, e));
            }
            if (flat.Count == 0) return pick;

            // Mesmo agrupamento por proximidade em Z do WedmLevels: arestas a até levelTolerance
            // do início do nível são o mesmo nível.
            var levels = new List<List<KeyValuePair<double, OpenEdgeSegment>>>();
            double levelStart = double.NaN;
            foreach (var ze in flat.OrderBy(x => x.Key))
            {
                if (levels.Count == 0 || ze.Key - levelStart > levelToleranceMm)
                {
                    levels.Add(new List<KeyValuePair<double, OpenEdgeSegment>>());
                    levelStart = ze.Key;
                }
                levels[levels.Count - 1].Add(ze);
            }

            pick.LevelCount = levels.Count;
            pick.Flat = levels.Count == 1;
            AddRim(pick, levels[0], isTop: pick.Flat);
            if (!pick.Flat) AddRim(pick, levels[levels.Count - 1], isTop: true);
            for (int i = 1; i < levels.Count - 1; i++) pick.EdgesDropped += levels[i].Count;
            return pick;
        }

        private static void AddRim(SurfaceRimPick pick, List<KeyValuePair<double, OpenEdgeSegment>> level, bool isTop)
        {
            var rim = new SurfaceRim { IsTop = isTop, Z = level.Average(x => x.Key) };
            rim.Label = WedmLevels.FormatZ(rim.Z);
            foreach (var ze in level) rim.Edges.Add(ze.Value);
            pick.Rims.Add(rim);
        }
    }
}
