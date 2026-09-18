using System;
using System.Collections.Generic;

namespace AutoEDM.Reverse
{
    /// <summary>Uma região da malha que foi reconhecida como superfície geométrica.</summary>
    public sealed class RecognizedSurface
    {
        /// <summary>"plano", "cilindro" ou "livre" (não coube em primitiva).</summary>
        public string Kind;
        public int TriangleCount;
        public double AreaMm2;

        /// <summary>Plano: a normal unitária. Cilindro: não usado.</summary>
        public double[] Normal;

        /// <summary>Plano: um ponto dele (centroide). Cilindro: um ponto do EIXO.</summary>
        public double[] PointMm;

        /// <summary>Cilindro: direção unitária do eixo.</summary>
        public double[] AxisMm;

        public double RadiusMm;

        /// <summary>Cilindro: comprimento medido ao longo do eixo.</summary>
        public double LengthMm;

        /// <summary>Cilindro: quanto da volta a região cobre, em graus (360 = furo/pino inteiro).</summary>
        public double SweepDeg;

        /// <summary>Desvio RMS dos vértices em relação à primitiva ajustada, em mm. É o número que
        /// separa "isto é um cilindro Ø12" de "isto se parece com um cilindro".</summary>
        public double FitRmsMm;

        public double[] MinMm;
        public double[] MaxMm;
    }

    public sealed class RecognitionResult
    {
        public readonly List<RecognizedSurface> Surfaces = new List<RecognizedSurface>();
        public int TriangleCount;
        public int DegenerateTriangles;
        public double TotalAreaMm2;
        public double[] MinMm;
        public double[] MaxMm;

        /// <summary>Área que ficou em regiões "livre" — o que um kernel free-form teria de cobrir.</summary>
        public double FreeFormAreaMm2;
    }

    public sealed class RecognizerOptions
    {
        /// <summary>Quanto a normal de um triângulo pode divergir da normal da região e ainda ser
        /// o mesmo plano.</summary>
        public double PlaneAngleToleranceDeg = 2.0;

        /// <summary>Distância máxima de um vértice ao plano da região.</summary>
        public double PlaneDistanceToleranceMm = 0.05;

        /// <summary>Ângulo diedral acima do qual a aresta é uma QUINA: separa regiões curvas
        /// vizinhas que não são a mesma superfície.</summary>
        public double CreaseAngleDeg = 35.0;

        /// <summary>Região com menos triângulos que isto não vira superfície reconhecida. O padrão
        /// é 2 porque uma caixa exportada em malha tem DOIS triângulos por face: exigir mais
        /// faria o reconhecedor não achar face nenhuma justamente no caso mais simples.</summary>
        public int MinTrianglesPerRegion = 2;

        /// <summary>Tolerância relativa do ajuste de cilindro: RMS aceito = raio × isto.</summary>
        public double CylinderRelativeTolerance = 0.02;

        /// <summary>Piso absoluto da tolerância do cilindro, para raio pequeno.</summary>
        public double CylinderAbsoluteToleranceMm = 0.02;
    }

    /// <summary>
    /// RECONHECIMENTO DE SUPERFÍCIE SOBRE MALHA — geometria PURA, sem uma linha de COM, para
    /// poder ser testada sem a Solid Edge aberta.
    ///
    /// POR QUE ELE EXISTE. A sonda de malha (rodada 1, 2026-09-18) mediu na malha real:
    /// <c>Body.GetFacetData</c> entrega os triângulos, mas <c>Body.Faces</c> é INACESSÍVEL num
    /// corpo de facetas — não há região pré-segmentada para agarrar. E o dump da typelib já tinha
    /// dito que a Solid Edge não expõe por COM nenhum ajuste de plano/cilindro/cone sobre malha.
    /// Logo a segmentação e o ajuste têm de ser nossos, a partir da sopa de triângulos.
    ///
    /// O QUE ELE FAZ, e em que ordem:
    ///   1. solda os vértices repetidos (a sopa vem sem índice) e monta a vizinhança por aresta;
    ///   2. quebra a malha em componentes pela QUINA (ângulo diedral) — é a quina que delimita
    ///      superfície, então é ela que decide onde uma face termina e outra começa;
    ///   3. pergunta de cada componente INTEIRO qual superfície ele é: plano (normal única dentro
    ///      da tolerância) ou cilindro (eixo = autovetor de menor autovalor de Σ área·n·nᵀ, porque
    ///      numa superfície cilíndrica toda normal é perpendicular ao eixo; raio por ajuste de
    ///      círculo na projeção);
    ///   4. componente MISTO — o caso do raio, em que o filete é tangente à face e não gera quina
    ///      — é descascado: cresce os planos de dentro dele e ajusta o que sobra;
    ///   5. o que não passa na tolerância fica "livre", e a ÁREA disso é o número que decide se a
    ///      rota prismática basta ou se um kernel free-form é inevitável.
    ///
    /// A ORDEM DO 2 ANTES DO 3 não é estilo, é correção: tentar planos primeiro, soltos na malha
    /// inteira, faz cada faixa de um cilindro tesselado virar um "plano" perfeito de dois
    /// triângulos — um Ø20 sai como 64 plaquinhas. O teste do cilindro pegou exatamente isso.
    ///
    /// Toda superfície sai com o RMS do próprio ajuste. Sem esse número o reconhecimento seria
    /// opinião.
    /// </summary>
    public static class SurfaceRecognizer
    {
        // Marcas do vetor 'region', todas negativas para não colidir com o índice da região.
        private const int Free = -1;      // livre, ainda não tentado
        private const int Dead = -2;      // triângulo degenerado
        private const int TriedSeed = -3; // já foi semente e não deu região
        private const int InQueue = -4;   // dentro do crescimento em curso

        /// <param name="pointsMm">Sopa de triângulos: 9 doubles por triângulo (3 vértices XYZ), em mm.</param>
        public static RecognitionResult Recognize(double[] pointsMm, RecognizerOptions options)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (pointsMm.Length % 9 != 0)
                throw new ArgumentException("A sopa de triângulos precisa ter 9 doubles por triângulo.", nameof(pointsMm));
            RecognizerOptions o = options ?? new RecognizerOptions();

            var result = new RecognitionResult();
            int triCount = pointsMm.Length / 9;

            // ------------------------------------------------------------ 1. triângulos
            var nx = new double[triCount];
            var ny = new double[triCount];
            var nz = new double[triCount];
            var area = new double[triCount];
            var cx = new double[triCount];
            var cy = new double[triCount];
            var cz = new double[triCount];
            var alive = new bool[triCount];

            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };

            for (int t = 0; t < triCount; t++)
            {
                int b = t * 9;
                double ax = pointsMm[b], ay = pointsMm[b + 1], az = pointsMm[b + 2];
                double bx = pointsMm[b + 3], by = pointsMm[b + 4], bz = pointsMm[b + 5];
                double gx = pointsMm[b + 6], gy = pointsMm[b + 7], gz = pointsMm[b + 8];

                for (int k = 0; k < 9; k += 3)
                    Span(min, max, pointsMm[b + k], pointsMm[b + k + 1], pointsMm[b + k + 2]);

                double ux = bx - ax, uy = by - ay, uz = bz - az;
                double vx = gx - ax, vy = gy - ay, vz = gz - az;
                double px = uy * vz - uz * vy;
                double py = uz * vx - ux * vz;
                double pz = ux * vy - uy * vx;
                double len = Math.Sqrt(px * px + py * py + pz * pz);

                if (len <= 1e-12) { result.DegenerateTriangles++; continue; }

                alive[t] = true;
                nx[t] = px / len; ny[t] = py / len; nz[t] = pz / len;
                area[t] = 0.5 * len;
                cx[t] = (ax + bx + gx) / 3.0;
                cy[t] = (ay + by + gy) / 3.0;
                cz[t] = (az + bz + gz) / 3.0;
                result.TotalAreaMm2 += area[t];
            }

            result.TriangleCount = triCount;
            if (triCount == 0 || result.TotalAreaMm2 <= 0)
            {
                result.MinMm = new double[3];
                result.MaxMm = new double[3];
                return result;
            }
            result.MinMm = min;
            result.MaxMm = max;

            // ------------------------------------------------------------ 2. vizinhança
            List<int>[] neighbours = BuildAdjacency(pointsMm, triCount, alive, Diagonal(min, max));

            // ------------------------------------------------------ 3. quebra pelas quinas
            // A ORDEM IMPORTA, e custou um teste vermelho para ficar clara: tentar planos
            // primeiro, solto na malha inteira, faz cada FAIXA de um cilindro tesselado virar um
            // "plano" perfeito de dois triângulos — 64 plaquinhas no lugar de um Ø20. A quina é
            // que delimita superfície; dentro dela é que se pergunta QUAL superfície é.
            double cosCrease = Math.Cos(o.CreaseAngleDeg * Math.PI / 180.0);
            double cosPlane = Math.Cos(o.PlaneAngleToleranceDeg * Math.PI / 180.0);

            var region = new int[triCount];
            for (int i = 0; i < triCount; i++) region[i] = alive[i] ? Free : Dead;

            var component = new int[triCount];
            for (int i = 0; i < triCount; i++) component[i] = -1;

            var components = new List<List<int>>();
            for (int start = 0; start < triCount; start++)
            {
                if (!alive[start] || component[start] >= 0) continue;
                List<int> comp = GrowSmooth(start, neighbours, component, components.Count, nx, ny, nz, cosCrease);
                if (comp.Count > 0) components.Add(comp);
            }

            // ------------------------------------------------- 4. que superfície é cada região
            foreach (List<int> comp in components)
            {
                // Região inteira de uma vez: é o caso comum (face de caixa, tampa, parede de furo).
                RecognizedSurface whole = TryPlaneWhole(comp, nx, ny, nz, area, pointsMm, cosPlane,
                                                        o.PlaneDistanceToleranceMm)
                                          ?? TryCylinder(comp, nx, ny, nz, area, pointsMm, o);
                if (whole != null)
                {
                    foreach (int t in comp) region[t] = result.Surfaces.Count;
                    result.Surfaces.Add(whole);
                    continue;
                }

                // Região MISTA. Acontece de verdade sempre que há raio: o filete é TANGENTE à face
                // plana, então não existe quina entre eles e os dois caem no mesmo componente.
                // Aqui sim vale descascar os planos por crescimento, e depois ajustar o que sobrou.
                SplitMixed(comp, neighbours, region, component, nx, ny, nz, cx, cy, cz, area,
                           pointsMm, cosPlane, cosCrease, o, result);
            }

            result.Surfaces.Sort((a, b) => b.AreaMm2.CompareTo(a.AreaMm2));
            return result;
        }

        // ------------------------------------------------------------------ vizinhança

        /// <summary>Solda os vértices repetidos por hash de coordenada quantizada e indexa os
        /// triângulos que compartilham cada aresta. A tolerância da solda acompanha o tamanho da
        /// peça: malha exportada em float não repete o vértice bit a bit.</summary>
        private static List<int>[] BuildAdjacency(double[] p, int triCount, bool[] alive, double diagonal)
        {
            double weld = Math.Max(1e-7, diagonal * 1e-6);
            var vertexOf = new Dictionary<long, int>(triCount * 2);
            var corner = new int[triCount * 3];
            int next = 0;

            for (int t = 0; t < triCount; t++)
            {
                if (!alive[t]) { corner[t * 3] = corner[t * 3 + 1] = corner[t * 3 + 2] = -1; continue; }
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    long key = CellKey(p[b], p[b + 1], p[b + 2], weld);
                    int id;
                    if (!vertexOf.TryGetValue(key, out id)) { id = next++; vertexOf[key] = id; }
                    corner[t * 3 + k] = id;
                }
            }

            var edgeOwner = new Dictionary<long, int>(triCount * 3);
            var neighbours = new List<int>[triCount];
            for (int t = 0; t < triCount; t++) neighbours[t] = new List<int>(3);

            for (int t = 0; t < triCount; t++)
            {
                if (!alive[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int a = corner[t * 3 + k];
                    int b2 = corner[t * 3 + (k + 1) % 3];
                    if (a < 0 || b2 < 0 || a == b2) continue;

                    long ek = a < b2 ? ((long)a << 32) | (uint)b2 : ((long)b2 << 32) | (uint)a;
                    int other;
                    if (edgeOwner.TryGetValue(ek, out other))
                    {
                        if (other != t) { neighbours[t].Add(other); neighbours[other].Add(t); }
                    }
                    else edgeOwner[ek] = t;
                }
            }
            return neighbours;
        }

        /// <summary>Hash de célula: vértices dentro da mesma célula soldam. Simples e
        /// determinístico — busca por raio custaria uma árvore espacial sem ganho aqui.</summary>
        private static long CellKey(double x, double y, double z, double weld)
        {
            long ix = (long)Math.Round(x / weld);
            long iy = (long)Math.Round(y / weld);
            long iz = (long)Math.Round(z / weld);
            unchecked
            {
                const long prime = 1099511628211L;
                long h = unchecked((long)14695981039346656037UL);
                h = (h ^ ix) * prime;
                h = (h ^ iy) * prime;
                h = (h ^ iz) * prime;
                return h;
            }
        }

        // ------------------------------------------------------------------ crescimento

        private static List<int> GrowPlane(int seed, List<int>[] neighbours, int[] region,
            int[] component, int compId,
            double[] nx, double[] ny, double[] nz, double[] cx, double[] cy, double[] cz,
            double[] area, double[] p, double cosTol, double distTolMm)
        {
            var taken = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            region[seed] = InQueue;
            taken.Add(seed);

            // Plano da região, sempre em dia: soma ponderada por área das normais e dos centroides.
            double sumA = area[seed];
            double anx = nx[seed] * sumA, any = ny[seed] * sumA, anz = nz[seed] * sumA;
            double apx = cx[seed] * sumA, apy = cy[seed] * sumA, apz = cz[seed] * sumA;

            while (queue.Count > 0)
            {
                int t = queue.Dequeue();
                foreach (int nb in neighbours[t])
                {
                    if (region[nb] != Free && region[nb] != TriedSeed) continue;
                    if (component[nb] != compId) continue;

                    double ln = Math.Sqrt(anx * anx + any * any + anz * anz);
                    if (ln <= 1e-12) continue;
                    double ux = anx / ln, uy = any / ln, uz = anz / ln;
                    double px = apx / sumA, py = apy / sumA, pz = apz / sumA;

                    if (nx[nb] * ux + ny[nb] * uy + nz[nb] * uz < cosTol) continue;

                    bool inside = true;
                    for (int k = 0; k < 3 && inside; k++)
                    {
                        int b = nb * 9 + k * 3;
                        double d = (p[b] - px) * ux + (p[b + 1] - py) * uy + (p[b + 2] - pz) * uz;
                        if (Math.Abs(d) > distTolMm) inside = false;
                    }
                    if (!inside) continue;

                    region[nb] = InQueue;
                    taken.Add(nb);
                    queue.Enqueue(nb);

                    double a = area[nb];
                    sumA += a;
                    anx += nx[nb] * a; any += ny[nb] * a; anz += nz[nb] * a;
                    apx += cx[nb] * a; apy += cy[nb] * a; apz += cz[nb] * a;
                }
            }
            return taken;
        }

        /// <summary>Componente conexo cujas arestas internas NÃO são quina. Delimita UMA
        /// superfície: dentro dele a malha é suave, e é atravessando a quina que se muda de
        /// face.</summary>
        private static List<int> GrowSmooth(int seed, List<int>[] neighbours, int[] component, int id,
            double[] nx, double[] ny, double[] nz, double cosCrease)
        {
            var comp = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            component[seed] = id;

            while (queue.Count > 0)
            {
                int t = queue.Dequeue();
                comp.Add(t);
                foreach (int nb in neighbours[t])
                {
                    if (component[nb] >= 0) continue;
                    if (nx[t] * nx[nb] + ny[t] * ny[nb] + nz[t] * nz[nb] < cosCrease) continue;
                    component[nb] = id;
                    queue.Enqueue(nb);
                }
            }
            return comp;
        }

        /// <summary>
        /// A região INTEIRA é um plano? Cobra as duas coisas de todo triângulo dela: normal
        /// alinhada com a média e vértice dentro da tolerância. Um único triângulo fora reprova a
        /// região — é o que impede uma parede de cilindro de passar por plano.
        /// </summary>
        private static RecognizedSurface TryPlaneWhole(List<int> tris, double[] nx, double[] ny, double[] nz,
            double[] area, double[] p, double cosTol, double distTolMm)
        {
            if (tris.Count == 0) return null;

            double sumA = 0, anx = 0, any = 0, anz = 0, apx = 0, apy = 0, apz = 0;
            foreach (int t in tris)
            {
                double a = area[t];
                sumA += a;
                anx += nx[t] * a; any += ny[t] * a; anz += nz[t] * a;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    apx += p[b] * a / 3.0;
                    apy += p[b + 1] * a / 3.0;
                    apz += p[b + 2] * a / 3.0;
                }
            }

            double ln = Math.Sqrt(anx * anx + any * any + anz * anz);
            if (ln <= 1e-12 || sumA <= 0) return null;
            double ux = anx / ln, uy = any / ln, uz = anz / ln;
            double px = apx / sumA, py = apy / sumA, pz = apz / sumA;

            foreach (int t in tris)
            {
                if (nx[t] * ux + ny[t] * uy + nz[t] * uz < cosTol) return null;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double d = (p[b] - px) * ux + (p[b + 1] - py) * uy + (p[b + 2] - pz) * uz;
                    if (Math.Abs(d) > distTolMm) return null;
                }
            }

            return MakePlane(tris, nx, ny, nz, area, p);
        }

        /// <summary>
        /// Componente que não é um plano nem um cilindro inteiro — o caso do RAIO: o filete é
        /// tangente à face, não há quina entre eles, e os dois vieram no mesmo componente.
        /// Descasca os planos por crescimento e ajusta o que sobrou, pedaço conexo por pedaço.
        /// </summary>
        private static void SplitMixed(List<int> comp, List<int>[] neighbours, int[] region, int[] component,
            double[] nx, double[] ny, double[] nz, double[] cx, double[] cy, double[] cz, double[] area,
            double[] p, double cosPlane, double cosCrease, RecognizerOptions o, RecognitionResult result)
        {
            int compId = component[comp[0]];

            var byArea = new List<int>(comp);
            byArea.Sort((x, y) => area[y].CompareTo(area[x]));

            foreach (int seed in byArea)
            {
                if (region[seed] != Free) continue;

                List<int> grown = GrowPlane(seed, neighbours, region, component, compId,
                                            nx, ny, nz, cx, cy, cz, area, p, cosPlane,
                                            o.PlaneDistanceToleranceMm);

                if (grown.Count < o.MinTrianglesPerRegion)
                {
                    foreach (int t in grown) region[t] = Free;
                    region[seed] = TriedSeed;
                    continue;
                }

                foreach (int t in grown) region[t] = result.Surfaces.Count;
                result.Surfaces.Add(MakePlane(grown, nx, ny, nz, area, p));
            }

            // O que não virou plano: agrupa em pedaços conexos e tenta cilindro em cada um.
            var seen = new HashSet<int>();
            foreach (int start in comp)
            {
                if (region[start] >= 0 || seen.Contains(start)) continue;

                var piece = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    int t = queue.Dequeue();
                    piece.Add(t);
                    foreach (int nb in neighbours[t])
                    {
                        if (region[nb] >= 0 || seen.Contains(nb) || component[nb] != compId) continue;
                        if (nx[t] * nx[nb] + ny[t] * ny[nb] + nz[t] * nz[nb] < cosCrease) continue;
                        seen.Add(nb);
                        queue.Enqueue(nb);
                    }
                }

                RecognizedSurface s = TryCylinder(piece, nx, ny, nz, area, p, o)
                                      ?? MakeFreeForm(piece, area, p);
                foreach (int t in piece) region[t] = result.Surfaces.Count;
                if (s.Kind == "livre") result.FreeFormAreaMm2 += s.AreaMm2;
                result.Surfaces.Add(s);
            }
        }

        // ------------------------------------------------------------------ ajustes

        private static RecognizedSurface MakePlane(List<int> tris, double[] nx, double[] ny, double[] nz,
            double[] area, double[] p)
        {
            double sumA = 0, anx = 0, any = 0, anz = 0, apx = 0, apy = 0, apz = 0;
            foreach (int t in tris)
            {
                double a = area[t];
                sumA += a;
                anx += nx[t] * a; any += ny[t] * a; anz += nz[t] * a;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    apx += p[b] * a / 3.0;
                    apy += p[b + 1] * a / 3.0;
                    apz += p[b + 2] * a / 3.0;
                }
            }

            double ln = Math.Sqrt(anx * anx + any * any + anz * anz);
            double ux = anx / ln, uy = any / ln, uz = anz / ln;
            double px = apx / sumA, py = apy / sumA, pz = apz / sumA;

            double sq = 0;
            int n = 0;
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };

            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double d = (p[b] - px) * ux + (p[b + 1] - py) * uy + (p[b + 2] - pz) * uz;
                    sq += d * d;
                    n++;
                    Span(min, max, p[b], p[b + 1], p[b + 2]);
                }

            return new RecognizedSurface
            {
                Kind = "plano",
                TriangleCount = tris.Count,
                AreaMm2 = sumA,
                Normal = new[] { ux, uy, uz },
                PointMm = new[] { px, py, pz },
                FitRmsMm = Math.Sqrt(sq / Math.Max(1, n)),
                MinMm = min,
                MaxMm = max
            };
        }

        /// <summary>
        /// Ajuste de cilindro. O eixo vem do autovetor de MENOR autovalor de Σ área·n·nᵀ: numa
        /// superfície cilíndrica toda normal é perpendicular ao eixo, então é a direção que as
        /// normais menos ocupam. Com o eixo, o raio sai de um ajuste de círculo (Kåsa) na
        /// projeção — linear, sem iteração, e com resíduo mensurável.
        /// Devolve null quando o resíduo não cabe na tolerância: NÃO reconhecer é um resultado.
        /// </summary>
        private static RecognizedSurface TryCylinder(List<int> tris, double[] nx, double[] ny, double[] nz,
            double[] area, double[] p, RecognizerOptions o)
        {
            if (tris.Count < o.MinTrianglesPerRegion) return null;

            var m = new double[3, 3];
            foreach (int t in tris)
            {
                double a = area[t];
                double[] v = { nx[t], ny[t], nz[t] };
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++) m[i, j] += a * v[i] * v[j];
            }

            double[] axis = SmallestEigenvector(m);
            if (axis == null) return null;

            double[] e1 = Perpendicular(axis);
            double[] e2 = Cross(axis, e1);

            double su = 0, sv = 0, suu = 0, svv = 0, suv = 0, sr = 0, sru = 0, srv = 0;
            int n = 0;
            double axMin = double.MaxValue, axMax = double.MinValue;
            var us = new List<double>(tris.Count * 3);
            var vs = new List<double>(tris.Count * 3);

            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double x = p[b], y = p[b + 1], z = p[b + 2];
                    double u = x * e1[0] + y * e1[1] + z * e1[2];
                    double v = x * e2[0] + y * e2[1] + z * e2[2];
                    double w = x * axis[0] + y * axis[1] + z * axis[2];
                    if (w < axMin) axMin = w;
                    if (w > axMax) axMax = w;

                    us.Add(u);
                    vs.Add(v);
                    double rr = u * u + v * v;
                    su += u; sv += v;
                    suu += u * u; svv += v * v; suv += u * v;
                    sr += rr; sru += rr * u; srv += rr * v;
                    n++;
                }
            if (n < 6) return null;

            // Kåsa: sistema normal 3×3 para (A,B,C) de u² + v² + A·u + B·v + C = 0.
            double[,] a3 =
            {
                { suu, suv, su },
                { suv, svv, sv },
                { su,  sv,  n  }
            };
            double[] rhs = { -sru, -srv, -sr };
            double[] sol = Solve3(a3, rhs);
            if (sol == null) return null;

            double cu = -sol[0] / 2.0, cv = -sol[1] / 2.0;
            double rsq = cu * cu + cv * cv - sol[2];
            if (rsq <= 1e-12) return null;
            double radius = Math.Sqrt(rsq);

            double sq = 0;
            var angles = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                double du = us[i] - cu, dv = vs[i] - cv;
                double d = Math.Sqrt(du * du + dv * dv) - radius;
                sq += d * d;
                angles.Add(Math.Atan2(dv, du));
            }
            double rms = Math.Sqrt(sq / n);
            double tol = Math.Max(o.CylinderAbsoluteToleranceMm, radius * o.CylinderRelativeTolerance);
            if (rms > tol) return null;

            // Centro do eixo, de volta ao mundo, na altura média da região.
            double wMid = (axMin + axMax) / 2.0;
            double[] centre =
            {
                cu * e1[0] + cv * e2[0] + wMid * axis[0],
                cu * e1[1] + cv * e2[1] + wMid * axis[1],
                cu * e1[2] + cv * e2[2] + wMid * axis[2]
            };

            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            double areaSum = 0;
            foreach (int t in tris)
            {
                areaSum += area[t];
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    Span(min, max, p[b], p[b + 1], p[b + 2]);
                }
            }

            return new RecognizedSurface
            {
                Kind = "cilindro",
                TriangleCount = tris.Count,
                AreaMm2 = areaSum,
                AxisMm = axis,
                PointMm = centre,
                RadiusMm = radius,
                LengthMm = axMax - axMin,
                SweepDeg = SweepDegrees(angles),
                FitRmsMm = rms,
                MinMm = min,
                MaxMm = max
            };
        }

        /// <summary>Quanto da volta a região cobre. Mede pelo MAIOR vão vazio entre ângulos
        /// ordenados: é o que distingue um furo inteiro (360°) de um raio de canto.</summary>
        public static double SweepDegrees(List<double> angles)
        {
            if (angles == null || angles.Count < 2) return 0;
            var sorted = new List<double>(angles);
            sorted.Sort();

            double biggestGap = (sorted[0] + 2 * Math.PI) - sorted[sorted.Count - 1];
            for (int i = 1; i < sorted.Count; i++)
            {
                double g = sorted[i] - sorted[i - 1];
                if (g > biggestGap) biggestGap = g;
            }

            double sweep = (2 * Math.PI - biggestGap) * 180.0 / Math.PI;
            return sweep < 0 ? 0 : sweep;
        }

        private static RecognizedSurface MakeFreeForm(List<int> tris, double[] area, double[] p)
        {
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            double sum = 0;

            foreach (int t in tris)
            {
                sum += area[t];
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    Span(min, max, p[b], p[b + 1], p[b + 2]);
                }
            }

            return new RecognizedSurface
            {
                Kind = "livre",
                TriangleCount = tris.Count,
                AreaMm2 = sum,
                MinMm = min,
                MaxMm = max,
                FitRmsMm = double.NaN
            };
        }

        // ------------------------------------------------------------------ álgebra

        /// <summary>Autovetor do MENOR autovalor de uma simétrica 3×3, por rotações de Jacobi.
        /// 3×3 não justifica dependência externa, e Jacobi converge em poucas varreduras.</summary>
        public static double[] SmallestEigenvector(double[,] m)
        {
            var a = (double[,])m.Clone();
            var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

            for (int sweep = 0; sweep < 32; sweep++)
            {
                double off = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
                if (off < 1e-24) break;

                for (int pIdx = 0; pIdx < 2; pIdx++)
                    for (int q = pIdx + 1; q < 3; q++)
                    {
                        if (Math.Abs(a[pIdx, q]) < 1e-18) continue;

                        double theta = (a[q, q] - a[pIdx, pIdx]) / (2 * a[pIdx, q]);
                        double tt = theta == 0
                            ? 1.0
                            : Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        double c = 1 / Math.Sqrt(tt * tt + 1);
                        double s = tt * c;

                        for (int k = 0; k < 3; k++)
                        {
                            double akp = a[k, pIdx], akq = a[k, q];
                            a[k, pIdx] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            double apk = a[pIdx, k], aqk = a[q, k];
                            a[pIdx, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            double vkp = v[k, pIdx], vkq = v[k, q];
                            v[k, pIdx] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
            }

            int best = 0;
            for (int i = 1; i < 3; i++) if (a[i, i] < a[best, best]) best = i;

            double[] e = { v[0, best], v[1, best], v[2, best] };
            double len = Math.Sqrt(e[0] * e[0] + e[1] * e[1] + e[2] * e[2]);
            if (len <= 1e-12) return null;
            e[0] /= len; e[1] /= len; e[2] /= len;

            // Sinal canônico: a maior componente fica positiva, para dois runs darem o mesmo eixo.
            int dom = Math.Abs(e[0]) >= Math.Abs(e[1])
                ? (Math.Abs(e[0]) >= Math.Abs(e[2]) ? 0 : 2)
                : (Math.Abs(e[1]) >= Math.Abs(e[2]) ? 1 : 2);
            if (e[dom] < 0) { e[0] = -e[0]; e[1] = -e[1]; e[2] = -e[2]; }
            return e;
        }

        private static double[] Solve3(double[,] a, double[] b)
        {
            var m = (double[,])a.Clone();
            var r = (double[])b.Clone();

            for (int col = 0; col < 3; col++)
            {
                int pivot = col;
                for (int i = col + 1; i < 3; i++)
                    if (Math.Abs(m[i, col]) > Math.Abs(m[pivot, col])) pivot = i;
                if (Math.Abs(m[pivot, col]) < 1e-15) return null;

                if (pivot != col)
                {
                    for (int k = 0; k < 3; k++) { double tmp = m[col, k]; m[col, k] = m[pivot, k]; m[pivot, k] = tmp; }
                    double tr = r[col]; r[col] = r[pivot]; r[pivot] = tr;
                }

                for (int i = col + 1; i < 3; i++)
                {
                    double f = m[i, col] / m[col, col];
                    for (int k = col; k < 3; k++) m[i, k] -= f * m[col, k];
                    r[i] -= f * r[col];
                }
            }

            var x = new double[3];
            for (int i = 2; i >= 0; i--)
            {
                double s = r[i];
                for (int k = i + 1; k < 3; k++) s -= m[i, k] * x[k];
                x[i] = s / m[i, i];
            }
            return x;
        }

        private static double[] Perpendicular(double[] a)
        {
            double[] pick = Math.Abs(a[0]) < 0.9 ? new double[] { 1, 0, 0 } : new double[] { 0, 1, 0 };
            double[] e = Cross(a, pick);
            double len = Math.Sqrt(e[0] * e[0] + e[1] * e[1] + e[2] * e[2]);
            return new[] { e[0] / len, e[1] / len, e[2] / len };
        }

        private static double[] Cross(double[] a, double[] b)
        {
            return new[]
            {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0]
            };
        }

        private static void Span(double[] min, double[] max, double x, double y, double z)
        {
            if (x < min[0]) min[0] = x;
            if (x > max[0]) max[0] = x;
            if (y < min[1]) min[1] = y;
            if (y > max[1]) max[1] = y;
            if (z < min[2]) min[2] = z;
            if (z > max[2]) max[2] = z;
        }

        private static double Diagonal(double[] min, double[] max)
        {
            double dx = max[0] - min[0], dy = max[1] - min[1], dz = max[2] - min[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static int[] OrderByAreaDesc(double[] area, bool[] alive)
        {
            var idx = new List<int>(area.Length);
            for (int i = 0; i < area.Length; i++) if (alive[i]) idx.Add(i);
            idx.Sort((x, y) => area[y].CompareTo(area[x]));
            return idx.ToArray();
        }
    }
}
