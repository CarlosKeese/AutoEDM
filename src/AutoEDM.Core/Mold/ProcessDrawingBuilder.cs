using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mold
{
    /// <summary>Em que máquina a peça vai ser trabalhada — cada uma vira uma peça de processo.</summary>
    [Flags]
    public enum ProcessMachine
    {
        None = 0,
        /// <summary>Centro de usinagem → &lt;peça&gt;_CDU.par</summary>
        Cdu = 1,
        /// <summary>Centro de torneamento → &lt;peça&gt;_CDT.par</summary>
        Cdt = 2,
    }

    /// <summary>Uma peça de processo do plano, vinculada à original por Cópia de peça.</summary>
    public sealed class ProcessCopy
    {
        public ProcessMachine Machine { get; set; }
        public string Path { get; set; }
        /// <summary>Já existe no disco: não é recriada, só entra na montagem se faltar lá.</summary>
        public bool Exists { get; set; }
    }

    /// <summary>O que o "Desenho de processo" vai criar — calculado antes de tocar em qualquer arquivo.</summary>
    public sealed class ProcessDrawingPlan
    {
        /// <summary>A peça original (arquivo salvo).</summary>
        public string SourcePath { get; set; }
        public string PartName { get; set; }

        /// <summary>A pasta "02 - PROCESSO" do molde.</summary>
        public string ProcessRoot { get; set; }

        /// <summary>02 - PROCESSO\&lt;peça&gt;</summary>
        public string Folder { get; set; }

        /// <summary>As peças de processo escolhidas (_CDU e/ou _CDT), nessa ordem.</summary>
        public List<ProcessCopy> Copies { get; } = new List<ProcessCopy>();

        /// <summary>&lt;peça&gt;_WP.asm — original e peças de processo sobrepostas.</summary>
        public string AssemblyPath { get; set; }
        public bool AssemblyExists { get; set; }

        /// <summary>Por que não dá para criar; null = pronto.</summary>
        public string Error { get; set; }

        public IEnumerable<ProcessCopy> ToCreate => Copies.Where(c => !c.Exists);

        /// <summary>Tudo o que foi pedido já existe — só resta abrir a montagem.</summary>
        public bool NothingToDo => Error == null && AssemblyExists && !ToCreate.Any();

        public bool Ready => Error == null && !NothingToDo;
    }

    /// <summary>
    /// Botão "Desenho de processo": para cada máquina escolhida (Carlos, 2026-10-22) nasce uma PEÇA
    /// NOVA em <c>&lt;molde&gt;\02 - PROCESSO\&lt;peça&gt;\</c> — <c>&lt;peça&gt;_CDU.par</c> (centro de
    /// usinagem) e/ou <c>&lt;peça&gt;_CDT.par</c> (centro de torneamento) — com uma CÓPIA DE PEÇA
    /// (<c>Models.AddCopiedPart</c>) da original, em ORDENADO. A cópia fica VINCULADA: a original
    /// alterada e salva chega à peça de processo (provado por tools/PartCopyProbe em 2026-10-22: o
    /// ressalto acrescentado na original apareceu na cópia ao reabri-la). Até 2026-10-21 era um
    /// File.Copy para &lt;peça&gt;_W01.par, sem vínculo nenhum.
    ///
    /// A montagem <c>&lt;peça&gt;_WP.asm</c> recebe a ORIGINAL e as peças de processo na mesma posição
    /// (origem, sem rotação). Se ela já existe, é aberta e recebe só o que falta — nada é
    /// sobrescrito, e uma peça de processo que já existe é reaproveitada, não recriada.
    ///
    /// A pasta do molde é a primeira, subindo a partir da pasta da peça, que tem uma
    /// "02 - PROCESSO" dentro — a peça pode estar na raiz do molde ou numa subpasta dele.
    /// </summary>
    public static class ProcessDrawingBuilder
    {
        /// <summary>"02 - PROCESSO", tolerando caixa e espaço em volta do hífen.</summary>
        private static readonly Regex ProcessFolderName = new Regex(@"^\s*02\s*-\s*PROCESSO\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public const string AssemblySuffix = "_WP";
        private const long IgFeatureOK = 1216476310L;

        /// <summary>Sufixo do arquivo de cada máquina.</summary>
        public static string SuffixOf(ProcessMachine machine) =>
            machine == ProcessMachine.Cdu ? "_CDU" : machine == ProcessMachine.Cdt ? "_CDT" : null;

        /// <summary>Monta o plano para a peça — só olha o disco, não cria nada.</summary>
        public static ProcessDrawingPlan Plan(string sourcePartPath, ProcessMachine machines)
        {
            var plan = new ProcessDrawingPlan { SourcePath = sourcePartPath };
            if (string.IsNullOrWhiteSpace(sourcePartPath))
            { plan.Error = "a peça ainda não foi salva — salve-a antes de criar o desenho de processo."; return plan; }
            if (!File.Exists(sourcePartPath))
            { plan.Error = $"o arquivo da peça não foi encontrado: {sourcePartPath}"; return plan; }
            if (!string.Equals(Path.GetExtension(sourcePartPath), ".par", StringComparison.OrdinalIgnoreCase))
            { plan.Error = "o desenho de processo é feito de uma PEÇA (.par)."; return plan; }
            if (machines == ProcessMachine.None)
            { plan.Error = "escolha pelo menos uma máquina: CDU (centro de usinagem) ou CDT (centro de torneamento)."; return plan; }

            plan.PartName = Path.GetFileNameWithoutExtension(sourcePartPath);
            plan.ProcessRoot = FindProcessRoot(Path.GetDirectoryName(sourcePartPath));
            if (plan.ProcessRoot == null)
            {
                plan.Error = "não achei a pasta \"02 - PROCESSO\" subindo a partir da pasta da peça " +
                             $"({Path.GetDirectoryName(sourcePartPath)}). A peça está dentro da pasta do molde?";
                return plan;
            }

            plan.Folder = Path.Combine(plan.ProcessRoot, plan.PartName);
            foreach (ProcessMachine m in new[] { ProcessMachine.Cdu, ProcessMachine.Cdt })
            {
                if ((machines & m) == 0) continue;
                string path = Path.Combine(plan.Folder, plan.PartName + SuffixOf(m) + ".par");
                plan.Copies.Add(new ProcessCopy { Machine = m, Path = path, Exists = File.Exists(path) });
            }
            plan.AssemblyPath = Path.Combine(plan.Folder, plan.PartName + AssemblySuffix + ".asm");
            plan.AssemblyExists = File.Exists(plan.AssemblyPath);
            return plan;
        }

        /// <summary>A primeira "02 - PROCESSO" que aparece subindo a partir de <paramref name="startFolder"/>.</summary>
        public static string FindProcessRoot(string startFolder)
        {
            for (var dir = string.IsNullOrEmpty(startFolder) ? null : new DirectoryInfo(startFolder); dir != null; dir = dir.Parent)
            {
                DirectoryInfo[] subs;
                try { subs = dir.GetDirectories(); }
                catch { continue; }   // sem permissão de listar: tenta o de cima
                var hit = subs.FirstOrDefault(d => ProcessFolderName.IsMatch(d.Name));
                if (hit != null) return hit.FullName;
            }
            return null;
        }

        /// <summary>
        /// A peça de onde o desenho parte: a PEÇA ativa, ou a ocorrência selecionada na
        /// MONTAGEM. <paramref name="unsaved"/> = a peça ativa tem alterações não salvas (a
        /// cópia sai do ARQUIVO, então elas não iriam junto).
        /// </summary>
        public static string ResolveSource(object activeDoc, out bool unsaved, out string error)
        {
            unsaved = false; error = null;
            int type = 0;
            try { type = Convert.ToInt32(Get(activeDoc, "Type")); } catch { }
            if (type == 1)   // igPartDocument
            {
                string path = Str(Get(activeDoc, "FullName"));
                try { unsaved = Convert.ToBoolean(Get(activeDoc, "Dirty")); } catch { }
                if (string.IsNullOrEmpty(path)) error = "a peça ainda não foi salva — salve-a antes.";
                return path;
            }
            if (type != 3)   // igAssemblyDocument
            {
                error = "abra a PEÇA, ou selecione a peça na MONTAGEM do molde, e clique de novo.";
                return null;
            }

            object ss = Get(activeDoc, "SelectSet");
            int n = 0;
            try { n = Convert.ToInt32(Get(ss, "Count")); } catch { }
            var files = new List<string>();
            for (int i = 1; i <= n; i++)
            {
                object item;
                try { item = ss.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, ss, new object[] { i }); }
                catch { continue; }
                // Ocorrência selecionada inteira, ou face/aresta dela (embrulho com .ImmediateParent).
                string file = Str(Get(item, "OccurrenceFileName")) ?? Str(Get(Get(item, "ImmediateParent"), "OccurrenceFileName"));
                if (!string.IsNullOrEmpty(file) && !files.Contains(file, StringComparer.OrdinalIgnoreCase)) files.Add(file);
            }
            if (files.Count == 1) return files[0];
            error = files.Count == 0
                ? "na MONTAGEM, selecione a peça (a ocorrência) e clique de novo."
                : $"há {files.Count} peças diferentes selecionadas — selecione só uma.";
            return null;
        }

        /// <summary>
        /// Cria as peças de processo que faltam (Cópia de peça vinculada, ordenado) e põe original +
        /// peças de processo na origem da montagem — nova, ou a existente aberta. A montagem fica
        /// ABERTA e salva. Devolve false com o motivo em <paramref name="error"/>; o que já foi
        /// criado até ali fica (nada é apagado do disco).
        /// </summary>
        public static bool Build(object application, ProcessDrawingPlan plan, out string error)
        {
            error = null;
            if (plan == null || !plan.Ready) { error = plan?.Error ?? "plano incompleto."; return false; }

            try { Directory.CreateDirectory(plan.Folder); }
            catch (Exception e) { error = "a pasta não pôde ser criada: " + e.GetBaseException().Message; return false; }

            foreach (ProcessCopy copy in plan.ToCreate.ToList())
                if (!CreateLinkedCopy(application, plan.SourcePath, copy.Path, out error)) return false;

            dynamic app = application;
            dynamic asm;
            try { asm = plan.AssemblyExists ? app.Documents.Open(plan.AssemblyPath) : app.Documents.Add("SolidEdge.AssemblyDocument"); }
            catch (Exception e) { error = "a montagem não pôde ser aberta/criada: " + e.GetBaseException().Message; return false; }

            // O que a montagem já tem, pelo arquivo — só entra o que falta.
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                dynamic occs = asm.Occurrences;
                for (int i = 1; i <= (int)occs.Count; i++)
                {
                    string f = Str(Get((object)occs.Item(i), "OccurrenceFileName"));
                    if (!string.IsNullOrEmpty(f)) present.Add(Path.GetFullPath(f));
                }
            }
            catch (Exception e) { Log.Warn("[processo] ocorrências da montagem ilegíveis — " + e.GetBaseException().Message); }

            var identity = OccurrenceTransform.FromMatrix(
                new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, new[] { 0.0, 0.0, 0.0 });
            foreach (string path in new[] { plan.SourcePath }.Concat(plan.Copies.Select(c => c.Path)))
            {
                if (present.Contains(Path.GetFullPath(path))) { Log.Info($"[processo] já está na montagem: {Path.GetFileName(path)}"); continue; }
                try
                {
                    object occ = asm.Occurrences.AddByFilename(path);
                    if (!TryPutMatrix(occ, identity)) ((dynamic)occ).PutOrigin(0.0, 0.0, 0.0);
                    Log.Info($"[processo] na montagem, na origem: {Path.GetFileName(path)}");
                }
                catch (Exception e)
                {
                    error = $"'{Path.GetFileName(path)}' não entrou na montagem: {e.GetBaseException().Message}";
                    return false;
                }
            }

            try { if (plan.AssemblyExists) asm.Save(); else asm.SaveAs(plan.AssemblyPath); }
            catch (Exception e) { error = "a montagem foi montada mas não pôde ser salva: " + e.GetBaseException().Message; return false; }
            Log.Info($"[processo] montagem salva e aberta: {plan.AssemblyPath}");
            return true;
        }

        /// <summary>
        /// Peça NOVA, ordenada, com uma Cópia de peça da original (<c>Models.AddCopiedPart</c>, material
        /// vinculado), salva em <paramref name="path"/>. O vínculo aponta para o ARQUIVO da original:
        /// o que for salvo nela chega à peça de processo.
        /// </summary>
        private static bool CreateLinkedCopy(object application, string sourcePath, string path, out string error)
        {
            error = null;
            dynamic app = application;
            dynamic part = null;
            try
            {
                part = app.Documents.Add("SolidEdge.PartDocument");
                // Documento NOSSO, recém-criado e vazio: trocar o ambiente aqui não estraga nada.
                try { if ((int)part.ModelingMode != 2) part.ModelingMode = 2; }
                catch (Exception e) { Log.Warn("[processo] a peça nova não foi para ordenado — " + e.GetBaseException().Message); }

                part.Models.AddCopiedPart(sourcePath);
                dynamic copied = part.Models.Item(1).CopiedParts.Item(1);
                long status = Convert.ToInt64(copied.Status) & 0xFFFFFFFFL;
                if (status != IgFeatureOK)
                {
                    error = $"a Cópia de peça em {Path.GetFileName(path)} falhou (status {status}).";
                    return false;
                }
                try { copied.LinkMaterial = true; }
                catch (Exception e) { Log.Warn("[processo] material não vinculado — " + e.GetBaseException().Message); }

                part.SaveAs(path);
                part.Close();
                part = null;
                Log.Info($"[processo] peça de processo vinculada: {path} (Cópia de peça de {Path.GetFileName(sourcePath)}, ordenado).");
                return true;
            }
            catch (Exception e)
            {
                error = $"{Path.GetFileName(path)} não foi criada: {e.GetBaseException().Message}";
                return false;
            }
            finally
            {
                if (part != null) { try { part.Close(false); } catch { } }
            }
        }

        /// <summary>Abre um arquivo que já existe (a montagem de processo de uma rodada anterior).</summary>
        public static void Open(object application, string path)
        {
            dynamic app = application;
            app.Documents.Open(path);
        }

        private static bool TryPutMatrix(object occ, OccurrenceTransform pose)
        {
            try
            {
                occ.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, occ,
                    new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("[processo] PutMatrix recusado, posicionando por PutOrigin — " + e.GetBaseException().Message);
                return false;
            }
        }

        private static object Get(object o, string prop)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }

        private static string Str(object o) => o == null ? null : Convert.ToString(o, CultureInfo.InvariantCulture);
    }
}
