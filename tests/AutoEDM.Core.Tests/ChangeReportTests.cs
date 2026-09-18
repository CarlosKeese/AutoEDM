using System;
using System.Linq;
using AutoEDM.Reporting;
using AutoEDM.Revisions;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Lista de modificações (Carlos, 2026-09-17): cabeçalho tirado da pasta do projeto e a folha de
    /// revisões em HTML, para colar no Google Sheets.
    /// </summary>
    public class ChangeReportTests
    {
        private const string PartPath =
            @"W:\09 - Projetos\Projetos Atuais\02 - MOLDES\MD-14309 [PN-13972] [PA-10925]\14309.205.par";

        [Fact]
        public void ProjectFolder_ReadsTheCodesFromTheFolderName()
        {
            ProjectFolder f = ProjectFolder.Parse(PartPath);
            Assert.True(f.Found);
            Assert.Equal("14309", f.MoldCode);
            Assert.Equal("13972", f.PartNumbers);
            Assert.Equal("10925", f.ProductCode);
            Assert.EndsWith(@"MD-14309 [PN-13972] [PA-10925]", f.Directory);
            Assert.StartsWith(@"W:\09 - Projetos", f.Directory);
        }

        [Fact]
        public void ProjectFolder_TakesTheFolderClosestToThePart()
        {
            // Projeto dentro de projeto: vale o de baixo, que é o dono da peça.
            ProjectFolder f = ProjectFolder.Parse(@"W:\MOLDES\MD-14309 [PN-13972]\MD-14310 [PN-13980]\14310.100.par");
            Assert.Equal("14310", f.MoldCode);
            Assert.Equal("13980", f.PartNumbers);
            Assert.Null(f.ProductCode);
        }

        [Fact]
        public void ProjectFolder_OutsideThePattern_FindsNothingInsteadOfGuessing()
        {
            ProjectFolder f = ProjectFolder.Parse(@"C:\temp\peça.par");
            Assert.False(f.Found);
            Assert.Null(f.Directory);
            Assert.False(ProjectFolder.Parse(null).Found);
        }

        [Fact]
        public void ProjectFolder_KeepsTheUncPrefix()
        {
            ProjectFolder f = ProjectFolder.Parse(@"\\servidor\projetos\MD-14309 [PN-13972]\14309.205.par");
            Assert.StartsWith(@"\\servidor\projetos\MD-14309", f.Directory);
        }

        [Fact]
        public void Sheet_StartsBlank_AndEachPartMarksItsOwnBoxes()
        {
            var a = new PartChange { FileName = "14309.205.par" };
            var b = new PartChange { FileName = "14309.216.par" };
            Assert.All(a.Tasks, t => Assert.False(t.Checked));

            a.Tasks.First(t => t.Label.StartsWith("EROSÃO")).Checked = true;
            Assert.Single(a.CheckedTasks);
            Assert.Empty(b.CheckedTasks);
        }

        [Fact]
        public void Html_CarriesHeaderPartsActionsAndCheckboxes()
        {
            ChangeReport r = Sample();
            string html = ChangeReportFormatter.ToHtmlFragment(r);

            Assert.Contains("REGISTRO DE REVISÕES", html);
            Assert.Contains("Rev. 2", html);
            Assert.Contains("10925", html);                                  // PA do cabeçalho
            Assert.Contains("Adicionado postiço para reparar machos", html); // descrição na capa e na folha
            Assert.Contains("1 - Usinar face plana para possibilitar furação", html);
            Assert.Contains("3 - Cortar perfil do postiço", html);
            Assert.Contains(ChangeReportFormatter.Checked + " EROSÃO / FURO RÁPIDO / FIO", html);
            Assert.Contains(ChangeReportFormatter.Unchecked + " SOLDA TIG / LASER", html);
            Assert.Contains("<table", html);
        }

        [Fact]
        public void Html_LeavesOutTheUncheckedParts()
        {
            ChangeReport r = Sample();
            r.Parts.Add(new PartChange { FileName = "14309.999.par", Description = "não entra", Include = false });

            string html = ChangeReportFormatter.ToHtmlFragment(r);
            Assert.DoesNotContain("14309.999.par", html);
            Assert.Contains("Total: 1 peça(s) alterada(s)", html);
        }

        [Fact]
        public void Html_EscapesTextAndWarnsAboutWhatIsMissing()
        {
            var r = new ChangeReport { Revision = 3, AssemblyName = "14309 <montagem>.asm" };
            r.Parts.Add(new PartChange { FileName = "14309.300.par" });   // sem descrição nem ação

            string html = ChangeReportFormatter.ToHtmlFragment(r);
            Assert.Contains("14309 &lt;montagem&gt;.asm", html);
            Assert.DoesNotContain("<montagem>", html);
            Assert.Contains("(sem descrição — preencher)", html);
            Assert.Contains("ATENÇÃO: 1 peça(s) sem descrição ou sem ação indicada", html);
            Assert.Equal(1, ChangeReportFormatter.PendingCount(r));
        }

        [Fact]
        public void ClipboardBytes_AreUtf8WithoutBom()
        {
            byte[] bytes = ChangeReportFormatter.ToClipboardHtmlBytes(Sample());
            Assert.Equal((byte)'V', bytes[0]);   // "Version:" no byte 0, sem BOM
            Assert.Contains("REGISTRO DE REVISÕES", System.Text.Encoding.UTF8.GetString(bytes));
        }

        private static ChangeReport Sample()
        {
            ProjectFolder f = ProjectFolder.Parse(PartPath);
            var r = new ChangeReport
            {
                Revision = 2,
                When = new DateTime(2026, 9, 17, 11, 19, 0),
                AssemblyName = "14309.asm",
                MoldCode = f.MoldCode,
                PartNumbers = f.PartNumbers,
                ProductCode = f.ProductCode,
                ProjectDirectory = f.Directory,
            };
            var p = new PartChange
            {
                FileName = "14309.205.par",
                FullPath = PartPath,
                Description = "Adicionado postiço para reparar machos",
            };
            p.Actions.Add("Usinar face plana para possibilitar furação");
            p.Actions.Add("Furar e calibrar alojamento do pino trava");
            p.Actions.Add("Cortar perfil do postiço");
            p.Tasks.First(t => t.Label.StartsWith("EROSÃO")).Checked = true;
            p.Tasks.First(t => t.Label.StartsWith("RECUPERAR")).Checked = true;
            r.Parts.Add(p);
            return r;
        }
    }
}
