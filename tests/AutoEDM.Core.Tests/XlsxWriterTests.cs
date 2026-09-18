using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AutoEDM.Reporting;
using AutoEDM.Reporting.Xlsx;
using AutoEDM.Revisions;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// O .xlsx escrito à mão (Carlos, 2026-09-18): um .zip de XMLs. Os testes abrem o pacote e
    /// conferem as partes — é o que dá para verificar sem o Excel instalado.
    /// </summary>
    public class XlsxWriterTests
    {
        [Theory]
        [InlineData(1, "A")]
        [InlineData(26, "Z")]
        [InlineData(27, "AA")]
        [InlineData(52, "AZ")]
        [InlineData(53, "BA")]
        public void ColumnName_FollowsTheSpreadsheetAlphabet(int column, string expected)
        {
            Assert.Equal(expected, XlsxSheet.ColumnName(column));
            Assert.Equal(expected + "7", XlsxSheet.CellRef(7, column));
        }

        [Fact]
        public void SheetName_DropsWhatExcelRefuses()
        {
            Assert.Equal("Rev-2", XlsxSheet.SafeName("Rev/2"));
            Assert.Equal(31, XlsxSheet.SafeName(new string('x', 40)).Length);
            Assert.Equal("Planilha1", XlsxSheet.SafeName("   "));
        }

        [Fact]
        public void PngSize_ComesFromTheHeader_NotFromLoadingTheImage()
        {
            byte[] png = FakePng(320, 240);
            int w, h;
            Assert.True(XlsxWriter.TryReadPngSize(png, out w, out h));
            Assert.Equal(320, w);
            Assert.Equal(240, h);

            Assert.False(XlsxWriter.TryReadPngSize(new byte[] { 1, 2, 3 }, out w, out h));
            Assert.False(XlsxWriter.TryReadPngSize(null, out w, out h));
        }

        [Fact]
        public void Package_HasTheMandatoryParts()
        {
            var sheet = new XlsxSheet("Teste");
            sheet.Set(1, 1, "olá");

            using (ZipArchive zip = Open(sheet))
            {
                string[] names = zip.Entries.Select(e => e.FullName).ToArray();
                Assert.Contains("[Content_Types].xml", names);
                Assert.Contains("_rels/.rels", names);
                Assert.Contains("xl/workbook.xml", names);
                Assert.Contains("xl/_rels/workbook.xml.rels", names);
                Assert.Contains("xl/styles.xml", names);
                Assert.Contains("xl/worksheets/sheet1.xml", names);
                // Sem imagem não existe desenho — nem a parte, nem a referência.
                Assert.DoesNotContain("xl/drawings/drawing1.xml", names);
                Assert.DoesNotContain("<drawing", Read(zip, "xl/worksheets/sheet1.xml"));
            }
        }

        [Fact]
        public void Text_GoesAsInlineString_AndIsEscaped()
        {
            var sheet = new XlsxSheet();
            sheet.Set(2, 3, "peça <A & B>");
            sheet.SetNumber(2, 4, 14309);

            using (ZipArchive zip = Open(sheet))
            {
                string xml = Read(zip, "xl/worksheets/sheet1.xml");
                Assert.Contains("t=\"inlineStr\"", xml);
                Assert.Contains("peça &lt;A &amp; B&gt;", xml);
                Assert.DoesNotContain("<A & B>", xml);
                Assert.Contains("r=\"C2\"", xml);
                Assert.Contains("<v>14309</v>", xml);
            }
        }

        [Fact]
        public void Image_BringsDrawingPartsAndAnchorsZeroBased()
        {
            var sheet = new XlsxSheet();
            sheet.Set(1, 1, "com imagem");
            sheet.Images.Add(new XlsxImage { Row = 4, Column = 3, Png = FakePng(100, 50), WidthPx = 100, HeightPx = 50 });

            using (ZipArchive zip = Open(sheet))
            {
                string[] names = zip.Entries.Select(e => e.FullName).ToArray();
                Assert.Contains("xl/drawings/drawing1.xml", names);
                Assert.Contains("xl/drawings/_rels/drawing1.xml.rels", names);
                Assert.Contains("xl/media/image1.png", names);
                Assert.Contains("xl/worksheets/_rels/sheet1.xml.rels", names);
                Assert.Contains("<drawing r:id=\"rId1\"/>", Read(zip, "xl/worksheets/sheet1.xml"));

                string drawing = Read(zip, "xl/drawings/drawing1.xml");
                Assert.Contains("<xdr:col>2</xdr:col>", drawing);   // coluna 3 → 2 (0-based no desenho)
                Assert.Contains("<xdr:row>3</xdr:row>", drawing);   // linha 4 → 3
                Assert.Contains("cx=\"952500\"", drawing);          // 100 px × 9525 EMU
                Assert.Contains("cy=\"476250\"", drawing);          // 50 px × 9525
                Assert.Contains("image/png", Read(zip, "[Content_Types].xml"));
            }
        }

        [Fact]
        public void EqualStyles_ShareOneEntry_AndTheDefaultIsIndexZero()
        {
            var sheet = new XlsxSheet();
            var bold = new XlsxStyle { Bold = true };
            sheet.Set(1, 1, "a", bold);
            sheet.Set(1, 2, "b", new XlsxStyle { Bold = true });   // igual por VALOR
            sheet.Set(1, 3, "c");                                   // sem estilo

            using (ZipArchive zip = Open(sheet))
            {
                Assert.Contains("<cellXfs count=\"2\">", Read(zip, "xl/styles.xml"));
                string xml = Read(zip, "xl/worksheets/sheet1.xml");
                Assert.Contains("r=\"A1\" s=\"1\"", xml);
                Assert.Contains("r=\"B1\" s=\"1\"", xml);
                Assert.Contains("r=\"C1\" s=\"0\"", xml);
            }
        }

        [Fact]
        public void ChangeReport_SheetMirrorsTheMdForm()
        {
            ChangeReport r = Sample();
            XlsxSheet sheet = ChangeReportXlsx.Build(r);

            Assert.Equal("Rev.2", sheet.Name);
            Assert.Contains(sheet.Cells, c => c.Text == ChangeReport.Title);
            Assert.Contains(sheet.Cells, c => c.Number == 2);                       // a revisão
            Assert.Contains(sheet.Cells, c => c.Text == "10925");                   // PA vindo da pasta
            Assert.Contains(sheet.Cells, c => c.Text == "14309.205.par");
            Assert.Contains(sheet.Cells, c => c.Text != null && c.Text.Contains("1 - Usinar face plana"));
            Assert.Contains(sheet.Cells, c => c.Text == ChangeReportFormatter.Checked + " EROSÃO / FURO RÁPIDO / FIO");
            Assert.Contains(sheet.Cells, c => c.Text == ChangeReportFormatter.Unchecked + " SOLDA TIG / LASER");
            Assert.Equal(2, sheet.Images.Count);   // Z+ e Z−
            // Layout desenhado pelo Carlos (2026-09-18): as duas vistas ficam em C e D, ao lado da
            // lista de caixas (coluna B) — nenhuma linha cresce para caber imagem.
            Assert.Equal(new[] { 3, 4 }, sheet.Images.Select(i => i.Column).ToArray());
            Assert.DoesNotContain(sheet.RowHeights, h => h.Value > 100);
            // A legenda fica na mesma linha do 1º bloco de caixas, e a imagem começa logo abaixo.
            XlsxCell caption = sheet.Cells.First(c => c.Text == "VISTA Z+ (de cima)");
            Assert.Contains(sheet.Cells, c => c.Row == caption.Row && c.Column == 2 &&
                                              c.Text == AutoEDM.Revisions.ChangeTaskCatalog.ActionGroup);
            Assert.All(sheet.Images, i => Assert.Equal(caption.Row + 1, i.Row));
        }

        [Fact]
        public void EveryFilledCellOfTheBlock_HasABorder()
        {
            // "Os contornos estão faltando nas células" (Carlos, 2026-09-18): as caixas de seleção,
            // os títulos dos blocos e as legendas saíam sem contorno, e a folha parecia rascunho.
            ChangeReport r = Sample();
            XlsxSheet sheet = ChangeReportXlsx.Build(r);

            var semBorda = sheet.Cells
                .Where(c => !string.IsNullOrEmpty(c.Text) && c.Text != ChangeReport.Title)
                .Where(c => c.Style == null || !c.Style.Border)
                .Select(c => XlsxSheet.CellRef(c.Row, c.Column) + " = " + c.Text)
                .ToList();
            Assert.True(semBorda.Count == 0, "sem contorno: " + string.Join(" | ", semBorda));
        }

        [Fact]
        public void MergedRange_StylesTheCoveredCells_SoTheBorderClosesTheWholeRange()
        {
            var sheet = new XlsxSheet();
            var bordered = new XlsxStyle { Border = true };
            sheet.Set(1, 1, "vai de A1 até D1", bordered);
            sheet.Merge(1, 1, 1, 4, bordered);

            // B1, C1 e D1 entram vazias MAS com o estilo: sem isso o contorno morre depois de A1.
            Assert.Equal(4, sheet.Cells.Count);
            Assert.All(sheet.Cells, c => Assert.True(c.Style.Border));
            Assert.Equal(new[] { 1, 2, 3, 4 }, sheet.Cells.Select(c => c.Column).OrderBy(x => x).ToArray());
        }

        [Fact]
        public void Sheet_CarriesTheOperationsReadFromTheGroup()
        {
            // 1ª planilha de verdade (2026-09-18) saiu sem elas: ficavam só na janela, e quem
            // recebe a folha não tinha como conferir a descrição contra o que mudou no modelo.
            ChangeReport r = Sample();
            r.Parts[0].Features.AddRange(new[] { "Recorte 6", "Substituir Face 2" });

            XlsxSheet sheet = ChangeReportXlsx.Build(r);
            Assert.Contains(sheet.Cells, c => c.Text != null && c.Text.StartsWith("OPERAÇÕES DO GRUPO (Rev. 2)"));
            Assert.Contains(sheet.Cells, c => c.Text == "Recorte 6, Substituir Face 2");
        }

        [Fact]
        public void Sheet_WithoutOperations_DoesNotLeaveAnEmptyRow()
        {
            XlsxSheet sheet = ChangeReportXlsx.Build(Sample());
            Assert.DoesNotContain(sheet.Cells, c => c.Text != null && c.Text.StartsWith("OPERAÇÕES DO GRUPO"));
        }

        [Fact]
        public void ChangeReport_SavesAFileThatOpensAsAZip()
        {
            string path = Path.Combine(Path.GetTempPath(), "autoedm_test_" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                ChangeReportXlsx.Save(path, Sample());
                Assert.True(new FileInfo(path).Length > 0);
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    Assert.Contains(zip.Entries, e => e.FullName == "xl/worksheets/sheet1.xml");
                    Assert.Contains(zip.Entries, e => e.FullName == "xl/media/image1.png");
                }
            }
            finally { try { File.Delete(path); } catch { } }
        }

        // ------------------------------------------------------------------ apoio

        private static ChangeReport Sample()
        {
            const string path = @"W:\09 - Projetos\02 - MOLDES\MD-14309 [PN-13972] [PA-10925]\14309.205.par";
            ProjectFolder f = ProjectFolder.Parse(path);
            var r = new ChangeReport
            {
                Revision = 2,
                When = new DateTime(2026, 9, 18, 9, 0, 0),
                AssemblyName = "14309.asm",
                MoldCode = f.MoldCode,
                PartNumbers = f.PartNumbers,
                ProductCode = f.ProductCode,
                ProjectDirectory = f.Directory,
            };
            var p = new PartChange
            {
                FileName = "14309.205.par",
                FullPath = path,
                Description = "Adicionado postiço para reparar machos",
            };
            p.Thumbnails.Add(FakePng(270, 200));
            p.Thumbnails.Add(FakePng(270, 200));
            p.Actions.Add("Usinar face plana para possibilitar furação");
            p.Tasks.First(t => t.Label.StartsWith("EROSÃO")).Checked = true;
            r.Parts.Add(p);
            return r;
        }

        /// <summary>PNG só com assinatura + IHDR: é tudo que o escritor lê (largura/altura).</summary>
        private static byte[] FakePng(int width, int height)
        {
            var png = new byte[24];
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            Array.Copy(signature, png, signature.Length);
            png[12] = (byte)'I'; png[13] = (byte)'H'; png[14] = (byte)'D'; png[15] = (byte)'R';
            png[16] = (byte)(width >> 24); png[17] = (byte)(width >> 16); png[18] = (byte)(width >> 8); png[19] = (byte)width;
            png[20] = (byte)(height >> 24); png[21] = (byte)(height >> 16); png[22] = (byte)(height >> 8); png[23] = (byte)height;
            return png;
        }

        private static ZipArchive Open(XlsxSheet sheet)
        {
            var buffer = new MemoryStream();
            XlsxWriter.Save(buffer, sheet);
            buffer.Position = 0;
            return new ZipArchive(buffer, ZipArchiveMode.Read);
        }

        private static string Read(ZipArchive zip, string entry)
        {
            using (var reader = new StreamReader(zip.GetEntry(entry).Open(), Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }
}
