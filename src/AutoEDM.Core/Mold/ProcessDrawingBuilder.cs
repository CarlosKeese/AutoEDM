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

        /// <summary>&lt;peça&gt;_W01.par — a cópia que vai sendo trabalhada.</summary>
        public string CopyPath { get; set; }

        /// <summary>&lt;peça&gt;_WP.asm — original e cópia sobrepostas.</summary>
        public string AssemblyPath { get; set; }

        /// <summary>Arquivos de destino que já existem (nada é sobrescrito).</summary>
        public List<string> Existing { get; } = new List<string>();

        /// <summary>Por que não dá para criar; null = pronto.</summary>
        public string Error { get; set; }

        public bool Ready => Error == null && Existing.Count == 0;
    }

    /// <summary>
    /// Botão "Desenho de processo": a peça é COPIADA para dentro de
    /// <c>&lt;molde&gt;\02 - PROCESSO\&lt;peça&gt;\</c> como <c>&lt;peça&gt;_W01.par</c>, e uma
    /// montagem nova <c>&lt;peça&gt;_WP.asm</c> recebe a ORIGINAL e a CÓPIA na mesma posição
    /// (origem, sem rotação), uma sobre a outra (Carlos, 2026-10-21). A montagem fica aberta.
    ///
    /// A pasta do molde é a primeira, subindo a partir da pasta da peça, que tem uma
    /// "02 - PROCESSO" dentro — a peça pode estar na raiz do molde ou numa subpasta dele.
    /// Nada é sobrescrito: se a cópia ou a montagem já existem, o plano diz e o botão oferece
    /// abrir a montagem que já está lá.
    /// </summary>
    public static class ProcessDrawingBuilder
    {
        /// <summary>"02 - PROCESSO", tolerando caixa e espaço em volta do hífen.</summary>
        private static readonly Regex ProcessFolderName = new Regex(@"^\s*02\s*-\s*PROCESSO\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public const string CopySuffix = "_W01";
        public const string AssemblySuffix = "_WP";

        /// <summary>Monta o plano para a peça — só olha o disco, não cria nada.</summary>
        public static ProcessDrawingPlan Plan(string sourcePartPath)
        {
            var plan = new ProcessDrawingPlan { SourcePath = sourcePartPath };
            if (string.IsNullOrWhiteSpace(sourcePartPath))
            { plan.Error = "a peça ainda não foi salva — salve-a antes de criar o desenho de processo."; return plan; }
            if (!File.Exists(sourcePartPath))
            { plan.Error = $"o arquivo da peça não foi encontrado: {sourcePartPath}"; return plan; }
            if (!string.Equals(Path.GetExtension(sourcePartPath), ".par", StringComparison.OrdinalIgnoreCase))
            { plan.Error = "o desenho de processo é feito de uma PEÇA (.par)."; return plan; }

            plan.PartName = Path.GetFileNameWithoutExtension(sourcePartPath);
            plan.ProcessRoot = FindProcessRoot(Path.GetDirectoryName(sourcePartPath));
            if (plan.ProcessRoot == null)
            {
                plan.Error = "não achei a pasta \"02 - PROCESSO\" subindo a partir da pasta da peça " +
                             $"({Path.GetDirectoryName(sourcePartPath)}). A peça está dentro da pasta do molde?";
                return plan;
            }

            plan.Folder = Path.Combine(plan.ProcessRoot, plan.PartName);
            plan.CopyPath = Path.Combine(plan.Folder, plan.PartName + CopySuffix + ".par");
            plan.AssemblyPath = Path.Combine(plan.Folder, plan.PartName + AssemblySuffix + ".asm");
            if (File.Exists(plan.CopyPath)) plan.Existing.Add(plan.CopyPath);
            if (File.Exists(plan.AssemblyPath)) plan.Existing.Add(plan.AssemblyPath);
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
        /// Cria a pasta, copia a peça e monta original + cópia na origem. A montagem fica
        /// ABERTA e salva. Devolve false com o motivo em <paramref name="error"/>; o que já foi
        /// criado até ali fica (nada é apagado do disco).
        /// </summary>
        public static bool Build(object application, ProcessDrawingPlan plan, out string error)
        {
            error = null;
            if (plan == null || !plan.Ready) { error = plan?.Error ?? "plano incompleto."; return false; }

            try
            {
                Directory.CreateDirectory(plan.Folder);
                File.Copy(plan.SourcePath, plan.CopyPath, overwrite: false);
                // A cópia herda o "somente leitura" de um arquivo liberado/arquivado.
                var attr = File.GetAttributes(plan.CopyPath);
                if ((attr & FileAttributes.ReadOnly) != 0) File.SetAttributes(plan.CopyPath, attr & ~FileAttributes.ReadOnly);
                Log.Info($"[processo] cópia: {plan.CopyPath}");
            }
            catch (Exception e) { error = "a cópia da peça falhou: " + e.GetBaseException().Message; return false; }

            dynamic app = application;
            dynamic asm;
            try { asm = app.Documents.Add("SolidEdge.AssemblyDocument"); }
            catch (Exception e) { error = "a montagem nova não pôde ser criada: " + e.GetBaseException().Message; return false; }

            var identity = OccurrenceTransform.FromMatrix(
                new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, new[] { 0.0, 0.0, 0.0 });
            foreach (string path in new[] { plan.SourcePath, plan.CopyPath })
            {
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

            try { asm.SaveAs(plan.AssemblyPath); }
            catch (Exception e) { error = "a montagem foi criada mas não pôde ser salva: " + e.GetBaseException().Message; return false; }
            Log.Info($"[processo] montagem salva e aberta: {plan.AssemblyPath}");
            return true;
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
