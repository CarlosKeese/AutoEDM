using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>Resultado de <see cref="VisibleFacePicker.PickNearest"/>.</summary>
    public sealed class VisiblePick
    {
        /// <summary>A face do 1º impacto, com a ocorrência dona. Null = o raio não acertou nada.</summary>
        public PickedFace Face { get; set; }
        /// <summary>Referência de montagem (ocorrência + face) para o realce; null se a SE recusar.</summary>
        public object HighlightItem { get; set; }
        public double DistanceMm { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>
    /// "Qual face está À VISTA sob o clique?" — decidido pelo add-in, não pela localização da SE.
    ///
    /// Motivo (Carlos, 2026-09-24): dentro de um comando nosso, QuickPick e Simples devolvem
    /// uma face qualquer entre as que o clique atravessa — com uma peça atrás da peça-alvo,
    /// vinha a face MAIOR de trás, e para pegar face pequena era preciso isolar a peça. O
    /// SmartLocate, que respeitaria a vista, não entrega geometria ao comando.
    ///
    /// Como: o raio de visão (câmera da janela + ponto do clique, <see cref="RayMath.TryViewRay"/>)
    /// é levado para o espaço de cada ocorrência de PEÇA visível pela inversa da pose
    /// (<see cref="OccurrenceTransform"/>); a caixa do corpo filtra; <c>Body.FacesByRay</c>
    /// reduz as candidatas; a malha de cada candidata (<c>Face.GetFacetData</c>, a mesma leitura da
    /// coluna Secção) dá a distância exata do impacto; ganha a menor. Rotação ortonormal não
    /// muda distância, então as distâncias de ocorrências diferentes se comparam direto.
    ///
    /// Só leitura, nunca lança. Limites da V1: só ocorrências de PEÇA no 1º nível (submontagem
    /// é contada e pulada — o chamador cai na face que a SE localizou).
    /// </summary>
    public static class VisibleFacePicker
    {
        /// <summary>Corda da malha (m). 0,05 mm: face pequena ainda tem triângulo, face grande não explode.</summary>
        private const double FacetToleranceM = 0.00005;

        /// <summary>
        /// Câmera da janela do clique: <c>Window.View.GetCamera</c> — 11 args by-ref (olho xyz, alvo
        /// xyz, cima xyz, perspectiva, escala/ângulo), assinatura do interop 219.
        /// </summary>
        public static bool TryReadCamera(object window, out double[] eye, out double[] target, out bool perspective)
        {
            eye = null; target = null; perspective = false;
            if (window == null) { Log.Warn("[vista] clique sem janela — sem câmera."); return false; }
            try
            {
                object view = window.GetType().InvokeMember("View", BindingFlags.GetProperty, null, window, null);
                object[] args = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, false, 0.0 };
                var mod = new ParameterModifier(11);
                for (int i = 0; i < 11; i++) mod[i] = true;
                view.GetType().InvokeMember("GetCamera", BindingFlags.InvokeMethod, null, view, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                eye = new[] { D(args[0]), D(args[1]), D(args[2]) };
                target = new[] { D(args[3]), D(args[4]), D(args[5]) };
                perspective = Convert.ToBoolean(args[9]);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("[vista] câmera ilegível (Window.View.GetCamera): " + e.GetBaseException().Message);
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint p);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativePoint { public int X, Y; }

        /// <summary>
        /// Posição do cursor na TELA → ponto de modelo (metros) sob ele, pela própria vista:
        /// <c>ScreenToClient(Window.DrawHwnd)</c> → <c>View.TransformDCToModel(x, y, [out] X, Y, Z)</c>.
        ///
        /// É ESTE o ponto do raio, não o (x, y, z) do <c>MouseClick</c>: sondado ao vivo em
        /// 2026-09-24 (MD-15335), o raio pelo ponto do evento acertava a placa de trás, enquanto o
        /// raio pelo ponto convertido do pixel acertou a face que o Carlos tinha selecionado. A
        /// ida-e-volta <c>TransformModelToDC</c>/<c>TransformDCToModel</c> sobre um ponto da face
        /// fechou (DC em pixels da janela de DESENHO, não da moldura <c>hWnd</c>).
        /// </summary>
        public static bool TryScreenToModel(object window, int screenX, int screenY, out double[] pointM)
        {
            pointM = null;
            if (window == null) return false;
            try
            {
                object draw = window.GetType().InvokeMember("DrawHwnd", BindingFlags.GetProperty, null, window, null);
                var p = new NativePoint { X = screenX, Y = screenY };
                if (!ScreenToClient(new IntPtr(Convert.ToInt64(draw)), ref p))
                {
                    Log.Warn("[vista] ScreenToClient recusado para a janela de desenho.");
                    return false;
                }

                object view = window.GetType().InvokeMember("View", BindingFlags.GetProperty, null, window, null);
                object[] args = { p.X, p.Y, 0.0, 0.0, 0.0 };
                var mod = new ParameterModifier(5);
                mod[2] = true; mod[3] = true; mod[4] = true;
                view.GetType().InvokeMember("TransformDCToModel", BindingFlags.InvokeMethod, null, view, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                pointM = new[] { D(args[2]), D(args[3]), D(args[4]) };
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "[vista] cursor na janela de desenho ({0}, {1}) px → modelo ({2:0.###}, {3:0.###}, {4:0.###}) mm.",
                    p.X, p.Y, pointM[0] * 1000, pointM[1] * 1000, pointM[2] * 1000));
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("[vista] pixel → modelo falhou (DrawHwnd/TransformDCToModel): " + e.GetBaseException().Message);
                return false;
            }
        }

        /// <summary>
        /// A face do 1º impacto do raio (metros, espaço da MONTAGEM) entre as ocorrências de peça
        /// visíveis. Resultado com <c>Face == null</c> quando nada é atingido.
        /// <paramref name="scene"/> é a leitura das peças guardada entre cliques
        /// (<see cref="VisibleScene"/>); null = lê tudo agora (mais lento, sempre fresco).
        /// </summary>
        public static VisiblePick PickNearest(dynamic asmDoc, double[] originM, double[] dirM, VisibleScene scene = null)
        {
            var sw = Stopwatch.StartNew();
            var res = new VisiblePick();
            if (scene == null) scene = VisibleScene.Build((object)asmDoc);
            int boxHits = 0, meshed = 0, byRayFail = 0;
            double bestT = double.NaN;
            object bestFace = null; OccurrenceInfo bestOcc = null;
            // Corpos cuja caixa o raio cruza mas o FacesByRay respondeu vazio — 2ª chance por varredura.
            var retry = new List<Tuple<OccurrenceInfo, object, double[], double[]>>();

            foreach (VisibleScene.Part part in scene.Parts)
            {
                OccurrenceTransform pose = part.Pose;
                pose.InverseTransformPointM(originM[0], originM[1], originM[2], out double ox, out double oy, out double oz);
                pose.InverseRotate(dirM[0], dirM[1], dirM[2], out double dx, out double dy, out double dz);
                double[] o = { ox, oy, oz }, d = { dx, dy, dz };

                foreach (VisibleScene.BodyBox bb in part.Bodies)
                {
                    if (!RayMath.HitsBox(o, d, bb.MinM, bb.MaxM, pad: 0.0001)) continue;
                    boxHits++;

                    List<object> candidates = FacesByRay(bb.Body, o, d, out bool rayOk);
                    if (!rayOk) { byRayFail++; candidates = FacesCrossedByBox(bb.Body, o, d); }
                    else if (candidates.Count == 0) { retry.Add(Tuple.Create(part.Occurrence, bb.Body, o, d)); continue; }

                    Consider(candidates, o, d, part.Occurrence, ref bestT, ref bestFace, ref bestOcc, ref meshed);
                }
            }

            // FacesByRay sem nenhum impacto em lugar nenhum: talvez ele não ache o que devia (nunca
            // usado ao vivo antes). Varre as faces pela caixa só dos corpos que o raio cruzou.
            if (bestFace == null && retry.Count > 0)
            {
                foreach (var r in retry)
                    Consider(FacesCrossedByBox(r.Item2, r.Item3, r.Item4), r.Item3, r.Item4, r.Item1,
                             ref bestT, ref bestFace, ref bestOcc, ref meshed);
                if (bestFace != null) Log.Warn("[vista] FacesByRay respondeu VAZIO onde a varredura achou impacto — confira.");
            }

            string counts = scene.Describe() + $"; {boxHits} corpo(s) no raio, {meshed} face(s) malhada(s)" +
                            (byRayFail > 0 ? $", FacesByRay falhou em {byRayFail}" : "");
            if (bestFace == null)
            {
                res.Summary = $"[vista] nenhum impacto — {counts} ({sw.ElapsedMilliseconds} ms).";
                Log.Info(res.Summary);
                return res;
            }

            object comBest = bestOcc.ComOccurrence;
            string name = Get(comBest, "Name") as string ?? bestOcc.Name;
            res.Face = new PickedFace(bestFace, comBest, name);
            res.DistanceMm = Units.MToMm(bestT);
            res.HighlightItem = TryCreateReference(asmDoc, comBest, bestFace);
            res.Summary = $"[vista] face à vista: '{name}', {res.DistanceMm:0.000} mm ao longo do raio — {counts} ({sw.ElapsedMilliseconds} ms).";
            Log.Info(res.Summary);
            return res;
        }

        private static void Consider(List<object> faces, double[] o, double[] d, OccurrenceInfo occ,
            ref double bestT, ref object bestFace, ref OccurrenceInfo bestOcc, ref int meshed)
        {
            foreach (object f in faces)
            {
                if (!SectionAreaCalculator.TryGetFacetPointsM(f, FacetToleranceM, out double[] pts, out _)) continue;
                meshed++;
                double t = RayMath.NearestHit(o, d, pts);
                if (!double.IsNaN(t) && (double.IsNaN(bestT) || t < bestT)) { bestT = t; bestFace = f; bestOcc = occ; }
            }
        }

        /// <summary><c>Body.FacesByRay[x,y,z, dx,dy,dz]</c> — propriedade COM parametrizada (dump:
        /// SolidEdgeGeometry.Body). <paramref name="ok"/> = a chamada respondeu (mesmo vazia).</summary>
        private static List<object> FacesByRay(object body, double[] o, double[] d, out bool ok)
        {
            var list = new List<object>();
            ok = false;
            try
            {
                object faces = body.GetType().InvokeMember("FacesByRay", BindingFlags.GetProperty, null, body,
                    new object[] { o[0], o[1], o[2], d[0], d[1], d[2] });
                ok = true;
                if (faces == null) return list;
                dynamic coll = faces;
                int n = 0; try { n = (int)coll.Count; } catch { }
                for (int i = 1; i <= n; i++) { try { list.Add(coll.Item(i)); } catch { } }
            }
            catch (Exception e)
            {
                Log.Warn("[vista] Body.FacesByRay falhou: " + e.GetBaseException().Message);
            }
            return list;
        }

        /// <summary>Plano B: faces do corpo cuja caixa o raio cruza.</summary>
        private static List<object> FacesCrossedByBox(object body, double[] o, double[] d)
        {
            var hits = new List<object>();
            try
            {
                dynamic all = ((dynamic)body).Faces[1]; // 1 = igQueryAll
                int n = 0; try { n = (int)all.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f; try { f = all.Item(i); } catch { continue; }
                    if (!FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) { hits.Add(f); continue; } // sem caixa: deixa a malha decidir
                    if (RayMath.HitsBox(o, d, ToM(mn), ToM(mx), pad: 0.0001)) hits.Add(f);
                }
            }
            catch (Exception e) { Log.Warn("[vista] faces do corpo ilegíveis: " + e.GetBaseException().Message); }
            return hits;
        }

        /// <summary>
        /// <c>AssemblyDocument.CreateReference(Occurrence, Face)</c> — a face "vista pela
        /// ocorrência", que é o que o realce da montagem precisa (a face crua mora na peça).
        /// </summary>
        private static object TryCreateReference(object asmDoc, object occurrence, object face)
        {
            try
            {
                return asmDoc.GetType().InvokeMember("CreateReference", BindingFlags.InvokeMethod, null, asmDoc,
                    new[] { occurrence, face });
            }
            catch (Exception e)
            {
                Log.Warn("[vista] CreateReference recusado (sem realce desta face): " + e.GetBaseException().Message);
                return null;
            }
        }

        private static object Get(object o, string name)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }

        private static double D(object v) => Convert.ToDouble(v, CultureInfo.InvariantCulture);

        private static double[] ToM(double[] mm) => new[] { Units.MmToM(mm[0]), Units.MmToM(mm[1]), Units.MmToM(mm[2]) };
    }

    /// <summary>
    /// A leitura das peças que o raio de visão precisa — ocorrência visível de peça, pose e
    /// caixa de cada corpo — guardada entre cliques. Refazer isso a cada clique custava ~0,35 s
    /// numa montagem de 292 ocorrências (MD-15335, 2026-09-24): quase tudo em GetMatrix,
    /// Visible e Body.GetRange de peça que o raio nem cruza.
    ///
    /// Retrato do momento da leitura: mover, esconder ou mostrar peça depois NÃO aparece até
    /// quem guarda a cena montar outra (a janela tem "Atualizar peças" e refaz sozinha depois
    /// de criar um eletrodo).
    /// </summary>
    public sealed class VisibleScene
    {
        public sealed class BodyBox
        {
            public object Body;
            public double[] MinM, MaxM;
        }

        public sealed class Part
        {
            public OccurrenceInfo Occurrence;
            public OccurrenceTransform Pose;
            public List<BodyBox> Bodies = new List<BodyBox>();
        }

        public List<Part> Parts { get; } = new List<Part>();
        public int Occurrences { get; private set; }
        public int Hidden { get; private set; }
        public int NotParts { get; private set; }
        public int NoPose { get; private set; }
        public int NoBox { get; private set; }
        public long BuildMs { get; private set; }

        public static VisibleScene Build(object asmDoc)
        {
            var sw = Stopwatch.StartNew();
            var scene = new VisibleScene();
            var ctx = new AssemblyContext(asmDoc);
            foreach (OccurrenceInfo occ in ctx.GetOccurrences())
            {
                scene.Occurrences++;
                if (Get(occ.ComOccurrence, "Visible") is bool vis && !vis) { scene.Hidden++; continue; }

                object doc = occ.OccurrenceDocument;
                int type = -1; try { type = Convert.ToInt32(Get(doc, "Type")); } catch { }
                if (type != 1) { scene.NotParts++; continue; }                  // 1 = igPartDocument

                OccurrenceTransform pose = AssemblyContext.TryGetPose(occ);
                if (pose == null) { scene.NoPose++; continue; }

                var part = new Part { Occurrence = occ, Pose = pose };
                foreach (object body in BodiesOf(doc))
                {
                    if (!FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)) { scene.NoBox++; continue; }
                    part.Bodies.Add(new BodyBox
                    {
                        Body = body,
                        MinM = new[] { Units.MmToM(mn[0]), Units.MmToM(mn[1]), Units.MmToM(mn[2]) },
                        MaxM = new[] { Units.MmToM(mx[0]), Units.MmToM(mx[1]), Units.MmToM(mx[2]) },
                    });
                }
                if (part.Bodies.Count > 0) scene.Parts.Add(part);
            }
            scene.BuildMs = sw.ElapsedMilliseconds;
            Log.Info($"[vista] cena lida: {scene.Describe()} ({scene.BuildMs} ms).");
            return scene;
        }

        public string Describe() =>
            $"{Occurrences} ocorrência(s): {Hidden} oculta(s), {NotParts} submontagem/outra(s) pulada(s), " +
            $"{NoPose} sem pose, {NoBox} corpo(s) sem caixa, {Parts.Count} peça(s) na cena";

        /// <summary>Corpos de projeto da peça (<c>Models.Item(i).Body</c>).</summary>
        private static IEnumerable<object> BodiesOf(object partDoc)
        {
            var list = new List<object>();
            try
            {
                dynamic models = Get(partDoc, "Models");
                int n = 0; try { n = (int)models.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    try { object b = models.Item(i).Body; if (b != null) list.Add(b); } catch { }
                }
            }
            catch { }
            return list;
        }

        private static object Get(object o, string name)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }
    }
}
