using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Reverse
{
    /// <summary>Um trecho do perfil de uma correção, no plano do esboço (u, v), em mm.</summary>
    public sealed class ProfileEdge
    {
        /// <summary>"reta" ou "arco".</summary>
        public string Kind;
        public double[] Start, End;

        /// <summary>Arco: um ponto do meio dele (arco por 3 pontos, sentido sem ambiguidade).</summary>
        public double[] Mid;
    }

    /// <summary>
    /// Uma região de fronteira que o sólido prismático (faixas Z) não reproduz e que é corrigida
    /// por UMA feature: soma de material (protrusão) ou recorte, por revolução em volta de um eixo
    /// ou por extrusão ao longo de uma direção.
    /// </summary>
    public sealed class CorrectionRegion
    {
        /// <summary>"revolucao" (em volta do eixo V que passa pela origem, u = 0) ou "extrusao"
        /// (ao longo de W = U × V, de 0 a <see cref="DepthMm"/>).</summary>
        public string Kind;

        /// <summary>true = protrusão (a malha tem material aqui); false = recorte.</summary>
        public bool Add;

        /// <summary>Superfície reconhecida que gerou a correção (índice em RecognitionResult.Surfaces).</summary>
        public int Surface;

        /// <summary>O que é, para o relatório: "chanfro Ø4 → Ø6", "raio R2,51 eixo Y"…</summary>
        public string Label;

        /// <summary>Plano do esboço: origem e eixos U, V (unitários, ortogonais), em mm.</summary>
        public double[] Origin, U, V;

        public readonly List<ProfileEdge> Edges = new List<ProfileEdge>();

        /// <summary>Extrusão: comprimento ao longo de W, a partir do plano do esboço.</summary>
        public double DepthMm;

        /// <summary>Volume da região pela geometria exata (Pappus na revolução), em mm³.</summary>
        public double VolumeMm3;

        /// <summary>Revolução: ângulo total em radianos. Menor que 2π = revolução PARCIAL,
        /// simétrica em volta do plano do esboço (o esboço fica no meio do arco).</summary>
        public double SweepRad = 2 * Math.PI;

        public double[] W { get { return Cross(U, V); } }

        /// <summary>Ponto do esboço (u, v) no mundo, a uma distância w do plano do esboço.</summary>
        public double[] ToWorld(double u, double v, double w)
        {
            double[] wv = W;
            return new[]
            {
                Origin[0] + u * U[0] + v * V[0] + w * wv[0],
                Origin[1] + u * U[1] + v * V[1] + w * wv[1],
                Origin[2] + u * U[2] + v * V[2] + w * wv[2]
            };
        }

        internal static double[] Cross(double[] a, double[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }
    }

    public sealed class CorrectionPlan
    {
        public readonly List<CorrectionRegion> Regions = new List<CorrectionRegion>();

        /// <summary>O que foi reconhecido mas não virou correção, e por quê.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>Regiões em que o sólido prismático já concorda com a malha (nada a fazer).</summary>
        public int AlreadyRight;
    }

    /// <summary>
    /// O QUE O SÓLIDO 2,5D NÃO FAZ, CORRIGIDO POR REGIÃO.
    ///
    /// A rota prismática secciona cada faixa Z no meio e extruda reto: chanfro de furo, raio em
    /// aresta horizontal e face inclinada ficam de fora (ou aproximados pela secção da malha).
    /// Cada uma dessas superfícies já é conhecida EXATA pelo reconhecedor; o que falta é dizer ao
    /// sólido de que lado dela fica o material.
    ///
    /// Para cada superfície, no plano em que ela vira curva (o meridiano do cone, a secção
    /// perpendicular ao eixo do raio, a secção vertical que contém a linha de maior declive da
    /// face inclinada), a curva divide uma região pequena em duas — e é a MALHA que responde, pelo
    /// número de enrolamento, se cada uma tem material:
    ///   - cone de volta inteira (chanfro, escareado, colarinho): a caixa (r, h) entre as duas
    ///     bocas, cortada pela geratriz → dois triângulos, revolvidos 360°;
    ///   - cilindro em arco de eixo fora de Z (raio em aresta horizontal): o canto entre o arco e
    ///     as tangentes nas pontas, e o segmento circular entre o arco e a corda, extrudados ao
    ///     longo do eixo; arco maior que 90° é partido em pedaços de até 90°;
    ///   - plano inclinado: a caixa (s, z) da linha de maior declive, cortada por ela → dois
    ///     triângulos, extrudados ao longo da horizontal da face.
    /// Região com material → protrusão; sem material → recorte. As duas operações são
    /// IDEMPOTENTES contra o que o sólido já tem (somar onde já há material, ou recortar onde já
    /// é vazio, não muda nada), e é isso que torna a correção segura sem conhecer o sólido: o
    /// prismático só pode errar DENTRO dessas regiões, porque fora delas a secção é exata.
    ///
    /// Região que a malha diz ser meio cheia, meio vazia (outra superfície atravessa a caixa) NÃO
    /// é corrigida: vira aviso. Região em que o plano prismático já concorda com a malha também
    /// não, para não encher a árvore de features que não mudam nada.
    /// </summary>
    public static class BoundaryCorrections
    {
        /// <summary>Plano inclinado menor que isto não é corrigido (retalho de concordância).</summary>
        public const double MinPlaneAreaMm2 = 2.0;

        public static CorrectionPlan Plan(double[] pointsMm, RecognitionResult r, PrismaticPlan prismatic)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (r == null) throw new ArgumentNullException(nameof(r));
            var plan = new CorrectionPlan();

            for (int i = 0; i < r.Surfaces.Count; i++)
            {
                RecognizedSurface s = r.Surfaces[i];
                var candidates = new List<CorrectionRegion>();

                if (s.Kind == "cone" && s.AxisMm != null && s.ApexMm != null)
                {
                    if (s.SweepDeg < 359.5)
                    {
                        plan.Warnings.Add($"{Describe(s)}: cone de {F(s.SweepDeg)}° de volta — só o de volta inteira é corrigido por revolução.");
                        continue;
                    }
                    candidates.AddRange(ConeRegions(s, r));
                }
                else if ((s.Kind == "toro" || s.Kind == "esfera") && s.AxisMm != null && s.PointMm != null)
                {
                    candidates.AddRange(TorusRegions(s));
                }
                else if (s.Kind == "cilindro" && s.AxisMm != null && Math.Abs(s.AxisMm[2]) != 1.0)
                {
                    if (Math.Abs(s.AxisMm[2]) > 1e-9)
                    {
                        plan.Warnings.Add($"{Describe(s)}: eixo inclinado (nem Z nem horizontal) — fica para a próxima.");
                        continue;
                    }
                    candidates.AddRange(CylinderRegions(s));
                }
                else if (s.Kind == "plano" && s.Normal != null && s.AreaMm2 >= MinPlaneAreaMm2
                         && Math.Abs(s.Normal[2]) > 1e-9 && Math.Abs(s.Normal[2]) < 1.0)
                {
                    candidates.AddRange(InclinedPlaneRegions(s, pointsMm, r, i));
                }
                else continue;

                foreach (CorrectionRegion c in candidates)
                {
                    c.Surface = i;
                    string verdict = Decide(c, pointsMm, prismatic);
                    if (verdict == "ok") plan.Regions.Add(c);
                    else if (verdict == "igual") plan.AlreadyRight++;
                    else plan.Warnings.Add(c.Label + ": " + verdict);
                }
            }
            return plan;
        }

        // ------------------------------------------------------------------ cone

        private static IEnumerable<CorrectionRegion> ConeRegions(RecognizedSurface s, RecognitionResult r)
        {
            double tanB = Math.Tan(s.HalfAngleDeg * Math.PI / 180.0);
            double r0 = s.RadiusMinMm, r1 = s.RadiusMaxMm;
            // As bocas do cone ENCOSTAM nos planos perpendiculares ao eixo que estão a menos de
            // 0,02 mm delas. O ápice sai do ajuste com o erro do STL (Z 82,9989 em vez de 83 no
            // chanfro da malha 6): sem isto o recorte parava em 85,999 e deixava uma pele de
            // 0,001 mm sobre o furo, com a quina viva intacta embaixo dela.
            double apexW = Dot(s.ApexMm, s.AxisMm);
            double h0 = SnapToPlane(apexW + r0 / tanB, s.AxisMm, r) - apexW;
            double h1 = SnapToPlane(apexW + r1 / tanB, s.AxisMm, r) - apexW;
            double[] u = Perpendicular(s.AxisMm);
            string label = (Math.Abs(s.HalfAngleDeg - 45) < 1e-9 ? "chanfro" : "cone " + F(s.HalfAngleDeg) + "°") +
                           $" Ø{F(2 * r0)} → Ø{F(2 * r1)} em {P(s.ApexMm)}";

            // Triângulo do lado do eixo (r0, h1) e do lado de fora (r1, h0).
            foreach (double[] corner in new[] { new[] { r0, h1 }, new[] { r1, h0 } })
            {
                var c = new CorrectionRegion
                {
                    Kind = "revolucao", Label = label, Origin = s.ApexMm, U = u, V = s.AxisMm
                };
                AddPolygon(c, new[] { new[] { r0, h0 }, new[] { r1, h1 }, corner });
                double area = 0.5 * Math.Abs((r1 - r0) * (corner[1] - h0) - (corner[0] - r0) * (h1 - h0));
                double rc = (r0 + r1 + corner[0]) / 3.0;
                c.VolumeMm3 = area * 2 * Math.PI * rc;
                yield return c;
            }
        }

        /// <summary>Posição w (ao longo do eixo) levada ao plano reconhecido perpendicular ao eixo
        /// mais próximo, se houver um a menos de 0,02 mm.</summary>
        private static double SnapToPlane(double w, double[] axis, RecognitionResult r)
        {
            double best = w, bestGap = 0.02;
            foreach (RecognizedSurface p in r.Surfaces)
            {
                if (p.Kind != "plano" || p.Normal == null) continue;
                double align = Dot(p.Normal, axis);
                if (Math.Abs(Math.Abs(align) - 1) > 1e-9) continue;
                double level = Dot(p.PointMm, axis);
                double gap = Math.Abs(level - w);
                if (gap < bestGap) { bestGap = gap; best = level; }
            }
            return best;
        }

        // ------------------------------------------------------------------ cilindro horizontal

        // ------------------------------------------------------------------ toro

        /// <summary>
        /// Toro (raio que contorna uma curva — o R2,5 da alça da malha 2 dando a volta na ponta
        /// R3): no meridiano (ρ, h), o perfil é um arco de círculo de raio r centrado a R do eixo;
        /// as regiões são as do cilindro (canto entre arco e tangentes, segmento entre arco e
        /// corda), REVOLVIDAS em volta do eixo pelo arco que o toro cobre, com o esboço no meio
        /// desse arco e a revolução simétrica — o sentido de giro nunca fica ambíguo.
        /// </summary>
        private static IEnumerable<CorrectionRegion> TorusRegions(RecognizedSurface s)
        {
            double[] a = s.AxisMm;
            double sweep = s.SweepDeg >= 359.5 || s.ArcFrom == null ? 2 * Math.PI : s.SweepDeg * Math.PI / 180;
            double[] u;
            if (sweep >= 2 * Math.PI - 1e-9) u = Perpendicular(a);
            else
            {
                // Meio do arco: ArcFrom girado de sweep/2 em volta do eixo.
                double[] f = s.ArcFrom, g = CorrectionRegion.Cross(a, f);
                double h = sweep / 2;
                u = Normalize(Add(Scale(f, Math.Cos(h)), Scale(g, Math.Sin(h))));
            }
            string label = $"raio R{F(s.RadiusMm)} contornando eixo {AxisLetter(a)} em {P(s.PointMm)}";

            double start = s.TubeFromDeg * Math.PI / 180;
            double tube = (s.TubeToDeg - s.TubeFromDeg) * Math.PI / 180;
            while (tube <= 0) tube += 2 * Math.PI;

            foreach (CorrectionRegion c in ArcRegions(label, new[] { s.MajorRadiusMm, 0.0 }, s.RadiusMm, start, tube))
            {
                c.Kind = "revolucao";
                c.Origin = s.PointMm;
                c.U = u;
                c.V = a;
                c.SweepRad = sweep;
                bool crossesAxis = false;
                double area, rc;
                AreaCentroidU(c, out area, out rc);
                foreach (ProfileEdge e in c.Edges) if (e.Start[0] < -1e-9) crossesAxis = true;
                if (crossesAxis) continue;
                c.VolumeMm3 = area * sweep * rc;
                yield return c;
            }
        }

        /// <summary>Regiões de um arco de raio R e centro c (no plano do esboço), partido em
        /// pedaços de até 90°: para cada pedaço, o canto entre o arco e as tangentes nas pontas e
        /// o segmento entre o arco e a corda; na volta inteira, também o miolo entre as cordas.
        /// Origem/eixos/tipo ficam para quem chama.</summary>
        private static IEnumerable<CorrectionRegion> ArcRegions(string label, double[] c, double R, double start, double sweep)
        {
            // Até ~92° é UM pedaço: o raio de 90° ajustado em 90,4° não pode virar dois cantos.
            int pieces = Math.Max(1, (int)Math.Ceiling(sweep / (Math.PI / 2) - 0.03));
            double step = sweep / pieces;
            var rim = new List<double[]>();
            for (int k = 0; k < pieces; k++)
            {
                double t0 = start + k * step, t1 = t0 + step, tm = t0 + step / 2;
                double[] pa = { c[0] + R * Math.Cos(t0), c[1] + R * Math.Sin(t0) };
                double[] pb = { c[0] + R * Math.Cos(t1), c[1] + R * Math.Sin(t1) };
                double[] pm = { c[0] + R * Math.Cos(tm), c[1] + R * Math.Sin(tm) };
                rim.Add(pa);
                double rc = R / Math.Cos(step / 2);
                double[] corner = { c[0] + rc * Math.Cos(tm), c[1] + rc * Math.Sin(tm) };

                var outer = new CorrectionRegion { Label = label };
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = pa, End = corner });
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = corner, End = pb });
                outer.Edges.Add(new ProfileEdge { Kind = "arco", Start = pb, Mid = pm, End = pa });
                yield return outer;

                var segment = new CorrectionRegion { Label = label };
                segment.Edges.Add(new ProfileEdge { Kind = "arco", Start = pa, Mid = pm, End = pb });
                segment.Edges.Add(new ProfileEdge { Kind = "reta", Start = pb, End = pa });
                yield return segment;
            }
            if (sweep >= 2 * Math.PI - 1e-9 && rim.Count >= 3)
            {
                var core = new CorrectionRegion { Label = label };
                AddPolygon(core, rim.ToArray());
                yield return core;
            }
        }

        /// <summary>Área e coordenada u do centroide da região (arcos polignalizados finos).</summary>
        private static void AreaCentroidU(CorrectionRegion c, out double area, out double cu)
        {
            var poly = new List<double[]>();
            foreach (ProfileEdge e in c.Edges)
            {
                if (e.Kind != "arco") { poly.Add(e.Start); continue; }
                // Arco por 3 pontos → centro, e 32 passos no sentido que passa pelo meio.
                double[] ctr = Circumcenter(e.Start, e.Mid, e.End);
                double a0 = Math.Atan2(e.Start[1] - ctr[1], e.Start[0] - ctr[0]);
                double am = Math.Atan2(e.Mid[1] - ctr[1], e.Mid[0] - ctr[0]);
                double a1 = Math.Atan2(e.End[1] - ctr[1], e.End[0] - ctr[0]);
                double ccw = Norm2Pi(a1 - a0);
                double sw = Norm2Pi(am - a0) < ccw ? ccw : ccw - 2 * Math.PI;
                double r = Math.Sqrt((e.Start[0] - ctr[0]) * (e.Start[0] - ctr[0]) + (e.Start[1] - ctr[1]) * (e.Start[1] - ctr[1]));
                for (int k = 0; k < 32; k++)
                {
                    double t = a0 + sw * k / 32;
                    poly.Add(new[] { ctr[0] + r * Math.Cos(t), ctr[1] + r * Math.Sin(t) });
                }
            }
            double A = 0, Cx = 0;
            for (int k = 0; k < poly.Count; k++)
            {
                double[] p = poly[k], q = poly[(k + 1) % poly.Count];
                double cr = p[0] * q[1] - q[0] * p[1];
                A += cr;
                Cx += (p[0] + q[0]) * cr;
            }
            area = Math.Abs(A) / 2;
            cu = Math.Abs(A) < 1e-15 ? 0 : Cx / (3 * A);
        }

        private static double[] Circumcenter(double[] a, double[] b, double[] c)
        {
            double d = 2 * (a[0] * (b[1] - c[1]) + b[0] * (c[1] - a[1]) + c[0] * (a[1] - b[1]));
            double a2 = a[0] * a[0] + a[1] * a[1], b2 = b[0] * b[0] + b[1] * b[1], c2 = c[0] * c[0] + c[1] * c[1];
            return new[]
            {
                (a2 * (b[1] - c[1]) + b2 * (c[1] - a[1]) + c2 * (a[1] - b[1])) / d,
                (a2 * (c[0] - b[0]) + b2 * (a[0] - c[0]) + c2 * (b[0] - a[0])) / d
            };
        }

        private static IEnumerable<CorrectionRegion> CylinderRegions(RecognizedSurface s)
        {
            double[] a = s.AxisMm;
            double[] u = Perpendicular(a);
            double[] v = CorrectionRegion.Cross(a, u); // U × V = a
            double R = s.RadiusMm;
            double wMid = Dot(s.PointMm, a);
            double w0 = wMid - s.LengthMm / 2.0;
            double[] origin = Add(s.PointMm, Scale(a, w0 - wMid));
            string label = $"raio R{F(R)} eixo {AxisLetter(a)} em {P(s.PointMm)}";

            double start, sweep;
            if (s.SweepDeg >= 360.0 || s.ArcFrom == null || s.ArcTo == null)
            {
                start = 0; sweep = 2 * Math.PI;
                label = $"furo/pino Ø{F(2 * R)} eixo {AxisLetter(a)} em {P(s.PointMm)}";
            }
            else
            {
                start = Math.Atan2(Dot(s.ArcFrom, v), Dot(s.ArcFrom, u));
                double end = Math.Atan2(Dot(s.ArcTo, v), Dot(s.ArcTo, u));
                sweep = end - start;
                while (sweep <= 0) sweep += 2 * Math.PI;
            }

            // Até ~92° é UM pedaço: o raio de 90° ajustado em 90,4° não pode virar dois cantos.
            int pieces = Math.Max(1, (int)Math.Ceiling(sweep / (Math.PI / 2) - 0.03));
            double step = sweep / pieces;
            var rim = new List<double[]>();
            for (int k = 0; k < pieces; k++)
            {
                double t0 = start + k * step, t1 = t0 + step, tm = t0 + step / 2;
                double[] pa = { R * Math.Cos(t0), R * Math.Sin(t0) };
                double[] pb = { R * Math.Cos(t1), R * Math.Sin(t1) };
                double[] pm = { R * Math.Cos(tm), R * Math.Sin(tm) };
                rim.Add(pa);

                // Canto: interseção das tangentes nas pontas, a R / cos(passo/2) do centro.
                double rc = R / Math.Cos(step / 2);
                double[] corner = { rc * Math.Cos(tm), rc * Math.Sin(tm) };

                var outer = Region(label, origin, u, v, s.LengthMm);
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = pa, End = corner });
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = corner, End = pb });
                outer.Edges.Add(new ProfileEdge { Kind = "arco", Start = pb, Mid = pm, End = pa });
                outer.VolumeMm3 = (R * R * Math.Tan(step / 2) - R * R * step / 2) * s.LengthMm;
                yield return outer;

                var segment = Region(label, origin, u, v, s.LengthMm);
                segment.Edges.Add(new ProfileEdge { Kind = "arco", Start = pa, Mid = pm, End = pb });
                segment.Edges.Add(new ProfileEdge { Kind = "reta", Start = pb, End = pa });
                segment.VolumeMm3 = R * R / 2 * (step - Math.Sin(step)) * s.LengthMm;
                yield return segment;
            }

            // Volta inteira: o miolo entre as cordas também é região (o furo inteiro).
            if (sweep >= 2 * Math.PI - 1e-9 && rim.Count >= 3)
            {
                var core = Region(label, origin, u, v, s.LengthMm);
                AddPolygon(core, rim.ToArray());
                double area = 0;
                for (int k = 0; k < rim.Count; k++)
                {
                    double[] p = rim[k], q = rim[(k + 1) % rim.Count];
                    area += p[0] * q[1] - q[0] * p[1];
                }
                core.VolumeMm3 = Math.Abs(area) / 2 * s.LengthMm;
                yield return core;
            }
        }

        // ------------------------------------------------------------------ plano inclinado

        private static IEnumerable<CorrectionRegion> InclinedPlaneRegions(RecognizedSurface s, double[] pointsMm,
            RecognitionResult r, int index)
        {
            double[] n = s.Normal;
            double[] z = { 0, 0, 1 };
            double[] d = Normalize(CorrectionRegion.Cross(n, z)); // horizontal, dentro da face
            double[] q = CorrectionRegion.Cross(z, d);           // horizontal, perpendicular a d

            // Extensão da face ao longo de d, e a linha de maior declive na secção (s, z).
            double wMin = double.MaxValue, wMax = double.MinValue;
            double zMin = double.MaxValue, zMax = double.MinValue, sAtZMin = 0, sAtZMax = 0;
            for (int t = 0; t < r.RegionOfTriangle.Length; t++)
            {
                if (r.RegionOfTriangle[t] != index) continue;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double[] p = { pointsMm[b], pointsMm[b + 1], pointsMm[b + 2] };
                    double w = Dot(p, d), sv = Dot(p, q);
                    if (w < wMin) wMin = w;
                    if (w > wMax) wMax = w;
                    if (p[2] < zMin) { zMin = p[2]; sAtZMin = sv; }
                    if (p[2] > zMax) { zMax = p[2]; sAtZMax = sv; }
                }
            }
            if (wMax - wMin < 1e-6 || zMax - zMin < 1e-6) yield break;

            // A linha passa EXATA pelo plano reconhecido: s sai do plano, não do vértice.
            double c = Dot(n, s.PointMm);
            double nq = Dot(n, q);
            if (Math.Abs(nq) < 1e-12) yield break;
            double s0 = (c - n[2] * zMin) / nq, s1 = (c - n[2] * zMax) / nq;

            double fromVertical = Math.Asin(Math.Min(1, Math.Abs(n[2]))) * 180 / Math.PI;
            string label = $"face inclinada {F(fromVertical)}° da vertical, normal {P(n)}";
            double[] origin = Scale(d, wMin);

            foreach (double[] corner in new[] { new[] { s0, zMax }, new[] { s1, zMin } })
            {
                var reg = Region(label, origin, q, z, wMax - wMin);
                AddPolygon(reg, new[] { new[] { s0, zMin }, new[] { s1, zMax }, corner });
                reg.VolumeMm3 = 0.5 * Math.Abs(s1 - s0) * (zMax - zMin) * (wMax - wMin);
                yield return reg;
            }
        }

        // ------------------------------------------------------------------ decisão

        /// <summary>
        /// "ok" (a correção entra, com <see cref="CorrectionRegion.Add"/> preenchido), "igual"
        /// (o plano prismático já concorda com a malha) ou o motivo de recusa.
        /// </summary>
        private static string Decide(CorrectionRegion c, double[] pointsMm, PrismaticPlan prismatic)
        {
            List<double[]> samples = Samples(c);
            if (samples.Count == 0) return "região fina demais para testar contra a malha (menos de 2 × " + SampleMarginMm.ToString(CultureInfo.InvariantCulture) + " mm)";

            int inside = 0;
            foreach (double[] p in samples) if (MeshSolid.Contains(pointsMm, p)) inside++;
            if (inside != 0 && inside != samples.Count)
                return $"região meio cheia ({inside} de {samples.Count} pontos dentro da malha) — outra superfície a atravessa; não corrigida";
            c.Add = inside == samples.Count;

            if (prismatic != null)
            {
                int agree = 0;
                foreach (double[] p in samples) if (PrismaticContains(prismatic, p) == c.Add) agree++;
                if (agree == samples.Count) return "igual";
            }
            return "ok";
        }

        /// <summary>Pontos de teste: o centroide da região e quatro pontos entre ele e as pontas,
        /// no meio da extrusão — ou em três ângulos em volta do eixo, na revolução.</summary>
        /// <summary>Folga mínima entre ponto de teste e borda da região: acima da flecha da
        /// tesselação (~0,02 mm), senão o ponto cai do lado errado do polígono INSCRITO da malha.</summary>
        private const double SampleMarginMm = 0.04;

        /// <summary>
        /// Pontos de teste DENTRO da região e longe da borda. A média dos vértices não serve: o
        /// canto entre o arco R2,5 e as tangentes é uma meia-lua de 0,21 mm, e a média (com o
        /// meio do arco) caía do lado de dentro do arco — a região parecia "meio cheia" e o raio
        /// da alça da malha 2 não era corrigido. Grade sobre a caixa da região, fica o que está
        /// dentro do polígono e a mais da folga da borda, os mais fundos primeiro.
        /// </summary>
        private static List<double[]> InteriorPoints(CorrectionRegion c)
        {
            List<double[]> poly = Polygon(c);
            double minU = double.MaxValue, minV = double.MaxValue, maxU = double.MinValue, maxV = double.MinValue;
            foreach (double[] p in poly)
            {
                minU = Math.Min(minU, p[0]); maxU = Math.Max(maxU, p[0]);
                minV = Math.Min(minV, p[1]); maxV = Math.Max(maxV, p[1]);
            }
            var cand = new List<Tuple<double, double[]>>();
            const int N = 24;
            for (int i = 1; i < N; i++)
                for (int j = 1; j < N; j++)
                {
                    double[] q = { minU + (maxU - minU) * i / N, minV + (maxV - minV) * j / N };
                    if (Math.Abs(WindingXY(poly, q[0], q[1])) < 0.5) continue;
                    double d = DistanceToPolygon(poly, q);
                    if (d >= SampleMarginMm) cand.Add(Tuple.Create(d, q));
                }
            cand.Sort((a, b) => b.Item1.CompareTo(a.Item1));

            // Os mais fundos, mas espalhados: pula candidato colado num já escolhido.
            var chosen = new List<double[]>();
            double spread = 0.15 * Math.Max(maxU - minU, maxV - minV);
            foreach (var t in cand)
            {
                bool near = false;
                foreach (double[] q in chosen)
                    if (Math.Sqrt((q[0] - t.Item2[0]) * (q[0] - t.Item2[0]) + (q[1] - t.Item2[1]) * (q[1] - t.Item2[1])) < spread) { near = true; break; }
                if (near) continue;
                chosen.Add(t.Item2);
                if (chosen.Count == 5) break;
            }
            return chosen;
        }

        /// <summary>A região como polígono, com os arcos em 32 passos.</summary>
        private static List<double[]> Polygon(CorrectionRegion c)
        {
            var poly = new List<double[]>();
            foreach (ProfileEdge e in c.Edges)
            {
                if (e.Kind != "arco") { poly.Add(e.Start); continue; }
                double[] ctr = Circumcenter(e.Start, e.Mid, e.End);
                double a0 = Math.Atan2(e.Start[1] - ctr[1], e.Start[0] - ctr[0]);
                double am = Math.Atan2(e.Mid[1] - ctr[1], e.Mid[0] - ctr[0]);
                double a1 = Math.Atan2(e.End[1] - ctr[1], e.End[0] - ctr[0]);
                double ccw = Norm2Pi(a1 - a0);
                double sw = Norm2Pi(am - a0) < ccw ? ccw : ccw - 2 * Math.PI;
                double r = Math.Sqrt((e.Start[0] - ctr[0]) * (e.Start[0] - ctr[0]) + (e.Start[1] - ctr[1]) * (e.Start[1] - ctr[1]));
                for (int k = 0; k < 32; k++)
                {
                    double t = a0 + sw * k / 32;
                    poly.Add(new[] { ctr[0] + r * Math.Cos(t), ctr[1] + r * Math.Sin(t) });
                }
            }
            return poly;
        }

        private static double DistanceToPolygon(List<double[]> poly, double[] q)
        {
            double best = double.MaxValue;
            for (int k = 0; k < poly.Count; k++)
            {
                double[] a = poly[k], b = poly[(k + 1) % poly.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1];
                double l2 = dx * dx + dy * dy;
                double t = l2 < 1e-24 ? 0 : Math.Max(0, Math.Min(1, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / l2));
                double ex = a[0] + t * dx - q[0], ey = a[1] + t * dy - q[1];
                best = Math.Min(best, Math.Sqrt(ex * ex + ey * ey));
            }
            return best;
        }

        private static List<double[]> Samples(CorrectionRegion c)
        {
            List<double[]> local = InteriorPoints(c);

            var world = new List<double[]>();
            if (c.Kind == "revolucao")
            {
                double[] w = c.W;
                double[] angs = c.SweepRad >= 2 * Math.PI - 1e-9
                    ? new[] { 0.0, 2 * Math.PI / 3, 4 * Math.PI / 3 }
                    : new[] { -0.35 * c.SweepRad, 0.0, 0.35 * c.SweepRad };
                foreach (double ang in angs)
                {
                    double ca = Math.Cos(ang), sa = Math.Sin(ang);
                    double[] radial = { ca * c.U[0] + sa * w[0], ca * c.U[1] + sa * w[1], ca * c.U[2] + sa * w[2] };
                    foreach (double[] p in local)
                        world.Add(new[]
                        {
                            c.Origin[0] + p[0] * radial[0] + p[1] * c.V[0],
                            c.Origin[1] + p[0] * radial[1] + p[1] * c.V[1],
                            c.Origin[2] + p[0] * radial[2] + p[1] * c.V[2]
                        });
                }
            }
            else
            {
                foreach (double frac in new[] { 0.25, 0.5, 0.75 })
                    foreach (double[] p in local) world.Add(c.ToWorld(p[0], p[1], frac * c.DepthMm));
            }
            return world;
        }

        /// <summary>O ponto está dentro do sólido que o plano prismático vai construir? Faixa que
        /// contém o z, e enrolamento dos laços (contorno anti-horário, furo horário).</summary>
        public static bool PrismaticContains(PrismaticPlan plan, double[] p)
        {
            foreach (SectionBand band in plan.Bands)
            {
                if (p[2] <= band.Z0 || p[2] >= band.Z1) continue;
                double winding = 0;
                foreach (SectionLoop loop in band.Loops)
                    winding += WindingXY(Polygonize(loop), p[0], p[1]);
                return Math.Abs(winding) > 0.5;
            }
            return false;
        }

        private static List<double[]> Polygonize(SectionLoop loop)
        {
            var poly = new List<double[]>();
            foreach (SectionElement e in loop.Elements)
            {
                if (e.Kind == "circulo")
                {
                    bool cw = loop.SignedAreaMm2 < 0;
                    for (int k = 0; k < 72; k++)
                    {
                        double t = 2 * Math.PI * k / 72 * (cw ? -1 : 1);
                        poly.Add(new[] { e.Center[0] + e.RadiusMm * Math.Cos(t), e.Center[1] + e.RadiusMm * Math.Sin(t) });
                    }
                    continue;
                }
                poly.Add(e.Start);
                if (e.Kind == "arco" && e.Center != null && e.Mid != null)
                {
                    double a0 = Math.Atan2(e.Start[1] - e.Center[1], e.Start[0] - e.Center[0]);
                    double am = Math.Atan2(e.Mid[1] - e.Center[1], e.Mid[0] - e.Center[0]);
                    double a1 = Math.Atan2(e.End[1] - e.Center[1], e.End[0] - e.Center[0]);
                    double ccw = Norm2Pi(a1 - a0);
                    bool viaMidCcw = Norm2Pi(am - a0) < ccw;
                    double sweep = viaMidCcw ? ccw : ccw - 2 * Math.PI;
                    for (int k = 1; k < 16; k++)
                    {
                        double t = a0 + sweep * k / 16;
                        poly.Add(new[] { e.Center[0] + e.RadiusMm * Math.Cos(t), e.Center[1] + e.RadiusMm * Math.Sin(t) });
                    }
                }
            }
            return poly;
        }

        private static double Norm2Pi(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

        private static double WindingXY(List<double[]> poly, double x, double y)
        {
            double sum = 0;
            for (int k = 0; k < poly.Count; k++)
            {
                double[] a = poly[k], b = poly[(k + 1) % poly.Count];
                double a0 = Math.Atan2(a[1] - y, a[0] - x), a1 = Math.Atan2(b[1] - y, b[0] - x);
                double d = a1 - a0;
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d < -Math.PI) d += 2 * Math.PI;
                sum += d;
            }
            return sum / (2 * Math.PI);
        }

        // ------------------------------------------------------------------ apoio

        private static CorrectionRegion Region(string label, double[] origin, double[] u, double[] v, double depth)
        {
            return new CorrectionRegion { Kind = "extrusao", Label = label, Origin = origin, U = u, V = v, DepthMm = depth };
        }

        private static void AddPolygon(CorrectionRegion c, double[][] pts)
        {
            for (int k = 0; k < pts.Length; k++)
                c.Edges.Add(new ProfileEdge { Kind = "reta", Start = pts[k], End = pts[(k + 1) % pts.Length] });
        }

        private static string Describe(RecognizedSurface s)
        {
            return s.Kind + " " + P(s.PointMm);
        }

        private static string AxisLetter(double[] a)
        {
            if (Math.Abs(a[0]) == 1) return "X";
            if (Math.Abs(a[1]) == 1) return "Y";
            if (Math.Abs(a[2]) == 1) return "Z";
            return P(a);
        }

        /// <summary>Perpendicular unitária determinística (a mesma regra do reconhecedor).</summary>
        internal static double[] Perpendicular(double[] a)
        {
            double[] pick = Math.Abs(a[0]) < 0.9 ? new double[] { 1, 0, 0 } : new double[] { 0, 1, 0 };
            return Normalize(CorrectionRegion.Cross(a, pick));
        }

        private static double[] Normalize(double[] v)
        {
            double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            return new[] { v[0] / l, v[1] / l, v[2] / l };
        }

        private static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
        private static double[] Add(double[] a, double[] b) { return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] }; }
        private static double[] Scale(double[] a, double k) { return new[] { a[0] * k, a[1] * k, a[2] * k }; }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string P(double[] v) => v == null ? "(?)" : $"({F(v[0])}; {F(v[1])}; {F(v[2])})";
    }
}
