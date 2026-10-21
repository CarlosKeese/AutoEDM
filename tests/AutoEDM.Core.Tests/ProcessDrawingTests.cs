using System;
using System.IO;
using AutoEDM.Mold;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O plano do "Desenho de processo": onde a cópia e a montagem vão parar. Só disco — a
    /// parte que monta no Solid Edge é conferida ao vivo.
    /// </summary>
    public class ProcessDrawingTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AutoEDM_proc_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private string Part(string relative)
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
            return path;
        }

        [Fact]
        public void PecaNaRaizDoMolde_vaiParaASubpastaComONomeDela()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02 - PROCESSO"));
            string part = Part(@"MD-15335\15335.101.par");

            var plan = ProcessDrawingBuilder.Plan(part);

            Assert.True(plan.Ready, plan.Error);
            Assert.Equal(Path.Combine(_root, "MD-15335", "02 - PROCESSO", "15335.101"), plan.Folder);
            Assert.Equal(Path.Combine(plan.Folder, "15335.101_W01.par"), plan.CopyPath);
            Assert.Equal(Path.Combine(plan.Folder, "15335.101_WP.asm"), plan.AssemblyPath);
        }

        [Fact]
        public void PecaNumaSubpasta_achaO02ProcessoSubindo()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02-processo"));
            string part = Part(@"MD-15335\01 - MODELOS\Fixa\15335.101.par");

            var plan = ProcessDrawingBuilder.Plan(part);

            Assert.True(plan.Ready, plan.Error);
            Assert.Equal(Path.Combine(_root, "MD-15335", "02-processo"), plan.ProcessRoot);
        }

        [Fact]
        public void SemPastaDeProcesso_explica()
        {
            string part = Part(@"MD-15335\15335.101.par");
            var plan = ProcessDrawingBuilder.Plan(part);
            Assert.False(plan.Ready);
            Assert.Contains("02 - PROCESSO", plan.Error);
        }

        [Fact]
        public void JaExiste_naoSobrescreve()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02 - PROCESSO"));
            string part = Part(@"MD-15335\15335.101.par");
            Part(@"MD-15335\02 - PROCESSO\15335.101\15335.101_WP.asm");

            var plan = ProcessDrawingBuilder.Plan(part);

            Assert.Null(plan.Error);
            Assert.False(plan.Ready);
            Assert.Single(plan.Existing);
        }

        [Fact]
        public void NaoEPeca_recusa()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02 - PROCESSO"));
            string asm = Part(@"MD-15335\15335.asm");
            Assert.False(ProcessDrawingBuilder.Plan(asm).Ready);
        }
    }
}
