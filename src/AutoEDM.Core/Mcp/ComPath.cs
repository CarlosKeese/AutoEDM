using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace AutoEDM.Mcp
{
    /// <summary>Um passo de um caminho COM: <c>Models</c>, <c>Item(1)</c>, <c>Faces(1)</c>.</summary>
    public sealed class ComPathSegment
    {
        public string Name { get; set; }

        /// <summary>null = sem parênteses (propriedade simples); vazio = <c>Nome()</c>.</summary>
        public object[] Args { get; set; }

        public override string ToString()
        {
            if (Args == null) return Name;
            var parts = new List<string>();
            foreach (object a in Args)
                parts.Add(a is string s ? "\"" + s + "\"" : Convert.ToString(a, CultureInfo.InvariantCulture));
            return Name + "(" + string.Join(", ", parts) + ")";
        }
    }

    /// <summary>
    /// Caminho de navegação na API COM da Solid Edge, escrito como texto:
    /// <c>doc.Models.Item(1).Body.Faces(1).Item(3)</c>. É o que deixa o agente andar pelo modelo
    /// SEM uma ferramenta dedicada para cada pergunta.
    ///
    /// A análise do texto é lógica pura (testável sem SE). A navegação usa late binding por
    /// <c>InvokeMember</c>; no modo somente-leitura cada passo é LEITURA DE PROPRIEDADE
    /// (<c>DISPATCH_PROPERTYGET</c>) — a única chamada de método aceita é <c>Item</c>, que em
    /// várias coleções é método e não propriedade. Assim um caminho de leitura não tem como
    /// executar <c>Delete</c>, <c>Save</c> ou <c>Close</c> por engano.
    /// </summary>
    public static class ComPath
    {
        /// <summary>Quebra o caminho em raiz + passos. Lança <see cref="FormatException"/> com a
        /// posição do erro — a mensagem vai para o agente.</summary>
        public static List<ComPathSegment> Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new FormatException("Caminho vazio.");
            var result = new List<ComPathSegment>();
            int i = 0;
            string s = path.Trim();

            while (i < s.Length)
            {
                // [n] ou ["nome"] logo depois de um passo = Item(...)
                if (s[i] == '[')
                {
                    if (result.Count == 0) throw new FormatException("O caminho não pode começar por '['.");
                    result.Add(new ComPathSegment { Name = "Item", Args = ReadArgs(s, ref i, '[', ']') });
                }
                else
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || (s[i] == '$' && i == start))) i++;
                    if (i == start) throw new FormatException($"Esperava um nome na posição {i + 1} de '{s}'.");
                    var seg = new ComPathSegment { Name = s.Substring(start, i - start) };
                    if (i < s.Length && s[i] == '(') seg.Args = ReadArgs(s, ref i, '(', ')');
                    result.Add(seg);
                }

                if (i >= s.Length) break;
                if (s[i] == '.')
                {
                    i++;
                    if (i >= s.Length) throw new FormatException("O caminho termina em '.'.");
                }
                else if (s[i] != '[') throw new FormatException($"Caractere inesperado '{s[i]}' na posição {i + 1} de '{s}'.");
            }

            if (result[0].Args != null) throw new FormatException("A raiz do caminho não leva argumentos (use app, doc, sel, janela, docs ou $N).");
            return result;
        }

        private static object[] ReadArgs(string s, ref int i, char open, char close)
        {
            i++; // abre
            var args = new List<object>();
            while (true)
            {
                while (i < s.Length && s[i] == ' ') i++;
                if (i >= s.Length) throw new FormatException($"Faltou fechar '{open}' em '{s}'.");
                if (s[i] == close) { i++; break; }

                if (s[i] == '"' || s[i] == '\'')
                {
                    char q = s[i++];
                    int start = i;
                    while (i < s.Length && s[i] != q) i++;
                    if (i >= s.Length) throw new FormatException($"Texto sem aspas de fechamento em '{s}'.");
                    args.Add(s.Substring(start, i - start));
                    i++;
                }
                else
                {
                    int start = i;
                    while (i < s.Length && s[i] != ',' && s[i] != close) i++;
                    string token = s.Substring(start, i - start).Trim();
                    args.Add(ParseLiteral(token, s));
                }

                while (i < s.Length && s[i] == ' ') i++;
                if (i < s.Length && s[i] == ',') i++;
            }
            return args.ToArray();
        }

        private static object ParseLiteral(string token, string whole)
        {
            if (token.Length == 0) throw new FormatException($"Argumento vazio em '{whole}'.");
            if (string.Equals(token, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(token, "false", StringComparison.OrdinalIgnoreCase)) return false;
            if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n)) return n;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            throw new FormatException($"Argumento '{token}' não é número, true/false nem texto entre aspas (em '{whole}').");
        }

        /// <summary>
        /// Anda o caminho a partir da raiz que <paramref name="root"/> resolve. Devolve o valor
        /// final (objeto COM, número, texto, array...). Erro num passo vira
        /// <see cref="InvalidOperationException"/> dizendo QUAL passo falhou e até onde chegou.
        /// </summary>
        /// <param name="readOnly">true = só leitura de propriedade (+ <c>Item</c>).</param>
        public static object Evaluate(Func<string, object> root, string path, bool readOnly)
        {
            List<ComPathSegment> segs = Parse(path);
            object current = root(segs[0].Name);
            if (current == null)
                throw new InvalidOperationException($"A raiz '{segs[0].Name}' não existe agora (sem documento ativo, sem janela ou identificador desconhecido).");

            var walked = new StringBuilder(segs[0].Name);
            for (int k = 1; k < segs.Count; k++)
            {
                ComPathSegment seg = segs[k];
                if (current == null)
                    throw new InvalidOperationException($"'{walked}' devolveu vazio; não dá para ler '{seg}' a partir dele.");
                try { current = Step(current, seg, readOnly); }
                catch (Exception ex)
                {
                    Exception b = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex.GetBaseException();
                    throw new InvalidOperationException(
                        $"'{walked}.{seg}' falhou: {b.Message}" +
                        (readOnly && seg.Args != null && !IsItem(seg.Name)
                            ? "  (em leitura só se lê PROPRIEDADE; se '" + seg.Name + "' é método, use 'se_com_chamar'.)"
                            : ""), b);
                }
                walked.Append('.').Append(seg);
            }
            return current;
        }

        private static bool IsItem(string name) => string.Equals(name, "Item", StringComparison.OrdinalIgnoreCase);

        private static object Step(object target, ComPathSegment seg, bool readOnly)
        {
            BindingFlags flags = BindingFlags.GetProperty;
            if (!readOnly || IsItem(seg.Name)) flags |= BindingFlags.InvokeMethod;
            return target.GetType().InvokeMember(seg.Name, flags, null, target, seg.Args ?? new object[0], CultureInfo.InvariantCulture);
        }
    }
}
