using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AutoEDM.Electrode;
using AutoEDM.Reporting;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>"Copiar para impressão" da Lista de corte: texto e HTML da área de transferência.</summary>
    public class SawCutReportFormatterTests
    {
        private static readonly DateTime When = new DateTime(2026, 9, 14, 10, 30, 0);

        private static SawCutListItem[] Items()
        {
            var lib = new StandardBlankLibrary();
            var ok = new SawCutListItem { FileName = "15142.200_EDM_EE01.par", Positions = 4, SizeKnown = true, SizeXmm = 50, SizeYmm = 19, SizeZmm = 42.3 };
            ok.Cut = SawCutPlanner.Identify(50, 19, 42.3, null, lib.Catalog);
            var missing = new SawCutListItem { FileName = "Eletrodo <ção>.par", Positions = 1 };
            return new[] { ok, missing };
        }

        [Fact]
        public void Text_HasFileProfilePositionsAndCut()
        {
            string text = SawCutReportFormatter.ToText(Items(), "15142.200_EDM.asm", When);
            Assert.Contains("15142.200_EDM.asm", text);
            Assert.Contains("14/09/2026 10:30", text);
            Assert.Contains("5 mm de sobremetal", text);
            Assert.Matches(new Regex(@"15142\.200_EDM_EE01\.par\s+4\s+RET\. 50 x 19\s+em pé\s+8844\s+48"), text);
            Assert.Contains("NÃO IDENTIFICADO", text);
            Assert.Contains("Total: 2 arquivo(s), 5 posição(ões)", text);
            Assert.Contains("1 eletrodo(s) sem medida de corte", text);
        }

        [Fact]
        public void Footer_WarnsWithPrintableTextNotSymbol()
        {
            string[] footer = SawCutReportFormatter.FooterLines(Items());
            Assert.Equal(2, footer.Length);
            Assert.StartsWith("ATENÇÃO: 1 eletrodo(s)", footer[1]);
            Assert.DoesNotContain("⚠", string.Join("\n", footer));
        }

        [Fact]
        public void ClipboardHtmlBytes_AreUtf8WithoutBom()
        {
            byte[] bytes = SawCutReportFormatter.ToClipboardHtmlBytes("<b>Posições em pé</b>");
            Assert.Equal((byte)'V', bytes[0]); // sem BOM: o cabeçalho "Version:" começa no byte 0
            Assert.Contains("Posições em pé", Encoding.UTF8.GetString(bytes));
        }

        [Fact]
        public void Html_EscapesFileNames()
        {
            string html = SawCutReportFormatter.ToHtmlFragment(Items(), null, When);
            Assert.Contains("Eletrodo &lt;ção&gt;.par", html);
            Assert.DoesNotContain("<ção>", html);
        }

        [Fact]
        public void ClipboardHtml_OffsetsAreUtf8Bytes()
        {
            string fragment = SawCutReportFormatter.ToHtmlFragment(Items(), "Montagem ç", When);
            string cf = SawCutReportFormatter.ToClipboardHtml(fragment);
            byte[] bytes = Encoding.UTF8.GetBytes(cf);

            int Offset(string key) => int.Parse(Regex.Match(cf, key + @":(\d{10})").Groups[1].Value, CultureInfo.InvariantCulture);
            int startHtml = Offset("StartHTML"), endHtml = Offset("EndHTML");
            int startFragment = Offset("StartFragment"), endFragment = Offset("EndFragment");

            Assert.StartsWith("<html>", Encoding.UTF8.GetString(bytes, startHtml, 6));
            Assert.Equal(bytes.Length, endHtml);
            Assert.Equal(fragment, Encoding.UTF8.GetString(bytes, startFragment, endFragment - startFragment));
        }
    }
}
