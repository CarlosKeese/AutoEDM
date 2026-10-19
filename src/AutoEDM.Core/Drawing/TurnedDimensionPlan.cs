using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Drawing
{
    /// <summary>Cota de posição ao longo do eixo (cota coordenada a partir da origem).</summary>
    public sealed class AxialStation
    {
        public double Z { get; set; }
        public bool IsOrigin { get; set; }
        /// <summary>Face plana que define a estação (null = transição entre cilindro e cone).</summary>
        public RevolvedFace Plane { get; set; }
        public FinishClass Finish => Plane?.Finish;
        public bool External => Plane?.External ?? true;
    }

    public sealed class DiameterSpec
    {
        public RevolvedFace Face { get; set; }
        /// <summary>Posição no eixo onde a cota pega (meio do cilindro, ponta do cone).</summary>
        public double Z { get; set; }
        public double Radius { get; set; }
        public double Diameter => 2 * Radius;
        /// <summary>Quantas faces iguais esta cota representa (Ø27 em dois trechos = 2).</summary>
        public int Count { get; set; } = 1;
        public FinishClass Finish => Face?.Finish;
    }

    public sealed class RadiusSpec
    {
        public RevolvedFace Face { get; set; }
        public int Count { get; set; } = 1;
        public double Radius => Face.MinorRadius;
        public FinishClass Finish => Face?.Finish;
    }

    public sealed class ChamferSpec
    {
        public RevolvedFace Face { get; set; }
        public double Leg => Face.Length;
        /// <summary>Chanfros iguais viram UMA cota "Nx 0,4 X 45°" (a representante é um de fora).</summary>
        public int Count { get; set; } = 1;
    }

    public sealed class AngleSpec
    {
        public RevolvedFace Face { get; set; }
        public double IncludedDeg => 2 * Face.HalfAngleDeg;
    }

    /// <summary>
    /// O QUE cotar numa peça torneada, sem tocar na Solid Edge. A regra reproduz o desenho feito à
    /// mão do 15142.101-CDT (26 cotas, Carlos):
    ///
    /// • ESTAÇÕES no eixo (cota coordenada a partir da ponta de menor Z): toda face plana, mais as
    ///   pontas de cilindro/cone onde o perfil muda sem passar por um plano (Ø27 → canal). Ponta
    ///   que termina em arredondamento pequeno ou chanfro NÃO vira estação — o canto vivo dela é
    ///   o plano vizinho, já cotado. Trecho curto (fundo de canal de 0,2 mm) também não: o canal
    ///   fica definido pelo raio e pelo Ø do fundo.
    /// • DIÂMETROS: um por raio distinto de cilindro (trechos iguais viram uma cota só); cone de
    ///   saída leva Ø nas duas pontas e o ângulo incluído.
    /// • RAIOS: um por arredondamento; tubos iguais com o mesmo raio maior viram "2x".
    /// • CHANFROS: cone de 45° de perna curta.
    ///
    /// TOLERÂNCIA vem da cor: Ø de face colorida pega a coluna furo/eixo da tabela; estação
    /// definida por plano colorido pega a coluna de eixo (ressalto); raio de arredondamento
    /// colorido também. Face sem cor fica com a tolerância geral do carimbo.
    /// </summary>
    public sealed class TurnedDimensionPlan
    {
        public const double Eps = 0.01;
        public double MinSegmentMm { get; set; } = 1.0;
        public double SmallFilletMm { get; set; } = 1.0;
        public double ChamferMaxLegMm { get; set; } = 2.0;
        public double TaperMaxHalfAngleDeg { get; set; } = 15.0;

        public List<AxialStation> Stations { get; } = new List<AxialStation>();
        public List<DiameterSpec> Diameters { get; } = new List<DiameterSpec>();
        public List<RadiusSpec> Radii { get; } = new List<RadiusSpec>();
        public List<ChamferSpec> Chamfers { get; } = new List<ChamferSpec>();
        public List<AngleSpec> Angles { get; } = new List<AngleSpec>();
        /// <summary>Faces que a regra não sabe cotar (o desenho sai sem elas — o usuário completa).</summary>
        public List<RevolvedFace> Skipped { get; } = new List<RevolvedFace>();

        public static TurnedDimensionPlan Build(TurnedProfile profile, Action<TurnedDimensionPlan> configure = null)
        {
            var plan = new TurnedDimensionPlan();
            configure?.Invoke(plan);
            plan.Compute(profile);
            return plan;
        }

        private void Compute(TurnedProfile p)
        {
            List<RevolvedFace> faces = p.Faces;

            // --- estações
            foreach (RevolvedFace f in faces.Where(f => f.Kind == RevolvedKind.Plane))
                AddStation(f.Z0, f);

            foreach (RevolvedFace f in faces.Where(f => IsProfileSegment(f) && f.Length >= MinSegmentMm - 1e-9))
            {
                TryAddEndStation(faces, f, f.Z0, f.R0);
                TryAddEndStation(faces, f, f.Z1, f.R1);
            }

            Stations.Sort((a, b) => a.Z.CompareTo(b.Z));
            if (Stations.Count > 0)
            {
                Stations[0].IsOrigin = true;
            }

            // --- diâmetros
            foreach (RevolvedFace f in faces.Where(f => f.Kind == RevolvedKind.Cylinder).OrderBy(f => f.Z0))
            {
                DiameterSpec same = Diameters.FirstOrDefault(d =>
                    d.Face.Kind == RevolvedKind.Cylinder && Math.Abs(d.Radius - f.R0) < Eps / 2 &&
                    ReferenceEquals(d.Finish, f.Finish));
                if (same != null) { same.Count++; continue; }
                Diameters.Add(new DiameterSpec { Face = f, Z = (f.Z0 + f.Z1) / 2, Radius = f.R0 });
            }

            foreach (RevolvedFace f in faces.Where(f => f.Kind == RevolvedKind.Cone).OrderBy(f => f.Z0))
            {
                if (f.IsChamfer(ChamferMaxLegMm))
                {
                    ChamferSpec same = Chamfers.FirstOrDefault(c => Math.Abs(c.Leg - f.Length) < Eps / 2);
                    if (same == null) { Chamfers.Add(new ChamferSpec { Face = f }); continue; }
                    same.Count++;
                    if (!same.Face.External && f.External) same.Face = f;   // cota no de fora: furo só aparece oculto
                    continue;
                }
                if (f.IsTaper(TaperMaxHalfAngleDeg))
                {
                    Diameters.Add(SharpEnd(faces, f, f.Z0, f.R0));
                    Diameters.Add(SharpEnd(faces, f, f.Z1, f.R1));
                    Angles.Add(new AngleSpec { Face = f });
                    continue;
                }
                Skipped.Add(f);
            }

            // --- raios
            foreach (RevolvedFace f in faces.Where(f => f.Kind == RevolvedKind.Torus).OrderBy(f => f.Z0))
            {
                RadiusSpec same = Radii.FirstOrDefault(r =>
                    Math.Abs(r.Face.MinorRadius - f.MinorRadius) < Eps / 2 &&
                    Math.Abs(r.Face.MajorRadius - f.MajorRadius) < Eps &&
                    ReferenceEquals(r.Finish, f.Finish));
                if (same != null) { same.Count++; continue; }
                Radii.Add(new RadiusSpec { Face = f });
            }

            foreach (RevolvedFace f in faces.Where(f => f.Kind == RevolvedKind.Other)) Skipped.Add(f);
        }

        /// <summary>
        /// Ø da ponta do cone no CANTO VIVO: se a ponta morre num arredondamento pequeno, o cone é
        /// prolongado até a outra ponta do arredondamento (o plano vizinho). É assim que o desenho
        /// à mão cotou — Ø19,48 no plano, não Ø19,467 na tangência do R0,51.
        /// </summary>
        private DiameterSpec SharpEnd(List<RevolvedFace> faces, RevolvedFace cone, double z, double r)
        {
            RevolvedFace fillet = faces.FirstOrDefault(n => !ReferenceEquals(n, cone) &&
                n.IsSmallFillet(SmallFilletMm) && Touches(n, z, r));
            if (fillet != null && cone.Length > 1e-9)
            {
                double zSharp = Math.Abs(fillet.Z0 - z) < Eps ? fillet.Z1 : fillet.Z0;
                double slope = (cone.R1 - cone.R0) / cone.Length;
                return new DiameterSpec { Face = cone, Z = zSharp, Radius = r + slope * (zSharp - z) };
            }
            return new DiameterSpec { Face = cone, Z = z, Radius = r };
        }

        private static bool IsProfileSegment(RevolvedFace f) =>
            f.Kind == RevolvedKind.Cylinder || f.Kind == RevolvedKind.Cone;

        private void TryAddEndStation(List<RevolvedFace> faces, RevolvedFace f, double z, double r)
        {
            if (f.IsChamfer(ChamferMaxLegMm)) return;
            foreach (RevolvedFace n in faces)
            {
                if (ReferenceEquals(n, f) || !Touches(n, z, r)) continue;
                if (n.Kind == RevolvedKind.Plane) return;                  // o plano já é estação
                if (n.IsSmallFillet(SmallFilletMm)) return;                // canto arredondado: vale o plano vizinho
                if (n.IsChamfer(ChamferMaxLegMm)) return;                  // idem para chanfro
            }
            AddStation(z, null);
        }

        private static bool Touches(RevolvedFace n, double z, double r)
        {
            if (n.Kind == RevolvedKind.Plane)
                return Math.Abs(n.Z0 - z) < Eps && r >= Math.Min(n.R0, n.R1) - Eps && r <= Math.Max(n.R0, n.R1) + Eps;
            return (Math.Abs(n.Z0 - z) < Eps && Math.Abs(n.R0 - r) < Eps) ||
                   (Math.Abs(n.Z1 - z) < Eps && Math.Abs(n.R1 - r) < Eps);
        }

        private void AddStation(double z, RevolvedFace plane)
        {
            AxialStation same = Stations.FirstOrDefault(s => Math.Abs(s.Z - z) < Eps);
            if (same != null)
            {
                // Dois planos na mesma altura (ressalto de dois lados): fica o colorido.
                if (same.Plane == null || (same.Plane.Finish == null && plane?.Finish != null)) same.Plane = plane ?? same.Plane;
                return;
            }
            Stations.Add(new AxialStation { Z = z, Plane = plane });
        }
    }
}
