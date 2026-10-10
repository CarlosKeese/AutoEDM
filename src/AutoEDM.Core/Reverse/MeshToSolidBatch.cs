using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;

namespace AutoEDM.Reverse
{
    /// <summary>Um corpo de malha da peça: índice 1-based em <c>Models</c> e nome.</summary>
    public sealed class MeshBody
    {
        public int Index;
        public string Name;
    }

    public sealed class BatchBodyResult
    {
        public MeshBody Body;
        public RecognitionResult Recognition;
        public PrismaticPlan Plan;
        public CorrectionPlan Corrections;
        public PrismaticBuildResult Build;

        /// <summary>Onde a peça foi salva (só com montagem).</summary>
        public string SavedPath;

        /// <summary>Por que este corpo não foi convertido (null = foi).</summary>
        public string Error;
    }

    public sealed class BatchResult
    {
        public readonly List<BatchBodyResult> Bodies = new List<BatchBodyResult>();
        public string Folder;
        public string AssemblyPath;
        public readonly List<string> Messages = new List<string>();
    }

    /// <summary>
    /// Malha → sólido em LOTE: vários corpos de malha da mesma peça, cada um numa peça nova, e —
    /// se pedido — todas as peças salvas numa pasta e reunidas numa MONTAGEM nova, cada uma na
    /// posição identidade. Como cada sólido é construído nas coordenadas da própria malha, a
    /// montagem reproduz o arranjo original sem posicionar nada à mão.
    ///
    /// Receita de montagem já provada no projeto (Nova peça, eletrodo): peça salva e FECHADA →
    /// <c>Occurrences.AddByFilename</c> → <c>PutMatrix</c> identidade (com <c>PutOrigin</c> de
    /// reserva). Nenhum arquivo existente é sobrescrito: nome repetido ganha " (2)", " (3)"…
    /// A peça da malha não é tocada.
    /// </summary>
    public static class MeshToSolidBatch
    {
        /// <summary>Os corpos de MALHA (facetas) da peça, na ordem de <c>Models</c>.</summary>
        public static List<MeshBody> ListMeshBodies(object partDocument)
        {
            var list = new List<MeshBody>();
            dynamic doc = partDocument;
            int count = 0;
            try { count = (int)doc.Models.Count; } catch { }
            for (int i = 1; i <= count; i++)
            {
                dynamic m = doc.Models.Item(i);
                bool facet = false;
                try { facet = (bool)m.IsFacetBody; } catch { }
                if (!facet) continue;
                string nm = "corpo " + i;
                try { nm = (string)m.BodyName; } catch { }
                list.Add(new MeshBody { Index = i, Name = nm });
            }
            return list;
        }

        /// <summary>
        /// Converte os corpos pedidos. Sem <paramref name="build"/> só planeja (leitura). Com
        /// <paramref name="assembly"/> (exige <paramref name="build"/>) salva cada peça em
        /// <paramref name="folder"/> (padrão: "&lt;peça&gt; - sólidos" ao lado da peça da malha) e
        /// monta todas numa montagem nova, salva na mesma pasta.
        /// </summary>
        public static BatchResult Run(object application, object meshDocument, IList<MeshBody> bodies,
            bool build, bool assembly, string folder, RecognizerOptions options)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (meshDocument == null) throw new ArgumentNullException(nameof(meshDocument));
            var result = new BatchResult();
            dynamic doc = meshDocument;

            if (assembly)
            {
                string why;
                result.Folder = ResolveFolder(doc, folder, out why);
                if (result.Folder == null) { result.Messages.Add(why); return result; }
            }

            foreach (MeshBody b in bodies)
            {
                var r = new BatchBodyResult { Body = b };
                result.Bodies.Add(r);
                try
                {
                    MeshData mesh = MeshReader.Read((object)doc.Models.Item(b.Index).Body, 0.01);
                    if (!mesh.Ok) { r.Error = "malha ilegível: " + mesh.Error; continue; }
                    r.Recognition = SurfaceRecognizer.Recognize(mesh.PointsMm, options ?? new RecognizerOptions());
                    r.Plan = PrismaticSection.Plan(mesh.PointsMm, r.Recognition, null);
                    r.Corrections = BoundaryCorrections.Plan(mesh.PointsMm, r.Recognition, r.Plan);
                    if (r.Plan.Bands.Count == 0) { r.Error = "nenhuma faixa: a malha não fecha secção em Z"; continue; }
                    if (!build) continue;

                    r.Build = PrismaticBuilder.Build(application, r.Plan, r.Corrections, b.Name, mesh.PointsMm);
                    if (r.Build.BandsBuilt == 0) { r.Error = "nenhuma faixa construída"; continue; }

                    if (assembly)
                    {
                        string path = FreePath(result.Folder, SafeName(DocumentTitle(doc) + " - " + b.Name), ".par");
                        dynamic part = r.Build.Document;
                        part.SaveAs(path);
                        part.Close();
                        r.Build.Document = null;
                        r.SavedPath = path;
                        Log.Info($"[ENG.REVERSA] lote: '{b.Name}' salvo em {path}");
                    }
                }
                catch (Exception ex)
                {
                    r.Error = "ERRO " + ex.GetBaseException().Message;
                    Log.Warn("[ENG.REVERSA] lote, corpo '" + b.Name + "': " + ex.GetBaseException());
                }
            }

            if (assembly) Assemble(application, doc, result);
            return result;
        }

        // ------------------------------------------------------------------ montagem

        private static void Assemble(object application, dynamic meshDoc, BatchResult result)
        {
            var parts = result.Bodies.Where(b => b.SavedPath != null).ToList();
            if (parts.Count == 0) { result.Messages.Add("Nenhuma peça salva — montagem não criada."); return; }

            dynamic app = application;
            dynamic asm = null;
            try
            {
                asm = app.Documents.Add("SolidEdge.AssemblyDocument");
                OccurrenceTransform identity = OccurrenceTransform.FromMatrix(
                    new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, new[] { 0.0, 0.0, 0.0 });
                foreach (BatchBodyResult b in parts)
                {
                    try
                    {
                        dynamic occ = asm.Occurrences.AddByFilename(b.SavedPath);
                        if (!TryPutMatrix(occ, identity)) occ.PutOrigin(0.0, 0.0, 0.0);
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"'{Path.GetFileName(b.SavedPath)}' não entrou na montagem: {ex.GetBaseException().Message}");
                    }
                }
                string asmPath = FreePath(result.Folder, SafeName(DocumentTitle(meshDoc) + " - sólidos"), ".asm");
                asm.SaveAs(asmPath);
                result.AssemblyPath = asmPath;
                Log.Info($"[ENG.REVERSA] lote: montagem com {parts.Count} peça(s) em {asmPath}");
            }
            catch (Exception ex)
            {
                result.Messages.Add("Montagem não criada: " + ex.GetBaseException().Message);
                Log.Warn("[ENG.REVERSA] lote, montagem: " + ex.GetBaseException());
            }
        }

        /// <summary><c>Occurrence.PutMatrix(Matrix, Replace: true)</c> — a mesma chamada da
        /// "Nova peça" e do eletrodo.</summary>
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
                Log.Warn("[ENG.REVERSA] PutMatrix falhou (" + e.GetBaseException().Message + ") — caindo para PutOrigin.");
                return false;
            }
        }

        // ------------------------------------------------------------------ pasta e nomes

        /// <summary>Pasta de saída: a pedida, ou "&lt;peça&gt; - sólidos" ao lado da peça da malha.
        /// Peça da malha nunca salva não tem "ao lado": aí a pasta tem de ser dada.</summary>
        public static string ResolveFolder(dynamic meshDoc, string requested, out string why)
        {
            why = null;
            try
            {
                string folder = string.IsNullOrWhiteSpace(requested) ? DefaultFolder(meshDoc) : requested;
                if (folder == null)
                {
                    why = "A peça da malha nunca foi salva, então não há pasta \"ao lado\" dela: salve a peça ou informe a pasta.";
                    return null;
                }
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch (Exception ex)
            {
                why = "Pasta de saída inválida: " + ex.GetBaseException().Message;
                return null;
            }
        }

        /// <summary>"&lt;peça&gt; - sólidos" ao lado da peça da malha, SEM criar; null se a peça
        /// nunca foi salva.</summary>
        public static string DefaultFolder(object meshDocument)
        {
            dynamic doc = meshDocument;
            string full = null;
            try { full = (string)doc.FullName; } catch { }
            if (string.IsNullOrWhiteSpace(full) || !Path.IsPathRooted(full)) return null;
            return Path.Combine(Path.GetDirectoryName(full), SafeName(DocumentTitle(doc) + " - sólidos"));
        }

        private static string DocumentTitle(dynamic doc)
        {
            string name = "peça";
            try { name = (string)doc.Name; } catch { }
            return Path.GetFileNameWithoutExtension(name);
        }

        /// <summary>Nome de arquivo sem caractere proibido.</summary>
        public static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            return sb.ToString().Trim();
        }

        /// <summary>Caminho que NÃO existe ainda: "nome.ext", senão "nome (2).ext", "nome (3).ext"…</summary>
        public static string FreePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            for (int k = 2; File.Exists(path); k++) path = Path.Combine(folder, name + " (" + k + ")" + ext);
            return path;
        }

        // ------------------------------------------------------------------ relatório

        /// <summary>Uma linha por corpo (volume, desvio, veredito) e o que aconteceu com a
        /// montagem. O detalhe de cada corpo é o relatório normal, que vai para o log.</summary>
        public static string Summary(BatchResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Malha → sólido em lote: {r.Bodies.Count} corpo(s).");
            foreach (BatchBodyResult b in r.Bodies)
            {
                string head = "  • " + b.Body.Name + ": ";
                if (b.Error != null) { sb.AppendLine(head + b.Error); continue; }
                if (b.Build == null)
                {
                    sb.AppendLine(head + string.Format(CultureInfo.InvariantCulture, "plano com {0} faixa(s) e {1} correção(ões); volume exato {2:0.0} mm³ (malha {3:0.0}).",
                        b.Plan.Bands.Count, b.Corrections.Regions.Count, b.Plan.ExactVolumeMm3, b.Plan.MeshVolumeMm3));
                    continue;
                }
                double worst = Math.Max(b.Build.MeshToSolid?.MaxMm ?? double.NaN, b.Build.SolidToMesh?.MaxMm ?? double.NaN);
                string verdict = double.IsNaN(worst) ? "desvio não medido"
                    : worst <= PrismaticBuilder.DeviationLimitMm ? "EXATO" : string.Format(CultureInfo.InvariantCulture, "diverge {0:0.###} mm", worst);
                sb.AppendLine(head + string.Format(CultureInfo.InvariantCulture,
                    "{0} — volume {1:0.0} mm³ (malha {2:0.0}), {3}/{4} faixa(s), {5} correção(ões){6}.",
                    verdict, b.Build.BodyVolumeMm3, b.Plan.MeshVolumeMm3, b.Build.BandsBuilt,
                    b.Build.BandsBuilt + b.Build.BandsFailed, b.Build.CorrectionsBuilt,
                    b.Build.CorrectionsFailed > 0 ? ", " + b.Build.CorrectionsFailed + " com falha" : "") +
                    (b.SavedPath != null ? "  → " + Path.GetFileName(b.SavedPath) : "  (" + b.Build.DocumentName + ", não salva)"));
            }
            if (r.AssemblyPath != null) sb.AppendLine("Montagem: " + r.AssemblyPath);
            else if (r.Folder != null) sb.AppendLine("Pasta: " + r.Folder);
            foreach (string m in r.Messages) sb.AppendLine("  ! " + m);
            return sb.ToString();
        }
    }
}
