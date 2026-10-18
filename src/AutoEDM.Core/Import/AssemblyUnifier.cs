using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Import
{
    /// <summary>
    /// A limpeza de um projeto importado de outro CAD: encontra as peças GEOMETRICAMENTE IGUAIS
    /// que chegaram como arquivos separados e faz todas as posições apontarem para UMA cópia,
    /// sem mexer um milímetro de onde cada uma está.
    ///
    /// O PROBLEMA. Ao importar STEP/Parasolid, cada posição da mesma peça vira um arquivo
    /// próprio: 48 pinos de extração iguais chegam como 48 arquivos. A montagem fica pesada, a
    /// lista de materiais mente (48 itens em vez de 1 item com quantidade 48) e qualquer
    /// alteração tem de ser repetida 48 vezes.
    ///
    /// A REGRA QUE GOVERNA ESTA CLASSE: a posição é intocável. Toda substituição é
    /// <b>conferida</b> — a pose é lida de volta do Solid Edge depois de escrita e comparada com
    /// a pretendida; se não bater, a ocorrência NOVA é desfeita e a ORIGINAL fica. Daí a ordem
    /// das operações ser acrescentar-conferir-apagar, e nunca apagar-acrescentar: se a inserção
    /// falhar no meio, o pior caso é uma ocorrência duplicada visível, não um buraco na montagem.
    ///
    /// O QUE NÃO FAZ, de propósito:
    /// - não apaga arquivo nenhum do disco (os órfãos vão para o relatório);
    /// - não salva a montagem (quem confere e salva é o usuário);
    /// - não unifica peça ESPELHADA (ver <see cref="RigidFit"/> — det(R) = -1);
    /// - não entra em subconjunto (só ocorrências do primeiro nível).
    ///
    /// API: <c>Occurrences.AddByFilename</c> + <c>Occurrence.PutMatrix</c> + <c>Occurrence.Delete</c>
    /// — as três já rodadas neste projeto (Nova peça, eletrodo, malha->sólido em lote).
    /// <c>Occurrence.Replace</c> EXISTE (dispid 34, 3 parâmetros, confirmado no dump e na
    /// introspecção ao vivo) e seria o caminho mais limpo, porque preserva a identidade da
    /// ocorrência; não é usado aqui porque o significado dos 3 parâmetros não está confirmado, e
    /// a regra de ouro do projeto é não inventar assinatura. <see cref="ProbeReplaceSignature"/>
    /// registra a assinatura real no log para uma sessão futura trocar o caminho com segurança.
    /// </summary>
    public static class AssemblyUnifier
    {
        /// <summary>Tolerância (mm) de "mesma geometria" e de conferência de posição. Tradução
        /// entre CADs não é bit-a-bit: 0,01 mm é folgado para o arredondamento do tradutor e
        /// apertado para não confundir duas peças de medidas vizinhas.</summary>
        public const double DefaultToleranceMm = 0.01;

        // ------------------------------------------------------------------ varredura (SÓ LEITURA)

        /// <summary>
        /// Varre a montagem e monta o plano. NÃO escreve nada. É esta função que alimenta a
        /// janela de conferência.
        /// </summary>
        public static UnifyScan Scan(object assemblyDocument, double tolMm, bool allowMirror)
        {
            var scan = new UnifyScan();
            if (assemblyDocument == null) { scan.Error = "nenhuma montagem."; return scan; }
            if (tolMm <= 0) tolMm = DefaultToleranceMm;

            var ctx = new AssemblyContext(assemblyDocument);
            IReadOnlyList<OccurrenceInfo> occs = ctx.GetOccurrences();
            scan.OccurrenceCount = occs.Count;
            if (occs.Count == 0) { scan.Error = "a montagem não tem ocorrências."; return scan; }

            // 1. Agrupar por ARQUIVO. Ler a geometria uma vez por arquivo, não uma vez por
            //    ocorrência: numa montagem já parcialmente limpa isso sozinho corta o trabalho.
            var byFile = new Dictionary<string, ImportedPart>(StringComparer.OrdinalIgnoreCase);
            int identityPoses = 0, posedCount = 0;

            foreach (OccurrenceInfo occ in occs)
            {
                var rec = new ImportedOccurrence { Com = occ.ComOccurrence, Name = occ.Name };

                if (IsSubassembly(occ))
                {
                    rec.Skipped = "é um subconjunto (só o primeiro nível é analisado).";
                    scan.Skipped.Add(rec);
                    continue;
                }

                string path = FileNameOf(occ);
                if (string.IsNullOrEmpty(path))
                {
                    rec.Skipped = "não deu para ler o arquivo que ela referencia.";
                    scan.Skipped.Add(rec);
                    continue;
                }
                rec.FilePath = path;

                OccurrenceTransform pose = AssemblyContext.TryGetPose(occ);
                if (pose == null)
                {
                    // Sem a pose não há como compor a posição nova — e chutar identidade aqui
                    // moveria a peça. Fora da análise.
                    rec.Skipped = "a pose (GetMatrix) não pôde ser lida.";
                    scan.Skipped.Add(rec);
                    continue;
                }
                rec.Pose = pose;
                if (IsIdentity(pose)) identityPoses++; else posedCount++;

                ImportedPart part;
                if (!byFile.TryGetValue(path, out part))
                {
                    part = new ImportedPart { FilePath = path, FileName = Path.GetFileName(path) };
                    byFile[path] = part;
                }
                part.Occurrences.Add(rec);
            }

            scan.DistinctFilesBefore = byFile.Count;
            scan.PlacementStyle = DescribePlacement(identityPoses, posedCount);

            // 2. Peneira grossa: volume + contagens, 4 leituras por arquivo.
            var parts = new List<ImportedPart>();
            foreach (ImportedPart p in byFile.Values)
            {
                object body = BodyOf(p, scan);
                if (body == null) continue;
                p.Signature = BodySampler.ReadCoarse(body);
                parts.Add(p);
            }

            var coarse = new Dictionary<string, List<ImportedPart>>();
            foreach (ImportedPart p in parts)
            {
                if (p.Signature.Partial)
                {
                    scan.Notes.Add($"  {p.FileName}: leitura incompleta ({p.Signature.ReadError}) — fora da unificação.");
                    scan.Unique.Add(p);
                    continue;
                }
                string key = p.Signature.CoarseKey();
                List<ImportedPart> bucket;
                if (!coarse.TryGetValue(key, out bucket)) coarse[key] = bucket = new List<ImportedPart>();
                bucket.Add(p);
            }

            // 3. Dentro de cada balde: crivo fino, depois o encaixe medido.
            foreach (List<ImportedPart> bucket in coarse.Values)
            {
                if (bucket.Count == 1) { scan.Unique.Add(bucket[0]); continue; }

                foreach (ImportedPart p in bucket)
                {
                    object body = BodyOf(p, scan);
                    if (body != null) BodySampler.ReadFine(body, p.Signature);
                }
                BuildGroups(bucket, scan, tolMm, allowMirror);
            }

            scan.Groups.Sort((a, b) => b.OccurrencesReplaced.CompareTo(a.OccurrencesReplaced));
            scan.Ok = true;
            LogScan(scan);

            // Junta no log a assinatura real de Occurrence.Replace enquanto há uma ocorrência à
            // mão. É só leitura e sai de graça aqui — e é o que vai permitir, numa sessão
            // futura, trocar o apaga-e-recria por uma troca que PRESERVA a identidade da
            // ocorrência (ver a nota da classe).
            if (occs.Count > 0) ProbeReplaceSignature(occs[0].ComOccurrence);
            return scan;
        }

        /// <summary>
        /// Monta os grupos dentro de um balde: pega o candidato mais "forte" como mestra, mede o
        /// encaixe de cada outro contra ela, e o que não encaixar vira semente de um grupo novo
        /// (ou peça única). O que encaixa só ESPELHANDO vai para os recusados, com o motivo.
        /// </summary>
        private static void BuildGroups(List<ImportedPart> bucket, UnifyScan scan, double tolMm, bool allowMirror)
        {
            // A mestra é a que já tem mais posições (se o tradutor reaproveitou alguma) e, no
            // empate, a de nome mais curto — nome importado costuma ganhar sufixo "_1", "_2",
            // então o mais curto tende a ser o original.
            bucket.Sort((a, b) =>
            {
                int byCount = b.Occurrences.Count.CompareTo(a.Occurrences.Count);
                if (byCount != 0) return byCount;
                int byLen = a.FileName.Length.CompareTo(b.FileName.Length);
                return byLen != 0 ? byLen : string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            });

            var pending = new List<ImportedPart>(bucket);
            while (pending.Count > 0)
            {
                ImportedPart master = pending[0];
                pending.RemoveAt(0);

                var group = new DuplicateGroup { Master = master };
                EnsureVertices(master, scan);

                var leftovers = new List<ImportedPart>();
                foreach (ImportedPart cand in pending)
                {
                    if (!master.Signature.Matches(cand.Signature, tolMm)) { leftovers.Add(cand); continue; }

                    EnsureVertices(cand, scan);

                    // CAMINHO TORNEADO, primeiro. Peça sem vértice (bucha, pino, coluna — as
                    // mais repetidas de um molde) não tem ponto nenhum para casar, e o encaixe
                    // por nuvem recusaria. Mas ela tem EIXO e PERFIL, e isso a determina por
                    // inteiro. O run real de 2026-10-18 é que trouxe este ramo: 4 de 16 peças da
                    // base de molde tinham 0 vértices.
                    if (TooFewVertices(master) || TooFewVertices(cand))
                    {
                        EnsureRevolution(master, scan, tolMm);
                        EnsureRevolution(cand, scan, tolMm);

                        if (master.Revolution != null && cand.Revolution != null &&
                            master.Revolution.Ok && cand.Revolution.Ok)
                        {
                            AxisProfileFit.Result rev = AxisProfileFit.Fit(master.Revolution, cand.Revolution, tolMm);
                            if (rev.Ok)
                            {
                                cand.UsedRevolutionPath = true;
                                group.Members.Add(new GroupMember
                                {
                                    Part = cand,
                                    FitR = rev.R,
                                    FitTMm = rev.TMm,
                                    MaxDeviationMm = rev.MaxDeviationMm,
                                    RotationDeg = RigidFit.AngleDeg(rev.R),
                                    ByRevolution = true
                                });
                                continue;
                            }
                            group.Rejected.Add(new RejectedMember { Part = cand, Why = rev.Why });
                            leftovers.Add(cand);
                            continue;
                        }

                        group.Rejected.Add(new RejectedMember
                        {
                            Part = cand,
                            Why = "sem vértices para casar e não é sólido de revolução: " +
                                  (master.Revolution?.Why ?? cand.Revolution?.Why ?? "motivo não registrado")
                        });
                        leftovers.Add(cand);
                        continue;
                    }

                    if (master.VerticesMm == null || cand.VerticesMm == null ||
                        master.VerticesPartial || cand.VerticesPartial)
                    {
                        group.Rejected.Add(new RejectedMember
                        {
                            Part = cand,
                            Why = "leitura de vértices incompleta: " +
                                  (master.VerticesWhy ?? cand.VerticesWhy ?? "motivo não registrado")
                        });
                        continue;
                    }

                    // Os ANÉIS do corpo entram como prova adicional: encaixar os vértices não
                    // basta, porque eles são um subconjunto da peça (ver RingEvidence). Sem anel
                    // nenhum a prova não se aplica e sobra a preferência pelo menor giro.
                    EnsureRings(master, scan);
                    EnsureRings(cand, scan);
                    RigidFit.WholeBodyCheck check = null;
                    if (master.Rings != null && cand.Rings != null &&
                        master.Rings.Count > 0 && cand.Rings.Count > 0)
                        check = (r, t) => RingEvidence.Agree(master.Rings, cand.Rings, r, t, tolMm);

                    RigidFit.Result fit = RigidFit.Fit(master.VerticesMm, cand.VerticesMm, tolMm, allowMirror, check);
                    if (!fit.Ok)
                    {
                        // Mede de novo só para SABER se era espelho — a informação é a que o
                        // usuário precisa ("são espelhadas", não "não encaixou").
                        bool mirror = false;
                        if (!allowMirror)
                        {
                            RigidFit.Result m = RigidFit.Fit(master.VerticesMm, cand.VerticesMm, tolMm, true, check);
                            mirror = m.Ok && m.IsMirror;
                        }
                        group.Rejected.Add(new RejectedMember { Part = cand, Why = fit.Why, IsMirror = mirror });

                        // Recusada por ESTA mestra, mas volta para a fila de qualquer forma —
                        // inclusive a espelhada. Um postiço de mão esquerda que aparece 10 vezes
                        // não se unifica com o de mão direita, mas as 10 cópias DELE se unificam
                        // entre si: ele vira mestra do próprio grupo na rodada seguinte.
                        // Descartá-lo aqui (o que este código fazia antes) perdia justamente a
                        // metade espelhada de um molde de duas cavidades espelhadas.
                        leftovers.Add(cand);
                        continue;
                    }

                    group.Members.Add(new GroupMember
                    {
                        Part = cand,
                        FitR = fit.R,
                        FitTMm = fit.TMm,
                        MaxDeviationMm = fit.MaxDeviationMm,
                        RotationDeg = RigidFit.AngleDeg(fit.R),
                        IsMirror = fit.IsMirror
                    });
                }

                pending = leftovers;
                if (group.Members.Count > 0 || group.Rejected.Count > 0) scan.Groups.Add(group);
                else scan.Unique.Add(master);
            }
        }

        // ------------------------------------------------------------------ execução (ESCREVE)

        /// <summary>
        /// Aplica o plano: cada ocorrência dos arquivos dispensados é substituída por uma
        /// ocorrência da peça-mestra, na pose composta. NÃO salva a montagem.
        ///
        /// A sequência por ocorrência, nesta ordem e por este motivo:
        ///   1. acrescenta a ocorrência nova da mestra, na pose calculada;
        ///   2. LÊ a pose de volta e compara com a pretendida;
        ///   3. só então apaga a original — e, se o passo 2 reprovar, apaga a NOVA e mantém a
        ///      original intacta.
        /// Apagar primeiro seria mais simples e deixaria a montagem quebrada em caso de falha.
        /// </summary>
        public static UnifyResult Apply(object assemblyDocument, UnifyScan scan, double tolMm)
        {
            var result = new UnifyResult();
            if (assemblyDocument == null || scan == null || !scan.Ok)
            {
                result.Messages.Add("Plano inválido — nada foi feito.");
                return result;
            }
            if (tolMm <= 0) tolMm = DefaultToleranceMm;

            dynamic asm = assemblyDocument;

            foreach (DuplicateGroup g in scan.Groups)
            {
                if (g.Members.Count == 0) continue;
                string masterPath = g.Master.FilePath;
                if (!File.Exists(masterPath))
                {
                    result.Messages.Add($"  {g.Master.FileName}: arquivo não encontrado no disco — grupo ignorado.");
                    result.Failed += g.OccurrencesReplaced;
                    continue;
                }

                foreach (GroupMember m in g.Members)
                {
                    // ESPELHO NÃO SE APLICA POR AQUI, e isto não é conservadorismo: a matriz do
                    // encaixe espelhado tem determinante -1, e isso não é um posicionamento
                    // rígido. Mandá-la para PutMatrix é pedir à Solid Edge uma colocação que ela
                    // não faz por esse caminho — na melhor hipótese recusa, na pior aceita e
                    // produz corpo invertido. Uma peça de mão trocada num molde é refugo, então
                    // o par espelhado fica REPORTADO e intocado. (Occurrence.Mirror existe, mas
                    // a semântica do parâmetro dele não está confirmada — regra de ouro.)
                    if (m.IsMirror)
                    {
                        result.Messages.Add($"  {m.Part.FileName}: encaixe ESPELHADO — não aplicado. " +
                                            "Peça de mão esquerda e de mão direita não são a mesma peça.");
                        result.Failed += m.Part.Occurrences.Count;
                        continue;
                    }

                    bool allOk = true;
                    foreach (ImportedOccurrence occ in m.Part.Occurrences)
                    {
                        // A pose nova: a pose ORIGINAL desta ocorrência, composta com o encaixe
                        // que leva a geometria da mestra sobre a geometria desta peça.
                        OccurrenceTransform target = occ.Pose.ComposedWith(
                            m.FitR,
                            new[] { Units.MmToM(m.FitTMm[0]), Units.MmToM(m.FitTMm[1]), Units.MmToM(m.FitTMm[2]) });

                        string why;
                        double errMm;
                        if (ReplaceOne(asm, occ, masterPath, target, tolMm, out errMm, out why))
                        {
                            result.Replaced++;
                            if (errMm > result.WorstPositionErrorMm) result.WorstPositionErrorMm = errMm;
                            Log.Info($"[IMPORT] '{occ.Name}' -> {g.Master.FileName}, erro de posição {errMm:0.0000} mm.");
                        }
                        else
                        {
                            allOk = false;
                            result.Failed++;
                            result.Messages.Add($"  {occ.Name}: {why} — ocorrência original MANTIDA.");
                            Log.Warn($"[IMPORT] '{occ.Name}' não substituída: {why}");
                        }
                    }
                    if (allOk) result.OrphanedFiles.Add(m.Part.FilePath);
                }
            }
            return result;
        }

        /// <summary>
        /// Uma substituição, com a conferência que a torna reversível. O erro devolvido é a
        /// distância (mm) entre a origem pretendida e a que o Solid Edge realmente gravou.
        /// </summary>
        private static bool ReplaceOne(dynamic asm, ImportedOccurrence occ, string masterPath,
            OccurrenceTransform target, double tolMm, out double errorMm, out string why)
        {
            errorMm = 0;
            why = null;
            object added = null;
            try
            {
                try { added = (object)asm.Occurrences.AddByFilename(masterPath); }
                catch (Exception ex) { why = "AddByFilename falhou: " + ex.GetBaseException().Message; return false; }
                if (added == null) { why = "AddByFilename devolveu nulo."; return false; }

                if (!TryPutMatrix(added, target))
                {
                    why = "PutMatrix falhou — a ocorrência nova não pôde ser posicionada.";
                    SafeDelete(added);
                    return false;
                }

                // A CONFERÊNCIA. Sem ela, um PutMatrix que "funcionou" mas gravou a rotação
                // transposta passaria batido — e peça 180° invertida num molde é refugo, não
                // detalhe. Ler de volta é a única prova.
                var check = new OccurrenceInfo(added, occ.Name, null);
                OccurrenceTransform got = AssemblyContext.TryGetPose(check);
                if (got == null)
                {
                    why = "a pose da ocorrência nova não pôde ser lida de volta para conferir.";
                    SafeDelete(added);
                    return false;
                }

                errorMm = OriginErrorMm(target, got);
                double rotErr = RotationErrorDeg(target, got);
                if (errorMm > Math.Max(tolMm, 1e-4) || rotErr > 0.01)
                {
                    why = string.Format(CultureInfo.InvariantCulture,
                        "a posição conferida saiu fora ({0:0.0000} mm, {1:0.000}°)", errorMm, rotErr);
                    SafeDelete(added);
                    return false;
                }

                // Só agora a original sai de cena.
                try { ((dynamic)occ.Com).Delete(); }
                catch (Exception ex)
                {
                    why = "a ocorrência nova entrou certa, mas a original não pôde ser apagada: " +
                          ex.GetBaseException().Message;
                    SafeDelete(added);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                why = "erro inesperado: " + ex.GetBaseException().Message;
                if (added != null) SafeDelete(added);
                return false;
            }
        }

        /// <summary><c>Occurrence.PutMatrix(Matrix, Replace: true)</c> — mesma chamada já usada
        /// na "Nova peça", no eletrodo e no lote de malha->sólido.</summary>
        private static bool TryPutMatrix(object occ, OccurrenceTransform pose)
        {
            try
            {
                occ.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, occ,
                    new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("[IMPORT] PutMatrix falhou: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private static void SafeDelete(object occ)
        {
            try { ((dynamic)occ).Delete(); }
            catch (Exception ex) { Log.Warn("[IMPORT] não deu para desfazer a ocorrência nova: " + ex.GetBaseException().Message); }
        }

        // ------------------------------------------------------------------ sonda do Replace

        /// <summary>
        /// Registra no log a assinatura REAL de <c>Occurrence.Replace</c> lida do objeto ao
        /// vivo. Só leitura — não chama o método.
        ///
        /// Por que existe: <c>Replace</c> é o caminho certo a prazo (preserva a identidade da
        /// ocorrência, o nome e as relações que a referenciam, em vez de apagar e recriar), mas
        /// tem 3 parâmetros cujo significado não está confirmado em nenhuma fonte que este
        /// projeto tenha conferido. A regra de ouro é não inventar assinatura: então o caminho
        /// conservador roda hoje e esta sonda junta a evidência para trocar amanhã.
        /// </summary>
        public static void ProbeReplaceSignature(object anyOccurrence)
        {
            if (anyOccurrence == null) return;
            try
            {
                Com.ComDiagnostics.LogSignatures(anyOccurrence, "Replace", "PutMatrix", "GetMatrix", "Delete", "Mirror");
            }
            catch (Exception ex)
            {
                Log.Warn("[IMPORT] sonda do Replace falhou: " + ex.GetBaseException().Message);
            }
        }

        // ------------------------------------------------------------------ utilidades

        private static object BodyOf(ImportedPart part, UnifyScan scan)
        {
            if (part.Occurrences.Count == 0) return null;
            try
            {
                dynamic occ = part.Occurrences[0].Com;
                dynamic doc = occ.OccurrenceDocument;
                if (doc == null)
                {
                    scan.Notes.Add($"  {part.FileName}: o documento da peça não está acessível.");
                    return null;
                }
                string why;
                object body = BodySampler.SolidBodyOf((object)doc, out why);
                if (body == null) scan.Notes.Add($"  {part.FileName}: {why}");
                return body;
            }
            catch (Exception ex)
            {
                scan.Notes.Add($"  {part.FileName}: {ex.GetBaseException().Message}");
                return null;
            }
        }

        /// <summary>Menos de 3 vértices não fixa rotação — é o gatilho do caminho torneado.</summary>
        private static bool TooFewVertices(ImportedPart part) =>
            part.VerticesMm == null || part.VerticesMm.Count < 3;

        /// <summary>Lê os anéis da peça uma vez. Serve tanto à prova do corpo inteiro (caso
        /// geral) quanto ao perfil de revolução (caso torneado).</summary>
        private static void EnsureRings(ImportedPart part, UnifyScan scan)
        {
            if (part.Rings != null) return;
            object body = BodyOf(part, scan);
            if (body == null) { part.Rings = new List<Ring>(); return; }
            bool allCircular;
            string why;
            part.Rings = BodySampler.ReadRings(body, out allCircular, out why);
            part.AllEdgesCircular = allCircular;
        }

        private static void EnsureRevolution(ImportedPart part, UnifyScan scan, double tolMm)
        {
            if (part.Revolution != null) return;
            object body = BodyOf(part, scan);
            if (body == null)
            {
                part.Revolution = new AxisProfileFit.Profile { Ok = false, Why = "corpo inacessível." };
                return;
            }

            bool allCircular;
            string why;
            List<Ring> rings;
            if (part.Rings != null) { rings = part.Rings; allCircular = part.AllEdgesCircular; why = null; }
            else
            {
                rings = BodySampler.ReadRings(body, out allCircular, out why);
                part.Rings = rings;
                part.AllEdgesCircular = allCircular;
            }

            // Exigir que TODA aresta seja circular não é zelo excessivo: é o que autoriza a
            // conclusão "sólido de revolução". Uma aresta reta esquecida significa que existe
            // geometria que quebra a simetria angular, e aí o giro em torno do eixo deixa de ser
            // livre — o encaixe sairia com a peça girada.
            if (!allCircular)
            {
                part.Revolution = new AxisProfileFit.Profile { Ok = false, Why = why ?? "arestas não são todas circulares." };
                return;
            }
            part.Revolution = AxisProfileFit.BuildProfile(rings, tolMm);
        }

        private static void EnsureVertices(ImportedPart part, UnifyScan scan)
        {
            if (part.VerticesMm != null) return;
            object body = BodyOf(part, scan);
            if (body == null)
            {
                part.VerticesPartial = true;
                part.VerticesWhy = "corpo inacessível.";
                part.VerticesMm = new List<double[]>();
                return;
            }
            bool partial;
            string why;
            part.VerticesMm = BodySampler.ReadVertices(body, out partial, out why);
            part.VerticesPartial = partial;
            part.VerticesWhy = why;
        }

        private static bool IsSubassembly(OccurrenceInfo occ)
        {
            try { return (bool)((dynamic)occ.ComOccurrence).Subassembly; }
            catch { return false; }
        }

        private static string FileNameOf(OccurrenceInfo occ)
        {
            try
            {
                string s = (string)((dynamic)occ.ComOccurrence).OccurrenceFileName;
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
        }

        private static bool IsIdentity(OccurrenceTransform p)
        {
            if (Math.Abs(p.TxM) > 1e-9 || Math.Abs(p.TyM) > 1e-9 || Math.Abs(p.TzM) > 1e-9) return false;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (Math.Abs(p.R(i, j) - (i == j ? 1.0 : 0.0)) > 1e-9) return false;
            return true;
        }

        /// <summary>
        /// Como a montagem carrega a posição. É a diferença entre os dois comportamentos de
        /// tradutor que este botão tem de atender, e vale dizer na cara do usuário porque muda o
        /// que ele deve esperar do resultado.
        /// </summary>
        private static string DescribePlacement(int identity, int posed)
        {
            if (identity > 0 && posed == 0)
                return $"todas as {identity} ocorrências estão na identidade — o tradutor assou a posição " +
                       "DENTRO da geometria de cada peça. O encaixe medido é que carrega a posição.";
            if (posed > 0 && identity == 0)
                return $"todas as {posed} ocorrências têm pose própria — o tradutor deixou a geometria na " +
                       "origem e pôs a posição na ocorrência.";
            return $"misto: {posed} ocorrência(s) com pose própria e {identity} na identidade.";
        }

        private static double OriginErrorMm(OccurrenceTransform want, OccurrenceTransform got)
        {
            double dx = Units.MToMm(want.TxM - got.TxM);
            double dy = Units.MToMm(want.TyM - got.TyM);
            double dz = Units.MToMm(want.TzM - got.TzM);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>O quanto a rotação gravada difere da pretendida, em graus. Pega a transposta
        /// silenciosa, que é o modo de falha que não aparece na origem.</summary>
        private static double RotationErrorDeg(OccurrenceTransform want, OccurrenceTransform got)
        {
            double worst = 0;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    worst = Math.Max(worst, Math.Abs(want.R(i, j) - got.R(i, j)));
            // Diferença máxima de componente -> ângulo aproximado. Serve de limiar, não de medida.
            return Math.Asin(Math.Min(1.0, worst)) * 180.0 / Math.PI;
        }

        private static void LogScan(UnifyScan scan)
        {
            Log.Info($"[IMPORT] varredura: {scan.OccurrenceCount} ocorrência(s), {scan.DistinctFilesBefore} arquivo(s) " +
                     $"-> {scan.DistinctFilesAfter}; {scan.Groups.Count} grupo(s) de repetidas.");
            Log.Info("[IMPORT] " + scan.PlacementStyle);
            foreach (DuplicateGroup g in scan.Groups)
            {
                Log.Info($"[IMPORT] grupo mestra={g.Master.FileName} ({g.Master.Signature.Describe()})");
                foreach (GroupMember m in g.Members)
                    Log.Info($"[IMPORT]   + {m.Part.FileName}: {m.Part.Occurrences.Count} posição(ões), " +
                             $"giro {m.RotationDeg:0.0}°, desvio {m.MaxDeviationMm:0.0000} mm");
                foreach (RejectedMember r in g.Rejected)
                    Log.Info($"[IMPORT]   - {r.Part.FileName} RECUSADA{(r.IsMirror ? " (ESPELHADA)" : "")}: {r.Why}");
            }
        }
    }
}
