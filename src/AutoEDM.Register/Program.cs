using System;
using System.IO;
using Microsoft.Win32;
using AutoEDM.AddIn;

namespace AutoEDM.Register
{
    /// <summary>
    /// Registra o add-in do AutoEDM SÓ para o usuário atual (HKCU\Software\Classes),
    /// sem privilégio de administrador. Replica as chaves que o regasm (.NET COM) e o
    /// framework SolidEdge.Community.AddIn escreveriam em HKCR — mas em HKCU, que o
    /// COM e a Solid Edge do usuário consultam antes do HKLM.
    ///
    /// Chaves mínimas para carregar: CLSID\{guid}\InprocServer32 (mscoree + assembly)
    /// + Implemented Categories\{CATID_SolidEdgeAddIn} + AutoConnect. Environment
    /// Categories e título são cosméticos/comportamentais.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool unregister = args.Length > 0 &&
                (args[0].Equals("/u", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("u", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("unregister", StringComparison.OrdinalIgnoreCase));

            try
            {
                Type t = typeof(ElectrodeAddIn);
                string clsid = t.GUID.ToString("B").ToUpperInvariant();
                string progId = t.FullName;

                using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                {
                    if (unregister)
                    {
                        TryDelete(classes, @"CLSID\" + clsid);
                        TryDelete(classes, progId);
                        Console.WriteLine("Add-in REMOVIDO do usuário (HKCU): " + clsid);
                        Console.WriteLine("Reinicie a Solid Edge para descarregar.");
                        return 0;
                    }

                    var asm = t.Assembly;
                    string asmFull = asm.FullName;
                    string asmVer = asm.GetName().Version.ToString();
                    string runtime = typeof(object).Assembly.ImageRuntimeVersion; // v4.0.30319
                    const string title = "AutoEDM — Eletrodos";

                    // DEPLOY: copia o add-in + dependências para uma pasta ESTÁVEL,
                    // fora da árvore de build, e registra a partir dela. Assim a SE
                    // trava o dll do deploy (não o de bin/) e você recompila com a SE
                    // aberta. Só precisa fechar a SE quando for ATUALIZAR o add-in.
                    string srcDir = Path.GetDirectoryName(asm.Location);
                    string deployDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AutoEDM", "addin");
                    Directory.CreateDirectory(deployDir);
                    int copied = 0, locked = 0;
                    foreach (string f in Directory.GetFiles(srcDir, "*.dll"))
                    {
                        try { File.Copy(f, Path.Combine(deployDir, Path.GetFileName(f)), true); copied++; }
                        catch { locked++; }
                    }
                    Console.WriteLine($"Deploy: {copied} dll(s) -> {deployDir}" +
                        (locked > 0 ? $"  ({locked} travado(s): feche a SE para atualizar o add-in)" : ""));

                    string deployedDll = Path.Combine(deployDir, "AutoEDM.AddIn.dll");
                    string codeBase = "file:///" + deployedDll.Replace('\\', '/');

                    using (RegistryKey k = classes.CreateSubKey(@"CLSID\" + clsid))
                    {
                        k.SetValue(null, title);

                        // .NET COM in-process server (equivale a regasm /codebase).
                        using (RegistryKey ip = k.CreateSubKey("InprocServer32"))
                        {
                            ip.SetValue(null, "mscoree.dll");
                            ip.SetValue("ThreadingModel", "Both");
                            ip.SetValue("Class", t.FullName);
                            ip.SetValue("Assembly", asmFull);
                            ip.SetValue("RuntimeVersion", runtime);
                            ip.SetValue("CodeBase", codeBase);
                            using (RegistryKey v = ip.CreateSubKey(asmVer))
                            {
                                v.SetValue("Class", t.FullName);
                                v.SetValue("Assembly", asmFull);
                                v.SetValue("RuntimeVersion", runtime);
                                v.SetValue("CodeBase", codeBase);
                            }
                        }
                        using (RegistryKey pk = k.CreateSubKey("ProgId"))
                            pk.SetValue(null, progId);

                        // Chaves de add-in do Solid Edge (framework SolidEdgeCommunity).
                        k.SetValue("AutoConnect", 1, RegistryValueKind.DWord);

                        Guid catid = new Guid(SolidEdgeSDK.CATID.SolidEdgeAddIn);
                        k.CreateSubKey(@"Implemented Categories\" + catid.ToString("B").ToUpperInvariant()).Dispose();

                        Guid env = SolidEdgeSDK.EnvironmentCategories.AllDocumentEnvrionments;
                        k.CreateSubKey(@"Environment Categories\" + env.ToString("B").ToUpperInvariant()).Dispose();
                    }

                    using (RegistryKey pk = classes.CreateSubKey(progId))
                    {
                        pk.SetValue(null, title);
                        using (RegistryKey pc = pk.CreateSubKey("CLSID"))
                            pc.SetValue(null, clsid);
                    }

                    Console.WriteLine("Add-in REGISTRADO no usuário (HKCU), sem admin.");
                    Console.WriteLine("  CLSID:    " + clsid);
                    Console.WriteLine("  CodeBase: " + asm.Location);
                    Console.WriteLine("Reinicie a Solid Edge. Aba 'AutoEDM' > 'Criar eletrodos'.");
                    Console.WriteLine("Para remover: AutoEDM.Register.exe /u");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FALHA: " + ex.Message);
                return 1;
            }
        }

        private static void TryDelete(RegistryKey parent, string subkey)
        {
            try { parent.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
