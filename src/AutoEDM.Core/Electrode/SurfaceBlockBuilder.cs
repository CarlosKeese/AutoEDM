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

        /// <summary>Blank escolhido no pop-up. Null = auto (o mais compacto que serve).</summary>
        public BlankSpec ChosenBlank { get; set; }

        /// <summary>Espaço (mm) entre o TOPO das superfícies e a BASE do bloco — o controle
        /// "aumentar o espaço". A forma copiada sobe do fundo até tocar o bloco; o gap dá
        /// folga entre a ponta da forma e o bloco. Default 1 mm (pedido do Carlos).</summary>
        public double GapMm { get; set; } = 1.0;

        /// <summary>Altura do bloco/holder (mm).</summary>
        public double BlockHeightMm { get; set; } = 15.0;

        /// <summary>Aplicar a fixação (furos M6+2×Ø4 se couberem, senão eixo no topo).</summary>
        public bool ApplyFixation { get; set; } = true;

        /// <summary>Padrão de fixação (diâmetros/profundidades/eixo).</summary>
        public FixationPattern Fixation { get; set; } = new FixationPattern();
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

        public IReadOnlyList<BlankSpec> EligibleBlanks = new List<BlankSpec>();
        public BlankSpec ChosenBlank;
        public bool RoundBlank;
        public double BlockXmm, BlockYmm, BlockHmm;
        public double BlockBaseZmm;                 // topo das superfícies + gap

        public readonly List<string> Warnings = new List<string>();

        /// <summary>Texto de 1 parágrafo p/ o rótulo da janela.</summary>
        public string Summary()
        {
            if (!SurfacesFound) return "Nenhuma superfície encontrada. Selecione a superfície copiada e tente de novo.";
            string blk = ChosenBlank != null ? ChosenBlank.Describe() : "sem blank do catálogo (bloco = pegada / comprar material)";
            string s =
                $"Pegada: {FootprintXmm:0.0} × {FootprintYmm:0.0} mm  (fonte: {SurfaceSource})\r\n" +
                $"Blank: {blk}\r\n" +
                $"Bloco: {BlockXmm:0.0} × {BlockYmm:0.0} × {BlockHmm:0.0} mm\r\n" +
                $"Base do bloco: Z = {BlockBaseZmm:0.0} mm (gap {(BlockBaseZmm - SurfacesTopZmm):0.0} mm acima do topo das superfícies)";
            if (Warnings.Count > 0) s += "\r\n⚠ " + string.Join("; ", Warnings);
            return s;
        }
    }

    /// <summary>Resultado da modelagem — inclui os handles p/ o Preview limpar.</summary>
    public sealed class BlockOverSurfacesResult
    {
        public BlockOverSurfacesPlan Plan;
        public bool BlockCreated, SurfacesUnited, FixationApplied;
        /// <summary>Features criadas, na ordem de criação (o Cleanup apaga em ordem reversa).</summary>
        public readonly List<object> CreatedFeatures = new List<object>();
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

            // Blanks elegíveis (do mais compacto ao maior) p/ o pop-up.
            double footLong = Math.Max(box.SizeX, box.SizeY);
            double footShort = Math.Min(box.SizeX, box.SizeY);
            var needBox = new BoundingBox { MaxX = footLong, MaxY = footShort };
            plan.EligibleBlanks = _blanks.EligibleBlanks(needBox, 0.0, opt.Material);

            BlankSpec blank = opt.ChosenBlank ?? plan.EligibleBlanks.FirstOrDefault();
            plan.ChosenBlank = blank;
            if (blank == null)
                plan.Warnings.Add($"Pegada {footLong:0.0}×{footShort:0.0} mm excede a maior seção de '{opt.Material}' — comprar material (bloco cru = pegada).");

            ComputeBlockDims(plan, blank, box);
            plan.BlockHmm = opt.BlockHeightMm;
            plan.BlockBaseZmm = box.MaxZ + opt.GapMm;
            return plan;
        }

        private static void ComputeBlockDims(BlockOverSurfacesPlan plan, BlankSpec blank, BoundingBox box)
        {
            double footLong = Math.Max(box.SizeX, box.SizeY);
            double footShort = Math.Min(box.SizeX, box.SizeY);
            double blockLong, blockShort;
            bool round = false;

            if (blank == null)
            {
                blockLong = footLong; blockShort = footShort; // sem catálogo: bloco cru = pegada
            }
            else
            {
                switch (blank.Shape)
                {
                    case BlankShape.Round:
                        blockLong = blockShort = blank.DimA; round = true; break;
                    case BlankShape.Rectangular:
                        blockLong = Math.Max(blank.DimA, blank.DimB ?? blank.DimA);
                        blockShort = Math.Min(blank.DimA, blank.DimB ?? blank.DimA);
                        break;
                    default: // Square
                        blockLong = blockShort = blank.DimA; break;
                }
            }
            plan.RoundBlank = round;
            bool xIsLong = box.SizeX >= box.SizeY; // orienta o lado maior do blank ao lado maior da pegada
            plan.BlockXmm = xIsLong ? blockLong : blockShort;
            plan.BlockYmm = xIsLong ? blockShort : blockLong;
        }

        // ================================================================ BUILD

        /// <summary>Modela: bloco + estende/fecha/une as superfícies + fixação. Devolve os handles p/ Preview.</summary>
        public BlockOverSurfacesResult Build(dynamic partDoc, BlockOverSurfacesOptions opt)
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
                     $"bloco {plan.BlockXmm:0.0}×{plan.BlockYmm:0.0}×{plan.BlockHmm:0.0}, base Z={plan.BlockBaseZmm:0.0} (gap {opt.GapMm:0.0}).");

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

            // (2) ESTENDER + FECHAR + UNIR as superfícies ao bloco.
            try
            {
                var surfItems = CollectSurfaceItems(partDoc);
                var created = TryCloseAndUnite(partDoc, surfItems, plan);
                result.CreatedFeatures.AddRange(created);
                result.SurfacesUnited = created.Count > 0;
            }
            catch (Exception ex)
            {
                Log.Warn("Estender/fechar/unir superfícies (bloco preservado): " + ex.GetBaseException().Message);
            }

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

            Log.Info($"Concluído: bloco={result.BlockCreated}, superfícies unidas={result.SurfacesUnited}, " +
                     $"fixação={result.FixationApplied}. Features criadas: {result.CreatedFeatures.Count}.");
            return result;
        }

        // ============================================================== CLEANUP

        /// <summary>Apaga (ordem reversa) as features criadas por um Build — usado no re-preview e no Cancel.</summary>
        public void Cleanup(BlockOverSurfacesResult result)
        {
            if (result?.CreatedFeatures == null || result.CreatedFeatures.Count == 0) return;
            int ok = 0, fail = 0;
            for (int i = result.CreatedFeatures.Count - 1; i >= 0; i--)
            {
                object f = result.CreatedFeatures[i];
                if (f == null) continue;
                try { Retry(() => { ((dynamic)f).Delete(); return true; }, "Delete feature"); ok++; }
                catch (Exception ex) { fail++; Log.Warn("Cleanup: não removeu uma feature: " + ex.GetBaseException().Message); }
            }
            result.CreatedFeatures.Clear();
            result.BlockCreated = result.SurfacesUnited = result.FixationApplied = false;
            Log.Info($"Preview limpo: {ok} feature(s) removida(s)" + (fail > 0 ? $", {fail} falha(s)" : "") + ".");
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
        /// Estende as superfícies até o bloco, fecha-as e une ao bloco. As assinaturas
        /// exatas dessas operações de superfície ainda serão confirmadas pelo PROBE da 1ª
        /// execução; por ora tenta a hipótese 1 (StitchSurfaces do <see cref="ModelingHelpers"/>)
        /// e loga o resultado — o bloco é preservado se falhar. Devolve as features criadas.
        /// </summary>
        private static List<object> TryCloseAndUnite(dynamic partDoc, List<object> surfItems, BlockOverSurfacesPlan plan)
        {
            var created = new List<object>();
            if (surfItems == null || surfItems.Count == 0)
            {
                Log.Warn("Estender/fechar/unir: nenhuma superfície-item para costurar.");
                return created;
            }

            Log.Info($"Estender/fechar/unir {surfItems.Count} superfície(s) até o bloco (base Z={plan.BlockBaseZmm:0.0}). " +
                     "Assinaturas definitivas dependem do PROBE — tentando hipótese StitchSurfaces.");
            try
            {
                dynamic constructions = partDoc.Constructions;
                object stitch = ModelingHelpers.StitchSurfaces(constructions, surfItems.ToArray(), true, 0.0001);
                if (stitch != null)
                {
                    created.Add(stitch);
                    Log.Info("StitchSurfaces: OK (confirmar visualmente se fechou/uniu ao bloco).");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("StitchSurfaces (hipótese 1) falhou — ver PROBE p/ a API correta: " + ex.GetBaseException().Message);
            }
            return created;
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

        // ================================================================ infra

        private static T Retry<T>(Func<T> action, string what)
        {
            try { return action(); }
            catch (Exception ex) when (IsStale(ex))
            {
                Log.Warn($"  {what}: 0x80010114 (modelo regenerando) — nova tentativa em 250ms...");
                System.Threading.Thread.Sleep(250);
                return action();
            }
        }

        private static bool IsStale(Exception ex)
            => ex.GetBaseException() is COMException ce && unchecked((uint)ce.ErrorCode) == 0x80010114u;
    }
}
