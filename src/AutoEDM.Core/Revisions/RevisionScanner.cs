using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;

namespace AutoEDM.Revisions
{
    /// <summary>O grupo de revisão lido de UMA peça.</summary>
    public sealed class RevisionGroup
    {
        /// <summary>Número da revisão (o N de "Rev.N").</summary>
        public int Revision { get; set; }

        /// <summary>Nome do grupo como está na árvore ("Rev.2").</summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Nomes das operações do grupo, na ordem em que estão nele e COMO A ÁRVORE MOSTRA
        /// (EdgebarName: "Recorte 6", "Substituir Face 2") — é o que o Carlos reconhece.
        /// </summary>
        public List<string> Operations { get; } = new List<string>();

        /// <summary>O objeto COM do grupo, vivo enquanto o documento estiver aberto. Alimenta a
        /// miniatura (ele expõe <c>Faces</c>) sem varrer a árvore de novo.</summary>
        public object Feature { get; set; }
    }

    /// <summary>
    /// Acha, numa peça, o grupo "Rev.N" mais recente da árvore ORDENADA — o que o Carlos usa para
    /// marcar o que mudou nesta revisão do molde.
    ///
    /// COM CONFIRMADO NA SONDAGEM (pela ponte MCP, peça 14309.101.par, SE 223.00.13.05, 2026-09-18):
    /// o grupo é um item de <c>PartDocument.DesignEdgebarFeatures</c> e é ele próprio uma COLEÇÃO.
    /// <list type="bullet">
    ///   <item><c>Name</c> / <c>DisplayName</c> / <c>EdgebarName</c> = o que o usuário digitou ("Rev.2").</item>
    ///   <item><c>SystemName</c> = "Group_1" — é assim que se sabe que o item É um grupo, sem depender
    ///   do nome que o usuário deu.</item>
    ///   <item><c>Count</c> + <c>Item(i)</c> = as features do grupo (não é preciso adivinhar por
    ///   posição na árvore: os filhos aparecem DEPOIS do grupo na coleção plana, mas ali não dá para
    ///   distinguir um filho de uma feature criada depois, fora do grupo).</item>
    ///   <item>Também expõe <c>Faces(...)</c>, <c>ExactRange</c> e <c>Ungroup()</c> — este último é
    ///   destrutivo e NUNCA é chamado aqui: esta classe é só leitura.</item>
    /// </list>
    /// </summary>
    public static class RevisionScanner
    {
        /// <summary>
        /// ⚠ NÃO ler a propriedade padrão de revisão do arquivo (Carlos, 2026-09-18): "Número da
        /// Revisão" existe em TODA peça e a Solid Edge a usa como CONTADOR DE SALVAMENTOS — na
        /// 14309v1.asm ela trouxe 43 peças como "novas", com valores 153, 24, 9, 12... e a revisão
        /// do relatório virou 153. Quem diz que a peça mudou continua sendo o GRUPO da árvore.
        /// </summary>
        /// <summary>Prefixo do <c>SystemName</c> que identifica um grupo da árvore (independe do nome dado).</summary>
        private const string GroupSystemPrefix = "Group_";

        /// <summary>
        /// O grupo de revisão de maior número da peça. Null = a peça não tem grupo "Rev.N"
        /// (não foi alterada, ou o Carlos não agrupou).
        /// </summary>
        public static RevisionGroup LatestRevision(object partDocument)
        {
            if (partDocument == null) return null;
            dynamic features;
            try { features = ((dynamic)partDocument).DesignEdgebarFeatures; }
            catch (Exception ex) { Log.Warn("Revisões: DesignEdgebarFeatures indisponível — " + ex.Message); return null; }

            int count = 0;
            try { count = (int)features.Count; } catch { }

            RevisionGroup best = null;
            for (int i = 1; i <= count; i++)
            {
                dynamic item = null;
                try { item = features.Item(i); } catch { continue; }
                if (!IsGroup(item)) continue;

                string name = Text(item, "Name");
                int revision;
                if (!RevisionName.TryParse(name, out revision)) continue;
                if (best != null && revision <= best.Revision) continue;

                best = new RevisionGroup { Revision = revision, GroupName = name, Feature = (object)item };
            }

            if (best != null) ReadOperations(best);
            return best;
        }

        /// <summary>
        /// Varre a montagem e devolve uma linha por ARQUIVO de peça alterada, da revisão mais alta
        /// encontrada para baixo. <paramref name="projectDirectory"/> filtra o catálogo: numa
        /// montagem de molde a maioria das ocorrências é pino, mola e parafuso de biblioteca, que
        /// mora fora da pasta do projeto e nunca é alterada (173 ocorrências na 14309v1.asm, e as
        /// alteradas são as "14309.xxx" — medido em 2026-09-18). Null = não filtra.
        /// </summary>
        public static List<PartChange> Scan(dynamic assemblyDocument, string projectDirectory, out int latestRevision,
            IEnumerable<string> revisionPropertyNames = null)
        {
            List<string> names = (revisionPropertyNames ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            latestRevision = 0;
            var result = new List<PartChange>();
            if (assemblyDocument == null) return result;

            var ctx = new AssemblyContext(assemblyDocument);
            var seen = new Dictionary<string, PartChange>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> positions = CountPositions(ctx);
            int examined = 0, diagnosed = 0;

            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                object partDoc = (object)occ.OccurrenceDocument;
                if (partDoc == null) continue;

                string path = FullName(partDoc);
                string key = path ?? occ.Name;
                if (key == null || seen.ContainsKey(key)) continue;   // outra ocorrência do mesmo arquivo
                if (!InProject(path, projectDirectory)) continue;

                string fileName = path != null ? System.IO.Path.GetFileName(path) : occ.Name;
                examined++;

                RevisionGroup group;
                int fromProperty = 0;
                string propertySource = null;
                try
                {
                    group = LatestRevision(partDoc);
                    fromProperty = RevisionFromProperty(partDoc, names, out propertySource);
                    // Diagnóstico: com a lista desligada, ou nas primeiras peças em que NENHUM nome
                    // da lista casou, o log mostra que propriedades de revisão a peça tem de fato.
                    // Sem isto, "a peça não apareceu" vira silêncio — foi o que aconteceu em
                    // 2026-09-18 com o nome traduzido da tela (Carlos).
                    if (fromProperty == 0 && diagnosed < 3 && LogRevisionProperties(partDoc, fileName))
                        diagnosed++;
                }
                catch (Exception ex) { Log.Warn($"Revisões: '{key}' não pôde ser lida — {ex.Message}"); continue; }

                bool isNew;
                int revision = Decide(group != null ? group.Revision : 0, fromProperty, out isNew);
                if (revision <= 0) continue;   // nem grupo nem propriedade: a peça não mudou

                var change = new PartChange
                {
                    FileName = fileName,
                    FullPath = path,
                    Revision = revision,
                    RevisionSource = isNew ? propertySource : "grupo " + group.GroupName,
                    Description = isNew ? null : RevisionName.DescriptionAfterNumber(group.GroupName),
                };
                if (!isNew && group != null)
                {
                    change.Features.AddRange(group.Operations);
                    // Feature numerada no grupo JÁ É a ação indicada, escrita por ele na árvore:
                    // vira rascunho da lista, na ordem do número (Carlos, 2026-09-18). O que ficar
                    // gravado no .json depois manda sobre isto.
                    foreach (string action in NumberedOperations(group.Operations))
                        change.Actions.Add(action);
                }

                int count;
                if (!positions.TryGetValue(key, out count)) count = 1;
                change.Positions = count;
                change.PartDocument = partDoc;
                change.RevisionFeature = isNew ? null : group?.Feature;
                if (isNew) change.MarkAsNewPart(count); else change.MarkAsChangedPart();

                seen[key] = change;
                result.Add(change);
                if (revision > latestRevision) latestRevision = revision;

                Log.Info(isNew
                    ? $"Revisões: {fileName} — PEÇA NOVA Rev.{revision} ({propertySource}), {count} posição(ões) a fabricar."
                    : $"Revisões: {fileName} — {group.GroupName} com {group.Operations.Count} operação(ões).");
            }

            // Uma linha de fecho que explica o resultado: sem ela, "a peça X não apareceu" não tem
            // como ser diagnosticado depois (Carlos, 2026-09-18).
            Log.Info($"Revisões: {examined} peça(s) do projeto examinada(s), {result.Count} com revisão " +
                     $"({result.Count(p => p.IsNew)} nova(s)); mais recente = Rev.{latestRevision}. " +
                     $"Propriedade de peça nova: {(names.Count == 0 ? "(desligada)" : "'" + string.Join("' / '", names) + "'")}.");

            // Só a revisão MAIS RECENTE sai marcada (regra do Carlos). A peça que parou na Rev.1
            // enquanto a montagem já está na Rev.2 continua na lista, mas DESMARCADA: esconder
            // seria decidir por ele, e às vezes a peça atrasada é justamente o que se quer olhar.
            foreach (PartChange p in result) p.Include = p.Revision == latestRevision;
            return result;
        }

        /// <summary>
        /// Revisão de uma PEÇA NOVA, lida da propriedade de nome EXATO <paramref name="propertyName"/>
        /// (Carlos, 2026-09-18: peça nova não tem recurso para agrupar, então o número dela mora numa
        /// propriedade do arquivo; projeto novo nasce com tudo em 0/vazio). 0 = sem número, ou seja,
        /// peça que não é nova. Nome vazio desliga a leitura.
        /// </summary>
        public static int RevisionFromProperty(object partDocument, IEnumerable<string> propertyNames, out string source)
        {
            source = null;
            List<string> names = (propertyNames ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (partDocument == null || names.Count == 0) return 0;

            int best = 0;
            // Pode haver mais de um conjunto com um campo desse NOME (resumo × personalizado). Vale
            // o maior valor VÁLIDO entre eles — o vazio de um não pode apagar o número do outro.
            foreach (var found in RevisionProperties(partDocument))
            {
                if (!names.Any(n => Same(found.Key, n))) continue;
                int revision;
                if (!RevisionName.TryParseValue(found.Value, out revision) || revision <= best) continue;
                best = revision;
                source = $"propriedade '{found.Key}' = '{found.Value}'";
            }
            return best;
        }

        /// <summary>
        /// Escreve no log TODAS as propriedades da peça que falam de revisão. É o diagnóstico que
        /// identifica qual campo a empresa usa: numa rodada real o log mostra lado a lado o campo do
        /// Carlos (0 ou vazio nas peças antigas) e o contador da Solid Edge (153, 24, 9...). Só roda
        /// enquanto <see cref="AutoEDM.Config.AutoEdmConfig.RevisionPropertyName"/> não estiver
        /// definido — depois disso o log volta a ser curto.
        /// </summary>
        public static bool LogRevisionProperties(object partDocument, string fileName)
        {
            var found = RevisionProperties(partDocument).ToList();
            // Peça SEM nenhuma propriedade de revisão não ensina nada — o diagnóstico existe para
            // mostrar os nomes que EXISTEM e não casaram.
            if (found.Count == 0) return false;
            Log.Info($"Revisões: {fileName} — propriedades de revisão: " +
                     string.Join("; ", found.Select(kv => $"'{kv.Key}' = '{kv.Value}'")));
            return true;
        }

        /// <summary>Pares nome/valor de toda propriedade cujo nome fale de revisão, em todos os conjuntos.</summary>
        private static IEnumerable<KeyValuePair<string, string>> RevisionProperties(object partDocument)
        {
            var found = new List<KeyValuePair<string, string>>();
            if (partDocument == null) return found;

            dynamic sets;
            try { sets = ((dynamic)partDocument).Properties; }
            catch { return found; }

            int setCount = 0;
            try { setCount = (int)sets.Count; } catch { return found; }

            for (int i = 1; i <= setCount; i++)
            {
                dynamic set = null;
                try { set = sets.Item(i); } catch { continue; }
                int count = 0;
                try { count = (int)set.Count; } catch { continue; }

                for (int j = 1; j <= count; j++)
                {
                    dynamic property = null;
                    try { property = set.Item(j); } catch { continue; }

                    string name = null;
                    try { name = (string)property.Name; } catch { }
                    if (name == null || name.IndexOf("rev", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    string value = null;
                    try { value = Convert.ToString(property.Value, System.Globalization.CultureInfo.InvariantCulture); }
                    catch { }
                    found.Add(new KeyValuePair<string, string>(name, value ?? ""));
                }
            }
            return found;
        }

        /// <summary>
        /// Qual revisão vale e se a peça é NOVA. Lógica pura (testável sem o Solid Edge):
        /// <list type="bullet">
        ///   <item>só o grupo → peça ALTERADA nessa revisão;</item>
        ///   <item>só a propriedade → peça NOVA;</item>
        ///   <item>os dois → vence o MAIOR; empate fica com o grupo, porque peça que tem recurso
        ///   agrupado foi alterada, não criada.</item>
        /// </list>
        /// </summary>
        public static int Decide(int groupRevision, int propertyRevision, out bool isNew)
        {
            isNew = propertyRevision > groupRevision;
            return Math.Max(groupRevision, propertyRevision);
        }

        /// <summary>Compara nomes de propriedade ignorando maiúsculas, acentos e espaços nas pontas.</summary>
        public static bool Same(string a, string b) => Normalize(a) == Normalize(b);

        private static string Normalize(string s)
        {
            if (s == null) return "";
            string decomposed = s.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// As operações NUMERADAS do grupo, em ordem de número e já sem o número ("1 - Ajustar a
        /// chaveta" → "Ajustar a chaveta"): a folha renumera sozinha ao imprimir, e é o mesmo número
        /// que aparece no balão da miniatura.
        /// </summary>
        public static List<string> NumberedOperations(IEnumerable<string> operations)
        {
            var numbered = new List<KeyValuePair<int, string>>();
            foreach (string name in operations ?? Enumerable.Empty<string>())
            {
                int number;
                string text;
                if (!RevisionName.TryParseOperation(name, out number, out text)) continue;
                numbered.Add(new KeyValuePair<int, string>(number, string.IsNullOrWhiteSpace(text) ? name.Trim() : text));
            }
            return numbered.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        }

        /// <summary>O item da árvore É um grupo? Pelo <c>SystemName</c> ("Group_1"), não pelo nome dado.</summary>
        private static bool IsGroup(dynamic feature)
        {
            string systemName = Text(feature, "SystemName");
            return systemName != null &&
                   systemName.StartsWith(GroupSystemPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>As features de dentro do grupo, pelo próprio grupo (ele é uma coleção).</summary>
        private static void ReadOperations(RevisionGroup group)
        {
            dynamic g = group.Feature;
            int count = 0;
            try { count = (int)g.Count; } catch { return; }

            for (int i = 1; i <= count; i++)
            {
                dynamic child = null;
                try { child = g.Item(i); } catch { continue; }
                // EdgebarName é o rótulo da árvore ("Recorte 6"); Name é o interno ("ExtrudedCutout_6").
                string label = Text(child, "EdgebarName") ?? Text(child, "DisplayName") ?? Text(child, "Name");
                if (label != null) group.Operations.Add(label);
            }
        }

        /// <summary>
        /// Quantas vezes cada arquivo aparece na montagem INTEIRA — a quantidade a fabricar de uma
        /// peça nova. Mesma contagem de posições da Lista de corte (molde multi-cavidade usa o
        /// mesmo .par em vários lugares).
        /// </summary>
        private static Dictionary<string, int> CountPositions(AssemblyContext ctx)
        {
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                string key = FullName((object)occ.OccurrenceDocument) ?? occ.Name;
                if (key == null) continue;
                int n;
                positions.TryGetValue(key, out n);
                positions[key] = n + 1;
            }
            return positions;
        }

        /// <summary>A peça mora na pasta do projeto? Sem caminho, entra (melhor sobrar que faltar).</summary>
        private static bool InProject(string path, string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory) || path == null) return true;
            // O mesmo projeto aparece como "W:\..." (unidade mapeada) e "\\servidor02\...$\..." (UNC)
            // na MESMA sessão — visto na sondagem. Comparar o caminho inteiro perderia metade das
            // peças, então o que vale é o NOME DA PASTA do projeto ("MD-14309 [PN-13972] [PA-10925]").
            string folder = LastSegment(projectDirectory);
            return folder == null || path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string LastSegment(string directory)
        {
            string[] parts = (directory ?? "").Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts[parts.Length - 1];
        }

        private static string FullName(object doc)
        {
            try
            {
                string s = (string)((dynamic)doc).FullName;
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
        }

        /// <summary>
        /// Propriedade de texto de um objeto COM, best-effort. A coleção da árvore mistura tipos e
        /// nem todo item tem toda propriedade (o SPY mostrou leituras que levantam E_NOINTERFACE e
        /// E_NOTIMPL no MESMO objeto), então ausência é null e não exceção.
        /// </summary>
        private static string Text(dynamic comObject, string property)
        {
            try
            {
                string s;
                switch (property)
                {
                    case "Name": s = (string)comObject.Name; break;
                    case "SystemName": s = (string)comObject.SystemName; break;
                    case "EdgebarName": s = (string)comObject.EdgebarName; break;
                    case "DisplayName": s = (string)comObject.DisplayName; break;
                    default: return null;
                }
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
        }
    }
}
