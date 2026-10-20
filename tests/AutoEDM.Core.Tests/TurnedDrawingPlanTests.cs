using System.Drawing;
using System.Linq;
using AutoEDM.Drawing;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// "Desenho 2D" da peça torneada. O caso de referência é o 15142.101-CDT, lido face a face da
    /// Solid Edge em 2026-10-19 (z no eixo a partir do plano de Z=-32,907 do modelo), e o desenho
    /// que o Carlos fez à mão dele: 8 cotas no eixo, os Ø, 2x R4,2 etc. Se a regra mudar e deixar
    /// de reproduzir o desenho à mão, estes testes dizem o quê.
    /// </summary>
    public class TurnedDrawingPlanTests
    {
        private static readonly FinishCatalog Catalog = new FinishCatalog();

        private static RevolvedFace Plane(int id, double z, double r0, double r1, string style = null) =>
            new RevolvedFace { Id = id, Kind = RevolvedKind.Plane, Z0 = z, Z1 = z, R0 = r0, R1 = r1, StyleName = style, Finish = Catalog.ResolveByStyleName(style) };

        private static RevolvedFace Cyl(int id, double z0, double z1, double r, string style = null) =>
            new RevolvedFace { Id = id, Kind = RevolvedKind.Cylinder, Z0 = z0, Z1 = z1, R0 = r, R1 = r, StyleName = style, Finish = Catalog.ResolveByStyleName(style) };

        private static RevolvedFace Cone(int id, double z0, double z1, double r0, double r1, double half, string style = null) =>
            new RevolvedFace { Id = id, Kind = RevolvedKind.Cone, Z0 = z0, Z1 = z1, R0 = r0, R1 = r1, HalfAngleDeg = half, StyleName = style, Finish = Catalog.ResolveByStyleName(style) };

        private static RevolvedFace Torus(int id, double z0, double z1, double r0, double r1, double minor, double major, double cz, string style = null) =>
            new RevolvedFace { Id = id, Kind = RevolvedKind.Torus, Z0 = z0, Z1 = z1, R0 = r0, R1 = r1, MinorRadius = minor, MajorRadius = major, CenterZ = cz, StyleName = style, Finish = Catalog.ResolveByStyleName(style) };

        private const string Fecham = "10 - FECHAM. (F -0,01 / E +0,01)";
        private const string Calib = "11 - MED CALIB (F H7 / E g6)";
        private const string Vedacao = "20 - VEDAÇÃO (±0,1)";
        private const string Agua = "21 - PASS DE AGUA (±0,2)";

        /// <summary>As 23 faces do 15142.101-CDT, como o leitor as devolveu.</summary>
        private static TurnedProfile Cdt()
        {
            var p = new TurnedProfile();
            p.Faces.AddRange(new[]
            {
                Cone(1175, -20.91, -0.508, 8.119, 8.208, 0.25),
                Plane(410, -20.91, 0, 8.119, Fecham),
                Torus(1678, -0.508, 0, 8.208, 8.718, 0.51, 8.718, -0.51),
                Cone(1158, 0, 7.657, 9.633, 9.733, 0.75),
                Plane(1167, 0, 8.718, 9.633),
                Torus(1670, 7.657, 8.16, 9.733, 10.243, 0.51, 10.243, 7.65),
                Plane(942, 8.16, 10.243, 10.5),
                Cyl(25, 8.16, 15.557, 10.5, Calib),
                Torus(3862, 15.557, 15.857, 10.5, 10.8, 0.3, 10.8, 15.557),
                Plane(64, 15.857, 10.8, 13.1, Vedacao),
                Cone(3777, 15.857, 16.257, 13.1, 13.5, 45),
                Cyl(148, 16.257, 17.857, 13.5, Calib),
                Cone(173, 17.857, 18.308, 13.5, 12.264, 70, Agua),
                Torus(289, 18.308, 22.254, 12.264, 9.5, 4.2, 13.7, 22.254, Agua),
                Cyl(177, 22.254, 22.461, 9.5, Agua),
                Torus(260, 22.461, 26.407, 9.5, 12.264, 4.2, 13.7, 22.46, Agua),
                Cone(181, 26.407, 26.857, 12.264, 13.5, 70, Agua),
                Cyl(56, 26.857, 29.557, 13.5, Calib),
                Torus(3827, 29.557, 29.857, 13.5, 13.8, 0.3, 13.8, 29.557),
                Cyl(97, 29.857, 32.507, 16.1, Calib),
                Plane(105, 29.857, 13.8, 16.1, Vedacao),
                Cone(3763, 32.507, 32.907, 16.1, 15.7, 45),
                Plane(102, 32.907, 0, 15.7, Fecham),
            });
            return p;
        }

        // ------------------------------------------------------------------ tabela de cores

        [Theory]
        [InlineData("11 - MED CALIB (F H7 / E g6)", "11")]
        [InlineData("20 - VEDAÇÃO (±0,1)", "20")]
        [InlineData("10 - FECHAM. (F -0,01 / E +0,01)", "10")]
        [InlineData("22 - SUP. ALTERADA", "22")]
        public void StyleName_ResolvesByCode(string style, string code)
        {
            Assert.Equal(code, Catalog.ResolveByStyleName(style).Code);
        }

        [Fact]
        public void Rgb_IsTheFallback_WhenTheStyleIsNotFromTheTable()
        {
            // A peça real tinha o canal de água pintado com "Blue (dull)", que tem o RGB do 21.
            FinishClass f = Catalog.Resolve("Blue (dull)", Color.FromArgb(54, 111, 178));
            Assert.Equal("21", f.Code);
            Assert.Null(Catalog.Resolve("Model Default", Color.FromArgb(129, 136, 140)));
        }

        [Fact]
        public void ErosionRoughness_TakesTheErodedCavityTolerance()
        {
            FinishClass f = Catalog.ResolveByStyleName("EROS. 1,6");
            Assert.Equal(1.6, f.Ra);
            Assert.Equal(0.01, f.Shaft.Upper);
            Assert.Equal(-0.01, f.Shaft.Lower);
        }

        [Fact]
        public void Green_ShaftColumn_IsMinus001Minus002_WithG6()
        {
            FinishClass verde = Catalog.ResolveByStyleName(Calib);
            Assert.Equal(-0.01, verde.For(external: true).Upper);
            Assert.Equal(-0.02, verde.For(external: true).Lower);
            Assert.Equal("g6", verde.FitFor(external: true));
            Assert.Equal(0.02, verde.For(external: false).Upper);
            Assert.Equal("H7", verde.FitFor(external: false));
        }

        [Fact]
        public void ChangedSurface_HasNoNumber_OnlyReview()
        {
            Assert.True(Catalog.ResolveByStyleName("22 - SUP. ALTERADA").NeedsReview);
        }

        // ------------------------------------------------------------------ plano de cotas

        [Fact]
        public void Stations_MatchTheHandMadeDrawing()
        {
            TurnedDimensionPlan plan = TurnedDimensionPlan.Build(Cdt());
            double origin = plan.Stations[0].Z;
            double[] got = plan.Stations.Select(s => System.Math.Round(s.Z - origin, 2)).ToArray();
            Assert.Equal(new[] { 0, 20.91, 29.07, 36.77, 38.77, 47.77, 50.77, 53.82 }, got);
            Assert.True(plan.Stations[0].IsOrigin);
        }

        [Fact]
        public void Stations_FromColoredPlanes_CarryTheirFinish()
        {
            TurnedDimensionPlan plan = TurnedDimensionPlan.Build(Cdt());
            double origin = plan.Stations[0].Z;
            AxialStation ved = plan.Stations.Single(s => System.Math.Abs(s.Z - origin - 36.767) < 0.01);
            Assert.Equal("20", ved.Finish.Code);
            AxialStation transition = plan.Stations.Single(s => System.Math.Abs(s.Z - origin - 38.767) < 0.01);
            Assert.Null(transition.Plane);
        }

        [Fact]
        public void Diameters_OnePerCylinderRadius_TwoPerTaper_SharpCornerAtFillets()
        {
            TurnedDimensionPlan plan = TurnedDimensionPlan.Build(Cdt());
            var ds = plan.Diameters.Select(d => System.Math.Round(d.Diameter, 2)).OrderBy(x => x).ToArray();
            // Ø19,48 e Ø16,42 = cone prolongado até o plano (canto vivo), como no desenho à mão.
            Assert.Equal(new[] { 16.24, 16.42, 19, 19.27, 19.48, 21, 27, 32.2 }, ds);
            Assert.Equal(2, plan.Diameters.Single(d => System.Math.Abs(d.Diameter - 27) < 1e-6).Count);
        }

        [Fact]
        public void Radii_SameTubeTwice_BecomesTwoX()
        {
            TurnedDimensionPlan plan = TurnedDimensionPlan.Build(Cdt());
            Assert.Equal(5, plan.Radii.Count);
            RadiusSpec canal = plan.Radii.Single(r => System.Math.Abs(r.Radius - 4.2) < 1e-6);
            Assert.Equal(2, canal.Count);
            Assert.Equal("21", canal.Finish.Code);
        }

        [Fact]
        public void ChamfersAnglesAndSkipped()
        {
            TurnedDimensionPlan plan = TurnedDimensionPlan.Build(Cdt());
            // Os dois chanfros 0,4 são a mesma cota "2x 0,4 X 45°".
            ChamferSpec ch = Assert.Single(plan.Chamfers);
            Assert.Equal(0.4, ch.Leg, 3);
            Assert.Equal(2, ch.Count);
            Assert.Equal(new[] { 0.5, 1.5 }, plan.Angles.Select(a => System.Math.Round(a.IncludedDeg, 2)).OrderBy(x => x).ToArray());
            // Flancos de 70° do canal de água: sem regra, ficam para cotar à mão.
            Assert.Equal(new[] { 173, 181 }, plan.Skipped.Select(f => f.Id).OrderBy(x => x).ToArray());
        }

        // ------------------------------------------------------------------ GD&T

        [Fact]
        public void Gdt_DatumA_IsTheLargestPrecisionCylinder_DatumB_TheLargestPrecisionPlane()
        {
            GdtPlan g = GdtPlan.Build(Cdt());
            Assert.Equal(25, g.DatumA.Id);    // Ø21 verde, o ajuste mais comprido
            Assert.Equal(102, g.DatumB.Id);   // face do flange (azul claro), a maior de fechamento
        }

        [Fact]
        public void Gdt_FramesFollowTheColors_AllPosition()
        {
            GdtPlan g = GdtPlan.Build(Cdt());
            // As referências levam só o símbolo, sem quadro.
            Assert.DoesNotContain(g.Frames, f => f.Face.Id == 25 || f.Face.Id == 102);
            // Os outros cilindros verdes: posição Ø a A (zona cilíndrica no eixo).
            foreach (int id in new[] { 97, 148, 56 })
                Assert.Contains(g.Frames, f => f.Face.Id == id && f.Symbol == GdtSymbol.Position && f.Datum == "A" && f.DiameterZone);
            // A outra ponta azul-clara: posição a B, sem Ø.
            Assert.Contains(g.Frames, f => f.Face.Id == 410 && f.Symbol == GdtSymbol.Position && f.Datum == "B" && !f.DiameterZone);
            Assert.All(g.Frames, f => Assert.Equal(GdtSymbol.Position, f.Symbol));
            // Vedação e água não pedem forma nem posição.
            Assert.DoesNotContain(g.Frames, f => f.Face.Finish != null && (f.Face.Finish.Code == "20" || f.Face.Finish.Code == "21"));
            Assert.Equal(4, g.Frames.Count);   // 3 cilindros + 1 plano
        }

        [Fact]
        public void Gdt_WithoutPrecisionCylinder_HasNoDatumA_AndNoFrame()
        {
            var p = new TurnedProfile();
            p.Faces.Add(Plane(1, 0, 0, 10, Fecham));
            p.Faces.Add(Cyl(2, 0, 20, 10, "21 - PASS DE AGUA (±0,2)"));
            GdtPlan g = GdtPlan.Build(p);
            Assert.Null(g.DatumA);
            Assert.Equal(1, g.DatumB.Id);
            Assert.Empty(g.Frames);   // só a referência B, nada posicionado contra ela
        }

        [Fact]
        public void Gdt_PositionFrameText_UsesDiameterZoneOnCylinders()
        {
            var f = new GdtFrame { Symbol = GdtSymbol.Position, Tolerance = 0.01, Datum = "A", DiameterZone = true };
            Assert.Equal("posição Ø0,01 | A", f.ToString());
        }

        [Fact]
        public void Formatter_ShowsToleranceFromColor()
        {
            TurnedProfile p = Cdt();
            string text = TurnedPlanFormatter.Describe(p, TurnedDimensionPlan.Build(p));
            Assert.Contains("Ø21", text);
            Assert.Contains("-0,01 / -0,02", text);
            Assert.Contains("2x R4,2", text);
        }
    }
}
