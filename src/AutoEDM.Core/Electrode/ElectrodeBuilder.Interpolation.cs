using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Drawing;
using AutoEDM.Model;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// ELETRODO DE INTERPOLAÇÃO (Carlos, 2026-10-22). Eletrodo REDONDO, que gira no próprio eixo e
    /// percorre o perímetro da peça para erodir uma região NEGATIVA (rebaixo na parede).
    ///
    /// Roteiro, nas escolhas do Carlos: o perfil é a seção da superfície por um plano vertical que
    /// contém a NORMAL da face; o afastamento do eixo vem do Ø FINAL do eletrodo (o ponto mais
    /// fundo do perfil fica a Ø/2 do eixo); o botão gera SÓ o corpo de revolução — haste e base
    /// saem depois, pelo "Criar Base" ou à mão. A peça nasce em SÍNCRONO, como a do eletrodo manual.
    ///
    /// Rota no SE: a cópia da queima (<see cref="BurnSurfaceCopier"/>) → <c>IntersectionCurves.Add</c>
    /// da superfície com o plano XZ da peça → <c>Profile.IncludeEdge</c> → fechamento até o eixo →
    /// <c>Models.AddFiniteRevolvedProtrusion</c>. O fundo do perfil só é conhecido DEPOIS da
    /// seção, e é ele que decide onde fica a origem — por isso são duas passadas: a primeira, numa
    /// peça descartável, só mede; a segunda constrói o eletrodo já com a origem no eixo.
    ///
    /// Validado por tools/InterpolationProbe (2026-10-22, SE 223) em peça descartável: interseção
    /// UMA face por chamada, IncludeEdge, emendas amarradas e revolução pelo método sem "Sync".
    /// O botão real ainda não refez o caminho inteiro com uma cavidade.
    /// </summary>
    public sealed partial class ElectrodeBuilder
    {
        private const string InterpTag = "Eletrodo de interpolação";
        private const int IgQueryAll = 1;                       // FeatureTopologyQueryTypeConstants
        private const int IgProfileClosedAllowNested = 1 | 8192; // o mesmo do Malha → sólido
        private const int IgLeft = 1, IgRight = 2;              // FeaturePropertyConstants

        /// <summary>
        /// Cria o eletrodo de interpolação a partir das faces escolhidas. <paramref name="diameterMm"/>
        /// é o Ø final do eletrodo. ESCREVE na montagem (não salva). Com <paramref name="previewOnly"/>
        /// faz as duas passadas em peças descartáveis e mede o corpo, sem gravar arquivo nem tocar na
        /// montagem — é como a sonda (tools/InterpolationProbe --previa) testa com a cavidade real.
        /// </summary>
        public ManualElectrodeResult CreateInterpolationElectrode(dynamic asmDoc, ElectrodeParams p,
            IList<PickedFace> picks, double diameterMm, bool previewOnly = false)
        {
            var result = new ManualElectrodeResult();
            if (_connector.Application == null)
                throw new InvalidOperationException("Conecte o SolidEdgeConnector primeiro.");
            if (asmDoc == null) throw new ArgumentNullException(nameof(asmDoc));

            dynamic app = _connector.Application;
            var ctx = new AssemblyContext(asmDoc);
            List<PickedFace> valid = (picks ?? new List<PickedFace>()).Where(k => k?.Face != null).ToList();
            if (valid.Count == 0) return Fail(result, "Nenhuma FACE escolhida. Clique na superfície da região a erodir.");
            if (!(diameterMm > 0)) return Fail(result, "Informe o diâmetro do eletrodo.");

            // Poses: o mesmo esquema do eletrodo manual — cada face vai para a montagem pela sua ocorrência.
            var poses = new Dictionary<string, OccurrenceTransform>();
            OccurrenceTransform PoseOf(PickedFace k)
            {
                if (k?.Occurrence == null || k.OccurrenceName == null) return null;
                if (!poses.TryGetValue(k.OccurrenceName, out OccurrenceTransform t))
                    poses[k.OccurrenceName] = t = AssemblyContext.TryGetPose(WrapOccurrence(k.Occurrence));
                return t;
            }
            PickedFace owned = valid.FirstOrDefault(k => k.Occurrence != null);
            OccurrenceInfo cavity = owned != null ? WrapOccurrence(owned.Occurrence) : FindOwningOccurrence(ctx, valid[0].Face);
            OccurrenceTransform cavityPose = owned != null ? PoseOf(owned) : (cavity != null ? AssemblyContext.TryGetPose(cavity) : null);
            OccurrenceTransform PoseOfPick(PickedFace k) => k?.Occurrence != null ? PoseOf(k) : cavityPose;

            // 1) Menor Z da superfície e a normal que decide o plano da seção, tudo na MONTAGEM.
            double minZ = double.MaxValue;
            double[] refPoint = null, refNormal = null;
            double bestHorizontal = -1;
            foreach (PickedFace k in valid)
            {
                OccurrenceTransform pose = PoseOfPick(k);
                if (FaceGeometry.TryGetRangeMm(k.Face, out double[] mn, out double[] mx))
                {
                    if (pose != null) OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), mn, mx, out mn, out mx);
                    minZ = Math.Min(minZ, mn[2]);
                }
                if (!TryFaceMidPointAndNormal(k.Face, out double[] pt, out double[] n)) continue;
                if (pose != null) { pt = MapPointMm(pose, pt); n = MapVector(pose, n); }
                double h = Math.Sqrt(n[0] * n[0] + n[1] * n[1]) / Math.Max(1e-12, Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]));
                if (h > bestHorizontal) { bestHorizontal = h; refPoint = pt; refNormal = n; }
            }
            if (minZ == double.MaxValue) return Fail(result, "Não consegui ler a geometria (bounding box) das faces escolhidas.");
            if (refNormal == null) return Fail(result, "Não consegui ler a normal de nenhuma face escolhida (veja o log).");
            if (!InterpolationGeometry.TryHorizontalAxis(refNormal, out double[] xAxis))
                return Fail(result, "As faces escolhidas são quase horizontais: a normal não diz para que lado o eletrodo " +
                                    "trabalha. Inclua a face da PAREDE do rebaixo.");
            Log.Info($"{InterpTag}: {valid.Count} face(s); normal de referência ({refNormal[0]:0.000}, {refNormal[1]:0.000}, {refNormal[2]:0.000}) " +
                     $"em ({refPoint[0]:0.00}, {refPoint[1]:0.00}, {refPoint[2]:0.00}) → X da peça ({xAxis[0]:0.000}, {xAxis[1]:0.000}); " +
                     $"menor Z = {minZ:0.00}; Ø pedido {diameterMm:0.00} mm.");

            MatrixLayout layout = cavityPose?.Layout ?? MatrixLayout.TranslationLast;
            double[] rotation = InterpolationGeometry.RotationRows(xAxis);
            OccurrenceTransform FrameAt(double[] originMm) =>
                OccurrenceTransform.Identity(layout).ComposedWith(rotation, originMm.Select(Units.MmToM).ToArray());

            // 2) Passada de MEDIÇÃO: origem no ponto da normal, na menor altura. Só lê a seção.
            double[] origin1 = { refPoint[0], refPoint[1], minZ };
            OccurrenceTransform pose1 = FrameAt(origin1);
            List<BurnSource> sources1 = BuildBurnSources(valid, cavity, PoseOfPick, pose1);
            if (sources1.Count == 0) return Fail(result, "Nenhuma face com ocorrência dona — não há o que copiar.");

            double[][] box1;
            string error = MeasureSection((object)app, sources1, LocalOf(pose1, refPoint), out box1);
            if (error != null) return Fail(result, error);
            Log.Info($"{InterpTag}: perfil na 1ª passada X [{box1[0][0]:0.000} .. {box1[1][0]:0.000}], " +
                     $"Z [{box1[0][2]:0.000} .. {box1[1][2]:0.000}] mm (referencial da peça).");

            error = InterpolationGeometry.CheckDiameter(box1[0][0], box1[1][0], diameterMm);
            if (error != null) return Fail(result, error);

            // 3) O deslocamento da origem em X: o eixo fica a Ø/2 do ponto mais fundo do perfil.
            double shift = InterpolationGeometry.AxisXmm(box1[0][0], diameterMm);
            double[] origin2 = { origin1[0] + shift * xAxis[0], origin1[1] + shift * xAxis[1], minZ };
            OccurrenceTransform pose2 = FrameAt(origin2);
            Log.Info($"{InterpTag}: origem deslocada {shift:0.000} mm em X → eixo em ({origin2[0]:0.00}, {origin2[1]:0.00}, {origin2[2]:0.00}).");

            // 4) O eletrodo de verdade.
            List<object> faces = valid.Select(k => k.Face).ToList();
            double? detectedRa = DetectCommonRa(faces, app);
            string folder = ElectrodeNaming.ResolveElectrodeFolder(asmDoc, p);
            if (!previewOnly) System.IO.Directory.CreateDirectory(folder);
            string prefix = ElectrodeNaming.ElectrodeNamePrefix(asmDoc);
            int index = ElectrodeNaming.NextElectrodeIndex(ctx, prefix);
            string name = $"{prefix}{index:00}";
            string path = System.IO.Path.Combine(folder, name + ".par");

            var burnCopy = new BurnCopyResult();
            double measured;
            error = BuildInterpolationPart((object)app, BuildBurnSources(valid, cavity, PoseOfPick, pose2), LocalOf(pose2, refPoint),
                                           detectedRa, previewOnly ? null : path, burnCopy, out measured);
            result.BurnCopy = burnCopy;
            if (error != null) return Fail(result, $"{name}: {error}");
            if (previewOnly)
            {
                result.Message = $"PRÉVIA {name}: Ø {diameterMm:0.00} mm pedido, " +
                                 (double.IsNaN(measured) ? "corpo sem medida" : $"{measured:0.000} mm medido no corpo") +
                                 $"; eixo em X {origin2[0]:0.00} / Y {origin2[1]:0.00}, base em Z {minZ:0.00}. Nada gravado." +
                                 Environment.NewLine + burnCopy.Summary();
                Log.Info($"{InterpTag}: " + result.Message);
                return result;
            }

            object occ = asmDoc.Occurrences.AddByFilename(path);
            if (!TryPutMatrix(occ, pose2, index))
                Log.Warn($"{InterpTag}: {name} ficou na origem da montagem — posicione à mão (eixo em " +
                         $"{origin2[0]:0.00}, {origin2[1]:0.00}, {origin2[2]:0.00}).");

            result.Created = true;
            result.Path = path;
            result.FaceCount = valid.Count;
            result.CenterXmm = origin2[0]; result.CenterYmm = origin2[1]; result.DeepestZmm = minZ;
            result.Message = $"Eletrodo de interpolação {name} criado: Ø {diameterMm:0.00} mm pedido" +
                             (double.IsNaN(measured) ? "" : $", {measured:0.00} mm medido no corpo") +
                             $"; eixo em X {origin2[0]:0.00} / Y {origin2[1]:0.00}, base em Z {minZ:0.00}." +
                             (detectedRa.HasValue ? $" Ra {detectedRa.Value:0.0} gravado na peça." : "") +
                             Environment.NewLine + burnCopy.Summary();
            Log.Info($"{InterpTag}: " + result.Message);
            return result;
        }

        private static ManualElectrodeResult Fail(ManualElectrodeResult r, string message)
        {
            r.Message = message;
            Log.Warn($"{InterpTag}: {message}");
            return r;
        }

        /// <summary>1ª passada: peça descartável, só para medir a caixa do perfil. Fecha sem salvar.</summary>
        private static string MeasureSection(dynamic app, List<BurnSource> sources, double[] refPointMm, out double[][] box)
        {
            box = null;
            dynamic doc = null;
            try
            {
                doc = app.Documents.Add("SolidEdge.PartDocument");
                string err = CopyIntoSync((object)doc, sources, null);
                if (err != null) return err;
                using (var scope = new SketchScope((object)doc, InterpTag + " (medição)"))
                {
                    SectionRun run = CutSection((object)doc, scope, refPointMm);
                    if (run.Error != null) return run.Error;
                    box = run.Box;
                }
                return null;
            }
            catch (Exception ex) { return "a passada de medição falhou: " + ex.GetBaseException().Message; }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(false); }
                    catch (Exception ex) { Log.Warn($"{InterpTag}: a peça de medição não fechou — feche sem salvar ({ex.GetBaseException().Message})."); }
                }
            }
        }

        /// <summary>2ª passada: a peça do eletrodo, com a revolução, salva em <paramref name="path"/> (null = prévia, fecha sem salvar).</summary>
        private static string BuildInterpolationPart(dynamic app, List<BurnSource> sources, double[] refPointMm,
            double? ra, string path, BurnCopyResult burnCopy, out double measuredDiameterMm)
        {
            measuredDiameterMm = double.NaN;
            dynamic doc = null;
            try
            {
                doc = app.Documents.Add("SolidEdge.PartDocument");
                if (ra.HasValue) RaVariableStore.TryWrite(doc, ra.Value);
                string err = CopyIntoSync((object)doc, sources, burnCopy);
                if (err != null) return err;

                err = RevolveCopiedSurfaces((object)doc, refPointMm, out measuredDiameterMm);
                if (err != null) return err;

                if (path == null) return null;   // prévia: o finally fecha sem salvar
                doc.SaveAs(path);
                doc.Close();
                doc = null;
                return null;
            }
            catch (Exception ex) { return "a construção da peça falhou: " + ex.GetBaseException().Message; }
            finally
            {
                if (doc != null) { try { doc.Close(false); } catch { } }
            }
        }

        /// <summary>
        /// O miolo da 2ª passada, numa peça SÍNCRONA que já tem as superfícies copiadas e a origem no
        /// eixo: seção pelo plano XZ, revolução em Z, limpeza e medida do corpo. Público para a sonda
        /// (tools/InterpolationProbe) exercitar a mesma cadeia numa peça descartável. Null = deu certo.
        /// </summary>
        public static string RevolveCopiedSurfaces(object partDoc, double[] refPointMm, out double measuredDiameterMm)
        {
            measuredDiameterMm = double.NaN;
            using (var scope = new SketchScope(partDoc, InterpTag))
            {
                SectionRun run = CutSection(partDoc, scope, refPointMm);
                if (run.Error != null) return run.Error;

                object body = Revolve(partDoc, scope, run, out string err);
                if (body == null) return err;

                foreach (object curve in run.Curves)
                {
                    try { ((dynamic)curve).Delete(); }
                    catch (Exception ex) { Log.Info($"{InterpTag}: curva de interseção ficou na peça ({ex.GetBaseException().Message})."); }
                }
            }
            HideCopiedSurfaces(partDoc);

            if (TryBodyDiameterMm(partDoc, out double d, out double h))
            {
                measuredDiameterMm = d;
                Log.Info($"{InterpTag}: corpo medido Ø {d:0.000} mm × {h:0.000} mm de altura.");
            }
            return null;
        }

        /// <summary>Peça em SÍNCRONO (documento nosso, recém-criado) e a cópia da queima dentro dela.</summary>
        private static string CopyIntoSync(dynamic doc, List<BurnSource> sources, BurnCopyResult into)
        {
            try { if ((int)doc.ModelingMode != 1) doc.ModelingMode = 1; }
            catch (Exception me) { Log.Warn($"{InterpTag}: não consegui pôr a peça em síncrono ({me.GetBaseException().Message})."); }

            BurnCopyResult copied = BurnSurfaceCopier.Copy((object)doc, sources);
            Log.Info($"{InterpTag}: " + copied.Summary());
            if (into != null)
            {
                into.Requested = copied.Requested; into.Copied = copied.Copied; into.Repainted = copied.Repainted;
                into.Surfaces.AddRange(copied.Surfaces); into.Warnings.AddRange(copied.Warnings);
            }
            return copied.Copied == 0 ? "a cópia da superfície para a peça nova falhou (veja o log)." : null;
        }

        // ------------------------------------------------------------------ seção

        private sealed class SectionRun
        {
            public object Plane;
            public readonly List<object> Curves = new List<object>();
            public readonly List<SectionEdge> Edges = new List<SectionEdge>();
            public List<int> Chain;
            public double[] Top, Bottom;
            public double[][] Box;
            public string Error;
        }

        /// <summary>
        /// A seção das superfícies copiadas pelo plano XZ da peça, reduzida à cadeia do lado
        /// escolhido (a mais perto de <paramref name="refPointMm"/>) e com as duas pontas livres.
        /// </summary>
        private static SectionRun CutSection(dynamic doc, SketchScope scope, double[] refPointMm)
        {
            var run = new SectionRun();
            run.Plane = FindXzPlane((object)doc, scope);
            if (run.Plane == null) { run.Error = "não achei o plano XZ da peça nova (veja o log)."; return run; }

            dynamic surfaces = doc.Constructions.CopySurfaces;
            int n = 0;
            try { n = (int)surfaces.Count; } catch { }
            int tried = 0;
            for (int i = 1; i <= n; i++)
            {
                int faceCount = 0;
                try { faceCount = (int)surfaces.Item(i).Faces[IgQueryAll].Count; } catch { }
                for (int j = 1; j <= faceCount; j++)
                {
                    tried++;
                    // Relida a cada volta: cada interseção criada pode envelhecer os proxies das faces.
                    object face = surfaces.Item(i).Faces[IgQueryAll].Item(j);
                    object curve = Intersect((object)doc, face, run.Plane, $"superfície {i}, face {j}");
                    if (curve != null) run.Curves.Add(curve);
                }
            }
            if (run.Curves.Count == 0)
            {
                run.Error = $"o plano da seção não cortou nenhuma das {tried} face(s) copiadas (IntersectionCurves.Add) — " +
                            "confira se a face escolhida é a da parede do rebaixo.";
                return run;
            }

            foreach (object curve in run.Curves)
            {
                dynamic edges = ((dynamic)curve).Edges[IgQueryAll];
                int m = (int)edges.Count;
                for (int i = 1; i <= m; i++)
                {
                    object edge = edges.Item(i);
                    if (!FaceGeometry.TryTwoPointOutMm(edge, "GetEndPoints", out double[] s, out double[] e, out string why))
                    { Log.Warn($"{InterpTag}: aresta {i} da seção sem pontas ({why})."); continue; }
                    if (!FaceGeometry.TryGetExactRangeMm(edge, out double[] mn, out double[] mx)) { mn = s; mx = e; }
                    run.Edges.Add(new SectionEdge { StartMm = s, EndMm = e, MinMm = mn, MaxMm = mx, Com = edge });
                }
            }
            if (run.Edges.Count == 0) { run.Error = "a curva de interseção veio sem arestas legíveis."; return run; }

            List<List<int>> chains = InterpolationGeometry.Chains(run.Edges);
            run.Chain = InterpolationGeometry.NearestChain(chains, run.Edges, refPointMm);
            Log.Info($"{InterpTag}: seção com {run.Edges.Count} aresta(s) em {chains.Count} trecho(s); " +
                     $"usando o de {run.Chain.Count} aresta(s), o mais perto da face de referência.");
            if (!InterpolationGeometry.TryOpenEnds(run.Edges, run.Chain, out run.Top, out run.Bottom))
            {
                run.Error = "a seção do lado escolhido não é um perfil aberto com duas pontas (fechado ou ramificado) — " +
                            "não há como ligá-la ao eixo.";
                return run;
            }
            run.Box = InterpolationGeometry.ChainBox(run.Edges, run.Chain);
            return run;
        }

        /// <summary>
        /// <c>IntersectionCurves.Add(FaceSet1, FaceSet2)</c> com UMA face e o RefPlane — a única forma
        /// aceita (tools/InterpolationProbe, 2026-10-22, SE 223): a superfície inteira (CopySurface) e o
        /// Body dão E_INVALIDARG, e qualquer array (object[] ou Face[] tipado) dá DISP_E_TYPEMISMATCH.
        /// Face que o plano não corta pode recusar: isso é esperado e só vai para o log como Info.
        /// </summary>
        private static object Intersect(dynamic doc, object face, object plane, string label)
        {
            object collection = (object)doc.Constructions.IntersectionCurves;
            try
            {
                object curve = collection.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, collection,
                    new[] { face, plane });
                string status = FeatureStatusText(curve);
                if (curve != null && status != "igFeatureFailed")
                {
                    Log.Info($"{InterpTag}: IntersectionCurves.Add({label}, plano XZ) → {status}.");
                    return curve;
                }
                Log.Info($"{InterpTag}: IntersectionCurves.Add({label}, plano XZ) → {status} — descartada.");
                if (curve != null) { try { ((dynamic)curve).Delete(); } catch { } }
            }
            catch (Exception ex) { Log.Info($"{InterpTag}: IntersectionCurves.Add({label}, plano XZ): {ex.GetBaseException().Message} (face fora do plano?)."); }
            return null;
        }

        /// <summary>
        /// O plano de referência base que é o XZ da peça: normal ±Y e passando pela origem —
        /// perguntado ao próprio esboço (<see cref="ProfilePlaneFrame"/>), nunca deduzido do índice.
        /// </summary>
        private static object FindXzPlane(dynamic doc, SketchScope scope)
        {
            for (int i = 1; i <= 3; i++)
            {
                object plane;
                try { plane = doc.RefPlanes.Item(i); } catch { continue; }
                dynamic probe = null;
                try
                {
                    probe = scope.AddProfileSet();
                    ProfilePlaneFrame f = ProfilePlaneFrame.Discover((object)probe.Profiles.Add(plane));
                    if (f != null && Math.Abs(f.Normal[1]) > 0.999 && Math.Abs(f.SignedDistance(new[] { 0.0, 0.0, 0.0 })) < 0.01)
                    {
                        Log.Info($"{InterpTag}: plano XZ = RefPlanes.Item({i}).");
                        return plane;
                    }
                }
                catch (Exception ex) { Log.Warn($"{InterpTag}: sondagem de RefPlanes.Item({i}): {ex.GetBaseException().Message}"); }
                finally { if (probe != null) scope.DropProfileSet((object)probe); }
            }
            return null;
        }

        // ------------------------------------------------------------------ revolução

        /// <summary>
        /// Esboço no plano XZ: as arestas da seção incluídas (<c>IncludeEdge</c>), o fechamento até o
        /// eixo (x = 0), as emendas AMARRADAS e a revolução de 360° em torno do segmento do eixo.
        ///
        /// Medido em tools/InterpolationProbe (2026-10-22, SE 223, peça síncrona sem sólido):
        /// sem as relações de ponto-chave nas emendas o perfil não fecha (<c>End = -103</c>) mesmo com
        /// as pontas coincidentes; com elas, <c>End = 0</c>. E a revolução só sai pelo método SEM
        /// "Sync" — <c>Models.AddFiniteRevolvedProtrusionSync</c> deu E_FAIL em toda variante, e
        /// <c>Models.AddFiniteRevolvedProtrusion(1, Profile[], eixo, igRight, 2π)</c> gerou o corpo
        /// esperado (Ø24 × 10, ao 0,001 mm). É o mesmo caso da extrusão do <see cref="BlankModeler"/>.
        /// </summary>
        private static object Revolve(dynamic doc, SketchScope scope, SectionRun run, out string error)
        {
            error = null;
            dynamic ps = scope.AddProfileSet();
            dynamic profile = ps.Profiles.Add(run.Plane);
            ProfilePlaneFrame frame = ProfilePlaneFrame.Discover((object)profile);
            if (frame == null) { error = "o esboço da revolução não informou onde está o plano."; return null; }

            // O que o IncludeEdge acrescentou sai pela DIFERENÇA: o [out] dele volta vazio (regra da skill).
            var before = ORingGrooveModeler.SketchElements((object)profile);
            foreach (int i in run.Chain)
            {
                try
                {
                    object[] args = { run.Edges[i].Com, null };
                    var mod = new ParameterModifier(2);
                    mod[1] = true;
                    ((object)profile).GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, (object)profile,
                        args, new[] { mod }, null, null);
                }
                catch (Exception ex) { Log.Warn($"{InterpTag}: IncludeEdge da aresta {i}: {ex.GetBaseException().Message}"); }
            }
            var added = ORingGrooveModeler.NewElements(before, ORingGrooveModeler.SketchElements((object)profile));
            Log.Info($"{InterpTag}: {added.Count} elemento(s) incluído(s) no esboço para {run.Chain.Count} aresta(s) da seção: " +
                     string.Join(", ", added.Select(a => a.Key)) + ".");
            if (added.Count == 0) { error = "o perfil da seção não entrou no esboço (IncludeEdge não acrescentou nada)."; return null; }

            double[][] closing = InterpolationGeometry.ClosingPoints(run.Top, run.Bottom);
            var lines = new List<object>();
            for (int k = 0; k + 1 < closing.Length; k++)
            {
                if (!frame.TryTo2dMm((object)profile, closing[k], out double x1, out double y1) ||
                    !frame.TryTo2dMm((object)profile, closing[k + 1], out double x2, out double y2))
                { error = "o fechamento do perfil não converteu para o esboço."; return null; }
                lines.Add((object)profile.Lines2d.AddBy2Points(Units.MmToM(x1), Units.MmToM(y1), Units.MmToM(x2), Units.MmToM(y2)));
            }

            var all = new List<KeyValuePair<string, object>>(added);
            all.AddRange(lines.Select(l => new KeyValuePair<string, object>("Lines2d", l)));
            int relations = TieCoincidentEnds((object)profile, all);
            Log.Info($"{InterpTag}: {relations} emenda(s) amarrada(s) por relação de ponto-chave.");

            object refAxis;
            try { refAxis = profile.SetAxisOfRevolution(lines[1]); }
            catch (Exception ex) { error = "SetAxisOfRevolution recusou o segmento do eixo: " + ex.GetBaseException().Message; return null; }

            int end = int.MinValue;
            try { end = (int)profile.End(IgProfileClosedAllowNested); }
            catch (Exception ex) { Log.Warn($"{InterpTag}: Profile.End: {ex.GetBaseException().Message}"); }
            Log.Info($"{InterpTag}: Profile.End = {end} (diagnóstico, não decide).");

            SolidEdgePart.Profile[] arr;
            try { arr = new[] { (SolidEdgePart.Profile)profile }; }
            catch (Exception ex) { error = "o perfil não converteu para SolidEdgePart.Profile: " + ex.GetBaseException().Message; return null; }

            object models = (object)doc.Models;
            foreach (int side in new[] { IgRight, IgLeft })
            {
                try
                {
                    // Devolve um Model (sem Status): a prova é o corpo existir.
                    object model = models.GetType().InvokeMember("AddFiniteRevolvedProtrusion", BindingFlags.InvokeMethod,
                        null, models, new object[] { 1, arr, refAxis, side, 2.0 * Math.PI });
                    int count = -1;
                    try { count = (int)doc.Models.Count; } catch { }
                    if (model != null && count > 0)
                    {
                        Log.Info($"{InterpTag}: revolução criada (Models.AddFiniteRevolvedProtrusion, lado {side}).");
                        // A revolução CONSOME o esboço (sonda: ProfileSets.Count 1 → 0, e o Delete depois dá
                        // RPC_E_DISCONNECTED). Sai do escopo para o Dispose não acusar "esboço preso".
                        scope.Release((object)ps);
                        try { profile.Visible = false; } catch { }
                        try { ((dynamic)refAxis).Visible = false; } catch { }
                        return model;
                    }
                    Log.Warn($"{InterpTag}: AddFiniteRevolvedProtrusion(lado {side}) não criou corpo (Models.Count = {count}).");
                }
                catch (Exception ex) { Log.Warn($"{InterpTag}: AddFiniteRevolvedProtrusion(lado {side}): {ex.GetBaseException().Message}"); }
            }
            error = "o Solid Edge recusou a revolução do perfil (veja o log).";
            return null;
        }

        /// <summary>
        /// Amarra com <c>Relations2d.AddKeypoint</c> cada par de pontas que coincidem (1 µm) entre
        /// elementos diferentes do esboço. Índices das pontas (KeypointIndexConstants, interop SE 2023):
        /// linha e B-spline 0/1, arco e arco elíptico 1/2. Devolve quantas relações entraram.
        /// </summary>
        private static int TieCoincidentEnds(object profile, List<KeyValuePair<string, object>> elements)
        {
            var ends = new List<Tuple<object, int, double, double>>();   // elemento, índice, x, y (m)
            foreach (var e in elements)
            {
                int start, finish;
                switch (e.Key)
                {
                    case "Lines2d": case "BSplineCurves2d": start = 0; finish = 1; break;
                    case "Arcs2d": case "EllipticalArcs2d": start = 1; finish = 2; break;
                    default: Log.Warn($"{InterpTag}: elemento '{e.Key}' no perfil — pontas não amarradas."); continue;
                }
                try
                {
                    double sx, sy, ex, ey;
                    ((dynamic)e.Value).GetStartPoint(out sx, out sy);
                    ((dynamic)e.Value).GetEndPoint(out ex, out ey);
                    ends.Add(Tuple.Create(e.Value, start, sx, sy));
                    ends.Add(Tuple.Create(e.Value, finish, ex, ey));
                }
                catch (Exception ex) { Log.Warn($"{InterpTag}: pontas de '{e.Key}' ilegíveis — {ex.GetBaseException().Message}"); }
            }

            const double tolM = 1e-6;
            int made = 0;
            dynamic relations = ((dynamic)profile).Relations2d;
            for (int a = 0; a < ends.Count; a++)
                for (int b = a + 1; b < ends.Count; b++)
                {
                    if (ReferenceEquals(ends[a].Item1, ends[b].Item1)) continue;
                    double dx = ends[a].Item3 - ends[b].Item3, dy = ends[a].Item4 - ends[b].Item4;
                    if (dx * dx + dy * dy > tolM * tolM) continue;
                    try { relations.AddKeypoint(ends[a].Item1, ends[a].Item2, ends[b].Item1, ends[b].Item2); made++; }
                    catch (Exception ex) { Log.Warn($"{InterpTag}: relação de ponto-chave recusada — {ex.GetBaseException().Message}"); }
                }
            return made;
        }

        // ------------------------------------------------------------------ apoio

        /// <summary>Ponto (mm) e normal no meio do intervalo de parâmetros da face, no local da peça dona.</summary>
        private static bool TryFaceMidPointAndNormal(object face, out double[] pointMm, out double[] normal)
        {
            pointMm = normal = null;
            if (!FaceGeometry.TryTwoArrayOut(face, "GetParamRange", out double[] pMin, out double[] pMax, out string why) ||
                pMin == null || pMax == null || pMin.Length < 2 || pMax.Length < 2)
            { Log.Warn($"{InterpTag}: GetParamRange da face: {why ?? "vazio"}"); return false; }
            var uv = new[] { (pMin[0] + pMax[0]) / 2.0, (pMin[1] + pMax[1]) / 2.0 };
            object[] pt = TurnedProfileReader.TryCall(face, "GetPointAtParam", new object[] { 1, uv, new double[3] }, 1, 2);
            object[] nr = TurnedProfileReader.TryCall(face, "GetNormal", new object[] { 1, (double[])uv.Clone(), new double[3] }, 1, 2);
            if (!(pt?[2] is Array pa) || pa.Length < 3 || !(nr?[2] is Array na) || na.Length < 3)
            { Log.Warn($"{InterpTag}: ponto/normal da face ilegíveis."); return false; }
            pointMm = new[] { Convert.ToDouble(pa.GetValue(0)) * 1000.0, Convert.ToDouble(pa.GetValue(1)) * 1000.0, Convert.ToDouble(pa.GetValue(2)) * 1000.0 };
            normal = new[] { Convert.ToDouble(na.GetValue(0)), Convert.ToDouble(na.GetValue(1)), Convert.ToDouble(na.GetValue(2)) };
            return true;
        }

        private static double[] MapPointMm(OccurrenceTransform pose, double[] pMm)
        {
            pose.TransformPointM(Units.MmToM(pMm[0]), Units.MmToM(pMm[1]), Units.MmToM(pMm[2]), out double x, out double y, out double z);
            return new[] { Units.MToMm(x), Units.MToMm(y), Units.MToMm(z) };
        }

        /// <summary>Direção LOCAL → MONTAGEM: só a rotação (diferença de dois pontos transformados).</summary>
        private static double[] MapVector(OccurrenceTransform pose, double[] v)
        {
            pose.TransformPointM(v[0], v[1], v[2], out double x, out double y, out double z);
            pose.TransformPointM(0, 0, 0, out double x0, out double y0, out double z0);
            return new[] { x - x0, y - y0, z - z0 };
        }

        /// <summary>Ponto da MONTAGEM (mm) no local da peça do eletrodo (mm).</summary>
        private static double[] LocalOf(OccurrenceTransform pose, double[] pMm)
        {
            pose.InverseTransformPointM(Units.MmToM(pMm[0]), Units.MmToM(pMm[1]), Units.MmToM(pMm[2]), out double x, out double y, out double z);
            return new[] { Units.MToMm(x), Units.MToMm(y), Units.MToMm(z) };
        }

        private static void HideCopiedSurfaces(dynamic doc)
        {
            try
            {
                dynamic surfaces = doc.Constructions.CopySurfaces;
                for (int i = 1; i <= (int)surfaces.Count; i++) surfaces.Item(i).Visible = false;
            }
            catch (Exception ex) { Log.Info($"{InterpTag}: a superfície copiada continua visível ({ex.GetBaseException().Message})."); }
        }

        private static bool TryBodyDiameterMm(dynamic doc, out double diameterMm, out double heightMm)
        {
            diameterMm = heightMm = double.NaN;
            try
            {
                object body = doc.Models.Item(1).Body;
                if (!FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)) return false;
                diameterMm = Math.Max(mx[0] - mn[0], mx[1] - mn[1]);
                heightMm = mx[2] - mn[2];
                return true;
            }
            catch (Exception ex) { Log.Warn($"{InterpTag}: não consegui medir o corpo ({ex.GetBaseException().Message})."); return false; }
        }

        private static string FeatureStatusText(object feature)
        {
            if (feature == null) return "(null)";
            try
            {
                long v = Convert.ToInt64(((dynamic)feature).Status) & 0xFFFFFFFFL;
                return v == 1216476310L ? "igFeatureOK" : v == 1216476311L ? "igFeatureFailed" : v.ToString();
            }
            catch { return "(sem Status)"; }
        }
    }
}
