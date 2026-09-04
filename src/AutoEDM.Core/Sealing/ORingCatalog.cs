using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AutoEDM.Diagnostics;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Catálogo de anéis O'ring — ISO 3601-1 SÉRIE G (a série em polegada, equivalente às
    /// medidas AS568), que foi a escolhida pelo Carlos.
    ///
    /// ARQUIVO DE TEXTO, não JSON, de propósito: o <c>System.Text.Json</c> NÃO INICIALIZA
    /// dentro do Solid Edge (net472 in-process) — o log de 2026-09-03 mostra o config.json
    /// falhando exatamente assim. Um `.txt` de `;` também é o que o Carlos consegue colar
    /// direto da planilha do fornecedor, e é o mesmo formato que o próprio SE usa no
    /// HOLES.TXT. Local: <c>%LOCALAPPDATA%\AutoEDM\oring-catalog.txt</c>, criado com a tabela
    /// embutida na primeira vez.
    ///
    /// PROCEDÊNCIA DOS NÚMEROS: a tabela embutida é TRANSCRITA do catálogo Parker
    /// "O'Rings 001-5 BR" (junho/2009), que declara "medidas e tolerâncias conforme norma
    /// SAE AS 568-A" — extraída do PDF por script e conferida por amostragem. São 349 medidas,
    /// d1 de 0,74 a 658,88 mm, e saem marcadas como CONFERIDAS.
    ///
    /// Ainda assim, trocar por SUA lista de estoque continua valendo a pena: aí o "anel mais
    /// próximo" passa a ser um anel que existe na sua gaveta, e não só um que existe na norma.
    ///
    /// A COTA DO CANAL NÃO DEPENDE DESTA TABELA: ela é calculada a partir do d2 e do diâmetro
    /// MEDIDO na peça (<see cref="ORingGrooveCalculator"/>). O catálogo só responde "qual anel
    /// comprar" — se ele estiver incompleto, o canal ainda sai certo para o anel escolhido.
    /// </summary>
    public sealed class ORingCatalog
    {
        public const string FileName = "oring-catalog.txt";

        /// <summary>Seções (d2, mm) da série G da ISO 3601-1. Estes valores são certos.</summary>
        public static readonly double[] SeriesGCrossSections = { 1.78, 2.62, 3.53, 5.33, 6.99 };

        private readonly List<ORingSize> _sizes;

        public ORingCatalog(IEnumerable<ORingSize> sizes)
        {
            _sizes = (sizes ?? Enumerable.Empty<ORingSize>())
                .Where(s => s != null && s.InnerDiameter > 0 && s.CrossSection > 0)
                .OrderBy(s => s.CrossSection).ThenBy(s => s.InnerDiameter)
                .ToList();
        }

        public IReadOnlyList<ORingSize> Sizes => _sizes;

        public int Count => _sizes.Count;

        /// <summary>Seções distintas que o catálogo realmente cobre (mm), em ordem.</summary>
        public IReadOnlyList<double> CrossSections =>
            _sizes.Select(s => s.CrossSection).Distinct().OrderBy(v => v).ToList();

        /// <summary>Anéis de uma seção. <paramref name="crossSection"/> casa por proximidade
        /// (0,01 mm) para não depender de arredondamento na escrita do arquivo.</summary>
        public IEnumerable<ORingSize> WithCrossSection(double crossSection) =>
            _sizes.Where(s => Math.Abs(s.CrossSection - crossSection) < 0.01);

        // ------------------------------------------------------------------ carga

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "AutoEDM", FileName);

        /// <summary>
        /// Lê o catálogo do disco; se não existir, GRAVA a tabela embutida lá e usa ela. Nunca
        /// lança — sem catálogo o AutoEDM ainda calcula o canal e informa o d1 necessário.
        /// </summary>
        public static ORingCatalog LoadOrCreateDefault(string path = null)
        {
            path = path ?? DefaultPath;
            try
            {
                if (File.Exists(path))
                {
                    var loaded = Parse(File.ReadAllLines(path));
                    Log.Info($"Catálogo de O'ring: {loaded.Count} medida(s) de {path}.");
                    return loaded;
                }
            }
            catch (Exception e) { Log.Warn($"Catálogo de O'ring: falha ao ler {path} ({e.GetBaseException().Message}); usando a tabela embutida."); }

            var builtIn = BuiltInSeriesG();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Render(builtIn), new UTF8Encoding(false));
                Log.Info($"Catálogo de O'ring criado em {path} ({builtIn.Count} medidas, TODAS a conferir).");
            }
            catch (Exception e) { Log.Warn("Catálogo de O'ring: não deu para gravar o arquivo: " + e.GetBaseException().Message); }
            return builtIn;
        }

        /// <summary>
        /// Formato de linha: <c>d1 ; d2 ; código ; conferido</c> (mm, ponto ou vírgula
        /// decimal). Linhas em branco e as que começam com <c>\\</c> ou <c>#</c> são ignoradas
        /// — mesmo estilo do HOLES.TXT do Solid Edge. Uma linha ruim é pulada com aviso, nunca
        /// derruba o catálogo inteiro.
        /// </summary>
        public static ORingCatalog Parse(IEnumerable<string> lines)
        {
            var sizes = new List<ORingSize>();
            int lineNo = 0;
            foreach (var raw in lines ?? Enumerable.Empty<string>())
            {
                lineNo++;
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("\\") || line.StartsWith("#")) continue;

                var f = line.Split(';');
                if (f.Length < 2) { Log.Warn($"Catálogo de O'ring, linha {lineNo}: menos de 2 campos — pulada."); continue; }
                double d1, d2;
                if (!TryNumber(f[0], out d1) || !TryNumber(f[1], out d2) || d1 <= 0 || d2 <= 0)
                { Log.Warn($"Catálogo de O'ring, linha {lineNo}: d1/d2 inválidos ('{line}') — pulada."); continue; }

                double tol;
                if (f.Length < 5 || !TryNumber(f[4], out tol)) tol = 0.0;

                sizes.Add(new ORingSize
                {
                    InnerDiameter = d1,
                    CrossSection = d2,
                    Series = "G",
                    Code = f.Length > 2 ? f[2].Trim() : null,
                    Verified = f.Length > 3 && IsTruthy(f[3]),
                    Tolerance = tol
                });
            }
            return new ORingCatalog(sizes);
        }

        /// <summary>Aceita vírgula OU ponto como separador decimal — o arquivo vai ser editado
        /// à mão numa máquina pt-BR e colado de planilha, então os dois têm de passar.</summary>
        private static bool TryNumber(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static bool IsTruthy(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s == "1" || s == "sim" || s == "s" || s == "true" || s == "x" || s == "ok";
        }

        public static string Render(ORingCatalog catalog)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\\\\ Catálogo de anéis O'ring do AutoEDM — ISO 3601-1 série G (polegada / AS568).");
            sb.AppendLine("\\\\");
            sb.AppendLine("\\\\ Formato:  d1 ; d2 ; codigo ; conferido ; tolerancia");
            sb.AppendLine("\\\\   d1 = diâmetro INTERNO do anel (mm)   d2 = seção do cordão (mm)");
            sb.AppendLine("\\\\   codigo    = referência do fornecedor/AS568 (opcional)");
            sb.AppendLine("\\\\   conferido = 1/sim quando você já bateu a medida com o catálogo do fornecedor");
            sb.AppendLine("\\\\ Decimal com ponto ou vírgula. Linhas começando com \\\\ ou # são comentário.");
            sb.AppendLine("\\\\");
            sb.AppendLine("\\\\ Tabela de fábrica: catálogo Parker \"O'Rings 001-5 BR\" (jun/2009), páginas 6 e 7,");
            sb.AppendLine("\\\\ que declara \"medidas e tolerâncias conforme norma SAE AS 568-A\". 349 medidas.");
            sb.AppendLine("\\\\ Troque por sua lista de ESTOQUE se quiser que o AutoEDM só ofereça anel que você tem.");
            sb.AppendLine();
            double last = -1;
            foreach (var s in catalog.Sizes)
            {
                if (Math.Abs(s.CrossSection - last) > 0.01)
                {
                    last = s.CrossSection;
                    sb.AppendLine();
                    sb.AppendLine($"\\\\ ---- seção d2 = {s.CrossSection.ToString("0.00", CultureInfo.InvariantCulture)} mm ----");
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,8:0.00} ; {1,5:0.00} ; {2,-6} ; {3} ; {4:0.00}",
                    s.InnerDiameter, s.CrossSection, s.Code ?? "", s.Verified ? "1" : "0", s.Tolerance));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ tabela embutida

        /// <summary>
        /// Tabela AS568 / ISO 3601-1 série G, TRANSCRITA do catálogo Parker "O'Rings 001-5 BR"
        /// (junho/2009), páginas 6 e 7 — o próprio catálogo diz "medidas e tolerâncias conforme
        /// norma SAE AS 568-A". Extraída do PDF por script, não digitada, e conferida por
        /// amostragem contra a página impressa (ver ORingGrooveTests).
        ///
        /// Campos: d1 ; d2 ; código Parker ; conferido ; tolerância de d1 (±mm).
        /// As 349 linhas cobrem d1 de 0,74 a 658,88 mm nas seções 1,02 / 1,27 / 1,52 (os três
        /// códigos especiais 2-001..003) e 1,78 / 2,62 / 3,53 / 5,33 / 6,99 mm.
        ///
        /// POR QUE ISTO É LITERAL E NÃO UMA PROGRESSÃO. A versão anterior gerava os d1 pelo
        /// passo das séries AS568 (1/32", 1/16", 1/8", 1/4"). Funcionava no começo de cada
        /// série e DESANDAVA onde o passo muda sem aviso: o 2-246 saía Ø75,79 quando o real é
        /// Ø113,89 — 38 mm de erro, num número que vira canal usinado. Tabela é tabela.
        /// </summary>
        private const string SeriesGTable = @"
0.74;1.02;2-001;1;0.10
1.07;1.27;2-002;1;0.10
1.42;1.52;2-003;1;0.10
1.78;1.78;2-004;1;0.13
2.57;1.78;2-005;1;0.13
2.90;1.78;2-006;1;0.13
3.68;1.78;2-007;1;0.13
4.47;1.78;2-008;1;0.13
5.28;1.78;2-009;1;0.13
6.07;1.78;2-010;1;0.13
7.65;1.78;2-011;1;0.13
9.25;1.78;2-012;1;0.13
10.82;1.78;2-013;1;0.13
12.42;1.78;2-014;1;0.13
14.00;1.78;2-015;1;0.18
15.60;1.78;2-016;1;0.23
17.17;1.78;2-017;1;0.23
18.77;1.78;2-018;1;0.23
20.35;1.78;2-019;1;0.23
21.95;1.78;2-020;1;0.23
23.52;1.78;2-021;1;0.23
25.12;1.78;2-022;1;0.25
26.70;1.78;2-023;1;0.25
28.30;1.78;2-024;1;0.25
29.87;1.78;2-025;1;0.28
31.47;1.78;2-026;1;0.28
33.05;1.78;2-027;1;0.28
34.65;1.78;2-028;1;0.33
37.82;1.78;2-029;1;0.33
41.00;1.78;2-030;1;0.33
44.17;1.78;2-031;1;0.38
47.35;1.78;2-032;1;0.38
50.52;1.78;2-033;1;0.46
53.70;1.78;2-034;1;0.46
56.87;1.78;2-035;1;0.46
60.05;1.78;2-036;1;0.46
63.22;1.78;2-037;1;0.46
66.40;1.78;2-038;1;0.51
69.57;1.78;2-039;1;0.51
72.75;1.78;2-040;1;0.51
75.92;1.78;2-041;1;0.61
82.27;1.78;2-042;1;0.61
88.62;1.78;2-043;1;0.61
94.97;1.78;2-044;1;0.69
101.32;1.78;2-045;1;0.69
107.67;1.78;2-046;1;0.76
114.02;1.78;2-047;1;0.76
120.37;1.78;2-048;1;0.76
126.72;1.78;2-049;1;0.94
133.07;1.78;2-050;1;0.94
1.24;2.62;2-102;1;0.13
2.06;2.62;2-103;1;0.13
2.84;2.62;2-104;1;0.13
3.63;2.62;2-105;1;0.13
4.42;2.62;2-106;1;0.13
5.23;2.62;2-107;1;0.13
6.02;2.62;2-108;1;0.13
7.59;2.62;2-109;1;0.13
9.19;2.62;2-110;1;0.13
10.77;2.62;2-111;1;0.13
12.37;2.62;2-112;1;0.13
13.94;2.62;2-113;1;0.18
15.54;2.62;2-114;1;0.23
17.12;2.62;2-115;1;0.23
18.72;2.62;2-116;1;0.23
20.30;2.62;2-117;1;0.25
21.89;2.62;2-118;1;0.25
23.47;2.62;2-119;1;0.25
25.07;2.62;2-120;1;0.25
26.64;2.62;2-121;1;0.25
28.24;2.62;2-122;1;0.25
29.82;2.62;2-123;1;0.30
31.42;2.62;2-124;1;0.30
32.99;2.62;2-125;1;0.30
34.59;2.62;2-126;1;0.30
36.17;2.62;2-127;1;0.30
37.77;2.62;2-128;1;0.30
39.34;2.62;2-129;1;0.38
40.94;2.62;2-130;1;0.38
42.52;2.62;2-131;1;0.38
44.12;2.62;2-132;1;0.38
45.69;2.62;2-133;1;0.38
47.29;2.62;2-134;1;0.38
48.90;2.62;2-135;1;0.43
50.47;2.62;2-136;1;0.43
52.07;2.62;2-137;1;0.43
53.64;2.62;2-138;1;0.43
55.25;2.62;2-139;1;0.43
56.82;2.62;2-140;1;0.43
58.42;2.62;2-141;1;0.51
59.99;2.62;2-142;1;0.51
61.60;2.62;2-143;1;0.51
63.17;2.62;2-144;1;0.51
64.77;2.62;2-145;1;0.51
66.34;2.62;2-146;1;0.51
67.95;2.62;2-147;1;0.56
69.52;2.62;2-148;1;0.56
71.12;2.62;2-149;1;0.56
72.69;2.62;2-150;1;0.56
75.87;2.62;2-151;1;0.61
82.22;2.62;2-152;1;0.61
88.57;2.62;2-153;1;0.61
94.92;2.62;2-154;1;0.71
101.27;2.62;2-155;1;0.71
107.62;2.62;2-156;1;0.76
113.97;2.62;2-157;1;0.76
120.32;2.62;2-158;1;0.76
126.67;2.62;2-159;1;0.89
133.02;2.62;2-160;1;0.89
139.37;2.62;2-161;1;0.89
145.72;2.62;2-162;1;0.89
152.07;2.62;2-163;1;0.89
158.42;2.62;2-164;1;1.02
164.77;2.62;2-165;1;1.02
171.12;2.62;2-166;1;1.02
177.47;2.62;2-167;1;1.02
183.82;2.62;2-168;1;1.14
190.17;2.62;2-169;1;1.14
196.52;2.62;2-170;1;1.14
202.87;2.62;2-171;1;1.14
209.22;2.62;2-172;1;1.27
215.57;2.62;2-173;1;1.27
221.92;2.62;2-174;1;1.27
228.27;2.62;2-175;1;1.27
234.62;2.62;2-176;1;1.40
240.97;2.62;2-177;1;1.40
247.32;2.62;2-178;1;1.40
4.34;3.53;2-201;1;0.13
5.94;3.53;2-202;1;0.13
7.52;3.53;2-203;1;0.13
9.12;3.53;2-204;1;0.13
10.69;3.53;2-205;1;0.13
12.29;3.53;2-206;1;0.13
13.87;3.53;2-207;1;0.18
15.47;3.53;2-208;1;0.23
17.04;3.53;2-209;1;0.23
18.64;3.53;2-210;1;0.25
20.22;3.53;2-211;1;0.25
21.82;3.53;2-212;1;0.25
23.39;3.53;2-213;1;0.25
24.99;3.53;2-214;1;0.25
26.57;3.53;2-215;1;0.25
28.17;3.53;2-216;1;0.30
29.74;3.53;2-217;1;0.30
31.34;3.53;2-218;1;0.30
32.92;3.53;2-219;1;0.30
34.52;3.53;2-220;1;0.30
36.09;3.53;2-221;1;0.30
37.69;3.53;2-222;1;0.38
40.87;3.53;2-223;1;0.38
44.04;3.53;2-224;1;0.38
47.22;3.53;2-225;1;0.46
50.39;3.53;2-226;1;0.46
53.57;3.53;2-227;1;0.46
56.74;3.53;2-228;1;0.51
59.92;3.53;2-229;1;0.51
63.09;3.53;2-230;1;0.51
66.27;3.53;2-231;1;0.51
69.44;3.53;2-232;1;0.61
72.62;3.53;2-233;1;0.61
75.79;3.53;2-234;1;0.61
78.97;3.53;2-235;1;0.61
82.14;3.53;2-236;1;0.61
85.32;3.53;2-237;1;0.61
88.49;3.53;2-238;1;0.61
91.67;3.53;2-239;1;0.71
94.84;3.53;2-240;1;0.71
98.02;3.53;2-241;1;0.71
101.19;3.53;2-242;1;0.71
104.37;3.53;2-243;1;0.71
107.54;3.53;2-244;1;0.76
110.72;3.53;2-245;1;0.76
113.89;3.53;2-246;1;0.76
117.07;3.53;2-247;1;0.76
120.24;3.53;2-248;1;0.76
123.42;3.53;2-249;1;0.89
126.59;3.53;2-250;1;0.89
129.77;3.53;2-251;1;0.89
132.94;3.53;2-252;1;0.89
136.12;3.53;2-253;1;0.89
139.29;3.53;2-254;1;0.89
142.47;3.53;2-255;1;0.89
145.64;3.53;2-256;1;0.89
148.82;3.53;2-257;1;0.89
151.99;3.53;2-258;1;0.89
158.34;3.53;2-259;1;1.02
164.69;3.53;2-260;1;1.02
171.04;3.53;2-261;1;1.02
177.39;3.53;2-262;1;1.02
183.74;3.53;2-263;1;1.14
190.09;3.53;2-264;1;1.14
196.44;3.53;2-265;1;1.14
202.79;3.53;2-266;1;1.14
209.14;3.53;2-267;1;1.27
215.49;3.53;2-268;1;1.27
221.84;3.53;2-269;1;1.27
228.19;3.53;2-270;1;1.27
234.54;3.53;2-271;1;1.40
240.89;3.53;2-272;1;1.40
247.24;3.53;2-273;1;1.40
253.59;3.53;2-274;1;1.40
266.29;3.53;2-275;1;1.40
278.99;3.53;2-276;1;1.65
291.69;3.53;2-277;1;1.65
304.39;3.53;2-278;1;1.65
329.79;3.53;2-279;1;1.65
355.19;3.53;2-280;1;1.65
380.59;3.53;2-281;1;1.65
405.26;3.53;2-282;1;1.91
430.66;3.53;2-283;1;2.03
456.06;3.53;2-284;1;2.16
10.46;5.33;2-309;1;0.13
12.07;5.33;2-310;1;0.13
13.64;5.33;2-311;1;0.18
15.24;5.33;2-312;1;0.23
16.81;5.33;2-313;1;0.23
18.42;5.33;2-314;1;0.25
19.99;5.33;2-315;1;0.25
21.59;5.33;2-316;1;0.25
23.16;5.33;2-317;1;0.25
24.77;5.33;2-318;1;0.25
26.34;5.33;2-319;1;0.25
27.94;5.33;2-320;1;0.30
29.51;5.33;2-321;1;0.30
31.12;5.33;2-322;1;0.30
32.69;5.33;2-323;1;0.30
34.29;5.33;2-324;1;0.30
37.47;5.33;2-325;1;0.38
40.64;5.33;2-326;1;0.38
43.82;5.33;2-327;1;0.38
46.99;5.33;2-328;1;0.38
50.17;5.33;2-329;1;0.46
53.34;5.33;2-330;1;0.46
56.52;5.33;2-331;1;0.46
59.69;5.33;2-332;1;0.46
62.87;5.33;2-333;1;0.51
66.04;5.33;2-334;1;0.51
69.22;5.33;2-335;1;0.51
72.39;5.33;2-336;1;0.51
75.57;5.33;2-337;1;0.61
78.74;5.33;2-338;1;0.61
81.92;5.33;2-339;1;0.61
85.09;5.33;2-340;1;0.61
88.27;5.33;2-341;1;0.61
91.44;5.33;2-342;1;0.71
94.62;5.33;2-343;1;0.71
97.79;5.33;2-344;1;0.71
100.97;5.33;2-345;1;0.71
104.14;5.33;2-346;1;0.71
107.32;5.33;2-347;1;0.76
110.49;5.33;2-348;1;0.76
113.67;5.33;2-349;1;0.76
116.84;5.33;2-350;1;0.76
120.02;5.33;2-351;1;0.76
123.19;5.33;2-352;1;0.76
126.37;5.33;2-353;1;0.94
129.54;5.33;2-354;1;0.94
132.72;5.33;2-355;1;0.94
135.89;5.33;2-356;1;0.94
139.07;5.33;2-357;1;0.94
142.24;5.33;2-358;1;0.94
145.42;5.33;2-359;1;0.94
148.59;5.33;2-360;1;0.94
151.77;5.33;2-361;1;0.94
158.12;5.33;2-362;1;1.02
164.47;5.33;2-363;1;1.02
170.82;5.33;2-364;1;1.02
177.17;5.33;2-365;1;1.02
183.52;5.33;2-366;1;1.14
189.87;5.33;2-367;1;1.14
196.22;5.33;2-368;1;1.14
202.57;5.33;2-369;1;1.14
208.92;5.33;2-370;1;1.27
215.27;5.33;2-371;1;1.27
221.62;5.33;2-372;1;1.27
227.97;5.33;2-373;1;1.27
234.32;5.33;2-374;1;1.40
240.67;5.33;2-375;1;1.40
247.02;5.33;2-376;1;1.40
253.37;5.33;2-377;1;1.40
266.07;5.33;2-378;1;1.52
278.77;5.33;2-379;1;1.52
291.47;5.33;2-380;1;1.65
304.17;5.33;2-381;1;1.65
329.57;5.33;2-382;1;1.65
354.97;5.33;2-383;1;1.78
380.37;5.33;2-384;1;1.78
405.26;5.33;2-385;1;1.91
430.66;5.33;2-386;1;2.03
456.06;5.33;2-387;1;2.16
481.41;5.33;2-388;1;2.29
506.81;5.33;2-389;1;2.41
532.21;5.33;2-390;1;2.41
557.61;5.33;2-391;1;2.54
582.68;5.33;2-392;1;2.67
608.08;5.33;2-393;1;2.79
633.48;5.33;2-394;1;2.92
658.88;5.33;2-395;1;3.05
113.67;6.99;2-425;1;0.84
116.84;6.99;2-426;1;0.84
120.02;6.99;2-427;1;0.84
123.19;6.99;2-428;1;0.84
126.37;6.99;2-429;1;0.94
129.54;6.99;2-430;1;0.94
132.72;6.99;2-431;1;0.94
135.89;6.99;2-432;1;0.94
139.07;6.99;2-433;1;0.94
142.24;6.99;2-434;1;0.94
145.42;6.99;2-435;1;0.94
148.59;6.99;2-436;1;0.94
151.77;6.99;2-437;1;0.94
158.12;6.99;2-438;1;1.02
164.47;6.99;2-439;1;1.02
170.82;6.99;2-440;1;1.02
177.17;6.99;2-441;1;1.02
183.52;6.99;2-442;1;1.14
189.87;6.99;2-443;1;1.14
196.22;6.99;2-444;1;1.14
202.57;6.99;2-445;1;1.14
215.27;6.99;2-446;1;1.40
227.97;6.99;2-447;1;1.40
240.67;6.99;2-448;1;1.40
253.37;6.99;2-449;1;1.40
266.07;6.99;2-450;1;1.52
278.77;6.99;2-451;1;1.52
291.47;6.99;2-452;1;1.52
304.17;6.99;2-453;1;1.52
316.87;6.99;2-454;1;1.52
329.57;6.99;2-455;1;1.52
342.27;6.99;2-456;1;1.78
354.97;6.99;2-457;1;1.78
367.67;6.99;2-458;1;1.78
380.37;6.99;2-459;1;1.78
393.07;6.99;2-460;1;1.78
405.26;6.99;2-461;1;1.91
417.96;6.99;2-462;1;1.91
430.66;6.99;2-463;1;2.03
443.36;6.99;2-464;1;2.16
456.06;6.99;2-465;1;2.16
468.76;6.99;2-466;1;2.16
481.46;6.99;2-467;1;2.29
494.16;6.99;2-468;1;2.29
506.86;6.99;2-469;1;2.41
532.26;6.99;2-470;1;2.41
557.66;6.99;2-471;1;2.54
582.68;6.99;2-472;1;2.67
608.08;6.99;2-473;1;2.79
633.48;6.99;2-474;1;2.92
658.88;6.99;2-475;1;3.05
";

        /// <summary>Catálogo de fábrica — a tabela AS568 acima, já pronta para uso.</summary>
        public static ORingCatalog BuiltInSeriesG() =>
            Parse(SeriesGTable.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
