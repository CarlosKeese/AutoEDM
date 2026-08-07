using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Catálogo real de blanks de cobre (ver [[autoedm-decisions]]): trava os valores
    /// nominais em polegada ("13"/"25" = 12,7/25,4mm reais) e as regras de elegibilidade
    /// (material CuW80 só entra se pedido) contra regressão.</summary>
    public class StandardBlankLibraryTests
    {
        private readonly StandardBlankLibrary _lib = new StandardBlankLibrary();

        [Fact]
        public void SelectBlank_ReturnsSmallestFittingBySection()
        {
            var box = new BoundingBox { MinX = 0, MaxX = 5, MinY = 0, MaxY = 5, MinZ = 0, MaxZ = 3 };
            BlankSpec chosen = _lib.SelectBlank(box, marginPerSide: 0, material: "Cobre");
            Assert.NotNull(chosen);
            Assert.True(chosen.Fits(5, 5));

            // Nenhum blank elegível pode ter MaxDim menor que o escolhido (é o critério de ordenação real).
            var all = _lib.EligibleBlanks(box, marginPerSide: 0, material: "Cobre");
            Assert.Equal(chosen.Code, all.First().Code);
            Assert.True(all.All(b => b.MaxDim >= chosen.MaxDim));
        }

        [Fact]
        public void SelectBlank_ReturnsNull_WhenFootprintExceedsCatalog()
        {
            var box = new BoundingBox { MinX = 0, MaxX = 500, MinY = 0, MaxY = 500, MinZ = 0, MaxZ = 3 };
            Assert.Null(_lib.SelectBlank(box, marginPerSide: 0, material: "Cobre"));
        }

        [Fact]
        public void Catalog_NominalInchSizes_AreRealMillimeters()
        {
            // Carlos, 2026-07-17: "13"/"25" do catálogo nominal são 12,7/25,4mm reais (polegada).
            var box = new BoundingBox { MinX = 0, MaxX = 1, MinY = 0, MaxY = 1, MinZ = 0, MaxZ = 1 };
            var all = _lib.EligibleBlanks(box, marginPerSide: 0, material: "Cobre");

            var round13 = all.First(b => b.Code == "8836");
            Assert.Equal(12.7, round13.DimA);

            var round25 = all.First(b => b.Code == "11718");
            Assert.Equal(25.4, round25.DimA);
        }

        [Fact]
        public void CuW80_NotEligible_WhenMaterialNotRequested()
        {
            var box = new BoundingBox { MinX = 0, MaxX = 4, MinY = 0, MaxY = 4, MinZ = 0, MaxZ = 3 };
            var eligible = _lib.EligibleBlanks(box, marginPerSide: 0, material: "Cobre");
            Assert.DoesNotContain(eligible, b => b.Material == "CuW80");
        }

        [Fact]
        public void CuW80_Eligible_WhenMaterialRequestsIt()
        {
            var box = new BoundingBox { MinX = 0, MaxX = 4, MinY = 0, MaxY = 4, MinZ = 0, MaxZ = 3 };
            var eligible = _lib.EligibleBlanks(box, marginPerSide: 0, material: "Cobre CuW80");
            Assert.Contains(eligible, b => b.Material == "CuW80");
        }
    }
}
