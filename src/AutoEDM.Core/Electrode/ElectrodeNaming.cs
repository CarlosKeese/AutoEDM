using System;
using AutoEDM.Assembly;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Nomes e pastas de eletrodo/peça — extraído de <see cref="ElectrodeBuilder"/> (revisão
    /// 2026-07-23, docs/REVISAO-AutoEDM.md P2.1): são métodos quase todos estáticos e puros,
    /// sem acoplamento com o pipeline COM principal, então o corte é seguro.
    /// </summary>
    public static class ElectrodeNaming
    {
        /// <summary>Subpasta "Eletrodos" ao lado da montagem (escolha do Carlos); fallback local.</summary>
        public static string ResolveElectrodeFolder(dynamic asmDoc, ElectrodeParams p)
        {
            if (!string.IsNullOrWhiteSpace(p.OutputFolder)) return p.OutputFolder;
            string dir = ResolveProjectFolder(asmDoc);
            if (dir != null) return System.IO.Path.Combine(dir, "Eletrodos");
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoEDM", "electrodes");
        }

        /// <summary>
        /// Pasta do PROJETO ATIVO (a pasta onde a montagem .asm está salva) — usada tanto pelos
        /// eletrodos gerados (subpasta "Eletrodos", ver <see cref="ResolveElectrodeFolder"/>)
        /// quanto pelos relatórios (coordenadas de queima / ficha de eletrodos, Carlos
        /// 2026-07-22: "os relatórios precisam ser salvos na pasta do projeto ativo" — antes iam
        /// pro %LOCALAPPDATA%\AutoEDM\reports, fora de vista, sem relação nenhuma com o job). Null
        /// se a montagem ainda não foi salva em disco (doc.FullName vazio/inacessível) — quem
        /// chama decide o fallback.
        /// </summary>
        public static string ResolveProjectFolder(dynamic asmDoc)
        {
            try
            {
                string full = (string)asmDoc.FullName;
                string dir = System.IO.Path.GetDirectoryName(full);
                if (!string.IsNullOrWhiteSpace(dir)) return dir;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Nome-base do eletrodo GERADO (Carlos, 2026-07-23): "Nome da montagem" + "Número do
        /// eletrodo", ex. montagem "15142.200_EDM.asm" -> eletrodos "15142.200_EDM_EE01",
        /// "15142.200_EDM_EE02" (prefixo <see cref="ElectrodeNamePrefix"/> + índice). Substitui o
        /// antigo prefixo fixo "ELD" (`ElectrodeParams.ElectrodeName`, nunca editável na UI —
        /// era só um placeholder) — o nome agora é sempre derivado da montagem ATIVA, sem
        /// depender de configuração. Fallback "ELD" só se a montagem ainda não tiver nome
        /// (documento novo, não salvo).
        /// </summary>
        public static string ResolveElectrodeBaseName(dynamic asmDoc)
        {
            try
            {
                string name = (string)asmDoc.Name; // ex.: "15142.200_EDM.asm"
                string baseName = System.IO.Path.GetFileNameWithoutExtension(name);
                if (!string.IsNullOrWhiteSpace(baseName)) return baseName;
            }
            catch { }
            return "ELD";
        }

        /// <summary>Prefixo "{montagem}_EE" usado tanto para nomear o eletrodo ao CRIAR
        /// (<see cref="ElectrodeBuilder"/>) quanto para achar o próximo índice livre
        /// (<see cref="NextElectrodeIndex"/>) — um só lugar define o formato do nome.</summary>
        public static string ElectrodeNamePrefix(dynamic asmDoc) => ResolveElectrodeBaseName(asmDoc) + "_EE";

        /// <summary>Próximo índice "EE##" livre, olhando os nomes das ocorrências já na montagem
        /// (ex.: "15142.200_EDM_EE01", "...EE02" -> devolve 3) — assim eletrodos manuais e
        /// automáticos nunca colidem de nome, mesmo entre sessões do SE (o contador não é um
        /// campo estático). <paramref name="namePrefix"/> já vem pronto (ver
        /// <see cref="ElectrodeNamePrefix"/>), sem sufixo — só concatena o índice.</summary>
        public static int NextElectrodeIndex(AssemblyContext ctx, string namePrefix)
        {
            int max = 0;
            string prefix = namePrefix ?? "ELD_EE";
            foreach (var occ in ctx.GetOccurrences())
            {
                string name = occ.Name ?? "";
                int i = name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string rest = name.Substring(i + prefix.Length);
                int j = 0; while (j < rest.Length && char.IsDigit(rest[j])) j++;
                if (j > 0 && int.TryParse(rest.Substring(0, j), out int n) && n > max) max = n;
            }
            return max + 1;
        }

        public static string SafeDocName(dynamic doc)
        {
            try { return (string)doc.Name; } catch { return "<montagem>"; }
        }

        public static string SafeName(dynamic occ)
        {
            try { return (string)occ.Name; } catch { return "<sem nome>"; }
        }

        public static dynamic SafeDoc(dynamic occ)
        {
            try { return occ.OccurrenceDocument; } catch { return null; }
        }
    }
}
