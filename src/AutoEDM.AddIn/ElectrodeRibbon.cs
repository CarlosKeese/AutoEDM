using System;
using System.Windows.Forms;
using SolidEdgeCommunity.AddIn;
using AutoEDM.AddIn.UI;
using AutoEDM.Com;
using AutoEDM.Config;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Mcp;
using AutoEDM.Model;
using AutoEDM.Reporting;
using AutoEDM.Revisions;
using AutoEDM.Reverse;
using AutoEDM.Sealing;
using AutoEDM.Wedm;

namespace AutoEDM.AddIn
{
    /// <summary>
    /// Ribbon "AutoEDM". Carrega o layout de Ribbon.xml (recurso embutido) e trata os
    /// cliques por CommandId (= id do XML). Cada comando é uma casca fina sobre um
    /// método do núcleo AutoEDM.Core, rodando in-process na montagem ATIVA.
    /// </summary>
    public class ElectrodeRibbon : Ribbon
    {
        private const int CmdCriarEletrodos = 1; // Criar eletrodos + base
        private const int CmdCoordenadas = 2;    // Coordenadas: janela com os eletrodos SELECIONADOS (posição + GAP/Ra)
        private const int CmdAnalisarZ = 3;      // Analisar eletrodos por Z
        private const int CmdSpecSheet = 4;      // Ficha (spec-sheet)
        private const int CmdCriarBase = 5;       // Criar Base (ambiente de PEÇA)
        private const int CmdInspecionar = 6;       // SPY: dumpa o objeto COM selecionado
        private const int CmdUnirSuperficies = 7;   // Engrossar a queima e unir ao bloco (ISOLADO)
        private const int CmdIniciarLeitura = 8;    // Gravador: snapshot inicial (ação manual)
        private const int CmdGravarLeitura = 9;     // Gravador: diff + dump das features novas
        private const int CmdCriarEletrodoManual = 10; // Criar 1 eletrodo a partir da seleção manual de faces
        private const int CmdAplicarGap = 11;       // GAP + cor + nome no corpo já unido (ambiente de PEÇA)
        private const int CmdDuplicarEletrodo = 12; // Duplicar eletrodo(s) selecionado(s) p/ o próximo Ra da tabela
        private const int CmdDiagRosca = 13;        // Sonda da rosca M6 (peça descartável)
        private const int CmdAlojamentoORing = 14;  // Alojamento de anel O'ring (janela modeless)
        private const int CmdSondaInterPart = 15;   // Sonda do inter-part (rodada 2) — só leitura + peça descartável
        private const int CmdListaCorte = 16;       // Lista de corte: perfil do estoque + medida na serra dos eletrodos SELECIONADOS
        private const int CmdExportarPerfisWedm = 17; // WEDM: curvas de construção → um .igs por altura Z (PEÇA síncrona)
        private const int CmdCurvasDasSuperficies = 18; // WEDM: curvas nas extremidades paralelas a XY das superfícies (PEÇA síncrona)
        private const int CmdMcpLigar = 19;         // MCP: sobe a ponte (named pipe) para o Claude Code alcançar a SE
        private const int CmdMcpDesligar = 20;      // MCP: derruba a ponte
        private const int CmdMcpStatus = 21;        // MCP: em que pé está a ponte (modo, pedidos atendidos)
        private const int CmdMcpLiberarEscrita = 22; // MCP: permite as ferramentas que ESCREVEM (só o usuário liga)
        private const int CmdMcpSomenteLeitura = 23; // MCP: volta a ponte para somente-leitura
        private const int CmdSondaMalha = 24;       // ENG. REVERSA: sonda de diagnóstico sobre a malha (SÓ LEITURA)
        private const int CmdListaModificacoes = 25; // Folha de revisões: peças com grupo "Rev.N" na árvore ordenada

        /// <summary>Snapshot (nomes dos itens por coleção) no "Iniciar leitura" — diffado no "Gravar log".</summary>
        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _recBaseline;

        public ElectrodeRibbon() : base()
        {
            LoadXml(System.Reflection.Assembly.GetExecutingAssembly(), "AutoEDM.AddIn.Ribbon.xml");
            StartStateWatch();
        }

        public override void OnControlClick(RibbonControl control)
        {
            // Rede de segurança: o relógio de estado (StartStateWatch) já deixa o botão
            // cinza no ambiente errado, mas se a SE clicar assim mesmo (estado velho, ribbon
            // ainda não repintada), o comando explica em vez de estragar a peça.
            if (!AllowedHere(control.CommandId, explain: true)) return;

            switch (control.CommandId)
            {
                case CmdCriarEletrodos: CriarEletrodos(); break;
                case CmdCriarEletrodoManual: CriarEletrodoManual(); break;
                case CmdCoordenadas: AbrirCoordenadas(); break;
                case CmdListaCorte: AbrirListaCorte(); break;
                case CmdListaModificacoes: AbrirListaModificacoes(); break;
                case CmdExportarPerfisWedm: ExportarPerfisWedm(); break;
                case CmdCurvasDasSuperficies: CurvasDasSuperficies(); break;
                case CmdAnalisarZ: AnalisarZ(); break;
                case CmdSpecSheet: GerarSpecSheet(); break;
                case CmdCriarBase: CriarBase(); break;
                case CmdUnirSuperficies: UnirSuperficies(); break;
                case CmdAplicarGap: AplicarGap(); break;
                case CmdDuplicarEletrodo: DuplicarEletrodo(); break;
                case CmdInspecionar: InspecionarSelecao(); break;
                case CmdDiagRosca: DiagnosticarRosca(); break;
                case CmdAlojamentoORing: AlojamentoORing(); break;
                case CmdSondaInterPart: SondarInterPart(); break;
                case CmdIniciarLeitura: IniciarLeitura(); break;
                case CmdGravarLeitura: GravarLeitura(); break;
                case CmdMcpLigar: McpLigar(); break;
                case CmdMcpDesligar: McpDesligar(); break;
                case CmdMcpStatus: McpStatus(); break;
                case CmdMcpLiberarEscrita: McpDefinirModo(BridgeMode.Write); break;
                case CmdMcpSomenteLeitura: McpDefinirModo(BridgeMode.ReadOnly); break;
                case CmdSondaMalha: SondarMalha(); break;
            }
        }

        // -------------------------------------------------------------- comandos

        /// <summary>Analisa (NÃO-destrutivo) e propõe os eletrodos por nível de Z.</summary>
        private void AnalisarZ()
        {
            Run(CmdAnalisarZ, (connector, doc, p) =>
            {
                Selection.ZAnalysisResult res = NewBuilder(connector).AnalyzeElectrodesByZ(doc, p);

                // A usinabilidade só aparece quando reprovou alguma coisa: silêncio aqui é
                // resultado bom, e um bloco fixo dizendo "nada encontrado" a cada clique treina o
                // usuário a fechar a janela sem ler.
                string machinability = res.DescribeMachinability();
                MessageBox.Show(
                    $"{res.Electrodes.Count} eletrodo(s) proposto(s), por nível de Z.\n" +
                    $"({res.FlatFaces} piso / {res.SteepFaces} parede)\n" +
                    (machinability.Length > 0 ? "\n" + machinability + "\n" : "") +
                    "\nVeja as posições no log.",
                    "AutoEDM — Analisar eletrodos", MessageBoxButtons.OK,
                    res.HasEdmOnlyGeometry ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// SONDA DO INTER-PART (rodada 2). Só leitura na montagem + peças DESCARTÁVEIS: responde,
        /// numa rodada, as rotas de cópia entre peças que nunca foram executadas e mapeia a API
        /// para a skill. Não altera a cavidade, não salva a montagem.
        /// </summary>
        private void SondarInterPart()
        {
            Run(CmdSondaInterPart, (connector, doc, p) =>
            {
                AutoEDM.Experiments.InterPartProbe.Run(connector, doc);
                MessageBox.Show(
                    "Sonda concluída. TODO o resultado está no log — é ele que interessa, não esta janela.\n\n" +
                    "Se quiser o teste decisivo: entre em EDIÇÃO EM CONTEXTO no eletrodo à mão, " +
                    "deixe aberta e rode a sonda de novo. Comparar os dois logs responde se a cópia " +
                    "entre peças depende só desse estado.",
                    "AutoEDM — Sonda inter-part", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>Cria as peças de eletrodo (vazias) e as posiciona. ESCREVE na montagem. A
        /// base (bloco/holder) é gerada depois, pela "Criar Base", sobre a geometria real.</summary>
        private void CriarEletrodos()
        {
            Run(CmdCriarEletrodos, (connector, doc, p) =>
            {
                var builder = NewBuilder(connector);

                // CONFERIR ANTES DE CRIAR (Log 57): mostra a queima detectada + as cores não
                // mapeadas; se a maior região colorida não estiver mapeada, avisa. Assim o
                // usuário não cria eletrodos em cor RESIDUAL quando a queima real está fora do mapa.
                Selection.ZAnalysisResult res = builder.AnalyzeElectrodesByZ(doc, p);
                if (res.Electrodes.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhum eletrodo detectado (sem cor de queima MAPEADA). Nada foi criado.\n\n" +
                        "Veja no log as cores encontradas — a queima pode estar numa cor fora do mapa.",
                        "AutoEDM — Criar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Log.Info("Criar eletrodos: nada detectado.");
                    return;
                }

                var icon = res.HasDominantUnmappedColor ? MessageBoxIcon.Warning : MessageBoxIcon.Question;
                var confirm = MessageBox.Show(
                    res.DescribeBurnDetection() +
                    "\n\nIsto vai CRIAR uma peça VAZIA por eletrodo (sem bloco/base ainda) na subpasta 'Eletrodos' " +
                    "ao lado da montagem e inseri-las posicionadas. A montagem NÃO será salva automaticamente.\n\n" +
                    "A queima detectada está CORRETA? Criar os eletrodos?",
                    "AutoEDM — Conferir antes de criar", MessageBoxButtons.YesNo, icon);
                if (confirm != DialogResult.Yes) { Log.Info("Criação cancelada pelo usuário (conferência)."); return; }

                int n = builder.CreateElectrodesWithBlank(doc, p);
                MessageBox.Show(
                    $"{n} eletrodo(s) criado(s) e posicionado(s).\n\n" +
                    "Revise, SALVE a montagem, edite cada peça em contexto, copie (Inter-Part Copy) as faces de " +
                    "queima e use 'Criar Base' para gerar o bloco.",
                    "AutoEDM — Criar eletrodos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Versão MANUAL do "Criar eletrodos" (Carlos): em vez da detecção automática por
        /// cor/nível de Z, o usuário escolhe à mão a(s) FACE(s) do fundo do bolsão. Desde
        /// 2026-09-24 o botão não exige mais a pré-seleção: abre uma janela MODELESS que assume
        /// o mouse da SE e recebe as faces clique a clique (<see cref="ManualElectrodeForm"/>);
        /// o que já estava selecionado entra na lista. Cada "Criar eletrodo" cria e posiciona
        /// UMA peça VAZIA no centro XY + Z mais fundo das faces — mesmo pipeline do automático
        /// — e a janela segue aberta para o próximo. ESCREVE na montagem (não salva).
        /// </summary>
        private static ManualElectrodeForm _manualForm;

        private void CriarEletrodoManual()
        {
            if (!AllowedHere(CmdCriarEletrodoManual, explain: true)) return;
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                if (_manualForm != null && !_manualForm.IsDisposed)
                {
                    _manualForm.BringToFront();
                    _manualForm.Activate();
                    return;
                }

                Log.Info("===== CRIAR ELETRODO (MANUAL) — janela aberta =====");
                // `object`, não `dynamic`: com argumento dynamic a chamada inteira vira dinâmica
                // (a regra do "Duplicar eletrodo"), e aqui o tipo de retorno importa.
                object appObj = app;
                _manualForm = new ManualElectrodeForm(appObj, (doc, faces) =>
                    NewBuilder(SolidEdgeConnector.Attach(appObj)).CreateElectrodeFromFaces(doc, LoadParams(), faces));
                _manualForm.FormClosed += (s, e) => { _manualForm = null; Log.Info("===== FIM (CRIAR ELETRODO MANUAL) ====="); };
                _manualForm.Show();
            }
            catch (Exception ex) { Fail("abrir o criar eletrodo (manual)", ex); }
        }

        /// <summary>
        /// Botão "Coordenadas" (ambiente de MONTAGEM, Carlos, 2026-08-04): SEM detecção
        /// automática por cor — o usuário SELECIONA na montagem os eletrodos de interesse
        /// (ocorrências) e clica. Abre uma janela com uma linha por eletrodo: a posição
        /// (mesma leitura de "Propriedades de Ocorrência" no SE) e o GAP/Ra gravados na
        /// peça (mesma fonte que "Duplicar eletrodo" usa). Somente leitura — não altera a
        /// montagem nem as peças.
        /// </summary>
        private void AbrirCoordenadas()
        {
            Run(CmdCoordenadas, (connector, doc, p) =>
            {
                var items = NewBuilder(connector).ListSelectedElectrodes(doc);
                if (items.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhum eletrodo selecionado. Na montagem, selecione a(s) ocorrência(s) do(s) eletrodo(s) " +
                        "(clique na peça, não numa face) e clique em \"Coordenadas\" de novo.",
                        "AutoEDM — Coordenadas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (var form = new ElectrodeListForm(items))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>
        /// Botão "Lista de corte" (ambiente de MONTAGEM, Carlos, 2026-09-14): mesma seleção do
        /// "Coordenadas", agrupada por ARQUIVO — posições na montagem, perfil de cobre do estoque
        /// (identificado pelas medidas da peça) e medida na serra com 5 mm de sobremetal. A janela
        /// copia a tabela formatada para impressão. Somente leitura.
        /// </summary>
        private void AbrirListaCorte()
        {
            Run(CmdListaCorte, (connector, doc, p) =>
            {
                ElectrodeBuilder builder = NewBuilder(connector);
                System.Collections.Generic.List<SawCutListItem> items = builder.ListSawCuts(doc);
                if (items.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhum eletrodo selecionado. Na montagem, selecione a(s) ocorrência(s) do(s) eletrodo(s) " +
                        "(clique na peça, não numa face) e clique em \"Lista de corte\" de novo.",
                        "AutoEDM — Lista de corte", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string asmName = null;
                try { asmName = (string)doc.Name; } catch { }
                using (var form = new SawCutListForm(items, builder.BlankCatalog, asmName,
                           it => builder.RenderElectrodeThumbnail(it.PartDocument)))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>
        /// Botão "Lista de modificações" (ambiente de MONTAGEM, Carlos, 2026-09-17/18): a folha de
        /// revisões do molde, que hoje ele monta à mão no Google Sheets. Varre as peças DO PROJETO
        /// (as de catálogo ficam de fora pelo caminho do arquivo), acha em cada uma o grupo "Rev.N"
        /// mais recente da árvore ordenada e abre a janela para ele escrever descrição e ações.
        /// Somente leitura no modelo: o que sai daqui é .xlsx e um .json ao lado da montagem.
        /// </summary>
        private void AbrirListaModificacoes()
        {
            const string title = "AutoEDM — Lista de modificações";
            Run(CmdListaModificacoes, (connector, doc, p) =>
            {
                string asmPath = null, asmName = null;
                try { asmPath = (string)doc.FullName; } catch { }
                try { asmName = (string)doc.Name; } catch { }

                ProjectFolder folder = ProjectFolder.Parse(asmPath);
                int revision;
                System.Collections.Generic.List<PartChange> parts =
                    RevisionScanner.Scan(doc, folder.Directory, out revision,
                        AutoEdmConfig.LoadOrCreateDefault().RevisionPropertyNames);

                if (parts.Count == 0)
                {
                    MessageBox.Show(
                        "Nenhuma peça desta montagem tem um grupo \"Rev.N\" na árvore ordenada." +
                        Environment.NewLine + Environment.NewLine +
                        "É assim que o AutoEDM sabe o que mudou: agrupe os recursos da alteração na árvore da peça " +
                        "e nomeie o grupo \"Rev.1\", \"Rev.2\"... Depois clique aqui de novo.",
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var report = new ChangeReport
                {
                    Revision = revision,
                    AssemblyName = asmName,
                    ProductCode = folder.ProductCode,
                    PartNumbers = folder.PartNumbers,
                    MoldCode = folder.MoldCode,
                    MoldBaseCode = folder.MoldCode,   // na folha do Carlos o PM costuma ser o próprio MD
                    ProjectDirectory = folder.Directory,
                };
                report.Parts.AddRange(parts);

                string storePath = ChangeReportStore.PathFor(asmPath);
                ChangeReportStore.Apply(ChangeReportStore.Load(storePath), report);

                ElectrodeBuilder builder = NewBuilder(connector);
                using (var form = new ChangeReportForm(report, storePath,
                           part => RenderPartPngs(builder, part)))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>
        /// As miniaturas de uma peça da Lista de modificações, em PNG: isométrica DE CIMA (Z+) e DE
        /// BAIXO (Z−) — a alteração pode estar de qualquer lado, e é justamente o lado escondido
        /// que costuma faltar no desenho de bancada (Carlos, 2026-09-18). As faces das features da
        /// revisão saem em roxo, com a chamada numerada de cada uma. Peça de molde, não eletrodo:
        /// por isso a vista de cima vem primeiro. Lista vazia = a folha sai sem imagem.
        /// </summary>
        private static System.Collections.Generic.List<byte[]> RenderPartPngs(ElectrodeBuilder builder, PartChange part)
        {
            var images = new System.Collections.Generic.List<byte[]>();
            if (part?.PartDocument == null) return images;

            foreach (var view in new[] { ElectrodeThumbnail.View.FromAbove, ElectrodeThumbnail.View.FromBelow })
            {
                try
                {
                    // 300 px: as duas vistas cabem lado a lado na planilha sem cobrir o bloco de texto.
                    using (System.Drawing.Bitmap bmp = builder.RenderElectrodeThumbnail(
                               part.PartDocument, 300, view, part.RevisionFeature))
                    {
                        if (bmp == null) continue;
                        using (var buffer = new System.IO.MemoryStream())
                        {
                            bmp.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
                            images.Add(buffer.ToArray());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Miniatura ({view}) de '{part.FileName}' falhou — {ex.GetBaseException().Message}");
                }
            }
            return images;
        }

        /// <summary>
        /// Botão "Curvas das superfícies" do grupo WEDM (PEÇA síncrona, Carlos, 2026-09-15): cria na
        /// peça uma curva derivada sobre cada extremidade PARALELA AO PLANO XY das superfícies —
        /// contorno do fundo (Z mínimo) e do topo (Z máximo). Não exporta: quem grava os .igs
        /// continua sendo o "Exportar perfis (IGES)", que lê estas curvas como quaisquer outras.
        /// </summary>
        private void CurvasDasSuperficies()
        {
            const string title = "AutoEDM — Curvas das superfícies";
            Run(CmdCurvasDasSuperficies, (connector, doc, p) =>
            {
                SurfaceRimResult r = SurfaceRimCurveBuilder.Build((object)doc);
                if (!r.Ok)
                {
                    MessageBox.Show(r.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{r.Curves.Count} curva(s) criada(s) a partir de {r.SurfacesRead} superfície(s) — {r.Source}.");
                sb.AppendLine();
                foreach (var g in System.Linq.Enumerable.OrderBy(
                             System.Linq.Enumerable.GroupBy(r.Curves, c => new { c.Label, c.IsTop }),
                             g => double.Parse(g.Key.Label, System.Globalization.CultureInfo.InvariantCulture)))
                {
                    int edges = System.Linq.Enumerable.Sum(g, c => c.EdgeCount);
                    int open = System.Linq.Enumerable.Count(g, c => !c.Closed);
                    int pieces = System.Linq.Enumerable.Sum(g, c => c.PieceCount);
                    int contours = System.Linq.Enumerable.Count(g);
                    sb.AppendLine($"   Z = {g.Key.Label} ({(g.Key.IsTop ? "topo" : "fundo")})  —  " +
                                  $"{contours} contorno(s), {edges} aresta(s)" +
                                  (pieces > contours ? $", em {pieces} curva(s) na árvore" : "") +
                                  (open > 0 ? $", {open} aberto(s)" : ""));
                }

                var notes = new System.Collections.Generic.List<string>();
                if (r.Deleted > 0)
                    notes.Add($"{r.Deleted} curva(s) desta mesma ferramenta, de uma rodada anterior, foram substituídas.");
                // Contorno aberto é normal em corte reto (o fio entra e sai da peça), então só
                // vira ATENÇÃO quando as pontas ficaram perto demais para ser abertura de projeto.
                if (r.LoopsSuspect > 0)
                    notes.Add($"{r.LoopsSuspect} contorno(s) abertos com as pontas a menos de 1 mm uma da outra (ou de outro contorno) — " +
                              "provavelmente é um perfil só que se partiu em pedaços por uma folga entre as arestas. Veja as distâncias no log.");
                if (r.LoopsSplit > 0)
                    notes.Add($"{r.LoopsSplit} contorno(s) o Solid Edge recusou inteiros e saíram FATIADOS, uma curva por aresta. " +
                              "O perfil está completo e a exportação não se importa, mas a árvore fica com várias curvas no mesmo Z.");
                if (r.RenameFailed)
                    notes.Add("Alguma curva não ficou com o nome \"WEDM Z = ...\" — a próxima rodada NÃO vai substituí-la. " +
                              "Apague as curvas desta rodada à mão antes de clicar de novo, senão o perfil sai duplicado no .igs.");
                if (r.EdgesDropped > 0)
                    notes.Add($"{r.EdgesDropped} aresta(s) horizontal(is) em alturas intermediárias ficaram de fora (só o Z mínimo e o máximo viram curva).");
                if (r.SurfacesWithoutRim > 0)
                    notes.Add($"{r.SurfacesWithoutRim} superfície(s) não têm extremidade paralela ao plano XY.");
                if (r.LoopsFailed > 0)
                    notes.Add($"{r.LoopsFailed} contorno(s) não viraram curva — veja o log.");
                foreach (string w in System.Linq.Enumerable.Take(r.Warnings, 5)) notes.Add(w);
                if (notes.Count > 0)
                {
                    sb.AppendLine();
                    foreach (string n in notes) sb.AppendLine("ATENÇÃO: " + n);
                }
                sb.AppendLine();
                sb.Append("As curvas estão na árvore da peça com o nome \"WEDM Z = ...\". Confira e clique em \"Exportar perfis (IGES)\" para gerar os arquivos.");

                MessageBox.Show(sb.ToString(), title, MessageBoxButtons.OK,
                    notes.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Botão "Exportar perfis (IGES)" do grupo WEDM (PEÇA síncrona, Carlos, 2026-09-14): as curvas
        /// de construção visíveis da peça viram um .igs por altura Z, na pasta da peça, com o nome
        /// dela + "Z = XX.XX" — perfis de corte a fio para o Pitágoras, nas coordenadas da peça. Não
        /// altera o modelo.
        /// </summary>
        private void ExportarPerfisWedm()
        {
            const string title = "AutoEDM — Exportar perfis WEDM";
            Run(CmdExportarPerfisWedm, (connector, doc, p) =>
            {
                WedmExportResult r = WedmProfileExporter.Export((object)doc);
                if (!r.Ok)
                {
                    MessageBox.Show(r.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{r.Files.Count} perfil(is) gravado(s) em:");
                sb.AppendLine(r.Folder);
                sb.AppendLine();
                foreach (WedmLevelFile f in r.Files)
                    sb.AppendLine($"   {System.IO.Path.GetFileName(f.Path)}  —  {f.CurveCount} curva(s){(f.Replaced ? " (substituído)" : "")}");

                var notes = new System.Collections.Generic.List<string>();
                if (r.NotHorizontal.Count > 0)
                    notes.Add($"{r.NotHorizontal.Count} curva(s) fora de plano horizontal NÃO foram exportadas.");
                if (r.Read.FeaturesHidden > 0)
                    notes.Add($"{r.Read.FeaturesHidden} curva(s) oculta(s) ou suprimida(s) ficaram de fora.");
                if (r.Read.EdgesStroked > 0)
                    notes.Add($"{r.Read.EdgesStroked} curva(s) sem leitura exata saíram como polilinha de 0,001 mm.");
                if (r.Read.EdgesFailed > 0)
                    notes.Add($"{r.Read.EdgesFailed} aresta(s) não puderam ser lidas — veja o log.");
                if (r.StaleFiles.Count > 0)
                    notes.Add($"{r.StaleFiles.Count} arquivo(s) de outros níveis, de uma exportação anterior, continuam na pasta: " +
                              string.Join(", ", System.Linq.Enumerable.Select(r.StaleFiles, System.IO.Path.GetFileName)));
                if (notes.Count > 0)
                {
                    sb.AppendLine();
                    foreach (string n in notes) sb.AppendLine("ATENÇÃO: " + n);
                }
                sb.AppendLine();
                sb.Append("Abrir a pasta?");

                bool attention = notes.Count > 0;
                if (MessageBox.Show(sb.ToString(), title, MessageBoxButtons.YesNo,
                        attention ? MessageBoxIcon.Warning : MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{r.Files[0].Path}\"");
            });
        }

        /// <summary>Folha de dados (spec-sheet) por eletrodo (.txt + .csv). Somente leitura.</summary>
        private void GerarSpecSheet()
        {
            Run(CmdSpecSheet, (connector, doc, p) =>
            {
                ElectrodeBuildPlan plan = NewBuilder(connector).PlanFromAssemblyDocument(doc, p);
                Log.Info(ElectrodeSpecSheet.ToText(plan, p));
                string path = ElectrodeSpecSheetWriter.Save(plan, p, folder: ElectrodeNaming.ResolveProjectFolder(doc));
                MessageBox.Show(
                    $"Ficha gerada para {plan.Regions.Count} detalhe(s).\n\nArquivos (.txt e .csv):\n{path}",
                    "AutoEDM — Ficha", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// "Criar Base" (ambiente de PEÇA): abre a janela com parâmetros + Preview. O usuário
        /// já copiou as faces de queima na peça manualmente; o AutoEDM cria a base (bloco +
        /// faixa de medição) e aplica a fixação. SEM gap/offset por cor (a superfície copiada
        /// não carrega a cor original) — isso e o trabalho de superfície ficam no botão
        /// "Unir superfícies". ESCREVE na peça.
        /// </summary>
        private void CriarBase()
        {
            Run(CmdCriarBase, (connector, doc, p) =>
            {
                // O Core loga cada passo + o probe.
                var builder = new SurfaceBlockBuilder();
                using (var form = new BlockOverSurfacesForm(builder, (object)connector.Application, (object)doc))
                {
                    form.ShowDialog();
                }
            });
        }

        /// <summary>
        /// Botão ISOLADO "Unir superfícies" (ambiente de PEÇA): engrossa a superfície de
        /// queima selecionada para cima até dentro do bloco e une num sólido único. Separado
        /// do "Criar Base" porque o thicken, ao falhar, envenenava o doc e
        /// derrubava a fixação — aqui o experimento fica isolado. SÓ UNE (Carlos, 2026-07-21:
        /// separado do GAP/cor/nome — quando a união automática falha, ele une NA MÃO no SE e
        /// segue direto para "Aplicar GAP", sem precisar que este botão também acerte a união).
        /// ESCREVE na peça.
        /// </summary>
        private void UnirSuperficies()
        {
            Run(CmdUnirSuperficies, (connector, doc, p) =>
            {
                var builder = new SurfaceBlockBuilder();
                BlockOverSurfacesResult res = builder.UniteSurfacesToBlock(doc, new BlockOverSurfacesOptions());
                string gaps = res.SideGapsPatched > 0
                    ? $"{res.SideGapsPatched} vão(s) lateral(is) fechado(s) automaticamente com 'Limite'.\n\n"
                    : "";
                string msg = res.SurfacesUnited
                    ? gaps + "Superfície unida ao bloco ✓. Confira no modelo — depois use 'Aplicar GAP' para o offset/cor.\n\nDetalhe no log (linhas 'Unir:')."
                    : gaps + "Não uni as superfícies ao bloco ainda.\n\nVeja o log (linhas 'Unir:') para o ponto exato — o bloco/faixa/furos NÃO foram tocados. " +
                      "Se preferir, una NA MÃO no SE e use 'Aplicar GAP' no corpo já mesclado.";
                if (res.Warnings.Count > 0) msg += "\n\n⚠ " + string.Join("\n⚠ ", res.Warnings);
                MessageBox.Show(msg, "AutoEDM — Unir superfícies", MessageBoxButtons.OK,
                    res.SurfacesUnited ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// Botão "Aplicar GAP" (ambiente de PEÇA, Carlos, 2026-07-21): separado de "Unir
        /// superfícies" — roda tanto depois de uma união automática quanto de uma união MANUAL
        /// feita direto no SE (quando o botão "Unir" falha, o Carlos tem essa saída). Selecione
        /// as faces de queima no corpo já unido (ou deixe em branco logo após um "Unir
        /// superfícies" bem-sucedido na mesma sessão — cai no heurístico antigo) e clique.
        /// A lista já abre pré-selecionada no Ra gravado na peça, se houver (detectado da cor
        /// pela seleção manual, ou de uma aplicação anterior). ESCREVE na peça.
        /// </summary>
        private void AplicarGap()
        {
            Run(CmdAplicarGap, (connector, doc, p) =>
            {
                double? preselect = RaVariableStore.TryRead(doc, out double ra) ? (double?)ra : null;
                var choices = RaGapPresets.All(p.Material, Config.BuildColorMap(), Config.BuildOffsetPolicy());
                RaGapPresets.Choice chosen;
                using (var picker = new RaGapPickerForm(preselect, choices))
                {
                    if (picker.ShowDialog() != DialogResult.OK || picker.Chosen == null)
                    {
                        Log.Info("Aplicar GAP: cancelado pelo usuário.");
                        return;
                    }
                    chosen = picker.Chosen;
                }
                Log.Info($"Aplicar GAP: escolhido {chosen.Label}.");

                var builder = new SurfaceBlockBuilder();
                BlockOverSurfacesResult res = builder.ApplyGapToUnitedSurfaces(doc, chosen);
                string msg = res.SurfacesOffset
                    ? $"GAP {chosen.GapMm:0.00}mm (Ra {chosen.Ra:0.0}) aplicado + cor + nome da feature. Confira no modelo.\n\nDetalhe no log (linhas 'Aplicar GAP:')."
                    : "Não apliquei o GAP.\n\n" + (res.Warnings.Count > 0 ? res.Warnings[0] : "Veja o log (linhas 'Aplicar GAP:').");
                MessageBox.Show(msg, "AutoEDM — Aplicar GAP", MessageBoxButtons.OK,
                    res.SurfacesOffset ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// Botão "Duplicar eletrodo" (ambiente de MONTAGEM, Carlos, 2026-07-21): selecione a
        /// ocorrência de UM eletrodo já com GAP aplicado (tem Ra gravado — variável ou feature
        /// "GAP: ... - Ra: ...") e clique. Cria uma cópia da peça com o GAP no PRÓXIMO Ra da
        /// escada (mais grosso = desbaste, <see cref="RaColorMap.RoughingRaFor"/>) e posiciona
        /// essa cópia em TODAS as posições onde o eletrodo original aparece na montagem (copia
        /// as instâncias — não só a selecionada) — assim um par desbaste/acabamento sai em 1
        /// clique por posição repetida. Não salva a montagem.
        /// </summary>
        private void DuplicarEletrodo()
        {
            Run(CmdDuplicarEletrodo, (connector, doc, p) =>
            {
                DuplicateElectrodeResult res = NewBuilder(connector).DuplicateElectrodeToNextGap(doc, p);
                MessageBox.Show(res.Message, "AutoEDM — Duplicar eletrodo", MessageBoxButtons.OK,
                    res.Created ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        /// <summary>
        /// GRAVADOR (Carlos, 2026-07-16) — passo 1: tira o snapshot das contagens das coleções
        /// (superfícies de construção + features do Model) do documento ativo. O usuário clica,
        /// faz a ação manual no SE (criar "Limite", costurar, estender/mover…) e depois clica em
        /// "Gravar log da leitura". Assim o AutoEDM dumpa exatamente as features que ele criou.
        /// </summary>
        private void IniciarLeitura()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== INICIAR LEITURA DE AÇÃO MANUAL =====");
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }
                if (!ConfirmDocParaGravacao(doc, "iniciar a gravação")) return;
                _recBaseline = ComDiagnostics.SnapshotInventory((object)doc);
                MessageBox.Show(
                    "Gravação INICIADA.\n\nAgora faça a ação manual no Solid Edge (ex.: criar a superfície 'Limite', costurar, estender/mover a face até o bloco).\n\n" +
                    "Quando terminar, clique em \"Gravar log da leitura\".",
                    "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("iniciar a leitura de ação manual", ex); }
        }

        /// <summary>
        /// GRAVADOR — passo 2: diffa contra o snapshot do "Iniciar leitura" e dumpa (SPY) as
        /// features NOVAS de cada coleção que cresceu — revela a coleção/tipo/propriedades das
        /// features que o usuário criou à mão, para reproduzir por COM.
        /// </summary>
        private void GravarLeitura()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== GRAVAR LOG DA LEITURA =====");
                if (_recBaseline == null)
                {
                    MessageBox.Show("Clique primeiro em \"Iniciar leitura de ação manual\", faça a ação, e só então grave.", "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }
                if (!ConfirmDocParaGravacao(doc, "gravar o log")) return;
                ComDiagnostics.DumpNewSince((object)doc, _recBaseline);
                _recBaseline = null; // consome o snapshot (evita diff contra estado velho)
                MessageBox.Show(
                    "Leitura gravada no log (procure as linhas [REC] e [SPY]):\n%LOCALAPPDATA%\\AutoEDM\\logs\n\nMe mande esse log.",
                    "AutoEDM — Gravador", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log.Info("===== FIM (GRAVAR LOG DA LEITURA) =====");
            }
            catch (Exception ex) { Fail("gravar o log da leitura", ex); }
        }

        /// <summary>
        /// SPY: dumpa o(s) objeto(s) COM selecionado(s) no SE (tipo + propriedades/valores +
        /// assinaturas de método/setter, recursivo 1 nível, coleções expandidas) para o log.
        /// Serve para descobrir a API REAL sem adivinhar — ex.: crie um furo (ou uma superfície
        /// copiada/offsetada/costurada) DO SEU JEITO no SE, selecione a feature e clique aqui: o
        /// log revela a coleção/método/propriedades para reproduzir por COM. Funciona em
        /// qualquer documento (peça/montagem). Cada clique TAMBÉM engorda o dump acumulado do
        /// SDK (%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_&lt;versão&gt;.txt) com a type library de
        /// tudo que foi tocado — é assim que Geometry/Assembly (que a sonda estática não
        /// alcançava) entram no mapa: basta selecionar uma Face/Edge/Ocorrência qualquer.
        /// </summary>
        private void InspecionarSelecao()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                Log.Info("===== INSPECIONAR SELEÇÃO (SPY) =====");
                dynamic doc = app.ActiveDocument;
                if (doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }

                dynamic ss = doc.SelectSet;
                int n = 0; try { n = (int)ss.Count; } catch { }
                if (n == 0)
                {
                    MessageBox.Show(
                        "Nada selecionado. Selecione uma feature no SE (ex.: um furo, uma superfície copiada/offsetada) e clique de novo.\n\n" +
                        "O dump vai para o log (%LOCALAPPDATA%\\AutoEDM\\logs).",
                        "AutoEDM — Inspecionar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Log.Info("Inspecionar: nada selecionado.");
                    return;
                }

                Log.Info($"Inspecionando {n} objeto(s) selecionado(s) — veja [SPY] no log.");
                for (int i = 1; i <= n; i++)
                {
                    object item; try { item = ss.Item(i); } catch { continue; }
                    ComDiagnostics.DumpObject($"Seleção[{i}]", item, 1);
                }
                MessageBox.Show(
                    $"{n} objeto(s) inspecionado(s). Veja o dump [SPY] no log e o mapa acumulado da API:\n" +
                    "%LOCALAPPDATA%\\AutoEDM\\logs  (AutoEDM_*.log = [SPY]; SE_API_dump_<versão>.txt = mapa completo)",
                    "AutoEDM — Inspecionar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log.Info("===== FIM (INSPECIONAR SELEÇÃO) =====");
            }
            catch (Exception ex) { Fail("inspecionar a seleção", ex); }
        }

        /// <summary>
        /// SONDA DA ROSCA M6 (diagnóstico): cria uma peça DESCARTÁVEL com um bloco e quatro
        /// furos M6, um por receita de API, e loga tudo (HoleData completo, Status da feature,
        /// CreatePhysicalThread, código de erro da rosca física e o Ø REAL medido de cada
        /// cilindro). Roda numa peça própria de propósito — um Holes.AddSync que falha envenena
        /// o proxy do documento e derrubaria a furação que já funciona no fluxo do eletrodo.
        /// Também confere a opção GLOBAL de exibição de rosca do SE: com ela desligada, mesmo o
        /// furo roscado certo desenha liso. Ver <see cref="AutoEDM.Electrode.ThreadProbe"/>.
        /// </summary>
        private void DiagnosticarRosca()
        {
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }

            var confirm = MessageBox.Show(
                "Isto vai CRIAR uma PEÇA NOVA (descartável, não salva) com um bloco 120×40×15 e quatro furos M6 — " +
                "cada um por uma receita diferente da API de rosca.\n\n" +
                "Depois: olhe a peça e me diga em qual X a rosca saiu CORTADA de verdade\n" +
                "(1=-42  2=-14  3=+14  4=+42 mm) e me mande o log.\n\nContinuar?",
                "AutoEDM — Sonda de rosca M6", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) { Log.Info("Sonda de rosca cancelada pelo usuário."); return; }

            try
            {
                bool displayOff = ThreadProbe.ThreadedDisplayIsOff(app);
                ThreadProbe.RunM6Matrix(app);

                // A exibição de rosca é uma OPÇÃO DO APLICATIVO, não do documento: mexer nela
                // muda o Solid Edge inteiro do usuário, então só com o "sim" dele.
                if (displayOff)
                {
                    var liga = MessageBox.Show(
                        "A opção GLOBAL do Solid Edge \"exibir roscas\" está DESLIGADA nesta instalação.\n\n" +
                        "Com ela desligada, um furo roscado CORRETO aparece liso na tela — dá para confundir " +
                        "com \"a rosca não foi criada\".\n\n" +
                        "Ligar agora? (é uma opção do Solid Edge inteiro, não só desta peça)",
                        "AutoEDM — exibição de rosca", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (liga == DialogResult.Yes) ThreadProbe.EnableThreadedDisplay(app);
                    else Log.Info("Exibição de rosca deixada DESLIGADA a pedido do usuário.");
                }

                MessageBox.Show(
                    "Sonda concluída. Na peça nova, os quatro furos M6 (X = -42, -14, +14, +42 mm):\n\n" +
                    "1 (-42): base de furos + rosca só como anotação\n" +
                    "2 (-14): base de furos + hélice pedida na CRIAÇÃO (AddSyncEx)\n" +
                    "3 (+14): base de furos + hélice pedida DEPOIS do furo\n" +
                    "4 (+42): valores na mão, sem a base de furos\n\n" +
                    "O log traz o Ø REAL medido de cada furo — o esperado é 5,0 mm (broca de M6), não 6,0.\n\n" +
                    "Diga qual saiu com a hélice CORTADA e mande o log (%LOCALAPPDATA%\\AutoEDM\\logs).",
                    "AutoEDM — Sonda de rosca M6", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("rodar a sonda de rosca M6", ex); }
        }

        /// <summary>
        /// Alojamento de anel O'ring (ambiente de PEÇA). Abre a janela MODELESS — a única do
        /// add-in — porque o fluxo é "selecione uma face, depois uma aresta" e uma janela modal
        /// congelaria o Solid Edge justamente na hora de clicar no modelo.
        ///
        /// A janela é guardada num campo estático: um Form modeless que sai de escopo é
        /// coletado e some da tela sem erro nenhum. Se já estiver aberta, traz para a frente
        /// em vez de abrir uma segunda.
        /// </summary>
        private static ORingGrooveForm _oringForm;

        private void AlojamentoORing()
        {
            if (!AllowedHere(CmdAlojamentoORing, explain: true)) return;
            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null) { MessageBox.Show("Add-in não inicializado.", "AutoEDM"); return; }
            try
            {
                if (_oringForm != null && !_oringForm.IsDisposed)
                {
                    _oringForm.BringToFront();
                    _oringForm.Activate();
                    return;
                }

                Log.Info("===== ALOJAMENTO DE O'RING (janela aberta) =====");
                var catalog = ORingCatalog.LoadOrCreateDefault();
                _oringForm = new ORingGrooveForm((object)app, catalog);
                _oringForm.FormClosed += (s, e) => { _oringForm = null; Log.Info("===== FIM (ALOJAMENTO DE O'RING) ====="); };
                _oringForm.Show();
            }
            catch (Exception ex) { Fail("abrir o alojamento de O'ring", ex); }
        }

        // ------------------------------------------------------------------ MCP

        /// <summary>
        /// A ponte MCP. Um por sessão da Solid Edge, criada NA THREAD DA SE (este clique roda
        /// nela) e nunca sozinha: enquanto o Carlos não clicar em "Ligar ponte", nenhum agente
        /// alcança o CAD. Esse é o ponto — a decisão de expor uma montagem de molde viva a um
        /// agente é do usuário, não do add-in.
        /// </summary>
        private static McpBridgeHost _mcp;

        private static McpBridgeHost Mcp => _mcp ?? (_mcp = new McpBridgeHost());

        /// <summary>
        /// Derruba a ponte quando a Solid Edge descarrega o add-in. Sem isto, o pipe ficaria
        /// escutando num processo que está fechando, e a próxima instância da SE veria o nome
        /// tomado — o sintoma seria "a ponte não sobe mais" sem motivo aparente.
        /// </summary>
        internal static void ShutdownMcp()
        {
            try { if (_mcp != null) { _mcp.Dispose(); _mcp = null; } }
            catch (Exception ex) { Log.Warn("MCP: falha ao derrubar a ponte no encerramento — " + ex.GetBaseException().Message); }
        }

        private void McpLigar()
        {
            try
            {
                string error;
                if (Mcp.Start(out error))
                {
                    MessageBox.Show(
                        "Ponte MCP NO AR.\n\n" +
                        $"Pipe: {BridgeProtocol.PipeName}\n" +
                        "Modo: SOMENTE-LEITURA (as ferramentas que alteram o modelo estão recusando).\n\n" +
                        "No Claude Code, o servidor 'autoedm' já encontra a Solid Edge — peça a ele um 'se_status' " +
                        "para confirmar a ligação ponta a ponta.\n\n" +
                        "Para permitir que o agente ALTERE a peça, clique em \"Liberar escrita\".",
                        "AutoEDM — MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("A ponte NÃO subiu.\n\n" + error, "AutoEDM — MCP",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) { Fail("ligar a ponte MCP", ex); }
        }

        private void McpDesligar()
        {
            try
            {
                if (_mcp == null || !_mcp.Running)
                {
                    MessageBox.Show("A ponte já está desligada.", "AutoEDM — MCP",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _mcp.Stop();
                MessageBox.Show(
                    "Ponte MCP desligada. Nenhum agente alcança a Solid Edge agora.\n\n" +
                    "Ao religar, a ponte volta em SOMENTE-LEITURA — a liberação de escrita não sobrevive a um " +
                    "desligamento, de propósito.",
                    "AutoEDM — MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("desligar a ponte MCP", ex); }
        }

        private void McpStatus()
        {
            try
            {
                bool up = _mcp != null && _mcp.Running;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(up ? "Ponte MCP: NO AR" : "Ponte MCP: DESLIGADA");
                sb.AppendLine($"Pipe: {BridgeProtocol.PipeName}  (contrato v{BridgeProtocol.Version})");
                if (up)
                {
                    sb.AppendLine($"Modo: {(_mcp.Mode == BridgeMode.Write ? "ESCRITA LIBERADA" : "SOMENTE-LEITURA")}");
                    sb.AppendLine($"Pedidos atendidos nesta sessão: {_mcp.Served}");
                    sb.AppendLine($"Última ferramenta pedida: {_mcp.LastTool ?? "(nenhuma ainda)"}");
                }
                sb.AppendLine();
                sb.AppendLine($"Ferramentas no catálogo: {ToolCatalog.All.Count}");
                foreach (ToolSpec t in ToolCatalog.All)
                    sb.AppendLine($"   {(t.Writes ? "[escreve]" : "[leitura]")}  {t.Name}");

                MessageBox.Show(sb.ToString(), "AutoEDM — MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("ler o status da ponte MCP", ex); }
        }

        private void McpDefinirModo(BridgeMode mode)
        {
            try
            {
                if (_mcp == null || !_mcp.Running)
                {
                    MessageBox.Show("A ponte está desligada — ligue-a antes de escolher o modo.",
                        "AutoEDM — MCP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (mode == BridgeMode.Write)
                {
                    // Confirmação explícita: daqui em diante um agente pode criar curva na peça e
                    // gravar arquivo. Vale uma pergunta, não um clique acidental.
                    var r = MessageBox.Show(
                        "Liberar ESCRITA para o agente?\n\n" +
                        "Com a escrita liberada, o Claude Code pode ALTERAR o documento aberto (criar as curvas de WEDM) " +
                        "e GRAVAR arquivos na pasta da peça, sem pedir confirmação a cada operação.\n\n" +
                        "Recomendado: salve o que estiver aberto antes. A liberação vale só até a ponte ser desligada " +
                        "ou a Solid Edge fechar.\n\n" +
                        "Liberar?",
                        "AutoEDM — MCP", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) { Log.Info("MCP: liberação de escrita cancelada pelo usuário."); return; }
                }

                _mcp.SetMode(mode);
                MessageBox.Show(
                    mode == BridgeMode.Write
                        ? "Escrita LIBERADA. O agente pode alterar o documento aberto e gravar arquivos."
                        : "Ponte de volta para SOMENTE-LEITURA. As ferramentas que alteram o modelo voltam a recusar.",
                    "AutoEDM — MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Fail("trocar o modo da ponte MCP", ex); }
        }

        // ------------------------------------------------------------ Eng. Reversa

        /// <summary>
        /// SONDA DA MALHA (rodada 1). SÓ LEITURA: não cria feature, não altera a peça, não salva.
        /// Existe porque a decisão de COMO reconstruir um sólido a partir de malha não deve ser
        /// tomada por aposta. O dump da typelib já provou que a Solid Edge NÃO expõe por COM
        /// nenhum ajuste de plano/cilindro/cone sobre região de malha — os comandos da aba nativa
        /// de Eng. Reversa não estão no modelo de objetos. Então o ajuste terá de ser nosso, e
        /// esta sonda mede, na malha REAL do Carlos, o que de fato responde:
        /// leitura dos triângulos, remalhamento, cura/fechamento de furo e — o mais importante —
        /// o seccionamento com reconhecimento de retas/arcos/círculos, que é a alavanca da rota
        /// prismática. O resultado inteiro vai para o log.
        /// </summary>
        private void SondarMalha()
        {
            Run(CmdSondaMalha, (connector, doc, p) =>
            {
                const string title = "AutoEDM — Sonda de malha (Eng. Reversa)";

                // O seccionamento é o teste que DECIDE a rota prismática, e é o único que precisa
                // escrever (ele cria esboços). Perguntar aqui, e não no Core, mantém a sonda
                // utilizável sem interface — e o padrão do projeto é conferir antes de criar.
                var ask = MessageBox.Show(
                    "A sonda lê a malha sem alterar nada: triângulos, corpos de facetas, e quais membros da API de " +
                    "malha existem nesta versão da Solid Edge.\n\n" +
                    "Há UM teste a mais, e é o que decide o rumo do botão de Eng. Reversa: o SECCIONAMENTO " +
                    "(CreateSectionSketches), que corta o corpo em planos e devolve esboços com retas, arcos e " +
                    "círculos já RECONHECIDOS. Se ele funcionar sobre malha, não precisamos escrever reconhecimento " +
                    "de primitiva 2D — a reconstrução sai como feature editável.\n\n" +
                    "Esse teste CRIA esboços na peça. A sonda tenta apagá-los depois e NUNCA salva o arquivo.\n\n" +
                    "Incluir o teste de seccionamento?",
                    title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (ask == DialogResult.Cancel) { Log.Info("Sonda de malha: cancelada pelo usuário."); return; }

                MeshProbeResult res = MeshProbe.Run((object)doc, allowSectionTest: ask == DialogResult.Yes);
                MessageBox.Show(res.Summary, title, MessageBoxButtons.OK,
                    res.FoundMesh ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        // -------------------------------------------------------------- infra

        /// <summary>Tipo de documento exigido por um comando (= SolidEdgeFramework.DocumentTypeConstants).</summary>
        private enum DocKind { Any = 0, Part = 1, Assembly = 3 } // igPartDocument / igAssemblyDocument

        /// <summary>O que um comando exige do documento ativo para PODER rodar.</summary>
        private sealed class CommandSpec
        {
            public readonly string Title;
            public readonly DocKind Kind;
            public readonly ModelingEnv Env;
            public CommandSpec(string title, DocKind kind, ModelingEnv env) { Title = title; Kind = kind; Env = env; }
        }

        /// <summary>
        /// PRÉ-REQUISITO DE CADA BOTÃO, num lugar só (Carlos, 2026-09-08). Antes cada comando
        /// declarava apenas o TIPO de documento, e o ambiente de modelagem era resolvido lá
        /// dentro — às vezes TROCANDO o modo da peça pelas costas do usuário. Daí saíram os
        /// dois problemas relatados:
        ///
        /// • esboços presos entre síncrono e ordenado, impossíveis de apagar: o alojamento de
        ///   O'ring criava esboço ORDENADO (<c>ProfileSets</c> só faz desse tipo) e o consumia
        ///   com um recurso SÍNCRONO — o esboço fica órfão no nó "Ordenado" do PathFinder e a
        ///   interface do SE se recusa a removê-lo;
        /// • "Aplicar GAP" errando quando a peça estava em síncrono: ele coletava as faces
        ///   selecionadas, TROCAVA o documento para ordenado e só então pintava/offsetava — a
        ///   troca reconstrói o corpo, e as faces lidas antes dela viram proxies mortos.
        ///
        /// A regra passa a ser uma só, para todo botão: <b>o comando declara o ambiente que
        /// exige; o AutoEDM nunca troca o ambiente da peça.</b> No ambiente errado o botão fica
        /// CINZA (<see cref="RefreshControlStates"/>) e, se ainda assim for clicado, explica ao
        /// usuário como trocar no SE.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, CommandSpec> Specs =
            new System.Collections.Generic.Dictionary<int, CommandSpec>
            {
                // --- montagem: nada aqui depende do ambiente de modelagem de peça ---
                { CmdAnalisarZ,           new CommandSpec("ANALISAR ELETRODOS (Z)",               DocKind.Assembly, ModelingEnv.Any) },
                { CmdCriarEletrodos,      new CommandSpec("CRIAR ELETRODOS",                      DocKind.Assembly, ModelingEnv.Any) },
                { CmdCriarEletrodoManual, new CommandSpec("CRIAR ELETRODO (MANUAL, da seleção)",  DocKind.Assembly, ModelingEnv.Any) },
                { CmdDuplicarEletrodo,    new CommandSpec("DUPLICAR ELETRODO",                    DocKind.Assembly, ModelingEnv.Any) },
                { CmdCoordenadas,         new CommandSpec("COORDENADAS (eletrodos selecionados)", DocKind.Assembly, ModelingEnv.Any) },
                { CmdListaCorte,          new CommandSpec("LISTA DE CORTE (eletrodos selecionados)", DocKind.Assembly, ModelingEnv.Any) },
                { CmdListaModificacoes,   new CommandSpec("LISTA DE MODIFICAÇÕES (folha de revisões)", DocKind.Assembly, ModelingEnv.Any) },
                { CmdSpecSheet,           new CommandSpec("FICHA DE ELETRODOS (spec-sheet)",      DocKind.Assembly, ModelingEnv.Any) },

                // --- peça: cada botão tem UM ambiente, o do recurso que ele cria ---
                // Bloco + faixa + furos: extrusão e furação da família síncrona (BlankModeler),
                // validadas em campo com a peça em síncrono (ModelingMode=1 nos logs 55/58).
                { CmdCriarBase,           new CommandSpec("CRIAR BASE",           DocKind.Part, ModelingEnv.Synchronous) },
                // "Limite", Costurar, Anexar e a booleana de união são SÍNCRONAS — em ordenado
                // o botão criava só uma feature de costura e não unia (relato de 2026-07-20).
                { CmdUnirSuperficies,     new CommandSpec("UNIR SUPERFÍCIES",     DocKind.Part, ModelingEnv.Synchronous) },
                // Model.FaceOffsets (o GAP que fica editável na árvore) só existe em ORDENADO.
                { CmdAplicarGap,          new CommandSpec("APLICAR GAP",          DocKind.Part, ModelingEnv.Ordered) },
                // Esboço + corte revolvido: em ORDENADO o esboço é filho legítimo do recurso e
                // some junto quando o usuário apaga o canal. Em síncrono viraria órfão.
                { CmdAlojamentoORing,     new CommandSpec("ALOJAMENTO DE O'RING", DocKind.Part, ModelingEnv.Ordered) },
                // WEDM: só LÊ as curvas e grava .igs. Síncrono porque é onde o Carlos prepara os perfis.
                { CmdExportarPerfisWedm,  new CommandSpec("EXPORTAR PERFIS WEDM (IGES por Z)", DocKind.Part, ModelingEnv.Synchronous) },
                // Cria curva derivada a partir das arestas das superfícies: mesmo ambiente do
                // resto do WEDM (é onde as superfícies copiadas vivem) e onde o Carlos trabalha.
                { CmdCurvasDasSuperficies, new CommandSpec("CURVAS DAS SUPERFÍCIES (extremidades XY)", DocKind.Part, ModelingEnv.Synchronous) },

                // --- diagnóstico: só leem, ou criam documento próprio — servem em qualquer ambiente ---
                { CmdInspecionar,         new CommandSpec("INSPECIONAR SELEÇÃO",   DocKind.Any, ModelingEnv.Any) },
                { CmdIniciarLeitura,      new CommandSpec("INICIAR LEITURA",       DocKind.Any, ModelingEnv.Any) },
                { CmdGravarLeitura,       new CommandSpec("GRAVAR LOG DA LEITURA", DocKind.Any, ModelingEnv.Any) },
                { CmdDiagRosca,           new CommandSpec("SONDA DE ROSCA M6",     DocKind.Any, ModelingEnv.Any) },
                // Sonda do inter-part: lê a montagem e trabalha em peça DESCARTÁVEL. Serve
                // tanto com a montagem normal quanto com o usuário em edição em contexto —
                // é justamente a diferença entre os dois estados que ela mede.
                { CmdSondaInterPart,      new CommandSpec("SONDA INTER-PART",      DocKind.Assembly, ModelingEnv.Any) },
                // Sonda da malha: só LÊ (triângulos, secções, diagnóstico). A malha importada vive
                // numa PEÇA; o ambiente não importa porque nada é criado.
                { CmdSondaMalha,          new CommandSpec("SONDA DE MALHA (ENG. REVERSA)", DocKind.Part, ModelingEnv.Any) },

                // --- os 5 comandos do grupo MCP ficam DE FORA desta tabela de propósito ---
                // Ligar/desligar a ponte e ler o status não dependem de documento nenhum: o Carlos
                // precisa poder subir a ponte com a Solid Edge recém-aberta e VAZIA, que é o caso
                // normal (o agente é quem vai pedir para abrir o arquivo). Comando ausente daqui
                // é permitido em qualquer contexto — ver AllowedHere. Quem confere documento e
                // ambiente para as FERRAMENTAS do agente é o SeToolRunner, com a mesma regra.
            };

        /// <summary>
        /// O documento ativo atende ao pré-requisito deste comando? Com
        /// <paramref name="explain"/>, também diz ao usuário o que falta — o clique explica, o
        /// relógio de estado não (senão abriria caixas de diálogo sozinho).
        /// </summary>
        private static bool AllowedHere(int commandId, bool explain)
        {
            CommandSpec spec;
            if (!Specs.TryGetValue(commandId, out spec)) return true; // comando sem pré-requisito declarado

            dynamic app = ElectrodeAddIn.Current?.App;
            if (app == null)
            {
                if (explain) MessageBox.Show("Add-in não inicializado.", "AutoEDM");
                return false;
            }

            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null)
            {
                if (explain) MessageBox.Show("Nenhum documento ativo.", "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (spec.Kind != DocKind.Any)
            {
                int type = -1; try { type = (int)doc.Type; } catch { }
                if (type != (int)spec.Kind)
                {
                    if (explain)
                        MessageBox.Show(spec.Kind == DocKind.Assembly
                            ? "Abra uma MONTAGEM (.asm) ativa (a cavidade no zero-máquina) para usar esta ferramenta."
                            : "Abra uma PEÇA (.par) ativa, com as faces de queima já copiadas nela, para usar esta ferramenta.",
                            "AutoEDM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            ModelingEnv actual = ModelingEnvironment.Read(doc);
            if (!ModelingEnvironment.Matches(spec.Env, actual))
            {
                if (explain)
                    MessageBox.Show(ModelingEnvironment.WrongEnvironmentMessage(spec.Title, spec.Env, actual),
                        "AutoEDM — ambiente de modelagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // --------------------------------------------- botão cinza quando o ambiente é o errado

        /// <summary>
        /// O Solid Edge pergunta o estado de cada comando do add-in enquanto a faixa está
        /// visível (<c>ISEAddInEventsEx.OnCommandUpdateUI</c>) e o framework responde com o que
        /// estiver em <c>RibbonControl.Enabled</c> naquele instante — é uma propriedade comum
        /// (auto-property), sem callback para interceptar. Então quem mantém esse valor fresco
        /// é este relógio: a cada tique ele relê documento + ambiente e liga/desliga cada botão.
        ///
        /// Roda na thread STA da própria SE (o add-in é in-process), então ler COM daqui é
        /// seguro e não passa por marshaling. Tudo é best-effort: um tique que falhe não pode
        /// derrubar a ribbon e, no pior caso, o botão continua clicável — por isso o clique
        /// ainda passa por <see cref="AllowedHere"/>.
        /// </summary>
        private Timer _stateTimer;
        private bool _refreshing;

        private void StartStateWatch()
        {
            try
            {
                _stateTimer = new Timer { Interval = 750 };
                _stateTimer.Tick += (s, e) => RefreshControlStates();
                _stateTimer.Start();
            }
            catch (Exception ex) { Log.Warn("Estado dos botões: relógio não iniciou — " + ex.GetBaseException().Message); }
        }

        private void RefreshControlStates()
        {
            if (_refreshing) return;                 // um tique lento não pode empilhar o próximo
            _refreshing = true;
            try
            {
                foreach (RibbonControl c in Controls)
                {
                    bool allowed = AllowedHere(c.CommandId, explain: false);
                    if (c.Enabled != allowed) c.Enabled = allowed;
                }
            }
            catch { /* estado do botão é conforto, nunca motivo de erro dentro da SE */ }
            finally { _refreshing = false; }
        }

        /// <summary>
        /// Boilerplate comum aos comandos: confere o pré-requisito declarado em
        /// <see cref="Specs"/> (tipo de documento + ambiente de modelagem), conecta, loga
        /// início/fim e envolve qualquer exceção em <see cref="Fail"/> — nenhuma exceção sobe
        /// direto pro Solid Edge por esquecimento de try/catch (achado A1 da revisão do
        /// add-in). Os 3 comandos de diagnóstico/gravador
        /// (IniciarLeitura/GravarLeitura/InspecionarSelecao) têm fluxo próprio
        /// (ConfirmDocParaGravacao) — ficam fora deste wrapper de propósito.
        /// </summary>
        private void Run(int commandId, Action<SolidEdgeConnector, dynamic, ElectrodeParams> body)
        {
            CommandSpec spec = Specs[commandId];
            if (!AllowedHere(commandId, explain: true)) return;

            dynamic app = ElectrodeAddIn.Current?.App;
            dynamic doc = null;
            try { doc = app?.ActiveDocument; } catch { }
            if (app == null || doc == null) { MessageBox.Show("Nenhum documento ativo.", "AutoEDM"); return; }

            try
            {
                Log.Info($"===== {spec.Title} (add-in) — modelagem {ModelingEnvironment.Name(ModelingEnvironment.Read(doc))} =====");
                body(SolidEdgeConnector.Attach(app), doc, LoadParams());
                Log.Info($"===== FIM ({spec.Title}) =====");
            }
            catch (Exception ex) { Fail(spec.Title.ToLowerInvariant(), ex); }
        }

        /// <summary>
        /// Configuração do usuário (revisão A3, docs/REVISAO-AutoEDM.md): lida uma vez por
        /// sessão do add-in de %LOCALAPPDATA%\AutoEDM\config.json (criado com os defaults de
        /// sempre se ainda não existir). Prefixo do eletrodo, tabela de Ra e mapa de cor de
        /// queima passam a vir daqui em vez de fixos no código.
        /// </summary>
        private static AutoEdmConfig _config;
        private static AutoEdmConfig Config => _config ?? (_config = AutoEdmConfig.LoadOrCreateDefault());

        private static ElectrodeParams LoadParams() => Config.ToElectrodeParams();

        /// <summary>Novo <see cref="ElectrodeBuilder"/> já com a tabela de Ra e o mapa de cor
        /// desta configuração — sem isso, a detecção de queima e o cálculo de GAP usariam
        /// sempre a paleta de fábrica mesmo com um config.json customizado.</summary>
        private static ElectrodeBuilder NewBuilder(SolidEdgeConnector connector) =>
            new ElectrodeBuilder(connector, offsetPolicy: Config.BuildOffsetPolicy(), raColorMap: Config.BuildColorMap());

        /// <summary>
        /// Avisa (não bloqueia) quando o documento ativo NÃO é uma PEÇA — o Gravador quase
        /// sempre registra ação manual numa peça (fechar/costurar/estender superfície), e
        /// `Application.ActiveDocument` segue a janela do SE com FOCO no momento do clique: se
        /// a janela da MONTAGEM estiver na frente (em vez da peça) quando o usuário clica
        /// "Iniciar leitura" OU "Gravar log", o snapshot sai do documento errado e o diff dá
        /// "nada mudou" mesmo com features novas de verdade na peça (2026-07-17, log real: os
        /// dois cliques leram '14595.101_EDM.asm' Type=3 — a peça nunca foi vista). Pega o erro
        /// NA HORA em vez de só no fim, quando o trabalho manual já foi feito.
        /// </summary>
        private static bool ConfirmDocParaGravacao(dynamic doc, string acao)
        {
            int type = -1; string name = "?";
            try { type = (int)doc.Type; } catch { }
            try { name = (string)doc.Name; } catch { }
            if (type == 1) return true; // 1 = igPartDocument — caso normal, sem aviso

            string tipoDesc = type == 3 ? "uma MONTAGEM" : type == 2 ? "um DESENHO" : $"tipo {type}";
            var r = MessageBox.Show(
                $"O documento ativo agora é {tipoDesc} ('{name}'), não uma PEÇA.\n\n" +
                "O Gravador normalmente registra ações numa PEÇA (fechar/costurar/estender superfície de queima). " +
                "Se a janela da peça não estiver em foco no SE neste clique, a gravação sai vazia ou compara o documento errado.\n\n" +
                $"Trocar para a janela da peça e clicar de novo é o mais seguro. Continuar mesmo assim ({acao})?",
                "AutoEDM — Gravador", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return r == DialogResult.Yes;
        }

        private static void Fail(string what, Exception ex)
        {
            Log.Error($"Falha ao {what}.", ex);
            string logPath = ElectrodeAddIn.Current?.LogPath;
            string detail = string.IsNullOrEmpty(logPath)
                ? "Detalhes técnicos no log em %LOCALAPPDATA%\\AutoEDM\\logs."
                : $"Detalhes técnicos no log:\n{logPath}";
            MessageBox.Show($"Não foi possível {what}.\n\n{detail}", "AutoEDM — erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
