using System;
using System.Globalization;
using System.Runtime.InteropServices;
using SolidEdgeCommunity.AddIn;
using AutoEDM.Diagnostics;

namespace AutoEDM.AddIn
{
    /// <summary>
    /// Add-in COM do AutoEDM para o Solid Edge. Adiciona a aba/ribbon "AutoEDM" com
    /// o botão "Criar eletrodos", que dispara a rotina de extração sobre a montagem
    /// ATIVA — reaproveitando o mesmo núcleo (ElectrodeBuilder) que a GUI de debug.
    ///
    /// In-process: recebemos o Application direto no OnConnection (sem ROT). O padrão
    /// (SolidEdgeAddIn, RibbonController, ComRegister/Unregister) segue o framework
    /// SolidEdge.Community.AddIn.
    /// </summary>
    [ComVisible(true)]
    [Guid("B8F2B1E6-3C7A-4E2D-9A11-7E5A2C4D9F01")]
    [ProgId("AutoEDM.AddIn.ElectrodeAddIn")]
    public class ElectrodeAddIn : SolidEdgeCommunity.AddIn.SolidEdgeAddIn
    {
        /// <summary>Instância corrente, para a ribbon alcançar o Application.</summary>
        public static ElectrodeAddIn Current { get; private set; }

        /// <summary>Application do Solid Edge (in-process).</summary>
        public SolidEdgeFramework.Application App { get; private set; }

        /// <summary>Caminho do arquivo de log desta sessão, para mostrar ao usuário em mensagens de erro.</summary>
        public string LogPath => _logSink?.FilePath;

        private FileLogSink _logSink;

        public override void OnConnection(SolidEdgeFramework.Application application,
            SolidEdgeFramework.SeConnectMode ConnectMode,
            SolidEdgeFramework.AddIn AddInInstance)
        {
            // PRIMEIRA linha, antes de qualquer coisa que possa carregar dependência: hospedados
            // pelo Edge.exe não temos bindingRedirect, e sem este gancho o System.Text.Json
            // morre no construtor estático. Ver AssemblyRedirect.
            AssemblyRedirect.Install();

            base.OnConnection(application, ConnectMode, AddInInstance);
            AddInEx.GuiVersion = 16; // incrementar ao mudar a ribbon (v16 = "Lista de modificações" em Relatórios)

            Current = this;
            App = application;
            // In-process, a pasta do processo é o Program Files da SE (sem escrita).
            // Loga numa pasta do usuário: %LOCALAPPDATA%\AutoEDM\logs.
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutoEDM", "logs");
                _logSink = new FileLogSink(dir);
                Log.Info($"Log do add-in em: {_logSink.FilePath}");
            }
            catch { /* log em arquivo é best-effort */ }
            Log.Info("AutoEDM add-in conectado ao Solid Edge.");
            Log.Info("Build carregado: " + BuildStamp());
            // O gancho de redirecionamento roda antes do log existir; o que ele fez sai agora.
            foreach (var linha in AssemblyRedirect.Report) Log.Info(linha);
        }

        /// <summary>Carimbo dos binários EM MEMÓRIA (AddIn + Core) com a data de build — para
        /// confirmar no log QUAL assembly o Solid Edge está rodando. O SE mantém o add-in
        /// carregado no processo: recompilar SEM reiniciar o SE continua rodando o código
        /// ANTIGO (diagnóstico 2026-07-15 — o botão parecia não estender porque a versão em
        /// memória era pré-correção). Ver [[autoedm-decisions]].</summary>
        private static string BuildStamp()
        {
            // O CAMINHO entra no carimbo, não só o nome: a SE carrega o add-in do CodeBase
            // registrado (%LOCALAPPDATA%\AutoEDM\addin), e não da pasta de build. Sem o
            // caminho, um log de build velho é indistinguível de um novo — foi assim que uma
            // rodada de teste inteira se perdeu em 2026-09-04, lendo o log do DLL anterior.
            string One(System.Reflection.Assembly a)
            {
                try { return $"{a.Location} @ {System.IO.File.GetLastWriteTime(a.Location):yyyy-MM-dd HH:mm:ss}"; }
                catch { return a?.GetName()?.Name ?? "?"; }
            }
            // Versão do PACOTE (InformationalVersion, gravada por Directory.Build.props com
            // -p:AutoEdmVersion). A AssemblyVersion é fixa em 1.0.0.0 por causa do registro
            // COM, então só este carimbo diz qual release a máquina instalou.
            string ver;
            try
            {
                var attr = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(ElectrodeAddIn).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                ver = attr?.InformationalVersion ?? "?";
                // O SDK anexa a revisão do git: "2026.8.14+<sha de 40 chars>". Encurta o
                // sha para 7 — continua identificando o commit e cabe numa linha de log.
                int plus = ver.IndexOf('+');
                if (plus > 0 && ver.Length > plus + 8) ver = ver.Substring(0, plus + 8);
            }
            catch { ver = "?"; }
            return $"v{ver}  |  " + One(typeof(ElectrodeAddIn).Assembly) + "  |  " + One(typeof(AutoEDM.Electrode.SurfaceBlockBuilder).Assembly);
        }

        public override void OnConnectToEnvironment(SolidEdgeFramework.Environment environment, bool firstTime)
        {
        }

        public override void OnCreateRibbon(RibbonController controller, Guid environmentCategory, bool firstTime)
        {
            controller.Add<ElectrodeRibbon>(environmentCategory, firstTime);
        }

        public override void OnDisconnection(SolidEdgeFramework.SeDisconnectMode DisconnectMode)
        {
            // Antes do log fechar: derrubar a ponte MCP ainda registra o encerramento no arquivo.
            ElectrodeRibbon.ShutdownMcp();
            try { _logSink?.Dispose(); } catch { }
            App = null;
            Current = null;
        }

        // --- registro COM (regasm chama estes) ---------------------------------

        [ComRegisterFunction]
        public static void OnRegister(Type t)
        {
            var settings = new RegistrationSettings(t) { Enabled = true };
            settings.Environments.Add(SolidEdgeSDK.EnvironmentCategories.AllDocumentEnvrionments);

            var pt = CultureInfo.GetCultureInfo(1046); // pt-BR
            settings.Titles.Add(pt, "AutoEDM — Eletrodos");
            settings.Summaries.Add(pt, "Extração automática de eletrodos de EDM.");

            var en = CultureInfo.GetCultureInfo(1033); // en-US
            settings.Titles.Add(en, "AutoEDM — Electrodes");
            settings.Summaries.Add(en, "Automatic EDM electrode extraction.");

            Register(settings);
        }

        [ComUnregisterFunction]
        public static void OnUnregister(Type t) => Unregister(t);
    }
}
