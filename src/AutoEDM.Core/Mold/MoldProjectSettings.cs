using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mold
{
    /// <summary>
    /// O que o "Nova peça" lembra POR PROJETO (chave = caminho da montagem): o eixo de altura —
    /// que é do projeto, não do usuário, porque cada montagem pode estar orientada de um jeito —,
    /// o lado da origem e a última parte escolhida. Fica em
    /// %LOCALAPPDATA%\AutoEDM\mold-projects.json, fora da pasta do projeto (não suja a rede).
    /// Best-effort: arquivo ilegível vira padrão (Z, origem embaixo, parte fixa) e o log diz.
    /// </summary>
    public sealed class MoldProjectSettings
    {
        public HeightAxis HeightAxis { get; set; } = HeightAxis.Z;
        public bool OriginAtTop { get; set; }
        public MoldSection LastSection { get; set; } = MoldSection.Fixed;

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoEDM", "mold-projects.json");

        private static string Key(string assemblyPath) => (assemblyPath ?? "").Trim().ToLowerInvariant();

        public static MoldProjectSettings Load(string assemblyPath, string storePath = null)
        {
            Dictionary<string, MoldProjectSettings> all = ReadAll(storePath ?? DefaultPath);
            return all.TryGetValue(Key(assemblyPath), out MoldProjectSettings s) && s != null ? s : new MoldProjectSettings();
        }

        public void Save(string assemblyPath, string storePath = null)
        {
            string path = storePath ?? DefaultPath;
            try
            {
                Dictionary<string, MoldProjectSettings> all = ReadAll(path);
                all[Key(assemblyPath)] = this;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { Log.Warn("Nova peça: preferências do projeto não gravadas — " + e.GetBaseException().Message); }
        }

        private static Dictionary<string, MoldProjectSettings> ReadAll(string path)
        {
            try
            {
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<Dictionary<string, MoldProjectSettings>>(File.ReadAllText(path))
                           ?? new Dictionary<string, MoldProjectSettings>();
            }
            catch (Exception e) { Log.Warn($"Nova peça: '{path}' ilegível, usando o padrão — " + e.GetBaseException().Message); }
            return new Dictionary<string, MoldProjectSettings>();
        }
    }
}
