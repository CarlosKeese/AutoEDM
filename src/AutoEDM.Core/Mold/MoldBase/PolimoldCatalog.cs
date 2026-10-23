using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>Um par de furos simétricos em relação ao centro do molde: entre-centros na largura (A) e no comprimento (C).</summary>
    public sealed class HolePair
    {
        public double AcrossA { get; }   // entre-centros no sentido de A (0 = sobre a linha de centro)
        public double AlongC { get; }    // entre-centros no sentido de C
        public double Diameter { get; }
        public string Thread { get; }    // "M12" quando é parafuso; null para coluna/pino

        public HolePair(double acrossA, double alongC, double diameter, string thread = null)
        {
            AcrossA = acrossA; AlongC = alongC; Diameter = diameter; Thread = thread;
        }
    }

    /// <summary>Um dos três cursos de extração padronizados da série, com a altura de calço (E's) que o dá.</summary>
    public sealed class StrokeOption
    {
        public int Index { get; }        // 1, 2 ou 3 — o dígito do código de pedido
        public double Stroke { get; }
        public double SpacerHeight { get; }

        public StrokeOption(int index, double stroke, double spacerHeight) { Index = index; Stroke = stroke; SpacerHeight = spacerHeight; }
    }

    /// <summary>
    /// Uma série do porta-molde padrão Polimold de 2 placas (catálogo CATPM2P, jun/2013, p.5–7), em mm.
    /// A = largura de P1, P2, PS e espaçadores; B = largura das placas base com ABA (Tipo 4); C = comprimento;
    /// P = largura das placas extratoras (CPE/PE); X × Y = área útil para cavidades/postiços; ESP = largura do
    /// espaçador, rente à borda de A.
    /// </summary>
    public sealed class PolimoldSeries
    {
        public string Name { get; internal set; }          // "30.35"
        public double A { get; internal set; }
        public double B { get; internal set; }
        public double C { get; internal set; }
        public double P { get; internal set; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public double PBS { get; internal set; }
        public double PS { get; internal set; }
        public double ESP { get; internal set; }
        public double CPE { get; internal set; }
        public double PE { get; internal set; }
        public double PBI { get; internal set; }

        public HolePair Columns { get; internal set; }        // D × E, Ø D2 (coluna/bucha)
        public HolePair MoldScrews { get; internal set; }     // F × G, Pf1/Pf2
        public HolePair EjectorGuides { get; internal set; }  // H × I, Ø D2 da guia da extratora (H = 0 → sobre a linha de centro)
        public HolePair EjectorScrews { get; internal set; }  // J × K, Pf3
        public HolePair ReturnPins { get; internal set; }     // L × M, Ø PR
        public HolePair StopButtons { get; internal set; }    // J × O, Ø EP
        public double SlotW { get; internal set; }            // rasgo de fixação do Tipo 2: W, V, Z
        public double SlotV { get; internal set; }
        public double SlotZ { get; internal set; }

        public IReadOnlyList<double> Thicknesses { get; internal set; }   // espessuras de P1/P2 da faixa
        public IReadOnlyList<StrokeOption> Strokes { get; internal set; }

        /// <summary>"3035" — os quatro primeiros dígitos do código de pedido (A/10 e C/10).</summary>
        public string Code => Name.Replace(".", "");

        public double PlateArea => A * C;

        public override string ToString() => $"{Name} ({A:0}×{C:0})";
    }

    /// <summary>
    /// Tabelas do catálogo Polimold de porta-molde de 2 placas (CATPM2P, impressão jun/2013), transcritas das
    /// imagens das páginas 5 (séries), 6 (coordenadas) e 7 (sub-séries e cursos) — o texto do PDF tem a fonte
    /// corrompida. Três transcrições independentes bateram. Fonte:
    /// https://polimold.com.br/wp-content/uploads/downloads/porta_molde/2placas/catalogo_2placas_web_full.pdf
    ///
    /// Regras DEDUZIDAS das tabelas (fecham nas 53 séries, mas não estão escritas no catálogo — os testes travam):
    /// R1 E's = curso + CPE + PE + 4 (o 4 é o encosto padrão entre a PE e a PBI);
    /// R2 X = P − 2; R3 Y = I − Ø guia da extratora − 8.
    /// </summary>
    public static class PolimoldCatalog
    {
        // Série | A B C | P X Y | PBS PS ESP CPE PE PBI
        private const string SeriesTable = @"
15.15 156 206 156  90  88  42 22 27 32  9 17 22
15.20 156 206 196  90  88  82 22 27 32  9 17 22
15.25 156 196 246  90  88 132 22 27 32  9 17 22
20.20 196 246 196 118 116  72 22 27 38 12 17 22
20.25 196 246 246 108 106 122 27 36 43 12 17 27
20.30 196 246 296 108 106 160 27 36 43 12 17 27
20.35 196 246 346 108 106 210 27 36 43 12 17 27
20.40 196 246 396 108 106 260 36 36 43 17 22 36
25.25 246 296 246 158 156  96 27 36 43 12 17 27
25.30 246 296 296 158 156 146 27 36 43 12 17 27
25.35 246 296 346 158 156 196 27 46 43 17 22 27
25.40 246 296 396 150 148 246 36 46 47 17 22 36
25.45 246 296 446 158 156 296 36 46 43 17 22 36
25.50 246 296 496 158 156 346 36 46 43 17 22 36
30.30 296 346 296 208 206 146 27 46 43 12 17 27
30.35 296 346 346 208 206 196 36 46 43 17 22 36
30.40 296 346 396 200 198 246 36 46 47 17 22 36
30.45 296 346 446 208 206 296 36 46 43 17 22 36
30.50 296 346 496 208 206 346 36 46 43 17 22 36
35.35 346 396 346 258 256 196 36 46 43 17 22 36
35.40 346 396 396 218 216 238 36 46 62 17 22 36
35.45 346 396 446 218 216 288 36 46 62 17 22 36
35.50 346 396 496 218 216 338 36 46 62 17 22 36
35.55 346 396 546 218 216 388 36 46 62 22 27 36
35.60 346 396 596 218 216 438 36 46 62 22 27 36
40.40 396 446 396 268 266 238 36 46 62 17 22 36
40.45 396 446 446 268 266 288 36 46 62 17 22 36
40.50 396 446 496 268 266 338 36 46 62 22 27 36
40.55 396 496 546 268 266 388 36 46 62 17 27 36
40.60 396 496 596 268 266 438 36 46 62 22 27 36
45.45 446 546 446 318 316 288 36 46 62 17 27 36
45.50 446 546 496 318 316 338 36 56 62 22 27 36
45.55 446 546 546 318 316 388 36 56 62 22 27 36
45.60 446 546 596 318 316 438 36 56 62 22 27 36
45.70 446 546 696 318 316 538 36 56 62 27 36 36
50.50 496 596 496 368 366 322 36 56 62 27 36 36
50.55 496 596 546 368 366 372 36 56 62 27 36 36
50.60 496 596 596 368 366 422 36 56 62 27 36 36
50.70 496 596 696 368 366 522 36 56 62 27 36 36
50.80 496 596 796 324 322 598 46 76 84 27 36 46
55.55 546 646 546 418 416 372 46 76 62 27 36 46
55.60 546 646 596 418 416 422 46 76 62 27 36 46
55.70 546 646 696 418 416 522 46 76 62 27 36 46
55.80 546 646 796 374 372 598 56 76 84 27 36 56
55.90 546 646 896 374 372 698 56 76 84 27 36 56
60.60 596 696 596 468 466 422 46 76 62 27 36 46
60.70 596 696 696 424 422 498 56 76 84 27 36 56
60.80 596 696 796 424 422 598 56 76 84 27 36 56
60.90 596 696 896 424 422 698 56 76 84 27 36 56
70.70 696 796 696 524 522 498 56 76 84 27 36 56
70.80 696 796 796 524 522 598 56 76 84 27 36 56
70.90 696 796 896 524 522 698 56 76 84 27 36 56
70.100 696 796 996 524 522 798 56 76 84 27 36 56";

        // Série | D E ØD2 | F G Pf1/2 | H I ØD2 | J K Pf3 | L M ØPR | J O ØEP | W V Z   (H = 0 → "0*" do catálogo)
        private const string CoordinateTable = @"
15.15 122 122 20 122  72 M10   0  70 20  68 100 M8  40 126 10  68 136 18 15 27  66
15.20 120 156 20 126 114 M10   0 110 20  68 140 M8  40 166 10  68 172 18 15 27 102
15.25 122 212 20 122 162 M10   0 160 20  68 186 M8  40 216 10  68 224 18 15 27 150
20.20 154 152 26 154 100 M10   0 100 20  96 132 M8  62 160 10  96 166 18 20 27  88
20.25 150 202 26 148 150 M10  46 150 20  90 182 M8  56 210 10  90 216 18 20 27 138
20.30 150 244 30 150 186 M12  40 194 26  90 230 M8  56 260 10  90 266 18 20 27 172
20.35 150 294 30 150 236 M12  40 244 26  90 280 M8  56 310 10  90 314 18 20 27 222
20.40 144 344 30 144 274 M12  40 294 26  90 330 M8  56 360 10  90 364 18 20 27 260
25.25 200 200 30 200 130 M12  80 130 26 130 180 M8  90 204 12 130 214 18 20 27 116
25.30 200 244 30 200 186 M12  80 180 26 130 230 M8  90 254 12 130 264 18 20 27 172
25.35 194 294 30 194 224 M12  86 230 26 136 286 M8  96 304 12 136 324 18 20 27 210
25.40 198 340 30 198 284 M12  72 280 26 122 330 M8  82 354 12 122 364 18 20 27 270
25.45 194 394 30 194 324 M12  80 330 26 130 380 M8  90 404 12 130 414 18 20 27 310
25.50 194 444 30 194 374 M12  80 380 26 130 430 M8  90 454 12 130 464 18 20 27 360
30.30 250 244 30 250 186 M12 130 180 26 180 230 M8 140 254 14 180 266 18 20 27 172
30.35 244 294 30 244 224 M12 136 230 26 186 286 M8 146 304 14 186 324 18 20 27 210
30.40 248 340 30 248 284 M12 126 280 26 176 330 M8 136 354 14 176 364 18 20 27 270
30.45 244 394 30 244 324 M12 134 330 26 184 380 M8 144 404 14 184 414 18 20 27 310
30.50 244 444 30 244 374 M12 128 380 26 180 418 M10 140 454 14 180 464 18 20 27 360
35.35 294 294 30 294 224 M12 186 230 26 236 286 M8 196 304 14 236 324 18 22 27 210
35.40 274 316 42 274 230 M16 134 276 30 190 320 M10 148 354 14 190 366 18 22 30 212
35.45 274 374 42 274 280 M16 130 326 30 186 364 M10 144 404 14 186 414 18 22 30 262
35.50 274 416 42 274 330 M16 134 376 30 190 420 M10 148 454 14 190 466 18 22 30 312
35.55 274 474 42 274 380 M16 134 426 30 190 470 M10 148 504 14 190 516 18 22 30 362
35.60 274 524 42 274 430 M16 134 476 30 190 520 M10 148 554 14 190 566 18 22 30 412
40.40 324 324 42 324 230 M16 180 276 30 236 314 M10 194 354 16 236 364 24 22 30 212
40.45 324 374 42 324 280 M16 184 326 30 240 364 M10 198 404 16 240 414 24 22 30 262
40.50 324 424 42 324 330 M16 184 376 30 240 414 M10 198 454 16 240 460 24 22 30 312
40.55 324 466 42 324 380 M16 184 426 30 240 464 M10 198 504 16 240 510 24 22 30 362
40.60 324 524 42 324 430 M16 184 476 30 240 514 M10 198 554 16 240 560 24 22 30 412
45.45 374 374 42 374 280 M16 230 326 30 286 364 M10 244 404 16 286 414 24 25 30 262
45.50 374 424 42 374 330 M16 230 376 30 286 414 M10 244 454 16 286 464 24 25 30 312
45.55 374 474 42 374 380 M16 230 426 30 286 464 M10 244 504 16 286 514 24 25 30 362
45.60 374 524 42 374 430 M16 230 476 30 286 514 M10 244 554 16 286 564 24 25 30 412
45.70 374 624 42 374 530 M16 230 576 30 286 614 M10 244 654 16 286 664 24 25 30 512
50.50 424 424 42 424 330 M16 280 360 30 336 414 M10 286 446 20 336 464 24 25 30 312
50.55 424 474 42 424 380 M16 280 410 30 336 464 M10 286 496 20 336 514 24 25 30 362
50.60 424 524 42 424 430 M16 280 460 30 336 514 M10 286 546 20 336 564 24 25 30 412
50.70 424 624 42 424 530 M16 280 560 30 336 614 M10 286 646 20 336 664 24 25 30 512
50.80 392 692 54 392 574 M20 218 648 42 288 710 M12 236 746 20 288 764 24 25 30 552
55.55 474 474 42 474 380 M16 330 410 30 386 464 M10 336 496 20 386 514 24 25 30 362
55.60 474 524 42 474 430 M16 330 460 30 386 514 M10 336 546 20 386 564 24 25 30 412
55.70 474 624 42 474 530 M16 330 560 30 386 614 M10 336 646 20 386 664 24 25 30 512
55.80 442 692 54 442 574 M20 268 648 42 338 710 M12 286 746 20 338 764 24 25 30 552
55.90 442 792 54 442 674 M20 268 748 42 338 810 M12 286 846 20 338 864 24 25 30 652
60.60 524 524 42 524 430 M16 380 460 30 436 514 M10 386 546 20 436 564 24 25 30 412
60.70 492 592 54 492 474 M20 318 548 42 388 610 M12 336 646 20 388 664 24 25 30 452
60.80 492 692 54 492 574 M20 318 648 42 388 710 M12 336 746 20 388 764 24 25 30 552
60.90 492 792 54 492 674 M20 318 748 42 388 810 M12 336 846 20 388 864 24 25 30 652
70.70 592 592 54 592 474 M20 418 548 42 488 610 M12 436 646 20 488 664 24 25 30 452
70.80 592 692 54 592 574 M20 418 648 42 488 710 M12 436 746 20 488 764 24 25 30 552
70.90 592 792 54 592 674 M20 418 748 42 488 810 M12 436 846 20 488 864 24 25 30 652
70.100 592 892 54 592 774 M20 418 848 42 488 910 M12 436 946 20 488 964 24 25 30 752";

        // Cursos: séries → curso1/E's1 curso2/E's2 curso3/E's3 (p.7)
        private static readonly string[] StrokeTable =
        {
            "15.15 15.20 15.25 : 16/46 26/56 46/76",
            "20.20 20.25 : 13/46 23/56 43/76",
            "20.30 20.35 25.25 25.30 30.30 : 23/56 43/76 63/96",
            "20.40 25.35 25.40 25.45 25.50 30.35 30.40 30.45 30.50 35.35 : 13/56 33/76 53/96",
            "35.40 35.45 35.50 40.40 40.45 : 33/76 53/96 73/116",
            "35.55 35.60 40.50 40.60 : 33/86 63/116 83/136",
            "40.55 : 38/86 68/116 88/136",
            "45.45 : 28/76 48/96 68/116",
            "45.50 45.55 45.60 : 43/96 63/116 83/136",
            "45.70 50.50 50.55 50.60 50.70 55.55 55.60 55.70 60.60 : 29/96 49/116 69/136",
            "50.80 55.80 55.90 60.70 60.80 60.90 70.70 70.80 70.90 70.100 : 49/116 69/136 89/156",
        };

        // Espessuras de P1/P2 por faixa, na ordem da tabela de séries (p.7). A faixa de 20.40 e a de 55.55–55.70 /
        // 60.60 / 70.70 saem da ORDEM da tabela — o título da faixa não as nomeia (a confirmar com a Polimold).
        private static readonly double[] Set15 = { 22, 27, 36, 46, 56, 66 };
        private static readonly double[] Set20 = { 27, 36, 46, 56, 66, 76, 96 };
        private static readonly double[] Set35 = { 36, 46, 56, 66, 76, 96, 116 };
        private static readonly double[] Set45 = { 36, 46, 56, 66, 76, 96, 116, 136 };
        private static readonly double[] Set50 = { 56, 66, 76, 96, 116, 136, 156, 196 };

        private static readonly Lazy<IReadOnlyList<PolimoldSeries>> _all = new Lazy<IReadOnlyList<PolimoldSeries>>(Build);

        /// <summary>As 53 séries, na ordem do catálogo.</summary>
        public static IReadOnlyList<PolimoldSeries> All => _all.Value;

        public static PolimoldSeries Find(string name) =>
            All.FirstOrDefault(s => string.Equals(s.Name, (name ?? "").Trim().Replace(',', '.'), StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(s.Code, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Número da sub-série: (índice de P1 − 1)·n + índice de P2, índices de 1 a n (p.7). −1 se a espessura não é da faixa.</summary>
        public static int SubSeries(PolimoldSeries s, double p1, double p2)
        {
            int i = IndexOf(s.Thicknesses, p1), j = IndexOf(s.Thicknesses, p2);
            return i < 0 || j < 0 ? -1 : i * s.Thicknesses.Count + j + 1;
        }

        /// <summary>O inverso de <see cref="SubSeries"/>: as espessuras de P1 e P2 de um número de sub-série.</summary>
        public static bool TryDecodeSubSeries(PolimoldSeries s, int sub, out double p1, out double p2)
        {
            p1 = p2 = 0;
            int n = s.Thicknesses.Count;
            if (sub < 1 || sub > n * n) return false;
            p1 = s.Thicknesses[(sub - 1) / n];
            p2 = s.Thicknesses[(sub - 1) % n];
            return true;
        }

        /// <summary>Diâmetro nominal de "M12" → 12.</summary>
        public static double ThreadNominal(string thread)
        {
            if (string.IsNullOrWhiteSpace(thread)) return 0;
            string t = thread.Trim().TrimStart('M', 'm');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
        }

        private static int IndexOf(IReadOnlyList<double> set, double v)
        {
            for (int i = 0; i < set.Count; i++) if (Math.Abs(set[i] - v) < 1e-6) return i;
            return -1;
        }

        private static IReadOnlyList<PolimoldSeries> Build()
        {
            var list = new List<PolimoldSeries>();
            foreach (string[] f in Rows(SeriesTable))
            {
                var s = new PolimoldSeries { Name = f[0] };
                double[] v = f.Skip(1).Select(Num).ToArray();
                s.A = v[0]; s.B = v[1]; s.C = v[2]; s.P = v[3]; s.X = v[4]; s.Y = v[5];
                s.PBS = v[6]; s.PS = v[7]; s.ESP = v[8]; s.CPE = v[9]; s.PE = v[10]; s.PBI = v[11];
                list.Add(s);
            }

            var byName = list.ToDictionary(s => s.Name);
            foreach (string[] f in Rows(CoordinateTable))
            {
                PolimoldSeries s = byName[f[0]];
                s.Columns = new HolePair(Num(f[1]), Num(f[2]), Num(f[3]));
                s.MoldScrews = new HolePair(Num(f[4]), Num(f[5]), ThreadNominal(f[6]), f[6]);
                s.EjectorGuides = new HolePair(Num(f[7]), Num(f[8]), Num(f[9]));
                s.EjectorScrews = new HolePair(Num(f[10]), Num(f[11]), ThreadNominal(f[12]), f[12]);
                s.ReturnPins = new HolePair(Num(f[13]), Num(f[14]), Num(f[15]));
                s.StopButtons = new HolePair(Num(f[16]), Num(f[17]), Num(f[18]));
                s.SlotW = Num(f[19]); s.SlotV = Num(f[20]); s.SlotZ = Num(f[21]);
            }

            foreach (string line in StrokeTable)
            {
                string[] halves = line.Split(':');
                var options = halves[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select((p, i) => { string[] cs = p.Split('/'); return new StrokeOption(i + 1, Num(cs[0]), Num(cs[1])); })
                    .ToList();
                foreach (string name in halves[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    byName[name].Strokes = options;
            }

            for (int i = 0; i < list.Count; i++)
            {
                PolimoldSeries s = list[i];
                int pos = i;   // ordem da tabela
                s.Thicknesses = pos <= Index(list, "15.25") ? Set15
                              : pos <= Index(list, "35.35") ? Set20
                              : pos <= Index(list, "40.60") ? Set35
                              : pos <= Index(list, "50.70") ? Set45
                              : Set50;
            }
            return list;
        }

        private static int Index(List<PolimoldSeries> list, string name) => list.FindIndex(s => s.Name == name);

        private static IEnumerable<string[]> Rows(string table) =>
            table.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(l => l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                 .Where(f => f.Length > 0);

        private static double Num(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
