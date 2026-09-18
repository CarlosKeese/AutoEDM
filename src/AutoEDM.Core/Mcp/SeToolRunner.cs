using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AutoEDM.Com;
using AutoEDM.Config;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Selection;
using AutoEDM.Wedm;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// Executa as ferramentas do <see cref="ToolCatalog"/> contra a Solid Edge viva.
    ///
    /// THREAD: todo método público daqui PRESSUPÕE estar na thread STA da Solid Edge. Quem
    /// garante isso é o hospedeiro (o add-in marshala do thread do pipe para a thread da SE
    /// antes de chamar). Chamar COM da thread do pipe atravessaria apartamento e renderia
    /// RPC_E_* aleatório — o mesmo tipo de erro que o OleMessageFilter existe para tratar na
    /// GUI externa.
    /// </summary>
    public sealed class SeToolRunner
    {
        private readonly Func<object> _app;
        private readonly Func<BridgeMode> _mode;
        private readonly Func<string> _logPath;

        public SeToolRunner(Func<object> appProvider, Func<BridgeMode> modeProvider, Func<string> logPathProvider)
        {
            _app = appProvider ?? throw new ArgumentNullException(nameof(appProvider));
            _mode = modeProvider ?? throw new ArgumentNullException(nameof(modeProvider));
            _logPath = logPathProvider ?? (() => null);
        }

        /// <summary>Documento/ambiente que cada ferramenta exige — mesma ideia da tabela
        /// <c>Specs</c> da ribbon. Ferramenta fora desta tabela não exige documento nenhum.</summary>
        private static readonly Dictionary<string, Requirement> Requirements =
            new Dictionary<string, Requirement>(StringComparer.OrdinalIgnoreCase)
            {
                { "se_inspecionar_selecao",   new Requirement(DocKind.Any,      ModelingEnv.Any) },
                { "se_arvore",               new Requirement(DocKind.Any,      ModelingEnv.Any) },
                { "se_medir_selecao",        new Requirement(DocKind.Any,      ModelingEnv.Any) },
                { "se_analisar_z",           new Requirement(DocKind.Assembly, ModelingEnv.Any) },
                { "se_coordenadas",          new Requirement(DocKind.Assembly, ModelingEnv.Any) },
                { "se_curvas_superficies",   new Requirement(DocKind.Part,     ModelingEnv.Synchronous) },
                { "se_exportar_perfis_wedm", new Requirement(DocKind.Part,     ModelingEnv.Synchronous) },
            };

        private enum DocKind { None = 0, Any = -1, Part = 1, Assembly = 3 }

        private sealed class Requirement
        {
            public readonly DocKind Kind;
            public readonly ModelingEnv Env;
            public Requirement(DocKind kind, ModelingEnv env) { Kind = kind; Env = env; }
        }

        /// <summary>
        /// Roda uma ferramenta e devolve texto para o agente. NUNCA levanta exceção: uma
        /// exceção que subisse daqui morreria na thread da SE, e o agente veria só o pipe
        /// fechar sem motivo.
        /// </summary>
        public BridgeResponse Execute(BridgeRequest req)
        {
            if (req == null) return BridgeResponse.Bad(0, "Pedido vazio.");

            ToolSpec spec = ToolCatalog.Find(req.Tool);
            if (spec == null)
                return BridgeResponse.Bad(req.Id, $"Ferramenta desconhecida: '{req.Tool}'.");

            // A chave de escrita. Recusa ANTES de tocar no documento: o agente recebe o
            // motivo e o que fazer, em vez de um erro genérico.
            if (spec.Writes && _mode() != BridgeMode.Write)
                return BridgeResponse.Bad(req.Id,
                    $"RECUSADO: '{spec.Name}' ALTERA o modelo ou grava arquivo, e a ponte está em SOMENTE-LEITURA.\n\n" +
                    "A ponte nasce em somente-leitura a cada sessão da Solid Edge, de propósito. Para liberar, o Carlos " +
                    "precisa clicar em \"Liberar escrita\" no grupo MCP da ribbon do AutoEDM. Peça isso a ele — não há " +
                    "como um agente ligar essa chave.");

            try
            {
                // O log é a saída de verdade de várias ferramentas (a introspecção grava tudo
                // nele). Captura o que ESTA chamada gerou e devolve junto, senão o agente
                // receberia "pronto, veja o log" — e não tem como ver o log de outra máquina.
                var captured = new List<string>();
                Action<LogLevel, string> sink = (lvl, msg) =>
                {
                    lock (captured) { if (captured.Count < 4000) captured.Add(lvl == LogLevel.Info ? msg : $"[{lvl}] {msg}"); }
                };

                Log.OnMessage += sink;
                string text;
                try { text = Dispatch(spec, req.ArgsJson, captured); }
                finally { Log.OnMessage -= sink; }

                return BridgeResponse.Good(req.Id, text);
            }
            catch (Exception ex)
            {
                Log.Error($"MCP: ferramenta '{spec.Name}' falhou.", ex);
                return BridgeResponse.Bad(req.Id,
                    $"A ferramenta '{spec.Name}' falhou: {ex.GetBaseException().Message}\n\n" +
                    "O rastro completo está no log do add-in (chame 'se_log').");
            }
        }

        private string Dispatch(ToolSpec spec, string argsJson, List<string> captured)
        {
            dynamic app = _app();
            if (app == null)
                return "Add-in não inicializado (sem Application da Solid Edge). A SE está aberta com o AutoEDM carregado?";

            if (string.Equals(spec.Name, "se_status", StringComparison.OrdinalIgnoreCase)) return Status(app);
            if (string.Equals(spec.Name, "se_log", StringComparison.OrdinalIgnoreCase)) return TailLog(argsJson);

            // Daqui para baixo tudo exige documento: confere o pré-requisito declarado.
            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null) return "Nenhum documento ativo na Solid Edge. Abra a peça ou a montagem e chame de novo.";

            string refusal = CheckRequirement(spec.Name, doc);
            if (refusal != null) return refusal;

            switch (spec.Name.ToLowerInvariant())
            {
                case "se_inspecionar_selecao": return Inspect(doc, argsJson, captured);
                case "se_arvore": return Tree(doc);
                case "se_medir_selecao": return Measure(doc);
                case "se_analisar_z": return AnalyzeZ(app, doc);
                case "se_coordenadas": return Coordinates(app, doc);
                case "se_curvas_superficies": return SurfaceRimCurves(doc, captured);
                case "se_exportar_perfis_wedm": return ExportWedm(doc, captured);
            }
            return $"Ferramenta '{spec.Name}' está no catálogo mas não tem implementação — isto é um defeito do AutoEDM.";
        }

        /// <summary>Mesma regra dos botões: o tipo de documento e o ambiente de modelagem são
        /// pré-requisito, e o AutoEDM NUNCA troca o ambiente sozinho.</summary>
        private static string CheckRequirement(string tool, dynamic doc)
        {
            Requirement req;
            if (!Requirements.TryGetValue(tool, out req)) return null;

            if (req.Kind != DocKind.Any && req.Kind != DocKind.None)
            {
                int type = -1; try { type = (int)doc.Type; } catch { }
                if (type != (int)req.Kind)
                {
                    string wanted = req.Kind == DocKind.Assembly ? "uma MONTAGEM (.asm)" : "uma PEÇA (.par)";
                    string got = type == 1 ? "uma peça" : type == 3 ? "uma montagem" : type == 2 ? "um desenho" : $"tipo {type}";
                    return $"RECUSADO: '{tool}' exige {wanted} ativa, e o documento ativo é {got}. " +
                           "Troque a janela em foco no Solid Edge e chame de novo.";
                }
            }

            ModelingEnv actual = ModelingEnvironment.Read(doc);
            if (!ModelingEnvironment.Matches(req.Env, actual))
                return $"RECUSADO: '{tool}' exige modelagem {ModelingEnvironment.Name(req.Env)}, e a peça está em " +
                       $"{ModelingEnvironment.Name(actual)}.\n\n" +
                       "O AutoEDM nunca troca o ambiente de modelagem sozinho — a troca reconstrói o corpo e mata as faces " +
                       "já lidas. Quem troca é o usuário, na barra de status do Solid Edge.";
            return null;
        }

        // ------------------------------------------------------------------ ferramentas

        private string Status(dynamic app)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Solid Edge — estado agora");
            sb.AppendLine(Str(() => "  Versão: " + (string)app.Version, "  Versão: (não lida)"));
            sb.AppendLine($"  Modo da ponte MCP: {(_mode() == BridgeMode.Write ? "ESCRITA LIBERADA" : "SOMENTE-LEITURA")}");

            int docs = -1; try { docs = (int)app.Documents.Count; } catch { }
            sb.AppendLine($"  Documentos abertos: {(docs < 0 ? "(não lido)" : docs.ToString(CultureInfo.InvariantCulture))}");

            dynamic doc = null; try { doc = app.ActiveDocument; } catch { }
            if (doc == null)
            {
                sb.AppendLine("  Documento ATIVO: nenhum.");
                sb.AppendLine();
                sb.Append("Sem documento ativo só 'se_status' e 'se_log' funcionam. Abra a peça ou a montagem no SE.");
                return sb.ToString();
            }

            int type = -1; try { type = (int)doc.Type; } catch { }
            string kind = type == 1 ? "PEÇA (.par)" : type == 3 ? "MONTAGEM (.asm)" : type == 2 ? "DESENHO (.dft)" : $"tipo {type}";
            sb.AppendLine($"  Documento ATIVO: {Str(() => (string)doc.Name, "(sem nome)")}  —  {kind}");
            sb.AppendLine(Str(() => "  Caminho: " + (string)doc.FullName, "  Caminho: (não salvo ainda)"));

            ModelingEnv env = ModelingEnvironment.Read(doc);
            sb.AppendLine($"  Modelagem: {ModelingEnvironment.Name(env)}");

            int sel = -1; try { sel = (int)doc.SelectSet.Count; } catch { }
            sb.AppendLine($"  Selecionado(s): {(sel < 0 ? "(não lido)" : sel.ToString(CultureInfo.InvariantCulture))}");

            sb.AppendLine();
            sb.Append("Lembre: 'se_analisar_z' e 'se_coordenadas' pedem MONTAGEM; as ferramentas de WEDM pedem PEÇA em SÍNCRONO.");
            return sb.ToString();
        }

        private static string Inspect(dynamic doc, string argsJson, List<string> captured)
        {
            int depth = ReadInt(argsJson, "profundidade", 1, 0, 3);

            dynamic ss = null; try { ss = doc.SelectSet; } catch { }
            int n = 0; try { n = (int)ss.Count; } catch { }
            if (n == 0)
                return "Nada selecionado no Solid Edge. Selecione no modelo a feature, face, aresta, superfície ou ocorrência " +
                       "que você quer conhecer e chame de novo.\n\n" +
                       "Dica de seleção de FACE: clique na peça e clique DE NOVO no mesmo ponto (ou segure Alt) — é o " +
                       "comportamento nativo de 2 cliques do SE, não existe um modo a ligar.";

            Log.Info($"===== MCP: INSPECIONAR SELEÇÃO ({n} objeto(s), profundidade {depth}) =====");
            for (int i = 1; i <= n; i++)
            {
                object item; try { item = ss.Item(i); } catch (Exception ex) { Log.Warn($"Seleção[{i}] inacessível: {ex.GetBaseException().Message}"); continue; }
                ComDiagnostics.DumpObject($"Seleção[{i}]", item, depth);
            }
            Log.Info("===== FIM (MCP: INSPECIONAR SELEÇÃO) =====");

            return $"{n} objeto(s) inspecionado(s), profundidade {depth}.\n\n" + Join(captured);
        }

        private static string Tree(dynamic doc)
        {
            int type = -1; try { type = (int)doc.Type; } catch { }
            var sb = new StringBuilder();
            sb.AppendLine($"Árvore de '{Str(() => (string)doc.Name, "(sem nome)")}'");

            if (type == 3) // montagem
            {
                sb.AppendLine();
                sb.AppendLine("Ocorrências:");
                int n = 0; try { n = (int)doc.Occurrences.Count; } catch { }
                if (n == 0) sb.AppendLine("  (nenhuma)");
                for (int i = 1; i <= n && i <= 500; i++)
                {
                    dynamic occ = null; try { occ = doc.Occurrences.Item(i); } catch { continue; }
                    string name = Str(() => (string)occ.Name, "?");
                    string file = Str(() => System.IO.Path.GetFileName((string)occ.OccurrenceFileName), "?");
                    sb.AppendLine($"  [{i}] {name}   ({file})");
                }
                if (n > 500) sb.AppendLine($"  ... e mais {n - 500} (cortado).");
                return sb.ToString();
            }

            // peça. O cast para object é OBRIGATÓRIO: com 'doc' dynamic, a chamada inteira vira
            // dinâmica, e o compilador proíbe passar lambda/delegado anônimo para uma chamada
            // ligada dinamicamente (CS1977).
            object part = doc;
            AppendCollection(sb, part, "Models", "Corpos (Models)", delegate (dynamic m)
            {
                string extra = "";
                try { if ((bool)m.IsFacetBody) extra = "  [CORPO DE FACETAS / MALHA]"; } catch { }
                if (extra.Length == 0) { try { if ((bool)m.IsMixedFacetBody) extra = "  [MISTO: facetas + B-rep]"; } catch { } }
                return Str(() => (string)m.Name, "(sem nome)") + extra;
            });
            AppendCollection(sb, part, "Features", "Features (ordem da árvore)", delegate (dynamic f) { return Str(() => (string)f.Name, "(sem nome)"); });
            AppendCollection(sb, part, "Constructions", "Superfícies de construção", delegate (dynamic c) { return Str(() => (string)c.Name, "(sem nome)"); });
            AppendCollection(sb, part, "DesignEdgebarFeatures", "Curvas/recursos da EdgeBar", delegate (dynamic c) { return Str(() => (string)c.Name, "(sem nome)"); });
            return sb.ToString();
        }

        /// <summary>Uma coleção da peça, best-effort: coleção que não existe nesta versão da SE
        /// simplesmente não aparece, em vez de derrubar a ferramenta inteira.</summary>
        private static void AppendCollection(StringBuilder sb, object doc, string property, string title, Func<dynamic, string> describe)
        {
            dynamic col = null;
            try { col = InvokeByName(doc, property); } catch { }
            if (col == null) return;

            int n = -1; try { n = (int)col.Count; } catch { }
            if (n < 0) return;

            sb.AppendLine();
            sb.AppendLine($"{title}: {n}");
            for (int i = 1; i <= n && i <= 300; i++)
            {
                dynamic item = null; try { item = col.Item(i); } catch { continue; }
                string d; try { d = describe(item); } catch { d = "(indescritível)"; }
                sb.AppendLine($"  [{i}] {d}");
            }
            if (n > 300) sb.AppendLine($"  ... e mais {n - 300} (cortado).");
        }

        private static dynamic InvokeByName(object target, string property) =>
            target.GetType().InvokeMember(property, System.Reflection.BindingFlags.GetProperty, null, target, null);

        private static string Measure(dynamic doc)
        {
            dynamic ss = null; try { ss = doc.SelectSet; } catch { }
            int n = 0; try { n = (int)ss.Count; } catch { }
            if (n == 0) return "Nada selecionado. Selecione face(s), aresta(s) ou corpo(s) no Solid Edge e chame de novo.";

            var sb = new StringBuilder();
            sb.AppendLine($"{n} objeto(s) selecionado(s) — medidas em MILÍMETROS, coordenadas da própria peça.");
            for (int i = 1; i <= n; i++)
            {
                object item; try { item = ss.Item(i); } catch { sb.AppendLine($"  [{i}] inacessível."); continue; }

                string type = Str(() => ComTypeName(item), "?");
                sb.AppendLine();
                sb.AppendLine($"  [{i}] {type}");

                string geom = TryGeometryKind(item);
                if (geom != null) sb.AppendLine($"       geometria: {geom}");

                double[] min, max;
                // Exato primeiro: em aresta B-spline o GetRange devolve caixa INFLADA (±0,005 mm
                // por lado, medido em 2026-09-16) e uma medida assim mente sobre planaridade.
                if (FaceGeometry.TryGetExactRangeMm(item, out min, out max) ||
                    FaceGeometry.TryGetRangeMm(item, out min, out max))
                {
                    sb.AppendLine($"       min: X={F(min[0])}  Y={F(min[1])}  Z={F(min[2])}");
                    sb.AppendLine($"       max: X={F(max[0])}  Y={F(max[1])}  Z={F(max[2])}");
                    sb.AppendLine($"       tamanho: {F(max[0] - min[0])} × {F(max[1] - min[1])} × {F(max[2] - min[2])}");
                }
                else sb.AppendLine("       extensão: não foi possível medir (o objeto não expõe GetRange/GetExactRange).");
            }
            return sb.ToString();
        }

        /// <summary>Tipo de superfície/curva subjacente, quando o objeto expõe Geometry.Type.</summary>
        private static string TryGeometryKind(object com)
        {
            try
            {
                dynamic d = com;
                dynamic g = d.Geometry;
                if (g == null) return null;
                int t = (int)g.Type;
                switch (t)
                {
                    case 1: return "plano";
                    case 2: return "cilindro";
                    case 3: return "cone";
                    case 4: return "esfera";
                    case 5: return "toro";
                    case 6: return "superfície B-spline";
                    default: return $"tipo {t}";
                }
            }
            catch { return null; }
        }

        private static string AnalyzeZ(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            var builder = new ElectrodeBuilder(SolidEdgeConnector.Attach(app),
                offsetPolicy: cfg.BuildOffsetPolicy(), raColorMap: cfg.BuildColorMap());

            ZAnalysisResult res = builder.AnalyzeElectrodesByZ(doc, cfg.ToElectrodeParams());

            var sb = new StringBuilder();
            sb.AppendLine($"{res.Electrodes.Count} eletrodo(s) proposto(s), por nível de Z.");
            sb.AppendLine($"Faces: {res.TotalFaces} no total, {res.FlatFaces} piso, {res.SteepFaces} parede, {res.BurnFaceCount} de queima.");
            sb.AppendLine();
            sb.AppendLine(res.DescribeBurnDetection());

            string mach = res.DescribeMachinability();
            if (!string.IsNullOrWhiteSpace(mach))
            {
                sb.AppendLine();
                sb.AppendLine("USINABILIDADE:");
                sb.AppendLine(mach);
            }
            else sb.AppendLine("\nUsinabilidade: nada reprovado.");

            if (res.HasDominantUnmappedColor)
                sb.AppendLine("\nATENÇÃO: a maior região colorida está numa cor FORA do mapa de queima — a detecção pode estar " +
                              "olhando a cor errada. Confira o mapa de cores no config.json antes de criar eletrodo.");
            return sb.ToString();
        }

        private static string Coordinates(dynamic app, dynamic doc)
        {
            AutoEdmConfig cfg = AutoEdmConfig.LoadOrCreateDefault();
            var builder = new ElectrodeBuilder(SolidEdgeConnector.Attach(app),
                offsetPolicy: cfg.BuildOffsetPolicy(), raColorMap: cfg.BuildColorMap());

            List<ElectrodeListItem> items = builder.ListSelectedElectrodes(doc);
            if (items.Count == 0)
                return "Nenhum eletrodo selecionado. Na montagem, selecione a(s) OCORRÊNCIA(s) do(s) eletrodo(s) " +
                       "(clique na peça, não numa face) e chame de novo.";

            var sb = new StringBuilder();
            sb.AppendLine($"{items.Count} eletrodo(s) selecionado(s) — posições em MILÍMETROS.");
            foreach (ElectrodeListItem it in items)
            {
                sb.AppendLine();
                sb.AppendLine($"  {it.Name}");
                sb.AppendLine(it.PositionKnown
                    ? $"    posição: X={F(it.X)}  Y={F(it.Y)}  Z={F(it.Z)}   giro Z={F(it.AzDeg)}°"
                    : "    posição: NÃO lida (veja as notas)");
                if (it.Ra.HasValue) sb.AppendLine($"    Ra gravado: {F(it.Ra.Value)}   GAP: {(it.GapMm.HasValue ? F(it.GapMm.Value) + " mm" : "(sem GAP)")}");
                else sb.AppendLine("    Ra gravado: nenhum (o eletrodo ainda não passou pelo 'Aplicar GAP')");
                if (it.BurnAreaCm2.HasValue) sb.AppendLine($"    área de queima: {F(it.BurnAreaCm2.Value)} cm²");
                if (it.SectionZMm.HasValue) sb.AppendLine($"    seção em Z: {F(it.SectionZMm.Value)} mm");
                foreach (string note in it.Notes) sb.AppendLine($"    nota: {note}");
            }
            return sb.ToString();
        }

        private static string SurfaceRimCurves(dynamic doc, List<string> captured)
        {
            SurfaceRimResult r = SurfaceRimCurveBuilder.Build((object)doc);
            if (!r.Ok) return "Não criou curva nenhuma: " + r.Message;

            var sb = new StringBuilder();
            sb.AppendLine($"{r.Curves.Count} curva(s) criada(s) a partir de {r.SurfacesRead} superfície(s) — {r.Source}.");
            foreach (RimCurveCreated c in r.Curves)
                sb.AppendLine($"  Z = {c.Label} ({(c.IsTop ? "topo" : "fundo")})  —  {c.EdgeCount} aresta(s)" +
                              (c.Closed ? "" : ", ABERTO") + (c.PieceCount > 1 ? $", em {c.PieceCount} pedaço(s)" : ""));

            if (r.Deleted > 0) sb.AppendLine($"\nSubstituiu {r.Deleted} curva(s) de uma rodada anterior desta mesma ferramenta.");
            if (r.LoopsSuspect > 0) sb.AppendLine($"ATENÇÃO: {r.LoopsSuspect} contorno(s) aberto(s) com as pontas a menos de 1 mm — provavelmente um perfil só que se partiu por folga entre arestas.");
            if (r.LoopsSplit > 0) sb.AppendLine($"ATENÇÃO: {r.LoopsSplit} contorno(s) saíram FATIADOS (uma curva por aresta). O perfil está completo e a exportação não se importa.");
            if (r.RenameFailed) sb.AppendLine("ATENÇÃO: alguma curva não ficou com o nome 'WEDM Z = ...' — a próxima rodada NÃO vai substituí-la, e o perfil sairia duplicado no .igs.");
            if (r.EdgesDropped > 0) sb.AppendLine($"{r.EdgesDropped} aresta(s) horizontal(is) em altura intermediária ficaram de fora (só Z mínimo e máximo viram curva).");
            if (r.SurfacesWithoutRim > 0) sb.AppendLine($"{r.SurfacesWithoutRim} superfície(s) não têm extremidade paralela ao plano XY.");
            if (r.LoopsFailed > 0) sb.AppendLine($"ATENÇÃO: {r.LoopsFailed} contorno(s) não viraram curva.");
            foreach (string w in r.Warnings) sb.AppendLine("ATENÇÃO: " + w);

            sb.AppendLine("\nAs curvas estão na árvore como 'WEDM Z = ...'. Confira antes de exportar com 'se_exportar_perfis_wedm'.");
            return sb.ToString();
        }

        private static string ExportWedm(dynamic doc, List<string> captured)
        {
            WedmExportResult r = WedmProfileExporter.Export((object)doc);
            if (!r.Ok) return "Não exportou nada: " + r.Message;

            var sb = new StringBuilder();
            sb.AppendLine($"{r.Files.Count} arquivo(s) .igs gravado(s).");
            foreach (WedmLevelFile f in r.Files) sb.AppendLine("  " + Describe(f));
            if (r.Read != null) sb.AppendLine($"\nCurvas lidas: {DescribeRead(r.Read)}");
            sb.AppendLine("\nDetalhe por curva no log ('se_log').");
            return sb.ToString();
        }

        // Estes dois ficam tolerantes de propósito: os DTOs do WEDM podem ganhar campo, e uma
        // ferramenta de leitura não deve quebrar por causa de um nome de propriedade.
        private static string Describe(object f)
        {
            try { dynamic d = f; return $"{System.IO.Path.GetFileName((string)d.Path)}  —  {d.CurveCount} curva(s)"; }
            catch { return f == null ? "(nulo)" : f.ToString(); }
        }

        private static string DescribeRead(object read)
        {
            try { dynamic d = read; return $"{d.Curves.Count} curva(s) de construção."; }
            catch { return "(não descrito)"; }
        }

        private string TailLog(string argsJson)
        {
            int want = ReadInt(argsJson, "linhas", 200, 1, 2000);
            string path = _logPath();
            if (string.IsNullOrEmpty(path))
                return "O add-in não abriu arquivo de log nesta sessão (a gravação em arquivo é best-effort).";
            if (!System.IO.File.Exists(path))
                return $"O log ainda não existe em disco: {path}";

            var tail = new LinkedList<string>();
            try
            {
                // Abre com ReadWrite compartilhado: o FileLogSink está com o arquivo ABERTO
                // para escrita, e sem ShareWrite isto daria IOException a cada chamada.
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                           System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var sr = new System.IO.StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        tail.AddLast(line);
                        if (tail.Count > want) tail.RemoveFirst();
                    }
                }
            }
            catch (Exception ex) { return $"Não foi possível ler o log ({path}): {ex.GetBaseException().Message}"; }

            var sb = new StringBuilder();
            sb.AppendLine($"Últimas {tail.Count} linha(s) de {path}:");
            sb.AppendLine();
            foreach (string l in tail) sb.AppendLine(l);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ utilidades

        private static string Join(List<string> captured)
        {
            lock (captured)
            {
                if (captured.Count == 0) return "(o log desta chamada saiu vazio)";
                var sb = new StringBuilder();
                foreach (string l in captured) sb.AppendLine(l);
                return sb.ToString();
            }
        }

        /// <summary>Lê um inteiro do JSON de argumentos, com limites. Argumento ausente,
        /// nulo ou fora de faixa cai no padrão em vez de virar erro.</summary>
        private static int ReadInt(string argsJson, string name, int fallback, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(argsJson)) return fallback;
            try
            {
                using (JsonDocument d = JsonDocument.Parse(argsJson))
                {
                    JsonElement el;
                    if (!d.RootElement.TryGetProperty(name, out el)) return fallback;
                    int v;
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out v))
                        return v < min ? min : v > max ? max : v;
                    // Um agente às vezes manda número como string; aceitar é mais útil que recusar.
                    if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out v))
                        return v < min ? min : v > max ? max : v;
                }
            }
            catch { }
            return fallback;
        }

        private static string ComTypeName(object com)
        {
            string t = ComDiagnostics.TypeNameOf(com);
            if (!string.IsNullOrEmpty(t)) return t;
            // Late-bound sem type info: GetType().Name dá "__ComObject", então avisa em vez de mentir.
            try { return com.GetType().Name; } catch { return "?"; }
        }

        private static string Str(Func<string> read, string fallback)
        {
            try { string s = read(); return string.IsNullOrEmpty(s) ? fallback : s; }
            catch { return fallback; }
        }

        private static string F(double mm) => mm.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
