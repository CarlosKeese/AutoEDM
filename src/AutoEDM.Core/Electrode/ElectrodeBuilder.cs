using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Reporting;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>Resultado de <see cref="ElectrodeBuilder.CreateElectrodeFromSelection"/> (1 clique = 1 eletrodo).</summary>
    public sealed class ManualElectrodeResult
    {
        public bool Created { get; set; }
        public string Message { get; set; }
        public string Path { get; set; }
        public int FaceCount { get; set; }
        public double CenterXmm { get; set; }
        public double CenterYmm { get; set; }
        public double DeepestZmm { get; set; }
        public double TopZmm { get; set; }
    }

    /// <summary>Resultado de <see cref="ElectrodeBuilder.DuplicateElectrodeToNextGap"/>.</summary>
    public sealed class DuplicateElectrodeResult
    {
        public bool Created { get; set; }
        public string Message { get; set; }
        public string NewPath { get; set; }
        public int InstanceCount { get; set; }
    }

    /// <summary>
    /// Orquestra a extração de eletrodos a partir de uma montagem, em contexto.
    ///
    /// Fluxo real modelado:
    ///   1. A origem da .asm é o zero-máquina; a cavidade é uma ocorrência nela.
    ///   2. Cada eletrodo é uma peça NOVA em contexto, com origem sobre a região;
    ///      a geometria entra por cópia entre peças das faces coloridas.
    ///   3. Raios pequenos demais para usinar são sinalizados.
    ///   4. As faces de queima recebem offset PARA DENTRO = f(Ra da cor). O Ra vem
    ///      da cor (RaColorMap); o desbaste usa a faixa de Ra acima; acabamento usa
    ///      o Ra da cor. Um eletrodo por passe, da mesma geometria-base.
    ///   5. Blank padrão do catálogo + holder + furos de fixação (M6 + 2xØ4@15).
    ///   6. Re-pintura das faces de queima e SaveAs .par nativo (o NX lê direto).
    ///
    /// Este estágio (<see cref="PlanFromAssembly"/>) é NÃO-DESTRUTIVO: só leituras
    /// COM + decisões. A geometria fica nos métodos declarados abaixo, implementados
    /// após validação num molde real.
    /// </summary>
    public sealed class ElectrodeBuilder
    {
        private readonly SolidEdgeConnector _connector;
        private readonly IOffsetPolicy _offsetPolicy;
        private readonly IBlankLibrary _blankLibrary;
        private readonly RaColorMap _raColorMap;
        private readonly FixationPattern _fixation;
        private readonly FaceSelector _faceSelector;
        private readonly RegionSplitter _splitter = new RegionSplitter();

        public ElectrodeBuilder(
            SolidEdgeConnector connector,
            IOffsetPolicy offsetPolicy = null,
            IBlankLibrary blankLibrary = null,
            RaColorMap raColorMap = null,
            FixationPattern fixation = null,
            FaceSelector faceSelector = null)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _offsetPolicy = offsetPolicy ?? new RaOffsetTablePolicy();
            _blankLibrary = blankLibrary ?? new StandardBlankLibrary();
            _raColorMap = raColorMap ?? new RaColorMap();
            _fixation = fixation ?? new FixationPattern();
            _faceSelector = faceSelector ?? new FaceSelector();
        }

        /// <summary>
        /// Abre a montagem, acha a ocorrência com faces de queima e monta o plano
        /// (por região de cor/Ra) sem modificar nada.
        /// </summary>
        public ElectrodeBuildPlan PlanFromAssembly(string assemblyPath, ElectrodeParams p)
        {
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");

            dynamic asmDoc = _connector.OpenDocument(assemblyPath);
            return PlanFromAssemblyDocument(asmDoc, p);
        }

        /// <summary>
        /// Igual ao <see cref="PlanFromAssembly"/>, mas sobre um documento de
        /// montagem já aberto (ex.: a montagem ATIVA no Solid Edge).
        /// </summary>
        public ElectrodeBuildPlan PlanFromAssemblyDocument(dynamic asmDoc, ElectrodeParams p)
        {
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null)
                throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);

            var plan = new ElectrodeBuildPlan { AssemblyName = SafeDocName(asmDoc) };

            var hit = FindBurnOccurrence(ctx, app);
            OccurrenceInfo target = hit.Item1;
            IReadOnlyList<FaceGroup> groups = hit.Item2;

            if (target == null)
            {
                plan.Warnings.Add("Nenhuma ocorrência com faces nas cores de queima foi encontrada.");
                Log.Warn(plan.Warnings.Last());
                return plan;
            }

            plan.TargetOccurrenceName = target.Name;
            Log.Info($"Ocorrência-alvo: '{target.Name}' com {groups.Count} cor(es) de queima.");

            // Cada cor pode conter vários DETALHES separados (regiões conexas); cada
            // detalhe vira um eletrodo individual.
            foreach (var g in groups)
            {
                var details = _splitter.SplitBySpatialProximity(g.Faces, p.DetailGapMm);
                Log.Info($"Cor Ra {g.Ra} µm: {g.Faces.Count} face(s) -> {details.Count} detalhe(s).");
                for (int d = 0; d < details.Count; d++)
                    plan.Regions.Add(BuildRegionPlan(g.Ra, g.Color, details[d], p, d + 1));
            }

            Log.Info($"Total: {plan.Regions.Count} detalhe(s)/eletrodo(s) a gerar.");
            return plan;
        }

        /// <summary>
        /// Resolve a ocorrência de queima e as faces do PRIMEIRO detalhe — usado pelo
        /// copy-test e pela criação incremental. Não modifica nada.
        /// </summary>
        public bool TryResolveFirstDetail(dynamic asmDoc, ElectrodeParams p,
            out OccurrenceInfo target, out IReadOnlyList<SelectedFace> faces)
        {
            target = null;
            faces = null;
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);

            var hit = FindBurnOccurrence(ctx, app);
            target = hit.Item1;
            IReadOnlyList<FaceGroup> groups = hit.Item2;
            if (target == null || groups.Count == 0)
            {
                Log.Warn("Nenhuma ocorrência com faces de queima para o copy-test.");
                return false;
            }

            FaceGroup g = groups.OrderByDescending(x => x.Ra).First();
            var details = _splitter.SplitBySpatialProximity(g.Faces, p.DetailGapMm);
            if (details.Count == 0) return false;

            faces = details[0];
            Log.Info($"1º detalhe: {faces.Count} face(s), Ra {g.Ra} µm, " +
                     $"cor RGB({g.Color.R},{g.Color.G},{g.Color.B}). Alvo: {target.Name}.");
            return true;
        }

        // ------------------------------------------------------------------
        //  Ferramenta 1: relatório de coordenadas de queima (SOMENTE LEITURA)
        // ------------------------------------------------------------------

        /// <summary>
        /// Gera o relatório de coordenadas de queima da montagem: uma linha por
        /// detalhe (= por eletrodo), com a coordenada relativa ao zero-máquina
        /// (origem da montagem). NÃO cria geometria — reusa a mesma seleção por
        /// cor→Ra e segmentação por proximidade do planejamento.
        ///
        /// Convenção da coordenada: XY = centro da pegada do detalhe; Z = fundo
        /// (ponto mais baixo). Se a cavidade tiver rotação na montagem, apenas a
        /// translação é aplicada e cada linha é marcada (a rotação exigiria a matriz
        /// completa; fica para quando for validada no SE real).
        /// </summary>
        public BurnCoordinateReport BuildBurnReport(dynamic asmDoc, ElectrodeParams p)
        {
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);
            var report = new BurnCoordinateReport { AssemblyName = SafeDocName(asmDoc) };

            var hit = FindBurnOccurrence(ctx, app);
            OccurrenceInfo target = hit.Item1;
            IReadOnlyList<FaceGroup> groups = hit.Item2;
            if (target == null)
            {
                report.Warnings.Add("Nenhuma ocorrência com faces de queima encontrada.");
                Log.Warn(report.Warnings.Last());
                return report;
            }
            report.TargetOccurrenceName = target.Name;

            // Posição da cavidade na montagem (part -> zero-máquina). GetTransform
            // devolve metros; convertemos para mm.
            bool hasPlacement = ctx.TryGetPlacement(target,
                out double ox, out double oy, out double oz,
                out double axr, out double ayr, out double azr);
            bool rotated = hasPlacement && (Math.Abs(axr) + Math.Abs(ayr) + Math.Abs(azr) > 1e-6);
            report.OriginKnown = hasPlacement;
            report.OriginX = ox * 1000.0; report.OriginY = oy * 1000.0; report.OriginZ = oz * 1000.0;
            if (rotated)
                report.Warnings.Add("Cavidade tem rotação na montagem; só a translação foi aplicada às coordenadas.");

            int detailNo = 0;
            foreach (var g in groups)
            {
                var details = _splitter.SplitBySpatialProximity(g.Faces, p.DetailGapMm);
                foreach (var faces in details)
                {
                    detailNo++;
                    var bc = new BurnCoordinate
                    {
                        DetailIndex = detailNo,
                        Ra = g.Ra,
                        Color = g.Color,
                        FaceCount = faces.Count
                    };

                    if (TryGetBurnBoundingBox(faces, out BoundingBox box))
                    {
                        bc.CoordinateKnown = true;
                        bc.X = (box.MinX + box.MaxX) / 2.0 + report.OriginX;
                        bc.Y = (box.MinY + box.MaxY) / 2.0 + report.OriginY;
                        bc.Z = box.MinZ + report.OriginZ; // fundo da região
                        bc.SizeX = box.SizeX; bc.SizeY = box.SizeY; bc.SizeZ = box.SizeZ;
                        if (!hasPlacement)
                            bc.Notes.Add("posição da cavidade não lida; coordenada em sistema local da peça");
                        if (rotated)
                            bc.Notes.Add("rotação da cavidade NÃO aplicada");
                    }
                    else
                    {
                        bc.Notes.Add("bounding box não lida (API Range a validar)");
                    }

                    report.Coordinates.Add(bc);
                }
            }

            Log.Info($"Relatório: {report.Coordinates.Count} coordenada(s) de queima em '{target.Name}'.");
            return report;
        }

        // ------------------------------------------------------------------
        //  Ferramenta: analisar eletrodos por níveis de Z (SOMENTE LEITURA)
        // ------------------------------------------------------------------

        /// <summary>
        /// Segmenta as faces de queima em ELETRODOS por níveis de Z (ideia do Carlos:
        /// pisos de bolsão distintos = eletrodos distintos) e propõe a posição de cada
        /// um (centro XY + Z mais fundo). NÃO cria geometria — passo 1 (não-destrutivo)
        /// da ferramenta de criação/posicionamento. Reusa a mesma detecção de faces de
        /// queima do resto do pipeline.
        /// </summary>
        public Selection.ZAnalysisResult AnalyzeElectrodesByZ(dynamic asmDoc, ElectrodeParams p,
            Selection.ZSegmentationParams zprm = null)
        {
            OccurrenceInfo ignored;
            return AnalyzeElectrodesByZWithSource(asmDoc, p, zprm, out ignored);
        }

        /// <summary>
        /// Igual a <see cref="AnalyzeElectrodesByZ"/>, mas devolve também a OCORRÊNCIA da
        /// cavidade analisada — as coordenadas da análise são LOCAIS da peça, e quem cria
        /// eletrodos precisa do transform da ocorrência p/ converter ao espaço da montagem.
        /// </summary>
        public Selection.ZAnalysisResult AnalyzeElectrodesByZWithSource(dynamic asmDoc, ElectrodeParams p,
            Selection.ZSegmentationParams zprm, out OccurrenceInfo cavity)
        {
            cavity = null;
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);
            var hit = FindBurnOccurrence(ctx, app);
            OccurrenceInfo target = hit.Item1;
            cavity = target;
            IReadOnlyList<FaceGroup> groups = hit.Item2;
            IReadOnlyList<Selection.ColorTally> tally = hit.Item3;
            if (target == null)
            {
                Log.Warn("Nenhuma ocorrência com faces de queima encontrada.");
                return new Selection.ZAnalysisResult();
            }

            var all = new List<SelectedFace>();
            foreach (var g in groups) all.AddRange(g.Faces);
            Log.Info($"Análise Z de eletrodos em '{target.Name}': {all.Count} face(s) de queima " +
                     $"em {groups.Count} cor(es).");

            var result = new Selection.ElectrodeZAnalyzer().Analyze(all, zprm ?? new Selection.ZSegmentationParams());

            // Anexa a detecção de cor p/ a confirmação "conferir antes de criar" (Log 57):
            // cor de queima escolhida (grupo com mais faces) + histograma da peça-alvo.
            var main = groups.OrderByDescending(g => g.Faces.Count).FirstOrDefault();
            if (main != null)
            {
                result.BurnColor = main.Color;
                result.BurnRa = main.Ra;
                result.BurnFaceCount = main.Faces.Count;
            }
            if (tally != null) result.ColorTally.AddRange(tally);
            return result;
        }

        // ------------------------------------------------------------------
        //  Ferramenta: criar eletrodos posicionados COM BLOCO (sem cópia de faces)
        // ------------------------------------------------------------------

        /// <summary>
        /// Cria, para cada eletrodo proposto pela análise de Z, uma PEÇA com o BLOCO do
        /// blank já modelado, posicionada na montagem (origem = centro XY + fundo Z).
        /// NÃO copia faces — o desenhista subtrai a cavidade de cada bloco para extrair
        /// o eletrodo (ideia do Carlos; contorna o inter-part copy bloqueado).
        ///
        /// Caminho 100% validado: peça standalone (Documents.Add) → bloco por
        /// sketch+extrusão (<see cref="BlankModeler"/>) → SaveAs na subpasta "Eletrodos"
        /// ao lado da montagem → AddByFilename na montagem → PutOrigin. NÃO salva a
        /// montagem (o usuário revisa e salva).
        /// </summary>
        public int CreateElectrodesWithBlank(dynamic asmDoc, ElectrodeParams p,
            Selection.ZSegmentationParams zprm = null)
        {
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            OccurrenceInfo cavity;
            Selection.ZAnalysisResult res = AnalyzeElectrodesByZWithSource(asmDoc, p, zprm, out cavity);
            if (res.Electrodes.Count == 0) { Log.Warn("Nenhum eletrodo proposto — nada a criar."); return 0; }

            // As coordenadas da análise são LOCAIS da peça da cavidade; a ocorrência do
            // eletrodo é posicionada no espaço da MONTAGEM aplicando o TRANSFORM da
            // ocorrência da cavidade — translação E rotação (a cavidade pode estar girada
            // na montagem; sem aplicar a rotação, o bloco sai atravessado, Log 53).
            double occXmm = 0, occYmm = 0, occZmm = 0;   // translação da cavidade (mm)
            double occAx = 0, occAy = 0, occAz = 0;       // rotação da cavidade (rad)
            if (cavity != null)
            {
                var actx = new AssemblyContext(asmDoc);
                if (actx.TryGetPlacement(cavity, out double cox, out double coy, out double coz,
                                         out double cax, out double cay, out double caz))
                {
                    occXmm = cox * 1000.0; occYmm = coy * 1000.0; occZmm = coz * 1000.0; // METROS→mm
                    occAx = cax; occAy = cay; occAz = caz;
                    bool rotated = Math.Abs(cax) + Math.Abs(cay) + Math.Abs(caz) > 1e-6;
                    Log.Info($"Cavidade '{cavity.Name}' na montagem: origem ({occXmm:0.0}, {occYmm:0.0}, {occZmm:0.0}) mm" +
                             (rotated ? $" + ROTAÇÃO (rad X={cax:0.###} Y={cay:0.###} Z={caz:0.###})" : ", sem rotação"));
                    if (Math.Abs(cax) + Math.Abs(cay) > 1e-4)
                        Log.Warn("Cavidade INCLINADA (rotação X/Y ≠ 0) — só a rotação Z é aplicada ao eletrodo; confira a orientação.");
                }
                else Log.Warn("Transform da cavidade ilegível — usando coordenadas locais como se fossem da montagem.");
            }

            string folder = ResolveElectrodeFolder(asmDoc, p);
            System.IO.Directory.CreateDirectory(folder);
            Log.Info($"Criando {res.Electrodes.Count} eletrodo(s) (peça + bloco) em: {folder}");

            // Fixação decidida por TAMANHO (BlankModeler): furos M6+2×Ø4 se couberem no
            // bloco, senão EIXO cilíndrico no topo (regra do Carlos). O bloco NÃO é inflado
            // para caber os furos — é dimensionado pela pegada; a fixação se adapta.
            var fix = new FixationPattern();
            double blockMin = fix.ShaftDiameterSmall + 4.0; // ~10mm: piso p/ ao menos o eixo Ø6,1 caber

            int created = 0;
            foreach (var e in res.Electrodes)
                if (CreateAndPlaceElectrode(app, asmDoc, folder, e, occXmm, occYmm, occZmm, occAz, p, fix, blockMin))
                    created++;

            Log.Info($"{created}/{res.Electrodes.Count} eletrodo(s) criado(s). " +
                     "Revise no SE; SALVE a montagem manualmente; depois subtraia a cavidade de cada bloco.");
            return created;
        }

        // ------------------------------------------------------------------
        //  Ferramenta: criar UM eletrodo a partir da SELEÇÃO manual de faces
        // ------------------------------------------------------------------

        /// <summary>
        /// Versão MANUAL da criação de eletrodos (Carlos): em vez da análise automática
        /// por cor/nível de Z, o usuário SELECIONA à mão (no SE — clique de novo no mesmo
        /// ponto, ou segure Alt, para pegar a FACE em vez da ocorrência inteira) as faces
        /// do fundo do bolsão a erodir e clica este comando UMA vez por eletrodo. Reusa o
        /// MESMO pipeline de posicionamento/blank/fixação de <see cref="CreateElectrodesWithBlank"/>
        /// (<see cref="CreateAndPlaceElectrode"/>) — a única diferença é a origem do
        /// "candidato": aqui vem do bounding box da seleção, não do agrupamento por Z.
        /// </summary>
        public ManualElectrodeResult CreateElectrodeFromSelection(dynamic asmDoc, ElectrodeParams p)
        {
            var result = new ManualElectrodeResult();
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);

            List<object> faces = CollectSelectedFaces(asmDoc, out int skipped);
            if (faces.Count == 0)
            {
                result.Message = "Nenhuma FACE selecionada. No Solid Edge, clique na ocorrência e clique DE NOVO " +
                    "no mesmo ponto (ou segure Alt ao clicar) para selecionar a FACE em vez da peça inteira — " +
                    "selecione o(s) fundo(s) do bolsão a erodir e tente de novo.";
                Log.Warn("Criar eletrodo manual: " + result.Message);
                return result;
            }
            if (skipped > 0)
                Log.Warn($"Criar eletrodo manual: {skipped} item(ns) da seleção ignorado(s) (não são faces).");

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            int withBox = 0;
            foreach (var f in faces)
            {
                if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                withBox++;
                minX = Math.Min(minX, mn[0]); maxX = Math.Max(maxX, mx[0]);
                minY = Math.Min(minY, mn[1]); maxY = Math.Max(maxY, mx[1]);
                minZ = Math.Min(minZ, mn[2]); maxZ = Math.Max(maxZ, mx[2]);
            }
            if (withBox == 0)
            {
                result.Message = "Não consegui ler a geometria (bounding box) das faces selecionadas.";
                Log.Warn("Criar eletrodo manual: " + result.Message);
                return result;
            }

            // Ocorrência dona das faces (top-level) -> transform peça->montagem, igual ao
            // fluxo automático (translação + rotação Z; X/Y avisa e não aplica, Log 53).
            OccurrenceInfo cavity = FindOwningOccurrence(ctx, faces[0]);
            double occXmm = 0, occYmm = 0, occZmm = 0, occAz = 0;
            if (cavity != null)
            {
                if (ctx.TryGetPlacement(cavity, out double cox, out double coy, out double coz,
                                         out double cax, out double cay, out double caz))
                {
                    occXmm = cox * 1000.0; occYmm = coy * 1000.0; occZmm = coz * 1000.0;
                    occAz = caz;
                    Log.Info($"Criar eletrodo manual: faces da ocorrência '{cavity.Name}' — origem ({occXmm:0.0}, {occYmm:0.0}, {occZmm:0.0}) mm.");
                    if (Math.Abs(cax) + Math.Abs(cay) > 1e-4)
                        Log.Warn("Criar eletrodo manual: ocorrência INCLINADA (rotação X/Y ≠ 0) — só a rotação Z é aplicada; confira a orientação.");
                }
                else Log.Warn($"Criar eletrodo manual: transform de '{cavity.Name}' ilegível — usando coordenadas locais como se fossem da montagem.");
            }
            else
            {
                Log.Warn("Criar eletrodo manual: não achei a ocorrência (top-level) dona das faces selecionadas " +
                         "— usando as coordenadas locais como se já fossem da montagem.");
            }

            var e = new Selection.ProposedElectrode
            {
                Index = NextElectrodeIndex(ctx, p.ElectrodeName),
                FaceCount = faces.Count,
                CenterXmm = (minX + maxX) / 2.0,
                CenterYmm = (minY + maxY) / 2.0,
                DeepestZmm = minZ,
                TopZmm = maxZ,
                FootprintXmm = maxX - minX,
                FootprintYmm = maxY - minY
            };
            result.FaceCount = e.FaceCount;
            result.CenterXmm = e.CenterXmm; result.CenterYmm = e.CenterYmm;
            result.DeepestZmm = e.DeepestZmm; result.TopZmm = e.TopZmm;

            // Cor->Ra (Carlos, 2026-07-21): lida AQUI, nas faces originais da CAVIDADE ainda na
            // montagem — é a ÚNICA janela em que a cor de queima está disponível (depois de
            // copiada/unida, a superfície não carrega mais a cor original, ver [[autoedm-decisions]]).
            // Best-effort: só orienta o "Aplicar GAP" depois (RaVariableStore); nunca bloqueia a
            // criação do eletrodo.
            double? detectedRa = DetectCommonRa(faces, app);

            string folder = ResolveElectrodeFolder(asmDoc, p);
            System.IO.Directory.CreateDirectory(folder);
            var fix = new FixationPattern();
            double blockMin = fix.ShaftDiameterSmall + 4.0;

            Log.Info($"Criar eletrodo manual: D{e.Index:00}, {e.FaceCount} face(s), " +
                     $"centro local ({e.CenterXmm:0.0}, {e.CenterYmm:0.0}), fundo Z={e.DeepestZmm:0.0} (local).");

            result.Created = CreateAndPlaceElectrode(app, asmDoc, folder, e, occXmm, occYmm, occZmm, occAz, p, fix, blockMin, detectedRa);
            result.Path = System.IO.Path.Combine(folder, $"{p.ElectrodeName}_D{e.Index:00}.par");
            result.Message = result.Created
                ? $"Eletrodo D{e.Index:00} criado e posicionado no centro de {e.FaceCount} face(s) (fundo Z={e.DeepestZmm:0.0} mm)." +
                  (detectedRa.HasValue ? $" Ra {detectedRa.Value:0.0} detectado pela cor e gravado na peça." : "")
                : $"Falha ao criar o eletrodo D{e.Index:00} — veja o log.";
            return result;
        }

        /// <summary>
        /// Ra comum às faces selecionadas, lido pela cor (<see cref="FaceStyleColorReader"/> +
        /// <see cref="RaColorMap"/>) — mesma leitura usada pelo fluxo automático, aqui aplicada
        /// só às faces que o usuário escolheu. Devolve null (sem gravar nada) se nenhuma face
        /// tiver cor mapeada, ou se as faces mapeadas discordarem entre si (queima com Ra
        /// misto não é o caso normal — mais seguro não adivinhar).
        /// </summary>
        private static double? DetectCommonRa(List<object> faces, dynamic application)
        {
            var colorReader = new FaceStyleColorReader();
            var colorMap = new RaColorMap();
            double? ra = null;
            int matched = 0, mismatched = 0;
            foreach (var f in faces)
            {
                if (!colorReader.TryReadColor(f, application, out System.Drawing.Color color, out string colorSource)) continue;
                if (!colorMap.TryGetRa(color, out double faceRa, out _)) continue;
                matched++;
                if (ra == null) ra = faceRa;
                else if (Math.Abs(ra.Value - faceRa) > 1e-6) mismatched++;
            }
            if (matched == 0) { Log.Info("Criar eletrodo manual: nenhuma face com cor mapeada — Ra não detectado."); return null; }
            if (mismatched > 0)
            {
                Log.Warn($"Criar eletrodo manual: faces selecionadas têm Ra MISTO ({matched} mapeada(s), {mismatched} discordância(s)) — não gravando Ra (ambíguo).");
                return null;
            }
            Log.Info($"Criar eletrodo manual: Ra {ra.Value:0.0} detectado pela cor ({matched} face(s)).");
            return ra;
        }

        /// <summary>
        /// Faces (objetos COM crus) da SelectSet atual — tolerante a itens que não são faces.
        /// NUNCA falha silenciosamente: se a SelectSet vier vazia/inacessível, ou se algum item
        /// não for uma face utilizável, loga o motivo real (exceção ou dump SPY do tipo do item)
        /// em vez de só devolver "0 faces" sem explicação — é a única forma de descobrir, num
        /// próximo teste real, o que a seleção de uma face de OCORRÊNCIA (direto na montagem,
        /// sem entrar em contexto) realmente entrega em <c>AssemblyDocument.SelectSet</c>.
        /// </summary>
        private static List<object> CollectSelectedFaces(dynamic doc, out int skipped)
        {
            var faces = new List<object>();
            skipped = 0;
            dynamic ss;
            try { ss = doc.SelectSet; }
            catch (Exception ex) { Log.Warn($"Criar eletrodo manual: doc.SelectSet inacessível: {ex.GetBaseException().Message}"); return faces; }

            int n = 0;
            try { n = (int)ss.Count; }
            catch (Exception ex) { Log.Warn($"Criar eletrodo manual: SelectSet.Count falhou: {ex.GetBaseException().Message}"); return faces; }
            Log.Info($"Criar eletrodo manual: SelectSet.Count={n}.");

            for (int i = 1; i <= n; i++)
            {
                object item;
                try { item = ss.Item(i); }
                catch (Exception ex) { Log.Warn($"Criar eletrodo manual: SelectSet.Item({i}) falhou: {ex.GetBaseException().Message}"); continue; }
                if (item != null && FaceGeometry.TryGetRangeMm(item, out _, out _))
                {
                    faces.Add(item);
                }
                else
                {
                    skipped++;
                    ComDiagnostics.DumpObject($"Criar eletrodo manual: SelectSet[{i}] não é face utilizável", item, 1);
                }
            }
            return faces;
        }

        /// <summary>
        /// Acha, entre as ocorrências TOP-LEVEL da montagem, a que contém o documento da
        /// face amostrada (via Face.Document, comparado por FullName/Name — a mesma peça
        /// pode ter proxies COM diferentes, então comparar por REFERÊNCIA não é confiável).
        /// Null se a face não vier de nenhuma ocorrência top-level conhecida (ex.: dentro
        /// de uma subMontagem — fora do escopo atual, igual ao <see cref="FindBurnOccurrence"/>).
        /// </summary>
        private static OccurrenceInfo FindOwningOccurrence(AssemblyContext ctx, object sampleFace)
        {
            string docName = null, docFull = null;
            try
            {
                dynamic fdoc = ((dynamic)sampleFace).Document;
                try { docName = (string)fdoc.Name; } catch { }
                try { docFull = (string)fdoc.FullName; } catch { }
            }
            catch (Exception ex) { Log.Warn($"Criar eletrodo manual: Face.Document indisponível: {ex.GetBaseException().Message}"); }
            if (docName == null && docFull == null) return null;

            foreach (var occ in ctx.GetOccurrences())
            {
                if (occ.OccurrenceDocument == null) continue;
                string oName = null, oFull = null;
                try { oName = (string)occ.OccurrenceDocument.Name; } catch { }
                try { oFull = (string)occ.OccurrenceDocument.FullName; } catch { }
                if ((docFull != null && string.Equals(oFull, docFull, StringComparison.OrdinalIgnoreCase)) ||
                    (docName != null && string.Equals(oName, docName, StringComparison.OrdinalIgnoreCase)))
                    return occ;
            }
            return null;
        }

        /// <summary>Próximo índice "D##" livre, olhando os nomes das ocorrências já na montagem
        /// (ex.: "ELD_D01", "ELD_D02" -> devolve 3) — assim eletrodos manuais e automáticos
        /// nunca colidem de nome, mesmo entre sessões do SE (o contador não é um campo estático).</summary>
        private static int NextElectrodeIndex(AssemblyContext ctx, string electrodeName)
        {
            int max = 0;
            string prefix = (electrodeName ?? "ELD") + "_D";
            foreach (var occ in ctx.GetOccurrences())
            {
                string name = occ.Name ?? "";
                int i = name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string rest = name.Substring(i + prefix.Length);
                int j = 0; while (j < rest.Length && char.IsDigit(rest[j])) j++;
                if (j > 0 && int.TryParse(rest.Substring(0, j), out int n) && n > max) max = n;
            }
            return max + 1;
        }

        /// <summary>
        /// Cria e posiciona UM eletrodo (peça + bloco + fixação) a partir de um
        /// <see cref="Selection.ProposedElectrode"/> — reusado pela criação AUTOMÁTICA
        /// (<see cref="CreateElectrodesWithBlank"/>, um por candidato da análise de Z) e
        /// pela criação MANUAL (<see cref="CreateElectrodeFromSelection"/>, um por clique,
        /// a partir das faces que o usuário selecionou à mão). Mesmo pipeline dos dois
        /// fluxos: dimensiona o blank pela pegada, modela o bloco (caixa ou cilindro),
        /// aplica a fixação (furos ou eixo) e posiciona a ocorrência na montagem via
        /// PutTransform (origem = centro XY + fundo Z da região; rotação Z da cavidade).
        /// </summary>
        private bool CreateAndPlaceElectrode(dynamic app, dynamic asmDoc, string folder,
            Selection.ProposedElectrode e, double occXmm, double occYmm, double occZmm, double occAz,
            ElectrodeParams p, FixationPattern fix, double blockMin, double? detectedRa = null)
        {
            dynamic partDoc = null;
            try
            {
                double blockH = p.HolderHeight;

                // DIMENSIONAMENTO: menor blank PADRÃO (catálogo de cobre) que comporta a
                // PEGADA da queima (SEM sobremetal). NÃO inflado p/ os furos — a fixação
                // se adapta (furos ou eixo). Piso mínimo só p/ o eixo menor caber.
                double footLong  = Math.Max(e.FootprintXmm, e.FootprintYmm) + 2 * p.BlankMargin;
                double footShort = Math.Min(e.FootprintXmm, e.FootprintYmm) + 2 * p.BlankMargin;
                double needLong  = Math.Max(footLong,  blockMin);
                double needShort = Math.Max(footShort, blockMin);

                var needBox = new BoundingBox { MaxX = needLong, MaxY = needShort };
                BlankSpec blank = _blankLibrary.SelectBlank(needBox, 0.0, p.Material);

                double blockLong, blockShort; bool roundBlank = false;
                if (blank != null)
                {
                    switch (blank.Shape)
                    {
                        case BlankShape.Rectangular: blockLong = blank.DimA; blockShort = blank.DimB ?? blank.DimA; break;
                        case BlankShape.Round:       roundBlank = true; blockLong = blockShort = blank.DimA; break;
                        default:                     blockLong = blockShort = blank.DimA; break; // Square
                    }
                    Log.Info($"Eletrodo {e.Index}: blank {blank.Describe()} p/ pegada {e.FootprintXmm:0.0}×{e.FootprintYmm:0.0}.");
                }
                else
                {
                    blockLong = needLong; blockShort = needShort;
                    Log.Warn($"Eletrodo {e.Index}: NENHUM blank de '{p.Material}' comporta {needLong:0.0}×{needShort:0.0} mm — " +
                             "COMPRAR MATERIAL. Usando caixa sob medida.");
                }

                // Orienta o lado MAIOR do blank ao longo do lado maior da pegada.
                bool xIsLong = e.FootprintXmm >= e.FootprintYmm;
                double blockX = xIsLong ? blockLong : blockShort;
                double blockY = xIsLong ? blockShort : blockLong;

                // POSICIONAMENTO (regra do Carlos, Logs 51-52) — DOIS deslocamentos:
                //  (1) MONTAGEM: PutOrigin coloca o ZERO-PEÇA (origem do .par) na
                //      SUPERFÍCIE de queima, no espaço da MONTAGEM. A superfície local da
                //      cavidade vira montagem somando o TRANSFORM da ocorrência da cavidade
                //      (occ*mm) — que agora é lido CORRETO (antes vinha 0 por bug do
                //      GetTransform, jogando o eletrodo ~23mm fora no Z).
                //  (2) .par: o bloco é levantado internamente pela distância
                //      (superfície→zero-máquina) + folga, de modo que o FUNDO do holder
                //      fique 'HolderBaseClearanceMm' acima do zero-máquina (origem da
                //      montagem). Assim a origem toca a superfície (lá embaixo) e o holder
                //      fica no plano de referência da máquina (todos os holders juntos).
                //
                // A superfície que o eletrodo toca é o FUNDO do bolsão — a face
                // PERPENDICULAR ao Z no ponto MAIS FUNDO (Z mais negativo na montagem),
                // não o topo/abertura (paredes paralelas ao Z). Por isso DeepestZmm
                // (Z mínimo das faces), não TopZmm — antes estava invertido (Log 52).
                double baseZmm = e.DeepestZmm;                      // fundo do bolsão (Z mín.), LOCAL da cavidade
                // Aplica a rotação Z da cavidade ao CENTRO da queima (local -> montagem).
                // Z não muda numa rotação em torno de Z. A ocorrência do eletrodo também
                // é girada por occAz (PutTransform), alinhando o bloco à região de queima.
                double cosZ = Math.Cos(occAz), sinZ = Math.Sin(occAz);
                double rcx = e.CenterXmm * cosZ - e.CenterYmm * sinZ;
                double rcy = e.CenterXmm * sinZ + e.CenterYmm * cosZ;
                double asmX = occXmm + rcx;
                double asmY = occYmm + rcy;
                double asmZ = occZmm + baseZmm;                     // superfície, MONTAGEM (a origem vai aqui)

                double clearance = p.HolderBaseClearanceMm;         // folga do fundo do bloco acima do zero-máquina
                double lift = clearance - asmZ;                     // leva o fundo do bloco a Z=+clearance na montagem
                if (lift <= 0)
                {
                    Log.Warn($"Eletrodo {e.Index}: superfície de queima em Z={asmZ:0.0}mm (montagem) NÃO está abaixo do " +
                             $"zero-máquina+{clearance:0.0} — lift calculado {lift:0.0} inválido; usando {clearance:0.0}mm. " +
                             "Confira o transform da cavidade / o sinal de Z.");
                    lift = clearance;
                }
                double blockBaseAsm = asmZ + lift;                  // ~= clearance (fundo do bloco na montagem)

                string path = System.IO.Path.Combine(folder, $"{p.ElectrodeName}_D{e.Index:00}.par");
                Log.Info($"Eletrodo {e.Index}: base {blockX:0.0}×{blockY:0.0}×{blockH:0.0} mm; " +
                         $"superfície local Z={baseZmm:0.0} -> montagem Z={asmZ:0.0} (origem); " +
                         $"lift .par={lift:0.0}mm -> fundo do bloco na montagem Z={blockBaseAsm:0.0}mm; " +
                         $"XY montagem ({asmX:0.0}, {asmY:0.0}), rotZ={occAz * 180.0 / Math.PI:0.0}° -> {System.IO.Path.GetFileName(path)}");

                partDoc = app.Documents.Add("SolidEdge.PartDocument");
                // Bloco: cilindro se o blank é redondo, senão caixa. Origem na superfície;
                // levantado 'lift' até o fundo do holder ficar no zero-máquina+folga.
                if (roundBlank) BlankModeler.CreateCylinder(partDoc, blockLong, blockH, 1, 2, lift);
                else            BlankModeler.CreateBox(partDoc, blockX, blockY, blockH, 1, 2, lift);

                // Fixação: furos M6+2×Ø4 se couberem no bloco; senão EIXO no topo (Carlos).
                if (BlankModeler.FixationHolesFit(blockX, blockY, fix))
                {
                    Log.Info($"Eletrodo {e.Index}: fixação por FUROS (M6 + 2×Ø4).");
                    BlankModeler.AddFixationHoles(partDoc, blockX, blockY, blockH, lift, fix);
                }
                else
                {
                    Log.Info($"Eletrodo {e.Index}: furos não cabem no bloco {blockX:0.0}×{blockY:0.0} — fixação por EIXO no topo.");
                    BlankModeler.AddShaft(partDoc, blockX, blockY, lift + blockH, fix);
                }
                if (detectedRa.HasValue) RaVariableStore.TryWrite(partDoc, detectedRa.Value);

                partDoc.SaveAs(path);
                partDoc.Close();
                partDoc = null;

                dynamic occ = asmDoc.Occurrences.AddByFilename(path);
                // PutTransform (dump linha 6707): origem→superfície + rotação Z da cavidade,
                // alinhando o bloco à região de queima girada. Fallback p/ PutOrigin.
                try { occ.PutTransform(asmX / 1000.0, asmY / 1000.0, asmZ / 1000.0, 0.0, 0.0, occAz); }
                catch (Exception pe)
                {
                    Log.Warn($"Eletrodo {e.Index}: PutTransform falhou ({pe.GetBaseException().Message}); tentando PutOrigin (sem rotação).");
                    try { occ.PutOrigin(asmX / 1000.0, asmY / 1000.0, asmZ / 1000.0); }
                    catch (Exception pe2) { Log.Warn($"Eletrodo {e.Index}: PutOrigin também falhou: {pe2.GetBaseException().Message}"); }
                }

                Log.Info($"Eletrodo {e.Index} criado e posicionado ✓");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"Eletrodo {e.Index} falhou: {ex.GetBaseException().Message}");
                try { if (partDoc != null) partDoc.Close(); } catch { }
                return false;
            }
        }

        /// <summary>Subpasta "Eletrodos" ao lado da montagem (escolha do Carlos); fallback local.</summary>
        private static string ResolveElectrodeFolder(dynamic asmDoc, ElectrodeParams p)
        {
            if (!string.IsNullOrWhiteSpace(p.OutputFolder)) return p.OutputFolder;
            try
            {
                string full = (string)asmDoc.FullName;
                string dir = System.IO.Path.GetDirectoryName(full);
                if (!string.IsNullOrWhiteSpace(dir)) return System.IO.Path.Combine(dir, "Eletrodos");
            }
            catch { }
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoEDM", "electrodes");
        }

        private static string SafeDocName(dynamic doc)
        {
            try { return (string)doc.Name; } catch { return "<montagem>"; }
        }

        private RegionPlan BuildRegionPlan(double ra, System.Drawing.Color color,
            IReadOnlyList<SelectedFace> faces, ElectrodeParams p, int detailIndex)
        {
            var region = new RegionPlan
            {
                DetailIndex = detailIndex,
                Ra = ra,
                Color = color,
                FaceCount = faces.Count
            };

            // Bounding box do detalhe -> blank.
            if (TryGetBurnBoundingBox(faces, out BoundingBox box))
            {
                region.BoundingBoxKnown = true;
                region.BurnBox = box;
                region.SelectedBlank = _blankLibrary.SelectBlank(box, p.BlankMargin, p.Material);
                if (region.SelectedBlank == null)
                    region.Warnings.Add(
                        $"Pegada {box.SizeX:F1} x {box.SizeY:F1} mm excede a seção das barras em estoque " +
                        $"(material {p.Material}), mesmo girada — comprar material. " +
                        "(O comprimento da barra é livre até 500 mm = altura; a seção não comporta a pegada.)");
            }
            else
            {
                region.Warnings.Add("Bounding box das faces não lido (API Range a validar).");
            }

            // Passes: desbaste (Ra acima) + acabamento (Ra da cor).
            double roughRa = _raColorMap.RoughingRaFor(ra);
            if (roughRa > ra)
                region.Passes.Add(MakePass("DESB", roughRa, p, detailIndex));
            region.Passes.Add(MakePass("ACAB", ra, p, detailIndex));

            return region;
        }

        private PassPlan MakePass(string suffix, double ra, ElectrodeParams p, int detailIndex)
        {
            var pass = new ElectrodePass(suffix, ra);
            double offset = _offsetPolicy.GetInwardOffsetMm(pass, p.Material);
            string raTag = ra.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
            return new PassPlan
            {
                Pass = pass,
                InwardOffsetMm = offset,
                ElectrodeFileName = $"{p.ElectrodeName}_D{detailIndex:00}_Ra{raTag}_{suffix}.par"
            };
        }

        // ------------------------------------------------------------------
        //  Localiza a ocorrência com faces nas cores de queima
        // ------------------------------------------------------------------

        private Tuple<OccurrenceInfo, IReadOnlyList<FaceGroup>, IReadOnlyList<Selection.ColorTally>> FindBurnOccurrence(
            AssemblyContext ctx, dynamic app)
        {
            OccurrenceInfo best = null;
            IReadOnlyList<FaceGroup> bestGroups = new List<FaceGroup>();
            IReadOnlyList<Selection.ColorTally> bestTally = new List<Selection.ColorTally>();
            int bestFaceCount = 0;

            foreach (var occ in ctx.GetOccurrences())
            {
                if (occ.OccurrenceDocument == null) continue;

                IReadOnlyList<FaceGroup> groups;
                IReadOnlyList<Selection.ColorTally> tally;
                try { groups = _faceSelector.SelectByRaColorMap(occ.OccurrenceDocument, app, _raColorMap, out tally); }
                catch (Exception ex)
                {
                    Log.Warn($"Falha ao analisar '{occ.Name}': {ex.Message}");
                    continue;
                }

                // "Melhor" = a que tem MAIS faces de queima MAPEADAS (mantém a mira original);
                // guarda também o histograma DELA (com as cores não mapeadas, ex. roxo) p/ o
                // aviso de "cor dominante não mapeada" na confirmação.
                int faceCount = groups.Sum(gr => gr.Faces.Count);
                if (faceCount > bestFaceCount)
                {
                    bestFaceCount = faceCount;
                    best = occ;
                    bestGroups = groups;
                    bestTally = tally;
                }
            }

            if (best == null)
                try { DiagnoseNoBurn(ctx); }
                catch (Exception e) { Log.Warn("[DIAG] no-burn falhou: " + e.GetBaseException().Message); }

            return Tuple.Create(best, bestGroups, bestTally);
        }

        /// <summary>
        /// Diagnóstico do caso "nenhuma queima encontrada": dumpa a estrutura da
        /// montagem (ocorrências, corpos, faces) e, na 1ª face da 1ª peça, a cor por
        /// VÁRIAS fontes — face.Style (peça), Face.GetRGBAVals e Occurrence.GetFaceStyle2
        /// (estilo no nível da ocorrência, comum quando a cor é pintada no contexto da
        /// montagem, não na peça). Revela como a montagem codifica a cor de queima.
        /// </summary>
        private void DiagnoseNoBurn(AssemblyContext ctx)
        {
            Log.Warn("Nenhuma face de queima detectada — DIAGNÓSTICO da montagem:");
            int occN = 0;
            bool faceDumped = false;
            foreach (var occ in ctx.GetOccurrences())
            {
                occN++;
                dynamic doc = null; string docType = "?";
                try { doc = occ.OccurrenceDocument; docType = Convert.ToString((int)doc.Type); } catch { }

                int bodies = 0, faces = 0;
                dynamic firstBody = null;
                try
                {
                    dynamic models = doc.Models;
                    int mc = (int)models.Count;
                    for (int i = 1; i <= mc; i++)
                    {
                        try
                        {
                            dynamic body = models.Item(i).Body;
                            if (body == null) continue;
                            bodies++;
                            if (firstBody == null) firstBody = body;
                            faces += (int)body.Faces[1].Count;
                        }
                        catch { }
                    }
                }
                catch { }

                Log.Info($"  Occ '{occ.Name}': doc Type={docType} (1=peça,4=submontagem), {bodies} corpo(s), {faces} face(s).");

                if (!faceDumped && faces > 0 && firstBody != null)
                {
                    faceDumped = true;
                    object firstFace = null;
                    try { firstFace = (object)firstBody.Faces[1].Item(1); } catch { }
                    if (firstFace != null)
                    {
                        try { DumpFaceColorSources(firstFace, occ.ComOccurrence); }
                        catch (Exception e) { Log.Warn("  [DIAG] cor da 1ª face falhou: " + e.GetBaseException().Message); }
                        try { DumpFeatureInfo(doc, firstFace); }
                        catch (Exception e) { Log.Warn("  [DIAG] features falhou: " + e.GetBaseException().Message); }
                    }
                }
            }
            Log.Info($"  Total: {occN} ocorrência(s). Se a cor de queima EXISTE mas não foi lida, " +
                     "veja as fontes acima — ajusto o leitor p/ a fonte certa.");
        }

        private static void DumpFaceColorSources(object face, dynamic comOcc)
        {
            Log.Info("  [DIAG] Cor da 1ª face por FONTE:");

            object style = null;
            try { style = ((dynamic)face).Style; } catch { }
            Log.Info("    face.Style (peça) = " + (style == null ? "null (sem estilo por-face na peça)" : "PRESENTE"));

            // Face.GetRGBAVals — fonte alternativa direta na face.
            try { Com.ComDiagnostics.LogSignatures(face, "GetRGBAVals"); } catch { }
            try
            {
                object[] a = { 0.0, 0.0, 0.0, 0.0 };
                var mod = new System.Reflection.ParameterModifier(4);
                mod[0] = mod[1] = mod[2] = mod[3] = true; // [out] by-ref, senão volta 0
                face.GetType().InvokeMember("GetRGBAVals", System.Reflection.BindingFlags.InvokeMethod,
                    null, face, a, new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                Log.Info($"    Face.GetRGBAVals -> R={a[0]} G={a[1]} B={a[2]} A={a[3]} (×255 ≈ {(int)(Convert.ToDouble(a[0])*255)},{(int)(Convert.ToDouble(a[1])*255)},{(int)(Convert.ToDouble(a[2])*255)})");
            }
            catch (Exception e) { Log.Info("    Face.GetRGBAVals falhou: " + e.GetBaseException().Message); }

            // Occurrence.GetFaceStyle2 — estilo no nível da ocorrência (cor pintada no contexto).
            if (comOcc != null)
            {
                try { Com.ComDiagnostics.LogSignatures((object)comOcc, "GetFaceStyle2"); } catch { }
                try
                {
                    dynamic st = comOcc.GetFaceStyle2(face);
                    Log.Info("    Occurrence.GetFaceStyle2(face) = " + (st == null ? "null" : "PRESENTE (cor no nível da ocorrência!)"));
                }
                catch (Exception e) { Log.Info("    Occurrence.GetFaceStyle2 falhou: " + e.GetBaseException().Message); }
            }
        }

        /// <summary>
        /// Introspecção da cor pintada por FEATURE: GetRGBAVals dá a cor do CORPO, não a
        /// pintura de feature (camada de exibição). Aqui dumpamos a estrutura para achar
        /// como ler a cor da feature: FeatureIDsAndNames da face, os membros de Model[1]
        /// (achar a coleção de features) e os membros da 1ª feature (achar Style/cor).
        /// </summary>
        private static void DumpFeatureInfo(dynamic partDoc, object firstFace)
        {
            Log.Info("  [DIAG] FEATURES (cor pintada por feature — GetRGBAVals dá a cor do corpo, não a da feature):");

            // Como a face aponta p/ suas features.
            try { Com.ComDiagnostics.LogSignatures(firstFace, "FeatureIDsAndNames"); } catch { }
            try
            {
                object[] a = { null, null };
                var mod = new System.Reflection.ParameterModifier(2); mod[0] = mod[1] = true;
                firstFace.GetType().InvokeMember("FeatureIDsAndNames", System.Reflection.BindingFlags.InvokeMethod,
                    null, firstFace, a, new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                Log.Info($"    Face.FeatureIDsAndNames -> [0]={Fmt(a[0])} [1]={Fmt(a[1])}");
            }
            catch (Exception e) { Log.Info("    Face.FeatureIDsAndNames falhou: " + e.GetBaseException().Message); }

            dynamic model = null;
            try { model = partDoc.Models.Item(1); } catch { }
            if (model == null) { Log.Info("    partDoc.Models.Item(1) indisponível."); return; }

            // Dump dos membros do Model p/ achar a coleção de features e cor.
            try { Com.ComDiagnostics.LogMembers("Model[1]", (object)model); } catch { }

            // Tenta a coleção Features e o Style/cor da 1ª feature.
            try
            {
                dynamic feats = model.Features;
                int fc = (int)feats.Count;
                Log.Info($"    Model[1].Features = {fc} feature(s).");
                if (fc > 0) Com.ComDiagnostics.LogMembers("Feature[1]", (object)feats.Item(1));
            }
            catch (Exception e) { Log.Info("    Model[1].Features indisponível: " + e.GetBaseException().Message); }
        }

        private static string Fmt(object o)
        {
            if (o == null) return "null";
            if (o is Array arr)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var x in arr) parts.Add(Convert.ToString(x));
                return "[" + string.Join(",", parts) + "]";
            }
            return Convert.ToString(o);
        }

        // ------------------------------------------------------------------
        //  Bounding box das faces (best-effort late-bound)
        // ------------------------------------------------------------------

        private static bool TryGetBurnBoundingBox(IReadOnlyList<SelectedFace> faces, out BoundingBox box)
        {
            box = new BoundingBox
            {
                MinX = double.MaxValue, MinY = double.MaxValue, MinZ = double.MaxValue,
                MaxX = double.MinValue, MaxY = double.MinValue, MaxZ = double.MinValue
            };

            bool any = false;
            foreach (var f in faces)
            {
                if (FaceGeometry.TryGetRangeMm(f.ComFace, out double[] min, out double[] max))
                {
                    any = true;
                    box.MinX = Math.Min(box.MinX, min[0]); box.MaxX = Math.Max(box.MaxX, max[0]);
                    box.MinY = Math.Min(box.MinY, min[1]); box.MaxY = Math.Max(box.MaxY, max[1]);
                    box.MinZ = Math.Min(box.MinZ, min[2]); box.MaxZ = Math.Max(box.MaxZ, max[2]);
                }
            }
            return any;
        }

        // ------------------------------------------------------------------
        //  Estágios de geometria — implementados de forma defensiva, com logs
        //  detalhados para validação incremental no SE 2023/2026.
        // ------------------------------------------------------------------

        /// <summary>
        /// Cria uma nova peça EM CONTEXTO (in-place) via <c>Occurrences.AddByTemplate</c>
        /// e copia as faces de queima da cavidade via Inter-Part Copy associativo.
        /// Retorna o documento do eletrodo.
        ///
        /// Por que AddByTemplate (e não AddByFilename): o Inter-Part Copy só funciona
        /// com a peça criada DENTRO da montagem (in-context). AddByFilename apenas
        /// insere um arquivo JÁ existente -> peça standalone -> CopySurfaces.Add
        /// retornava E_FAIL (Log 18). AddByTemplate cria o part já em contexto, então
        /// a cópia entre peças passa a ser válida.
        /// </summary>
        public dynamic CreateInContextPart(dynamic assemblyDocument, OccurrenceInfo target,
            IReadOnlyList<SelectedFace> faces, ElectrodeParams p)
        {
            if (assemblyDocument == null) throw new ArgumentNullException(nameof(assemblyDocument));
            if (target?.ComOccurrence == null) throw new ArgumentException("Ocorrência de destino inválida.", nameof(target));
            if (_connector.Application == null) throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");

            dynamic app = _connector.Application;
            // Habilita as opções globais de Inter-Part do SE. Sem isso, mesmo com a peça
            // in-place ativada, CopySurfaces.Add retorna E_FAIL (Logs 21/22).
            EnableInterPartCopy(app);

            // A montagem (.asm) é quem tem Occurrences — NÃO target.OccurrenceDocument
            // (esse é a PEÇA da cavidade, que não tem .Occurrences).
            dynamic asmDoc = assemblyDocument;
            dynamic occurrences = asmDoc.Occurrences;

            // Assinatura real (Log 21): AddByTemplate(OccurrenceFileName, [opt]TemplateFileName).
            // arg1 = CAMINHO do NOVO part a criar; arg2 = o template. Passar só o template
            // (como arg1) fez o SE inserir o próprio arquivo de template como ocorrência
            // standalone (read-only, no Program Files) -> CopySurfaces.Add dava E_FAIL.
            string template = ResolvePartTemplate(app, p);
            string newPartPath = System.IO.Path.Combine(
                p.OutputFolder ?? System.IO.Path.GetTempPath(),
                $"{p.ElectrodeName}_{Guid.NewGuid():N}.par");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(newPartPath));
            Log.Info($"Novo eletrodo (arg1): {newPartPath}");
            Log.Info($"Template (arg2): {template}");

            // Introspecção-primeiro: grava a assinatura real de AddByTemplate no log.
            Com.ComDiagnostics.LogSignatures((object)occurrences, "AddByTemplate", "AddByFilename");

            Log.Info("Criando eletrodo EM CONTEXTO via Occurrences.AddByTemplate(novoPart, template)...");
            dynamic occurrence = occurrences.AddByTemplate(newPartPath, template);
            Log.Info($"Ocorrência de eletrodo criada in-place: {SafeName(occurrence)}");

            // Posiciona sobre a região (best-effort; a cópia associativa traz a
            // geometria na posição real de qualquer forma).
            try
            {
                var ctx = new AssemblyContext(asmDoc);
                if (ctx.TryGetOrigin(target, out double ox, out double oy, out double oz))
                    occurrence.PutOrigin(ox, oy, oz); // já em METROS (GetTransform devolve metros); PutOrigin espera metros
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível posicionar o eletrodo: {ex.Message}");
            }

            // Inter-Part Copy dentro de edição in-place. Sem SaveAs antecipado: salvar
            // um part in-place cedo pode quebrar o vínculo in-context.
            dynamic electrodeDoc;
            using (var scope = new EditInPlaceScope(occurrence))
            {
                electrodeDoc = scope.ActiveDocument ?? SafeDoc(occurrence);
                var copier = new InterPartCopier();
                copier.CopyBurnFaces(asmDoc, target, faces, electrodeDoc);
            }

            return electrodeDoc;
        }

        /// <summary>
        /// Resolve o template de peça (.par) para o AddByTemplate: usa
        /// <see cref="ElectrodeParams.TemplatePath"/> se existir; senão pede ao SE o
        /// template padrão de Part (<c>Application.GetDefaultTemplatePath</c>,
        /// igPartDocument=1). Lança se não achar — AddByTemplate exige um path válido.
        /// </summary>
        private static string ResolvePartTemplate(dynamic app, ElectrodeParams p)
        {
            if (!string.IsNullOrWhiteSpace(p.TemplatePath) && System.IO.File.Exists(p.TemplatePath))
                return p.TemplatePath;

            try
            {
                string def = app.GetDefaultTemplatePath(1); // 1 = igPartDocument
                if (!string.IsNullOrWhiteSpace(def) && System.IO.File.Exists(def))
                    return def;
                if (!string.IsNullOrWhiteSpace(def))
                    Log.Warn($"Template padrão do SE não existe no disco: {def}");
            }
            catch (Exception ex)
            {
                Log.Warn($"GetDefaultTemplatePath falhou: {ex.Message}");
            }

            throw new InvalidOperationException(
                "Não foi possível resolver um template de peça (.par) para o AddByTemplate. " +
                "Defina ElectrodeParams.TemplatePath com um .par de template válido.");
        }

        /// <summary>
        /// Habilita as opções globais de Inter-Part do SE (necessárias para o
        /// CopySurfaces.Add entre peças). Doc da Siemens: o Inter-Part Copy só funciona
        /// com a peça in-place ativada E com a opção "Inter-Part Copy" ligada nas opções.
        /// Constantes confirmadas no dump: seApplicationGlobalAllowInterPart=253,
        /// seApplicationGlobalInterPartCopyCommand=254. Via InvokeMember (o IDispatch
        /// coage o int p/ ApplicationGlobalConstants e o bool p/ VARIANT_BOOL).
        /// </summary>
        private static void EnableInterPartCopy(dynamic app)
        {
            const int AllowInterPart = 253;
            const int InterPartCopyCommand = 254;
            try
            {
                object target = app;
                Log.Info($"Inter-Part ANTES: Allow(253)={ReadGlobal(target, AllowInterPart)}, " +
                         $"CopyCmd(254)={ReadGlobal(target, InterPartCopyCommand)}");

                target.GetType().InvokeMember("SetGlobalParameter",
                    BindingFlags.InvokeMethod, null, target, new object[] { AllowInterPart, true });
                target.GetType().InvokeMember("SetGlobalParameter",
                    BindingFlags.InvokeMethod, null, target, new object[] { InterPartCopyCommand, true });

                Log.Info($"Inter-Part DEPOIS do set: Allow(253)={ReadGlobal(target, AllowInterPart)}, " +
                         $"CopyCmd(254)={ReadGlobal(target, InterPartCopyCommand)}");
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível habilitar Inter-Part Copy: {ex.GetBaseException().Message}");
            }
        }

        /// <summary>
        /// Lê um parâmetro global do SE. GetGlobalParameter tem Value como [in,out]
        /// VARIANT*: sem ParameterModifier marcando by-ref o valor NÃO popula em late
        /// binding (voltaria o valor-semente). Devolve "?" se não conseguir ler.
        /// </summary>
        private static string ReadGlobal(object app, int param)
        {
            try
            {
                object[] args = { param, false };
                var mod = new ParameterModifier(2);
                mod[1] = true; // Value é [in,out]
                app.GetType().InvokeMember("GetGlobalParameter",
                    BindingFlags.InvokeMethod, null, app, args,
                    new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                return Convert.ToString(args[1]);
            }
            catch (Exception ex) { return "? (" + ex.GetBaseException().Message + ")"; }
        }

        private static string SafeName(dynamic occ)
        {
            try { return (string)occ.Name; } catch { return "<sem nome>"; }
        }

        private static dynamic SafeDoc(dynamic occ)
        {
            try { return occ.OccurrenceDocument; } catch { return null; }
        }

        /// <summary>Sinaliza arestas/faces abaixo do raio mínimo usinável.</summary>
        public IReadOnlyList<string> CheckMinimumRadii(dynamic electrodePart, double minRadiusMm)
        {
            Log.Warn("CheckMinimumRadii: análise de raios mínimos ainda não implementada.");
            return Array.Empty<string>();
        }

        /// <summary>
        /// Aplica o offset interno das faces de queima de um passe. Usa FaceOffsets
        /// como primeira tentativa; em caso de falha, tenta OffsetSurfaces + Stitch.
        /// </summary>
        public void ApplyOffset(dynamic electrodePart, double inwardOffsetMm)
        {
            if (electrodePart == null) throw new ArgumentNullException(nameof(electrodePart));

            dynamic constructions = electrodePart.Constructions;
            dynamic copySurfaces = constructions.CopySurfaces;
            if (copySurfaces.Count == 0)
            {
                Log.Warn("ApplyOffset: nenhuma CopySurface encontrada no eletrodo.");
                return;
            }

            dynamic copyFeature = copySurfaces.Item(1);
            object[] faces = ModelingHelpers.GetFeatureFaces(copyFeature);
            if (faces.Length == 0)
            {
                Log.Warn("ApplyOffset: não foi possível obter faces da CopySurface.");
                return;
            }

            double offsetM = -inwardOffsetMm / 1000.0; // negativo = para dentro
            Log.Info($"Aplicando offset {inwardOffsetMm:F3} mm para dentro em {faces.Length} face(s).");

            try
            {
                dynamic faceOffset = ModelingHelpers.AddFaceOffset(constructions, faces, offsetM);
                Log.Info("Offset aplicado via FaceOffsets.Add.");
                return;
            }
            catch (Exception ex)
            {
                Log.Warn($"FaceOffsets.Add falhou: {ex.Message}. Tentando OffsetSurfaces + Stitch.");
            }

            try
            {
                dynamic offsetSurface = ModelingHelpers.AddOffsetSurface(constructions, faces, offsetM);
                ModelingHelpers.StitchSurfaces(constructions, new[] { (object)offsetSurface });
                Log.Info("Offset aplicado via OffsetSurfaces + Stitch.");
            }
            catch (Exception ex)
            {
                Log.Error($"Offset também falhou via OffsetSurfaces: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cria blank + holder abaixo das faces de queima. O blank é um bloco
        /// centrado na pegada da queima; o holder é um bloco maior/mais baixo.
        /// </summary>
        public void CreateBlankAndHolder(dynamic electrodePart, BlankSpec blank, ElectrodeParams p)
        {
            if (electrodePart == null) throw new ArgumentNullException(nameof(electrodePart));
            if (blank == null) throw new ArgumentNullException(nameof(blank));

            dynamic models = electrodePart.Models;
            dynamic plane = ModelingHelpers.GetBaseRefPlane(electrodePart);
            if (plane == null)
            {
                Log.Warn("CreateBlankAndHolder: plano de referência base não encontrado.");
                return;
            }

            // Dimensões do blank em metros.
            double w = blank.DimA / 1000.0;
            double h = (blank.DimB ?? blank.DimA) / 1000.0;
            double blankHeight = p.HolderHeight / 1000.0;

            // Centro no plano XY; Z=0 é a base do blank, subindo para +Z.
            double x0 = -w / 2, y0 = -h / 2, z0 = 0.0;
            double x1 = w / 2, y1 = h / 2, z1 = 0.0;

            Log.Info($"Criando blank {blank.Name} com altura {p.HolderHeight} mm.");
            ModelingHelpers.AddBoxByTwoPoints(models,
                x0, y0, z0, x1, y1, z1,
                blankHeight, plane,
                extentSide: 1); // igLeft

            // Holder: bloco maior, abaixo do blank.
            double holderW = w * 1.2;
            double holderH = h * 1.2;
            double holderHeight = 0.020; // 20 mm fixo; substituir por parâmetro futuramente.
            double hx0 = -holderW / 2, hy0 = -holderH / 2, hz0 = -holderHeight;
            double hx1 = holderW / 2, hy1 = holderH / 2, hz1 = -holderHeight;

            Log.Info("Criando holder abaixo do blank.");
            ModelingHelpers.AddBoxByTwoPoints(models,
                hx0, hy0, hz0, hx1, hy1, hz1,
                holderHeight, plane,
                extentSide: 1);
        }

        /// <summary>
        /// Re-pinta as faces de queima com a cor original e salva o eletrodo como
        /// .par nativo.
        /// </summary>
        public void RecolorAndSave(dynamic electrodePart, string parPath, ElectrodeParams p)
        {
            if (electrodePart == null) throw new ArgumentNullException(nameof(electrodePart));

            try
            {
                dynamic copySurfaces = electrodePart.Constructions.CopySurfaces;
                if (copySurfaces.Count > 0)
                {
                    dynamic copyFeature = copySurfaces.Item(1);
                    var faces = ModelingHelpers.GetFeatureFaces(copyFeature);
                    ModelingHelpers.SetFacesColor(faces, p.BurnColor.R, p.BurnColor.G, p.BurnColor.B);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"RecolorAndSave: não foi possível repintar faces: {ex.Message}");
            }

            if (!string.IsNullOrWhiteSpace(parPath))
            {
                Log.Info($"Salvando eletrodo: {parPath}");
                electrodePart.SaveAs(parPath);
            }
        }

        // ------------------------------------------------------------------
        //  Ferramenta: duplicar eletrodo p/ o PRÓXIMO Ra da tabela (desbaste)
        // ------------------------------------------------------------------

        /// <summary>
        /// Botão "Duplicar eletrodo" (Carlos, 2026-07-21): parte de UM eletrodo já com GAP
        /// aplicado (Ra conhecido — variável <see cref="RaVariableStore"/> ou feature "GAP:
        /// ... - Ra: ...") e cria uma cópia com o GAP no PRÓXIMO Ra da escada (mais grosso =
        /// desbaste, <see cref="RaColorMap.RoughingRaFor"/>), posicionada em TODAS as posições
        /// onde esse eletrodo aparece na montagem (a mesma peça pode se repetir p/ cavidades
        /// simétricas) — 1 clique gera o par desbaste/acabamento em cada posição repetida.
        ///
        /// NUNCA edita a peça original nem a ocorrência selecionada: copia o ARQUIVO no disco
        /// (não o Documento já aberto/referenciado pela montagem — um SaveAs no doc já
        /// referenciado redirecionaria as ocorrências existentes para o arquivo novo), abre a
        /// cópia como documento separado, ajusta GAP/cor/nome/variável NELA, salva e fecha — só
        /// então adiciona as ocorrências novas na montagem. ESCREVE um arquivo novo + a
        /// montagem (novas ocorrências); não salva a montagem.
        /// </summary>
        public DuplicateElectrodeResult DuplicateElectrodeToNextGap(dynamic asmDoc, ElectrodeParams p)
        {
            var result = new DuplicateElectrodeResult();
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);

            OccurrenceInfo selected = CollectSelectedOccurrences(asmDoc, out int skipped).FirstOrDefault();
            if (selected == null)
            {
                result.Message = "Nenhuma ocorrência de eletrodo selecionada. Na montagem, selecione a ocorrência do eletrodo " +
                    "(a peça inteira, não uma face) já com GAP aplicado e tente de novo.";
                Log.Warn("Duplicar eletrodo: " + result.Message);
                return result;
            }
            if (skipped > 0)
                Log.Warn($"Duplicar eletrodo: {skipped} item(ns) da seleção ignorado(s) (não são ocorrências).");

            dynamic sourceDoc = selected.OccurrenceDocument;
            string sourcePath;
            try { sourcePath = (string)sourceDoc.FullName; }
            catch (Exception ex)
            {
                result.Message = "Não consegui ler o caminho do arquivo da peça selecionada.";
                Log.Warn("Duplicar eletrodo: " + result.Message + " " + ex.GetBaseException().Message);
                return result;
            }

            if (!TryReadElectrodeRa(sourceDoc, out double currentRa, out string raSrc))
            {
                result.Message = $"Não achei o Ra de '{selected.Name}' (nem variável, nem feature de GAP nomeada) — " +
                                  "aplique o GAP primeiro (botão 'Aplicar GAP').";
                Log.Warn("Duplicar eletrodo: " + result.Message);
                return result;
            }
            Log.Info($"Duplicar eletrodo: '{selected.Name}' Ra atual = {currentRa:0.0} ({raSrc}).");

            RaGapPresets.Choice next = RaGapPresets.NextCoarser(currentRa, p.Material);
            if (next == null)
            {
                result.Message = $"'{selected.Name}' já está no Ra mais grosso da tabela ({currentRa:0.0}) — não há passe de desbaste seguinte.";
                Log.Warn("Duplicar eletrodo: " + result.Message);
                return result;
            }
            Log.Info($"Duplicar eletrodo: próximo passe = {next.Label}.");

            // Todas as posições onde ESSE MESMO arquivo aparece na montagem (não só a ocorrência
            // selecionada) — cavidades repetidas (moldes multi-cavidade) usam o MESMO .par em
            // várias posições. TryGetPlacement já devolve METROS/RADIANOS (mesma unidade do
            // PutTransform/PutOrigin) — sem conversão aqui.
            var placements = new List<(double x, double y, double z, double az)>();
            foreach (var occ in ctx.GetOccurrences())
            {
                if (!SameDocument(occ, sourcePath)) continue;
                if (!ctx.TryGetPlacement(occ, out double x, out double y, out double z, out double ax, out double ay, out double az))
                {
                    Log.Warn($"Duplicar eletrodo: transform de '{occ.Name}' ilegível — pulando essa posição.");
                    continue;
                }
                placements.Add((x, y, z, az));
            }
            if (placements.Count == 0)
            {
                result.Message = "Não achei nenhuma posição válida (transform ilegível) para replicar.";
                Log.Warn("Duplicar eletrodo: " + result.Message);
                return result;
            }
            Log.Info($"Duplicar eletrodo: {placements.Count} posição(ões) da mesma peça na montagem.");

            string newPath = NextAvailablePath(sourcePath, next.Ra);
            dynamic newDoc = null;
            try
            {
                System.IO.File.Copy(sourcePath, newPath);
                Log.Info($"Duplicar eletrodo: cópia do arquivo -> {System.IO.Path.GetFileName(newPath)}");

                newDoc = app.Documents.Open(newPath);
                if (!AdjustGapOnDuplicate(newDoc, next))
                {
                    result.Message = "Copiei a peça, mas não consegui ajustar o GAP nela (veja o log). " +
                                      "Arquivo: " + newPath + " — apague ou ajuste manualmente.";
                    Log.Warn("Duplicar eletrodo: " + result.Message);
                    try { newDoc.Close(false); } catch { }
                    return result;
                }
                RaVariableStore.TryWrite(newDoc, next.Ra);
                newDoc.Save();
                newDoc.Close();
                newDoc = null;
            }
            catch (Exception ex)
            {
                Log.Warn("Duplicar eletrodo: falha ao processar a cópia — " + ex.GetBaseException().Message);
                try { if (newDoc != null) newDoc.Close(false); } catch { }
                result.Message = "Falha ao processar a cópia da peça — veja o log. Arquivo (pode ter ficado incompleto): " + newPath;
                return result;
            }

            int placed = 0;
            foreach (var t in placements)
            {
                try
                {
                    dynamic occ = asmDoc.Occurrences.AddByFilename(newPath);
                    try { occ.PutTransform(t.x, t.y, t.z, 0.0, 0.0, t.az); }
                    catch (Exception pe)
                    {
                        Log.Warn($"Duplicar eletrodo: PutTransform falhou ({pe.GetBaseException().Message}); tentando PutOrigin.");
                        try { occ.PutOrigin(t.x, t.y, t.z); }
                        catch (Exception pe2) { Log.Warn("Duplicar eletrodo: PutOrigin também falhou: " + pe2.GetBaseException().Message); }
                    }
                    placed++;
                }
                catch (Exception ex) { Log.Warn("Duplicar eletrodo: falha ao adicionar ocorrência — " + ex.GetBaseException().Message); }
            }

            result.Created = placed > 0;
            result.NewPath = newPath;
            result.InstanceCount = placed;
            result.Message = result.Created
                ? $"{placed}/{placements.Count} ocorrência(s) de '{System.IO.Path.GetFileName(newPath)}' criada(s) ({next.Label}), nas mesmas posições de '{selected.Name}'."
                : "Copiei e ajustei a peça, mas não consegui posicionar nenhuma ocorrência na montagem — veja o log.";
            return result;
        }

        /// <summary>Ocorrências (objetos COM crus, envolvidos em <see cref="OccurrenceInfo"/>) da
        /// SelectSet atual — tolerante a itens que não são ocorrências (ex.: uma face
        /// selecionada por engano). NUNCA falha silenciosamente, mesmo padrão de
        /// <see cref="CollectSelectedFaces"/>.</summary>
        private static List<OccurrenceInfo> CollectSelectedOccurrences(dynamic doc, out int skipped)
        {
            var result = new List<OccurrenceInfo>();
            skipped = 0;
            dynamic ss;
            try { ss = doc.SelectSet; }
            catch (Exception ex) { Log.Warn($"Duplicar eletrodo: doc.SelectSet inacessível: {ex.GetBaseException().Message}"); return result; }

            int n = 0;
            try { n = (int)ss.Count; }
            catch (Exception ex) { Log.Warn($"Duplicar eletrodo: SelectSet.Count falhou: {ex.GetBaseException().Message}"); return result; }
            Log.Info($"Duplicar eletrodo: SelectSet.Count={n}.");

            for (int i = 1; i <= n; i++)
            {
                object item;
                try { item = ss.Item(i); }
                catch (Exception ex) { Log.Warn($"Duplicar eletrodo: SelectSet.Item({i}) falhou: {ex.GetBaseException().Message}"); continue; }
                if (item == null) { skipped++; continue; }

                dynamic occDoc = null; string name = null;
                try { occDoc = ((dynamic)item).OccurrenceDocument; name = (string)((dynamic)item).Name; } catch { }
                if (occDoc == null)
                {
                    skipped++;
                    ComDiagnostics.DumpObject($"Duplicar eletrodo: SelectSet[{i}] não é ocorrência", item, 1);
                    continue;
                }
                result.Add(new OccurrenceInfo(item, name ?? "<sem nome>", occDoc));
            }
            return result;
        }

        private static bool SameDocument(OccurrenceInfo occ, string sourcePath)
        {
            if (occ.OccurrenceDocument == null) return false;
            try { return string.Equals((string)occ.OccurrenceDocument.FullName, sourcePath, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>Ra atual do eletrodo: (1) variável <see cref="RaVariableStore"/>; (2) fallback —
        /// parseia o nome da feature de GAP ("GAP: 0,10 - Ra: 1,6", formato de
        /// <c>SurfaceBlockBuilder.TryNameGapFeature</c>) para peças criadas antes dessa variável existir.</summary>
        private static bool TryReadElectrodeRa(dynamic partDoc, out double ra, out string source)
        {
            if (RaVariableStore.TryRead(partDoc, out ra)) { source = "variável"; return true; }

            try
            {
                dynamic faceOffsets = partDoc.Models.Item(1).FaceOffsets;
                int n = (int)faceOffsets.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic feat; try { feat = faceOffsets.Item(i); } catch { continue; }
                    string name; try { name = (string)feat.Name; } catch { continue; }
                    if (TryParseRaFromFeatureName(name, out ra)) { source = $"feature '{name}'"; return true; }
                }
            }
            catch { /* sem Models/FaceOffsets — cai no "não achei" abaixo */ }

            ra = 0; source = null;
            return false;
        }

        private static readonly Regex RaNamePattern = new Regex(@"Ra:\s*([0-9]+(?:[.,][0-9]+)?)", RegexOptions.IgnoreCase);

        private static bool TryParseRaFromFeatureName(string name, out double ra)
        {
            ra = 0;
            if (string.IsNullOrEmpty(name)) return false;
            var m = RaNamePattern.Match(name);
            if (!m.Success) return false;
            return double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out ra);
        }

        /// <summary>Caminho novo, evitando sobrescrever um arquivo já duplicado antes.</summary>
        private static string NextAvailablePath(string sourcePath, double ra)
        {
            string dir = System.IO.Path.GetDirectoryName(sourcePath);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string ext = System.IO.Path.GetExtension(sourcePath);
            string raTag = ra.ToString("0.0", CultureInfo.InvariantCulture);
            string candidate = System.IO.Path.Combine(dir, $"{baseName}_Ra{raTag}{ext}");
            int n = 1;
            while (System.IO.File.Exists(candidate))
                candidate = System.IO.Path.Combine(dir, $"{baseName}_Ra{raTag}_{++n}{ext}");
            return candidate;
        }

        /// <summary>
        /// Na peça JÁ COPIADA (arquivo separado, nunca a original): acha a feature de GAP
        /// (<c>Model.FaceOffsets</c>), muda <c>FaceOffset.Distance</c> p/ o novo offset
        /// (propriedade get/put double, CONFIRMADA no dump da typelib SE 2023 — não precisa de
        /// InvokeMember), renomeia e repinta as faces dela (<see cref="ModelingHelpers.GetFeatureFaces"/>
        /// já usado por <see cref="RecolorAndSave"/> nesta mesma classe). NUNCA lança.
        /// </summary>
        private static bool AdjustGapOnDuplicate(dynamic partDoc, RaGapPresets.Choice next)
        {
            dynamic feature = FindGapOffsetFeature(partDoc, out string foundBy);
            if (feature == null)
            {
                Log.Warn("Duplicar eletrodo: não achei a feature de GAP (Model.FaceOffsets) na cópia.");
                return false;
            }
            Log.Info($"Duplicar eletrodo: feature de GAP achada ({foundBy}).");

            try
            {
                feature.Distance = -Math.Abs(next.GapMm) / 1000.0; // metros, negativo = encolhe
                Log.Info($"Duplicar eletrodo: Distance ajustada para {next.GapMm:0.00}mm ({next.Label}).");
            }
            catch (Exception ex)
            {
                Log.Warn("Duplicar eletrodo: feature.Distance = valor falhou — " + ex.GetBaseException().Message);
                return false;
            }

            try { feature.Name = $"GAP: {next.GapMm:0.00} - Ra: {next.Ra:0.0}"; }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: renomear a feature falhou (cosmético, segue) — " + ex.GetBaseException().Message); }

            try
            {
                object[] faces = ModelingHelpers.GetFeatureFaces(feature);
                ModelingHelpers.SetFacesColor(faces, next.Color.R, next.Color.G, next.Color.B);
            }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: repintar as faces falhou (cosmético, segue) — " + ex.GetBaseException().Message); }

            return true;
        }

        private static dynamic FindGapOffsetFeature(dynamic partDoc, out string foundBy)
        {
            foundBy = null;
            dynamic faceOffsets;
            try { faceOffsets = partDoc.Models.Item(1).FaceOffsets; }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: Model.FaceOffsets inacessível — " + ex.GetBaseException().Message); return null; }

            int n = 0; try { n = (int)faceOffsets.Count; } catch { }
            dynamic firstItem = null;
            for (int i = 1; i <= n; i++)
            {
                dynamic feat; try { feat = faceOffsets.Item(i); } catch { continue; }
                if (firstItem == null) firstItem = feat;
                string name; try { name = (string)feat.Name; } catch { name = null; }
                if (name != null && name.StartsWith("GAP:", StringComparison.OrdinalIgnoreCase)) { foundBy = $"por nome ('{name}')"; return feat; }
            }
            if (firstItem != null) { foundBy = $"única feature de GAP (de {n}, sem nome 'GAP:' — pega a 1ª)"; return firstItem; }
            return null;
        }
    }
}
