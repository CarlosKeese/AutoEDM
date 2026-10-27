using System.Collections.Generic;
using AutoEDM.Com;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Os templates da casa: a biblioteca primeiro, a cópia PanFlight ao lado dos templates da SE
    /// quando o W: está fora, e null (= padrão da SE) quando nenhum existe.
    /// </summary>
    public class HouseTemplatesTests
    {
        private const string SeRoot = @"C:\Program Files\Siemens\Solid Edge 2023\Template";

        [Fact]
        public void Library_wins_when_it_exists()
        {
            var disk = new HashSet<string>
            {
                HouseTemplates.LibraryFolder + @"\PanFlight Part.par",
                SeRoot + @"\PanFlight\PanFlight Part.par",
            };
            Assert.Equal(HouseTemplates.LibraryFolder + @"\PanFlight Part.par",
                HouseTemplates.Resolve(HouseDocumentKind.Part, HouseTemplates.LibraryFolder, SeRoot, disk.Contains));
        }

        [Fact]
        public void Local_copy_when_library_is_offline()
        {
            var disk = new HashSet<string> { SeRoot + @"\PanFlight\PanFlight Assembly.asm" };
            Assert.Equal(SeRoot + @"\PanFlight\PanFlight Assembly.asm",
                HouseTemplates.Resolve(HouseDocumentKind.Assembly, HouseTemplates.LibraryFolder, SeRoot, disk.Contains));
        }

        [Fact]
        public void Null_when_no_house_template_exists()
        {
            Assert.Null(HouseTemplates.Resolve(HouseDocumentKind.Draft, HouseTemplates.LibraryFolder, SeRoot, _ => false));
        }

        [Fact]
        public void Empty_folder_skips_the_library()
        {
            var disk = new HashSet<string> { SeRoot + @"\PanFlight\PanFlight Draft.dft" };
            Assert.Equal(SeRoot + @"\PanFlight\PanFlight Draft.dft",
                HouseTemplates.Resolve(HouseDocumentKind.Draft, "", SeRoot, disk.Contains));
        }

        [Fact]
        public void Template_root_comes_from_the_se_default_template()
        {
            Assert.Equal(SeRoot, HouseTemplates.TemplateRootOf(SeRoot + @"\ISO Metric\Iso Metric Part.par"));
            Assert.Null(HouseTemplates.TemplateRootOf(null));
        }

        [Fact]
        public void Config_defaults_to_the_library()
        {
            Assert.Equal(HouseTemplates.LibraryFolder, new AutoEDM.Config.AutoEdmConfig().TemplateFolder);
        }
    }
}
