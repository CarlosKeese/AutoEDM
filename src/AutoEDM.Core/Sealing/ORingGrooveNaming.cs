using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Nome da feature do alojamento na árvore: o anel que vai nele, e um número de instância.
    /// Ex.: <c>O'ring 2-214 - d2 3,53 x d1 24,99 - 2</c>. É o que o Carlos lê no PathFinder para
    /// saber que anel comprar — por isso vai o CÓDIGO e as duas medidas, na ordem que ele pediu
    /// (seção, diâmetro interno). O número conta só os alojamentos do MESMO anel: dois canais de
    /// 2-214 viram "- 1" e "- 2" e não brigam pelo nome.
    /// </summary>
    public static class ORingGrooveNaming
    {
        /// <summary>O nome sem o número de instância.</summary>
        public static string BaseName(ORingSize ring)
        {
            if (ring == null) throw new ArgumentNullException(nameof(ring));
            var c = CultureInfo.CurrentCulture;
            string code = string.IsNullOrWhiteSpace(ring.Code) ? "" : " " + ring.Code.Trim();
            return $"O'ring{code} - d2 {ring.CrossSection.ToString("0.00", c)} x d1 {ring.InnerDiameter.ToString("0.00", c)}";
        }

        /// <summary>
        /// O próximo nome livre para este anel, dados os nomes que já existem na peça: um a mais
        /// que o MAIOR número em uso (não o primeiro buraco — apagar o "- 1" não faz o próximo
        /// canal herdar o nome de um que já foi para o desenho).
        /// </summary>
        public static string NextName(ORingSize ring, IEnumerable<string> existingNames)
        {
            string baseName = BaseName(ring);
            string prefix = baseName + " - ";
            int max = 0;
            if (existingNames != null)
                foreach (string name in existingNames)
                {
                    if (name == null || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    int n;
                    if (int.TryParse(name.Substring(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > max)
                        max = n;
                }
            return prefix + (max + 1).ToString(CultureInfo.InvariantCulture);
        }
    }
}
