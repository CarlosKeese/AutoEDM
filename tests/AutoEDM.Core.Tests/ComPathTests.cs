using System;
using System.Collections.Generic;
using AutoEDM.Mcp;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O caminho COM que o agente escreve em 'se_com_ler' / 'se_com_chamar'. A análise do texto e a
    /// regra "leitura não executa método" são testáveis sem Solid Edge: o InvokeMember trata um
    /// objeto .NET comum do mesmo jeito.
    /// </summary>
    public class ComPathTests
    {
        public sealed class Leaf
        {
            public int Value { get; set; } = 42;
            public bool Deleted { get; private set; }
            public void Delete() { Deleted = true; }
        }

        public sealed class Bag
        {
            public Leaf Only { get; } = new Leaf();
            public Leaf Item(int index) => index == 1 ? Only : null;
            public string Named(string key) => "<" + key + ">";
        }

        [Fact]
        public void Parse_SplitsRootStepsAndArguments()
        {
            List<ComPathSegment> s = ComPath.Parse("doc.Models.Item(1).Body.Faces(1)[3].Style('Azul claro', 2.5, true)");

            Assert.Equal(new[] { "doc", "Models", "Item", "Body", "Faces", "Item", "Style" }, s.ConvertAll(x => x.Name).ToArray());
            Assert.Null(s[1].Args);                       // propriedade simples
            Assert.Equal(new object[] { 1 }, s[2].Args);
            Assert.Equal(new object[] { 3 }, s[5].Args);  // [3] = Item(3)
            Assert.Equal(new object[] { "Azul claro", 2.5, true }, s[6].Args);
        }

        [Fact]
        public void Parse_AcceptsHandleRootAndNegativeNumbers()
        {
            List<ComPathSegment> s = ComPath.Parse("$12.Move(-0.02, 1e-3)");
            Assert.Equal("$12", s[0].Name);
            Assert.Equal(new object[] { -0.02, 0.001 }, s[1].Args);
        }

        [Theory]
        [InlineData("")]
        [InlineData("doc.")]
        [InlineData("doc..Models")]
        [InlineData("doc.Item(1")]
        [InlineData("doc.Item(abc)")]
        [InlineData("doc(1).Models")]
        [InlineData("[1].Name")]
        public void Parse_RejectsMalformedPaths(string path) => Assert.Throws<FormatException>(() => ComPath.Parse(path));

        [Fact]
        public void Evaluate_WalksPropertiesAndItem()
        {
            var bag = new Bag();
            Assert.Equal(42, ComPath.Evaluate(_ => bag, "x.Only.Value", readOnly: true));
            Assert.Equal(42, ComPath.Evaluate(_ => bag, "x.Item(1).Value", readOnly: true));
            Assert.Equal(42, ComPath.Evaluate(_ => bag, "x[1].Value", readOnly: true));
        }

        [Fact]
        public void Evaluate_ReadOnly_NeverRunsAMethod()
        {
            // A garantia de 'se_com_ler': um caminho de leitura não executa Delete/Save/Close.
            var bag = new Bag();
            var ex = Assert.Throws<InvalidOperationException>(() => ComPath.Evaluate(_ => bag, "x.Only.Delete()", readOnly: true));
            Assert.False(bag.Only.Deleted);
            Assert.Contains("se_com_chamar", ex.Message);

            Assert.Throws<InvalidOperationException>(() => ComPath.Evaluate(_ => bag, "x.Named('a')", readOnly: true));
            Assert.Equal("<a>", ComPath.Evaluate(_ => bag, "x.Named('a')", readOnly: false));
        }

        [Fact]
        public void Evaluate_SaysWhichStepFailed()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ComPath.Evaluate(_ => new Bag(), "x.Only.Nope.Value", readOnly: true));
            Assert.Contains("x.Only.Nope", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => ComPath.Evaluate(_ => new Bag(), "x.Item(2).Value", readOnly: true));
            Assert.Contains("devolveu vazio", ex.Message);
        }

        [Fact]
        public void GroupOccurrences_CollapsesRepeatsIntoRanges()
        {
            var input = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(1, "15428.101.par"),
                new KeyValuePair<int, string>(2, "13337.par"),
                new KeyValuePair<int, string>(3, "13337.par"),
                new KeyValuePair<int, string>(4, "13337.par"),
                new KeyValuePair<int, string>(5, "8937.par"),
                new KeyValuePair<int, string>(6, "13337.par"),
                new KeyValuePair<int, string>(7, "8937.PAR"),
            };

            Assert.Equal(
                new[] { "[1] 15428.101.par", "13337.par  ×4  (índices 2–4, 6)", "8937.par  ×2  (índices 5, 7)" },
                SeToolRunner.GroupOccurrences(input).ToArray());
        }

        [Fact]
        public void NewTools_AreInTheCatalog_WithTheRightWriteFlag()
        {
            foreach (string read in new[] { "se_captura", "se_documentos", "se_ativar_documento", "se_selecionar", "se_com_ler" })
                Assert.False(ToolCatalog.Find(read).Writes, read + " é leitura.");
            foreach (string write in new[] { "se_abrir_documento", "se_fechar_documento", "se_com_chamar" })
                Assert.True(ToolCatalog.Find(write).Writes, write + " tem de exigir a escrita liberada.");
        }
    }
}
