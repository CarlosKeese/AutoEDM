using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Machinability;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>O que o "Criar Eletrodos" inteligente aceita de fora.</summary>
    public sealed class SmartElectrodeOptions
    {
        public SmartPlanOptions Plan = new SmartPlanOptions();
        /// <summary>Ra das faces "17 - EROS. CAVIDADES" (rosa). Carlos, 2026-11-10: 1,6 no mínimo.</summary>
        public double ErodedCavityRa = 1.6;
        /// <summary>Ra das faces "16" (roxo, usinadas) que a fresa não alcança. Carlos, 2026-11-10: 3,2.</summary>
        public double MachinedRa = 3.2;
        /// <summary>Faces usinadas (roxo) que a fresa não faz também viram queima. T8 (Carlos, 2026-11-17): "deixando as áreas
        /// em roxo para usinagem e rosa para erosão" — padrão FALSO: só o rosa queima.</summary>
        public bool IncludeMachinedNotMillable = false;
        /// <summary>
        /// Dividir o eletrodo que a fresa não faz (T2; regra do Carlos de 2026-11-12). Padrão (T2c/C2): só divide se
        /// TODOS os pedaços saírem fresáveis; senão entrega o eletrodo inteiro marcado "corte a fio".
        /// </summary>
        public SplitMode Split = SplitMode.Off;   // T9: padrão = eletrodo INTEIRO; quina e guilhotina são experimentais
        /// <summary>T9: com faces selecionadas, constrói UM eletrodo só com elas (modo semiautomático). False ignora a seleção.</summary>
        public bool UseSelection = true;
        /// <summary>T9: a seleção já lida (o botão lê uma vez e usa no plano e na construção). Null: lê a do documento ativo.</summary>
        public List<SelectedBurnFace> Selection;
        public SplitOptions SplitRules = new SplitOptions();
        /// <summary>Furação da fixação (null = a padrão: M6 broca Ø5 × 10, pinos Ø4 × 7).</summary>
        public FixationPattern Fixation;
        /// <summary>T3: recuar as faces de queima o GAP do Ra (a peça do eletrodo termina em ORDENADO). DESLIGADO por padrão
        /// enquanto o FaceOffsets.AddEx não vinga nesta peça (medido 2026-11-13: igFeatureFailed até com 1 face e 0,001 mm).</summary>
        public bool ApplyGap = false;
        /// <summary>T5: agrupamento das regiões-problema (divisão PADRÃO quando o inteiro não é fresável).</summary>
        public RegionOptions Regions = new RegionOptions();
        /// <summary>T6: divisão NA QUINA — profundidade máxima e nº máximo de eletrodos que saem de um inteiro.</summary>
        public int CornerMaxDepth = 6;
        public int CornerMaxPieces = 16;
    }

    /// <summary>Um eletrodo construído (ou a tentativa).</summary>
    public sealed class SmartBuilt
    {
        public int Index;
        public string Path;
        public bool Ok;
        public string Failure;
        public double VolumeMm3;
        /// <summary>Só a ponta (abaixo do fechamento) e só o pescoço, em mm³.</summary>
        public double TipVolumeMm3, NeckVolumeMm3;
        /// <summary>Raios internos medidos no eletrodo construído (mm, montagem).</summary>
        public List<SplitFinding> Findings;
        /// <summary>Conferência entre o previsto pela peça e o medido no eletrodo.</summary>
        public string Prediction;
        /// <summary>O eletrodo não foi salvo: os achados medidos nele pediram nova divisão (passo 5 da T2).</summary>
        public SplitPlan Resplit;
        /// <summary>T5: o eletrodo inteiro não saiu fresável e foi separado nestas regiões (o chamador constrói o principal e as regiões).</summary>
        public List<EdmRegion> Regions;
        public List<FaceRec> RegionFaces;
        /// <summary>T5: cantos vivos internos entre duas paredes, abaixo do fechamento (regra do Carlos).</summary>
        public int SharpCorners;
        public double ReliefRemovedMm3;
        public string RegionNote;
        /// <summary>T6: o veredito do juiz único e, se o eletrodo foi dividido na quina, o corte (o chamador constrói as partes).</summary>
        public MillJudgeResult Judge;
        /// <summary>T9: origem da peça do eletrodo na montagem _EDM (mm): local + isto = posição na montagem (1ª posição).</summary>
        public double[] OriginMm;
        public double PlaneRemovedMm3;
        /// <summary>T7: o plano da divisão na quina (partes do referencial do inteiro) e como refazê-lo forçando reunir partes.</summary>
        public List<CornerCell> CornerPlan;
        public Func<ISet<string>, (List<CornerCell> cells, List<string> log)> Replan;
        public readonly List<string> CornerLog = new List<string>();
        /// <summary>Sai "corte a fio": o motivo.</summary>
        public string WireCut;
        /// <summary>T2c/C1: a ponta antes de tirar o enchimento, e o que saiu (enchimentos; lâminas da sobreposição), mm³.</summary>
        public double TipBeforeCutsMm3, FillerRemovedMm3, StripRemovedMm3;
        /// <summary>T2c: os achados que restam no eletrodo construído, agrupados pelo motivo (null: nenhum).</summary>
        public string Remaining;
        /// <summary>T2c/C2: pedaço interrompido antes de salvar porque não saiu fresável (a divisão será desfeita).</summary>
        public bool Aborted;
        public string PieceOf;
        public readonly List<string> CutLog = new List<string>();
        /// <summary>T3: o GAP aplicado (null: não aplicado) e as conferências depois dele.</summary>
        public ElectrodeGap.Result Gap;
        public RibMeasure RibBefore, RibAfter;
        public double DeepestBeforeMm = double.NaN, DeepestAfterMm = double.NaN;
        public string MachiningAfterGap, RemainingAfterGap;
        public readonly List<string> HolesAfterGap = new List<string>();
        /// <summary>T2b: o que aconteceu com cada furo da fixação.</summary>
        public readonly List<string> Holes = new List<string>();
        public string Base;
        public string Machining;
        public int Placed;
        public readonly List<string> Warnings = new List<string>();
    }

    public sealed class SmartElectrodeReport
    {
        public string AssemblyPath;
        /// <summary>A montagem de erosão em que o plano foi feito (a ativa, ou a criada a partir da peça).</summary>
        public object Assembly;
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Scan = new List<string>();
        public List<SmartElectrode> Electrodes = new List<SmartElectrode>();
        public readonly List<SmartBuilt> Built = new List<SmartBuilt>();
        public string Failure;
        public int RegionCount;

        public string Describe()
        {
            var sb = new StringBuilder();
            if (Failure != null) { sb.AppendLine("FALHOU: " + Failure); }
            if (AssemblyPath != null) sb.AppendLine("Montagem: " + AssemblyPath);
            foreach (string n in Notes) sb.AppendLine("• " + n);
            if (Scan.Count > 0) { sb.AppendLine(); sb.AppendLine("Queima encontrada:"); foreach (string s in Scan) sb.AppendLine("  " + s); }
            if (Electrodes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{RegionCount} região(ões) de queima → {Electrodes.Count} eletrodo(s):");
                foreach (SmartElectrode e in Electrodes)
                {
                    sb.AppendLine("  " + e.Describe());
                    foreach (string n in e.Notes) sb.AppendLine("      ! " + n);
                }
            }
            if (Built.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Construídos:");
                foreach (SmartBuilt b in Built)
                {
                    sb.AppendLine(b.Ok
                        ? string.Format(CultureInfo.InvariantCulture, "  E{0:00}  ✓ {1}  ponta {2:0.##} + pescoço {3:0.##} mm³  {4}  {5}  ({6} posição(ões))",
                            b.Index, System.IO.Path.GetFileName(b.Path), b.TipVolumeMm3, b.NeckVolumeMm3, b.Base, b.Machining, b.Placed)
                        : $"  E{b.Index:00}  ✗ {b.Failure}");
                    if (b.PieceOf != null) sb.AppendLine("      pedaço de " + b.PieceOf);
                    if (b.TipBeforeCutsMm3 > 0 && (b.FillerRemovedMm3 > 0 || b.StripRemovedMm3 > 0 || b.CutLog.Count > 0))
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "      recortes: ponta {0:0.###} → {1:0.###} mm³ (enchimentos −{2:0.####}, lâminas da divisa −{3:0.####}, alívio das regiões −{4:0.###})",
                            b.TipBeforeCutsMm3, b.TipVolumeMm3, b.FillerRemovedMm3, b.StripRemovedMm3, b.ReliefRemovedMm3));
                    foreach (string c in b.CutLog) sb.AppendLine("        · " + c);
                    if (b.Judge != null)
                    {
                        // T9: o juiz (com o GAP do Ra) é o veredito; cada achado com a posição na montagem _EDM
                        sb.AppendLine("      veredito: " + JudgeVerdict(b.Judge));
                        foreach (string line in JudgeLines(b.Judge, b.OriginMm)) sb.AppendLine("        · " + line);
                    }
                    else
                    {
                        if (b.Prediction != null) sb.AppendLine("      achados: " + b.Prediction);
                        sb.AppendLine("      restam: " + (b.Remaining ?? (b.Findings != null ? "nenhum raio que a fresa não faça — fresável" : "?")));
                    }
                    if (b.WireCut != null) sb.AppendLine("      CORTE A FIO: " + b.WireCut);
                    foreach (string c in b.CornerLog) sb.AppendLine("      plano: " + c);
                    if (b.Gap != null)
                    {
                        ElectrodeGap.Result g = b.Gap;
                        sb.AppendLine(g.Ok
                            ? string.Format(CultureInfo.InvariantCulture,
                                "      GAP {0:0.00} mm (Ra {1:0.0}) ✓ '{2}' em {3} face(s) de queima, volume {4:0.###} → {5:0.###} mm³ (peça em ORDENADO; troca {6:0.0} s)",
                                g.GapMm, g.Ra, g.FeatureName, g.Matched, g.VolumeBeforeMm3, g.VolumeAfterMm3, g.SwitchSeconds)
                            : $"      GAP {g.GapMm:0.00} mm (Ra {g.Ra:0.0}) ✗ {g.Failure}");
                        foreach (string n in g.Notes) sb.AppendLine("        ! " + n);
                        if (g.Ok)
                            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "        medida: {0} → {1}; fundo Z {2:0.####} → {3:0.####}; tirou {4:0.###} mm³ (área × GAP {5:0.###})",
                                b.RibBefore?.ToString() ?? "sem nervura", b.RibAfter?.ToString() ?? "—", b.DeepestBeforeMm, b.DeepestAfterMm,
                                g.VolumeBeforeMm3 - g.VolumeAfterMm3, g.ExpectedRemovedMm3));
                        if (b.MachiningAfterGap != null) sb.AppendLine("        depois do GAP: " + b.MachiningAfterGap + (b.RemainingAfterGap != null ? " — " + b.RemainingAfterGap : ""));
                        foreach (string h in b.HolesAfterGap) sb.AppendLine("        furo depois do GAP: " + h);
                    }
                    foreach (string h in b.Holes) sb.AppendLine("      furo: " + h);
                    foreach (string w in b.Warnings) sb.AppendLine("      ! " + w);
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string JudgeVerdict(MillJudgeResult r) => MillJudge.Verdict(r);
        private static IEnumerable<string> JudgeLines(MillJudgeResult r, double[] origin) => MillJudge.ReportLines(r, origin);
    }

    public sealed partial class ElectrodeBuilder
    {
        // Cores dos estilos da casa (FinishCatalog): 17 = cavidades erodidas, 16 = cavidades usinadas.
        private static readonly Color ErodedCavityColor = Color.FromArgb(176, 0, 178);
        private static readonly Color MachinedCavityColor = Color.FromArgb(116, 54, 178);

        /// <summary>
        /// O "Criar Eletrodos" inteligente (regras do Carlos de 2026-11-10; plano em docs/PLANO_CRIAR_ELETRODOS.md).
        /// <list type="number">
        /// <item>Numa PEÇA: cria/abre a montagem <c>&lt;código&gt;_EDM.asm</c> com a peça no zero-máquina.</item>
        /// <item>Acha a queima em TODAS as peças: rosa ("17", Ra 1,6), cores de Ra e roxo ("16") que a fresa não faz (Ra 3,2).</item>
        /// <item>Divide por aresta compartilhada; une regiões que cabem num perfil de cobre; dobra geometria idêntica.</item>
        /// <item>Com <paramref name="build"/>: cada eletrodo nasce SÓLIDO (prisma − cavidade) com faixa, bloco e fixação,
        /// e é inserido em cada posição.</item>
        /// </list>
        /// Sem <paramref name="build"/> só devolve o plano. Não salva a montagem.
        /// </summary>
        public SmartElectrodeReport CreateSmart(dynamic activeDoc, ElectrodeParams p, SmartElectrodeOptions o = null, bool build = false)
        {
            o = o ?? new SmartElectrodeOptions();
            p = p ?? new ElectrodeParams();
            var report = new SmartElectrodeReport();
            if (_connector.Application == null) throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            dynamic app = _connector.Application;

            // 0) T9: faces selecionadas → modo semiautomático (lidas ANTES da _EDM abrir: a troca de documento mata a seleção)
            List<SelectedBurnFace> selection = o.UseSelection ? o.Selection : null;   // lida antes pelo botão (o plano troca o documento)
            if (o.UseSelection && selection == null)
            {
                var selNotes = new List<string>();
                try { selection = ReadSelectionForSmart(activeDoc, selNotes); }
                catch (Exception ex) { selNotes.Add("seleção ilegível: " + ex.GetBaseException().Message); }
                report.Notes.AddRange(selNotes);
            }
            bool semi = selection != null && selection.Count > 0;

            // 1) a montagem de erosão
            EdmAssemblyResult edm = EdmAssemblyBuilder.Ensure((object)app, (object)activeDoc, _faceSelector);
            report.Notes.AddRange(edm.Notes);
            if (edm.Assembly == null) { report.Failure = edm.Failure ?? "sem montagem."; return report; }
            dynamic asmDoc = edm.Assembly;
            report.Assembly = edm.Assembly;
            report.AssemblyPath = edm.Path;
            var ctx = new AssemblyContext(asmDoc);

            // 2) queima → regiões (ou, no semiautomático, a região única das faces selecionadas)
            List<(int id, BoundingBox box)> burnOfOwner = null;
            List<SmartRegion> regions;
            if (semi)
            {
                report.Notes.Add($"modo semiautomático: 1 eletrodo com {selection.Count} face(s) selecionada(s).");
                regions = SelectedRegions(app, ctx, selection, o, report, out burnOfOwner);
                o.Plan.Merge = true;
                o.Plan.MergeGapMm = Math.Max(o.Plan.MergeGapMm, 500.0);
            }
            else regions = ScanBurnRegions(app, ctx, o, report);
            report.RegionCount = regions.Count;
            if (regions.Count == 0)
            {
                report.Notes.Add("nenhuma face de queima: nem rosa (\"17 - EROS. CAVIDADES\"), nem cor de Ra, nem roxo fora do alcance das fresas.");
                return report;
            }

            // 3) plano
            o.Plan.Material = p.Material ?? o.Plan.Material;
            o.Plan.NeckMm = p.HolderBaseClearanceMm;
            report.Electrodes = SmartElectrodePlanner.Plan(regions, _blankLibrary, o.Plan);
            Log.Info($"Criar eletrodos: {regions.Count} região(ões) → {report.Electrodes.Count} eletrodo(s).");
            if (semi)
            {
                if (report.Electrodes.Count != 1) report.Notes.Add($"ATENÇÃO: as faces selecionadas saíram em {report.Electrodes.Count} eletrodos (não cabem num perfil só).");
                foreach (SmartElectrode se in report.Electrodes) SelectionWarnings(se, o, burnOfOwner, report);
            }

            // 3b) divisão do que a fresa não faz, prevista pela PEÇA (o raio convexo dela é o raio interno do eletrodo)
            //     (sem "dividir", a previsão é feita do mesmo jeito: ela é conferida contra o eletrodo construído)
            report.Electrodes = SplitWhatTheMillCannotDo(app, report.Electrodes, o, report);
            if (!build) return report;

            // 4) construção
            string folder = ElectrodeNaming.ResolveElectrodeFolder(asmDoc, p);
            System.IO.Directory.CreateDirectory(folder);
            string prefix = ElectrodeNaming.ElectrodeNamePrefix(asmDoc);
            int first = ElectrodeNaming.NextElectrodeIndex(ctx, prefix, folder);   // T9: nunca sobrescreve um EE anterior
            int running = first;   // os números saem em sequência, na ordem em que os eletrodos são construídos
            string PathOf(int n) => System.IO.Path.Combine(folder, $"{prefix}{n:00}.par");
            var final = new List<SmartElectrode>();
            int k = 0;
            while (k < report.Electrodes.Count)
            {
                SmartElectrode e = report.Electrodes[k];
                if (o.Split == SplitMode.Auto && e.Parent != null)
                {
                    // T2c/C2: os pedaços de um eletrodo só ficam se TODOS saírem fresáveis; senão, o inteiro.
                    var group = new List<SmartElectrode>();
                    while (k < report.Electrodes.Count && report.Electrodes[k].Parent == e.Parent) group.Add(report.Electrodes[k++]);
                    running = BuildPiecesOrWhole((object)app, (object)asmDoc, group, running, first, (Func<int, string>)PathOf, p, o, report, final);
                    continue;
                }
                k++;
                final.Add(e);
                int number = running++;
                e.Index = number - first + 1;
                string path = PathOf(number);
                bool corner = o.Split == SplitMode.Auto;
                SmartBuilt b = BuildSmartElectrode(app, asmDoc, e, number, path, p, o, mayResplit: o.Split == SplitMode.Force, cornerSplit: corner);
                if (b.CornerPlan != null)
                {
                    // T7: o inteiro não saiu fresável — dividido NA QUINA pelo plano; cada parte é construída e julgada.
                    final.Remove(e);
                    report.Notes.Add(string.Format(CultureInfo.InvariantCulture, "E{0:00} inteiro: ponta {1:0.###} mm³, {2}.", number, b.TipVolumeMm3, b.Machining));
                    running = BuildPlannedParts(app, asmDoc, e, b, number, first, (Func<int, string>)PathOf, p, o, report, final);
                    continue;
                }
                if (b.Resplit == null)
                {
                    report.Built.Add(b);
                    if (!b.Ok && e.Footprint.HasValue)
                    {
                        BoundingBox f = e.Footprint.Value;
                        report.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                            "ATENÇÃO: o pedaço E{0:00} de {1} falhou — a queima em x {2:0.##}…{3:0.##}, y {4:0.##}…{5:0.##} ficou SEM eletrodo.",
                            number, e.PieceOf, f.MinX, f.MaxX, f.MinY, f.MaxY));
                    }
                    continue;
                }

                // Passo 5 da T2: o eletrodo construído ainda tem raio que a fresa não faz e os achados MEDIDOS nele
                // dividem a pegada: refaz uma vez, em pedaços; o que sobrar depois disso sai "corte a fio".
                List<SmartElectrode> again = PiecesOf(e, b.Resplit, o);
                report.Notes.Add($"E{e.Index:00} redividido pelos achados medidos no eletrodo construído: {b.Resplit.Describe()}.");
                final.Remove(e);
                bool firstPiece = true;
                foreach (SmartElectrode pe in again)
                {
                    int n = firstPiece ? number : running++;
                    pe.Index = n - first + 1;
                    final.Add(pe);
                    firstPiece = false;
                    report.Built.Add(BuildSmartElectrode(app, asmDoc, pe, n, PathOf(n), p, o, mayResplit: false));
                }
                e.Notes.Add($"redividido em {again.Count} pedaço(s) (E{number:00} e seguintes)");
            }
            report.Electrodes = final;
            return report;
        }

        /// <summary>
        /// T2c/C2: constrói os pedaços de UM eletrodo sem inseri-los; o pedaço que não sai fresável (medido nele depois de
        /// tirar o enchimento) interrompe tudo: os pedaços já salvos são apagados e o eletrodo inteiro é construído, com a
        /// nota "a divisão não resolve" e a marca "corte a fio". Devolve o próximo número livre.
        /// </summary>
        private int BuildPiecesOrWhole(dynamic app, dynamic asmDoc, List<SmartElectrode> group, int running, int first,
            Func<int, string> pathOf, ElectrodeParams p, SmartElectrodeOptions o, SmartElectrodeReport report, List<SmartElectrode> final)
        {
            SmartElectrode whole = group[0].Parent;
            int start = running;
            var built = new List<SmartBuilt>();
            var remaining = new List<int>();
            foreach (SmartElectrode pe in group)
            {
                int n = running++;
                pe.Index = n - first + 1;
                SmartBuilt b = BuildSmartElectrode(app, asmDoc, pe, n, pathOf(n), p, o, mayResplit: false, place: false, abortIfNotMillable: true);
                built.Add(b);
                remaining.Add(b.Ok ? b.Findings?.Count ?? 0 : (b.Aborted ? Math.Max(1, b.Findings?.Count ?? 1) : -1));
                if (!b.Ok) break;
            }
            if (SplitDecision.KeepPieces(SplitMode.Auto, remaining, out string why))
            {
                for (int i = 0; i < group.Count; i++)
                {
                    PlaceElectrode(asmDoc, group[i], built[i]);
                    report.Built.Add(built[i]);
                    final.Add(group[i]);
                }
                return running;
            }

            // Desfaz: os pedaços salvos não chegaram a ser inseridos — basta apagar os arquivos.
            SmartBuilt last = built[built.Count - 1];
            string detail = last.Aborted ? $"{last.PieceOf ?? whole.PieceOf} não saiu fresável ({last.Remaining})" : $"um pedaço falhou ({last.Failure})";
            foreach (SmartBuilt b in built.Where(b => b.Ok))
            {
                try { if (System.IO.File.Exists(b.Path)) System.IO.File.Delete(b.Path); }
                catch (Exception ex) { Log.Warn($"Desfazer divisão: '{b.Path}' ficou — {ex.GetBaseException().Message}"); }
            }
            string note = why + " — " + detail + "; os pedaços foram desfeitos e o eletrodo sai INTEIRO";
            Log.Info($"Criar eletrodos: {note}.");
            report.Notes.Add($"{group[0].PieceOf}: {note}.");
            whole.WireCut = why;
            whole.Notes.Add(note);
            int number = start;
            whole.Index = number - first + 1;
            final.Add(whole);
            report.Built.Add(BuildSmartElectrode(app, asmDoc, whole, number, pathOf(number), p, o, mayResplit: false));
            return number + 1;
        }

        /// <summary>Faces de queima de cada peça da montagem, agrupadas por aresta compartilhada.</summary>
        private List<SmartRegion> ScanBurnRegions(dynamic app, AssemblyContext ctx, SmartElectrodeOptions o, SmartElectrodeReport report)
        {
            var regions = new List<SmartRegion>();
            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(occ.Name ?? "", @"_EE\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                dynamic doc = occ.OccurrenceDocument;
                int type = 0; try { type = (int)doc.Type; } catch { }
                if (type != 1) continue;   // submontagem / chapa: fora do escopo desta versão

                OccurrenceTransform pose = AssemblyContext.TryGetPose(occ);
                var burn = new List<SelectedFace>();
                var info = new Dictionary<SelectedFace, SmartFace>();
                var machined = new List<SelectedFace>();
                int rosa = 0, raColor = 0;

                foreach (SelectedFace sf in _faceSelector.EnumerateColoredFaces(doc, app))
                {
                    Color c = sf.DetectedColor;
                    if (_raColorMap.TryGetRa(c, out double ra, out Color _))
                    { raColor++; AddBurn(sf, BurnKind.RaColor, ra, pose, burn, info); }
                    else if (OleColor.Matches(c, ErodedCavityColor, 8))
                    { rosa++; AddBurn(sf, BurnKind.ErodedCavity, o.ErodedCavityRa, pose, burn, info); }
                    else if (o.IncludeMachinedNotMillable && OleColor.Matches(c, MachinedCavityColor, 8)) machined.Add(sf);
                    else ComLifetime.Release(sf.ComFace);
                }

                // Roxo: é usinado, a menos que a fresa não faça (raio, diâmetro ou profundidade) — aí é eletrodo.
                int notMillable = 0;
                if (machined.Count > 0)
                {
                    var hitIds = new HashSet<int>();
                    try
                    {
                        object body = TryGetSolidBody(doc);
                        foreach (SmallRadiusHit hit in BRepRadiusProbe.Probe(body, ToolLadder.Shop())) hitIds.Add(hit.FaceId);
                    }
                    catch (Exception ex) { Log.Warn($"Criar eletrodos: usinabilidade de '{occ.Name}' não avaliada — {ex.GetBaseException().Message}"); }
                    foreach (SelectedFace sf in machined)
                    {
                        int id = 0; try { id = (int)sf.ComFace.ID; } catch { }
                        if (id > 0 && hitIds.Contains(id)) { notMillable++; AddBurn(sf, BurnKind.MachinedNotMillable, o.MachinedRa, pose, burn, info); }
                        else ComLifetime.Release(sf.ComFace);
                    }
                }

                if (burn.Count == 0) continue;
                IReadOnlyList<List<SelectedFace>> groups = _splitter.SplitByConnectivity(burn);
                foreach (List<SelectedFace> g in groups)
                {
                    var r = new SmartRegion { Owner = occ.Name, OwnerTag = occ };
                    foreach (SelectedFace sf in g) if (info.TryGetValue(sf, out SmartFace f)) r.Faces.Add(f);
                    if (r.Faces.Count > 0) regions.Add(r);
                }
                report.Scan.Add($"{occ.Name}: {rosa} rosa (Ra {o.ErodedCavityRa:0.0})" +
                                (raColor > 0 ? $", {raColor} com cor de Ra" : "") +
                                (machined.Count > 0 ? $", {notMillable} de {machined.Count} roxa(s) fora do alcance da fresa (Ra {o.MachinedRa:0.0})" : "") +
                                $" → {groups.Count} região(ões)");
            }
            return regions;
        }

        private static void AddBurn(SelectedFace sf, BurnKind kind, double ra, OccurrenceTransform pose,
            List<SelectedFace> burn, Dictionary<SelectedFace, SmartFace> info)
        {
            object face = sf.ComFace;
            if (!FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx)) return;
            if (pose != null) OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), mn, mx, out mn, out mx);
            int id = 0; double area = 0;
            try { id = (int)((dynamic)face).ID; } catch { }
            try { area = (double)((dynamic)face).Area * 1e6; } catch { }
            burn.Add(sf);
            info[sf] = new SmartFace { FaceId = id, Kind = kind, Ra = ra, AreaMm2 = area, MinMm = mn, MaxMm = mx, ComFace = face };
        }

        /// <summary>Um eletrodo: peça nova → ponta sólida (prisma − cavidade) → base → salvar → inserir em cada posição.</summary>
        private SmartBuilt BuildSmartElectrode(dynamic app, dynamic asmDoc, SmartElectrode e, int number, string path,
            ElectrodeParams p, SmartElectrodeOptions o, bool mayResplit = false, bool place = true, bool abortIfNotMillable = false, bool cornerSplit = false)
        {
            var built = new SmartBuilt { Index = number, Path = path, PieceOf = e.Footprint.HasValue ? e.PieceOf : null, OriginMm = new[] { e.CenterXMm, e.CenterYMm, e.DeepestZMm } };
            Log.Info($"Eletrodo {number}: construindo {(e.Planes != null ? $"parte {e.PieceOf} ({e.Planes.Count} corte(s) na quina)" : e.PieceOf ?? "inteiro")}.");
            dynamic partDoc = null;
            try
            {
                var owner = (OccurrenceInfo)e.OwnerTag;
                OccurrenceTransform ownerPose = AssemblyContext.TryGetPose(owner);
                if (ownerPose == null) { built.Failure = $"pose de '{owner.Name}' ilegível."; return built; }
                string cavityFile = null;
                try { cavityFile = (string)owner.ComOccurrence.OccurrenceFileName; } catch { }
                try { if ((bool)owner.OccurrenceDocument.Dirty) built.Warnings.Add($"'{owner.Name}' tem alterações não salvas: a ponta sai do arquivo SALVO."); } catch { }

                // Plano de fechamento na montagem: o plano exato do topo do aço, ou a face "10 - FECHAM." quando o topo
                // da peça não é o fechamento (regra 8 do Carlos).
                double refZ = ClosingZMm(app, owner, ownerPose, built.Warnings);
                if (double.IsNaN(refZ)) { built.Failure = $"caixa do corpo de '{owner.Name}' ilegível."; return built; }

                OccurrenceTransform electrodePose = OccurrenceTransform.Identity(ownerPose.Layout).WithTranslationM(
                    Units.MmToM(e.CenterXMm), Units.MmToM(e.CenterYMm), Units.MmToM(e.DeepestZMm));

                partDoc = HouseTemplates.Add((object)app, HouseDocumentKind.Part);
                try { if ((int)partDoc.ModelingMode != 1) partDoc.ModelingMode = 1; } catch { }
                RaVariableStore.TryWrite(partDoc, e.Ra);

                BoundingBox fp = e.PlanFootprint(o.Plan.MarginMm);
                double halfX = fp.SizeX / 2.0, halfY = fp.SizeY / 2.0;
                double bottomLocal = e.Footprint.HasValue ? Math.Min(0.0, e.ParentDeepestZMm - PieceBottomAllowanceMm - e.DeepestZMm) : 0.0;
                TipResult tip = ElectrodeTipBuilder.Build((object)partDoc, new TipRequest
                {
                    CavityFile = cavityFile,
                    CavityInElectrode = ownerPose.SeenFrom(electrodePose),
                    HalfXMm = halfX, HalfYMm = halfY,
                    RefZMm = refZ - e.DeepestZMm,
                    BottomZMm = bottomLocal,
                    NeckMm = o.Plan.NeckMm,
                    Label = $"Eletrodo {number}",
                    Cuts = CutsOf(e, e.DeepestZMm + bottomLocal, refZ, o.SplitRules, halfX, halfY),
                    Planes = e.Planes?.Select(c => c.Shifted(-e.CenterXMm, -e.CenterYMm, -e.DeepestZMm)).ToList()
                });
                built.PlaneRemovedMm3 = tip.PlaneRemovedMm3;
                built.Warnings.AddRange(tip.Warnings);
                built.TipBeforeCutsMm3 = tip.TipBeforeCutsMm3;
                built.FillerRemovedMm3 = tip.FillerRemovedMm3;
                built.StripRemovedMm3 = tip.StripRemovedMm3;
                built.ReliefRemovedMm3 = tip.ReliefRemovedMm3;
                built.CutLog.AddRange(tip.CutLog);
                if (!tip.Ok)
                {
                    built.Failure = "ponta: " + tip.Failure;
                    try { partDoc.Close(false); } catch { }
                    return built;
                }
                built.VolumeMm3 = tip.VolumeMm3;
                built.TipVolumeMm3 = tip.TipVolumeMm3;
                built.NeckVolumeMm3 = tip.VolumeMm3 - tip.TipVolumeMm3;

                // Usinabilidade do ELETRODO (o negativo): raios internos e cantos vivos que a fresa não faz.
                MillJudgeReader.Reading judged = null;
                try
                {
                    // T8: o juiz considera o GAP do Ra (o raio côncavo final é r + GAP)
                    double judgeGap = _offsetPolicy.GetInwardOffsetMm(new ElectrodePass("ACAB", e.Ra), p.Material);
                    judged = MillJudgeReader.Read((object)partDoc, refZ - e.DeepestZMm, MillJudgeReader.Options(judgeGap),
                        e.Planes?.Select(c => c.Shifted(-e.CenterXMm, -e.CenterYMm, -e.DeepestZMm)).ToList());
                    built.Judge = judged.Result;
                    built.Machining = judged.Result.Summary();
                    foreach (MillHit h in judged.Result.Failed) Log.Info($"  [juiz E{number:00}] REPROVA {h.Describe()} — {h.Why}");
                    foreach (MillHit h in judged.Result.Slivers) Log.Info($"  [juiz E{number:00}] lasca {h.Describe()}");
                }
                catch (Exception ex) { built.Machining = "usinabilidade não avaliada (" + ex.GetBaseException().Message + ")"; built.Warnings.Add("juiz: " + ex.GetBaseException().Message); }
                built.Findings = ElectrodeFindings((object)partDoc, e, refZ - e.DeepestZMm, o.SplitRules.AlignToleranceDeg);
                if (e.PredictedFindings != null)
                {
                    built.Prediction = CompareFindings(e.PredictedFindings.Where(f => Touches(f, e.PlanFootprint(o.Plan.MarginMm))).ToList(), built.Findings);
                    Log.Info($"Eletrodo {number}: conferência dos achados — {built.Prediction}");
                }
                built.Remaining = DescribeRemaining(e.PredictedFindings, built.Findings, e.Piece != null);
                if (built.Remaining != null) Log.Info($"Eletrodo {number}: restam {built.Remaining}");
                // T6: o JUIZ ÚNICO manda (cantos vivos de qualquer par de faces e inclinação, raios, contra-saída, canal,
                // alcance). No modo padrão, eletrodo reprovado com quina divisível é dividido NA QUINA: o chamador constrói
                // as duas partes e julga cada uma de novo.
                if (built.Judge != null)
                {
                    built.SharpCorners = built.Judge.Corners;
                    if (!built.Judge.Millable)
                        built.Remaining = (built.Remaining != null ? built.Remaining + "; " : "") + "juiz: " + built.Judge.Summary();
                    else if (built.Findings.Count > 0)
                        built.Warnings.Add("o BRepRadiusProbe acusa raio que o juiz passou: " + built.Remaining);
                }
                if (cornerSplit && built.Judge != null && !built.Judge.Millable && o.Split == SplitMode.Auto && e.Piece == null && judged != null)
                {
                    // T7: o PLANO inteiro da divisão (cantos, raios com enchimento, recuo vertical, reunião) antes de construir
                    var rect = new BoundingBox { MinX = -halfX, MaxX = halfX, MinY = -halfY, MaxY = halfY };
                    double zLo = Math.Min(0.0, bottomLocal) - 0.1, zHi = refZ - e.DeepestZMm;
                    MillJudgeReader.Reading jr = judged;
                    var popt = new CornerPlanOptions { MaxDepth = o.CornerMaxDepth, MaxCells = o.CornerMaxPieces, FillerClearanceMm = o.SplitRules.FillerClearanceMm };
                    built.Replan = small =>
                    {
                        var plog = new List<string>();
                        List<CornerCell> cells = CornerPlanner.Plan(jr.Faces, jr.Edges, jr.Result, rect, zLo, zHi, popt, plog, small);
                        return (cells, plog);
                    };
                    var (plan, log) = built.Replan(null);
                    foreach (string l in log) Log.Info($"Eletrodo {number}: [plano] {l}");
                    built.CornerLog.AddRange(log);
                    if (plan.Count >= 2)
                    {
                        built.CornerPlan = plan;
                        built.Failure = "dividido na quina (ver as partes)";
                        partDoc.Close(false);
                        partDoc = null;
                        return built;
                    }
                    Log.Info($"Eletrodo {number}: o plano da divisão não separou nada — o eletrodo fica inteiro.");
                }
                if (built.Findings.Count > 0 && abortIfNotMillable)
                {
                    // T2c/C2: pedaço que não sai fresável — o chamador desfaz a divisão; nada é salvo.
                    built.Aborted = true;
                    built.Failure = "não saiu fresável: " + built.Remaining;
                    partDoc.Close(false);
                    partDoc = null;
                    return built;
                }
                if (built.Findings.Count > 0 && o.Split != SplitMode.Off)
                {
                    // Passo 5: sobrou raio que a fresa não faz. Uma vez: redivide pelos achados medidos (o chamador
                    // constrói os pedaços); se não dividir, ou já for a segunda vez, o eletrodo sai "corte a fio".
                    SplitPlan again = mayResplit ? ResplitPlan(e, built.Findings, o) : null;
                    if (again != null)
                    {
                        built.Resplit = again;
                        built.Failure = "redividido (ver os pedaços)";
                        partDoc.Close(false);
                        partDoc = null;
                        return built;
                    }
                    built.WireCut = (e.WireCut != null ? e.WireCut + "; " : "") + "no eletrodo construído: " + built.Remaining;
                }
                else if (built.Findings.Count > 0) built.WireCut = $"{built.Findings.Count} raio(s) interno(s) que a fresa não faz (divisão desligada)";
                if (built.Judge != null && !built.Judge.Millable && built.WireCut == null)
                    built.WireCut = (e.WireCut != null ? e.WireCut + "; " : "") + "juiz: " + built.Judge.Summary();
                if (built.Judge != null && !built.Judge.Millable)
                    built.WireCut += " — " + string.Join("; ", built.Judge.Failed.Select(h => h.Describe()));
                // T8: o juiz (com o GAP) é o veredito único — raio que o BRepRadiusProbe acusa e o juiz passa não manda para fio
                if (built.Judge != null && built.Judge.Millable && o.Split == SplitMode.Auto) built.WireCut = null;
                // T9: no padrão (inteiro) não existe "corte a fio" — o veredito é "fresável" ou "precisa dividir" (fio é decisão do Carlos)
                if (o.Split == SplitMode.Off) built.WireCut = null;

                // T3: as faces de queima, guardadas pela GEOMETRIA agora (síncrono, só a ponta e o pescoço): a troca para
                // ordenado, no fim, mata as referências de face.
                double refLocal = refZ - e.DeepestZMm;
                List<FaceSig> burnSigs = null;
                if (o.ApplyGap)
                {
                    try
                    {
                        burnSigs = ElectrodeGap.BurnFaces((object)partDoc, refLocal, halfX, halfY);
                        built.RibBefore = ElectrodeGap.NarrowestRib(burnSigs);
                        if (burnSigs.Count > 0) built.DeepestBeforeMm = ElectrodeGap.DeepestZ(burnSigs);
                        Log.Info($"Eletrodo {number}: {burnSigs.Count} face(s) de queima guardadas para o GAP; {built.RibBefore?.ToString() ?? "sem nervura"}.");
                    }
                    catch (Exception ex) { built.Warnings.Add("faces de queima para o GAP ilegíveis: " + ex.GetBaseException().Message); burnSigs = null; }
                }

                // Base: faixa + bloco + fixação, a partir do topo do pescoço (o que o Criar Base já faz).
                var blocks = new SurfaceBlockBuilder(_blankLibrary);
                var bopt = new BlockOverSurfacesOptions
                {
                    Material = p.Material, GapMm = 0.0, ChosenBlank = e.Blank, SwitchToOrdered = false,
                    BandMarginMm = o.Plan.BandMarginMm, BlockOversizeMm = o.Plan.BlockOversizeMm, BandHeightMm = o.Plan.BandHeightMm
                };
                if (o.Fixation != null) bopt.Fixation = o.Fixation;
                var foot = new BoundingBox { MinX = -halfX, MaxX = halfX, MinY = -halfY, MaxY = halfY, MinZ = 0.0, MaxZ = tip.TopZMm };
                BlockOverSurfacesPlan bplan = blocks.PlanForFootprint(foot, bopt, "ponta sólida");
                // Barra deitada: a espessura dela contém a PONTA também, não só faixa + bloco.
                if (bplan.ChosenBlank != null && bplan.ChosenBlank.LaidDown && bplan.ChosenBlank.TotalHeightMm.HasValue)
                    bplan.BlockHmm = Math.Max(bplan.ChosenBlank.TotalHeightMm.Value - o.Plan.BandHeightMm - tip.TopZMm, o.Plan.MinBlockMm);
                BlockOverSurfacesResult bres = blocks.BuildOnPlan(partDoc, bopt, bplan, preview: true);
                built.Base = (bplan.ChosenBlank?.Describe() ?? "bloco cru") +
                             $" (bloco {(bres.BlockCreated ? "✓" : "✗")}, faixa {(bres.BandCreated ? "✓" : "✗")}, fixação {(bres.FixationApplied ? "✓" : "✗")})";
                built.Warnings.AddRange(bres.Warnings);
                // T2b: o furo pode entrar no pescoço e na ponta, mas não pode estourar o cobre.
                if (bres.FixationApplied && BlankModeler.FixationHolesFit(bplan.BlockXmm, bplan.BlockYmm, bopt.Fixation))
                {
                    built.Holes.AddRange(FixationHoleGuard.Ensure(app, bopt.Fixation, bplan.BlockXmm, bplan.BlockYmm, bplan.CenterXmm, bplan.CenterYmm));
                }
                int bodies = 0; try { bodies = (int)((dynamic)app.ActiveDocument).Models.Count; } catch { }
                if (bodies > 1) built.Warnings.Add($"a peça ficou com {bodies} corpos — a base não fundiu com a ponta.");

                // Os furos re-adquirem o documento: salva pelo documento ATIVO, que é esta peça.
                dynamic live = app.ActiveDocument;

                // T3: GAP por último (troca a peça NOVA para ordenado), depois as mesmas conferências de novo.
                if (burnSigs != null && burnSigs.Count > 0)
                    ApplyGapAndRecheck(app, (object)live, e, built, burnSigs, refLocal, p, o,
                        bres.FixationApplied && BlankModeler.FixationHolesFit(bplan.BlockXmm, bplan.BlockYmm, bopt.Fixation) ? bopt.Fixation : null,
                        bplan, number);
                else if (o.ApplyGap) built.Warnings.Add("GAP não aplicado: nenhuma face de queima achada.");
                live.SaveAs(path);
                live.Close();
                partDoc = null;

                built.Ok = true;
                if (place) PlaceElectrode((object)asmDoc, e, built);
                Log.Info($"Eletrodo {number}: {System.IO.Path.GetFileName(path)} criado, {built.Placed} posição(ões) ✓");
                return built;
            }
            catch (Exception ex)
            {
                built.Failure = ex.GetBaseException().Message;
                Log.Warn($"Eletrodo {number} falhou: {built.Failure}");
                try { if (partDoc != null) partDoc.Close(false); } catch { }
                return built;
            }
        }

        /// <summary>
        /// T3: aplica o GAP (<see cref="ElectrodeGap.Apply"/>) e repete, já em ORDENADO, a medida de uma nervura e do fundo,
        /// a usinabilidade e a conferência dos furos (que pode encurtar um furo que o GAP tenha feito estourar).
        /// </summary>
        private void ApplyGapAndRecheck(dynamic app, object partDoc, SmartElectrode e, SmartBuilt built, List<FaceSig> burnSigs,
            double refLocal, ElectrodeParams p, SmartElectrodeOptions o, FixationPattern fixation, BlockOverSurfacesPlan bplan, int number)
        {
            double gap = _offsetPolicy.GetInwardOffsetMm(new ElectrodePass("ACAB", e.Ra), p.Material);
            built.Gap = ElectrodeGap.Apply(partDoc, burnSigs, gap, e.Ra, $"Eletrodo {number}");
            if (!built.Gap.Ok) { built.Warnings.Add("GAP: " + built.Gap.Failure); return; }
            try
            {
                List<FaceSig> after = ElectrodeGap.ReadFaces(partDoc);
                if (built.RibBefore != null) built.RibAfter = ElectrodeGap.NarrowestRib(after, built.RibBefore, gap + 0.05);
                built.DeepestAfterMm = ElectrodeGap.DeepestZ(after);
            }
            catch (Exception ex) { built.Warnings.Add("medida depois do GAP falhou: " + ex.GetBaseException().Message); }
            built.MachiningAfterGap = DescribeElectrodeMachining(partDoc, refLocal);
            try
            {
                List<SplitFinding> f = ElectrodeFindings(partDoc, e, refLocal, o.SplitRules.AlignToleranceDeg);
                if (f.Count > 0)
                    built.RemainingAfterGap = string.Format(CultureInfo.InvariantCulture, "{0} raio(s) interno(s): R {1}", f.Count,
                        string.Join(", ", f.GroupBy(x => Math.Round(x.RadiusMm, 3)).OrderBy(g => g.Key).Select(g => g.Count() + "× " + g.Key.ToString("0.###", CultureInfo.InvariantCulture))));
            }
            catch (Exception ex) { built.Warnings.Add("achados depois do GAP: " + ex.GetBaseException().Message); }
            if (fixation != null)
                built.HolesAfterGap.AddRange(FixationHoleGuard.Ensure(app, fixation, bplan.BlockXmm, bplan.BlockYmm, bplan.CenterXmm, bplan.CenterYmm));
        }

        /// <summary>Insere o eletrodo salvo em cada posição do plano.</summary>
        private static void PlaceElectrode(dynamic asmDoc, SmartElectrode e, SmartBuilt built)
        {
            OccurrenceTransform ownerPose = AssemblyContext.TryGetPose((OccurrenceInfo)e.OwnerTag);
            if (ownerPose == null) { built.Warnings.Add("pose da peça ilegível: o eletrodo não foi inserido."); return; }
            foreach (SmartPlacement at in e.Placements)
            {
                dynamic occ = asmDoc.Occurrences.AddByFilename(built.Path);
                double rad = at.RotZDeg * Math.PI / 180.0;
                double c = Math.Round(Math.Cos(rad), 12), s = Math.Round(Math.Sin(rad), 12);
                OccurrenceTransform pose = OccurrenceTransform.Identity(ownerPose.Layout).ComposedWith(
                    new[] { c, -s, 0, s, c, 0, 0, 0, 1.0 },
                    new[] { Units.MmToM(at.XMm), Units.MmToM(at.YMm), Units.MmToM(at.ZMm) });
                if (TryPutMatrix(occ, pose, built.Index)) built.Placed++;
            }
        }

        /// <summary>
        /// T2c/C1: as caixas do enchimento (e da lâmina da sobreposição) do pedaço, levadas ao referencial do eletrodo
        /// (origem no centro da pegada e no fundo da queima). Null para eletrodo inteiro.
        /// </summary>
        /// <summary>As caixas a tirar da ponta: enchimento/lâmina (pedaço da guilhotina) e o ALÍVIO das regiões destacadas (T5).</summary>
        private static List<TipCut> CutsOf(SmartElectrode e, double bottomZ, double closingZ, SplitOptions rules, double halfX, double halfY)
        {
            List<TipCut> cuts = FillerCutsOf(e, bottomZ, closingZ, rules) ?? new List<TipCut>();
            if (e.CornerFillers != null)
            {
                // T7: enchimento dos raios resolvidos pela divisão (rota C1 da T2c: caixa da face, rente e com folga)
                foreach (TipCut f in e.CornerFillers) cuts.Add(f.Shifted(-e.CenterXMm, -e.CenterYMm, -e.DeepestZMm));
            }
            if (e.CutOutLocal != null)
            {
                double z0 = bottomZ - e.DeepestZMm - 1.0, z1 = closingZ - e.DeepestZMm + 1.0;
                foreach (BoundingBox f in e.CutOutLocal)
                {
                    // do fundo ao fechamento; onde encosta na borda da pegada, passa 1 mm para fora
                    var b = new BoundingBox
                    {
                        MinX = f.MinX <= -halfX + 1e-6 ? -halfX - 1 : f.MinX, MaxX = f.MaxX >= halfX - 1e-6 ? halfX + 1 : f.MaxX,
                        MinY = f.MinY <= -halfY + 1e-6 ? -halfY - 1 : f.MinY, MaxY = f.MaxY >= halfY - 1e-6 ? halfY + 1 : f.MaxY,
                        MinZ = z0, MaxZ = z1
                    };
                    cuts.Add(new TipCut { What = string.Format(CultureInfo.InvariantCulture, "alívio da região (x {0:0.##}…{1:0.##}, y {2:0.##}…{3:0.##})", f.MinX, f.MaxX, f.MinY, f.MaxY), Flush = b, Clear = b, IsRelief = true });
                }
            }
            return cuts.Count == 0 ? null : cuts;
        }

        private static List<TipCut> FillerCutsOf(SmartElectrode e, double bottomZ, double closingZ, SplitOptions rules)
        {
            if (e.Piece == null) return null;
            List<SplitFinding> all = e.PredictedFindings ?? e.Piece.OnBorder;
            List<FillerCut> flush = ElectrodeSplitter.FillerBoxes(e.Piece, all, bottomZ, closingZ, 0.0, rules);
            List<FillerCut> clear = ElectrodeSplitter.FillerBoxes(e.Piece, all, bottomZ, closingZ, rules.FillerClearanceMm, rules);
            double ox = e.CenterXMm, oy = e.CenterYMm, oz = e.DeepestZMm;
            BoundingBox Local(BoundingBox b) => new BoundingBox
            {
                MinX = b.MinX - ox, MaxX = b.MaxX - ox, MinY = b.MinY - oy, MaxY = b.MaxY - oy, MinZ = b.MinZ - oz, MaxZ = b.MaxZ - oz
            };
            var cuts = new List<TipCut>();
            for (int i = 0; i < flush.Count; i++)
                cuts.Add(new TipCut
                {
                    What = flush[i].What, IsStrip = flush[i].Finding == null,
                    Flush = Local(flush[i].Box),
                    Clear = i < clear.Count ? Local(clear[i].Box) : default(BoundingBox)
                });
            // As lâminas primeiro: tiradas antes do enchimento, o pedaço nunca fica com uma lâmina solta (dois corpos).
            return cuts.OrderBy(c => c.IsStrip ? 0 : 1).ToList();
        }

        private const int igPlaneGeometry = -1909484335;   // GNTTypePropertyConstants.igPlane

        /// <summary>
        /// O prisma do pedaço começa este tanto ABAIXO do fundo do eletrodo inteiro. Medido no 15428.100 (2026-11-13),
        /// pedaço de 35,7 × 29,8 mm: com o fundo do prisma em Z −7,149 (coplanar com o piso mais fundo da queima, que
        /// atravessa a divisa) a booleana dá E_NOINTERFACE; em −7,2 ela vinga. O que fica abaixo é aço e sai na subtração.
        /// </summary>
        private const double PieceBottomAllowanceMm = 0.05;

        /// <summary>
        /// Z EXATO (mm, na montagem) do plano horizontal mais alto do corpo, procurado a até 0,1 mm abaixo de
        /// <paramref name="approxTopMm"/> (o topo da caixa folgada). Lido da geometria do plano
        /// (<c>Plane.GetRootPoint</c>/<c>GetNormalVector</c>), que não tem a folga do <c>GetRange</c>. NaN se não houver.
        /// </summary>
        private static double TopPlaneZMm(object body, OccurrenceTransform pose, double approxTopMm)
        {
            double best = double.NaN;
            try
            {
                dynamic faces = ((dynamic)body).Faces(1);   // igQueryAll
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    try
                    {
                        object geo = (object)faces.Item(i).Geometry;
                        if ((int)((dynamic)geo).Type != igPlaneGeometry) continue;
                        if (!FaceGeometry.TryOneArrayOut(geo, "GetNormalVector", out double[] nv, out _)) continue;
                        if (!FaceGeometry.TryOneArrayOut(geo, "GetRootPoint", out double[] rp, out _)) continue;
                        pose.TransformPointM(nv[0], nv[1], nv[2], out _, out _, out double nz);
                        nz -= pose.TzM;   // vetor: só a rotação
                        if (Math.Abs(nz) < 0.9999) continue;
                        pose.TransformPointM(rp[0], rp[1], rp[2], out _, out _, out double zM);
                        double z = Units.MToMm(zM);
                        if (z > approxTopMm + 1e-6 || z < approxTopMm - 0.1) continue;
                        if (double.IsNaN(best) || z > best) best = z;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log.Info("Topo exato do aço: " + ex.GetBaseException().Message); }
            if (!double.IsNaN(best))
                Log.Info(string.Format(CultureInfo.InvariantCulture, "Topo do aço: plano exato Z = {0:0.######} mm (caixa {1:0.######}).", best, approxTopMm));
            return best;
        }

        /// <summary>"fresável" ou o que impede: raio interno abaixo da menor fresa, alcance, canto vivo.</summary>
        /// <param name="closingZMm">Z do plano de fechamento na peça do eletrodo. Acima dele fica o pescoço, que não
        /// queima nem encosta em aço: o raio que a fresa deixar ali não importa, e não entra no veredito.</param>
        private static string DescribeElectrodeMachining(object partDoc, double closingZMm)
        {
            // T6: o juiz único (MillJudge) — o SharpCornerProbe não entra mais no veredito do eletrodo.
            try
            {
                MillJudgeResult r = MillJudgeReader.Judge(partDoc, closingZMm);
                foreach (MillHit h in r.Failed) Log.Info("  [usinar eletrodo] " + h.Describe() + " — " + h.Why);
                return r.Summary();
            }
            catch (Exception ex) { return "usinabilidade não avaliada (" + ex.GetBaseException().Message + ")"; }
        }
    }
}
