using System;
using System.Collections.Generic;
using System.Globalization;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Import;

namespace AutoEDM.Tools.ImportScanProbe
{
    /// <summary>
    /// Sonda externa da limpeza de importação. SÓ LEITURA, por construção: chama
    /// <see cref="AssemblyUnifier.Scan"/> e a proposta de relações em modo seco, e nada mais.
    /// Nenhuma escrita, nenhum save, nenhuma troca de ambiente.
    ///
    /// Responde às perguntas que os testes de unidade não podem responder, porque dependem do
    /// Solid Edge de verdade:
    ///   1. A montagem importada carrega a posição na OCORRÊNCIA ou assada na GEOMETRIA?
    ///   2. <c>Face.Vertices</c> + <c>Vertex.GetPointData</c> funcionam em peça importada
    ///      (corpo que não foi modelado aqui)?
    ///   3. Quantas peças repetidas existem de fato, e o encaixe fecha dentro da tolerância?
    ///   4. Há pares ESPELHADOS (mão esquerda/direita) na montagem?
    ///   5. Quais relações óbvias a posição atual já satisfaz?
    /// </summary>
    internal static class Program
    {
        [STAThread]   // obrigatório para COM com a Solid Edge
        private static int Main(string[] args)
        {
            double tolMm = AssemblyUnifier.DefaultToleranceMm;
            bool allowMirror = false;
            bool relations = true;

            foreach (string a in args)
            {
                if (a.StartsWith("--tol=", StringComparison.OrdinalIgnoreCase))
                    double.TryParse(a.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out tolMm);
                else if (a.Equals("--espelho", StringComparison.OrdinalIgnoreCase)) allowMirror = true;
                else if (a.Equals("--sem-relacoes", StringComparison.OrdinalIgnoreCase)) relations = false;
            }

            bool ringsOnly = false;
            string ringsFile = null;
            string e2eSource = null;
            string e2eSecond = null;
            bool selfFit = false;
            string cmpPair = null;
            foreach (string a in args)
            {
                if (a.StartsWith("--aneis", StringComparison.OrdinalIgnoreCase))
                {
                    ringsOnly = true;
                    int eq = a.IndexOf('=');
                    if (eq > 0) ringsFile = a.Substring(eq + 1);
                }
                else if (a.StartsWith("--e2e=", StringComparison.OrdinalIgnoreCase))
                    e2eSource = a.Substring(6);
                else if (a.StartsWith("--e2e-b=", StringComparison.OrdinalIgnoreCase))
                    e2eSecond = a.Substring(8);
                else if (a.Equals("--autoencaixe", StringComparison.OrdinalIgnoreCase))
                    selfFit = true;
                else if (a.StartsWith("--comparar=", StringComparison.OrdinalIgnoreCase))
                    cmpPair = a.Substring(11);
            }

            Console.WriteLine("=== AutoEDM — sonda da limpeza de importação (SÓ LEITURA) ===");
            Console.WriteLine($"Build: {typeof(AssemblyUnifier).Assembly.Location}");
            try
            {
                Console.WriteLine($"       {System.IO.File.GetLastWriteTime(typeof(AssemblyUnifier).Assembly.Location):yyyy-MM-dd HH:mm:ss}");
            }
            catch { }
            Console.WriteLine($"Tolerância: {tolMm:0.###} mm | espelhadas: {(allowMirror ? "UNIFICAR" : "só identificar")}");
            Console.WriteLine();

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app;
                try
                {
                    // startIfNotRunning: FALSE, e isto não é detalhe. Subir instância é
                    // justamente a armadilha registrada na skill: Activator.CreateInstance
                    // abre uma SEGUNDA Solid Edge INVISÍVEL e a sonda rodaria inteira numa
                    // sessão que o Carlos não vê, relatando sucesso sobre nada.
                    app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERRO ao conectar: " + ex.GetBaseException().Message);
                    return 2;
                }

                if (app == null)
                {
                    Console.WriteLine("Nenhuma Solid Edge ABERTA encontrada.");
                    Console.WriteLine();
                    Console.WriteLine("Abra a Solid Edge, abra a MONTAGEM importada do outro CAD e rode de novo.");
                    Console.WriteLine("A sonda não abre o CAD sozinha de propósito: iniciar a SE por COM cria uma");
                    Console.WriteLine("instância INVISÍVEL, e o resultado seria sobre uma sessão que você não vê.");
                    return 1;
                }

                dynamic doc = connector.GetActiveDocument();
                if (doc == null) { Console.WriteLine("Nenhum documento ativo na Solid Edge."); return 1; }

                int type = -1;
                try { type = (int)doc.Type; } catch { }
                string name = "?";
                try { name = (string)doc.Name; } catch { }
                Console.WriteLine($"Documento ativo: {name} (Type = {type})");

                if (type != 3) // igAssemblyDocument
                {
                    Console.WriteLine("Não é uma MONTAGEM (.asm). Abra a montagem importada e rode de novo.");
                    return 1;
                }
                Console.WriteLine();

                if (ringsOnly) return ProbeRings((object)doc, ringsFile);
                if (e2eSource != null) return EndToEnd(app, e2eSource, e2eSecond, tolMm);
                if (selfFit) return SelfFit((object)doc, tolMm);
                if (cmpPair != null) return ComparePair((object)doc, cmpPair);

                // ---------------------------------------------------------- varredura
                Console.WriteLine("--- Varredura de peças repetidas -------------------------------");
                UnifyScan scan;
                try { scan = AssemblyUnifier.Scan((object)doc, tolMm, allowMirror); }
                catch (Exception ex)
                {
                    Console.WriteLine("ERRO na varredura: " + ex.GetBaseException());
                    return 3;
                }

                if (!scan.Ok) { Console.WriteLine("Varredura não concluiu: " + scan.Error); return 3; }
                Console.WriteLine(scan.Summary());

                // O detalhe que decide se vale confiar no resultado: o encaixe de cada membro.
                foreach (DuplicateGroup g in scan.Groups)
                {
                    Console.WriteLine($"  MESTRA {g.Master.FileName}");
                    Console.WriteLine($"      {g.Master.Signature.Describe()}");
                    Console.WriteLine($"      vértices lidos: {Count(g.Master.VerticesMm)}" +
                                      (g.Master.VerticesPartial ? "  [PARCIAL: " + g.Master.VerticesWhy + "]" : ""));
                    foreach (GroupMember m in g.Members)
                        Console.WriteLine($"    + {m.Part.FileName}: {m.Part.Occurrences.Count} posição(ões), " +
                                          $"giro {m.RotationDeg:0.0}°, desvio máx {m.MaxDeviationMm:0.0000} mm");
                    foreach (RejectedMember r in g.Rejected)
                        Console.WriteLine($"    - {r.Part.FileName} RECUSADA{(r.IsMirror ? " [ESPELHADA]" : "")}: {r.Why}");
                }

                if (scan.Unique.Count > 0)
                    Console.WriteLine($"\n  {scan.Unique.Count} peça(s) sem repetição.");

                // ---------------------------------------------------------- diagnóstico
                // POR QUE ISTO EXISTE: na 1ª rodada real a varredura devolveu "0 grupos" numa
                // base de molde que tem, visivelmente, a mesma bucha guia em dois arquivos. Sem
                // ver a impressão digital de cada arquivo não há como saber se elas se separaram
                // na peneira GROSSA (chaves diferentes, nunca comparadas) ou na FINA (comparadas
                // e reprovadas) — e são defeitos diferentes, com correções diferentes.
                Console.WriteLine();
                Console.WriteLine("--- Impressão digital por arquivo -----------------------------");
                var all = new List<ImportedPart>();
                all.AddRange(scan.Unique);
                foreach (DuplicateGroup g in scan.Groups)
                {
                    all.Add(g.Master);
                    foreach (GroupMember m in g.Members) all.Add(m.Part);
                    foreach (RejectedMember r in g.Rejected) all.Add(r.Part);
                }

                var buckets = new Dictionary<string, List<ImportedPart>>();
                foreach (ImportedPart p in all)
                {
                    if (p.Signature == null) continue;
                    string k = p.Signature.CoarseKey();
                    if (!buckets.ContainsKey(k)) buckets[k] = new List<ImportedPart>();
                    buckets[k].Add(p);
                }

                foreach (var kv in buckets)
                {
                    Console.WriteLine($"  balde [{kv.Key}]  ->  {kv.Value.Count} arquivo(s)");
                    foreach (ImportedPart p in kv.Value)
                    {
                        GeometrySignature s = p.Signature;

                        // Força o crivo FINO e a caixa envolvente mesmo em arquivo sem par: a
                        // varredura os lê por preguiça (só dentro de balde com 2+), e aqui o que
                        // se quer é justamente a evidência de que dois arquivos de nome parecido
                        // são ou não a mesma peça.
                        try
                        {
                            dynamic occ0 = p.Occurrences[0].Com;
                            string whyB;
                            object body = BodySampler.SolidBodyOf((object)occ0.OccurrenceDocument, out whyB);
                            if (body != null)
                            {
                                if (s.SortedFaceAreasMm2 == null) BodySampler.ReadFine(body, s);
                                double[] mn, mx;
                                if (AutoEDM.Selection.FaceGeometry.TryGetBodyRangeMm(body, out mn, out mx))
                                    Console.WriteLine($"     {p.FileName}  ({p.Occurrences.Count} pos.)  " +
                                        $"caixa {mx[0] - mn[0]:0.##} x {mx[1] - mn[1]:0.##} x {mx[2] - mn[2]:0.##} mm");
                                else Console.WriteLine($"     {p.FileName}  ({p.Occurrences.Count} pos.)");
                                string radii = s.SortedCylinderRadiiMm == null || s.SortedCylinderRadiiMm.Length == 0
                                    ? "nenhum"
                                    : string.Join(", ", Array.ConvertAll(s.SortedCylinderRadiiMm, r => (2 * r).ToString("0.###")));
                                Console.WriteLine($"        Ø dos cilindros: {radii}");
                            }
                            else Console.WriteLine($"     {p.FileName}  ({p.Occurrences.Count} pos.)  [corpo: {whyB}]");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"     {p.FileName}  ({p.Occurrences.Count} pos.)  [erro: {ex.GetBaseException().Message}]");
                        }
                        Console.WriteLine($"        vol {s.VolumeMm3:0.####} mm3 | {s.FaceCount}F {s.EdgeCount}A {s.VertexCount}V" +
                                          $" | área {s.AreaMm2:0.##} mm2" +
                                          $" | fino: {(s.SortedFaceAreasMm2 == null ? "NÃO LIDO" : s.SortedFaceAreasMm2.Length + " áreas, " + (s.SortedCylinderRadiiMm == null ? 0 : s.SortedCylinderRadiiMm.Length) + " raios")}" +
                                          (s.Partial ? $" | PARCIAL: {s.ReadError}" : ""));
                    }

                    // Dentro de um balde com mais de um arquivo, dizer EXATAMENTE por que o
                    // crivo fino separou cada par — é a informação que faltava.
                    if (kv.Value.Count > 1)
                        for (int i = 0; i < kv.Value.Count; i++)
                            for (int j = i + 1; j < kv.Value.Count; j++)
                            {
                                ImportedPart a2 = kv.Value[i], b2 = kv.Value[j];
                                bool ok = a2.Signature.Matches(b2.Signature, tolMm);
                                Console.WriteLine($"        crivo fino {a2.FileName} x {b2.FileName}: {(ok ? "PASSA" : "REPROVA")}");
                                if (!ok) Console.WriteLine("           " + WhyNotMatched(a2.Signature, b2.Signature, tolMm));
                            }
                }

                // ---------------------------------------------------------- relações (modo seco)
                if (relations)
                {
                    Console.WriteLine();
                    Console.WriteLine("--- Relações óbvias que a posição ATUAL já satisfaz -----------");
                    Console.WriteLine("(modo seco: nada é criado, nenhum aterro é removido)");
                    try
                    {
                        RelationResult proposed = RelationInferrer.Run((object)doc, dryRun: true);
                        if (proposed.Outcomes.Count == 0)
                            Console.WriteLine("Nenhuma. Lembre: só entram faces e eixos paralelos a X, Y ou Z da montagem.");
                        foreach (string m in proposed.Messages) Console.WriteLine(m);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERRO na proposta de relações: " + ex.GetBaseException());
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Nada foi alterado na montagem. Log completo em %LOCALAPPDATA%\\AutoEDM\\logs.");
                Log.Info("[SONDA] varredura de importação concluída (só leitura).");
                return 0;
            }
        }

        private static int Count(List<double[]> list) => list == null ? 0 : list.Count;

        /// <summary>
        /// Confirma no objeto VIVO as três chamadas de que o caso TORNEADO depende, antes de
        /// qualquer implementação: <c>Circle.Radius</c>, <c>Circle.GetCenterPoint</c> e
        /// <c>Circle.GetAxisVector</c>.
        ///
        /// Por que isto vem antes do código: o run real mostrou que bucha, pino e coluna têm
        /// ZERO vértices (numa peça torneada toda aresta é círculo), então o encaixe por vértices
        /// não as alcança. O substituto natural é o anel — centro + eixo + raio —, mas as duas
        /// chamadas de ponto são <c>[in,out] SAFEARRAY(double)</c>, a forma que já deu
        /// E_INVALIDARG neste projeto quando o array não chega alocado. Uma rodada de leitura
        /// responde se o caminho existe; escrever 300 linhas primeiro seria apostar.
        /// </summary>
        private static int ProbeRings(object assemblyDocument, string fileFilter)
        {
            const int igCircle = 167551105;
            Console.WriteLine("--- Sonda de ANÉIS (arestas circulares) -----------------------");
            Console.WriteLine("Confirma Circle.Radius / GetCenterPoint / GetAxisVector ao vivo.");
            Console.WriteLine();

            var ctx = new AssemblyContext(assemblyDocument);
            var seen = new HashSet<string>();

            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                string path;
                try { path = (string)((dynamic)occ.ComOccurrence).OccurrenceFileName; } catch { continue; }
                if (string.IsNullOrEmpty(path)) continue;
                string file = System.IO.Path.GetFileName(path);
                if (!seen.Add(file)) continue;
                if (fileFilter != null && file.IndexOf(fileFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                object body;
                try
                {
                    string why;
                    body = BodySampler.SolidBodyOf((object)((dynamic)occ.ComOccurrence).OccurrenceDocument, out why);
                    if (body == null) { Console.WriteLine($"{file}: {why}"); continue; }
                }
                catch (Exception ex) { Console.WriteLine($"{file}: {ex.GetBaseException().Message}"); continue; }

                int circles = 0, withCenter = 0, withAxis = 0, otherType = 0;
                string firstErr = null;
                var sample = new List<string>();

                try
                {
                    dynamic edges = ((dynamic)body).Edges[1]; // igQueryAll
                    int n = (int)edges.Count;
                    for (int i = 1; i <= n; i++)
                    {
                        dynamic e, geom;
                        try { e = edges.Item(i); geom = e.Geometry; } catch { continue; }
                        if (geom == null) continue;

                        int t;
                        try { t = Convert.ToInt32(geom.Type); } catch { continue; }
                        if (t != igCircle) { otherType++; continue; }
                        circles++;

                        double r = 0;
                        try { r = Convert.ToDouble(geom.Radius) * 1000.0; } catch { }

                        double[] c = TryVec3(geom, "GetCenterPoint", ref firstErr);
                        double[] ax = TryVec3(geom, "GetAxisVector", ref firstErr);
                        if (c != null) withCenter++;
                        if (ax != null) withAxis++;

                        if (sample.Count < 6 && c != null && ax != null)
                            sample.Add(string.Format(CultureInfo.InvariantCulture,
                                "Ø{0:0.###} centro ({1:0.##}, {2:0.##}, {3:0.##}) eixo ({4:0.###}, {5:0.###}, {6:0.###})",
                                2 * r, c[0] * 1000, c[1] * 1000, c[2] * 1000, ax[0], ax[1], ax[2]));
                    }
                }
                catch (Exception ex) { Console.WriteLine($"{file}: arestas ilegíveis: {ex.GetBaseException().Message}"); continue; }

                Console.WriteLine($"{file}");
                Console.WriteLine($"   círculos {circles} (outras curvas {otherType}) | centro OK {withCenter} | eixo OK {withAxis}" +
                                  (firstErr != null ? $" | 1º erro: {firstErr}" : ""));
                foreach (string s in sample) Console.WriteLine("      " + s);
            }
            Console.WriteLine();
            Console.WriteLine("Nada foi alterado na montagem.");
            return 0;
        }

        /// <summary>
        /// Compara DUAS peças com tolerância crescente e relata, separadamente, o melhor
        /// encaixe PRÓPRIO (rotação) e o melhor ESPELHADO (reflexão).
        ///
        /// Existe para decidir uma dúvida que não se resolve por veredito: quando a ferramenta
        /// diz "espelhadas" e o projetista diz "são iguais", a pergunta real é QUANTO falta para
        /// o encaixe próprio fechar. Se faltar 5 mm, são mesmo mão esquerda e mão direita. Se
        /// faltar 2 centésimos, o problema é a TOLERÂNCIA, não a quiralidade — e aí quem está
        /// certo é o projetista.
        /// </summary>
        private static int ComparePair(object assemblyDocument, string pair)
        {
            string[] parts = pair.Split('|');
            if (parts.Length != 2) { Console.WriteLine("Use --comparar=trechoA|trechoB"); return 1; }

            Console.WriteLine("--- Comparação dirigida: próprio x espelhado ------------------");
            var ctx = new AssemblyContext(assemblyDocument);
            var clouds = new Dictionary<string, List<double[]>>();
            var seen = new HashSet<string>();

            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                string path;
                try { path = (string)((dynamic)occ.ComOccurrence).OccurrenceFileName; } catch { continue; }
                if (string.IsNullOrEmpty(path)) continue;
                string file = System.IO.Path.GetFileName(path);
                if (!seen.Add(file)) continue;

                bool match = false;
                foreach (string frag in parts) if (file.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) match = true;
                if (!match) continue;

                object body;
                try
                {
                    string why;
                    body = BodySampler.SolidBodyOf((object)((dynamic)occ.ComOccurrence).OccurrenceDocument, out why);
                    if (body == null) { Console.WriteLine($"{file}: {why}"); continue; }
                }
                catch { continue; }

                bool partial; string vwhy;
                clouds[file] = BodySampler.ReadVertices(body, out partial, out vwhy);
                Console.WriteLine($"{file}: {clouds[file].Count} vértices" + (partial ? " [PARCIAL]" : ""));
            }

            if (clouds.Count < 2) { Console.WriteLine("Preciso de dois arquivos distintos."); return 1; }

            var names = new List<string>(clouds.Keys);
            for (int i = 0; i < names.Count; i++)
                for (int j = i + 1; j < names.Count; j++)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{names[i]}  x  {names[j]}");
                    foreach (bool mirror in new[] { false, true })
                    {
                        string label = mirror ? "ESPELHADO" : "PRÓPRIO  ";
                        bool done = false;
                        foreach (double tol in new[] { 0.005, 0.01, 0.02, 0.05, 0.1, 0.5, 2.0 })
                        {
                            RigidFit.Result f = RigidFit.Fit(clouds[names[i]], clouds[names[j]], tol, mirror);
                            // Com allowMirror=true o encaixe pode devolver o PRÓPRIO; só
                            // interessa aqui o que de fato veio espelhado.
                            if (f.Ok && f.IsMirror == mirror)
                            {
                                Console.WriteLine($"   {label}: fecha com tolerância {tol:0.###} mm " +
                                                  $"(desvio {f.MaxDeviationMm:0.0000} mm, giro {RigidFit.AngleDeg(f.R):0.0}°)");
                                done = true;
                                break;
                            }
                        }
                        if (!done) Console.WriteLine($"   {label}: NÃO fecha nem com 2 mm de tolerância.");
                    }
                }

            Console.WriteLine();
            Console.WriteLine("Leitura: se o PRÓPRIO fecha com tolerância pouco maior que a usada, as peças são");
            Console.WriteLine("iguais e o que faltou foi tolerância. Se só o ESPELHADO fecha, são mão esquerda");
            Console.WriteLine("e mão direita de verdade.");
            return 0;
        }

        /// <summary>
        /// AUTOENCAIXE: encaixa a nuvem de vértices de cada peça CONTRA ELA MESMA.
        ///
        /// A resposta certa é sempre a identidade. Se o encaixe devolver uma rotação, está
        /// provado que aquela nuvem de vértices admite MAIS DE UM movimento válido — e aí o
        /// código, que aceita o primeiro candidato que passa, pode escolher o errado e assentar
        /// a peça girada ou invertida. É o diagnóstico do relato "alguns parafusos ficaram
        /// invertidos".
        ///
        /// A razão de a ambiguidade existir: os vértices são um SUBCONJUNTO do corpo. Um
        /// sextavado interno de parafuso tem o anel de vértices do topo e o do fundo, iguais e
        /// paralelos — virar a peça 180° em torno de um eixo transversal troca um anel pelo
        /// outro e a NUVEM coincide, mesmo que o parafuso (que tem haste só para um lado) não.
        /// </summary>
        private static int SelfFit(object assemblyDocument, double tolMm)
        {
            Console.WriteLine("--- AUTOENCAIXE: cada peça contra ela mesma -------------------");
            Console.WriteLine("Esperado: identidade (giro 0°). Qualquer outro giro é AMBIGUIDADE.");
            Console.WriteLine();

            var ctx = new AssemblyContext(assemblyDocument);
            var seen = new HashSet<string>();
            int total = 0, ambiguous = 0, noVerts = 0;

            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                string path;
                try { path = (string)((dynamic)occ.ComOccurrence).OccurrenceFileName; } catch { continue; }
                if (string.IsNullOrEmpty(path)) continue;
                string file = System.IO.Path.GetFileName(path);
                if (!seen.Add(file)) continue;

                object body;
                try
                {
                    string why;
                    body = BodySampler.SolidBodyOf((object)((dynamic)occ.ComOccurrence).OccurrenceDocument, out why);
                    if (body == null) continue;
                }
                catch { continue; }

                bool partial;
                string vwhy;
                List<double[]> pts = BodySampler.ReadVertices(body, out partial, out vwhy);
                total++;
                if (pts.Count < 3) { noVerts++; continue; }

                RigidFit.Result f = RigidFit.Fit(pts, pts, tolMm, allowMirror: false);
                if (!f.Ok) { Console.WriteLine($"  {file}: autoencaixe FALHOU ({f.Why})"); continue; }

                double deg = RigidFit.AngleDeg(f.R);
                double shift = Math.Sqrt(f.TMm[0] * f.TMm[0] + f.TMm[1] * f.TMm[1] + f.TMm[2] * f.TMm[2]);
                if (deg > 0.5 || shift > tolMm)
                {
                    ambiguous++;
                    Console.WriteLine($"  AMBÍGUA {file}: {pts.Count} vértices, autoencaixe deu giro " +
                                      $"{deg:0.0}° e deslocamento {shift:0.###} mm (deveria ser 0)");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{total} peça(s) examinada(s): {ambiguous} AMBÍGUA(S), " +
                              $"{noVerts} sem vértices suficientes (caminho torneado).");
            Console.WriteLine(ambiguous > 0
                ? "A ambiguidade confirma que validar só por vértices não basta: o mesmo conjunto " +
                  "de vértices aceita vários movimentos, e o corpo inteiro não."
                : "Nenhuma ambiguidade por vértices nesta montagem.");
            return 0;
        }

        /// <summary>
        /// TESTE PONTA A PONTA em geometria REAL, reproduzindo o problema que o botão resolve:
        /// a MESMA peça em dois arquivos diferentes, em posições diferentes.
        ///
        /// O que faz: copia o .par indicado duas vezes para uma pasta TEMPORÁRIA própria, monta
        /// uma montagem NOVA (documento novo, nunca um do Carlos) com uma cópia em cada pose,
        /// mede a caixa envolvente de cada ocorrência EM COORDENADAS DA MONTAGEM, roda a
        /// unificação e mede de novo. A prova de que a posição foi preservada é a caixa na
        /// montagem ficar a mesma — não a matriz bater, que é o que o código já afirma de si.
        ///
        /// Não toca em arquivo nenhum do usuário e não salva a montagem de rascunho.
        /// </summary>
        private static int EndToEnd(dynamic app, string source, string second, double tolMm)
        {
            // Com `second`, o teste é NEGATIVO: duas peças DIFERENTES na mesma montagem, e o
            // esperado é que NADA seja unificado. É o teste que protege o molde — na base real
            // as duas buchas guia têm a mesma caixa, a mesma topologia e nomes quase iguais,
            // diferindo só no furo (Ø30 contra Ø32), porque uma delas é a centragem que impede
            // montar o molde ao contrário.
            bool negative = second != null;
            Console.WriteLine(negative
                ? "--- Teste ponta a ponta NEGATIVO (peças diferentes) ----------"
                : "--- Teste ponta a ponta (montagem de RASCUNHO) ----------------");
            if (!System.IO.File.Exists(source)) { Console.WriteLine("Arquivo de origem não existe: " + source); return 1; }
            if (negative && !System.IO.File.Exists(second)) { Console.WriteLine("Segundo arquivo não existe: " + second); return 1; }

            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoEDM_e2e_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(dir);
            string a = System.IO.Path.Combine(dir, "copia-A.par");
            string b = System.IO.Path.Combine(dir, "copia-B.par");
            System.IO.File.Copy(source, a);
            System.IO.File.Copy(negative ? second : source, b);
            Console.WriteLine($"Origem A: {System.IO.Path.GetFileName(source)}");
            Console.WriteLine($"Origem B: {System.IO.Path.GetFileName(negative ? second : source)}");
            Console.WriteLine($"Pasta de rascunho: {dir}");
            Console.WriteLine(negative
                ? "Peças DIFERENTES: o esperado é NENHUMA unificação."
                : "Duas cópias do MESMO arquivo = exatamente o que o tradutor de STEP produz.");
            Console.WriteLine();

            dynamic asm = null;
            try
            {
                asm = app.Documents.Add("SolidEdge.AssemblyDocument");

                // Duas poses bem diferentes, incluindo rotação: se a unificação estragar a
                // posição, estraga de forma visível.
                var poses = new List<OccurrenceTransform>
                {
                    OccurrenceTransform.Identity().WithTranslationM(0.137, -0.158, -0.096),
                    RotatedPose(0.025, 0.040, 0.010)
                };

                var paths = new[] { a, b };
                for (int i = 0; i < 2; i++)
                {
                    dynamic occ = asm.Occurrences.AddByFilename(paths[i]);
                    occ.GetType().InvokeMember("PutMatrix", System.Reflection.BindingFlags.InvokeMethod, null,
                        (object)occ, new object[] { poses[i].ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                }

                Console.WriteLine("ANTES da unificação:");
                Dictionary<string, double[]> before = AssemblyBoxes((object)asm);
                foreach (var kv in before) Console.WriteLine("   " + Fmt(kv));

                UnifyScan scan = AssemblyUnifier.Scan((object)asm, tolMm, false);
                Console.WriteLine();
                Console.WriteLine(scan.Summary());
                foreach (DuplicateGroup g in scan.Groups)
                    foreach (GroupMember m in g.Members)
                        Console.WriteLine($"   encaixe: {m.Part.FileName} -> {g.Master.FileName}, " +
                                          $"desvio {m.MaxDeviationMm:0.0000} mm, giro {m.RotationDeg:0.0}°, " +
                                          $"prova por {(m.ByRevolution ? "PERFIL DE REVOLUÇÃO (peça torneada)" : "vértices")}");

                if (negative)
                {
                    Console.WriteLine();
                    foreach (DuplicateGroup g in scan.Groups)
                        foreach (RejectedMember r in g.Rejected)
                            Console.WriteLine($"   recusa: {r.Part.FileName}: {r.Why}");
                    bool ok = scan.OccurrencesToReplace == 0;
                    Console.WriteLine(ok
                        ? "CORRETO: nada unificado — as peças são diferentes e a montagem fica intacta."
                        : "FALHOU: unificou peças DIFERENTES. Isto destruiria a centragem do molde.");
                    return ok ? 0 : 6;
                }

                if (scan.OccurrencesToReplace == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("FALHOU: duas cópias do mesmo arquivo deveriam ter sido reconhecidas.");
                    foreach (DuplicateGroup g in scan.Groups)
                        foreach (RejectedMember r in g.Rejected)
                            Console.WriteLine($"   recusa: {r.Part.FileName}: {r.Why}");
                    return 4;
                }

                UnifyResult res = AssemblyUnifier.Apply((object)asm, scan, tolMm);
                Console.WriteLine();
                Console.WriteLine(res.Summary());

                Console.WriteLine("DEPOIS da unificação:");
                Dictionary<string, double[]> after = AssemblyBoxes((object)asm);
                foreach (var kv in after) Console.WriteLine("   " + Fmt(kv));

                // A CONFERÊNCIA: o conjunto de caixas na montagem tem de ser o mesmo. Compara por
                // posição, não por nome — o nome da ocorrência MUDA na troca, e é esperado.
                Console.WriteLine();
                double worst = WorstBoxMismatch(before, after);
                Console.WriteLine(worst <= 0.01
                    ? $"POSIÇÃO PRESERVADA: pior diferença de caixa {worst:0.0000} mm."
                    : $"FALHOU: a geometria se moveu {worst:0.0000} mm.");
                return worst <= 0.01 ? 0 : 5;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO: " + ex.GetBaseException());
                return 3;
            }
            finally
            {
                // Fecha a montagem de rascunho SEM salvar e limpa a pasta temporária.
                try { if (asm != null) asm.Close(false); } catch { }
                try { System.IO.Directory.Delete(dir, true); } catch { }
            }
        }

        private static OccurrenceTransform RotatedPose(double xM, double yM, double zM)
        {
            // 90° em torno de Y, na arrumação [12,13,14] (vetor-linha) que a SE usa.
            var m = new double[16];
            m[0] = 0; m[1] = 0; m[2] = -1;
            m[4] = 0; m[5] = 1; m[6] = 0;
            m[8] = 1; m[9] = 0; m[10] = 0;
            m[12] = xM; m[13] = yM; m[14] = zM; m[15] = 1;
            return OccurrenceTransform.FromMatrix(m, new[] { xM, yM, zM });
        }

        /// <summary>Caixa envolvente de cada ocorrência EM COORDENADAS DA MONTAGEM (mm), pela
        /// pose aplicada aos 8 cantos da caixa local.</summary>
        private static Dictionary<string, double[]> AssemblyBoxes(object assemblyDocument)
        {
            var result = new Dictionary<string, double[]>();
            var ctx = new AssemblyContext(assemblyDocument);
            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                OccurrenceTransform pose = AssemblyContext.TryGetPose(occ);
                if (pose == null) continue;
                string why;
                object body;
                try { body = BodySampler.SolidBodyOf((object)((dynamic)occ.ComOccurrence).OccurrenceDocument, out why); }
                catch { continue; }
                if (body == null) continue;

                double[] mn, mx;
                if (!AutoEDM.Selection.FaceGeometry.TryGetBodyRangeMm(body, out mn, out mx)) continue;

                var lo = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
                var hi = new[] { double.MinValue, double.MinValue, double.MinValue };
                for (int c = 0; c < 8; c++)
                {
                    double x = ((c & 1) == 0 ? mn[0] : mx[0]) / 1000.0;
                    double y = ((c & 2) == 0 ? mn[1] : mx[1]) / 1000.0;
                    double z = ((c & 4) == 0 ? mn[2] : mx[2]) / 1000.0;
                    pose.TransformPointM(x, y, z, out double ax, out double ay, out double az);
                    double[] p = { ax * 1000, ay * 1000, az * 1000 };
                    for (int k = 0; k < 3; k++) { if (p[k] < lo[k]) lo[k] = p[k]; if (p[k] > hi[k]) hi[k] = p[k]; }
                }
                result[occ.Name] = new[] { lo[0], lo[1], lo[2], hi[0], hi[1], hi[2] };
            }
            return result;
        }

        private static string Fmt(KeyValuePair<string, double[]> kv) => string.Format(CultureInfo.InvariantCulture,
            "{0,-24} caixa na montagem ({1:0.##}..{2:0.##}, {3:0.##}..{4:0.##}, {5:0.##}..{6:0.##}) mm",
            kv.Key, kv.Value[0], kv.Value[3], kv.Value[1], kv.Value[4], kv.Value[2], kv.Value[5]);

        /// <summary>Para cada caixa de ANTES, a mais próxima de DEPOIS. Se alguma não tem par,
        /// a geometria se moveu.</summary>
        private static double WorstBoxMismatch(Dictionary<string, double[]> before, Dictionary<string, double[]> after)
        {
            double worst = 0;
            foreach (var b in before.Values)
            {
                double best = double.MaxValue;
                foreach (var a in after.Values)
                {
                    double d = 0;
                    for (int i = 0; i < 6; i++) d = Math.Max(d, Math.Abs(a[i] - b[i]));
                    best = Math.Min(best, d);
                }
                if (best > worst) worst = best;
            }
            return worst;
        }

        /// <summary>Uma chamada <c>[in,out] SAFEARRAY(double)</c> de 3 posições. O array vai
        /// ALOCADO — <c>ref null</c> dá E_INVALIDARG.</summary>
        private static double[] TryVec3(object com, string method, ref string firstErr)
        {
            try
            {
                object buf = new double[3];
                switch (method)
                {
                    case "GetCenterPoint": ((dynamic)com).GetCenterPoint(ref buf); break;
                    case "GetAxisVector": ((dynamic)com).GetAxisVector(ref buf); break;
                    default: return null;
                }
                var raw = buf as double[];
                return raw != null && raw.Length >= 3 ? raw : null;
            }
            catch (Exception ex)
            {
                if (firstErr == null) firstErr = method + ": " + ex.GetBaseException().Message;
                return null;
            }
        }

        /// <summary>Qual das comparações do crivo fino reprovou, e por quanto. Espelha a ordem
        /// de <see cref="GeometrySignature.Matches"/>.</summary>
        private static string WhyNotMatched(GeometrySignature a, GeometrySignature b, double tolMm)
        {
            if (a.Partial || b.Partial) return "leitura PARCIAL de um dos dois.";
            if (a.FaceCount != b.FaceCount) return $"faces: {a.FaceCount} x {b.FaceCount}";
            if (a.EdgeCount != b.EdgeCount) return $"arestas: {a.EdgeCount} x {b.EdgeCount}";
            if (a.VertexCount != b.VertexCount) return $"vértices: {a.VertexCount} x {b.VertexCount}";

            double typical = Math.Sqrt(Math.Max(a.AreaMm2, 1e-9));
            double dv = Math.Abs(a.VolumeMm3 - b.VolumeMm3), tv = tolMm * Math.Max(a.AreaMm2, 1e-9);
            if (dv > Math.Max(tv, 1e-9)) return $"volume difere {dv:0.######} mm3 (tolerado {tv:0.######})";

            double da = Math.Abs(a.AreaMm2 - b.AreaMm2), ta = tolMm * Math.Max(typical, 1e-9) * 4.0;
            if (da > Math.Max(ta, 1e-9)) return $"área difere {da:0.######} mm2 (tolerado {ta:0.######})";

            string s = ListDiff(a.SortedFaceAreasMm2, b.SortedFaceAreasMm2, tolMm * Math.Max(typical, 1e-9), "áreas de face");
            if (s != null) return s;
            s = ListDiff(a.SortedCylinderRadiiMm, b.SortedCylinderRadiiMm, tolMm, "raios de cilindro");
            if (s != null) return s;
            return "todas as comparações passaram (o crivo fino não é a causa).";
        }

        private static string ListDiff(double[] a, double[] b, double tol, string what)
        {
            if (a == null || b == null) return a == b ? null : $"{what}: um dos dois não foi lido";
            if (a.Length != b.Length) return $"{what}: {a.Length} x {b.Length} itens";
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i] - b[i]) > Math.Max(tol, 1e-9))
                    return $"{what}: item {i + 1} difere {Math.Abs(a[i] - b[i]):0.######} (tolerado {tol:0.######}) — {a[i]:0.####} x {b[i]:0.####}";
            return null;
        }
    }
}
