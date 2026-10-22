using System;
using System.IO;
using System.Linq;
using AutoEDM.Mold;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O plano do "Desenho de processo": onde as peças _CDU/_CDT e a montagem vão parar. Só disco — a
    /// Cópia de peça vinculada é conferida ao vivo (tools/PartCopyProbe).
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

        private string Mold()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02 - PROCESSO"));
            return Part(@"MD-15335\15335.101.par");
        }

        [Fact]
        public void PecaNaRaizDoMolde_vaiParaASubpastaComONomeDela()
        {
            string part = Mold();

            var plan = ProcessDrawingBuilder.Plan(part, ProcessMachine.Cdu);

            Assert.True(plan.Ready, plan.Error);
            Assert.Equal(Path.Combine(_root, "MD-15335", "02 - PROCESSO", "15335.101"), plan.Folder);
            Assert.Equal(Path.Combine(plan.Folder, "15335.101_CDU.par"), Assert.Single(plan.Copies).Path);
            Assert.Equal(Path.Combine(plan.Folder, "15335.101_WP.asm"), plan.AssemblyPath);
        }

        [Fact]
        public void AsDuasMaquinas_geramCduECdt_nessaOrdem()
        {
            var plan = ProcessDrawingBuilder.Plan(Mold(), ProcessMachine.Cdt | ProcessMachine.Cdu);

            Assert.True(plan.Ready, plan.Error);
            Assert.Equal(new[] { "15335.101_CDU.par", "15335.101_CDT.par" },
                         plan.Copies.Select(c => Path.GetFileName(c.Path)).ToArray());
        }

        [Fact]
        public void SemMaquina_explica()
        {
            var plan = ProcessDrawingBuilder.Plan(Mold(), ProcessMachine.None);
            Assert.False(plan.Ready);
            Assert.Contains("CDU", plan.Error);
        }

        [Fact]
        public void PecaNumaSubpasta_achaO02ProcessoSubindo()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02-processo"));
            string part = Part(@"MD-15335\01 - MODELOS\Fixa\15335.101.par");

            var plan = ProcessDrawingBuilder.Plan(part, ProcessMachine.Cdt);

            Assert.True(plan.Ready, plan.Error);
            Assert.Equal(Path.Combine(_root, "MD-15335", "02-processo"), plan.ProcessRoot);
        }

        [Fact]
        public void SemPastaDeProcesso_explica()
        {
            string part = Part(@"MD-15335\15335.101.par");
            var plan = ProcessDrawingBuilder.Plan(part, ProcessMachine.Cdu);
            Assert.False(plan.Ready);
            Assert.Contains("02 - PROCESSO", plan.Error);
        }

        [Fact]
        public void MontagemJaExiste_acrescentaSoAPecaQueFalta()
        {
            string part = Mold();
            Part(@"MD-15335\02 - PROCESSO\15335.101\15335.101_CDU.par");
            Part(@"MD-15335\02 - PROCESSO\15335.101\15335.101_WP.asm");

            var plan = ProcessDrawingBuilder.Plan(part, ProcessMachine.Cdu | ProcessMachine.Cdt);

            Assert.True(plan.Ready, plan.Error);
            Assert.True(plan.AssemblyExists);
            Assert.Equal("15335.101_CDT.par", Path.GetFileName(Assert.Single(plan.ToCreate).Path));
        }

        [Fact]
        public void TudoJaExiste_soAbre()
        {
            string part = Mold();
            Part(@"MD-15335\02 - PROCESSO\15335.101\15335.101_CDU.par");
            Part(@"MD-15335\02 - PROCESSO\15335.101\15335.101_WP.asm");

            var plan = ProcessDrawingBuilder.Plan(part, ProcessMachine.Cdu);

            Assert.True(plan.NothingToDo);
            Assert.False(plan.Ready);
        }

        [Fact]
        public void NaoEPeca_recusa()
        {
            Directory.CreateDirectory(Path.Combine(_root, "MD-15335", "02 - PROCESSO"));
            string asm = Part(@"MD-15335\15335.asm");
            Assert.False(ProcessDrawingBuilder.Plan(asm, ProcessMachine.Cdu).Ready);
        }
    }
}
