using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// Plano B da PRÉVIA quando o histórico de desfazer não serve (Carlos, 2026-10-30): com a peça
    /// em edição DENTRO da montagem, o <c>UndoTransactionCount</c> voltou a 0 depois do canal de
    /// O'ring (antes 1, depois 0) — não há passos para o <c>Undo(n)</c>. Então a prévia anota os
    /// nomes do PathFinder antes de criar e, para desfazer, apaga o que apareceu, do mais novo
    /// para o mais velho (acabamento antes do corte que ele usa).
    /// </summary>
    internal static class PreviewFeatures
    {
        /// <summary>Nomes de <c>PartDocument.DesignEdgebarFeatures</c> na ordem do PathFinder; null se ilegível.</summary>
        public static List<string> Names(object doc)
        {
            try
            {
                var names = new List<string>();
                dynamic col = ((dynamic)doc).DesignEdgebarFeatures;
                for (int i = 1; i <= (int)col.Count; i++)
                {
                    string n = NameOf(col.Item(i));
                    if (n != null) names.Add(n);
                }
                return names;
            }
            catch (Exception e)
            {
                Log.Warn("[prévia] features da peça ilegíveis: " + e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>O que entrou entre as duas leituras, na ordem de criação.</summary>
        public static List<string> Added(List<string> before, List<string> after)
        {
            if (before == null || after == null) return new List<string>();
            var old = new HashSet<string>(before);
            return after.Where(n => !old.Contains(n)).ToList();
        }

        /// <summary>
        /// Apaga <paramref name="created"/> do mais novo para o mais velho. Recusa (false, sem
        /// apagar nada) se o PathFinder não for EXATAMENTE o de antes + as criadas: o usuário mexeu
        /// na peça e apagar levaria o trabalho dele junto. Plano de referência que não sai (a API
        /// ordenada recusa o Delete dele) não conta como falha: ele está escondido.
        /// </summary>
        public static bool DeleteCreated(object doc, List<string> before, List<string> created, out List<string> left, out string refused)
        {
            left = new List<string>();
            refused = null;
            List<string> now = Names(doc);
            if (now == null) { refused = "as features da peça não puderam ser lidas"; return false; }
            List<string> added = Added(before, now);
            if (before.Any(n => !now.Contains(n)) || !added.SequenceEqual(created))
            {
                refused = "a peça mudou depois da prévia";
                return false;
            }

            for (int k = created.Count - 1; k >= 0; k--)
            {
                object f = Find(doc, created[k]);
                if (f == null) continue;   // saiu junto com a de cima (acabamento do corte, esboço do recurso)
                string type = ComDiagnostics.TypeNameOf(f) ?? "?";
                try
                {
                    ((dynamic)f).Delete();
                    Log.Info($"[prévia] apagada '{created[k]}' ({type}).");
                }
                catch (Exception e)
                {
                    bool plane = type.IndexOf("RefPlane", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (plane) { TryHide(f); Log.Info($"[prévia] plano '{created[k]}' não apaga pela API — fica escondido."); }
                    else { left.Add(created[k]); Log.Warn($"[prévia] '{created[k]}' ({type}) não apagou: " + e.GetBaseException().Message); }
                }
            }
            return true;
        }

        private static object Find(object doc, string name)
        {
            try
            {
                dynamic col = ((dynamic)doc).DesignEdgebarFeatures;
                for (int i = 1; i <= (int)col.Count; i++)
                {
                    object f = col.Item(i);
                    if (NameOf(f) == name) return f;
                }
            }
            catch { }
            return null;
        }

        private static string NameOf(object f)
        {
            try { return (string)((dynamic)f).Name; } catch { return null; }
        }

        private static void TryHide(object f)
        {
            try { ((dynamic)f).Visible = false; } catch { }
        }
    }
}
