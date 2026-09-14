using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Lista de corte (Carlos, 2026-09-14): identificação do perfil do estoque pelas medidas
    /// da peça e medida na serra com 5 mm de sobremetal.</summary>
    public class SawCutPlannerTests
    {
        private readonly BlankSpec[] _catalog = new StandardBlankLibrary().Catalog.ToArray();

        private BlankSpec Code(string code) => _catalog.Single(b => b.Code == code);

        [Fact]
        public void StandingRectangle_CutsElectrodeHeightPlusAllowance()
        {
            SawCut cut = SawCutPlanner.Identify(50, 19, 42.3, null, _catalog);
            Assert.Equal("8844", cut.Blank.Code);
            Assert.False(cut.LaidDown);
            Assert.Equal(42.3, cut.LengthMm);
            Assert.Equal(48, cut.CutMm);   // 42,3 → 43 + 5
            Assert.True(cut.AutoIdentified);
        }

        [Fact]
        public void Identification_IgnoresXYOrientation_AndSmallModelNoise()
        {
            Assert.Equal("8844", SawCutPlanner.Identify(18.9, 50.2, 30, null, _catalog).Blank.Code);
        }

        [Fact]
        public void ExactWholeMillimeter_IsNotRoundedUpAgain()
        {
            // A caixa do SE chega com ruído (40,000000001) e não pode virar 41 + 5.
            Assert.Equal(45, SawCutPlanner.Identify(32, 32, 40.000000001, null, _catalog).CutMm);
        }

        [Fact]
        public void Round_IdentifiedByBoundingBox_AndInchSizesAreDistinct()
        {
            Assert.Equal("11718", SawCutPlanner.Identify(25.4, 25.4, 30, null, _catalog).Blank.Code);
            Assert.Equal("8836", SawCutPlanner.Identify(12.7, 12.7, 30, null, _catalog).Blank.Code); // não o RED 12 CuW80
        }

        [Fact]
        public void LaidDown_CutIsTheLongFootprintSide()
        {
            // QUAD 19 deitado cortado a 24: pegada 24 × 19, altura 19 (+ a queima).
            SawCut cut = SawCutPlanner.Identify(24, 19, 25, null, _catalog);
            Assert.Equal("11715", cut.Blank.Code);
            Assert.True(cut.LaidDown);
            Assert.Equal(29, cut.CutMm);
            // RET 50×19 deitado daria 50 de altura — não cabe em 25 mm de peça.
            Assert.DoesNotContain(cut.Alternatives, b => b.Code == "8844");
        }

        [Fact]
        public void Standing_WinsOverLaidDown_ButAmbiguityIsReported()
        {
            SawCut cut = SawCutPlanner.Identify(50, 19, 60, null, _catalog);
            Assert.Equal("8844", cut.Blank.Code);
            Assert.False(cut.LaidDown);
            Assert.Contains(cut.Alternatives, b => b.Code == "11715"); // QUAD 19 deitado cortado a 50
            Assert.NotNull(cut.Note);
        }

        [Fact]
        public void SameSection_PartMaterialPicksCuW80()
        {
            SawCut plain = SawCutPlanner.Identify(16, 16, 20, null, _catalog);
            Assert.Equal("8837", plain.Blank.Code);
            Assert.Contains(plain.Alternatives, b => b.Code == "12345");

            Assert.Equal("12345", SawCutPlanner.Identify(16, 16, 20, "CuW80", _catalog).Blank.Code);
        }

        [Fact]
        public void NoMatchingProfile_HasNoCut()
        {
            SawCut cut = SawCutPlanner.Identify(40, 30, 20, null, _catalog);
            Assert.Null(cut.Blank);
            Assert.Null(cut.CutMm);
            Assert.NotNull(cut.Note);
        }

        [Fact]
        public void ForBlank_StandingWhenSectionHoldsTheElectrode()
        {
            // Ex.: eletrodo antigo com holder — o usuário escolhe o RET 50×19 à mão.
            SawCut cut = SawCutPlanner.ForBlank(Code("8844"), 30, 10, 40);
            Assert.False(cut.LaidDown);
            Assert.Equal(45, cut.CutMm);
        }

        [Fact]
        public void ForBlank_LaidDownWhenLongerThanSection()
        {
            SawCut cut = SawCutPlanner.ForBlank(Code("11715"), 60, 15, 18);
            Assert.True(cut.LaidDown);
            Assert.Equal(65, cut.CutMm);
            Assert.Null(cut.Note);
        }

        [Fact]
        public void ForBlank_ElectrodeWiderThanSection_HasNoCut()
        {
            SawCut cut = SawCutPlanner.ForBlank(Code("8835"), 30, 20, 20); // RED 10
            Assert.Null(cut.CutMm);
            Assert.NotNull(cut.Note);
        }

        [Fact]
        public void CutLongerThanBar_IsFlagged()
        {
            SawCut cut = SawCutPlanner.ForBlank(Code("11715"), 498, 19, 19);
            Assert.Equal(503, cut.CutMm);
            Assert.NotNull(cut.Note);
        }
    }
}
