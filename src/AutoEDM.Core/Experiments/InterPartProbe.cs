using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Experiments
{
    /// <summary>
    /// SONDA DO INTER-PART — rodada 2 (2026-09-11), depois do inventário completo do que já
    /// falhou (logs 016–029, 13 execuções) e do que NUNCA rodou.
    ///
    /// **O que o inventário estabeleceu como FATO, e portanto NÃO se testa aqui:**
    /// <list type="bullet">
    /// <item>As opções globais 253/254 (<c>AllowInterPart</c>/<c>InterPartCopyCommand</c>) já
    /// foram ligadas (log 024, <c>True,True</c>) e o E_FAIL persistiu. Não é isso.</item>
    /// <item><c>Occurrence.Activate = true</c> NÃO entra em in-place: o log 029 imprimiu
    /// <c>ModelingInAssembly=False, InPlaceActivated=False</c> logo antes do E_FAIL.</item>
    /// <item><c>CopySurfaces.Add</c> com <c>Face[]</c> TIPADO funciona 67 vezes INTRA-peça. O
    /// E_FAIL não é de tipo, nem de marshaling, nem de nº de faces — é do cruzamento de peças.</item>
    /// <item>Não existe setter de <c>ModelingInAssembly</c>/<c>InPlaceActivated</c> em lugar
    /// nenhum da API (varredura exaustiva). Entrar em in-place por COM está fora.</item>
    /// </list>
    ///
    /// **O que esta sonda responde, em UMA rodada.** Cada bloco é independente e nunca lança —
    /// um bloco que quebra não impede os seguintes, porque o valor está em voltar com o mapa
    /// inteiro, não com a primeira resposta.
    ///
    /// <list type="number">
    /// <item><b>Estado</b> — o teste discriminante que o projeto nunca usou como guarda: em
    /// in-place de verdade o <c>ActiveDocument</c> é a PEÇA (Type=1), não a montagem (Type=3).
    /// Prova no log de 2026-07-17, com o Carlos em contexto à mão.</item>
    /// <item><b>CopyConstructions por ARQUIVO</b> — a rota recomendada. Lê do disco, então
    /// dispensa in-place por construção. É a ÚNICA com <c>CopyColors</c>, e a cor perdida é hoje
    /// um custo real: as superfícies copiadas à mão não carregam o Ra, e foi por isso que o
    /// GAP virou passo separado.</item>
    /// <item><b>Faces de <c>Occurrence.Body</c></b> — as 124 faces das 13 tentativas sempre
    /// saíram de <c>PartDocument.Body</c> (espaço da PEÇA). Para uma API que é "inter-part
    /// dentro da montagem", usar o corpo no espaço da MONTAGEM é a variante mais barata que
    /// sobrou, e nunca foi tentada.</item>
    /// <item><b>TopologyReference</b> — <c>Face.GetReferenceKey</c> →
    /// <c>Occurrence.CreateTopologyReference</c>. A própria memória do projeto elegeu este eixo
    /// como "a alternativa mais promissora" e ele nunca teve uma única execução.</item>
    /// <item><b><c>CreateReference2</c></b> — o irmão nunca testado do único método que
    /// aceitou argumentos inter-part (<c>CreateReference</c> criou 124 referências e foi
    /// abandonado depois de UM log, sem nunca ser consumido por ninguém).</item>
    /// <item><b><c>AddBodyByTag</c> / <c>AddCopiedPart</c></b> — trazer o CORPO em vez das
    /// faces. Se a tag do Parasolid valer na sessão, atravessa documentos sem arquivo nenhum.</item>
    /// <item><b>Mapa da API</b> — inventário dos comandos nativos por nome e assinaturas vivas
    /// dos objetos-chave, para alimentar a skill. Vale mesmo que tudo acima falhe.</item>
    /// </list>
    ///
    /// SEGURANÇA: trabalha numa peça DESCARTÁVEL (mesma disciplina do <c>ThreadProbe</c>) — uma
    /// chamada que falha pode envenenar o proxy do documento, e isso não pode respingar na
    /// cavidade do usuário. Não salva a montagem. Não altera a cavidade.
    /// </summary>
    public static class InterPartProbe
    {
        private const int igQueryAll = 1;

        // ApplicationGlobalConstants — lidos só para registrar no log (já se sabe que não é a causa)
        private const int seAllowInterPart = 253;
        private const int seInterPartCopyCommand = 254;
        private const int seDisplayInterPartCopies = 211;

        public static void Run(SolidEdgeConnector connector, dynamic asmDoc)
        {
            if (connector == null || connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = connector.Application;
            Log.Info("========== SONDA INTER-PART (rodada 2) ==========");

            OccurrenceInfo cavity = ResolveSelectedOccurrence(asmDoc);
            if (cavity == null)
            {
                Log.Warn("Sonda inter-part: selecione a ocorrência da CAVIDADE na montagem e rode de novo.");
                return;
            }
            Log.Info($"Cavidade: '{cavity.Name}'.");

            string cavityPath = TryGetDocumentPath(cavity.OccurrenceDocument);
            Log.Info($"Arquivo da cavidade: {(cavityPath ?? "(não legível)")}");

            Block("1. ESTADO (o guarda discriminante)", () => ProbeState(app, asmDoc, cavity));

            List<object> partFaces = CollectFaces(TryGetBody(cavity.OccurrenceDocument), "PartDocument.Body");
            List<object> occFaces = CollectFaces(TryGetOccurrenceBody(cavity), "Occurrence.Body");

            Block("2. COPYCONSTRUCTIONS POR ARQUIVO (rota recomendada)",
                () => ProbeCopyConstructions(app, cavityPath));

            Block("3. FACES DE Occurrence.Body (variante nunca tentada)",
                () => ProbeCrossPartCopy(app, occFaces, "Occurrence.Body"));

            Block("3b. FACES DE PartDocument.Body (controle — o E_FAIL conhecido)",
                () => ProbeCrossPartCopy(app, partFaces, "PartDocument.Body"));

            Block("4. TOPOLOGYREFERENCE (o eixo nunca executado)",
                () => ProbeTopologyReference(app, cavity, partFaces));

            Block("5. CreateReference2 (irmão nunca testado)",
                () => ProbeCreateReference2(asmDoc, cavity, partFaces));

            Block("6. CORPO em vez de FACES (AddBodyByTag / AddCopiedPart)",
                () => ProbeBodyRoutes(app, cavity, cavityPath));

            Block("7. MAPA DA API (para a skill)", () => ProbeApiMap(app, asmDoc, cavity));

            Log.Info("========== FIM DA SONDA INTER-PART ==========");
        }

        // ==================================================================== 1. estado

        /// <summary>
        /// O estado de in-place, lido em TODOS os lugares onde ele existe — não só na montagem,
        /// que era o único lugar onde o projeto olhava. <c>PartDocument.InPlaceActivated</c> está
        /// nos dumps desde o log 005 e nunca foi lido.
        ///
        /// É este bloco que dá sentido ao teste que só o usuário pode fazer: abrir a edição em
        /// contexto À MÃO e rodar a sonda. Se aqui aparecer Type=1/True e o bloco 3 passar, o
        /// problema deixa de ser "a API funciona?" e vira "como chegar neste estado".
        /// </summary>
        private static void ProbeState(dynamic app, dynamic asmDoc, OccurrenceInfo cavity)
        {
            LogProp("Application.ActiveDocument.Type", () =>
            {
                object active = (object)app.ActiveDocument;
                int type = Convert.ToInt32(((dynamic)active).Type);
                string name = Convert.ToString(((dynamic)active).Name);
                string kind = type == 1 ? "PEÇA — É ISTO que aparece em in-place REAL"
                            : type == 3 ? "MONTAGEM — foi o que o log 029 viu antes do E_FAIL"
                            : "outro";
                return $"{type} ({kind}); documento='{name}'";
            });

            LogProp("AssemblyDocument.ModelingInAssembly", () => Convert.ToString(asmDoc.ModelingInAssembly));
            LogProp("AssemblyDocument.InPlaceActivated", () => Convert.ToString(asmDoc.InPlaceActivated));

            // NUNCA LIDO ATÉ HOJE: a peça tem a sua própria flag.
            LogProp("PartDocument.InPlaceActivated (cavidade)",
                () => Convert.ToString(((dynamic)cavity.OccurrenceDocument).InPlaceActivated));
            LogProp("PartDocument.HasInterpartLinks (cavidade)",
                () => Convert.ToString(((dynamic)cavity.OccurrenceDocument).HasInterpartLinks));
            LogProp("PartDocument.GetInContextAssemblyNameForInterpartLinks",
                () => InvokeToString(cavity.OccurrenceDocument, "GetInContextAssemblyNameForInterpartLinks"));

            foreach (var g in new[]
            {
                Tuple.Create(seAllowInterPart, "AllowInterPart(253)"),
                Tuple.Create(seInterPartCopyCommand, "InterPartCopyCommand(254)"),
                Tuple.Create(seDisplayInterPartCopies, "DisplayInterPartCopies(211)")
            })
            {
                int id = g.Item1;
                LogProp("GlobalParameter " + g.Item2, () => ReadGlobalParameter(app, id));
            }
        }

        // ========================================= 2. CopyConstructions (rota recomendada)

        /// <summary>
        /// A rota que dispensa in-place por construção: a cavidade entra por ARQUIVO.
        ///
        /// Roda em SÍNCRONO e em ORDENADO, porque nenhuma fonte diz em qual modo a
        /// <c>Add</c> funciona — e o projeto já foi mordido por no-op SILENCIOSO no modo errado
        /// (<c>AddThickenFeature</c>). Cada tentativa confere <c>Status</c> e conta as faces,
        /// que é o único jeito de distinguir "funcionou" de "não fez nada e não reclamou".
        /// </summary>
        private static void ProbeCopyConstructions(dynamic app, string cavityPath)
        {
            if (string.IsNullOrEmpty(cavityPath))
            {
                Log.Warn("  Caminho da cavidade indisponível — bloco pulado (a Add exige FileName).");
                return;
            }

            foreach (int mode in new[] { 2, 1 }) // 2=ordenado primeiro (é feature de árvore)
            {
                dynamic part = null;
                try
                {
                    part = app.Documents.Add("SolidEdge.PartDocument");
                    TrySetModelingMode(part, mode);
                    string modeName = mode == 2 ? "ORDENADO" : "SÍNCRONO";
                    Log.Info($"  --- peça descartável em {modeName} ---");

                    object col;
                    try { col = (object)part.Constructions.CopyConstructions; }
                    catch (Exception ex) { Log.Warn("  Constructions.CopyConstructions indisponível: " + Msg(ex)); continue; }

                    ComDiagnostics.LogSignatures(col, "Add", "AddEx", "AddBodyByTag");

                    // 14 [in] + 1 [out]; o [out] volta como VALOR DE RETORNO no IDispatch::Invoke,
                    // igual ao CopySurfaces.Add que já funciona — nenhum ParameterModifier aqui.
                    object[] args =
                    {
                        cavityPath,                        //  1 FileName
                        1.0, 1.0, 1.0,                     //  2-4 escalas
                        Type.Missing,                      //  5 MirrorPlane
                        Type.Missing,                      //  6 FamilyOfPartsMember
                        Type.Missing,                      //  7 CoordinateSystem — na origem, 1º teste
                        true,                              //  8 IncludeDesignBody
                        false, false, false,               //  9-11 corpos de construção
                        Type.Missing, Type.Missing,        // 12-13 IncludeBodies
                        true                               // 14 CopyColors  ← a pergunta do bloco
                    };

                    object cc = null;
                    try
                    {
                        cc = col.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, col, args,
                            null, CultureInfo.InvariantCulture, null);
                        Log.Info($"  CopyConstructions.Add: OK -> {(cc == null ? "null" : cc.GetType().Name)}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"  CopyConstructions.Add FALHOU ({modeName}): {Msg(ex)}");
                        continue;
                    }
                    if (cc == null) continue;

                    LogFeatureStatus(cc, "CopyConstruction");

                    var faces = CollectFaces(cc, "CopyConstruction.Faces[igQueryAll]");
                    if (faces.Count == 0) { Log.Warn("  Cópia sem faces — CopyColors não tem o que provar."); continue; }

                    // A PROVA do CopyColors: o tally de cores do corpo copiado tem de bater com o
                    // da cavidade. Se vier tudo numa cor só, as cores NÃO sobreviveram.
                    LogColorTally(app, faces, "corpo copiado");

                    // E o pulo do gato: agora as faces são DESTA peça, então o CopySurfaces.Add
                    // roda no modo que comprovadamente funciona (67 sucessos) — INTRA-part.
                    TryCopySurfaces(part, faces.Take(Math.Min(faces.Count, 40)).ToList(), "intra-part sobre a cópia");
                }
                catch (Exception ex) { Log.Warn("  Bloco CopyConstructions: " + Msg(ex)); }
                finally { TryClose(part); }
            }
        }

        // ============================== 3. cruzamento de peças: de onde vêm as faces

        /// <summary>
        /// O <c>CopySurfaces.Add</c> cruzando peças, alimentado por faces de uma origem ou de
        /// outra. Espera-se E_FAIL com <c>PartDocument.Body</c> (é o resultado conhecido de 13
        /// execuções); o que interessa é se <c>Occurrence.Body</c> dá um erro DIFERENTE — erro
        /// diferente é a única pista de que o caminho é outro.
        /// </summary>
        private static void ProbeCrossPartCopy(dynamic app, List<object> faces, string origin)
        {
            if (faces.Count == 0) { Log.Warn($"  Sem faces de {origin} — bloco pulado."); return; }
            dynamic part = null;
            try
            {
                part = app.Documents.Add("SolidEdge.PartDocument");
                TryCopySurfaces(part, faces.Take(1).ToList(), $"1 face de {origin}");
                TryCopySurfaces(part, faces.Take(Math.Min(faces.Count, 40)).ToList(), $"{Math.Min(faces.Count, 40)} faces de {origin}");
            }
            catch (Exception ex) { Log.Warn("  Bloco cruzamento: " + Msg(ex)); }
            finally { TryClose(part); }
        }

        // ======================================= 4. TopologyReference (nunca executado)

        /// <summary>
        /// <c>Face.GetReferenceKey([out] ReferenceKey, [opt][out] KeySize)</c> na cavidade →
        /// <c>Occurrence.CreateTopologyReference(ReferenceKey, [out] TopologyReference)</c>.
        ///
        /// É a API DEDICADA de referência de topologia in-context, e a própria memória do
        /// projeto a elegeu como a alternativa mais promissora — sem nunca rodar uma vez. A
        /// chave é um <c>SAFEARRAY(byte)</c> por parâmetro de saída, então vale a mesma receita
        /// by-ref de sempre; sem ela a chave volta vazia e o teste mente.
        /// </summary>
        private static void ProbeTopologyReference(dynamic app, OccurrenceInfo cavity, List<object> partFaces)
        {
            if (partFaces.Count == 0) { Log.Warn("  Sem faces — bloco pulado."); return; }
            object face = partFaces[0];

            ComDiagnostics.LogSignatures(face, "GetReferenceKey");
            ComDiagnostics.LogSignatures((object)cavity.ComOccurrence, "CreateTopologyReference", "BindKeyToTopology", "GetReferenceKey");

            object key = null;
            try
            {
                object[] args = { new byte[0], Type.Missing };
                var mod = new ParameterModifier(2);
                mod[0] = true; mod[1] = true;
                face.GetType().InvokeMember("GetReferenceKey", BindingFlags.InvokeMethod, null, face, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                key = args[0];
                var arr = key as Array;
                Log.Info($"  Face.GetReferenceKey: OK — chave com {(arr == null ? 0 : arr.Length)} byte(s), KeySize={args[1]}");
                if (arr == null || arr.Length == 0) { Log.Warn("  Chave VAZIA — o [out] não populou; o resto do bloco não vale."); return; }
            }
            catch (Exception ex) { Log.Warn("  Face.GetReferenceKey falhou: " + Msg(ex)); return; }

            object topoRef = null;
            try
            {
                object[] args = { key, null };
                var mod = new ParameterModifier(2);
                mod[1] = true;
                object occ = (object)cavity.ComOccurrence;
                occ.GetType().InvokeMember("CreateTopologyReference", BindingFlags.InvokeMethod, null, occ, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                topoRef = args[1];
                Log.Info($"  Occurrence.CreateTopologyReference: OK -> {(topoRef == null ? "null (out não populou)" : topoRef.GetType().Name)}");
            }
            catch (Exception ex) { Log.Warn("  CreateTopologyReference falhou: " + Msg(ex)); return; }
            if (topoRef == null) return;

            ComDiagnostics.LogMembers("TopologyReference", topoRef);

            dynamic part = null;
            try
            {
                part = app.Documents.Add("SolidEdge.PartDocument");
                TryCopySurfacesRaw(part, new object[] { topoRef }, "CopySurfaces.Add(TopologyReference)");
                TryInterpart(part, new object[] { topoRef }, "InterpartConstructions.Add(TopologyReference)");
            }
            catch (Exception ex) { Log.Warn("  Consumo da TopologyReference: " + Msg(ex)); }
            finally { TryClose(part); }
        }

        // ================================================= 5. CreateReference2

        /// <summary>
        /// <c>CreateReference(occ, face)</c> FUNCIONOU no log 026 — 124 referências criadas, o
        /// único objeto que aceitou argumentos inter-part. Foi abandonado depois de um log, e o
        /// irmão <c>CreateReference2(Object: IDispatch, Entity: VARIANT)</c> nunca foi chamado.
        /// A diferença (<c>Occurrence*</c> tipado vs <c>IDispatch</c> genérico) é exatamente o
        /// tipo de coisa que resolve <c>E_NOINTERFACE</c>.
        /// </summary>
        private static void ProbeCreateReference2(dynamic asmDoc, OccurrenceInfo cavity, List<object> partFaces)
        {
            if (partFaces.Count == 0) { Log.Warn("  Sem faces — bloco pulado."); return; }
            ComDiagnostics.LogSignatures((object)asmDoc, "CreateReference", "CreateReference2");

            object r1 = TryInvoke(asmDoc, "CreateReference", new object[] { (object)cavity.ComOccurrence, partFaces[0] },
                "AssemblyDocument.CreateReference(occ, face)");
            object r2 = TryInvoke(asmDoc, "CreateReference2", new object[] { (object)cavity.ComOccurrence, partFaces[0] },
                "AssemblyDocument.CreateReference2(occ, face)");
            object r3 = TryInvoke(asmDoc, "CreateReference2", new object[] { (object)cavity.OccurrenceDocument, partFaces[0] },
                "AssemblyDocument.CreateReference2(partDoc, face)");

            foreach (var r in new[] { r1, r2, r3 })
                if (r != null) ComDiagnostics.LogMembers("Reference devolvida", r);
        }

        // ============================================ 6. corpo em vez de faces

        /// <summary>
        /// Trazer o CORPO, não as faces. <c>Models.AddBodyByTag(tag)</c> é o teste de cinco
        /// linhas: se a tag do Parasolid valer na SESSÃO (e não só no documento), atravessa
        /// documentos sem arquivo nenhum e sem in-place. <c>Models.AddCopiedPart</c> é o plano B
        /// da rota por arquivo — mais conveniente que <c>CopyConstructions</c>, mas SEM
        /// <c>CopyColors</c>, e sem cor some o critério que identifica a queima.
        /// </summary>
        private static void ProbeBodyRoutes(dynamic app, OccurrenceInfo cavity, string cavityPath)
        {
            object body = TryGetBody(cavity.OccurrenceDocument);
            int tag = 0;
            if (body != null)
            {
                try { tag = Convert.ToInt32(((dynamic)body).Tag); Log.Info($"  Body.Tag da cavidade = {tag}"); }
                catch (Exception ex) { Log.Warn("  Body.Tag ilegível: " + Msg(ex)); }
            }

            dynamic part = null;
            try
            {
                part = app.Documents.Add("SolidEdge.PartDocument");
                object models = (object)part.Models;
                ComDiagnostics.LogSignatures(models, "AddBodyByTag", "AddCopiedPart", "AddCopiedPartEx", "AddBodyFeature");

                if (tag != 0)
                {
                    object m = TryInvoke(models, "AddBodyByTag", new object[] { tag }, "Models.AddBodyByTag(tag da OUTRA peça)");
                    if (m != null) Log.Info("  ⇒ A tag atravessa documentos. Rota barata CONFIRMADA.");
                }

                if (!string.IsNullOrEmpty(cavityPath))
                {
                    object m2 = TryInvoke(models, "AddCopiedPart", new object[] { cavityPath }, "Models.AddCopiedPart(arquivo)");
                    if (m2 != null)
                    {
                        LogFeatureStatus(m2, "CopiedPart");
                        var faces = CollectFaces(m2, "CopiedPart");
                        if (faces.Count > 0) LogColorTally(app, faces, "AddCopiedPart (esperado: SEM cor)");
                    }
                }
            }
            catch (Exception ex) { Log.Warn("  Bloco corpo: " + Msg(ex)); }
            finally { TryClose(part); }
        }

        // ==================================================== 7. mapa da API (skill)

        /// <summary>
        /// Inventário para alimentar a skill. Vale mesmo que TODOS os blocos acima falhem — e é
        /// a única forma de fechar a pergunta do comando nativo: o enum
        /// <c>SolidEdgeCommandConstants</c> tem só 9 membros e nenhum de cópia, mas
        /// <c>Environment.CommandCategories</c> enumera todo comando por NOME e ID. Um dump e a
        /// incógnita morre.
        /// </summary>
        private static void ProbeApiMap(dynamic app, dynamic asmDoc, OccurrenceInfo cavity)
        {
            DumpCommands(app);

            ComDiagnostics.LogSignatures((object)app, "StartCommand", "CommandEnabled", "GetActiveCommand", "AbortCommand", "DoIdle");
            ComDiagnostics.LogMembers("Occurrence (cavidade)", (object)cavity.ComOccurrence);
            ComDiagnostics.LogMembers("PartDocument (cavidade)", (object)cavity.OccurrenceDocument);

            object ipl = null;
            try { ipl = (object)((dynamic)cavity.OccurrenceDocument).InterpartLinks; } catch { }
            if (ipl != null)
            {
                ComDiagnostics.LogMembers("PartDocument.InterpartLinks", ipl);
                try { Log.Info($"  InterpartLinks.Count = {((dynamic)ipl).Count}"); } catch { }
            }
            else Log.Info("  PartDocument.InterpartLinks indisponível nesta peça.");
        }

        /// <summary>
        /// Varre <c>Environment.CommandCategories</c> → <c>CommandCategory.Item(i)</c> →
        /// <c>CommandInfo{Caption,Id}</c> e grava tudo. Depois disto, dirigir o comando nativo
        /// deixa de depender de adivinhar ID. Loga só o que interessa (cópia/superfície/peça)
        /// para o log não virar um despejo de milhares de linhas — o arquivo completo vai para o
        /// disco ao lado do log.
        /// </summary>
        private static void DumpCommands(dynamic app)
        {
            string outPath = Path.Combine(Path.GetTempPath(), "AutoEDM_comandos_SE.txt");
            var interesting = new[] { "cópia", "copia", "copy", "superf", "surface", "peça", "part", "inter" };
            int total = 0, hits = 0;
            var sb = new System.Text.StringBuilder();

            try
            {
                dynamic envs = app.Environments;
                int envCount = 0;
                try { envCount = (int)envs.Count; } catch { }
                for (int e = 1; e <= envCount; e++)
                {
                    dynamic env;
                    string envName = "?";
                    try { env = envs.Item(e); envName = Convert.ToString(env.Name); } catch { continue; }

                    dynamic cats;
                    try { cats = env.CommandCategories; } catch { continue; }
                    int catCount = 0;
                    try { catCount = (int)cats.Count; } catch { }

                    for (int c = 1; c <= catCount; c++)
                    {
                        dynamic cat;
                        try { cat = cats.Item(c); } catch { continue; }
                        int cmdCount = 0;
                        try { cmdCount = (int)cat.Count; } catch { }

                        for (int k = 1; k <= cmdCount; k++)
                        {
                            try
                            {
                                dynamic info = cat.Item(k);
                                string caption = Convert.ToString(info.Caption);
                                int id = Convert.ToInt32(info.Id);
                                total++;
                                sb.AppendLine($"{envName}\t{id}\t{caption}");
                                string low = (caption ?? "").ToLowerInvariant();
                                if (interesting.Any(w => low.Contains(w)))
                                {
                                    Log.Info($"  [CMD] {envName} id={id} \"{caption}\"");
                                    hits++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                Log.Info($"  Comandos varridos: {total}; {hits} com cara de cópia/superfície.");
                try { File.WriteAllText(outPath, sb.ToString()); Log.Info($"  Lista COMPLETA gravada em {outPath}"); }
                catch (Exception ex) { Log.Warn("  Não foi possível gravar a lista: " + Msg(ex)); }
            }
            catch (Exception ex) { Log.Warn("  Varredura de comandos falhou: " + Msg(ex)); }
        }

        // ==================================================================== auxiliares

        /// <summary>Roda um bloco isolado: nunca deixa uma falha impedir os blocos seguintes.</summary>
        private static void Block(string title, Action body)
        {
            Log.Info("----- " + title + " -----");
            try { body(); }
            catch (Exception ex) { Log.Warn($"Bloco '{title}' abortou: " + Msg(ex)); }
        }

        /// <summary>
        /// <c>CopySurfaces.Add</c> com array TIPADO — a forma que funciona intra-part (67x). O
        /// array precisa ser <c>SolidEdgeGeometry.Face[]</c>: um <c>object[]</c> marshala como
        /// SAFEARRAY(VARIANT) e devolve DISP_E_TYPEMISMATCH, que foi o erro dos logs 016/017 e
        /// que NÃO é o erro que interessa medir aqui.
        /// </summary>
        private static void TryCopySurfaces(dynamic part, List<object> faces, string label)
        {
            try
            {
                var typed = new SolidEdgeGeometry.Face[faces.Count];
                for (int i = 0; i < faces.Count; i++) typed[i] = (SolidEdgeGeometry.Face)faces[i];
                object col = (object)part.Constructions.CopySurfaces;
                object r = col.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, col,
                    new object[] { typed.Length, typed, Type.Missing, Type.Missing },
                    null, CultureInfo.InvariantCulture, null);
                Log.Info($"  CopySurfaces.Add [{label}]: OK -> {(r == null ? "null" : r.GetType().Name)}");
            }
            catch (Exception ex) { Log.Warn($"  CopySurfaces.Add [{label}] FALHOU: {Msg(ex)}"); }
        }

        private static void TryCopySurfacesRaw(dynamic part, object[] items, string label)
        {
            try
            {
                object col = (object)part.Constructions.CopySurfaces;
                object r = col.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, col,
                    new object[] { items.Length, items, Type.Missing, Type.Missing },
                    null, CultureInfo.InvariantCulture, null);
                Log.Info($"  {label}: OK -> {(r == null ? "null" : r.GetType().Name)}");
            }
            catch (Exception ex) { Log.Warn($"  {label} FALHOU: {Msg(ex)}"); }
        }

        private static void TryInterpart(dynamic part, object[] items, string label)
        {
            try
            {
                object col = (object)part.Constructions.InterpartConstructions;
                object r = col.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, col,
                    new object[] { items.Length == 1 ? items[0] : items },
                    null, CultureInfo.InvariantCulture, null);
                Log.Info($"  {label}: OK -> {(r == null ? "null" : r.GetType().Name)}");
            }
            catch (Exception ex) { Log.Warn($"  {label} FALHOU: {Msg(ex)}"); }
        }

        private static object TryInvoke(object target, string method, object[] args, string label)
        {
            try
            {
                object r = target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args,
                    null, CultureInfo.InvariantCulture, null);
                Log.Info($"  {label}: OK -> {(r == null ? "null" : r.GetType().Name)}");
                return r;
            }
            catch (Exception ex) { Log.Warn($"  {label} FALHOU: {Msg(ex)}"); return null; }
        }

        /// <summary>
        /// <c>Status</c> tem um <c>[opt][out] Description</c>: sem <c>ParameterModifier</c> by-ref
        /// a descrição volta vazia e um falso "OK" passa batido — a mesma armadilha do
        /// <c>Face.GetRange</c>.
        /// </summary>
        private static void LogFeatureStatus(object feature, string label)
        {
            try
            {
                object[] args = { null };
                var mod = new ParameterModifier(1);
                mod[0] = true;
                object st = feature.GetType().InvokeMember("Status", BindingFlags.GetProperty, null, feature, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                Log.Info($"  {label}.Status = {st} (0x{Convert.ToInt32(st):X}); descrição: {args[0] ?? "(vazia)"}");
            }
            catch (Exception ex) { Log.Warn($"  {label}.Status ilegível: " + Msg(ex)); }
        }

        /// <summary>Histograma de cores — é ele que PROVA (ou desmente) o <c>CopyColors</c>.</summary>
        private static void LogColorTally(dynamic app, List<object> faces, string label)
        {
            var reader = new FaceStyleColorReader();
            var tally = new Dictionary<string, int>();
            int unread = 0;
            foreach (object f in faces)
            {
                Color c; string src;
                if (reader.TryReadColor(f, app, out c, out src))
                {
                    string k = $"RGB({c.R},{c.G},{c.B})";
                    tally[k] = tally.ContainsKey(k) ? tally[k] + 1 : 1;
                }
                else unread++;
            }
            Log.Info($"  Cores em {label}: {tally.Count} cor(es) distinta(s) em {faces.Count} face(s)" +
                     (unread > 0 ? $", {unread} ilegível(is)" : "") + ".");
            foreach (var kv in tally.OrderByDescending(k => k.Value).Take(8))
                Log.Info($"    {kv.Key}: {kv.Value} face(s)");
            if (tally.Count <= 1 && faces.Count > 4)
                Log.Warn("  ⇒ UMA cor só num corpo inteiro: as cores NÃO sobreviveram à cópia.");
        }

        private static List<object> CollectFaces(object owner, string label)
        {
            var result = new List<object>();
            if (owner == null) { Log.Warn($"  {label}: fonte nula."); return result; }
            try
            {
                dynamic faces = ((dynamic)owner).Faces[igQueryAll];
                int n = 0;
                try { n = (int)faces.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    try { result.Add(faces.Item(i)); } catch { }
                }
                Log.Info($"  {label}: {result.Count} face(s).");
            }
            catch (Exception ex) { Log.Warn($"  {label} inacessível: " + Msg(ex)); }
            return result;
        }

        private static object TryGetBody(dynamic partDoc)
        {
            try { return (object)partDoc.Models.Item(1).Body; } catch { return null; }
        }

        /// <summary>O corpo da OCORRÊNCIA — espaço da montagem. Nunca foi usado como fonte de faces.</summary>
        private static object TryGetOccurrenceBody(OccurrenceInfo occ)
        {
            try { return (object)((dynamic)occ.ComOccurrence).Body; }
            catch (Exception ex) { Log.Warn("  Occurrence.Body indisponível: " + Msg(ex)); return null; }
        }

        private static OccurrenceInfo ResolveSelectedOccurrence(dynamic asmDoc)
        {
            try
            {
                dynamic ss = asmDoc.SelectSet;
                int n = 0;
                try { n = (int)ss.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object item;
                    try { item = ss.Item(i); } catch { continue; }
                    try
                    {
                        dynamic occ = item;
                        string name = Convert.ToString(occ.Name);
                        dynamic doc = occ.OccurrenceDocument;
                        if (doc != null) return new OccurrenceInfo(occ, name, doc);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log.Warn("SelectSet ilegível: " + Msg(ex)); }
            return null;
        }

        private static string TryGetDocumentPath(dynamic doc)
        {
            try
            {
                string p = Convert.ToString(doc.FullName);
                return string.IsNullOrWhiteSpace(p) ? null : p;
            }
            catch { return null; }
        }

        private static void TrySetModelingMode(dynamic part, int mode)
        {
            try { part.ModelingMode = mode; }
            catch (Exception ex) { Log.Warn($"  ModelingMode={mode} recusado: " + Msg(ex)); }
        }

        /// <summary>Fecha a peça descartável SEM salvar. Cosmético: nunca decide o resultado.</summary>
        private static void TryClose(dynamic part)
        {
            if (part == null) return;
            try { ((object)part).GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, (object)part, new object[] { false }); }
            catch (Exception ex) { Log.Warn("  Fechar peça descartável: " + Msg(ex)); }
        }

        private static string ReadGlobalParameter(dynamic app, int id)
        {
            try
            {
                object[] args = { id, null };
                var mod = new ParameterModifier(2);
                mod[1] = true;
                object a = (object)app;
                a.GetType().InvokeMember("GetGlobalParameter", BindingFlags.InvokeMethod, null, a, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                return Convert.ToString(args[1]);
            }
            catch (Exception ex) { return "? (" + Msg(ex) + ")"; }
        }

        private static string InvokeToString(object target, string method)
        {
            try
            {
                object r = target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, new object[0]);
                return Convert.ToString(r);
            }
            catch (Exception ex) { return "? (" + Msg(ex) + ")"; }
        }

        private static void LogProp(string label, Func<string> read)
        {
            try { Log.Info($"  {label} = {read()}"); }
            catch (Exception ex) { Log.Warn($"  {label}: ilegível — " + Msg(ex)); }
        }

        private static string Msg(Exception ex)
        {
            var b = ex.GetBaseException();
            var com = b as System.Runtime.InteropServices.COMException;
            return com != null
                ? $"{com.Message} (HRESULT 0x{com.HResult:X8})"
                : $"{b.GetType().Name}: {b.Message}";
        }
    }
}
