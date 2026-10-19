using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Drawing
{
    /// <summary>Característica geométrica (ISO 1101) que o desenho automático usa.</summary>
    public enum GdtSymbol { Cylindricity, Flatness, Perpendicularity, Parallelism, CircularRunout }

    /// <summary>Um quadro de controle: a face, a característica, o valor e a referência (null = forma).</summary>
    public sealed class GdtFrame
    {
        public RevolvedFace Face { get; set; }
        public GdtSymbol Symbol { get; set; }
        public double Tolerance { get; set; }
        public string Datum { get; set; }

        public override string ToString()
        {
            string t = Tolerance.ToString("0.0##", CultureInfo.GetCultureInfo("pt-BR"));
            return Datum == null ? $"{Name(Symbol)} {t}" : $"{Name(Symbol)} {t} | {Datum}";
        }

        public static string Name(GdtSymbol s)
        {
            switch (s)
            {
                case GdtSymbol.Cylindricity: return "cilindricidade";
                case GdtSymbol.Flatness: return "planeza";
                case GdtSymbol.Perpendicularity: return "perpendicularidade";
                case GdtSymbol.Parallelism: return "paralelismo";
                default: return "batimento circular";
            }
        }
    }

    /// <summary>
    /// GD&amp;T da peça torneada, decidido pelas cores. O desenho à mão do Carlos não tinha GD&amp;T
    /// ("faltava", 2026-10-19); a regra vem da nota da tabela da ferramentaria: "Tolerâncias de
    /// forma e posição = 0,01 mm (sempre que possível)".
    ///
    /// • Só as faces de PRECISÃO recebem quadro — as cores de ajuste/fechamento de faixa
    ///   apertada (10, 11, 15, 16, 17, 19 e as de Ra de erosão). Vedação (±0,1), água (±0,2) e
    ///   folgas não pedem forma nem posição.
    /// • Referência A = eixo do cilindro de precisão de maior área (o ajuste que centra a peça);
    ///   leva cilindricidade 0,01.
    /// • Referência B = face plana de precisão de maior área (o encosto/fechamento); leva
    ///   perpendicularidade 0,01 a A.
    /// • Demais cilindros e cones de precisão: batimento circular 0,01 a A.
    /// • Demais planos de precisão: paralelismo 0,01 a B.
    /// Sem cilindro de precisão não há A (e nada que dependa dele); sem plano de precisão não há B.
    /// </summary>
    public sealed class GdtPlan
    {
        public const double DefaultTolerance = 0.01;
        private static readonly HashSet<string> PrecisionCodes =
            new HashSet<string> { "10", "11", "15", "16", "17", "19", "Ra" };

        public RevolvedFace DatumA { get; private set; }
        public RevolvedFace DatumB { get; private set; }
        public List<GdtFrame> Frames { get; } = new List<GdtFrame>();

        public static bool IsPrecision(RevolvedFace f) =>
            f?.Finish != null && !f.Finish.NeedsReview && PrecisionCodes.Contains(f.Finish.Code);

        public static GdtPlan Build(TurnedProfile profile, double tolerance = DefaultTolerance)
        {
            var g = new GdtPlan();
            List<RevolvedFace> precise = profile.Faces.Where(IsPrecision).ToList();

            g.DatumA = precise.Where(f => f.Kind == RevolvedKind.Cylinder).OrderByDescending(Area).FirstOrDefault();
            g.DatumB = precise.Where(f => f.Kind == RevolvedKind.Plane).OrderByDescending(Area).FirstOrDefault();

            if (g.DatumA != null)
                g.Frames.Add(new GdtFrame { Face = g.DatumA, Symbol = GdtSymbol.Cylindricity, Tolerance = tolerance });
            if (g.DatumB != null)
                g.Frames.Add(g.DatumA != null
                    ? new GdtFrame { Face = g.DatumB, Symbol = GdtSymbol.Perpendicularity, Tolerance = tolerance, Datum = "A" }
                    : new GdtFrame { Face = g.DatumB, Symbol = GdtSymbol.Flatness, Tolerance = tolerance });

            foreach (RevolvedFace f in precise)
            {
                if (ReferenceEquals(f, g.DatumA) || ReferenceEquals(f, g.DatumB)) continue;
                if ((f.Kind == RevolvedKind.Cylinder || f.Kind == RevolvedKind.Cone) && g.DatumA != null)
                {
                    g.Frames.Add(new GdtFrame { Face = f, Symbol = GdtSymbol.CircularRunout, Tolerance = tolerance, Datum = "A" });
                }
                else if (f.Kind == RevolvedKind.Plane && g.DatumB != null)
                {
                    g.Frames.Add(new GdtFrame { Face = f, Symbol = GdtSymbol.Parallelism, Tolerance = tolerance, Datum = "B" });
                }
            }
            return g;
        }

        /// <summary>Área da face (mm²) — só para escolher a referência, então aproximada.</summary>
        public static double Area(RevolvedFace f)
        {
            switch (f.Kind)
            {
                case RevolvedKind.Plane: return Math.PI * Math.Abs(f.R1 * f.R1 - f.R0 * f.R0);
                case RevolvedKind.Cylinder: return 2 * Math.PI * f.R0 * f.Length;
                case RevolvedKind.Cone: return Math.PI * (f.R0 + f.R1) * Math.Sqrt(f.Length * f.Length + (f.R1 - f.R0) * (f.R1 - f.R0));
                default: return 0;
            }
        }
    }
}
