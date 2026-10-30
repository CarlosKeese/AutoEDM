using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Mold.Cooling
{
    public sealed class CoolingResult
    {
        public int Created { get; set; }
        public List<string> Failed { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Modela o plano da refrigeração na PEÇA ORDENADA (Carlos, 2026-09-24): um recurso de FURO
    /// por passada de broca, mais um furo roscado coaxial por engate/tampão.
    ///
    /// Por que furo e não corte extrudado: é o recurso que o ferramenteiro reconhece na árvore, e a
    /// ponta de broca (118°, profundidade até o ombro), a rosca de tubo e o nome saem do próprio
    /// <c>HoleData</c> — receitas já validadas no projeto (skill <c>modeling-recipes.md</c>).
    ///
    /// O furo ACOMPANHA o esboço 3D (Carlos, 2026-10-30: "editar o rascunho 3D não atualiza os
    /// furos"): ver <see cref="TryHoleAssociative"/>. Se a receita associativa falhar num furo, ele
    /// sai pelo caminho antigo — plano paralelo ao plano-base a distância FIXA, centro e
    /// profundidade fixos (<see cref="Candidate"/>) — e o log diz que aquele não acompanha.
    ///
    /// Nada é aceito por "não lançou": o referencial do esboço é CONFERIDO (origem na ponta,
    /// normal no eixo do canal) e o furo criado é MEDIDO (as faces dele têm de cobrir um ponto
    /// de dentro do canal) — lado errado apaga e tenta o outro.
    /// </summary>
    public static class CoolingChannelModeler
    {
        private const int igLeft = 1, igRight = 2;
        private const int igCurveStart = 14, igCurveEnd = 15, igPivotStart = 3, igPivotEnd = 4;
        private const int igRegularHole = 33, igTappedHole = 37;
        private const int igFinite = 13, igVBottomDimToFlat = 145;
        private const int seTapDrillDiameter = 0;
        private const int igStraightPipeThread = 165, igTaperedPipeThread = 166;
        private const int igQueryAll = 1;

        /// <summary>Referencial de um plano-base (Item 1..3), lido UMA vez por rodada.</summary>
        private sealed class BaseFrame
        {
            public int Index;
            public double[] Normal, Origin;
        }

        public static CoolingResult Create(dynamic app, IList<CoolingLine> lines, CoolingPlan plan, CoolingOptions opt)
        {
            var res = new CoolingResult();
            dynamic doc0 = null;
            try { doc0 = app.ActiveDocument; } catch { }
            List<BaseFrame> frames = doc0 == null ? new List<BaseFrame>() : BaseFrames(doc0);

            foreach (CoolingHolePlan h in plan.Holes)
            {
                CoolingLine line = lines.FirstOrDefault(l => l.Index == h.EntryLineIndex);

                // Documento FRESCO a cada furo: furo que falha pode desconectar o proxy da peça.
                dynamic doc = null;
                try { doc = app.ActiveDocument; } catch { }
                if (doc == null) { res.Failed.Add($"{h.Label}: documento ativo sumiu."); break; }

                string why;
                if (TryHoleAssociative(doc, lines, h, opt, res.Warnings, out why)) { res.Created++; continue; }
                Log.Info($"  [associativo] {why} — vai pelo plano a distância fixa (não acompanha o esboço 3D).");
                doc = null;
                try { doc = app.ActiveDocument; } catch { }
                if (doc == null) { res.Failed.Add($"{h.Label}: documento ativo sumiu."); break; }
                if (TryHole(doc, line, frames, h, opt, res.Warnings, out why)) res.Created++;
                else res.Failed.Add($"{h.Label}: {why}");
            }
            Log.Info($"Refrigeração: {res.Created} furo(s) criado(s), {res.Failed.Count} falha(s).");
            return res;
        }

        /// <summary>
        /// Onde o esboço do furo pode nascer, em ordem de preferência:
        /// <list type="number">
        /// <item>Canal paralelo a um eixo (o caso normal): um plano PARALELO ao plano-base
        ///   perpendicular a ele, na distância da boca — os dois lados de <c>AddParallelByDistance</c>,
        ///   a conferência do referencial descarta o errado. Não depende da aresta do esboço 3D, que
        ///   MORRE a cada furo criado (1º run ao vivo, 2026-09-24: depois do 1º furo, todo
        ///   <c>AddNormalToCurve</c> na aresta lida antes deu E_FAIL), e serve para a boca de
        ///   PROLONGAMENTO, que nem está em cima da linha.</item>
        /// <item>Canal inclinado com a boca na ponta da linha: plano normal à aresta, relida AGORA.</item>
        /// </list>
        /// </summary>
        private sealed class Candidate
        {
            public Func<object> Make;
            public bool IsBase;          // plano-base do documento: nunca apagar nem esconder
        }

        private static IEnumerable<Candidate> PlaneCandidates(dynamic doc, CoolingLine line, List<BaseFrame> frames, CoolingHolePlan h)
        {
            BaseFrame f = frames.FirstOrDefault(b => Math.Abs(ProfilePlaneFrame.Dot(b.Normal, h.DirMm)) > 0.9999);
            if (f != null)
            {
                double dist = ProfilePlaneFrame.Dot(ProfilePlaneFrame.Sub(h.EntryMm, f.Origin), f.Normal);
                if (Math.Abs(dist) < 0.001) yield return new Candidate { Make = () => BasePlane(doc, f.Index), IsBase = true };
                else
                    foreach (int side in new[] { 2, 1 })
                    {
                        int sd = side;
                        yield return new Candidate { Make = () => ParallelPlane(doc, f.Index, Math.Abs(dist), sd) };
                    }
                yield break;
            }
            if (h.Extended || line == null) yield break;

            object edge = CoolingLineReader.FindEdge(doc, line.StartMm, line.EndMm) ?? line.Edge;
            for (int orient = 1; orient <= 3; orient++)
            {
                int o = orient;
                yield return new Candidate { Make = () => NormalPlane(doc, edge, h.EntryAtLineStart, o) };
            }
        }

        private static List<BaseFrame> BaseFrames(dynamic doc)
        {
            var list = new List<BaseFrame>();
            // Pela geometria do plano (GetRootPoint/GetNormal): o esboço descartável de antes, ao ser
            // apagado, ZERAVA a pilha de desfazer da peça (sonda UndoProbe, 2026-10-29). Ele fica só
            // de reserva, se a geometria não responder.
            for (int i = 1; i <= 3; i++)
            {
                ProfilePlaneFrame fr = ProfilePlaneFrame.OfRefPlane(BasePlane(doc, i));
                if (fr != null) list.Add(new BaseFrame { Index = i, Normal = fr.Normal, Origin = fr.Origin });
            }
            if (list.Count == 3) return list;
            list.Clear();
            using (var scope = new SketchScope(doc, "Refrigeração (planos-base)"))
                for (int i = 1; i <= 3; i++)
                {
                    try
                    {
                        dynamic ps = scope.AddProfileSet();
                        dynamic prof = ps.Profiles.Add(doc.RefPlanes.Item(i));
                        ProfilePlaneFrame fr = ProfilePlaneFrame.Discover((object)prof);
                        if (fr != null) list.Add(new BaseFrame { Index = i, Normal = fr.Normal, Origin = fr.Origin });
                        scope.DropProfileSet((object)ps);
                    }
                    catch (Exception e) { Log.Warn($"  [plano] plano-base {i} ilegível — " + e.GetBaseException().Message); }
                }
            return list;
        }

        private static object BasePlane(dynamic doc, int i)
        {
            try { return (object)doc.RefPlanes.Item(i); } catch { return null; }
        }

        /// <summary><c>RefPlanes.AddParallelByDistance(Base, Distância, NormalSide, …)</c> — receita do bloco/O'ring.</summary>
        private static object ParallelPlane(dynamic doc, int i, double distMm, int normalSide)
        {
            try
            {
                return (object)doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(i), Units.MmToM(distMm), normalSide,
                    Type.Missing, Type.Missing, Type.Missing);
            }
            catch (Exception e)
            {
                Log.Warn($"  [plano] AddParallelByDistance(Item({i}), {distMm:0.###} mm, lado {normalSide}): " + e.GetBaseException().Message);
                return null;
            }
        }

        private static bool TryHole(dynamic doc, CoolingLine line, List<BaseFrame> frames, CoolingHolePlan h, CoolingOptions opt, List<string> warnings, out string why)
        {
            why = "nenhuma tentativa deu certo (veja o log).";
            Log.Info(string.Format(CultureInfo.InvariantCulture,
                "Refrigeração: {0} — entrada ({1:0.###}, {2:0.###}, {3:0.###}), Ø{4:0.###}, profundidade {5:0.###} mm, {6}.",
                h.Label, h.EntryMm[0], h.EntryMm[1], h.EntryMm[2], h.DiameterMm, h.DepthMm,
                h.Thread != null ? "rosca " + h.Thread.Size.Trim() : h.DrillPoint ? "ponta de broca" : "fundo reto"));

            using (var scope = new SketchScope(doc, "Refrigeração " + h.Label))
            {
                // Lado: primeiro o que o referencial indica (side 1 = −normal, observado no furo da
                // fixação do bloco), depois o outro. Cada tentativa tem plano e esboço próprios —
                // apagar um furo leva o esboço dele junto.
                bool? preferRight = null;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    bool anyCandidate = false;
                    foreach (Candidate c in PlaneCandidates(doc, line, frames, h))
                    {
                        anyCandidate = true;
                        object plane = c.Make();
                        if (plane == null) continue;
                        bool isBase = c.IsBase;
                        // Plano fora da boca do canal descartado pela GEOMETRIA, sem abrir esboço nele:
                        // esboço apagado zera a pilha de desfazer da peça (sonda UndoProbe, 2026-10-29).
                        ProfilePlaneFrame where = ProfilePlaneFrame.OfRefPlane(plane);
                        if (where != null && (Math.Abs(where.SignedDistance(h.EntryMm)) > 0.05 ||
                                              Math.Abs(ProfilePlaneFrame.Dot(where.Normal, h.DirMm)) < 0.999))
                        {
                            Log.Info("  [plano] o plano não está na boca/perpendicular ao canal — descartado sem esboço.");
                            if (!isBase) { try { ((dynamic)plane).Visible = false; } catch { } }
                            continue;
                        }
                        if (!isBase) scope.TrackTempPlane(plane);

                        dynamic ps = scope.AddProfileSet();
                        dynamic prof = ps.Profiles.Add(plane);
                        ProfilePlaneFrame frame = ProfilePlaneFrame.Discover((object)prof);
                        if (frame == null || Math.Abs(frame.SignedDistance(h.EntryMm)) > 0.05 ||
                            Math.Abs(ProfilePlaneFrame.Dot(frame.Normal, h.DirMm)) < 0.999)
                        {
                            Log.Warn("  [plano] o plano não está na boca/perpendicular ao canal — descartado.");
                            scope.DropProfileSet((object)ps);
                            continue;
                        }
                        if (preferRight == null) preferRight = ProfilePlaneFrame.Dot(frame.Normal, h.DirMm) > 0;
                        int side = (attempt == 0) == preferRight.Value ? igRight : igLeft;

                        if (!frame.TryTo2dMm((object)prof, h.EntryMm, out double x, out double y)) { scope.DropProfileSet((object)ps); continue; }
                        prof.Holes2d.Add(Units.MmToM(x), Units.MmToM(y));
                        try { prof.End(1); } catch (Exception e) { Log.Warn("  Profile.End: " + e.GetBaseException().Message); }

                        object hd = HoleData(doc, h, opt, warnings);
                        if (hd == null) { why = "o Solid Edge recusou os dados do furo (HoleData)."; scope.DropProfileSet((object)ps); return false; }

                        object feat = null;
                        try
                        {
                            dynamic holes = doc.Models.Item(1).Holes;
                            feat = h.Thread != null
                                ? (object)holes.AddFiniteEx(prof, side, Units.MmToM(h.DepthMm), hd, false)
                                : (object)holes.AddFinite(prof, side, Units.MmToM(h.DepthMm), hd);
                        }
                        catch (Exception e)
                        {
                            Log.Warn($"  Holes.AddFinite{(h.Thread != null ? "Ex" : "")}(lado {side}): " + e.GetBaseException().Message);
                            scope.DropProfileSet((object)ps);
                            break;   // lado recusado: vai para o outro lado
                        }

                        string status = StatusOf(feat);
                        bool reaches = Reaches(feat, h, h.DiameterMm / 2.0 + 0.5, out string measure);
                        Log.Info($"  furo lado {side}: {status}; {measure}.");
                        if (status != "igFeatureFailed" && reaches)
                        {
                            scope.Release((object)ps);
                            scope.Release(plane);
                            if (!isBase) scope.HideTempPlane(plane);
                            try { ((dynamic)feat).Name = h.Label; } catch (Exception e) { Log.Warn("  nome do furo não aplicado — " + e.GetBaseException().Message); }
                            try { prof.Visible = false; } catch { }
                            Sealing.ORingGrooveModeler.HideFeatureSketch(feat);
                            return true;
                        }

                        // Lado errado (ou falhou): o furo sai, e a próxima tentativa refaz tudo do outro lado.
                        try { ((dynamic)feat).Delete(); }
                        catch (Exception e) { Log.Warn("  furo do lado errado não pôde ser apagado — " + e.GetBaseException().Message); }
                        scope.DropProfileSet((object)ps);
                        break;
                    }
                    if (!anyCandidate)
                    {
                        why = "canal inclinado com a boca fora da linha (prolongado até a face) — ainda não suportado; " +
                              "desenhe a linha até a face.";
                        return false;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Furo PRESO ao esboço 3D — receita medida na sonda <c>tools/UndoProbe --assoc</c> (peça
        /// descartável, 2026-10-30), mudando o comprimento da linha pela API:
        /// <list type="bullet">
        /// <item>Perfil num plano NORMAL à linha na ponta (<c>AddNormalToCurve</c>, aresta relida
        ///   agora): a origem do esboço cai na ponta, então o centro do furo vai junto com ela.</item>
        /// <item>Furo de plano a plano (<c>Holes.AddFromTo</c>/<c>AddFromToEx</c>): até o plano
        ///   normal na ponta de lá, ou um PARALELO a ele (sobrefuro, rompimento) — paralelo acompanha
        ///   o plano-pai. <c>AddNormalToCurveAtDistance</c> NÃO passa da ponta (negativo cai nela).</item>
        /// <item>Boca prolongada até a face: o "de" é um plano paralelo À FACE a 0 mm (a face
        ///   direto no <c>AddFromTo</c> não fura nada — nem plana nem cilíndrica). Assim a boca fica
        ///   na face mesmo se a linha encolher.</item>
        /// <item>Rosca (engate/tampão): da boca até um paralelo a ela, na profundidade da rosca.</item>
        /// </list>
        /// Planos criados ficam ESCONDIDOS (são pais do furo). Falhou = nada fica: furo apagado,
        /// esboço descartado, planos escondidos; quem chama tenta o caminho antigo.
        /// </summary>
        private static bool TryHoleAssociative(dynamic doc, IList<CoolingLine> lines, CoolingHolePlan h, CoolingOptions opt, List<string> warnings, out string why)
        {
            why = "receita associativa indisponível";
            CoolingLine line = lines?.FirstOrDefault(l => l.Index == h.EntryLineIndex);
            if (line == null) { why = "linha da boca não identificada"; return false; }
            if (Math.Abs(ProfilePlaneFrame.Dot(Vec.Unit(Vec.Sub(line.EndMm, line.StartMm)), h.DirMm)) < 0.9999)
            {
                why = "o furo não está no eixo da linha";
                return false;
            }
            double[] tip = h.EntryAtLineStart ? line.StartMm : line.EndMm;
            var planes = new List<object>();

            using (var scope = new SketchScope(doc, "Refrigeração " + h.Label))
            {
                object feat = null;
                dynamic ps = null;
                try
                {
                    object mouth = NormalPlaneAt(doc, line, h.EntryAtLineStart, h.DirMm, planes);
                    if (mouth == null) { why = "plano normal à linha na boca não saiu"; return Undo(planes, null, scope, null); }

                    object from = mouth;
                    if (h.ExtensionMm > opt.EndToleranceMm)
                    {
                        object face = FaceAt(doc, h.EntryMm, h.DirMm);
                        from = face != null ? ParallelThrough(doc, face, 0, h.EntryMm, planes) : null;
                        if (from == null)
                        {
                            Log.Info("  [associativo] face da boca não achada — boca a distância fixa da ponta da linha.");
                            from = ParallelThrough(doc, mouth, h.ExtensionMm, h.EntryMm, planes);
                        }
                        if (from == null) { why = "plano da boca na face não saiu"; return Undo(planes, null, scope, null); }
                    }

                    object to;
                    if (h.FarLineIndex > 0)
                    {
                        CoolingLine far = lines.FirstOrDefault(l => l.Index == h.FarLineIndex);
                        object farPlane = far == null ? null : NormalPlaneAt(doc, far, h.FarAtLineStart, h.DirMm, planes);
                        double[] farTip = far == null ? null : h.FarAtLineStart ? far.StartMm : far.EndMm;
                        to = farPlane == null ? null
                           : h.FarBeyondMm > opt.EndToleranceMm ? ParallelThrough(doc, farPlane, h.FarBeyondMm, Vec.Add(farTip, h.DirMm, h.FarBeyondMm), planes)
                           : farPlane;
                    }
                    else to = ParallelThrough(doc, from, h.DepthMm, Vec.Add(h.EntryMm, h.DirMm, h.DepthMm), planes);
                    if (to == null) { why = "plano do fundo não saiu"; return Undo(planes, null, scope, null); }

                    ps = scope.AddProfileSet();
                    dynamic prof = ps.Profiles.Add(mouth);
                    ProfilePlaneFrame frame = ProfilePlaneFrame.Discover((object)prof);
                    double x = 0, y = 0;
                    if (frame == null || !frame.TryTo2dMm((object)prof, tip, out x, out y))
                        Log.Info("  [associativo] referencial do esboço ilegível — centro na origem (é a ponta da linha).");
                    prof.Holes2d.Add(Units.MmToM(x), Units.MmToM(y));
                    try { prof.End(1); } catch (Exception e) { Log.Warn("  Profile.End: " + e.GetBaseException().Message); }

                    object hd = HoleData(doc, h, opt, warnings);
                    if (hd == null) { why = "o Solid Edge recusou os dados do furo (HoleData)"; return Undo(planes, null, scope, ps); }
                    dynamic holes = doc.Models.Item(1).Holes;
                    feat = h.Thread != null ? (object)holes.AddFromToEx(prof, from, to, hd, false) : (object)holes.AddFromTo(prof, from, to, hd);

                    string status = StatusOf(feat);
                    bool reaches = Reaches(feat, h, h.DiameterMm / 2.0 + 0.5, out string measure);
                    Log.Info($"  [associativo] furo de plano a plano: {status}; {measure}.");
                    if (status == "igFeatureFailed" || !reaches) { why = "o furo de plano a plano não vingou"; return Undo(planes, feat, scope, ps); }

                    scope.Release((object)ps);
                    foreach (object p in planes) { scope.Release(p); Hide(p); }
                    try { ((dynamic)feat).Name = h.Label; } catch (Exception e) { Log.Warn("  nome do furo não aplicado — " + e.GetBaseException().Message); }
                    try { prof.Visible = false; } catch { }
                    Sealing.ORingGrooveModeler.HideFeatureSketch(feat);
                    Log.Info($"  [associativo] {h.Label}: preso ao esboço 3D ({planes.Count} plano(s) escondido(s)).");
                    return true;
                }
                catch (Exception e)
                {
                    why = "erro na receita associativa: " + e.GetBaseException().Message;
                    return Undo(planes, feat, scope, ps);
                }
            }
        }

        /// <summary>Desfaz uma tentativa associativa: furo apagado, esboço descartado, planos escondidos. Sempre false.</summary>
        private static bool Undo(List<object> planes, object feat, SketchScope scope, object ps)
        {
            if (feat != null) { try { ((dynamic)feat).Delete(); } catch (Exception e) { Log.Warn("  [associativo] furo não pôde ser apagado — " + e.GetBaseException().Message); } }
            if (ps != null) scope.DropProfileSet(ps);
            foreach (object p in planes) Hide(p);
            return false;
        }

        private static void Hide(object plane) { try { ((dynamic)plane).Visible = false; } catch { } }

        /// <summary>
        /// Plano normal à linha na ponta pedida, com a aresta RELIDA agora (a lida antes morre a cada
        /// recurso) e a geometria CONFERIDA: origem na ponta, normal no eixo.
        /// </summary>
        private static object NormalPlaneAt(dynamic doc, CoolingLine line, bool atLineStart, double[] axis, List<object> planes)
        {
            double[] tip = atLineStart ? line.StartMm : line.EndMm;
            object edge = CoolingLineReader.FindEdge(doc, line.StartMm, line.EndMm);
            if (edge == null || !EdgeGeometry.TryGetEndPointsMm(edge, out double[] a, out double[] b, out _)) return null;
            bool atEdgeStart = Vec.Dist(a, tip) < 1e-3;
            for (int orient = 1; orient <= 3; orient++)
            {
                object p = NormalPlane(doc, edge, atEdgeStart, orient);
                if (p == null) continue;
                planes.Add(p);
                ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane(p);
                if (f == null || (Vec.Dist(f.Origin, tip) < 0.01 && Math.Abs(ProfilePlaneFrame.Dot(f.Normal, axis)) > 0.999)) return p;
                Log.Info($"  [associativo] plano normal (orientação {orient}) fora da ponta/eixo — escondido.");
                Hide(p);
            }
            return null;
        }

        /// <summary>Plano paralelo a <paramref name="basePlane"/> (plano ou face plana) a <paramref name="distMm"/>, do lado que passa por <paramref name="targetMm"/>.</summary>
        private static object ParallelThrough(dynamic doc, object basePlane, double distMm, double[] targetMm, List<object> planes)
        {
            foreach (int side in new[] { igRight, igLeft })
            {
                object p;
                try { p = (object)doc.RefPlanes.AddParallelByDistance(basePlane, Units.MmToM(distMm), side, Type.Missing, Type.Missing, Type.Missing); }
                catch (Exception e) { Log.Warn($"  [associativo] AddParallelByDistance({distMm:0.###} mm, lado {side}): " + e.GetBaseException().Message); continue; }
                planes.Add(p);
                ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane(p);
                if (f == null || Math.Abs(f.SignedDistance(targetMm)) < 0.01) return p;
                Hide(p);
                if (distMm < 1e-9) break;   // a 0 mm os dois lados são o mesmo plano
            }
            return null;
        }

        /// <summary>A face plana do corpo onde está a boca, perpendicular ao canal (pela caixa dela: achatada no eixo).</summary>
        private static object FaceAt(dynamic doc, double[] pointMm, double[] axis)
        {
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[igQueryAll];
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    object f = (object)faces.Item(i);
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    bool inside = true;
                    for (int k = 0; k < 3; k++) if (pointMm[k] < mn[k] - 0.05 || pointMm[k] > mx[k] + 0.05) inside = false;
                    if (inside && Math.Abs(ProfilePlaneFrame.Dot(Vec.Sub(mx, mn), axis)) < 0.05) return f;
                }
            }
            catch (Exception e) { Log.Warn("  [associativo] faces do corpo ilegíveis — " + e.GetBaseException().Message); }
            return null;
        }

        /// <summary>
        /// <c>RefPlanes.AddNormalToCurve(Curve, PlanePoint, OrientationPlaneOrPivot, PivotOrigin, [Local], [ParentCurve])</c>
        /// — assinatura do dump, validada ao vivo no O'ring com aresta circular.
        /// </summary>
        private static object NormalPlane(dynamic doc, object edge, bool atStart, int orient)
        {
            try
            {
                object refPlanes = (object)doc.RefPlanes;
                object orientPlane = (object)doc.RefPlanes.Item(orient);
                return refPlanes.GetType().InvokeMember("AddNormalToCurve", BindingFlags.InvokeMethod, null, refPlanes,
                    new object[] { edge, atStart ? igCurveStart : igCurveEnd, orientPlane, atStart ? igPivotStart : igPivotEnd, Type.Missing, Type.Missing });
            }
            catch (Exception e)
            {
                Log.Warn($"  [plano] AddNormalToCurve(linha, {(atStart ? "início" : "fim")}, RefPlanes.Item({orient})): " + e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>
        /// Canal liso: <c>HoleDataCollection.Add</c> (21 params) com ponta de broca quando pedida
        /// (<c>BottomAngle</c> em GRAUS, profundidade até o OMBRO). Rosca de tubo: <c>AddEx</c> pela
        /// base de furos com as strings EXATAS da planilha, <c>TreatmentType = 37</c> (é ele que liga a
        /// rosca, não o HoleType) e <c>IgnoreSavedDefaultValues = true</c> sempre.
        /// </summary>
        private static object HoleData(dynamic doc, CoolingHolePlan h, CoolingOptions opt, List<string> warnings)
        {
            object hdc;
            try { hdc = (object)doc.HoleDataCollection; }
            catch (Exception e) { Log.Warn("  HoleDataCollection: " + e.GetBaseException().Message); return null; }

            if (h.Thread == null)
            {
                var a = new object[21];
                for (int i = 0; i < a.Length; i++) a[i] = Type.Missing;
                a[0] = igRegularHole;
                a[1] = Units.MmToM(h.DiameterMm);
                if (h.DrillPoint) { a[6] = opt.DrillPointDeg; a[13] = igVBottomDimToFlat; }
                a[20] = true;
                try { return hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, a); }
                catch (Exception e) { Log.Warn("  HoleDataCollection.Add: " + e.GetBaseException().Message); return null; }
            }

            var x = new object[27];
            for (int i = 0; i < x.Length; i++) x[i] = Type.Missing;
            x[0] = igRegularHole;
            x[1] = h.Thread.Standard;
            x[2] = h.Thread.SubType;
            x[3] = h.Thread.Size;                                // EXATO, com os espaços da planilha
            x[5] = Units.MmToM(h.Thread.NominalMm);
            x[11] = igTappedHole;                                // TreatmentType
            x[15] = igFinite;                                    // ThreadDepthMethod
            x[16] = Units.MmToM(h.ThreadDepthMm);                // ThreadDepth
            x[24] = true;                                        // IgnoreSavedDefaultValues
            x[25] = seTapDrillDiameter;                          // ThreadDiameterOption
            x[26] = Units.MmToM(h.Thread.TapDrillMm);            // ThreadTapDrillDiameter
            try
            {
                object hd = hdc.GetType().InvokeMember("AddEx", BindingFlags.InvokeMethod, null, hdc, x);
                dynamic d = hd;
                try { d.ThreadSetting = h.Thread.Tapered ? igTaperedPipeThread : igStraightPipeThread; }
                catch (Exception e) { Log.Warn("  ThreadSetting: " + e.GetBaseException().Message); }
                int treatment = -1; try { treatment = Convert.ToInt32(d.TreatmentType); } catch { }
                if (treatment != igTappedHole)
                {
                    string msg = $"{h.Label}: o Solid Edge não ligou a rosca ({h.Thread.Standard} / {h.Thread.Size.Trim()}; " +
                                 $"TreatmentType={treatment}) — o furo sai liso no Ø da broca.";
                    warnings.Add(msg);
                    Log.Warn("  " + msg);
                }
                return hd;
            }
            catch (Exception e)
            {
                Log.Warn("  HoleDataCollection.AddEx (rosca de tubo): " + e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>
        /// O furo foi para o lado certo? Primeiro: as faces cobrem o ponto de dentro (boca + 5 mm).
        /// Se não cobrem, ainda vale se a caixa das faces está À FRENTE da boca e no eixo do canal:
        /// onde o canal atravessa furo que já existia, o furo novo não cria face (L1/L3 da MD-15428,
        /// 2026-10-30: faces só em X −65…65 com a boca em −80; o lado certo era apagado e o outro,
        /// furando para fora, falhava).
        /// </summary>
        private static bool Reaches(object feat, CoolingHolePlan h, double tolMm, out string measure)
        {
            double[] pointMm = h.TowardMm;
            measure = "sem faces legíveis";
            try
            {
                object faces = feat.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, feat, new object[] { igQueryAll });
                dynamic coll = faces;
                int n = 0; try { n = (int)coll.Count; } catch { }
                if (n == 0) { measure = "furo sem faces"; return false; }
                double[] mn = { double.MaxValue, double.MaxValue, double.MaxValue };
                double[] mx = { double.MinValue, double.MinValue, double.MinValue };
                int read = 0;
                for (int i = 1; i <= n; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)coll.Item(i), out double[] a, out double[] b)) continue;
                    read++;
                    for (int k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], a[k]); mx[k] = Math.Max(mx[k], b[k]); }
                }
                if (read == 0) { measure = $"{n} face(s), nenhuma caixa legível — aceito pelo Status"; return true; }
                bool inside = true;
                for (int k = 0; k < 3; k++) if (pointMm[k] < mn[k] - tolMm || pointMm[k] > mx[k] + tolMm) inside = false;

                // Centro da caixa: avanço ao longo do canal a partir da boca e afastamento do eixo.
                double[] c = { (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mn[2] + mx[2]) / 2 };
                double[] rel = ProfilePlaneFrame.Sub(c, h.EntryMm);
                double ahead = ProfilePlaneFrame.Dot(rel, h.DirMm);
                double off2 = ProfilePlaneFrame.Dot(rel, rel) - ahead * ahead;
                bool onAxisAhead = ahead > tolMm && off2 < tolMm * tolMm;

                measure = string.Format(CultureInfo.InvariantCulture,
                    "{0} face(s), caixa X {1:0.#}…{2:0.#} Y {3:0.#}…{4:0.#} Z {5:0.#}…{6:0.#} mm, ponto de dentro {7}",
                    n, mn[0], mx[0], mn[1], mx[1], mn[2], mx[2],
                    inside ? "COBERTO" : onAxisAhead ? $"fora, mas as faces estão {ahead:0.#} mm à frente da boca, no eixo (atravessa furo existente?) — aceito" : "fora");
                return inside || onAxisAhead;
            }
            catch (Exception e)
            {
                measure = "faces do furo ilegíveis (" + e.GetBaseException().Message + ") — aceito pelo Status";
                return true;
            }
        }

        private static string StatusOf(object feature)
        {
            if (feature == null) return "(null)";
            try
            {
                long v = Convert.ToInt64(((dynamic)feature).Status);
                return v == 1216476310L ? "igFeatureOK" : v == 1216476311L ? "igFeatureFailed" : v.ToString(CultureInfo.InvariantCulture);
            }
            catch { return "(sem Status)"; }
        }
    }
}
