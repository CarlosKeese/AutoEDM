using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>As faces de queima de UMA peça de origem (uma ocorrência), e onde elas caem no eletrodo.</summary>
    public sealed class BurnSource
    {
        /// <summary>Nome da ocorrência, só para o log e o relatório.</summary>
        public string Label;
        /// <summary>O arquivo .par da peça dona das faces. A cópia LÊ ESTE ARQUIVO.</summary>
        public string FilePath;
        /// <summary>O documento da peça dona (para ler os estilos das faces); pode ser null.</summary>
        public object SourceDocument;
        /// <summary>Local da peça de origem → local da peça do eletrodo (<see cref="OccurrenceTransform.SeenFrom"/>).</summary>
        public OccurrenceTransform Placement;
        /// <summary>As faces escolhidas, da peça de origem (o <c>.Object</c> do embrulho da seleção).</summary>
        public List<object> Faces = new List<object>();
    }

    public sealed class BurnCopyResult
    {
        public int Requested;
        public int Copied;
        public int Repainted;
        public List<string> Surfaces = new List<string>();
        public List<string> Warnings = new List<string>();
        public bool AllCopied => Requested > 0 && Copied == Requested;

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(Copied == 0
                ? "As faces de queima NÃO foram copiadas para o eletrodo."
                : $"{Copied} de {Requested} face(s) de queima copiadas para dentro do eletrodo ({string.Join(", ", Surfaces)}), sem vínculo com a cavidade.");
            foreach (string w in Warnings) sb.Append(Environment.NewLine).Append("  • ").Append(w);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Copia as faces de queima para DENTRO da peça do eletrodo SEM edição em contexto. É a rota
    /// medida pela sonda <c>tools/ElectrodeCopyProbe</c> (2026-10-19, docs/INTER-PART.md §8):
    /// <list type="number">
    /// <item><c>CoordinateSystems.AddByMatrix(M)</c>: a cópia cai em M·p;</item>
    /// <item><c>CopyConstructions.Add(arquivo, …, CopyColors)</c>: o corpo inteiro da peça de origem, LIDO DO ARQUIVO;</item>
    /// <item>as faces da cópia têm o MESMO <c>Face.ID</c> da origem, então as escolhidas são achadas pelo ID;</item>
    /// <item><c>CopySurfaces.Add</c> INTRA-peça sobre elas;</item>
    /// <item>em síncrono a cor não chega na superfície: repinta cada face com o estilo da origem;</item>
    /// <item>apaga a cópia do corpo (e o sistema de coordenadas): a superfície fica, solta da cavidade
    /// — é o "quebrar" que o Carlos fazia à mão.</item>
    /// </list>
    /// Nunca lança: o eletrodo continua válido sem a cópia, e o relatório diz o que faltou.
    /// </summary>
    public static class BurnSurfaceCopier
    {
        public static BurnCopyResult Copy(object partDoc, IList<BurnSource> sources)
        {
            var res = new BurnCopyResult();
            foreach (BurnSource src in sources ?? new List<BurnSource>())
            {
                res.Requested += src.Faces.Count;
                try { CopyOne(partDoc, src, res); }
                catch (Exception ex)
                {
                    res.Warnings.Add($"{src.Label}: {ex.GetBaseException().Message}");
                    Log.Warn($"Cópia da queima ({src.Label}) falhou: {ex.GetBaseException().Message}");
                }
            }
            return res;
        }

        private static void CopyOne(object partDoc, BurnSource src, BurnCopyResult res)
        {
            dynamic part = partDoc;
            if (string.IsNullOrEmpty(src.FilePath) || !System.IO.File.Exists(src.FilePath))
            {
                res.Warnings.Add($"{src.Label}: o arquivo da peça não existe no disco ({src.FilePath ?? "sem nome"}) — salve a peça e copie à mão.");
                return;
            }
            // O visual de cada face escolhida, lido ANTES — na origem a cor é certa.
            Dictionary<int, FaceLook> featureStyles = src.SourceDocument != null
                ? FaceLookReader.ReadFeatureStyles(src.SourceDocument) : new Dictionary<int, FaceLook>();
            var wanted = new Dictionary<int, FaceLook>();
            var sourceArea = new Dictionary<int, double>();
            foreach (object f in src.Faces)
            {
                int id;
                try { id = (int)((dynamic)f).ID; } catch { continue; }
                wanted[id] = FaceLookReader.Resolve(f, featureStyles);
                try { sourceArea[id] = (double)((dynamic)f).Area * 1e6; } catch { }
            }
            if (wanted.Count == 0) { res.Warnings.Add($"{src.Label}: faces sem ID legível."); return; }

            object cs = Invoke((object)part.CoordinateSystems, "AddByMatrix", src.Placement.ToMatrix());

            // 14 [in]; o [out] volta como valor de retorno (mesmo idioma da sonda da rodada 2).
            object cc = Invoke((object)part.Constructions.CopyConstructions, "Add",
                src.FilePath, 1.0, 1.0, 1.0, Type.Missing, Type.Missing, cs,
                true, false, false, false, Type.Missing, Type.Missing, true);
            if (!IsOk(cc)) { res.Warnings.Add($"{src.Label}: a cópia do corpo falhou (status {StatusOf(cc)})."); Cleanup(part, cc, cs); return; }

            // As faces da cópia pelo ID da origem.
            var copyById = new Dictionary<int, object>();
            dynamic cf = ((dynamic)cc).Faces[1];
            for (int i = 1; i <= (int)cf.Count; i++)
            {
                object f = cf.Item(i);
                try { copyById[(int)((dynamic)f).ID] = f; } catch { }
            }
            var picked = new List<object>();
            var pickedIds = new List<int>();
            foreach (int id in wanted.Keys)
                if (copyById.TryGetValue(id, out object f)) { picked.Add(f); pickedIds.Add(id); }
            if (picked.Count < wanted.Count)
                res.Warnings.Add($"{src.Label}: {wanted.Count - picked.Count} face(s) sem par na cópia (a peça salva difere da aberta?).");
            if (picked.Count == 0) { Cleanup(part, cc, cs); return; }

            // A geometria das faces da cópia é lida AGORA: o CopySurfaces.Add reconstrói o modelo e
            // invalida estes proxies (em síncrono a leitura depois dele não devolveu nada).
            var looks = pickedIds.Select((id, k) => new { Id = id, Look = wanted[id], Geo = Geo.Of(picked[k]) })
                                 .Where(x => x.Geo != null).ToList();

            // A cópia LÊ O ARQUIVO: se a peça aberta mudou e não foi salva, a mesma face tem outra área.
            int changed = looks.Count(x => sourceArea.TryGetValue(x.Id, out double a0) && Math.Abs(a0 - x.Geo.Area) > 1e-3 + 1e-6 * a0);
            if (changed > 0)
                res.Warnings.Add($"{src.Label}: {changed} face(s) diferem da peça aberta — a cópia veio do arquivo SALVO; salve a peça e refaça.");

            var typed = picked.Select(f => (SolidEdgeGeometry.Face)f).ToArray();
            object surf = Invoke((object)part.Constructions.CopySurfaces, "Add", typed.Length, typed, Type.Missing, Type.Missing);
            if (!IsOk(surf)) { res.Warnings.Add($"{src.Label}: a cópia das superfícies falhou (status {StatusOf(surf)})."); Cleanup(part, cc, cs); return; }

            // Cada face da superfície ↔ a face da cópia na mesma posição (área e caixa) → o estilo da origem.
            var byStyle = new Dictionary<string, List<object>>();
            var colorOf = new Dictionary<string, Color>();
            dynamic sf = ((dynamic)surf).Faces[1];
            int nSurf = (int)sf.Count;
            for (int i = 1; i <= nSurf; i++)
            {
                object f = sf.Item(i);
                Geo g = Geo.Of(f);
                var hit = g == null ? null : looks.FirstOrDefault(x => x.Geo.Same(g));
                if (hit?.Look?.StyleName == null) continue;
                if (!byStyle.TryGetValue(hit.Look.StyleName, out List<object> list)) byStyle[hit.Look.StyleName] = list = new List<object>();
                list.Add(f);
                colorOf[hit.Look.StyleName] = hit.Look.Color ?? Color.Gray;
            }

            // Em ORDENADO a cor já veio; solta a superfície antes de apagar o pai (medido: só ordenado aceita).
            bool ordered = false;
            try { ordered = (int)part.ModelingMode == 2; } catch { }
            int repainted = 0;
            if (ordered)
            {
                try { ((dynamic)surf).DropParents(); } catch (Exception ex) { Log.Info("Cópia da queima: DropParents — " + ex.GetBaseException().Message); }
            }
            else
            {
                foreach (KeyValuePair<string, List<object>> kv in byStyle)
                {
                    string got = FaceColorPainter.PaintWithNamedStyle(partDoc, kv.Value, new[] { kv.Key }, kv.Key, colorOf[kv.Key]);
                    if (got != null) repainted += kv.Value.Count;
                }
            }

            res.Repainted += repainted;
            // Nome único por ocorrência ("x.par:2" → "Queima - x #2"): a SE recusa nome repetido.
            string instance = src.Label != null && src.Label.Contains(":") ? " #" + src.Label.Substring(src.Label.LastIndexOf(':') + 1) : "";
            string name = "Queima - " + System.IO.Path.GetFileNameWithoutExtension(src.FilePath) + instance;
            try { ((dynamic)surf).Name = name; } catch { }
            Cleanup(part, cc, cs);

            // Conferência: a superfície continua inteira depois de apagar o pai.
            int after = -1;
            try { after = (int)((dynamic)surf).Faces[1].Count; } catch { }
            if (after != nSurf || !IsOk(surf))
                res.Warnings.Add($"{src.Label}: a superfície mudou ao apagar a cópia do corpo ({nSurf} → {after} face(s), status {StatusOf(surf)}).");

            res.Copied += picked.Count;
            res.Surfaces.Add(name);
            Log.Info($"Cópia da queima ({src.Label}): {picked.Count} face(s) → '{name}' ({nSurf} face(s) na superfície; " +
                     $"{(ordered ? "cor da cópia" : $"{repainted} repintada(s)")}).");
        }

        /// <summary>A limpeza é COSMÉTICA: engole o erro e loga — não decide o veredito.</summary>
        private static void Cleanup(dynamic part, object cc, object cs)
        {
            if (cc != null) { try { ((dynamic)cc).Delete(); } catch (Exception ex) { Log.Warn("Cópia da queima: não apaguei a cópia do corpo — " + ex.GetBaseException().Message); } }
            if (cs != null) { try { ((dynamic)cs).Delete(); } catch (Exception ex) { Log.Info("Cópia da queima: sistema de coordenadas ficou — " + ex.GetBaseException().Message); } }
        }

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args, null, CultureInfo.InvariantCulture, null);

        private const long IgFeatureOK = 1216476310L;

        private static long? Status(object feature)
        {
            if (feature == null) return null;
            try { return Convert.ToInt64(((dynamic)feature).Status[1]) & 0xFFFFFFFFL; } catch { }
            try { return Convert.ToInt64(((dynamic)feature).Status) & 0xFFFFFFFFL; } catch { }
            return null;
        }

        private static bool IsOk(object feature) => feature != null && (Status(feature) ?? IgFeatureOK) == IgFeatureOK;
        private static string StatusOf(object feature) => Status(feature)?.ToString(CultureInfo.InvariantCulture) ?? "ilegível";

        private sealed class Geo
        {
            public double Area; public double[] Min, Max;

            public static Geo Of(object face)
            {
                var g = new Geo();
                try { g.Area = (double)((dynamic)face).Area * 1e6; } catch { return null; }
                return FaceGeometry.TryGetRangeMm(face, out g.Min, out g.Max) ? g : null;
            }

            public bool Same(Geo o) =>
                Math.Abs(Area - o.Area) < 1e-3 + 1e-6 * Area &&
                Enumerable.Range(0, 3).All(k => Math.Abs(Min[k] - o.Min[k]) < 1e-3 && Math.Abs(Max[k] - o.Max[k]) < 1e-3);
        }
    }
}
