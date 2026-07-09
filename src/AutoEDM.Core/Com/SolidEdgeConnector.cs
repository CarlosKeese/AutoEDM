using System;
using System.IO;
using System.Runtime.InteropServices;
using AutoEDM.Diagnostics;

namespace AutoEDM.Com
{
    /// <summary>
    /// Owns the connection to a running (or newly started) Solid Edge instance and
    /// opens documents through the COM Automation API.
    ///
    /// Late binding (dynamic) is used so this compiles and runs without the Solid
    /// Edge type libraries present. When early-binding interops are added, the
    /// returned <see cref="object"/> handles can be cast to
    /// SolidEdgeFramework.Application / SolidEdgePart.PartDocument.
    ///
    /// COM object model reference:
    ///   ProgID                     -> "SolidEdge.Application"
    ///   SolidEdgeFramework.Application
    ///     .Documents.Open(path)    -> SolidEdgeFramework.SolidEdgeDocument
    ///     .Documents.Add(progId)   -> new document from template
    ///
    /// Threading: Solid Edge automation must run on an STA thread. The console
    /// entry point sets [STAThread]; a UI thread is STA by default.
    /// </summary>
    public sealed class SolidEdgeConnector : IDisposable
    {
        private const string ApplicationProgId = "SolidEdge.Application";

        /// <summary>
        /// The Solid Edge Application COM object (late-bound). Null until Connect().
        /// </summary>
        public dynamic Application { get; private set; }

        /// <summary>True if this connector started the Solid Edge process itself.</summary>
        public bool StartedByUs { get; private set; }

        private bool _messageFilterRegistered;
        private bool _attached; // envolve um Application externo (add-in in-process): não libera no Dispose

        /// <summary>
        /// Envolve uma instância de Application JÁ obtida — caso do add-in in-process,
        /// que recebe o Application direto no OnConnection (sem ROT). Não registra
        /// message filter (a SE já bombeia mensagens na própria thread) e NÃO libera o
        /// Application no Dispose (ele pertence à Solid Edge).
        /// </summary>
        public static SolidEdgeConnector Attach(dynamic application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            return new SolidEdgeConnector { Application = application, _attached = true };
        }

        /// <summary>
        /// Connects to a running Solid Edge instance, or starts one if none is
        /// running and <paramref name="startIfNotRunning"/> is true.
        /// </summary>
        /// <param name="startIfNotRunning">Launch Solid Edge if not already open.</param>
        /// <param name="makeVisible">Ensure the application window is visible.</param>
        public dynamic Connect(bool startIfNotRunning = true, bool makeVisible = true)
        {
            EnsureMessageFilter();

            Application = TryGetActiveInstance();

            if (Application == null)
            {
                if (!startIfNotRunning)
                    throw new InvalidOperationException(
                        "Solid Edge is not running and startIfNotRunning was false. " +
                        "Open Solid Edge with a valid license and retry.");

                Application = StartNewInstance();
                StartedByUs = true;
            }

            try
            {
                if (makeVisible)
                {
                    Application.Visible = true;
                    // Bring to front without stealing focus aggressively.
                    Application.DisplayAlerts = false;
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: some headless configs reject Visible/DisplayAlerts.
                Log.Warn($"Could not set application display flags: {ex.Message}");
            }

            LogApplicationInfo();
            return Application;
        }

        /// <summary>
        /// Opens a document (typically a .par cavity file) and returns the
        /// document COM object. The caller is responsible for closing it.
        /// </summary>
        public dynamic OpenDocument(string path, bool visible = true)
        {
            if (Application == null)
                throw new InvalidOperationException("Not connected. Call Connect() first.");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Document not found.", path);

            Log.Info($"Opening document: {path}");
            dynamic documents = Application.Documents;

            // Documents.Open has several overloads across SE versions. The single
            // string overload is the most portable.
            dynamic doc = documents.Open(path);

            if (visible)
            {
                try { Application.Visible = true; } catch { /* headless */ }
            }

            Log.Info($"Opened: {SafeName(doc)} (Type: {SafeDocType(doc)})");
            return doc;
        }

        /// <summary>
        /// Creates a new document from a template path (e.g. an electrode .par
        /// template) via Documents.Add. Falls back to the version-default template
        /// when <paramref name="templatePath"/> is null/empty.
        /// </summary>
        public dynamic CreateDocumentFromTemplate(string templatePath)
        {
            if (Application == null)
                throw new InvalidOperationException("Not connected. Call Connect() first.");

            dynamic documents = Application.Documents;

            if (string.IsNullOrWhiteSpace(templatePath))
            {
                Log.Info("Creating new Part document from default template.");
                // "SolidEdge.PartDocument" is the ProgID for a default Part.
                return documents.Add("SolidEdge.PartDocument");
            }

            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Template not found.", templatePath);

            Log.Info($"Creating new document from template: {templatePath}");
            return documents.Add("SolidEdge.PartDocument", templatePath);
        }

        /// <summary>
        /// Documento ativo no Solid Edge (a montagem que o operador tem aberta),
        /// ou null se não houver nenhum. Não abre nada — usa o que já está ativo.
        /// </summary>
        public dynamic GetActiveDocument()
        {
            if (Application == null)
                throw new InvalidOperationException("Not connected. Call Connect() first.");
            try
            {
                dynamic doc = Application.ActiveDocument;
                if (doc == null) { Log.Warn("Nenhum documento ativo no Solid Edge."); return null; }
                Log.Info($"Documento ativo: {SafeName(doc)} (Type: {SafeDocType(doc)})");
                return doc;
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível obter o documento ativo: {ex.Message}");
                return null;
            }
        }

        // --- connection helpers -------------------------------------------------

        private static dynamic TryGetActiveInstance()
        {
            try
            {
                // Portable across .NET Framework and .NET 5+/10 (Marshal.GetActiveObject
                // was removed from modern .NET). Reads the running-object table for an
                // already-open Solid Edge.
                object app = ComInterop.GetActiveObject(ApplicationProgId);
                Log.Info("Connected to a running Solid Edge instance.");
                return app;
            }
            catch (COMException)
            {
                // MK_E_UNAVAILABLE (0x800401E3): no running instance registered,
                // or the ProgID is not registered on this machine.
                return null;
            }
        }

        private static dynamic StartNewInstance()
        {
            Log.Info("No running Solid Edge found. Starting a new instance...");
            Type t = Type.GetTypeFromProgID(ApplicationProgId);
            if (t == null)
                throw new InvalidOperationException(
                    "Solid Edge COM server is not registered on this machine. " +
                    "Verify Solid Edge 2023/2026 is installed.");

            object app = Activator.CreateInstance(t);
            return app;
        }

        private void EnsureMessageFilter()
        {
            if (_messageFilterRegistered) return;
            OleMessageFilter.Register();
            _messageFilterRegistered = true;
            Log.Info("OLE message filter registered (auto-retry on busy server).");
        }

        private void LogApplicationInfo()
        {
            try
            {
                string version = Application.Version;
                string name = Application.Name;
                Log.Info($"{name} version {version}");
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not read application version: {ex.Message}");
            }
        }

        private static string SafeName(dynamic doc)
        {
            try { return doc.Name; } catch { return "<unknown>"; }
        }

        private static string SafeDocType(dynamic doc)
        {
            try { return doc.Type.ToString(); } catch { return "<unknown>"; }
        }

        // --- teardown -----------------------------------------------------------

        /// <summary>
        /// Releases the COM references. Does NOT quit Solid Edge unless this
        /// connector started it and <paramref name="quitIfStartedByUs"/> is true.
        /// </summary>
        public void Disconnect(bool quitIfStartedByUs = false)
        {
            if (Application == null) return;

            try
            {
                if (StartedByUs && quitIfStartedByUs)
                {
                    Log.Info("Quitting Solid Edge instance that we started.");
                    Application.Quit();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error while quitting Solid Edge: {ex.Message}");
            }
            finally
            {
                // Só libera o Application se fomos nós que o obtivemos. Num add-in
                // in-process (Attach), o Application pertence à Solid Edge.
                if (!_attached)
                {
                    try { Marshal.FinalReleaseComObject(Application); } catch { /* ignore */ }
                }
                Application = null;

                if (_messageFilterRegistered)
                {
                    OleMessageFilter.Revoke();
                    _messageFilterRegistered = false;
                }
            }
        }

        public void Dispose() => Disconnect(quitIfStartedByUs: false);
    }
}
