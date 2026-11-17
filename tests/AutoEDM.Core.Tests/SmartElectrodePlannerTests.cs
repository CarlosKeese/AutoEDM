using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O plano do "Criar Eletrodos" inteligente, sem Solid Edge: unir regiões que cabem numa barra,
    /// reconhecer geometria idêntica (e recusar a espelhada) e posicionar a peça na montagem de erosão.
    /// Números do molde 15428.100 onde fizer sentido.
    /// </summary>
    public class SmartElectrodePlannerTests
    {
        private static SmartFace Face(double x0, double y0, double z0, double x1, double y1, double z1, double ra = 1.6) => new SmartFace
        {
            Kind = BurnKind.ErodedCavity, Ra = ra,
            MinMm = new[] { x0, y0, z0 }, MaxMm = new[] { x1, y1, z1 },
            AreaMm2 = Math.Max(x1 - x0, 0.01) * Math.Max(y1 - y0, 0.01)
        };

        private static SmartRegion Region(string owner, params SmartFace[] faces)
        {
            var r = new SmartRegion { Owner = owner };
            r.Faces.AddRange(faces);
            return r;
        }

        /// <summary>Um "L" assimétrico: não é igual à própria imagem espelhada.</summary>
        private static SmartRegion Ell(string owner, double ox, double oy, Func<double, double, double[]> map)
        {
            SmartFace F(double x0, double y0, double x1, double y1, double z0, double z1)
            {
                double[] a = map(x0, y0), b = map(x1, y1);
                return Face(Math.Min(a[0], b[0]) + ox, Math.Min(a[1], b[1]) + oy, z0, Math.Max(a[0], b[0]) + ox, Math.Max(a[1], b[1]) + oy, z1);
            }
            return Region(owner, F(0, 0, 10, 2, -3, -3), F(0, 2, 2, 7, -3, -1), F(0, 0, 10, 0.1, -3, 0));
        }

        [Fact]
        public void RegionsThatFitOneBar_AreMerged_AndFarOnesAreNot()
        {
            // Duas regiões lado a lado (folga 0,1 mm) e uma a 60 mm: as duas primeiras viram um eletrodo.
            var regions = new[]
            {
                Region("p:1", Face(0, 0, -2, 27, 6, 0)),
                Region("p:1", Face(-4, 0, -5, -0.1, 3.5, 0)),
                Region("p:1", Face(0, 66, -2, 27, 72, 0)),
            };

            List<SmartElectrode> plan = SmartElectrodePlanner.Plan(regions, new StandardBlankLibrary());

            Assert.Equal(2, plan.Count);
            Assert.Contains(plan, e => e.Regions.Count == 2 && Math.Abs(e.Box.SizeX - 31) < 1e-9);
            Assert.All(plan, e => Assert.NotNull(e.Blank));
        }

        [Fact]
        public void Merge_CanBeTurnedOff()
        {
            var regions = new[] { Region("p:1", Face(0, 0, -2, 27, 6, 0)), Region("p:1", Face(-4, 0, -5, -0.1, 3.5, 0)) };
            var plan = SmartElectrodePlanner.Plan(regions, new StandardBlankLibrary(), new SmartPlanOptions { Merge = false });
            Assert.Equal(2, plan.Count);
        }

        [Fact]
        public void RegionsOfDifferentParts_AreNeverMerged()
        {
            var regions = new[] { Region("a:1", Face(0, 0, -2, 5, 5, 0)), Region("b:1", Face(5.5, 0, -2, 10, 5, 0)) };
            Assert.Equal(2, SmartElectrodePlanner.Plan(regions, new StandardBlankLibrary()).Count);
        }

        [Fact]
        public void IdenticalGeometry_BecomesOneElectrodeUsedTwice_WithTheRotation()
        {
            // O mesmo "L" transladado e girado 90° em Z: um eletrodo, duas posições.
            SmartRegion a = Ell("p:1", 0, 0, (x, y) => new[] { x, y });
            SmartRegion b = Ell("p:1", 200, 0, (x, y) => new[] { -y, x });

            List<SmartElectrode> plan = SmartElectrodePlanner.Plan(new[] { a, b }, new StandardBlankLibrary());

            SmartElectrode e = Assert.Single(plan);
            Assert.Equal(2, e.Placements.Count);
            Assert.Equal(0, e.Placements[0].RotZDeg);
            Assert.Equal(90, e.Placements[1].RotZDeg);
        }

        [Fact]
        public void MirroredGeometry_IsADifferentElectrode()
        {
            // Espelhar em Y dá outra peça: nenhum giro em Z leva um "L" na sua imagem espelhada.
            SmartRegion a = Ell("p:1", 0, 0, (x, y) => new[] { x, y });
            SmartRegion b = Ell("p:1", 200, 0, (x, y) => new[] { x, -y });

            Assert.Equal(2, SmartElectrodePlanner.Plan(new[] { a, b }, new StandardBlankLibrary()).Count);
        }

        [Fact]
        public void ElectrodeRa_IsTheFinestAmongItsFaces()
        {
            var r = Region("p:1", Face(0, 0, -2, 5, 5, 0, 3.2), Face(5, 0, -2, 8, 5, 0, 1.6));
            Assert.Equal(1.6, SmartElectrodePlanner.Plan(new[] { r }, new StandardBlankLibrary()).Single().Ra);
        }

        [Fact]
        public void FootprintLargerThanAnyBar_IsReported_NotDropped()
        {
            var r = Region("p:1", Face(0, 0, -2, 300, 300, 0));
            SmartElectrode e = SmartElectrodePlanner.Plan(new[] { r }, new StandardBlankLibrary()).Single();
            Assert.Null(e.Blank);
            Assert.Contains(e.Notes, n => n.Contains("não cabe"));
        }

        [Fact]
        public void LaidDownBar_MustHoldTheWholeElectrode_NotOnlyTheBase()
        {
            // 15428.100 unido: pegada 33,7 × 79,9, queima de 7,2 mm. A RET 38×16 deitada tem 16 mm de espessura,
            // e a ponta (7,2 + 1 de pescoço) + faixa (5) + bloco mínimo (3) pedem 16,2: não serve. A 50×19 serve.
            var r = Region("p:1", Face(0, 0, -7.2, 33.7, 79.9, 0));
            SmartElectrode e = SmartElectrodePlanner.Plan(new[] { r }, new StandardBlankLibrary()).Single();

            Assert.NotNull(e.Blank);
            Assert.True(e.Blank.LaidDown);
            Assert.True(e.Blank.TotalHeightMm >= 16.2, e.Blank.Describe());
        }

        // ------------------------------------------------------------ montagem de erosão

        [Fact]
        public void EdmPlacement_PutsTheClosingFaceAtZero_AndThePartCentredInXY()
        {
            // Postiço com o fechamento em z = 4 (normal +Z), caixa deslocada da origem.
            EdmPlacement p = EdmAssemblyBuilder.PlacementFor(new[] { 0.0, 0.0, 1.0 }, new[] { 10.0, 0.0, 4.0 },
                new[] { -88.0, -98.0, -30.0 }, new[] { 108.0, 98.0, 4.0 });

            Assert.Equal(new[] { 1.0, 0, 0, 0, 1, 0, 0, 0, 1 }, p.R);
            Assert.Equal(-10.0, p.TMm[0], 9);
            Assert.Equal(0.0, p.TMm[1], 9);
            Assert.Equal(-4.0, p.TMm[2], 9);
        }

        [Fact]
        public void EdmPlacement_FlipsAPartWhoseClosingFaceLooksDown()
        {
            // Fechamento em z = -30 com normal -Z: a peça tem de virar para o fechamento olhar +Z.
            EdmPlacement p = EdmAssemblyBuilder.PlacementFor(new[] { 0.0, 0.0, -1.0 }, new[] { 0.0, 0.0, -30.0 },
                new[] { -98.0, -98.0, -30.0 }, new[] { 98.0, 98.0, 4.0 });

            // linha Z da rotação = a normal: um ponto do fechamento vai para Z = 0 e o resto da peça fica abaixo.
            double Z(double x, double y, double z) => p.R[6] * x + p.R[7] * y + p.R[8] * z + p.TMm[2];
            Assert.Equal(0.0, Z(0, 0, -30), 9);
            Assert.Equal(-34.0, Z(0, 0, 4), 9);
            // rotação própria (determinante +1), não espelho
            double det = p.R[0] * (p.R[4] * p.R[8] - p.R[5] * p.R[7]) - p.R[1] * (p.R[3] * p.R[8] - p.R[5] * p.R[6]) + p.R[2] * (p.R[3] * p.R[7] - p.R[4] * p.R[6]);
            Assert.Equal(1.0, det, 9);
        }

        [Fact]
        public void ClosingDirection_IsTheSideWhereTheCavityOpens_NotTheBiggestFace()
        {
            // 15428.100: a face de baixo (34927 mm²) é maior que a de cima (32552 mm²), mas a cavidade abre em cima.
            var candidates = new List<Tuple<double[], double, double>>
            {
                Tuple.Create(new[] { 0.0, 0.0, -1.0 }, 30.0, 34927.0),   // baixo: plano em z = -30
                Tuple.Create(new[] { 0.0, 0.0, 1.0 }, 4.0, 32552.0),     // cima: plano em z = +4
            };
            var cavity = new List<double[]> { new[] { 0.0, 0.0, -1.0 }, new[] { 5.0, 5.0, -3.0 }, new[] { -5.0, 2.0, 2.0 } };

            Assert.Equal(1, EdmAssemblyBuilder.ChooseClosingDirection(candidates, cavity));
            Assert.Equal(0, EdmAssemblyBuilder.ChooseClosingDirection(candidates, new List<double[]>()));   // sem cavidade: maior área
        }

        [Fact]
        public void EdmAssemblyName_IsThePartCodePlusEdm()
        {
            Assert.EndsWith("15428.100_EDM.asm", EdmAssemblyBuilder.AssemblyPathFor(@"C:\molde\15428.100.par"));
        }
    }
}
