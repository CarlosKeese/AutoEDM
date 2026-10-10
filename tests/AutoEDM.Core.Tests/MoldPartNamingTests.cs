using System.IO;
using AutoEDM.Mold;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Codificação das peças do molde (Carlos, 2026-09-24): .100 fixa, .200 móvel, .300 extração,
    /// sempre o próximo número da série. Os nomes vêm da pasta real do MD-15335.
    /// </summary>
    public class MoldPartNamingTests
    {
        private static readonly string[] Md15335 =
        {
            "15335.200.par", "15335.201.par", "15335.202.par", "15335.203.par", "15335.204.par",
            "15335.205.par", "15335.206.par", "15335.207.par", "15335.208.par", "15335.209.par",
            "15335.210.par", "15335.211.par", "Porta Molde - 3035415125B.asm", "Placa Suporte_1.par",
        };

        [Fact]
        public void Movel_SegueDoMaiorUsado()
        {
            Assert.Equal(212, MoldPartNaming.NextNumber(Md15335, "15335", MoldSection.Moving));
        }

        [Fact]
        public void SerieVazia_ComecaNoProprioInicio()
        {
            // O MD-15335 começa a móvel no 200 (15335.200.par existe) — então a fixa começa no 100.
            Assert.Equal(100, MoldPartNaming.NextNumber(Md15335, "15335", MoldSection.Fixed));
            Assert.Equal(300, MoldPartNaming.NextNumber(Md15335, "15335", MoldSection.Ejection));
        }

        [Fact]
        public void DesenhoETambemOcupaONumero()
        {
            var names = new[] { "15335.100.par", "15335.105.dft", "15335.103 - rev1.par" };
            Assert.Equal(106, MoldPartNaming.NextNumber(names, "15335", MoldSection.Fixed));
        }

        [Fact]
        public void OutroMolde_NaoConta()
        {
            var names = new[] { "15335.100.par", "14309.150.par" };
            Assert.Equal(101, MoldPartNaming.NextNumber(names, "15335", MoldSection.Fixed));
        }

        [Fact]
        public void NumeroDeQuatroDigitos_NaoEhDaSerie()
        {
            // "15335.2000.par" não é o 200: o código é de 3 dígitos.
            Assert.Equal(200, MoldPartNaming.NextNumber(new[] { "15335.2000.par" }, "15335", MoldSection.Moving));
        }

        [Fact]
        public void SerieCheia_DevolveMenosUm()
        {
            Assert.Equal(-1, MoldPartNaming.NextNumber(new[] { "15335.299.par" }, "15335", MoldSection.Moving));
        }

        [Fact]
        public void Prefixo_DasPecasOuDaPasta()
        {
            Assert.Equal("15335", MoldPartNaming.GuessPrefix(Md15335, "99999"));
            Assert.Equal("15335", MoldPartNaming.GuessPrefix(new[] { "Placa.par" }, "15335"));
            Assert.Null(MoldPartNaming.GuessPrefix(new[] { "Placa.par" }, null));
        }

        [Fact]
        public void NomeDoArquivo()
        {
            Assert.Equal("15335.212.par", MoldPartNaming.FileName("15335", 212));
            Assert.Equal("15335.100.par", MoldPartNaming.FileName("15335", 100));
        }

        [Theory]
        [InlineData(HeightAxis.Z, false, 5, 10, 0)]
        [InlineData(HeightAxis.Z, true, 5, 10, 30)]
        [InlineData(HeightAxis.Y, false, 5, -20, 15)]
        [InlineData(HeightAxis.X, true, 40, 10, 15)]
        public void Origem_CentroNaPlantaExtremoNaAltura(HeightAxis axis, bool top, double x, double y, double z)
        {
            double[] o = NewPartPlacement.OriginMm(new double[] { -30, -20, 0 }, new double[] { 40, 40, 30 }, axis, top);
            Assert.Equal(new[] { x, y, z }, o);
        }

        [Fact]
        public void Preferencias_DoProjeto_IdaEVolta()
        {
            string store = Path.Combine(Path.GetTempPath(), "autoedm-mold-" + System.Guid.NewGuid() + ".json");
            try
            {
                new MoldProjectSettings { HeightAxis = HeightAxis.Y, OriginAtTop = true, LastSection = MoldSection.Ejection }
                    .Save(@"W:\MD-15335\Porta Molde.asm", store);
                MoldProjectSettings back = MoldProjectSettings.Load(@"w:\md-15335\porta molde.asm", store);
                Assert.Equal(HeightAxis.Y, back.HeightAxis);
                Assert.True(back.OriginAtTop);
                Assert.Equal(MoldSection.Ejection, back.LastSection);

                MoldProjectSettings other = MoldProjectSettings.Load(@"W:\MD-14309\Outro.asm", store);
                Assert.Equal(HeightAxis.Z, other.HeightAxis);
            }
            finally { if (File.Exists(store)) File.Delete(store); }
        }
    }
}
