using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>Grupo de faces que compartilham a mesma cor/Ra.</summary>
    public sealed class FaceGroup
    {
        public Color Color { get; }
        public double Ra { get; }
        public List<SelectedFace> Faces { get; } = new List<SelectedFace>();

        public FaceGroup(Color color, double ra) { Color = color; Ra = ra; }
    }

    /// <summary>Contagem de faces por COR encontrada numa peça — mapeada (com Ra) ou não.
    /// Usado para o usuário CONFERIR a detecção antes de criar eletrodos (evita criar em
    /// cor residual quando a queima real está numa cor não mapeada — Log 57).</summary>
    public sealed class ColorTally
    {
        public Color Color { get; }
        public int FaceCount { get; }
        public bool Mapped { get; }
        public double Ra { get; }
        public ColorTally(Color color, int faceCount, bool mapped, double ra)
        { Color = color; FaceCount = faceCount; Mapped = mapped; Ra = ra; }
    }

    /// <summary>
    /// Seleciona faces de queima por cor.
    ///
    /// Object walk (SolidEdgePart / SolidEdgeGeometry):
    ///   PartDocument.Models -> Model.Body -> Body.Faces[queryType] -> Face
    ///
    /// O valor da constante de topologia (igQueryAll) varia por versão, então por
    /// padrão fazemos probing de candidatos e ficamos com o que retorna mais faces.
    /// Confirmado o valor, fixe em <see cref="ForcedFaceQueryType"/>.
    /// </summary>
    public sealed class FaceSelector
    {
        private readonly IFaceColorReader _colorReader;

        public int[] FaceQueryCandidates { get; set; } = { 0, 1, 2, 3, 4, 5, 70, 71 };

        // Validado no SE 2023 (run real): query type 1 = igQueryAll. Fixado como
        // padrão; ponha null para reativar o probing em outra versão.
        public int? ForcedFaceQueryType { get; set; } = 1;

        // Diagnóstico único: na 1ª face lida, loga o esquema de membros do objeto
        // Face. Já usado para descobrir a API de cor (face.Style.Diffuse*), então
        // fica desligado por padrão; reative se precisar inspecionar outra versão.
        public bool DiagnoseFirstFace { get; set; } = false;
        private bool _diagDone;

        // Diagnóstico único: na 1ª face, loga o esquema + assinaturas de geometria
        // (range/box/...). Ligado por padrão porque GetRange/GetExactRange falharam
        // em 100% das faces no SE 2023 (Log 8); desligar quando a API estiver fixada.
        public bool DiagnoseFirstFaceGeometry { get; set; } = true;
        private bool _geomDiagDone;

        public FaceSelector(IFaceColorReader colorReader = null)
        {
            // Leitor direto validado no SE 2023 (face.Style.Diffuse{R,G,B}).
            _colorReader = colorReader ?? new FaceStyleColorReader();
        }

        /// <summary>Faces cuja cor casa com <paramref name="target"/> na tolerância.</summary>
        public IReadOnlyList<SelectedFace> SelectByColor(dynamic partDocument, dynamic application,
            Color target, int tolerancePerChannel = 8)
        {
            IEnumerable<SelectedFace> all = EnumerateColoredFaces(partDocument, application);
            var result = all
                .Where(sf => OleColor.Matches(sf.DetectedColor, target, tolerancePerChannel))
                .ToList();
            Log.Info($"Selecionadas {result.Count} face(s) em RGB({target.R},{target.G},{target.B}).");
            return result;
        }

        /// <summary>Overload por RGB (assinatura da spec).</summary>
        public IReadOnlyList<SelectedFace> SelectByRGB(dynamic partDocument, dynamic application,
            int r, int g, int b, int tolerancePerChannel = 8)
            => SelectByColor(partDocument, application, Color.FromArgb(r, g, b), tolerancePerChannel);

        /// <summary>
        /// Varre as faces uma vez e agrupa por Ra segundo o <paramref name="raColorMap"/>.
        /// Faces sem cor mapeada são ignoradas. Grupos ordenados do Ra mais grosso
        /// para o mais fino.
        /// </summary>
        public IReadOnlyList<FaceGroup> SelectByRaColorMap(dynamic partDocument, dynamic application,
            Electrode.RaColorMap raColorMap)
        {
            IReadOnlyList<ColorTally> ignore;
            return SelectByRaColorMap(partDocument, application, raColorMap, out ignore);
        }

        /// <summary>Overload que também devolve o HISTOGRAMA de cores (todas as cores + contagem +
        /// se são mapeadas) — para a confirmação "conferir antes de criar".</summary>
        public IReadOnlyList<FaceGroup> SelectByRaColorMap(dynamic partDocument, dynamic application,
            Electrode.RaColorMap raColorMap, out IReadOnlyList<ColorTally> colorTally)
        {
            var groups = new Dictionary<double, FaceGroup>();
            var seen = new Dictionary<int, int>(); // RGB empacotado -> contagem
            IEnumerable<SelectedFace> all = EnumerateColoredFaces(partDocument, application);
            foreach (var sf in all)
            {
                int key = sf.DetectedColor.ToArgb();
                seen[key] = seen.TryGetValue(key, out int c) ? c + 1 : 1;

                if (!raColorMap.TryGetRa(sf.DetectedColor, out double ra, out Color matched))
                    continue;
                if (!groups.TryGetValue(ra, out var g))
                    groups[ra] = g = new FaceGroup(matched, ra);
                g.Faces.Add(sf);
            }

            // Diagnóstico + histograma: mostra TODAS as cores achadas e se casaram com o mapa Ra.
            var tally = new List<ColorTally>();
            foreach (var kv in seen)
            {
                Color col = Color.FromArgb(kv.Key);
                bool mapped = raColorMap.TryGetRa(col, out double raM, out _);
                tally.Add(new ColorTally(col, kv.Value, mapped, mapped ? raM : 0));
                Log.Info($"Cor RGB({col.R},{col.G},{col.B}) em {kv.Value} face(s)" +
                         (mapped ? $" -> Ra {raM}" : " (não mapeada)"));
            }
            colorTally = tally.OrderByDescending(t => t.FaceCount).ToList();

            return groups.Values.OrderByDescending(g => g.Ra).ToList();
        }

        // --- traversal (única passada, reaproveitada pelos métodos acima) -------

        /// <summary>Enumera toda face com cor legível, uma única vez.</summary>
        public IEnumerable<SelectedFace> EnumerateColoredFaces(dynamic partDocument, dynamic application)
        {
            if (partDocument == null) throw new ArgumentNullException(nameof(partDocument));

            // Tipar o resultado antes de qualquer LINQ: a chamada é dinâmica (doc é
            // dynamic), então ToList/Where só funcionam sobre uma variável tipada.
            IEnumerable<dynamic> bodyEnum = GetBodies(partDocument);
            List<dynamic> bodies = bodyEnum.ToList();
            if (bodies.Count == 0)
            {
                Log.Warn("Nenhum corpo (sólido/superfície) no documento.");
                yield break;
            }

            // Cor pintada por FEATURE não aparece em face.Style/GetRGBAVals (dão a cor do
            // corpo). Monta o mapa faceID -> cor da feature uma vez por peça.
            var featureColor = BuildFeatureColorMap(partDocument);

            bool sourceLogged = false;

            for (int b = 0; b < bodies.Count; b++)
            {
                // (object) força ligação estática das chamadas seguintes, evitando
                // dispatch dinâmico em métodos próprios / de extensão.
                List<dynamic> faces = GetAllFaces((object)bodies[b]);
                for (int f = 0; f < faces.Count; f++)
                {
                    object comFace = faces[f];

                    if (DiagnoseFirstFace && !_diagDone)
                    {
                        _diagDone = true;
                        Com.ComDiagnostics.LogColorDiscovery(comFace);
                    }

                    if (DiagnoseFirstFaceGeometry && !_geomDiagDone)
                    {
                        _geomDiagDone = true;
                        Com.ComDiagnostics.LogGeometryDiscovery(comFace);
                    }

                    bool got = _colorReader.TryReadColor(comFace, (object)application, out Color color, out string source);
                    // Pintura por FEATURE sobrepõe a cor do CORPO (GetRGBAVals) ou preenche o vazio.
                    Color fcol = default(Color);
                    if ((!got || source == "Face.GetRGBAVals") &&
                        TryFeatureColor(comFace, featureColor, out fcol))
                    {
                        color = fcol; source = "feature.GetStyle"; got = true;
                    }
                    if (!got) continue;

                    if (!sourceLogged)
                    {
                        Log.Info($"Cor da face lida via \"{source}\" (primeira leitura).");
                        sourceLogged = true;
                    }

                    yield return new SelectedFace(comFace, color, source, b, f);
                }
            }
        }

        /// <summary>
        /// Mapa faceID -> cor pintada na FEATURE. A cor aplicada a uma feature (menu de
        /// pintura padrão do SE) NÃO aparece em face.Style nem em Face.GetRGBAVals — só na
        /// própria feature (feature.GetStyle). Percorre Models[*].Features, lê a cor de
        /// cada feature e marca todas as faces dela (Feature.Faces, chave = Face.ID).
        /// </summary>
        private Dictionary<string, Color> BuildFeatureColorMap(dynamic partDoc)
        {
            var map = new Dictionary<string, Color>();
            int feats = 0, styled = 0;
            dynamic models;
            try { models = partDoc.Models; } catch { return map; }
            int mc;
            try { mc = (int)models.Count; } catch { return map; }

            for (int m = 1; m <= mc; m++)
            {
                dynamic fcoll;
                try { fcoll = models.Item(m).Features; } catch { continue; }
                int fc;
                try { fc = (int)fcoll.Count; } catch { continue; }

                for (int i = 1; i <= fc; i++)
                {
                    feats++;
                    dynamic feat; object style;
                    try { feat = fcoll.Item(i); style = feat.GetStyle(); } catch { continue; }
                    if (!FaceStyleColorReader.TryReadStyleColor(style, out Color col)) continue;
                    styled++;

                    List<dynamic> ffaces = GetFeatureFaces(feat);
                    if (!_featDiag)
                    {
                        _featDiag = true;
                        string fid = "?", fname = "?";
                        try { fname = Convert.ToString(feat.Name); } catch { }
                        try { if (ffaces.Count > 0) fid = Convert.ToString(((dynamic)ffaces[0]).ID); }
                        catch (Exception e) { fid = "erro:" + e.GetBaseException().Message; }
                        Log.Info($"  [DIAG] feature '{fname}' cor RGB({col.R},{col.G},{col.B}): " +
                                 $"{ffaces.Count} face(s), 1º Face.ID={fid}.");
                    }
                    foreach (var fface in ffaces)
                        try { map[Convert.ToString(((dynamic)fface).ID)] = col; } catch { }
                }
            }

            if (feats > 0)
                Log.Info($"Cor por FEATURE: {styled}/{feats} feature(s) com cor legível (feature.GetStyle) " +
                         $"-> {map.Count} face(s) mapeada(s).");
            return map;
        }

        private bool _featDiag;

        /// <summary>
        /// Faces de uma feature. Feature.Faces pode ser indexado por query type (como
        /// Body.Faces[1]) ou ser uma coleção direta — tenta as duas formas.
        /// </summary>
        private static List<dynamic> GetFeatureFaces(dynamic feat)
        {
            foreach (var attempt in new Func<dynamic>[] { () => feat.Faces[1], () => feat.Faces })
            {
                try
                {
                    dynamic coll = attempt();
                    int n = (int)coll.Count;
                    var list = new List<dynamic>(n);
                    for (int k = 1; k <= n; k++) list.Add(coll.Item(k));
                    if (n > 0) return list;
                }
                catch { }
            }
            return new List<dynamic>();
        }

        private static bool TryFeatureColor(object comFace, Dictionary<string, Color> map, out Color color)
        {
            color = default(Color);
            if (map.Count == 0) return false;
            try { return map.TryGetValue(Convert.ToString(((dynamic)comFace).ID), out color); }
            catch { return false; }
        }

        private static IEnumerable<dynamic> GetBodies(dynamic partDocument)
        {
            dynamic models;
            try { models = partDocument.Models; }
            catch (Exception ex)
            {
                Log.Warn($"PartDocument.Models inacessível: {ex.Message}");
                yield break;
            }

            int count;
            try { count = (int)models.Count; }
            catch { count = 0; }

            for (int i = 1; i <= count; i++) // 1-based
            {
                dynamic body = null;
                try { body = models.Item(i).Body; }
                catch (Exception ex) { Log.Warn($"Model[{i}].Body indisponível: {ex.Message}"); }
                if (body != null) yield return body;
            }
        }

        private List<dynamic> GetAllFaces(dynamic body)
        {
            object b = body; // ligação estática de ReadFaces
            if (ForcedFaceQueryType.HasValue)
                return ReadFaces(b, ForcedFaceQueryType.Value) ?? new List<dynamic>();

            List<dynamic> best = null;
            int bestQuery = 0;
            foreach (var q in FaceQueryCandidates)
            {
                var faces = ReadFaces(b, q);
                if (faces != null && (best == null || faces.Count > best.Count))
                {
                    best = faces;
                    bestQuery = q;
                }
            }

            if (best != null && best.Count > 0)
                Log.Info($"Face query type {bestQuery} ({best.Count} faces). " +
                         $"Fixe ForcedFaceQueryType={bestQuery} para evitar o probing.");

            return best ?? new List<dynamic>();
        }

        private static List<dynamic> ReadFaces(dynamic body, int queryType)
        {
            try
            {
                dynamic facesCollection = body.Faces[queryType];
                int count = (int)facesCollection.Count;
                var list = new List<dynamic>(count);
                for (int i = 1; i <= count; i++) // 1-based
                    list.Add(facesCollection.Item(i));
                return list;
            }
            catch
            {
                return null;
            }
        }
    }
}
