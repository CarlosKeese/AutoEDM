using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Electrode
{
    /// <summary>Uma reta de corte em planta: <c>Axis = 'X'</c> é o plano x = At; <c>'Y'</c> é o plano y = At.</summary>
    public struct SplitLine
    {
        public char Axis;
        public double At;
        /// <summary>
        /// De que lado da reta fica o enchimento do raio (o material do eletrodo entre o raio e a quina das paredes):
        /// +1 no lado de coordenada maior, −1 no de menor, 0 se não se sabe.
        /// </summary>
        public int CoveSide;

        public SplitLine(char axis, double at, int coveSide = 0) { Axis = axis; At = at; CoveSide = coveSide; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} = {1:0.###}", Axis == 'X' ? "x" : "y", At);
    }

    /// <summary>
    /// Um raio que a fresa não faz no ELETRODO, já em planta (mm, referencial da montagem), e as retas de corte que o
    /// resolvem. Vem do raio CONVEXO da peça (o eletrodo é o negativo dela) ou do raio CÔNCAVO do eletrodo construído.
    /// Lógica pura.
    /// </summary>
    public sealed class SplitFinding
    {
        public int FaceId;
        /// <summary>"pé de degrau" (eixo horizontal), "canto" (eixo vertical), "raio de canto" (toro de eixo vertical)…</summary>
        public string Kind;
        public double RadiusMm;
        public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        /// <summary>As retas que resolvem o achado (uma serve). Vazia: sem corte (ver <see cref="NoCut"/>).</summary>
        public readonly List<SplitLine> Options = new List<SplitLine>();
        /// <summary>
        /// T2c: paredes tangentes de um achado SEM corte (toro de canto de rebaixo) — não dividem, mas o pedaço que fica
        /// inteiro além de uma delas recebe a ponta do enchimento e a tira pela caixa da face.
        /// </summary>
        public readonly List<SplitLine> SideLines = new List<SplitLine>();
        public string NoCut;
        /// <summary>O eixo lido, para o log.</summary>
        public string Axis;
        /// <summary>A reta escolhida pelo plano (null: não resolvido).</summary>
        public SplitLine? Chosen;
        /// <summary>Por que ficou sem corte no plano (preenchido pelo <see cref="ElectrodeSplitter"/>).</summary>
        public string Unresolved;
        /// <summary>
        /// T2c/C3: o eixo foge de Z (ou de X/Y) mais que 0,5° e só ganhou corte pela
        /// <see cref="SplitOptions.AlignToleranceDeg"/>. O enchimento dele atravessa a divisa: sai nos DOIS pedaços.
        /// </summary>
        public bool Tilted;
        /// <summary>Quanto o eixo foge da direção alinhada (graus).</summary>
        public double TiltDeg;

        /// <summary>
        /// Quanto o eixo pode fugir de Z (ou de X/Y) e ainda contar como alinhado: 1 − cos(0,5°). Um canto com saída
        /// de 2° (medido no 15428.100) não tem parede vertical: o corte vertical não passa por ela, então fica sem corte
        /// (a menos que a tolerância de alinhamento, desligada por padrão, diga o contrário).
        /// </summary>
        private const double AxisTol = 4e-5;
        private const double SinTol = 0.0087;   // sen(0,5°)

        /// <summary>
        /// Monta o achado a partir do eixo (vetor unitário), do ponto do eixo, do raio (no toro, o MENOR) e da caixa da face,
        /// tudo no referencial da montagem (mm). As retas de corte são as PAREDES tangentes ao raio: o plano vertical da
        /// parede que o raio concorda. Cortar ali deixa a parede inteira num pedaço; o outro pedaço recebe o raio encostado
        /// na divisa, entre a divisa e a outra face (que então pode ser removido, ficando uma quina viva EXTERNA).
        /// Cortar pelo EIXO não serve: o raio inteiro fica num pedaço só, ainda côncavo (medido no SE, ver o plano).
        /// </summary>
        /// <param name="alignTolDeg">T2c/C3 (decisão do Carlos, desligada por padrão): com valor &gt; 0, um eixo que foge de
        /// Z ou de X/Y até esse ângulo é tratado como alinhado. No canto de eixo quase vertical o corte passa pela parede
        /// no TOPO da face (o mais perto do plano de fechamento); no pé de degrau de eixo quase horizontal, pelo meio da face.</param>
        public static SplitFinding From(int faceId, bool torus, double radiusMm, double majorRadiusMm,
            double[] axisDir, double[] axisPointMm, double[] faceMinMm, double[] faceMaxMm, double alignTolDeg = 0.0)
        {
            var f = new SplitFinding
            {
                FaceId = faceId, RadiusMm = radiusMm,
                MinX = faceMinMm[0], MinY = faceMinMm[1], MinZ = faceMinMm[2],
                MaxX = faceMaxMm[0], MaxY = faceMaxMm[1], MaxZ = faceMaxMm[2]
            };
            if (axisDir == null || axisPointMm == null) { f.Kind = torus ? "raio de canto" : "canto"; f.NoCut = "eixo ilegível"; return f; }
            double ax = axisDir[0], ay = axisDir[1], az = axisDir[2];
            double px = axisPointMm[0], py = axisPointMm[1];
            f.Axis = string.Format(CultureInfo.InvariantCulture, "eixo ({0:0.###}, {1:0.###}, {2:0.###}) por ({3:0.###}, {4:0.###}){5}",
                ax, ay, az, px, py, torus ? string.Format(CultureInfo.InvariantCulture, ", Rm {0:0.###}", majorRadiusMm) : "");
            double tolRad = Math.Max(0.0, alignTolDeg) * Math.PI / 180.0;
            double cosAligned = alignTolDeg > 0 ? Math.Min(1 - AxisTol, Math.Cos(tolRad)) : 1 - AxisTol;
            double sinLevel = alignTolDeg > 0 ? Math.Max(SinTol, Math.Sin(tolRad)) : SinTol;

            if (Math.Abs(az) > cosAligned)
            {
                // Eixo vertical: em planta é um arco de centro (px, py). Cilindro → raio R; toro → o arco de fora (Rm + r).
                // Só o QUARTO de arco alinhado a X e Y tem as duas paredes tangentes em x = cte e y = cte: a caixa da face
                // encosta no eixo de um lado e vai até o raio de fora do outro, igual em X e em Y.
                f.Kind = torus ? "raio de canto" : "canto";
                f.TiltDeg = Math.Acos(Math.Min(1.0, Math.Abs(az))) * 180.0 / Math.PI;
                double bx0 = f.MinX, bx1 = f.MaxX, by0 = f.MinY, by1 = f.MaxY;
                if (Math.Abs(az) <= 1 - AxisTol)
                {
                    // C3: eixo inclinado (saída). O arco anda em planta conforme desce; vale o do TOPO da face, e a caixa
                    // da face perde o quanto o arco andou até o fundo dela.
                    f.Tilted = true;
                    double kx = ax / az, ky = ay / az;
                    double pz = axisPointMm.Length > 2 ? axisPointMm[2] : f.MaxZ;
                    px += (f.MaxZ - pz) * kx;
                    py += (f.MaxZ - pz) * ky;
                    double mx = -(f.MaxZ - f.MinZ) * kx, my = -(f.MaxZ - f.MinZ) * ky;   // do topo ao fundo
                    if (mx > 0) bx1 -= mx; else bx0 -= mx;
                    if (my > 0) by1 -= my; else by0 -= my;
                }
                double tol = Math.Max(0.03, 0.15 * radiusMm);
                bool xFromAxis = Math.Abs(bx0 - px) <= tol || Math.Abs(bx1 - px) <= tol;
                bool yFromAxis = Math.Abs(by0 - py) <= tol || Math.Abs(by1 - py) <= tol;
                double dx = Math.Max(bx1 - px, px - bx0), dy = Math.Max(by1 - py, py - by0);
                if (!xFromAxis || !yFromAxis || Math.Abs(dx - dy) > tol)
                {
                    f.NoCut = "canto que não é um quarto de arco alinhado a X e Y (paredes inclinadas ou ângulo ≠ 90°)";
                    return f;
                }
                // A parede é onde a face fica VERTICAL. No cilindro de eixo vertical, o próprio raio. No toro, a face vai
                // de Rm (tangente horizontal, no alto ou embaixo do tubo) até Rm ± r (tangente vertical): se a caixa vai
                // até Rm + r, a parede está ali; se vai só até Rm, a face é o quarto de dentro e a parede está em Rm − r
                // (medido no 15428.100: canto de um rebaixo, Rm 1,297, r 0,302, parede em 0,995 do eixo).
                double reach = (dx + dy) / 2.0;
                bool towardCenter = true;   // o enchimento fica do lado do eixo (cilindro e toro "de fora")
                if (torus)
                {
                    bool outer = Math.Abs(reach - (majorRadiusMm + radiusMm)) <= Math.Abs(reach - majorRadiusMm);
                    reach = outer ? majorRadiusMm + radiusMm : Math.Abs(majorRadiusMm - radiusMm);
                    towardCenter = outer;   // toro "de dentro" (canto de rebaixo): a face fica além da parede
                }
                else if (Math.Abs(reach - radiusMm) <= tol) reach = radiusMm;
                // Parede tangente: do lado em que a caixa da face vai mais longe do centro.
                int sx = (bx1 - px) >= (px - bx0) ? 1 : -1, sy = (by1 - py) >= (py - by0) ? 1 : -1;
                int k = towardCenter ? -1 : 1;
                var lx = new SplitLine('X', px + sx * reach, k * sx);
                var ly = new SplitLine('Y', py + sy * reach, k * sy);
                if (!towardCenter)
                {
                    // T2c (medido no 15428.100, 2026-11-13): o toro "de dentro" é o arredondamento da boca de um rebaixo
                    // que CONTORNA o canto dele. As duas paredes tangentes só separam as pontas do arco; o meio fica no
                    // pedaço do canto (entre as duas retas), e lá a caixa da face pegaria o cobre do próprio rebaixo.
                    // Sem corte; as pontas que caem além de uma das retas saem pela caixa (SideLines). Sem elas, as pontas
                    // ficavam em 5 dos 8 pedaços (medido no 15428.100, 2026-11-13).
                    f.SideLines.Add(lx);
                    f.SideLines.Add(ly);
                    f.NoCut = "arredondamento que contorna o canto de um rebaixo: corte reto não o separa";
                    return f;
                }
                f.Options.Add(lx);
                f.Options.Add(ly);
                return f;
            }
            if (Math.Abs(az) < sinLevel && !torus)
            {
                f.Kind = "pé de degrau";
                double hl = Math.Sqrt(ax * ax + ay * ay);
                double cosPlan = alignTolDeg > 0 ? Math.Min(1 - AxisTol, Math.Cos(tolRad)) : 1 - AxisTol;
                bool alongX = Math.Abs(ax) > 1 - AxisTol || (alignTolDeg > 0 && Math.Abs(ax) / hl > cosPlan);
                bool alongY = !alongX && (Math.Abs(ay) > 1 - AxisTol || (alignTolDeg > 0 && Math.Abs(ay) / hl > cosPlan));
                // O enchimento fica entre o eixo e a parede: do lado do eixo.
                if (alongX)
                {
                    f.Tilted = Math.Abs(ax) <= 1 - AxisTol;
                    f.TiltDeg = Math.Acos(Math.Min(1.0, Math.Abs(ax))) * 180.0 / Math.PI;
                    // Eixo que gira em planta: a reta do eixo no MEIO da face.
                    if (f.Tilted) py += ((f.MinX + f.MaxX) / 2.0 - px) * ay / ax;
                    int s = (f.MaxY - py) >= (py - f.MinY) ? 1 : -1;
                    f.Options.Add(new SplitLine('Y', py + s * radiusMm, -s));
                }
                else if (alongY)
                {
                    f.Tilted = Math.Abs(ay) <= 1 - AxisTol;
                    f.TiltDeg = Math.Acos(Math.Min(1.0, Math.Abs(ay))) * 180.0 / Math.PI;
                    if (f.Tilted) px += ((f.MinY + f.MaxY) / 2.0 - py) * ax / ay;
                    int s = (f.MaxX - px) >= (px - f.MinX) ? 1 : -1;
                    f.Options.Add(new SplitLine('X', px + s * radiusMm, -s));
                }
                else f.NoCut = "eixo horizontal fora de X e de Y";
                return f;
            }
            f.Kind = torus ? "raio de canto" : "canto";
            f.NoCut = "eixo inclinado";
            return f;
        }

        public string Describe()
        {
            string where = string.Format(CultureInfo.InvariantCulture, "{0} R{1:0.###} em x {2:0.##}…{3:0.##}, y {4:0.##}…{5:0.##}, z {6:0.##}…{7:0.##}",
                Kind, RadiusMm, MinX, MaxX, MinY, MaxY, MinZ, MaxZ);
            if (FaceId != 0) where = $"face {FaceId}: " + where;
            if (Axis != null) where += " [" + Axis + "]";
            if (Tilted) where += string.Format(CultureInfo.InvariantCulture, " (eixo {0:0.0}° fora, aceito pela tolerância)", TiltDeg);
            if (Chosen.HasValue) return where + " → corte " + Chosen.Value;
            return where + " → sem corte (" + (Unresolved ?? NoCut ?? "?") + ")";
        }
    }

    public sealed class SplitOptions
    {
        /// <summary>Menor largura de um pedaço (mm). Decisão do Carlos a confirmar: padrão 2.</summary>
        public double MinPieceMm = 2.0;
        /// <summary>Máximo de pedaços por eletrodo. Decisão do Carlos a confirmar: padrão 12.</summary>
        public int MaxPieces = 12;
        /// <summary>Cortes a menos disto um do outro viram um corte só.</summary>
        public double CollinearTolMm = 0.05;
        /// <summary>
        /// Quanto o pedaço que recebe o enchimento de um raio passa da divisa, para o lado da parede (mm; negativo =
        /// para antes dela). Com a divisa EXATAMENTE na parede, o enchimento de um R0,1 encosta nela em tangência e a
        /// booleana do Solid Edge dá E_NOINTERFACE (medido no 15428.100, 2026-11-13); passando 0,0005 mm ela vinga.
        /// Passando para FORA, o pedaço fica com uma lâmina dessa espessura encostada na parede, que a T2c tira
        /// (<see cref="ElectrodeSplitter.FillerBoxes"/> devolve a faixa dela).
        /// </summary>
        public double CoveOverlapMm = 0.001;
        /// <summary>
        /// T2c/C3 — decisão do Carlos, DESLIGADA por padrão (0): quanto o eixo de um raio pode fugir de Z ou de X/Y
        /// (graus) e ainda ser tratado como alinhado. Ver <see cref="SplitFinding.From"/>.
        /// </summary>
        public double AlignToleranceDeg = 5.0;
        /// <summary>
        /// T2c/C1: folga (mm) da caixa do enchimento para fora da face do raio, na 2ª tentativa (a 1ª é rente à face).
        /// Rente, a caixa encosta em faces que ficam; com folga ela tira uma lâmina dessa espessura do cobre vizinho.
        /// </summary>
        public double FillerClearanceMm = 0.001;
        /// <summary>T2c/C1: a faixa que tira a lâmina da sobreposição começa este tanto ANTES da divisa (mm).</summary>
        public double StripInsetMm = 0.001;
    }

    /// <summary>Como o "Criar Eletrodos" trata a divisão (argumento <c>dividir</c>).</summary>
    public enum SplitMode
    {
        /// <summary>Sem o argumento (padrão, T2c/C2): divide só se TODOS os pedaços saírem fresáveis.</summary>
        Auto,
        /// <summary><c>"dividir": true</c>: divide sempre que houver corte.</summary>
        Force,
        /// <summary><c>"dividir": false</c>: nunca divide.</summary>
        Off
    }

    /// <summary>
    /// Uma caixa a tirar de um pedaço (mm, referencial da montagem): o ENCHIMENTO de um raio cortado pela divisa (o cobre
    /// entre a divisa e a face do raio; o resto da caixa da face é aço), ou a FAIXA da sobreposição na divisa.
    /// </summary>
    public sealed class FillerCut
    {
        public BoundingBox Box;
        /// <summary>O achado cujo enchimento sai; null na faixa da sobreposição.</summary>
        public SplitFinding Finding;
        public string What;
    }

    /// <summary>T2c/C2: dividir só quando resolve. Lógica pura.</summary>
    public static class SplitDecision
    {
        /// <summary>
        /// Pelo PLANO (previsão pela peça): aplicar a divisão? Em <see cref="SplitMode.Auto"/> só se ela divide e nenhum
        /// achado fica sem corte (o enchimento dos cortados sai pela caixa da face). <paramref name="note"/> diz por quê não.
        /// </summary>
        public static bool ApplyPlan(SplitPlan plan, SplitMode mode, out string note)
        {
            note = null;
            if (mode == SplitMode.Off || plan == null) return false;
            if (!plan.IsSplit)
            {
                note = plan.Abandoned != null
                    ? "a divisão não resolve: " + plan.Abandoned
                    : $"a divisão não resolve: {plan.Unresolved.Count} raio(s) sem corte possível";
                return false;
            }
            if (mode == SplitMode.Force) return true;
            if (plan.Unresolved.Count == 0 && plan.Pieces.All(p => p.Unresolved.Count == 0)) return true;
            note = $"a divisão não resolve: {plan.Unresolved.Count} raio(s) sem corte possível";
            return false;
        }

        /// <summary>
        /// Depois de construir (a conferência no eletrodo construído manda): ficar com os pedaços? Em
        /// <see cref="SplitMode.Auto"/> só se todos foram construídos e nenhum tem achado.
        /// <paramref name="remaining"/>: achados medidos em cada pedaço, ou −1 se o pedaço falhou.
        /// </summary>
        public static bool KeepPieces(SplitMode mode, IList<int> remaining, out string note)
        {
            note = null;
            if (mode != SplitMode.Auto) return true;
            int failed = remaining.Count(n => n < 0), bad = remaining.Count(n => n > 0);
            if (failed == 0 && bad == 0) return true;
            note = failed > 0
                ? $"a divisão não resolve: {failed} pedaço(s) não saíram construídos"
                : $"a divisão não resolve: {bad} pedaço(s) construídos ainda têm {remaining.Where(n => n > 0).Sum()} raio(s) que a fresa não faz";
            return false;
        }
    }

    /// <summary>Uma reta de corte escolhida e os achados que ela resolve.</summary>
    public sealed class SplitCut
    {
        public SplitLine Line;
        public readonly List<SplitFinding> Resolves = new List<SplitFinding>();
    }

    /// <summary>Um pedaço: pegada retangular (mm, montagem) e o que está na divisa dele.</summary>
    public sealed class SplitPiece
    {
        public BoundingBox Footprint;
        /// <summary>A pegada do PRISMA: a <see cref="Footprint"/> com as divisas em que o pedaço recebe o enchimento de
        /// um raio empurradas <see cref="SplitOptions.CoveOverlapMm"/> para fora.</summary>
        public BoundingBox BuildFootprint;
        /// <summary>Achados resolvidos por uma divisa deste pedaço.</summary>
        public readonly List<SplitFinding> OnBorder = new List<SplitFinding>();
        /// <summary>Achados sem corte que caem dentro do pedaço: ele não sai fresável.</summary>
        public readonly List<SplitFinding> Unresolved = new List<SplitFinding>();
    }

    public sealed class SplitPlan
    {
        public readonly List<SplitCut> Cuts = new List<SplitCut>();
        public readonly List<SplitPiece> Pieces = new List<SplitPiece>();
        public readonly List<SplitFinding> Unresolved = new List<SplitFinding>();
        /// <summary>Células sem queima descartadas.</summary>
        public int DroppedCells;
        /// <summary>Divisão abandonada (o eletrodo fica inteiro): o motivo.</summary>
        public string Abandoned;
        public bool IsSplit => Pieces.Count > 1;

        public string Describe()
        {
            var sb = new StringBuilder();
            if (Abandoned != null) sb.Append("divisão abandonada: ").Append(Abandoned).Append(". ");
            sb.Append($"{Cuts.Count} corte(s)");
            foreach (SplitCut c in Cuts) sb.Append($" [{c.Line}: {c.Resolves.Count} achado(s)]");
            sb.Append($" → {Pieces.Count} pedaço(s)");
            if (DroppedCells > 0) sb.Append($", {DroppedCells} célula(s) sem queima descartada(s)");
            if (Unresolved.Count > 0) sb.Append($"; {Unresolved.Count} achado(s) sem corte");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Divide a pegada de um eletrodo que a fresa não faz (regra do Carlos de 2026-11-12: melhor dividir, mesmo que a
    /// erosão deixe canto vivo). Lógica PURA, sobre retângulos em planta:
    /// <list type="number">
    /// <item>cada achado propõe as retas das paredes tangentes a ele; retas a até <see cref="SplitOptions.CollinearTolMm"/>
    /// viram uma só, e a escolha cobre todos os achados com o menor número de retas;</item>
    /// <item>as retas partem a pegada numa grade;</item>
    /// <item>células vizinhas se reúnem enquanto o retângulo reunido não tiver achado no INTERIOR (na divisa está
    /// resolvido) e couber numa barra;</item>
    /// <item>pedaço sem nenhuma face de queima sai.</item>
    /// </list>
    /// </summary>
    public static class ElectrodeSplitter
    {
        private const double Eps = 1e-6;

        public static SplitPlan Plan(BoundingBox footprint, IList<SplitFinding> findings, IList<BoundingBox> burnBoxes,
            Func<BoundingBox, bool> fitsBar, SplitOptions o = null)
        {
            o = o ?? new SplitOptions();
            fitsBar = fitsBar ?? (_ => true);
            var plan = new SplitPlan();
            foreach (SplitFinding f in findings) { f.Chosen = null; f.Unresolved = null; }

            // 1) retas válidas (deixam ao menos MinPiece até a borda da pegada) e agrupamento das colineares
            var clusters = new List<Cluster>();
            foreach (SplitFinding f in findings)
            {
                if (f.Options.Count == 0) { f.Unresolved = f.NoCut ?? "sem reta de corte"; continue; }
                bool any = false;
                foreach (SplitLine l in f.Options)
                {
                    double lo = l.Axis == 'X' ? footprint.MinX : footprint.MinY, hi = l.Axis == 'X' ? footprint.MaxX : footprint.MaxY;
                    if (l.At < lo + o.MinPieceMm - Eps || l.At > hi - o.MinPieceMm + Eps) continue;
                    any = true;
                    Cluster c = clusters.FirstOrDefault(k => k.Axis == l.Axis && Math.Abs(k.At - l.At) <= o.CollinearTolMm + Eps);
                    if (c == null) clusters.Add(c = new Cluster { Axis = l.Axis, At = l.At });
                    if (!c.Members.Contains(f)) { c.Members.Add(f); c.Values.Add(l.At); }
                }
                if (!any)
                    f.Unresolved = string.Format(CultureInfo.InvariantCulture,
                        "o corte deixaria um pedaço com menos de {0:0.##} mm (largura mínima)", o.MinPieceMm);
            }

            // 2) cobertura gulosa: a reta que resolve mais achados ainda abertos primeiro; reta a menos de MinPiece de
            //    outra paralela já escolhida deixaria um pedaço estreito demais — fica de fora.
            var open = new HashSet<SplitFinding>(findings.Where(f => f.Unresolved == null));
            var chosen = new List<Cluster>();
            while (open.Count > 0)
            {
                Cluster best = clusters
                    .Where(c => !chosen.Contains(c) && c.Members.Any(open.Contains))
                    .Where(c => !chosen.Any(k => k.Axis == c.Axis && Math.Abs(k.At - c.At) < o.MinPieceMm - Eps))
                    .OrderByDescending(c => c.Members.Count(open.Contains))
                    .ThenBy(c => c.Axis).ThenBy(c => c.At)
                    .FirstOrDefault();
                if (best == null) break;
                best.At = Median(best.Values);
                chosen.Add(best);
                var cut = new SplitCut { Line = new SplitLine(best.Axis, best.At) };
                foreach (SplitFinding f in best.Members.Where(open.Contains).ToList())
                {
                    SplitLine mine = f.Options.First(l => l.Axis == best.Axis && Math.Abs(l.At - best.At) <= o.CollinearTolMm + Eps);
                    f.Chosen = new SplitLine(best.Axis, best.At, mine.CoveSide);
                    cut.Resolves.Add(f);
                    open.Remove(f);
                }
                plan.Cuts.Add(cut);
            }
            foreach (SplitFinding f in open)
                f.Unresolved = string.Format(CultureInfo.InvariantCulture, "o corte ficaria a menos de {0:0.##} mm de outro", o.MinPieceMm);
            plan.Unresolved.AddRange(findings.Where(f => f.Chosen == null));

            if (plan.Cuts.Count == 0)
            {
                plan.Pieces.Add(Piece(footprint, findings));
                return plan;
            }

            // 3) grade
            List<double> xs = Grid(footprint.MinX, footprint.MaxX, plan.Cuts.Where(c => c.Line.Axis == 'X').Select(c => c.Line.At));
            List<double> ys = Grid(footprint.MinY, footprint.MaxY, plan.Cuts.Where(c => c.Line.Axis == 'Y').Select(c => c.Line.At));
            var rects = new List<Rect>();
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    var r = new Rect { I0 = i, I1 = i + 1, J0 = j, J1 = j + 1 };
                    r.Burn = burnBoxes.Any(b => Overlap(b.MinX, b.MaxX, xs[i], xs[i + 1]) && Overlap(b.MinY, b.MaxY, ys[j], ys[j + 1]));
                    rects.Add(r);
                }

            // 4) reunião: o maior retângulo válido primeiro, até não haver mais par que dê retângulo
            BoundingBox Box(Rect r) => new BoundingBox { MinX = xs[r.I0], MaxX = xs[r.I1], MinY = ys[r.J0], MaxY = ys[r.J1], MinZ = footprint.MinZ, MaxZ = footprint.MaxZ };
            while (true)
            {
                Rect a = null, b = null; double bestArea = -1;
                for (int p = 0; p < rects.Count; p++)
                    for (int q = p + 1; q < rects.Count; q++)
                    {
                        Rect u = Join(rects[p], rects[q]);
                        if (u == null) continue;
                        BoundingBox ub = Box(u);
                        double area = (ub.MaxX - ub.MinX) * (ub.MaxY - ub.MinY);
                        if (area <= bestArea + Eps) continue;
                        if (HasInteriorFinding(ub, findings, o.CollinearTolMm)) continue;
                        if (!fitsBar(ub)) continue;
                        a = rects[p]; b = rects[q]; bestArea = area;
                    }
                if (a == null) break;
                Rect m = Join(a, b);
                m.Burn = a.Burn || b.Burn;
                rects.Remove(a); rects.Remove(b); rects.Add(m);
            }

            plan.DroppedCells = rects.Count(r => !r.Burn);
            foreach (Rect r in rects.Where(r => r.Burn).OrderBy(r => r.J0).ThenBy(r => r.I0))
                plan.Pieces.Add(Piece(Box(r), findings, o.CoveOverlapMm));

            if (plan.Pieces.Count > o.MaxPieces)
            {
                plan.Abandoned = $"daria {plan.Pieces.Count} pedaços (máximo {o.MaxPieces})";
                plan.Pieces.Clear();
                plan.Pieces.Add(Piece(footprint, findings));
            }
            return plan;
        }

        /// <summary>O achado cai no INTERIOR do retângulo: a reta dele atravessa o retângulo onde o achado está.</summary>
        public static bool HasInteriorFinding(BoundingBox r, IEnumerable<SplitFinding> findings, double tol)
        {
            foreach (SplitFinding f in findings)
            {
                if (!f.Chosen.HasValue) continue;
                SplitLine l = f.Chosen.Value;
                if (l.Axis == 'X')
                {
                    if (l.At > r.MinX + tol && l.At < r.MaxX - tol && Overlap(f.MinY, f.MaxY, r.MinY, r.MaxY)) return true;
                }
                else if (l.At > r.MinY + tol && l.At < r.MaxY - tol && Overlap(f.MinX, f.MaxX, r.MinX, r.MaxX)) return true;
            }
            return false;
        }

        private static SplitPiece Piece(BoundingBox fp, IEnumerable<SplitFinding> findings, double coveOverlapMm = 0.0)
        {
            var p = new SplitPiece { Footprint = fp, BuildFootprint = fp };
            foreach (SplitFinding f in findings)
            {
                bool touches = Overlap(f.MinX, f.MaxX, fp.MinX - 0.01, fp.MaxX + 0.01) && Overlap(f.MinY, f.MaxY, fp.MinY - 0.01, fp.MaxY + 0.01);
                if (!touches) continue;
                if (f.Chosen.HasValue)
                {
                    SplitLine l = f.Chosen.Value;
                    double lo = l.Axis == 'X' ? fp.MinX : fp.MinY, hi = l.Axis == 'X' ? fp.MaxX : fp.MaxY;
                    bool atLo = Math.Abs(l.At - lo) <= 0.06, atHi = Math.Abs(l.At - hi) <= 0.06;
                    if (atLo || atHi) p.OnBorder.Add(f);
                    // O enchimento do raio está DENTRO deste pedaço: a divisa passa um pouco para o lado da parede
                    // (ou, com sobreposição negativa, para antes dela).
                    if (atLo && l.CoveSide > 0)
                    {
                        if (l.Axis == 'X') p.BuildFootprint.MinX = fp.MinX - coveOverlapMm;
                        else p.BuildFootprint.MinY = fp.MinY - coveOverlapMm;
                    }
                    if (atHi && l.CoveSide < 0)
                    {
                        if (l.Axis == 'X') p.BuildFootprint.MaxX = fp.MaxX + coveOverlapMm;
                        else p.BuildFootprint.MaxY = fp.MaxY + coveOverlapMm;
                    }
                }
                else
                {
                    double cx = (f.MinX + f.MaxX) / 2, cy = (f.MinY + f.MaxY) / 2;
                    if (cx >= fp.MinX && cx <= fp.MaxX && cy >= fp.MinY && cy <= fp.MaxY) p.Unresolved.Add(f);
                }
            }
            return p;
        }

        /// <summary>
        /// T2c/C1 — as caixas a tirar de um pedaço construído (mm, referencial da montagem). A regra do Carlos deixa a
        /// aresta do aço sair VIVA: o enchimento de um raio cortado (o cobre entre a divisa e a face do raio) cabe inteiro
        /// na caixa da face, e o resto dessa caixa é aço. Por isso, no pedaço que ficou com o enchimento:
        /// <list type="bullet">
        /// <item>a caixa da face, recortada ao pedaço: do lado da divisa ela passa <c>1 mm</c> para fora do pedaço (onde
        /// não há nada), assim como nas bordas que a face atravessa e no fechamento (Z) que ela alcança; nas outras faces
        /// ela fica a <paramref name="clearanceMm"/> da face do raio (0 = rente);</item>
        /// <item>achado <see cref="SplitFinding.Tilted"/> (C3): o enchimento atravessa a divisa, então a caixa sai nos dois
        /// pedaços vizinhos, cada um com a sua parte;</item>
        /// <item>com <see cref="SplitOptions.CoveOverlapMm"/> &gt; 0 o pedaço tem uma lâmina além da divisa: uma faixa que
        /// começa <see cref="SplitOptions.StripInsetMm"/> antes da divisa e vai até fora do pedaço, na altura toda.</item>
        /// </list>
        /// <paramref name="bottomZ"/> e <paramref name="closingZ"/>: fundo do prisma e plano de fechamento (mm, montagem).
        /// </summary>
        public static List<FillerCut> FillerBoxes(SplitPiece piece, IEnumerable<SplitFinding> findings, double bottomZ, double closingZ,
            double clearanceMm, SplitOptions o = null)
        {
            o = o ?? new SplitOptions();
            const double Out = 1.0, Reach = 0.01;
            var cuts = new List<FillerCut>();
            if (piece == null) return cuts;
            BoundingBox fp = piece.Footprint, bf = piece.BuildFootprint;
            foreach (SplitFinding f in findings ?? piece.OnBorder)
            {
                // a face tem de cair dentro do pedaço (em planta)
                if (Math.Min(f.MaxX, bf.MaxX) - Math.Max(f.MinX, bf.MinX) <= 1e-3) continue;
                if (Math.Min(f.MaxY, bf.MaxY) - Math.Max(f.MinY, bf.MinY) <= 1e-3) continue;
                IEnumerable<SplitLine> lines = f.Chosen.HasValue ? new[] { f.Chosen.Value } : (IEnumerable<SplitLine>)f.SideLines;
                foreach (SplitLine l in lines)
                {
                    // O enchimento fica do lado CoveSide da reta: só o pedaço inteiro desse lado o recebe (o do outro lado
                    // fica com a parede). Achado inclinado (C3): o enchimento atravessa a divisa, sai dos dois lados.
                    double lo = l.Axis == 'X' ? fp.MinX : fp.MinY, hi = l.Axis == 'X' ? fp.MaxX : fp.MaxY;
                    bool lowSide = hi <= l.At + 0.06, highSide = lo >= l.At - 0.06;
                    bool cove = (lowSide && l.CoveSide < 0) || (highSide && l.CoveSide > 0);
                    // A reta escolhida é a mediana das colineares (até 0,05 mm): se a parede DESTE achado ficou a mais de
                    // 1 µm dela, uma lasca do enchimento cai do outro lado também (medido no 15428.100 com 3°: x = −9,762
                    // contra a parede em −9,783).
                    double own = f.Options.Where(k => k.Axis == l.Axis).Select(k => k.At).DefaultIfEmpty(l.At)
                        .OrderBy(a => Math.Abs(a - l.At)).First();
                    bool straddles = f.Tilted || Math.Abs(own - l.At) > 1e-3;
                    if (!cove && !(straddles && (lowSide || highSide))) continue;

                    // A caixa da face, recortada ao pedaço: onde a face chega à borda do pedaço (a divisa, ou uma borda que
                    // ela atravessa), a caixa passa para fora dele; no resto, fica a clearanceMm da face.
                    double x0 = f.MinX <= bf.MinX + Reach ? bf.MinX - Out : f.MinX - clearanceMm;
                    double x1 = f.MaxX >= bf.MaxX - Reach ? bf.MaxX + Out : f.MaxX + clearanceMm;
                    double y0 = f.MinY <= bf.MinY + Reach ? bf.MinY - Out : f.MinY - clearanceMm;
                    double y1 = f.MaxY >= bf.MaxY - Reach ? bf.MaxY + Out : f.MaxY + clearanceMm;
                    double z0 = f.MinZ <= bottomZ + 1e-3 ? bottomZ - Out : f.MinZ - clearanceMm;
                    double z1 = f.MaxZ >= closingZ - 0.01 ? closingZ + Out : f.MaxZ + clearanceMm;
                    cuts.Add(new FillerCut
                    {
                        Box = new BoundingBox { MinX = x0, MaxX = x1, MinY = y0, MaxY = y1, MinZ = z0, MaxZ = z1 },
                        Finding = f,
                        What = string.Format(CultureInfo.InvariantCulture, "enchimento da face {0} ({1} R{2:0.###}, {3} {4}{5})",
                            f.FaceId, f.Kind, f.RadiusMm, f.Chosen.HasValue ? "divisa" : "ponta além de", l, cove ? "" : ", lado de lá")
                    });
                    break;   // um enchimento por face em cada pedaço
                }
            }

            if (o.CoveOverlapMm > 0)
            {
                double s = o.StripInsetMm, z0 = bottomZ - Out, z1 = closingZ + Out;
                if (bf.MaxX > fp.MaxX + 1e-9) cuts.Add(Strip(fp.MaxX - s, bf.MaxX + Out, bf.MinY - Out, bf.MaxY + Out, z0, z1, "x = " + Fmt(fp.MaxX)));
                if (bf.MinX < fp.MinX - 1e-9) cuts.Add(Strip(bf.MinX - Out, fp.MinX + s, bf.MinY - Out, bf.MaxY + Out, z0, z1, "x = " + Fmt(fp.MinX)));
                if (bf.MaxY > fp.MaxY + 1e-9) cuts.Add(Strip(bf.MinX - Out, bf.MaxX + Out, fp.MaxY - s, bf.MaxY + Out, z0, z1, "y = " + Fmt(fp.MaxY)));
                if (bf.MinY < fp.MinY - 1e-9) cuts.Add(Strip(bf.MinX - Out, bf.MaxX + Out, bf.MinY - Out, fp.MinY + s, z0, z1, "y = " + Fmt(fp.MinY)));
            }
            return cuts;
        }

        private static FillerCut Strip(double x0, double x1, double y0, double y1, double z0, double z1, string where) =>
            new FillerCut { Box = new BoundingBox { MinX = x0, MaxX = x1, MinY = y0, MaxY = y1, MinZ = z0, MaxZ = z1 }, What = "lâmina da sobreposição na divisa " + where };

        private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Dois intervalos se sobrepõem com área (encostar não conta).</summary>
        private static bool Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 1e-3;

        private static List<double> Grid(double lo, double hi, IEnumerable<double> cuts)
        {
            var g = new List<double> { lo };
            g.AddRange(cuts.OrderBy(v => v));
            g.Add(hi);
            return g;
        }

        private static double Median(List<double> v)
        {
            var s = v.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0;
        }

        /// <summary>A união de dois retângulos da grade, se ela for um retângulo (lado inteiro em comum); senão null.</summary>
        private static Rect Join(Rect a, Rect b)
        {
            if (a.J0 == b.J0 && a.J1 == b.J1 && (a.I1 == b.I0 || b.I1 == a.I0))
                return new Rect { I0 = Math.Min(a.I0, b.I0), I1 = Math.Max(a.I1, b.I1), J0 = a.J0, J1 = a.J1 };
            if (a.I0 == b.I0 && a.I1 == b.I1 && (a.J1 == b.J0 || b.J1 == a.J0))
                return new Rect { I0 = a.I0, I1 = a.I1, J0 = Math.Min(a.J0, b.J0), J1 = Math.Max(a.J1, b.J1) };
            return null;
        }

        private sealed class Rect { public int I0, I1, J0, J1; public bool Burn; }

        private sealed class Cluster
        {
            public char Axis;
            public double At;
            public readonly List<SplitFinding> Members = new List<SplitFinding>();
            public readonly List<double> Values = new List<double>();
        }
    }
}
