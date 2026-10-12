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

        /// <summary>Degrau de faixa (<c>BandStepRegions</c>): conserta o escalonamento de uma
        /// faixa sobre um raio ou rampa deitados. Plano que precisa disso não é "Z limpo".</summary>
        public bool BandStep;

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

        /// <summary>
        /// Degrau de faixa só entra se deixar pelo menos isto (mm³) de erro fora das correções já
        /// aceitas. Na vector_v2-6 (raios R0,5 em Y) os degraus de 137 eram quase todos
        /// lascas de centésimos e só 4 passavam de 0,5; no tool-organizer_v2, 51 de 54 passam de
        /// 0,5 e 50 passam de 5.
        /// </summary>
        public const double MinStepMm3 = 0.5;

        public static CorrectionPlan Plan(double[] pointsMm, RecognitionResult r, PrismaticPlan prismatic)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (r == null) throw new ArgumentNullException(nameof(r));
            var plan = new CorrectionPlan();
            // Tudo aqui é no frame do plano (Z local = direção de extrusão); só os RÓTULOS voltam
            // ao mundo, para o relatório falar das coordenadas e eixos que o projetista vê.
            ExtrusionFrame f = prismatic != null && prismatic.Frame != null ? prismatic.Frame : ExtrusionFrame.Z;
            PlanEvaluator.ColumnGrid columns = null;

            for (int i = 0; i < r.Surfaces.Count; i++)
            {
                RecognizedSurface s = r.Surfaces[i];
                var candidates = new List<CorrectionRegion>();

                if (s.Kind == "cone" && s.AxisMm != null && s.ApexMm != null && Math.Abs(s.AxisMm[2]) < 1e-9)
                {
                    // Cone DEITADO (eixo perpendicular à extrusão): as faixas o cortam em degraus,
                    // de volta inteira ou não — anel, miolo e cantos, todos revolvidos/extrudados.
                    candidates.AddRange(LyingConeRegions(s, r, f));
                }
                else if (s.Kind == "cone" && s.AxisMm != null && s.ApexMm != null)
                {
                    if (s.SweepDeg < 359.5)
                    {
                        plan.Warnings.Add($"{Describe(s, f)}: cone de {F(s.SweepDeg)}° de volta — só o de volta inteira é corrigido por revolução.");
                        continue;
                    }
                    candidates.AddRange(ConeRegions(s, r, f));
                }
                else if ((s.Kind == "toro" || s.Kind == "esfera") && s.AxisMm != null && s.PointMm != null)
                {
                    candidates.AddRange(TorusRegions(s, f));
                }
                else if (s.Kind == "cilindro" && s.AxisMm != null && Math.Abs(s.AxisMm[2]) != 1.0)
                {
                    if (Math.Abs(s.AxisMm[2]) > 1e-9)
                    {
                        plan.Warnings.Add($"{Describe(s, f)}: eixo inclinado (nem {f.AxisName} nem perpendicular a {f.AxisName}) — fica para a próxima.");
                        continue;
                    }
                    candidates.AddRange(CylinderRegions(s, r, f));
                }
                else if (s.Kind == "plano" && s.Normal != null && s.AreaMm2 >= MinPlaneAreaMm2
                         && Math.Abs(s.Normal[2]) > 1e-9 && Math.Abs(s.Normal[2]) < 1.0)
                {
                    candidates.AddRange(InclinedPlaneRegions(s, pointsMm, r, i, f));
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

                // Degraus das faixas (cilindro DEITADO e plano inclinado): o que o canto, o segmento
                // e os triângulos não cobrem.
                IEnumerable<CorrectionRegion> steps =
                    prismatic == null ? null
                    : s.Kind == "cilindro" && Math.Abs(s.AxisMm[2]) < 1e-9 ? BandStepRegions(s, r, prismatic, f)
                    : s.Kind == "plano" ? InclinedPlaneSteps(s, pointsMm, r, i, prismatic, f)
                    : null;
                if (steps != null)
                {
                    // Degrau é candidato aos milhares (5.583 na vector_v2-6, nas 7 direções): o
                    // enrolamento exato em 65 mil triângulos por ponto custava 91 s. A grade de
                    // colunas da previsão responde o mesmo "dentro da malha?" em microssegundos.
                    if (columns == null) columns = new PlanEvaluator.ColumnGrid(pointsMm, 0.5);
                    PlanEvaluator.ColumnGrid grid = columns;
                    Func<double[], bool> insideMesh = p => grid.Inside(p[0], p[1], p[2]);
                    foreach (CorrectionRegion c in steps)
                    {
                        c.Surface = i;
                        string verdict = Decide(c, insideMesh, prismatic);
                        if (verdict == "ok")
                        {
                            if (UncoveredMm3(c, plan.Regions) >= MinStepMm3) plan.Regions.Add(c);
                        }
                        else if (verdict == "igual") plan.AlreadyRight++;
                        // "meio cheia" aqui é o normal (outra superfície no caminho até a borda da
                        // faixa) e não vira aviso: o degrau é complemento, não a correção principal.
                    }
                }
            }
            return plan;
        }

        // ------------------------------------------------------------------ cone

        private static IEnumerable<CorrectionRegion> ConeRegions(RecognizedSurface s, RecognitionResult r, ExtrusionFrame f)
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
                           $" Ø{F(2 * r0)} → Ø{F(2 * r1)} em {P(s.ApexMm, f)}";

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

        // ------------------------------------------------------------------ cone deitado

        /// <summary>
        /// Cone de eixo PERPENDICULAR à extrusão — o recorte em U cônico (Ø22,5 → Ø27,8 a 44°,
        /// 179°) atravessando a parede da caixa do stack-container (2026-10-11). As faixas cortam
        /// o cone em degraus, e o arco saía um entalhe RETANGULAR no sólido. Três tipos de região,
        /// cada um decidido pela malha:
        ///   - o ANEL entre as duas bocas: os dois triângulos de <see cref="ConeRegions"/>,
        ///     revolvidos só pelo arco que existe (esboço no meio dele, revolução simétrica);
        ///   - o MIOLO, dentro da boca menor, de ponta a ponta do cone: segmentos entre arco e
        ///     corda e o leque do centro até o arco, extrudados ao longo do eixo;
        ///   - os CANTOS, fora da boca maior: entre o arco e as tangentes nas pontas, extrudados.
        /// </summary>
        private static IEnumerable<CorrectionRegion> LyingConeRegions(RecognizedSurface s, RecognitionResult r, ExtrusionFrame f)
        {
            double tanB = Math.Tan(s.HalfAngleDeg * Math.PI / 180.0);
            double r0 = s.RadiusMinMm, r1 = s.RadiusMaxMm;
            double apexW = Dot(s.ApexMm, s.AxisMm);
            double h0 = SnapToPlane(apexW + r0 / tanB, s.AxisMm, r) - apexW;
            double h1 = SnapToPlane(apexW + r1 / tanB, s.AxisMm, r) - apexW;
            double[] a = s.AxisMm;
            string label = (Math.Abs(s.HalfAngleDeg - 45) < 1e-9 ? "chanfro" : "cone " + F(s.HalfAngleDeg) + "°") +
                           $" Ø{F(2 * r0)} → Ø{F(2 * r1)} eixo {AxisLetter(a, f)} em {P(s.ApexMm, f)}";

            bool full = s.SweepDeg >= 359.5 || s.ArcFrom == null;
            double sweep = full ? 2 * Math.PI : s.SweepDeg * Math.PI / 180;
            double[] u0 = Perpendicular(a), v0 = CorrectionRegion.Cross(a, u0);
            double start = full ? 0 : Math.Atan2(Dot(s.ArcFrom, v0), Dot(s.ArcFrom, u0));
            double[] mid = full ? u0 : Normalize(Add(Scale(u0, Math.Cos(start + sweep / 2)), Scale(v0, Math.Sin(start + sweep / 2))));

            // Anel, revolvido pelo arco.
            foreach (double[] corner in new[] { new[] { r0, h1 }, new[] { r1, h0 } })
            {
                var c = new CorrectionRegion { Kind = "revolucao", Label = label, Origin = s.ApexMm, U = mid, V = a, SweepRad = sweep };
                AddPolygon(c, new[] { new[] { r0, h0 }, new[] { r1, h1 }, corner });
                double area = 0.5 * Math.Abs((r1 - r0) * (corner[1] - h0) - (corner[0] - r0) * (h1 - h0));
                c.VolumeMm3 = area * sweep * (r0 + r1 + corner[0]) / 3.0;
                yield return c;
            }

            // Miolo (boca menor) e cantos (boca maior), extrudados ao longo do eixo entre as bocas.
            double hLo = Math.Min(h0, h1), hHi = Math.Max(h0, h1);
            double[] origin = Add(s.ApexMm, Scale(a, hLo));
            foreach (CorrectionRegion c in ArcPieces(label, r0, start, sweep, false, true, true))
            {
                c.Kind = "extrusao"; c.Origin = origin; c.U = u0; c.V = v0; c.DepthMm = hHi - hLo;
                yield return c;
            }
            foreach (CorrectionRegion c in ArcPieces(label, r1, start, sweep, true, false, false))
            {
                c.Kind = "extrusao"; c.Origin = origin; c.U = u0; c.V = v0; c.DepthMm = hHi - hLo;
                yield return c;
            }
        }

        /// <summary>
        /// Pedaços (até ~92° cada) de um arco de raio R centrado na origem do esboço: canto entre
        /// arco e tangentes, segmento entre arco e corda, e o leque do centro (miolo) — cada um
        /// quando pedido. Origem, eixos, tipo e profundidade ficam para quem chama.
        /// </summary>
        private static IEnumerable<CorrectionRegion> ArcPieces(string label, double R, double start, double sweep,
            bool corners, bool segments, bool core)
        {
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
                if (k == pieces - 1 && sweep < 2 * Math.PI - 1e-9) rim.Add(pb);
                if (corners)
                {
                    double rc = R / Math.Cos(step / 2);
                    double[] corner = { rc * Math.Cos(tm), rc * Math.Sin(tm) };
                    var outer = new CorrectionRegion { Label = label };
                    outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = pa, End = corner });
                    outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = corner, End = pb });
                    outer.Edges.Add(new ProfileEdge { Kind = "arco", Start = pb, Mid = pm, End = pa });
                    yield return outer;
                }
                if (segments)
                {
                    var seg = new CorrectionRegion { Label = label };
                    seg.Edges.Add(new ProfileEdge { Kind = "arco", Start = pa, Mid = pm, End = pb });
                    seg.Edges.Add(new ProfileEdge { Kind = "reta", Start = pb, End = pa });
                    yield return seg;
                }
            }
            if (core && rim.Count >= 2)
            {
                var poly = new List<double[]>();
                if (sweep < 2 * Math.PI - 1e-9) poly.Add(new double[] { 0, 0 });
                poly.AddRange(rim);
                if (poly.Count >= 3)
                {
                    var c = new CorrectionRegion { Label = label };
                    AddPolygon(c, poly.ToArray());
                    yield return c;
                }
            }
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
        private static IEnumerable<CorrectionRegion> TorusRegions(RecognizedSurface s, ExtrusionFrame f)
        {
            double[] a = s.AxisMm;
            double sweep = s.SweepDeg >= 359.5 || s.ArcFrom == null ? 2 * Math.PI : s.SweepDeg * Math.PI / 180;
            double[] u;
            if (sweep >= 2 * Math.PI - 1e-9) u = Perpendicular(a);
            else
            {
                // Meio do arco: ArcFrom girado de sweep/2 em volta do eixo.
                double[] from = s.ArcFrom, g = CorrectionRegion.Cross(a, from);
                double h = sweep / 2;
                u = Normalize(Add(Scale(from, Math.Cos(h)), Scale(g, Math.Sin(h))));
            }
            string label = $"raio R{F(s.RadiusMm)} contornando eixo {AxisLetter(a, f)} em {P(s.PointMm, f)}";

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

        private static IEnumerable<CorrectionRegion> CylinderRegions(RecognizedSurface s, RecognitionResult r, ExtrusionFrame f)
        {
            double[] a = s.AxisMm;
            double[] u = Perpendicular(a);
            double[] v = CorrectionRegion.Cross(a, u); // U × V = a
            double R = s.RadiusMm;
            double wMid = Dot(s.PointMm, a);
            // As pontas ao longo do eixo vão às faces planas reconhecidas (o comprimento sai dos
            // vértices da malha, a 1e-5 delas): a correção não deixa lâmina nem fresta na parede.
            double w0 = SnapToPlane(wMid - s.LengthMm / 2.0, a, r);
            double len = SnapToPlane(wMid + s.LengthMm / 2.0, a, r) - w0;
            double[] origin = Add(s.PointMm, Scale(a, w0 - wMid));
            string label = $"raio R{F(R)} eixo {AxisLetter(a, f)} em {P(s.PointMm, f)}";

            double start, sweep;
            if (s.SweepDeg >= 360.0 || s.ArcFrom == null || s.ArcTo == null)
            {
                start = 0; sweep = 2 * Math.PI;
                label = $"furo/pino Ø{F(2 * R)} eixo {AxisLetter(a, f)} em {P(s.PointMm, f)}";
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

                var outer = Region(label, origin, u, v, len);
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = pa, End = corner });
                outer.Edges.Add(new ProfileEdge { Kind = "reta", Start = corner, End = pb });
                outer.Edges.Add(new ProfileEdge { Kind = "arco", Start = pb, Mid = pm, End = pa });
                outer.VolumeMm3 = (R * R * Math.Tan(step / 2) - R * R * step / 2) * len;
                yield return outer;

                var segment = Region(label, origin, u, v, len);
                segment.Edges.Add(new ProfileEdge { Kind = "arco", Start = pa, Mid = pm, End = pb });
                segment.Edges.Add(new ProfileEdge { Kind = "reta", Start = pb, End = pa });
                segment.VolumeMm3 = R * R / 2 * (step - Math.Sin(step)) * len;
                yield return segment;
            }

            // Cilindro DEITADO em arco de meia volta ou mais (o fundo de um rasgo em U atravessando
            // uma parede): as faixas erram também por DENTRO do arco — o leque do centro até ele é
            // região.
            if (sweep < 2 * Math.PI - 1e-9 && Math.Abs(a[2]) < 1e-9 && sweep >= Math.PI - 1e-9)
            {
                var fan = new List<double[]> { new double[] { 0, 0 } };
                fan.AddRange(rim);
                fan.Add(new[] { R * Math.Cos(start + sweep), R * Math.Sin(start + sweep) });
                var core = Region(label, origin, u, v, len);
                AddPolygon(core, fan.ToArray());
                double fa = 0;
                for (int k = 0; k < fan.Count; k++)
                {
                    double[] p = fan[k], q = fan[(k + 1) % fan.Count];
                    fa += p[0] * q[1] - q[0] * p[1];
                }
                core.VolumeMm3 = Math.Abs(fa) / 2 * len;
                yield return core;
            }

            // Volta inteira: o miolo entre as cordas também é região (o furo inteiro).
            if (sweep >= 2 * Math.PI - 1e-9 && rim.Count >= 3)
            {
                var core = Region(label, origin, u, v, len);
                AddPolygon(core, rim.ToArray());
                double area = 0;
                for (int k = 0; k < rim.Count; k++)
                {
                    double[] p = rim[k], q = rim[(k + 1) % rim.Count];
                    area += p[0] * q[1] - q[0] * p[1];
                }
                core.VolumeMm3 = Math.Abs(area) / 2 * len;
                yield return core;
            }
        }

        // ------------------------------------------------------------------ degraus das faixas

        /// <summary>Área mínima (mm²) de degrau que vale feature.</summary>
        private const double MinStepAreaMm2 = 0.01;

        /// <summary>
        /// Horizontal de degrau a menos disto do nível da faixa vai PARA o nível. A ponta do R20 das
        /// divisórias está em z 46,94215 e o nível (do vértice da malha) em 46,94214: o degrau de
        /// cima começava 0,00001 mm acima do de baixo, e o sólido ficou com uma fresta interna.
        /// </summary>
        private const double StepSnapMm = 0.001;

        private static double SnapToBand(double z, double z0, double z1)
        {
            if (Math.Abs(z - z0) < StepSnapMm) return z0;
            if (Math.Abs(z - z1) < StepSnapMm) return z1;
            return z;
        }

        /// <summary>
        /// Cilindro DEITADO (eixo perpendicular à extrusão) cortado pelas faixas: em cada faixa, a
        /// borda do sólido prismático na secção do cilindro é UMA reta vertical (a secção do meio
        /// da faixa vale de baixo a cima), e o erro é a área entre o arco e essa reta, na parte da
        /// altura da faixa em que o arco existe. O canto (tangentes) e o segmento (corda) de
        /// <see cref="CylinderRegions"/> só cobrem isso quando a faixa termina dentro do polígono
        /// das tangentes — no tool-organizer_v2 (2026-10-11) não terminava: sobrou um triângulo
        /// acima da tangente de 45° em cada divisória e, na parede da frente (o rebaixo para os
        /// dedos, arcos R20 → R32 → R36 → R513 em Y), blocos de até 3,4 mm acima da curva.
        ///
        /// Cada degrau: o pedaço do arco (monótono em altura) dentro da faixa, as horizontais até
        /// a borda da faixa e a vertical da borda — partido em dois onde o arco cruza a borda. A
        /// borda vem dos contornos da faixa, na reta da secção. A malha decide entre recorte e
        /// protrusão; degrau "meio cheio" (outra superfície no caminho) é descartado.
        /// </summary>
        private static IEnumerable<CorrectionRegion> BandStepRegions(RecognizedSurface s, RecognitionResult r, PrismaticPlan prismatic, ExtrusionFrame f)
        {
            if (s.SweepDeg >= 359.5 || s.ArcFrom == null || s.ArcTo == null || s.PointMm == null) yield break;
            double[] a = s.AxisMm;
            double[] ez = { 0, 0, 1 };
            double[] u = Normalize(CorrectionRegion.Cross(ez, a)); // horizontal; U × V = a
            double[] v = ez;
            double R = s.RadiusMm;
            double[] center = s.PointMm;
            double wMid = Dot(center, a);
            double w0 = SnapToPlane(wMid - s.LengthMm / 2, a, r);
            double L = SnapToPlane(wMid + s.LengthMm / 2, a, r) - w0;
            double[] origin = Add(center, Scale(a, w0 - wMid));
            string label = $"raio R{F(R)} eixo {AxisLetter(a, f)} em {P(s.PointMm, f)}, degrau da faixa";

            double start = Math.Atan2(Dot(s.ArcFrom, v), Dot(s.ArcFrom, u));
            double end = Math.Atan2(Dot(s.ArcTo, v), Dot(s.ArcTo, u));
            double sweep = end - start;
            while (sweep <= 0) sweep += 2 * Math.PI;

            // Pedaços monótonos em altura: corta nos ângulos ±90° (alto e baixo do círculo).
            var cuts = new List<double> { start };
            foreach (double extreme in new[] { Math.PI / 2, -Math.PI / 2 })
            {
                double t = start + Norm2Pi(extreme - start);
                if (t > start + 1e-9 && t < start + sweep - 1e-9) cuts.Add(t);
            }
            cuts.Add(start + sweep);
            cuts.Sort();

            foreach (SectionBand band in prismatic.Bands)
            {
                double z0 = band.Z0 - center[2], z1 = band.Z1 - center[2]; // relativo ao eixo
                List<double> borders = BorderCrossings(band, center, u);
                if (borders.Count == 0) continue;

                for (int k = 0; k + 1 < cuts.Count; k++)
                {
                    double ta = cuts[k], tb = cuts[k + 1];
                    double za = R * Math.Sin(ta), zb = R * Math.Sin(tb);
                    // A ponta do arco a um fio do nível vai até ele (o arco se estende 1e-5 mm).
                    double lo = SnapToBand(Math.Max(z0, Math.Min(za, zb)), z0, z1);
                    double hi = SnapToBand(Math.Min(z1, Math.Max(za, zb)), z0, z1);
                    if (hi - lo < 1e-6) continue;
                    // Ângulos do pedaço onde a altura é lo e hi (o pedaço é monótono).
                    double t1 = AngleAtHeight(ta, tb, lo / R), t2 = AngleAtHeight(ta, tb, hi / R);
                    if (t1 > t2) { double x = t1; t1 = t2; t2 = x; }
                    double uMid = R * Math.Cos((t1 + t2) / 2);

                    // A borda da faixa mais perto do arco, na reta da secção.
                    double uB = borders[0];
                    foreach (double b in borders) if (Math.Abs(b - uMid) < Math.Abs(uB - uMid)) uB = b;

                    foreach (double[][] poly in StepPolygons(R, t1, t2, uB, z => SnapToBand(z, z0, z1)))
                    {
                        var c = Region(label, origin, u, v, L);
                        c.BandStep = true;
                        c.Edges.Add(new ProfileEdge { Kind = "arco", Start = poly[0], Mid = poly[1], End = poly[2] });
                        List<double[]> chain = Distinct(poly, 2);
                        for (int e = 0; e < chain.Count; e++)
                            c.Edges.Add(new ProfileEdge { Kind = "reta", Start = chain[e], End = e + 1 < chain.Count ? chain[e + 1] : poly[0] });
                        AreaCentroidU(c, out double area, out _);
                        if (area < MinStepAreaMm2 || area * L < MinStepMm3) continue;
                        c.VolumeMm3 = area * L;
                        yield return c;
                    }
                }
            }
        }

        /// <summary>
        /// Os vértices de poly a partir de first (que fica), sem os que coincidem (a menos de
        /// 0,1 µm) com o anterior — nem os do fim que coincidem com poly[0], que fecha o laço. A borda da faixa passando
        /// exatamente pela ponta do arco (o R20 das divisórias, extrusão em Y) dava uma reta de
        /// comprimento zero e a SE recusava o perfil (End = −103).
        /// </summary>
        private static List<double[]> Distinct(double[][] poly, int first)
        {
            const double tol = 1e-4;
            var kept = new List<double[]> { poly[first] };
            for (int k = first + 1; k < poly.Length; k++)
                if (!Near(poly[k], kept[kept.Count - 1], tol)) kept.Add(poly[k]);
            while (kept.Count > 1 && Near(kept[kept.Count - 1], poly[0], tol)) kept.RemoveAt(kept.Count - 1);
            return kept;
        }

        private static bool Near(double[] a, double[] b, double tol)
        {
            return Math.Abs(a[0] - b[0]) < tol && Math.Abs(a[1] - b[1]) < tol;
        }

        /// <summary>Ângulo em [ta, tb] (pedaço monótono) em que sen(t) = sine.</summary>
        private static double AngleAtHeight(double ta, double tb, double sine)
        {
            sine = Math.Max(-1, Math.Min(1, sine));
            double b = Math.Asin(sine);
            foreach (double c in new[] { b, Math.PI - b })
                for (int turn = -2; turn <= 2; turn++)
                {
                    double t = c + 2 * Math.PI * turn;
                    if (t >= Math.Min(ta, tb) - 1e-4 && t <= Math.Max(ta, tb) + 1e-4) return t;
                }
            return Math.Abs(Math.Sin(ta) - sine) < Math.Abs(Math.Sin(tb) - sine) ? ta : tb;
        }

        /// <summary>
        /// Polígonos do degrau: [início do arco, meio, fim do arco, demais vértices...], em (u, z)
        /// relativos ao eixo. Um só se o arco não cruza u = uB; dois (um de cada lado) se cruza.
        /// </summary>
        private static IEnumerable<double[][]> StepPolygons(double R, double t1, double t2, double uB, Func<double, double> snap)
        {
            Func<double, double[]> at = t => new[] { R * Math.Cos(t), R * Math.Sin(t) };
            double cross = double.NaN;
            if (Math.Abs(uB) < R)
            {
                double b = Math.Acos(uB / R);
                foreach (double c in new[] { b, -b })
                    for (int turn = -2; turn <= 2 && double.IsNaN(cross); turn++)
                    {
                        double t = c + 2 * Math.PI * turn;
                        if (t > t1 + 1e-9 && t < t2 - 1e-9) cross = t;
                    }
            }
            double[] p1 = at(t1), p2 = at(t2);
            if (double.IsNaN(cross))
            {
                yield return new[] { p1, at((t1 + t2) / 2), p2, new[] { uB, snap(p2[1]) }, new[] { uB, snap(p1[1]) } };
                yield break;
            }
            double[] px = at(cross);
            yield return new[] { p1, at((t1 + cross) / 2), px, new[] { uB, snap(p1[1]) } };
            yield return new[] { px, at((cross + t2) / 2), p2, new[] { uB, snap(p2[1]) } };
        }

        /// <summary>u (ao longo de U, a partir do eixo) onde os contornos da faixa cruzam a reta
        /// horizontal da secção do cilindro.</summary>
        private static List<double> BorderCrossings(SectionBand band, double[] center, double[] u)
        {
            var result = new List<double>();
            foreach (SectionLoop loop in band.Loops)
            {
                List<double[]> poly = Polygonize(loop);
                for (int k = 0; k < poly.Count; k++)
                {
                    double[] p = poly[k], q = poly[(k + 1) % poly.Count];
                    // p + s (q − p) = center + t u, no plano XY.
                    double dx = q[0] - p[0], dy = q[1] - p[1];
                    double den = dx * u[1] - dy * u[0];
                    if (Math.Abs(den) < 1e-12) continue;
                    double rx = center[0] - p[0], ry = center[1] - p[1];
                    double sPar = (rx * u[1] - ry * u[0]) / den;
                    if (sPar < 0 || sPar >= 1) continue;
                    result.Add((rx * dy - ry * dx) / den);
                }
            }
            return result;
        }

        /// <summary>
        /// O degrau inteiro já está dentro de correções aceitas do mesmo sinal (o canto ou o
        /// segmento do próprio arco): feature repetida, não entra. Grade densa no meio da
        /// profundidade, não os 5 pontos de teste — no R20 das divisórias os 5 caíam dentro do
        /// canto e o triângulo de fora dele (o que o degrau existe para tirar) ficava.
        /// </summary>
        /// <summary>
        /// Volume (mm³) do degrau que NENHUMA correção aceita do mesmo sinal cobre (o canto ou o
        /// segmento do próprio arco, um degrau vizinho). Grade densa no meio da profundidade, não
        /// os 5 pontos de teste: no R20 das divisórias os 5 caíam dentro do canto e o triângulo
        /// de fora dele — o que o degrau existe para tirar — ficava.
        /// </summary>
        private static double UncoveredMm3(CorrectionRegion c, List<CorrectionRegion> accepted)
        {
            List<double[]> poly = Polygon(c);
            double minU = double.MaxValue, minV = double.MaxValue, maxU = double.MinValue, maxV = double.MinValue;
            foreach (double[] q in poly)
            {
                minU = Math.Min(minU, q[0]); maxU = Math.Max(maxU, q[0]);
                minV = Math.Min(minV, q[1]); maxV = Math.Max(maxV, q[1]);
            }
            double[] lo, hi;
            Box(c, poly, out lo, out hi);
            var others = new List<Tuple<CorrectionRegion, List<double[]>>>();
            foreach (CorrectionRegion r in accepted)
            {
                if (r.Add != c.Add || r.Kind != "extrusao") continue;
                List<double[]> rp = Polygon(r);
                double[] rlo, rhi;
                Box(r, rp, out rlo, out rhi);
                bool apart = false;
                for (int k = 0; k < 3; k++) if (rhi[k] < lo[k] || rlo[k] > hi[k]) apart = true;
                if (!apart) others.Add(Tuple.Create(r, rp));
            }

            const int N = 40;
            double cell = (maxU - minU) / N * (maxV - minV) / N;
            int open = 0;
            for (int i = 1; i < N; i++)
                for (int j = 1; j < N; j++)
                {
                    double[] q = { minU + (maxU - minU) * i / N, minV + (maxV - minV) * j / N };
                    if (Math.Abs(WindingXY(poly, q[0], q[1])) < 0.5 || DistanceToPolygon(poly, q) < SampleMarginMm) continue;
                    double[] p = c.ToWorld(q[0], q[1], 0.5 * c.DepthMm);
                    bool inside = false;
                    foreach (var o in others)
                    {
                        CorrectionRegion r = o.Item1;
                        double[] d = { p[0] - r.Origin[0], p[1] - r.Origin[1], p[2] - r.Origin[2] };
                        double w = Dot(d, r.W);
                        if (w <= 0 || w >= r.DepthMm) continue;
                        if (Math.Abs(WindingXY(o.Item2, Dot(d, r.U), Dot(d, r.V))) > 0.5) { inside = true; break; }
                    }
                    if (!inside) open++;
                }
            return open * cell * c.DepthMm;
        }

        /// <summary>Caixa no mundo de uma região extrudada (polígono nas duas faces).</summary>
        private static void Box(CorrectionRegion c, List<double[]> poly, out double[] lo, out double[] hi)
        {
            lo = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            hi = new[] { double.MinValue, double.MinValue, double.MinValue };
            foreach (double[] q in poly)
                foreach (double w in new[] { 0.0, c.DepthMm })
                {
                    double[] p = c.ToWorld(q[0], q[1], w);
                    for (int k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], p[k]); hi[k] = Math.Max(hi[k], p[k]); }
                }
        }

        /// <summary>
        /// Os degraus de <see cref="BandStepRegions"/> num plano inclinado: na secção (s, z), a
        /// reta do plano e, em cada faixa, a vertical da borda. Os dois triângulos de
        /// <see cref="InclinedPlaneRegions"/> são as metades do retângulo da face inteira; com a
        /// borda da faixa no meio deles, os 5 pontos de teste (os mais fundos, perto do canto)
        /// caíam todos do lado que o prismático já acerta — "igual" — e a rampa de 45° da fileira
        /// de cima das divisórias do tool-organizer_v2 ficou em degrau (2026-10-11).
        /// </summary>
        private static IEnumerable<CorrectionRegion> InclinedPlaneSteps(RecognizedSurface s, double[] pointsMm,
            RecognitionResult r, int index, PrismaticPlan prismatic, ExtrusionFrame f)
        {
            double[] n = s.Normal;
            double[] z = { 0, 0, 1 };
            double[] d = Normalize(CorrectionRegion.Cross(n, z));
            double[] q = CorrectionRegion.Cross(z, d);
            double wMin = double.MaxValue, wMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
            for (int t = 0; t < r.RegionOfTriangle.Length; t++)
            {
                if (r.RegionOfTriangle[t] != index) continue;
                for (int k = 0; k < 3; k++)
                {
                    int b = t * 9 + k * 3;
                    double[] p = { pointsMm[b], pointsMm[b + 1], pointsMm[b + 2] };
                    double w = Dot(p, d);
                    wMin = Math.Min(wMin, w); wMax = Math.Max(wMax, w);
                    zMin = Math.Min(zMin, p[2]); zMax = Math.Max(zMax, p[2]);
                }
            }
            wMin = SnapToPlane(wMin, d, r);
            wMax = SnapToPlane(wMax, d, r);
            if (wMax - wMin < 1e-6 || zMax - zMin < 1e-6) yield break;
            double c = Dot(n, s.PointMm), nq = Dot(n, q);
            if (Math.Abs(nq) < 1e-12) yield break;
            Func<double, double> sAt = zz => (c - n[2] * zz) / nq;

            double fromVertical = Math.Asin(Math.Min(1, Math.Abs(n[2]))) * 180 / Math.PI;
            string label = $"face inclinada {F(fromVertical)}° {(f.IsIdentity ? "da vertical" : "da direção " + f.AxisName)}, normal {P(n, f)}, degrau da faixa";
            double[] origin = Scale(d, wMin);
            double[] mid = Scale(d, (wMin + wMax) / 2); // a reta da secção passa aqui, ao longo de q

            foreach (SectionBand band in prismatic.Bands)
            {
                double lo = SnapToBand(Math.Max(band.Z0, zMin), band.Z0, band.Z1);
                double hi = SnapToBand(Math.Min(band.Z1, zMax), band.Z0, band.Z1);
                if (hi - lo < 1e-6) continue;
                List<double> borders = BorderCrossings(band, mid, q);
                if (borders.Count == 0) continue;
                double sMid = sAt((lo + hi) / 2);
                double uB = borders[0];
                foreach (double b in borders) if (Math.Abs(b - sMid) < Math.Abs(uB - sMid)) uB = b;

                double[] p1 = { sAt(lo), lo }, p2 = { sAt(hi), hi };
                var polys = new List<double[][]>();
                double zx = Math.Abs(n[2]) < 1e-12 ? double.NaN : (c - nq * uB) / n[2];
                if (!double.IsNaN(zx) && zx > lo + 1e-6 && zx < hi - 1e-6)
                {
                    double[] x = { uB, zx };
                    polys.Add(new[] { p1, x, new[] { uB, lo } });
                    polys.Add(new[] { x, p2, new[] { uB, hi } });
                }
                else polys.Add(new[] { p1, p2, new[] { uB, hi }, new[] { uB, lo } });

                foreach (double[][] poly in polys)
                {
                    var reg = Region(label, origin, q, z, wMax - wMin);
                    reg.BandStep = true;
                    List<double[]> ring = Distinct(poly, 0);
                    if (ring.Count < 3) continue;
                    AddPolygon(reg, ring.ToArray());
                    AreaCentroidU(reg, out double area, out _);
                    // Menor que o mínimo não descoberto: nem vale testar contra a malha.
                    if (area < MinStepAreaMm2 || area * (wMax - wMin) < MinStepMm3) continue;
                    reg.VolumeMm3 = area * (wMax - wMin);
                    yield return reg;
                }
            }
        }

        // ------------------------------------------------------------------ plano inclinado

        private static IEnumerable<CorrectionRegion> InclinedPlaneRegions(RecognizedSurface s, double[] pointsMm,
            RecognitionResult r, int index, ExtrusionFrame f)
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
            wMin = SnapToPlane(wMin, d, r);
            wMax = SnapToPlane(wMax, d, r);
            if (wMax - wMin < 1e-6 || zMax - zMin < 1e-6) yield break;

            // A linha passa EXATA pelo plano reconhecido: s sai do plano, não do vértice.
            double c = Dot(n, s.PointMm);
            double nq = Dot(n, q);
            if (Math.Abs(nq) < 1e-12) yield break;
            double s0 = (c - n[2] * zMin) / nq, s1 = (c - n[2] * zMax) / nq;

            double fromVertical = Math.Asin(Math.Min(1, Math.Abs(n[2]))) * 180 / Math.PI;
            string label = $"face inclinada {F(fromVertical)}° {(f.IsIdentity ? "da vertical" : "da direção " + f.AxisName)}, normal {P(n, f)}";
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
            return Decide(c, p => MeshSolid.Contains(pointsMm, p), prismatic);
        }

        private static string Decide(CorrectionRegion c, Func<double[], bool> insideMesh, PrismaticPlan prismatic)
        {
            List<double[]> samples = Samples(c);
            if (samples.Count == 0) return "região fina demais para testar contra a malha (menos de 2 × " + SampleMarginMm.ToString(CultureInfo.InvariantCulture) + " mm)";

            int inside = 0;
            foreach (double[] p in samples) if (insideMesh(p)) inside++;
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

        private static string Describe(RecognizedSurface s, ExtrusionFrame f)
        {
            return s.Kind + " " + P(s.PointMm, f);
        }

        private static string AxisLetter(double[] local, ExtrusionFrame f)
        {
            double[] a = f.ToWorld(local);
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
        private static string P(double[] local, ExtrusionFrame f) => P(f.ToWorld(local));
    }
}
