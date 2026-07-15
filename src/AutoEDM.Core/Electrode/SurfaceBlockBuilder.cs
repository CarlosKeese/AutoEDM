using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>Parâmetros do botão "Bloco sobre superfícies" (ambiente de PEÇA).</summary>
    public sealed class BlockOverSurfacesOptions
    {
        /// <summary>Material do eletrodo (filtra o catálogo de blanks). Default "Cobre".</summary>
        public string Material { get; set; } = "Cobre";

        /// <summary>Blank/uso escolhido no pop-up (em pé ou deitado c/ corte). Null = auto (o mais compacto).</summary>
        public BlankChoice ChosenBlank { get; set; }

        /// <summary>Comprimento máximo de corte da barra (mm) — a "medida variável". Default 500.</summary>
        public double BarMaxLengthMm { get; set; } = 500.0;

        /// <summary>Espaço (mm) entre o TOPO das superfícies e a BASE do bloco — o controle
        /// "aumentar o espaço". A forma copiada sobe do fundo até tocar o bloco; o gap dá
        /// folga entre a ponta da forma e o bloco. Default 1 mm (pedido do Carlos).</summary>
        public double GapMm { get; set; } = 1.0;

        /// <summary>Altura do bloco/holder (mm).</summary>
        public double BlockHeightMm { get; set; } = 15.0;

        /// <summary>
        /// SOBREMETAL do bloco por LADO (mm): o bloco ULTRAPASSA a pegada da queima para
        /// deixar material de usinagem (Carlos, 2026-07-15: "considerar um aumento de 0,5 no
        /// bloco para garantir sobremetal para usinagem" — o bloco vinha exatamente nos
        /// limites das superfícies). Entra no dimensionamento/corte do blank; a FAIXA continua
        /// contendo a pegada. Default 0,5 mm/lado.
        /// </summary>
        public double BlockOversizeMm { get; set; } = 0.5;

        /// <summary>Aplicar a fixação (furos M6+2×Ø4 se couberem, senão eixo no topo).</summary>
        public bool ApplyFixation { get; set; } = true;

        /// <summary>Padrão de fixação (diâmetros/profundidades/eixo).</summary>
        public FixationPattern Fixation { get; set; } = new FixationPattern();

        // -------- Faixa de medição (item 5, [[electrode-anatomy]]) --------

        /// <summary>Criar a FAIXA DE MEDIÇÃO (degrau menor + chanfro de orientação) sob o bloco.</summary>
        public bool AddMeasurementBand { get; set; } = true;

        /// <summary>Altura da faixa de medição (mm). Anatomia real = 5 mm.</summary>
        public double BandHeightMm { get; set; } = 5.0;

        /// <summary>Quanto a faixa é menor que o bloco, POR LADO (mm) — "um pouco menor que o blank".</summary>
        public double BandMarginMm { get; set; } = 0.5;

        /// <summary>Perna do chanfro 1×45° de orientação no canto X+ Y− (mm).</summary>
        public double ChamferLegMm { get; set; } = 3.0;

        // -------- Offset de faísca por cor (item 7) --------

        /// <summary>
        /// Aplicar o OFFSET (folga de faísca) nas superfícies de queima conforme as CORES
        /// (Ra→folga: 0,8→0,05; 1,6→0,10; 3,2→0,20; 6,3→0,30 mm) + <see cref="ExtraOffsetMm"/>.
        /// O eletrodo é SUBDIMENSIONADO (offset para dentro). Ver [[real-edm-workflow]].
        /// </summary>
        public bool ApplyColorOffset { get; set; } = true;

        /// <summary>Folga extra (mm) somada ao offset da tabela de Ra (ajuste manual). Default 0.</summary>
        public double ExtraOffsetMm { get; set; } = 0.0;

        /// <summary>
        /// Após unir, ALTERNAR para modelamento ORDENADO (item 7) — deixa a feature de união
        /// editável (o operador ajusta o offset/gap na árvore). 2 = igOrdered.
        /// </summary>
        public bool SwitchToOrdered { get; set; } = true;
    }

    /// <summary>Resultado do dimensionamento SEM modelar — alimenta o pop-up e o resumo ao vivo.</summary>
    public sealed class BlockOverSurfacesPlan
    {
        public bool SurfacesFound;
        public string SurfaceSource = "(nenhuma)";
        public int SurfaceFaceCount;

        public BoundingBox Footprint;
        public double FootprintXmm, FootprintYmm;
        public double CenterXmm, CenterYmm;         // centro XY da pegada (o bloco é centrado aqui)
        public double SurfacesTopZmm, SurfacesBottomZmm;

        public IReadOnlyList<BlankChoice> EligibleBlanks = new List<BlankChoice>();
        public BlankChoice ChosenBlank;
        public bool RoundBlank;
        public double BlockXmm, BlockYmm, BlockHmm;
        public double BandBaseZmm;                  // topo das superfícies + gap (base da FAIXA)
        public double BlockBaseZmm;                 // base do BLOCO = faixa (base+altura) OU só o gap se sem faixa
        public bool HasBand;                        // a faixa de medição entra no dimensionamento?

        // Offset de faísca por cor (item 7) — o que será aplicado às superfícies de queima.
        public readonly List<OffsetGroup> OffsetGroups = new List<OffsetGroup>();

        public readonly List<string> Warnings = new List<string>();

        /// <summary>Texto de 1 parágrafo p/ o rótulo da janela.</summary>
        public string Summary()
        {
            if (!SurfacesFound) return "Nenhuma superfície encontrada. Selecione a superfície copiada e tente de novo.";
            string blk = ChosenBlank != null ? ChosenBlank.Describe() : "sem blank do catálogo (bloco = pegada / comprar material)";
            string band = HasBand
                ? $"Faixa: Z {BandBaseZmm:0.0}→{BlockBaseZmm:0.0} mm (5 mm, gap {(BandBaseZmm - SurfacesTopZmm):0.0} mm acima da superfície) + chanfro X+ Y−\r\n"
                : "Faixa: (desligada)\r\n";
            string s =
                $"Pegada: {FootprintXmm:0.0} × {FootprintYmm:0.0} mm  (fonte: {SurfaceSource})\r\n" +
                $"Blank: {blk}\r\n" +
                band +
                $"Bloco: {BlockXmm:0.0} × {BlockYmm:0.0} × {BlockHmm:0.0} mm, parte de Z = {BlockBaseZmm:0.0} mm";
            if (OffsetGroups.Count > 0)
            {
                s += "\r\nOffset (folga de faísca):";
                foreach (var g in OffsetGroups) s += "\r\n  • " + g.Describe();
            }
            if (Warnings.Count > 0) s += "\r\n⚠ " + string.Join("; ", Warnings);
            return s;
        }
    }

    /// <summary>Um grupo de faces de queima da MESMA cor e o offset (folga, mm) a aplicar.</summary>
    public sealed class OffsetGroup
    {
        public System.Drawing.Color Color;
        public bool ColorMapped;      // a cor casou com o mapa Ra?
        public double Ra;             // Ra alvo (µm), 0 se não mapeada
        public double OffsetMm;       // folga total = tabela(Ra) + extra
        public readonly List<object> Faces = new List<object>();

        public string Describe()
        {
            string cor = $"RGB({Color.R},{Color.G},{Color.B})";
            return ColorMapped
                ? $"{cor} · Ra {Ra:0.#} · {Faces.Count} face(s) → offset {OffsetMm:0.###} mm"
                : $"{cor} (não mapeada) · {Faces.Count} face(s) → offset {OffsetMm:0.###} mm";
        }
    }

    /// <summary>Resultado da modelagem — inclui os handles p/ o Preview limpar.</summary>
    public sealed class BlockOverSurfacesResult
    {
        public BlockOverSurfacesPlan Plan;
        public bool BlockCreated, BandCreated, SurfacesOffset, SurfacesUnited, FixationApplied, SwitchedToOrdered;
        /// <summary>Features criadas, na ordem de criação (o Cleanup apaga em ordem reversa).</summary>
        public readonly List<object> CreatedFeatures = new List<object>();
        /// <summary>Contagens ANTES do build (o Cleanup apaga o que passou disso, via doc RE-ADQUIRIDO —
        /// os handles morrem quando os furos desconectam a peça, então deletar por handle não basta).</summary>
        public int ModelsBaseline = -1;
        public int CopySurfacesBaseline = -1;
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Cria um BLOCO sobre as superfícies de queima JÁ COPIADAS (manualmente pelo Carlos)
    /// numa PEÇA (.par), estende as superfícies até o bloco, fecha-as e une ao bloco —
    /// um sólido único = bloco em cima + forma embaixo. Opera no documento de PEÇA
    /// ativo (não na montagem), então NÃO precisa da cópia inter-part (bloqueada por COM).
    ///
    /// Reusa a receita validada de bloco (<see cref="BlankModeler"/>), o catálogo de blanks
    /// (<see cref="StandardBlankLibrary"/>) e a leitura de bbox de face
    /// (<see cref="FaceGeometry"/>). As operações de SUPERFÍCIE (estender/fechar/unir) usam
    /// APIs do SE ainda não exercitadas neste projeto — por isso a 1ª execução emite um
    /// PROBE de introspecção (uma vez) para confirmar as assinaturas antes de ligar as
    /// chamadas definitivas.
    /// </summary>
    public sealed class SurfaceBlockBuilder
    {
        private static bool _apiProbed;
        private readonly IBlankLibrary _blanks;

        public SurfaceBlockBuilder(IBlankLibrary blanks = null)
        {
            _blanks = blanks ?? new StandardBlankLibrary();
        }

        // ================================================================= PLAN

        /// <summary>Dimensiona (sem modelar): pegada, blanks elegíveis, blank escolhido e dims do bloco.</summary>
        public BlockOverSurfacesPlan Plan(dynamic partDoc, BlockOverSurfacesOptions opt)
        {
            opt = opt ?? new BlockOverSurfacesOptions();
            var plan = new BlockOverSurfacesPlan();

            var faces = CollectSurfaceFaces(partDoc, out string source);
            plan.SurfaceSource = source;
            plan.SurfaceFaceCount = faces.Count;
            plan.SurfacesFound = faces.Count > 0;
            if (!plan.SurfacesFound)
            {
                plan.Warnings.Add("Nenhuma superfície de queima encontrada — selecione a superfície copiada (ou veja o log do probe).");
                return plan;
            }

            if (!TryFootprint(faces, out BoundingBox box))
            {
                plan.Warnings.Add("Não foi possível ler a bounding box das superfícies.");
                return plan;
            }
            plan.Footprint = box;
            plan.FootprintXmm = box.SizeX;
            plan.FootprintYmm = box.SizeY;
            plan.CenterXmm = (box.MinX + box.MaxX) / 2.0;
            plan.CenterYmm = (box.MinY + box.MaxY) / 2.0;
            plan.SurfacesTopZmm = box.MaxZ;
            plan.SurfacesBottomZmm = box.MinZ;

            // Formas de usar o blank — EM PÉ (altura livre) ou DEITADO (corte da barra vira a
            // pegada, altura = seção). O pop-up escolhe; auto = o mais compacto.
            double footLong = Math.Max(box.SizeX, box.SizeY);
            double footShort = Math.Min(box.SizeX, box.SizeY);

            // O blank é escolhido/cortado para a pegada + 2·crescimento por lado, satisfazendo
            // as DUAS regras: (a) o BLOCO ultrapassa a pegada por BlockOversizeMm — sobremetal
            // de usinagem (Carlos, 2026-07-15: o bloco vinha exatamente nos limites das
            // superfícies); (b) a FAIXA (bloco − 2·margem) CONTÉM a pegada (Carlos: "elas
            // precisam estar dentro da faixa"). Crescimento = o MAIOR entre sobremetal e margem
            // da faixa → bloco ⩾ pegada + 2·sobremetal e faixa ⩾ pegada.
            double bandM = opt.AddMeasurementBand ? opt.BandMarginMm : 0.0;
            double growPerSide = Math.Max(opt.BlockOversizeMm, bandM);
            BoundingBox needBox = new BoundingBox
            {
                MinX = box.MinX - growPerSide, MaxX = box.MaxX + growPerSide,
                MinY = box.MinY - growPerSide, MaxY = box.MaxY + growPerSide,
                MinZ = box.MinZ, MaxZ = box.MaxZ
            };
            plan.EligibleBlanks = _blanks.BlankChoices(needBox, opt.Material, opt.BarMaxLengthMm, opt.BandHeightMm);

            BlankChoice choice = opt.ChosenBlank ?? plan.EligibleBlanks.FirstOrDefault();
            plan.ChosenBlank = choice;

            if (choice == null)
            {
                plan.Warnings.Add($"Pegada {footLong:0.0}×{footShort:0.0} mm não cabe em nenhuma barra de '{opt.Material}' (nem cortada) — comprar material (bloco cru = pegada + faixa).");
                plan.RoundBlank = false;
                plan.BlockXmm = needBox.SizeX; // pegada + 2·margem da faixa (faixa contém a pegada)
                plan.BlockYmm = needBox.SizeY;
                plan.BlockHmm = opt.BlockHeightMm;
            }
            else
            {
                plan.RoundBlank = choice.Round;
                plan.BlockXmm = choice.BlockXmm;
                plan.BlockYmm = choice.BlockYmm;
                // Altura: DEITADO impõe a altura da seção (contém faixa + bloco); EM PÉ = livre (param do usuário).
                plan.BlockHmm = choice.TotalHeightMm.HasValue
                    ? Math.Max(choice.TotalHeightMm.Value - (opt.AddMeasurementBand ? opt.BandHeightMm : 0.0), 3.0)
                    : opt.BlockHeightMm;
            }

            // Empilhamento em Z (Carlos, Log 2026-07-14): topo das superfícies → AFASTAMENTO
            // (gap) → FAIXA de medição (5 mm) → BLOCO (parte do topo da faixa). Sem faixa, o
            // bloco parte direto do gap. A faixa é modelada a partir da base (gap acima da
            // superfície); o bloco é levantado pela altura da faixa.
            plan.HasBand = opt.AddMeasurementBand;
            plan.BandBaseZmm = box.MaxZ + opt.GapMm;
            plan.BlockBaseZmm = plan.BandBaseZmm + (plan.HasBand ? opt.BandHeightMm : 0.0);

            // Offset de faísca por cor (item 7): agrupa as faces por cor e calcula a folga
            // (tabela Ra + extra). Não modela — só dimensiona; alimenta o resumo e o Build.
            if (opt.ApplyColorOffset)
                ComputeOffsetGroups(plan, faces, (object)SafeApp(partDoc), opt.ExtraOffsetMm);

            return plan;
        }

        private static object SafeApp(dynamic partDoc)
        {
            try { return partDoc.Application; } catch { return null; }
        }

        /// <summary>
        /// Agrupa as faces de queima por COR e calcula a folga (offset para dentro, mm) de
        /// cada grupo = tabela(Ra) + <paramref name="extraMm"/>. Cor→Ra por
        /// <see cref="RaColorMap"/>; Ra→folga por <see cref="RaOffsetTablePolicy"/>. Faces
        /// sem cor mapeada recebem só o extra (e um aviso). Eletrodo ENCOLHE (offset p/ dentro).
        /// </summary>
        private static void ComputeOffsetGroups(BlockOverSurfacesPlan plan, List<object> faces, object application, double extraMm)
        {
            var reader = new FaceStyleColorReader();
            var map = new RaColorMap();
            var policy = new RaOffsetTablePolicy();
            var byColor = new Dictionary<int, OffsetGroup>();

            foreach (var f in faces)
            {
                System.Drawing.Color color; string src;
                bool gotColor = reader.TryReadColor(f, application, out color, out src);
                if (!gotColor) color = System.Drawing.Color.Empty;

                int key = color.ToArgb();
                if (!byColor.TryGetValue(key, out OffsetGroup g))
                {
                    g = new OffsetGroup { Color = color };
                    if (gotColor && map.TryGetRa(color, out double ra, out _))
                    {
                        g.ColorMapped = true; g.Ra = ra;
                        g.OffsetMm = policy.GetInwardOffsetMm(new Model.ElectrodePass("", ra), plan.ChosenBlank?.Material ?? "Cobre") + extraMm;
                    }
                    else
                    {
                        g.ColorMapped = false; g.OffsetMm = extraMm; // sem Ra: só o extra
                    }
                    byColor[key] = g;
                }
                g.Faces.Add(f);
            }

            foreach (var g in byColor.Values.OrderByDescending(v => v.Faces.Count))
                plan.OffsetGroups.Add(g);

            if (plan.OffsetGroups.Any(g => !g.ColorMapped && g.Faces.Count > 0))
                plan.Warnings.Add("há faces de queima sem cor mapeada — offset dessas = só a folga extra (confira a cor).");
        }

        // ================================================================ BUILD

        /// <summary>
        /// Modela: bloco + faixa + offset/estende/une as superfícies + fixação. Devolve os
        /// handles p/ Preview. <paramref name="preview"/>=true mantém a peça em SÍNCRONO (para
        /// re-preview/Cancel deletarem fácil); a troca para ORDENADO (item 7) só ocorre no
        /// build FINAL (OK) — ver <see cref="FinalizeToOrdered"/>.
        /// </summary>
        public BlockOverSurfacesResult Build(dynamic partDoc, BlockOverSurfacesOptions opt, bool preview = false)
        {
            opt = opt ?? new BlockOverSurfacesOptions();
            var result = new BlockOverSurfacesResult();

            ProbeSurfaceApi(partDoc); // 1ª vez: loga a API de superfície p/ confirmar as assinaturas

            var plan = Plan(partDoc, opt);
            result.Plan = plan;
            foreach (var w in plan.Warnings) { Log.Warn(w); result.Warnings.Add(w); }
            if (!plan.SurfacesFound) return result;

            Log.Info($"Bloco sobre superfícies: pegada {plan.FootprintXmm:0.0}×{plan.FootprintYmm:0.0} mm @ " +
                     $"centro ({plan.CenterXmm:0.0},{plan.CenterYmm:0.0}), topo Z={plan.SurfacesTopZmm:0.0}; " +
                     $"blank {(plan.ChosenBlank?.Describe() ?? "(cru=pegada)")}; " +
                     $"bloco {plan.BlockXmm:0.0}×{plan.BlockYmm:0.0}×{plan.BlockHmm:0.0}, base Z={plan.BlockBaseZmm:0.0} " +
                     $"(gap {opt.GapMm:0.0}, sobremetal {opt.BlockOversizeMm:0.0}/lado).");

            // Baselines p/ o Cleanup (apaga o que passar disto, via doc re-adquirido).
            result.ModelsBaseline = ModelsCount(partDoc);
            result.CopySurfacesBaseline = CopySurfacesCount(partDoc);

            // (1) BLOCO — base do bloco = topo das superfícies + gap (lift a partir da origem).
            try
            {
                object ext = plan.RoundBlank
                    ? (object)BlankModeler.CreateCylinder(partDoc, plan.BlockXmm, plan.BlockHmm, 1, 2, plan.BlockBaseZmm, plan.CenterXmm, plan.CenterYmm)
                    : (object)BlankModeler.CreateBox(partDoc, plan.BlockXmm, plan.BlockYmm, plan.BlockHmm, 1, 2, plan.BlockBaseZmm, plan.CenterXmm, plan.CenterYmm);
                if (ext != null) result.CreatedFeatures.Add(ext);
                result.BlockCreated = true;
            }
            catch (Exception ex)
            {
                Log.Error("Falha ao criar o bloco.", ex);
                result.Warnings.Add("bloco: " + ex.GetBaseException().Message);
                return result; // sem bloco não há o que estender/unir
            }

            // Agora que existe um sólido (o bloco), sonda as operações de UNIR/estender que
            // só aparecem no Model (Replace Face, booleana, Thicken) — 1x, para ligar o
            // passo definitivo de "unir superfícies ao bloco" no próximo run.
            ProbeModelApi(partDoc);

            // (1b) FAIXA DE MEDIÇÃO (item 5): degrau menor + chanfro de orientação, sob o
            // bloco (topo da faixa = base do bloco). Funde com o bloco (protrusão).
            if (opt.AddMeasurementBand)
            {
                try
                {
                    object band = BlankModeler.AddMeasurementBand(partDoc, plan.BlockXmm, plan.BlockYmm,
                        plan.BlockBaseZmm, opt.BandHeightMm, opt.BandMarginMm, opt.ChamferLegMm,
                        plan.CenterXmm, plan.CenterYmm);
                    if (band != null) { result.CreatedFeatures.Add(band); result.BandCreated = true; }
                }
                catch (Exception ex) { Log.Warn("Faixa de medição (bloco preservado): " + ex.GetBaseException().Message); }
            }

            // (2) O trabalho de SUPERFÍCIE (engrossar/unir a queima ao bloco) foi ISOLADO num
            // botão separado ("Unir superfícies", CmdUnirSuperficies) — Carlos, 2026-07-15: o
            // thicken rodando aqui, ao falhar, ENVENENAVA o doc e derrubava os furos (todos E_FAIL).
            // Este botão faz só o que funciona bem: bloco + faixa + fixação. Ver [[autoedm-decisions]].

            // (3) FIXAÇÃO (Carlos: incluir).
            if (opt.ApplyFixation)
            {
                try
                {
                    double blockTopZ = plan.BlockBaseZmm + plan.BlockHmm;
                    if (BlankModeler.FixationHolesFit(plan.BlockXmm, plan.BlockYmm, opt.Fixation))
                    {
                        Log.Info("Fixação por FUROS (M6 + 2×Ø4).");
                        var holes = BlankModeler.AddFixationHoles(partDoc, plan.BlockXmm, plan.BlockYmm, plan.BlockHmm,
                            plan.BlockBaseZmm, opt.Fixation, plan.CenterXmm, plan.CenterYmm);
                        if (holes != null) result.CreatedFeatures.AddRange(holes);
                    }
                    else
                    {
                        Log.Info($"Furos não cabem no bloco {plan.BlockXmm:0.0}×{plan.BlockYmm:0.0} — EIXO no topo.");
                        object shaft = BlankModeler.AddShaft(partDoc, plan.BlockXmm, plan.BlockYmm, blockTopZ,
                            opt.Fixation, plan.CenterXmm, plan.CenterYmm);
                        if (shaft != null) result.CreatedFeatures.Add(shaft);
                    }
                    result.FixationApplied = true;
                }
                catch (Exception ex) { Log.Warn("Fixação (bloco preservado): " + ex.GetBaseException().Message); }
            }

            // (4) ALTERNAR PARA ORDENADO (item 7) — só no build FINAL (OK); no preview a peça
            // fica em síncrono para o re-preview/Cancel deletarem fácil.
            if (!preview) FinalizeToOrdered(partDoc, result, opt);

            Log.Info($"Concluído: bloco={result.BlockCreated}, faixa={result.BandCreated}, offset={result.SurfacesOffset}, " +
                     $"unidas={result.SurfacesUnited}, ordenado={result.SwitchedToOrdered}, fixação={result.FixationApplied}. " +
                     $"Features criadas: {result.CreatedFeatures.Count}.");
            return result;
        }

        // ============================================================== CLEANUP

        /// <summary>
        /// Apaga a geometria de um preview — usado no re-preview e no Cancel. Os handles das
        /// features criadas antes dos furos ficam DESCONECTADOS (os furos re-adquirem o doc e
        /// o AddSync desconecta o proxy anterior), então deletar por handle NÃO basta. Solução:
        /// RE-ADQUIRE o documento fresco de Application.ActiveDocument e apaga tudo que passou
        /// das baselines (Models = bloco+faixa+furos; CopySurfaces = superfície criada), em
        /// ordem reversa, com o doc vivo.
        /// </summary>
        public void Cleanup(dynamic partDoc, BlockOverSurfacesResult result)
        {
            if (result == null) return;

            dynamic doc = null;
            try { doc = partDoc?.Application?.ActiveDocument; } catch { }
            if (doc == null) doc = partDoc;
            if (doc == null) { result.CreatedFeatures.Clear(); return; }

            int ok = 0, fail = 0;
            ok += DeleteBeyond(SafeCollection(() => doc.Models), result.ModelsBaseline, "Model (sólido)", ref fail);
            ok += DeleteBeyond(SafeCollection(() => doc.Constructions.CopySurfaces), result.CopySurfacesBaseline, "CopySurface", ref fail);

            result.CreatedFeatures.Clear();
            result.BlockCreated = result.BandCreated = result.SurfacesOffset =
                result.SurfacesUnited = result.FixationApplied = result.SwitchedToOrdered = false;
            Log.Info($"Preview limpo (doc re-adquirido): {ok} feature(s) removida(s)" + (fail > 0 ? $", {fail} falha(s)" : "") + ".");
        }

        private static object SafeCollection(Func<object> get) { try { return get(); } catch { return null; } }

        /// <summary>Apaga (ordem reversa) os itens da coleção COM com índice &gt; baseline.</summary>
        private static int DeleteBeyond(object collection, int baseline, string label, ref int fail)
        {
            if (collection == null || baseline < 0) return 0;
            int ok = 0;
            try
            {
                dynamic c = collection;
                int n = 0; try { n = (int)c.Count; } catch { }
                for (int i = n; i > baseline; i--)
                {
                    try { c.Item(i).Delete(); ok++; }
                    catch (Exception e) { fail++; Log.Warn($"Cleanup: {label}[{i}] não removido — {e.GetBaseException().Message}"); }
                }
            }
            catch { }
            return ok;
        }

        private static int CopySurfacesCount(dynamic partDoc)
        {
            try { return (int)partDoc.Constructions.CopySurfaces.Count; } catch { return 0; }
        }

        // ============================================================ superfícies

        /// <summary>
        /// Junta as FACES das superfícies de queima já presentes na peça. Ordem de busca:
        /// (1) SelectSet — o usuário selecionou a superfície copiada antes de clicar;
        /// (2) Constructions.CopySurfaces — a cópia manual mais comum. Cada item pode ser
        /// uma superfície/corpo (tem .Faces) ou já uma Face.
        /// </summary>
        private static List<object> CollectSurfaceFaces(dynamic partDoc, out string source)
        {
            var faces = new List<object>();

            int selN = 0;
            try
            {
                dynamic ss = partDoc.SelectSet;
                try { selN = (int)ss.Count; } catch { selN = 0; }
                for (int i = 1; i <= selN; i++)
                {
                    object item; try { item = ss.Item(i); } catch { continue; }
                    AddFacesFrom(item, faces);
                }
            }
            catch { }
            if (faces.Count > 0) { source = $"SelectSet ({selN} sel.)"; return faces; }

            int csN = 0;
            try
            {
                dynamic cs = partDoc.Constructions.CopySurfaces;
                try { csN = (int)cs.Count; } catch { csN = 0; }
                for (int i = 1; i <= csN; i++)
                {
                    object item; try { item = cs.Item(i); } catch { continue; }
                    AddFacesFrom(item, faces);
                }
            }
            catch { }
            if (faces.Count > 0) { source = $"CopySurfaces ({csN})"; return faces; }

            source = "(nenhuma)";
            return faces;
        }

        /// <summary>Os ITENS de superfície (não as faces) p/ o estender/costurar — SelectSet ou CopySurfaces.</summary>
        private static List<object> CollectSurfaceItems(dynamic partDoc)
        {
            var items = new List<object>();
            try
            {
                dynamic ss = partDoc.SelectSet;
                int n = 0; try { n = (int)ss.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object item; try { item = ss.Item(i); } catch { continue; }
                    if (HasFaces(item)) items.Add(item);
                }
            }
            catch { }
            if (items.Count > 0) return items;

            try
            {
                dynamic cs = partDoc.Constructions.CopySurfaces;
                int n = 0; try { n = (int)cs.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object item; try { item = cs.Item(i); } catch { continue; }
                    items.Add(item);
                }
            }
            catch { }
            return items;
        }

        /// <summary>Adiciona as faces de um item (superfície/corpo -> suas faces; senão o próprio item é uma Face).</summary>
        private static void AddFacesFrom(object item, List<object> acc)
        {
            if (item == null) return;
            try
            {
                dynamic d = item;
                dynamic fc = d.Faces[1]; // 1 = igQueryAll
                int count = (int)fc.Count;
                if (count > 0)
                {
                    for (int i = 1; i <= count; i++) acc.Add(fc.Item(i));
                    return;
                }
            }
            catch { /* não é superfície/corpo: cai p/ tratar como Face */ }
            acc.Add(item); // provavelmente já é uma Face
        }

        private static bool HasFaces(object item)
        {
            try { dynamic d = item; var _ = d.Faces[1].Count; return true; }
            catch { return false; }
        }

        private static bool TryFootprint(List<object> faces, out BoundingBox box)
        {
            box = new BoundingBox
            {
                MinX = double.MaxValue, MinY = double.MaxValue, MinZ = double.MaxValue,
                MaxX = double.MinValue, MaxY = double.MinValue, MaxZ = double.MinValue
            };
            bool any = false;
            foreach (var f in faces)
            {
                if (FaceGeometry.TryGetRangeMm(f, out double[] min, out double[] max))
                {
                    any = true;
                    box.MinX = Math.Min(box.MinX, min[0]); box.MaxX = Math.Max(box.MaxX, max[0]);
                    box.MinY = Math.Min(box.MinY, min[1]); box.MaxY = Math.Max(box.MaxY, max[1]);
                    box.MinZ = Math.Min(box.MinZ, min[2]); box.MaxZ = Math.Max(box.MaxZ, max[2]);
                }
            }
            return any;
        }

        /// <summary>
        /// Item 7 (OFFSET por cor) + itens 3/4 (ESTENDER/FECHAR/UNIR ao bloco).
        /// (a) offseta cada grupo de cor pela folga de faísca (para DENTRO) via
        ///     <c>OffsetSurfaces.Add</c> (assinatura confirmada); (b) costura as superfícies
        ///     via <c>StitchSurfaces.Add</c> (confirmada). O passo DEFINITIVO de "unir ao
        ///     bloco" (estender até o bloco: Replace Face vs Thicken+booleana) ainda sai do
        ///     PROBE — é tentado guardado e logado; o bloco/faixa sobrevivem se falhar.
        /// Devolve as features criadas + flags de offset/união.
        /// </summary>
        /// <summary>
        /// Botão ISOLADO "Unir superfícies": engrossa a superfície de queima (faces
        /// SELECIONADAS ou CopySurface existente) PARA CIMA, até dentro do bloco, e UNE ao
        /// bloco → um sólido único. SEPARADO do "Bloco sobre superfícies" porque o thicken, ao
        /// falhar, ENVENENA o doc e derruba a fixação (Carlos, 2026-07-15) — aqui o experimento
        /// de superfície fica isolado do fluxo (bloco+faixa+furos) que já funciona. Requer o
        /// BLOCO já criado (Models.Item(1)) e as faces de queima selecionadas na peça.
        /// </summary>
        public BlockOverSurfacesResult UniteSurfacesToBlock(dynamic partDoc, BlockOverSurfacesOptions opt)
        {
            opt = opt ?? new BlockOverSurfacesOptions();
            var result = new BlockOverSurfacesResult { Plan = new BlockOverSurfacesPlan() };
            result.ModelsBaseline = ModelsCount(partDoc);
            result.CopySurfacesBaseline = CopySurfacesCount(partDoc);

            ProbeModelApi(partDoc); // diagnóstico das ops reais do Model (Thicken/Unions/…)

            // Topo/fundo das superfícies (p/ o overshoot do thicken).
            var faces = CollectSurfaceFaces(partDoc, out string src);
            BoundingBox box = default(BoundingBox);
            if (faces.Count > 0 && TryFootprint(faces, out box))
            {
                result.Plan.SurfacesTopZmm = box.MaxZ; result.Plan.SurfacesBottomZmm = box.MinZ;
                Log.Info($"Unir superfícies: {faces.Count} face(s) de queima ({src}), topo Z={box.MaxZ:0.0} mm.");
            }
            else Log.Warn("Unir superfícies: sem faces de queima — selecione as faces na peça (ou tenha uma CopySurface).");
            result.Plan.BlockHmm = opt.BlockHeightMm;

            TryExtendStitchUnite(partDoc, result.Plan, opt, result);
            return result;
        }

        // Path B (Carlos, 2026-07-15): ESTENDER a superfície de queima até o bloco →
        // COSTURAR+CURAR em sólido (AddByAutoTrim) → UNIR (booleana) ao bloco. Assinaturas
        // REAIS obtidas por reflexão do interop (SolidEdgePart). As coleções vêm por IDispatch
        // dinâmico e são CASTADAS ao tipo do interop para o marshaling correto do `ref Array`.
        // Cada passo é guardado e MUITO logado (inputs + resultado) — o bloco/faixa sobrevivem.
        // Popula 'result' direto (tipo concreto): retornar tupla nomeada de método com arg
        // dynamic perde os nomes em runtime (bug de 2026-07-14).
        private static void TryExtendStitchUnite(
            dynamic partDoc, BlockOverSurfacesPlan plan, BlockOverSurfacesOptions opt, BlockOverSurfacesResult result)
        {
            dynamic blockModel;
            try { blockModel = partDoc.Models.Item(1); }
            catch (Exception e) { Log.Warn("Unir: sem sólido do bloco — " + e.GetBaseException().Message); return; }

            // Face INFERIOR do bloco = alvo da extensão da superfície.
            object bottomFace = FindExtremePlanarFace(blockModel, false, out double blockBottomZmm);
            if (bottomFace == null) { Log.Warn("Unir: não achei a face inferior do bloco (alvo da extensão)."); return; }
            Log.Info($"Unir: face inferior do bloco em Z≈{blockBottomZmm:0.0}mm (alvo).");

            // Superfície de queima: usa a CopySurface existente OU cria uma das faces selecionadas.
            dynamic surf = GetBurnCopySurface(partDoc, out string surfSrc, out bool surfCreated);
            if (surf == null)
            {
                Log.Warn("Unir: sem superfície de queima (nem CopySurface, nem faces selecionadas). Selecione as faces de queima e rode de novo.");
                return;
            }
            if (surfCreated) result.CreatedFeatures.Add(surf);
            Log.Info($"Unir: superfície de queima = {surfSrc}.");

            // Offset de faísca (item 7): o que SERÁ aplicado (na feature de união, em ordenado).
            foreach (var g in plan.OffsetGroups) Log.Info($"  (offset planejado) {g.Describe()}");

            // O Model REAL não expõe ExtendSurfaces/IntersectSurfaces (probe 2026-07-15 — só
            // TrimExtendCollection/Intersects/Unions/Subtracts/RedefineFaces/Thickens). Caminho
            // robusto com o que EXISTE: ENGROSSAR (Models.AddThickenFeature) a superfície de queima
            // PARA CIMA — vira um sólido com a FORMA da queima no fundo, subindo até DENTRO do bloco
            // — e UNIR (Unions.Add) ao bloco → um sólido único (bloco/faixa em cima + forma embaixo).
            // (corte a fio: "copia a superfície, estende até a base" — [[real-edm-workflow]].)

            var burnFaces = new List<object>();
            AddFacesFrom((object)surf, burnFaces);
            if (ToTypedFaceArray(burnFaces).Length == 0) { Log.Warn("Unir: a superfície de queima não deu faces p/ engrossar."); return; }

            // Overshoot: do topo da queima até bem DENTRO do bloco — garante interseção p/ a união
            // fundir os corpos. O SENTIDO da normal é desconhecido, então tenta os 2 lados e usa o
            // que sobe até o bloco (o corpo do lado errado é limpo pela baseline no re-preview).
            double thickenMm = (blockBottomZmm - plan.SurfacesTopZmm) + Math.Max(plan.BlockHmm, opt.BandHeightMm);
            double thickenM = Math.Max(thickenMm, 3.0) / 1000.0;

            int before = ModelsCount(partDoc);
            bool thickened = false;
            foreach (int side in new[] { 1, 2 }) // FeaturePropertyConstants: lados da normal (igRight/igLeft)
            {
                try
                {
                    // Array TIPADO FRESCO por tentativa: reusar o mesmo array (com `ref`) corrompia
                    // a 2ª chamada ("converter argumento 0", Log 2026-07-15). `Faces` é [in] no
                    // probe → passa por VALOR (sem ref).
                    System.Array fa = ToTypedFaceArray(burnFaces);
                    object th = partDoc.Models.AddThickenFeature(side, thickenM, fa.Length, fa);
                    int after = ModelsCount(partDoc);
                    double maxZ = NewBodyMaxZmm(partDoc, before);
                    Log.Info($"Unir: AddThickenFeature(lado={side}, {thickenMm:0.0}mm, {fa.Length} face) → retorno={(th != null ? "ok" : "null")}, corpos {before}->{after}, topo do novo ≈ {maxZ:0.0}mm (precisa ≥ {blockBottomZmm:0.0}).");
                    if (after > before && maxZ >= blockBottomZmm - 0.5)
                    {
                        if (th != null) result.CreatedFeatures.Add(th);
                        thickened = true; break;
                    }
                    if (after > before) { TryDeleteFeature(th); } // engrossou p/ o lado errado — desfaz
                }
                catch (Exception e) { Log.Warn($"Unir: AddThickenFeature(lado={side}) falhou — {e.GetBaseException().Message}"); }
            }
            if (!thickened) { Log.Warn("Unir: não consegui engrossar a superfície até o bloco. Provável: as faces da CopySurface não são engrossáveis por esse caminho — inspecione a superfície pelo SPY (vamos iterar neste botão isolado)."); return; }

            // UNIR (booleana) o corpo engrossado ao bloco. Model.Unions.Add existe (probe).
            try
            {
                var uniCol = (SolidEdgePart.Unions)blockModel.Unions;
                int newCount = ModelsCount(partDoc);
                System.Array targets = ToTypedBodyArray((object)blockModel.Body);
                System.Array tools = ToTypedBodyArray((object)partDoc.Models.Item(newCount).Body);
                if (targets.Length == 1 && tools.Length == 1)
                {
                    uniCol.Add(1, ref targets, 1, ref tools,
                        SolidEdgePart.SETargetDesignBodyOption.igCreateSingleDesignBodyOnNonManifoldOption,
                        SolidEdgePart.SETargetConstructionBodyOption.igCreateSingleConstructionGeneralBodyOnNonManifoldOption);
                    result.SurfacesUnited = true;
                    Log.Info($"Unir: Unions.Add OK — forma engrossada unida ao bloco (corpos agora = {ModelsCount(partDoc)}).");
                }
                else Log.Warn("Unir: bloco ou corpo engrossado não expôs Body p/ unir (E_NOINTERFACE).");
            }
            catch (Exception e) { Log.Warn("Unir: Unions.Add falhou — " + e.GetBaseException().Message); }
        }

        /// <summary>Maior Z (mm) entre os corpos criados ALÉM da baseline (topo do corpo novo).</summary>
        private static double NewBodyMaxZmm(dynamic partDoc, int baselineCount)
        {
            double maxZ = double.NegativeInfinity;
            try
            {
                int n = ModelsCount(partDoc);
                for (int i = baselineCount + 1; i <= n; i++)
                {
                    dynamic m; try { m = partDoc.Models.Item(i); } catch { continue; }
                    dynamic faces; try { faces = m.Body.Faces[1]; } catch { continue; }
                    int fn = 0; try { fn = (int)faces.Count; } catch { }
                    for (int j = 1; j <= fn; j++)
                    {
                        object f; try { f = faces.Item(j); } catch { continue; }
                        if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) maxZ = Math.Max(maxZ, mx[2]);
                    }
                }
            }
            catch { }
            return maxZ;
        }

        /// <summary>Apaga uma feature recém-criada (best-effort) — p/ desfazer o engrosso do lado errado.</summary>
        private static void TryDeleteFeature(object feature)
        {
            if (feature == null) return;
            try { ((dynamic)feature).Delete(); } catch { }
        }

        /// <summary>Menor/maior face PLANAR do corpo (topo ou fundo) + seu Z (mm).</summary>
        private static object FindExtremePlanarFace(dynamic model, bool wantTop, out double zmm)
        {
            zmm = 0; object best = null;
            double bestZ = wantTop ? double.NegativeInfinity : double.PositiveInfinity;
            try
            {
                dynamic planar = model.Body.Faces[6]; // 6 = igQueryPlane
                int n = 0; try { n = (int)planar.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f; try { f = planar.Item(i); } catch { continue; }
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mx[2] - mn[2]) > 0.01) continue; // face não-horizontal
                    double z = wantTop ? mx[2] : mn[2];
                    if (wantTop ? z > bestZ : z < bestZ) { bestZ = z; best = f; zmm = z; }
                }
            }
            catch (Exception e) { Log.Warn("Unir: FindExtremePlanarFace — " + e.GetBaseException().Message); }
            return best;
        }

        /// <summary>
        /// A superfície de queima como CopySurface. Se a peça já tem uma, usa-a; SENÃO CRIA
        /// uma a partir das faces de queima SELECIONADAS (o Carlos seleciona as faces e clica —
        /// não precisa fazer Surface→Copy à mão). <paramref name="created"/>=true quando a
        /// criamos (o chamador rastreia p/ o Cleanup).
        /// </summary>
        private static dynamic GetBurnCopySurface(dynamic partDoc, out string src, out bool created)
        {
            src = null; created = false;

            // (1) Já existe uma CopySurface na peça?
            try
            {
                dynamic cs = partDoc.Constructions.CopySurfaces;
                int n = 0; try { n = (int)cs.Count; } catch { }
                if (n > 0) { src = $"CopySurface existente (de {n})"; return cs.Item(1); }
            }
            catch { }

            // (2) Senão, CRIA uma CopySurface das faces de queima selecionadas.
            var faces = CollectSurfaceFaces(partDoc, out string fsrc);
            if (faces.Count == 0) return null;
            try
            {
                var col = (SolidEdgePart.CopySurfaces)partDoc.Constructions.CopySurfaces;
                // O FaceArray é SAFEARRAY(IDispatch de Face): o array TEM de ser tipado
                // `SolidEdgeGeometry.Face[]` (object[] vira SAFEARRAY(VARIANT) → "não foi
                // possível converter argumento 2", Log 2026-07-15). Ver [[autoedm-decisions]].
                System.Array farr = ToTypedFaceArray(faces);
                if (farr.Length == 0)
                {
                    Log.Warn("Unir: as faces selecionadas não expõem a interface Face p/ criar a CopySurface automaticamente. " +
                             "CAMINHO CONFIÁVEL: faça 'Superfície → Copiar' nas faces de queima (cria a superfície de CONSTRUÇÃO) e rode o botão de novo — ele usa a CopySurface existente sem depender da seleção crua.");
                    return null;
                }
                object copy = col.Add(farr.Length, ref farr, Type.Missing, Type.Missing);
                if (copy != null)
                {
                    created = true;
                    src = $"CopySurface criada de {faces.Count} face(s) ({fsrc})";
                    return copy;
                }
            }
            catch (Exception e) { Log.Warn("Unir: criar CopySurface das faces selecionadas falhou — " + e.GetBaseException().Message); }
            return null;
        }

        /// <summary>Arestas da superfície que o SE aceita ESTENDER (boundary/laminar, ValidEdge).
        /// Um CopySurface pode expor as arestas de fontes diferentes (o próprio feature `.Edges`,
        /// o `.Body.Edges`, ou por face) — tenta as fontes em ordem e usa a 1ª não-vazia,
        /// logando qual funcionou (para o SPY não ser necessário no 1º run real).</summary>
        private static List<object> CollectExtendableEdges(dynamic surf, SolidEdgePart.ExtendSurfaces extCol)
        {
            foreach (var src in new[] { "Edges", "Body.Edges", "Faces[*].Edges" })
            {
                var raw = ReadEdges(surf, src);
                if (raw.Count == 0) continue;
                var valid = new List<object>();
                foreach (var e in raw) { try { if (extCol.ValidEdge(e)) valid.Add(e); } catch { } }
                Log.Info($"Unir: arestas via '{src}': {raw.Count} lida(s), {valid.Count} extensível(is) (ValidEdge).");
                if (valid.Count > 0) return valid;
            }
            Log.Warn("Unir: nenhuma fonte de arestas da superfície deu arestas extensíveis (inspecione a CopySurface pelo SPY).");
            return new List<object>();
        }

        /// <summary>Lê as arestas cruas da superfície por uma das fontes possíveis (best-effort).</summary>
        private static List<object> ReadEdges(dynamic surf, string source)
        {
            var edges = new List<object>();
            try
            {
                if (source == "Edges") AddFromCollection(surf.Edges, edges);
                else if (source == "Body.Edges") AddFromCollection(surf.Body.Edges, edges);
                else // por face
                {
                    dynamic faces = surf.Faces; int nf = 0; try { nf = (int)faces.Count; } catch { }
                    for (int i = 1; i <= nf; i++)
                    { try { AddFromCollection(faces.Item(i).Edges, edges); } catch { } }
                }
            }
            catch { /* fonte indisponível nesse tipo de superfície — tenta a próxima */ }
            return edges;
        }

        private static void AddFromCollection(dynamic ec, List<object> into)
        {
            int n = 0; try { n = (int)ec.Count; } catch { }
            for (int i = 1; i <= n; i++) { try { into.Add(ec.Item(i)); } catch { } }
        }

        // ---- Arrays TIPADOS p/ os SAFEARRAY(IDispatch) das APIs de superfície/booleana ----
        // As `Add*` recebem `ref Array` cujo SAFEARRAY é de um tipo COM específico (Face/Edge/
        // Body). Passar object[] gera SAFEARRAY(VARIANT) → "não foi possível converter
        // argumento N" (Log 2026-07-15). O array TEM de ter o tipo de elemento do interop.

        private static System.Array ToTypedFaceArray(List<object> faces)
        {
            var list = new List<SolidEdgeGeometry.Face>(faces.Count);
            int fail = 0;
            foreach (var f in faces) { try { list.Add((SolidEdgeGeometry.Face)f); } catch { fail++; } }
            if (fail > 0) Log.Warn($"Unir: {fail}/{faces.Count} face(s) não expõem a interface Face (E_NOINTERFACE) — ignoradas.");
            return list.ToArray();
        }

        private static System.Array ToTypedEdgeArray(List<object> edges)
        {
            var list = new List<SolidEdgeGeometry.Edge>(edges.Count);
            int fail = 0;
            foreach (var e in edges) { try { list.Add((SolidEdgeGeometry.Edge)e); } catch { fail++; } }
            if (fail > 0) Log.Warn($"Unir: {fail}/{edges.Count} aresta(s) não expõem a interface Edge (E_NOINTERFACE) — ignoradas.");
            return list.ToArray();
        }

        private static System.Array ToTypedBodyArray(object body)
        {
            try { return new SolidEdgeGeometry.Body[] { (SolidEdgeGeometry.Body)body }; }
            catch (Exception e) { Log.Warn("Unir: corpo não expõe a interface Body (E_NOINTERFACE) — " + e.GetBaseException().Message); return new SolidEdgeGeometry.Body[0]; }
        }

        /// <summary>O corpo (Body) de uma superfície de construção p/ o auto-trim — usa
        /// <c>surf.Body</c> se existir; senão o próprio <paramref name="surf"/>.</summary>
        private static object SurfBodyOf(dynamic surf)
        {
            try { object b = surf.Body; if (b != null) return b; } catch { }
            return (object)surf;
        }

        private static int ModelsCount(dynamic partDoc)
        {
            try { return (int)partDoc.Models.Count; } catch { return 0; }
        }

        // ================================================================ PROBE

        private static void ProbeSurfaceApi(dynamic partDoc)
        {
            if (_apiProbed) return;
            _apiProbed = true;
            Log.Info("[PROBE] Introspecção das APIs de superfície (1x) — para ligar estender/fechar/unir sem adivinhar.");
            try { ComDiagnostics.LogMembers("Constructions", (object)partDoc.Constructions); }
            catch (Exception e) { Log.Warn("[PROBE] Constructions: " + e.GetBaseException().Message); }
            try { ComDiagnostics.LogMembers("Models", (object)partDoc.Models); } catch { }
            try { ComDiagnostics.LogMembers("Models.Item(1)", (object)partDoc.Models.Item(1)); }
            catch (Exception e) { Log.Info("[PROBE] (sem sólido ainda) Models.Item(1): " + e.GetBaseException().Message); }

            foreach (var name in new[] { "CopySurfaces", "ExtendSurfaces", "StitchSurfaces",
                                         "OffsetSurfaces", "TrimSurfaces", "BoundedSurfaces", "DivideParts" })
                ProbeSub((object)partDoc.Constructions, "Constructions", name);
        }

        private static bool _modelProbed;

        /// <summary>
        /// Sonda (1x) o Model (sólido) recém-criado procurando as operações de UNIR as
        /// superfícies ao bloco: Replace Face, booleana (Combine/Unite/Subtract), Thicken.
        /// Roda DEPOIS do bloco (Model.Item(1) já existe). É o que faltava para ligar o
        /// passo definitivo dos itens 3/4 (estender/fechar/unir) sem adivinhar assinatura.
        /// </summary>
        private static void ProbeModelApi(dynamic partDoc)
        {
            if (_modelProbed) return;
            _modelProbed = true;
            try
            {
                object model = (object)partDoc.Models.Item(1);
                ComDiagnostics.LogMembers("Model (unir superfícies)", model);
                // Nomes REAIS confirmados pelo SPY (log 2026-07-15): as operações de estender/
                // unir/booleana vivem no MODEL/Body, não em Constructions.
                foreach (var name in new[] { "Unions", "Subtracts", "Intersects", "ExtendSurfaces",
                                             "TrimSurfaces", "IntersectSurfaces", "RedefineFaces",
                                             "ReplaceBody", "HoleGeometries" })
                    ProbeSub(model, "Model", name);
                ComDiagnostics.LogSignatures((object)partDoc.Models, "AddThickenFeature");
            }
            catch (Exception e) { Log.Info("[PROBE] Model (sem sólido ainda?): " + e.GetBaseException().Message); }
        }

        private static void ProbeSub(object owner, string ownerName, string subCollection)
        {
            try
            {
                object col = owner.GetType().InvokeMember(subCollection, BindingFlags.GetProperty, null, owner, null);
                if (col == null) return;
                ComDiagnostics.LogMembers($"{ownerName}.{subCollection}", col);
                ComDiagnostics.LogSignatures(col, "Add", "Add2", "AddSimple", "AddByFaces");
            }
            catch { /* sub-coleção inexistente nesta versão do SE — ok */ }
        }

        // ================================================================ ordenado

        /// <summary>
        /// Troca a peça para modelagem ORDENADA (item 7) — deixa a feature de união editável
        /// na árvore para o operador ajustar o offset/gap. Só no build FINAL (OK). Nunca aborta.
        /// </summary>
        public void FinalizeToOrdered(dynamic partDoc, BlockOverSurfacesResult result, BlockOverSurfacesOptions opt)
        {
            if (opt == null || !opt.SwitchToOrdered) return;
            if (result != null && !(result.SurfacesUnited || result.SurfacesOffset)) return; // nada de superfície criado
            try
            {
                partDoc.ModelingMode = 2; // igOrdered
                if (result != null) result.SwitchedToOrdered = true;
                Log.Info("Modelagem alternada para ORDENADO (feature de união editável para ajuste de offset/gap).");
            }
            catch (Exception ex) { Log.Warn("Alternar para ordenado falhou (modelo preservado): " + ex.GetBaseException().Message); }
        }

        // ================================================================ infra

        private static readonly int[] StaleBackoffMs = { 200, 400, 800, 1500 };

        private static T Retry<T>(Func<T> action, string what)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return action(); }
                catch (Exception ex) when (IsStale(ex) && attempt < StaleBackoffMs.Length)
                {
                    Log.Warn($"  {what}: 0x80010114 (modelo regenerando) — tentativa {attempt + 2}/{StaleBackoffMs.Length + 1} em {StaleBackoffMs[attempt]}ms...");
                    System.Threading.Thread.Sleep(StaleBackoffMs[attempt]);
                }
            }
        }

        private static bool IsStale(Exception ex)
            => ex.GetBaseException() is COMException ce && unchecked((uint)ce.ErrorCode) == 0x80010114u;
    }
}
