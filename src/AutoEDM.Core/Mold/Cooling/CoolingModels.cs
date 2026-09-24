using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// Como termina uma PONTA LIVRE de canal (Carlos, 2026-09-24). Ponta que é cruzamento de duas
    /// linhas nunca recebe terminação — lá o furo passa do cruzamento (sobrefuro) e acaba em ponta.
    /// </summary>
    public enum CoolingTerminal
    {
        /// <summary>Furo cego: termina com o cone de 118° da broca.</summary>
        Blind = 0,
        /// <summary>O furo atravessa a peça (fundo reto, passa um pouco da face).</summary>
        Through = 1,
        /// <summary>Rosca de tubo para engate rápido, coaxial ao canal.</summary>
        Fitting = 2,
        /// <summary>Rosca de tubo para tampão, fechando o furo de passagem.</summary>
        Plug = 3,
    }

    /// <summary>
    /// Uma rosca de tubo da BASE DE FUROS do Solid Edge (<c>Preferences\Holes\*.xlsx</c>, aba
    /// Threaded). As strings são as EXATAS da planilha — a NPT, por exemplo, vem com espaços em
    /// volta (" 1/8-27 NPT      ") e só casa assim. Nada daqui é digitado no código.
    /// </summary>
    public sealed class PipeThread
    {
        public string Standard { get; set; }     // nome do .xlsx: "ISO Metric", "ANSI Inch"...
        public string SubType { get; set; }      // "Straight Pipe Thread" / "Tapered Pipe Thread"
        public string Family { get; set; }       // "G", "R", "NPT"...
        public string Size { get; set; }         // EXATO da planilha
        public double NominalMm { get; set; }
        public double TapDrillMm { get; set; }
        /// <summary>Comprimento de rosca interna da planilha; 0 = a planilha não diz.</summary>
        public double InternalLengthMm { get; set; }

        public bool Tapered => SubType != null && SubType.IndexOf("Tapered", StringComparison.OrdinalIgnoreCase) >= 0;
        public string Label => (Size ?? "").Trim() + (Tapered ? " (cônica)" : "");

        public override string ToString() => Label;
    }

    /// <summary>Uma linha reta do esboço 3D (mm, coordenadas da peça). <see cref="Edge"/> é o COM.</summary>
    public sealed class CoolingLine
    {
        public int Index { get; set; }           // 1, 2, 3... (é o "L1" do relatório)
        public double[] StartMm { get; set; }
        public double[] EndMm { get; set; }
        public object Edge { get; set; }

        public double LengthMm => Vec.Len(Vec.Sub(EndMm, StartMm));
        public string Name => "L" + Index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Uma BOCA de canal na face da peça — onde a terminação (engate/tampão/passante/cega) mora.
    /// É a ponta da linha, se ela chega na face, ou o fim do PROLONGAMENTO até a face, quando a
    /// linha acaba num canto dentro da placa. A chave "L3:F" = linha 3, fim.
    /// </summary>
    public sealed class CoolingEnd
    {
        public string Key { get; set; }
        public int LineIndex { get; set; }
        public bool AtStart { get; set; }
        /// <summary>O ponto NA FACE (já prolongado, se for o caso).</summary>
        public double[] PointMm { get; set; }
        /// <summary>A linha acabava dentro da peça e foi prolongada até aqui (furo de fora + tampão).</summary>
        public bool Extended { get; set; }
        public double ExtensionMm { get; set; }
        /// <summary>O que o AutoEDM escolheu sozinho para esta boca (antes de o usuário mexer).</summary>
        public CoolingTerminal AutoTerminal { get; set; }
        /// <summary>O que vale no plano (automático ou escolhido).</summary>
        public CoolingTerminal Terminal { get; set; }

        public static string KeyOf(int lineIndex, bool atStart) =>
            "L" + lineIndex.ToString(CultureInfo.InvariantCulture) + (atStart ? ":I" : ":F");
    }

    /// <summary>
    /// Até onde vai o material a partir de um ponto, numa direção (medido na peça pelo raio).
    /// <see cref="DistanceMm"/> NaN = o ponto já está na face ou fora dela.
    /// </summary>
    public sealed class CoolingExit
    {
        public double DistanceMm { get; set; } = double.NaN;
        /// <summary>A saída é numa face EXTERNA da peça (na caixa envolvente), não num bolsão.</summary>
        public bool OuterFace { get; set; } = true;
    }

    public sealed class CoolingOptions
    {
        public double DiameterMm { get; set; } = 8.0;
        /// <summary>Rosca das bocas de ENGATE (entrada/saída de água) e de TAMPÃO (furo de fora).</summary>
        public PipeThread FittingThread { get; set; }
        public PipeThread PlugThread { get; set; }
        /// <summary>Ponto a menos disto da face conta como NA face.</summary>
        public double FaceToleranceMm { get; set; } = 0.5;
        /// <summary>Quanto o furo passa do cruzamento. Null = Ø/2 (a broca chega à parede oposta do canal cruzado).</summary>
        public double? OvershootMm { get; set; }
        /// <summary>Quanto o furo passante/roscado passa da face, para romper limpo.</summary>
        public double BreakthroughMm { get; set; } = 0.5;
        public double DrillPointDeg { get; set; } = 118.0;
        /// <summary>Profundidade da rosca quando a planilha não diz.</summary>
        public double DefaultThreadDepthMm { get; set; } = 12.0;
        public double EndToleranceMm { get; set; } = 0.01;

        public double Overshoot => OvershootMm ?? DiameterMm / 2.0;
    }

    /// <summary>Um furo a criar: do ponto de entrada, na direção da outra ponta, com a profundidade dada.</summary>
    public sealed class CoolingHolePlan
    {
        public string Label { get; set; }            // nome do recurso na árvore
        public int EntryLineIndex { get; set; }      // a aresta onde nasce o plano do furo
        public bool EntryAtLineStart { get; set; }
        public double[] EntryMm { get; set; }
        public double[] TowardMm { get; set; }       // um ponto DENTRO do furo, para conferir o lado
        public double[] DirMm { get; set; }          // unitário, da entrada para dentro
        public double DepthMm { get; set; }
        public double DiameterMm { get; set; }
        public bool DrillPoint { get; set; }
        public PipeThread Thread { get; set; }       // != null = furo roscado
        public double ThreadDepthMm { get; set; }
        /// <summary>A entrada foi prolongada até a face (o plano não pode nascer na aresta da linha).</summary>
        public bool Extended { get; set; }
    }

    public sealed class CoolingPlan
    {
        public List<CoolingEnd> FreeEnds { get; } = new List<CoolingEnd>();
        public List<CoolingHolePlan> Holes { get; } = new List<CoolingHolePlan>();
        public List<string> Problems { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>Vetores 3D mínimos (mm).</summary>
    internal static class Vec
    {
        public static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        public static double[] Scale(double[] a, double s) => new[] { a[0] * s, a[1] * s, a[2] * s };
        public static double[] Add(double[] a, double[] b, double s = 1) => new[] { a[0] + b[0] * s, a[1] + b[1] * s, a[2] + b[2] * s };
        public static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        public static double Len(double[] a) => Math.Sqrt(Dot(a, a));
        public static double Dist(double[] a, double[] b) => Len(Sub(a, b));
        public static double[] Unit(double[] a) { double n = Len(a); return n < 1e-12 ? new double[3] : new[] { a[0] / n, a[1] / n, a[2] / n }; }
        public static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]
        };
    }
}
