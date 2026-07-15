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

        private FileLogSink _logSink;

        public override void OnConnection(SolidEdgeFramework.Application application,
            SolidEdgeFramework.SeConnectMode ConnectMode,
            SolidEdgeFramework.AddIn AddInInstance)
        {
            base.OnConnection(application, ConnectMode, AddInInstance);
            AddInEx.GuiVersion = 6; // incrementar ao mudar a ribbon (v6 = + Unir superfícies [isolado])

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
        }

        /// <summary>Carimbo dos binários EM MEMÓRIA (AddIn + Core) com a data de build — para
        /// confirmar no log QUAL assembly o Solid Edge está rodando. O SE mantém o add-in
        /// carregado no processo: recompilar SEM reiniciar o SE continua rodando o código
        /// ANTIGO (diagnóstico 2026-07-15 — o botão parecia não estender porque a versão em
        /// memória era pré-correção). Ver [[autoedm-decisions]].</summary>
        private static string BuildStamp()
        {
            string One(System.Reflection.Assembly a)
            {
                try { return $"{System.IO.Path.GetFileName(a.Location)} @ {System.IO.File.GetLastWriteTime(a.Location):yyyy-MM-dd HH:mm:ss}"; }
                catch { return a?.GetName()?.Name ?? "?"; }
            }
            return One(typeof(ElectrodeAddIn).Assembly) + "  |  " + One(typeof(AutoEDM.Electrode.SurfaceBlockBuilder).Assembly);
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
