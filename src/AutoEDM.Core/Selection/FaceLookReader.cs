using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Selection
{
    /// <summary>O "visual" de uma face: o nome do estilo (o que a tabela de cores da casa usa) e a cor.</summary>
    public sealed class FaceLook
    {
        public string StyleName;
        public Color? Color;
    }

    /// <summary>
    /// Resolve o estilo de uma face na ordem em que a SE aplica a pintura: o estilo POR FACE e,
    /// sem ele, o da FEATURE que criou a face (é o padrão do comando de pintar da SE). Usado pelo
    /// Desenho 2D (cota pela cor) e pela cópia da queima (repinta a superfície copiada).
    /// </summary>
    public static class FaceLookReader
    {
        /// <summary>Mapa Face.ID → estilo da feature, de todas as features do 1º modelo da peça.</summary>
        public static Dictionary<int, FaceLook> ReadFeatureStyles(object partDoc)
        {
            var map = new Dictionary<int, FaceLook>();
            try
            {
                dynamic feats = ((dynamic)partDoc).Models.Item(1).Features;
                int n = (int)feats.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic feat = feats.Item(i);
                    object st = null;
                    try { st = feat.GetStyle(); } catch { }
                    if (st == null) continue;
                    var look = new FaceLook { StyleName = StyleName(st) };
                    if (FaceStyleColorReader.TryReadStyleColor(st, out Color c)) look.Color = c;
                    dynamic ff = null;
                    try { ff = feat.Faces[1]; } catch { try { ff = feat.Faces; } catch { } }
                    if (ff == null) continue;
                    int m = (int)ff.Count;
                    for (int k = 1; k <= m; k++) { try { map[(int)ff.Item(k).ID] = look; } catch { } }
                }
            }
            catch (Exception ex) { Log.Warn("Cor por feature ilegível: " + ex.GetBaseException().Message); }
            return map;
        }

        /// <summary>O estilo da face: o dela, ou o da feature (de <paramref name="featureStyles"/>). Null se nenhum.</summary>
        public static FaceLook Resolve(object face, IDictionary<int, FaceLook> featureStyles)
        {
            dynamic f = face;
            object style = null;
            try { style = f.Style; } catch { }
            if (style != null)
            {
                var look = new FaceLook { StyleName = StyleName(style) };
                if (FaceStyleColorReader.TryReadStyleColor(style, out Color col)) look.Color = col;
                ComLifetime.Release(style);
                if (look.StyleName != null || look.Color != null) return look;
            }
            int id = -1;
            try { id = (int)f.ID; } catch { }
            return featureStyles != null && featureStyles.TryGetValue(id, out FaceLook fl) ? fl : null;
        }

        public static string StyleName(object style)
        {
            foreach (string p in new[] { "StyleName", "Name" })
            {
                try
                {
                    object v = style.GetType().InvokeMember(p, BindingFlags.GetProperty, null, style, null);
                    if (v is string s && s.Length > 0) return s;
                }
                catch { }
            }
            return null;
        }
    }
}
