using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoEDM.Assembly
{
    /// <summary>
    /// Ordem "natural" dos nomes das peças no PathFinder (Carlos, 2026-10-19): os trechos numéricos
    /// comparam pelo VALOR, então "15335.100.par" vem antes de "15335.101.par", "15335.99" antes de
    /// "15335.100" e "Pino:2" antes de "Pino:10". Puro (só texto), para caber em teste.
    /// </summary>
    public static class PathFinderOrder
    {
        /// <summary>Compara dois nomes de arquivo: primeiro o nome sem extensão, depois a extensão.</summary>
        public static int CompareFileNames(string a, string b)
        {
            a = Path.GetFileName(a ?? ""); b = Path.GetFileName(b ?? "");
            int c = CompareNatural(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b));
            return c != 0 ? c : CompareNatural(Path.GetExtension(a), Path.GetExtension(b));
        }

        /// <summary>
        /// Comparação natural: o texto é cortado em trechos de dígitos e de não-dígitos. Dígitos
        /// comparam como número (sem limite de tamanho, então código longo não estoura); o resto,
        /// sem diferenciar maiúscula. Empate de valor ("007" e "7") desempata pelo texto.
        /// </summary>
        public static int CompareNatural(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                bool da = char.IsDigit(a[i]), db = char.IsDigit(b[j]);
                if (da && db)
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0'), nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length < nb.Length ? -1 : 1;
                    int c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c < 0 ? -1 : 1;
                }
                else
                {
                    int si = i, sj = j;
                    while (i < a.Length && !char.IsDigit(a[i])) i++;
                    while (j < b.Length && !char.IsDigit(b[j])) j++;
                    int c = string.Compare(a.Substring(si, i - si), b.Substring(sj, j - sj),
                                           CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
                    if (c != 0) return c < 0 ? -1 : 1;
                }
            }
            if (i < a.Length) return 1;
            if (j < b.Length) return -1;
            return string.CompareOrdinal(a, b) < 0 ? -1 : string.CompareOrdinal(a, b) > 0 ? 1 : 0;
        }

        /// <summary>
        /// A ordem desejada das ocorrências: pelo ARQUIVO (é o código da peça) e, entre posições do
        /// mesmo arquivo, pelo nome da ocorrência ("x.par:1", "x.par:2"…). Estável: empate total
        /// mantém a ordem atual. Devolve os índices (0-based) de <paramref name="items"/> na ordem nova.
        /// </summary>
        public static List<int> Sorted(IList<OccurrenceEntry> items)
        {
            return Enumerable.Range(0, items.Count)
                .OrderBy(k => items[k], Comparer<OccurrenceEntry>.Create((x, y) =>
                {
                    int c = CompareFileNames(x.FileName, y.FileName);
                    return c != 0 ? c : CompareNatural(x.Name, y.Name);
                }))
                .ToList();
        }
    }

    /// <summary>Uma ocorrência do 1º nível da montagem, como o PathFinder mostra.</summary>
    public sealed class OccurrenceEntry
    {
        public string Name;
        public string FileName;
        public OccurrenceEntry(string name, string fileName) { Name = name ?? ""; FileName = fileName ?? ""; }
        public override string ToString() => Name;
    }

    public sealed class PathFinderSortResult
    {
        public bool Ok;
        public string Error;
        public List<string> Before = new List<string>();
        public List<string> After = new List<string>();
        /// <summary>Quantas ocorrências mudam (ou mudaram) de lugar.</summary>
        public int Moves;
        public List<string> Failures = new List<string>();
        public bool AlreadySorted => Ok && Moves == 0;

        public string Summary()
        {
            if (!Ok) return "Não ordenou: " + Error;
            var sb = new StringBuilder();
            sb.AppendLine(Moves == 0
                ? $"{Before.Count} ocorrência(s) no 1º nível — já estão em sequência. Nada foi alterado."
                : $"{Before.Count} ocorrência(s) no 1º nível; {Moves} mudam de lugar.");
            if (Failures.Count > 0)
            {
                sb.AppendLine($"{Failures.Count} não puderam ser movidas:");
                foreach (string f in Failures) sb.AppendLine("  • " + f);
            }
            return sb.ToString();
        }

        /// <summary>A ordem nova, com marca nas que mudam de posição.</summary>
        public string Listing(int max = 400)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < After.Count && k < max; k++)
                sb.AppendLine((k < Before.Count && Before[k] == After[k] ? "   " : " → ") + After[k]);
            if (After.Count > max) sb.AppendLine($"   … mais {After.Count - max}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Ordena as ocorrências do 1º nível da montagem no PathFinder pela <see cref="PathFinderOrder"/>.
    /// Usa <c>Occurrences.ReorderOccurrence(OccurrenceToReorder, TargetOccurrence, AfterTarget)</c>
    /// (assinatura do Interop.SolidEdge). Só a ORDEM da árvore muda — posição, relações e
    /// arquivos não. Submontagens não são abertas: a ordem dentro delas é do arquivo delas.
    /// </summary>
    public static class PathFinderSorter
    {
        // O 3º argumento de ReorderOccurrence chama-se "AfterTarget", mas na SE 2023 (v223) o
        // sentido é o INVERSO do nome — medido pela sonda tools/PathFinderProbe (2026-10-19), com
        // A|B|C: Reorder(C, A, true) deu C|A|B e Reorder(C, A, false) deu A|C|B.
        private const bool Before = true;
        private const bool After = false;

        /// <summary>Só lê: a ordem atual e a nova.</summary>
        public static PathFinderSortResult Plan(object assemblyDoc)
        {
            var r = new PathFinderSortResult();
            try
            {
                List<OccurrenceEntry> items = Read(assemblyDoc, out _);
                List<int> order = PathFinderOrder.Sorted(items);
                r.Before = items.Select(e => e.Name).ToList();
                r.After = order.Select(k => items[k].Name).ToList();
                r.Moves = r.Before.Where((n, k) => n != r.After[k]).Count();
                r.Ok = true;
            }
            catch (Exception ex) { r.Error = ex.GetBaseException().Message; }
            return r;
        }

        /// <summary>
        /// Reordena e CONFERE: a árvore é lida de volta e comparada com a ordem pedida. Move só o
        /// que está fora do lugar — cada ocorrência vai para logo DEPOIS da anterior já arrumada
        /// (a primeira, para ANTES da que hoje abre a lista).
        /// </summary>
        public static PathFinderSortResult Apply(object assemblyDoc)
        {
            var r = new PathFinderSortResult();
            try
            {
                dynamic doc = assemblyDoc;
                List<OccurrenceEntry> items = Read(assemblyDoc, out List<object> occs);
                List<int> order = PathFinderOrder.Sorted(items);
                r.Before = items.Select(e => e.Name).ToList();
                r.After = order.Select(k => items[k].Name).ToList();

                // Simula a lista enquanto move, para só chamar a SE onde a posição está errada.
                var current = Enumerable.Range(0, items.Count).ToList();
                dynamic coll = doc.Occurrences;
                for (int pos = 0; pos < order.Count; pos++)
                {
                    int want = order[pos];
                    if (current[pos] == want) continue;
                    try
                    {
                        if (pos == 0) coll.ReorderOccurrence(occs[want], occs[current[0]], Before);
                        else coll.ReorderOccurrence(occs[want], occs[order[pos - 1]], After);
                        current.Remove(want);
                        current.Insert(pos, want);
                        r.Moves++;
                    }
                    catch (Exception ex)
                    {
                        r.Failures.Add($"{items[want].Name}: {ex.GetBaseException().Message}");
                    }
                }

                // Conferência: a ordem que a SE mostra agora.
                List<string> now = Read(assemblyDoc, out _).Select(e => e.Name).ToList();
                if (!now.SequenceEqual(r.After))
                {
                    int bad = Enumerable.Range(0, Math.Min(now.Count, r.After.Count)).FirstOrDefault(k => now[k] != r.After[k]);
                    r.Error = $"a árvore lida de volta não ficou na ordem pedida (posição {bad + 1}: " +
                              $"'{(bad < now.Count ? now[bad] : "?")}' no lugar de '{r.After[bad]}').";
                    r.After = now;
                    return r;
                }
                r.Ok = true;
            }
            catch (Exception ex) { r.Error = ex.GetBaseException().Message; }
            return r;
        }

        private static List<OccurrenceEntry> Read(object assemblyDoc, out List<object> occs)
        {
            dynamic doc = assemblyDoc;
            dynamic coll = doc.Occurrences;
            int n = (int)coll.Count;
            var items = new List<OccurrenceEntry>(n);
            occs = new List<object>(n);
            for (int i = 1; i <= n; i++)
            {
                dynamic o = coll.Item(i);
                string name = "", file = "";
                try { name = (string)o.Name; } catch { }
                try { file = Path.GetFileName((string)o.OccurrenceFileName); } catch { }
                if (string.IsNullOrEmpty(file)) file = name;
                items.Add(new OccurrenceEntry(name, file));
                occs.Add((object)o);
            }
            return items;
        }
    }
}
