using System;
using System.IO;
using System.Linq;
using AutoEDM.Revisions;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O .json ao lado da montagem: é ele que faz o texto digitado na janela sobreviver a fechar a
    /// Lista de modificações (Carlos, 2026-09-17).
    /// </summary>
    public class ChangeReportStoreTests
    {
        [Fact]
        public void FilePath_SitsNextToTheAssembly()
        {
            Assert.Equal(@"W:\MOLDES\MD-14309\14309v1_revisoes.json",
                ChangeReportStore.PathFor(@"W:\MOLDES\MD-14309\14309v1.asm"));
            Assert.Null(ChangeReportStore.PathFor(null));
            Assert.Null(ChangeReportStore.PathFor("   "));
        }

        [Fact]
        public void WhatWasTyped_ComesBackOnTheNextOpening()
        {
            ChangeReport first = Scanned();
            first.Parts[0].Description = "Adicionado alojamento para molas da extração";
            first.Parts[0].Actions.Add("Usinar face plana");
            first.Parts[0].Tasks.First(t => t.Label.StartsWith("EROSÃO")).Checked = true;
            first.Parts[0].Tasks.First(t => t.Label.StartsWith("FABRICAR")).Detail = "2 pçs";
            first.Parts[1].Include = false;
            first.Rvpa = "RV-99";

            ChangeArchive archive = ChangeReportStore.Capture(null, first, "14309v1.asm");

            // Segunda abertura: a varredura devolve o modelo cru, o arquivo devolve o que foi digitado.
            ChangeReport second = Scanned();
            ChangeReportStore.Apply(archive, second);

            Assert.Equal("Adicionado alojamento para molas da extração", second.Parts[0].Description);
            Assert.Equal(new[] { "Usinar face plana" }, second.Parts[0].Actions);
            Assert.True(second.Parts[0].Tasks.First(t => t.Label.StartsWith("EROSÃO")).Checked);
            Assert.Equal("2 pçs", second.Parts[0].Tasks.First(t => t.Label.StartsWith("FABRICAR")).Detail);
            Assert.False(second.Parts[1].Include);
            Assert.Equal("RV-99", second.Rvpa);
        }

        [Fact]
        public void ANewRevision_StartsBlank_ButKeepsTheOldOneOnFile()
        {
            ChangeReport rev2 = Scanned();
            rev2.Parts[0].Description = "postiço do macho";
            ChangeArchive archive = ChangeReportStore.Capture(null, rev2);

            ChangeReport rev3 = Scanned();
            rev3.Revision = 3;
            ChangeReportStore.Apply(archive, rev3);

            Assert.Null(rev3.Parts[0].Description);                    // Rev.3 não herda o texto da Rev.2
            Assert.Equal("postiço do macho", archive.Revisions["2"].Parts["14309.205.par"].Description);
        }

        [Fact]
        public void PartThatLeftTheAssembly_IsIgnored()
        {
            ChangeReport old = Scanned();
            old.Parts[0].Description = "saiu do molde";
            ChangeArchive archive = ChangeReportStore.Capture(null, old);

            var now = new ChangeReport { Revision = 2 };
            now.Parts.Add(new PartChange { FileName = "14309.999.par" });
            ChangeReportStore.Apply(archive, now);

            Assert.Null(now.Parts[0].Description);
            Assert.Single(now.Parts);
        }

        [Fact]
        public void RoundTripsThroughTheFile_ReadableWithAccents()
        {
            string path = Path.Combine(Path.GetTempPath(), "autoedm_" + Guid.NewGuid().ToString("N") + "_revisoes.json");
            try
            {
                ChangeReport report = Scanned();
                report.Parts[0].Description = "Adicionado postiço para reparar machos";
                Assert.True(ChangeReportStore.Save(path, ChangeReportStore.Capture(null, report, "14309v1.asm")));

                string json = File.ReadAllText(path);
                Assert.Contains("postiço", json);            // acento legível, não \u00e7
                Assert.Contains("\"revisoes\"", json);

                ChangeReport reopened = Scanned();
                ChangeReportStore.Apply(ChangeReportStore.Load(path), reopened);
                Assert.Equal("Adicionado postiço para reparar machos", reopened.Parts[0].Description);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        [Fact]
        public void BrokenFile_OpensBlankInsteadOfThrowing()
        {
            string path = Path.Combine(Path.GetTempPath(), "autoedm_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "{ isto não é json");
                ChangeArchive archive = ChangeReportStore.Load(path);
                Assert.Empty(archive.Revisions);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        [Fact]
        public void MissingFile_IsTheNormalFirstTime()
        {
            ChangeArchive archive = ChangeReportStore.Load(@"C:\nao\existe\x_revisoes.json");
            Assert.NotNull(archive);
            Assert.Empty(archive.Revisions);
        }

        /// <summary>O que a varredura do modelo devolve, antes de qualquer digitação.</summary>
        private static ChangeReport Scanned()
        {
            var r = new ChangeReport { Revision = 2, AssemblyName = "14309v1.asm" };
            r.Parts.Add(new PartChange { FileName = "14309.205.par", Revision = 2 });
            r.Parts.Add(new PartChange { FileName = "14309.216.par", Revision = 2 });
            return r;
        }
    }
}
