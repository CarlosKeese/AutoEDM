using System.Linq;
using AutoEDM.Reporting;
using AutoEDM.Reporting.Xlsx;
using AutoEDM.Revisions;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Peça NOVA na revisão (Carlos, 2026-09-18): quem não existia só pode ser FABRICADA, na
    /// quantidade que a montagem pede — e não se cobra dela "ação indicada".
    ///
    /// COMO a peça é reconhecida como nova está em aberto: a tentativa de ler a propriedade padrão
    /// "Número da Revisão" foi DESCARTADA no mesmo dia — ela existe em toda peça e a Solid Edge a
    /// usa como contador de salvamentos (43 peças vieram como "novas", revisão 153).
    /// </summary>
    public class NewPartRevisionTests
    {
        [Theory]
        [InlineData("2", 2)]
        [InlineData(" 3 ", 3)]
        [InlineData("02", 2)]
        [InlineData("Rev.4", 4)]
        public void PropertyValue_ReadsTheNumberTheWayTheFieldHoldsIt(string value, int expected)
        {
            int revision;
            Assert.True(RevisionName.TryParseValue(value, out revision));
            Assert.Equal(expected, revision);
        }

        [Theory]
        [InlineData("0")]      // projeto novo nasce assim — NÃO é peça nova
        [InlineData("")]
        [InlineData(null)]
        [InlineData("-1")]
        [InlineData("A")]
        public void ZeroOrEmpty_IsNotANewPart(string value)
        {
            int revision;
            Assert.False(RevisionName.TryParseValue(value, out revision));
        }

        [Theory]
        [InlineData(2, 0, 2, false)]   // só o grupo: peça alterada
        [InlineData(0, 2, 2, true)]    // só a propriedade: peça nova
        [InlineData(1, 3, 3, true)]    // propriedade mais recente: nova nesta revisão
        [InlineData(3, 1, 3, false)]   // grupo mais recente: alterada
        [InlineData(2, 2, 2, false)]   // empate fica com o grupo
        [InlineData(0, 0, 0, false)]   // não mudou
        public void Decide_PicksTheHigherRevision_AndSaysWhoIsNew(int group, int property, int expected, bool expectedNew)
        {
            bool isNew;
            Assert.Equal(expected, RevisionScanner.Decide(group, property, out isNew));
            Assert.Equal(expectedNew, isNew);
        }

        [Theory]
        [InlineData("Revisão", "revisao", true)]      // acento e caixa não importam
        [InlineData("Revisão", " REVISÃO ", true)]
        [InlineData("Revisão", "Número da Revisão", false)]   // o contador da SE NÃO pode casar
        [InlineData("Rev", "Revisão", false)]
        public void PropertyName_MatchesExactly_IgnoringCaseAndAccent(string a, string b, bool same)
        {
            Assert.Equal(same, RevisionScanner.Same(a, b));
        }

        [Fact]
        public void NewPart_ComesWithFabricarCheckedAndTheQuantity()
        {
            var p = new PartChange { FileName = "14309.220.par" };
            p.MarkAsNewPart(4);   // 4 posições na montagem

            ChangeTask fabricar = p.Tasks.First(t => t.Label.StartsWith("FABRICAR"));
            Assert.True(p.IsNew);
            Assert.True(fabricar.Checked);
            Assert.Equal("4", fabricar.Detail);
            Assert.Equal(4, p.Positions);
            // Quem fabrica precisa do material antes (Carlos, 2026-09-18).
            Assert.True(p.Tasks.First(t => t.Label.StartsWith("VERIF. MATERIAL")).Checked);
            Assert.Equal(2, p.CheckedTasks.Count());   // só essas duas — o resto continua com o usuário
            Assert.False(p.Tasks.First(t => t.Label.StartsWith("RECUPERAR")).Checked);
        }

        [Fact]
        public void ChangedPart_IsAlwaysRecuperar()
        {
            // Regra binária do Carlos (2026-09-18): o que não é fabricar é recuperar.
            var p = new PartChange { FileName = "14309.101.par" };
            p.MarkAsChangedPart();

            Assert.False(p.IsNew);
            Assert.True(p.Tasks.First(t => t.Label.StartsWith("RECUPERAR")).Checked);
            Assert.False(p.Tasks.First(t => t.Label.StartsWith("FABRICAR")).Checked);
            Assert.False(p.Tasks.First(t => t.Label.StartsWith("VERIF. MATERIAL")).Checked);
            Assert.Single(p.CheckedTasks);
        }

        [Fact]
        public void NewPartWithoutPositions_IsAtLeastOne()
        {
            var p = new PartChange();
            p.MarkAsNewPart(0);
            Assert.Equal("1", p.Tasks.First(t => t.Label.StartsWith("FABRICAR")).Detail);
        }

        [Fact]
        public void NewPart_IsOnlyPendingWhileItHasNoDescription()
        {
            var p = new PartChange { FileName = "14309.220.par" };
            p.MarkAsNewPart(2);
            Assert.True(ChangeReportFormatter.IsPending(p));     // sem descrição

            p.Description = "Postiço novo do macho da gaveta";
            // Sem AÇÃO indicada: peça nova não precisa, é fabricar e pronto.
            Assert.False(ChangeReportFormatter.IsPending(p));

            // Já a peça ALTERADA continua precisando de ação.
            var changed = new PartChange { Description = "Alargado o alojamento" };
            Assert.True(ChangeReportFormatter.IsPending(changed));
        }

        [Fact]
        public void Sheet_SaysItIsANewPartAndHowManyToMake()
        {
            var r = new ChangeReport { Revision = 3, AssemblyName = "14309v1.asm" };
            var p = new PartChange
            {
                FileName = "14309.220.par",
                Revision = 3,
                Description = "Postiço novo do macho da gaveta",
                RevisionSource = "marcada como nova",
            };
            p.MarkAsNewPart(2);
            r.Parts.Add(p);

            XlsxSheet sheet = ChangeReportXlsx.Build(r);
            Assert.Contains(sheet.Cells, c => c.Text != null && c.Text.StartsWith("PEÇA NOVA — fabricar 2"));
            Assert.Contains(sheet.Cells, c => c.Text == ChangeReportFormatter.Checked + " FABRICAR, QUANTIDADE: 2");
            Assert.DoesNotContain(sheet.Cells, c => c.Text != null && c.Text.StartsWith("OPERAÇÕES DO GRUPO"));

            string html = ChangeReportFormatter.ToHtmlFragment(r);
            Assert.Contains("PEÇA NOVA — fabricar 2", html);
        }

        [Fact]
        public void ChangedPart_SaysItIsChanged_NotNew()
        {
            var r = new ChangeReport { Revision = 2 };
            var p = new PartChange { FileName = "14309.101.par", Revision = 2, Description = "Alargado o alojamento" };
            p.Features.Add("Recorte 6");
            p.Actions.Add("Usinar o rasgo");
            r.Parts.Add(p);

            XlsxSheet sheet = ChangeReportXlsx.Build(r);
            Assert.Contains(sheet.Cells, c => c.Text == "peça alterada");
            Assert.Contains(sheet.Cells, c => c.Text == ChangeReportFormatter.Unchecked + " FABRICAR, QUANTIDADE:");
        }
    }
}
