using System.Text.RegularExpressions;

namespace AutoEDM.Revisions
{
    /// <summary>
    /// O nome que o Carlos dá ao GRUPO da árvore ordenada — "Rev.2" — e como tirar o número dele.
    /// Tolerante à digitação (ponto, espaço, traço, maiúscula, "Revisão 2"), porque quem digita é
    /// gente e o relatório não pode perder uma peça alterada por causa de um espaço.
    /// Lógica pura, sem COM — o que fala com o Solid Edge é o <see cref="RevisionScanner"/>.
    /// </summary>
    public static class RevisionName
    {
        // "Rev.2", "Rev 2", "REV-2", "Revisão 3", "rev2". O que vier depois do número é ignorado:
        // se um dia ele escrever "Rev.2 - molas da extração", a descrição não atrapalha o número.
        private static readonly Regex Rx =
            new Regex(@"^\s*rev(is[aã]o)?\s*[\.\-_]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Número da revisão no nome do grupo. false = não é um grupo de revisão.</summary>
        public static bool TryParse(string groupName, out int revision)
        {
            revision = 0;
            if (string.IsNullOrWhiteSpace(groupName)) return false;
            Match m = Rx.Match(groupName);
            return m.Success && int.TryParse(m.Groups[2].Value, out revision);
        }

        /// <summary>
        /// Igual ao <see cref="TryParse"/>, mas aceita também o NÚMERO SOZINHO ("2", "02"), que é
        /// como a revisão aparece numa PROPRIEDADE do arquivo — lá o campo já se chama revisão e
        /// ninguém repete a palavra dentro do valor. Zero, vazio e negativo NÃO contam: projeto
        /// novo nasce com tudo em 0 ou vazio, e é isso que separa a peça nova das demais
        /// (Carlos, 2026-09-18).
        /// </summary>
        public static bool TryParseValue(string propertyValue, out int revision)
        {
            revision = 0;
            if (string.IsNullOrWhiteSpace(propertyValue)) return false;

            string text = propertyValue.Trim();
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out revision))
                return revision > 0;

            return TryParse(text, out revision) && revision > 0;
        }

        // "1 - Ajustar a chaveta", "2 Furo do pino", "3) Cortar o perfil", "4. Rebaixo".
        private static readonly Regex OperationRx =
            new Regex(@"^\s*(\d+)\s*[\-–—.:)]?\s*(.*)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// O NÚMERO com que o Carlos batiza a feature dentro do grupo da revisão ("1 - Ajustar a
        /// chaveta"). Só a feature numerada vira ação indicada e ganha chamada na miniatura — é
        /// assim que ele separa o que a oficina precisa ver do resto do grupo (2026-09-18).
        /// false = feature com o nome automático da Solid Edge ("Recorte 6"), que não é ação.
        /// </summary>
        public static bool TryParseOperation(string featureName, out int number, out string text)
        {
            number = 0;
            text = null;
            if (string.IsNullOrWhiteSpace(featureName)) return false;

            Match m = OperationRx.Match(featureName);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out number) || number <= 0) return false;

            text = m.Groups[2].Value.Trim();
            return true;
        }

        /// <summary>
        /// O texto que o usuário escreveu DEPOIS do número, se escreveu ("Rev.2 - molas" → "molas").
        /// Serve de rascunho da descrição na janela; null quando o grupo só tem o "Rev.N".
        /// </summary>
        public static string DescriptionAfterNumber(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return null;
            Match m = Rx.Match(groupName);
            if (!m.Success) return null;
            string rest = groupName.Substring(m.Length).Trim().TrimStart('-', '=', ':', '–').Trim();
            return rest.Length == 0 ? null : rest;
        }
    }
}
