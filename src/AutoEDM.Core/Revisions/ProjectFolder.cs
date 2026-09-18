using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoEDM.Revisions
{
    /// <summary>
    /// Códigos do projeto tirados do CAMINHO do arquivo (Carlos, 2026-09-17): a pasta do molde se
    /// chama "MD-14309 [PN-13972] [PA-10925]", então o cabeçalho da folha de revisões
    /// (MOLDE / PART NUMBERS / PRODUTO ACABADO / DIRETÓRIO) sai de graça, sem ninguém digitar.
    /// Nada aqui toca o Solid Edge — é só texto.
    /// </summary>
    public sealed class ProjectFolder
    {
        /// <summary>MD — código do molde ("14309"). Null = a pasta não segue o padrão.</summary>
        public string MoldCode { get; set; }

        /// <summary>PN — part number(s) do produto ("13972"). Null = não achou.</summary>
        public string PartNumbers { get; set; }

        /// <summary>PA — produto acabado ("10925"). Null = não achou.</summary>
        public string ProductCode { get; set; }

        /// <summary>Pasta do projeto (a que carrega os códigos no nome). Null = não achou.</summary>
        public string Directory { get; set; }

        /// <summary>Achou pelo menos o código do molde?</summary>
        public bool Found => MoldCode != null;

        // "MD-14309", "MD 14309", "MD14309" — o traço e o espaço são opcionais na prática.
        private static readonly Regex MoldRx = new Regex(@"\bMD[\s\-_]*(\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex PartNumberRx = new Regex(@"\bPN[\s\-_]*([0-9][0-9\-/\s.]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ProductRx = new Regex(@"\bPA[\s\-_]*([0-9][0-9\-/\s.]*)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Lê os códigos do caminho de um arquivo (ou de uma pasta). Procura do fim para o começo:
        /// vale a pasta mais PRÓXIMA do arquivo, porque um projeto pode estar dentro de outro
        /// ("...\MD-14309 [...]\MD-14310 [...]\peça.par" é do 14310). Nunca devolve null.
        /// </summary>
        public static ProjectFolder Parse(string path)
        {
            var found = new ProjectFolder();
            if (string.IsNullOrWhiteSpace(path)) return found;

            string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                Match mold = MoldRx.Match(parts[i]);
                if (!mold.Success) continue;

                found.MoldCode = mold.Groups[1].Value;
                found.PartNumbers = Capture(PartNumberRx, parts[i]);
                found.ProductCode = Capture(ProductRx, parts[i]);
                found.Directory = string.Join("\\", parts.Take(i + 1));
                // Caminho de rede ("\\servidor\pasta") perde as duas barras no Split — devolve.
                if (path.StartsWith("\\\\", StringComparison.Ordinal)) found.Directory = "\\\\" + found.Directory;
                return found;
            }
            return found;
        }

        private static string Capture(Regex rx, string text)
        {
            Match m = rx.Match(text);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}
