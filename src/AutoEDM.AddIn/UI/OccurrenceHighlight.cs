using System;
using System.Collections.Generic;
using AutoEDM.Diagnostics;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// PRÉVIA POR REALCE dos botões de montagem (Carlos, 2026-10-30). Na peça a prévia cria de verdade e
    /// desfaz; na montagem o que muda são ocorrências (trocadas, criadas, medidas), então a prévia é acender
    /// no modelo, enquanto a confirmação está aberta, as ocorrências que o botão vai tocar — uma cor por papel.
    /// Um <c>HighlightSet</c> por cor (a cor é do conjunto, não do item). Cosmético: falha só no log.
    /// </summary>
    internal sealed class OccurrenceHighlight : IDisposable
    {
        public static readonly int Orange = Rgb(255, 128, 0);
        public static readonly int Green = Rgb(0, 200, 80);

        private readonly object _doc;
        private readonly string _logTag;
        private readonly List<object> _sets = new List<object>();

        public OccurrenceHighlight(object doc, string logTag)
        {
            _doc = doc;
            _logTag = logTag;
        }

        private static int Rgb(int r, int g, int b) { return r | (g << 8) | (b << 16); } // COLORREF

        /// <summary>Acende <paramref name="occurrences"/> na cor dada (acumula com o que já está aceso).</summary>
        public int Add(IEnumerable<object> occurrences, int color)
        {
            int n = 0;
            if (_doc == null || occurrences == null) return 0;
            try
            {
                dynamic hs = ((dynamic)_doc).HighlightSets.Add();
                _sets.Add((object)hs);
                hs.Color = color;
                foreach (object o in occurrences)
                {
                    if (o == null) continue;
                    try { hs.AddItem(o); n++; }
                    catch (Exception e) { Log.Warn($"{_logTag}: ocorrência fora do realce — " + e.GetBaseException().Message); }
                }
                hs.Draw();
            }
            catch (Exception e) { Log.Warn($"{_logTag}: realce não aplicado — " + e.GetBaseException().Message); }
            return n;
        }

        /// <summary>Apaga tudo o que este realce acendeu.</summary>
        public void Clear()
        {
            foreach (object s in _sets)
            {
                try
                {
                    dynamic hs = s;
                    hs.RemoveAll();
                    hs.Draw();
                    hs.Delete();
                }
                catch { /* documento já fechado ou realce já sumiu */ }
            }
            _sets.Clear();
        }

        public void Dispose() { Clear(); }
    }
}
