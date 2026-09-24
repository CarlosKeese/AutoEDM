using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using AutoEDM.Com;
using AutoEDM.Config;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Mold;
using AutoEDM.Reporting;
using AutoEDM.Reverse;
using AutoEDM.Revisions;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// Os BOTÕES da ribbon como ferramentas do agente, mais a troca de ambiente de modelagem.
    ///
    /// Cada ferramenta aqui chama o MESMO método do Core que o botão chama — o que muda é só a
    /// borda: onde o botão abre uma janela ou pergunta Sim/Não, a ferramenta recebe a resposta
    /// como ARGUMENTO. Nada daqui abre diálogo: uma caixa modal disparada por agente trava a
    /// thread da SE esperando um humano que não sabe que foi perguntado.
    ///
    /// Os botões do grupo MCP (ligar/desligar a ponte, liberar escrita) ficam DE FORA de
    /// propósito: a chave de escrita é do usuário, e um agente não pode girá-la.
    /// </summary>
    public sealed partial class SeToolRunner
    {
        /// <summary>Snapshot do gravador ("Iniciar leitura" → "Gravar log") pedido pelo AGENTE.
        /// Separado do da ribbon: um clique do Carlos no meio não consome o snapshot do agente.</summary>
        private Dictionary<string, List<string>> _recBaseline;

        /// <summary>Despacha as ferramentas-botão. null = não é uma delas.</summary>
        private string DispatchButton(string tool, dynamic app, dynamic doc, string argsJson, List<string> captured)
        {
            switch (tool.ToLowerInvariant())
            {
                case "se_trocar_ambiente": return SwitchEnvironment(app, doc, argsJson);
                case "se_criar_eletrodos": return CreateElectrodes(app, doc, argsJson);
                case "se_criar_eletrodo_manual": return CreateElectrodeManual(app, doc);
                case "se_nova_peca": return NewMoldPart(app, doc, argsJson);
                case "se_duplicar_eletrodo": return DuplicateElectrode(app, doc);
                case "se_lista_corte": return SawCutList(app, doc);
                case "se_lista_modificacoes": return ChangeList(doc);
                case "se_ficha": return SpecSheet(app, doc);
                case "se_criar_base": return CreateBase(app, doc, argsJson);
                case "se_unir_superficies": return UniteSurfaces(doc);
                case "se_aplicar_gap": return ApplyGap(doc, argsJson);
                case "se_alojamento_oring": return ORingGroove(app, doc, argsJson);
                case "se_sonda_malha": return MeshProbeTool(doc, argsJson);
                case "se_sonda_interpart": return InterPartProbeTool(app, doc, captured);
                case "se_gravador_iniciar": return RecorderStart(doc);
                case "se_gravador_gravar": return RecorderSave(doc, captured);
            }
            return null;
        }

        // ------------------------------------------------------------ ambiente de modelagem

        /// <summary>
        /// Alterna a PEÇA ativa entre síncrono e ordenado. É um passo à parte, pedido de forma
        /// explícita — nunca embutido numa operação. A regra do projeto continua de pé: o que
        /// deixava esboço órfão e face morta era trocar NO MEIO de uma operação, com faces já
        /// lidas na mão. Aqui a seleção é descartada antes da troca, e o agente é avisado de que
        /// qualquer Face/Edge lida antes não vale mais.
        /// </summary>
        private static string SwitchEnvironment(dynamic app, dynamic doc, string argsJson)
        {
            string wanted = (ReadString(argsJson, "ambiente") ?? "").Trim().ToLowerInvariant();
            ModelingEnv target =
                wanted.StartsWith("sinc") || wanted.StartsWith("sínc") || wanted.StartsWith("sync") ? ModelingEnv.Synchronous :
                wanted.StartsWith("orde") || wanted.StartsWith("order") ? ModelingEnv.Ordered :
                ModelingEnv.Any;
            if (target == ModelingEnv.Any)
                return "Diga o ambiente: \"ambiente\": \"sincrono\" ou \"ordenado\".";

            ModelingEnv before = ModelingEnvironment.Read(doc);
            string name = Str(() => (string)doc.Name, "(sem nome)");
            if (before == target)
                return $"'{name}' já está em {ModelingEnvironment.Name(target)}. Nada foi trocado.";

            int selected = 0; try { selected = (int)doc.SelectSet.Count; } catch { }
            // A seleção sai ANTES: depois da troca o corpo é reconstruído e esses itens viram
            // proxies mortos — deixá-los no SelectSet só entrega lixo à próxima ferramenta.
            try { doc.SelectSet.RemoveAll(); } catch { }

            // Alertas desligados só durante a troca: se a SE quiser perguntar algo, a pergunta
            // não pode ficar parada numa janela que ninguém está olhando.
            bool alertsWere = true, alertsTouched = false;
            try { alertsWere = (bool)app.DisplayAlerts; app.DisplayAlerts = false; alertsTouched = true; } catch { }

            Log.Info($"MCP: trocando '{name}' de {ModelingEnvironment.Name(before)} para {ModelingEnvironment.Name(target)}.");
            try { doc.ModelingMode = (int)target; }
            catch (Exception ex)
            {
                Log.Warn("MCP: a SE recusou a troca de ambiente — " + ex.GetBaseException().Message);
                return $"A Solid Edge recusou a troca para {ModelingEnvironment.Name(target)}: {ex.GetBaseException().Message}\n\n" +
                       "A peça continua como estava. Uma causa comum é um comando ou uma edição em andamento na SE.";
            }
            finally
            {
                if (alertsTouched) { try { app.DisplayAlerts = alertsWere; } catch { } }
            }

            // Relê por um documento FRESCO: o proxy de antes da troca pode não responder mais.
            dynamic fresh = null; try { fresh = app.ActiveDocument; } catch { }
            ModelingEnv after = ModelingEnvironment.Read(fresh ?? doc);
            Log.Info($"MCP: '{name}' agora em {ModelingEnvironment.Name(after)}.");

            if (after != target)
                return $"Pedi {ModelingEnvironment.Name(target)}, mas a peça responde {ModelingEnvironment.Name(after)}. " +
                       "Confira na janela da SE e no log ('se_log').";

            var sb = new StringBuilder();
            sb.AppendLine($"'{name}': {ModelingEnvironment.Name(before)} → {ModelingEnvironment.Name(after)}.");
            if (selected > 0)
                sb.AppendLine($"A seleção anterior ({selected} item(ns)) foi descartada — a troca reconstrói o corpo, então " +
                              "peça ao usuário para selecionar de novo o que a próxima ferramenta precisa.");
            sb.Append("Qualquer face ou aresta lida antes da troca não vale mais.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ montagem: eletrodos

        private static ElectrodeBuilder NewBuilder(dynamic app, AutoEdmConfig cfg) =>
            new ElectrodeBuilder(SolidEdgeConnector.Attach(app),
                offsetPolicy: cfg.BuildOffsetPolicy(), raColorMap: cfg.BuildColorMap());

        /// <summary>
        /// "Criar eletrodos". O botão confere a queima detectada com o usuário ANTES de criar;
        /// aqui a conferência é a própria chamada sem 'confirmar' — o agente mostra o texto ao
        /// usuário e só chama de novo com 'confirmar' depois do sim dele.
        /// </summary>
        private static string CreateElectrodes(dynamic app, dynamic doc, string argsJson)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            ElectrodeBuilder builder = NewBuilder(app, cfg);
            ElectrodeParams p = cfg.ToElectrodeParams();

            ZAnalysisResult res = builder.AnalyzeElectrodesByZ(doc, p);
            if (res.Electrodes.Count == 0)
                return "Nenhum eletrodo detectado (sem cor de queima MAPEADA). Nada foi criado.\n\n" + res.DescribeBurnDetection();

            if (!ReadBool(argsJson, "confirmar", false))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"CONFERÊNCIA — nada foi criado ainda. {res.Electrodes.Count} eletrodo(s) seriam criados.");
                sb.AppendLine();
                sb.AppendLine(res.DescribeBurnDetection());
                if (res.HasDominantUnmappedColor)
                    sb.AppendLine("\nATENÇÃO: a maior região colorida está numa cor FORA do mapa de queima — a detecção pode estar " +
                                  "olhando a cor errada.");
                sb.AppendLine();
                sb.Append("Mostre isto ao usuário. Se ele confirmar que a queima está CERTA, chame de novo com " +
                          "\"confirmar\": true — cria uma peça VAZIA por eletrodo na subpasta 'Eletrodos' e insere " +
                          "posicionada. A montagem NÃO é salva.");
                return sb.ToString();
            }

            int n = builder.CreateElectrodesWithBlank(doc, p);
            return $"{n} eletrodo(s) criado(s) e posicionado(s). A montagem NÃO foi salva.\n\n" +
                   "Próximo passo (do usuário): revisar, SALVAR a montagem, editar cada peça em contexto, copiar as faces " +
                   "de queima e então 'se_criar_base' em cada peça.";
        }

        private static string CreateElectrodeManual(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            ManualElectrodeResult res = NewBuilder(app, cfg).CreateElectrodeFromSelection(doc, cfg.ToElectrodeParams());
            return (res.Created ? "CRIADO. " : "NÃO criado. ") + res.Message;
        }

        /// <summary>Botão "Nova peça": faces da seleção; o que a janela pergunta vira argumento, e o
        /// que não vier sai das preferências gravadas para o projeto (as mesmas da janela).</summary>
        private static string NewMoldPart(dynamic app, dynamic doc, string argsJson)
        {
            string asmPath = null;
            try { asmPath = (string)doc.FullName; } catch { }
            MoldProjectSettings prefs = MoldProjectSettings.Load(asmPath);

            switch ((ReadString(argsJson, "parte") ?? "").ToLowerInvariant())
            {
                case "fixa": prefs.LastSection = MoldSection.Fixed; break;
                case "movel": case "móvel": prefs.LastSection = MoldSection.Moving; break;
                case "extracao": case "extração": prefs.LastSection = MoldSection.Ejection; break;
            }
            switch ((ReadString(argsJson, "eixoAltura") ?? "").ToUpperInvariant())
            {
                case "X": prefs.HeightAxis = HeightAxis.X; break;
                case "Y": prefs.HeightAxis = HeightAxis.Y; break;
                case "Z": prefs.HeightAxis = HeightAxis.Z; break;
            }
            string side = (ReadString(argsJson, "origem") ?? "").ToLowerInvariant();
            if (side == "alto") prefs.OriginAtTop = true; else if (side == "baixo") prefs.OriginAtTop = false;

            NewPartNamePlan plan = NewPartBuilder.PlanName(doc, prefs.LastSection);
            string head = $"{MoldPartNaming.SectionLabel(prefs.LastSection)}, altura em {prefs.HeightAxis}, " +
                          $"origem no ponto mais {(prefs.OriginAtTop ? "alto" : "baixo")}.";
            if (plan.Problem != null) return "NÃO criado. " + plan.Problem;
            if (ReadBool(argsJson, "apenasPlanejar", false))
                return $"PLANO — nada foi criado. Sairia '{plan.FileName}' em '{plan.Folder}'. {head}";

            List<PickedFace> faces = ElectrodeBuilder.ReadSelectedFaces(doc, out int skipped);
            if (faces.Count == 0)
                return "NÃO criado. Nenhuma FACE selecionada: selecione na montagem as faces de referência (clique na peça e " +
                       "clique de novo no mesmo ponto, ou Alt+clique) e chame de novo.";

            NewPartResult res = NewPartBuilder.Create(app, doc, faces, new NewPartOptions
            {
                Section = prefs.LastSection, HeightAxis = prefs.HeightAxis, OriginAtTop = prefs.OriginAtTop
            });
            if (res.Created) prefs.Save(asmPath);
            return (res.Created ? "CRIADO. " : "NÃO criado. ") + res.Message +
                   (skipped > 0 ? $" ({skipped} item(ns) da seleção não eram faces e ficaram de fora.)" : "");
        }

        private static string DuplicateElectrode(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            DuplicateElectrodeResult res = NewBuilder(app, cfg).DuplicateElectrodeToNextGap(doc, cfg.ToElectrodeParams());
            return (res.Created ? "DUPLICADO. " : "NÃO duplicado. ") + res.Message;
        }

        private static string SawCutList(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            List<SawCutListItem> items = NewBuilder(app, cfg).ListSawCuts(doc);
            if (items.Count == 0)
                return "Nenhum eletrodo selecionado. Na montagem, selecione a(s) OCORRÊNCIA(s) do(s) eletrodo(s) " +
                       "(clique na peça, não numa face) e chame de novo.";

            var sb = new StringBuilder();
            sb.AppendLine($"Lista de corte — {items.Count} arquivo(s). Medidas em MILÍMETROS, medida na serra já com o sobremetal.");
            foreach (SawCutListItem it in items)
            {
                SawCut c = it.Cut ?? new SawCut();
                sb.AppendLine();
                sb.AppendLine($"  {it.FileName}   ×{it.Positions} posição(ões)" + (it.Material != null ? $"   {it.Material}" : ""));
                if (it.SizeKnown) sb.AppendLine($"    peça: {F(it.SizeXmm)} × {F(it.SizeYmm)} × {F(it.SizeZmm)}");
                sb.AppendLine(c.Blank != null
                    ? $"    perfil: {c.Blank.Describe()}, {c.Orientation}" + (c.AutoIdentified ? "" : " (escolhido à mão)")
                    : "    perfil: NÃO identificado");
                if (c.CutMm.HasValue) sb.AppendLine($"    serra: {F(c.CutMm.Value)}   (comprimento {F(c.LengthMm ?? 0)})");
                if (!string.IsNullOrEmpty(c.Fit)) sb.AppendLine($"    sobra: {c.Fit}");
                if (!string.IsNullOrEmpty(c.Note)) sb.AppendLine($"    ATENÇÃO: {c.Note}");
                foreach (string note in it.Notes) sb.AppendLine($"    nota: {note}");
            }
            sb.AppendLine();
            sb.Append("A impressão e a troca de perfil continuam na janela do botão 'Lista de corte'.");
            return sb.ToString();
        }

        /// <summary>
        /// "Lista de modificações", só a VARREDURA: quais peças do projeto têm grupo "Rev.N" e o
        /// que há nele, com o que o Carlos já escreveu na folha (o .json ao lado da montagem). A
        /// planilha com miniaturas continua na janela — é lá que ele escreve a descrição.
        /// </summary>
        private static string ChangeList(dynamic doc)
        {
            string asmPath = null;
            try { asmPath = (string)doc.FullName; } catch { }

            ProjectFolder folder = ProjectFolder.Parse(asmPath);
            int revision;
            List<PartChange> parts = RevisionScanner.Scan(doc, folder.Directory, out revision,
                AutoEdmConfig.LoadOrCreateDefault().RevisionPropertyNames);
            if (parts.Count == 0)
                return "Nenhuma peça desta montagem tem um grupo \"Rev.N\" na árvore ordenada. É assim que o AutoEDM " +
                       "sabe o que mudou: o usuário agrupa os recursos da alteração e nomeia o grupo \"Rev.1\", \"Rev.2\"...";

            var report = new ChangeReport { Revision = revision, ProjectDirectory = folder.Directory };
            report.Parts.AddRange(parts);
            ChangeReportStore.Apply(ChangeReportStore.Load(ChangeReportStore.PathFor(asmPath)), report);

            var sb = new StringBuilder();
            sb.AppendLine($"Revisão {revision} — {report.Parts.Count} peça(s) com alteração.");
            foreach (PartChange part in report.Parts)
            {
                sb.AppendLine();
                sb.AppendLine($"  {part.FileName}   Rev.{part.Revision}{(part.IsNew ? "  (PEÇA NOVA)" : "")}" +
                              (part.Positions > 1 ? $"   ×{part.Positions}" : ""));
                foreach (string f in part.Features) sb.AppendLine($"    operação: {f}");
                if (!string.IsNullOrWhiteSpace(part.Description)) sb.AppendLine($"    descrição: {part.Description}");
                foreach (string a in part.Actions) sb.AppendLine($"    ação: {a}");
                foreach (ChangeTask t in part.CheckedTasks) sb.AppendLine($"    [x] {t.Label}" + (string.IsNullOrWhiteSpace(t.Detail) ? "" : $" — {t.Detail}"));
            }
            sb.AppendLine();
            sb.Append("Só leitura. A planilha .xlsx (com as miniaturas) sai da janela do botão 'Lista de modificações'.");
            return sb.ToString();
        }

        private static string SpecSheet(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            ElectrodeParams p = cfg.ToElectrodeParams();
            ElectrodeBuildPlan plan = NewBuilder(app, cfg).PlanFromAssemblyDocument(doc, p);
            string text = ElectrodeSpecSheet.ToText(plan, p);
            Log.Info(text);
            string path = ElectrodeSpecSheetWriter.Save(plan, p, folder: ElectrodeNaming.ResolveProjectFolder(doc));
            return $"Ficha gerada para {plan.Regions.Count} detalhe(s). Arquivos (.txt e .csv):\n{path}\n\n{text}";
        }

        // ------------------------------------------------------------------ peça

        /// <summary>
        /// "Criar Base" sem a janela. Primeiro dimensiona (o que a janela mostra no resumo) e
        /// lista os blanks que servem, numerados; 'apenasPlanejar' para aí. O build é o do botão
        /// "OK" da janela sem preview — constrói e finaliza de uma vez.
        /// </summary>
        private static string CreateBase(dynamic app, dynamic doc, string argsJson)
        {
            string material = ReadString(argsJson, "material");
            var opt = new BlockOverSurfacesOptions
            {
                Material = string.Equals(material, "CuW80", StringComparison.OrdinalIgnoreCase) ? "CuW80" : "Cobre",
                GapMm = ReadDouble(argsJson, "afastamentoMm", 0.0, 0.0, 100.0),
                BlockHeightMm = ReadDouble(argsJson, "alturaMm", 15.0, 3.0, 200.0),
                ApplyFixation = ReadBool(argsJson, "fixacao", true),
                AddMeasurementBand = ReadBool(argsJson, "faixa", true),
            };

            var builder = new SurfaceBlockBuilder();
            BlockOverSurfacesPlan plan = builder.Plan(doc, opt);
            if (!plan.SurfacesFound)
                return "Nenhuma superfície encontrada na peça. Selecione a superfície de queima copiada (ou deixe-a como " +
                       "superfície de construção) e chame de novo.";

            int blank = ReadInt(argsJson, "blank", 0, 0, 1000);
            if (blank > plan.EligibleBlanks.Count)
                return $"Blank [{blank}] não existe: há {plan.EligibleBlanks.Count} que servem.\n\n" + DescribeBlanks(plan);
            if (blank > 0)
            {
                opt.ChosenBlank = plan.EligibleBlanks[blank - 1];
                plan = builder.Plan(doc, opt);
            }

            if (ReadBool(argsJson, "apenasPlanejar", false))
                return "PLANO — nada foi modelado.\n\n" + Crlf(plan.Summary()) + "\n\n" + DescribeBlanks(plan) +
                       "\n\nPara construir, chame sem \"apenasPlanejar\" (e com \"blank\": N para fixar uma barra).";

            // Documento FRESCO para o build, como a janela faz: o plano pode ter lido faces.
            dynamic fresh = null; try { fresh = app.ActiveDocument; } catch { }
            BlockOverSurfacesResult r = builder.Build(fresh ?? doc, opt, preview: false);

            var sb = new StringBuilder();
            sb.AppendLine(Crlf(r.Plan != null ? r.Plan.Summary() : plan.Summary()));
            sb.AppendLine();
            sb.AppendLine($"bloco: {YesNo(r.BlockCreated)}   faixa: {YesNo(r.BandCreated)}   fixação: {YesNo(r.FixationApplied)}   " +
                          $"superfícies unidas: {YesNo(r.SurfacesUnited)}");
            if (r.SwitchedToOrdered) sb.AppendLine("A finalização deixou a peça em ORDENADO (é o comportamento do botão 'OK').");
            foreach (string w in r.Warnings) sb.AppendLine("ATENÇÃO: " + w);
            sb.Append("Confira no modelo. Detalhe no log ('se_log').");
            return sb.ToString();
        }

        private static string DescribeBlanks(BlockOverSurfacesPlan plan)
        {
            if (plan.EligibleBlanks.Count == 0) return "Nenhuma barra do catálogo serve — o bloco sai do tamanho da pegada (comprar material).";
            var sb = new StringBuilder();
            sb.AppendLine("Blanks que servem (\"blank\": N; 0 = automático, o mais compacto = [1]):");
            for (int i = 0; i < plan.EligibleBlanks.Count; i++)
                sb.AppendLine($"  [{i + 1}] {plan.EligibleBlanks[i].Describe()}");
            return sb.ToString().TrimEnd();
        }

        private static string UniteSurfaces(dynamic doc)
        {
            BlockOverSurfacesResult res = new SurfaceBlockBuilder().UniteSurfacesToBlock(doc, new BlockOverSurfacesOptions());
            var sb = new StringBuilder();
            if (res.SideGapsPatched > 0) sb.AppendLine($"{res.SideGapsPatched} vão(s) lateral(is) fechado(s) automaticamente com 'Limite'.");
            sb.AppendLine(res.SurfacesUnited
                ? "Superfície UNIDA ao bloco. Próximo passo: trocar para ORDENADO e 'se_aplicar_gap'."
                : "NÃO uniu as superfícies ao bloco. O bloco, a faixa e os furos NÃO foram tocados. O ponto exato está no " +
                  "log (linhas 'Unir:'); a alternativa é o usuário unir à mão e seguir para 'se_aplicar_gap'.");
            foreach (string w in res.Warnings) sb.AppendLine("ATENÇÃO: " + w);
            return sb.ToString();
        }

        /// <summary>"Aplicar GAP": o Ra que a janela pergunta vem como argumento. Sem ele, vale o
        /// Ra gravado na peça — o mesmo que a janela deixaria pré-selecionado.</summary>
        private static string ApplyGap(dynamic doc, string argsJson)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            ElectrodeParams p = cfg.ToElectrodeParams();
            IReadOnlyList<RaGapPresets.Choice> choices = RaGapPresets.All(p.Material, cfg.BuildColorMap(), cfg.BuildOffsetPolicy());

            double ra = ReadDouble(argsJson, "ra", double.NaN, 0.0, 1000.0);
            string origin = "pedido";
            if (double.IsNaN(ra))
            {
                double stored;
                if (!RaVariableStore.TryRead(doc, out stored))
                    return "Diga o Ra (\"ra\": número em µm) — a peça não tem Ra gravado para usar de padrão.\n\n" + DescribeChoices(choices);
                ra = stored;
                origin = "gravado na peça";
            }

            RaGapPresets.Choice chosen = RaGapPresets.ClosestTo(ra, p.Material, cfg.BuildColorMap(), cfg.BuildOffsetPolicy());
            if (chosen == null)
                return $"Ra {F(ra)} não está na tabela.\n\n" + DescribeChoices(choices);

            Log.Info($"Aplicar GAP (MCP): {chosen.Label} (Ra {origin}).");
            BlockOverSurfacesResult res = new SurfaceBlockBuilder().ApplyGapToUnitedSurfaces(doc, chosen);
            return res.SurfacesOffset
                ? $"GAP {chosen.GapMm:0.00} mm (Ra {chosen.Ra:0.0}, {origin}) aplicado + cor + nome da feature. Confira no modelo."
                : "NÃO aplicou o GAP. " + (res.Warnings.Count > 0 ? res.Warnings[0] : "Veja o log (linhas 'Aplicar GAP:').");
        }

        private static string DescribeChoices(IReadOnlyList<RaGapPresets.Choice> choices)
        {
            var sb = new StringBuilder("Combinações da tabela:\n");
            foreach (RaGapPresets.Choice c in choices) sb.AppendLine("  " + c.Label);
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ O'ring

        /// <summary>Um alojamento a criar — o GroovePlan da janela, sem a janela.</summary>
        private sealed class GroovePlan
        {
            public int EdgeIndex;
            public ORingTarget Target;
            public ORingGrooveSpec Spec;
            public double SealingDiameterMm;
            public double SectionMm;
            public IReadOnlyList<ORingCandidate> Candidates = new List<ORingCandidate>();
            public string Problem;
            public bool Ready { get { return Problem == null && Spec != null && Target != null && Target.Ok; } }
        }

        /// <summary>
        /// "Alojamento de O'ring" sem a janela. A janela coleta a aresta com um clique no modelo;
        /// um agente não clica, e a ferramenta Selecionar da SE normalmente NÃO deixa selecionar
        /// aresta. Então a entrada é só a FACE selecionada: as arestas circulares dela são
        /// listadas, numeradas, e 'arestas' escolhe quais viram alojamento. O cálculo é o mesmo
        /// da janela (ORingGrooveCalculator), e o corte é o mesmo ORingGrooveModeler.Cut.
        /// </summary>
        private static string ORingGroove(dynamic app, dynamic doc, string argsJson)
        {
            List<object> selected = SelectedObjects(doc);
            object face = selected.FirstOrDefault(o => KindOf(o) == "face");
            if (face == null)
                return "Selecione a FACE de vedação na peça (cilíndrica = eixo/furo, plana = vedação de face) e chame de novo. " +
                       "As arestas circulares dela são listadas aqui — não é preciso selecionar aresta.";

            // Arestas: as que vieram selecionadas junto (quando a SE deixou), senão as da face.
            var edges = new List<object>(selected.Where(o => KindOf(o) == "edge"));
            bool fromFace = edges.Count == 0;
            if (fromFace)
            {
                dynamic col = null; try { col = ((dynamic)face).Edges; } catch { }
                int n = 0; try { n = (int)col.Count; } catch { }
                for (int i = 1; i <= n; i++) { try { edges.Add((object)col.Item(i)); } catch { } }
            }

            // Só as arestas que o leitor aceita (circulares, coerentes com a face) contam.
            var circles = new List<GroovePlan>();
            for (int i = 0; i < edges.Count; i++)
            {
                ORingTarget t = ORingTargetReader.Read(face, edges[i]);
                if (t == null || !t.Ok) continue;
                if (circles.Any(c => SameCircle(c.Target, t))) continue;
                circles.Add(new GroovePlan { EdgeIndex = circles.Count + 1, Target = t });
            }
            if (circles.Count == 0)
                return "Nenhuma aresta circular legível " + (fromFace ? "nesta face" : "na seleção") +
                       ". A face selecionada é mesmo a de vedação?";

            ORingCatalog catalog = ORingCatalog.LoadOrCreateDefault();
            bool metric = ReadBool(argsJson, "metricos", false);
            if (metric) catalog = ORingCatalog.Merge(catalog, ORingCatalog.LoadOrCreateMetric());
            ORingTarget first = circles[0].Target;
            GrooveKind kind = ParseKind(ReadString(argsJson, "tipo"), first.SuggestedKind);
            SealMotion motion = ParseMotion(ReadString(argsJson, "vedacao"));
            Elastomer rubber = (ReadString(argsJson, "elastomero") ?? "").Trim().ToLowerInvariant().StartsWith("fkm")
                ? Elastomer.Fkm : Elastomer.Nbr;
            bool faceKind = kind == GrooveKind.AxialFace;
            FacePressure pressure = (ReadString(argsJson, "pressao") ?? "").Trim().ToLowerInvariant().StartsWith("ext")
                ? FacePressure.External : FacePressure.Internal;
            // Mesmos padrões e piso da janela: 5 mm axiais num canal de eixo/furo, 1 mm de parede num de face.
            double offset = ReadDouble(argsJson, "afastamentoMm", faceKind ? 1.0 : 5.0, faceKind ? 1.0 : 0.0, 500.0);
            double fixedSection = ReadDouble(argsJson, "secaoMm", double.NaN, 0.5, 50.0);
            if (!double.IsNaN(fixedSection))
                fixedSection = catalog.CrossSections.OrderBy(s => Math.Abs(s - fixedSection)).FirstOrDefault();

            List<int> wanted = ReadIntList(argsJson, "arestas");
            List<GroovePlan> plans = wanted.Count == 0
                ? (circles.Count == 1 ? circles : new List<GroovePlan>())
                : circles.Where(c => wanted.Contains(c.EdgeIndex)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine((first.FaceIsCylindrical ? $"Face cilíndrica Ø {F(first.FaceDiameterMm)} mm" : "Face plana") +
                          $" — canal {KindName(kind)}, vedação {MotionName(motion)}, {(rubber == Elastomer.Fkm ? "FKM" : "NBR")}, " +
                          (faceKind ? $"parede {F(offset)} mm, pressão {(pressure == FacePressure.Internal ? "interna (anel apoia no Ø externo)" : "externa (anel apoia no Ø interno)")}"
                                    : $"centro do canal a {F(offset)} mm da aresta, no eixo") + ".");
            sb.AppendLine();
            sb.AppendLine($"Arestas circulares{(fromFace ? " da face" : " selecionadas")}:");
            foreach (GroovePlan c in circles)
                sb.AppendLine($"  [{c.EdgeIndex}] Ø {F(c.Target.EdgeDiameterMm)}  centro ({F(c.Target.CenterMm[0])}, " +
                              $"{F(c.Target.CenterMm[1])}, {F(c.Target.CenterMm[2])})  eixo {c.Target.AxisName}");

            if (plans.Count == 0)
            {
                sb.AppendLine();
                sb.Append(wanted.Count == 0
                    ? "Há mais de uma aresta: diga quais viram alojamento com \"arestas\": [N, ...]."
                    : "Nenhum dos números pedidos em \"arestas\" está na lista acima.");
                return sb.ToString();
            }

            string ring = ReadString(argsJson, "anel");
            foreach (GroovePlan plan in plans)
            {
                ORingTarget t = plan.Target;
                plan.SectionMm = !double.IsNaN(fixedSection) ? fixedSection
                    : ORingGrooveCalculator.SuggestCrossSection(t.SealingDiameterMm, catalog.CrossSections);
                plan.SealingDiameterMm = faceKind
                    ? ORingGrooveCalculator.FaceSealingDiameter(t.EdgeDiameterMm, offset, plan.SectionMm, motion, rubber, t.FaceGrooveOutward)
                    : t.SealingDiameterMm;
                plan.Candidates = ORingGrooveCalculator.Rank(catalog, kind, plan.SealingDiameterMm, motion, rubber, plan.SectionMm, pressure,
                    metric && double.IsNaN(fixedSection) ? 0.30 : 0.0);
                if (plan.Candidates.Count == 0)
                {
                    double ideal = ORingGrooveCalculator.IdealInnerDiameter(kind, plan.SealingDiameterMm, plan.SectionMm, motion, rubber, pressure);
                    plan.Problem = $"nenhum anel no catálogo para a seção {plan.SectionMm:0.00} mm (o ideal seria d1 ≈ {ideal:0.00} × d2 {plan.SectionMm:0.00}).";
                    continue;
                }
                ORingCandidate pick = plan.Candidates[0];
                // Anel à mão só com UMA aresta — com várias, cada uma leva o melhor para o diâmetro dela, como na janela.
                if (!string.IsNullOrWhiteSpace(ring) && plans.Count == 1)
                {
                    pick = plan.Candidates.FirstOrDefault(c => string.Equals(c.Ring.Designation.Trim(), ring.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (pick == null) { plan.Problem = $"o anel '{ring}' não está entre os que servem a esta aresta."; continue; }
                }
                plan.Spec = pick.Spec;
            }

            foreach (GroovePlan plan in plans)
            {
                sb.AppendLine();
                if (!plan.Ready) { sb.AppendLine($"  ✗ [{plan.EdgeIndex}] Ø {F(plan.Target.EdgeDiameterMm)}: {plan.Problem}"); continue; }
                ORingGrooveSpec spec = plan.Spec;
                string flag = !spec.IsWithinStandard ? "✗ FORA DA NORMA" : spec.HasWarnings ? "! com avisos" : "✓";
                sb.AppendLine($"  [{plan.EdgeIndex}] Ø {F(plan.Target.EdgeDiameterMm)} → {spec.Ring.Designation}   {flag}");
                sb.AppendLine(Indent(Crlf(spec.Describe()), "    "));
                if (faceKind)
                {
                    double land = ORingGrooveCalculator.FaceGrooveWall(spec, plan.Target.EdgeDiameterMm, plan.Target.FaceGrooveOutward);
                    sb.AppendLine($"    parede entre a aresta e o canal: {land:0.00} mm" + (land < 0 ? "  — o canal INVADE a aresta!" : ""));
                }
                if (plans.Count == 1)
                {
                    sb.AppendLine("    anéis que servem (\"anel\": designação), do melhor para o pior:");
                    foreach (ORingCandidate c in plan.Candidates.Take(10))
                        sb.AppendLine($"      {(c.Spec.IsWithinStandard ? (c.Spec.HasWarnings ? "!" : "✓") : "✗")} {c.Ring.Designation}" +
                                      (c.Ring.Verified ? "" : "  (medida não conferida no fornecedor)"));
                }
            }

            List<GroovePlan> ready = plans.Where(pl => pl.Ready).ToList();
            if (ReadBool(argsJson, "apenasPlanejar", true) || ready.Count == 0)
            {
                sb.AppendLine();
                sb.Append(ready.Count == 0
                    ? "Nada pronto para cortar."
                    : "PLANO — nada foi cortado. Mostre ao usuário e, com o de acordo dele, chame de novo com \"apenasPlanejar\": false.");
                return sb.ToString();
            }

            // O Carlos pediu AVISAR em vez de recusar; sem janela, o "sim, criar assim mesmo" é um argumento.
            List<GroovePlan> offSpec = ready.Where(pl => !pl.Spec.IsWithinStandard).ToList();
            if (offSpec.Count > 0 && !ReadBool(argsJson, "aceitarForaDaNorma", false))
            {
                sb.AppendLine();
                sb.Append($"NADA foi cortado: {offSpec.Count} alojamento(s) FORA da norma. Mostre ao usuário; se ele quiser assim " +
                          "mesmo, chame com \"aceitarForaDaNorma\": true.");
                return sb.ToString();
            }

            var done = new List<string>();
            var failed = new List<string>();
            Log.Info($"===== ALOJAMENTO DE O'RING (MCP, {ready.Count} aresta(s)) =====");
            foreach (GroovePlan plan in ready)
            {
                string label = $"[{plan.EdgeIndex}] Ø {F(plan.Target.EdgeDiameterMm)} → {plan.Spec.Ring.Designation}";
                try
                {
                    Log.Info("--- aresta " + label + " ---");
                    Log.Info(plan.Spec.Describe());
                    string featureName;
                    bool ok = ORingGrooveModeler.Cut((object)app, plan.Target, plan.Spec, offset, out featureName);
                    (ok ? done : failed).Add(label + (ok && featureName != null ? $"  (feature \"{featureName}\")" : ""));
                }
                catch (Exception ex)
                {
                    Log.Error($"Alojamento de O'ring (MCP): aresta {label} falhou.", ex);
                    failed.Add($"{label} ({ex.GetBaseException().Message})");
                }
            }
            Log.Info($"===== FIM (ALOJAMENTO DE O'RING, MCP) — {done.Count} criado(s), {failed.Count} falha(s) =====");
            if (offSpec.Count > 0) Log.Warn("Alojamento de O'ring: criado FORA DA NORMA a pedido do usuário (via agente).");

            sb.AppendLine();
            foreach (string d in done) sb.AppendLine("  ✓ criado " + d);
            foreach (string f in failed) sb.AppendLine("  ✗ falhou " + f);
            if (done.Count > 0)
                sb.Append("O raio de fundo e a quebra de canto NÃO são cortados pelo AutoEDM — estão no relatório acima.");
            return sb.ToString();
        }

        /// <summary>Mesmo critério da janela: dois cliques no mesmo furo não viram dois cortes.</summary>
        private static bool SameCircle(ORingTarget a, ORingTarget b)
        {
            if (a == null || b == null || !a.Ok || !b.Ok) return false;
            if (a.AxisIndex != b.AxisIndex) return false;
            if (Math.Abs(a.EdgeDiameterMm - b.EdgeDiameterMm) > 0.005) return false;
            for (int i = 0; i < 3; i++)
                if (Math.Abs(a.CenterMm[i] - b.CenterMm[i]) > 0.01) return false;
            return true;
        }

        private static GrooveKind ParseKind(string s, GrooveKind suggested)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("eixo")) return GrooveKind.RadialExternal;
            if (s.StartsWith("furo")) return GrooveKind.RadialInternal;
            if (s.StartsWith("face")) return GrooveKind.AxialFace;
            return suggested;
        }

        private static SealMotion ParseMotion(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("rec")) return SealMotion.Reciprocating;
            if (s.StartsWith("rot")) return SealMotion.Rotary;
            return SealMotion.Static;
        }

        private static string KindName(GrooveKind k) =>
            k == GrooveKind.AxialFace ? "de FACE" : k == GrooveKind.RadialExternal ? "de EIXO (externo)" : "de FURO (interno)";

        private static string MotionName(SealMotion m) =>
            m == SealMotion.Reciprocating ? "recíproca" : m == SealMotion.Rotary ? "rotativa" : "estática";

        // ------------------------------------------------------------------ diagnóstico

        /// <summary>A sonda de malha só LÊ — menos o teste de seccionamento, que cria esboços.
        /// Por isso a ferramenta é de leitura e a chave de escrita é conferida só para ele.</summary>
        private string MeshProbeTool(dynamic doc, string argsJson)
        {
            bool section = ReadBool(argsJson, "seccionamento", false);
            if (section && _mode() != BridgeMode.Write)
                return "RECUSADO: o teste de SECCIONAMENTO cria esboços na peça, e a ponte está em SOMENTE-LEITURA. " +
                       "Chame sem \"seccionamento\" para a sonda só de leitura, ou peça ao usuário para clicar em " +
                       "\"Liberar escrita\" no grupo MCP da ribbon.";

            MeshProbeResult res = MeshProbe.Run((object)doc, allowSectionTest: section);
            return (res.FoundMesh ? "" : "Nenhuma malha encontrada nesta peça.\n\n") + res.Summary +
                   "\n\nO detalhe inteiro está no log ('se_log').";
        }

        private static string InterPartProbeTool(dynamic app, dynamic doc, List<string> captured)
        {
            AutoEDM.Experiments.InterPartProbe.Run(SolidEdgeConnector.Attach(app), doc);
            return "Sonda inter-part concluída (peças descartáveis, montagem não salva). O que ela registrou:\n\n" + Tail(captured, 400);
        }

        /// <summary>A sonda de rosca cria a PRÓPRIA peça; a exibição de rosca é opção do Solid
        /// Edge INTEIRO, então só é ligada com o pedido explícito.</summary>
        private static string ThreadProbeTool(dynamic app, string argsJson)
        {
            bool displayOff = ThreadProbe.ThreadedDisplayIsOff(app);
            ThreadProbe.RunM6Matrix(app);

            var sb = new StringBuilder();
            sb.AppendLine("Sonda de rosca concluída numa peça NOVA (não salva). Furos M6 em X = -42, -14, +14, +42 mm:");
            sb.AppendLine("  1 (-42): base de furos + rosca só como anotação");
            sb.AppendLine("  2 (-14): base de furos + hélice pedida na CRIAÇÃO");
            sb.AppendLine("  3 (+14): base de furos + hélice pedida DEPOIS do furo");
            sb.AppendLine("  4 (+42): valores na mão, sem a base de furos");
            sb.AppendLine("O Ø real medido de cada furo está no log — o esperado é 5,0 mm (broca de M6).");
            if (displayOff)
            {
                if (ReadBool(argsJson, "ligarExibicaoRosca", false))
                    sb.AppendLine(ThreadProbe.EnableThreadedDisplay(app)
                        ? "A opção GLOBAL \"exibir roscas\" foi LIGADA, como pedido."
                        : "Tentei ligar a opção GLOBAL \"exibir roscas\" e não consegui — veja o log.");
                else
                    sb.AppendLine("ATENÇÃO: a opção GLOBAL \"exibir roscas\" está DESLIGADA — rosca certa aparece lisa na tela. " +
                                  "Para ligar (muda o Solid Edge inteiro), chame com \"ligarExibicaoRosca\": true.");
            }
            sb.Append("Peça ao usuário para olhar a peça e dizer qual furo saiu com a hélice CORTADA.");
            return sb.ToString();
        }

        private string RecorderStart(dynamic doc)
        {
            _recBaseline = ComDiagnostics.SnapshotInventory((object)doc);
            return DocWarning(doc) +
                   "Gravação INICIADA. Peça ao usuário para fazer a ação manual no Solid Edge (criar 'Limite', costurar, " +
                   "estender/mover a face...) e, quando ele terminar, chame 'se_gravador_gravar'.";
        }

        private string RecorderSave(dynamic doc, List<string> captured)
        {
            if (_recBaseline == null)
                return "Não há gravação iniciada. Chame 'se_gravador_iniciar' ANTES da ação manual.";
            ComDiagnostics.DumpNewSince((object)doc, _recBaseline);
            _recBaseline = null;   // consome: um segundo "gravar" não compara contra estado velho
            return DocWarning(doc) + "O que mudou desde o início da gravação:\n\n" + Tail(captured, 600);
        }

        /// <summary>O gravador quase sempre registra ação numa PEÇA; com a montagem em foco o
        /// diff sai do documento errado. Avisa em vez de perguntar.</summary>
        private static string DocWarning(dynamic doc)
        {
            int type = -1; try { type = (int)doc.Type; } catch { }
            if (type == 1) return "";
            return $"ATENÇÃO: o documento ativo é {(type == 3 ? "uma MONTAGEM" : type == 2 ? "um DESENHO" : $"tipo {type}")}, " +
                   "não uma peça — se a ação for na peça, a janela dela precisa estar em foco.\n\n";
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Itens da seleção, desembrulhados: em montagem a face vem dentro de um
        /// wrapper com <c>.Object</c>.</summary>
        private static List<object> SelectedObjects(dynamic doc)
        {
            var list = new List<object>();
            dynamic ss = null; try { ss = doc.SelectSet; } catch { }
            int n = 0; try { n = (int)ss.Count; } catch { }
            for (int i = 1; i <= n; i++)
            {
                object item = null; try { item = ss.Item(i); } catch { }
                if (item == null) continue;
                object inner = null; try { inner = ((dynamic)item).Object; } catch { }
                list.Add(inner ?? item);
            }
            return list;
        }

        /// <summary>"face", "edge" ou null, pelo nome do tipo COM.</summary>
        private static string KindOf(object com)
        {
            string t = ComTypeName(com) ?? "";
            if (t.IndexOf("Edge", StringComparison.OrdinalIgnoreCase) >= 0) return "edge";
            if (t.IndexOf("Face", StringComparison.OrdinalIgnoreCase) >= 0) return "face";
            return null;
        }

        private static string Tail(List<string> captured, int max)
        {
            lock (captured)
            {
                if (captured.Count == 0) return "(o log desta chamada saiu vazio)";
                var sb = new StringBuilder();
                if (captured.Count > max) sb.AppendLine($"... ({captured.Count - max} linha(s) anteriores cortadas — 'se_log' traz tudo)");
                foreach (string l in captured.Skip(Math.Max(0, captured.Count - max))) sb.AppendLine(l);
                return sb.ToString();
            }
        }

        private static string ReadString(string argsJson, string name)
        {
            if (string.IsNullOrWhiteSpace(argsJson)) return null;
            try
            {
                using (JsonDocument d = JsonDocument.Parse(argsJson))
                {
                    JsonElement el;
                    if (!d.RootElement.TryGetProperty(name, out el)) return null;
                    if (el.ValueKind == JsonValueKind.String) return el.GetString();
                    if (el.ValueKind == JsonValueKind.Number) return el.GetRawText();
                }
            }
            catch { }
            return null;
        }

        private static List<int> ReadIntList(string argsJson, string name)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(argsJson)) return list;
            try
            {
                using (JsonDocument d = JsonDocument.Parse(argsJson))
                {
                    JsonElement el;
                    if (!d.RootElement.TryGetProperty(name, out el)) return list;
                    if (el.ValueKind == JsonValueKind.Number) { int one; if (el.TryGetInt32(out one)) list.Add(one); return list; }
                    if (el.ValueKind != JsonValueKind.Array) return list;
                    foreach (JsonElement it in el.EnumerateArray())
                    {
                        int v;
                        if (it.ValueKind == JsonValueKind.Number && it.TryGetInt32(out v)) list.Add(v);
                        else if (it.ValueKind == JsonValueKind.String && int.TryParse(it.GetString(), out v)) list.Add(v);
                    }
                }
            }
            catch { }
            return list;
        }

        private static string Crlf(string s) => (s ?? "").Replace("\r\n", "\n");

        private static string Indent(string s, string pad) =>
            pad + (s ?? "").TrimEnd().Replace("\n", "\n" + pad);

        private static string YesNo(bool b) => b ? "sim" : "não";
    }
}
