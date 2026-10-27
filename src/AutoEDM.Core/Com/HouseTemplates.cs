using System;
using System.Collections.Generic;
using System.IO;
using AutoEDM.Diagnostics;

namespace AutoEDM.Com
{
    /// <summary>Tipo de documento novo — cada um tem o seu template da casa.</summary>
    public enum HouseDocumentKind
    {
        Part,
        Assembly,
        Draft,
    }

    /// <summary>
    /// Os templates da casa (Carlos, 2026-10-27): toda peça, montagem e desenho que o AutoEDM cria
    /// nasce do template PanFlight da BIBLIOTECA, para seguir a iluminação e os estilos. Antes cada
    /// builder chamava <c>Documents.Add(progId)</c> sem template e caía no padrão da SE (ISO Metric
    /// nesta máquina); só o Desenho 2D usava o PanFlight, e da cópia em Program Files, desatualizada.
    ///
    /// Ordem: <see cref="Folder"/> (a biblioteca; <c>TemplateFolder</c> no config.json) → a pasta
    /// PanFlight ao lado dos templates da SE (cópia local, para quando o W: está fora) → o padrão da
    /// SE. Medido pela sonda tools/PartCopyProbe --templates: os templates vêm vazios e no mesmo
    /// ambiente do padrão (síncrono); a peça traz as variáveis da casa (X/Y/Z, Stock_*, RA, GAP…),
    /// nenhuma com nome que o AutoEDM cria.
    /// </summary>
    public static class HouseTemplates
    {
        public const string LibraryFolder = @"W:\01 - Biblioteca\Padrões\Preferences\Templates\PanFlight";
        private const string LocalFolderName = "PanFlight";

        private static string _folder;

        /// <summary>
        /// Pasta dos templates da casa — <c>TemplateFolder</c> do config.json, lido uma vez. Vazia =
        /// só a cópia local e o padrão da SE.
        /// </summary>
        public static string Folder
        {
            get => _folder ?? (_folder = Config.AutoEdmConfig.LoadOrCreateDefault().TemplateFolder ?? "");
            set => _folder = value;
        }

        public static string FileName(HouseDocumentKind kind)
        {
            switch (kind)
            {
                case HouseDocumentKind.Part: return "PanFlight Part.par";
                case HouseDocumentKind.Assembly: return "PanFlight Assembly.asm";
                default: return "PanFlight Draft.dft";
            }
        }

        public static string ProgId(HouseDocumentKind kind)
        {
            switch (kind)
            {
                case HouseDocumentKind.Part: return "SolidEdge.PartDocument";
                case HouseDocumentKind.Assembly: return "SolidEdge.AssemblyDocument";
                default: return "SolidEdge.DraftDocument";
            }
        }

        /// <summary><c>DocumentTypeConstants</c> para <c>Application.GetDefaultTemplatePath</c>.</summary>
        private static int SeDocumentType(HouseDocumentKind kind) =>
            kind == HouseDocumentKind.Part ? 1 : kind == HouseDocumentKind.Assembly ? 3 : 2;

        /// <summary>
        /// O template da casa que existe: na <paramref name="folder"/> e, sem ele, na pasta PanFlight
        /// dentro de <paramref name="seTemplateRoot"/> (a pasta Template da SE). Null = nenhum.
        /// </summary>
        public static string Resolve(HouseDocumentKind kind, string folder, string seTemplateRoot, Func<string, bool> exists = null)
        {
            exists = exists ?? File.Exists;
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(folder)) candidates.Add(Path.Combine(folder, FileName(kind)));
            if (!string.IsNullOrWhiteSpace(seTemplateRoot)) candidates.Add(Path.Combine(seTemplateRoot, LocalFolderName, FileName(kind)));
            foreach (string c in candidates)
            {
                try { if (exists(c)) return c; } catch { }
            }
            return null;
        }

        /// <summary>
        /// O template a usar, para quem precisa do CAMINHO (ex.: <c>Occurrences.AddByTemplate</c>):
        /// o da casa ou, sem ele, o padrão da SE. Null se nem o padrão existir.
        /// </summary>
        public static string ResolveOrSeDefault(object application, HouseDocumentKind kind)
        {
            string seDefault = SeDefault(application, kind);
            string house = Resolve(kind, Folder, TemplateRootOf(seDefault));
            if (house != null) return house;
            Log.Warn($"Template da casa '{FileName(kind)}' não encontrado (pasta '{Folder}' nem cópia local) — usando o padrão da SE: {seDefault ?? "?"}");
            return seDefault != null && File.Exists(seDefault) ? seDefault : null;
        }

        /// <summary>
        /// Documento NOVO do template da casa. Sem template da casa, ou se a SE recusar o arquivo,
        /// cai no <c>Documents.Add(progId)</c> de sempre — o template nunca impede a criação.
        /// </summary>
        public static dynamic Add(object application, HouseDocumentKind kind)
        {
            dynamic app = application;
            string template = Resolve(kind, Folder, TemplateRootOf(SeDefault(application, kind)));
            if (template != null)
            {
                try { return app.Documents.Add(ProgId(kind), template); }
                catch (Exception e) { Log.Warn($"Template da casa recusado ({template}) — usando o padrão da SE: {e.GetBaseException().Message}"); }
            }
            else Log.Warn($"Template da casa '{FileName(kind)}' não encontrado (pasta '{Folder}' nem cópia local) — usando o padrão da SE.");
            return app.Documents.Add(ProgId(kind));
        }

        private static string SeDefault(object application, HouseDocumentKind kind)
        {
            try { return Convert.ToString(((dynamic)application).GetDefaultTemplatePath(SeDocumentType(kind))); }
            catch { return null; }
        }

        /// <summary>...\Template\ISO Metric\Iso Metric Part.par → ...\Template.</summary>
        public static string TemplateRootOf(string seDefaultTemplate)
        {
            if (string.IsNullOrWhiteSpace(seDefaultTemplate)) return null;
            try { return Path.GetDirectoryName(Path.GetDirectoryName(seDefaultTemplate)); }
            catch { return null; }
        }
    }
}
