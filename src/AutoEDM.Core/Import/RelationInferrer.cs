using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Import
{
    /// <summary>O que aconteceu com uma relação proposta.</summary>
    public sealed class RelationOutcome
    {
        public ProposedRelation Proposal { get; set; }
        public bool Created { get; set; }

        /// <summary>Com que valor de <c>NormalsAligned</c> ela ficou. A skill registra que o
        /// significado desse sinalizador NÃO está resolvido (duas fontes do projeto se
        /// contradizem) — então aqui ele não é presumido: é MEDIDO, tentando um valor, vendo se
        /// a peça se moveu, e trocando se moveu. Este campo é a evidência do que funcionou.</summary>
        public bool NormalsAligned { get; set; }

        public long DetailedStatus { get; set; }
        public long OccurrenceStatus { get; set; }

        /// <summary>O quanto a peça se mexeu (mm) com a relação aplicada. É o critério de
        /// aceitação: a montagem importada JÁ está na posição certa, logo relação boa é relação
        /// que não move nada.</summary>
        public double MovedMm { get; set; }

        public string Why { get; set; }
    }

    /// <summary>O resultado da etapa de relações.</summary>
    public sealed class RelationResult
    {
        public int Created { get; set; }
        public int Refused { get; set; }
        public int GroundsRemoved { get; set; }
        public int GroundsRestored { get; set; }
        public string DatumName { get; set; }

        public readonly List<RelationOutcome> Outcomes = new List<RelationOutcome>();
        public readonly List<string> Messages = new List<string>();

        public double WorstMovementMm { get; set; }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Created} relação(ões) criada(s), {Refused} recusada(s).");
            if (DatumName != null) sb.AppendLine($"Peça de referência (segue fixa): {DatumName}");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Maior movimento aceito: {0:0.0000} mm (relação que movesse mais foi desfeita).{1}",
                WorstMovementMm, Environment.NewLine);

            int aligned = 0, notAligned = 0;
            foreach (RelationOutcome o in Outcomes)
            {
                if (!o.Created) continue;
                if (o.NormalsAligned) aligned++; else notAligned++;
            }
            if (Created > 0)
                sb.AppendLine($"NormalsAligned medido: {aligned} com true, {notAligned} com false.");

            foreach (string m in Messages) sb.AppendLine(m);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Cria as relações de montagem ÓBVIAS numa montagem importada — e nenhuma outra.
    ///
    /// POR QUE ESTA ETAPA É A ARRISCADA, e como o risco é contido. O arquivo
    /// `references/assembly-relations.md` da skill registra três coisas medidas em campo que
    /// moldam todo este código:
    ///
    /// 1. <b>Toda ocorrência inserida nasce ATERRADA</b> (um <c>GroundRelation3d</c> automático).
    ///    Uma relação entre duas ocorrências aterradas volta <c>DetailedStatus = 4</c>
    ///    (BetweenFixed): relata sucesso e não governa nada. Então, para a relação valer, o lado
    ///    que deve obedecer tem de ser DESATERRADO antes — e é esse desaterro que cria o risco
    ///    de o solver mover a peça.
    /// 2. <b>O sinalizador <c>NormalsAligned</c> não tem significado confirmado.</b> Duas fontes
    ///    do próprio projeto se contradizem, e o re-teste de 2026-10-17 não conseguiu decidir.
    ///    Errá-lo gira a peça 180°.
    /// 3. <b>Uma chamada de relação que falha pode deixar uma relação órfã para trás</b>, e duas
    ///    dessas sobredeterminaram uma placa silenciosamente.
    ///
    /// A SAÍDA para os três é a mesma, e é o que esta classe faz: a montagem importada <b>já
    /// está na posição certa</b>. Isso dá um oráculo que não depende de resolver (2): uma
    /// relação correta é a que <b>resolve E não move nada</b>. Então cada relação é criada,
    /// medida e — se mexeu a peça — DESFEITA e tentada com o sinalizador invertido; se mexer das
    /// duas, é descartada e o aterro volta. O sinalizador deixa de ser presumido e passa a ser
    /// medido caso a caso, e a contagem de <c>Relations3d</c> é conferida antes e depois para
    /// varrer os órfãos de (3).
    ///
    /// Nada aqui é salvo: quem confere e salva a montagem é o usuário.
    /// </summary>
    public static class RelationInferrer
    {
        // Indexação de Body.Faces[...] — FeatureTopologyQueryTypeConstants (inteiros pequenos).
        private const int igQueryPlane = 6;
        private const int igQueryCylinder = 10;

        // Relation3dDetailedStatusConstants (confirmados no dump).
        private const long Solved = 1;
        private const long BetweenFixed = 4;
        private const long Unsatisfied = 5;

        // OccurrenceStatusConstants (confirmados no dump).
        private const long StatusOverDefined = 32776;

        /// <summary>Quanto uma peça pode se mover e a relação ainda ser aceita. Apertado de
        /// propósito: a posição importada é a verdade, não um ponto de partida.</summary>
        public const double MaxMovementMm = 0.01;

        /// <summary>Quantos vizinhos maiores examinar por peca. Mais que isto so acrescenta
        /// candidatos piores: as relacoes obvias estao com os vizinhos GRANDES e encostados.</summary>
        private const int MaxPartners = 8;

        /// <summary>Teto de faces lidas por ocorrência. Uma placa de molde cheia de furos tem
        /// centenas de faces, e as relações óbvias estão nas GRANDES — ler tudo só encareceria.</summary>
        private const int MaxFacesPerOccurrence = 400;

        /// <summary>
        /// Deduz e cria as relações. <paramref name="dryRun"/> apenas propõe, sem escrever —
        /// é o que alimenta a janela de conferência.
        /// </summary>
        public static RelationResult Run(object assemblyDocument, bool dryRun, int maxPerOccurrence = 3)
        {
            var result = new RelationResult();
            if (assemblyDocument == null) { result.Messages.Add("Nenhuma montagem."); return result; }

            dynamic doc = assemblyDocument;
            var ctx = new AssemblyContext(assemblyDocument);
            IReadOnlyList<OccurrenceInfo> occs = ctx.GetOccurrences();
            if (occs.Count < 2)
            {
                result.Messages.Add("A montagem tem menos de duas ocorrências — nada a relacionar.");
                return result;
            }

            // 1. Ler pose + faces de cada ocorrência, já em coordenadas da MONTAGEM, mais a
            //    caixa envolvente da peça na montagem — é ela que permite descartar pares
            //    distantes sem olhar face nenhuma.
            var poses = new OccurrenceTransform[occs.Count];
            var faces = new List<RelationFace>[occs.Count];
            var sizes = new double[occs.Count];
            var boxes = new double[occs.Count][];

            for (int i = 0; i < occs.Count; i++)
            {
                poses[i] = AssemblyContext.TryGetPose(occs[i]);
                if (poses[i] == null) { faces[i] = new List<RelationFace>(); continue; }
                faces[i] = ReadFaces(occs[i], poses[i], i, out double sizeMm);
                sizes[i] = sizeMm;
                boxes[i] = BoxOf(faces[i]);
            }

            // 2. A peça de REFERÊNCIA é a maior: numa base de molde é a placa que todo o resto
            //    encosta. Ela fica aterrada, e todas as outras passam a obedecer a ela ou a
            //    alguma já posicionada — o que dá uma árvore com raiz, em vez de um grafo onde
            //    duas peças se governam mutuamente.
            int datum = 0;
            for (int i = 1; i < occs.Count; i++) if (sizes[i] > sizes[datum]) datum = i;
            result.DatumName = occs[datum].Name;

            int[] order = OrderBySizeDesc(sizes, datum);

            // 3. Propor — e aqui esta a correcao do relato "poucas relacoes" (2026-10-18).
            //
            //    O QUE ESTAVA ERRADO: cada ocorrencia era comparada so contra uma lista de
            //    pecas "firmes", que comecava com a referencia e SO CRESCIA quando alguma
            //    proposta era achada. Como as pecas sao percorridas por TAMANHO — que nada tem
            //    a ver com quem encosta em quem —, bastava as primeiras nao tocarem a
            //    referencia para a lista nunca crescer: nada era proposto, e o silencio se
            //    propagava. Num projeto de 508 ocorrencias isso rendeu quase nenhuma relacao.
            //
            //    O QUE VALE AGORA: cada peca e relacionada a quem for MAIOR que ela e estiver
            //    EM CONTATO com ela. "Maior" da uma dependencia aciclica de graca (a referencia
            //    e a maior de todas e fica aterrada; ninguem governa quem o governa), e "em
            //    contato" e o que de fato decide a intencao do projeto, em vez da ordem
            //    acidental de processamento.
            //
            //    A poda por caixa envolvente nao e so velocidade: sem ela o laco e O(n^2) sobre
            //    as listas de faces inteiras — com 508 ocorrencias de ate centenas de faces da
            //    mais de 10^9 comparacoes de par de face, e a ferramenta nao termina.
            var proposals = new Dictionary<int, List<ProposedRelation>>();
            var rank = new int[occs.Count];
            for (int i = 0; i < order.Length; i++) rank[order[i]] = i + 1;   // 0 fica para o datum

            foreach (int m in order)
            {
                if (faces[m].Count == 0 || boxes[m] == null) continue;

                // Vizinhos MAIORES e encostados, dos maiores para os menores: uma placa e
                // referencia melhor que um calco.
                var partners = new List<int>();
                for (int f = 0; f < occs.Count; f++)
                {
                    if (f == m || faces[f].Count == 0 || boxes[f] == null) continue;
                    if (f != datum && rank[f] >= rank[m]) continue;          // so quem e maior
                    if (!BoxesTouch(boxes[f], boxes[m], RelationCandidates.ContactGapMm)) continue;
                    partners.Add(f);
                }
                partners.Sort((x, y) => sizes[y].CompareTo(sizes[x]));
                if (partners.Count > MaxPartners) partners.RemoveRange(MaxPartners, partners.Count - MaxPartners);

                var all = new List<ProposedRelation>();
                foreach (int f in partners)
                    all.AddRange(RelationCandidates.Between(f, faces[f], m, faces[m]));

                all.Sort((x, y) => y.Strength.CompareTo(x.Strength));
                List<ProposedRelation> chosen = RelationCandidates.Choose(all, maxPerOccurrence);
                if (chosen.Count > 0) proposals[m] = chosen;
            }

            if (dryRun)
            {
                foreach (var kv in proposals)
                    foreach (ProposedRelation p in kv.Value)
                        result.Outcomes.Add(new RelationOutcome { Proposal = p, Created = false, Why = "proposta (nada escrito)" });
                result.Messages.Add(DescribeProposals(proposals, occs));
                return result;
            }

            // 4. Criar, medindo cada uma.
            var baseline = new Dictionary<int, OccurrenceTransform>();
            for (int i = 0; i < occs.Count; i++) if (poses[i] != null) baseline[i] = poses[i];
            var movable = new List<int>();

            foreach (var kv in proposals)
            {
                int m = kv.Key;
                bool ungrounded = RemoveGround(doc, occs[m].Name);
                if (ungrounded) { result.GroundsRemoved++; movable.Add(m); }

                int createdHere = 0;
                foreach (ProposedRelation p in kv.Value)
                {
                    RelationOutcome o = TryCreate(doc, occs, p, baseline, movable);
                    result.Outcomes.Add(o);
                    if (o.Created)
                    {
                        createdHere++;
                        result.Created++;
                        if (o.MovedMm > result.WorstMovementMm) result.WorstMovementMm = o.MovedMm;
                    }
                    else result.Refused++;
                }

                // Nada colou nesta peça: devolve o aterro, para ela não ficar solta na montagem.
                if (createdHere == 0 && ungrounded)
                {
                    if (RestoreGround(doc, occs[m].ComOccurrence)) result.GroundsRestored++;
                    movable.Remove(m);
                }
            }

            LogResult(result, occs);
            return result;
        }

        // ------------------------------------------------------------------ criação medida

        /// <summary>
        /// Cria UMA relação e a submete ao teste que vale: a peça se moveu? Tenta
        /// <c>NormalsAligned = true</c>, mede, e se moveu desfaz e tenta <c>false</c>. Se as
        /// duas moverem, descarta — e varre relações órfãs que a tentativa possa ter deixado.
        /// </summary>
        private static RelationOutcome TryCreate(dynamic doc, IReadOnlyList<OccurrenceInfo> occs,
            ProposedRelation p, Dictionary<int, OccurrenceTransform> baseline, List<int> movable)
        {
            var outcome = new RelationOutcome { Proposal = p };
            var tried = new List<string>();

            foreach (bool aligned in new[] { true, false })
            {
                int before = RelationCount(doc);
                object rel = null;
                string err = null;

                try { rel = Create(doc, occs, p, aligned); }
                catch (Exception ex) { err = ex.GetBaseException().Message; }

                if (rel == null)
                {
                    // Armadilha 4 da skill: a chamada falhou mas pode ter criado relação. Varre.
                    int orphans = DeleteRelationsAbove(doc, before);
                    tried.Add($"aligned={aligned}: {err ?? "devolveu nulo"}" +
                              (orphans > 0 ? $" (+{orphans} órfã(s) removida(s))" : ""));
                    continue;
                }

                long ds = StatusOf(rel, "DetailedStatus");
                long os = StatusOf(occs[p.MovingIndex].ComOccurrence, "Status");
                double moved = WorstMovement(occs, baseline, movable);

                bool good = (ds == Solved || ds == 0) && os != StatusOverDefined && moved <= MaxMovementMm;
                if (good)
                {
                    outcome.Created = true;
                    outcome.NormalsAligned = aligned;
                    outcome.DetailedStatus = ds;
                    outcome.OccurrenceStatus = os;
                    outcome.MovedMm = moved;
                    outcome.Why = $"criada com aligned={aligned}, status {ds}, moveu {moved:0.0000} mm";
                    return outcome;
                }

                string reason = moved > MaxMovementMm
                    ? $"moveu a peça {moved:0.0000} mm"
                    : ds == BetweenFixed ? "inerte (os dois lados aterrados)"
                    : ds == Unsatisfied ? "não satisfeita (status 5)"
                    : os == StatusOverDefined ? "deixou a peça sobredeterminada"
                    : $"status {ds}";
                tried.Add($"aligned={aligned}: {reason}");

                DeleteRelation(rel);
                DeleteRelationsAbove(doc, before);   // e qualquer resto que a tentativa deixou
            }

            outcome.Created = false;
            outcome.Why = "recusada — " + string.Join("; ", tried.ToArray());
            return outcome;
        }

        /// <summary>
        /// <c>Relations3d.AddAxial(Axis1, Axis2, NormalsAligned)</c> e
        /// <c>Relations3d.AddPlanar(Plane1, Plane2, NormalsAligned, ConstrainingPoint1, ConstrainingPoint2)</c>
        /// — assinaturas confirmadas no dump (dispids 6 e 5). <c>AddPlanar</c> tem 5 parâmetros e
        /// NENHUM opcional: não existe a forma sem os pontos.
        ///
        /// As faces são RELIDAS do documento da ocorrência aqui, imediatamente antes de criar a
        /// referência: proxy de face envelhece quando a montagem se regenera, e
        /// <c>CreateReference</c> com face velha dá E_FAIL (armadilha 5 da skill).
        /// </summary>
        private static object Create(dynamic doc, IReadOnlyList<OccurrenceInfo> occs,
            ProposedRelation p, bool aligned)
        {
            object f1 = Fresh(occs[p.FixedIndex], p.FixedFace);
            object f2 = Fresh(occs[p.MovingIndex], p.MovingFace);
            if (f1 == null || f2 == null) return null;

            dynamic r1 = doc.CreateReference(occs[p.FixedIndex].ComOccurrence, f1);
            dynamic r2 = doc.CreateReference(occs[p.MovingIndex].ComOccurrence, f2);
            if (r1 == null || r2 == null) return null;

            if (p.Kind == ProposedRelationKind.Axial)
                return (object)doc.Relations3d.AddAxial(r1, r2, aligned);

            // Pontos de restrição: vértices REAIS das faces (centro de caixa envolvente pode cair
            // dentro de um furo — nota da skill). A skill também registra que, com a ocorrência
            // movida, o ponto precisaria vir transformado pela ocorrência, e que isso NÃO foi
            // exercitado em teste; aqui ele vai em coordenadas da MONTAGEM, que é a leitura
            // fisicamente coerente, e a conferência de movimento pega o caso de estar errado.
            object pt1 = ToMeters(p.FixedFace.AnchorMm);
            object pt2 = ToMeters(p.MovingFace.AnchorMm);
            return (object)doc.Relations3d.AddPlanar(r1, r2, aligned, ref pt1, ref pt2);
        }

        /// <summary>O pior deslocamento, em mm, entre a pose guardada e a atual, das ocorrências
        /// que PODEM se mover (as desaterradas). As aterradas não entram: o solver não as move, e
        /// lê-las a cada tentativa custaria uma travessia COM por peça da montagem.</summary>
        private static double WorstMovement(IReadOnlyList<OccurrenceInfo> occs,
            Dictionary<int, OccurrenceTransform> baseline, List<int> movable)
        {
            double worst = 0;
            foreach (int i in movable)
            {
                OccurrenceTransform was;
                if (!baseline.TryGetValue(i, out was)) continue;
                OccurrenceTransform now = AssemblyContext.TryGetPose(occs[i]);
                if (now == null) continue;

                double dx = Units.MToMm(was.TxM - now.TxM);
                double dy = Units.MToMm(was.TyM - now.TyM);
                double dz = Units.MToMm(was.TzM - now.TzM);
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                // Rotação também conta: um giro de 180° em torno do centro de uma peça simétrica
                // quase não move a origem, e é justamente o estrago que o sinalizador errado faz.
                double rot = 0;
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                        rot = Math.Max(rot, Math.Abs(was.R(a, b) - now.R(a, b)));
                // Converte o erro de rotação num deslocamento equivalente na ponta da peça.
                d = Math.Max(d, rot * 100.0);

                if (d > worst) worst = d;
            }
            return worst;
        }

        // ------------------------------------------------------------------ aterro

        /// <summary>
        /// Remove o <c>GroundRelation3d</c> de uma ocorrência. Varre de TRÁS para a frente porque
        /// <c>Delete()</c> reindexa a coleção, e protege cada leitura: há relações cujo
        /// <c>Occurrence</c> devolve E_FAIL, e apagar o item errado é destrutivo (receita da skill).
        /// </summary>
        private static bool RemoveGround(dynamic doc, string occurrenceName)
        {
            try
            {
                for (int i = RelationCount(doc); i >= 1; i--)
                {
                    dynamic r;
                    try { r = doc.Relations3d.Item(i); } catch { continue; }

                    string owner;
                    try { owner = (string)r.Occurrence.Name; } catch { continue; }
                    if (owner != occurrenceName) continue;

                    try { r.Delete(); return true; } catch { return false; }
                }
            }
            catch (Exception ex) { Log.Warn("[IMPORT] remoção de aterro falhou: " + ex.GetBaseException().Message); }
            return false;
        }

        private static bool RestoreGround(dynamic doc, object occ)
        {
            try { return doc.Relations3d.AddGround(occ) != null; }
            catch (Exception ex)
            {
                Log.Warn("[IMPORT] não deu para devolver o aterro: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private static int RelationCount(dynamic doc)
        {
            try { return (int)doc.Relations3d.Count; } catch { return 0; }
        }

        /// <summary>Apaga tudo o que passou de <paramref name="countBefore"/> — a varredura de
        /// órfãos da armadilha 4.</summary>
        private static int DeleteRelationsAbove(dynamic doc, int countBefore)
        {
            int removed = 0;
            for (int i = RelationCount(doc); i > countBefore; i--)
            {
                try { doc.Relations3d.Item(i).Delete(); removed++; }
                catch { break; }
            }
            return removed;
        }

        private static void DeleteRelation(object rel)
        {
            try { ((dynamic)rel).Delete(); }
            catch (Exception ex) { Log.Warn("[IMPORT] não deu para desfazer a relação: " + ex.GetBaseException().Message); }
        }

        private static long StatusOf(object com, string member)
        {
            try
            {
                dynamic d = com;
                return Convert.ToInt64(member == "Status" ? d.Status : d.DetailedStatus);
            }
            catch { return 0; }
        }

        // ------------------------------------------------------------------ leitura das faces

        /// <summary>
        /// As faces planas e cilíndricas de uma ocorrência, levadas para coordenadas da MONTAGEM
        /// e em mm. Só o que é paralelo a X/Y/Z entra — ver a nota de <see cref="RelationFace"/>.
        /// </summary>
        private static List<RelationFace> ReadFaces(OccurrenceInfo occ, OccurrenceTransform pose,
            int index, out double sizeMm)
        {
            var list = new List<RelationFace>();
            sizeMm = 0;
            try
            {
                dynamic doc = occ.OccurrenceDocument;
                if (doc == null) return list;
                string why;
                object body = BodySampler.SolidBodyOf((object)doc, out why);
                if (body == null) return list;

                double[] bmin, bmax;
                if (FaceGeometry.TryGetBodyRangeMm(body, out bmin, out bmax))
                    sizeMm = Math.Max(bmax[0] - bmin[0], Math.Max(bmax[1] - bmin[1], bmax[2] - bmin[2]));

                AddPlanes(body, pose, index, list);
                AddCylinders(body, pose, index, list);
            }
            catch (Exception ex)
            {
                Log.Warn($"[IMPORT] faces de '{occ.Name}' ilegíveis: {ex.GetBaseException().Message}");
            }
            return list;
        }

        private static void AddPlanes(object body, OccurrenceTransform pose, int index, List<RelationFace> list)
        {
            dynamic faces;
            try { faces = ((dynamic)body).Faces[igQueryPlane]; } catch { return; }

            int n;
            try { n = (int)faces.Count; } catch { return; }
            if (n > MaxFacesPerOccurrence) n = MaxFacesPerOccurrence;

            for (int i = 1; i <= n; i++)
            {
                dynamic f;
                try { f = faces.Item(i); } catch { continue; }

                double areaMm2 = 0;
                try { areaMm2 = Convert.ToDouble(f.Area) * 1e6; } catch { }
                if (areaMm2 < RelationCandidates.MinContactAreaMm2) continue;

                double[] min, max;
                if (!FaceGeometry.TryGetRangeMm((object)f, out min, out max)) continue;
                ToAssembly(pose, min, max, out double[] amin, out double[] amax);

                // A normal é a direção em que a face plana não tem espessura. Se nenhuma das três
                // for ~zero, o plano está inclinado em relação aos eixos da montagem: fora.
                int axis = FlatAxis(amin, amax, 0.01);
                if (axis < 0) continue;

                double[] anchor;
                if (!AnchorOf((object)f, pose, out anchor))
                    anchor = new[] { (amin[0] + amax[0]) / 2, (amin[1] + amax[1]) / 2, (amin[2] + amax[2]) / 2 };

                list.Add(new RelationFace
                {
                    Kind = RelationFaceKind.Plane,
                    Com = (object)f,
                    FaceId = FaceIdOf((object)f),
                    OwnerIndex = index,
                    Axis = axis,
                    PlaneAtMm = (amin[axis] + amax[axis]) / 2.0,
                    MinMm = amin,
                    MaxMm = amax,
                    AreaMm2 = areaMm2,
                    AnchorMm = anchor
                });
            }
        }

        private static void AddCylinders(object body, OccurrenceTransform pose, int index, List<RelationFace> list)
        {
            dynamic faces;
            try { faces = ((dynamic)body).Faces[igQueryCylinder]; } catch { return; }

            int n;
            try { n = (int)faces.Count; } catch { return; }
            if (n > MaxFacesPerOccurrence) n = MaxFacesPerOccurrence;

            for (int i = 1; i <= n; i++)
            {
                dynamic f;
                try { f = faces.Item(i); } catch { continue; }

                // Raio pela GEOMETRIA, não pela caixa: numa face cilíndrica parcial a caixa mede
                // a corda. (Mesma razão registrada no alojamento de O'ring.)
                double rMm = BodySampler.RadiusMm((object)f);
                if (rMm <= 0) continue;

                double[] min, max;
                if (!FaceGeometry.TryGetRangeMm((object)f, out min, out max)) continue;
                ToAssembly(pose, min, max, out double[] amin, out double[] amax);

                // O eixo sai da ARESTA CIRCULAR, pela direção em que a caixa dela tem espessura
                // zero — a técnica já validada no O'ring. Direto da caixa da face não daria: um
                // cilindro tem extensão nos três eixos.
                int axis;
                double[] axisPoint;
                if (!AxisFromCircularEdge((object)f, pose, out axis, out axisPoint)) continue;

                double areaMm2 = 0;
                try { areaMm2 = Convert.ToDouble(f.Area) * 1e6; } catch { }

                list.Add(new RelationFace
                {
                    Kind = RelationFaceKind.Cylinder,
                    Com = (object)f,
                    FaceId = FaceIdOf((object)f),
                    OwnerIndex = index,
                    Axis = axis,
                    MinMm = amin,
                    MaxMm = amax,
                    AreaMm2 = areaMm2,
                    RadiusMm = rMm,
                    AxisPointMm = axisPoint
                });
            }
        }

        /// <summary>
        /// O eixo de uma face cilíndrica, pela aresta circular: a direção em que a caixa
        /// envolvente da aresta tem espessura zero, com as outras duas iguais (= círculo).
        /// Devolve o índice do eixo e o centro nos DOIS outros eixos, em coordenadas da montagem.
        /// </summary>
        private static bool AxisFromCircularEdge(object comFace, OccurrenceTransform pose,
            out int axis, out double[] axisPointMm)
        {
            axis = -1;
            axisPointMm = null;
            dynamic edges;
            try { edges = ((dynamic)comFace).Edges; } catch { return false; }
            if (edges == null) return false;

            int n;
            try { n = (int)edges.Count; } catch { return false; }

            for (int i = 1; i <= n; i++)
            {
                double[] min, max;
                object e;
                try { e = (object)edges.Item(i); } catch { continue; }
                if (!FaceGeometry.TryGetRangeMm(e, out min, out max)) continue;
                ToAssembly(pose, min, max, out double[] amin, out double[] amax);

                int flat = FlatAxis(amin, amax, 0.01);
                if (flat < 0) continue;

                int u = (flat + 1) % 3, v = (flat + 2) % 3;
                double du = amax[u] - amin[u], dv = amax[v] - amin[v];
                if (du <= 0.01 || Math.Abs(du - dv) > Math.Max(0.01, 0.01 * du)) continue;   // não é círculo

                axis = flat;
                axisPointMm = new[] { (amin[u] + amax[u]) / 2.0, (amin[v] + amax[v]) / 2.0 };
                return true;
            }
            return false;
        }

        /// <summary><c>Face.ID</c>, ou 0 quando a face não expõe identidade estável.</summary>
        private static int FaceIdOf(object comFace)
        {
            try { return Convert.ToInt32(((dynamic)comFace).ID); }
            catch { return 0; }
        }

        /// <summary>
        /// Um proxy NOVO da mesma face, reconquistado pelo <c>Face.ID</c> no corpo atual
        /// (<c>Body.GetFaceByFaceID</c>, dispid 514).
        ///
        /// Por que não basta reusar o objeto guardado na varredura: a montagem se REGENERA a cada
        /// relação criada, e depois disso <c>CreateReference</c> com a face lida antes devolve
        /// E_FAIL (armadilha 5 registrada na skill). Como as relações são criadas em sequência,
        /// só a primeira funcionaria. Cai para o proxy guardado quando não há ID — melhor tentar
        /// com ele do que desistir, e a conferência de movimento cobre o resultado de qualquer jeito.
        /// </summary>
        private static object Fresh(OccurrenceInfo occ, RelationFace face)
        {
            if (face.FaceId != 0)
            {
                try
                {
                    dynamic doc = occ.OccurrenceDocument;
                    string why;
                    object body = BodySampler.SolidBodyOf((object)doc, out why);
                    if (body != null)
                    {
                        dynamic f = ((dynamic)body).GetFaceByFaceID(face.FaceId);
                        if (f != null) return (object)f;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[IMPORT] releitura da face {face.FaceId} falhou ({ex.GetBaseException().Message}) — " +
                             "usando o proxy da varredura.");
                }
            }
            return face.Com;
        }

        /// <summary>Um vértice real da face, em coordenadas da montagem (mm).</summary>
        private static bool AnchorOf(object comFace, OccurrenceTransform pose, out double[] pointMm)
        {
            pointMm = null;
            try
            {
                dynamic verts = ((dynamic)comFace).Vertices;
                if (verts == null || (int)verts.Count < 1) return false;

                object buf = new double[3];
                ((dynamic)verts.Item(1)).GetPointData(ref buf);
                var raw = buf as double[];
                if (raw == null || raw.Length < 3) return false;

                pose.TransformPointM(raw[0], raw[1], raw[2], out double x, out double y, out double z);
                pointMm = new[] { Units.MToMm(x), Units.MToMm(y), Units.MToMm(z) };
                return true;
            }
            catch { return false; }
        }

        /// <summary>Caixa local (mm) -> caixa na montagem (mm), levando os 8 cantos pela pose.</summary>
        private static void ToAssembly(OccurrenceTransform pose, double[] minMm, double[] maxMm,
            out double[] outMin, out double[] outMax)
        {
            outMin = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            outMax = new[] { double.MinValue, double.MinValue, double.MinValue };
            for (int c = 0; c < 8; c++)
            {
                double x = Units.MmToM((c & 1) == 0 ? minMm[0] : maxMm[0]);
                double y = Units.MmToM((c & 2) == 0 ? minMm[1] : maxMm[1]);
                double z = Units.MmToM((c & 4) == 0 ? minMm[2] : maxMm[2]);
                pose.TransformPointM(x, y, z, out double ax, out double ay, out double az);
                double[] p = { Units.MToMm(ax), Units.MToMm(ay), Units.MToMm(az) };
                for (int k = 0; k < 3; k++)
                {
                    if (p[k] < outMin[k]) outMin[k] = p[k];
                    if (p[k] > outMax[k]) outMax[k] = p[k];
                }
            }
        }

        /// <summary>Caixa envolvente da ocorrencia na montagem (mm), a partir das faces ja
        /// lidas: [minX,minY,minZ,maxX,maxY,maxZ]. Null quando nao ha face.</summary>
        private static double[] BoxOf(List<RelationFace> faces)
        {
            if (faces == null || faces.Count == 0) return null;
            var b = new[] { double.MaxValue, double.MaxValue, double.MaxValue,
                            double.MinValue, double.MinValue, double.MinValue };
            foreach (RelationFace f in faces)
                for (int k = 0; k < 3; k++)
                {
                    if (f.MinMm[k] < b[k]) b[k] = f.MinMm[k];
                    if (f.MaxMm[k] > b[3 + k]) b[3 + k] = f.MaxMm[k];
                }
            return b;
        }

        /// <summary>As duas caixas se tocam (ou quase)? Pecas que nem se aproximam nao podem ter
        /// relacao obvia, e descarta-las aqui e o que torna o laco viavel.</summary>
        private static bool BoxesTouch(double[] a, double[] b, double gapMm)
        {
            for (int k = 0; k < 3; k++)
            {
                if (a[k] - b[3 + k] > gapMm) return false;
                if (b[k] - a[3 + k] > gapMm) return false;
            }
            return true;
        }

        /// <summary>O eixo em que a caixa é "achatada" (espessura ~zero), ou -1 se nenhum.</summary>
        private static int FlatAxis(double[] min, double[] max, double tolMm)
        {
            for (int i = 0; i < 3; i++) if (max[i] - min[i] <= tolMm) return i;
            return -1;
        }

        private static object ToMeters(double[] mm) => new[]
        {
            Units.MmToM(mm[0]), Units.MmToM(mm[1]), Units.MmToM(mm[2])
        };

        private static int[] OrderBySizeDesc(double[] sizes, int datum)
        {
            var idx = new List<int>();
            for (int i = 0; i < sizes.Length; i++) if (i != datum) idx.Add(i);
            idx.Sort((a, b) => sizes[b].CompareTo(sizes[a]));
            return idx.ToArray();
        }

        private static string DescribeProposals(Dictionary<int, List<ProposedRelation>> proposals,
            IReadOnlyList<OccurrenceInfo> occs)
        {
            var sb = new StringBuilder();
            int total = 0;
            foreach (var kv in proposals) total += kv.Value.Count;
            sb.AppendLine($"{total} relação(ões) proposta(s) em {proposals.Count} peça(s):");
            int shown = 0;
            foreach (var kv in proposals)
            {
                if (shown++ >= 12) { sb.AppendLine("  ..."); break; }
                sb.AppendLine($"  {occs[kv.Key].Name}:");
                foreach (ProposedRelation p in kv.Value)
                    sb.AppendLine($"     {p.Describe()} -> {occs[p.FixedIndex].Name}");
            }
            return sb.ToString();
        }

        private static void LogResult(RelationResult r, IReadOnlyList<OccurrenceInfo> occs)
        {
            Log.Info($"[IMPORT] relações: {r.Created} criada(s), {r.Refused} recusada(s); " +
                     $"referência '{r.DatumName}'; {r.GroundsRemoved} aterro(s) removido(s), " +
                     $"{r.GroundsRestored} devolvido(s); pior movimento {r.WorstMovementMm:0.0000} mm.");
            foreach (RelationOutcome o in r.Outcomes)
                Log.Info($"[IMPORT]   {(o.Created ? "+" : "-")} {o.Proposal.Describe()}: {o.Why}");
        }
    }
}
