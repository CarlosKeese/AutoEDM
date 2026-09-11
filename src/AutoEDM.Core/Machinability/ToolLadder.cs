using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Machinability
{
    /// <summary>
    /// Forma da ponta da fresa. NÃO muda o alcance — muda o CANTO que ela produz, e é
    /// por isso que as duas famílias entram separadas na escada:
    /// <list type="bullet">
    /// <item>topo reto faz canto VIVO piso↔parede (R0), mas deixa o raio da fresa no canto
    /// vertical parede↔parede;</item>
    /// <item>esférica deixa o raio da fresa em TODA junção côncava, e em compensação
    /// acompanha superfície esculpida.</item>
    /// </list>
    /// </summary>
    public enum ToolShape
    {
        /// <summary>Topo reto (canto vivo no piso).</summary>
        Flat,

        /// <summary>Esférica (raio da fresa em toda junção côncava).</summary>
        Ball
    }

    /// <summary>Veredito da escada para um par (raio exigido, profundidade).</summary>
    public enum MachinabilityVerdict
    {
        /// <summary>Alguma fresa serve o raio E chega na profundidade.</summary>
        Millable,

        /// <summary>Existe fresa para o raio, mas nenhuma delas é longa o bastante.</summary>
        BeyondReach,

        /// <summary>O raio exigido é menor que o da menor fresa — nenhuma serve, em profundidade nenhuma.</summary>
        BelowMinimumRadius,

        /// <summary>
        /// FURO fundo demais para a fresa que o raio aceita. NÃO é EDM: furo se faz com BROCA, e
        /// broca não está nesta escada. Veredito separado porque o primeiro run ao vivo
        /// (2026-09-11) marcou um Ø8 × 25 mm como "só EDM" — e é um furo trivial. Cair nesse
        /// falso positivo é o que faz o usuário parar de abrir o relatório.
        /// </summary>
        HoleBeyondMillReach
    }

    /// <summary>Uma fresa da ferramentaria: diâmetro, comprimento ÚTIL de corte e forma da ponta.</summary>
    public sealed class MillingTool
    {
        public MillingTool(double diameterMm, double fluteLengthMm, ToolShape shape, string note = null)
        {
            if (diameterMm <= 0) throw new ArgumentOutOfRangeException(nameof(diameterMm));
            if (fluteLengthMm <= 0) throw new ArgumentOutOfRangeException(nameof(fluteLengthMm));
            DiameterMm = diameterMm;
            FluteLengthMm = fluteLengthMm;
            Shape = shape;
            Note = note;
        }

        public double DiameterMm { get; }

        /// <summary>Comprimento ÚTIL de corte (mm) — o quanto ela desce, não o comprimento total.</summary>
        public double FluteLengthMm { get; }

        public ToolShape Shape { get; }

        /// <summary>Observação do chão de fábrica (ex.: "longneck"). Entra no rótulo.</summary>
        public string Note { get; }

        /// <summary>Raio mínimo de canto que esta fresa consegue produzir (mm).</summary>
        public double RadiusMm
        {
            get { return DiameterMm / 2.0; }
        }

        /// <summary>Rótulo para o usuário (pt-BR, vírgula decimal): "Ø1,5 × 4 mm topo reto".</summary>
        public string Label
        {
            get
            {
                string family = Shape == ToolShape.Flat ? "topo reto" : "esférica";
                string note = string.IsNullOrEmpty(Note) ? "" : " " + Note;
                return "Ø" + DiameterMm.ToString("0.##") + " × " + FluteLengthMm.ToString("0.##") +
                       " mm " + family + note;
            }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// A ferramentaria disponível, e as três perguntas que ela responde: qual o menor raio que
    /// existe, quão fundo se chega num dado raio, e qual fresa usar.
    ///
    /// **O detalhe que decide tudo (2026-09-11):** o pedido original partia de "Ø1 × 3 mm", mas a
    /// lista real do Carlos tem <b>Ø1 × 10 mm longneck</b> — o limite a raio 0,5 mm é 10 mm de
    /// profundidade, não 3. Calibrar em 3 mm marcaria como EDM uma pilha de bolsões que ele fresa
    /// hoje, e falso positivo é o pior defeito desta análise: na terceira região condenada que o
    /// usuário sabe usinar, ele para de abrir o relatório.
    ///
    /// **Por que a escada e não uma fresa só:** o comprimento útil é a profundidade de corte
    /// DAQUELA fresa, não a profundidade total da cavidade. Um bolsão de 10 mm cujas paredes só
    /// apertam nos últimos 3 mm é perfeitamente usinável — quem abre o grosso é uma fresa maior.
    /// Um ponto é usinável se QUALQUER fresa da escada o alcança.
    ///
    /// Lógica PURA (sem COM), testada em <c>ToolLadderTests</c>.
    /// </summary>
    public sealed class ToolLadder
    {
        /// <summary>
        /// Tolerância (mm) das comparações de raio e comprimento. Um canto modelado em R0,5
        /// exato TEM de aceitar a fresa de R0,5 — é justamente o raio que ela produz —, então a
        /// comparação é inclusiva com folga para o ruído de ponto flutuante do CAD.
        /// </summary>
        private const double ToleranceMm = 1e-6;

        private readonly List<MillingTool> _tools;

        public ToolLadder(IEnumerable<MillingTool> tools)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));
            _tools = tools.Where(t => t != null).ToList();
            if (_tools.Count == 0) throw new ArgumentException("A escada precisa de pelo menos uma fresa.", nameof(tools));
        }

        public IReadOnlyList<MillingTool> Tools
        {
            get { return _tools; }
        }

        /// <summary>
        /// A ferramentaria REAL do Carlos, informada em 2026-09-11. Enquanto não estiver em
        /// <c>config.json</c>, é esta a escada de fábrica — e é ela que define os 0,5 mm de raio
        /// mínimo e os tetos de 10 / 20 mm que o resto da análise usa.
        /// </summary>
        public static ToolLadder Shop()
        {
            return new ToolLadder(new[]
            {
                // --- topo reto ---
                new MillingTool(1.0,  3.0,  ToolShape.Flat),
                new MillingTool(1.0,  10.0, ToolShape.Flat, "longneck"),
                new MillingTool(1.5,  4.0,  ToolShape.Flat),
                new MillingTool(2.0,  6.0,  ToolShape.Flat),
                new MillingTool(2.5,  6.0,  ToolShape.Flat),
                new MillingTool(3.0,  8.0,  ToolShape.Flat),
                new MillingTool(4.0,  11.0, ToolShape.Flat),
                new MillingTool(5.0,  12.0, ToolShape.Flat),
                new MillingTool(6.0,  15.0, ToolShape.Flat),

                // --- esférica ---
                new MillingTool(1.0,  4.0,  ToolShape.Ball),
                new MillingTool(1.0,  10.0, ToolShape.Ball, "longneck"),
                new MillingTool(1.5,  4.0,  ToolShape.Ball),
                new MillingTool(2.0,  6.0,  ToolShape.Ball),
                new MillingTool(2.0,  20.0, ToolShape.Ball, "longneck"),
                new MillingTool(3.0,  6.0,  ToolShape.Ball),
                new MillingTool(4.0,  8.0,  ToolShape.Ball),
                new MillingTool(5.0,  10.0, ToolShape.Ball),
                new MillingTool(6.0,  12.0, ToolShape.Ball),
            });
        }

        /// <summary>Menor raio de canto que esta ferramentaria consegue produzir (mm).</summary>
        public double SmallestRadiusMm
        {
            get { return _tools.Min(t => t.RadiusMm); }
        }

        /// <summary>
        /// Profundidade máxima (mm) alcançável numa região que exige raio de canto
        /// <paramref name="requiredRadiusMm"/>. Zero = nenhuma fresa serve esse raio.
        ///
        /// Uma fresa só serve a região se o raio DELA couber no raio exigido (r ≤ ρ); entre as
        /// que servem, vale a mais longa.
        /// </summary>
        public double ReachMm(double requiredRadiusMm)
        {
            var usable = _tools.Where(t => Fits(t, requiredRadiusMm)).ToList();
            return usable.Count == 0 ? 0.0 : usable.Max(t => t.FluteLengthMm);
        }

        /// <summary>
        /// Igual a <see cref="ReachMm(double)"/>, restrito a uma família. Importa porque as duas
        /// não produzem o mesmo canto: numa região que só o topo reto resolve (canto de piso
        /// vivo), o teto é o da família dele, não o combinado.
        /// </summary>
        public double ReachMm(double requiredRadiusMm, ToolShape shape)
        {
            var usable = _tools.Where(t => t.Shape == shape && Fits(t, requiredRadiusMm)).ToList();
            return usable.Count == 0 ? 0.0 : usable.Max(t => t.FluteLengthMm);
        }

        /// <summary>
        /// A fresa a usar numa região que exige raio <paramref name="requiredRadiusMm"/> a
        /// <paramref name="depthMm"/> de profundidade, ou <c>null</c> se nenhuma serve.
        ///
        /// Entre as que servem e alcançam, escolhe a de MAIOR raio: quem pode usar a Ø6 não usa a
        /// Ø1: mais rígida, mais avanço, menos risco de quebrar dentro da peça.
        /// </summary>
        public MillingTool BestFor(double requiredRadiusMm, double depthMm)
        {
            if (depthMm < 0) depthMm = 0;
            return _tools
                .Where(t => Fits(t, requiredRadiusMm) && t.FluteLengthMm >= depthMm - ToleranceMm)
                .OrderByDescending(t => t.RadiusMm)
                .ThenByDescending(t => t.FluteLengthMm)
                .FirstOrDefault();
        }

        /// <summary>
        /// Classifica um par (raio exigido, profundidade). Separa os dois motivos de recusa
        /// porque eles não são a mesma conversa: <see cref="MachinabilityVerdict.BelowMinimumRadius"/>
        /// é geometria pura (nenhuma fresa desse raio existe), enquanto
        /// <see cref="MachinabilityVerdict.BeyondReach"/> é uma questão de ferramenta mais longa —
        /// e às vezes se resolve comprando uma, não queimando.
        /// </summary>
        public MachinabilityVerdict Classify(double requiredRadiusMm, double depthMm, out MillingTool tool)
        {
            tool = BestFor(requiredRadiusMm, depthMm);
            if (tool != null) return MachinabilityVerdict.Millable;
            return ReachMm(requiredRadiusMm) <= 0
                ? MachinabilityVerdict.BelowMinimumRadius
                : MachinabilityVerdict.BeyondReach;
        }

        /// <summary>
        /// As fresas que de fato decidem a resposta — as não dominadas (ver
        /// <see cref="DominatedTools"/>). Na escada do Carlos são 6 de 18.
        /// </summary>
        public IReadOnlyList<MillingTool> DecidingTools()
        {
            return _tools.Where(t => !_tools.Any(o => Dominates(o, t))).ToList();
        }

        /// <summary>
        /// As fresas DOMINADAS: para cada raio que elas servem existe, na mesma família, outra que
        /// serve o mesmo raio e vai mais fundo. Elas continuam existindo por rigidez, carga de
        /// cavaco e tempo de ciclo — só não mudam a resposta de "dá pra fazer?".
        /// </summary>
        public IReadOnlyList<MillingTool> DominatedTools()
        {
            return _tools.Where(t => _tools.Any(o => Dominates(o, t))).ToList();
        }

        /// <summary>Linha de log com o resumo da escada (invariante: vai para arquivo, não para tela).</summary>
        public string Describe()
        {
            var deciding = DecidingTools();
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Ferramentaria: {0} fresa(s), {1} decidem. Raio minimo R{2:0.00}mm (alcance {3:0.0}mm); " +
                "em R1.00mm o alcance vai a {4:0.0}mm. Decidem: {5}.",
                _tools.Count, deciding.Count, SmallestRadiusMm, ReachMm(SmallestRadiusMm),
                ReachMm(1.0), string.Join(" | ", deciding.Select(t => t.Label)));
        }

        /// <summary>A fresa <paramref name="t"/> serve uma região que exige raio de canto <paramref name="requiredRadiusMm"/>?</summary>
        private static bool Fits(MillingTool t, double requiredRadiusMm)
        {
            return t.RadiusMm <= requiredRadiusMm + ToleranceMm;
        }

        /// <summary>
        /// <paramref name="a"/> domina <paramref name="b"/>: mesma família, raio menor ou igual
        /// (serve tudo que b serve) e comprimento maior ou igual (chega onde b chega), sendo
        /// estritamente melhor em pelo menos um dos dois. Empate exato não domina — duas fresas
        /// idênticas não se anulam.
        /// </summary>
        private static bool Dominates(MillingTool a, MillingTool b)
        {
            if (ReferenceEquals(a, b)) return false;
            if (a.Shape != b.Shape) return false;
            if (a.RadiusMm > b.RadiusMm + ToleranceMm) return false;
            if (a.FluteLengthMm < b.FluteLengthMm - ToleranceMm) return false;
            return a.RadiusMm < b.RadiusMm - ToleranceMm
                || a.FluteLengthMm > b.FluteLengthMm + ToleranceMm;
        }
    }
}
