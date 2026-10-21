using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Raio do FUNDO e quebra de canto da ENTRADA do canal, cortados depois do recorte — antes
    /// ficavam só no relatório para o ferramenteiro aplicar à mão.
    ///
    /// As arestas saem das faces do próprio recorte: as circulares no Ø do fundo (canal radial)
    /// ou na profundidade do canal (canal de face) recebem o arredondamento; as do Ø da
    /// superfície / do plano da face recebem o chanfro.
    ///
    /// <c>Model.Rounds.Add(NumberOfEdgeSets, EdgeSetArray, RadiusArray, …)</c> e
    /// <c>Model.Chamfers.AddEqualSetback(NumberOfEdgeSets, EdgeSetArray, SetbackDistance)</c>
    /// vêm do dump da typelib (SE 223) e AINDA NÃO FORAM EXERCITADOS AO VIVO neste projeto —
    /// por isso cada passo é independente, confere o <c>Status</c> e nunca derruba o canal:
    /// um arredondamento que falha vira uma linha no log e no aviso, e o canal fica.
    /// </summary>
    public static class ORingGrooveBlends
    {
        private const int IgQueryAll = 1;
        private const double Tol = 0.02;   // mm — classificação das arestas

        public sealed class Result
        {
            public object RoundFeature;
            public object ChamferFeature;
            public List<string> Problems = new List<string>();
        }

        public static Result Apply(object model, object cutFeature, ORingTarget target, ORingGrooveSpec spec, double grooveOffsetMm)
        {
            var result = new Result();
            if (model == null || cutFeature == null || target == null || spec == null) return result;

            // As arestas são lidas DE NOVO antes de cada recurso: o arredondamento regenera o
            // corpo e invalida as referências lidas antes dele — o chanfro recebia arestas
            // mortas e a SE respondia E_FAIL nas quatro tentativas (log de 2026-10-21, 15:20).
            List<object> bottom, entry;
            if (spec.BottomRadius > 0.001)
            {
                Edges(cutFeature, target, spec, grooveOffsetMm, out bottom, out entry);
                if (bottom.Count == 0) result.Problems.Add("raio do fundo: arestas do fundo não identificadas.");
                else result.RoundFeature = Round(model, bottom, spec.BottomRadius, result.Problems);
            }
            if (spec.EdgeBreak > 0.001)
            {
                Edges(cutFeature, target, spec, grooveOffsetMm, out bottom, out entry);
                if (entry.Count == 0) result.Problems.Add("quebra de canto: arestas da entrada não identificadas.");
                else result.ChamferFeature = Chamfer(model, entry, spec.EdgeBreak, result.Problems);
            }
            return result;
        }

        private static void Edges(object cutFeature, ORingTarget target, ORingGrooveSpec spec, double grooveOffsetMm,
            out List<object> bottom, out List<object> entry)
        {
            var circles = CircularEdges(cutFeature, target.AxisIndex);
            Classify(circles, target, spec, grooveOffsetMm, out bottom, out entry);
            Log.Info($"  [acabamento] {circles.Count} aresta(s) circular(es) nas faces do canal — fundo: {bottom.Count}, entrada: {entry.Count}.");
        }

        private sealed class Circle
        {
            public object Edge;
            public double AxialMm;    // posição do centro ao longo do eixo
            public double RadiusMm;
        }

        private static List<Circle> CircularEdges(object feature, int axis)
        {
            var list = new List<Circle>();
            object faces = Get(feature, "Faces", IgQueryAll);
            foreach (object face in Items(faces))
            {
                foreach (object edge in Items(Get(face, "Edges")))
                {
                    object geo = Get(edge, "Geometry");
                    if (geo == null) continue;
                    var args = new object[] { new double[3], new double[3], 0.0 };
                    if (!Call(geo, "GetCircleData", args, 0, 1, 2)) continue;
                    var c = args[0] as double[] ?? ToDoubles(args[0]);
                    double r = Convert.ToDouble(args[2]) * 1000.0;
                    if (c == null || r <= 0) continue;
                    double axial = c[axis] * 1000.0;
                    // A mesma aresta aparece nas duas faces que ela separa.
                    if (list.Any(o => Math.Abs(o.AxialMm - axial) < 1e-4 && Math.Abs(o.RadiusMm - r) < 1e-4)) continue;
                    list.Add(new Circle { Edge = edge, AxialMm = axial, RadiusMm = r });
                }
            }
            return list;
        }

        private static void Classify(List<Circle> circles, ORingTarget t, ORingGrooveSpec spec, double offset,
            out List<object> bottom, out List<object> entry)
        {
            bottom = new List<object>(); entry = new List<object>();
            if (spec.Kind == GrooveKind.AxialFace)
            {
                double plane = t.EdgeAxialMm;
                double floor = t.EdgeAxialMm + t.MaterialDirection * spec.Depth;
                foreach (var c in circles)
                {
                    bool onRim = Math.Abs(c.RadiusMm - spec.GrooveInnerDiameter / 2) < Tol || Math.Abs(c.RadiusMm - spec.GrooveOuterDiameter / 2) < Tol;
                    if (!onRim) continue;
                    if (Math.Abs(c.AxialMm - floor) < Tol) bottom.Add(c.Edge);
                    else if (Math.Abs(c.AxialMm - plane) < Tol) entry.Add(c.Edge);
                }
                return;
            }

            // Radial: as duas paredes ficam a ± meia largura do centro do canal.
            double center = t.EdgeAxialMm + t.MaterialDirection * offset;
            double half = spec.Width / 2;
            foreach (var c in circles)
            {
                bool atWall = Math.Abs(Math.Abs(c.AxialMm - center) - half) < Tol;
                if (!atWall) continue;
                if (Math.Abs(c.RadiusMm - spec.GrooveBottomDiameter / 2) < Tol) bottom.Add(c.Edge);
                else if (Math.Abs(c.RadiusMm - spec.SealingDiameter / 2) < Tol) entry.Add(c.Edge);
            }
        }

        private static object Round(object model, List<object> edges, double radiusMm, List<string> problems)
        {
            object rounds = Get(model, "Rounds");
            if (rounds == null) { problems.Add("raio do fundo: Model.Rounds indisponível."); return null; }
            double r = radiusMm / 1000.0;

            // 1º jeito: um conjunto por aresta, todos com o mesmo raio. 2º: um conjunto só.
            var attempts = new List<object[]>
            {
                new object[] { edges.Count, edges.ToArray(), Enumerable.Repeat(r, edges.Count).ToArray() },
                new object[] { 1, edges.ToArray(), new[] { r } }
            };
            var typed = Typed(edges);
            if (typed != null)
            {
                attempts.Add(new object[] { edges.Count, typed, Enumerable.Repeat(r, edges.Count).ToArray() });
                attempts.Add(new object[] { 1, typed, new[] { r } });
            }
            return TryFeature(rounds, "Add", attempts, $"raio do fundo R{radiusMm:0.00}", problems);
        }

        private static object Chamfer(object model, List<object> edges, double setbackMm, List<string> problems)
        {
            object chamfers = Get(model, "Chamfers");
            if (chamfers == null) { problems.Add("quebra de canto: Model.Chamfers indisponível."); return null; }
            double s = setbackMm / 1000.0;
            var attempts = new List<object[]>
            {
                new object[] { edges.Count, edges.ToArray(), s },
                new object[] { 1, edges.ToArray(), s }
            };
            var typed = Typed(edges);
            if (typed != null)
            {
                attempts.Add(new object[] { edges.Count, typed, s });
                attempts.Add(new object[] { 1, typed, s });
            }
            return TryFeature(chamfers, "AddEqualSetback", attempts, $"quebra de canto {setbackMm:0.00}×45°", problems);
        }

        private static object TryFeature(object collection, string method, List<object[]> attempts, string what, List<string> problems)
        {
            string last = null;
            for (int i = 0; i < attempts.Count; i++)
            {
                object feature;
                try
                {
                    feature = collection.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, collection,
                        attempts[i], CultureInfo.InvariantCulture);
                }
                catch (Exception e) { last = e.GetBaseException().Message; Log.Info($"  [acabamento] {what}: tentativa {i + 1} recusada — {last}"); continue; }

                if (feature == null) { last = "sem feature devolvida"; continue; }
                if (Failed(feature))
                {
                    // Feature com falha fica na árvore se não for apagada; apagar às vezes
                    // desconecta o documento, então é a última coisa que se faz aqui.
                    Log.Warn($"  [acabamento] {what}: feature nasceu com FALHA — apagando.");
                    try { feature.GetType().InvokeMember("Delete", BindingFlags.InvokeMethod, null, feature, null); }
                    catch (Exception e) { Log.Warn($"  [acabamento] não apagou a feature falhada ({e.GetBaseException().Message}) — apague à mão."); }
                    problems.Add($"{what}: a feature nasceu com falha.");
                    return null;
                }
                Log.Info($"  [acabamento] {what} ✓ (tentativa {i + 1}, {attempts[i][0]} conjunto(s)).");
                return feature;
            }
            problems.Add($"{what}: recusado pela SE ({last}).");
            return null;
        }

        /// <summary>As mesmas arestas num array TIPADO — SAFEARRAY(IDispatch) em vez de VARIANT,
        /// o que outras chamadas de aresta/face exigiram neste projeto. null se não converter.</summary>
        private static SolidEdgeGeometry.Edge[] Typed(List<object> edges)
        {
            try { return edges.Select(e => (SolidEdgeGeometry.Edge)e).ToArray(); }
            catch { return null; }
        }

        private static bool Failed(object feature)
        {
            try
            {
                object st = feature.GetType().InvokeMember("Status", BindingFlags.GetProperty, null, feature, null);
                return unchecked((uint)Convert.ToInt64(st)) == 1216476311u;   // igFeatureFailed
            }
            catch { return false; }
        }

        private static IEnumerable<object> Items(object collection)
        {
            if (collection == null) yield break;
            int n = 0;
            try { n = Convert.ToInt32(Get(collection, "Count")); } catch { }
            for (int i = 1; i <= n; i++)
            {
                object item = null;
                try { item = collection.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { i }); }
                catch { }
                if (item != null) yield return item;
            }
        }

        private static object Get(object o, string prop, params object[] index)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, index.Length == 0 ? null : index); }
            catch { return null; }
        }

        private static bool Call(object obj, string method, object[] args, params int[] outIdx)
        {
            try
            {
                var pm = new ParameterModifier(args.Length);
                foreach (int i in outIdx) pm[i] = true;
                obj.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, obj, args,
                    new[] { pm }, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch { return false; }
        }

        private static double[] ToDoubles(object o)
        {
            var a = o as Array;
            if (a == null || a.Length < 3) return null;
            var d = new double[a.Length];
            for (int i = 0; i < a.Length; i++) d[i] = Convert.ToDouble(a.GetValue(i));
            return d;
        }
    }
}
