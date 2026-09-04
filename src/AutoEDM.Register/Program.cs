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

                    string deployedDll = Path.Combine(deployDir, "AutoEDM.AddIn.dll");
                    string codeBase = "file:///" + deployedDll.Replace('\\', '/');

                    // Se o pacote FOI EXTRAÍDO dentro do próprio deployDir, cada File.Copy
                    // seria origem == destino: lança, cai no catch e reportaria "travado",
                    // mandando o usuário fechar uma SE que nem está aberta. Nada a copiar.
                    if (SamePath(srcDir, deployDir))
                    {
                        Console.WriteLine("Deploy: rodando de dentro de " + deployDir + " — nada a copiar.");
                    }
                    else
                    {
                        // Copia TUDO menos os .pdb (não só *.dll): assim qualquer arquivo que
                        // passe a acompanhar o add-in (.config, template, ícone) vai junto em
                        // vez de ficar para trás em silêncio. Leva o próprio Register.exe, o
                        // que torna o deployDir autossuficiente para desinstalar depois.
                        int copied = 0, locked = 0;
                        bool addInCopied = false;
                        foreach (string f in Directory.GetFiles(srcDir))
                        {
                            if (f.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                            string name = Path.GetFileName(f);
                            try
                            {
                                File.Copy(f, Path.Combine(deployDir, name), true);
                                copied++;
                                if (name.Equals("AutoEDM.AddIn.dll", StringComparison.OrdinalIgnoreCase)) addInCopied = true;
                            }
                            catch { locked++; }
                        }
                        Console.WriteLine($"Deploy: {copied} arquivo(s) -> {deployDir}" +
                            (locked > 0 ? $"  ({locked} travado(s))" : ""));

                        // Se o dll principal não pôde ser sobrescrito, o deploy continua com o
                        // binário ANTIGO. Registrar e sair com 0 aqui seria o pior desfecho: o
                        // usuário vê "instalado com sucesso" e segue rodando a versão velha.
                        if (!addInCopied)
                        {
                            Console.Error.WriteLine();
                            Console.Error.WriteLine("FALHA: AutoEDM.AddIn.dll está EM USO — o add-in NÃO foi atualizado.");
                            Console.Error.WriteLine("Feche o Solid Edge por completo e rode de novo.");
                            return 1;
                        }
                    }

                    // Sem o dll no destino não adianta registrar: a SE carregaria um CodeBase
                    // inexistente e o add-in sumiria da ribbon sem erro visível.
                    if (!File.Exists(deployedDll))
                    {
                        Console.Error.WriteLine("FALHA: " + deployedDll + " não existe.");
                        Console.Error.WriteLine("Feche o Solid Edge e rode o instalador de novo.");
                        return 1;
                    }

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
                    Console.WriteLine("  CodeBase: " + deployedDll); // o que a SE vai carregar, não a pasta de origem
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

        /// <summary>Compara duas pastas ignorando barra final, caixa e caminho relativo.</summary>
        private static bool SamePath(string a, string b)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void TryDelete(RegistryKey parent, string subkey)
        {
            try { parent.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
