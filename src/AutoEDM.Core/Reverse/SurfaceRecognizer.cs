using System;
using System.Collections.Generic;

namespace AutoEDM.Reverse
{
    /// <summary>Uma região da malha que foi reconhecida como superfície geométrica.</summary>
    public sealed class RecognizedSurface
    {
        /// <summary>"plano", "cilindro", "cone" ou "livre" (não coube em primitiva).</summary>
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

        /// <summary>Cilindro/cone: quanto da volta a região cobre, em graus (360 = furo/pino inteiro).</summary>
        public double SweepDeg;

        /// <summary>Cilindro em ARCO (volta &lt; 360°): direções radiais unitárias do início e do
        /// fim do arco, perpendiculares ao eixo, com o arco correndo do início ao fim no sentido
        /// anti-horário em volta de <see cref="AxisMm"/>. null no cilindro de volta inteira.</summary>
        public double[] ArcFrom;
        public double[] ArcTo;

        /// <summary>Toro: raio MAIOR — distância do eixo ao centro do círculo do perfil. O raio
        /// cotado (o do perfil) fica em <see cref="RadiusMm"/>; <see cref="PointMm"/> é o centro
        /// do toro no eixo, na altura do centro do perfil.</summary>
        public double MajorRadiusMm;

        /// <summary>Toro: o trecho do círculo do perfil que a superfície cobre, em graus, no
        /// meridiano (ρ = distância ao eixo, h = altura ao longo do eixo), anti-horário de
        /// <see cref="TubeFromDeg"/> a <see cref="TubeToDeg"/>, medido a partir de +ρ.</summary>
        public double TubeFromDeg, TubeToDeg;

        /// <summary>Cone: semi-ângulo do vértice, em graus (45 = chanfro a 45°; ângulo de saída
        /// de 1° numa parede de revolução = 1).</summary>
        public double HalfAngleDeg;

        /// <summary>Cone: o vértice (ápice), em mm. O eixo aponta do ápice para a abertura.</summary>
        public double[] ApexMm;

        /// <summary>Cone: raio na ponta mais próxima e na mais distante do ápice, em mm.</summary>
        public double RadiusMinMm;
        public double RadiusMaxMm;

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

        /// <summary>Para cada triângulo, o índice em <see cref="Surfaces"/> da superfície a que ele
        /// pertence (−1 = degenerado). É o que liga a malha de volta à geometria exata: a secção
        /// 2,5D usa isto para trocar cada trecho do contorno pela superfície reconhecida.</summary>
        public int[] RegionOfTriangle;
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

        /// <summary>Tolerância relativa do ajuste de cilindro: desvio aceito = raio × isto.
        /// Era 0,02 e deixou passar um "cilindro Ø143" com RMS de 0,64 mm na malha real — o que
        /// mede a forma é a tolerância absoluta; esta só alarga para raio grande.</summary>
        public double CylinderRelativeTolerance = 0.001;

        /// <summary>Piso absoluto da tolerância do cilindro. 0,005 mm = metade do passo de
        /// arredondamento: na malha real (STL exportado de CAD) todo cilindro de verdade deu RMS
        /// 0,00000, e os fragmentos de concordância de canto forçados em cilindro ficaram entre
        /// 0,0002 e 0,02. Malha de SCANNER tem ruído — aí este número tem de subir.</summary>
        public double CylinderAbsoluteToleranceMm = 0.005;

        /// <summary>Passo de arredondamento das medidas reconhecidas (offset de plano, Ø, posição
        /// do eixo), em mm. 0,01 mm é a regra da ferramentaria para reconstruir: a malha traz o
        /// erro da tesselação/exportação, e o projeto original foi cotado em centésimos. 0 desliga.</summary>
        public double RoundToMm = 0.01;

        /// <summary>Arco mínimo para uma região ser cilindro, em graus. Abaixo disso a curvatura
        /// não é mensurável e a região é tratada como plana/mista.</summary>
        public double MinCylinderSweepDeg = 5.0;

        /// <summary>Flecha máxima entre vértices vizinhos de um cilindro tesselado, em mm. Furo
        /// Ø12,3 em 36 segmentos dá 0,023; duas faces planas em ângulo, ajustadas como "cilindro",
        /// davam ~1 mm na malha real.</summary>
        public double MaxSagittaMm = 0.1;

        /// <summary>Normal de plano ou eixo de cilindro a menos disto de X, Y ou Z vira EXATAMENTE
        /// esse eixo. Sem isso, um plano "horizontal" sairia com normal (0,00001; 0; 1) e a peça
        /// reconstruída teria faces tortas por um erro de arredondamento do STL. 0 desliga.</summary>
        public double SnapAngleDeg = 0.05;
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
    public static partial class SurfaceRecognizer
    {
        // Marcas do vetor 'region', todas negativas para não colidir com o índice da região.
        private const int Free = -1;      // livre, ainda não tentado
        private const int Dead = -2;      // triângulo degenerado
        private const int TriedSeed = -3; // já foi semente e não deu região
        private const int InQueue = -4;   // dentro do crescimento em curso
        private const int Patched = -5;   // já está num retalho plano de componente misto

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
                                          ?? TryCylinder(comp, nx, ny, nz, area, pointsMm, o)
                                          ?? TryCone(comp, nx, ny, nz, area, pointsMm, o);
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

            CompactRemoved(result, region);
            MergeIntoTori(result, region, neighbours, nx, ny, nz, area, pointsMm, o);
            MergeCoaxialFragments(result, region, neighbours, area, pointsMm, o);
            CompactRemoved(result, region);
            RecognizeFillets(result, region, neighbours, nx, ny, nz, area, pointsMm, o);
            CompactRemoved(result, region);

            foreach (RecognizedSurface s in result.Surfaces) Snap(s, o);

            // Ordena por área, e leva o mapa triângulo → superfície junto com a ordenação.
            var before = new List<RecognizedSurface>(result.Surfaces);
            result.Surfaces.Sort((a, b) => b.AreaMm2.CompareTo(a.AreaMm2));
            var newIndex = new Dictionary<RecognizedSurface, int>();
            for (int i = 0; i < result.Surfaces.Count; i++) newIndex[result.Surfaces[i]] = i;
            result.RegionOfTriangle = new int[triCount];
            for (int t = 0; t < triCount; t++)
                result.RegionOfTriangle[t] = region[t] >= 0 ? newIndex[before[region[t]]] : -1;
            return result;
        }

        // ------------------------------------------------------------------ arredondamento

        /// <summary>
        /// Leva a superfície ajustada para a medida de projeto: direção alinhada ao eixo do
        /// sistema quando está a menos de <see cref="RecognizerOptions.SnapAngleDeg"/> dele, e
        /// offset/Ø/posição do eixo no passo de <see cref="RecognizerOptions.RoundToMm"/>.
        /// O RMS do ajuste NÃO é recalculado: ele continua dizendo quão bem a malha coube na
        /// primitiva, que é a informação que o arredondamento não pode esconder.
        /// </summary>
        /// <summary>Eixo de cone a menos disto de X, Y ou Z vira o eixo (ver <see cref="Snap"/>).</summary>
        private const double ConeSnapDeg = 0.5;

        public static void Snap(RecognizedSurface s, RecognizerOptions o)
        {
            if (s == null || o == null) return;
            if (s.Kind == "plano" && s.Normal != null && s.PointMm != null)
            {
                s.Normal = SnapDirection(s.Normal, o.SnapAngleDeg);
                if (o.RoundToMm > 0)
                {
                    double[] n = s.Normal;
                    double d = n[0] * s.PointMm[0] + n[1] * s.PointMm[1] + n[2] * s.PointMm[2];
                    double shift = Round(d, o.RoundToMm) - d;
                    s.PointMm = new[] { s.PointMm[0] + shift * n[0], s.PointMm[1] + shift * n[1], s.PointMm[2] + shift * n[2] };
                }
            }
            else if (s.Kind == "cilindro" && s.AxisMm != null && s.PointMm != null)
            {
                s.AxisMm = SnapDirection(s.AxisMm, o.SnapAngleDeg);
                s.ArcFrom = SnapDirection(Perpendicularize(s.ArcFrom, s.AxisMm), ArcEndSnapDeg);
                s.ArcTo = SnapDirection(Perpendicularize(s.ArcTo, s.AxisMm), ArcEndSnapDeg);
                ResweepArc(s);
                if (o.RoundToMm > 0)
                {
                    // Ø arredondado no passo (é o Ø que a ferramentaria cota), e o raio sai dele.
                    s.RadiusMm = Round(2 * s.RadiusMm, o.RoundToMm) / 2.0;
                    // Posição do eixo: só as coordenadas PERPENDICULARES a um eixo alinhado se
                    // arredondam — arredondar ao longo do eixo não move a superfície, e num eixo
                    // inclinado arredondar coordenada por coordenada tiraria o ponto da reta.
                    int along = AlignedAxis(s.AxisMm);
                    if (along >= 0)
                    {
                        var c = (double[])s.PointMm.Clone();
                        for (int k = 0; k < 3; k++) if (k != along) c[k] = Round(c[k], o.RoundToMm);
                        s.PointMm = c;
                    }
                }
            }
            else if ((s.Kind == "toro" || s.Kind == "esfera") && s.AxisMm != null && s.PointMm != null)
            {
                s.AxisMm = SnapDirection(s.AxisMm, o.SnapAngleDeg);
                s.ArcFrom = SnapDirection(Perpendicularize(s.ArcFrom, s.AxisMm), ArcEndSnapDeg);
                s.ArcTo = SnapDirection(Perpendicularize(s.ArcTo, s.AxisMm), ArcEndSnapDeg);
                if (o.RoundToMm > 0)
                {
                    // O raio do perfil é o que se cota (R2,5); o raio maior também sai do desenho
                    // (R3 da ponta − R2,5 = 0,5). Os dois no passo, como o Ø do cilindro.
                    s.RadiusMm = Round(2 * s.RadiusMm, o.RoundToMm) / 2.0;
                    s.MajorRadiusMm = Round(2 * s.MajorRadiusMm, o.RoundToMm) / 2.0;
                    int along = AlignedAxis(s.AxisMm);
                    var c = (double[])s.PointMm.Clone();
                    for (int k = 0; k < 3; k++) c[k] = Round(c[k], o.RoundToMm);
                    if (along >= 0) s.PointMm = c;
                }
                s.TubeFromDeg = Math.Round(s.TubeFromDeg, 1);
                s.TubeToDeg = Math.Round(s.TubeToDeg, 1);
            }
            else if (s.Kind == "cone" && s.AxisMm != null && s.ApexMm != null)
            {
                // O eixo do cone sai do ajuste com mais erro que o do cilindro — o recorte cônico
                // em U da alça do stack-container (meio cone) saiu 0,27° fora de Y, e assim nem
                // era "deitado" para a correção. A menos de ConeSnapDeg de um eixo, vira ele, mas
                // GIRANDO EM VOLTA DO MEIO DA SUPERFÍCIE (não do ápice, a 13 mm dali): o giro
                // mexe na superfície só o braço de meia largura × o ângulo (~0,006 mm ali).
                double[] old = s.AxisMm;
                double[] snapped = SnapDirection(old, Math.Max(o.SnapAngleDeg, ConeSnapDeg));
                if (s.PointMm != null && (snapped[0] != old[0] || snapped[1] != old[1] || snapped[2] != old[2]))
                {
                    double hMid = (s.PointMm[0] - s.ApexMm[0]) * old[0] + (s.PointMm[1] - s.ApexMm[1]) * old[1] + (s.PointMm[2] - s.ApexMm[2]) * old[2];
                    s.ApexMm = new[] { s.PointMm[0] - hMid * snapped[0], s.PointMm[1] - hMid * snapped[1], s.PointMm[2] - hMid * snapped[2] };
                }
                s.AxisMm = snapped;
                if (s.ArcFrom != null) s.ArcFrom = SnapDirection(Perpendicularize(s.ArcFrom, s.AxisMm), ArcEndSnapDeg);
                if (s.ArcTo != null) s.ArcTo = SnapDirection(Perpendicularize(s.ArcTo, s.AxisMm), ArcEndSnapDeg);
                // Semi-ângulo de projeto é grau redondo (45°, 30°, 1° de saída): a menos de 0,05°
                // dele, vira ele; senão fica no centésimo de grau.
                double whole = Math.Round(s.HalfAngleDeg);
                s.HalfAngleDeg = Math.Abs(s.HalfAngleDeg - whole) <= 0.05 ? whole : Math.Round(s.HalfAngleDeg, 2);
                if (o.RoundToMm > 0)
                {
                    s.RadiusMinMm = Round(2 * s.RadiusMinMm, o.RoundToMm) / 2.0;
                    s.RadiusMaxMm = Round(2 * s.RadiusMaxMm, o.RoundToMm) / 2.0;
                    s.RadiusMm = Round(2 * s.RadiusMm, o.RoundToMm) / 2.0;
                    int along = AlignedAxis(s.AxisMm);
                    if (along >= 0)
                    {
                        var c = (double[])s.ApexMm.Clone();
                        for (int k = 0; k < 3; k++) if (k != along) c[k] = Round(c[k], o.RoundToMm);
                        s.ApexMm = c;
                        var q = (double[])s.PointMm.Clone();
                        for (int k = 0; k < 3; k++) if (k != along) q[k] = c[k];
                        s.PointMm = q;
                    }
                }
            }
        }

        /// <summary>Ponta de arco a menos disto de X/Y/Z vira exatamente o eixo: o raio de 90° da
        /// alça da malha 2 saía de 90,4° e virava dois cantos de 45,2° na construção.</summary>
        private const double ArcEndSnapDeg = 1.0;

        /// <summary>Volta do arco recalculada depois de as pontas serem alinhadas.</summary>
        private static void ResweepArc(RecognizedSurface s)
        {
            if (s.ArcFrom == null || s.ArcTo == null || s.AxisMm == null) return;
            double[] g = Cross(s.AxisMm, s.ArcFrom);
            double c = s.ArcTo[0] * s.ArcFrom[0] + s.ArcTo[1] * s.ArcFrom[1] + s.ArcTo[2] * s.ArcFrom[2];
            double sn = s.ArcTo[0] * g[0] + s.ArcTo[1] * g[1] + s.ArcTo[2] * g[2];
            double a = Math.Atan2(sn, c);
            if (a <= 0) a += 2 * Math.PI;
            s.SweepDeg = a * 180 / Math.PI;
        }

        private static double[] SnapDirection(double[] v, double snapDeg)
        {
            if (v == null || snapDeg <= 0) return v;
            double cosSnap = Math.Cos(snapDeg * Math.PI / 180.0);
            for (int k = 0; k < 3; k++)
                if (Math.Abs(v[k]) >= cosSnap)
                {
                    var e = new double[3];
                    e[k] = Math.Sign(v[k]);
                    return e;
                }

            // Fora dos eixos, ainda assim: parede "vertical" de normal (−0,625; −0,781; 0,0003)
            // é vertical — a componente que está a menos do ângulo de snap de zero vira zero, e
            // não aparece como "saída de 0,02°" (corpo 3 do Rack_Anghinoni).
            double sinSnap = Math.Sin(snapDeg * Math.PI / 180.0);
            var w = (double[])v.Clone();
            bool changed = false;
            for (int k = 0; k < 3; k++)
                if (w[k] != 0 && Math.Abs(w[k]) < sinSnap) { w[k] = 0; changed = true; }
            if (!changed) return v;
            double len = Math.Sqrt(w[0] * w[0] + w[1] * w[1] + w[2] * w[2]);
            return new[] { w[0] / len, w[1] / len, w[2] / len };
        }

        /// <summary>Índice do eixo do sistema ao qual a direção é EXATAMENTE paralela, ou -1.</summary>
        private static int AlignedAxis(double[] v)
        {
            for (int k = 0; k < 3; k++)
                if (Math.Abs(v[k]) == 1.0 && v[(k + 1) % 3] == 0 && v[(k + 2) % 3] == 0) return k;
            return -1;
        }

        private static double Round(double value, double step)
        {
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
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
        /// Componente que não é um plano nem um cilindro inteiro — o caso do RAIO e da PONTA DE
        /// RASGO: a superfície curva é tangente às faces planas, não há quina entre elas, e todas
        /// vieram no mesmo componente.
        ///
        /// A ORDEM AQUI É CILINDRO PRIMEIRO, e o motivo foi medido na malha real (2026-10-09): a
        /// versão anterior descascava planos primeiro, e num cilindro tesselado cada FAIXA é um
        /// plano perfeito de dois triângulos — 276 das 302 regiões pequenas da malha 4 do
        /// Rack_Anghinoni eram faixas das pontas Ø14 dos rasgos. Agora:
        ///   1. o componente é partido em retalhos planos (coplanares por crescimento);
        ///   2. cada retalho pequeno, junto com os vizinhos de tamanho parecido, semeia um
        ///      cilindro, que cresce pelos retalhos vizinhos enquanto TODO vértice novo couber na
        ///      tolerância. Um plano tangente é recusado sozinho: os vértices dele longe da
        ///      tangência saem do cilindro;
        ///   3. o que sobra vira plano — exceto a cadeia de faixas estreitas que nenhum cilindro
        ///      explicou (cone, toro, esfera), que vai para "livre" em vez de virar mosaico.
        /// </summary>
        /// <summary>
        /// Superfície curva que dispensa o teste de "regrada": arco de 90° ou mais com 12
        /// triângulos ou mais. O teste existe contra o cilindro falso feito de retalho de
        /// concordância (pequeno, de poucos graus); mas STL de fatiador costuma ter a tesselação
        /// TORCIDA — os vértices da boca de dentro e da de fora em ângulos diferentes —, e o recorte
        /// cônico da alça do stack-container (172°, 50 triângulos, RMS 0,001 mm, 2026-10-11) saía
        /// "livre" e virava facetas no sólido.
        /// </summary>
        private static RecognizedSurface Broad(RecognizedSurface s, List<int> tris)
        {
            return s != null && s.SweepDeg >= 90 && tris.Count >= 12 ? s : null;
        }

        /// <summary>Ângulo máximo entre duas faixas vizinhas da mesma cadeia de superfície curva.</summary>
        private static readonly double CosChainJoint = Math.Cos(25.0 * Math.PI / 180.0);

        private static void SplitMixed(List<int> comp, List<int>[] neighbours, int[] region, int[] component,
            double[] nx, double[] ny, double[] nz, double[] cx, double[] cy, double[] cz, double[] area,
            double[] p, double cosPlane, double cosCrease, RecognizerOptions o, RecognitionResult result)
        {
            int compId = component[comp[0]];

            // ------------------------------------------------ 1. retalhos planos
            double cosPatch = Math.Cos(o.PlaneAngleToleranceDeg / 4 * Math.PI / 180.0);
            var byArea = new List<int>(comp);
            byArea.Sort((x, y) => area[y].CompareTo(area[x]));

            var patchOf = new Dictionary<int, int>(comp.Count);
            var patches = new List<List<int>>();
            foreach (int seed in byArea)
            {
                if (patchOf.ContainsKey(seed)) continue;
                // Tolerância APERTADA aqui dentro: o raio de concordância nasce tangente à parede,
                // e com os 2° / 0,05 mm do plano solto o retalho atravessava da faceta vertical
                // para o raio de cima (perna do corpo 3: a última faceta da ponta R1 virou um
                // "plano" de Z 4 a 40 e saiu do cilindro). STL de CAD tem plano de RMS zero; malha
                // de scanner, que pede tolerância maior, continua proporcional.
                List<int> grown = GrowPlane(seed, neighbours, region, component, compId,
                                            nx, ny, nz, cx, cy, cz, area, p, cosPatch,
                                            o.PlaneDistanceToleranceMm / 5);
                foreach (int t in grown) { region[t] = Patched; patchOf[t] = patches.Count; }
                patches.Add(grown);
            }

            int np = patches.Count;
            var patchArea = new double[np];
            for (int i = 0; i < np; i++) foreach (int t in patches[i]) patchArea[i] += area[t];

            var adj = new HashSet<int>[np];
            for (int i = 0; i < np; i++) adj[i] = new HashSet<int>();
            foreach (int t in comp)
                foreach (int nb in neighbours[t])
                {
                    int pa, pb;
                    if (!patchOf.TryGetValue(t, out pa) || !patchOf.TryGetValue(nb, out pb) || pa == pb) continue;
                    adj[pa].Add(pb);
                    adj[pb].Add(pa);
                }

            // ------------------------------------------------ 2. cilindros por crescimento
            var cylMembers = new Dictionary<int, List<int>>();
            var owner = new int[np];
            for (int i = 0; i < np; i++) owner[i] = -1;

            var ascending = new List<int>();
            for (int i = 0; i < np; i++) ascending.Add(i);
            ascending.Sort((x, y) => patchArea[x].CompareTo(patchArea[y]));

            foreach (int seedPatch in ascending)
            {
                if (owner[seedPatch] >= 0 || adj[seedPatch].Count == 0) continue;

                List<int> cluster = SeedCluster(seedPatch, adj, owner, patches, patchArea, nx, ny, nz, area);
                if (cluster == null) continue;

                RecognizedSurface cyl = TryCylinder(Gather(cluster, patches), nx, ny, nz, area, p, o, true);
                if (cyl == null) continue;

                // Crescimento com REAJUSTE: o ajuste de 3 retalhos erra o raio no centésimo (o R2,5
                // da alça da malha 2 saía R2,51), e testar cada vizinho contra esse ajuste velho
                // recusava justamente as duas facetas de baixo do raio, que viravam "faces
                // inclinadas" de 7,5° e 22,5°. Agora o vizinho entra se o cilindro REAJUSTADO com
                // ele continua cabendo; o teste rápido (tolerância folgada) só poupa o reajuste.
                var inCluster = new HashSet<int>(cluster);
                var members = new List<int>(cluster);
                var frontier = new Queue<int>(cluster);
                var loose = new RecognizerOptions
                {
                    CylinderAbsoluteToleranceMm = 5 * o.CylinderAbsoluteToleranceMm,
                    CylinderRelativeTolerance = 5 * o.CylinderRelativeTolerance,
                    PlaneAngleToleranceDeg = 3 * o.PlaneAngleToleranceDeg
                };
                while (frontier.Count > 0)
                {
                    int c = frontier.Dequeue();
                    foreach (int q in adj[c])
                    {
                        if (owner[q] >= 0 || inCluster.Contains(q)) continue;
                        if (!FitsCylinder(patches[q], cyl, nx, ny, nz, p, loose)) continue;
                        members.Add(q);
                        RecognizedSurface refit = TryCylinder(Gather(members, patches), nx, ny, nz, area, p, o, true);
                        if (refit == null) { members.RemoveAt(members.Count - 1); continue; }
                        cyl = refit;
                        inCluster.Add(q);
                        frontier.Enqueue(q);
                    }
                }

                RecognizedSurface final = cyl;

                int id = result.Surfaces.Count;
                foreach (int m in members)
                {
                    owner[m] = id;
                    foreach (int t in patches[m]) region[t] = id;
                }
                result.Surfaces.Add(final);
                cylMembers[id] = members;
            }

            // ------------------------------------------------ 2b. disputa entre cilindros
            // Cilindro de POUCOS retalhos devolve cada retalho que cabe num cilindro vizinho
            // maior. Na perna do corpo 3 a alma de 45° e as últimas facetas das duas pontas R1
            // (três facetas em ângulos vizinhos sempre cabem num círculo grande) viraram um
            // "cilindro R10,04 de 18°" antes que o R1 crescesse até elas — as sementes vão da
            // menor para a maior. O que sobra com menos de 3 retalhos se desfaz e volta a plano.
            foreach (var small in new List<KeyValuePair<int, List<int>>>(cylMembers))
            {
                if (small.Value.Count > 3) continue;
                foreach (int m in new List<int>(small.Value))
                {
                    foreach (int q in adj[m])
                    {
                        int big = owner[q];
                        if (big < 0 || big == small.Key || !cylMembers.ContainsKey(big)) continue;
                        if (cylMembers[big].Count <= small.Value.Count) continue;
                        if (!FitsCylinder(patches[m], result.Surfaces[big], nx, ny, nz, p, o)) continue;
                        var grown = new List<int>(cylMembers[big]) { m };
                        RecognizedSurface refit = TryCylinder(Gather(grown, patches), nx, ny, nz, area, p, o, true);
                        if (refit == null) continue;
                        result.Surfaces[big] = refit;
                        cylMembers[big] = grown;
                        small.Value.Remove(m);
                        owner[m] = big;
                        foreach (int t in patches[m]) region[t] = big;
                        break;
                    }
                }
                if (small.Value.Count >= 3)
                {
                    RecognizedSurface refit = TryCylinder(Gather(small.Value, patches), nx, ny, nz, area, p, o, true);
                    if (refit != null) { result.Surfaces[small.Key] = refit; continue; }
                }
                // Desfeito: os retalhos voltam a ser de ninguém e o passo 3 decide (plano).
                foreach (int m in small.Value)
                {
                    owner[m] = -1;
                    foreach (int t in patches[m]) region[t] = Patched;
                }
                small.Value.Clear();
                result.Surfaces[small.Key] = null;
                cylMembers.Remove(small.Key);
            }

            // ------------------------------------------------ 3. o que sobrou
            // Faixa que nenhum cilindro explicou: retalho sobrando com dois ou mais vizinhos
            // também sobrando. É cadeia de superfície curva (cone, toro, esfera) — vai inteira
            // para "livre", nunca para plano, porque plano aqui seria mosaico.
            var strip = new bool[np];
            for (int i = 0; i < np; i++)
            {
                if (owner[i] >= 0) continue;
                int left = 0;
                foreach (int q in adj[i]) if (owner[q] < 0) left++;
                strip[i] = left >= 2 && patches[i].Count <= 4;
            }

            var seenPatch = new bool[np];
            for (int i = 0; i < np; i++)
            {
                if (owner[i] >= 0 || seenPatch[i]) continue;
                seenPatch[i] = true;

                if (!strip[i])
                {
                    int id = result.Surfaces.Count;
                    foreach (int t in patches[i]) region[t] = id;
                    result.Surfaces.Add(MakePlane(patches[i], nx, ny, nz, area, p));
                    continue;
                }

                // A cadeia só junta faixas de ângulo de TESSELAÇÃO entre si (até 25°): o ressalto de
                // encaixe da Enclosure Bottom (2026-10-11) tem a face de baixo e as duas pontas
                // triangulares a 41° umas das outras — faces distintas, não facetas de uma curva —,
                // e juntas viravam uma região "livre" que nem parede nem correção tratavam.
                var chain = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    chain.Add(c);
                    double[] nc = PatchNormal(patches[c], nx, ny, nz, area);
                    foreach (int q in adj[c])
                    {
                        if (owner[q] >= 0 || seenPatch[q] || !strip[q]) continue;
                        double[] nq = PatchNormal(patches[q], nx, ny, nz, area);
                        if (nc[0] * nq[0] + nc[1] * nq[1] + nc[2] * nq[2] < CosChainJoint) continue;
                        seenPatch[q] = true;
                        queue.Enqueue(q);
                    }
                }

                // Cadeia com faixas muito maiores que as outras (as laterais planas do recorte em U
                // da alça do stack-container, tangentes ao cone): sem elas o resto pode ser UMA
                // superfície curva; elas viram plano cada uma.
                if (chain.Count >= 6)
                {
                    var areas = chain.ConvertAll(c => patchArea[c]);
                    areas.Sort();
                    double median = areas[areas.Count / 2];
                    var core = chain.FindAll(c => patchArea[c] <= 3 * median);
                    if (core.Count < chain.Count)
                    {
                        List<int> coreTris = Gather(core, patches);
                        RecognizedSurface curved = coreTris.Count >= 12
                            ? (TryCylinder(coreTris, nx, ny, nz, area, p, o, true) ?? TryCone(coreTris, nx, ny, nz, area, p, o, true)
                               ?? Broad(TryCylinder(coreTris, nx, ny, nz, area, p, o), coreTris)
                               ?? Broad(TryCone(coreTris, nx, ny, nz, area, p, o), coreTris))
                            : null;
                        if (curved != null)
                        {
                            int cid = result.Surfaces.Count;
                            foreach (int t in coreTris) region[t] = cid;
                            result.Surfaces.Add(curved);
                            foreach (int c in chain)
                            {
                                if (core.Contains(c)) continue;
                                int pid = result.Surfaces.Count;
                                foreach (int t in patches[c]) region[t] = pid;
                                result.Surfaces.Add(MakePlane(patches[c], nx, ny, nz, area, p));
                            }
                            continue;
                        }
                    }
                }

                List<int> tris = Gather(chain, patches);
                // Faixa estreita que é UM plano (a alma de 45° da perna do corpo 3: 4 triângulos de
                // Z 4 a 40) é plano, não forma livre.
                RecognizedSurface s = TryCylinder(tris, nx, ny, nz, area, p, o, true)
                                      ?? TryCone(tris, nx, ny, nz, area, p, o, true)
                                      ?? Broad(TryCylinder(tris, nx, ny, nz, area, p, o), tris)
                                      ?? Broad(TryCone(tris, nx, ny, nz, area, p, o), tris)
                                      ?? TryPlaneWhole(tris, nx, ny, nz, area, p, cosPlane, o.PlaneDistanceToleranceMm)
                                      ?? MakeFreeForm(tris, area, p);
                int sid = result.Surfaces.Count;
                foreach (int t in tris) region[t] = sid;
                if (s.Kind == "livre") result.FreeFormAreaMm2 += s.AreaMm2;
                result.Surfaces.Add(s);
            }
        }

        /// <summary>Tira as superfícies desfeitas (null) da lista e refaz o mapa triângulo →
        /// superfície. Triângulo de superfície desfeita sem dono novo vira −1.</summary>
        private static void CompactRemoved(RecognitionResult result, int[] region)
        {
            if (!result.Surfaces.Contains(null)) return;
            var map = new int[result.Surfaces.Count];
            var kept = new List<RecognizedSurface>();
            for (int i = 0; i < result.Surfaces.Count; i++)
            {
                if (result.Surfaces[i] == null) { map[i] = -1; continue; }
                map[i] = kept.Count;
                kept.Add(result.Surfaces[i]);
            }
            for (int t = 0; t < region.Length; t++) if (region[t] >= 0) region[t] = map[region[t]];
            result.Surfaces.Clear();
            result.Surfaces.AddRange(kept);
        }

        // ------------------------------------------------------------------ toro

        /// <summary>
        /// Junta em TORO os pedaços que o crescimento de cilindro deixou picados. O raio que
        /// contorna uma curva (o R2,5 do topo da alça da malha 2 dando a volta na ponta R3) é um
        /// toro, e cada faixa de meridiano dele cabe sozinha num "cilindro" curto de eixo
        /// deitado — a ponta saía em ~12 cilindros R2,495 de eixos diferentes, mais retalhos
        /// livres. Candidato: cilindro CURTO (comprimento menor que o raio) ou região livre;
        /// vizinhos candidatos entram no grupo enquanto a união ainda couber num toro.
        /// </summary>
        private static void MergeIntoTori(RecognitionResult result, int[] region, List<int>[] neighbours,
            double[] nx, double[] ny, double[] nz, double[] area, double[] p, RecognizerOptions o)
        {
            int ns = result.Surfaces.Count;
            var tris = new List<int>[ns];
            for (int i = 0; i < ns; i++) tris[i] = new List<int>();
            for (int t = 0; t < region.Length; t++) if (region[t] >= 0) tris[region[t]].Add(t);

            var candidate = new bool[ns];
            for (int i = 0; i < ns; i++)
            {
                RecognizedSurface s = result.Surfaces[i];
                candidate[i] = s.Kind == "livre" || (s.Kind == "cilindro" && s.SweepDeg < 360 && s.LengthMm < s.RadiusMm);
            }

            var adj = new HashSet<int>[ns];
            var anyAdj = new HashSet<int>[ns];
            for (int i = 0; i < ns; i++) { adj[i] = new HashSet<int>(); anyAdj[i] = new HashSet<int>(); }
            for (int t = 0; t < region.Length; t++)
            {
                if (region[t] < 0) continue;
                foreach (int nb in neighbours[t])
                {
                    if (region[nb] < 0 || region[nb] == region[t]) continue;
                    anyAdj[region[t]].Add(region[nb]);
                    if (candidate[region[t]] && candidate[region[nb]]) adj[region[t]].Add(region[nb]);
                }
            }

            var used = new bool[ns];
            var order = new List<int>();
            for (int i = 0; i < ns; i++) if (candidate[i]) order.Add(i);
            order.Sort((a, b) => result.Surfaces[b].AreaMm2.CompareTo(result.Surfaces[a].AreaMm2));

            var removed = new bool[ns];
            var added = new List<Tuple<RecognizedSurface, List<int>>>();
            // 1º o grupo CONECTADO inteiro de uma vez. Ajustar par a par não serve: as normais de
            // duas faixas de meridiano cortam também a reta que liga os centros dos dois perfis,
            // e o eixo sai indeterminado (a malha 2 dava eixos "(0,92; −0,39; −0,07)").
            // Componente que NÃO coube num toro não muda depois: o que vira toro some inteiro, então
            // nenhum vizinho dele é consumido pela metade. Sem esta marca, cada semente de um
            // componente grande refazia o mesmo ajuste — com centenas de raios R0,5 encostados uns
            // nos outros (vector_v2-6, 65 mil triângulos) eram quase 2 minutos aqui.
            var failed = new bool[ns];
            foreach (int seed in order)
            {
                if (used[seed] || failed[seed]) continue;
                var compGroup = new List<int>();
                var q0 = new Queue<int>();
                q0.Enqueue(seed);
                var seenC = new HashSet<int> { seed };
                while (q0.Count > 0)
                {
                    int c = q0.Dequeue();
                    compGroup.Add(c);
                    foreach (int r in adj[c]) if (!used[r] && seenC.Add(r)) q0.Enqueue(r);
                }
                if (compGroup.Count < 3) continue;
                var allC = new List<int>();
                foreach (int g in compGroup) allC.AddRange(tris[g]);
                RecognizedSurface whole = TryTorus(allC, nx, ny, nz, area, p, o);
                if (whole == null) { foreach (int g in compGroup) failed[g] = true; continue; }
                foreach (int g in compGroup) { used[g] = true; removed[g] = true; }
                added.Add(Tuple.Create(whole, allC));
            }

            // 2º, o que sobrou: cresce a partir de uma semente que já começa com os vizinhos
            // até 4 pedaços, para o primeiro ajuste já ter eixo determinado.
            foreach (int seed in order)
            {
                if (used[seed]) continue;
                var group = new List<int> { seed };
                var tried = new HashSet<int> { seed };
                var ring = new Queue<int>(adj[seed]);
                while (ring.Count > 0 && group.Count < 4)
                {
                    int q = ring.Dequeue();
                    if (used[q] || !tried.Add(q)) continue;
                    group.Add(q);
                    foreach (int r in adj[q]) if (!tried.Contains(r)) ring.Enqueue(r);
                }
                if (group.Count < 4) continue;
                var all = new List<int>();
                foreach (int g in group) all.AddRange(tris[g]);
                RecognizedSurface torus = TryTorus(all, nx, ny, nz, area, p, o);
                if (torus == null) continue;
                var frontier = new Queue<int>(ring);
                while (frontier.Count > 0)
                {
                    int q = frontier.Dequeue();
                    if (used[q] || !tried.Add(q)) continue;
                    var union = new List<int>(all);
                    union.AddRange(tris[q]);
                    RecognizedSurface fit = TryTorus(union, nx, ny, nz, area, p, o);
                    if (fit == null) continue;
                    torus = fit;
                    all = union;
                    group.Add(q);
                    foreach (int r in adj[q]) if (!tried.Contains(r)) frontier.Enqueue(r);
                }
                if (torus == null || group.Count < 2) continue;

                foreach (int g in group) { used[g] = true; removed[g] = true; }
                added.Add(Tuple.Create(torus, all));
            }
            if (added.Count == 0) return;

            // Absorção: vizinho de QUALQUER tipo (a fileira de perto do eixo, quase plana, saía
            // como "cone" e "planos" pequenos) entra no toro se a união ainda couber nele. Face
            // plana de verdade não cabe — os vértices dela saem do toro — e fica de fora.
            for (int k = 0; k < added.Count; k++)
            {
                var all = added[k].Item2;
                var inAll = new HashSet<int>(all);
                var mine = new HashSet<int>();
                for (int i = 0; i < ns; i++) if (removed[i]) foreach (int t in tris[i]) if (inAll.Contains(t)) { mine.Add(i); break; }
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    var around = new HashSet<int>();
                    foreach (int i in mine) foreach (int q in anyAdj[i]) if (!removed[q]) around.Add(q);
                    foreach (int q in around)
                    {
                        if (result.Surfaces[q].AreaMm2 > added[k].Item1.AreaMm2) continue;
                        var union = new List<int>(all);
                        union.AddRange(tris[q]);
                        RecognizedSurface fit = TryTorus(union, nx, ny, nz, area, p, o);
                        if (fit == null) continue;
                        removed[q] = true;
                        mine.Add(q);
                        all = union;
                        added[k] = Tuple.Create(fit, all);
                        grew = true;
                    }
                }
            }

            // Compacta: tira os pedaços absorvidos, põe os toros, e refaz o mapa triângulo → superfície.
            var map = new int[ns];
            var kept = new List<RecognizedSurface>();
            for (int i = 0; i < ns; i++)
            {
                if (removed[i])
                {
                    map[i] = -1;
                    if (result.Surfaces[i].Kind == "livre") result.FreeFormAreaMm2 -= result.Surfaces[i].AreaMm2;
                    continue;
                }
                map[i] = kept.Count;
                kept.Add(result.Surfaces[i]);
            }
            for (int t = 0; t < region.Length; t++) if (region[t] >= 0) region[t] = map[region[t]];
            foreach (var a in added)
            {
                foreach (int t in a.Item2) region[t] = kept.Count;
                kept.Add(a.Item1);
            }
            result.Surfaces.Clear();
            result.Surfaces.AddRange(kept);
        }

        /// <summary>
        /// Ajuste de TORO (superfície de revolução de perfil circular). Toda normal de uma
        /// superfície de revolução CORTA o eixo, e "a reta L corta a reta da normal" é linear nas
        /// coordenadas de Plücker de L: d·(c × n) + n·m = 0. O eixo é o vetor singular de menor
        /// valor do sistema 6×6 (Pottmann). Com o eixo, cada vértice vai para o meridiano
        /// (ρ, h), e o perfil é um círculo (Kåsa): o centro dá o raio maior, o raio do círculo é
        /// o RAIO que o projetista cotou. Devolve null se não couber na tolerância.
        /// </summary>
        public static RecognizedSurface TryTorus(List<int> tris, double[] nx, double[] ny, double[] nz,
            double[] area, double[] p, RecognizerOptions o)
        {
            if (tris.Count < 6) return null;

            var ata = new double[6, 6];
            foreach (int t in tris)
            {
                double cxT = 0, cyT = 0, czT = 0;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    cxT += p[b] / 3.0; cyT += p[b + 1] / 3.0; czT += p[b + 2] / 3.0;
                }
                double[] n = { nx[t], ny[t], nz[t] };
                double[] cn = Cross(new[] { cxT, cyT, czT }, n);
                double[] row = { cn[0], cn[1], cn[2], n[0], n[1], n[2] };
                double w = area[t];
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 6; j++) ata[i, j] += w * row[i] * row[j];
            }

            double[] x = SmallestEigenvectorN(ata, 6);
            if (x == null) return null;
            double[] d = { x[0], x[1], x[2] }, m = { x[3], x[4], x[5] };
            double dl = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (dl < 1e-9) return null;
            d[0] /= dl; d[1] /= dl; d[2] /= dl;
            m[0] /= dl; m[1] /= dl; m[2] /= dl;
            double[] p0 = Cross(d, m); // pé da perpendicular da origem ao eixo
            int dom = Math.Abs(d[0]) >= Math.Abs(d[1]) ? (Math.Abs(d[0]) >= Math.Abs(d[2]) ? 0 : 2) : (Math.Abs(d[1]) >= Math.Abs(d[2]) ? 1 : 2);
            if (d[dom] < 0) { d[0] = -d[0]; d[1] = -d[1]; d[2] = -d[2]; }

            // Refino pelos VÉRTICES: na tesselação grossa (15° nas duas direções, malha 2) a
            // normal da faceta não passa exatamente pelo eixo e o Plücker erra uns 10°; os
            // vértices estão SOBRE o toro. Nelder–Mead em 4 parâmetros (inclinação do eixo e
            // posição dele), partindo do Plücker e do eixo X/Y/Z mais próximo dele.
            var verts = new double[tris.Count * 9];
            for (int i = 0; i < tris.Count; i++) Array.Copy(p, tris[i] * 9, verts, i * 9, 9);
            double bestRms = double.MaxValue;
            double[] bestD = d, bestP = p0;
            var starts = new List<double[]> { d };
            double[] aligned = new double[3];
            aligned[dom] = 1;
            starts.Add(aligned);
            foreach (double[] d0 in starts)
            {
                double[] u1 = Perpendicular(d0), u2 = Cross(d0, u1);
                // p0 levado para o pé da perpendicular ao longo do novo eixo
                double along = p0[0] * d0[0] + p0[1] * d0[1] + p0[2] * d0[2];
                double[] base0 = { p0[0] - along * d0[0], p0[1] - along * d0[1], p0[2] - along * d0[2] };
                Func<double[], double> cost = q =>
                {
                    double[] dd = { d0[0] + q[0] * u1[0] + q[1] * u2[0], d0[1] + q[0] * u1[1] + q[1] * u2[1], d0[2] + q[0] * u1[2] + q[1] * u2[2] };
                    double l = Math.Sqrt(dd[0] * dd[0] + dd[1] * dd[1] + dd[2] * dd[2]);
                    dd[0] /= l; dd[1] /= l; dd[2] /= l;
                    double[] pp = { base0[0] + q[2] * u1[0] + q[3] * u2[0], base0[1] + q[2] * u1[1] + q[3] * u2[1], base0[2] + q[2] * u1[2] + q[3] * u2[2] };
                    return ProfileCircleRms(verts, dd, pp);
                };
                double[] xb = NelderMead(cost, new double[4], new[] { 0.05, 0.05, 0.5, 0.5 }, 600);
                double c = cost(xb);
                if (c < bestRms)
                {
                    bestRms = c;
                    double[] dd = { d0[0] + xb[0] * u1[0] + xb[1] * u2[0], d0[1] + xb[0] * u1[1] + xb[1] * u2[1], d0[2] + xb[0] * u1[2] + xb[1] * u2[2] };
                    double l = Math.Sqrt(dd[0] * dd[0] + dd[1] * dd[1] + dd[2] * dd[2]);
                    bestD = new[] { dd[0] / l, dd[1] / l, dd[2] / l };
                    bestP = new[] { base0[0] + xb[2] * u1[0] + xb[3] * u2[0], base0[1] + xb[2] * u1[1] + xb[3] * u2[1], base0[2] + xb[2] * u1[2] + xb[3] * u2[2] };
                }
            }
            d = bestD;
            p0 = bestP;
            dom = Math.Abs(d[0]) >= Math.Abs(d[1]) ? (Math.Abs(d[0]) >= Math.Abs(d[2]) ? 0 : 2) : (Math.Abs(d[1]) >= Math.Abs(d[2]) ? 1 : 2);
            if (d[dom] < 0) { d[0] = -d[0]; d[1] = -d[1]; d[2] = -d[2]; }

            double[] e1 = Perpendicular(d);
            double[] e2 = Cross(d, e1);
            var rs = new List<double>();
            var hs = new List<double>();
            var around = new List<double>();
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double vx = p[b] - p0[0], vy = p[b + 1] - p0[1], vz = p[b + 2] - p0[2];
                    double h = vx * d[0] + vy * d[1] + vz * d[2];
                    double rx = vx - h * d[0], ry = vy - h * d[1], rz = vz - h * d[2];
                    rs.Add(Math.Sqrt(rx * rx + ry * ry + rz * rz));
                    hs.Add(h);
                    around.Add(Math.Atan2(rx * e2[0] + ry * e2[1] + rz * e2[2], rx * e1[0] + ry * e1[1] + rz * e1[2]));
                }

            // Kåsa no meridiano (ρ, h).
            int nPts = rs.Count;
            double su = 0, sv = 0, suu = 0, svv = 0, suv = 0, sr = 0, sru = 0, srv = 0;
            for (int i = 0; i < nPts; i++)
            {
                double u = rs[i], v = hs[i], rr = u * u + v * v;
                su += u; sv += v; suu += u * u; svv += v * v; suv += u * v;
                sr += rr; sru += rr * u; srv += rr * v;
            }
            double[] sol = Solve3(new[,] { { suu, suv, su }, { suv, svv, sv }, { su, sv, nPts } }, new[] { -sru, -srv, -sr });
            if (sol == null) return null;
            double r0 = -sol[0] / 2, h0 = -sol[1] / 2;
            double rsq = r0 * r0 + h0 * h0 - sol[2];
            if (rsq <= 1e-12) return null;
            double minor = Math.Sqrt(rsq);

            double sq = 0;
            var tube = new List<double>(nPts);
            for (int i = 0; i < nPts; i++)
            {
                double du = rs[i] - r0, dv = hs[i] - h0;
                double dd = Math.Sqrt(du * du + dv * dv) - minor;
                sq += dd * dd;
                tube.Add(Math.Atan2(dv, du));
            }
            double rms = Math.Sqrt(sq / nPts);
            if (rms > CylinderTolerance(minor, o)) return null;
            if (r0 < -1e-6) return null;

            double sweep = SweepDegrees(around);
            double tubeSweep = SweepDegrees(tube);
            if (sweep < o.MinCylinderSweepDeg || tubeSweep < o.MinCylinderSweepDeg) return null;

            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            double areaSum = 0;
            foreach (int t in tris)
            {
                areaSum += area[t];
                for (int k = 0; k < 3; k++) { int b = t * 9 + k * 3; Span(min, max, p[b], p[b + 1], p[b + 2]); }
            }

            double[] arcFrom = null, arcTo = null;
            if (sweep < 360.0)
            {
                double f, tt;
                ArcEnds(around, out f, out tt);
                arcFrom = Combine(e1, e2, Math.Cos(f), Math.Sin(f));
                arcTo = Combine(e1, e2, Math.Cos(tt), Math.Sin(tt));
            }
            double tf, tto;
            ArcEnds(tube, out tf, out tto);

            // Raio maior zero: o perfil passa pelo eixo e a superfície é uma ESFERA.
            bool sphere = r0 < Math.Max(0.01, 0.005 * minor);
            return new RecognizedSurface
            {
                Kind = sphere ? "esfera" : "toro",
                TriangleCount = tris.Count,
                AreaMm2 = areaSum,
                AxisMm = d,
                PointMm = new[] { p0[0] + h0 * d[0], p0[1] + h0 * d[1], p0[2] + h0 * d[2] },
                RadiusMm = minor,
                MajorRadiusMm = sphere ? 0 : r0,
                SweepDeg = sweep,
                ArcFrom = arcFrom,
                ArcTo = arcTo,
                TubeFromDeg = tf * 180 / Math.PI,
                TubeToDeg = tto * 180 / Math.PI,
                LengthMm = 0,
                FitRmsMm = rms,
                MinMm = min,
                MaxMm = max
            };
        }

        /// <summary>RMS do círculo (Kåsa) que melhor passa pelos vértices no meridiano (ρ, h) do
        /// eixo dado — o custo do refino do toro.</summary>
        private static double ProfileCircleRms(double[] verts, double[] d, double[] p0)
        {
            // Duas passadas sobre os vértices em vez de guardar (ρ, h): é a função que o
            // Nelder–Mead chama milhares de vezes, e alocar por chamada dominava o tempo.
            int n = verts.Length / 3;
            double dx = d[0], dy = d[1], dz = d[2], ox = p0[0], oy = p0[1], oz = p0[2];
            double su = 0, sv = 0, suu = 0, svv = 0, suv = 0, sr = 0, sru = 0, srv = 0;
            for (int i = 0; i < n; i++)
            {
                double vx = verts[3 * i] - ox, vy = verts[3 * i + 1] - oy, vz = verts[3 * i + 2] - oz;
                double h = vx * dx + vy * dy + vz * dz;
                double rx = vx - h * dx, ry = vy - h * dy, rz = vz - h * dz;
                double u = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                double rr = u * u + h * h;
                su += u; sv += h; suu += u * u; svv += h * h; suv += u * h;
                sr += rr; sru += rr * u; srv += rr * h;
            }
            double[] sol = Solve3(new[,] { { suu, suv, su }, { suv, svv, sv }, { su, sv, n } }, new[] { -sru, -srv, -sr });
            if (sol == null) return double.MaxValue;
            double r0 = -sol[0] / 2, h0 = -sol[1] / 2, rsq = r0 * r0 + h0 * h0 - sol[2];
            if (rsq <= 0) return double.MaxValue;
            double r = Math.Sqrt(rsq), sq = 0;
            for (int i = 0; i < n; i++)
            {
                double vx = verts[3 * i] - ox, vy = verts[3 * i + 1] - oy, vz = verts[3 * i + 2] - oz;
                double h = vx * dx + vy * dy + vz * dz;
                double rx = vx - h * dx, ry = vy - h * dy, rz = vz - h * dz;
                double u = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                double dd = Math.Sqrt((u - r0) * (u - r0) + (h - h0) * (h - h0)) - r;
                sq += dd * dd;
            }
            return Math.Sqrt(sq / n);
        }

        /// <summary>Nelder–Mead simples (sem dependência externa: 4 parâmetros não justificam uma).</summary>
        private static double[] NelderMead(Func<double[], double> f, double[] x0, double[] step, int iterations)
        {
            int n = x0.Length;
            var pts = new double[n + 1][];
            var val = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                pts[i] = (double[])x0.Clone();
                if (i > 0) pts[i][i - 1] += step[i - 1];
                val[i] = f(pts[i]);
            }
            for (int it = 0; it < iterations; it++)
            {
                Array.Sort(val, pts);
                if (Math.Abs(val[n] - val[0]) < 1e-12) break;
                var c = new double[n];
                for (int i = 0; i < n; i++) for (int k = 0; k < n; k++) c[k] += pts[i][k] / n;
                Func<double, double[]> along = a => { var x = new double[n]; for (int k = 0; k < n; k++) x[k] = c[k] + a * (pts[n][k] - c[k]); return x; };
                double[] xr = along(-1); double fr = f(xr);
                if (fr < val[0])
                {
                    double[] xe = along(-2); double fe = f(xe);
                    if (fe < fr) { pts[n] = xe; val[n] = fe; } else { pts[n] = xr; val[n] = fr; }
                }
                else if (fr < val[n - 1]) { pts[n] = xr; val[n] = fr; }
                else
                {
                    double[] xc = along(0.5); double fc = f(xc);
                    if (fc < val[n]) { pts[n] = xc; val[n] = fc; }
                    else
                        for (int i = 1; i <= n; i++)
                        {
                            for (int k = 0; k < n; k++) pts[i][k] = pts[0][k] + 0.5 * (pts[i][k] - pts[0][k]);
                            val[i] = f(pts[i]);
                        }
                }
            }
            Array.Sort(val, pts);
            return pts[0];
        }

        /// <summary>Autovetor do menor autovalor de uma simétrica n×n (Jacobi cíclico).</summary>
        public static double[] SmallestEigenvectorN(double[,] m, int n)
        {
            var a = (double[,])m.Clone();
            var v = new double[n, n];
            for (int i = 0; i < n; i++) v[i, i] = 1;
            for (int sweep = 0; sweep < 64; sweep++)
            {
                double off = 0;
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) off += a[i, j] * a[i, j];
                if (off < 1e-30) break;
                for (int pI = 0; pI < n - 1; pI++)
                    for (int q = pI + 1; q < n; q++)
                    {
                        if (Math.Abs(a[pI, q]) < 1e-300) continue;
                        double theta = (a[q, q] - a[pI, pI]) / (2 * a[pI, q]);
                        double tt = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        double c = 1 / Math.Sqrt(tt * tt + 1), s = tt * c;
                        for (int k = 0; k < n; k++)
                        {
                            double akp = a[k, pI], akq = a[k, q];
                            a[k, pI] = c * akp - s * akq; a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double apk = a[pI, k], aqk = a[q, k];
                            a[pI, k] = c * apk - s * aqk; a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double vkp = v[k, pI], vkq = v[k, q];
                            v[k, pI] = c * vkp - s * vkq; v[k, q] = s * vkp + c * vkq;
                        }
                    }
            }
            int best = 0;
            for (int i = 1; i < n; i++) if (a[i, i] < a[best, best]) best = i;
            var e = new double[n];
            for (int k = 0; k < n; k++) e[k] = v[k, best];
            return e;
        }

        /// <summary>
        /// Semente de cilindro: o retalho e os vizinhos que concordam com ele no EIXO. O eixo que
        /// dois retalhos de um mesmo cilindro sugerem é o produto vetorial das normais deles; um
        /// vizinho de OUTRA superfície sugere outro eixo. Antes a semente levava todo vizinho de
        /// área parecida — e na ponta da alça da malha 2 toda faceta da parede R3 tinha por
        /// vizinha uma faceta do toro de cima, o ajuste falhava sempre e a ponta inteira caía em
        /// "livre", virando 26 retas no esboço.
        /// </summary>
        private static List<int> SeedCluster(int seed, HashSet<int>[] adj, int[] owner, List<List<int>> patches,
            double[] patchArea, double[] nx, double[] ny, double[] nz, double[] area)
        {
            double[] ns = PatchNormal(patches[seed], nx, ny, nz, area);
            var axes = new List<Tuple<int, double[]>>();
            foreach (int q in adj[seed])
            {
                if (owner[q] >= 0 || patchArea[q] > 4.0 * patchArea[seed] + 1e-9) continue;
                double[] nq = PatchNormal(patches[q], nx, ny, nz, area);
                double[] ax = Cross(ns, nq);
                double len = Math.Sqrt(ax[0] * ax[0] + ax[1] * ax[1] + ax[2] * ax[2]);
                if (len < Math.Sin(0.5 * Math.PI / 180)) continue; // coplanar: não diz eixo
                axes.Add(Tuple.Create(q, new[] { ax[0] / len, ax[1] / len, ax[2] / len }));
            }

            List<int> best = null;
            double cosTol = Math.Cos(5.0 * Math.PI / 180);
            foreach (var a in axes)
            {
                var group = new List<int> { seed };
                foreach (var b in axes)
                {
                    double d = Math.Abs(a.Item2[0] * b.Item2[0] + a.Item2[1] * b.Item2[1] + a.Item2[2] * b.Item2[2]);
                    if (d >= cosTol) group.Add(b.Item1);
                }
                if (best == null || group.Count > best.Count) best = group;
            }
            return best != null && best.Count >= 3 ? best : null;
        }

        private static double[] PatchNormal(List<int> tris, double[] nx, double[] ny, double[] nz, double[] area)
        {
            double x = 0, y = 0, z = 0;
            foreach (int t in tris) { x += nx[t] * area[t]; y += ny[t] * area[t]; z += nz[t] * area[t]; }
            double l = Math.Sqrt(x * x + y * y + z * z);
            return l <= 1e-12 ? new double[] { 0, 0, 1 } : new[] { x / l, y / l, z / l };
        }

        private static List<int> Gather(List<int> patchIds, List<List<int>> patches)
        {
            var tris = new List<int>();
            foreach (int id in patchIds) tris.AddRange(patches[id]);
            return tris;
        }

        /// <summary>O retalho cabe no cilindro já ajustado? Todo vértice dentro da tolerância
        /// radial e toda normal perpendicular ao eixo. Um vértice fora reprova.</summary>
        private static bool FitsCylinder(List<int> tris, RecognizedSurface cyl,
            double[] nx, double[] ny, double[] nz, double[] p, RecognizerOptions o)
        {
            double[] a = cyl.AxisMm, c = cyl.PointMm;
            double tol = CylinderTolerance(cyl.RadiusMm, o);
            double sinTol = Math.Sin(o.PlaneAngleToleranceDeg * Math.PI / 180.0);

            foreach (int t in tris)
            {
                if (Math.Abs(nx[t] * a[0] + ny[t] * a[1] + nz[t] * a[2]) > sinTol) return false;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double dx = p[b] - c[0], dy = p[b + 1] - c[1], dz = p[b + 2] - c[2];
                    double along = dx * a[0] + dy * a[1] + dz * a[2];
                    double rx = dx - along * a[0], ry = dy - along * a[1], rz = dz - along * a[2];
                    double dist = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    if (Math.Abs(dist - cyl.RadiusMm) > tol) return false;
                }
            }
            return true;
        }

        /// <summary>Tolerância radial do cilindro. A parte relativa tem TETO de 5× a absoluta:
        /// sem ele, raio grande compraria tolerância grande e o ajuste aceitaria qualquer coisa.</summary>
        private static double CylinderTolerance(double radiusMm, RecognizerOptions o)
        {
            double relative = Math.Min(radiusMm * o.CylinderRelativeTolerance, 5 * o.CylinderAbsoluteToleranceMm);
            return Math.Max(o.CylinderAbsoluteToleranceMm, relative);
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
            double[] area, double[] p, RecognizerOptions o, bool requireRuled = false)
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
            if (rms > CylinderTolerance(radius, o)) return null;

            // Plano disfarçado: com a tolerância relativa, um "cilindro" de raio enorme engole
            // qualquer face plana (a malha real deu Ø38.035 com volta de 0° no fundo de uma
            // peça). Cilindro de verdade cobre arco mensurável E tem raio da ordem da região.
            double sweepDeg = SweepDegrees(angles);
            if (sweepDeg < o.MinCylinderSweepDeg) return null;

            // Plano + plano disfarçados: TRÊS pontos de perfil sempre cabem num círculo, então
            // duas faces planas em ângulo (parede + face inclinada) davam "cilindro" de RMS zero
            // (malha real: Ø53,57 com volta de 39° no corpo 1 do Rack_Anghinoni). Tesselação de
            // cilindro de verdade tem FLECHA pequena entre vértices vizinhos; duas faces planas
            // têm flecha de milímetro.
            if (MaxSagittaMm(angles, radius) > o.MaxSagittaMm) return null;

            if (requireRuled)
            {
                var heights = new List<double>(n);
                foreach (int t in tris)
                    for (int k = 0; k < 3; k++)
                    {
                        int b = t * 9 + k * 3;
                        heights.Add(p[b] * axis[0] + p[b + 1] * axis[1] + p[b + 2] * axis[2]);
                    }
                if (!IsRuled(angles, heights, CylinderTolerance(radius, o) / radius)) return null;
            }

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

            double[] arcFrom = null, arcTo = null;
            if (sweepDeg < 360.0)
            {
                double from, to;
                ArcEnds(angles, out from, out to);
                arcFrom = Combine(e1, e2, Math.Cos(from), Math.Sin(from));
                arcTo = Combine(e1, e2, Math.Cos(to), Math.Sin(to));
            }

            return new RecognizedSurface
            {
                Kind = "cilindro",
                ArcFrom = arcFrom,
                ArcTo = arcTo,
                TriangleCount = tris.Count,
                AreaMm2 = areaSum,
                AxisMm = axis,
                PointMm = centre,
                RadiusMm = radius,
                LengthMm = axMax - axMin,
                SweepDeg = sweepDeg,
                FitRmsMm = rms,
                MinMm = min,
                MaxMm = max
            };
        }

        /// <summary>
        /// Ajuste de CONE — chanfro de furo, escareado, ressalto com ângulo de saída. Sem ele,
        /// o chanfro 1×45° de cada um dos 24 furos da malha 6 saía picado em ~20 "cilindros
        /// inclinados" de três triângulos.
        ///
        /// Duas propriedades do cone fazem o ajuste ser linear, sem iteração:
        ///   1. toda normal faz o MESMO ângulo com o eixo, então o eixo é a direção em que as
        ///      normais menos VARIAM (autovetor de menor autovalor da covariância das normais —
        ///      no cilindro é a mesma conta com a média zero);
        ///   2. todo plano tangente passa pelo ÁPICE — e a faceta da tesselação também, porque
        ///      ela tem dois vértices numa geratriz. O ápice é o ponto que melhor satisfaz
        ///      n·x = n·c de todas as facetas (mínimos quadrados 3×3).
        /// Com eixo e ápice, o semi-ângulo sai de ρ = h·tanβ e o RMS é a distância de cada vértice
        /// à geratriz. Devolve null quando não cabe: plano e cilindro já foram tentados antes.
        /// </summary>
        private static RecognizedSurface TryCone(List<int> tris, double[] nx, double[] ny, double[] nz,
            double[] area, double[] p, RecognizerOptions o, bool requireRuled = false)
        {
            if (tris.Count < 3) return null;

            double sumA = 0, mx = 0, my = 0, mz = 0;
            foreach (int t in tris)
            {
                sumA += area[t];
                mx += nx[t] * area[t]; my += ny[t] * area[t]; mz += nz[t] * area[t];
            }
            if (sumA <= 0) return null;
            mx /= sumA; my /= sumA; mz /= sumA;

            var cov = new double[3, 3];
            var nn = new double[3, 3];
            var rhs = new double[3];
            foreach (int t in tris)
            {
                double a = area[t];
                double[] d = { nx[t] - mx, ny[t] - my, nz[t] - mz };
                double[] n = { nx[t], ny[t], nz[t] };
                double cxT = 0, cyT = 0, czT = 0;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    cxT += p[b] / 3.0; cyT += p[b + 1] / 3.0; czT += p[b + 2] / 3.0;
                }
                double off = n[0] * cxT + n[1] * cyT + n[2] * czT;
                for (int i = 0; i < 3; i++)
                {
                    rhs[i] += a * n[i] * off;
                    for (int j = 0; j < 3; j++)
                    {
                        cov[i, j] += a * d[i] * d[j];
                        nn[i, j] += a * n[i] * n[j];
                    }
                }
            }

            double[] axis = SmallestEigenvector(cov);
            if (axis == null) return null;
            double[] apex = Solve3(nn, rhs);
            if (apex == null) return null;

            // Eixo do ápice para a abertura: a altura média dos vértices tem de ser positiva.
            double hSum = 0;
            int count = 0;
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    hSum += (p[b] - apex[0]) * axis[0] + (p[b + 1] - apex[1]) * axis[1] + (p[b + 2] - apex[2]) * axis[2];
                    count++;
                }
            if (hSum < 0) { axis[0] = -axis[0]; axis[1] = -axis[1]; axis[2] = -axis[2]; }

            double[] e1 = Perpendicular(axis);
            double[] e2 = Cross(axis, e1);

            var hs = new List<double>(count);
            var rhos = new List<double>(count);
            var angles = new List<double>(count);
            double shr = 0, shh = 0;
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double vx = p[b] - apex[0], vy = p[b + 1] - apex[1], vz = p[b + 2] - apex[2];
                    double h = vx * axis[0] + vy * axis[1] + vz * axis[2];
                    double rx = vx - h * axis[0], ry = vy - h * axis[1], rz = vz - h * axis[2];
                    double rho = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    hs.Add(h);
                    rhos.Add(rho);
                    angles.Add(Math.Atan2(rx * e2[0] + ry * e2[1] + rz * e2[2], rx * e1[0] + ry * e1[1] + rz * e1[2]));
                    shr += h * rho; shh += h * h;
                }
            if (shh <= 1e-12) return null;

            double beta = Math.Atan(shr / shh);
            double betaDeg = beta * 180.0 / Math.PI;
            // Abaixo de 0,5° é cilindro (que já recusou); acima de 89,5° é plano.
            if (betaDeg < 0.5 || betaDeg > 89.5) return null;

            double cb = Math.Cos(beta), sb = Math.Sin(beta);
            double sq = 0, hMin = double.MaxValue, hMax = double.MinValue, rMax = 0;
            for (int i = 0; i < hs.Count; i++)
            {
                if (hs[i] <= 0) return null; // vértice atrás do ápice: não é UMA folha de cone
                double d = rhos[i] * cb - hs[i] * sb;
                sq += d * d;
                if (hs[i] < hMin) hMin = hs[i];
                if (hs[i] > hMax) hMax = hs[i];
                if (rhos[i] > rMax) rMax = rhos[i];
            }
            double rms = Math.Sqrt(sq / hs.Count);
            if (rms > CylinderTolerance(rMax, o)) return null;

            double sweepDeg = SweepDegrees(angles);
            if (sweepDeg < o.MinCylinderSweepDeg) return null;
            if (MaxSagittaMm(angles, rMax) > o.MaxSagittaMm) return null;
            if (requireRuled && !IsRuled(angles, hs, CylinderTolerance(rMax, o) / Math.Max(rMax, 1e-6))) return null;

            double tanB = Math.Tan(beta);
            double hMid = (hMin + hMax) / 2.0;
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    Span(min, max, p[b], p[b + 1], p[b + 2]);
                }

            // Cone em ARCO: as pontas, como no cilindro — é o que deixa a correção revolver só o
            // trecho que existe (o recorte em U cônico da alça do stack-container, 179°).
            double[] coneFrom = null, coneTo = null;
            if (sweepDeg < 360.0)
            {
                double f0, t0;
                ArcEnds(angles, out f0, out t0);
                coneFrom = Combine(e1, e2, Math.Cos(f0), Math.Sin(f0));
                coneTo = Combine(e1, e2, Math.Cos(t0), Math.Sin(t0));
            }

            return new RecognizedSurface
            {
                Kind = "cone",
                ArcFrom = coneFrom,
                ArcTo = coneTo,
                TriangleCount = tris.Count,
                AreaMm2 = sumA,
                AxisMm = axis,
                ApexMm = apex,
                PointMm = new[] { apex[0] + hMid * axis[0], apex[1] + hMid * axis[1], apex[2] + hMid * axis[2] },
                HalfAngleDeg = betaDeg,
                RadiusMm = hMid * tanB,
                RadiusMinMm = hMin * tanB,
                RadiusMaxMm = hMax * tanB,
                LengthMm = hMax - hMin,
                SweepDeg = sweepDeg,
                FitRmsMm = rms,
                MinMm = min,
                MaxMm = max
            };
        }

        /// <summary>Início e fim do arco (radianos no referencial do ajuste): o arco é o
        /// complemento do MAIOR vão, correndo no sentido anti-horário do fim do vão ao começo
        /// dele.</summary>
        private static void ArcEnds(List<double> angles, out double from, out double to)
        {
            var sorted = new List<double>(angles);
            sorted.Sort();
            double bestGap = (sorted[0] + 2 * Math.PI) - sorted[sorted.Count - 1];
            from = sorted[0];
            to = sorted[sorted.Count - 1];
            for (int i = 1; i < sorted.Count; i++)
            {
                double g = sorted[i] - sorted[i - 1];
                if (g > bestGap) { bestGap = g; from = sorted[i]; to = sorted[i - 1]; }
            }
        }

        private static double[] Combine(double[] e1, double[] e2, double c, double s)
        {
            return new[] { c * e1[0] + s * e2[0], c * e1[1] + s * e2[1], c * e1[2] + s * e2[2] };
        }

        /// <summary>Tira de v a componente ao longo do eixo e normaliza.</summary>
        private static double[] Perpendicularize(double[] v, double[] axis)
        {
            if (v == null) return null;
            double d = v[0] * axis[0] + v[1] * axis[1] + v[2] * axis[2];
            double[] w = { v[0] - d * axis[0], v[1] - d * axis[1], v[2] - d * axis[2] };
            double len = Math.Sqrt(w[0] * w[0] + w[1] * w[1] + w[2] * w[2]);
            return len <= 1e-12 ? v : new[] { w[0] / len, w[1] / len, w[2] / len };
        }

        /// <summary>Quanto da volta a região cobre. Mede pelo MAIOR vão vazio entre ângulos
        /// ordenados: é o que distingue um furo inteiro (360°) de um raio de canto.</summary>
        public static double SweepDegrees(List<double> angles)
        {
            if (angles == null || angles.Count < 2) return 0;
            var sorted = new List<double>(angles);
            sorted.Sort();

            // Vãos entre ângulos DISTINTOS: os vértices se repetem entre triângulos, e vão zero
            // derrubaria a mediana.
            var gaps = new List<double>();
            for (int i = 1; i < sorted.Count; i++)
            {
                double g = sorted[i] - sorted[i - 1];
                if (g > 1e-9) gaps.Add(g);
            }
            double wrap = (sorted[0] + 2 * Math.PI) - sorted[sorted.Count - 1];
            if (wrap > 1e-9) gaps.Add(wrap);
            if (gaps.Count == 0) return 0;

            double biggestGap = 0;
            foreach (double g in gaps) if (g > biggestGap) biggestGap = g;

            // Furo inteiro tesselado em N segmentos tem TODOS os vãos iguais a 360°/N — o maior
            // vão não é abertura, é só o passo da tesselação (a malha real dava "345°" para furo
            // inteiro). Abertura de verdade é um vão que se destaca dos outros.
            // O passo de referência é o QUARTIL de cima, não a mediana: o cone do chanfro tem os
            // anéis das duas bocas com vértices em ângulos diferentes, os vãos alternam curto e
            // longo, e a mediana (curta) fazia o furo inteiro sair "346°".
            gaps.Sort();
            double upper = gaps[(gaps.Count * 3) / 4];
            if (gaps.Count >= 6 && biggestGap <= 1.5 * upper) return 360.0;

            double sweep = (2 * Math.PI - biggestGap) * 180.0 / Math.PI;
            return sweep < 0 ? 0 : sweep;
        }

        /// <summary>
        /// Cilindro e cone são superfícies REGRADAS: a tesselação põe os vértices em GERATRIZES
        /// (mesmo ângulo em volta do eixo, alturas diferentes). Fragmento de superfície de dupla
        /// curvatura — concordância de canto, toro, esfera — forçado num cilindro tem os vértices
        /// espalhados, um por ângulo. Foi o que encheu o corpo 3 do Rack_Anghinoni de "R0,09 com
        /// eixo (−0,097; 0,181; 0,979)": centenas de cilindros de três triângulos na concordância
        /// das pernas. Exige que a maioria dos vértices esteja numa geratriz com outro vértice.
        /// Só vale para RETALHOS dentro de componente misto: superfície inteira delimitada por
        /// quina pode vir com tesselação torcida (os anéis das bocas do chanfro da malha 6 têm
        /// vértices em ângulos diferentes) e já é provada pelo ajuste.
        /// </summary>
        public static bool IsRuled(List<double> angles, List<double> heights, double angleTolRad)
        {
            int n = angles.Count;
            if (n < 4) return false;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            idx.Sort((a, b) => angles[a].CompareTo(angles[b]));

            double tol = Math.Max(angleTolRad, 1e-6);
            double hMin = double.MaxValue, hMax = double.MinValue;
            foreach (double h in heights) { if (h < hMin) hMin = h; if (h > hMax) hMax = h; }
            double minRise = Math.Max(1e-3, 0.05 * (hMax - hMin));

            int onGeneratrix = 0;
            int start = 0;
            while (start < n)
            {
                int end = start;
                while (end + 1 < n && angles[idx[end + 1]] - angles[idx[end]] <= tol) end++;
                double lo = double.MaxValue, hi = double.MinValue;
                for (int i = start; i <= end; i++)
                {
                    double h = heights[idx[i]];
                    if (h < lo) lo = h;
                    if (h > hi) hi = h;
                }
                if (hi - lo >= minRise) onGeneratrix += end - start + 1;
                start = end + 1;
            }
            return onGeneratrix >= 0.6 * n;
        }

        /// <summary>Maior flecha da tesselação: R·(1 − cos(vão/2)) no maior vão entre ângulos
        /// vizinhos DENTRO do arco (a abertura do arco, se houver, não conta).</summary>
        public static double MaxSagittaMm(List<double> angles, double radiusMm)
        {
            if (angles == null || angles.Count < 2) return 0;
            var sorted = new List<double>(angles);
            sorted.Sort();
            var gaps = new List<double>();
            for (int i = 1; i < sorted.Count; i++)
            {
                double g = sorted[i] - sorted[i - 1];
                if (g > 1e-9) gaps.Add(g);
            }
            double wrap = (sorted[0] + 2 * Math.PI) - sorted[sorted.Count - 1];
            if (wrap > 1e-9) gaps.Add(wrap);
            if (gaps.Count < 2) return 0;

            // O maior vão é a abertura do arco — a não ser que o arco seja a volta inteira
            // (todos os vãos iguais), e aí ele também é passo de tesselação.
            gaps.Sort();
            double largest = gaps[gaps.Count - 1];
            double step = SweepDegrees(angles) >= 360.0 ? largest : gaps[gaps.Count - 2];
            return radiusMm * (1 - Math.Cos(step / 2));
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
