using System.Collections.Generic;
using System.Drawing;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>Offset resolvido para um passe (desbaste ou acabamento).</summary>
    public sealed class PassPlan
    {
        public ElectrodePass Pass { get; set; }
        public double InwardOffsetMm { get; set; }
        public string ElectrodeFileName { get; set; }
    }

    /// <summary>
    /// Uma região de queima (conjunto de faces de uma mesma cor/Ra). Cada região é
    /// candidata a um eletrodo, com seus passes de desbaste/acabamento.
    /// </summary>
    public sealed class RegionPlan
    {
        /// <summary>Índice do detalhe dentro da cor (1-based). Cada detalhe = 1 eletrodo.</summary>
        public int DetailIndex { get; set; }
        public double Ra { get; set; }
        public Color Color { get; set; }
        public int FaceCount { get; set; }

        public bool BoundingBoxKnown { get; set; }
        public BoundingBox BurnBox { get; set; }
        public BlankSpec SelectedBlank { get; set; }

        public List<PassPlan> Passes { get; } = new List<PassPlan>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Resultado não-destrutivo da análise da montagem: tudo que se pretende
    /// construir, calculado a partir de leituras COM confiáveis, antes de gerar
    /// qualquer geometria.
    /// </summary>
    public sealed class ElectrodeBuildPlan
    {
        public string AssemblyName { get; set; }
        public string TargetOccurrenceName { get; set; }
        public List<RegionPlan> Regions { get; } = new List<RegionPlan>();
        public List<string> Warnings { get; } = new List<string>();
    }
}
