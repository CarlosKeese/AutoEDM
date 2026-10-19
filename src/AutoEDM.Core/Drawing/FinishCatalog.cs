using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using AutoEDM.Electrode;

namespace AutoEDM.Drawing
{
    /// <summary>
    /// Faixa de tolerância de UMA coluna da tabela (furo OU eixo), em mm. <see cref="Upper"/> é
    /// sempre o afastamento maior: a tabela da ferramentaria escreve "-0,02 / -0,01" e o desenho
    /// do Carlos cotou isso como superior -0,01 / inferior -0,02.
    /// </summary>
    public sealed class ToleranceBand
    {
        public double Upper { get; }
        public double Lower { get; }

        public ToleranceBand(double a, double b)
        {
            Upper = Math.Max(a, b);
            Lower = Math.Min(a, b);
        }

        public bool IsSymmetric => Math.Abs(Upper + Lower) < 1e-9;

        public override string ToString()
        {
            var ci = CultureInfo.GetCultureInfo("pt-BR");
            return IsSymmetric
                ? "±" + Upper.ToString("0.0##", ci)
                : Upper.ToString("+0.0##;-0.0##;0", ci) + " / " + Lower.ToString("+0.0##;-0.0##;0", ci);
        }
    }

    /// <summary>
    /// Uma linha da "Tabela de cores" da ferramentaria: o que a cor da face significa e a
    /// tolerância que ela impõe no desenho. A face de FURO (canal, alojamento) e a de EIXO
    /// (ressalto, bloco) têm colunas diferentes — <see cref="Hole"/> e <see cref="Shaft"/>.
    /// Tolerância nula = "Verificar" (a cor pede atenção, não um número).
    /// </summary>
    public sealed class FinishClass
    {
        /// <summary>Código do estilo de face no template ("11"), ou "Ra" para as cores de erosão.</summary>
        public string Code { get; }
        /// <summary>Nome da cor na tabela ("Verde").</summary>
        public string ColorName { get; }
        /// <summary>Finalidade da face, como na tabela.</summary>
        public string Purpose { get; }
        public ToleranceBand Hole { get; }
        public ToleranceBand Shaft { get; }
        /// <summary>RGB do estilo de face do template PanFlight (o que de fato fica na peça).</summary>
        public Color Rgb { get; }
        /// <summary>Ra (µm) quando a cor é de rugosidade de erosão; 0 nas demais.</summary>
        public double Ra { get; }
        /// <summary>Classe de ajuste ISO que acompanha o número ("H7"/"g6"), quando o estilo a traz.</summary>
        public string HoleFit { get; }
        public string ShaftFit { get; }

        public FinishClass(string code, string colorName, string purpose, Color rgb,
                           ToleranceBand hole, ToleranceBand shaft, double ra = 0,
                           string holeFit = null, string shaftFit = null)
        {
            Code = code; ColorName = colorName; Purpose = purpose; Rgb = rgb;
            Hole = hole; Shaft = shaft; Ra = ra; HoleFit = holeFit; ShaftFit = shaftFit;
        }

        /// <summary>A classe de ajuste da coluna que vale para a face (null = só os números).</summary>
        public string FitFor(bool external) => external ? ShaftFit : HoleFit;

        public bool NeedsReview => Hole == null && Shaft == null;

        /// <summary>A coluna que vale para a face: furo (material por fora) ou eixo (material por dentro).</summary>
        public ToleranceBand For(bool external) => external ? Shaft : Hole;

        public override string ToString() => Ra > 0
            ? $"Ra {Ra.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"))} ({Purpose})"
            : $"{Code} {ColorName} ({Purpose})";
    }

    /// <summary>
    /// Tabela de cores da ferramentaria PanFlight (Carlos, 2026-10-19), casada com os estilos de
    /// face do template de peça. A cor chega na face como um ESTILO NOMEADO
    /// ("11 - MED CALIB (F H7 / E g6)") — o nome é a chave mais confiável; o RGB é o reserva para
    /// peça que recebeu a cor sem o estilo (pintura direta, peça importada).
    ///
    /// As TOLERÂNCIAS vêm da tabela da ferramentaria, não do texto do nome do estilo: os dois
    /// discordam em alguns casos (o estilo 15 diz ±0,005, a tabela diz ±0,01) e a tabela é a que
    /// o Carlos mandou como referência.
    ///
    /// As cores de RUGOSIDADE de erosão (estilos "EROS. 0,1" … "EROS. 6,3") entram com a mesma
    /// tolerância das cavidades erodidas (Rosa, ±0,01) — regra dita pelo Carlos — e levam o Ra
    /// junto para o símbolo de acabamento.
    /// </summary>
    public sealed class FinishCatalog
    {
        private readonly List<FinishClass> _classes;
        private readonly RaColorMap _ra;
        private const int RgbTolerance = 3;

        public FinishCatalog(RaColorMap raMap = null)
        {
            _ra = raMap ?? new RaColorMap();
            _classes = Default().ToList();
        }

        public IReadOnlyList<FinishClass> Classes => _classes;

        /// <summary>A classe das cavidades erodidas (Rosa) — também a das cores de Ra.</summary>
        public FinishClass ErodedCavity => _classes.First(c => c.Code == "17");

        /// <summary>
        /// Classifica a face pelo nome do estilo (preferido) e, sem estilo conhecido, pelo RGB.
        /// Devolve null para face sem cor da tabela (cinza do corpo, cor qualquer).
        /// </summary>
        public FinishClass Resolve(string styleName, Color? rgb)
        {
            FinishClass byName = ResolveByStyleName(styleName);
            if (byName != null) return byName;
            return rgb.HasValue ? ResolveByRgb(rgb.Value) : null;
        }

        public FinishClass ResolveByStyleName(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName)) return null;
            string s = styleName.Trim();

            // "EROS. 1,6" — rugosidade de erosão. "EROS. CAVIDADES" é o código 17, e começa com
            // "17 -", então cai no caso de baixo antes de chegar aqui.
            if (s.StartsWith("EROS.", StringComparison.OrdinalIgnoreCase))
            {
                string num = s.Substring(5).Trim().Replace(',', '.');
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double ra))
                    return RaClass(ra);
            }

            // "11 - MED CALIB (F H7 / E g6)" -> código antes do hífen.
            int dash = s.IndexOf('-');
            if (dash > 0)
            {
                string code = s.Substring(0, dash).Trim();
                FinishClass c = _classes.FirstOrDefault(x => x.Code == code);
                if (c != null) return c;
            }
            return null;
        }

        public FinishClass ResolveByRgb(Color rgb)
        {
            FinishClass c = _classes.FirstOrDefault(x => Near(x.Rgb, rgb));
            if (c != null) return c;
            return _ra.TryGetRa(rgb, out double ra, out _) ? RaClass(ra) : null;
        }

        private FinishClass RaClass(double ra)
        {
            FinishClass rosa = ErodedCavity;
            return new FinishClass("Ra", "Ra " + ra.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")),
                "Erosão Ra " + ra.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " µm",
                Color.Empty, rosa.Hole, rosa.Shaft, ra);
        }

        private static bool Near(Color a, Color b) =>
            Math.Abs(a.R - b.R) <= RgbTolerance && Math.Abs(a.G - b.G) <= RgbTolerance && Math.Abs(a.B - b.B) <= RgbTolerance;

        private static ToleranceBand T(double a, double b) => new ToleranceBand(a, b);

        /// <summary>
        /// A tabela. RGB = estilo de face do template PanFlight (lido da peça 15142.101-CDT em
        /// 2026-10-19), coluna furo / coluna eixo = "FERRAMENTARIA: TABELA DE CORES". H7/g6 vêm
        /// do nome do estilo (11 e 19) e do desenho à mão, onde o Ø21 verde saiu "g6 -0,01/-0,02".
        /// </summary>
        private static IEnumerable<FinishClass> Default() => new[]
        {
            new FinishClass("10", "Azul Claro",    "Fechamentos e regiões de ajuste",          Color.FromArgb(0, 178, 178),   T(0, -0.01),     T(0.01, 0)),
            new FinishClass("11", "Verde",         "Ajuste rotativo, montagem deslizante",     Color.FromArgb(0, 128, 0),     T(0.02, 0.01),   T(-0.02, -0.01), holeFit: "H7", shaftFit: "g6"),
            new FinishClass("12", "Verde Claro",   "Folga mínima, movimento livre",            Color.FromArgb(33, 77, 42),    T(0.04, 0.06),   T(-0.04, -0.06)),
            new FinishClass("13", "Amarelo",       "Folga média, alívios em geral",            Color.FromArgb(204, 194, 0),   T(0.1, 0.02),    T(-0.02, -0.1)),
            new FinishClass("14", "Vermelho",      "Folga máxima, medida irrelevante",         Color.FromArgb(204, 0, 0),     T(0.2, -0.2),    T(0.2, -0.2)),
            new FinishClass("15", "Branco",        "Passagem de gases",                        Color.FromArgb(232, 232, 232), T(0.01, -0.01),  T(0.01, -0.01)),
            new FinishClass("16", "Roxo",          "Cavidades usinadas (Ra 0,8 a 1,6 µm)",     Color.FromArgb(116, 54, 178),  T(0.01, -0.01),  T(0.01, -0.01)),
            new FinishClass("17", "Rosa",          "Cavidades erodidas (Ra vide plano de erosão)", Color.FromArgb(176, 0, 178), T(0.01, -0.01), T(0.01, -0.01)),
            new FinishClass("18", "Rosa Claro",    "Região para polimento ou texturização",    Color.FromArgb(247, 131, 216), T(-0.1, -0.2),   T(0.1, 0.2)),
            new FinishClass("19", "Amarelo Claro", "Processo de erosão a fio",                 Color.FromArgb(178, 174, 134), T(0.02, 0.01),   T(-0.02, -0.01), holeFit: "H7", shaftFit: "g6"),
            new FinishClass("20", "Laranja",       "Montagem de elementos de vedação",         Color.FromArgb(204, 103, 0),   T(0.1, -0.1),    T(0.1, -0.1)),
            new FinishClass("21", "Azul",          "Passagem de água ou óleo (regulação térmica)", Color.FromArgb(54, 111, 178), T(0.2, -0.2), T(0.2, -0.2)),
            new FinishClass("22", "Laranja Claro", "Alteração de projeto — verificar",         Color.FromArgb(245, 128, 66),  null,            null),
        };
    }
}
