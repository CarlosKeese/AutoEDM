using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>T9: o argumento <c>dividir</c> do "Criar eletrodos" (lógica pura).</summary>
    public static class SplitModes
    {
        /// <summary>
        /// Ausente ou <c>false</c> → eletrodo INTEIRO (<see cref="SplitMode.Off"/>, o padrão desde a T9);
        /// <c>"quina"</c> → divisão na quina (T6–T8, experimental, <see cref="SplitMode.Auto"/>);
        /// <c>true</c> ou <c>"guilhotina"</c> → guilhotina (T2, experimental, <see cref="SplitMode.Force"/>).
        /// Valor desconhecido → inteiro, com <paramref name="note"/>.
        /// </summary>
        public static SplitMode Parse(string token, out string note)
        {
            note = null;
            string t = (token ?? "").Trim().Trim('"').ToLowerInvariant();
            switch (t)
            {
                case "": case "false": case "inteiro": case "nao": case "não": return SplitMode.Off;
                case "quina": note = "divisão na quina (experimental)"; return SplitMode.Auto;
                case "true": case "guilhotina": note = "divisão por guilhotina (experimental)"; return SplitMode.Force;
                default: note = $"\"dividir\": \"{token}\" não reconhecido — eletrodo inteiro (aceitos: \"quina\", \"guilhotina\", true, false)."; return SplitMode.Off;
            }
        }
    }

    /// <summary>T9: conferência da seleção do modo semiautomático (lógica pura).</summary>
    public static class SelectionCheck
    {
        /// <summary>
        /// Faces de queima NÃO selecionadas cuja caixa entra na pegada (o eletrodo vai queimá-las também) e faces
        /// selecionadas que não são de queima. Caixas e pegada em mm, montagem; a sobreposição em planta precisa passar de
        /// <paramref name="tolMm"/>.
        /// </summary>
        public static (List<int> unselectedInside, List<int> selectedNotBurn) Warnings(BoundingBox footprint,
            IEnumerable<(int id, BoundingBox box)> burn, ICollection<int> selected, double tolMm = 0.01)
        {
            var burnList = burn?.ToList() ?? new List<(int, BoundingBox)>();
            var burnIds = new HashSet<int>(burnList.Select(b => b.id));
            var inside = burnList.Where(b => !selected.Contains(b.id) &&
                                             Math.Min(b.box.MaxX, footprint.MaxX) - Math.Max(b.box.MinX, footprint.MinX) > tolMm &&
                                             Math.Min(b.box.MaxY, footprint.MaxY) - Math.Max(b.box.MinY, footprint.MinY) > tolMm)
                                 .Select(b => b.id).Distinct().OrderBy(i => i).ToList();
            var notBurn = selected.Where(id => !burnIds.Contains(id)).Distinct().OrderBy(i => i).ToList();
            return (inside, notBurn);
        }
    }

    public static partial class ElectrodeNamingLogic
    {
        /// <summary>
        /// Próximo "EE##" livre: o maior índice entre os nomes dados (ocorrências da _EDM e arquivos já gravados na pasta)
        /// + 1. Assim rodar o botão de novo nunca sobrescreve o eletrodo anterior.
        /// </summary>
        public static int NextIndex(IEnumerable<string> names, string prefix)
        {
            int max = 0;
            string p = prefix ?? "ELD_EE";
            foreach (string raw in names ?? Enumerable.Empty<string>())
            {
                string name = raw ?? "";
                int i = name.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string rest = name.Substring(i + p.Length);
                int j = 0; while (j < rest.Length && char.IsDigit(rest[j])) j++;
                if (j > 0 && int.TryParse(rest.Substring(0, j), out int n) && n > max) max = n;
            }
            return max + 1;
        }
    }
}
