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

            var plan = new ElectrodeBuildPlan { AssemblyName = ElectrodeNaming.SafeDocName(asmDoc) };

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
            var report = new BurnCoordinateReport { AssemblyName = ElectrodeNaming.SafeDocName(asmDoc) };

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
                out double oxM, out double oyM, out double ozM,
                out double axRad, out double ayRad, out double azRad);
            bool rotated = hasPlacement && (Math.Abs(axRad) + Math.Abs(ayRad) + Math.Abs(azRad) > 1e-6);
            report.OriginKnown = hasPlacement;
            report.OriginX = Units.MToMm(oxM); report.OriginY = Units.MToMm(oyM); report.OriginZ = Units.MToMm(ozM);
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

            // A peça SELECIONADA manda (Carlos, 2026-09-11) — ver a nota de FindBurnOccurrence.
            // Sem seleção, cai na mira automática por cor, que é o comportamento de sempre.
            int ignoredSel;
            var selected = CollectSelectedOccurrences(asmDoc, out ignoredSel, "Analisar (Z)");
            OccurrenceInfo preferred = selected.Count > 0 ? selected[0] : null;
            if (selected.Count > 1)
                Log.Warn($"Analisar (Z): {selected.Count} ocorrências selecionadas — usando a 1ª ('{preferred.Name}'). " +
                         "Selecione UMA peça para não haver dúvida.");
            else if (preferred == null)
                Log.Info("Analisar (Z): nada selecionado — mirando pela cor de queima. Se cair no eletrodo em vez do " +
                         "postiço, selecione a peça certa na montagem e clique de novo.");

            var hit = FindBurnOccurrence(ctx, app, preferred);
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

            // NÍVEL 1 da usinabilidade (2026-09-11), SÓ LEITURA: mede o raio EXATO das faces
            // curvas da cavidade e sinaliza o que a ferramentaria não produz. O corpo sai de
            // Face.Body (propriedade confirmada no dump), então não é preciso reabrir a peça da
            // ocorrência. Best-effort: uma falha aqui não pode derrubar a análise de Z, que é o
            // resultado principal do botão.
            try
            {
                object cavityBody = TryGetSolidBody(target.OccurrenceDocument)
                                    ?? (all.Count > 0 ? (object)all[0].ComFace.Body : null);
                foreach (var radiusHit in Machinability.BRepRadiusProbe.Probe(cavityBody, null))
                {
                    result.MachinabilityWarnings.Add(radiusHit.Describe());
                    switch (radiusHit.Verdict)
                    {
                        case Machinability.MachinabilityVerdict.BelowMinimumRadius:
                            result.BelowMinimumRadiusCount++; break;
                        case Machinability.MachinabilityVerdict.HoleBeyondMillReach:
                            result.DeepHoleCount++; break;
                        default:
                            result.BeyondReachCount++; break;
                    }
                }

                // CANTO VIVO (Carlos, 2026-09-11): o raio pequeno demais é só metade do nível 1.
                // Canto vivo não tem face curva nenhuma para medir — é uma ARESTA entre dois
                // planos, raio ZERO — e toda fresa deixa ali o próprio raio. Quem responde é a
                // topologia, e a sonda vive separada por isso.
                foreach (var corner in Machinability.SharpCornerProbe.Probe(cavityBody))
                {
                    result.MachinabilityWarnings.Add(corner.Describe());
                    result.SharpCornerCount++;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Usinabilidade: análise pulada — " + ex.GetBaseException().Message);
            }

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
            OccurrenceTransform pose = null;              // pose 3D completa (preferida)
            if (cavity != null)
            {
                var actx = new AssemblyContext(asmDoc);
                if (actx.TryGetPlacement(cavity, out double coxM, out double coyM, out double cozM,
                                         out double caxRad, out double cayRad, out double cazRad))
                {
                    occXmm = Units.MToMm(coxM); occYmm = Units.MToMm(coyM); occZmm = Units.MToMm(cozM); // METROS→mm
                    occAx = caxRad; occAy = cayRad; occAz = cazRad;
                    bool rotated = Math.Abs(caxRad) + Math.Abs(cayRad) + Math.Abs(cazRad) > 1e-6;
                    Log.Info($"Cavidade '{cavity.Name}' na montagem: origem ({occXmm:0.0}, {occYmm:0.0}, {occZmm:0.0}) mm" +
                             (rotated ? $" + ROTAÇÃO (rad X={caxRad:0.###} Y={cayRad:0.###} Z={cazRad:0.###})" : ", sem rotação"));
                }
                else Log.Warn("Transform da cavidade ilegível — usando coordenadas locais como se fossem da montagem.");

                // A pose completa (GetMatrix) é o caminho preferido: resolve a cavidade
                // INCLINADA, que a soma de Z + rotação Z não resolve. Se a leitura falhar,
                // segue o caminho antigo — que continua correto enquanto o eixo Z local for
                // paralelo ao da montagem, e é avisado quando não for.
                pose = AssemblyContext.TryGetPose(cavity);
                if (pose == null && Math.Abs(occAx) + Math.Abs(occAy) > 1e-4)
                    Log.Warn("Cavidade INCLINADA (rotação X/Y ≠ 0) e a matriz da ocorrência não pôde ser lida — " +
                             "só a rotação Z será aplicada; confira a orientação de cada eletrodo.");
                else if (pose != null && pose.IsTilted())
                    Log.Info("Cavidade INCLINADA — posicionamento pela matriz completa da ocorrência " +
                             "(o eletrodo herda a inclinação da cavidade).");
            }

            string folder = ElectrodeNaming.ResolveElectrodeFolder(asmDoc, p);
            System.IO.Directory.CreateDirectory(folder);
            Log.Info($"Criando {res.Electrodes.Count} eletrodo(s) (peça, sem bloco) em: {folder}");

            int created = 0;
            foreach (var e in res.Electrodes)
                if (CreateAndPlaceElectrode(app, asmDoc, folder, e, occXmm, occYmm, occZmm, occAz, null, pose))
                    created++;

            Log.Info($"{created}/{res.Electrodes.Count} eletrodo(s) criado(s). " +
                     "Revise no SE; SALVE a montagem manualmente; depois edite cada um em contexto, copie " +
                     "(Inter-Part Copy) as faces de queima e use 'Criar Base' para gerar o bloco.");
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

            List<object> faces = CollectSelectedFaces(asmDoc, out int skipped, out object firstParentOccurrence);
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
            // Preferência: .ImmediateParent capturado direto do embrulho da seleção (confirmado
            // ao vivo 2026-07-21 — ver CollectSelectedFaces); fallback = casamento por nome de
            // documento, p/ quando a seleção não vier embrulhada.
            OccurrenceInfo cavity = firstParentOccurrence != null
                ? WrapOccurrence(firstParentOccurrence)
                : FindOwningOccurrence(ctx, faces[0]);
            double occXmm = 0, occYmm = 0, occZmm = 0, occAz = 0;
            OccurrenceTransform pose = null;
            if (cavity != null)
            {
                double tiltRad = 0;
                if (ctx.TryGetPlacement(cavity, out double coxM, out double coyM, out double cozM,
                                         out double caxRad, out double cayRad, out double cazRad))
                {
                    occXmm = Units.MToMm(coxM); occYmm = Units.MToMm(coyM); occZmm = Units.MToMm(cozM);
                    occAz = cazRad;
                    tiltRad = Math.Abs(caxRad) + Math.Abs(cayRad);
                    Log.Info($"Criar eletrodo manual: faces da ocorrência '{cavity.Name}' — origem ({occXmm:0.0}, {occYmm:0.0}, {occZmm:0.0}) mm.");
                }
                else Log.Warn($"Criar eletrodo manual: transform de '{cavity.Name}' ilegível — usando coordenadas locais como se fossem da montagem.");

                // Mesma pose 3D do fluxo automático — os dois posicionam pelo mesmo método.
                pose = AssemblyContext.TryGetPose(cavity);
                if (pose == null && tiltRad > 1e-4)
                    Log.Warn("Criar eletrodo manual: ocorrência INCLINADA (rotação X/Y ≠ 0) e matriz ilegível — " +
                             "só a rotação Z é aplicada; confira a orientação.");
                else if (pose != null && pose.IsTilted())
                    Log.Info("Criar eletrodo manual: ocorrência INCLINADA — posicionamento pela matriz completa " +
                             "(o eletrodo herda a inclinação).");
            }
            else
            {
                Log.Warn("Criar eletrodo manual: não achei a ocorrência (top-level) dona das faces selecionadas " +
                         "— usando as coordenadas locais como se já fossem da montagem.");
            }

            string electrodeNamePrefix = ElectrodeNaming.ElectrodeNamePrefix(asmDoc);
            var e = new Selection.ProposedElectrode
            {
                Index = ElectrodeNaming.NextElectrodeIndex(ctx, electrodeNamePrefix),
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

            string folder = ElectrodeNaming.ResolveElectrodeFolder(asmDoc, p);
            System.IO.Directory.CreateDirectory(folder);

            string electrodeName = $"{electrodeNamePrefix}{e.Index:00}";
            Log.Info($"Criar eletrodo manual: {electrodeName}, {e.FaceCount} face(s), " +
                     $"centro local ({e.CenterXmm:0.0}, {e.CenterYmm:0.0}), fundo Z={e.DeepestZmm:0.0} (local).");

            result.Created = CreateAndPlaceElectrode(app, asmDoc, folder, e, occXmm, occYmm, occZmm, occAz, detectedRa, pose);
            result.Path = System.IO.Path.Combine(folder, $"{electrodeName}.par");
            result.Message = result.Created
                ? $"Eletrodo {electrodeName} criado e posicionado no centro de {e.FaceCount} face(s) (fundo Z={e.DeepestZmm:0.0} mm)." +
                  (detectedRa.HasValue ? $" Ra {detectedRa.Value:0.0} detectado pela cor e gravado na peça." : "")
                : $"Falha ao criar o eletrodo {electrodeName} — veja o log.";
            return result;
        }

        /// <summary>
        /// Ra comum às faces selecionadas, lido pela cor (<see cref="FaceStyleColorReader"/> +
        /// <see cref="RaColorMap"/>) — mesma leitura usada pelo fluxo automático, aqui aplicada
        /// só às faces que o usuário escolheu. Devolve null (sem gravar nada) se nenhuma face
        /// tiver cor mapeada, ou se as faces mapeadas discordarem entre si (queima com Ra
        /// misto não é o caso normal — mais seguro não adivinhar).
        /// </summary>
        private double? DetectCommonRa(List<object> faces, dynamic application)
        {
            var colorReader = new FaceStyleColorReader();
            double? ra = null;
            int matched = 0, mismatched = 0;
            foreach (var f in faces)
            {
                if (!colorReader.TryReadColor(f, application, out System.Drawing.Color color, out string colorSource)) continue;
                if (!_raColorMap.TryGetRa(color, out double faceRa, out _)) continue;
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
        /// em vez de só devolver "0 faces" sem explicação.
        ///
        /// CONFIRMADO ao vivo 2026-07-21 (log `101106`): selecionar uma face de OCORRÊNCIA direto
        /// na montagem (sem entrar em contexto) NÃO devolve a `Face` crua em `SelectSet.Item(i)` —
        /// devolve um objeto EMBRULHO com `.Object` (a `Face` de verdade — confirmado no dump:
        /// membros Area/Body/Edges/GetRange/Vertices) e `.ImmediateParent` (a `Occurrence` dona —
        /// confirmado: Name/OccurrenceDocument/PartFileName/GetTransform). Por isso
        /// `SelectSet.Count` já vinha correto (o bug de contagem zerada de antes era outra coisa/
        /// já resolvido), mas TODO item falhava `TryGetRangeMm` e a mensagem "Nenhuma FACE
        /// selecionada" saía mesmo com a seleção visível. Fix: se o item cru não for uma face
        /// utilizável, tenta `.Object` antes de desistir. Também devolve o `.ImmediateParent` do
        /// PRIMEIRO item embrulhado — é a ocorrência dona de forma DIRETA, mais confiável que o
        /// casamento por nome de documento do <see cref="FindOwningOccurrence"/> (fallback p/
        /// quando a seleção não vem embrulhada, ex.: SE de outra versão).
        /// </summary>
        private static List<object> CollectSelectedFaces(dynamic doc, out int skipped, out object firstParentOccurrence)
        {
            var faces = new List<object>();
            skipped = 0;
            firstParentOccurrence = null;
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
                if (item == null) { skipped++; continue; }

                object candidate = item;
                object parent = null;
                if (!FaceGeometry.TryGetRangeMm(candidate, out _, out _))
                {
                    // Seleção de face de ocorrência (fora de contexto) — desembrulha via .Object;
                    // .ImmediateParent (se existir) é a Occurrence dona, capturada de graça aqui.
                    // InvokeMember (não `dynamic`) de propósito: é o MESMO mecanismo que o SPY
                    // (ComDiagnostics.DumpObjectInner) usa pra ler ".Object" com sucesso — troca
                    // feita 2026-07-22 (log `073330`) depois que a versão com `dynamic` ainda
                    // devolvia "Nenhuma FACE selecionada" mesmo com o SPY mostrando `.Object`
                    // como uma Face genuína (Area/Body/GetRange/Vertices); exceções agora são
                    // LOGADAS (nunca mais escondidas atrás de um catch vazio).
                    parent = TryGetComProperty(item, "ImmediateParent", "Criar eletrodo manual", i);
                    candidate = TryGetComProperty(item, "Object", "Criar eletrodo manual", i);
                }

                if (candidate != null && FaceGeometry.TryGetRangeMm(candidate, out _, out _))
                {
                    faces.Add(candidate);
                    if (firstParentOccurrence == null && parent != null) firstParentOccurrence = parent;
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
        /// Lê uma propriedade COM SEM PARÂMETROS via <c>Type.InvokeMember</c> (não `dynamic`) —
        /// o mesmo mecanismo comprovado que o SPY (<see cref="ComDiagnostics"/>) usa pra
        /// introspectar objetos "estranhos" (embrulhos de seleção, etc.) com sucesso. Trocado
        /// de `dynamic` 2026-07-22 (log `073330`): "Criar eletrodo manual" continuava dizendo
        /// "Nenhuma FACE selecionada" mesmo com o SPY mostrando que `.Object` era uma Face
        /// genuína — `dynamic` ficou sob suspeita como o elo fraco. Nunca lança: devolve null e
        /// LOGA o motivo (nunca mais um catch vazio escondendo a causa real).
        /// </summary>
        private static object TryGetComProperty(object comObject, string propertyName, string logContext, int? itemIndex = null)
        {
            if (comObject == null) return null;
            try
            {
                return comObject.GetType().InvokeMember(
                    propertyName, BindingFlags.GetProperty, null, comObject, null);
            }
            catch (Exception ex)
            {
                string where = itemIndex.HasValue ? $"SelectSet[{itemIndex}]" : "objeto";
                Log.Warn($"{logContext}: {where}.{propertyName} indisponível: {ex.GetBaseException().Message}");
                return null;
            }
        }

        /// <summary>Mesma leitura de <see cref="TryGetComProperty"/>, mas SEM logar — para o
        /// primeiro "é isto mesmo?" indicador (ex.: testar se um item de SelectSet já é a
        /// Occurrence crua, antes de decidir se precisa desembrulhar via `.ImmediateParent`),
        /// onde falhar é o caminho NORMAL/esperado, não um erro a reportar.</summary>
        private static object TryGetComPropertyQuiet(object comObject, string propertyName)
        {
            if (comObject == null) return null;
            try
            {
                return comObject.GetType().InvokeMember(
                    propertyName, BindingFlags.GetProperty, null, comObject, null);
            }
            catch { return null; }
        }

        /// <summary>Embrulha um objeto Occurrence cru (ex.: `.ImmediateParent` do item de
        /// SelectSet) num <see cref="OccurrenceInfo"/>, lendo Name/OccurrenceDocument direto —
        /// sem precisar casar por nome de documento contra <see cref="AssemblyContext.GetOccurrences"/>.</summary>
        private static OccurrenceInfo WrapOccurrence(object comOccurrence)
        {
            string name = "<sem nome>"; dynamic occDoc = null;
            try { name = (string)((dynamic)comOccurrence).Name; } catch { }
            try { occDoc = ((dynamic)comOccurrence).OccurrenceDocument; } catch { }
            return new OccurrenceInfo(comOccurrence, name, occDoc);
        }

        /// <summary>
        /// Acha, entre as ocorrências TOP-LEVEL da montagem, a que contém o documento da
        /// face amostrada (via Face.Document, comparado por FullName/Name — a mesma peça
        /// pode ter proxies COM diferentes, então comparar por REFERÊNCIA não é confiável).
        /// Null se a face não vier de nenhuma ocorrência top-level conhecida (ex.: dentro
        /// de uma subMontagem — fora do escopo atual, igual ao <see cref="FindBurnOccurrence"/>).
        /// FALLBACK de <see cref="WrapOccurrence"/> (usado quando a seleção não vem embrulhada).
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

        /// <summary>
        /// Cria e posiciona UM eletrodo (SÓ a peça, vazia) a partir de um
        /// <see cref="Selection.ProposedElectrode"/> — reusado pela criação AUTOMÁTICA
        /// (<see cref="CreateElectrodesWithBlank"/>, um por candidato da análise de Z) e
        /// pela criação MANUAL (<see cref="CreateElectrodeFromSelection"/>, um por clique,
        /// a partir das faces que o usuário selecionou à mão).
        ///
        /// Carlos, 2026-07-23: NÃO desenha bloco/fixação aqui — nesse momento não existem
        /// superfícies FINALIZADAS (a queima ainda precisa ser copiada por Inter-Part Copy,
        /// tratada/fechada/costurada em contexto, ver [[real-edm-workflow]]), então um bloco
        /// desenhado agora, sobre uma pegada preliminar, pouco ajuda — só suja a árvore e
        /// precisa ser refeito. O bloco (com blank/fixação corretos) é construído DEPOIS,
        /// pela ferramenta "Criar Base" (<see cref="SurfaceBlockBuilder"/>), sobre a
        /// geometria real já copiada/tratada. Aqui só posiciona a ocorrência na montagem via
        /// PutTransform (origem = centro XY + fundo Z da região; rotação Z da cavidade) —
        /// "tocando o fundo da região a erodir".
        /// </summary>
        private bool CreateAndPlaceElectrode(dynamic app, dynamic asmDoc, string folder,
            Selection.ProposedElectrode e, double occXmm, double occYmm, double occZmm, double occAz,
            double? detectedRa = null, OccurrenceTransform pose = null)
        {
            dynamic partDoc = null;
            try
            {
                // POSICIONAMENTO (regra do Carlos, Logs 51-52): a origem do .par (zero-peça)
                // vai exatamente na SUPERFÍCIE de queima, no espaço da MONTAGEM. A superfície
                // local da cavidade vira montagem somando o TRANSFORM da ocorrência da
                // cavidade (occ*mm).
                //
                // A superfície que o eletrodo toca é o FUNDO do bolsão — a face
                // PERPENDICULAR ao Z no ponto MAIS FUNDO (Z mais negativo na montagem),
                // não o topo/abertura (paredes paralelas ao Z). Por isso DeepestZmm
                // (Z mínimo das faces), não TopZmm — antes estava invertido (Log 52).
                double baseZmm = e.DeepestZmm;                      // fundo do bolsão (Z mín.), LOCAL da cavidade
                double asmX, asmY, asmZ;
                OccurrenceTransform electrodePose = null;

                if (pose != null)
                {
                    // CAMINHO PREFERIDO: o ponto de queima é LOCAL da cavidade, e a matriz da
                    // ocorrência leva local -> montagem de uma vez — sem somar coordenadas de
                    // eixos que podem nem ser paralelos. É isto que conserta a cavidade
                    // inclinada; com a cavidade "em pé" dá exatamente o mesmo que a conta antiga.
                    pose.TransformPointM(Units.MmToM(e.CenterXmm), Units.MmToM(e.CenterYmm), Units.MmToM(baseZmm),
                        out double pxM, out double pyM, out double pzM);
                    asmX = Units.MToMm(pxM); asmY = Units.MToMm(pyM); asmZ = Units.MToMm(pzM);

                    // O eletrodo herda a ORIENTAÇÃO da cavidade: só assim o eixo em que ele
                    // desce é o eixo em que a cavidade foi aberta.
                    electrodePose = pose.WithTranslationM(pxM, pyM, pzM);
                }
                else
                {
                    // Caminho antigo (matriz ilegível): rotação Z apenas. Continua correto
                    // enquanto o Z local for paralelo ao da montagem — e o aviso de cavidade
                    // inclinada já foi dado por quem chamou.
                    double cosZ = Math.Cos(occAz), sinZ = Math.Sin(occAz);
                    double rcx = e.CenterXmm * cosZ - e.CenterYmm * sinZ;
                    double rcy = e.CenterXmm * sinZ + e.CenterYmm * cosZ;
                    asmX = occXmm + rcx;
                    asmY = occYmm + rcy;
                    asmZ = occZmm + baseZmm;                        // superfície, MONTAGEM (a origem vai aqui)
                }

                // Nome do eletrodo (Carlos, 2026-07-23): "Nome da montagem" + "EE" + índice, ex.
                // "15142.200_EDM_EE01" — substitui o antigo "ELD_D01" (prefixo fixo, não
                // derivado do job). Ver ElectrodeNaming.ResolveElectrodeBaseName/ElectrodeNamePrefix.
                string path = System.IO.Path.Combine(folder, $"{ElectrodeNaming.ElectrodeNamePrefix(asmDoc)}{e.Index:00}.par");
                Log.Info($"Eletrodo {e.Index}: peça vazia; superfície local Z={baseZmm:0.0} -> montagem Z={asmZ:0.0} " +
                         $"(origem); XY montagem ({asmX:0.0}, {asmY:0.0}), rotZ={occAz * 180.0 / Math.PI:0.0}° -> " +
                         $"{System.IO.Path.GetFileName(path)}");

                partDoc = app.Documents.Add("SolidEdge.PartDocument");
                if (detectedRa.HasValue) RaVariableStore.TryWrite(partDoc, detectedRa.Value);

                partDoc.SaveAs(path);
                partDoc.Close();
                partDoc = null;

                dynamic occ = asmDoc.Occurrences.AddByFilename(path);

                // Escada de posicionamento, do mais completo para o mais pobre:
                //   PutMatrix  — pose 3D inteira (única que acerta cavidade inclinada);
                //   PutTransform — origem + rotação Z (o que existia antes);
                //   PutOrigin  — só a translação.
                bool placed = false;
                if (electrodePose != null)
                    placed = TryPutMatrix(occ, electrodePose, e.Index);

                if (!placed)
                {
                    // PutTransform (dump linha 6707): origem→superfície + rotação Z da cavidade.
                    try { occ.PutTransform(Units.MmToM(asmX), Units.MmToM(asmY), Units.MmToM(asmZ), 0.0, 0.0, occAz); placed = true; }
                    catch (Exception pe)
                    {
                        Log.Warn($"Eletrodo {e.Index}: PutTransform falhou ({pe.GetBaseException().Message}); tentando PutOrigin (sem rotação).");
                        try { occ.PutOrigin(Units.MmToM(asmX), Units.MmToM(asmY), Units.MmToM(asmZ)); placed = true; }
                        catch (Exception pe2) { Log.Warn($"Eletrodo {e.Index}: PutOrigin também falhou: {pe2.GetBaseException().Message}"); }
                    }
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

        /// <summary>
        /// Posiciona a ocorrência pela pose 3D completa, via
        /// <c>Occurrence.PutMatrix(Matrix: SAFEARRAY(double), Replace: bool)</c> — assinatura
        /// confirmada no dump da typelib SE 2023, ao lado do <c>GetMatrix</c> de onde a pose veio.
        /// <c>Replace = true</c>: a matriz SUBSTITUI a posição atual, não é composta com ela
        /// (compor aplicaria a rotação duas vezes).
        ///
        /// Devolve false (com log) em vez de lançar — quem chama cai na escada de fallback.
        /// </summary>
        private static bool TryPutMatrix(object occ, OccurrenceTransform pose, int index)
        {
            try
            {
                object[] args = { pose.ToMatrix(), true };
                occ.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, occ, args,
                    null, CultureInfo.InvariantCulture, null);
                Log.Info($"Eletrodo {index}: posicionado pela matriz completa da ocorrência ({pose.Describe()}).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"Eletrodo {index}: PutMatrix falhou ({ex.GetBaseException().Message}) — " +
                         "caindo para PutTransform (só rotação Z; confira a orientação se a cavidade for inclinada).");
                return false;
            }
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

        /// <summary>
        /// Resolve qual ocorrência analisar e suas faces de queima.
        ///
        /// <paramref name="preferred"/> (a ocorrência SELECIONADA pelo usuário) manda sobre tudo.
        /// Escolha do Carlos em 2026-09-11, e ela conserta um erro que o 1º run ao vivo expôs: a
        /// mira automática é "quem tem MAIS faces de queima mapeadas", e o ELETRODO também tem as
        /// faces pintadas — então ele ganhava da cavidade. Naquele log a análise inteira (níveis de
        /// Z, pegada, raios) descreveu `5956.209_EDM_EE01.par`, que é o eletrodo, e não o postiço
        /// `5956.209.par`. Nenhuma heurística de nome ou de tamanho é chutada aqui: quem sabe qual
        /// peça é qual é quem está com a montagem aberta.
        /// </summary>
        private Tuple<OccurrenceInfo, IReadOnlyList<FaceGroup>, IReadOnlyList<Selection.ColorTally>> FindBurnOccurrence(
            AssemblyContext ctx, dynamic app, OccurrenceInfo preferred = null)
        {
            OccurrenceInfo best = null;
            IReadOnlyList<FaceGroup> bestGroups = new List<FaceGroup>();
            IReadOnlyList<Selection.ColorTally> bestTally = new List<Selection.ColorTally>();
            int bestFaceCount = 0;

            IEnumerable<OccurrenceInfo> toScan = preferred != null
                ? new[] { preferred }
                : ctx.GetOccurrences();

            foreach (var occ in toScan)
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
                if (preferred != null)
                {
                    // Peça apontada pelo usuário: vale mesmo sem nenhuma face de queima mapeada —
                    // a usinabilidade (raio e canto vivo) não depende de tinta nenhuma.
                    best = occ; bestGroups = groups; bestTally = tally;
                    Log.Info($"Ocorrência SELECIONADA pelo usuário: '{occ.Name}' ({faceCount} face(s) de queima mapeada(s)).");
                    break;
                }
                if (faceCount > bestFaceCount)
                {
                    bestFaceCount = faceCount;
                    best = occ;
                    bestGroups = groups;
                    bestTally = tally;
                }
            }

            if (best == null)
                try { ElectrodeDiagnostics.DiagnoseNoBurn(ctx); }
                catch (Exception e) { Log.Warn("[DIAG] no-burn falhou: " + e.GetBaseException().Message); }

            return Tuple.Create(best, bestGroups, bestTally);
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
            Log.Info($"Ocorrência de eletrodo criada in-place: {ElectrodeNaming.SafeName(occurrence)}");

            // Posiciona sobre a região (best-effort; a cópia associativa traz a
            // geometria na posição real de qualquer forma).
            try
            {
                var ctx = new AssemblyContext(asmDoc);
                if (ctx.TryGetOrigin(target, out double oxM, out double oyM, out double ozM))
                    occurrence.PutOrigin(oxM, oyM, ozM); // já em METROS (GetTransform devolve metros); PutOrigin espera metros
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
                electrodeDoc = scope.ActiveDocument ?? ElectrodeNaming.SafeDoc(occurrence);
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

        /// <summary>
        /// Sinaliza as faces cuja curvatura está fora do que a ferramentaria produz — NÍVEL 1 da
        /// análise de usinabilidade: o raio EXATO, direto do B-Rep, sem malha e sem limiar para
        /// calibrar (ver <see cref="Machinability.BRepRadiusProbe"/>).
        ///
        /// <paramref name="ladder"/> nula = a ferramentaria de fábrica
        /// (<see cref="Machinability.ToolLadder.Shop"/>). SOMENTE LEITURA.
        /// </summary>
        public IReadOnlyList<string> CheckMinimumRadii(dynamic electrodePart, Machinability.ToolLadder ladder = null)
        {
            object body = TryGetSolidBody(electrodePart);
            if (body == null)
            {
                Log.Warn("Raios mínimos: nenhum corpo sólido acessível — análise não executada.");
                return Array.Empty<string>();
            }
            var lines = Machinability.BRepRadiusProbe.Probe(body, ladder).Select(h => h.Describe()).ToList();
            lines.AddRange(Machinability.SharpCornerProbe.Probe(body).Select(c => c.Describe()));
            return lines;
        }

        /// <summary>
        /// Corpo sólido de uma peça — ou o próprio objeto, quando já vem um Body. Best-effort:
        /// devolve null em vez de lançar, porque quem chama é análise de leitura.
        /// </summary>
        private static object TryGetSolidBody(dynamic partOrBody)
        {
            if (partOrBody == null) return null;
            try { return (object)partOrBody.Models.Item(1).Body; }
            catch { }
            // Já é um Body? A coleção Faces[igQueryAll] existe nele e não num PartDocument.
            try
            {
                object probe = ((dynamic)partOrBody).Faces[1];
                if (probe != null) return (object)partOrBody;
            }
            catch { }
            return null;
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

            double offsetM = -Units.MmToM(inwardOffsetMm); // negativo = para dentro
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
            double w = Units.MmToM(blank.DimA);
            double h = Units.MmToM(blank.DimB ?? blank.DimA);
            double blankHeight = Units.MmToM(p.HolderHeight);

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
            double holderHeight = Units.MmToM(20.0); // 20 mm fixo; substituir por parâmetro futuramente.
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

            // NUNCA encadear .FirstOrDefault() direto no retorno de um método chamado com um
            // argumento `dynamic` (asmDoc): como o compilador não resolve o overload em tempo de
            // compilação, a expressão INTEIRA (incl. a extensão LINQ encadeada) vira uma
            // invocação dinâmica — e o DLR não sabe achar métodos de EXTENSÃO (FirstOrDefault)
            // em `dynamic`, só instância (RuntimeBinderException "não contém uma definição para
            // 'FirstOrDefault'", achado 2026-07-22, log `073330`). Fix: variável local
            // ESTATICAMENTE tipada quebra a cadeia — a atribuição a List<OccurrenceInfo> já
            // resolve a conversão de `dynamic`, e o LINQ na linha seguinte volta a ser estático.
            List<OccurrenceInfo> selectedOccurrences = CollectSelectedOccurrences(asmDoc, out int skipped);
            OccurrenceInfo selected = selectedOccurrences.FirstOrDefault();
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

            RaGapPresets.Choice next = RaGapPresets.NextCoarser(currentRa, p.Material, _raColorMap, _offsetPolicy);
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
            var placements = new List<(double xM, double yM, double zM, double azRad)>();
            foreach (var occ in ctx.GetOccurrences())
            {
                if (!SameDocument(occ, sourcePath)) continue;
                if (!ctx.TryGetPlacement(occ, out double xM, out double yM, out double zM, out double axRad, out double ayRad, out double azRad))
                {
                    Log.Warn($"Duplicar eletrodo: transform de '{occ.Name}' ilegível — pulando essa posição.");
                    continue;
                }
                placements.Add((xM, yM, zM, azRad));
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
                MakeElectrodeCopy(sourceDoc, sourcePath, newPath);

                newDoc = OpenCopyForEdit(app, newPath);
                if (newDoc == null)
                {
                    result.Message = "Copiei a peça, mas o Solid Edge não abriu a cópia (veja o log). " +
                                      "Arquivo: " + newPath + " — abra e ajuste o GAP manualmente.";
                    Log.Warn("Duplicar eletrodo: " + result.Message);
                    return result;
                }
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
                    try { occ.PutTransform(t.xM, t.yM, t.zM, 0.0, 0.0, t.azRad); }
                    catch (Exception pe)
                    {
                        Log.Warn($"Duplicar eletrodo: PutTransform falhou ({pe.GetBaseException().Message}); tentando PutOrigin.");
                        try { occ.PutOrigin(t.xM, t.yM, t.zM); }
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

        // ------------------------------------------------------------------
        //  Ferramenta: listar eletrodos SELECIONADOS (janela "Coordenadas")
        // ------------------------------------------------------------------

        /// <summary>
        /// Lista os eletrodos SELECIONADOS na montagem (ocorrências) para a janela
        /// "Coordenadas" (Carlos, 2026-08-04): sem detecção automática por cor — o
        /// usuário escolhe à mão quais eletrodos entram na lista. Cada linha traz a
        /// posição da ocorrência (mesma leitura de "Propriedades de Ocorrência" no SE)
        /// e o GAP/Ra gravados na peça (mesma fonte que "Duplicar eletrodo" usa para
        /// achar o próximo passe). SOMENTE-LEITURA — não altera a montagem nem as peças.
        /// </summary>
        public List<ElectrodeListItem> ListSelectedElectrodes(dynamic asmDoc)
        {
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));
            var result = new List<ElectrodeListItem>();
            var ctx = new AssemblyContext(asmDoc);

            List<OccurrenceInfo> selected = CollectSelectedOccurrences(asmDoc, out int skipped, "Coordenadas");
            if (skipped > 0)
                Log.Info($"Coordenadas: {skipped} item(ns) da seleção ignorado(s) (não são ocorrências).");

            foreach (var occ in selected)
            {
                var item = new ElectrodeListItem { Name = occ.Name };

                if (ctx.TryGetPlacement(occ, out double xM, out double yM, out double zM,
                                         out double axRad, out double ayRad, out double azRad))
                {
                    item.PositionKnown = true;
                    item.X = Units.MToMm(xM);
                    item.Y = Units.MToMm(yM);
                    item.Z = Units.MToMm(zM);
                    item.AzDeg = azRad * 180.0 / Math.PI;
                }
                else
                {
                    item.Notes.Add("posição (Propriedades de Ocorrência) não lida");
                }

                dynamic partDoc = occ.OccurrenceDocument;
                if (partDoc != null)
                {
                    if (TryReadElectrodeRa(partDoc, out double ra, out string raSrc)) item.Ra = ra;
                    else item.Notes.Add("Ra não encontrado (nem variável, nem feature de GAP)");

                    // A MESMA feature de GAP dá as duas colunas seguintes (o offset aplicado e
                    // as faces de queima que a área de secção mede) — acha uma vez só, em vez de
                    // varrer Model.FaceOffsets duas vezes por eletrodo.
                    dynamic gapFeature = FindGapOffsetFeature(partDoc, out string gapSrc);
                    if (gapFeature == null)
                    {
                        // A feature de GAP é quem situa o plano da secção — sem ela as DUAS
                        // colunas ficam vazias, então o motivo tem de dizer isso de uma vez.
                        item.Notes.Add("GAP não encontrado (feature Model.FaceOffsets) — sem ele também não dá para medir a secção");
                    }
                    else
                    {
                        if (TryReadElectrodeGapMm(gapFeature, out double gap)) item.GapMm = gap;
                        else item.Notes.Add("GAP não lido (FaceOffset.Distance)");

                        if (TryReadBurnSectionAreaCm2(partDoc, gapFeature, out double areaCm2, out double zMidMm, out string areaErr))
                        {
                            item.BurnAreaCm2 = areaCm2;
                            item.SectionZMm = zMidMm;
                        }
                        else
                        {
                            item.Notes.Add("área da secção não calculada: " + areaErr);
                        }
                    }
                }
                else
                {
                    item.Notes.Add("documento da peça inacessível");
                }

                result.Add(item);
            }

            Log.Info($"Coordenadas: {result.Count} eletrodo(s) da seleção listado(s).");
            return result;
        }

        /// <summary>GAP atual do eletrodo (mm): lê <c>FaceOffset.Distance</c> (metros, negativo =
        /// encolhe) da feature que <see cref="FindGapOffsetFeature"/> acha — o valor REAL
        /// aplicado, não uma reinterpretação do nome da feature.</summary>
        private static bool TryReadElectrodeGapMm(dynamic gapFeature, out double gapMm)
        {
            gapMm = 0;
            try
            {
                double distanceM = (double)gapFeature.Distance;
                gapMm = Math.Abs(Units.MToMm(distanceM));
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Coordenadas: ler feature.Distance falhou — " + ex.GetBaseException().Message);
                return false;
            }
        }

        /// <summary>
        /// Área da secção de queima (cm²) do eletrodo: corte horizontal no MEIO da altura das
        /// faces da feature de GAP — ou seja, do corpo JÁ subdimensionado pelo offset, que é o
        /// que a peça .par realmente tem. Só leitura (malha via <c>Face.GetFacetData</c>, nenhuma
        /// geometria criada). Ver <see cref="SectionAreaCalculator"/>.
        ///
        /// As faces da feature de GAP dizem só ONDE cortar; quem é malhado é o CORPO
        /// (<c>Models.Item(1).Body</c>), que é fechado por construção — as faces do GAP são as
        /// que o usuário selecionou em "Aplicar GAP" e podem não dar a volta no eletrodo, que era
        /// a causa da coluna sair vazia em alguns eletrodos (Carlos, 2026-09-02).
        /// </summary>
        private static bool TryReadBurnSectionAreaCm2(dynamic partDoc, dynamic gapFeature,
            out double areaCm2, out double zMidMm, out string error)
        {
            areaCm2 = 0; zMidMm = 0;
            object[] faces;
            try { faces = ModelingHelpers.GetFeatureFaces(gapFeature); }
            catch (Exception ex) { error = "faces da feature de GAP inacessíveis — " + ex.GetBaseException().Message; return false; }

            if (faces == null || faces.Length == 0) { error = "feature de GAP sem faces"; return false; }

            object body = null;
            try { body = (object)partDoc.Models.Item(1).Body; }
            catch (Exception ex) { Log.Warn("Coordenadas: corpo do eletrodo inacessível (malhando só as faces de GAP) — " + ex.GetBaseException().Message); }

            return SectionAreaCalculator.TryMidSectionAreaCm2(faces, body, out areaCm2, out zMidMm, out error);
        }

        /// <summary>Ocorrências (objetos COM crus, envolvidos em <see cref="OccurrenceInfo"/>) da
        /// SelectSet atual — tolerante a itens que não são ocorrências (ex.: uma face
        /// selecionada por engano). NUNCA falha silenciosamente, mesmo padrão de
        /// <see cref="CollectSelectedFaces"/>. <paramref name="logTag"/> identifica o comando
        /// chamador nas linhas de log (ex.: "Duplicar eletrodo", "Coordenadas") — a mesma
        /// leitura da SelectSet serve a mais de um botão.</summary>
        private static List<OccurrenceInfo> CollectSelectedOccurrences(dynamic doc, out int skipped, string logTag = "Duplicar eletrodo")
        {
            var result = new List<OccurrenceInfo>();
            skipped = 0;
            dynamic ss;
            try { ss = doc.SelectSet; }
            catch (Exception ex) { Log.Warn($"{logTag}: doc.SelectSet inacessível: {ex.GetBaseException().Message}"); return result; }

            int n = 0;
            try { n = (int)ss.Count; }
            catch (Exception ex) { Log.Warn($"{logTag}: SelectSet.Count falhou: {ex.GetBaseException().Message}"); return result; }
            Log.Info($"{logTag}: SelectSet.Count={n}.");

            for (int i = 1; i <= n; i++)
            {
                object item;
                try { item = ss.Item(i); }
                catch (Exception ex) { Log.Warn($"{logTag}: SelectSet.Item({i}) falhou: {ex.GetBaseException().Message}"); continue; }
                if (item == null) { skipped++; continue; }

                object candidate = item;
                // OccurrenceDocument primeiro, silencioso (só indicador de "é ocorrência de
                // verdade?" — falhar aqui é o caminho NORMAL quando a seleção vem embrulhada,
                // não um erro; loga só se a tentativa de desembrulhar também falhar, abaixo).
                object occDoc = TryGetComPropertyQuiet(candidate, "OccurrenceDocument");
                string name = occDoc != null ? TryGetComPropertyQuiet(candidate, "Name") as string : null;
                if (occDoc == null)
                {
                    // Mesmo embrulho achado na seleção de face de ocorrência (2026-07-21/22, ver
                    // CollectSelectedFaces) — se selecionar a ocorrência inteira também vier
                    // embrulhado nalguma situação, `.ImmediateParent` é a Occurrence de verdade.
                    // InvokeMember (não `dynamic`) pelo mesmo motivo de CollectSelectedFaces:
                    // é o mecanismo comprovado (usado pelo SPY) — o `dynamic` ficou sob suspeita
                    // depois de "Criar eletrodo manual" continuar falhando com ele (log `073330`).
                    candidate = TryGetComProperty(item, "ImmediateParent", logTag, i);
                    if (candidate != null)
                    {
                        occDoc = TryGetComProperty(candidate, "OccurrenceDocument", logTag, i);
                        name = TryGetComProperty(candidate, "Name", logTag, i) as string;
                    }
                }
                if (occDoc == null)
                {
                    skipped++;
                    ComDiagnostics.DumpObject($"{logTag}: SelectSet[{i}] não é ocorrência", item, 1);
                    continue;
                }
                result.Add(new OccurrenceInfo(candidate, name ?? "<sem nome>", occDoc));
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
        /// InvokeMember), renomeia e repinta as faces dela via <see cref="FaceColorPainter"/>
        /// (`Body.SetFacesStyle` — `Face.Style.Diffuse*` direto falha silenciosamente quando a
        /// face não tem override próprio, achado 2026-07-21 no "Aplicar GAP"). NUNCA lança.
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
                feature.Distance = -Units.MmToM(Math.Abs(next.GapMm)); // metros, negativo = encolhe
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
                FaceColorPainter.Paint(partDoc, faces, next.Color, next.Ra);
            }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: repintar as faces falhou (cosmético, segue) — " + ex.GetBaseException().Message); }

            return true;
        }

        /// <summary>
        /// A CÓPIA DA PEÇA — e por que não é `File.Copy` (correção 2026-09-08, log `085342`).
        ///
        /// Um `File.Copy` produz um .par byte-a-byte idêntico, INCLUSIVE o ID interno de
        /// documento do original — que está ABERTO, porque a montagem o carregou. Ao pedir
        /// `Documents.Open` nesse arquivo, a SE vê um ID que já tem em memória e devolve NULO
        /// (sem lançar). Daí o "Model.FaceOffsets — referência nula" em TODA tentativa, 246 ms
        /// depois da cópia: rápido demais para a SE ter aberto peça alguma.
        ///
        /// `PartDocument.SaveCopyAs(caminho)` é o caminho nativo: a própria SE grava a cópia com
        /// identidade NOVA e o documento de origem continua onde está — ao contrário de
        /// `SaveAs`, que RENOMEARIA a peça dentro da montagem do usuário. O `File.Copy` fica de
        /// reserva, para o caso de `SaveCopyAs` não existir nesta versão do SE.
        /// </summary>
        private static void MakeElectrodeCopy(dynamic sourceDoc, string sourcePath, string newPath)
        {
            try
            {
                sourceDoc.SaveCopyAs(newPath);
                if (System.IO.File.Exists(newPath))
                {
                    Log.Info($"Duplicar eletrodo: cópia por SaveCopyAs -> {System.IO.Path.GetFileName(newPath)}");
                    return;
                }
                Log.Warn("Duplicar eletrodo: SaveCopyAs não lançou, mas o arquivo não apareceu — caindo no File.Copy.");
            }
            catch (Exception ex)
            {
                Log.Warn("Duplicar eletrodo: SaveCopyAs falhou (" + ex.GetBaseException().Message + ") — caindo no File.Copy.");
            }

            System.IO.File.Copy(sourcePath, newPath);
            Log.Warn($"Duplicar eletrodo: cópia por File.Copy -> {System.IO.Path.GetFileName(newPath)} " +
                     "(mesmo ID interno do original; se o Open devolver nulo, é por isso).");
        }

        /// <summary>
        /// Abre a cópia para edição. NÃO é só `Documents.Open` (falha real de 2026-09-08, log
        /// `085342`: "Model.FaceOffsets inacessível — não é possível fazer associação em tempo
        /// de execução em uma referência NULA", 246 ms depois da cópia, em toda tentativa).
        /// A mesma expressão `Models.Item(1).FaceOffsets` funcionava na peça de ORIGEM na linha
        /// de cima — logo o problema não é a API, é o documento recém-aberto.
        ///
        /// Duas defesas, porque `Documents.Open` é declarado devolvendo `IDispatch` (pode vir
        /// NULO) e porque a SE termina de montar o documento no ciclo de OCIOSO — chamada de
        /// dentro de um comando do add-in, que segura a thread de UI, esse ciclo não acontece
        /// sozinho e a árvore pode voltar vazia:
        ///   1. `Application.DoIdle()` depois do Open, dando à SE a volta que falta;
        ///   2. se ainda vier nulo, procurar o arquivo em `Application.Documents` — ele costuma
        ///      estar aberto mesmo quando o retorno do Open veio vazio.
        /// Loga o que abriu (tipo, ambiente, nº de corpos), senão a próxima falha volta a ser
        /// um "referência nula" que não diz de quem.
        /// </summary>
        private static dynamic OpenCopyForEdit(dynamic app, string path)
        {
            dynamic doc = null;
            try { doc = app.Documents.Open(path); }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: Documents.Open falhou — " + ex.GetBaseException().Message); }

            try { app.DoIdle(); } catch (Exception ex) { Log.Info("Duplicar eletrodo: DoIdle indisponível (" + ex.GetBaseException().Message + ")."); }

            if (doc == null)
            {
                doc = FindOpenDocumentByPath(app, path);
                Log.Warn(doc == null
                    ? "Duplicar eletrodo: Documents.Open devolveu NULO e a cópia não apareceu em Application.Documents."
                    : "Duplicar eletrodo: Documents.Open devolveu nulo, mas a cópia ESTAVA aberta — recuperada de Application.Documents.");
            }

            if (doc != null) Log.Info("Duplicar eletrodo: cópia aberta — " + DescribeDoc(doc));
            return doc;
        }

        /// <summary>A cópia entre os documentos abertos, pelo caminho completo.</summary>
        private static dynamic FindOpenDocumentByPath(dynamic app, string path)
        {
            try
            {
                dynamic docs = app.Documents;
                int n = 0; try { n = (int)docs.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    dynamic d; try { d = docs.Item(i); } catch { continue; }
                    string full; try { full = (string)d.FullName; } catch { continue; }
                    if (string.Equals(full, path, StringComparison.OrdinalIgnoreCase)) return d;
                }
            }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: varrer Application.Documents falhou — " + ex.GetBaseException().Message); }
            return null;
        }

        /// <summary>Retrato de um documento para o log — é o que distingue "não abriu" de
        /// "abriu vazio" de "abriu no ambiente errado".</summary>
        private static string DescribeDoc(dynamic doc)
        {
            string name = "?", type = "?", mode = "?", models = "?";
            try { name = (string)doc.Name; } catch { }
            try { type = ((int)doc.Type).ToString(); } catch { }
            try { mode = AutoEDM.Com.ModelingEnvironment.Name(AutoEDM.Com.ModelingEnvironment.Read(doc)); } catch { }
            try { models = ((int)doc.Models.Count).ToString(); } catch (Exception ex) { models = "ilegível (" + ex.GetBaseException().Message + ")"; }
            return $"'{name}' Type={type} (1=peça), modelagem {mode}, Models.Count={models}";
        }

        private static dynamic FindGapOffsetFeature(dynamic partDoc, out string foundBy)
        {
            foundBy = null;

            // PASSO A PASSO de propósito: `partDoc.Models.Item(1).FaceOffsets` numa linha só
            // devolve sempre o mesmo "referência nula" do binder, sem dizer QUAL elo é nulo
            // (foi o que custou a rodada de 2026-09-08). Cada elo agora se identifica.
            dynamic models;
            try { models = partDoc.Models; }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: doc.Models inacessível — " + ex.GetBaseException().Message); return null; }
            if (models == null) { Log.Warn("Duplicar eletrodo: doc.Models veio NULO (documento não terminou de abrir?)."); return null; }

            int bodies = -1; try { bodies = (int)models.Count; } catch { }
            if (bodies == 0) { Log.Warn("Duplicar eletrodo: a cópia abriu SEM sólido (Models.Count=0) — nada a ajustar."); return null; }

            dynamic model;
            try { model = models.Item(1); }
            catch (Exception ex) { Log.Warn($"Duplicar eletrodo: Models.Item(1) falhou (Count={bodies}) — " + ex.GetBaseException().Message); return null; }
            if (model == null) { Log.Warn($"Duplicar eletrodo: Models.Item(1) veio NULO (Count={bodies})."); return null; }

            dynamic faceOffsets;
            try { faceOffsets = model.FaceOffsets; }
            catch (Exception ex) { Log.Warn("Duplicar eletrodo: Model.FaceOffsets inacessível — " + ex.GetBaseException().Message); return null; }
            if (faceOffsets == null) { Log.Warn("Duplicar eletrodo: Model.FaceOffsets veio NULO."); return null; }

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
