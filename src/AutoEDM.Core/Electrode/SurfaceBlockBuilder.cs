using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>Parâmetros do botão "Criar Base" (ambiente de PEÇA).</summary>
    public sealed class BlockOverSurfacesOptions
    {
        /// <summary>Material do eletrodo (filtra o catálogo de blanks). Default "Cobre".</summary>
        public string Material { get; set; } = "Cobre";

        /// <summary>Blank/uso escolhido no pop-up (em pé ou deitado c/ corte). Null = auto (o mais compacto).</summary>
        public BlankChoice ChosenBlank { get; set; }

        /// <summary>Comprimento máximo de corte da barra (mm) — a "medida variável". Default 500.</summary>
        public double BarMaxLengthMm { get; set; } = 500.0;

        /// <summary>Espaço (mm) entre o TOPO das superfícies e a BASE do bloco — o controle
        /// "aumentar o espaço". Default 0 (Carlos, 2026-07-17): esse espaço só fazia sentido
        /// como folga para o "Unir superfícies" ESTENDER a superfície até o bloco; enquanto
        /// esse estender não é automatizado, deixar espaço só cria um vão solto — melhor a
        /// base tocar direto no topo da superfície (gap 0), pronta p/ unir sem precisar
        /// estender.</summary>
        public double GapMm { get; set; } = 0.0;

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

        /// <summary>Perna do chanfro 1×45° de orientação no canto X+ Y− (blank QUAD/RET); no
        /// blank REDONDO, a mesma medida vira a profundidade do FLAT de orientação em Y−
        /// (Carlos, 2026-07-17: a faixa redonda precisa de uma face chata voltada para Y−,
        /// em vez do canto chanfrado — não faz sentido chanfrar um canto que não existe).</summary>
        public double ChamferLegMm { get; set; } = 3.0;

        /// <summary>
        /// Após unir, ALTERNAR para modelamento ORDENADO (item 7) — deixa a feature de união
        /// editável (o operador ajusta o offset/gap na árvore). 2 = igOrdered.
        /// </summary>
        public bool SwitchToOrdered { get; set; } = true;

        /// <summary>
        /// GAP/Ra/cor escolhidos na lista suspensa do "Unir superfícies" (Carlos, 2026-07-17).
        /// Null = só diagnostica as arestas abertas (comportamento antigo, sem aplicar nada).
        /// Com valor: aplica o offset via <c>Model.FaceOffsets</c> (ORDENADO, editável depois —
        /// NÃO <c>Constructions.OffsetSurfaces</c> em síncrono, que não permite reeditar o GAP)
        /// e tenta pintar a superfície com a cor do Ra escolhido, então une ao bloco.
        /// </summary>
        public RaGapPresets.Choice UniteChoice { get; set; }
    }

    /// <summary>Resultado do dimensionamento SEM modelar — alimenta o pop-up e o resumo ao vivo.</summary>
    public sealed class BlockOverSurfacesPlan
    {
        public bool SurfacesFound;
        public string SurfaceSource = "(nenhuma)";
        public int SurfaceFaceCount;

        public BoundingBox Footprint;
        public double FootprintXmm, FootprintYmm;
        public double FootprintCenterXmm, FootprintCenterYmm; // centro XY da PEGADA (pode estar deslocado da origem)
        public double CenterXmm, CenterYmm;         // centro do BLOCO/faixa/fixação = ORIGEM (0,0), p/ fixação/zero-máquina
        public double SurfacesTopZmm, SurfacesBottomZmm;

        public IReadOnlyList<BlankChoice> EligibleBlanks = new List<BlankChoice>();
        public BlankChoice ChosenBlank;
        public bool RoundBlank;
        public double BlockXmm, BlockYmm, BlockHmm;
        public double BandBaseZmm;                  // topo das superfícies + gap (base da FAIXA)
        public double BlockBaseZmm;                 // base do BLOCO = faixa (base+altura) OU só o gap se sem faixa
        public bool HasBand;                        // a faixa de medição entra no dimensionamento?

        public readonly List<string> Warnings = new List<string>();

        /// <summary>Texto de 1 parágrafo p/ o rótulo da janela.</summary>
        public string Summary()
        {
            if (!SurfacesFound) return "Nenhuma superfície encontrada. Selecione a superfície copiada e tente de novo.";
            string blk = ChosenBlank != null ? ChosenBlank.Describe() : "sem blank do catálogo (bloco = pegada / comprar material)";
            string orientKey = RoundBlank ? "flat em Y−" : "chanfro X+ Y−";
            string band = HasBand
                ? $"Faixa: Z {BandBaseZmm:0.0}→{BlockBaseZmm:0.0} mm (5 mm, gap {(BandBaseZmm - SurfacesTopZmm):0.0} mm acima da superfície) + {orientKey}\r\n"
                : "Faixa: (desligada)\r\n";
            string s =
                $"Pegada: {FootprintXmm:0.0} × {FootprintYmm:0.0} mm  (fonte: {SurfaceSource})\r\n" +
                $"Blank: {blk}\r\n" +
                band +
                $"Bloco: {BlockXmm:0.0} × {BlockYmm:0.0} × {BlockHmm:0.0} mm, parte de Z = {BlockBaseZmm:0.0} mm";
            if (Warnings.Count > 0) s += "\r\n⚠ " + string.Join("; ", Warnings);
            return s;
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

            // Rede de segurança (achado 2026-07-17): o bbox por-FACE perde qualquer face cuja
            // leitura de range falhe por completo (visto numa face curva sem Vertices) — isso
            // subestimou o topo real de uma superfície com calota/nariz arredondado, e a faixa de
            // medição saiu cortando 0,2mm DENTRO da geometria. O bbox do CORPO inteiro
            // (Body.GetRange) não depende de nenhuma face individual, então expande (nunca
            // encolhe) o bbox por-face com o range de cada item de superfície.
            foreach (var item in CollectSurfaceItems(partDoc))
            {
                if (!FaceGeometry.TryGetBodyRangeMm(item, out double[] bmin, out double[] bmax)) continue;
                if (bmin[0] < box.MinX) box.MinX = bmin[0]; if (bmax[0] > box.MaxX) box.MaxX = bmax[0];
                if (bmin[1] < box.MinY) box.MinY = bmin[1]; if (bmax[1] > box.MaxY) box.MaxY = bmax[1];
                if (bmin[2] < box.MinZ) box.MinZ = bmin[2]; if (bmax[2] > box.MaxZ) box.MaxZ = bmax[2];
            }
            plan.Footprint = box;
            plan.FootprintXmm = box.SizeX;
            plan.FootprintYmm = box.SizeY;
            plan.FootprintCenterXmm = (box.MinX + box.MaxX) / 2.0; // centro da PEGADA (pode estar deslocado)
            plan.FootprintCenterYmm = (box.MinY + box.MaxY) / 2.0;
            // O bloco/faixa/fixação são CENTRADOS NA ORIGEM da peça (0,0) — a referência de
            // fixação / zero-máquina (Carlos, 2026-07-16: bloco fora do centro em X,Y gera
            // problema de fabricação), NÃO no centro da pegada.
            plan.CenterXmm = 0.0;
            plan.CenterYmm = 0.0;
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
            // needBox CENTRADO NA ORIGEM cobrindo a pegada: meia-largura = distância da origem à
            // borda MAIS LONGE da pegada + crescimento. Assim o bloco centrado em (0,0) contém a
            // queima mesmo deslocada da origem (Carlos, 2026-07-16). Pegada centrada → igual ao antigo.
            double halfX = Math.Max(Math.Abs(box.MinX), Math.Abs(box.MaxX)) + growPerSide;
            double halfY = Math.Max(Math.Abs(box.MinY), Math.Abs(box.MaxY)) + growPerSide;
            BoundingBox needBox = new BoundingBox
            {
                MinX = -halfX, MaxX = halfX,
                MinY = -halfY, MaxY = halfY,
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

            return plan;
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

            Log.Info($"Criar Base: pegada {plan.FootprintXmm:0.0}×{plan.FootprintYmm:0.0} mm, centro da pegada " +
                     $"({plan.FootprintCenterXmm:0.0},{plan.FootprintCenterYmm:0.0}), topo Z={plan.SurfacesTopZmm:0.0}; " +
                     $"blank {(plan.ChosenBlank?.Describe() ?? "(cru=pegada)")}; " +
                     $"bloco {plan.BlockXmm:0.0}×{plan.BlockYmm:0.0}×{plan.BlockHmm:0.0} CENTRADO NA ORIGEM (0,0), base Z={plan.BlockBaseZmm:0.0} " +
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

            // (1b) FAIXA DE MEDIÇÃO (item 5): degrau menor + orientação, sob o bloco (topo da
            // faixa = base do bloco). Funde com o bloco (protrusão). Blank QUAD/RET -> faixa
            // retangular + chanfro no canto X+ Y−; blank REDONDO -> faixa REDONDA + flat em Y−
            // (Carlos, 2026-07-17: a faixa precisa acompanhar a forma do blank).
            if (opt.AddMeasurementBand)
            {
                try
                {
                    object band = plan.RoundBlank
                        ? BlankModeler.AddMeasurementBandRound(partDoc, plan.BlockXmm,
                            plan.BlockBaseZmm, opt.BandHeightMm, opt.BandMarginMm, opt.ChamferLegMm,
                            plan.CenterXmm, plan.CenterYmm)
                        : BlankModeler.AddMeasurementBand(partDoc, plan.BlockXmm, plan.BlockYmm,
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

            Log.Info($"Concluído: bloco={result.BlockCreated}, faixa={result.BandCreated}, " +
                     $"ordenado={result.SwitchedToOrdered}, fixação={result.FixationApplied}. " +
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
        /// Botão ISOLADO "Unir superfícies": une a superfície de queima (faces SELECIONADAS
        /// ou CopySurface existente) ao bloco → um sólido único, via o comando "Unir"
        /// (<c>Model.Unions.Add</c>) DIRETO na superfície crua, em SÍNCRONO — SEM costurar
        /// (Carlos, 2026-07-20: "Unir superfícies" estava tentando costurar as superfícies pra
        /// unir ao bloco, esse não é o recurso correto; o comando certo é só "Unir", no síncrono,
        /// não no ordenado — Costurar junta múltiplas superfícies numa só, "Unir" já é a booleana
        /// que funde a superfície ao sólido). SEPARADO do "Criar Base" porque o thicken, ao
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

            int mode0 = 1; try { mode0 = (int)partDoc.ModelingMode; } catch { }
            Log.Info($"Unir superfícies: ModelingMode = {mode0} (1=síncrono, 2=ordenado). Passo atual = DIAGNÓSTICO das arestas abertas (read-only, não altera a peça).");

            TryExtendStitchUnite(partDoc, result.Plan, opt, result);
            return result;
        }

        // Processo de superfície (Carlos, 2026-07-20 — CORRIGE o processo de 2026-07-16/17
        // abaixo): diagnostica vãos VERTICAIS (X,Y, ainda fechados manualmente com "Limite") →
        // UNE direto ao bloco com o comando "Unir" (Model.Unions.Add), SEM costurar, em
        // SÍNCRONO. "Costurar" (StitchSurfaces) NÃO é o recurso certo aqui — junta múltiplas
        // superfícies numa só; o que fecha a superfície de queima ao bloco é a booleana "Unir"
        // agindo direto entre o sólido do bloco e a superfície crua (encostada nele, GapMm=0).
        // Só troca para Ordenado DEPOIS da união, e só por causa do GAP (FaceOffsets exige
        // Ordenado) — o União em si roda em Síncrono.
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

            bool readyToUnite = DiagnoseOpenEdges(surf, blockBottomZmm, result);
            if (!readyToUnite) return;

            if (opt.UniteChoice == null)
            {
                Log.Info("Unir: pronta p/ unir, mas nenhum GAP/Ra escolhido (opt.UniteChoice=null) — só diagnóstico, nada foi alterado.");
                return;
            }

            // Cor ANTES de unir — pinta a CopySurface enquanto as faces ainda são referências
            // frescas (o União pode invalidar as antigas e criar faces novas no sólido mesclado).
            TryPaintSurface(surf, opt.UniteChoice.Color);

            bool united = TryUniteToBlock(partDoc, blockModel, surf);
            if (!united)
            {
                Log.Warn("Unir: União falhou — GAP não aplicado (bloco/faixa/furos preservados, nada foi perdido).");
                return;
            }
            result.SurfacesUnited = true;

            // GAP (offset de faísca) DEPOIS de unir, no sólido já mesclado. `Model.FaceOffsets`
            // só existe em modelagem ORDENADA (Carlos, 2026-07-17: síncrono não permite reeditar
            // o GAP depois) — troca AGORA, só para esse passo; o União ficou em Síncrono.
            int mode = 1; try { mode = (int)partDoc.ModelingMode; } catch { }
            if (mode != 2)
            {
                try { partDoc.ModelingMode = 2; result.SwitchedToOrdered = true; Log.Info("Unir: alternado para ORDENADO só para o GAP (FaceOffsets não existe em síncrono)."); }
                catch (Exception e) { Log.Warn("Unir: alternar p/ Ordenado (GAP) falhou — união ficou sem GAP: " + e.GetBaseException().Message); return; }
            }

            // REAQUIRE as faces de queima DO CORPO JÁ MESCLADO — as faces da `surf` ORIGINAL
            // (capturadas antes do União) ficam obsoletas, a superfície foi CONSUMIDA pelo Attach
            // (achado 2026-07-20, log `094346`: `FaceOffsets.AddEx` deu DISP_E_TYPEMISMATCH usando
            // as faces antigas). O bloco tem base em `blockBottomZmm`; a queima ficava abaixo/na
            // base, então as faces do corpo mesclado com Z ≤ base são a queima.
            var mergedBurnFaces = CollectBodyFacesAtOrBelowZ(blockModel, blockBottomZmm);
            Log.Info($"Unir: {mergedBurnFaces.Count} face(s) de queima reaquirida(s) do corpo mesclado (Z ≤ {blockBottomZmm:0.0}mm) p/ o GAP.");

            object offsetFeature = TryApplyGapOffset(mergedBurnFaces, opt.UniteChoice.GapMm, blockModel);
            if (offsetFeature != null) { result.CreatedFeatures.Add(offsetFeature); result.SurfacesOffset = true; }
        }

        /// <summary>
        /// Une a superfície de queima ao SÓLIDO do bloco, em SÍNCRONO. NUNCA lança — se falhar,
        /// bloco/faixa/furos ficam intactos e o log dá o material p/ corrigir. Devolve TRUE se
        /// uniu (não há um "feature" novo pra devolver — ver CORREÇÃO #3 abaixo).
        ///
        /// CORREÇÃO 2026-07-20 #1 (Carlos): o botão estava tentando `StitchSurfaces` como HACK pra
        /// satisfazer o União (costurando a superfície + um patch sintético do rim) — esse NÃO é o
        /// recurso certo. O União roda em SÍNCRONO, sem alternar para Ordenado antes (só o GAP
        /// depois exige Ordenado — ver o chamador).
        ///
        /// CORREÇÃO #2: `Model.Unions.Add` (booleana ORDENADA) SEMPRE deu E_FAIL em síncrono, com
        /// alvo/ferramenta corretamente tipados (2 testes reais, log `082048`/`083010`).
        ///
        /// CORREÇÃO #3, com dado real (Carlos gravou o processo manual dele, log `083010`,
        /// 08:32:35): depois do "Unir" manual + GAP, NENHUMA feature nova de união/booleana
        /// apareceu em nenhuma coleção — a CopySurface original só passou a aparecer em
        /// `Model.Features` (antes só em `Constructions.CopySurfaces`). Bate com
        /// `Model.Attach(NumOfObjects, psaObjects, bAdd, fpcSide) -> void` (RETORNA void = não
        /// cria feature) — o "anexar" que o Carlos já tinha descrito (existe na UI, NÃO é feature
        /// registrada). `Model.BooleanFeatures.Add` (`Function=3`=`seBooleanUnite`) fica de
        /// FALLBACK. Ambos via `InvokeMember` (não cast/acesso dinâmico) porque nenhum dos dois
        /// está confirmado na PIA estática do projeto (`Interop.SolidEdge` 219.0.0, mais velha que
        /// o SE 223 rodando).
        ///
        /// CORREÇÃO #4 (log `092656`): (a) `Attach` deu `DISP_E_PARAMNOTOPTIONAL` — `fpcSide` NÃO
        /// é opcional (a sondagem não tem `[opt]` nesse parâmetro, ao contrário do `PlaneSide` do
        /// `BooleanFeatures`); tenta os 2 valores conhecidos de lado (`igRight=2`, `igLeft=1`,
        /// mesma convenção já usada em `BlankModeler`). (b) NESSA gravação, com uma superfície de
        /// 7 faces soltas (via SelectSet, não uma CopySurface pronta), o processo manual do Carlos
        /// (que "funciona perfeitamente") voltou a incluir `StitchSurfaces` — bate com
        /// [[real-edm-workflow]] ("...trata/fecha/costura → gera a base → anexa..."): costurar as
        /// PRÓPRIAS faces da superfície (sem patch de rim nenhum) consolida um CopySurface de
        /// várias faces soltas numa ÚNICA superfície coesa ANTES de anexar — diferente do HACK da
        /// CORREÇÃO #1 (que costurava com um patch sintético só pra enganar o União). Por isso essa
        /// costura de auto-consolidação volta como 1º passo, opcional (segue com a superfície crua
        /// se falhar).
        ///
        /// CORREÇÃO 2026-07-20 #5 (Carlos): "se o botão for usado no ordenado ele cria uma feature
        /// de costura apenas" — o documento pode chegar aqui JÁ em Ordenado (sobrou de um run
        /// anterior que trocou de modo p/ o GAP). `Attach` parece ser operação SÍNCRONA (mesmo
        /// padrão do `AddThickenFeature`, que virava no-op silencioso em peça síncrona quando a
        /// feature era só-Ordenada) — então força SÍNCRONO aqui, ANTES de costurar/anexar, mesmo
        /// que o doc já esteja em Ordenado; só volta pra Ordenado DEPOIS, para o GAP (ver chamador).
        /// </summary>
        private static bool TryUniteToBlock(dynamic partDoc, dynamic blockModel, dynamic surf)
        {
            try
            {
                int m = (int)partDoc.ModelingMode;
                if (m != 1) { partDoc.ModelingMode = 1; Log.Info("Unir: alternado de volta pra SÍNCRONO (Costurar/Anexar não funcionam em Ordenado)."); }
            }
            catch (Exception e) { Log.Warn("Unir: checar/alternar p/ Síncrono falhou (seguindo mesmo assim) — " + e.GetBaseException().Message); }

            object model = (object)blockModel;

            dynamic tool = surf;
            try
            {
                var stitchCol = (SolidEdgePart.StitchSurfaces)partDoc.Constructions.StitchSurfaces;
                System.Array surfArr = new SolidEdgePart.CopySurface[] { (SolidEdgePart.CopySurface)surf };
                tool = stitchCol.Add(surfArr.Length, ref surfArr, true, Type.Missing);
                Log.Info("Unir: superfície costurada (consolida as próprias faces — sem patch de rim).");
            }
            catch (Exception e) { Log.Warn("Unir: costura de consolidação falhou (seguindo com a superfície crua) — " + e.GetBaseException().Message); }

            // SAFEARRAY(IDispatch) — precisa ser TIPADO, não `object[]` (vira SAFEARRAY(VARIANT) →
            // DISP_E_TYPEMISMATCH, achado no teste real 2026-07-20, log `091655`). `tool` pode ser
            // StitchSurface (se a costura acima funcionou) ou a CopySurface crua (se falhou).
            System.Array tools;
            try { tools = new SolidEdgePart.StitchSurface[] { (SolidEdgePart.StitchSurface)tool }; }
            catch
            {
                try { tools = new SolidEdgePart.CopySurface[] { (SolidEdgePart.CopySurface)tool }; }
                catch (Exception e)
                {
                    Log.Warn("Unir: ferramenta não tipável (nem StitchSurface, nem CopySurface, E_NOINTERFACE) — " + e.GetBaseException().Message);
                    return false;
                }
            }

            // fpcSide NÃO é opcional (achado 2026-07-20, log `092656`) — tenta os 2 lados.
            foreach (var side in new[] { 2 /* igRight */, 1 /* igLeft */ })
            {
                try
                {
                    object[] attachArgs = { tools.Length, tools, true, side };
                    model.GetType().InvokeMember("Attach", BindingFlags.InvokeMethod, null, model, attachArgs);
                    Log.Info($"Unir: superfície ANEXADA ao bloco (Model.Attach, bAdd=true, fpcSide={side}).");
                    return true;
                }
                catch (Exception e) { Log.Warn($"Unir: Model.Attach (fpcSide={side}) falhou — " + e.GetBaseException().Message); }
            }

            const int seBooleanUnite = 3; // SolidEdgePart.BooleanFeatureConstants (docs/api/constants.md)
            try
            {
                // Pega a coleção via InvokeMember (GetProperty), NÃO acesso dinâmico
                // (`blockModel.BooleanFeatures`) — achado no teste real 2026-07-20 (log `091655`):
                // o acesso dinâmico tenta um QueryInterface p/ um tipo da PIA estática do projeto
                // (`Interop.SolidEdge` 219.0.0, mais velha que o SE 223 rodando) e dá E_NOINTERFACE;
                // `InvokeMember` passa direto pelo IDispatch ao vivo (mesmo caminho do SPY/ProbeSub).
                object boolFeatures = model.GetType().InvokeMember(
                    "BooleanFeatures", BindingFlags.GetProperty, null, model, null);
                object[] args = { tools.Length, tools, seBooleanUnite, Type.Missing };
                object feature = boolFeatures.GetType().InvokeMember(
                    "Add", BindingFlags.InvokeMethod, null, boolFeatures, args);
                Log.Info($"Unir: superfície UNIDA ao bloco (Model.BooleanFeatures.Add, seBooleanUnite) — Status {FeatureStatusText(feature)}.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Unir: Model.BooleanFeatures.Add também falhou — " + e.GetBaseException().Message);
                return false;
            }
        }

        /// <summary>
        /// GAP (offset de faísca) no sólido já unido — `Model.FaceOffsets.AddEx`. NÃO existe na
        /// PIA estática do projeto (Interop.SolidEdge.dll só tem o `Add` de 8 params — versão
        /// antiga do SDK) — por isso via `InvokeMember` direto no objeto COM vivo, igual
        /// `AddFiniteExtrudedProtrusion`. Valores confirmados 2026-07-17 pela leitura de volta de
        /// um offset real feito pelo Carlos na UI: FaceOffsetType=1
        /// (igFaceOffsetBySynchronousOffset), BlendRecreation=194 (igIgnoreBlends),
        /// AlongOrReverseVector=20 (igNormal), AlongOrReverseDirectionToKeyPoint=44 (igNone, sem
        /// keypoint). offsetDistance NEGATIVO em metros = encolhe. NUNCA lança — GAP é secundário
        /// à união (já concluída nesse ponto).
        ///
        /// CORREÇÃO 2026-07-20 (log `095906`): mesmo com faces REAQUIRIDAS FRESCAS do corpo já
        /// mesclado (não mais as antigas, capturadas antes do União), `AddEx` continua com
        /// `DISP_E_TYPEMISMATCH` idêntico — NÃO é staleness de face, é outro argumento. Como o
        /// `NumOfLiveRules=0` sugere "nenhum array", tenta primeiro com `LiveRules`/`LiveRulesOnOff`
        /// = `null` (SAFEARRAY nulo — mais idiomático em COM p/ "nenhum elemento" que um array
        /// TIPADO de tamanho 0) antes do array vazio; se as duas falharem, loga o TIPO .NET de cada
        /// argumento p/ o próximo round não precisar adivinhar de novo.
        /// </summary>
        private static object TryApplyGapOffset(List<object> burnFaces, double gapMm, dynamic blockModel)
        {
            System.Array farr = ToTypedFaceArray(burnFaces);
            if (farr.Length == 0) { Log.Warn("Unir (GAP): sem faces tipáveis — offset pulado."); return null; }

            object faceOffsets;
            try { faceOffsets = (object)blockModel.FaceOffsets; }
            catch (Exception e) { Log.Warn("Unir (GAP): Model.FaceOffsets inacessível — " + e.GetBaseException().Message); return null; }
            try { ComDiagnostics.LogSignatures(faceOffsets, "AddEx", "Add"); } catch { }

            double offsetM = -Math.Abs(gapMm) / 1000.0;
            object lastArgs = null;
            Exception lastEx = null;
            // As 2 variantes de LiveRules (null vs array vazio tipado) deram o MESMO
            // DISP_E_TYPEMISMATCH no teste real 2026-07-20 (log `101852`) — não era isso. O dump
            // por-argumento do MESMO log revelou o real culpado: `arg[9]`/`arg[10]`
            // (ToReferenceEntity/ToKeyPoint) eram `null` PURO. Armadilha clássica do COM Interop:
            // um `null` cru num `object[]` passado a `Type.InvokeMember` vira VARIANT `VT_EMPTY`,
            // não `VT_DISPATCH` com ponteiro nulo — e um parâmetro `IDispatch` tipado (não VARIANT)
            // rejeita VT_EMPTY como tipo incompatível. Fix: `DispatchWrapper(null)` força o VARIANT
            // certo (`VT_DISPATCH`, ponteiro nulo) p/ "sem referência".
            object noRef = new DispatchWrapper(null);
            foreach (var liveRules in new[] { new object[] { new int[0], new bool[0] }, new object[] { null, null } })
            {
                object[] args =
                {
                    farr.Length, farr,
                    1,                          // FaceOffsetType = igFaceOffsetBySynchronousOffset
                    0, liveRules[0], liveRules[1], // NumOfLiveRules=0, sem regras ativas
                    194,                        // BlendRecreation = igIgnoreBlends
                    20,                         // AlongOrReverseVector = igNormal
                    offsetM,                    // offsetDistance (m) — negativo encolhe
                    noRef, noRef,               // ToReferenceEntity, ToKeyPoint — sem referência
                    0.0,                        // DistanceFromKeyPoint
                    44                          // AlongOrReverseDirectionToKeyPoint = igNone
                };
                try
                {
                    object result = faceOffsets.GetType().InvokeMember(
                        "AddEx", BindingFlags.InvokeMethod, null, faceOffsets, args);
                    Log.Info($"Unir (GAP): offset {gapMm:0.00}mm aplicado (Ordenado — editável na árvore) — Status {FeatureStatusText(result)}.");
                    return result;
                }
                catch (Exception e)
                {
                    lastEx = e; lastArgs = args;
                    Log.Warn($"Unir (GAP): AddEx (LiveRules={(liveRules[0] == null ? "null" : "array vazio tipado")}) falhou — " + e.GetBaseException().Message);
                }
            }

            if (lastEx != null && lastArgs is object[] argsArr)
            {
                for (int i = 0; i < argsArr.Length; i++)
                    Log.Info($"  Unir (GAP) arg[{i}] = {(argsArr[i] == null ? "null" : argsArr[i].GetType().FullName)}");
            }
            return null;
        }

        /// <summary>
        /// Pinta as faces da superfície de queima com a cor do Ra escolhido — tentativa
        /// DIRETA via `Face.Style.Diffuse{Red,Green,Blue}` (mesmo objeto confirmado p/ LEITURA
        /// em <see cref="AutoEDM.Selection.FaceStyleColorReader"/>; escrita ainda NÃO confirmada
        /// — 1ª tentativa). Se falhar, loga os membros do Style p/ achar o setter certo sem
        /// adivinhar de novo. NUNCA lança — cor é secundária à união/GAP.
        /// </summary>
        private static void TryPaintSurface(dynamic surf, Color color)
        {
            var faces = new List<object>();
            AddFacesFrom((object)surf, faces);
            if (faces.Count == 0) { Log.Warn("Unir (cor): sem faces p/ pintar."); return; }

            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            int ok = 0;
            foreach (var f in faces)
            {
                try
                {
                    dynamic face = f;
                    dynamic style = face.Style;
                    style.DiffuseRed = r; style.DiffuseGreen = g; style.DiffuseBlue = b;
                    ok++;
                }
                catch { /* tenta a próxima; resumo + dump abaixo */ }
            }

            if (ok == faces.Count)
                Log.Info($"Unir (cor): {ok}/{faces.Count} face(s) pintada(s) ✓ (RGB {color.R},{color.G},{color.B}).");
            else
            {
                Log.Warn($"Unir (cor): só {ok}/{faces.Count} face(s) pintada(s) — Style.Diffuse* pode ser SÓ LEITURA; dump p/ achar o setter certo:");
                try { ComDiagnostics.LogMembers("Face.Style", (object)((dynamic)faces[0]).Style); } catch { }
            }
        }

        /// <summary>
        /// Diagnóstico (Carlos): reporta as arestas ABERTAS (não-costuradas) da superfície de
        /// queima — as que pertencem a UMA só face (fronteira/laminar). Classifica em VERTICAIS
        /// (Z varia = vãos laterais X,Y, ainda fechados manualmente com "Limite" na peça antes de
        /// rodar) e horizontais (rim de topo/fundo — informativo só, o rim encostando no bloco
        /// (GapMm=0) é o que o comando "Unir" (booleana) espera; não precisa virar patch — ver
        /// <see cref="TryUniteToBlock"/>). Equivale a "Exibir Arestas Não-Costuradas". Não modela.
        /// Devolve TRUE se não há vãos VERTICAIS (X,Y).
        /// </summary>
        private static bool DiagnoseOpenEdges(dynamic surf, double blockBottomZmm, BlockOverSurfacesResult result)
        {
            // A CopySurface não expõe `.Body` (Log 2026-07-16). Pego as FACES (surf.Faces[1], que
            // já funciona) e, de cada face, suas arestas. Uma aresta de FRONTEIRA (aberta) pertence
            // a UMA só face, então aparece UMA vez ao varrer as faces (sem dupla contagem); as
            // internas (costuradas, 2 faces) aparecem 2× mas são filtradas por EdgeFaceCount!=1.
            var faces = new List<object>();
            AddFacesFrom((object)surf, faces);
            if (faces.Count == 0) { Log.Warn("Unir: a superfície de queima não deu faces p/ analisar as arestas."); return false; }

            const double zTol = 0.05; // mm — abaixo disso a aresta é "horizontal"
            int visits = 0, open = 0, vertical = 0, horizontal = 0, unknownFaceCount = 0, shown = 0;
            double vTopZ = double.NegativeInfinity, vBotZ = double.PositiveInfinity;
            foreach (var f in faces)
            {
                dynamic fedges; try { fedges = ((dynamic)f).Edges; } catch { continue; }
                int ne = 0; try { ne = (int)fedges.Count; } catch { }
                for (int i = 1; i <= ne; i++)
                {
                    object e; try { e = fedges.Item(i); } catch { continue; }
                    visits++;
                    int nf = EdgeFaceCount(e);
                    if (nf < 0) { unknownFaceCount++; continue; }
                    if (nf != 1) continue;                       // costurada (2+ faces) — não é fronteira
                    open++;
                    if (!FaceGeometry.TryGetRangeMm(e, out double[] mn, out double[] mx)) continue;
                    bool isVert = (mx[2] - mn[2]) > zTol;
                    if (isVert) { vertical++; vTopZ = Math.Max(vTopZ, mx[2]); vBotZ = Math.Min(vBotZ, mn[2]); }
                    else horizontal++;
                    if (shown++ < 40) Log.Info($"  aresta aberta: Z {mn[2]:0.0}→{mx[2]:0.0} mm ({(isVert ? "VERTICAL (vão lateral X,Y)" : "horizontal (rim)")}).");
                }
            }

            Log.Info($"Unir (diagnóstico de fechamento): {faces.Count} face(s), {visits} aresta(s) visitada(s), {open} ABERTA(s) — {horizontal} horizontal(is) (rim topo/fundo), {vertical} vertical(is) (vãos laterais X,Y a fechar)." +
                     (unknownFaceCount > 0 ? $" ({unknownFaceCount} sem contagem de faces)" : ""));

            if (vertical == 0)
            {
                Log.Info($"Unir: sem vãos VERTICAIS (X,Y) — pronta p/ unir ao bloco (rim Z≈{blockBottomZmm:0.0}mm).");
                return true;
            }

            Log.Info($"Unir: há {vertical} aresta(s) VERTICAL(is) aberta(s) (Z {vBotZ:0.0}→{vTopZ:0.0}) = VÃOS laterais em X,Y. Feche cada vão com 'Limite' (SurfaceByBoundaries) na peça e rode de novo.");
            result.Warnings.Add($"Superfície ABERTA nas laterais: {vertical} vão(s) X,Y a fechar com 'Limite' (ver log) antes de unir.");
            return false;
        }

        /// <summary>Maior Z (mm) entre os corpos criados ALÉM da baseline (topo do corpo novo).</summary>
        private static double NewBodyMaxZmm(dynamic partDoc, int baselineCount)
            => NewBodyExtremeZmm(partDoc, baselineCount, wantMax: true);

        /// <summary>Menor Z (mm) entre os corpos criados ALÉM da baseline (fundo do corpo novo).</summary>
        private static double NewBodyMinZmm(dynamic partDoc, int baselineCount)
            => NewBodyExtremeZmm(partDoc, baselineCount, wantMax: false);

        private static double NewBodyExtremeZmm(dynamic partDoc, int baselineCount, bool wantMax)
        {
            double best = wantMax ? double.NegativeInfinity : double.PositiveInfinity;
            try
            {
                int n = ModelsCount(partDoc);
                for (int i = baselineCount + 1; i <= n; i++)
                {
                    dynamic m; try { m = partDoc.Models.Item(i); } catch { continue; }
                    double z = wantMax ? BodyMaxZmm(m) : BodyMinZmm(m);
                    best = wantMax ? Math.Max(best, z) : Math.Min(best, z);
                }
            }
            catch { }
            return best;
        }

        /// <summary>Maior/menor Z (mm) do corpo de um Model (varre as faces).</summary>
        private static double BodyMaxZmm(dynamic model) => BodyExtremeZmm(model, wantMax: true);
        private static double BodyMinZmm(dynamic model) => BodyExtremeZmm(model, wantMax: false);

        private static double BodyExtremeZmm(dynamic model, bool wantMax)
        {
            double best = wantMax ? double.NegativeInfinity : double.PositiveInfinity;
            try
            {
                dynamic faces = model.Body.Faces[1];
                int fn = 0; try { fn = (int)faces.Count; } catch { }
                for (int j = 1; j <= fn; j++)
                {
                    object f; try { f = faces.Item(j); } catch { continue; }
                    if (FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx))
                        best = wantMax ? Math.Max(best, mx[2]) : Math.Min(best, mn[2]);
                }
            }
            catch { }
            return best;
        }

        /// <summary>Apaga uma feature recém-criada (best-effort) — p/ desfazer o engrosso do lado errado.</summary>
        private static void TryDeleteFeature(object feature)
        {
            if (feature == null) return;
            try { ((dynamic)feature).Delete(); } catch { }
        }

        /// <summary>Nº de faces do corpo de um Model (p/ detectar se o engrosso fundiu nele).</summary>
        private static int BodyFaceCount(dynamic model)
        {
            try { dynamic faces = model.Body.Faces[1]; return (int)faces.Count; } catch { return -1; }
        }

        /// <summary>Nº de faces adjacentes a uma aresta: 1 = fronteira (aberta/não-costurada); 2 = costurada.</summary>
        private static int EdgeFaceCount(object edge)
        {
            try { dynamic e = edge; dynamic f = e.Faces; return (int)f.Count; } catch { }
            try { dynamic e = edge; dynamic f = e.Faces[1]; return (int)f.Count; } catch { }
            return -1;
        }

        /// <summary>Texto do Status de uma feature (se existir) — igFeatureOK=0x4877F5D6.</summary>
        private static string FeatureStatusText(object feature)
        {
            if (feature == null) return "null";
            try { uint s = unchecked((uint)(int)((dynamic)feature).Status); return s == 0x4877F5D6u ? "OK" : $"0x{s:X8}"; }
            catch { return "n/a"; }
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
        /// Faces do corpo do bloco (JÁ mesclado com a queima, pós-`Attach`) cujo Z MÁXIMO fica em
        /// ou abaixo de <paramref name="zMaxMm"/> (a base do bloco) — usado p/ REAQUIRIR as faces
        /// de queima DEPOIS do União: as referências capturadas ANTES (`surf.Faces`) ficam
        /// obsoletas porque a superfície original foi CONSUMIDA pelo `Attach` (achado 2026-07-20,
        /// log `094346`: `FaceOffsets.AddEx` deu `DISP_E_TYPEMISMATCH` usando as faces antigas).
        /// As faces do BLOCO em si (caixa/cilindro) ficam inteiramente ACIMA dessa linha, então não
        /// entram — só a geometria da queima (que ficava abaixo/na base) e agora é parte do mesmo
        /// corpo.
        /// </summary>
        private static List<object> CollectBodyFacesAtOrBelowZ(dynamic model, double zMaxMm, double tolMm = 0.05)
        {
            var faces = new List<object>();
            try
            {
                dynamic all = model.Body.Faces[1]; // 1 = igQueryAll
                int n = 0; try { n = (int)all.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f; try { f = all.Item(i); } catch { continue; }
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mx[2] <= zMaxMm + tolMm) faces.Add(f);
                }
            }
            catch (Exception e) { Log.Warn("Unir: CollectBodyFacesAtOrBelowZ — " + e.GetBaseException().Message); }
            return faces;
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
                                             "ReplaceBody", "HoleGeometries",
                                             // 2026-07-20: `Model.Unions.Add` deu E_FAIL em SÍNCRONO
                                             // (com o cast certo pra CopySurface) — `BooleanFeatures`
                                             // apareceu na lista de membros do Model e nunca foi
                                             // sondado; nome sugere ser o equivalente SÍNCRONO das
                                             // booleanas (como `SyncLinearMove`/`SyncRotate` são o
                                             // par síncrono de outras operações).
                                             "BooleanFeatures" })
                    ProbeSub(model, "Model", name);
                ComDiagnostics.LogSignatures((object)partDoc.Models, "AddThickenFeature");
                // `Attach`/`Detach` (métodos do Model, não coleções) — candidatos ao "anexar"
                // que o Carlos descreveu (comando que existe na UI mas NÃO é feature registrada
                // na árvore, ver [[autoedm-decisions]]).
                ComDiagnostics.LogSignatures(model, "Attach", "Detach");
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
