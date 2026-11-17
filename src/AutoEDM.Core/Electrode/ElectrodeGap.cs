using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>Uma face guardada pela geometria (mm, referencial da peça do eletrodo): o tipo e a caixa exata.</summary>
    public sealed class FaceSig
    {
        public int Type;
        public double[] Min, Max;
        public double AreaMm2;
        /// <summary>Plana e vertical: a normal PARA FORA (unitária) — serve para medir nervuras.</summary>
        public double[] OutwardNormal;

        public bool SameAs(FaceSig o, double tolMm = 2e-4)
        {
            if (o == null || o.Type != Type) return false;
            for (int i = 0; i < 3; i++)
                if (Math.Abs(o.Min[i] - Min[i]) > tolMm || Math.Abs(o.Max[i] - Max[i]) > tolMm) return false;
            return true;
        }
    }

    /// <summary>Uma nervura da ponta: duas faces planas verticais paralelas, de normais opostas, com cobre entre elas.</summary>
    public sealed class RibMeasure
    {
        public char Axis;
        /// <summary>Plano da face de normal +eixo (lado de cima) e da de normal −eixo (lado de baixo), mm.</summary>
        public double High, Low;
        public double Width => High - Low;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "nervura em {0}: {1:0.####} … {2:0.####} (largura {3:0.####} mm)", Axis == 'X' ? "x" : "y", Low, High, Width);
    }

    /// <summary>
    /// T3 do "Criar Eletrodos" — o GAP dentro do fluxo. As faces que vieram da peça (abaixo do plano de fechamento, menos
    /// as laterais do prisma e as divisas da T2) recuam o GAP do Ra. <c>Model.FaceOffsets</c> só existe em ORDENADO: a
    /// peça do eletrodo (criada agora pela própria ferramenta — a única exceção à regra de não trocar o ambiente) é
    /// construída toda em síncrono e trocada para ordenado no fim. A troca reconstrói o corpo e mata as faces lidas antes:
    /// as faces de queima são guardadas pela GEOMETRIA (<see cref="FaceSig"/>) e reencontradas depois da troca.
    /// </summary>
    public static class ElectrodeGap
    {
        private const int igPlane = -1909484335;
        private const uint igFeatureOK = 1216476310;

        /// <summary>
        /// (FaceOffsetType, BlendRecreation) em ordem. A 1ª é a do "Aplicar GAP", lida de um offset feito à mão
        /// (igFaceOffsetBySynchronousOffset, igIgnoreBlends); as outras: igFaceOffsetByOffset = 2, igRecreateBlends = 193.
        /// </summary>
        private static readonly int[][] Variants = { new[] { 1, 194 }, new[] { 1, 193 }, new[] { 2, 194 }, new[] { 2, 193 } };

        // ------------------------------------------------------------------ lógica pura

        /// <summary>
        /// A face recua? Só a que vem da peça: tem algum ponto abaixo do fechamento (<paramref name="refZ"/>) e não é a
        /// face plana vertical do corte do prisma (lateral da pegada ou divisa, em x = ±<paramref name="halfX"/> ou
        /// y = ±<paramref name="halfY"/>, a <paramref name="tolMm"/>; a faixa da T2c deixa a divisa 0,002 mm para dentro).
        /// </summary>
        public static bool IsBurnFace(bool planar, double[] mn, double[] mx, double refZ, double halfX, double halfY, double tolMm = 0.005)
        {
            if (mn[2] >= refZ - 1e-3) return false;
            if (!planar) return true;
            bool onX = mx[0] - mn[0] < 1e-4 && (Math.Abs(mx[0] - halfX) <= tolMm || Math.Abs(mn[0] + halfX) <= tolMm);
            bool onY = mx[1] - mn[1] < 1e-4 && (Math.Abs(mx[1] - halfY) <= tolMm || Math.Abs(mn[1] + halfY) <= tolMm);
            return !(onX || onY);
        }

        /// <summary>
        /// A nervura mais estreita entre as faces dadas: uma de normal para fora +eixo no plano <c>High</c>, outra de normal
        /// −eixo no plano <c>Low</c> &lt; High, que se sobrepõem (mais de <paramref name="minOverlapMm"/>) na outra direção
        /// horizontal e em Z. Com <paramref name="near"/>, só a que fica a até <paramref name="nearTolMm"/> dela (mesmo eixo).
        /// </summary>
        public static RibMeasure NarrowestRib(IEnumerable<FaceSig> faces, RibMeasure near = null, double nearTolMm = 0.3,
            double minOverlapMm = 0.1, double minWidthMm = 0.05)
        {
            // paredes verticais ou com saída (até ~5,7°): a posição é o meio da caixa na direção da normal
            List<FaceSig> planes = faces.Where(f => f.OutwardNormal != null && Math.Abs(f.OutwardNormal[2]) < 0.1).ToList();
            RibMeasure best = null;
            foreach (char axis in new[] { 'X', 'Y' })
            {
                int a = axis == 'X' ? 0 : 1, b = 1 - a;
                var hi = planes.Where(f => f.OutwardNormal[a] > 0.99).ToList();
                var lo = planes.Where(f => f.OutwardNormal[a] < -0.99).ToList();
                foreach (FaceSig h in hi)
                    foreach (FaceSig l in lo)
                    {
                        double ph = (h.Min[a] + h.Max[a]) / 2.0, pl = (l.Min[a] + l.Max[a]) / 2.0, w = ph - pl;
                        if (w < minWidthMm) continue;
                        if (Math.Min(h.Max[b], l.Max[b]) - Math.Max(h.Min[b], l.Min[b]) < minOverlapMm) continue;
                        if (Math.Min(h.Max[2], l.Max[2]) - Math.Max(h.Min[2], l.Min[2]) < minOverlapMm) continue;
                        var r = new RibMeasure { Axis = axis, High = ph, Low = pl };
                        if (near != null && (near.Axis != axis || Math.Abs(r.High - near.High) > nearTolMm || Math.Abs(r.Low - near.Low) > nearTolMm)) continue;
                        if (best == null || r.Width < best.Width) best = r;
                    }
            }
            return best;
        }

        /// <summary>As faces de <paramref name="after"/> que batem com alguma de <paramref name="burn"/> (mesmo tipo e caixa).</summary>
        public static List<int> Match(IList<FaceSig> burn, IList<FaceSig> after, double tolMm = 2e-4)
        {
            var hits = new List<int>();
            for (int i = 0; i < after.Count; i++)
                if (burn.Any(b => b.SameAs(after[i], tolMm))) hits.Add(i);
            return hits;
        }

        // ------------------------------------------------------------------ Solid Edge

        /// <summary>Todas as faces do 1º corpo, com tipo e caixa exata (mm); <paramref name="coms"/> recebe os objetos COM.</summary>
        public static List<FaceSig> ReadFaces(object partDoc, List<object> coms = null)
        {
            var list = new List<FaceSig>();
            dynamic faces = ((dynamic)partDoc).Models.Item(1).Body.Faces(1);   // igQueryAll
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                object f = faces.Item(i);
                if (!FaceGeometry.TryGetExactRangeMm(f, out double[] mn, out double[] mx)) continue;
                int type = 0;
                try { type = (int)((dynamic)f).Geometry.Type; } catch { }
                var s = new FaceSig { Type = type, Min = mn, Max = mx };
                try { s.AreaMm2 = (double)((dynamic)f).Area * 1e6; } catch { }
                if (type == igPlane && EdmAssemblyBuilder.TryOutwardNormal(f, out double[] nv))
                {
                    double l = Math.Sqrt(nv[0] * nv[0] + nv[1] * nv[1] + nv[2] * nv[2]);
                    s.OutwardNormal = new[] { nv[0] / l, nv[1] / l, nv[2] / l };
                }
                list.Add(s);
                coms?.Add(f);
            }
            return list;
        }

        /// <summary>As faces de queima da ponta, lidas pela geometria (antes da base e da troca de ambiente).</summary>
        public static List<FaceSig> BurnFaces(object partDoc, double refZ, double halfX, double halfY) =>
            ReadFaces(partDoc).Where(f => IsBurnFace(f.Type == igPlane, f.Min, f.Max, refZ, halfX, halfY)).ToList();

        /// <summary>O que o GAP fez numa peça.</summary>
        public sealed class Result
        {
            public bool Ok;
            public string Failure;
            public double GapMm, Ra;
            public int Faces, Matched;
            public uint Status;
            public string FeatureName, Variant;
            public double VolumeBeforeMm3, VolumeAfterMm3, SwitchSeconds, ExpectedRemovedMm3;
            public readonly List<string> Notes = new List<string>();
        }

        /// <summary>
        /// Troca a peça (nova, desta ferramenta) para ORDENADO, reencontra as faces de queima pela geometria e aplica
        /// <c>Model.FaceOffsets.AddEx</c> com −GAP (a mesma chamada, já validada, do "Aplicar GAP"), nomeando a feature
        /// "GAP: g - Ra: r". Nunca lança.
        /// </summary>
        public static Result Apply(dynamic partDoc, IList<FaceSig> burn, double gapMm, double ra, string tag)
        {
            var r = new Result { GapMm = gapMm, Ra = ra, Faces = burn.Count };
            try
            {
                r.VolumeBeforeMm3 = ElectrodeTipBuilder.Volume(partDoc);
                int mode = 0; try { mode = (int)partDoc.ModelingMode; } catch { }
                // Síncrono não tem recuo de faces (medido 2026-11-13: FaceOffsets.AddEx em peça síncrona dá E_POINTER /
                // DISP_E_TYPEMISMATCH e não cria nada); por isso a troca.
                if (mode != 2)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    partDoc.ModelingMode = 2;   // peça recém-criada pela ferramenta: a única exceção à regra do ambiente
                    r.SwitchSeconds = sw.Elapsed.TotalSeconds;
                }
                var choice = new RaGapPresets.Choice(ra, gapMm, Color.Empty);
                foreach (int[] v in Variants)
                {
                    var coms = new List<object>();
                    List<FaceSig> after = ReadFaces((object)partDoc, coms);
                    List<int> hit = Match(burn, after);
                    r.Matched = hit.Count;
                    if (hit.Count == 0) { r.Failure = "nenhuma face de queima reencontrada depois da troca para ordenado."; return r; }
                    object model = (object)partDoc.Models.Item(1);
                    object feature = SurfaceBlockBuilder.TryApplyGapOffset(hit.Select(i => coms[i]).ToList(), choice, model, v[0], v[1]);
                    uint status = 0;
                    if (feature != null) { try { status = Convert.ToUInt32(((dynamic)feature).Status); } catch { } }
                    double vol = ElectrodeTipBuilder.Volume(partDoc);
                    string what = $"FaceOffsetType {v[0]}, BlendRecreation {v[1]}";
                    // Guarda (medido no 15428.100, 2026-11-13): com status OK o recuo chegou a cortar o corpo inteiro por um plano
                    // (−1.409,8 mm³ num pedaço de ponta de 186 mm³). O recuo não pode tirar muito mais que área × GAP.
                    double expected = hit.Sum(i => after[i].AreaMm2) * gapMm, removed = r.VolumeBeforeMm3 - vol;
                    r.ExpectedRemovedMm3 = expected;
                    if (feature != null && status == igFeatureOK && removed > 1.5 * expected + 0.05)
                    {
                        r.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0}: status OK mas tirou {1:0.###} mm³ (área das faces × GAP = {2:0.###}): recuo desfeito.", $"FaceOffsetType {v[0]}, BlendRecreation {v[1]}", removed, expected));
                        Log.Warn($"{tag}: GAP — {r.Notes[r.Notes.Count - 1]}");
                        try { ((dynamic)feature).Delete(); } catch (Exception ex) { r.Notes.Add("apagar o recuo errado: " + ex.GetBaseException().Message); }
                        r.Failure = "o recuo saiu com status OK mas cortou o corpo além das faces de queima (desfeito).";
                        return r;
                    }
                    if (feature != null && status == igFeatureOK && vol < r.VolumeBeforeMm3 - 1e-6)
                    {
                        r.Status = status; r.VolumeAfterMm3 = vol; r.Ok = true; r.Variant = what;
                        try { r.FeatureName = (string)((dynamic)feature).Name; } catch { }
                        break;
                    }
                    r.Notes.Add($"{what}: {(feature == null ? "sem feature" : "status " + status + " (" + StatusDescription(feature) + ")")}, volume {r.VolumeBeforeMm3:0.###} → {vol:0.###} mm³");
                    Log.Info($"{tag}: GAP com {what} não vingou — {r.Notes[r.Notes.Count - 1]}");
                    if (feature != null) { try { ((dynamic)feature).Delete(); } catch (Exception ex) { r.Notes.Add("apagar a feature que falhou: " + ex.GetBaseException().Message); } }
                }
                if (r.Matched < burn.Count)
                    r.Notes.Add($"{burn.Count - r.Matched} de {burn.Count} face(s) de queima não reencontrada(s) depois da troca para ordenado.");
                // Medido no 15428.100 (2026-11-13): as 4 variantes deram igFeatureFailed — também com 1 face só (o piso mais
                // fundo) e com 0,001 mm; GetStatusEx não traz descrição. Ver o relatório da T3.
                if (!r.Ok) { r.Failure = "nenhuma variante do FaceOffsets.AddEx vingou (igFeatureFailed)."; return r; }
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "{0}: GAP {1:0.00} mm (Ra {2:0.0}) em {3}/{4} face(s), '{5}' ({10}) status {6}; volume {7:0.###} → {8:0.###} mm³; troca para ordenado {9:0.0} s.",
                    tag, gapMm, ra, r.Matched, burn.Count, r.FeatureName, r.Status, r.VolumeBeforeMm3, r.VolumeAfterMm3, r.SwitchSeconds, r.Variant));
            }
            catch (Exception ex) { r.Failure = ex.GetBaseException().Message; Log.Warn($"{tag}: GAP falhou — {r.Failure}"); }
            return r;
        }

        /// <summary>O texto do <c>GetStatusEx([opt][out] Description)</c> de uma feature (dump do SE 2023). "?" se ilegível.</summary>
        internal static string StatusDescription(object feature)
        {
            try
            {
                object[] args = { null };
                var mod = new System.Reflection.ParameterModifier(1); mod[0] = true;
                object st = feature.GetType().InvokeMember("GetStatusEx", System.Reflection.BindingFlags.InvokeMethod, null, feature, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                return $"GetStatusEx {st}: {args[0] ?? "(sem descrição)"}";
            }
            catch (Exception ex) { return "GetStatusEx: " + ex.GetBaseException().Message; }
        }

        /// <summary>O ponto mais fundo da ponta (mm): o menor Z das caixas exatas das faces.</summary>
        public static double DeepestZ(IEnumerable<FaceSig> faces) => faces.Min(f => f.Min[2]);
    }
}
