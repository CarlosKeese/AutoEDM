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
    /// Por que o plano nasce da ARESTA da linha (<c>RefPlanes.AddNormalToCurve</c> na ponta de
    /// entrada — a receita do O'ring, validada ao vivo): o furo fica preso à linha do esboço 3D;
    /// mover a linha leva o furo junto, e linha inclinada não precisa de caso especial.
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
                        bool reaches = Reaches(feat, h.TowardMm, h.DiameterMm / 2.0 + 0.5, out string measure);
                        Log.Info($"  furo lado {side}: {status}; {measure}.");
                        if (status != "igFeatureFailed" && reaches)
                        {
                            scope.Release((object)ps);
                            scope.Release(plane);
                            if (!isBase) scope.HideTempPlane(plane);
                            try { ((dynamic)feat).Name = h.Label; } catch (Exception e) { Log.Warn("  nome do furo não aplicado — " + e.GetBaseException().Message); }
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

        /// <summary>As faces do furo cobrem o ponto de dentro do canal? É o que diz se o lado está certo.</summary>
        private static bool Reaches(object feat, double[] pointMm, double tolMm, out string measure)
        {
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
                measure = string.Format(CultureInfo.InvariantCulture,
                    "{0} face(s), caixa X {1:0.#}…{2:0.#} Y {3:0.#}…{4:0.#} Z {5:0.#}…{6:0.#} mm, ponto de dentro {7}",
                    n, mn[0], mx[0], mn[1], mx[1], mn[2], mx[2], inside ? "COBERTO" : "fora");
                return inside;
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
