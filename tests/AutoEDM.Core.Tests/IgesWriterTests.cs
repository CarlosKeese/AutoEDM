using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Wedm;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>IGES 5.3 do perfil WEDM: formato de 80 colunas, ponteiros e coordenadas exatas.</summary>
    public class IgesWriterTests
    {
        private static readonly DateTime When = new DateTime(2026, 9, 14, 16, 0, 0);

        private static WireCurve[] Sample() => new[]
        {
            WireCurve.Line(new[] { 1.5, -2.25, 10 }, new double[] { 3, 4, 10 }),
            WireCurve.HorizontalArc(new double[] { 0, 0, 10 }, 5, new double[] { 5, 0, 10 }, new double[] { 0, 5, 10 }, new[] { 3.5355, 3.5355, 10 }),
            WireCurve.BSpline(2, new double[] { 0, 0, 0, 1, 1, 1 }, new[] { 1, Math.Sqrt(0.5), 1 },
                new[] { new double[] { 10, 0, 10 }, new double[] { 10, 10, 10 }, new double[] { 0, 10, 10 } }, 0, 1),
            WireCurve.Polyline(new[] { new double[] { 0, 0, 10 }, new double[] { 1, 0, 10 }, new double[] { 1, 1, 10 } }),
        };

        private static string[] Records(string iges) =>
            iges.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        [Fact]
        public void EveryRecordHas80Columns_AndTerminateCountsMatchSections()
        {
            string[] rec = Records(IgesWriter.Write(Sample(), "PECA Z = 10.00.igs", "Punção de teste", When));
            Assert.All(rec, r => Assert.Equal(80, r.Length));

            var counts = rec.GroupBy(r => r[72]).ToDictionary(g => g.Key, g => g.Count());
            string t = rec.Last();
            Assert.Equal('T', t[72]);
            Assert.Equal($"S{counts['S'],7}G{counts['G'],7}D{counts['D'],7}P{counts['P'],7}", t.Substring(0, 32));
            // Seções na ordem S, G, D, P, T.
            Assert.Equal("SGDPT", new string(rec.Select(r => r[72]).Distinct().ToArray()));
        }

        [Fact]
        public void LineEntity_KeepsExactCoordinatesInMillimeters()
        {
            string iges = IgesWriter.Write(Sample(), "x.igs", "", When);
            Assert.Contains("110,1.5,-2.25,10.0,3.0,4.0,10.0;", iges);
            Assert.Contains("100,10.0,0.0,0.0,5.0,0.0,0.0,5.0;", iges);

            // A seção global é quebrada em registros de 72 colunas: junta antes de procurar.
            string global = string.Concat(Records(iges).Where(r => r[72] == 'G').Select(r => r.Substring(0, 72).TrimEnd()));
            Assert.Contains(",2,2HMM,", global); // unidade: milímetro
            Assert.Contains(",11,0,", global);   // IGES 5.3
        }

        [Fact]
        public void DirectoryAndParameterPointersAgree()
        {
            string[] rec = Records(IgesWriter.Write(Sample(), "x.igs", "", When));
            string[] d = rec.Where(r => r[72] == 'D').ToArray();
            string[] p = rec.Where(r => r[72] == 'P').ToArray();

            for (int i = 0; i < d.Length; i += 2)
            {
                int deSeq = i + 1;
                int type = int.Parse(d[i].Substring(0, 8));
                int pStart = int.Parse(d[i].Substring(8, 8));
                int pLines = int.Parse(d[i + 1].Substring(24, 8));
                for (int k = 0; k < pLines; k++)
                    Assert.Equal(deSeq, int.Parse(p[pStart - 1 + k].Substring(65, 7)));
                Assert.StartsWith(type + ",", p[pStart - 1]);
            }
        }

        [Fact]
        public void Polyline_IsWrittenAsOneLinePerSegment()
        {
            var poly = WireCurve.Polyline(new[] { new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 1, 1, 0 } });
            string[] d = Records(IgesWriter.Write(new[] { poly }, "x.igs", "", When)).Where(r => r[72] == 'D').ToArray();
            Assert.Equal(4, d.Length); // 2 entidades × 2 linhas de diretório
            Assert.All(d, r => Assert.Equal("     110", r.Substring(0, 8)));
        }

        [Fact]
        public void Output_IsPlainAscii()
        {
            string iges = IgesWriter.Write(Sample(), "Punção Z = 10.00.igs", "Punção cônica — ç", When);
            Assert.All(iges, ch => Assert.True(ch < 128));
            Assert.Contains("Puncao", iges);
        }
    }
}
