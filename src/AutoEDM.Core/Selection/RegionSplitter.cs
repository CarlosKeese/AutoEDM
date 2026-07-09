using System;
using System.Collections.Generic;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Separa um conjunto de faces (já da mesma cor/Ra) em REGIÕES por conectividade:
    /// faces que compartilham arestas pertencem ao mesmo detalhe físico; detalhes
    /// separados na cavidade viram componentes conexos distintos — e cada um será um
    /// eletrodo individual.
    ///
    /// Motivo (observação do Carlos): uma única cor de queima costuma marcar VÁRIOS
    /// detalhes espalhados; tratá-los como uma região só estaria errado.
    ///
    /// Método: duas faces são adjacentes se compartilham uma aresta (mesma chave de
    /// aresta). Union-find agrupa as faces transitivamente conectadas. A chave da
    /// aresta usa Edge.ID (com fallback Tag) — validado no SE 2023 por introspecção
    /// da mesma forma que a cor.
    /// </summary>
    public sealed class RegionSplitter
    {
        /// <summary>
        /// Separa as faces em detalhes por PROXIMIDADE espacial (single-linkage):
        /// duas faces ficam no mesmo detalhe se a folga entre seus bounding boxes for
        /// &lt;= <paramref name="gapMm"/>. Áreas contínuas permanecem juntas; ilhas
        /// afastadas viram detalhes separados — cobrindo o caso "misto".
        /// </summary>
        public IReadOnlyList<List<SelectedFace>> SplitBySpatialProximity(
            IReadOnlyList<SelectedFace> faces, double gapMm)
        {
            int n = faces.Count;
            if (n <= 1)
                return n == 0 ? new List<List<SelectedFace>>()
                              : new List<List<SelectedFace>> { new List<SelectedFace>(faces) };

            var min = new double[n][];
            var max = new double[n][];
            var has = new bool[n];
            int ok = 0;
            for (int i = 0; i < n; i++)
                if (FaceGeometry.TryGetRangeMm(faces[i].ComFace, out min[i], out max[i])) { has[i] = true; ok++; }

            Log.Info($"RegionSplitter(proximidade): bounding box lido em {ok}/{n} face(s), gap = {gapMm} mm.");

            if (ok == 0)
            {
                Log.Warn("Nenhum bounding box de face lido (GetRange/GetExactRange a validar). " +
                         "Tratando como uma única região.");
                return new List<List<SelectedFace>> { new List<SelectedFace>(faces) };
            }

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            for (int i = 0; i < n; i++)
            {
                if (!has[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!has[j]) continue;
                    if (AabbGap(min[i], max[i], min[j], max[j]) <= gapMm)
                        Union(parent, i, j);
                }
            }

            var byRoot = new Dictionary<int, List<SelectedFace>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(parent, i);
                if (!byRoot.TryGetValue(r, out var list)) byRoot[r] = list = new List<SelectedFace>();
                list.Add(faces[i]);
            }

            var result = new List<List<SelectedFace>>(byRoot.Values);
            result.Sort((a, b) => b.Count.CompareTo(a.Count));

            var sizes = new List<int>();
            foreach (var c in result) sizes.Add(c.Count);
            Log.Info($"RegionSplitter(proximidade): {n} face(s) -> {result.Count} detalhe(s). " +
                     $"Tamanhos: [{string.Join(", ", sizes)}].");
            return result;
        }

        private static double AabbGap(double[] minA, double[] maxA, double[] minB, double[] maxB)
        {
            double dx = AxisSep(minA[0], maxA[0], minB[0], maxB[0]);
            double dy = AxisSep(minA[1], maxA[1], minB[1], maxB[1]);
            double dz = AxisSep(minA[2], maxA[2], minB[2], maxB[2]);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double AxisSep(double aMin, double aMax, double bMin, double bMax)
        {
            if (bMin > aMax) return bMin - aMax;
            if (aMin > bMax) return aMin - bMax;
            return 0; // sobrepõem neste eixo
        }

        public IReadOnlyList<List<SelectedFace>> SplitByConnectivity(IReadOnlyList<SelectedFace> faces)
        {
            int n = faces.Count;
            if (n <= 1)
                return n == 0 ? new List<List<SelectedFace>>()
                              : new List<List<SelectedFace>> { new List<SelectedFace>(faces) };

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            // Chave de aresta -> faces que a possuem. Faces que compartilham a
            // mesma aresta (manifold: 2 faces) são unidas.
            var edgeToFaces = new Dictionary<string, List<int>>();
            string keyMethod = null;
            int edgelessFaces = 0;
            int totalRefs = 0;

            for (int i = 0; i < n; i++)
            {
                var keys = GetEdgeKeys(faces[i].ComFace, ref keyMethod);
                if (keys.Count == 0) edgelessFaces++;

                foreach (var key in keys)
                {
                    totalRefs++;
                    if (!edgeToFaces.TryGetValue(key, out List<int> list))
                        edgeToFaces[key] = list = new List<int>();
                    list.Add(i);
                }
            }

            int sharedKeys = 0;
            foreach (var kv in edgeToFaces)
            {
                var list = kv.Value;
                int first = list[0];
                bool shared = false;
                for (int k = 1; k < list.Count; k++)
                {
                    if (list[k] != first) shared = true;
                    Union(parent, first, list[k]);
                }
                if (shared) sharedKeys++;
            }

            // Diagnóstico da topologia: se 'arestas distintas' for minúsculo, o
            // Edge.ID está colidindo (une demais). Se for da ordem de metade das
            // refs, os ids são únicos e a conectividade é real.
            Log.Info($"RegionSplitter diag: {n} face(s), {totalRefs} ref(s) de aresta, " +
                     $"{edgeToFaces.Count} aresta(s) distinta(s), {sharedKeys} compartilhada(s) entre faces" +
                     (keyMethod != null ? $" (via {keyMethod})" : "") + ".");

            if (keyMethod == null)
            {
                // Não conseguimos ler arestas -> não dá para dividir. Trata como 1
                // região e sinaliza (melhor não fragmentar errado).
                Log.Warn("RegionSplitter: não foi possível ler arestas das faces " +
                         "(Edge.ID/Tag indisponível). Tratando as faces como uma única região.");
                return new List<List<SelectedFace>> { new List<SelectedFace>(faces) };
            }

            var byRoot = new Dictionary<int, List<SelectedFace>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(parent, i);
                if (!byRoot.TryGetValue(r, out var list))
                    byRoot[r] = list = new List<SelectedFace>();
                list.Add(faces[i]);
            }

            Log.Info($"RegionSplitter: {n} face(s) -> {byRoot.Count} detalhe(s) " +
                     $"(chave de aresta via {keyMethod}" +
                     (edgelessFaces > 0 ? $"; {edgelessFaces} face(s) sem arestas legíveis" : "") + ").");

            var result = new List<List<SelectedFace>>(byRoot.Values);
            // Maiores detalhes primeiro (mais faces = provavelmente o principal).
            result.Sort((a, b) => b.Count.CompareTo(a.Count));
            return result;
        }

        private static List<string> GetEdgeKeys(dynamic comFace, ref string keyMethod)
        {
            var keys = new List<string>();
            dynamic edges;
            try { edges = comFace.Edges; }
            catch { return keys; }
            if (edges == null) return keys;

            int count;
            try { count = (int)edges.Count; }
            catch { return keys; }

            for (int i = 1; i <= count; i++) // coleções do SE são 1-based
            {
                object edge;
                try { edge = edges.Item(i); }
                catch { continue; }

                string key = EdgeKey(edge, ref keyMethod);
                if (key != null) keys.Add(key);
            }
            return keys;
        }

        private static string EdgeKey(object edge, ref string keyMethod)
        {
            dynamic e = edge;

            object id = Try(() => e.ID);
            if (id != null) { keyMethod = keyMethod ?? "Edge.ID"; return "id:" + Convert.ToString(id); }

            object tag = Try(() => e.Tag);
            if (tag != null) { keyMethod = keyMethod ?? "Edge.Tag"; return "tag:" + Convert.ToString(tag); }

            return null;
        }

        private static object Try(Func<object> f)
        {
            try { return f(); } catch { return null; }
        }

        // --- union-find ---------------------------------------------------------

        private static int Find(int[] p, int x)
        {
            while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
            return x;
        }

        private static void Union(int[] p, int a, int b)
        {
            int ra = Find(p, a), rb = Find(p, b);
            if (ra != rb) p[ra] = rb;
        }
    }
}
