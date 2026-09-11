using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Machinability
{
    /// <summary>Uma broca: diâmetro nominal e comprimento do CANAL helicoidal (parte que corta).</summary>
    public sealed class Drill
    {
        public Drill(double diameterMm, double fluteLengthMm)
        {
            DiameterMm = diameterMm;
            FluteLengthMm = fluteLengthMm;
        }

        public double DiameterMm { get; }

        /// <summary>Comprimento do canal (mm) — o teto GEOMÉTRICO de profundidade daquele diâmetro.</summary>
        public double FluteLengthMm { get; }

        /// <summary>Rótulo para o usuário (pt-BR): "broca Ø8 (canal 75 mm)".</summary>
        public string Label
        {
            get { return "broca Ø" + DiameterMm.ToString("0.##") + " (canal " + FluteLengthMm.ToString("0.##") + " mm)"; }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// O jogo de brocas — e por que ele é uma classe SEPARADA de <see cref="ToolLadder"/>.
    ///
    /// Fresa e broca não respondem à mesma pergunta. Uma fresa de raio r serve QUALQUER canto de
    /// raio ρ ≥ r (ela cabe e sobra); uma broca de Ø d faz um furo de Ø d e ponto — o diâmetro tem
    /// de BATER, não de caber. Misturar as duas na mesma escada daria resposta errada nas duas
    /// pontas, então a sonda consulta esta classe só nas faces que dão a VOLTA COMPLETA
    /// (<c>SmallRadiusHit.IsFullRevolution</c>), que são as únicas que uma broca poderia ter feito.
    ///
    /// **Por que isto existe (achado do 1º run ao vivo, 2026-09-11):** uma face de Ø8 a 25 mm de
    /// profundidade saiu como "só EDM" porque a fresa mais longa da ferramentaria para em 20 mm. É
    /// um furo trivial: a broca DIN 338 de Ø8 tem 75 mm de canal. Sem o jogo de brocas, a análise
    /// condenava furo comum — o falso positivo que faz o usuário parar de abrir o relatório.
    ///
    /// Lógica PURA (sem COM), testada em <c>DrillSetTests</c>.
    /// </summary>
    public sealed class DrillSet
    {
        private readonly List<Drill> _drills;

        public DrillSet(IEnumerable<Drill> drills)
        {
            if (drills == null) throw new ArgumentNullException(nameof(drills));
            _drills = drills.Where(d => d != null).OrderBy(d => d.DiameterMm).ToList();
        }

        public IReadOnlyList<Drill> Drills
        {
            get { return _drills; }
        }

        /// <summary>
        /// O jogo do Carlos (informado em 2026-09-11): <b>DIN 338 de Ø1 a Ø19 mm</b>.
        ///
        /// A DIN 338 (broca helicoidal tipo N, série curta/"jobber") amarra o comprimento do canal
        /// ao diâmetro, então a tabela abaixo é NORMA, não preferência de oficina — e é por isso
        /// que dá para responder com número em vez de "confira a broca". A razão canal/diâmetro
        /// cai conforme a broca engrossa: ~12× no Ø1, ~9× no Ø6, ~7× no Ø19.
        ///
        /// **CONFERIR contra o jogo real.** Os valores são os da tabela da norma; se alguma broca
        /// da gaveta for série curta (DIN 1897) ou longa (DIN 340), o canal muda e o número aqui
        /// fica otimista ou pessimista. Os diâmetros intermediários que a norma também cobre (7,5 /
        /// 8,5 / 9,5 …) foram deixados de fora de propósito: a consulta usa o diâmetro tabelado
        /// IMEDIATAMENTE ABAIXO, o que subestima o canal — errar para o lado conservador.
        /// </summary>
        public static DrillSet Din338()
        {
            return new DrillSet(new[]
            {
                new Drill(1.0, 12),  new Drill(1.5, 18),  new Drill(2.0, 24),
                new Drill(2.5, 30),  new Drill(3.0, 33),  new Drill(3.5, 39),
                new Drill(4.0, 43),  new Drill(4.5, 47),  new Drill(5.0, 52),
                new Drill(5.5, 57),  new Drill(6.0, 57),  new Drill(6.5, 63),
                new Drill(7.0, 69),  new Drill(8.0, 75),  new Drill(9.0, 81),
                new Drill(10.0, 87), new Drill(11.0, 94), new Drill(12.0, 101),
                new Drill(13.0, 101), new Drill(14.0, 108), new Drill(15.0, 114),
                new Drill(16.0, 120), new Drill(17.0, 125), new Drill(18.0, 130),
                new Drill(19.0, 135),
            });
        }

        public double SmallestDiameterMm
        {
            get { return _drills.Count == 0 ? 0 : _drills[0].DiameterMm; }
        }

        public double LargestDiameterMm
        {
            get { return _drills.Count == 0 ? 0 : _drills[_drills.Count - 1].DiameterMm; }
        }

        /// <summary>
        /// A broca que atende um furo de <paramref name="holeDiameterMm"/> — a de maior diâmetro
        /// TABELADO que não passe dele. Null quando o furo é menor que a menor broca do jogo.
        ///
        /// Furo acima da maior broca não é problema: fura-se menor e abre-se por interpolação com
        /// fresa; quem julga esse caso é a <see cref="ToolLadder"/>, não esta classe.
        /// </summary>
        public Drill For(double holeDiameterMm)
        {
            const double ToleranceMm = 1e-6;
            return _drills.LastOrDefault(d => d.DiameterMm <= holeDiameterMm + ToleranceMm);
        }

        /// <summary>
        /// Dá para furar <paramref name="holeDiameterMm"/> até <paramref name="depthMm"/>?
        ///
        /// O teto é o comprimento do CANAL — o limite geométrico. Na prática furo fundo pede
        /// ciclo de picada (o cavaco tem de sair), e isso é tempo, não impossibilidade: a análise
        /// responde "dá para fazer", e a decisão de como fazer continua sendo do operador.
        /// </summary>
        public bool CanDrill(double holeDiameterMm, double depthMm, out Drill drill)
        {
            const double ToleranceMm = 1e-6;
            drill = For(holeDiameterMm);
            if (drill == null) return false;
            if (depthMm < 0) depthMm = 0;
            return depthMm <= drill.FluteLengthMm + ToleranceMm;
        }

        /// <summary>Linha de log com o resumo do jogo (invariante: vai para arquivo).</summary>
        public string Describe()
        {
            if (_drills.Count == 0) return "Brocas: jogo vazio.";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Brocas: {0} medida(s) de Ø{1:0.##} a Ø{2:0.##}mm; canal de {3:0.##}mm no menor e {4:0.##}mm no maior.",
                _drills.Count, SmallestDiameterMm, LargestDiameterMm,
                _drills[0].FluteLengthMm, _drills[_drills.Count - 1].FluteLengthMm);
        }
    }
}
