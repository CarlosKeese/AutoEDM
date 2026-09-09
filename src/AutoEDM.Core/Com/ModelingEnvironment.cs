using System;
using AutoEDM.Diagnostics;

namespace AutoEDM.Com
{
    /// <summary>Os dois ambientes de modelagem de uma peça (= <c>PartDocument.ModelingMode</c>).</summary>
    public enum ModelingEnv
    {
        /// <summary>Não interessa a este comando (leitura, montagem, diagnóstico).</summary>
        Any = 0,
        /// <summary>seModelingModeSynchronous — o padrão do template de peça do SE.</summary>
        Synchronous = 1,
        /// <summary>seModelingModeOrdered.</summary>
        Ordered = 2
    }

    /// <summary>
    /// PREMISSA DO PROJETO (Carlos, 2026-09-08 — depois do alojamento de O'ring deixar
    /// esboços presos entre os dois ambientes): <b>nenhum comando do AutoEDM troca o
    /// <c>ModelingMode</c> da peça do usuário.</b>
    ///
    /// O motivo é concreto, não estético. Sempre que o código alterna de ambiente no meio de
    /// uma operação acontecem duas coisas ruins:
    ///
    /// 1. <b>Esboço órfão.</b> <c>ProfileSets.Add()</c> cria um esboço ORDENADO mesmo numa peça
    ///    síncrona. Se um recurso SÍNCRONO consumir esse esboço, ele fica pendurado no nó
    ///    "Ordenado" do PathFinder sem dono — o usuário não consegue apagar pela interface.
    ///    Foi exatamente isso que sujou a peça do Carlos (ver <see cref="SketchScope"/>).
    /// 2. <b>Referência de face morta.</b> Trocar o modo faz a SE reconstruir o corpo: qualquer
    ///    Face/Edge lido ANTES da troca vira um proxy velho, e a chamada seguinte falha com
    ///    E_FAIL/RPC_E_DISCONNECTED sem dizer por quê. Era o bug do "Aplicar GAP" — ele
    ///    coletava as faces selecionadas, trocava para Ordenado e só então pintava/offsetava.
    ///
    /// Em vez de trocar, cada comando DECLARA o ambiente que exige. Se o documento estiver no
    /// outro, o botão fica desabilitado na ribbon e o clique explica o que fazer. Quem troca
    /// de ambiente é o usuário, no SE, conscientemente.
    /// </summary>
    public static class ModelingEnvironment
    {
        /// <summary>Lê o ambiente do documento. Devolve <see cref="ModelingEnv.Any"/> se o
        /// documento não tiver <c>ModelingMode</c> (montagem, desenho) ou não responder.</summary>
        public static ModelingEnv Read(dynamic doc)
        {
            if (doc == null) return ModelingEnv.Any;
            try
            {
                int m = (int)doc.ModelingMode;
                return m == 2 ? ModelingEnv.Ordered : m == 1 ? ModelingEnv.Synchronous : ModelingEnv.Any;
            }
            catch { return ModelingEnv.Any; }
        }

        /// <summary>true se o documento serve para um comando que exige <paramref name="required"/>.</summary>
        public static bool Matches(ModelingEnv required, ModelingEnv actual)
            => required == ModelingEnv.Any || required == actual;

        public static string Name(ModelingEnv env)
            => env == ModelingEnv.Ordered ? "ORDENADO"
             : env == ModelingEnv.Synchronous ? "SÍNCRONO"
             : "qualquer";

        /// <summary>Mensagem pronta para o usuário: por que o botão não roda e como trocar no SE.</summary>
        public static string WrongEnvironmentMessage(string command, ModelingEnv required, ModelingEnv actual)
        {
            // Any aqui não é "serve em qualquer um": é "não deu para ler o ambiente" — o
            // documento ativo não é uma peça, ou o ModelingMode não respondeu. Dizer
            // "está em qualquer" seria mentira e deixaria o usuário sem saber o que fazer.
            string onde = actual == ModelingEnv.Any
                ? "não deu para ler o ambiente deste documento (ele é mesmo uma peça?)"
                : $"esta peça está em {Name(actual)}";

            return $"\"{command}\" só funciona com a peça em modelagem {Name(required)} — {onde}.\n\n" +
                   "Troque no Solid Edge (aba Ferramentas → grupo Modelo → " +
                   (required == ModelingEnv.Ordered ? "Ordenado" : "Síncrono") +
                   ", ou pelo botão de ambiente na barra de status) e clique de novo.\n\n" +
                   "O AutoEDM NÃO troca o ambiente sozinho de propósito: a troca no meio da " +
                   "operação deixa esboços presos entre os dois ambientes (que você não " +
                   "consegue apagar) e invalida as faces já selecionadas.";
        }

        /// <summary>
        /// Confere o ambiente antes de uma operação do núcleo e loga. NÃO troca nada — é a
        /// rede de segurança para quando o comando é chamado por fora da ribbon (GUI externa,
        /// teste), já que na ribbon o botão nem fica clicável no ambiente errado.
        /// </summary>
        public static bool Require(dynamic doc, ModelingEnv required, string what)
        {
            ModelingEnv actual = Read(doc);
            if (Matches(required, actual)) return true;
            Log.Warn($"{what}: exige modelagem {Name(required)}, mas a peça está em {Name(actual)} — " +
                     "operação abortada (o AutoEDM não troca o ambiente sozinho).");
            return false;
        }
    }
}
