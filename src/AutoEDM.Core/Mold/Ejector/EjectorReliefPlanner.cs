using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Mold.Ejector
{
    public enum HoleSurfaceKind { Cylinder, Cone, Torus, Plane }

    /// <summary>
    /// Uma face do corpo como o planejador a vê (mm). Cilindro/cone/toro: um ponto do eixo e a
    /// direção dele; plano: um ponto e a normal (em <see cref="AxisDir"/>). Só caixas e eixos —
    /// o leitor COM (<see cref="EjectorHoleReader"/>) preenche; os testes montam à mão.
    /// </summary>
    public sealed class HoleSurface
    {
        public int Id;
        public HoleSurfaceKind Kind;
        public double[] AxisPointMm;
        public double[] AxisDir;
        public double RadiusMm;
        public double[] MinMm, MaxMm;
        /// <summary>Caixas (min, max) das arestas — só dos cilindros; dão a ponta mais curta do furo.</summary>
        public List<double[][]> EdgeBoxesMm = new List<double[][]>();
        /// <summary>Face de recurso de rosca (Furo roscado ou Rosca) — vem da árvore, não da geometria.</summary>
        public bool Threaded;
        /// <summary>A face COM (para o realce). O planejador não toca.</summary>
        public object Com;
    }

    /// <summary>A face plana selecionada: perpendicular a X, Y ou Z.</summary>
    public sealed class ReliefFace
    {
        public int Axis;                 // 0 = X, 1 = Y, 2 = Z
        public double PlaneMm;           // coordenada da face no eixo
        public double[] MinMm, MaxMm;    // caixa da face
        /// <summary>Para que lado do eixo fica o material (±1; 0 = não sei). Só desempata.</summary>
        public int IntoSignHint;
    }

    /// <summary>
    /// Convenção da guia do extrator (Carlos, 2026-11-09: "respeitando uma faixa para deslizamento
    /// do extrator conforme norma ou convenção"). As normas dos pinos (DIN ISO 8694 / 6751) fixam o
    /// pino, não o furo; a prática dos catálogos (HASCO, DME, Polimold) e da literatura de moldes é:
    /// guia ajustada (H7) de 1,5 a 3 × Ø, nunca menos que ~10 mm nem mais que ~20 mm (mais que isso
    /// só aumenta atrito e risco de engripar), e o resto do furo ALIVIADO com +0,5 mm no Ø para pinos
    /// finos e +1,0 mm para os grossos. Os números ficam editáveis na janela.
    /// </summary>
    public sealed class EjectorReliefOptions
    {
        public double GuideFactor = 2.0;          // guia = fator × Ø …
        public double GuideMinMm = 10.0;          // … limitada a este mínimo …
        public double GuideMaxMm = 20.0;          // … e a este máximo
        public double SmallClearanceMm = 0.5;     // folga no Ø até ClearanceLimitMm (inclusive)
        public double LargeClearanceMm = 1.0;     // folga no Ø acima dele
        public double ClearanceLimitMm = 6.0;
        public double MinReliefDepthMm = 1.0;     // alívio mais raso que isto não vale o furo

        public double GuideLengthMm(double pinMm)
        {
            double g = Math.Max(GuideMinMm, Math.Min(GuideMaxMm, GuideFactor * pinMm));
            return Math.Ceiling(g * 2.0 - 1e-9) / 2.0;                        // para cima, em 0,5 mm
        }

        public double ReliefDiameterMm(double pinMm)
        {
            double c = pinMm <= ClearanceLimitMm + 1e-9 ? SmallClearanceMm : LargeClearanceMm;
            return Math.Ceiling((pinMm + c) * 10.0 - 1e-6) / 10.0;            // para cima, em 0,1 mm
        }
    }

    public sealed class EjectorHole
    {
        public string Label;
        public double[] CenterMm;              // centro da boca, NA face selecionada
        public double DiameterMm;              // Ø do furo (nominal, 0,01)
        public double LengthMm;                // da face até a ponta MAIS CURTA do furo
        public double GuideMm;
        public double ReliefDiameterMm;
        public double ReliefDepthMm;
        /// <summary>null = a regra aprova; texto = por que a regra reprova.</summary>
        public string SkipReason;
        /// <summary>Sobra alívio depois da guia (≥ mínimo) — sem isso nem forçando sai.</summary>
        public bool CanRelieve;
        /// <summary>Escolha do usuário na tabela: null = segue a regra; true = alivia (até reprovado); false = não.</summary>
        public bool? UserChoice;
        public bool Relieve => CanRelieve && (UserChoice ?? SkipReason == null);
        /// <summary>Aliviado CONTRA a regra (o usuário marcou um reprovado).</summary>
        public bool Forced => Relieve && SkipReason != null;
        public List<object> Faces = new List<object>();
        /// <summary>As faces coaxiais com trecho, vão e profundidade — diagnóstico para o log.</summary>
        public string Detail;

        /// <summary>Identidade estável entre recálculos: o centro na face, a 0,1 mm.</summary>
        public string Key => string.Format(CultureInfo.InvariantCulture, "{0:0.0};{1:0.0};{2:0.0}", CenterMm[0], CenterMm[1], CenterMm[2]);
    }

    /// <summary>Furos com o MESMO alívio (Ø e profundidade): saem num recurso de furo só.</summary>
    public sealed class ReliefGroup
    {
        public double DiameterMm, DepthMm;
        public List<EjectorHole> Holes = new List<EjectorHole>();
        public string Labels => string.Join(", ", Holes.Select(h => h.Label));
    }

    public sealed class EjectorReliefPlan
    {
        public int Axis;
        public int IntoSign;                    // material em +eixo (1) ou −eixo (−1) a partir da face
        public double PlaneMm;
        public List<EjectorHole> Holes = new List<EjectorHole>();
        public List<string> Warnings = new List<string>();
        public IEnumerable<EjectorHole> ToRelieve => Holes.Where(h => h.Relieve);

        /// <summary>
        /// Os alívios agrupados por Ø e profundidade (Carlos, 2026-11-09: "furos com o mesmo diâmetro
        /// e comprimento agrupar na mesma feature"), na ordem dos rótulos.
        /// </summary>
        public List<ReliefGroup> Groups()
        {
            var list = new List<ReliefGroup>();
            foreach (EjectorHole h in ToRelieve)
            {
                ReliefGroup g = list.FirstOrDefault(x => Math.Abs(x.DiameterMm - h.ReliefDiameterMm) < 0.005 && Math.Abs(x.DepthMm - h.ReliefDepthMm) < 0.005);
                if (g == null) { g = new ReliefGroup { DiameterMm = h.ReliefDiameterMm, DepthMm = h.ReliefDepthMm }; list.Add(g); }
                g.Holes.Add(h);
            }
            return list;
        }

        public double[] IntoDir
        {
            get { var d = new double[3]; d[Axis] = IntoSign; return d; }
        }
    }

    /// <summary>
    /// Acha os furos que ABREM na face selecionada e planeja o alívio de cada um (Carlos,
    /// 2026-11-09). A face escolhida é a de TRÁS da placa/postiço (lado da placa extratora): o
    /// alívio entra por ela e a guia fica na outra ponta, junto à moldagem — medida a partir da
    /// ponta MAIS CURTA do furo (a saída pode ser inclinada ou curva).
    ///
    /// Ficam de fora, pela geometria (peça importada não tem recurso) e pela árvore (rosca):
    /// furo roscado; furo com dois Ø (rebaixo de cabeça, escalonado — ou já aliviado, o que torna o
    /// botão seguro de rodar de novo); cone (chanfro, escareado, ponta de broca); toro (raio na
    /// boca); fundo plano (cego); furo incompleto (meia-cana); e o furo curto demais para ter
    /// alívio depois da guia.
    /// </summary>
    public static class EjectorReliefPlanner
    {
        private const double Tol = 0.05;         // mm: "encosta na face", "mesma ponta"
        private const double CenterTol = 0.02;   // mm: coaxial
        private const double RadiusTol = 0.01;   // mm: mesmo Ø
        /// <summary>Cone que alarga na saída até isto no raio é chanfro (fora); mais que isto é sede cônica do extrator.</summary>
        private const double ExitChamferMaxMm = 1.0;

        private sealed class Group
        {
            public double C1, C2;
            public readonly List<HoleSurface> Members = new List<HoleSurface>();
            public double SMin = double.MaxValue, SMax = double.MinValue;   // coordenada COM sinal da face
        }

        /// <param name="choices">Escolhas do usuário na tabela, por <see cref="EjectorHole.Key"/> (true = aliviar, false = não).</param>
        public static EjectorReliefPlan Plan(ReliefFace face, IList<HoleSurface> surfaces, EjectorReliefOptions opt,
            IDictionary<string, bool> choices = null)
        {
            opt = opt ?? new EjectorReliefOptions();
            int ax = face.Axis, r1 = (ax + 1) % 3, r2 = (ax + 2) % 3;
            var plan = new EjectorReliefPlan { Axis = ax, PlaneMm = face.PlaneMm };

            // 1) Superfícies de revolução com o eixo perpendicular à face, agrupadas por eixo.
            var groups = new List<Group>();
            foreach (HoleSurface s in surfaces)
            {
                if (s.Kind == HoleSurfaceKind.Plane || s.AxisDir == null || s.AxisPointMm == null || s.MinMm == null) continue;
                if (Math.Abs(s.AxisDir[ax]) < 0.9999) continue;
                double c1 = s.AxisPointMm[r1], c2 = s.AxisPointMm[r2];
                Group g = groups.FirstOrDefault(x => Math.Abs(x.C1 - c1) < CenterTol && Math.Abs(x.C2 - c2) < CenterTol);
                if (g == null) { g = new Group { C1 = c1, C2 = c2 }; groups.Add(g); }
                g.Members.Add(s);
                g.SMin = Math.Min(g.SMin, s.MinMm[ax] - face.PlaneMm);
                g.SMax = Math.Max(g.SMax, s.MaxMm[ax] - face.PlaneMm);
            }

            // 2) Só os que abrem NA face: encostam no plano e o eixo cai dentro da caixa da face.
            var open = groups.Where(g =>
                    g.Members.Any(m => m.Kind == HoleSurfaceKind.Cylinder) &&
                    (Math.Abs(g.SMin) < Tol || Math.Abs(g.SMax) < Tol) &&
                    g.C1 > face.MinMm[r1] - Tol && g.C1 < face.MaxMm[r1] + Tol &&
                    g.C2 > face.MinMm[r2] - Tol && g.C2 < face.MaxMm[r2] + Tol)
                .ToList();

            // 3) Lado do material: o furo só pode ir para dentro dele (ressalto vai para fora).
            int plus = open.Count(g => g.SMax > Tol && g.SMin > -Tol);
            int minus = open.Count(g => g.SMin < -Tol && g.SMax < Tol);
            plan.IntoSign = plus > minus ? 1 : minus > plus ? -1 : (face.IntoSignHint != 0 ? Math.Sign(face.IntoSignHint) : 1);
            int into = plan.IntoSign;

            foreach (Group g in open)
            {
                bool inside = into > 0 ? (g.SMax > Tol && g.SMin > -Tol) : (g.SMin < -Tol && g.SMax < Tol);
                if (!inside) continue;
                plan.Holes.Add(Classify(face, g, surfaces, opt, into, r1, r2));
            }

            // Rótulos pela posição: primeiro pelo 1º eixo da face, depois pelo 2º.
            plan.Holes = plan.Holes.OrderBy(h => Math.Round(h.CenterMm[r1], 1)).ThenBy(h => Math.Round(h.CenterMm[r2], 1)).ToList();
            for (int i = 0; i < plan.Holes.Count; i++) plan.Holes[i].Label = "E" + (i + 1);
            if (choices != null)
                foreach (EjectorHole h in plan.Holes)
                    if (choices.TryGetValue(h.Key, out bool c)) h.UserChoice = c;

            // Alívios que se cruzam: só aviso — quem decide é o projetista.
            var ok = plan.ToRelieve.ToList();
            for (int i = 0; i < ok.Count; i++)
                for (int j = i + 1; j < ok.Count; j++)
                {
                    double d = Math.Sqrt(Sq(ok[i].CenterMm[r1] - ok[j].CenterMm[r1]) + Sq(ok[i].CenterMm[r2] - ok[j].CenterMm[r2]));
                    double need = (ok[i].ReliefDiameterMm + ok[j].ReliefDiameterMm) / 2.0;
                    if (d < need + 0.5)
                        plan.Warnings.Add(string.Format(Pt, "{0} e {1}: os alívios ficam a {2:0.##} mm entre centros (parede de {3:0.##} mm).",
                            ok[i].Label, ok[j].Label, d, d - need));
                }
            return plan;
        }

        private static EjectorHole Classify(ReliefFace face, Group g, IList<HoleSurface> surfaces, EjectorReliefOptions opt, int into, int r1, int r2)
        {
            int ax = face.Axis;
            var center = new double[3];
            center[ax] = face.PlaneMm; center[r1] = g.C1; center[r2] = g.C2;
            var h = new EjectorHole { CenterMm = center };

            Func<double, double> A = v => (v - face.PlaneMm) * into;        // profundidade a partir da face
            double Lo(HoleSurface m) => Math.Min(A(m.MinMm[ax]), A(m.MaxMm[ax]));
            double Hi(HoleSurface m) => Math.Max(A(m.MinMm[ax]), A(m.MaxMm[ax]));

            // O grupo coaxial em TRÊS trechos (MD-15428, 2026-11-09: raio da superfície da moldagem em
            // volta da saída, coaxial ao furo, tirava o extrator como "raio na borda"):
            //   FURO  = os cilindros de MENOR Ø — onde o extrator desliza;
            //   BOCA  = o que vem antes dele, desde a face de trás: chanfro, raio ou Ø maior ali = fora;
            //   SAÍDA = o que vem depois: só conta o que FECHA o furo (ponta de broca, fundo plano,
            //           fundo arredondado). Raio, cone ou Ø maior da moldagem em volta da saída: ignorados.
            List<HoleSurface> allCyl = g.Members.Where(m => m.Kind == HoleSurfaceKind.Cylinder).ToList();
            double rMin = allCyl.Where(m => m.RadiusMm > 0).Select(m => m.RadiusMm).DefaultIfEmpty(0).Min();
            List<HoleSurface> cyl = rMin > 0 ? allCyl.Where(m => Math.Abs(m.RadiusMm - rMin) < RadiusTol).ToList() : allCyl;
            double r = rMin > 0 ? rMin : cyl.Max(m => Math.Max(m.MaxMm[r1] - m.MinMm[r1], m.MaxMm[r2] - m.MinMm[r2])) / 2.0;
            double boreLo = cyl.Min(Lo), length = cyl.Max(Hi);
            List<HoleSurface> mouth = g.Members.Where(m => !cyl.Contains(m) && Hi(m) <= boreLo + Tol).ToList();
            List<HoleSurface> exit = g.Members.Where(m => !cyl.Contains(m) && Lo(m) >= length - Tol && Lo(m) <= length + Tol).ToList();

            // Cadeia CONTÍNUA de faces a partir do fim do furo. Tem um cone que ALARGA além de um
            // chanfro? Então é a SEDE cônica do extrator, e o toro encostado no furo é só a
            // concordância dela (MD-15428, 2026-11-09: E29/E30 = toro de 0,3 mm + cone Ø5→Ø9,4 /
            // Ø8→Ø16; o Ø14 dos cantos = toro até a face, sem cone → raio na borda).
            var chain = new List<HoleSurface>(exit);
            double chainHi = exit.Count == 0 ? length : exit.Max(Hi);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (HoleSurface m in g.Members)
                    if (!cyl.Contains(m) && !chain.Contains(m) && Lo(m) >= length - Tol && Lo(m) <= chainHi + Tol)
                    { chain.Add(m); chainHi = Math.Max(chainHi, Hi(m)); grew = true; }
            }
            double rr = cyl.Count > 0 && cyl[0].RadiusMm > 0 ? cyl[0].RadiusMm : 0;
            bool seat = chain.Any(m => m.Kind == HoleSurfaceKind.Cone &&
                (Math.Max(m.MaxMm[r1] - m.MinMm[r1], m.MaxMm[r2] - m.MinMm[r2]) - 2 * rr) / 2.0 > ExitChamferMaxMm);

            h.Faces.Clear();
            h.Faces.AddRange(mouth.Concat(cyl).Where(m => m.Com != null).Select(m => m.Com));
            // Diagnóstico (vai para o log quando a regra reprova): cada face coaxial, com o trecho a que
            // pertence, o vão (Ø da caixa) e a profundidade de início…fim a partir da face clicada.
            h.Detail = string.Join("; ", g.Members.OrderBy(Lo).Select(m => string.Format(Pt, "{0}{1} vão {2:0.##} prof {3:0.##}…{4:0.##}",
                cyl.Contains(m) ? "FURO " : mouth.Contains(m) ? "BOCA " : exit.Contains(m) ? "SAÍDA " : "",
                m.Kind == HoleSurfaceKind.Cylinder ? "cilindro Ø" + (2 * m.RadiusMm).ToString("0.##", Pt)
                    : m.Kind == HoleSurfaceKind.Cone ? "cone" : m.Kind == HoleSurfaceKind.Torus ? "toro" : "plano",
                Math.Max(m.MaxMm[r1] - m.MinMm[r1], m.MaxMm[r2] - m.MinMm[r2]), Lo(m), Hi(m))));
            h.DiameterMm = Nominal(2.0 * r);
            h.LengthMm = Math.Round(length, 2);

            // Ponta mais curta: o menor começo das arestas que NÃO encostam na face (as retas da
            // costura e da divisão em metades vão da face até a ponta e ficam de fora).
            double shortest = length;
            foreach (HoleSurface m in cyl)
                foreach (double[][] e in m.EdgeBoxesMm)
                {
                    double lo = Math.Min(A(e[0][ax]), A(e[1][ax]));
                    if (lo > Tol) shortest = Math.Min(shortest, lo);
                }
            h.LengthMm = Math.Round(shortest, 2);

            // As medidas saem SEMPRE, também para o reprovado: o Carlos pode forçar o alívio nele
            // (2026-11-09: "habilitar a possibilidade de criar os alívios em furos reprovados").
            h.GuideMm = opt.GuideLengthMm(h.DiameterMm);
            h.ReliefDiameterMm = opt.ReliefDiameterMm(h.DiameterMm);
            h.ReliefDepthMm = Math.Round(shortest - h.GuideMm, 2);
            h.CanRelieve = h.ReliefDepthMm >= opt.MinReliefDepthMm;
            h.SkipReason = RejectReason(face, g, surfaces, cyl, mouth, exit, seat, r, length, boreLo, A, r1, r2);
            if (!h.CanRelieve)
            {
                if (h.SkipReason == null)
                    h.SkipReason = string.Format(Pt, "curto: {0:0.##} mm de furo para {1:0.#} mm de guia", h.LengthMm, h.GuideMm);
                h.ReliefDepthMm = 0;
            }
            return h;
        }

        /// <summary>Por que a regra reprova o furo; null = aprovado.</summary>
        private static string RejectReason(ReliefFace face, Group g, IList<HoleSurface> surfaces, List<HoleSurface> cyl,
            List<HoleSurface> mouth, List<HoleSurface> exit, bool seat, double r, double length, double boreLo, Func<double, double> A, int r1, int r2)
        {
            int ax = face.Axis;
            if (cyl.Concat(mouth).Any(m => m.Threaded)) return "roscado";

            List<HoleSurface> bigger = mouth.Where(m => m.Kind == HoleSurfaceKind.Cylinder).ToList();
            if (bigger.Count > 0)
            {
                var radii = new List<double> { r };
                foreach (HoleSurface m in bigger)
                    if (m.RadiusMm > 0 && !radii.Any(x => Math.Abs(x - m.RadiusMm) < RadiusTol)) radii.Add(m.RadiusMm);
                return "dois Ø (" + string.Join(" e ", radii.OrderBy(x => x).Select(x => "Ø" + (2 * x).ToString("0.##", Pt))) +
                       ") — rebaixo de cabeça, escalonado ou já aliviado";
            }
            if (mouth.Any(m => m.Kind == HoleSurfaceKind.Cone)) return "chanfro ou escareado na boca";
            if (mouth.Any(m => m.Kind == HoleSurfaceKind.Torus)) return "raio (arredondamento) na boca";

            // SAÍDA: só o que ENCOSTA no fim do furo (MD-15428, 2026-11-09, 3º run). O que vem depois
            // disso (o raio da moldagem além da sede cônica dos extratores Ø5/Ø8 em X = 32) é ignorado.
            //   Ø maior encostado  → rebaixo de cabeça do outro lado (parafuso Ø9 + Ø15)  → fora
            //   raio encostado     → raio na borda da saída (Ø14 dos cantos)              → fora
            //   cone que estreita  → ponta de broca;  cone que alarga até 1 mm → chanfro → fora
            //   cone que alarga mais que isso → sede cônica do extrator                    → alivia
            foreach (HoleSurface m in exit)
            {
                double span = Math.Max(m.MaxMm[r1] - m.MinMm[r1], m.MaxMm[r2] - m.MinMm[r2]);
                double grow = (span - 2 * r) / 2.0;
                switch (m.Kind)
                {
                    case HoleSurfaceKind.Cylinder:
                        if (m.RadiusMm > r + RadiusTol)
                            return string.Format(Pt, "dois Ø (Ø{0:0.##} e Ø{1:0.##} na saída) — rebaixo de cabeça do outro lado", 2 * r, 2 * m.RadiusMm);
                        break;
                    case HoleSurfaceKind.Torus:
                        if (grow <= Tol) return "fundo arredondado (furo cego)";
                        if (seat) break;                    // concordância da sede cônica
                        return "raio (arredondamento) na saída";
                    case HoleSurfaceKind.Cone:
                        if (grow <= Tol) return "ponta de broca (furo cego)";
                        if (grow <= ExitChamferMaxMm) return "chanfro na saída";
                        break;
                }
            }

            // Fundo plano: disco perpendicular, centrado no eixo, do tamanho do furo, na ponta dele.
            foreach (HoleSurface p in surfaces)
            {
                if (p.Kind != HoleSurfaceKind.Plane || p.AxisDir == null || p.MinMm == null || Math.Abs(p.AxisDir[ax]) < 0.9999) continue;
                if (Math.Abs(p.MaxMm[ax] - p.MinMm[ax]) > Tol) continue;
                double s1 = p.MaxMm[r1] - p.MinMm[r1], s2 = p.MaxMm[r2] - p.MinMm[r2];
                double m1 = (p.MaxMm[r1] + p.MinMm[r1]) / 2.0, m2 = (p.MaxMm[r2] + p.MinMm[r2]) / 2.0;
                if (s1 > 2 * r + Tol || s2 > 2 * r + Tol || Math.Abs(m1 - g.C1) > Tol || Math.Abs(m2 - g.C2) > Tol) continue;
                if (Math.Abs(A(p.MinMm[ax]) - length) < Tol) return "furo cego (fundo plano)";
            }

            // Furo inteiro (não meia-cana): as caixas dos pedaços cobrem o Ø nas duas direções.
            double u1 = cyl.Max(m => m.MaxMm[r1]) - cyl.Min(m => m.MinMm[r1]);
            double u2 = cyl.Max(m => m.MaxMm[r2]) - cyl.Min(m => m.MinMm[r2]);
            if (u1 < 2 * r - Tol || u2 < 2 * r - Tol) return "furo incompleto (aberto na lateral)";
            if (boreLo > Tol) return "não começa na face";
            return null;
        }

        /// <summary>Ø nominal: encaixa no décimo quando está a menos de 0,02 mm dele (modelo com H7 vira o nominal).</summary>
        private static double Nominal(double d)
        {
            double tenth = Math.Round(d * 10.0) / 10.0;
            return Math.Abs(d - tenth) < 0.02 ? tenth : Math.Round(d, 2);
        }

        public static string Describe(EjectorReliefPlan plan, EjectorReliefOptions opt)
        {
            var sb = new StringBuilder();
            string axis = "XYZ"[plan.Axis].ToString();
            sb.AppendLine(string.Format(Pt, "Face em {0} = {1:0.##} mm; material para {2}{0}. {3} furo(s) abrindo nela, {4} para aliviar.",
                axis, plan.PlaneMm, plan.IntoSign > 0 ? "+" : "−", plan.Holes.Count, plan.ToRelieve.Count()));
            sb.AppendLine(string.Format(Pt, "Guia = {0:0.##} × Ø, entre {1:0.#} e {2:0.#} mm; alívio Ø + {3:0.##} até Ø{4:0.#}, Ø + {5:0.##} acima.",
                opt.GuideFactor, opt.GuideMinMm, opt.GuideMaxMm, opt.SmallClearanceMm, opt.ClearanceLimitMm, opt.LargeClearanceMm));
            foreach (EjectorHole h in plan.Holes)
            {
                string where = string.Format(Pt, "({0:0.##}; {1:0.##}; {2:0.##})", h.CenterMm[0], h.CenterMm[1], h.CenterMm[2]);
                sb.AppendLine(h.Relieve
                    ? string.Format(Pt, "{0} Ø{1:0.##} {2}: comprimento {3:0.##}, guia {4:0.#} → alívio Ø{5:0.0} × {6:0.##} mm{7}",
                        h.Label, h.DiameterMm, where, h.LengthMm, h.GuideMm, h.ReliefDiameterMm, h.ReliefDepthMm,
                        h.Forced ? " — FORÇADO (" + h.SkipReason + ")" : "")
                    : string.Format(Pt, "{0} Ø{1:0.##} {2}: FORA — {3}", h.Label, h.DiameterMm, where,
                        h.UserChoice == false && h.SkipReason == null ? "desmarcado" : h.SkipReason));
                if (h.SkipReason != null && h.Detail != null) sb.AppendLine("    faces: " + h.Detail);
            }
            List<ReliefGroup> groups = plan.Groups();
            if (groups.Count > 0)
                sb.AppendLine(string.Format(Pt, "{0} recurso(s) de furo: {1}.", groups.Count,
                    string.Join("; ", groups.Select(g => string.Format(Pt, "Ø{0:0.0} × {1:0.##} ({2})", g.DiameterMm, g.DepthMm, g.Labels)))));
            foreach (string w in plan.Warnings) sb.AppendLine("AVISO: " + w);
            return sb.ToString();
        }

        private static double Sq(double v) => v * v;
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    }
}
