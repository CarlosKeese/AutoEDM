using System;
using System.Globalization;
using AutoEDM.Diagnostics;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Lê/grava o Ra detectado numa VARIÁVEL da peça do eletrodo (Carlos, 2026-07-21):
    /// "Criar eletrodo (manual)" grava o Ra lido da cor da seleção (ainda na montagem);
    /// "Aplicar GAP" grava o Ra escolhido na lista (e lê de volta p/ pré-selecionar);
    /// "Duplicar eletrodo" lê o Ra atual p/ achar o PRÓXIMO da tabela (desbaste).
    ///
    /// `Document.Variables` é a coleção real (`_IVariablesAuto.Add(pName, pFormula,
    /// [opt]UnitsType, [out] ppVariable)`, confirmado no dump da typelib SE 2023).
    ///
    /// CORRIGIDO 2026-10-23 (medido ao vivo pela sonda tools/MoldBaseProbe, SE 2023):
    /// <c>Add</c> SEM UnitsType cria a variável como DISTÂNCIA (Ra 1.6 → <c>Value</c> 0.0016 m), e a
    /// <c>Formula</c> de uma CONSTANTE volta VAZIA — só expressões ficam nela. A leitura antiga, que
    /// parseava a Formula, nunca achava o Ra. Agora: grava como ESCALAR (<c>igUnitScalar</c> = 58,
    /// <c>Value</c> = o número cru) e lê pelo <c>Value</c>, convertendo as variáveis antigas de
    /// distância (m → o número que foi digitado em mm).
    /// </summary>
    public static class RaVariableStore
    {
        /// <summary><c>UnitTypeConstants.igUnitScalar</c> — número sem unidade.</summary>
        public const int UnitScalar = 58;
        /// <summary><c>UnitTypeConstants.igUnitDistance</c> — o que <c>Add</c> sem UnitsType cria.</summary>
        public const int UnitDistance = 1;

        /// <summary>Grava (cria ou atualiza) a variável de Ra na peça. NUNCA lança — é
        /// metadado auxiliar, não pode derrubar a criação/edição do eletrodo.</summary>
        public static void TryWrite(dynamic partDoc, double ra)
        {
            string formula = ra.ToString("0.0", CultureInfo.InvariantCulture);
            try
            {
                dynamic existing = TryGetVariable(partDoc);
                if (existing != null)
                {
                    // Variável antiga, de distância: apaga e recria escalar (o número digitado viraria mm).
                    if (Convert.ToInt32(existing.UnitsType) != UnitScalar) existing.Delete();
                    else
                    {
                        existing.Formula = formula;
                        Log.Info($"Variável '{RaGapPresets.RaVariableName}' atualizada para {formula}.");
                        return;
                    }
                }
            }
            catch (Exception e) { Log.Warn($"Atualizar variável '{RaGapPresets.RaVariableName}' falhou (tentando criar) — " + e.GetBaseException().Message); }

            try
            {
                dynamic vars = partDoc.Variables;
                vars.Add(RaGapPresets.RaVariableName, formula, UnitScalar);
                Log.Info($"Variável '{RaGapPresets.RaVariableName}' = {formula} criada na peça.");
            }
            catch (Exception e) { Log.Warn($"Gravar variável '{RaGapPresets.RaVariableName}' falhou (não bloqueia o eletrodo) — " + e.GetBaseException().Message); }
        }

        /// <summary>Lê o Ra gravado na peça, se houver. Nunca lança.</summary>
        public static bool TryRead(dynamic partDoc, out double ra)
        {
            ra = 0;
            try
            {
                dynamic v = TryGetVariable(partDoc);
                if (v == null) return false;
                return TryDecode(Convert.ToString(v.Formula), Convert.ToDouble(v.Value), Convert.ToInt32(v.UnitsType), out ra);
            }
            catch (Exception e) { Log.Warn($"Ler variável '{RaGapPresets.RaVariableName}' falhou — " + e.GetBaseException().Message); return false; }
        }

        /// <summary>
        /// O Ra a partir do que a variável devolve: a Formula, quando não vem vazia; senão o Value —
        /// cru se escalar, ×1000 se distância (a SE guarda em metros o que foi digitado em mm).
        /// </summary>
        public static bool TryDecode(string formula, double value, int unitsType, out double ra)
        {
            ra = 0;
            if (!string.IsNullOrWhiteSpace(formula) &&
                double.TryParse(formula.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out ra) && ra > 0)
                return true;
            if (double.IsNaN(value) || value <= 0) { ra = 0; return false; }
            ra = unitsType == UnitDistance ? Math.Round(value * 1000.0, 6) : value;
            return true;
        }

        private static dynamic TryGetVariable(dynamic partDoc)
        {
            try { return partDoc.Variables.Item(RaGapPresets.RaVariableName); }
            catch { return null; }
        }
    }
}
