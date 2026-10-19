using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Drawing
{
    /// <summary>Forma de uma face de peça torneada, pela geometria analítica do Solid Edge.</summary>
    public enum RevolvedKind { Other = 0, Plane, Cylinder, Cone, Torus }

    /// <summary>
    /// Uma face da peça torneada, já no sistema do EIXO: <see cref="Z0"/>/<see cref="Z1"/> são as
    /// posições ao longo do eixo (mm, Z0 ≤ Z1) e <see cref="R0"/>/<see cref="R1"/> o raio em cada
    /// uma delas. Plano perpendicular ao eixo tem Z0 = Z1 e R0/R1 = raio interno/externo do anel.
    /// </summary>
    public sealed class RevolvedFace
    {
        public int Id { get; set; }
        public RevolvedKind Kind { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public double R0 { get; set; }
        public double R1 { get; set; }
        /// <summary>Raio do tubo (toro = arredondamento).</summary>
        public double MinorRadius { get; set; }
        /// <summary>Toro: raio do círculo dos centros do tubo e a posição dele no eixo (mm).</summary>
        public double MajorRadius { get; set; }
        public double CenterZ { get; set; }
        /// <summary>Meio-ângulo do cone em graus (0° = cilindro, 90° = plano).</summary>
        public double HalfAngleDeg { get; set; }
        /// <summary>Normal apontando para FORA do eixo: face de eixo (true) ou de furo (false).</summary>
        public bool External { get; set; } = true;
        public string StyleName { get; set; }
        public FinishClass Finish { get; set; }

        public double Length => Z1 - Z0;
        public double RMax => Math.Max(R0, R1);

        /// <summary>Chanfro: cone a ~45° com perna curta. Recebe cota de chanfro, não de diâmetro.</summary>
        public bool IsChamfer(double maxLegMm) =>
            Kind == RevolvedKind.Cone && Math.Abs(HalfAngleDeg - 45) < 1.0 && Length <= maxLegMm + 1e-6;

        /// <summary>Cone de pouca conicidade (saída/ajuste): cota de Ø nas duas pontas + ângulo.</summary>
        public bool IsTaper(double maxHalfAngleDeg) =>
            Kind == RevolvedKind.Cone && HalfAngleDeg > 1e-3 && HalfAngleDeg <= maxHalfAngleDeg;

        /// <summary>Arredondamento pequeno de canto: recebe cota de raio, e as pontas dele não viram estação.</summary>
        public bool IsSmallFillet(double maxRadiusMm) =>
            Kind == RevolvedKind.Torus && MinorRadius <= maxRadiusMm + 1e-6;

        public override string ToString() =>
            $"{Kind} id={Id} z=[{Z0:0.###};{Z1:0.###}] r=[{R0:0.###};{R1:0.###}]" +
            (Finish != null ? " " + Finish : "");
    }

    /// <summary>A peça torneada inteira: faces no sistema do eixo, ordenáveis por Z.</summary>
    public sealed class TurnedProfile
    {
        public List<RevolvedFace> Faces { get; } = new List<RevolvedFace>();
        /// <summary>Faces que não são de revolução em torno do eixo (furo transversal, plano lateral...).</summary>
        public List<string> Rejected { get; } = new List<string>();
        /// <summary>Eixo da peça no sistema do modelo (unitário) e um ponto dele, em mm.</summary>
        public double[] AxisDir { get; set; } = { 0, 0, 1 };
        public double[] AxisPoint { get; set; } = { 0, 0, 0 };

        public double ZMin => Faces.Count == 0 ? 0 : Faces.Min(f => f.Z0);
        public double ZMax => Faces.Count == 0 ? 0 : Faces.Max(f => f.Z1);
        public double RMax => Faces.Count == 0 ? 0 : Faces.Max(f => f.RMax);
        public double Length => ZMax - ZMin;
        public bool IsTurned => Faces.Count > 0 && Rejected.Count == 0;
    }
}
