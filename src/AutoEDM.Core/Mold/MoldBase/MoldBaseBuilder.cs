using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoEDM.Com;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>Uma ocorrência do 1º nível da montagem, com a caixa em coordenadas da montagem (mm).</summary>
    public sealed class MoldBaseItem
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public object Occurrence { get; set; }
        public double[] MinMm { get; set; }
        public double[] MaxMm { get; set; }
        /// <summary>Entra na caixa das peças (área, espessuras).</summary>
        public bool Include { get; set; } = true;
        /// <summary>É o PRODUTO (peça injetada): a altura dele é a altura a extrair.</summary>
        public bool IsProduct { get; set; }
    }

    /// <summary>O porta-molde que o botão já criou nesta montagem, lido das variáveis da submontagem.</summary>
    public sealed class ExistingMoldBase
    {
        public object Occurrence { get; set; }
        public string Path { get; set; }
        public string SeriesName { get; set; }
        public double P1 { get; set; }
        public double P2 { get; set; }
        public int Stroke { get; set; }
        public MoldBaseOptions Options { get; set; }
        public string OrderCode { get; set; }
    }

    public sealed class MoldBaseScan
    {
        public string AssemblyPath { get; set; }
        public HeightAxis Axis { get; set; }
        public List<MoldBaseItem> Items { get; } = new List<MoldBaseItem>();
        public ExistingMoldBase Existing { get; set; }
        public string Problem { get; set; }
        /// <summary>A caixa veio da seleção do usuário (prevalece sobre as escolhas lembradas).</summary>
        public bool FromSelection { get; set; }
    }

    public sealed class MoldBaseResult
    {
        public bool Ok { get; set; }
        public bool Created { get; set; }
        public string Message { get; set; }
        public string AssemblyPath { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Botão "Porta-molde" — a parte COM. Lê as peças da montagem ativa, monta o plano pelo
    /// <see cref="MoldBasePlanner"/> e cria (ou atualiza) a submontagem <c>Porta Molde\Porta Molde.asm</c>
    /// com uma peça ordenada por placa, todas guiadas por variáveis.
    ///
    /// Trocar de porta-molde não reimporta nada: as mesmas peças recebem os valores novos, os grupos de furos
    /// que mudaram de forma são refeitos, a pilha é reposicionada e a submontagem é regravada. O giro na
    /// planta e o lado fixo ficam na MATRIZ da ocorrência da submontagem — as placas são sempre modeladas
    /// com A no X local, então girar não exige refazer peça.
    ///
    /// Medido ao vivo (sonda tools/MoldBaseProbe, 2026-10-23): <c>Documents.Open</c> de um documento que a
    /// montagem já carregou devolve O MESMO objeto, editável; <c>OccurrenceDocument.Save()</c> abre caixa
    /// MODAL ("não é possível salvar") e trava — por isso tudo é gravado pelo documento aberto, com
    /// <c>DisplayAlerts</c> desligado.
    /// </summary>
    public static class MoldBaseBuilder
    {
        public const string FolderName = "Porta Molde";
        public const string AssemblyFileName = "Porta Molde.asm";
        public const string MarkerVariable = "AutoEDM_PortaMolde";
        private const int UnitDistance = 1, UnitScalar = 58;

        // ------------------------------------------------------------------ leitura

        /// <summary>As ocorrências do 1º nível (caixas na montagem), o porta-molde já existente e a seleção.</summary>
        public static MoldBaseScan Scan(dynamic asmDoc, HeightAxis axis)
        {
            var scan = new MoldBaseScan { Axis = axis };
            try { scan.AssemblyPath = (string)asmDoc.FullName; } catch { }
            if (string.IsNullOrWhiteSpace(scan.AssemblyPath) || !File.Exists(scan.AssemblyPath))
            {
                scan.Problem = "Salve a montagem antes: o porta-molde é gravado numa pasta ao lado dela.";
                return scan;
            }

            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                dynamic sel = asmDoc.SelectSet;
                for (int i = 1; i <= (int)sel.Count; i++)
                    try { selected.Add((string)sel.Item(i).Name); } catch { }
            }
            catch { }
            string baseFolder = FolderFor(scan.AssemblyPath);

            foreach (OccurrenceInfo occ in new AssemblyContext(asmDoc).GetOccurrences())
            {
                string file = null;
                try { file = (string)occ.ComOccurrence.OccurrenceFileName; } catch { }
                if (IsMoldBase(occ, file))
                {
                    scan.Existing = ReadExisting(occ, file);
                    continue;
                }
                if (file != null && string.Equals(Path.GetDirectoryName(file), baseFolder, StringComparison.OrdinalIgnoreCase)) continue;
                bool visible = true;
                try { visible = (bool)occ.ComOccurrence.Visible; } catch { }
                if (!TryRange(occ.ComOccurrence, out double[] mn, out double[] mx))
                {
                    Log.Warn($"Porta-molde: caixa de '{occ.Name}' ilegível — fica de fora.");
                    continue;
                }
                string name = Path.GetFileNameWithoutExtension(file ?? occ.Name);
                bool standard = IsStandardBase(file);
                scan.Items.Add(new MoldBaseItem
                {
                    Name = occ.Name, FileName = file, Occurrence = (object)occ.ComOccurrence, MinMm = mn, MaxMm = mx,
                    // Eletrodo e porta-molde/quadro já referenciado não são peças do molde: ficam de fora por padrão
                    // (a janela deixa marcar).
                    Include = visible && !standard && !Regex.IsMatch(name ?? "", @"(_EE\d|_EDM|^EE\d)", RegexOptions.IgnoreCase),
                    IsProduct = !standard && !Regex.IsMatch(name ?? "", @"^\d+\.\d{3}($|[^\d])"),
                });
            }

            // Seleção: só conta o que é ocorrência de 1º nível desta lista. Selecionado o próprio porta-molde, uma
            // face ou algo dentro de um subconjunto, nada casa — e aí a seleção é ignorada, em vez de esvaziar a caixa.
            int matched = scan.Items.Count(i => selected.Contains(i.Name));
            if (matched > 0)
            {
                foreach (MoldBaseItem it in scan.Items) it.Include = selected.Contains(it.Name);
                scan.FromSelection = true;
            }
            else if (selected.Count > 0)
                Log.Info($"Porta-molde: {selected.Count} item(ns) selecionado(s), nenhum é peça do 1º nível — seleção ignorada.");
            if (scan.Items.Count == 0 && scan.Existing == null) scan.Problem = "A montagem não tem peças para dimensionar o porta-molde.";
            Log.Info($"Porta-molde: {scan.Items.Count} ocorrência(s) lida(s), {scan.Items.Count(i => i.Include)} na caixa, " +
                     $"{scan.Items.Count(i => i.Include && i.IsProduct)} produto(s); porta-molde existente: {(scan.Existing == null ? "não" : scan.Existing.Path)}.");
            return scan;
        }

        /// <summary>
        /// Porta-molde da biblioteca já referenciado pelo molde (as pastas "01 - PORTA MOLDES", o quadro 7160 da
        /// gaveta, uma peça de biblioteca dos padrões): não é peça a acomodar.
        /// </summary>
        public static bool IsStandardBase(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            string name = Path.GetFileName(file);
            return file.IndexOf("PORTA MOLDE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   MoldBaseStandards.LibraryFileNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Índices (eixo 1 de planta, eixo 2 de planta, altura) na ordem X=0, Y=1, Z=2 — permutação cíclica.</summary>
        public static int[] PlanAxes(HeightAxis axis) =>
            axis == HeightAxis.X ? new[] { 1, 2, 0 } : axis == HeightAxis.Y ? new[] { 2, 0, 1 } : new[] { 0, 1, 2 };

        /// <summary>A caixa das peças marcadas, no referencial de planta, com a altura a partir da partição.</summary>
        public static MoldBaseContent Content(MoldBaseScan scan, MoldBaseOptions opt, out double partingMm)
        {
            int[] ax = PlanAxes(scan.Axis);
            var inc = scan.Items.Where(i => i.Include).ToList();
            partingMm = 0;
            if (inc.Count == 0) return new MoldBaseContent();
            double wMin = inc.Min(i => i.MinMm[ax[2]]), wMax = inc.Max(i => i.MaxMm[ax[2]]);
            partingMm = opt.PartingMm ?? (wMin <= 0 && wMax >= 0 ? 0 : (opt.FixedSideNegative ? wMax : wMin));
            double s = opt.FixedSideNegative ? -1 : 1;
            double a = s * (wMin - partingMm), b = s * (wMax - partingMm);
            var products = inc.Where(i => i.IsProduct).ToList();
            return new MoldBaseContent
            {
                Min1 = inc.Min(i => i.MinMm[ax[0]]), Max1 = inc.Max(i => i.MaxMm[ax[0]]),
                Min2 = inc.Min(i => i.MinMm[ax[1]]), Max2 = inc.Max(i => i.MaxMm[ax[1]]),
                MinW = Math.Min(a, b), MaxW = Math.Max(a, b),
                Count = inc.Count,
                ProductHeight = products.Count == 0 ? 0 : products.Max(i => i.MaxMm[ax[2]] - i.MinMm[ax[2]]),
                ProductSize1 = products.Count == 0 ? 0 : products.Max(i => i.MaxMm[ax[0]]) - products.Min(i => i.MinMm[ax[0]]),
                ProductSize2 = products.Count == 0 ? 0 : products.Max(i => i.MaxMm[ax[1]]) - products.Min(i => i.MinMm[ax[1]]),
            };
        }

        /// <summary>
        /// Por onde a janela começa: as escolhas gravadas do projeto (sem as forçadas) e, se o porta-molde já
        /// existe, a configuração DELE forçada — rodar de novo mostra o atual conferido com as peças de agora, e
        /// quem decide trocar é o usuário.
        /// </summary>
        public static MoldBaseOptions InitialOptions(MoldBaseScan scan, MoldBaseOptions saved)
        {
            MoldBaseOptions o = saved?.Clone() ?? new MoldBaseOptions();
            if (scan != null)
                foreach (MoldBaseItem it in scan.Items)
                {
                    string f = Path.GetFileName(it.FileName ?? it.Name);
                    if (!scan.FromSelection && o.ExcludedFiles.Contains(f, StringComparer.OrdinalIgnoreCase)) it.Include = false;
                    else if (!scan.FromSelection && o.IncludedFiles.Contains(f, StringComparer.OrdinalIgnoreCase)) it.Include = true;
                    if (o.ProductFiles.Contains(f, StringComparer.OrdinalIgnoreCase)) it.IsProduct = true;
                    else if (o.NonProductFiles.Contains(f, StringComparer.OrdinalIgnoreCase)) it.IsProduct = false;
                }
            o.ForcedSeries = null;
            o.ForcedP1 = o.ForcedP2 = 0;
            o.ForcedStroke = 0;
            ExistingMoldBase ex = scan?.Existing;
            if (ex?.SeriesName != null)
            {
                o.Family = ex.Options.Family;
                o.WithGuides = ex.Options.WithGuides;
                o.WithReturnPins = ex.Options.WithReturnPins;
                o.ForcedSeries = ex.Options.Family == MoldBaseFamily.Polimold ? ex.SeriesName : null;
                if (ex.P1 > 0) o.ForcedP1 = ex.P1;
                if (ex.P2 > 0) o.ForcedP2 = ex.P2;
                if (ex.Stroke > 0) o.ForcedStroke = ex.Stroke;
                o.Type = ex.Options.Type;
                o.Steel = ex.Options.Steel;
                o.Mounting = ex.Options.Mounting;
                o.Guiding = ex.Options.Guiding;
                o.FixedSideNegative = ex.Options.FixedSideNegative;
            }
            return o;
        }

        /// <summary>Guarda nas escolhas o que entra na caixa e o que é produto — a próxima rodada começa igual.</summary>
        public static void RememberItems(MoldBaseScan scan, MoldBaseOptions o)
        {
            Func<Func<MoldBaseItem, bool>, List<string>> files = f => scan.Items.Where(f).Select(i => Path.GetFileName(i.FileName ?? i.Name)).Distinct().ToList();
            o.ExcludedFiles = files(i => !i.Include);
            o.IncludedFiles = files(i => i.Include);
            o.ProductFiles = files(i => i.IsProduct);
            o.NonProductFiles = files(i => !i.IsProduct);
        }

        public static MoldBasePlan Plan(MoldBaseScan scan, MoldBaseOptions opt)
        {
            MoldBaseContent content = Content(scan, opt, out double parting);
            MoldBasePlan plan = MoldBasePlanner.Plan(content, opt);
            plan.PartingMm = parting;
            return plan;
        }

        // ------------------------------------------------------------------ escrita

        /// <summary>
        /// Pasta do porta-molde DESTA montagem: "Porta Molde - &lt;montagem&gt;" ao lado dela. Uma pasta só para
        /// todas as montagens da pasta do molde faria o botão rodado na montagem de EDM adotar — e regravar — o
        /// porta-molde do molde.
        /// </summary>
        public static string FolderFor(string assemblyPath) =>
            Path.Combine(Path.GetDirectoryName(assemblyPath), FolderName + " - " + Path.GetFileNameWithoutExtension(assemblyPath));

        /// <summary>Cria a submontagem (ou atualiza a existente) e a posiciona. Não salva a montagem ativa.</summary>
        public static MoldBaseResult Apply(dynamic app, dynamic asmDoc, MoldBaseScan scan, MoldBasePlan plan)
        {
            var res = new MoldBaseResult();
            if (scan.Problem != null && scan.Existing == null) { res.Message = scan.Problem; return res; }
            if (!plan.Ready) { res.Message = plan.Problem ?? "Plano incompleto."; return res; }

            string subPath = scan.Existing?.Path ?? Path.Combine(FolderFor(scan.AssemblyPath), AssemblyFileName);
            string folder = Path.GetDirectoryName(subPath);
            res.AssemblyPath = subPath;

            bool alerts = true;
            try { alerts = (bool)app.DisplayAlerts; app.DisplayAlerts = false; } catch { }
            try
            {
                object occ = scan.Existing?.Occurrence;
                if (occ == null && File.Exists(subPath))
                {
                    // A submontagem DESTA montagem existe na pasta, mas a ocorrência foi apagada: volta e é atualizada.
                    Log.Info($"Porta-molde: '{subPath}' já existe e não está na montagem — inserindo para atualizar.");
                    occ = asmDoc.Occurrences.AddByFilename(subPath);
                }

                if (occ == null)
                {
                    if (!CreateBase(app, folder, subPath, plan, res)) return res;
                    occ = asmDoc.Occurrences.AddByFilename(subPath);
                    res.Created = true;
                }
                else if (!UpdateBase(app, subPath, plan, res)) return res;

                Place(occ, scan.Axis, plan, res);
                res.Ok = true;
                res.Message = $"Porta-molde {plan.Series.Name} ({plan.OrderCode}) {(res.Created ? "criado" : "atualizado")} em '{subPath}'. " +
                              "A montagem ativa não foi salva.";
                Log.Info("Porta-molde: " + res.Message);
                return res;
            }
            catch (Exception e)
            {
                res.Message = "Falha: " + e.GetBaseException().Message;
                Log.Error("Porta-molde", e);
                return res;
            }
            finally { try { app.DisplayAlerts = alerts; } catch { } }
        }

        private static bool CreateBase(dynamic app, string folder, string subPath, MoldBasePlan plan, MoldBaseResult res)
        {
            bool folderIsNew = !Directory.Exists(folder);
            Directory.CreateDirectory(folder);
            string clash = plan.Plates.Select(p => Path.Combine(folder, p.FileName)).FirstOrDefault(File.Exists);
            if (clash != null)
            {
                res.Message = $"'{clash}' já existe sem a '{AssemblyFileName}' — apague ou renomeie a pasta '{folder}' antes.";
                return false;
            }

            // Falhou no meio: o que ESTA execução gravou é apagado — senão a próxima tentativa esbarra nos órfãos.
            var written = new List<string>();
            bool ok = false;
            try
            {
                foreach (PlateSpec plate in plan.Plates)
                {
                    string path = Path.Combine(folder, plate.FileName);
                    dynamic doc = HouseTemplates.Add((object)app, HouseDocumentKind.Part);
                    try
                    {
                        Log.Info($"Porta-molde: criando {plate.FileName} ({F(plate.Width)} × {F(plate.Length)} × {F(plate.Thickness)}, {plate.Holes.Count} grupo(s) de furos).");
                        if (!MoldBasePlateModeler.Create(doc, plate, false, out string error))
                        {
                            res.Message = $"{plate.FileName}: {error}";
                            try { doc.Close(false); } catch { }
                            return false;
                        }
                        doc.SaveAs(path);
                        written.Add(path);
                        doc.Close();
                    }
                    catch (Exception e)
                    {
                        try { doc.Close(false); } catch { }
                        res.Message = $"{plate.FileName}: {e.GetBaseException().Message}";
                        return false;
                    }
                }

                foreach (ComponentSpec comp in plan.Components)
                {
                    string path = Path.Combine(folder, comp.FileName);
                    if (File.Exists(path)) continue;
                    if (!CreateComponent(app, comp, path, res)) return false;
                    written.Add(path);
                }

                dynamic sub = HouseTemplates.Add((object)app, HouseDocumentKind.Assembly);
                try
                {
                    foreach (PlateSpec plate in plan.Plates)
                        for (int k = 0; k < plate.Count; k++)
                        {
                            dynamic o = sub.Occurrences.AddByFilename(Path.Combine(folder, plate.FileName));
                            PutPlate(o, plate, k);
                        }
                    foreach (ComponentSpec comp in plan.Components)
                        foreach (double[] pos in comp.Positions)
                            PutComponent(sub.Occurrences.AddByFilename(Path.Combine(folder, comp.FileName)), comp, pos);
                    PutLibraryParts(sub, plan, res);
                    WriteConfig(sub.Variables, plan);
                    sub.SaveAs(subPath);
                    written.Add(subPath);
                    sub.Close();
                    ok = true;
                    return true;
                }
                catch (Exception e)
                {
                    try { sub.Close(false); } catch { }
                    res.Message = "submontagem: " + e.GetBaseException().Message;
                    return false;
                }
            }
            finally
            {
                if (!ok)
                {
                    foreach (string f in written) try { File.Delete(f); } catch (Exception e) { Log.Warn($"Porta-molde: não apagou '{f}' — {e.GetBaseException().Message}"); }
                    if (folderIsNew) try { if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder); } catch { }
                    if (written.Count > 0) Log.Warn($"Porta-molde: criação interrompida — {written.Count} arquivo(s) desta execução apagado(s).");
                }
            }
        }

        /// <summary>
        /// Troca de porta-molde em DUAS fases: primeiro todas as placas em memória; só se todas passarem, grava.
        /// Antes de tudo, cópia dos arquivos em "_antes\&lt;data-hora&gt;" — se algo falhar no meio da gravação, o
        /// estado anterior está lá (e a mensagem diz para fechar a montagem ativa sem salvar).
        /// </summary>
        private static bool UpdateBase(dynamic app, string subPath, MoldBasePlan plan, MoldBaseResult res)
        {
            string folder = Path.GetDirectoryName(subPath);

            // Antes de tocar em qualquer coisa: algum arquivo do porta-molde aberto numa janela com alteração não
            // salva? Recusar no meio deixaria as placas anteriores já mexidas na memória.
            foreach (string f in plan.Plates.Select(p => Path.Combine(folder, p.FileName)).Concat(new[] { subPath }))
                if (IsOpenAndDirty(app, f))
                {
                    res.Message = $"'{Path.GetFileName(f)}' está aberta numa janela com alterações NÃO SALVAS — salve ou feche antes; nada foi mudado.";
                    return false;
                }
            string backup = Backup(folder);

            dynamic sub = OpenForEdit(app, subPath, out bool subWasOpen, out string problem);
            if (sub == null) { res.Message = problem; return false; }
            var opened = new List<Tuple<object, bool>>();   // (documento, já estava aberto pelo usuário)
            bool ok = false;
            try
            {
                var byFile = new Dictionary<string, List<dynamic>>(StringComparer.OrdinalIgnoreCase);
                dynamic occs = sub.Occurrences;
                for (int i = 1; i <= (int)occs.Count; i++)
                {
                    dynamic o = occs.Item(i);
                    string f = Path.GetFileName((string)o.OccurrenceFileName);
                    if (!byFile.TryGetValue(f, out List<dynamic> l)) byFile[f] = l = new List<dynamic>();
                    l.Add(o);
                }

                // Fase 1: todas as placas em memória.
                foreach (PlateSpec plate in plan.Plates)
                {
                    byFile.TryGetValue(plate.FileName, out List<dynamic> mine);
                    // O caminho é o que a ocorrência usa (projeto copiado pode apontar para outra pasta).
                    string path = mine != null && mine.Count > 0 ? (string)mine[0].OccurrenceFileName : Path.Combine(folder, plate.FileName);
                    if ((mine == null || mine.Count == 0) && !File.Exists(path))
                    {
                        // Placa nova para este porta-molde (troca de tipo: a unidade MUD não tem PBS, a Babyplast tem
                        // placas de apoio…): nasce como na criação.
                        if (!CreatePlate(app, plate, path, res)) return false;
                        res.Warnings.Add($"{plate.FileName}: placa nova criada.");
                    }
                    if (!File.Exists(path)) { res.Message = $"'{plate.FileName}' sumiu ({path}) — não dá para atualizar."; return false; }
                    if (!string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase))
                    {
                        res.Message = $"'{plate.FileName}' do porta-molde aponta para outra pasta ({path}) — não vou gravar fora de '{folder}'.";
                        return false;
                    }
                    dynamic doc = OpenForEdit(app, path, out bool wasOpen, out problem);
                    if (doc == null) { res.Message = problem; return false; }
                    opened.Add(Tuple.Create((object)doc, wasOpen));
                    if (!MoldBasePlateModeler.Update(doc, plate, false, out string error, out int rebuilt))
                    {
                        res.Message = $"{plate.FileName}: {error}";
                        return false;
                    }
                    if (rebuilt > 0) res.Warnings.Add($"{plate.FileName}: {rebuilt} recurso(s) refeito(s) (furos, bolsão, ressalto, travas ou rasgos).");

                    mine = mine ?? new List<dynamic>();
                    while (mine.Count < plate.Count) mine.Add(sub.Occurrences.AddByFilename(path));
                    for (int k = 0; k < plate.Count; k++) PutPlate(mine[k], plate, k);
                }

                // Componentes: os de código novo nascem; as ocorrências de placa/componente que saíram do plano
                // (troca de série ou de tipo) saem da submontagem. Só o que é DESTE botão — peça que o usuário pôs na
                // submontagem fica.
                foreach (ComponentSpec comp in plan.Components)
                {
                    string path = Path.Combine(folder, comp.FileName);
                    if (!File.Exists(path) && !CreateComponent(app, comp, path, res)) return false;
                }
                var keepPlates = new HashSet<string>(plan.Plates.Select(p => p.FileName), StringComparer.OrdinalIgnoreCase);
                int removed = 0;
                foreach (KeyValuePair<string, List<dynamic>> kv in byFile)
                {
                    bool ours = IsGeneratedComponent(kv.Key) || (IsGeneratedPlate(kv.Key) && !keepPlates.Contains(kv.Key)) ||
                                MoldBaseStandards.LibraryFileNames.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
                    if (!ours) continue;
                    foreach (dynamic o in kv.Value) { try { o.Delete(); removed++; } catch (Exception e) { Log.Warn($"Porta-molde: tirar '{kv.Key}' — {e.GetBaseException().Message}"); } }
                }
                foreach (ComponentSpec comp in plan.Components)
                    foreach (double[] pos in comp.Positions)
                        PutComponent(sub.Occurrences.AddByFilename(Path.Combine(folder, comp.FileName)), comp, pos);
                PutLibraryParts(sub, plan, res);
                Log.Info($"Porta-molde: {removed} ocorrência(s) de placa/componente/biblioteca tirada(s); {plan.Components.Sum(c => c.Positions.Count)} componente(s) " +
                         $"e {plan.LibraryParts.Count} peça(s) de biblioteca posta(s).");
                WriteConfig(sub.Variables, plan);

                // Fase 2: gravar.
                foreach (Tuple<object, bool> d in opened) ((dynamic)d.Item1).Save();
                sub.Save();
                ok = true;
                try
                {
                    Directory.Delete(backup, true);
                    string parent = Path.GetDirectoryName(backup);
                    if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
                }
                catch { }
                return true;
            }
            catch (Exception e)
            {
                res.Message = "atualização: " + e.GetBaseException().Message;
                return false;
            }
            finally
            {
                // Só fecha o que ESTA execução abriu; o que o usuário tinha aberto fica na janela dele.
                foreach (Tuple<object, bool> d in opened)
                    if (!d.Item2) try { if (ok) ((dynamic)d.Item1).Close(); else ((dynamic)d.Item1).Close(false); } catch { }
                if (!subWasOpen) try { if (ok) sub.Close(); else sub.Close(false); } catch { }
                if (!ok)
                    res.Message += $" Nada foi gravado depois da falha, mas a montagem ativa pode estar com o porta-molde " +
                                   $"pela metade NA MEMÓRIA: feche-a SEM salvar e abra de novo. Cópia dos arquivos de antes: '{backup}'.";
            }
        }

        /// <summary>
        /// O documento para editar: se o USUÁRIO já o tem aberto numa janela (está em app.Documents), é ele —
        /// e recusa se houver alteração não salva, porque gravar levaria junto o trabalho dele. Senão,
        /// Documents.Open, que devolve o mesmo objeto que a montagem carregou, editável (medido em 2026-10-23).
        /// </summary>
        private static dynamic OpenForEdit(dynamic app, string path, out bool wasOpen, out string problem)
        {
            wasOpen = false;
            problem = null;
            try
            {
                dynamic docs = app.Documents;
                for (int i = 1; i <= (int)docs.Count; i++)
                {
                    dynamic d = docs.Item(i);
                    string fn = null;
                    try { fn = (string)d.FullName; } catch { }
                    if (!string.Equals(fn, path, StringComparison.OrdinalIgnoreCase)) continue;
                    wasOpen = true;
                    bool dirty = false;
                    try { dirty = (bool)d.Dirty; } catch { }
                    if (dirty)
                    {
                        problem = $"'{Path.GetFileName(path)}' está aberta numa janela com alterações NÃO SALVAS — salve ou feche antes; nada foi mudado.";
                        return null;
                    }
                    return d;
                }
            }
            catch (Exception e) { Log.Warn("Porta-molde: lista de documentos abertos ilegível — " + e.GetBaseException().Message); }
            dynamic opened = app.Documents.Open(path);
            try { Log.Info($"Porta-molde: aberto para editar '{Path.GetFileName(path)}' (Dirty {(bool)opened.Dirty}, ReadOnly {(bool)opened.ReadOnly})."); } catch { }
            return opened;
        }

        private static bool IsOpenAndDirty(dynamic app, string path)
        {
            try
            {
                dynamic docs = app.Documents;
                for (int i = 1; i <= (int)docs.Count; i++)
                {
                    dynamic d = docs.Item(i);
                    string fn = null;
                    try { fn = (string)d.FullName; } catch { }
                    if (string.Equals(fn, path, StringComparison.OrdinalIgnoreCase)) return (bool)d.Dirty;
                }
            }
            catch { }
            return false;
        }

        private static string Backup(string folder)
        {
            string dir = Path.Combine(folder, "_antes", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            try
            {
                Directory.CreateDirectory(dir);
                foreach (string f in Directory.GetFiles(folder))
                    if (f.EndsWith(".par", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".asm", StringComparison.OrdinalIgnoreCase))
                        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
            }
            catch (Exception e) { Log.Warn("Porta-molde: cópia de segurança incompleta — " + e.GetBaseException().Message); }
            return dir;
        }

        private static bool CreatePlate(dynamic app, PlateSpec plate, string path, MoldBaseResult res)
        {
            dynamic doc = HouseTemplates.Add((object)app, HouseDocumentKind.Part);
            try
            {
                Log.Info($"Porta-molde: criando {plate.FileName} ({F(plate.Width)} × {F(plate.Length)} × {F(plate.Thickness)}).");
                if (!MoldBasePlateModeler.Create(doc, plate, false, out string error))
                {
                    res.Message = $"{plate.FileName}: {error}";
                    try { doc.Close(false); } catch { }
                    return false;
                }
                doc.SaveAs(path);
                doc.Close();
                return true;
            }
            catch (Exception e)
            {
                try { doc.Close(false); } catch { }
                res.Message = $"{plate.FileName}: {e.GetBaseException().Message}";
                return false;
            }
        }

        private static bool CreateComponent(dynamic app, ComponentSpec comp, string path, MoldBaseResult res)
        {
            dynamic doc = HouseTemplates.Add((object)app, HouseDocumentKind.Part);
            try
            {
                Log.Info($"Porta-molde: criando {comp.Kind} {comp.FileName} (Ø{F(comp.MaxDiameter)} × {F(comp.MaxZ - comp.MinZ)}).");
                if (!MoldBaseComponentModeler.Create(doc, comp, out string error))
                {
                    res.Message = $"{comp.FileName}: {error}";
                    try { doc.Close(false); } catch { }
                    return false;
                }
                doc.SaveAs(path);
                doc.Close();
                return true;
            }
            catch (Exception e)
            {
                try { doc.Close(false); } catch { }
                res.Message = $"{comp.FileName}: {e.GetBaseException().Message}";
                return false;
            }
        }

        /// <summary>O componente: origem da peça na base dele (MinZ), então a ocorrência vai em posição + MinZ.</summary>
        private static void PutComponent(dynamic occ, ComponentSpec comp, double[] pos)
        {
            OccurrenceTransform pose = OccurrenceTransform.Identity().ComposedWith(new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 },
                new[] { Units.MmToM(pos[0]), Units.MmToM(pos[1]), Units.MmToM(pos[2] + comp.MinZ) });
            object o = occ;
            o.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, o,
                new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
        }

        private static readonly Regex GeneratedComponent = new Regex(@"^(C|CG|B|BG|PR|PG) [\d.]+( |\.par$)", RegexOptions.IgnoreCase);
        private static readonly Regex GeneratedPlate = new Regex(@"^(PBS|P1|P2|PS|ESP|CPE|PE|PBI|[1-3]0[0-2]) - .+\.par$", RegexOptions.IgnoreCase);
        public static bool IsGeneratedComponent(string fileName) => GeneratedComponent.IsMatch(fileName ?? "");
        public static bool IsGeneratedPlate(string fileName) => GeneratedPlate.IsMatch(fileName ?? "");

        /// <summary>
        /// As peças de BIBLIOTECA do padrão (quadro 7160, guias, puxadores, engate), por referência e na pose do
        /// padrão. Arquivo que não existe nesta máquina (rede fora) vira aviso — o porta-molde sai sem ela.
        /// </summary>
        private static void PutLibraryParts(dynamic sub, MoldBasePlan plan, MoldBaseResult res)
        {
            foreach (IGrouping<string, LibraryPart> g in plan.LibraryParts.GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(g.Key))
                {
                    res.Warnings.Add($"Biblioteca fora do alcance: '{g.Key}' — {g.Count()} peça(s) ({g.First().Label}) não posta(s).");
                    continue;
                }
                foreach (LibraryPart p in g)
                    try
                    {
                        object o = sub.Occurrences.AddByFilename(p.Path);
                        OccurrenceTransform pose = OccurrenceTransform.Identity().ComposedWith(p.R,
                            new[] { Units.MmToM(p.T[0]), Units.MmToM(p.T[1]), Units.MmToM(p.T[2]) });
                        o.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, o,
                            new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                    }
                    catch (Exception e) { res.Warnings.Add($"{p.Label} ({p.FileName}): {e.GetBaseException().Message}"); }
            }
        }

        /// <summary>
        /// A placa na submontagem: origem no centro da face de baixo (X local = A). Espaçadores em ±OffsetA, o
        /// de −OffsetA GIRADO 180° na planta: assim o furo fora de centro da peça (Pf2, coluna no colunamento C)
        /// cai em ±F/2 com a mesma peça.
        /// </summary>
        private static void PutPlate(dynamic occ, PlateSpec plate, int index)
        {
            bool turned = plate.Count > 1 && index == 0;
            double x = plate.Count > 1 ? (index == 0 ? -plate.OffsetA : plate.OffsetA) : 0;
            double[] r = turned ? new double[] { -1, 0, 0, 0, -1, 0, 0, 0, 1 } : new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            OccurrenceTransform pose = OccurrenceTransform.Identity().ComposedWith(r, new[] { Units.MmToM(x), 0.0, Units.MmToM(plate.BottomW) });
            object o = occ;
            o.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, o,
                new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
        }

        /// <summary>
        /// Pose da submontagem na montagem ativa: origem no centro do molde sobre a partição; X local (A) no 1º
        /// eixo de planta — ou no 2º, girado —, Z local para o lado fixo. Conferida pela MATRIZ lida de volta
        /// (não pela caixa: componentes que o usuário pôs na submontagem mudam a caixa). TranslationLast é a
        /// arrumação medida certa (2026-10-23); se a leitura não bater, tenta a outra e, sem acordo, volta à medida.
        /// </summary>
        private static void Place(object occ, HeightAxis axis, MoldBasePlan plan, MoldBaseResult res)
        {
            int[] ax = PlanAxes(axis);
            double s = plan.Options.FixedSideNegative ? -1 : 1;
            double[] e1 = Unit(ax[0]), e2 = Unit(ax[1]), ew = Unit(ax[2]);
            double[] lx = plan.Rotated ? e2 : e1;
            double[] ly = plan.Rotated ? Scale(e1, -s) : Scale(e2, s);
            double[] lz = Scale(ew, s);
            double[] r = { lx[0], ly[0], lz[0], lx[1], ly[1], lz[1], lx[2], ly[2], lz[2] };
            var t = new double[3];
            t[ax[0]] = Units.MmToM(plan.Center1); t[ax[1]] = Units.MmToM(plan.Center2); t[ax[2]] = Units.MmToM(plan.PartingMm);
            OccurrenceTransform want = OccurrenceTransform.Identity().ComposedWith(r, t);

            foreach (MatrixLayout layout in new[] { MatrixLayout.TranslationLast, MatrixLayout.TranslationColumn })
            {
                if (!PutPose(occ, OccurrenceTransform.Identity(layout).ComposedWith(r, t))) continue;
                OccurrenceTransform got = AssemblyContext.TryGetPose(new OccurrenceInfo(occ, "Porta Molde", null));
                if (got == null) { res.Warnings.Add("Matriz do porta-molde ilegível — posição não conferida."); return; }
                double err = PoseError(want, got);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "Porta-molde: matriz gravada em {0}, lida de volta com desvio {1:0.###} mm.", layout, err));
                if (err < 0.01) return;
            }
            PutPose(occ, OccurrenceTransform.Identity(MatrixLayout.TranslationLast).ComposedWith(r, t));
            res.Warnings.Add("A matriz lida de volta não bateu com a gravada em nenhuma arrumação — gravei a arrumação medida certa; confira a posição na montagem (detalhe no log).");
        }

        private static bool PutPose(object occ, OccurrenceTransform pose)
        {
            try
            {
                occ.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, occ,
                    new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception e) { Log.Warn("Porta-molde: PutMatrix — " + e.GetBaseException().Message); return false; }
        }

        /// <summary>Maior distância (mm) entre as duas poses, medida em pontos a 100 mm da origem nos três eixos locais.</summary>
        private static double PoseError(OccurrenceTransform a, OccurrenceTransform b)
        {
            double worst = 0;
            foreach (double[] p in new[] { new[] { 0.0, 0, 0 }, new[] { 0.1, 0, 0 }, new[] { 0, 0.1, 0 }, new[] { 0, 0, 0.1 } })
            {
                a.TransformPointM(p[0], p[1], p[2], out double ax, out double ay, out double az);
                b.TransformPointM(p[0], p[1], p[2], out double bx, out double by, out double bz);
                worst = Math.Max(worst, 1000 * Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz)));
            }
            return worst;
        }

        // ------------------------------------------------------------------ variáveis da submontagem

        /// <summary>A configuração na tabela de variáveis da submontagem — visível no SE e relida na próxima vez.</summary>
        private static void WriteConfig(dynamic vars, MoldBasePlan plan)
        {
            MoldBaseOptions o = plan.Options;
            MoldBasePlateModeler.SetVar(vars, MarkerVariable, 1, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Familia", (int)plan.Family, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Serie", plan.Family == MoldBaseFamily.Polimold ? double.Parse(plan.Series.Code, CultureInfo.InvariantCulture) : 0, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Guias", o.WithGuides ? 1 : 0, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_PinosRetorno", o.WithReturnPins ? 1 : 0, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_SubSerie", plan.SubSeries, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_P1", plan.P1, UnitDistance);
            MoldBasePlateModeler.SetVar(vars, "PM_P2", plan.P2, UnitDistance);
            MoldBasePlateModeler.SetVar(vars, "PM_Curso", plan.Stroke.Stroke, UnitDistance);
            MoldBasePlateModeler.SetVar(vars, "PM_CursoN", plan.Stroke.Index, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Espacador", plan.Stroke.SpacerHeight, UnitDistance);
            MoldBasePlateModeler.SetVar(vars, "PM_Tipo", o.Type, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Aco", o.Steel, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Montagem", o.Mounting, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Colunamento", char.ToUpperInvariant(o.Guiding) - 'A' + 1, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_Girado", plan.Rotated ? 1 : 0, UnitScalar);
            MoldBasePlateModeler.SetVar(vars, "PM_LadoFixo", o.FixedSideNegative ? -1 : 1, UnitScalar);
        }

        private static bool IsMoldBase(OccurrenceInfo occ, string file)
        {
            if (file != null && string.Equals(Path.GetFileName(file), AssemblyFileName, StringComparison.OrdinalIgnoreCase)) return true;
            try { return !double.IsNaN(MoldBasePlateModeler.GetVar(occ.ComOccurrence.OccurrenceDocument.Variables, MarkerVariable)); }
            catch { return false; }
        }

        private static ExistingMoldBase ReadExisting(OccurrenceInfo occ, string file)
        {
            var ex = new ExistingMoldBase { Occurrence = (object)occ.ComOccurrence, Path = file, Options = new MoldBaseOptions() };
            try
            {
                dynamic vars = occ.ComOccurrence.OccurrenceDocument.Variables;
                double V(string n) => MoldBasePlateModeler.GetVar(vars, n);
                double fam = V("PM_Familia");
                ex.Options.Family = double.IsNaN(fam) || fam < 1 || fam > 4 ? MoldBaseFamily.Polimold : (MoldBaseFamily)(int)fam;
                if (!double.IsNaN(V("PM_Guias"))) ex.Options.WithGuides = V("PM_Guias") > 0.5;
                if (!double.IsNaN(V("PM_PinosRetorno"))) ex.Options.WithReturnPins = V("PM_PinosRetorno") > 0.5;
                PolimoldSeries s = ex.Options.Family == MoldBaseFamily.Polimold
                    ? PolimoldCatalog.Find(((int)V("PM_Serie")).ToString(CultureInfo.InvariantCulture))
                    : MoldBaseStandards.UnitSeries(ex.Options.Family);
                ex.SeriesName = s?.Name;
                ex.P1 = V("PM_P1");
                ex.P2 = V("PM_P2");
                ex.Stroke = (int)V("PM_CursoN");
                if (!double.IsNaN(V("PM_Tipo"))) ex.Options.Type = (int)V("PM_Tipo");
                if (!double.IsNaN(V("PM_Aco"))) ex.Options.Steel = (int)V("PM_Aco");
                if (!double.IsNaN(V("PM_Montagem"))) ex.Options.Mounting = (int)V("PM_Montagem");
                if (!double.IsNaN(V("PM_Colunamento"))) ex.Options.Guiding = (char)('A' + (int)V("PM_Colunamento") - 1);
                ex.Options.FixedSideNegative = V("PM_LadoFixo") < 0;
                if (s != null && ex.Options.Family == MoldBaseFamily.Polimold)
                    ex.OrderCode = $"{s.Code} {ex.Options.Type} {(int)V("PM_SubSerie"):00} {ex.Options.Steel} {ex.Stroke} {ex.Options.Mounting}{ex.Options.Guiding}";
                else if (s != null)
                    ex.OrderCode = $"{s.Name} · {F(ex.P1)}/{F(ex.P2)}";
            }
            catch (Exception e) { Log.Warn("Porta-molde: variáveis do porta-molde existente ilegíveis — " + e.GetBaseException().Message); }
            return ex;
        }

        // ------------------------------------------------------------------ apoio

        /// <summary><c>Occurrence.GetRangeBox</c> — caixa em coordenadas da montagem (mm). Vale para peça e submontagem.</summary>
        public static bool TryRange(object occ, out double[] minMm, out double[] maxMm)
        {
            minMm = maxMm = null;
            try
            {
                object mn = new double[3], mx = new double[3];
                ((dynamic)occ).GetRangeBox(ref mn, ref mx);
                var a = (double[])mn; var b = (double[])mx;
                minMm = a.Select(v => v * 1000.0).ToArray();
                maxMm = b.Select(v => v * 1000.0).ToArray();
                return maxMm[0] >= minMm[0];
            }
            catch (Exception e) { Log.Warn("GetRangeBox: " + e.GetBaseException().Message); return false; }
        }

        private static double[] Unit(int i) { var v = new double[3]; v[i] = 1; return v; }
        private static double[] Scale(double[] v, double k) => new[] { v[0] * k, v[1] * k, v[2] * k };
        private static string F(double v) => MoldBasePlanner.F(v);
    }
}
