using System;
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
        public void LaidDown_CutIsTheLongFootprintSide_AndBeatsAStandingBarThatWastesMore()
        {
            // QUAD 19 deitado cortado a 24: pegada 24 × 19 e altura 19 p/ a peça de 18 (1 mm p/ facear).
            // Em pé só o RET 50×19 comportaria a pegada — e gastaria o dobro de material.
            SawCut cut = SawCutPlanner.Identify(24, 19, 18, null, _catalog);
            Assert.Equal("11715", cut.Blank.Code);
            Assert.True(cut.LaidDown);
            Assert.Equal(29, cut.CutMm);
            Assert.Contains(cut.Alternatives, b => b.Code == "8844");
        }

        [Fact]
        public void LaidDown_NeedsOneMillimeterOverTheHeightToFace()
        {
            // Mesma peça 1 mm mais alta: o QUAD 19 deitado dá 19 de altura p/ 19 de peça, sem material
            // p/ facear no centro de usinagem (Carlos, 2026-09-17) — sai de cena e o corte vira em pé.
            SawCut cut = SawCutPlanner.Identify(24, 19, 19, null, _catalog);
            Assert.Equal("8844", cut.Blank.Code);
            Assert.False(cut.LaidDown);
            Assert.Equal(24, cut.CutMm);
            Assert.DoesNotContain(cut.Alternatives, b => b.Code == "11715");
        }

        [Fact]
        public void LaidDown_DoesNotMatchAProfileShorterThanThePart()
        {
            // Caso real (Carlos, 2026-09-17): 29,0 × 25,4 × 31,0 saía "RET 25,4×12,7 deitado, corte 34"
            // só porque o 25,4 batia — deitado esse perfil dá 12,7 mm de altura e a peça tem 31.
            SawCut cut = SawCutPlanner.Identify(29.0, 25.4, 31.0, null, _catalog);
            Assert.Equal("8840", cut.Blank.Code);   // QUAD 32 em pé, a menor barra que comporta 29 × 25,4
            Assert.False(cut.LaidDown);
            Assert.Equal(36, cut.CutMm);            // 31 → 31 + 5
            Assert.DoesNotContain(cut.Alternatives, b => b.Code == "8842");
            Assert.NotNull(cut.Fit);                // "sobra 3,0 × 6,6 mm na seção"
        }

        [Fact]
        public void MaterialIsNeverSmallerThanThePart_AndTheLeftoverIsReported()
        {
            SawCut cut = SawCutPlanner.Identify(29.0, 25.4, 31.0, null, _catalog);
            double hi = Math.Max(cut.Blank.DimA, cut.Blank.DimB ?? cut.Blank.DimA);
            double lo = Math.Min(cut.Blank.DimA, cut.Blank.DimB ?? cut.Blank.DimA);
            Assert.True(hi >= 29.0 && lo >= 25.4);
            Assert.True(cut.CutMm >= 31.0 + SawCutPlanner.FacingAllowanceMm);
            Assert.Contains("sobra", cut.Fit);
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
        public void BiggerThanEveryBar_HasNoCut()
        {
            // 60 × 55 × 60: nenhuma seção comporta a pegada e nenhuma barra deitada dá 61 mm de altura.
            SawCut cut = SawCutPlanner.Identify(60, 55, 60, null, _catalog);
            Assert.Null(cut.Blank);
            Assert.Null(cut.CutMm);
            Assert.Contains("comprar material", cut.Note);
        }

        [Fact]
        public void SmallerThanTheBar_StillCutsFromStock()
        {
            // 40 × 30 × 20 não é medida de barra nenhuma, mas sai do QUAD 32 deitado (corte 45).
            SawCut cut = SawCutPlanner.Identify(40, 30, 20, null, _catalog);
            Assert.Equal("8840", cut.Blank.Code);
            Assert.True(cut.LaidDown);
            Assert.Equal(45, cut.CutMm);
            Assert.Contains("facear", cut.Fit);
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
            Assert.Null(cut.Note);   // 19 de seção p/ 18 de peça: dá o 1 mm de faceamento
        }

        [Fact]
        public void ForBlank_LaidDownWithoutRoomToFace_IsFlagged()
        {
            SawCut cut = SawCutPlanner.ForBlank(Code("11715"), 60, 15, 18.5);
            Assert.True(cut.LaidDown);
            Assert.Contains("facear", cut.Note);
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
