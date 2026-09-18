using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoEDM.Diagnostics;

namespace AutoEDM.Revisions
{
    /// <summary>O que o projetista digitou para UMA peça, guardado entre sessões.</summary>
    public sealed class StoredPart
    {
        [JsonPropertyName("incluir")] public bool Include { get; set; } = true;
        [JsonPropertyName("descricao")] public string Description { get; set; }
        [JsonPropertyName("acoes")] public List<string> Actions { get; set; } = new List<string>();

        /// <summary>Rótulos das caixas marcadas (o texto, não o índice: a lista de tarefas pode crescer).</summary>
        [JsonPropertyName("marcadas")] public List<string> Checked { get; set; } = new List<string>();

        /// <summary>Complemento por caixa, ex.: {"FABRICAR, QUANTIDADE:": "2 pçs"}.</summary>
        [JsonPropertyName("complementos")] public Dictionary<string, string> Details { get; set; }
            = new Dictionary<string, string>();
    }

    /// <summary>Uma revisão inteira: cabeçalho, responsáveis e as peças.</summary>
    public sealed class StoredRevision
    {
        [JsonPropertyName("produtoAcabado")] public string ProductCode { get; set; }
        [JsonPropertyName("partNumbers")] public string PartNumbers { get; set; }
        [JsonPropertyName("portaMolde")] public string MoldBaseCode { get; set; }
        [JsonPropertyName("molde")] public string MoldCode { get; set; }
        [JsonPropertyName("rvpa")] public string Rvpa { get; set; }
        [JsonPropertyName("responsaveis")] public List<string> Responsibles { get; set; } = new List<string>();
        [JsonPropertyName("pecas")] public Dictionary<string, StoredPart> Parts { get; set; }
            = new Dictionary<string, StoredPart>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>O arquivo inteiro: uma entrada por revisão, porque a Rev.3 não herda o texto da Rev.2.</summary>
    public sealed class ChangeArchive
    {
        [JsonPropertyName("montagem")] public string AssemblyName { get; set; }
        [JsonPropertyName("revisoes")] public Dictionary<string, StoredRevision> Revisions { get; set; }
            = new Dictionary<string, StoredRevision>();
    }

    /// <summary>
    /// Guarda o que o Carlos DIGITA na janela da Lista de modificações (Carlos, 2026-09-17: a
    /// descrição e as ações não saem do modelo, ele escreve na janela) num .json ao lado da
    /// montagem. Sem isto, fechar a janela jogaria fora o trabalho todo e a segunda rodada da
    /// mesma revisão seria digitada de novo.
    ///
    /// Guarda por REVISÃO: quando a montagem vira a Rev.3, a folha nasce em branco em vez de
    /// herdar o texto da Rev.2, mas o histórico continua no arquivo — é o registro das revisões
    /// passadas, que hoje só existe nas planilhas soltas do Drive.
    ///
    /// É JSON legível de propósito: se o add-in sumir, o conteúdo continua acessível num editor
    /// de texto. Lógica pura + arquivo, sem COM.
    /// </summary>
    public static class ChangeReportStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // O arquivo é para ser lido por gente: acento sai como acento, não como escape ç.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Sufixo do arquivo, ao lado da montagem: "14309v1_revisoes.json".</summary>
        public const string FileSuffix = "_revisoes.json";

        /// <summary>Caminho do arquivo para uma montagem. Null se o caminho dela não foi lido.</summary>
        public static string PathFor(string assemblyFullPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyFullPath)) return null;
            string dir = Path.GetDirectoryName(assemblyFullPath);
            string name = Path.GetFileNameWithoutExtension(assemblyFullPath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return null;
            return Path.Combine(dir, name + FileSuffix);
        }

        /// <summary>
        /// Lê o arquivo. Arquivo inexistente devolve um vazio (é o caso normal da 1ª vez), e
        /// arquivo ILEGÍVEL também — com aviso no log: perder o preenchimento é ruim, mas não abrir
        /// a janela por causa de um JSON estragado é pior.
        /// </summary>
        public static ChangeArchive Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new ChangeArchive();
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return JsonSerializer.Deserialize<ChangeArchive>(json, Options) ?? new ChangeArchive();
            }
            catch (Exception ex)
            {
                Log.Warn($"Lista de modificações: '{path}' não pôde ser lido ({ex.Message}) — começando em branco.");
                return new ChangeArchive();
            }
        }

        /// <summary>Grava. Devolve false (com aviso no log) se a pasta for somente-leitura.</summary>
        public static bool Save(string path, ChangeArchive archive)
        {
            if (string.IsNullOrWhiteSpace(path) || archive == null) return false;
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(archive, Options), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"Lista de modificações: não deu para gravar '{path}' — {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Despeja no relatório o que estava guardado para ESTA revisão. O que a varredura leu do
        /// modelo (arquivo, caminho, operações) manda; o que o usuário digitou volta por cima.
        /// Peça guardada que não está mais na montagem é ignorada — ela saiu do molde.
        /// </summary>
        public static void Apply(ChangeArchive archive, ChangeReport report)
        {
            if (archive == null || report == null) return;
            StoredRevision saved;
            if (!archive.Revisions.TryGetValue(Key(report.Revision), out saved) || saved == null) return;

            if (!string.IsNullOrWhiteSpace(saved.ProductCode)) report.ProductCode = saved.ProductCode;
            if (!string.IsNullOrWhiteSpace(saved.PartNumbers)) report.PartNumbers = saved.PartNumbers;
            if (!string.IsNullOrWhiteSpace(saved.MoldBaseCode)) report.MoldBaseCode = saved.MoldBaseCode;
            if (!string.IsNullOrWhiteSpace(saved.MoldCode)) report.MoldCode = saved.MoldCode;
            if (!string.IsNullOrWhiteSpace(saved.Rvpa)) report.Rvpa = saved.Rvpa;
            if (saved.Responsibles != null && saved.Responsibles.Count > 0)
            {
                report.Responsibles.Clear();
                report.Responsibles.AddRange(saved.Responsibles);
            }

            foreach (PartChange part in report.Parts)
            {
                StoredPart stored;
                if (part.FileName == null || !saved.Parts.TryGetValue(part.FileName, out stored) || stored == null) continue;

                part.Include = stored.Include;
                if (!string.IsNullOrWhiteSpace(stored.Description)) part.Description = stored.Description;
                // A lista gravada manda até quando está VAZIA: apagar uma ação é uma decisão, e o
                // rascunho vindo das features numeradas não pode ressuscitar o que ele tirou.
                part.Actions.Clear();
                if (stored.Actions != null) part.Actions.AddRange(stored.Actions);
                foreach (ChangeTask task in part.Tasks ?? new List<ChangeTask>())
                {
                    task.Checked = stored.Checked != null &&
                                   stored.Checked.Contains(task.Label, StringComparer.OrdinalIgnoreCase);
                    string detail;
                    if (stored.Details != null && stored.Details.TryGetValue(task.Label, out detail))
                        task.Detail = detail;
                }
            }
        }

        /// <summary>Recolhe o relatório para dentro do arquivo (substitui a entrada desta revisão).</summary>
        public static ChangeArchive Capture(ChangeArchive archive, ChangeReport report, string assemblyName = null)
        {
            ChangeArchive target = archive ?? new ChangeArchive();
            if (report == null) return target;

            target.AssemblyName = assemblyName ?? report.AssemblyName ?? target.AssemblyName;
            var revision = new StoredRevision
            {
                ProductCode = report.ProductCode,
                PartNumbers = report.PartNumbers,
                MoldBaseCode = report.MoldBaseCode,
                MoldCode = report.MoldCode,
                Rvpa = report.Rvpa,
                Responsibles = new List<string>(report.Responsibles),
            };

            foreach (PartChange part in report.Parts)
            {
                if (part.FileName == null) continue;
                revision.Parts[part.FileName] = new StoredPart
                {
                    Include = part.Include,
                    Description = part.Description,
                    Actions = part.Actions.Where(a => !string.IsNullOrWhiteSpace(a)).ToList(),
                    Checked = part.CheckedTasks.Select(t => t.Label).ToList(),
                    Details = (part.Tasks ?? new List<ChangeTask>())
                        .Where(t => !string.IsNullOrWhiteSpace(t.Detail))
                        .ToDictionary(t => t.Label, t => t.Detail),
                };
            }

            target.Revisions[Key(report.Revision)] = revision;
            return target;
        }

        private static string Key(int revision) => revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
