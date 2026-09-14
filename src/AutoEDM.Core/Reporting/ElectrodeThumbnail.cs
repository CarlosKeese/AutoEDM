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

        // Base da câmera (ortonormal, destra: Right × Up = Camera).
        private static readonly Vec Camera = Vec.Norm(1, -1, -1); // do objeto PARA a câmera: X+, Y−, abaixo
        private static readonly Vec Right = Vec.Norm(1, 1, 0);
        private static readonly Vec Up = Vec.Norm(1, -1, 2);      // +Z do modelo aponta para cima na tela
        // Luz fora do eixo da vista: numa iso as três faces de um cubo ficariam com o MESMO tom.
        private static readonly Vec Light = (Right * 0.3 + Up * -0.3 + Camera * 0.9).Normalized();

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
            /// <summary>Aresta viva a contornar (A-B, B-C, C-A).</summary>
            public bool EdgeAB, EdgeBC, EdgeCA;
        }

        /// <summary>Projeta a malha; ordenada do mais longe para o mais perto (só p/ inspeção — o desenho usa Z-buffer).</summary>
        public static List<Facet> Project(IEnumerable<double[]> meshesMm, double featureAngleDeg = DefaultFeatureAngleDeg)
        {
            var tris = new List<Tri>();
            foreach (double[] mesh in meshesMm ?? Enumerable.Empty<double[]>())
            {
                if (mesh == null) continue;
                for (int b = 0; b + 8 < mesh.Length; b += 9)
                {
                    var a = new Vec(mesh[b], mesh[b + 1], mesh[b + 2]);
                    var p = new Vec(mesh[b + 3], mesh[b + 4], mesh[b + 5]);
                    var c = new Vec(mesh[b + 6], mesh[b + 7], mesh[b + 8]);
                    Vec n = Vec.Cross(p - a, c - a);
                    if (n.Length < 1e-12) continue; // faceta degenerada
                    tris.Add(new Tri { V = new[] { a, p, c }, N = n.Normalized() });
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
                    A = ToScreen(tri.V[0]),
                    B = ToScreen(tri.V[1]),
                    C = ToScreen(tri.V[2]),
                    DepthA = Vec.Dot(tri.V[0], Camera),
                    DepthB = Vec.Dot(tri.V[1], Camera),
                    DepthC = Vec.Dot(tri.V[2], Camera),
                    Shade = Ambient + (1 - Ambient) * Math.Abs(Vec.Dot(tri.N, Light)),
                    EdgeAB = flags[0],
                    EdgeBC = flags[1],
                    EdgeCA = flags[2],
                });
            }
            return facets.OrderBy(f => f.Depth).ToList();
        }

        /// <summary>Miniatura quadrada, fundo branco, peça centrada. Null se a malha estiver vazia.</summary>
        public static Bitmap Render(IEnumerable<double[]> meshesMm, int sizePx = DefaultSizePx)
        {
            List<Facet> facets = Project(meshesMm);
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
            for (int i = 0; i < zbuf.Length; i++) { zbuf[i] = double.NegativeInfinity; rgb[i] = 0xFFFFFF; }

            foreach (Facet f in facets)
                FillTriangle(zbuf, rgb, s,
                    f.A.X * scale + offX, f.A.Y * scale + offY, f.DepthA,
                    f.B.X * scale + offX, f.B.Y * scale + offY, f.DepthB,
                    f.C.X * scale + offX, f.C.Y * scale + offY, f.DepthC,
                    ShadedRgb(f.Shade));

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

            return Downsample(rgb, s, sizePx);
        }

        private static void FillTriangle(double[] zbuf, int[] rgb, int s,
            double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz, int color)
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

        private static PointF ToScreen(Vec v) => new PointF((float)Vec.Dot(v, Right), (float)-Vec.Dot(v, Up));

        private static int ShadedRgb(double shade)
        {
            Func<int, int> ch = x => Math.Max(0, Math.Min(255, (int)Math.Round(x * shade)));
            return (ch(Copper.R) << 16) | (ch(Copper.G) << 8) | ch(Copper.B);
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
