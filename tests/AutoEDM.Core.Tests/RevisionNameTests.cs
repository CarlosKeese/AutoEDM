using AutoEDM.Revisions;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O nome do grupo da árvore ordenada ("Rev.2"), que é como o Carlos marca o que mudou.
    /// A leitura COM em volta disso está no RevisionScanner e só roda com o Solid Edge aberto;
    /// o que dá para fixar em teste é a interpretação do nome.
    /// </summary>
    public class RevisionNameTests
    {
        [Theory]
        [InlineData("Rev.2", 2)]
        [InlineData("rev 3", 3)]
        [InlineData("REV-10", 10)]
        [InlineData("Rev1", 1)]
        [InlineData("Revisão 4", 4)]
        [InlineData("Revisao 5", 5)]
        [InlineData("  Rev. 7  ", 7)]
        public void ReadsTheNumber_HoweverItWasTyped(string name, int expected)
        {
            int revision;
            Assert.True(RevisionName.TryParse(name, out revision));
            Assert.Equal(expected, revision);
        }

        [Theory]
        [InlineData("Group_1")]           // grupo sem nome de revisão
        [InlineData("Furos da gaveta")]   // grupo que o Carlos usa para outra coisa
        [InlineData("Revisar depois")]    // começa com "Rev" mas não tem número
        [InlineData("")]
        [InlineData(null)]
        public void WhatIsNotARevision_IsNotOne(string name)
        {
            int revision;
            Assert.False(RevisionName.TryParse(name, out revision));
            Assert.Equal(0, revision);
        }

        [Fact]
        public void TextAfterTheNumber_BecomesTheDraftDescription()
        {
            Assert.Equal("molas da extração", RevisionName.DescriptionAfterNumber("Rev.2 - molas da extração"));
            Assert.Equal("molas da extração", RevisionName.DescriptionAfterNumber("Rev.2 = molas da extração"));
            Assert.Equal("postiço do macho", RevisionName.DescriptionAfterNumber("Rev 3: postiço do macho"));
        }

        [Fact]
        public void NameWithOnlyTheNumber_LeavesTheDescriptionForTheWindow()
        {
            Assert.Null(RevisionName.DescriptionAfterNumber("Rev.2"));
            Assert.Null(RevisionName.DescriptionAfterNumber("Rev. 2   "));
            Assert.Null(RevisionName.DescriptionAfterNumber("Furos da gaveta"));
        }

        [Theory]
        [InlineData("1 - Ajustar a chaveta", 1, "Ajustar a chaveta")]
        [InlineData("2 Furo do pino trava", 2, "Furo do pino trava")]
        [InlineData("3) Cortar o perfil", 3, "Cortar o perfil")]
        [InlineData("10. Rebaixo", 10, "Rebaixo")]
        [InlineData("4", 4, "")]
        public void NumberedFeature_IsAnIndicatedAction(string name, int number, string text)
        {
            int n;
            string t;
            Assert.True(RevisionName.TryParseOperation(name, out n, out t));
            Assert.Equal(number, n);
            Assert.Equal(text, t);
        }

        [Theory]
        [InlineData("Recorte 6")]          // nome automático da Solid Edge: não é ação
        [InlineData("Substituir Face 2")]
        [InlineData("Furo M6")]
        [InlineData("0 - nada")]
        [InlineData("")]
        [InlineData(null)]
        public void FeatureWithoutALeadingNumber_IsNotAnAction(string name)
        {
            int n;
            string t;
            Assert.False(RevisionName.TryParseOperation(name, out n, out t));
        }

        [Fact]
        public void NumberedOperations_ComeInOrder_WithoutTheNumber()
        {
            var operations = new[] { "Substituir Face 2", "2 - Furar o pino", "Recorte 6", "1 - Ajustar a chaveta" };
            Assert.Equal(new[] { "Ajustar a chaveta", "Furar o pino" },
                RevisionScanner.NumberedOperations(operations));
        }

        [Fact]
        public void TwoDigitRevision_IsNotReadAsOne()
        {
            int revision;
            RevisionName.TryParse("Rev.12", out revision);
            Assert.Equal(12, revision);
        }
    }
}
