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
    /// [opt]UnitsType, [out] ppVariable)`, confirmado no dump da typelib SE 2023) — o
    /// [out] final vira o retorno em Automation/late-binding, mesmo padrão de
    /// `HoleDataCollection.Add`. Lemos/escrevemos por `Variable.Formula` (string), NUNCA
    /// `Variable.Value` (double): Value é escalado pela unidade da peça (pode não ser mm),
    /// Formula é o texto cru — sem conversão nenhuma, sem armadilha de unidade.
    /// </summary>
    public static class RaVariableStore
    {
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
                    existing.Formula = formula;
                    Log.Info($"Variável '{RaGapPresets.RaVariableName}' atualizada para {formula}.");
                    return;
                }
            }
            catch (Exception e) { Log.Warn($"Atualizar variável '{RaGapPresets.RaVariableName}' falhou (tentando criar) — " + e.GetBaseException().Message); }

            try
            {
                dynamic vars = partDoc.Variables;
                vars.Add(RaGapPresets.RaVariableName, formula, Type.Missing);
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
                string formula = Convert.ToString(v.Formula);
                return double.TryParse(formula, NumberStyles.Float, CultureInfo.InvariantCulture, out ra);
            }
            catch (Exception e) { Log.Warn($"Ler variável '{RaGapPresets.RaVariableName}' falhou — " + e.GetBaseException().Message); return false; }
        }

        private static dynamic TryGetVariable(dynamic partDoc)
        {
            try { return partDoc.Variables.Item(RaGapPresets.RaVariableName); }
            catch { return null; }
        }
    }
}
