using System.Collections.Generic;
using System.Drawing;

namespace AutoEDM.Model
{
    /// <summary>Holder / base geometry style for the electrode.</summary>
    public enum HolderType
    {
        Cylindrical,
        Rectangular
    }

    /// <summary>
    /// One erosion pass. The electrode base geometry is built once; each pass is
    /// the same geometry with a different burn-face offset. The offset is derived
    /// from the target Ra via the shop table (see RaOffsetTablePolicy); the
    /// "repasse" (desbaste -> acabamento) is just passes with different Ra.
    /// </summary>
    public sealed class ElectrodePass
    {
        /// <summary>Suffix appended to the electrode name, e.g. "DESB", "ACAB".</summary>
        public string Suffix { get; set; }

        /// <summary>Target surface roughness Ra (µm). Drives the offset via the table.</summary>
        public double Ra { get; set; }

        /// <summary>
        /// Manual offset override (mm). 0 = derive from <see cref="Ra"/> via the
        /// offset policy. Use for the small case-by-case tweaks the operator makes.
        /// </summary>
        public double OffsetOverrideMm { get; set; }

        public ElectrodePass() { }

        public ElectrodePass(string suffix, double ra, double offsetOverrideMm = 0)
        {
            Suffix = suffix;
            Ra = ra;
            OffsetOverrideMm = offsetOverrideMm;
        }
    }

    /// <summary>
    /// All operator-supplied parameters for a single electrode extraction run.
    /// Units: mm (lengths), µm (Ra). The electrode is delivered as a native .par
    /// (NX CAM reads it directly, preserving the burn colors), so no Parasolid/STEP
    /// export is part of the electrode flow.
    /// </summary>
    public sealed class ElectrodeParams
    {
        /// <summary>Color painted on the cavity faces that will be eroded (burn area).</summary>
        public Color BurnColor { get; set; } = Color.FromArgb(255, 0, 0);

        /// <summary>Per-channel tolerance (0-255) when matching face colors.</summary>
        public int ColorTolerance { get; set; } = 8;

        /// <summary>
        /// Passes to generate. If empty, a single pass is built from
        /// <see cref="DefaultRa"/>. Typical setup:
        /// [ ("DESB", 6.3), ("ACAB", 0.8) ] -> offsets 0.30 e 0.05 mm.
        /// </summary>
        public List<ElectrodePass> Passes { get; set; } = new List<ElectrodePass>();

        /// <summary>Fallback Ra (µm) when <see cref="Passes"/> is empty.</summary>
        public double DefaultRa { get; set; } = 3.2;

        /// <summary>
        /// Folga por lado (mm) entre a pegada da queima e a seção do blank. O Carlos
        /// NÃO adiciona sobremetal ao blank: a forma tem que caber DENTRO das medidas
        /// disponíveis -> default 0. (A folga necessária para a FIXAÇÃO — 2×Ø4 + M6
        /// central — é uma regra à parte, ainda a modelar.)
        /// </summary>
        public double BlankMargin { get; set; } = 0.0;

        /// <summary>
        /// Folga vazia (mm) que separa dois detalhes distintos na segmentação por
        /// proximidade. Faces com bounding boxes a até esta distância ficam no mesmo
        /// detalhe. Menor = fragmenta mais; maior = agrupa mais. Calibrável.
        /// </summary>
        public double DetailGapMm { get; set; } = 1.0;

        /// <summary>Holder / base style.</summary>
        public HolderType BaseType { get; set; } = HolderType.Rectangular;

        /// <summary>Holder height below the blank (mm).</summary>
        public double HolderHeight { get; set; } = 15.0;

        /// <summary>
        /// Folga (mm) do FUNDO do bloco/holder ACIMA do zero-máquina (origem da montagem).
        /// Regra do Carlos (Log 51+): a origem/zero-peça do .par toca a SUPERFÍCIE de
        /// queima; o bloco é levantado DENTRO do .par pela distância (superfície→zero-máquina)
        /// + esta folga, de modo que o fundo do holder fique este tanto acima do zero-máquina
        /// (todos os holders no mesmo plano de referência da máquina). O lift interno é
        /// CALCULADO por eletrodo (não é fixo); esta folga é só o "+1 mm de espaço".
        /// </summary>
        public double HolderBaseClearanceMm { get; set; } = 1.0;

        /// <summary>Electrode identifier used for file naming and the drawing table.</summary>
        public string ElectrodeName { get; set; } = "ELD-001";

        /// <summary>
        /// Electrode material (cobre / grafite). Default "Cobre": o Carlos pediu para
        /// preferir cobre por enquanto — só a pasta COBRE tem catálogo de blank
        /// dimensionado (grafite usa templates de base EE_BASE_*, ainda a modelar).
        /// </summary>
        public string Material { get; set; } = "Cobre";

        /// <summary>Optional template .par used as the base for the electrode document.</summary>
        public string TemplatePath { get; set; }

        /// <summary>Output folder for the generated electrode files.</summary>
        public string OutputFolder { get; set; }

        /// <summary>Returns the configured passes, or a single default pass.</summary>
        public IReadOnlyList<ElectrodePass> EffectivePasses()
        {
            if (Passes != null && Passes.Count > 0) return Passes;
            return new[] { new ElectrodePass("", DefaultRa) };
        }
    }
}
