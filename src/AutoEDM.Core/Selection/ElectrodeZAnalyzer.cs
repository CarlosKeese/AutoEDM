using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>Limiares (calibráveis) da segmentação por níveis de Z.</summary>
    public sealed class ZSegmentationParams
    {
        /// <summary>
        /// Tolerância de NÍVEL (mm): faces cujo fundo (MinZ) difere &lt;= isto ficam no
        /// mesmo nível de Z. Regra do Carlos: superfícies no mesmo nível = 1 eletrodo.
        /// Aumentar funde níveis próximos (ex.: -13,7 e -12,3 distam 1,4 mm).
        /// </summary>
        public double LevelGapZmm { get; set; } = 1.0;

        /// <summary>
        /// Alcance XY (mm) dentro de um nível: duas regiões do mesmo nível a uma folga
        /// &lt;= isto viram o MESMO eletrodo (Carlos: "se a distância estiver dentro do
        /// tamanho dos blanks"). Default ~ maior blank útil. Calibrável.
        /// </summary>
        public double SameElectrodeReachMm { get; set; } = 50.0;

        /// <summary>Só p/ diagnóstico: face é "plana" (piso) se ΔZ &lt;= isto (mm). Não afeta o agrupamento.</summary>
        public double FlatMaxZmm { get; set; } = 1.0;
    }

    /// <summary>Um eletrodo proposto pela análise de Z (posição, sem geometria criada).</summary>
    public sealed class ProposedElectrode
    {
        public int Index { get; set; }
        public int FaceCount { get; set; }
        public double LevelZmm { get; set; }
        public double CenterXmm { get; set; }
        public double CenterYmm { get; set; }
        /// <summary>Z mais fundo (MinZ) do bolsão — onde a origem do eletrodo encosta.</summary>
        public double DeepestZmm { get; set; }
        /// <summary>Z mais alto (MaxZ) das faces — topo do bolsão. Altura da queima = Topo - Fundo.</summary>
        public double TopZmm { get; set; }
        public double FootprintXmm { get; set; }
        public double FootprintYmm { get; set; }
    }

    public sealed class ZAnalysisResult
    {
        public List<ProposedElectrode> Electrodes { get; } = new List<ProposedElectrode>();
        public int TotalFaces { get; set; }
        public int FacesWithBox { get; set; }
        public int FlatFaces { get; set; }
        public int SteepFaces { get; set; }
        public List<string> Warnings { get; } = new List<string>();

        // --- Detecção de QUEIMA (para conferir antes de criar — Log 57) ---------
        /// <summary>Cor mapeada usada como queima (a de mais faces). Vazia se nada mapeado.</summary>
        public Color BurnColor { get; set; }
        /// <summary>Ra da cor de queima escolhida.</summary>
        public double BurnRa { get; set; }
        /// <summary>Nº de faces na cor de queima.</summary>
        public int BurnFaceCount { get; set; }
        /// <summary>Histograma de TODAS as cores na peça-alvo (mapeadas e não), maior primeiro.</summary>
        public List<ColorTally> ColorTally { get; } = new List<ColorTally>();

        /// <summary>Existe uma cor NÃO mapeada com MAIS faces que a queima detectada? Sinal
        /// forte de que a queima real foi pintada numa cor fora do mapa (Log 57: roxo 132 &gt;
        /// vermelho 52) — a detecção provavelmente pegou a região errada.</summary>
        public bool HasDominantUnmappedColor
        {
            get
            {
                var topUnmapped = ColorTally.Where(t => !t.Mapped).OrderByDescending(t => t.FaceCount).FirstOrDefault();
                return topUnmapped != null && topUnmapped.FaceCount > BurnFaceCount;
            }
        }

        /// <summary>Texto p/ a confirmação "conferir antes de criar": cores detectadas +
        /// nº de eletrodos + aviso se a maior região colorida não estiver mapeada.</summary>
        public string DescribeBurnDetection()
        {
            var sb = new System.Text.StringBuilder();
            if (BurnFaceCount > 0)
                sb.AppendLine($"Queima detectada: RGB({BurnColor.R},{BurnColor.G},{BurnColor.B}) → Ra {BurnRa:0.0} — {BurnFaceCount} face(s).");
            else
                sb.AppendLine("Nenhuma cor de queima MAPEADA foi encontrada.");

            sb.AppendLine($"Eletrodos propostos: {Electrodes.Count}.");
            foreach (var e in Electrodes)
                sb.AppendLine($"  • D{e.Index:00}: pegada {e.FootprintXmm:0.0}×{e.FootprintYmm:0.0} mm, fundo Z={e.DeepestZmm:0.0}.");

            var unmapped = ColorTally.Where(t => !t.Mapped).OrderByDescending(t => t.FaceCount).Take(3).ToList();
            if (unmapped.Count > 0)
            {
                sb.AppendLine("Cores NÃO mapeadas na peça (maiores):");
                foreach (var t in unmapped)
                    sb.AppendLine($"  • RGB({t.Color.R},{t.Color.G},{t.Color.B}): {t.FaceCount} face(s)");
            }
            if (HasDominantUnmappedColor)
                sb.AppendLine("\n⚠ A MAIOR região colorida NÃO está mapeada — a queima real pode estar numa cor fora do mapa. Confira antes de criar.");
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Segmenta as faces de queima em ELETRODOS por níveis de Z (ideia do Carlos):
    /// faces "planas" (extensão em Z pequena = "4 pontos na mesma altura") são fundos
    /// de bolsão = candidatos a eletrodo; faces "íngremes" (extensão em Z grande) são
    /// paredes. Conta-se os pisos distintos (agrupados por proximidade XY + nível de
    /// Z) — cada um é um eletrodo, posicionado no centro XY do piso e no Z mais fundo.
    ///
    /// A conectividade/proximidade pura NÃO separa isto (um patch contínuo de queima
    /// pode conter vários bolsões — Runs 6/7). Este critério é o que distingue.
    ///
    /// NÃO cria geometria — só analisa e propõe (passo 1, não-destrutivo). O run
    /// revela a distribuição real de Z para calibrar os limiares.
    /// </summary>
    public sealed class ElectrodeZAnalyzer
    {
        public ZAnalysisResult Analyze(IReadOnlyList<SelectedFace> faces, ZSegmentationParams prm)
        {
            prm = prm ?? new ZSegmentationParams();
            var result = new ZAnalysisResult { TotalFaces = faces?.Count ?? 0 };
            if (faces == null || faces.Count == 0)
            {
                result.Warnings.Add("Nenhuma face de queima para analisar.");
                return result;
            }

            int n = faces.Count;
            var min = new double[n][];
            var max = new double[n][];
            var has = new bool[n];
            for (int i = 0; i < n; i++)
                if (FaceGeometry.TryGetRangeMm(faces[i].ComFace, out min[i], out max[i])) has[i] = true;

            result.FacesWithBox = has.Count(b => b);
            Log.Info($"Análise Z: bounding box lido em {result.FacesWithBox}/{n} face(s). " +
                     $"Limiares: nível Z<={F(prm.LevelGapZmm)}mm, alcance XY<={F(prm.SameElectrodeReachMm)}mm.");
            if (result.FacesWithBox == 0)
            {
                result.Warnings.Add("Nenhum bounding box lido — análise de Z impossível.");
                Log.Warn(result.Warnings.Last());
                return result;
            }

            LogZEvidence(faces, min, max, has, prm);

            // Diagnóstico (não afeta o agrupamento): plano x parede.
            for (int i = 0; i < n; i++)
            {
                if (!has[i]) continue;
                if (max[i][2] - min[i][2] <= prm.FlatMaxZmm) result.FlatFaces++; else result.SteepFaces++;
            }
            Log.Info($"Análise Z (diag): {result.FlatFaces} face(s) plana(s) x {result.SteepFaces} íngreme(s).");

            // 1) NÍVEL de Z (regra do Carlos): níveis de fundo DISTINTOS = eletrodos
            //    distintos, mesmo colados. Agrupa por ÂNCORA FIXA (compara com o 1º MinZ
            //    do nível, não com o anterior) — assim as paredes com fundos
            //    intermediários NÃO encadeiam níveis próximos (ex.: -14,9/-13,7/-12,3
            //    ficam separados). Escolha do Carlos (Log 32).
            var order = new List<int>();
            for (int i = 0; i < n; i++) if (has[i]) order.Add(i);
            order.Sort((a, b) => min[a][2].CompareTo(min[b][2]));

            var levelOf = new Dictionary<int, int>();
            int levelId = 0;
            double anchorZ = min[order[0]][2];
            foreach (int i in order)
            {
                if (min[i][2] - anchorZ > prm.LevelGapZmm) { levelId++; anchorZ = min[i][2]; }
                levelOf[i] = levelId;
            }
            int levelCount = levelId + 1;

            // 2) Dentro do nível: une o que está a <= SameElectrodeReachMm em XY
            //    (cabe num blank -> mesmo eletrodo).
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            for (int a = 0; a < order.Count; a++)
                for (int b = a + 1; b < order.Count; b++)
                {
                    int i = order[a], j = order[b];
                    if (levelOf[i] != levelOf[j]) continue;
                    if (XyGap(min[i], max[i], min[j], max[j]) <= prm.SameElectrodeReachMm)
                        Union(parent, i, j);
                }

            var byRoot = new Dictionary<int, List<int>>();
            foreach (int i in order)
            {
                int r = Find(parent, i);
                if (!byRoot.TryGetValue(r, out var list)) byRoot[r] = list = new List<int>();
                list.Add(i);
            }

            int idx = 0;
            foreach (var cluster in byRoot.Values.OrderBy(c => min[c[0]][2]))
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, deep = double.MaxValue, top = double.MinValue;
                foreach (int i in cluster)
                {
                    minX = Math.Min(minX, min[i][0]); maxX = Math.Max(maxX, max[i][0]);
                    minY = Math.Min(minY, min[i][1]); maxY = Math.Max(maxY, max[i][1]);
                    deep = Math.Min(deep, min[i][2]); top = Math.Max(top, max[i][2]);
                }
                result.Electrodes.Add(new ProposedElectrode
                {
                    Index = ++idx,
                    FaceCount = cluster.Count,
                    LevelZmm = deep,
                    CenterXmm = (minX + maxX) / 2.0,
                    CenterYmm = (minY + maxY) / 2.0,
                    DeepestZmm = deep,
                    TopZmm = top,
                    FootprintXmm = maxX - minX,
                    FootprintYmm = maxY - minY
                });
            }

            Log.Info($"Análise Z: {levelCount} nível(is) de Z -> {result.Electrodes.Count} ELETRODO(S) proposto(s) " +
                     $"(alcance XY {F(prm.SameElectrodeReachMm)}mm).");
            Log.Info("Idx  Faces      X          Y        Fundo Z    PegX    PegY");
            Log.Info("---  -----  ---------  ---------  ---------  ------  ------");
            foreach (var e in result.Electrodes)
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "{0,3}  {1,5}  {2,9}  {3,9}  {4,9}  {5,6}  {6,6}",
                    e.Index, e.FaceCount, F(e.CenterXmm), F(e.CenterYmm), F(e.DeepestZmm),
                    F(e.FootprintXmm), F(e.FootprintYmm)));

            return result;
        }

        /// <summary>Loga a evidência bruta de Z (para calibrar os limiares).</summary>
        private static void LogZEvidence(IReadOnlyList<SelectedFace> faces, double[][] min, double[][] max, bool[] has, ZSegmentationParams prm)
        {
            // Distribuição de ΔZ (plano x parede).
            double[] bkt = { 0.5, 1, 2, 5, 10, 20 };
            var cnt = new int[bkt.Length + 1];
            double sMin = double.MaxValue, sMax = double.MinValue;
            for (int i = 0; i < faces.Count; i++)
            {
                if (!has[i]) continue;
                double sz = max[i][2] - min[i][2];
                sMin = Math.Min(sMin, sz); sMax = Math.Max(sMax, sz);
                int b = 0; while (b < bkt.Length && sz > bkt[b]) b++;
                cnt[b]++;
            }
            var parts = new List<string>();
            for (int b = 0; b < bkt.Length; b++) parts.Add($"<={F(bkt[b])}:{cnt[b]}");
            parts.Add($">{F(bkt[bkt.Length - 1])}:{cnt[bkt.Length]}");
            Log.Info($"Análise Z: ΔZ por face (mm) — min={F(sMin)}, max={F(sMax)}; buckets [{string.Join(", ", parts)}].");

            // Níveis de Z distintos (agrupa MinZ das faces com tolerância LevelGapZmm).
            var mins = new List<double>();
            for (int i = 0; i < faces.Count; i++) if (has[i]) mins.Add(min[i][2]);
            mins.Sort();
            var levels = new List<KeyValuePair<double, int>>(); // (nível, contagem)
            foreach (double z in mins)
            {
                if (levels.Count > 0 && Math.Abs(z - levels[levels.Count - 1].Key) <= prm.LevelGapZmm)
                    levels[levels.Count - 1] = new KeyValuePair<double, int>(levels[levels.Count - 1].Key, levels[levels.Count - 1].Value + 1);
                else
                    levels.Add(new KeyValuePair<double, int>(z, 1));
            }
            Log.Info($"Análise Z: {levels.Count} nível(is) de fundo distinto(s) (MinZ, tol {F(prm.LevelGapZmm)}mm): " +
                     string.Join(", ", levels.Select(l => $"{F(l.Key)}mm×{l.Value}")));
        }

        private static double XyGap(double[] minA, double[] maxA, double[] minB, double[] maxB)
        {
            double dx = AxisSep(minA[0], maxA[0], minB[0], maxB[0]);
            double dy = AxisSep(minA[1], maxA[1], minB[1], maxB[1]);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double AxisSep(double aMin, double aMax, double bMin, double bMax)
        {
            if (bMin > aMax) return bMin - aMax;
            if (aMin > bMax) return aMin - bMax;
            return 0;
        }

        private static int Find(int[] p, int x) { while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; } return x; }
        private static void Union(int[] p, int a, int b) { int ra = Find(p, a), rb = Find(p, b); if (ra != rb) p[ra] = rb; }
        private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
