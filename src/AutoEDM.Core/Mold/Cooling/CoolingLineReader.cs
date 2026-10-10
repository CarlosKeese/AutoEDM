using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// Linhas do traçado de refrigeração, lidas da PEÇA. Só leitura.
    ///
    /// De onde vêm (dump ao vivo da typelib, SE 2023 — o interop 219 nem tem os tipos de esboço 3D):
    /// <c>PartDocument.Constructions.Sketch3DFeatures</c> → <c>Sketch3DFeature</c>, que não expõe as
    /// linhas como tal, mas expõe a TOPOLOGIA: <c>Edges[igQueryAll=1]</c>. Cada aresta reta é uma
    /// linha, e as pontas saem de <c>Edge.GetEndPoints</c> — caminho já provado no projeto
    /// (<see cref="EdgeGeometry"/>). A aresta COM fica guardada: é nela que o plano do furo nasce
    /// (<c>RefPlanes.AddNormalToCurve</c>), o que prende o furo à linha.
    /// </summary>
    public static class CoolingLineReader
    {
        private const int igQueryAll = 1;

        /// <summary>
        /// Linhas a partir do que o usuário clicou: aresta/linha do esboço, ou o próprio recurso de
        /// esboço 3D (vale por todas as linhas dele). Curva que não é reta fica de fora, com aviso.
        /// </summary>
        public static List<CoolingLine> FromItems(IEnumerable<object> items, List<string> warnings)
        {
            var edges = new List<object>();
            foreach (object it in items ?? Enumerable.Empty<object>())
            {
                if (it == null) continue;
                object inner = Get(it, "Object");                         // embrulho de seleção
                object o = inner ?? it;
                // Recurso (o esboço 3D inteiro) tem nome na árvore; aresta não tem.
                if (Get(o, "EdgebarName") != null) edges.AddRange(EdgesOfFeature(o));
                else edges.Add(o);
            }
            return ToLines(edges, warnings);
        }

        /// <summary>Todas as linhas de todos os esboços 3D da peça.</summary>
        public static List<CoolingLine> AllSketch3D(dynamic partDoc, List<string> warnings, out int sketches)
        {
            sketches = 0;
            var edges = new List<object>();
            foreach (object f in Sketch3DFeatures(partDoc))
            {
                sketches++;
                edges.AddRange(EdgesOfFeature(f));
            }
            if (sketches == 0) warnings?.Add("A peça não tem esboço 3D (Constructions.Sketch3DFeatures vazio).");
            return ToLines(edges, warnings);
        }

        /// <summary>
        /// A aresta VIVA da linha com estas pontas, relida agora. As arestas lidas antes morrem a cada
        /// recurso criado (o modelo recalcula) — usar uma velha dá E_FAIL em <c>AddNormalToCurve</c>.
        /// </summary>
        public static object FindEdge(dynamic partDoc, double[] startMm, double[] endMm)
        {
            foreach (object f in Sketch3DFeatures(partDoc))
                foreach (object e in EdgesOfFeature(f))
                {
                    if (!EdgeGeometry.TryGetEndPointsMm(e, out double[] a, out double[] b, out _)) continue;
                    if ((Vec.Dist(a, startMm) < 1e-4 && Vec.Dist(b, endMm) < 1e-4) || (Vec.Dist(a, endMm) < 1e-4 && Vec.Dist(b, startMm) < 1e-4))
                        return e;
                }
            return null;
        }

        public static List<object> Sketch3DFeatures(dynamic partDoc)
        {
            var list = new List<object>();
            try
            {
                dynamic coll = partDoc.Constructions.Sketch3DFeatures;
                int n = 0; try { n = (int)coll.Count; } catch { }
                for (int i = 1; i <= n; i++) { try { list.Add((object)coll.Item(i)); } catch { } }
                Log.Info($"Refrigeração: {n} esboço(s) 3D na peça.");
            }
            catch (Exception e) { Log.Warn("Refrigeração: Constructions.Sketch3DFeatures inacessível — " + e.GetBaseException().Message); }
            return list;
        }

        private static List<object> EdgesOfFeature(object feature)
        {
            var list = new List<object>();
            try
            {
                object edges = feature.GetType().InvokeMember("Edges", BindingFlags.GetProperty, null, feature, new object[] { igQueryAll });
                dynamic coll = edges;
                int n = 0; try { n = (int)coll.Count; } catch { }
                for (int i = 1; i <= n; i++) { try { list.Add((object)coll.Item(i)); } catch { } }
            }
            catch (Exception e) { Log.Warn("Refrigeração: Edges do esboço 3D ilegíveis — " + e.GetBaseException().Message); }
            return list;
        }

        private static List<CoolingLine> ToLines(List<object> edges, List<string> warnings)
        {
            var lines = new List<CoolingLine>();
            int notStraight = 0, unreadable = 0;
            foreach (object e in edges)
            {
                if (!EdgeGeometry.TryGetEndPointsMm(e, out double[] a, out double[] b, out _)) { unreadable++; continue; }
                if (!IsStraight(e, a, b)) { notStraight++; continue; }
                // Mesma aresta clicada duas vezes (ou recurso + aresta dele): fica uma.
                if (lines.Any(l => Same(l, a, b))) continue;
                lines.Add(new CoolingLine { Index = lines.Count + 1, StartMm = a, EndMm = b, Edge = e });
            }
            if (notStraight > 0) warnings?.Add($"{notStraight} curva(s) que não são retas ficaram de fora (canal é furo reto).");
            if (unreadable > 0) warnings?.Add($"{unreadable} item(ns) sem pontas legíveis ficaram de fora (o log mostra o tipo).");
            Log.Info($"Refrigeração: {lines.Count} linha(s) reta(s) lidas" +
                     (notStraight > 0 ? $", {notStraight} curva(s) descartada(s)" : "") +
                     (unreadable > 0 ? $", {unreadable} ilegível(is)" : "") + ".");
            return lines;
        }

        /// <summary>
        /// Reta? Pelo tipo da geometria (<c>Edge.Geometry.Type == igLine</c>, o teste do
        /// SharpCornerProbe); se o tipo não vier, pelo ponto médio da aresta estar na corda.
        /// </summary>
        private static bool IsStraight(object edge, double[] a, double[] b)
        {
            const int igLine = 167551109;
            try
            {
                object g = Get(edge, "Geometry");
                if (g != null)
                {
                    object t = Get(g, "Type");
                    if (t != null) return Convert.ToInt32(t) == igLine;
                }
            }
            catch { }
            // Sem tipo legível: a caixa da aresta tem de ser a caixa da corda (curva estufa a caixa).
            if (!FaceGeometry.TryGetExactRangeMm(edge, out double[] mn, out double[] mx)) return true;
            for (int k = 0; k < 3; k++)
                if (mn[k] < Math.Min(a[k], b[k]) - 0.01 || mx[k] > Math.Max(a[k], b[k]) + 0.01) return false;
            return true;
        }

        private static bool Same(CoolingLine l, double[] a, double[] b) =>
            (Vec.Dist(l.StartMm, a) < 1e-6 && Vec.Dist(l.EndMm, b) < 1e-6) ||
            (Vec.Dist(l.StartMm, b) < 1e-6 && Vec.Dist(l.EndMm, a) < 1e-6);

        private static object Get(object o, string name)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }
    }
}
