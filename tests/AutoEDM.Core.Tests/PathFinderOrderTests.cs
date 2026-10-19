using System.Collections.Generic;
using System.Linq;
using AutoEDM.Assembly;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Botão "Ordenar PathFinder" (Carlos, 2026-10-19): "uma peça 15335.101.par deve vir depois de
    /// uma 15335.100.par". Números comparam pelo valor; posições do mesmo arquivo ficam juntas.
    /// </summary>
    public class PathFinderOrderTests
    {
        private static List<string> Sort(params (string name, string file)[] occ)
        {
            var items = occ.Select(o => new OccurrenceEntry(o.name, o.file)).ToList();
            return PathFinderOrder.Sorted(items).Select(k => items[k].Name).ToList();
        }

        [Theory]
        [InlineData("15335.100.par", "15335.101.par")]
        [InlineData("15335.99.par", "15335.100.par")]
        [InlineData("15335.101.par", "15335.200.par")]
        [InlineData("15335.200.par", "15335.1000.par")]
        [InlineData("9286.par", "15335.100.par")]
        [InlineData("15335.100.par", "Parafuso M6.par")]
        [InlineData("15335.100.asm", "15335.100.par")]
        [InlineData("15335.100.par", "15335.100-A.par")]
        public void VemAntes(string first, string second)
        {
            Assert.True(PathFinderOrder.CompareFileNames(first, second) < 0, $"{first} < {second}");
            Assert.True(PathFinderOrder.CompareFileNames(second, first) > 0, $"{second} > {first}");
        }

        [Fact]
        public void IgnoraMaiusculaEPasta()
        {
            Assert.True(PathFinderOrder.CompareFileNames(@"W:\MD-15335\15335.101.PAR", "15335.100.par") > 0);
            Assert.True(PathFinderOrder.CompareFileNames(@"W:\MD-15335\15335.100.PAR", "15335.101.par") < 0);
        }

        [Fact]
        public void CodigoLongoNaoEstoura()
        {
            Assert.True(PathFinderOrder.CompareNatural("x99999999999999999999", "x100000000000000000000") < 0);
        }

        [Fact]
        public void PosicoesDoMesmoArquivoFicamJuntasEmOrdem()
        {
            List<string> got = Sort(
                ("15335.200.par:1", "15335.200.par"),
                ("15335.100.par:10", "15335.100.par"),
                ("Parafuso M6.par:1", "Parafuso M6.par"),
                ("15335.101.par:1", "15335.101.par"),
                ("15335.100.par:2", "15335.100.par"),
                ("15335.100.par:1", "15335.100.par"));
            Assert.Equal(new[] { "15335.100.par:1", "15335.100.par:2", "15335.100.par:10", "15335.101.par:1", "15335.200.par:1", "Parafuso M6.par:1" }, got);
        }

        [Fact]
        public void OrdemPeloArquivo_NaoPeloNomeDaOcorrencia()
        {
            // Ocorrência renomeada na árvore: o que vale é o código do arquivo.
            List<string> got = Sort(("Placa B", "15335.201.par"), ("Placa A", "15335.202.par"), ("Bucha", "15335.101.par"));
            Assert.Equal(new[] { "Bucha", "Placa B", "Placa A" }, got);
        }

        [Fact]
        public void EmpateMantemOrdemAtual()
        {
            List<string> got = Sort(("b", "15335.100.par"), ("b", "15335.100.par"));
            Assert.Equal(new[] { "b", "b" }, got);
            var items = new[] { new OccurrenceEntry("b", "x.par"), new OccurrenceEntry("b", "x.par") };
            Assert.Equal(new[] { 0, 1 }, PathFinderOrder.Sorted(items));
        }
    }
}
