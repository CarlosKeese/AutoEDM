using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AutoEDM.AddIn
{
    /// <summary>
    /// O bindingRedirect que o add-in NÃO pode ter, feito à mão.
    ///
    /// O PROBLEMA (diagnosticado 2026-09-10, logs 09-09 e 09-10). Toda sessão do add-in
    /// falhava ao gravar o config.json com "O inicializador de tipo de
    /// 'System.Text.Json.JsonSerializer' acionou uma exceção" — a configuração inteira
    /// (prefixo, tabela de Ra, cores, folgas) nunca era lida nem gravada, e o AutoEDM rodava
    /// sempre nos defaults sem ninguém perceber, porque o código trata isso como best-effort.
    ///
    /// A CAUSA. As dependências do System.Text.Json 8.0.5 pedem versões antigas umas das
    /// outras: <c>System.Memory 4.0.1.2</c> e <c>System.Threading.Tasks.Extensions 4.2.0.1</c>
    /// referenciam <c>System.Runtime.CompilerServices.Unsafe 4.0.4.1</c>, mas o que o NuGet
    /// implanta na pasta é o <c>6.0.0.0</c>. Num .exe normal o MSBuild resolve isso gerando
    /// bindingRedirects no App.exe.config — e é justamente isso que não existe aqui: o add-in
    /// é hospedado in-process pelo <c>Edge.exe</c>, então quem vale é o .config DA SOLID EDGE,
    /// não o nosso. O binder não acha a 4.0.4.1, o load falha, e o construtor estático do
    /// JsonSerializer morre junto.
    ///
    /// A SOLUÇÃO. <see cref="AppDomain.AssemblyResolve"/> dispara justamente quando o binder
    /// desiste — é o gancho onde dá para dizer "pode usar a que está na pasta, a versão não
    /// importa". Resolver por NOME SIMPLES é exatamente o que um bindingRedirect faz, e cobre
    /// de uma vez qualquer par de dependências que venha a divergir no futuro, não só este.
    ///
    /// Por que não tirar o System.Text.Json do projeto: o mesmo tropeço voltaria na próxima
    /// dependência transitiva que o add-in ganhasse. O gancho conserta a CLASSE do problema.
    /// </summary>
    internal static class AssemblyRedirect
    {
        private static bool _installed;
        private static string _folder;
        private static readonly List<string> _log = new List<string>();

        /// <summary>
        /// O que o gancho resolveu nesta sessão — para o <see cref="ElectrodeAddIn"/> despejar
        /// no log assim que o arquivo de log existir. O gancho roda ANTES do log estar de pé
        /// (é esse o ponto: ele tem de estar armado antes de qualquer carga), então o que ele
        /// tem a contar fica guardado aqui até alguém poder escrever.
        /// </summary>
        public static IEnumerable<string> Report => _log;

        /// <summary>
        /// Arma o gancho. Idempotente e sem efeito nenhum além de assinar o evento — pode ser
        /// a primeiríssima linha do OnConnection, e deve ser: qualquer carga que aconteça antes
        /// disso já falha sem chance de conserto.
        /// </summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            try { _folder = Path.GetDirectoryName(typeof(AssemblyRedirect).Assembly.Location); }
            catch { _folder = null; }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _log.Add($"Redirecionamento de assembly armado (pasta: {_folder ?? "?"}).");
        }

        private static System.Reflection.Assembly Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName wanted;
            try { wanted = new AssemblyName(args.Name); }
            catch { return null; }

            // Satélites de recurso não são nosso caso: devolver null deixa o CLR seguir o
            // caminho normal (cair no idioma neutro) em vez de procurar um .dll que não existe.
            if (wanted.Name != null && wanted.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            // 1) Já tem essa assembly carregada, em OUTRA versão? Serve — é literalmente o que
            //    o bindingRedirect faria, e evita duas cópias da mesma coisa no processo.
            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(loaded.GetName().Name, wanted.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Add($"Redirecionado (já carregada): {wanted.Name} {wanted.Version} -> {loaded.GetName().Version}.");
                    return loaded;
                }
            }

            // 2) Senão, pega da pasta do add-in pelo nome simples, ignorando a versão pedida.
            if (_folder == null) return null;
            try
            {
                string path = Path.Combine(_folder, wanted.Name + ".dll");
                if (!File.Exists(path)) return null;

                var found = System.Reflection.Assembly.LoadFrom(path);
                _log.Add($"Redirecionado (da pasta): {wanted.Name} {wanted.Version} -> {found.GetName().Version}.");
                return found;
            }
            catch (Exception ex)
            {
                // Nunca deixar o gancho lançar: uma exceção aqui vira falha de carga do
                // chamador, que é o problema que viemos consertar.
                _log.Add($"Redirecionamento de {wanted.Name} falhou: {ex.Message}");
                return null;
            }
        }
    }
}
