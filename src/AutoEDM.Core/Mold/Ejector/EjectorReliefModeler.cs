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

namespace AutoEDM.Mold.Ejector
{
    public sealed class EjectorReliefResult
    {
        public int Created { get; set; }
        public int Features { get; set; }
        public List<string> Failed { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Modela os alívios na PEÇA ORDENADA: um recurso de FURO por extrator, Ø de alívio, fundo
    /// reto, da face selecionada até deixar a guia na outra ponta. Receita da Refrigeração
    /// (<see cref="Cooling.CoolingChannelModeler"/>), já validada no SE:
    /// <list type="bullet">
    /// <item>Plano paralelo À FACE a 0 mm (<c>RefPlanes.AddParallelByDistance</c>) — o alívio
    /// acompanha a face se a placa mudar de espessura; plano-base paralelo é a reserva.</item>
    /// <item><c>Profile.Holes2d.Add</c> + <c>Holes.AddFinite(perfil, lado, profundidade, HoleData)</c>,
    /// com <c>HoleDataCollection.Add</c> (21 parâmetros, <c>IgnoreSavedDefaultValues = true</c>).</item>
    /// <item>Lado conferido pela MEDIDA (as faces do furo cobrem um ponto dentro do alívio); lado
    /// errado apaga e tenta o outro, e o lado bom vale para os furos seguintes.</item>
    /// </list>
    /// </summary>
    public static class EjectorReliefModeler
    {
        private const int igLeft = 1, igRight = 2;

        private const int igRegularHole = 33;
        private const int igQueryAll = 1;

        public static EjectorReliefResult Create(dynamic app, object comFace, EjectorReliefPlan plan)
        {
            var res = new EjectorReliefResult();
            List<EjectorHole> holes = plan.ToRelieve.ToList();
            if (holes.Count == 0) return res;

            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null) { res.Failed.Add("documento ativo sumiu."); return res; }

            double[] axisVec = new double[3]; axisVec[plan.Axis] = 1;
            int? goodSide = null;

            using (var scope = new SketchScope(doc, "Alívio de extratores"))
            {
                var src = new PlaneSource(doc, scope, comFace, plan, holes[0].CenterMm, axisVec);
                if (src.Get() == null) { res.Failed.Add("o plano na face não saiu (nem paralelo à face nem a um plano-base)."); return res; }

                // Um recurso por GRUPO de mesmo Ø e profundidade (Carlos, 2026-11-09). Se a SE recusar
                // o grupo, sai um por um — com o plano RECRIADO se o apagar do grupo o matou (1º run
                // agrupado, 2026-11-09: RPC_E_DISCONNECTED no plano derrubou todos os furos seguintes).
                // Grupo = como a SE faz à mão (gravador, 2026-11-09: o Furo de 3 furos feito na interface
                // é um UserDefinedPattern com UM PERFIL POR FURO — vários Holes2d num perfil só não
                // funcionam pela API: End colapsa para 1, sem End o furo falha). Pela API: o 1º furo é a
                // SEMENTE (Holes.AddFinite, caminho provado) e os outros entram por
                // UserDefinedPatterns.AddByProfiles, cada um no seu perfil, preso à boca do furo.
                foreach (ReliefGroup g in plan.Groups())
                {
                    string why;
                    if (g.Holes.Count > 1)
                    {
                        if (TryGroup(doc, scope, src, plan, g, ref goodSide, out why)) { res.Created += g.Holes.Count; res.Features++; continue; }
                        Log.Warn($"Alívio de extratores: grupo {g.Labels} recusado ({why}) — um por um.");
                    }
                    foreach (EjectorHole h in g.Holes)
                    {
                        if (TryRelief(doc, scope, src, plan, new List<EjectorHole> { h }, ref goodSide, out why, out object _)) { res.Created++; res.Features++; }
                        else res.Failed.Add($"{h.Label}: {why}");
                    }
                }
                src.Finish(res.Created > 0);
            }
            Log.Info($"Alívio de extratores: {res.Created} alívio(s) em {res.Features} recurso(s), {res.Failed.Count} falha(s).");
            return res;
        }

        /// <summary>Um recurso de furo com os alívios de <paramref name="holes"/> (mesmo Ø e profundidade).</summary>
        private static bool TryRelief(dynamic doc, SketchScope scope, PlaneSource src, EjectorReliefPlan plan, List<EjectorHole> holes, ref int? goodSide, out string why, out object created)
        {
            why = "nenhum dos dois lados deu certo (veja o log).";
            created = null;
            EjectorHole h0 = holes[0];
            string labels = string.Join(", ", holes.Select(x => x.Label));
            foreach (EjectorHole h in holes)
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "Alívio {0}: centro ({1:0.###}, {2:0.###}, {3:0.###}), furo Ø{4:0.##} × {5:0.##}, guia {6:0.#} → alívio Ø{7:0.0} × {8:0.##} mm{9}.",
                    h.Label, h.CenterMm[0], h.CenterMm[1], h.CenterMm[2], h.DiameterMm, h.LengthMm, h.GuideMm, h.ReliefDiameterMm, h.ReliefDepthMm,
                    h.Forced ? " (FORÇADO: " + h.SkipReason + ")" : ""));

            double[] into = plan.IntoDir;
            bool? preferRight = goodSide.HasValue ? (bool?)(goodSide.Value == igRight) : null;

            // Primeiro PRESO ao furo (centro concêntrico à aresta da boca); se a SE recusar o furo
            // assim, sai solto, como no 1º run (2026-11-09) — e o log diz.
            foreach (bool assoc in new[] { true, false })
            {
                bool tiedAny = false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    dynamic ps = scope.AddProfileSet();
                    dynamic prof = null;
                    for (int p = 0; p < 2 && prof == null; p++)
                    {
                        object plane = src.Get();
                        if (plane == null) break;
                        try { prof = ps.Profiles.Add(plane); }
                        catch (Exception e)
                        {
                            why = "esboço no plano da face recusado — " + e.GetBaseException().Message;
                            Log.Warn("  " + why + (p == 0 ? " — recriando o plano." : ""));
                            src.Invalidate();
                        }
                    }
                    if (prof == null) { scope.DropProfileSet((object)ps); return false; }

                    ProfilePlaneFrame frame = ProfilePlaneFrame.Discover((object)prof);
                    if (preferRight == null) preferRight = frame == null || ProfilePlaneFrame.Dot(frame.Normal, into) > 0;
                    int side = (attempt == 0) == preferRight.Value ? igRight : igLeft;

                    bool bad = false;
                    int tiedCount = 0;
                    foreach (EjectorHole h in holes)
                    {
                        double x = 0, y = 0;
                        if (frame == null || !frame.TryTo2dMm((object)prof, h.CenterMm, out x, out y)) { bad = true; break; }
                        object hole2d = (object)prof.Holes2d.Add(Units.MmToM(x), Units.MmToM(y));
                        if (assoc && TieToHole(doc, (object)prof, hole2d, plan, h)) tiedCount++;
                    }
                    if (bad) { why = "referencial do esboço ilegível"; scope.DropProfileSet((object)ps); return false; }
                    bool tied = tiedCount == holes.Count;
                    tiedAny |= tiedCount > 0;
                    // Profile.End COLAPSA os Holes2d: 3 → 1 em qualquer validação (0, 1, 8193, 524288 —
                    // sonda tools/UndoProbe --multifuro, 2026-11-09), e o recurso furava só o primeiro
                    // (MD-15428, medido no corpo). Com vários furos o perfil vai sem End.
                    if (holes.Count == 1)
                    {
                        try { prof.End(1); } catch (Exception e) { Log.Warn("  Profile.End: " + e.GetBaseException().Message); }
                    }
                    else Log.Info($"  {holes.Count} furos no esboço, sem Profile.End (Holes2d.Count = {Convert.ToString(TryGet(() => (object)prof.Holes2d.Count))}).");

                    object hd = HoleData(doc, h0.ReliefDiameterMm);
                    if (hd == null) { why = "o Solid Edge recusou os dados do furo (HoleData)."; scope.DropProfileSet((object)ps); return false; }

                    string tieNote = !assoc ? ", SOLTO" : tied ? ", preso ao furo" : $", {tiedCount}/{holes.Count} preso(s)";
                    object feat;
                    try { feat = (object)doc.Models.Item(1).Holes.AddFinite(prof, side, Units.MmToM(h0.ReliefDepthMm), hd); }
                    catch (Exception e)
                    {
                        Log.Warn($"  Holes.AddFinite({labels}, lado {side}{tieNote}): " + e.GetBaseException().Message);
                        scope.DropProfileSet((object)ps);
                        continue;
                    }

                    string status = StatusOf(feat);
                    var inside = holes.Select(h => ProfilePlaneFrame.Add(h.CenterMm, into, Math.Min(h.ReliefDepthMm / 2.0, 1.0))).ToList();
                    bool reaches = Covers(feat, inside, 0.05, out string measure);
                    bool partial = false;
                    if (!reaches && status != "igFeatureFailed" && holes.Count > 1)
                    {
                        // Hole.Faces pode listar só um furo do recurso; quem decide é o CORPO: tem o
                        // cilindro do alívio em cada centro?
                        int found = BodyHasReliefs(doc, plan, holes, out string bodyMeasure);
                        measure += $"; no corpo: {bodyMeasure}";
                        reaches = found == holes.Count;
                        partial = !reaches && found > 0;
                    }
                    Log.Info($"  alívio {labels} lado {side}{tieNote}: {status}; {measure}.");
                    if (status != "igFeatureFailed" && reaches)
                    {
                        goodSide = side;
                        scope.Release((object)ps);
                        var pt = CultureInfo.GetCultureInfo("pt-BR");
                        string name = holes.Count == 1
                            ? string.Format(pt, "Alívio {0} Ø{1:0.0}", h0.Label, h0.ReliefDiameterMm)
                            : string.Format(pt, "Alívio Ø{0:0.0} × {1:0.##} ({2})", h0.ReliefDiameterMm, h0.ReliefDepthMm, labels);
                        try { ((dynamic)feat).Name = name; }
                        catch (Exception e) { Log.Warn("  nome do alívio não aplicado — " + e.GetBaseException().Message); }
                        try { prof.Visible = false; } catch { }
                        ORingGrooveModeler.HideFeatureSketch(feat);
                        if (!tied) Log.Warn($"  {labels}: {holes.Count - tiedCount} alívio(s) SEM vínculo com o furo — não acompanham se o furo mudar de lugar.");
                        created = feat;
                        return true;
                    }

                    try { ((dynamic)feat).Delete(); }
                    catch (Exception e) { Log.Warn("  alívio recusado não pôde ser apagado — " + e.GetBaseException().Message); }
                    scope.DropProfileSet((object)ps);
                    if (partial)
                    {
                        // Lado certo, mas a SE furou só parte do grupo: outro lado ou "solto" não muda
                        // isso, e cada recurso apagado custa o Ctrl+Z e o plano. Vai para um por um.
                        why = "a SE fez só parte dos furos do grupo num recurso";
                        return false;
                    }
                }
                if (holes.Count > 1) break;           // grupo: só preso ao furo; o solto fica para um por um
                if (!assoc || !tiedAny) break;      // solto já foi tentado (ou o vínculo nem entrou)
                Log.Warn($"  {labels}: preso ao furo não vingou nos dois lados — tentando solto.");
            }
            return false;
        }

        /// <summary>
        /// Um grupo (mesmo Ø e profundidade) num recurso só, do jeito que a SE faz o Furo de vários furos
        /// na interface — receita PROVADA na sonda <c>tools/UndoProbe --multifuro padrao-preso</c>
        /// (peça descartável, 2026-11-09): 3/3 alívios no corpo, UM item na árvore, e o alívio foi junto
        /// quando o furo de extrator foi movido 10 mm.
        /// <list type="number">
        /// <item>UM ProfileSet com UM PERFIL POR FURO (o 2º <c>Profiles.Add</c> no mesmo set funciona no
        ///   mesmo plano); em cada perfil um Hole2d preso à boca do furo (IncludeEdge + AddConcentric) e
        ///   <c>End(1)</c>. Vários Holes2d num perfil só NÃO: End colapsa para 1, sem End o furo falha.</item>
        /// <item>Semente: <c>Holes.AddFinite</c> no 1º perfil.</item>
        /// <item><c>UserDefinedPatterns.AddByProfiles(N, TODOS os perfis — o da semente incluso —, semente)</c>:
        ///   a semente é absorvida (vira o UserDefinedPattern, como no Furo da interface). Perfis em sets
        ///   separados e sem o da semente: SEM vínculo o padrão saiu vazio; COM vínculo DERRUBOU a SE.</item>
        /// </list>
        /// Conferido no CORPO. Recusado = tudo do grupo sai e quem chama faz um por um.
        /// </summary>
        private static bool TryGroup(dynamic doc, SketchScope scope, PlaneSource src, EjectorReliefPlan plan,
            ReliefGroup g, ref int? goodSide, out string why)
        {
            why = "grupo não saiu (veja o log)";
            foreach (EjectorHole h in g.Holes)
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "Alívio {0}: centro ({1:0.###}, {2:0.###}, {3:0.###}), furo Ø{4:0.##} × {5:0.##}, guia {6:0.#} → alívio Ø{7:0.0} × {8:0.##} mm{9}.",
                    h.Label, h.CenterMm[0], h.CenterMm[1], h.CenterMm[2], h.DiameterMm, h.LengthMm, h.GuideMm, h.ReliefDiameterMm, h.ReliefDepthMm,
                    h.Forced ? " (FORÇADO: " + h.SkipReason + ")" : ""));
            double[] into = plan.IntoDir;
            bool? preferRight = goodSide.HasValue ? (bool?)(goodSide.Value == igRight) : null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                object plane = src.Get();
                if (plane == null) { why = "plano na face não saiu"; return false; }
                dynamic ps = scope.AddProfileSet();
                var profs = new List<SolidEdgePart.Profile>();
                ProfilePlaneFrame frame = null;
                int tied = 0;
                foreach (EjectorHole h in g.Holes)
                {
                    dynamic prof;
                    try { prof = ps.Profiles.Add(plane); }
                    catch (Exception e)
                    {
                        why = $"perfil de {h.Label} recusado — {e.GetBaseException().Message}";
                        scope.DropProfileSet((object)ps); src.Invalidate(); return false;
                    }
                    if (frame == null) frame = ProfilePlaneFrame.Discover((object)prof);
                    double x = 0, y = 0;
                    if (frame == null || !frame.TryTo2dMm((object)prof, h.CenterMm, out x, out y))
                    { why = "referencial do esboço ilegível"; scope.DropProfileSet((object)ps); return false; }
                    object hole2d = (object)prof.Holes2d.Add(Units.MmToM(x), Units.MmToM(y));
                    if (TieToHole(doc, (object)prof, hole2d, plan, h)) tied++;
                    object end = TryGet(() => (object)prof.End(1));
                    if (!"0".Equals(Convert.ToString(end))) Log.Warn($"  [grupo] perfil de {h.Label}: End(1) = {end}.");
                    profs.Add((SolidEdgePart.Profile)(object)prof);
                }
                if (preferRight == null) preferRight = ProfilePlaneFrame.Dot(frame.Normal, into) > 0;
                int side = (attempt == 0) == preferRight.Value ? igRight : igLeft;

                object hd = HoleData(doc, g.DiameterMm);
                if (hd == null) { why = "o Solid Edge recusou os dados do furo (HoleData)."; scope.DropProfileSet((object)ps); return false; }
                object seed;
                try { seed = (object)doc.Models.Item(1).Holes.AddFinite(profs[0], side, Units.MmToM(g.DepthMm), hd); }
                catch (Exception e) { Log.Warn($"  [grupo] semente lado {side}: " + e.GetBaseException().Message); scope.DropProfileSet((object)ps); src.Invalidate(); continue; }
                EjectorHole h0 = g.Holes[0];
                var inside0 = new List<double[]> { ProfilePlaneFrame.Add(h0.CenterMm, into, Math.Min(h0.ReliefDepthMm / 2.0, 1.0)) };
                string seedStatus = StatusOf(seed);
                bool seedOk = seedStatus != "igFeatureFailed" && Covers(seed, inside0, 0.05, out string seedMeasure);
                Log.Info($"  [grupo] semente {h0.Label} lado {side}: {seedStatus}{(seedOk ? "" : " — lado errado")}.");
                if (!seedOk)
                {
                    try { ((dynamic)seed).Delete(); } catch (Exception e) { Log.Warn("  [grupo] semente do lado errado não apagou — " + e.GetBaseException().Message); }
                    scope.DropProfileSet((object)ps); src.Invalidate();
                    continue;
                }
                goodSide = side;

                object udp;
                try
                {
                    object coll = (object)doc.Models.Item(1).UserDefinedPatterns;
                    udp = coll.GetType().InvokeMember("AddByProfiles", BindingFlags.InvokeMethod, null, coll,
                        new object[] { profs.Count, profs.ToArray(), seed });
                }
                catch (Exception e)
                {
                    why = "UserDefinedPatterns.AddByProfiles: " + e.GetBaseException().Message;
                    try { ((dynamic)seed).Delete(); } catch { }
                    scope.DropProfileSet((object)ps); src.Invalidate();
                    return false;
                }
                string status = StatusOf(udp);
                int found = BodyHasReliefs(doc, plan, g.Holes, out string bodyMeasure);
                Log.Info($"  [grupo] {g.Labels}: padrão {status}, NumberInputFeatures = {TryGet(() => (object)((dynamic)udp).NumberInputFeatures)}; " +
                         $"{tied}/{g.Holes.Count} preso(s); no corpo: {bodyMeasure}.");
                if (status != "igFeatureFailed" && found == g.Holes.Count)
                {
                    scope.Release((object)ps);
                    foreach (SolidEdgePart.Profile p in profs) { try { ((dynamic)p).Visible = false; } catch { } }
                    string name = string.Format(CultureInfo.GetCultureInfo("pt-BR"), "Alívio Ø{0:0.0} × {1:0.##} ({2})", g.DiameterMm, g.DepthMm, g.Labels);
                    try { ((dynamic)udp).Name = name; } catch (Exception e) { Log.Warn("  [grupo] nome não aplicado — " + e.GetBaseException().Message); }
                    ORingGrooveModeler.HideFeatureSketch(udp);
                    if (tied < g.Holes.Count) Log.Warn($"  [grupo] {g.Holes.Count - tied} furo(s) SEM vínculo com a boca do furo.");
                    return true;
                }
                why = status == "igFeatureFailed" ? "o padrão nasceu com falha" : $"o padrão fez {found} de {g.Holes.Count} alívio(s)";
                try { ((dynamic)udp).Delete(); } catch (Exception e) { Log.Warn("  [grupo] padrão recusado não apagou — " + e.GetBaseException().Message); }
                try { ((dynamic)seed).Delete(); } catch { }
                scope.DropProfileSet((object)ps); src.Invalidate();
                return false;
            }
            return false;
        }

        private static object TryGet(Func<object> f) { try { return f(); } catch (Exception e) { return "?" + e.GetBaseException().Message; } }

        // ------------------------------------------------------------------ plano que se recria

        /// <summary>
        /// O plano do esboço, recriado quando o proxy morre. Apagar um recurso recusado desconectou o
        /// plano ao vivo (2026-11-09, RPC_E_DISCONNECTED) e todo furo seguinte caía junto. A face
        /// clicada também morre a cada recurso, então ela é REACHADA no corpo pela cota e pelo centro.
        /// </summary>
        private sealed class PlaneSource
        {
            private readonly dynamic _doc;
            private readonly SketchScope _scope;
            private readonly object _clickedFace;
            private readonly EjectorReliefPlan _plan;
            private readonly double[] _center, _axisVec;
            private readonly List<object> _made = new List<object>();
            private object _plane;
            private bool _isBase;

            public PlaneSource(dynamic doc, SketchScope scope, object face, EjectorReliefPlan plan, double[] center, double[] axisVec)
            {
                _doc = doc; _scope = scope; _clickedFace = face; _plan = plan; _center = center; _axisVec = axisVec;
            }

            public object Get()
            {
                if (_plane != null && Alive(_plane)) return _plane;
                bool again = _plane != null || _made.Count > 0;
                _plane = null;
                object face = !again && Alive(_clickedFace) ? _clickedFace : FindFace(_doc, _plan, _center);
                _plane = FacePlane(_doc, face, _plan, _center, _axisVec, out _isBase);
                if (_plane != null && !_isBase) { _scope.TrackTempPlane(_plane); _made.Add(_plane); }
                if (again) Log.Info($"  [plano] recriado ({(_plane == null ? "FALHOU" : _isBase ? "plano-base" : "paralelo à face")}).");
                return _plane;
            }

            public void Invalidate() { _plane = null; }

            /// <summary>Planos que viraram pais de furos ficam escondidos; os outros o escopo apaga.</summary>
            public void Finish(bool anyCreated)
            {
                if (!anyCreated) return;
                foreach (object p in _made) _scope.HideTempPlane(p);
            }

            private static bool Alive(object com)
            {
                if (com == null) return false;
                try { object _ = ((dynamic)com).Visible; return true; }
                catch
                {
                    try { object _ = ((dynamic)com).Type; return true; } catch { return false; }
                }
            }
        }

        /// <summary>A face plana do corpo na cota da face clicada que contém o centro dado (a original morreu).</summary>
        private static object FindFace(dynamic doc, EjectorReliefPlan plan, double[] centerMm)
        {
            int ax = plan.Axis, r1 = (ax + 1) % 3, r2 = (ax + 2) % 3;
            object best = null;
            double bestArea = 0;
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[igQueryAll];
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    object f = (object)faces.Item(i);
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mx[ax] - mn[ax] > 0.02 || Math.Abs(mn[ax] - plan.PlaneMm) > 0.02) continue;
                    if (centerMm[r1] < mn[r1] || centerMm[r1] > mx[r1] || centerMm[r2] < mn[r2] || centerMm[r2] > mx[r2]) continue;
                    double area = (mx[r1] - mn[r1]) * (mx[r2] - mn[r2]);
                    if (area > bestArea) { best = f; bestArea = area; }
                }
            }
            catch (Exception e) { Log.Warn("  [plano] face de trás não reachada: " + e.GetBaseException().Message); }
            return best;
        }

        /// <summary>
        /// Quantos furos têm, no CORPO, a face cilíndrica do alívio: caixa com o Ø do alívio, centrada
        /// no furo e encostando na face clicada.
        /// </summary>
        private static int BodyHasReliefs(dynamic doc, EjectorReliefPlan plan, List<EjectorHole> holes, out string measure)
        {
            int ax = plan.Axis, r1 = (ax + 1) % 3, r2 = (ax + 2) % 3;
            var found = new HashSet<EjectorHole>();
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[igQueryAll];
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)faces.Item(i), out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mn[ax] - plan.PlaneMm) > 0.05 && Math.Abs(mx[ax] - plan.PlaneMm) > 0.05) continue;
                    foreach (EjectorHole h in holes)
                    {
                        double d = h.ReliefDiameterMm;
                        if (Math.Abs((mx[r1] - mn[r1]) - d) > 0.05 || Math.Abs((mx[r2] - mn[r2]) - d) > 0.05) continue;
                        if (Math.Abs((mx[r1] + mn[r1]) / 2 - h.CenterMm[r1]) > 0.05 || Math.Abs((mx[r2] + mn[r2]) / 2 - h.CenterMm[r2]) > 0.05) continue;
                        if (Math.Abs(Math.Abs(mx[ax] - mn[ax]) - h.ReliefDepthMm) > 0.1) continue;
                        found.Add(h);
                    }
                }
            }
            catch (Exception e) { measure = "corpo ilegível (" + e.GetBaseException().Message + ")"; return 0; }
            measure = $"{found.Count}/{holes.Count} alívio(s) achado(s)" +
                      (found.Count < holes.Count ? " — faltam " + string.Join(", ", holes.Where(h => !found.Contains(h)).Select(h => h.Label)) : "");
            return found.Count;
        }

        // ------------------------------------------------------------------ vínculo com o furo

        private const int igCircleCenter = 0;   // KeypointIndexConstants (interop, reflexão 2026-09-21)

        /// <summary>
        /// Prende o centro do alívio ao furo existente: <c>Profile.IncludeEdge</c> da aresta da boca
        /// (achada pela DIFERENÇA das coleções 2D — o [out] vem vazio; receita do O'ring validada em
        /// 2026-09-21), vira construção e <c>Relations2d.AddConcentric(Hole2d, círculo)</c>; reserva:
        /// <c>AddKeypoint</c> centro com centro. Tudo que não for exatamente um círculo/arco incluído
        /// é apagado — elemento estranho no esboço mata o recurso.
        /// </summary>
        private static bool TieToHole(dynamic doc, object prof, object hole2d, EjectorReliefPlan plan, EjectorHole h)
        {
            object edge = MouthEdge(doc, plan, h);
            if (edge == null) { Log.Info($"  [vínculo] {h.Label}: aresta da boca não achada."); return false; }

            List<object> before = SketchElements(prof);
            try
            {
                object[] args = { edge, null };
                var mod = new ParameterModifier(2);
                mod[1] = true;
                prof.GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, prof, args, new[] { mod }, null, null);
            }
            catch (Exception e) { Log.Warn("  [vínculo] IncludeEdge falhou: " + e.GetBaseException().Message); return false; }
            List<object> added = SketchElements(prof).Where(a => !before.Any(b => SameCom(a, b))).ToList();
            if (added.Count != 1)
            {
                Log.Warn($"  [vínculo] a aresta entrou como {added.Count} elemento(s) — desfeito.");
                foreach (object a in added) { try { ((dynamic)a).Delete(); } catch { } }
                return false;
            }
            object refCircle = added[0];
            try { ((dynamic)prof).ToggleConstruction(refCircle); }
            catch (Exception e)
            {
                Log.Warn("  [vínculo] a aresta incluída não virou construção — desfeito: " + e.GetBaseException().Message);
                try { ((dynamic)refCircle).Delete(); } catch { }
                return false;
            }

            object rel = null;
            try { rel = prof.GetType().InvokeMember("Relations2d", BindingFlags.GetProperty, null, prof, null); } catch { }
            if (rel != null && (Relate(rel, "AddConcentric", hole2d, refCircle) ||
                                Relate(rel, "AddKeypoint", hole2d, igCircleCenter, refCircle, igCircleCenter)))
            {
                Log.Info($"  [vínculo] {h.Label}: centro do alívio preso à aresta da boca do furo.");
                return true;
            }
            Log.Warn($"  [vínculo] {h.Label}: nenhuma relação aceita — círculo incluído apagado.");
            try { ((dynamic)refCircle).Delete(); } catch { }
            return false;
        }

        /// <summary>
        /// A aresta circular da boca do furo, RELIDA agora (as arestas lidas antes morrem a cada furo
        /// criado): face do corpo com a caixa do furo encostando no plano, e nela a aresta achatada no
        /// plano com o Ø do furo. Círculo inteiro antes de arco (furo dividido em metades).
        /// </summary>
        private static object MouthEdge(dynamic doc, EjectorReliefPlan plan, EjectorHole h)
        {
            int ax = plan.Axis, r1 = (ax + 1) % 3, r2 = (ax + 2) % 3;
            double r = h.DiameterMm / 2.0;
            object best = null;
            double bestSpan = 0;
            try
            {
                dynamic faces = doc.Models.Item(1).Body.Faces[igQueryAll];
                int n = (int)faces.Count;
                for (int i = 1; i <= n; i++)
                {
                    object f = (object)faces.Item(i);
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mn[ax] - plan.PlaneMm) > 0.05 && Math.Abs(mx[ax] - plan.PlaneMm) > 0.05) continue;
                    if (mx[ax] - mn[ax] < 0.05) continue;                                    // face plana: não
                    if (mn[r1] < h.CenterMm[r1] - r - 0.05 || mx[r1] > h.CenterMm[r1] + r + 0.05 ||
                        mn[r2] < h.CenterMm[r2] - r - 0.05 || mx[r2] > h.CenterMm[r2] + r + 0.05) continue;
                    dynamic edges = ((dynamic)f).Edges;
                    int ne = (int)edges.Count;
                    for (int k = 1; k <= ne; k++)
                    {
                        object e = (object)edges.Item(k);
                        if (!FaceGeometry.TryGetRangeMm(e, out double[] a, out double[] b)) continue;
                        if (Math.Abs(a[ax] - plan.PlaneMm) > 0.05 || b[ax] - a[ax] > 0.02) continue;
                        double span = Math.Min(b[r1] - a[r1], b[r2] - a[r2]);
                        if (span >= 2 * r - 0.05) return e;                                // círculo inteiro
                        if (span > bestSpan) { best = e; bestSpan = span; }
                    }
                }
            }
            catch (Exception e) { Log.Warn("  [vínculo] faces/arestas ilegíveis: " + e.GetBaseException().Message); }
            return best;
        }

        private static List<object> SketchElements(object prof)
        {
            var list = new List<object>();
            foreach (string name in new[] { "Circles2d", "Arcs2d", "Lines2d", "Ellipses2d", "EllipticalArcs2d", "BSplineCurves2d" })
            {
                try
                {
                    dynamic c = prof.GetType().InvokeMember(name, BindingFlags.GetProperty, null, prof, null);
                    int n = (int)c.Count;
                    for (int i = 1; i <= n; i++) list.Add((object)c.Item(i));
                }
                catch { }
            }
            return list;
        }

        private static bool SameCom(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            IntPtr pa = IntPtr.Zero, pb = IntPtr.Zero;
            try
            {
                pa = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(a);
                pb = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(b);
                return pa == pb;
            }
            catch { return false; }
            finally
            {
                if (pa != IntPtr.Zero) System.Runtime.InteropServices.Marshal.Release(pa);
                if (pb != IntPtr.Zero) System.Runtime.InteropServices.Marshal.Release(pb);
            }
        }

        private static bool Relate(object relations, string method, params object[] args)
        {
            var full = new object[args.Length + 1];
            Array.Copy(args, full, args.Length);
            full[args.Length] = Type.Missing;   // guaranteed_ok
            try
            {
                relations.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, relations, full);
                return true;
            }
            catch (Exception e) { Log.Warn($"  [vínculo] Relations2d.{method} falhou: " + e.GetBaseException().Message); return false; }
        }

        /// <summary>
        /// Plano na face: paralelo à FACE a 0 mm (receita da boca prolongada da Refrigeração);
        /// reserva: paralelo ao plano-base perpendicular ao eixo, na cota da face. Conferido pela
        /// geometria (<c>GetRootPoint</c>/<c>GetNormal</c>), sem esboço de sondagem — esboço apagado
        /// zera o Ctrl+Z da peça (sonda UndoProbe, 2026-10-29).
        /// </summary>
        private static object FacePlane(dynamic doc, object comFace, EjectorReliefPlan plan, double[] onPlaneMm, double[] axisVec, out bool isBase)
        {
            isBase = false;
            Func<object, bool> fits = p =>
            {
                ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane(p);
                return f == null || (Math.Abs(f.SignedDistance(onPlaneMm)) < 0.01 && Math.Abs(ProfilePlaneFrame.Dot(f.Normal, axisVec)) > 0.999);
            };

            try
            {
                object p = (object)doc.RefPlanes.AddParallelByDistance(comFace, 0.0, igRight, Type.Missing, Type.Missing, Type.Missing);
                if (p != null && fits(p)) return p;
                Hide(p);
                Log.Info("  [plano] paralelo à face fora do lugar — tentando pelo plano-base.");
            }
            catch (Exception e) { Log.Warn("  [plano] AddParallelByDistance(face, 0): " + e.GetBaseException().Message); }

            for (int i = 1; i <= 3; i++)
            {
                object basePlane;
                try { basePlane = (object)doc.RefPlanes.Item(i); } catch { continue; }
                ProfilePlaneFrame bf = ProfilePlaneFrame.OfRefPlane(basePlane);
                if (bf == null || Math.Abs(ProfilePlaneFrame.Dot(bf.Normal, axisVec)) < 0.9999) continue;
                double dist = Math.Abs(bf.SignedDistance(onPlaneMm));
                if (dist < 0.001) { isBase = true; return basePlane; }
                foreach (int side in new[] { igRight, igLeft })
                {
                    object p = null;
                    try { p = (object)doc.RefPlanes.AddParallelByDistance(basePlane, Units.MmToM(dist), side, Type.Missing, Type.Missing, Type.Missing); }
                    catch (Exception e) { Log.Warn($"  [plano] AddParallelByDistance(Item({i}), {dist:0.###} mm, lado {side}): " + e.GetBaseException().Message); continue; }
                    if (p != null && fits(p)) return p;
                    Hide(p);
                }
            }
            return null;
        }

        /// <summary>Furo liso, fundo reto: <c>HoleDataCollection.Add</c> (21 parâmetros), Ø em metros.</summary>
        private static object HoleData(dynamic doc, double diameterMm)
        {
            try
            {
                object hdc = (object)doc.HoleDataCollection;
                var a = new object[21];
                for (int i = 0; i < a.Length; i++) a[i] = Type.Missing;
                a[0] = igRegularHole;
                a[1] = Units.MmToM(diameterMm);
                a[20] = true;                       // IgnoreSavedDefaultValues
                return hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, a);
            }
            catch (Exception e) { Log.Warn("  HoleDataCollection.Add: " + e.GetBaseException().Message); return null; }
        }

        /// <summary>
        /// O recurso fez TODOS os alívios do lado certo? Cada ponto de dentro (1 mm abaixo da boca)
        /// tem de estar na caixa de alguma face do recurso — caixa por face, não a união, para um
        /// furo do grupo que falhou não passar escondido entre os outros.
        /// </summary>
        private static bool Covers(object feat, List<double[]> pointsMm, double tolMm, out string measure)
        {
            measure = "sem faces legíveis";
            try
            {
                object faces = feat.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, feat, new object[] { igQueryAll });
                dynamic coll = faces;
                int n = 0; try { n = (int)coll.Count; } catch { }
                if (n == 0) { measure = "furo sem faces"; return false; }
                var boxes = new List<double[][]>();
                for (int i = 1; i <= n; i++)
                    if (FaceGeometry.TryGetRangeMm((object)coll.Item(i), out double[] a, out double[] b)) boxes.Add(new[] { a, b });
                if (boxes.Count == 0) { measure = $"{n} face(s), nenhuma caixa legível — aceito pelo Status"; return true; }
                int covered = pointsMm.Count(p => boxes.Any(bx =>
                {
                    for (int k = 0; k < 3; k++) if (p[k] < bx[0][k] - tolMm || p[k] > bx[1][k] + tolMm) return false;
                    return true;
                }));
                measure = $"{n} face(s), {covered}/{pointsMm.Count} furo(s) com o ponto de dentro COBERTO";
                return covered == pointsMm.Count;
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

        private static void Hide(object plane) { if (plane == null) return; try { ((dynamic)plane).Visible = false; } catch { } }
    }
}
