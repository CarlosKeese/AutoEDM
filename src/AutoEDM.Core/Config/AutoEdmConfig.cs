using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Config
{
    /// <summary>Uma faixa Ra-superior -> offset (mm); espelha <see cref="Electrode.RaOffsetTablePolicy"/>.</summary>
    public sealed class RaOffsetBand
    {
        public double RaUpper { get; set; }
        public double OffsetMm { get; set; }
    }

    /// <summary>Uma cor de queima (RGB 0-255) -> Ra alvo; espelha <see cref="Electrode.RaColorMap.Entry"/>.</summary>
    public sealed class RaColorEntry
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public double Ra { get; set; }
    }

    /// <summary>
    /// Configuração externa do AutoEDM (revisão 2026-07-23/24, docs/REVISAO-AutoEDM.md A3):
    /// prefixo do eletrodo, tabela de offset por Ra e mapa de cor de queima viviam só como
    /// defaults fixos no código — certo enquanto só o Carlos usa a ferramenta, mas a
    /// primeira pergunta de qualquer outra pessoa que instalar o add-in ("como mudo o
    /// prefixo?", "minha tabela de Ra é diferente", "aqui a cor de queima é outra").
    ///
    /// Arquivo em <see cref="DefaultPath"/> (%LOCALAPPDATA%\AutoEDM\config.json). Ausente OU
    /// com uma lista vazia/null em <see cref="RaOffsetBands"/>/<see cref="RaColorEntries"/> ->
    /// cai nos MESMOS defaults que já estavam no código (ver [[autoedm-decisions]]) — nenhum
    /// comportamento muda para quem nunca tocar no arquivo.
    /// </summary>
    public sealed class AutoEdmConfig
    {
        public string ElectrodeNamePrefix { get; set; } = "ELD";
        public string Material { get; set; } = "Cobre";
        public int ColorTolerance { get; set; } = 8;
        public double DetailGapMm { get; set; } = 1.0;
        public double HolderHeightMm { get; set; } = 15.0;
        public double HolderBaseClearanceMm { get; set; } = 1.0;

        /// <summary>Null/vazio = usa a tabela de fábrica de <see cref="Electrode.RaOffsetTablePolicy"/>.</summary>
        public List<RaOffsetBand> RaOffsetBands { get; set; }

        /// <summary>Null/vazio = usa a paleta de fábrica de <see cref="Electrode.RaColorMap"/>.</summary>
        public List<RaColorEntry> RaColorEntries { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoEDM", "config.json");

        /// <summary>
        /// Lê config.json se existir; senão devolve os defaults e GRAVA o arquivo (para o
        /// usuário achar e editar, em vez de precisar descobrir o formato sozinho). Nunca
        /// lança — config é metadado auxiliar, um erro de leitura não pode travar a
        /// ferramenta: loga e segue com o default.
        /// </summary>
        public static AutoEdmConfig LoadOrCreateDefault(string path = null)
        {
            path = path ?? DefaultPath;
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<AutoEdmConfig>(json, JsonOptions);
                    if (cfg != null)
                    {
                        Log.Info($"Configuração carregada de {path}.");
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível ler {path} ({ex.Message}) — usando configuração padrão.");
            }

            var defaultCfg = new AutoEdmConfig();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(defaultCfg, JsonOptions));
                Log.Info($"Configuração padrão gravada em {path}.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível gravar {path} ({ex.Message}) — seguindo só em memória.");
            }
            return defaultCfg;
        }

        /// <summary>Parâmetros de eletrodo derivados desta configuração (prefixo, material,
        /// tolerância de cor, folgas). O resto de <see cref="ElectrodeParams"/> mantém os
        /// defaults de fábrica — não fazem parte do que a revisão pediu para configurar.</summary>
        public ElectrodeParams ToElectrodeParams() => new ElectrodeParams
        {
            ElectrodeName = ElectrodeNamePrefix,
            Material = Material,
            ColorTolerance = ColorTolerance,
            DetailGapMm = DetailGapMm,
            HolderHeight = HolderHeightMm,
            HolderBaseClearanceMm = HolderBaseClearanceMm,
        };

        /// <summary>Tabela de offset por Ra pronta para o núcleo; bandas vazias/nulas = default de fábrica.</summary>
        public Electrode.RaOffsetTablePolicy BuildOffsetPolicy()
        {
            if (RaOffsetBands == null || RaOffsetBands.Count == 0) return new Electrode.RaOffsetTablePolicy();
            return new Electrode.RaOffsetTablePolicy(RaOffsetBands.Select(b => (b.RaUpper, b.OffsetMm)));
        }

        /// <summary>Mapa cor->Ra pronto para o núcleo; entradas vazias/nulas = paleta de fábrica.</summary>
        public Electrode.RaColorMap BuildColorMap()
        {
            var map = (RaColorEntries == null || RaColorEntries.Count == 0)
                ? new Electrode.RaColorMap()
                : new Electrode.RaColorMap(RaColorEntries.Select(e =>
                    new Electrode.RaColorMap.Entry(System.Drawing.Color.FromArgb(e.R, e.G, e.B), e.Ra)));
            map.Tolerance = ColorTolerance;
            return map;
        }
    }
}
