using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// Miniatura ISOMÉTRICA do eletrodo para a impressão da Lista de corte (Carlos, 2026-09-14).
    /// Os eletrodos são modelados sempre de pé no eixo Z com a figura de queima EMBAIXO, então a
    /// câmera olha DE BAIXO (lado Z−), vinda do canto X+ Y− — o do chanfro de orientação —, com +Z
    /// para cima na imagem: as faces de baixo, onde está a figura, ficam visíveis. Sem cota: a
    /// altura Z já está na tabela.
    ///
    /// Entrada = malha de triângulos em mm (9 doubles por faceta, uma lista por face — o formato do
    /// <c>Face.GetFacetData</c> já convertido). Desenho por Z-BUFFER, não pelo algoritmo do pintor:
    /// ordenar pelo centro da faceta erra com os triângulos compridos que as faces planas do SE geram
    /// (a 1ª versão pintava faixas escuras atravessando o bloco). Sombreamento chapado de dois lados
    /// (a orientação das facetas do SE não é garantida), superamostragem 2× para suavizar e contorno
    /// das ARESTAS VIVAS — borda da malha de cada face ou dobra acima de
    /// <see cref="DefaultFeatureAngleDeg"/> —, com o mesmo teste de profundidade (aresta escondida não
    /// aparece). É o contorno que deixa a forma legível numa impressora P&amp;B. Lógica pura, sem COM.
    /// </summary>
    public static class ElectrodeThumbnail
    {
        public const int DefaultSizePx = 360;
        public const double DefaultFeatureAngleDeg = 30.0;

        private const double Ambient = 0.45;
        private const double Margin = 0.06;
        private const int Supersample = 2;
        /// <summary>Vértices a menos de 1 µm são o mesmo vértice (faces vizinhas repetem as coordenadas da borda).</summary>
        private const double VertexQuantumMm = 0.001;

        private static readonly Color Copper = Color.FromArgb(205, 128, 72);
        private static readonly Color EdgeColor = Color.FromArgb(60, 38, 22);
        /// <summary>Cor das faces da revisão — o mesmo roxo que o Carlos usa à mão na folha MD.</summary>
        private static readonly Color HighlightColor = Color.FromArgb(198, 76, 196);
        private static readonly Color CalloutColor = Color.FromArgb(200, 0, 0);

        /// <summary>
        /// Com destaques, quanto a peça SEM modificação é puxada para o branco (0 = cor cheia,
        /// 1 = some). Ela vira fundo e as faces alteradas saltam (Carlos, 2026-10-02: "um pouco
        /// mais transparentes nas faces sem modificação").
        /// </summary>
        private const double PlainWash = 0.6;

        /// <summary>
        /// Opacidade da face alterada que fica ATRÁS da peça, vista através dela: a peça é
        /// "transparente", então a alteração do lado escondido também aparece, mais apagada que a
        /// da frente para não confundir os lados.
        /// </summary>
        private const double HiddenHighlightAlpha = 0.55;

        /// <summary>
        /// Uma FEATURE da revisão a destacar: as faces dela saem em roxo e ganham uma chamada
        /// numerada apontando para o meio delas — é o que o Carlos desenha à mão hoje (2026-09-18).
        /// </summary>
        public sealed class HighlightGroup
        {
            /// <summary>O que aparece na chamada: "1", "2", "3"...</summary>
            public string Label { get; set; }

            /// <summary>Índices das MALHAS (uma por face) que pertencem a esta feature.</summary>
            public List<int> MeshIndices { get; set; } = new List<int>();
        }

        /// <summary>
        /// De onde se olha a peça. O ELETRODO é olhado de BAIXO (a figura de queima fica embaixo —
        /// regra de 2026-09-14); peça de molde na Lista de modificações é olhada de CIMA, como
        /// qualquer isométrica de desenho (Carlos, 2026-09-18).
        /// </summary>
        public enum View
        {
            /// <summary>Câmera em X+ Y− ABAIXO — o padrão do eletrodo.</summary>
            FromBelow,
            /// <summary>Câmera em X+ Y− ACIMA — isométrica comum.</summary>
            FromAbove
        }

        /// <summary>Base ortonormal destra da câmera (Right × Up = Camera) + a luz daquela vista.</summary>
        private sealed class Basis
        {
            public Vec Camera, Right, Up, Light;

            public static Basis Of(View view)
            {
                Vec camera = view == View.FromAbove ? Vec.Norm(1, -1, 1) : Vec.Norm(1, -1, -1);
                Vec right = Vec.Norm(1, 1, 0);
                // +Z do modelo aponta para CIMA na tela nas duas vistas (a tela tem y para baixo).
                Vec up = view == View.FromAbove ? Vec.Norm(-1, 1, 2) : Vec.Norm(1, -1, 2);
                return new Basis
                {
                    Camera = camera,
                    Right = right,
                    Up = up,
                    // Luz fora do eixo da vista: numa iso as três faces de um cubo ficariam com o MESMO tom.
                    Light = (right * 0.3 + up * -0.3 + camera * 0.9).Normalized(),
                };
            }
        }

        /// <summary>Uma faceta já projetada.</summary>
        public sealed class Facet
        {
            /// <summary>Vértices na tela, em mm (x → direita, y → BAIXO, como no bitmap).</summary>
            public PointF A, B, C;
            /// <summary>Profundidade de cada vértice ao longo da vista: MAIOR = mais perto da câmera.</summary>
            public double DepthA, DepthB, DepthC;
            /// <summary>Profundidade do centro da faceta.</summary>
            public double Depth => (DepthA + DepthB + DepthC) / 3.0;
            /// <summary>Intensidade da luz, <see cref="Ambient"/>..1.</summary>
            public double Shade;
            /// <summary>Índice do <see cref="HighlightGroup"/> a que a face pertence; -1 = peça comum.</summary>
            public int Group = -1;

            /// <summary>Aresta viva a contornar (A-B, B-C, C-A).</summary>
            public bool EdgeAB, EdgeBC, EdgeCA;
        }

        /// <summary>Projeta a malha; ordenada do mais longe para o mais perto (só p/ inspeção — o desenho usa Z-buffer).</summary>
        public static List<Facet> Project(IEnumerable<double[]> meshesMm, double featureAngleDeg = DefaultFeatureAngleDeg,
            View view = View.FromBelow, IReadOnlyList<int> meshGroups = null)
        {
            Basis basis = Basis.Of(view);
            var tris = new List<Tri>();
            int meshIndex = -1;
            foreach (double[] mesh in meshesMm ?? Enumerable.Empty<double[]>())
            {
                meshIndex++;
                if (mesh == null) continue;
                int group = meshGroups != null && meshIndex < meshGroups.Count ? meshGroups[meshIndex] : -1;
                for (int b = 0; b + 8 < mesh.Length; b += 9)
                {
                    var a = new Vec(mesh[b], mesh[b + 1], mesh[b + 2]);
                    var p = new Vec(mesh[b + 3], mesh[b + 4], mesh[b + 5]);
                    var c = new Vec(mesh[b + 6], mesh[b + 7], mesh[b + 8]);
                    Vec n = Vec.Cross(p - a, c - a);
                    if (n.Length < 1e-12) continue; // faceta degenerada
                    tris.Add(new Tri { V = new[] { a, p, c }, N = n.Normalized(), Group = group });
                }
            }

            // Arestas por vértice quantizado: a mesma aresta em 2 facetas quase coplanares não é contornada.
            var vertexIds = new Dictionary<long[], int>(new KeyComparer());
            var edges = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris.Count; t++)
            {
                tris[t].Ids = tris[t].V.Select(v => VertexId(vertexIds, v)).ToArray();
                for (int k = 0; k < 3; k++)
                {
                    long key = EdgeKey(tris[t].Ids[k], tris[t].Ids[(k + 1) % 3]);
                    if (!edges.TryGetValue(key, out List<int> owners)) edges[key] = owners = new List<int>(2);
                    owners.Add(t);
                }
            }

            double cosLimit = Math.Cos(featureAngleDeg * Math.PI / 180.0);
            var facets = new List<Facet>(tris.Count);
            for (int t = 0; t < tris.Count; t++)
            {
                Tri tri = tris[t];
                var flags = new bool[3];
                for (int k = 0; k < 3; k++)
                {
                    List<int> owners = edges[EdgeKey(tri.Ids[k], tri.Ids[(k + 1) % 3])];
                    int other = owners.Count == 2 ? (owners[0] == t ? owners[1] : owners[0]) : -1;
                    // |n·n|: sem orientação confiável, dobra = ângulo entre as RETAS normais.
                    flags[k] = other < 0 || Math.Abs(Vec.Dot(tri.N, tris[other].N)) < cosLimit;
                }

                facets.Add(new Facet
                {
                    A = ToScreen(tri.V[0], basis),
                    B = ToScreen(tri.V[1], basis),
                    C = ToScreen(tri.V[2], basis),
                    DepthA = Vec.Dot(tri.V[0], basis.Camera),
                    DepthB = Vec.Dot(tri.V[1], basis.Camera),
                    DepthC = Vec.Dot(tri.V[2], basis.Camera),
                    Shade = Ambient + (1 - Ambient) * Math.Abs(Vec.Dot(tri.N, basis.Light)),
                    EdgeAB = flags[0],
                    EdgeBC = flags[1],
                    EdgeCA = flags[2],
                    Group = tri.Group,
                });
            }
            return facets.OrderBy(f => f.Depth).ToList();
        }

        /// <summary>
        /// Miniatura quadrada, fundo branco, peça centrada. Null se a malha estiver vazia.
        /// <paramref name="highlights"/> pinta de roxo as faces das features da revisão e desenha a
        /// chamada numerada de cada uma — a folha de revisões precisa mostrar O QUE mudou, não só a
        /// peça (Carlos, 2026-09-18).
        /// </summary>
        public static Bitmap Render(IEnumerable<double[]> meshesMm, int sizePx = DefaultSizePx,
            View view = View.FromBelow, IReadOnlyList<HighlightGroup> highlights = null)
        {
            List<double[]> meshes = (meshesMm ?? Enumerable.Empty<double[]>()).ToList();
            int[] meshGroups = MeshGroups(meshes.Count, highlights);
            List<Facet> facets = Project(meshes, DefaultFeatureAngleDeg, view, meshGroups);
            if (facets.Count == 0 || sizePx <= 0) return null;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            double minD = double.MaxValue, maxD = double.MinValue;
            foreach (Facet f in facets)
            {
                foreach (PointF p in new[] { f.A, f.B, f.C })
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
                minD = Math.Min(minD, Math.Min(f.DepthA, Math.Min(f.DepthB, f.DepthC)));
                maxD = Math.Max(maxD, Math.Max(f.DepthA, Math.Max(f.DepthB, f.DepthC)));
            }

            int s = sizePx * Supersample;
            double span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-6);
            double scale = s * (1 - 2 * Margin) / span;
            double offX = (s - (maxX - minX) * scale) / 2 - minX * scale;
            double offY = (s - (maxY - minY) * scale) / 2 - minY * scale;

            var zbuf = new double[s * s];
            var rgb = new int[s * s];
            // Quem ficou VISÍVEL em cada pixel: é o que decide se uma feature ganha chamada e onde.
            var gbuf = new int[s * s];
            for (int i = 0; i < zbuf.Length; i++) { zbuf[i] = double.NegativeInfinity; rgb[i] = 0xFFFFFF; gbuf[i] = -1; }

            foreach (Facet f in facets)
                FillTriangle(zbuf, rgb, gbuf, s,
                    f.A.X * scale + offX, f.A.Y * scale + offY, f.DepthA,
                    f.B.X * scale + offX, f.B.Y * scale + offY, f.DepthB,
                    f.C.X * scale + offX, f.C.Y * scale + offY, f.DepthC,
                    ShadedRgb(f.Shade, f.Group >= 0 ? HighlightColor : Copper), f.Group);

            // Raio-X das faces alteradas: um z-buffer SÓ delas, sem a peça na frente. É o que
            // mostra a alteração do lado escondido através da peça esmaecida.
            bool xray = facets.Any(f => f.Group >= 0);
            double[] zbufH = null;
            int[] rgbH = null, gbufH = null;
            if (xray)
            {
                zbufH = new double[s * s];
                rgbH = new int[s * s];
                gbufH = new int[s * s];
                for (int i = 0; i < zbufH.Length; i++) { zbufH[i] = double.NegativeInfinity; gbufH[i] = -1; }
                foreach (Facet f in facets.Where(f => f.Group >= 0))
                    FillTriangle(zbufH, rgbH, gbufH, s,
                        f.A.X * scale + offX, f.A.Y * scale + offY, f.DepthA,
                        f.B.X * scale + offX, f.B.Y * scale + offY, f.DepthB,
                        f.C.X * scale + offX, f.C.Y * scale + offY, f.DepthC,
                        ShadedRgb(f.Shade, HighlightColor), f.Group);
            }

            // Arestas depois de TODAS as faces: o Z-buffer já sabe o que está na frente.
            double bias = Math.Max(maxD - minD, 1e-6) * 0.01;
            int edge = (EdgeColor.R << 16) | (EdgeColor.G << 8) | EdgeColor.B;
            int thickness = Math.Max(2, s / 300);
            foreach (Facet f in facets)
            {
                double ax = f.A.X * scale + offX, ay = f.A.Y * scale + offY;
                double bx = f.B.X * scale + offX, by = f.B.Y * scale + offY;
                double cx = f.C.X * scale + offX, cy = f.C.Y * scale + offY;
                if (f.EdgeAB) DrawEdge(zbuf, rgb, s, ax, ay, f.DepthA, bx, by, f.DepthB, edge, thickness, bias);
                if (f.EdgeBC) DrawEdge(zbuf, rgb, s, bx, by, f.DepthB, cx, cy, f.DepthC, edge, thickness, bias);
                if (f.EdgeCA) DrawEdge(zbuf, rgb, s, cx, cy, f.DepthC, ax, ay, f.DepthA, edge, thickness, bias);
            }

            if (xray) Composite(rgb, zbuf, gbuf, rgbH, gbufH);

            Bitmap bmp = Downsample(rgb, s, sizePx);
            // Chamadas pelo raio-X: a alteração escondida agora aparece, então também ganha balão.
            return WithCallouts(bmp, xray ? gbufH : gbuf, s, highlights);
        }

        /// <summary>Índice do grupo de cada malha (-1 = peça comum), a partir dos destaques.</summary>
        private static int[] MeshGroups(int meshCount, IReadOnlyList<HighlightGroup> highlights)
        {
            var groups = new int[meshCount];
            for (int i = 0; i < meshCount; i++) groups[i] = -1;
            if (highlights == null) return groups;

            for (int g = 0; g < highlights.Count; g++)
            {
                if (highlights[g]?.MeshIndices == null) continue;
                foreach (int mesh in highlights[g].MeshIndices)
                    if (mesh >= 0 && mesh < meshCount) groups[mesh] = g;
            }
            return groups;
        }

        /// <summary>
        /// A chamada numerada de cada feature: um balão com o número, ligado por uma linha ao meio
        /// do que se VÊ daquela feature. O centro e a própria existência da chamada saem dos pixels
        /// do <paramref name="gbuf"/> — com destaques, o do RAIO-X: a peça é desenhada esmaecida e a
        /// alteração do lado escondido aparece através dela, então também ganha balão (2026-10-02).
        /// Feature que nem assim aparece (fora do enquadramento, respingo) fica sem chamada.
        ///
        /// Os balões ficam em FAIXAS LATERAIS acrescentadas à imagem, fora da peça: à esquerda os
        /// que apontam para a metade esquerda, à direita os outros, cada um na altura do que aponta.
        /// O anel em volta da peça (versão de 2026-09-18) ainda caía em cima dos cantos e das bordas
        /// dela — "os balões estão muito em cima do detalhe" (Carlos, 2026-10-01). Com destaques a
        /// imagem fica mais LARGA que alta; sem eles, volta como veio.
        /// Desenhada com GDI+ depois da redução de escala: texto rasterizado à mão sairia serrilhado.
        /// </summary>
        private static Bitmap WithCallouts(Bitmap part, int[] gbuf, int s, IReadOnlyList<HighlightGroup> highlights)
        {
            if (part == null || highlights == null || highlights.Count == 0) return part;

            // Menos que isto é respingo de faceta: a feature está praticamente escondida.
            int minPixels = Math.Max(8, s * s / 20000);

            // A ponta da linha vai para a MAIOR MANCHA visível de cada feature, não para a média de
            // todos os pixels dela: uma feature com partes em lados opostos da peça tem média no
            // MEIO, e a linha terminava no vazio, no centro (Carlos, 2026-09-18).
            PointF[] tip = LargestBlobCenters(gbuf, s, highlights.Count, minPixels);

            float radius = Math.Max(9f, part.Height / 18f);
            int gutter = (int)Math.Ceiling(3 * radius);   // o balão e meio raio de folga de cada lado
            int width = part.Width + 2 * gutter, height = part.Height;

            // Mesma largura nas duas vistas, com ou sem chamada visível: na planilha elas ficam lado
            // a lado e têm de sair na mesma escala.
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.DrawImageUnscaled(part, gutter, 0);
            }
            part.Dispose();

            // Cada chamada visível vai para o lado mais perto do que aponta.
            var left = new List<int>();
            var right = new List<int>();
            var target = new PointF[highlights.Count];
            for (int group = 0; group < highlights.Count; group++)
            {
                if (float.IsNaN(tip[group].X)) continue;   // feature escondida nesta vista
                target[group] = new PointF(gutter + tip[group].X / Supersample, tip[group].Y / Supersample);
                (target[group].X < width / 2f ? left : right).Add(group);
            }
            if (left.Count + right.Count == 0) return bmp;

            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Arial", Math.Max(7f, height / 26f), FontStyle.Bold))
            using (var pen = new Pen(CalloutColor, Math.Max(1f, height / 220f)))
            using (var fill = new SolidBrush(Color.White))
            using (var ink = new SolidBrush(CalloutColor))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                DrawSide(g, left, gutter / 2f);
                DrawSide(g, right, width - gutter / 2f);

                void DrawSide(Graphics gr, List<int> groups, float bx)
                {
                    if (groups.Count == 0) return;
                    // Na altura do que aponta, afastados o bastante para não encostar um no outro.
                    List<double> ys = groups.Select(i => (double)target[i].Y).ToList();
                    SpreadAlong(ys, 2.3 * radius, radius + 2, height - radius - 2);

                    for (int i = 0; i < groups.Count; i++)
                    {
                        int group = groups[i];
                        float by = (float)ys[i];
                        PointF to = target[group];

                        // A linha sai da BORDA do balão (senão atravessa o número) e termina num
                        // ponto cheio em cima da feature, que é o que diz "é isto aqui".
                        double dx = to.X - bx, dy = to.Y - by;
                        double len = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1e-6);
                        if (len > radius + 2)
                            gr.DrawLine(pen, bx + (float)(dx / len * radius), by + (float)(dy / len * radius), to.X, to.Y);
                        float dot = Math.Max(2f, radius / 5f);
                        gr.FillEllipse(ink, to.X - dot, to.Y - dot, dot * 2, dot * 2);

                        gr.FillEllipse(fill, bx - radius, by - radius, radius * 2, radius * 2);
                        gr.DrawEllipse(pen, bx - radius, by - radius, radius * 2, radius * 2);

                        string label = string.IsNullOrWhiteSpace(highlights[group].Label)
                            ? (group + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : highlights[group].Label;
                        SizeF size = gr.MeasureString(label, font);
                        gr.DrawString(label, font, ink, bx - size.Width / 2, by - size.Height / 2);
                    }
                }
            }
            return bmp;
        }

        /// <summary>
        /// Para cada feature, um ponto EM CIMA dela: o centro da maior mancha conexa que sobrou no
        /// z-buffer, já encostado num pixel de verdade daquela mancha. Assim a linha do balão
        /// termina sobre a feature mesmo quando ela é côncava ou está partida em pedaços — a média
        /// simples caía no meio da peça, longe de tudo. NaN = feature sem mancha grande o bastante
        /// (escondida nesta vista). Coordenadas no buffer SUPERAMOSTRADO.
        /// </summary>
        public static PointF[] LargestBlobCenters(int[] gbuf, int s, int groupCount, int minPixels)
        {
            var best = new PointF[groupCount];
            for (int i = 0; i < groupCount; i++) best[i] = new PointF(float.NaN, float.NaN);
            if (groupCount == 0) return best;

            var bestSize = new int[groupCount];
            var seen = new bool[gbuf.Length];
            var queue = new Queue<int>();
            var blob = new List<int>();

            for (int start = 0; start < gbuf.Length; start++)
            {
                int group = gbuf[start];
                if (group < 0 || group >= groupCount || seen[start]) continue;

                // Mancha conexa por 4 vizinhos, a partir deste pixel.
                blob.Clear();
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    blob.Add(i);
                    int x = i % s, y = i / s;
                    if (x > 0) Push(gbuf, seen, queue, i - 1, group);
                    if (x < s - 1) Push(gbuf, seen, queue, i + 1, group);
                    if (y > 0) Push(gbuf, seen, queue, i - s, group);
                    if (y < s - 1) Push(gbuf, seen, queue, i + s, group);
                }

                if (blob.Count < minPixels || blob.Count <= bestSize[group]) continue;
                bestSize[group] = blob.Count;

                double sx = 0, sy = 0;
                foreach (int i in blob) { sx += i % s; sy += i / s; }
                double cx = sx / blob.Count, cy = sy / blob.Count;

                // Encosta o centro num pixel REAL da mancha: em feature em U ou em L, o centro
                // geométrico cai fora dela.
                double nearest = double.MaxValue;
                foreach (int i in blob)
                {
                    double dx = i % s - cx, dy = i / s - cy;
                    double d = dx * dx + dy * dy;
                    if (d >= nearest) continue;
                    nearest = d;
                    best[group] = new PointF(i % s, i / s);
                }
            }
            return best;
        }

        private static void Push(int[] gbuf, bool[] seen, Queue<int> queue, int index, int group)
        {
            if (seen[index] || gbuf[index] != group) return;
            seen[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// Afasta as posições dos balões de uma faixa lateral até nenhum par vizinho ficar a menos
        /// de <paramref name="minGap"/>, sem sair de [<paramref name="lo"/>, <paramref name="hi"/>] e
        /// PRESERVANDO a ordem — quem aponta mais para cima continua em cima, então as linhas não se
        /// cruzam. Sem isso, dois balões de features vizinhas caem um em cima do outro.
        ///
        /// Colocação determinística (a lição do anel de 2026-09-18: empurra-empurra iterativo
        /// oscilava e deixava pares sobrepostos): ordena, varre para baixo empurrando cada um para
        /// depois do anterior, e se o último passou do fim varre de volta para cima. Se nem assim
        /// cabem, distribui em partes iguais: apertado é melhor que sobreposto.
        /// </summary>
        public static void SpreadAlong(List<double> values, double minGap, double lo, double hi)
        {
            int n = values.Count;
            if (n == 0) return;
            if (hi < lo) hi = lo;
            List<int> order = Enumerable.Range(0, n).OrderBy(i => values[i]).ToList();

            if (n > 1 && minGap * (n - 1) > hi - lo)
            {
                double step = (hi - lo) / (n - 1);
                for (int k = 0; k < n; k++) values[order[k]] = lo + k * step;
                return;
            }

            for (int k = 0; k < n; k++)
            {
                double floor = k == 0 ? lo : values[order[k - 1]] + minGap;
                values[order[k]] = Math.Min(Math.Max(values[order[k]], floor), hi);
            }
            for (int k = n - 1; k >= 0; k--)
            {
                double ceiling = k == n - 1 ? hi : values[order[k + 1]] - minGap;
                if (values[order[k]] > ceiling) values[order[k]] = ceiling;
            }
        }

        /// <summary>
        /// Junta a cena opaca com o raio-X: a face alterada que está NA FRENTE fica com a cor
        /// cheia; o resto da peça (faces e arestas) é esmaecido para o branco, e onde houver uma
        /// face alterada atrás dele ela aparece por transparência. O fundo branco não muda.
        /// </summary>
        private static void Composite(int[] rgb, double[] zbuf, int[] gbuf, int[] rgbH, int[] gbufH)
        {
            for (int i = 0; i < rgb.Length; i++)
            {
                if (gbuf[i] >= 0 || double.IsNegativeInfinity(zbuf[i])) continue;
                int c = Mix(rgb[i], 0xFFFFFF, PlainWash);
                if (gbufH[i] >= 0) c = Mix(c, rgbH[i], HiddenHighlightAlpha);
                rgb[i] = c;
            }
        }

        /// <summary>Mistura duas cores RRGGBB: <paramref name="t"/> = 0 dá <paramref name="a"/>, 1 dá <paramref name="b"/>.</summary>
        private static int Mix(int a, int b, double t)
        {
            Func<int, int> ch = shift =>
            {
                int x = (a >> shift) & 0xFF, y = (b >> shift) & 0xFF;
                return (int)Math.Round(x + (y - x) * t) & 0xFF;
            };
            return (ch(16) << 16) | (ch(8) << 8) | ch(0);
        }

        private static void FillTriangle(double[] zbuf, int[] rgb, int[] gbuf, int s,
            double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz,
            int color, int group)
        {
            double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-9) return;

            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
            int x1 = Math.Min(s - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
            int y1 = Math.Min(s - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            const double eps = -1e-6; // inclui a borda: facetas vizinhas não deixam fresta

            for (int y = y0; y <= y1; y++)
            {
                double py = y + 0.5;
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5;
                    double l0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                    double l1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                    double l2 = 1 - l0 - l1;
                    if (l0 < eps || l1 < eps || l2 < eps) continue;
                    double z = l0 * az + l1 * bz + l2 * cz;
                    int i = y * s + x;
                    if (z <= zbuf[i]) continue;
                    zbuf[i] = z;
                    rgb[i] = color;
                    gbuf[i] = group;
                }
            }
        }

        private static void DrawEdge(double[] zbuf, int[] rgb, int s,
            double ax, double ay, double az, double bx, double by, double bz, int color, int thickness, double bias)
        {
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay))) + 1;
            int half = thickness / 2;
            for (int k = 0; k <= steps; k++)
            {
                double t = (double)k / steps;
                double z = az + (bz - az) * t;
                int cx = (int)Math.Floor(ax + (bx - ax) * t);
                int cy = (int)Math.Floor(ay + (by - ay) * t);
                for (int dy = -half; dy < thickness - half; dy++)
                    for (int dx = -half; dx < thickness - half; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= s || y >= s) continue;
                        int i = y * s + x;
                        if (z + bias >= zbuf[i]) rgb[i] = color;
                    }
            }
        }

        private static Bitmap Downsample(int[] rgb, int s, int size)
        {
            var pixels = new int[size * size];
            int n = Supersample * Supersample;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < Supersample; dy++)
                        for (int dx = 0; dx < Supersample; dx++)
                        {
                            int c = rgb[(y * Supersample + dy) * s + x * Supersample + dx];
                            r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF;
                        }
                    pixels[y * size + x] = unchecked((int)0xFF000000) | ((r / n) << 16) | ((g / n) << 8) | (b / n);
                }

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < size; y++)
                    Marshal.Copy(pixels, y * size, IntPtr.Add(data.Scan0, y * data.Stride), size);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        private static PointF ToScreen(Vec v, Basis b) => new PointF((float)Vec.Dot(v, b.Right), (float)-Vec.Dot(v, b.Up));

        private static int ShadedRgb(double shade, Color baseColor)
        {
            Func<int, int> ch = x => Math.Max(0, Math.Min(255, (int)Math.Round(x * shade)));
            return (ch(baseColor.R) << 16) | (ch(baseColor.G) << 8) | ch(baseColor.B);
        }

        private static int VertexId(Dictionary<long[], int> ids, Vec v)
        {
            var key = new[]
            {
                (long)Math.Round(v.X / VertexQuantumMm), (long)Math.Round(v.Y / VertexQuantumMm), (long)Math.Round(v.Z / VertexQuantumMm),
            };
            if (!ids.TryGetValue(key, out int id)) ids[key] = id = ids.Count;
            return id;
        }

        private static long EdgeKey(int i, int j) => i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;

        private sealed class Tri
        {
            public Vec[] V;
            public Vec N;
            public int[] Ids;
            public int Group = -1;   // feature da revisão a que esta faceta pertence
        }

        private sealed class KeyComparer : IEqualityComparer<long[]>
        {
            public bool Equals(long[] x, long[] y) => x[0] == y[0] && x[1] == y[1] && x[2] == y[2];
            public int GetHashCode(long[] k) => unchecked((int)(k[0] * 73856093 ^ k[1] * 19349663 ^ k[2] * 83492791));
        }

        private struct Vec
        {
            public readonly double X, Y, Z;
            public Vec(double x, double y, double z) { X = x; Y = y; Z = z; }
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public Vec Normalized() { double l = Length; return new Vec(X / l, Y / l, Z / l); }
            public static Vec Norm(double x, double y, double z) => new Vec(x, y, z).Normalized();
            public static double Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static Vec Cross(Vec a, Vec b) => new Vec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static Vec operator *(Vec a, double s) => new Vec(a.X * s, a.Y * s, a.Z * s);
        }
    }
}
