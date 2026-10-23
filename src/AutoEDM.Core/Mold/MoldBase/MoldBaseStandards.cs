using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>Coluna e bucha guia Polimold de um alojamento ØD2 (catálogo de componentes, p.2 e p.4).</summary>
    public sealed class GuideSize
    {
        public double D2 { get; }        // corpo (m6) = alojamento das coordenadas gerais
        public double D1 { get; }        // guia (g6) / furo da bucha (H7)
        public double D1Anti { get; }    // a coluna anti-inversão: ponta menor (CATPM2P p.3)
        public double D3 { get; }        // cabeça
        public double Head { get; }      // altura da cabeça (L3 da coluna, L2 da bucha)
        public double S { get; }         // comprimento de guia além da cabeça (colunamento B/C)

        public GuideSize(double d2, double d1, double d1Anti, double d3, double head, double s)
        {
            D2 = d2; D1 = d1; D1Anti = d1Anti; D3 = d3; Head = head; S = s;
        }
    }

    /// <summary>
    /// Os padrões de cada tipo de porta-molde, aprendidos em 2026-10-23 nos arquivos da casa (lidos sem alterar
    /// nada): os padrões em W:\01 - Biblioteca\Padrões\Moldes e os moldes de W:\09 - Projetos\Projetos Atuais.
    /// • Polimold — o 15354 (3035) da biblioteca confere com o catálogo: coluna CG Ø24/30/35 com a cabeça
    ///   rebaixada 6 no topo da P1 e 16 de guia na PBS; bucha BG Ø24/30/35 com a cabeça no fundo da P2 e 16 na PS;
    ///   pino de retorno Ø14 com cabeça Ø22 × 7 rebaixada no fundo da CPE, comprimento CPE + curso + PS + P2.
    /// • MUD = o padrão TIPO GAVETA (000.asm): a unidade 246 × 170 desliza no quadro 7160 (346 × 296), que vem da
    ///   biblioteca. Fixa "100" de 46 (o canal do quadro tem 45,5); móvel = "200" de 32 com ressalto 186 × 110 × 5
    ///   encaixado no alojamento passante da "201" de 55 (200 + 201 = 87, o canal do lado móvel); extratoras
    ///   "301"/"302" de 184 × 108 × 12 dentro da 201, a 0,7 do fundo, empurradas pelos extratores do quadro
    ///   (Ø10 com cabeça Ø16 × 5 — são do QUADRO, não pinos de retorno: o padrão não tem pino de retorno);
    ///   6 parafusos M8 da 201 na 200; guias 8760.001v3 em rasgos de 7 × 23 nas laterais; puxadores 8748.
    /// • Babyplast = o padrão BABY PLAST (PADRAO.asm — o mais usado, Carlos): metades "100" de 40 e "200" de 44,
    ///   76 × 76 nos primeiros 19 / 23 mm a partir da partição e 75 × 75 nos 21 de trás, travadas por 4 ressaltos
    ///   de canto de 12 × 4,1 na fixa e rebaixos de 12,1 × 4,2 na móvel (não há pino-guia); 4 M6 em 28,2 × 28,2
    ///   nas costas de cada metade; placas de apoio "101"/"201" de 74 × 74 × 6 presas por 4 M6 em 63 × 63;
    ///   extratoras "300"/"301" de 70 × 70 × 12 unidas por 4 M6 em 31,2 × 31,2; engate de extração 8760.019.
    ///   (A pasta BABY PLAST - 75 X 75 é a versão antiga, toda 75 × 75, travas de 11,5.)
    /// • Zamak = ZAMAK\MINI (Matriz - via metal 2.asm): FIXO 160 × 160 × 39 e MÓVEL × 40, presos por 4 M8 que
    ///   entram pela partição (rebaixo Ø15 × 9) no cavalete fixo e nos paralelos; 4 guias Ø12 com a furação 1 mm
    ///   fora de centro (anti-inversão), cegas 36,4 na FIXO e passantes na MÓVEL; paralelos 37,9 × 160 × 45;
    ///   extratoras 130 × 80 × 16 (6 M8, também 1 mm fora de centro) com 4 pinos de retorno Ø8 sem cabeça cravados
    ///   13,4 na de cima; curso 13; cavaletes 200 × 220 × 20 com janela 91 × 91, o móvel preso por 6 M8 aos
    ///   paralelos.
    /// </summary>
    public static class MoldBaseStandards
    {
        // ------------------------------------------------------------------ Polimold: colunas, buchas, pinos

        private static readonly GuideSize[] Guides =
        {
            new GuideSize(20, 15, 14, 25, 6, 12),
            new GuideSize(26, 20, 18, 31, 6, 17),
            new GuideSize(30, 24, 22, 35, 6, 17),
            new GuideSize(42, 32, 30, 47, 6, 21),
            new GuideSize(54, 42, 40, 60, 10, 21),
            new GuideSize(60, 50, 48, 70, 12, 21),
            new GuideSize(70, 60, 58, 80, 12, 21),
        };

        /// <summary>Comprimentos de ponta (L1) padronizados da coluna (p.2).</summary>
        public static readonly double[] ColumnTipLengths = { 22, 27, 36, 46, 56, 66, 76, 96, 116, 156, 196 };

        public static GuideSize GuideFor(double housingD2) =>
            Guides.FirstOrDefault(g => Math.Abs(g.D2 - housingD2) < 0.5) ?? Guides.OrderBy(g => Math.Abs(g.D2 - housingD2)).First();

        /// <summary>Ponta da coluna: o menor L1 padrão que atravessa a P2 inteira e passa 5 mm (no 3035: P2 36 → 46).</summary>
        public static double ColumnTip(double p2)
        {
            foreach (double l in ColumnTipLengths) if (l >= p2 + 5 - 1e-6) return l;
            return Math.Ceiling(p2 + 5);
        }

        /// <summary>Cabeça do pino de retorno: Ø d + 8 × 7 (medido: Ø14 → Ø22 × 7).</summary>
        public static double ReturnPinHead(double d) => d + 8;
        public const double ReturnPinHeadHeight = 7;

        // ------------------------------------------------------------------ MUD (tipo gaveta, quadro 7160)

        public const double MudWidth = 170;            // sentido de A (entre as guias do quadro)
        public const double MudLength = 246;           // sentido de C (o sentido de deslizar)
        public const double MudFixedThickness = 46;    // "100": o canal do lado fixo do quadro tem 45,5
        public const double MudMovingThickness = 87;   // "200" + "201": o canal do lado móvel
        public const double MudCavityPlate = 32;       // "200" do padrão
        public const double MudBossWidth = 110, MudBossLength = 186, MudBossHeight = 5;   // encaixe 200 → 201
        public const double MudEjectorWidth = 108, MudEjectorLength = 184, MudEjectorThickness = 12;
        public const double MudEjectorRest = 0.7;      // a 302 fica 0,7 acima do fundo da 201
        public const double MudCouplingPocket = 72, MudCouplingDepth = 6;   // batente 8760.018 no fundo da 302
        /// <summary>Os 6 M8 da 201 na 200: ±72 em A × (−92, 0, 92) em C; Ø9 + rebaixo Ø15 × 9 na 201, rosca 16 na 200.</summary>
        public static readonly HolePair MudScrews = new HolePair(144, 184, 8, "M8");
        public const double MudScrewClearance = 9, MudScrewHead = 15, MudScrewHeadDepth = 9, MudScrewThread = 16;
        /// <summary>Rasgo das guias 8760.001v3: 7 para dentro, faixa de altura a partir da partição.</summary>
        public const double MudGrooveDepth = 7;
        public static readonly double[] MudFixedGroove = { 12, 35 }, MudMovingGroove = { -55, -32 };
        public const string MudFrameName = "7160 - PORTA MOLDE ESPECIAL 256X296MM";

        /// <summary>Curso da gaveta: o que sobra na 201 depois do ressalto da 200, das duas extratoras e do 0,7 de fundo.</summary>
        public static double MudStroke(double p2) => MudMovingThickness - p2 - MudBossHeight - 2 * MudEjectorThickness - MudEjectorRest;

        // ------------------------------------------------------------------ Babyplast

        public const double BabySide = 76, BabyBackSide = 75, BabyStepDepth = 21;   // 76 perto da partição, 75 nos 21 de trás
        public const double BabyFixedHalf = 40, BabyMovingHalf = 44;
        public const double BabyBackingSide = 74;
        public const double BabyBackingThickness = 6;
        public const double BabyEjectorSide = 70;
        public const double BabyEjectorThickness = 12;
        public const double BabyLockBoss = 12, BabyLockBossHeight = 4.1, BabyLockRecess = 12.1, BabyLockRecessDepth = 4.2;
        /// <summary>4 M6 nas costas de cada metade, em 28,2 × 28,2, 12 de fundo (finalidade a confirmar).</summary>
        public static readonly HolePair BabyBackScrews = new HolePair(28.2, 28.2, 6, "M6");
        public const double BabyBackScrewDepth = 12;
        /// <summary>M6 das placas de apoio nas metades: Ø6,2 passante na placa de apoio, rosca 15 na metade.</summary>
        public static readonly HolePair BabyBackingScrews = new HolePair(63, 63, 6, "M6");
        public const double BabyBackingClearance = 6.2, BabyBackingThread = 15;
        /// <summary>M6 que unem as extratoras: rosca passante na 300, Ø6,5 + rebaixo Ø11 × 6,5 na 301.</summary>
        public static readonly HolePair BabyEjectorScrews = new HolePair(31.2, 31.2, 6, "M6");
        public const double BabyEjectorClearance = 6.5, BabyEjectorHead = 11, BabyEjectorHeadDepth = 6.5;

        // ------------------------------------------------------------------ Zamak (MINI)

        public const double ZamakSide = 160, ZamakFixed = 39, ZamakMoving = 40;
        public const double ZamakClampWidth = 220, ZamakClampLength = 200, ZamakClampThickness = 20;   // A × C
        public const double ZamakWindowA = 91.2, ZamakWindowCFixed = 91.5, ZamakWindowCMoving = 91.2;
        public const double ZamakParallelWidth = 37.9, ZamakEjectorWidth = 80, ZamakEjectorLength = 130, ZamakEjectorThickness = 16;
        public const double ZamakStroke = 13;
        /// <summary>Guias Ø12: (±51; −55,5) e (±51; 57,5) — a furação 1 mm fora de centro em C é a anti-inversão.</summary>
        public static readonly double[][] ZamakGuides = { new[] { -51.0, -55.5 }, new[] { 51.0, -55.5 }, new[] { 51.0, 57.5 }, new[] { -51.0, 57.5 } };
        public const double ZamakGuide = 12, ZamakGuideBack = 2.6;      // cega na FIXO deixando 2,6 atrás
        /// <summary>M8 da FIXO no cavalete fixo e da MÓVEL nos paralelos, pela partição (Ø9 + rebaixo Ø15 × 9).</summary>
        public static readonly HolePair ZamakPlateScrews = new HolePair(140, 120, 8, "M8");
        public const double ZamakScrewClearance = 9, ZamakScrewHead = 15, ZamakScrewHeadDepth = 9, ZamakParallelThread = 20;
        /// <summary>6 M8 do cavalete móvel nos paralelos: ±61 em A × (−50, 0, 50) em C.</summary>
        public static readonly HolePair ZamakBaseScrews = new HolePair(122, 100, 8, "M8");
        public const double ZamakBaseThread = 16;
        /// <summary>Pinos de retorno Ø8 sem cabeça, cravados 13,4 na extratora de cima: ±22,5 em A × ±55 em C.</summary>
        public static readonly HolePair ZamakReturnPins = new HolePair(45, 110, 8.1);
        public const double ZamakReturnPin = 8, ZamakReturnPinSeat = 13.4;
        /// <summary>6 M8 das extratoras: ±31 em A × (−42; 1; 44) em C; Ø7,8 + rebaixo Ø13 × 10 na de baixo (como no padrão).</summary>
        public static readonly double[][] ZamakEjectorScrews =
        {
            new[] { -31.0, -42 }, new[] { 31.0, -42 }, new[] { -31.0, 1 }, new[] { 31.0, 1 }, new[] { -31.0, 44 }, new[] { 31.0, 44 },
        };
        public const double ZamakEjectorClearance = 7.8, ZamakEjectorHead = 13, ZamakEjectorHeadDepth = 10;

        // ------------------------------------------------------------------ peças de biblioteca dos padrões

        private const string Library = @"\\servidor02\ferramentaria$\01 - Biblioteca\";
        public const string MudFramePath = @"\\servidor02\ferramentaria$\09 - Projetos\Projetos Atuais\01 - PORTA MOLDES\" +
                                           MudFrameName + @"\" + MudFrameName + ".asm";
        private const string Rail = Library + @"Componentes\Guias Moldes de Gaveta\8760.001v3.par";
        private const string HandleStandoff = Library + @"Componentes\Puxadores de Gaveta\8748.007.par";
        private const string HandleBar = Library + @"Componentes\Puxadores de Gaveta\8748.008.par";
        private const string CouplingPlate = Library + @"Componentes\Batentes\8760.018.par";
        private const string ScrewM8x60 = Library + @"Componentes\Parafusos\DIN 912 - ALLEN CAB. CILINDRICA\10395.par";
        private const string EjectorCoupling = Library + @"Padrões\Pinos\Engate Extração\8760.019.par";

        private static readonly double[] I3 = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        /// <summary>
        /// As peças de biblioteca do 000.asm da gaveta, com a pose LIDA do padrão (R linha a linha, t em mm, no
        /// referencial dele: X = 246, Y = 170, Z para o lado fixo, partição em 0). Ficaram de fora o que depende do
        /// molde (engates e bujões da refrigeração) e os parafusos que pedem furo lateral (guias, puxadores, batente).
        /// </summary>
        private static IEnumerable<LibraryPart> MudTemplate()
        {
            yield return Part(MudFramePath, "Quadro 7160", I3, 0, 0, 0);
            yield return Part(Rail, "Guia da gaveta", new double[] { 0, -1, 0, -1, 0, 0, 0, 0, -1 }, 0.45, 78, 23.5);
            yield return Part(Rail, "Guia da gaveta", new double[] { 0, -1, 0, 1, 0, 0, 0, 0, 1 }, 0.45, -78, 23.5);
            yield return Part(Rail, "Guia da gaveta", new double[] { 0, -1, 0, -1, 0, 0, 0, 0, -1 }, 0.45, 78, -43.5);
            yield return Part(Rail, "Guia da gaveta", new double[] { 0, -1, 0, 1, 0, 0, 0, 0, 1 }, 0.45, -78, -43.5);
            yield return Part(HandleStandoff, "Puxador", I3, 120, 0, 18);
            yield return Part(HandleBar, "Puxador", I3, 166, 0, 18);
            yield return Part(HandleStandoff, "Puxador", new double[] { 1, 0, 0, 0, -1, 0, 0, 0, -1 }, 120, 0, -44);
            yield return Part(HandleBar, "Puxador", new double[] { 1, 0, 0, 0, -1, 0, 0, 0, -1 }, 166, 0, -44);
            yield return Part(CouplingPlate, "Batente da extratora", new double[] { -1, 0, 0, 0, 1, 0, 0, 0, -1 }, 0, 0, -80.3);
            foreach (double x in new[] { -92.0, 0, 92 })
                foreach (double y in new[] { -72.0, 72 })
                    yield return Part(ScrewM8x60, "Parafuso M8 × 60", new double[] { 0, 0, -1, 0, 1, 0, 1, 0, 0 }, x, y, -78);
        }

        /// <summary>
        /// As peças de biblioteca do padrão, no referencial da SUBMONTAGEM (X = A, Y = C, Z para o lado fixo). A
        /// gaveta: o padrão tem o comprimento em X — gira 90° (x = −y', y = x'). O Babyplast: o eixo do molde é o X
        /// do padrão — (x, y, z) = (y', z', x'), e o engate acompanha a face de cima da extratora "301".
        /// </summary>
        public static List<LibraryPart> LibraryParts(MoldBaseFamily family, double ejectorTopW = 0)
        {
            var list = new List<LibraryPart>();
            if (family == MoldBaseFamily.Mud)
            {
                double[] m = { 0, -1, 0, 1, 0, 0, 0, 0, 1 };
                foreach (LibraryPart p in MudTemplate()) list.Add(Transform(p, m));
            }
            else if (family == MoldBaseFamily.Babyplast)
            {
                double[] m = { 0, 1, 0, 0, 0, 1, 1, 0, 0 };
                double c = Math.Sqrt(0.5);
                list.Add(Transform(Part(EjectorCoupling, "Engate de extração", new double[] { 0, 0, 1, c, c, 0, -c, c, 0 }, ejectorTopW, 0, 0), m));
            }
            return list;
        }

        /// <summary>Todos os arquivos de biblioteca que o botão põe — para tirar e repor na troca de porta-molde.</summary>
        public static readonly string[] LibraryFileNames =
        {
            MudFrameName + ".asm", "8760.001v3.par", "8748.007.par", "8748.008.par", "8760.018.par", "10395.par", "8760.019.par",
        };

        private static LibraryPart Part(string path, string label, double[] r, double x, double y, double z) =>
            new LibraryPart { Path = path, Label = label, R = r, T = new[] { x, y, z } };

        private static LibraryPart Transform(LibraryPart p, double[] m) =>
            new LibraryPart { Path = p.Path, Label = p.Label, R = Mul(m, p.R), T = Mul(m, p.T) };

        private static double[] Mul(double[] a, double[] b)
        {
            if (b.Length == 3)
                return new[] { a[0] * b[0] + a[1] * b[1] + a[2] * b[2], a[3] * b[0] + a[4] * b[1] + a[5] * b[2], a[6] * b[0] + a[7] * b[1] + a[8] * b[2] };
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[3 * i + j] = a[3 * i] * b[j] + a[3 * i + 1] * b[3 + j] + a[3 * i + 2] * b[6 + j];
            return r;
        }

        /// <summary>
        /// Uma "série" sintética para MUD e Babyplast: o mesmo tipo do Polimold, para que pilha, relatório e
        /// posicionamento sejam um caminho só. A área útil é a placa inteira (a margem da casa faz a parede).
        /// </summary>
        public static PolimoldSeries UnitSeries(MoldBaseFamily family)
        {
            if (family == MoldBaseFamily.Mud)
                return new PolimoldSeries
                {
                    Name = "Gaveta 170×246", A = MudWidth, B = MudWidth, C = MudLength, X = MudWidth, Y = MudLength,
                    P = MudWidth, Thicknesses = new[] { MudFixedThickness },
                };
            if (family == MoldBaseFamily.Zamak)
                return new PolimoldSeries
                {
                    Name = "Zamak MINI 160×160", A = ZamakSide, B = ZamakClampWidth, C = ZamakSide, X = ZamakSide, Y = ZamakSide,
                    P = ZamakEjectorWidth, Thicknesses = new[] { ZamakFixed, ZamakMoving },
                };
            return new PolimoldSeries
            {
                Name = "Babyplast 76×76", A = BabySide, B = BabySide, C = BabySide, X = BabySide, Y = BabySide,
                P = BabyEjectorSide, Thicknesses = new double[0],
            };
        }

        public static string FamilyLabel(MoldBaseFamily f) =>
            f == MoldBaseFamily.Mud ? "MUD — tipo gaveta (quadro 7160)" : f == MoldBaseFamily.Babyplast ? "Babyplast 76 × 76"
            : f == MoldBaseFamily.Zamak ? "Zamak MINI 160 × 160" : "Polimold (2 placas)";
    }
}
