using AutoEDM.Com;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// A premissa "cada comando declara o ambiente e o AutoEDM nunca troca" vale tanto quanto a
    /// leitura do ambiente for confiável. Estes testes fecham o cerco pelo lado que dá para
    /// exercitar sem o Solid Edge aberto: a leitura do <c>ModelingMode</c> por late binding e a
    /// regra de compatibilidade.
    /// </summary>
    public class ModelingEnvironmentTests
    {
        /// <summary>Dublê de PartDocument: só precisa ter <c>ModelingMode</c>, que é como o
        /// código real fala com o documento (via <c>dynamic</c>).</summary>
        public sealed class FakePartDoc
        {
            public int ModelingMode { get; set; }
        }

        /// <summary>Dublê de montagem/desenho: NÃO tem ModelingMode — o acesso lança.</summary>
        public sealed class FakeAssemblyDoc
        {
        }

        [Theory]
        [InlineData(1, ModelingEnv.Synchronous)]
        [InlineData(2, ModelingEnv.Ordered)]
        public void Read_traduzOModelingModeDoDocumento(int mode, ModelingEnv esperado)
        {
            Assert.Equal(esperado, ModelingEnvironment.Read(new FakePartDoc { ModelingMode = mode }));
        }

        [Fact]
        public void Read_semModelingMode_naoLanca_eDaAny()
        {
            // Montagem e desenho não têm ModelingMode; o gate precisa tratar isso como
            // "ambiente não se aplica" em vez de estourar RuntimeBinderException na ribbon.
            Assert.Equal(ModelingEnv.Any, ModelingEnvironment.Read(new FakeAssemblyDoc()));
            Assert.Equal(ModelingEnv.Any, ModelingEnvironment.Read(null));
        }

        [Fact]
        public void Read_modoDesconhecido_naoViraSincronoPorEngano()
        {
            // Um valor fora de {1,2} não pode cair no síncrono por descuido: seria liberar
            // "Unir superfícies" num ambiente que ninguém conferiu.
            Assert.Equal(ModelingEnv.Any, ModelingEnvironment.Read(new FakePartDoc { ModelingMode = 7 }));
        }

        [Fact]
        public void Matches_ambienteExigidoTemDeBaterExatamente()
        {
            Assert.True(ModelingEnvironment.Matches(ModelingEnv.Ordered, ModelingEnv.Ordered));
            Assert.True(ModelingEnvironment.Matches(ModelingEnv.Synchronous, ModelingEnv.Synchronous));
            Assert.False(ModelingEnvironment.Matches(ModelingEnv.Ordered, ModelingEnv.Synchronous));
            Assert.False(ModelingEnvironment.Matches(ModelingEnv.Synchronous, ModelingEnv.Ordered));

            // Comando que não depende do ambiente (montagem, diagnóstico) roda em qualquer um.
            Assert.True(ModelingEnvironment.Matches(ModelingEnv.Any, ModelingEnv.Synchronous));
            Assert.True(ModelingEnvironment.Matches(ModelingEnv.Any, ModelingEnv.Ordered));
            Assert.True(ModelingEnvironment.Matches(ModelingEnv.Any, ModelingEnv.Any));

            // ...mas um comando que EXIGE ambiente não roda num documento sem ambiente.
            Assert.False(ModelingEnvironment.Matches(ModelingEnv.Ordered, ModelingEnv.Any));
        }

        [Fact]
        public void Require_naoTrocaOAmbienteDoDocumento()
        {
            // O ponto da premissa inteira: recusar, nunca corrigir a peça do usuário por conta
            // própria. Se algum dia alguém reintroduzir o `ModelingMode = 2`, este teste cai.
            var doc = new FakePartDoc { ModelingMode = 1 };

            Assert.False(ModelingEnvironment.Require(doc, ModelingEnv.Ordered, "Aplicar GAP"));
            Assert.Equal(1, doc.ModelingMode);

            Assert.True(ModelingEnvironment.Require(doc, ModelingEnv.Synchronous, "Unir superfícies"));
            Assert.Equal(1, doc.ModelingMode);
        }

        [Fact]
        public void WrongEnvironmentMessage_dizOComandoOsDoisAmbientesEComoTrocar()
        {
            string msg = ModelingEnvironment.WrongEnvironmentMessage(
                "APLICAR GAP", ModelingEnv.Ordered, ModelingEnv.Synchronous);

            Assert.Contains("APLICAR GAP", msg);
            Assert.Contains("ORDENADO", msg);   // o que ele precisa
            Assert.Contains("SÍNCRONO", msg);   // onde ele está
            Assert.Contains("Ordenado", msg);   // o caminho no menu do SE
        }
    }
}
