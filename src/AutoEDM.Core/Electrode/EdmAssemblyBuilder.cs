using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>Onde a peça fica na montagem de erosão (rotação linha a linha, translação em mm).</summary>
    public sealed class EdmPlacement
    {
        public double[] R = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        public double[] TMm = { 0, 0, 0 };
        /// <summary>De onde veio o plano de fechamento (cor "10 - FECHAM." ou o topo da caixa).</summary>
        public string ClosingSource;
        public double ClosingAreaMm2;
    }

    public sealed class EdmAssemblyResult
    {
        public object Assembly;
        public string Path;
        public bool Created;
        public bool Opened;
        public string Failure;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// A montagem de EROSÃO de uma peça (regra do Carlos, 2026-11-10): quando o "Criar Eletrodos" é usado
    /// numa PEÇA, nasce a montagem <c>&lt;código da peça&gt;_EDM.asm</c> ao lado dela, com a peça posicionada de
    /// modo que a origem da montagem (o zero-máquina) fique NA FACE DE FECHAMENTO em Z e no CENTRO da peça em
    /// X e Y, com a face de fechamento virada para +Z (o eletrodo desce de cima).
    /// </summary>
    public static class EdmAssemblyBuilder
    {
        /// <summary>Cor do estilo da casa "10 - FECHAM." (fechamentos e regiões de ajuste).</summary>
        public static readonly Color ClosingColor = Color.FromArgb(0, 178, 178);

        /// <summary>Cores de CAVIDADE da casa ("17" erodida, "16" usinada): dizem de que lado a peça abre.</summary>
        public static readonly Color[] CavityColors = { Color.FromArgb(176, 0, 178), Color.FromArgb(116, 54, 178) };

        /// <summary>
        /// Entre as direções candidatas a fechamento (cada uma com a altura do seu plano mais alto e a área
        /// pintada), escolhe a do lado em que a CAVIDADE abre: aquela cujo plano fica mais perto das faces de
        /// cavidade. Um postiço costuma ter a face de baixo pintada como fechamento também, e ela é MAIOR que
        /// a de cima (que tem o bolsão recortado) — escolher pela área virava a peça de cabeça para baixo
        /// (medido no 15428.100 em 2026-11-11). Sem faces de cavidade, vale a maior área. Lógica pura.
        /// </summary>
        /// <param name="candidates">normal (unitária, para fora), altura do plano (n·p, mm), área (mm²).</param>
        /// <param name="cavityCentersMm">centros das faces de cavidade, no referencial da peça.</param>
        public static int ChooseClosingDirection(IList<Tuple<double[], double, double>> candidates, IList<double[]> cavityCentersMm)
        {
            if (candidates == null || candidates.Count == 0) return -1;
            int best = -1;
            if (cavityCentersMm != null && cavityCentersMm.Count > 0)
            {
                double bestDepth = double.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    double[] n = candidates[i].Item1;
                    double h = candidates[i].Item2;
                    var depths = cavityCentersMm.Select(c => h - (n[0] * c[0] + n[1] * c[1] + n[2] * c[2])).OrderBy(d => d).ToList();
                    double median = depths[depths.Count / 2];
                    if (median < -1.0) continue;                 // cavidade ACIMA do plano: não é o fechamento dela
                    if (median < bestDepth) { bestDepth = median; best = i; }
                }
                if (best >= 0) return best;
            }
            double bestArea = -1;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Item3 > bestArea) { bestArea = candidates[i].Item3; best = i; }
            return best;
        }

        public static string AssemblyPathFor(string partPath) =>
            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(partPath) ?? "",
                System.IO.Path.GetFileNameWithoutExtension(partPath) + "_EDM.asm");

        /// <summary>
        /// A pose a partir do plano de fechamento (normal para FORA do aço e um ponto dele, em mm) e da caixa
        /// da peça (mm, local). Lógica pura.
        /// </summary>
        public static EdmPlacement PlacementFor(double[] normal, double[] pointOnPlaneMm, double[] boxMinMm, double[] boxMaxMm)
        {
            double len = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            if (len < 1e-9) throw new ArgumentException("normal nula.", nameof(normal));
            double[] n = { normal[0] / len, normal[1] / len, normal[2] / len };

            // Base ortonormal com n no lugar do Z: x' = o eixo local menos alinhado com n, sem a componente em n.
            double[] seed = Math.Abs(n[0]) < 0.9 ? new[] { 1.0, 0.0, 0.0 } : new[] { 0.0, 1.0, 0.0 };
            double d = seed[0] * n[0] + seed[1] * n[1] + seed[2] * n[2];
            double[] x = { seed[0] - d * n[0], seed[1] - d * n[1], seed[2] - d * n[2] };
            double xl = Math.Sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);
            x = new[] { x[0] / xl, x[1] / xl, x[2] / xl };
            double[] y = { n[1] * x[2] - n[2] * x[1], n[2] * x[0] - n[0] * x[2], n[0] * x[1] - n[1] * x[0] };

            var p = new EdmPlacement { R = new[] { x[0], x[1], x[2], y[0], y[1], y[2], n[0], n[1], n[2] } };

            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int c = 0; c < 8; c++)
            {
                double lx = (c & 1) == 0 ? boxMinMm[0] : boxMaxMm[0];
                double ly = (c & 2) == 0 ? boxMinMm[1] : boxMaxMm[1];
                double lz = (c & 4) == 0 ? boxMinMm[2] : boxMaxMm[2];
                double ax = x[0] * lx + x[1] * ly + x[2] * lz, ay = y[0] * lx + y[1] * ly + y[2] * lz;
                minX = Math.Min(minX, ax); maxX = Math.Max(maxX, ax);
                minY = Math.Min(minY, ay); maxY = Math.Max(maxY, ay);
            }
            double h = n[0] * pointOnPlaneMm[0] + n[1] * pointOnPlaneMm[1] + n[2] * pointOnPlaneMm[2];
            p.TMm = new[] { -(minX + maxX) / 2.0, -(minY + maxY) / 2.0, -h };
            return p;
        }

        /// <summary>
        /// Se <paramref name="activeDoc"/> já é montagem, devolve-a. Se é peça, abre a <c>_EDM.asm</c> que já
        /// existir ou cria uma nova com a peça posicionada. Nunca lança: o motivo vai em <see cref="EdmAssemblyResult.Failure"/>.
        /// </summary>
        public static EdmAssemblyResult Ensure(object application, object activeDoc, FaceSelector faceSelector = null)
        {
            var res = new EdmAssemblyResult();
            dynamic app = application;
            dynamic doc = activeDoc;
            try
            {
                int type = (int)doc.Type;
                if (type == 3) { res.Assembly = activeDoc; res.Path = Safe(() => (string)doc.FullName); return res; }
                if (type != 1) { res.Failure = "o documento ativo não é peça nem montagem."; return res; }

                string partPath = Safe(() => (string)doc.FullName);
                if (string.IsNullOrEmpty(partPath) || !System.IO.File.Exists(partPath))
                { res.Failure = "a peça ainda não foi salva em disco — salve antes (a montagem de erosão referencia o arquivo)."; return res; }

                res.Path = AssemblyPathFor(partPath);
                if (System.IO.File.Exists(res.Path))
                {
                    res.Assembly = (object)app.Documents.Open(res.Path);
                    res.Opened = true;
                    res.Notes.Add($"a montagem de erosão já existia e foi aberta: {System.IO.Path.GetFileName(res.Path)}.");
                    return res;
                }

                EdmPlacement place = ReadPlacement(app, doc, faceSelector ?? new FaceSelector(), res.Notes);

                dynamic asm = HouseTemplates.Add(application, HouseDocumentKind.Assembly);
                dynamic occ = asm.Occurrences.AddByFilename(partPath);
                OccurrenceTransform current = AssemblyContext.TryGetPose(new OccurrenceInfo(occ, "peça", null));
                MatrixLayout layout = current?.Layout ?? MatrixLayout.TranslationLast;
                OccurrenceTransform pose = OccurrenceTransform.Identity(layout).ComposedWith(place.R,
                    new[] { Units.MmToM(place.TMm[0]), Units.MmToM(place.TMm[1]), Units.MmToM(place.TMm[2]) });
                object[] args = { pose.ToMatrix(), true };
                ((object)occ).GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, (object)occ, args,
                    null, CultureInfo.InvariantCulture, null);

                // Confere lendo de volta: a pose escrita tem de ser a pose lida.
                OccurrenceTransform back = AssemblyContext.TryGetPose(new OccurrenceInfo(occ, "peça", null));
                if (back != null)
                {
                    double err = Math.Abs(back.TxM - pose.TxM) + Math.Abs(back.TyM - pose.TyM) + Math.Abs(back.TzM - pose.TzM);
                    if (err > 1e-6) res.Notes.Add($"a posição lida de volta difere da escrita em {Units.MToMm(err):0.###} mm — confira a peça na montagem.");
                }

                asm.SaveAs(res.Path);
                res.Assembly = (object)asm;
                res.Created = true;
                res.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                    "montagem de erosão criada: {0} — origem na face de fechamento ({1}, {2:0.#} mm²) e no centro da peça em X e Y.",
                    System.IO.Path.GetFileName(res.Path), place.ClosingSource, place.ClosingAreaMm2));
                Log.Info("Montagem de erosão: " + res.Notes[res.Notes.Count - 1] + " " + pose.Describe());
                return res;
            }
            catch (Exception ex)
            {
                res.Failure = ex.GetBaseException().Message;
                Log.Warn("Montagem de erosão: falhou — " + res.Failure);
                return res;
            }
        }

        /// <summary>Lê da peça o plano de fechamento e a caixa, e devolve a pose.</summary>
        private static EdmPlacement ReadPlacement(dynamic app, dynamic partDoc, FaceSelector selector, List<string> notes)
        {
            double[] bmin = null, bmax = null;
            try { FaceGeometry.TryGetBodyRangeMm((object)partDoc.Models.Item(1).Body, out bmin, out bmax); } catch { }
            if (bmin == null) throw new InvalidOperationException("não consegui ler a caixa do corpo da peça.");

            // Faces planas na cor de fechamento, agrupadas pela direção da normal; vence a direção de maior área,
            // e nela o plano mais ALTO (o fechamento é a face de cima do postiço).
            var byDir = new Dictionary<string, List<Tuple<double[], double[], double>>>();
            var cavityCenters = new List<double[]>();
            foreach (SelectedFace sf in selector.EnumerateColoredFaces(partDoc, app))
            {
                try
                {
                    if (CavityColors.Any(c => OleColor.Matches(sf.DetectedColor, c, 8)))
                    {
                        if (FaceGeometry.TryGetRangeMm((object)sf.ComFace, out double[] cmn, out double[] cmx))
                            cavityCenters.Add(new[] { (cmn[0] + cmx[0]) / 2, (cmn[1] + cmx[1]) / 2, (cmn[2] + cmx[2]) / 2 });
                        continue;
                    }
                    if (!OleColor.Matches(sf.DetectedColor, ClosingColor, 8)) continue;
                    object face = sf.ComFace;
                    if (!IsPlane(face)) continue;
                    if (!TryOutwardNormal(face, out double[] n)) continue;
                    if (!FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx)) continue;
                    double area = (double)((dynamic)face).Area * 1e6;
                    double[] point = { (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mn[2] + mx[2]) / 2 };
                    string key = $"{Math.Round(n[0], 2)}|{Math.Round(n[1], 2)}|{Math.Round(n[2], 2)}";
                    if (!byDir.TryGetValue(key, out var list)) byDir[key] = list = new List<Tuple<double[], double[], double>>();
                    list.Add(Tuple.Create(n, point, area));
                }
                catch { }
            }

            if (byDir.Count > 0)
            {
                var groups = byDir.Values.ToList();
                var candidates = groups.Select(g =>
                {
                    double[] gn = g[0].Item1;
                    return Tuple.Create(gn, g.Max(t => gn[0] * t.Item2[0] + gn[1] * t.Item2[1] + gn[2] * t.Item2[2]), g.Sum(t => t.Item3));
                }).ToList();
                int pick = ChooseClosingDirection(candidates, cavityCenters);
                var best = groups[pick];
                double[] n = best[0].Item1;
                var top = best.OrderByDescending(t => n[0] * t.Item2[0] + n[1] * t.Item2[1] + n[2] * t.Item2[2]).First();
                EdmPlacement p = PlacementFor(n, top.Item2, bmin, bmax);
                p.ClosingSource = cavityCenters.Count > 0
                    ? "cor \"10 - FECHAM.\", do lado em que a cavidade abre"
                    : "cor \"10 - FECHAM.\", maior área (peça sem cor de cavidade)";
                p.ClosingAreaMm2 = best.Sum(t => t.Item3);
                return p;
            }

            notes.Add("nenhuma face plana com a cor de fechamento (\"10 - FECHAM.\"): usei o topo da peça em +Z como fechamento — confira.");
            EdmPlacement fallback = PlacementFor(new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 0.0, bmax[2] }, bmin, bmax);
            fallback.ClosingSource = "topo da caixa em +Z";
            return fallback;
        }

        private const int igPlane = -1909484335; // GNTTypePropertyConstants.igPlane (índice da typelib; -114972029 é igCylinder)

        private static bool IsPlane(object face)
        {
            try { return Convert.ToInt32(((dynamic)face).Geometry.Type) == igPlane; } catch { return false; }
        }

        /// <summary>Normal para FORA do material no meio da face: Face.GetParamRange + Face.GetNormal ([in,out]).</summary>
        internal static bool TryOutwardNormal(object face, out double[] normal)
        {
            normal = null;
            try
            {
                object[] range = { new double[2], new double[2] };
                var m2 = new ParameterModifier(2); m2[0] = true; m2[1] = true;
                face.GetType().InvokeMember("GetParamRange", BindingFlags.InvokeMethod, null, face, range, new[] { m2 }, CultureInfo.InvariantCulture, null);
                double[] lo = (double[])range[0], hi = (double[])range[1];
                object[] args = { 1, new[] { (lo[0] + hi[0]) / 2.0, (lo[1] + hi[1]) / 2.0 }, new double[3] };
                var m3 = new ParameterModifier(3); m3[1] = true; m3[2] = true;
                face.GetType().InvokeMember("GetNormal", BindingFlags.InvokeMethod, null, face, args, new[] { m3 }, CultureInfo.InvariantCulture, null);
                double[] n = (double[])args[2];
                if (n == null || n.Length < 3 || Math.Abs(n[0]) + Math.Abs(n[1]) + Math.Abs(n[2]) < 1e-9) return false;
                normal = n;
                return true;
            }
            catch { return false; }
        }

        private static string Safe(Func<string> read) { try { return read(); } catch { return null; } }
    }
}
