using System;
using System.IO;
using AutoEDM.Reverse;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>As partes puras do lote: nome de arquivo e caminho que nunca sobrescreve.</summary>
    public class MeshToSolidBatchTests
    {
        [Fact]
        public void Nome_de_arquivo_troca_caractere_proibido_e_mantem_o_resto()
        {
            Assert.Equal("Rack - Corpo_1 _a_b_", MeshToSolidBatch.SafeName("Rack - Corpo:1 <a|b>"));
            Assert.Equal("Rack_Anghinoni (1) - sólidos", MeshToSolidBatch.SafeName("Rack_Anghinoni (1) - sólidos"));
        }

        [Fact]
        public void Caminho_livre_nunca_aponta_para_arquivo_que_ja_existe()
        {
            string dir = Path.Combine(Path.GetTempPath(), "autoedm-lote-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            try
            {
                string first = MeshToSolidBatch.FreePath(dir, "Peça", ".par");
                Assert.Equal(Path.Combine(dir, "Peça.par"), first);
                File.WriteAllText(first, "");

                string second = MeshToSolidBatch.FreePath(dir, "Peça", ".par");
                Assert.Equal(Path.Combine(dir, "Peça (2).par"), second);
                File.WriteAllText(second, "");

                Assert.Equal(Path.Combine(dir, "Peça (3).par"), MeshToSolidBatch.FreePath(dir, "Peça", ".par"));
                // Outra extensão não colide.
                Assert.Equal(Path.Combine(dir, "Peça.asm"), MeshToSolidBatch.FreePath(dir, "Peça", ".asm"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
