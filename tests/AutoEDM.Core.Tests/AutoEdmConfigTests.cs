using System;
using System.Collections.Generic;
using System.IO;
using AutoEDM.Config;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>Config externa (revisão A3, docs/REVISAO-AutoEDM.md): garante que ausência de
    /// config.json cai nos MESMOS defaults que já estavam no código, e que um arquivo
    /// customizado realmente sobrescreve prefixo/tabela de Ra/mapa de cor.</summary>
    public class AutoEdmConfigTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;

        public AutoEdmConfigTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AutoEDM_Tests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "config.json");
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public void LoadOrCreateDefault_WritesFile_WhenMissing()
        {
            Assert.False(File.Exists(_path));
            var cfg = AutoEdmConfig.LoadOrCreateDefault(_path);

            Assert.True(File.Exists(_path));
            Assert.Equal("ELD", cfg.ElectrodeNamePrefix);
            Assert.Equal("Cobre", cfg.Material);
        }

        [Fact]
        public void LoadOrCreateDefault_ReadsBackCustomValues()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{ \"ElectrodeNamePrefix\": \"EE\", \"Material\": \"Grafite\" }");

            var cfg = AutoEdmConfig.LoadOrCreateDefault(_path);

            Assert.Equal("EE", cfg.ElectrodeNamePrefix);
            Assert.Equal("Grafite", cfg.Material);
        }

        [Fact]
        public void ToElectrodeParams_CarriesPrefixAndFolgas()
        {
            var cfg = new AutoEdmConfig { ElectrodeNamePrefix = "EE", DetailGapMm = 2.5, HolderHeightMm = 20 };
            var p = cfg.ToElectrodeParams();

            Assert.Equal("EE", p.ElectrodeName);
            Assert.Equal(2.5, p.DetailGapMm);
            Assert.Equal(20, p.HolderHeight);
        }

        [Fact]
        public void BuildOffsetPolicy_UsesFactoryDefault_WhenBandsEmpty()
        {
            var cfg = new AutoEdmConfig();
            var policy = cfg.BuildOffsetPolicy();

            var pass = new Model.ElectrodePass("ACAB", 1.6);
            Assert.Equal(0.10, policy.GetInwardOffsetMm(pass, "Cobre"), 6);
        }

        [Fact]
        public void BuildOffsetPolicy_UsesCustomBands_WhenProvided()
        {
            var cfg = new AutoEdmConfig
            {
                RaOffsetBands = new List<RaOffsetBand> { new RaOffsetBand { RaUpper = 1.6, OffsetMm = 0.99 } }
            };
            var policy = cfg.BuildOffsetPolicy();

            var pass = new Model.ElectrodePass("ACAB", 1.6);
            Assert.Equal(0.99, policy.GetInwardOffsetMm(pass, "Cobre"), 6);
        }

        [Fact]
        public void BuildColorMap_UsesCustomEntries_WhenProvided()
        {
            var cfg = new AutoEdmConfig
            {
                RaColorEntries = new List<RaColorEntry> { new RaColorEntry { R = 9, G = 9, B = 9, Ra = 4.4 } }
            };
            var map = cfg.BuildColorMap();

            bool ok = map.TryGetRa(System.Drawing.Color.FromArgb(9, 9, 9), out double ra, out _);
            Assert.True(ok);
            Assert.Equal(4.4, ra);
        }
    }
}
