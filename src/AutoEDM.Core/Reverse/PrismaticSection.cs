using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoEDM.Reverse
{
    /// <summary>Um trecho do contorno de uma secção, já em geometria EXATA (mm, plano XY).</summary>
    public sealed class SectionElement
    {
        /// <summary>"reta", "arco" ou "circulo".</summary>
        public string Kind;

        public double[] Start;
        public double[] End;

        /// <summary>Arco: um ponto do meio dele — define o sentido sem ambiguidade (arco por 3 pontos).</summary>
        public double[] Mid;

        /// <summary>Arco/círculo.</summary>
        public double[] Center;
        public double RadiusMm;

        /// <summary>Superfície reconhecida que deu este trecho (índice em RecognitionResult.Surfaces).</summary>
        public int Region;

        /// <summary>false = o trecho veio da própria malha (a superfície não era parede vertical
        /// reconhecida) e é aproximação poligonal, não geometria exata.</summary>
        public bool Exact;
    }

    public sealed class SectionLoop
    {
        public readonly List<SectionElement> Elements = new List<SectionElement>();

        /// <summary>Área com sinal do polígono da malha (anti-horário positivo).</summary>
        public double SignedAreaMm2;

        /// <summary>Área com sinal do contorno EXATO (retas + arcos integrados + círculos). É
        /// contra ela que o sólido construído se confere: a malha tem furo e raio como polígono
        /// INSCRITO, e por isso nunca dá o volume da peça de verdade.</summary>
        public double ExactAreaMm2;
    }

    /// <summary>Uma faixa Z em que a secção não muda: vira UMA extrusão de Z0 a Z1.</summary>
    public sealed class SectionBand
    {
        public double Z0, Z1;
        public readonly List<SectionLoop> Loops = new List<SectionLoop>();

        /// <summary>O que a construção teve de ajustar nesta faixa (ex.: furo tangente aberto).</summary>
        public readonly List<string> Notes = new List<string>();

        /// <summary>Maior diferença entre a área da secção da malha no meio da faixa e em quatro
        /// outras alturas dela, em mm². Faixa prismática de verdade dá ~0; o que passa disso é
        /// raio em aresta horizontal, chanfro, ângulo de saída — forma que a extrusão reta não
        /// reproduz e que o volume exato do plano, sozinho, não denunciaria.</summary>
        public double AreaVariationMm2;
    }

    public sealed class PrismaticPlan
    {
        public readonly List<SectionBand> Bands = new List<SectionBand>();
        public readonly List<double> Levels = new List<double>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>Volume da malha pelo teorema da divergência — o número contra o qual o sólido
        /// reconstruído é conferido.</summary>
        public double MeshVolumeMm3;

        /// <summary>Volume do plano pelo polígono da malha: Σ área dos laços × altura da faixa.
        /// Tem de bater com <see cref="MeshVolumeMm3"/> — se não bate, a secção perdeu laço.</summary>
        public double PlanVolumeMm3;

        /// <summary>Volume do plano pela geometria EXATA. É o que o sólido construído tem de dar.</summary>
        public double ExactVolumeMm3;
    }

    public sealed class PrismaticOptions
    {
        /// <summary>Dois níveis Z mais próximos que isto são o mesmo nível.</summary>
        public double LevelMergeMm = 0.005;

        /// <summary>Faixa mais fina que isto não vira extrusão.</summary>
        public double MinBandMm = 0.01;

        /// <summary>Distância máxima entre o vértice exato (interseção das duas superfícies
        /// vizinhas) e o ponto de transição na malha. Acima disto o vértice exato é suspeito e
        /// fica o da malha, com aviso.</summary>
        public double VertexToleranceMm = 0.5;

        /// <summary>Variação de área da secção dentro de uma faixa a partir da qual ela é declarada
        /// NÃO prismática: o maior entre este piso absoluto e a fração abaixo da área.</summary>
        public double MaxAreaVariationMm2 = 0.05;
        public double MaxAreaVariationFraction = 0.0005;
    }

    /// <summary>
    /// SECÇÃO 2,5D — de malha + superfícies reconhecidas para um plano de extrusões por faixa Z.
    /// Geometria PURA, sem COM.
    ///
    /// Por que esta rota (decidida em 2026-10-09, depois de medir no SE): superfícies avulsas +
    /// auto-trim fecham casca CONVEXA, mas não furam as faces planas nem resolvem bolsão. Placa,
    /// postiço e eletrodo são 2,5D: a forma é uma pilha de faixas em que o contorno não muda.
    /// Então:
    ///   1. os NÍVEIS são as alturas dos planos horizontais reconhecidos (já no centésimo) mais o
    ///      fundo e o topo da malha;
    ///   2. em cada faixa a malha é cortada na altura do meio — a secção sai como laços de
    ///      segmentos, cada um sabendo de que triângulo (logo de que superfície) veio;
    ///   3. cada sequência de segmentos da MESMA superfície vira um trecho exato: parede plana
    ///      vertical → reta, cilindro de eixo Z → arco/círculo com o centro e o Ø reconhecidos;
    ///   4. os vértices saem da interseção EXATA das duas superfícies vizinhas (reta×reta,
    ///      reta×círculo, círculo×círculo), escolhida a solução mais perto da transição na malha
    ///      — é isso que dá canto em centésimo em vez de canto de tesselação;
    ///   5. faixas vizinhas com a mesma secção se fundem.
    /// O que não é parede vertical reconhecida (raio de borda horizontal, cone de saída, forma
    /// livre) sai como polilinha da malha, marcado <see cref="SectionElement.Exact"/> = false e
    /// avisado — nunca escondido.
    /// </summary>
    public static class PrismaticSection
    {
        public static PrismaticPlan Plan(double[] pointsMm, RecognitionResult r, PrismaticOptions options)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.RegionOfTriangle == null || r.RegionOfTriangle.Length * 9 != pointsMm.Length)
                throw new ArgumentException("O reconhecimento não corresponde a esta malha.", nameof(r));
            PrismaticOptions o = options ?? new PrismaticOptions();

            var plan = new PrismaticPlan();
            if (pointsMm.Length == 0) return plan;
            plan.MeshVolumeMm3 = MeshVolume(pointsMm);

            // ------------------------------------------------------------ 1. níveis
            double zMin = double.MaxValue, zMax = double.MinValue;
            for (int i = 2; i < pointsMm.Length; i += 3)
            {
                if (pointsMm[i] < zMin) zMin = pointsMm[i];
                if (pointsMm[i] > zMax) zMax = pointsMm[i];
            }

            var raw = new List<double> { zMin, zMax };
            foreach (RecognizedSurface s in r.Surfaces)
                if (s.Kind == "plano" && s.Normal != null && Math.Abs(s.Normal[2]) == 1.0)
                    raw.Add(s.PointMm[2]);
            raw.Sort();
            foreach (double z in raw)
                if (plan.Levels.Count == 0 || z - plan.Levels[plan.Levels.Count - 1] > o.LevelMergeMm)
                    plan.Levels.Add(z);

            // Fundo e topo da malha valem pelo plano reconhecido quando há um — é ele que está
            // no centésimo. O extremo cru da malha só fica se nenhum plano horizontal o cobrir.
            SnapEnds(plan.Levels, zMin, zMax, o.LevelMergeMm, raw);

            // ------------------------------------------------------------ 2–4. secções
            SectionBand previous = null;
            string previousKey = null;
            for (int i = 0; i + 1 < plan.Levels.Count; i++)
            {
                double z0 = plan.Levels[i], z1 = plan.Levels[i + 1];
                if (z1 - z0 < o.MinBandMm) continue;

                var band = new SectionBand { Z0 = z0, Z1 = z1 };
                List<List<Seg>> loops = Cut(pointsMm, r.RegionOfTriangle, (z0 + z1) / 2.0);
                foreach (List<Seg> raw2 in loops)
                {
                    SectionLoop loop = Exactify(raw2, r, o, plan.Warnings, z0, z1);
                    if (loop == null) continue;
                    band.Loops.AddRange(SplitTouchingCircles(loop));
                }
                if (band.Loops.Count == 0) continue;
                band.AreaVariationMm2 = AreaVariation(pointsMm, r.RegionOfTriangle, z0, z1);

                // ------------------------------------------------------- 5. funde faixas iguais
                string key = Signature(band);
                if (previous != null && key == previousKey && Math.Abs(previous.Z1 - z0) < 1e-9)
                {
                    previous.Z1 = z1;
                    previous.AreaVariationMm2 = Math.Max(previous.AreaVariationMm2, band.AreaVariationMm2);
                    continue;
                }
                plan.Bands.Add(band);
                previous = band;
                previousKey = key;
            }

            foreach (SectionBand b in plan.Bands)
            {
                double mid = 0;
                foreach (SectionLoop l in b.Loops) mid += l.SignedAreaMm2;
                if (b.AreaVariationMm2 > Math.Max(o.MaxAreaVariationMm2, Math.Abs(mid) * o.MaxAreaVariationFraction))
                    plan.Warnings.Add(string.Format(CultureInfo.InvariantCulture,
                        "Faixa Z {0:0.00}–{1:0.00} NÃO é prismática: a área da secção varia {2:0.###} mm² ao longo dela " +
                        "({3:0.##}% da área) — há raio, chanfro ou saída que a extrusão reta não reproduz.",
                        b.Z0, b.Z1, b.AreaVariationMm2, Math.Abs(mid) > 0 ? 100 * b.AreaVariationMm2 / Math.Abs(mid) : 0));
            }

            foreach (SectionBand b in plan.Bands)
                foreach (SectionLoop l in b.Loops)
                {
                    l.ExactAreaMm2 = ExactArea(l);
                    plan.PlanVolumeMm3 += l.SignedAreaMm2 * (b.Z1 - b.Z0);
                    plan.ExactVolumeMm3 += l.ExactAreaMm2 * (b.Z1 - b.Z0);
                }
            return plan;
        }

        /// <summary>Área da secção em quatro alturas da faixa (10, 30, 70 e 90 %), comparada com a
        /// do meio.</summary>
        private static double AreaVariation(double[] p, int[] regionOf, double z0, double z1)
        {
            double mid = SectionArea(p, regionOf, (z0 + z1) / 2);
            double worst = 0;
            foreach (double f in new[] { 0.1, 0.3, 0.7, 0.9 })
                worst = Math.Max(worst, Math.Abs(SectionArea(p, regionOf, z0 + f * (z1 - z0)) - mid));
            return worst;
        }

        public static double SectionArea(double[] p, int[] regionOf, double z)
        {
            double a = 0;
            foreach (List<Seg> loop in Cut(p, regionOf, z))
                foreach (Seg s in loop) a += s.Ax * s.By - s.Bx * s.Ay;
            return a / 2;
        }

        private static void SnapEnds(List<double> levels, double zMin, double zMax, double merge, List<double> raw)
        {
            // Os extremos já estão na lista; se um plano reconhecido caiu a menos de 'merge'
            // deles, a fusão acima manteve o PRIMEIRO da ordem (o cru, no fundo). Troca pelo
            // reconhecido, que é o que está arredondado.
            if (levels.Count == 0) return;
            foreach (double z in raw)
            {
                if (z != zMin && Math.Abs(z - levels[0]) <= merge) levels[0] = z;
                if (z != zMax && Math.Abs(z - levels[levels.Count - 1]) <= merge) levels[levels.Count - 1] = z;
            }
        }

        // ------------------------------------------------------------------ corte da malha

        private struct Seg
        {
            public double Ax, Ay, Bx, By;
            public int Region;
        }

        /// <summary>Corta a malha em Z = z e encadeia os segmentos em laços fechados.</summary>
        private static List<List<Seg>> Cut(double[] p, int[] regionOf, double z)
        {
            z = OffVertex(p, z);
            var segs = new List<Seg>();
            int triCount = p.Length / 9;
            var pts = new List<double[]>(2);

            for (int t = 0; t < triCount; t++)
            {
                pts.Clear();
                for (int k = 0; k < 3; k++)
                {
                    int a = t * 9 + k * 3, b = t * 9 + ((k + 1) % 3) * 3;
                    double za = p[a + 2] - z, zb = p[b + 2] - z;
                    if ((za < 0 && zb > 0) || (za > 0 && zb < 0))
                        pts.Add(EdgeCut(p, a, b, z));
                }
                if (pts.Count != 2) continue;

                // Sentido do segmento: com as normais para fora, o contorno externo sai
                // anti-horário (material à esquerda) se o segmento seguir Z × n = (−ny, nx).
                double[] n = Normal(p, t);
                double dx = pts[1][0] - pts[0][0], dy = pts[1][1] - pts[0][1];
                double ex = -n[1], ey = n[0];
                bool flip = dx * ex + dy * ey < 0;
                double[] s0 = flip ? pts[1] : pts[0], s1 = flip ? pts[0] : pts[1];
                segs.Add(new Seg { Ax = s0[0], Ay = s0[1], Bx = s1[0], By = s1[1], Region = regionOf[t] });
            }

            // Encadeia pelo ponto inicial. O ponto de corte de uma aresta é calculado sempre com
            // os vértices na MESMA ordem (EdgeCut), então os dois triângulos que dividem a aresta
            // dão o ponto bit a bit igual — a chave pode ser exata.
            var byStart = new Dictionary<string, List<int>>(segs.Count);
            for (int i = 0; i < segs.Count; i++)
            {
                string k = Key(segs[i].Ax, segs[i].Ay);
                List<int> list;
                if (!byStart.TryGetValue(k, out list)) byStart[k] = list = new List<int>(1);
                list.Add(i);
            }

            var used = new bool[segs.Count];
            var loops = new List<List<Seg>>();
            for (int i = 0; i < segs.Count; i++)
            {
                if (used[i]) continue;
                var loop = new List<Seg>();
                int cur = i;
                while (cur >= 0 && !used[cur])
                {
                    used[cur] = true;
                    loop.Add(segs[cur]);
                    cur = Next(segs, byStart, used, segs[cur]);
                }
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        /// <summary>
        /// Próximo segmento do laço. Num vértice com UMA saída livre, é ela. Com mais de uma —
        /// dois laços se tocando, o caso do furo tangente à parede do bolsão (malha 6 do
        /// Rack_Anghinoni, 2026-10-09) — vale a regra de percorrer face de grafo planar: com o
        /// material sempre à esquerda, segue-se a saída que vira MAIS para a ESQUERDA, e a
        /// meia-volta por último. Conferido nos dois casos: no canto encostado, ficar no mesmo
        /// bloco é virar à esquerda (+90°), e a direita (−90°) entra no vizinho; na tangência,
        /// seguir reto (0°) é o certo, e a meia-volta (180°) entra no furo.
        /// </summary>
        private static int Next(List<Seg> segs, Dictionary<string, List<int>> byStart, bool[] used, Seg from)
        {
            List<int> cands;
            if (!byStart.TryGetValue(Key(from.Bx, from.By), out cands)) return -1;

            int best = -1;
            double bestTurn = double.MinValue;
            double inX = from.Bx - from.Ax, inY = from.By - from.Ay;
            foreach (int c in cands)
            {
                if (used[c]) continue;
                double outX = segs[c].Bx - segs[c].Ax, outY = segs[c].By - segs[c].Ay;
                // Giro com sinal: positivo = para a esquerda. Meia-volta vale −π: a escolha MENOS
                // preferida, venha o atan2 com +π ou −π.
                double turn = Math.Atan2(inX * outY - inY * outX, inX * outX + inY * outY);
                if (Math.Abs(Math.Abs(turn) - Math.PI) < 1e-9) turn = -Math.PI;
                if (turn > bestTurn) { bestTurn = turn; best = c; }
            }
            return best;
        }

        /// <summary>
        /// Altura de corte que não passa por vértice nenhum. Aresta com ponta EXATAMENTE na
        /// altura não conta como cruzada, o triângulo dá um ponto só, o segmento some e o laço se
        /// fecha por outro caminho: na perna do corpo 3 a faixa 4–40 era cortada em Z 22, altura
        /// de vértices da face R10, e o contorno pulava da ponta R1 direto para a parede X = 45 —
        /// R10, a outra ponta R1 e um trecho de parede sumiam da perna inteira.
        /// </summary>
        public static double OffVertex(double[] p, double z)
        {
            const double step = 1e-4;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                double candidate = z + (attempt % 2 == 0 ? 1 : -1) * step * ((attempt + 1) / 2);
                bool hit = false;
                for (int i = 2; i < p.Length; i += 3)
                    if (Math.Abs(p[i] - candidate) < 1e-9) { hit = true; break; }
                if (!hit) return candidate;
            }
            return z;
        }

        private static double[] EdgeCut(double[] p, int a, int b, double z)
        {
            // Ordem canônica dos vértices: o mesmo ponto sai das duas faces da aresta.
            if (Less(p, b, a)) { int t = a; a = b; b = t; }
            double f = (z - p[a + 2]) / (p[b + 2] - p[a + 2]);
            return new[] { p[a] + f * (p[b] - p[a]), p[a + 1] + f * (p[b + 1] - p[a + 1]) };
        }

        private static bool Less(double[] p, int a, int b)
        {
            if (p[a] != p[b]) return p[a] < p[b];
            if (p[a + 1] != p[b + 1]) return p[a + 1] < p[b + 1];
            return p[a + 2] < p[b + 2];
        }

        private static string Key(double x, double y)
        {
            return Math.Round(x, 6).ToString("R", CultureInfo.InvariantCulture) + "|" +
                   Math.Round(y, 6).ToString("R", CultureInfo.InvariantCulture);
        }

        private static double[] Normal(double[] p, int t)
        {
            int b = t * 9;
            double ux = p[b + 3] - p[b], uy = p[b + 4] - p[b + 1], uz = p[b + 5] - p[b + 2];
            double vx = p[b + 6] - p[b], vy = p[b + 7] - p[b + 1], vz = p[b + 8] - p[b + 2];
            return new[] { uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx };
        }

        // ------------------------------------------------------------------ geometria exata

        /// <summary>Superfície que vira trecho exato no plano XY: reta (parede plana vertical) ou
        /// círculo (cilindro de eixo Z). null = não serve, o trecho fica poligonal.</summary>
        private sealed class Carrier
        {
            public bool IsLine;
            public double Px, Py, Dx, Dy;   // reta: ponto e direção unitária
            public double Cx, Cy, R;        // círculo
        }

        private static Carrier CarrierOf(RecognizedSurface s)
        {
            if (s == null) return null;
            if (s.Kind == "plano" && s.Normal != null && s.Normal[2] == 0)
            {
                double nx = s.Normal[0], ny = s.Normal[1];
                double len = Math.Sqrt(nx * nx + ny * ny);
                if (len < 1e-12) return null;
                return new Carrier { IsLine = true, Px = s.PointMm[0], Py = s.PointMm[1], Dx = -ny / len, Dy = nx / len };
            }
            if (s.Kind == "cilindro" && s.AxisMm != null && Math.Abs(s.AxisMm[2]) == 1.0)
                return new Carrier { Cx = s.PointMm[0], Cy = s.PointMm[1], R = s.RadiusMm };
            return null;
        }

        private sealed class Run
        {
            public int Region;
            public readonly List<Seg> Segs = new List<Seg>();
        }

        private static SectionLoop Exactify(List<Seg> segs, RecognitionResult r, PrismaticOptions o,
            List<string> warnings, double z0, double z1)
        {
            var loop = new SectionLoop();
            double area = 0;
            foreach (Seg s in segs) area += s.Ax * s.By - s.Bx * s.Ay;
            loop.SignedAreaMm2 = area / 2.0;
            if (Math.Abs(loop.SignedAreaMm2) < 1e-9) return null;

            // ---------------------------------------------- sequências por superfície
            var runs = new List<Run>();
            foreach (Seg s in segs)
            {
                if (runs.Count == 0 || runs[runs.Count - 1].Region != s.Region)
                    runs.Add(new Run { Region = s.Region });
                runs[runs.Count - 1].Segs.Add(s);
            }
            if (runs.Count > 1 && runs[0].Region == runs[runs.Count - 1].Region)
            {
                Run last = runs[runs.Count - 1];
                last.Segs.AddRange(runs[0].Segs);
                runs.RemoveAt(0);
            }

            var carriers = new Carrier[runs.Count];
            for (int i = 0; i < runs.Count; i++)
            {
                int reg = runs[i].Region;
                carriers[i] = reg >= 0 && reg < r.Surfaces.Count ? CarrierOf(r.Surfaces[reg]) : null;
            }

            // ---------------------------------------------- círculo inteiro (furo, pino)
            if (runs.Count == 1 && carriers[0] != null && !carriers[0].IsLine)
            {
                Carrier c = carriers[0];
                loop.Elements.Add(new SectionElement
                {
                    Kind = "circulo", Center = new[] { c.Cx, c.Cy }, RadiusMm = c.R,
                    Region = runs[0].Region, Exact = true
                });
                return loop;
            }

            // ---------------------------------------------- vértices exatos
            int n = runs.Count;
            var vertex = new double[n][]; // vertex[i] = junção entre runs[i-1] e runs[i]
            for (int i = 0; i < n; i++)
            {
                Seg join = runs[i].Segs[0];
                double qx = join.Ax, qy = join.Ay;
                Carrier a = carriers[(i - 1 + n) % n], b = carriers[i];

                double[] v = null;
                if (a != null && b != null) v = Intersect(a, b, qx, qy);
                if (v != null && Dist(v[0], v[1], qx, qy) > o.VertexToleranceMm)
                {
                    warnings.Add(string.Format(CultureInfo.InvariantCulture,
                        "Faixa Z {0:0.00}–{1:0.00}: vértice exato ({2:0.000}; {3:0.000}) a {4:0.000} mm da malha — ficou o da malha.",
                        z0, z1, v[0], v[1], Dist(v[0], v[1], qx, qy)));
                    v = null;
                }
                vertex[i] = v ?? new[] { qx, qy };
            }

            // ---------------------------------------------- trechos
            bool warnedApprox = false;
            for (int i = 0; i < n; i++)
            {
                double[] start = vertex[i], end = vertex[(i + 1) % n];
                Carrier c = carriers[i];
                Run run = runs[i];

                if (c != null && c.IsLine)
                {
                    loop.Elements.Add(new SectionElement { Kind = "reta", Start = start, End = end, Region = run.Region, Exact = true });
                }
                else if (c != null)
                {
                    // Ponto do meio: o meio do caminho da malha nesta sequência, levado ao raio.
                    Seg mid = run.Segs[run.Segs.Count / 2];
                    double mx = (mid.Ax + mid.Bx) / 2 - c.Cx, my = (mid.Ay + mid.By) / 2 - c.Cy;
                    double ml = Math.Sqrt(mx * mx + my * my);
                    double[] m = ml > 1e-12 ? new[] { c.Cx + mx / ml * c.R, c.Cy + my / ml * c.R } : new[] { mid.Ax, mid.Ay };
                    loop.Elements.Add(new SectionElement
                    {
                        Kind = "arco", Start = start, End = end, Mid = m,
                        Center = new[] { c.Cx, c.Cy }, RadiusMm = c.R, Region = run.Region, Exact = true
                    });
                }
                else
                {
                    // Superfície que não é parede vertical reconhecida: polilinha da malha, com
                    // as pontas presas aos vértices já calculados para o laço continuar fechado.
                    if (!warnedApprox)
                    {
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "Faixa Z {0:0.00}–{1:0.00}: trecho sem parede vertical reconhecida (superfície {2}) — aproximado pela malha.",
                            z0, z1, run.Region));
                        warnedApprox = true;
                    }
                    double[] from = start;
                    for (int k = 0; k < run.Segs.Count; k++)
                    {
                        double[] to = k == run.Segs.Count - 1 ? end : new[] { run.Segs[k].Bx, run.Segs[k].By };
                        if (Dist(from[0], from[1], to[0], to[1]) < 1e-6) continue;
                        loop.Elements.Add(new SectionElement { Kind = "reta", Start = from, End = to, Region = run.Region, Exact = false });
                        from = to;
                    }
                }
            }

            // Duas sequências da mesma reta separadas por nada (sobra de malha): descarta RETA de
            // comprimento zero que a interseção exata produziu. Arco que volta ao próprio início
            // NÃO é sobra — é círculo inteiro encostado no laço (ver SplitTouchingCircles).
            loop.Elements.RemoveAll(e => e.Kind == "reta" && Dist(e.Start[0], e.Start[1], e.End[0], e.End[1]) < 1e-6);
            return loop.Elements.Count >= 2 ? loop : null;
        }

        /// <summary>
        /// Furo que TOCA outro contorno num único ponto (o Ø2 tangente à parede do bolsão na malha
        /// 6 do Rack_Anghinoni) chega como um desvio dentro do laço vizinho: o trecho de cilindro
        /// sai do vértice de contato e volta a ele. Esse trecho é um círculo inteiro e vira laço
        /// próprio; o laço vizinho continua fechado, porque o desvio começava e terminava no
        /// mesmo ponto.
        /// </summary>
        private static IEnumerable<SectionLoop> SplitTouchingCircles(SectionLoop loop)
        {
            var result = new List<SectionLoop>();
            for (int i = loop.Elements.Count - 1; i >= 0; i--)
            {
                SectionElement e = loop.Elements[i];
                if (e.Kind != "arco" || Dist(e.Start[0], e.Start[1], e.End[0], e.End[1]) > 1e-6) continue;
                loop.Elements.RemoveAt(i);
                var circle = new SectionLoop { SignedAreaMm2 = Math.Sign(loop.SignedAreaMm2) * Math.PI * e.RadiusMm * e.RadiusMm };
                circle.Elements.Add(new SectionElement
                {
                    Kind = "circulo", Center = e.Center, RadiusMm = e.RadiusMm, Region = e.Region, Exact = true
                });
                loop.SignedAreaMm2 -= circle.SignedAreaMm2;
                result.Add(circle);
            }
            // Laço de um círculo só (o furo comum) é laço válido; o que não pode sobrar é o
            // laço vizinho reduzido a menos de dois trechos depois de perder o desvio.
            if (result.Count == 0 || loop.Elements.Count >= 2) result.Insert(0, loop);
            return result;
        }

        /// <summary>Interseção exata de dois portadores, a mais perto de (qx, qy). Tangência
        /// (reta encostando no círculo, dois círculos tangentes) cai no pé da perpendicular.</summary>
        private static double[] Intersect(Carrier a, Carrier b, double qx, double qy)
        {
            if (a.IsLine && b.IsLine)
            {
                double den = a.Dx * b.Dy - a.Dy * b.Dx;
                if (Math.Abs(den) < 1e-12) return null; // paralelas: a mesma parede em dois pedaços
                double t = ((b.Px - a.Px) * b.Dy - (b.Py - a.Py) * b.Dx) / den;
                return new[] { a.Px + t * a.Dx, a.Py + t * a.Dy };
            }
            if (a.IsLine) return LineCircle(a, b, qx, qy);
            if (b.IsLine) return LineCircle(b, a, qx, qy);
            return CircleCircle(a, b, qx, qy);
        }

        private static double[] LineCircle(Carrier l, Carrier c, double qx, double qy)
        {
            // Ponto da reta mais perto do centro, e meia-corda.
            double t0 = (c.Cx - l.Px) * l.Dx + (c.Cy - l.Py) * l.Dy;
            double fx = l.Px + t0 * l.Dx, fy = l.Py + t0 * l.Dy;
            double d = Dist(fx, fy, c.Cx, c.Cy);
            double h2 = c.R * c.R - d * d;
            if (h2 <= 0) return new[] { fx, fy }; // tangente (ou quase, por arredondamento)
            double h = Math.Sqrt(h2);
            double[] p1 = { fx + h * l.Dx, fy + h * l.Dy }, p2 = { fx - h * l.Dx, fy - h * l.Dy };
            return Dist(p1[0], p1[1], qx, qy) <= Dist(p2[0], p2[1], qx, qy) ? p1 : p2;
        }

        private static double[] CircleCircle(Carrier a, Carrier b, double qx, double qy)
        {
            double dx = b.Cx - a.Cx, dy = b.Cy - a.Cy;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-12) return null; // concêntricos: não se cruzam
            double along = (a.R * a.R - b.R * b.R + d * d) / (2 * d);
            double h2 = a.R * a.R - along * along;
            double mx = a.Cx + along * dx / d, my = a.Cy + along * dy / d;
            if (h2 <= 0) return new[] { mx, my };
            double h = Math.Sqrt(h2);
            double[] p1 = { mx - h * dy / d, my + h * dx / d }, p2 = { mx + h * dy / d, my - h * dx / d };
            return Dist(p1[0], p1[1], qx, qy) <= Dist(p2[0], p2[1], qx, qy) ? p1 : p2;
        }

        private static double Dist(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ------------------------------------------------------------------ área exata

        /// <summary>Área com sinal do contorno exato: ½∮(x dy − y dx). Reta pelo termo de
        /// Gauss; arco integrado em θ (½[R²Δθ + R·cx(sen θe − sen θs) − R·cy(cos θe − cos θs)]),
        /// com o sentido tirado do ponto do meio; círculo inteiro = ±πR² no sinal do laço.</summary>
        public static double ExactArea(SectionLoop loop)
        {
            double a2 = 0; // 2 × área
            foreach (SectionElement e in loop.Elements)
            {
                if (e.Kind == "circulo")
                    return Math.Sign(loop.SignedAreaMm2) * Math.PI * e.RadiusMm * e.RadiusMm;

                if (e.Kind == "reta")
                {
                    a2 += e.Start[0] * e.End[1] - e.End[0] * e.Start[1];
                    continue;
                }

                double cx = e.Center[0], cy = e.Center[1], rr = e.RadiusMm;
                double ts = Math.Atan2(e.Start[1] - cy, e.Start[0] - cx);
                double tm = Math.Atan2(e.Mid[1] - cy, e.Mid[0] - cx);
                double te = Math.Atan2(e.End[1] - cy, e.End[0] - cx);
                double toMid = Wrap(tm - ts), toEnd = Wrap(te - ts);
                double sweep = toMid <= toEnd ? toEnd : toEnd - 2 * Math.PI; // anti-horário se o meio vem antes do fim
                a2 += rr * rr * sweep + rr * cx * (Math.Sin(ts + sweep) - Math.Sin(ts)) - rr * cy * (Math.Cos(ts + sweep) - Math.Cos(ts));
            }
            return a2 / 2.0;
        }

        private static double Wrap(double a)
        {
            double t = a % (2 * Math.PI);
            return t < 0 ? t + 2 * Math.PI : t;
        }

        // ------------------------------------------------------------------ apoio

        /// <summary>Assinatura geométrica da secção, no centésimo: duas faixas com a mesma
        /// assinatura são a mesma extrusão.</summary>
        private static string Signature(SectionBand b)
        {
            var parts = new List<string>();
            foreach (SectionLoop l in b.Loops)
            {
                var sb = new StringBuilder();
                foreach (SectionElement e in l.Elements)
                {
                    sb.Append(e.Kind[0]);
                    if (e.Kind == "circulo") sb.Append(P(e.Center)).Append(R(e.RadiusMm));
                    else sb.Append(P(e.Start)).Append(P(e.End));
                }
                parts.Add(sb.ToString());
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("/", parts);
        }

        private static string P(double[] v) { return "(" + R(v[0]) + "," + R(v[1]) + ")"; }
        private static string R(double v) { return Math.Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>Volume de malha fechada com normais para fora: Σ (a · (b × c)) / 6.</summary>
        public static double MeshVolume(double[] p)
        {
            double v = 0;
            for (int b = 0; b + 8 < p.Length; b += 9)
            {
                double ax = p[b], ay = p[b + 1], az = p[b + 2];
                double bx = p[b + 3], by = p[b + 4], bz = p[b + 5];
                double cx = p[b + 6], cy = p[b + 7], cz = p[b + 8];
                v += ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
            }
            return v / 6.0;
        }
    }
}
