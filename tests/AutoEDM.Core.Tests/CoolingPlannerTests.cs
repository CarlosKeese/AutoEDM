using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AutoEDM.Mold.Cooling;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Refrigeração (Carlos, 2026-09-24): linhas do esboço 3D → furos. O plano é puro; os casos
    /// abaixo são as redes típicas de uma placa: L, T, passada colinear e as que não se furam.
    /// </summary>
    public class CoolingPlannerTests
    {
        private static CoolingLine L(int i, double x0, double y0, double z0, double x1, double y1, double z1) =>
            new CoolingLine { Index = i, StartMm = new[] { x0, y0, z0 }, EndMm = new[] { x1, y1, z1 } };

        private static readonly PipeThread G14 = new PipeThread
        {
            Standard = "ISO Metric", SubType = "Straight Pipe Thread", Family = "G", Size = "G1/4",
            NominalMm = 13.157, TapDrillMm = 11.8
        };

        private static readonly PipeThread G18 = new PipeThread
        {
            Standard = "ISO Metric", SubType = "Straight Pipe Thread", Family = "G", Size = "G1/8",
            NominalMm = 9.728, TapDrillMm = 8.8
        };

        private static CoolingOptions Opt(double dia = 8) => new CoolingOptions { DiameterMm = dia, FittingThread = G14, PlugThread = G18 };

        /// <summary>
        /// A placa do 1º run ao vivo (MD-15335): X ±148, Y ±173, Z 0…45. Sonda de caixa — a distância
        /// do ponto até a lateral na direção pedida; ponto fora/na face = NaN.
        /// </summary>
        private static CoolingExit Box(double[] p, double[] d)
        {
            double[] min = { -148, -173, 0 }, max = { 148, 173, 45 };
            double t = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                if (System.Math.Abs(d[k]) < 1e-12) continue;
                double lim = d[k] > 0 ? max[k] : min[k];
                t = System.Math.Min(t, (lim - p[k]) / d[k]);
            }
            return new CoolingExit { DistanceMm = t <= 1e-9 ? double.NaN : t, OuterFace = true };
        }

        [Fact]
        public void CircuitoDesenhado_CantosSaoProlongadosETampados_PontasSaoEngates()
        {
            // O desenho real do Carlos (log 2026-09-24 16:29): entra em y=173, desce, esquerda, sobe,
            // direita, sai em y=173 — os cantos ficam DENTRO da placa.
            var lines = new[]
            {
                L(1, 90.85, -119.58, 21.9, -89.91, -119.58, 21.9),
                L(2, 90.85, 173, 21.9, 90.85, -119.58, 21.9),
                L(3, -89.91, -119.58, 21.9, -89.91, 119.05, 21.9),
                L(4, -89.91, 119.05, 21.9, 58.34, 119.05, 21.9),
                L(5, 58.34, 119.05, 21.9, 58.34, 173, 21.9),
            };
            CoolingPlan p = CoolingPlanner.Plan(lines, null, Opt(), Box);

            Assert.Empty(p.Problems);
            Assert.Equal(5, p.Holes.Count(h => h.Thread == null));

            // Pontas do caminho na face = engate; bocas dos prolongamentos = tampão.
            Assert.Equal(CoolingTerminal.Fitting, p.FreeEnds.Single(e => e.Key == "L2:I").Terminal);
            Assert.Equal(CoolingTerminal.Fitting, p.FreeEnds.Single(e => e.Key == "L5:F").Terminal);
            Assert.Equal(3, p.FreeEnds.Count(e => e.Extended && e.Terminal == CoolingTerminal.Plug));
            Assert.Equal(2, p.Holes.Count(h => h.Label.StartsWith("Engate G1/4")));
            Assert.Equal(3, p.Holes.Count(h => h.Label.StartsWith("Tampão G1/8")));

            // L1: canto a canto; a saída mais curta é +X (57,15 contra 58,09) → entra em x=148.
            CoolingHolePlan h1 = p.Holes.Single(h => h.Label == "Refrigeração Ø8 - L1");
            Assert.True(h1.Extended);
            Assert.Equal(148.0, h1.EntryMm[0], 6);
            Assert.Equal(148 + 89.91 + 4, h1.DepthMm, 6);        // até o canto oposto + Ø/2
            Assert.True(h1.DrillPoint);

            // L3: sai por -Y (53,42 contra 53,95).
            Assert.Equal(-173.0, p.Holes.Single(h => h.Label == "Refrigeração Ø8 - L3").EntryMm[1], 6);

            // L2 entra pelo engate em y=173 e passa do canto.
            CoolingHolePlan h2 = p.Holes.Single(h => h.Label == "Refrigeração Ø8 - L2");
            Assert.False(h2.Extended);
            Assert.Equal(173.0, h2.EntryMm[1], 6);
            Assert.Equal(173 + 119.58 + 4, h2.DepthMm, 6);
        }

        [Fact]
        public void LinhasDePontaAPonta_EntramPelaFaceESaemPelaOutra()
        {
            // O 1º desenho do Carlos: linhas atravessando a placa inteira, cruzando-se.
            var lines = new[] { L(1, -89.91, -173, 21.9, -89.91, 173, 21.9), L(2, 148, -119.58, 21.9, -148, -119.58, 21.9) };
            CoolingPlan p = CoolingPlanner.Plan(lines, null, Opt(), Box);
            Assert.Empty(p.Problems);
            CoolingHolePlan h = p.Holes.Single(x => x.Label.EndsWith("L1"));
            Assert.Equal(346.5, h.DepthMm, 6);                   // atravessa e rompe 0,5
            Assert.False(h.DrillPoint);
        }

        [Fact]
        public void Colineares_SaoUmaPassadaSo()
        {
            var lines = new[] { L(1, 0, 0, 0, 50, 0, 0), L(2, 50, 0, 0, 120, 0, 0), L(3, 50, 0, 0, 50, 60, 0) };
            var over = new Dictionary<string, CoolingTerminal> { { "L2:F", CoolingTerminal.Blind } };
            CoolingPlan p = CoolingPlanner.Plan(lines, over, Opt(10));

            Assert.Empty(p.Problems);
            CoolingHolePlan run = p.Holes.Single(h => h.Label == "Refrigeração Ø10 - L1+L2");
            Assert.Equal(new double[] { 0, 0, 0 }, run.EntryMm);
            Assert.Equal(120.0, run.DepthMm, 6);                 // termina cego na ponta desenhada
            Assert.True(run.DrillPoint);

            CoolingHolePlan t = p.Holes.Single(h => h.Label == "Refrigeração Ø10 - L3");
            Assert.Equal(new double[] { 50, 60, 0 }, t.EntryMm);
            Assert.Equal(65.0, t.DepthMm, 6);                    // 60 + Ø/2 no cruzamento em T
        }

        [Fact]
        public void Passante_FundoRetoRompendoAFace()
        {
            var lines = new[] { L(1, 0, 0, 0, 0, 0, 90) };
            var over = new Dictionary<string, CoolingTerminal> { { "L1:I", CoolingTerminal.Through }, { "L1:F", CoolingTerminal.Through } };
            CoolingHolePlan h = Assert.Single(CoolingPlanner.Plan(lines, over, Opt(6)).Holes);
            Assert.Equal(90.5, h.DepthMm, 6);
            Assert.False(h.DrillPoint);
        }

        [Fact]
        public void SemMedirAPeca_QuadradoFechadoEhProblema()
        {
            var lines = new[]
            {
                L(1, 0, 0, 0, 50, 0, 0), L(2, 50, 0, 0, 50, 50, 0), L(3, 50, 50, 0, 0, 50, 0), L(4, 0, 50, 0, 0, 0, 0),
            };
            CoolingPlan p = CoolingPlanner.Plan(lines, null, Opt());
            Assert.Empty(p.Holes);
            Assert.Equal(4, p.Problems.Count);
        }

        [Fact]
        public void EntradaMarcadaCega_EhProblema()
        {
            var lines = new[] { L(1, 0, 0, 0, 40, 0, 0), L(2, 40, 0, 0, 40, 40, 0) };
            var over = new Dictionary<string, CoolingTerminal> { { "L1:I", CoolingTerminal.Blind } };
            CoolingPlan p = CoolingPlanner.Plan(lines, over, Opt());
            Assert.Contains(p.Problems, s => s.StartsWith("L1:"));
            Assert.Contains(p.Holes, h => h.Label.EndsWith("- L2"));
        }

        [Fact]
        public void SobrefuroExplicito()
        {
            var lines = new[] { L(1, 100, 0, 0, 0, 0, 0), L(2, 0, 0, 0, 0, 80, 0) };
            var opt = Opt(); opt.OvershootMm = 1.5;
            Assert.Equal(101.5, CoolingPlanner.Plan(lines, null, opt).Holes.Single(h => h.Label.EndsWith("L1")).DepthMm, 6);
        }

        [Fact]
        public void EngateSemRosca_EhProblemaMasOCanalSai()
        {
            var lines = new[] { L(1, 0, 0, 0, 30, 0, 0) };
            CoolingPlan p = CoolingPlanner.Plan(lines, null, new CoolingOptions());
            Assert.Single(p.Holes);
            Assert.Equal(2, p.Problems.Count);
        }

        // ------------------------------------------------------------ base de furos

        [Fact]
        public void BaseDeFuros_LeRoscasDeTuboComAStringExata()
        {
            string dir = Path.Combine(Path.GetTempPath(), "autoedm-holes-" + System.Guid.NewGuid());
            Directory.CreateDirectory(dir);
            try
            {
                WriteXlsx(Path.Combine(dir, "ANSI Inch.xlsx"), new[]
                {
                    new[] { "Thread Type", "Sub Type", "ThreadFamily", "Size", "Nominal Diameter", "Tap Drill Diameter" },
                    new[] { "1", "Standard Thread", "UNC", "1/4-20 UNC", "0.25", "0.201" },
                    new[] { "3", "Tapered Pipe Thread", "NPT", " 1/8-27 NPT      ", "", "0.339" },
                });
                List<PipeThread> t = HoleDatabase.ReadPipeThreads(dir);
                PipeThread npt = Assert.Single(t);
                Assert.Equal(" 1/8-27 NPT      ", npt.Size);          // com os espaços: é assim que o AddEx casa
                Assert.Equal("ANSI Inch", npt.Standard);
                Assert.True(npt.Tapered);
                Assert.Equal(8.6106, npt.TapDrillMm, 3);            // polegada → mm
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void BaseDeFuros_DaInstalacao_SeExistir()
        {
            string folder = HoleDatabase.FindFolder();
            if (folder == null || !File.Exists(Path.Combine(folder, "ISO Metric.xlsx"))) return;   // máquina sem SE
            List<PipeThread> t = HoleDatabase.ReadPipeThreadsFromFile(Path.Combine(folder, "ISO Metric.xlsx"));
            PipeThread g = t.Single(x => x.Size == "G1/4");
            Assert.Equal("Straight Pipe Thread", g.SubType);
            Assert.Equal(11.8, g.TapDrillMm, 3);
            Assert.Contains(t, x => x.Size == "R 1/8-28" && x.Tapered);
        }

        /// <summary>Um .xlsx mínimo, com strings compartilhadas, só com a aba "Threaded".</summary>
        private static void WriteXlsx(string path, string[][] rows)
        {
            var strings = rows.SelectMany(r => r).Distinct().ToList();
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                void Put(string name, string xml)
                {
                    using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(xml);
                }
                const string m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                const string r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                Put("xl/workbook.xml", $"<workbook xmlns=\"{m}\" xmlns:r=\"{r}\"><sheets><sheet name=\"Threaded\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Put("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                Put("xl/sharedStrings.xml", $"<sst xmlns=\"{m}\">" + string.Concat(strings.Select(s => $"<si><t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(s)}</t></si>")) + "</sst>");
                var sb = new StringBuilder($"<worksheet xmlns=\"{m}\"><sheetData>");
                for (int i = 0; i < rows.Length; i++)
                {
                    sb.Append("<row>");
                    for (int j = 0; j < rows[i].Length; j++)
                        if (rows[i][j] != "") sb.Append($"<c r=\"{(char)('A' + j)}{i + 1}\" t=\"s\"><v>{strings.IndexOf(rows[i][j])}</v></c>");
                    sb.Append("</row>");
                }
                Put("xl/worksheets/sheet1.xml", sb.Append("</sheetData></worksheet>").ToString());
            }
        }
    }
}
