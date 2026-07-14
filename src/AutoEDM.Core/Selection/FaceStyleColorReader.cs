using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Leitor de cor de face DIRETO, baseado na API real do Solid Edge 2023
    /// (descoberta por introspecção COM):
    ///
    ///   Face.Style  -> objeto Style (aparência/material) com os canais de cor
    ///                  como propriedades 0..1: DiffuseRed / DiffuseGreen / DiffuseBlue
    ///                  (e o método GetDiffuse(out r, out g, out b) como alternativa).
    ///
    /// A cor de "queima" pintada pelo usuário corresponde ao Diffuse do estilo da
    /// face. Valores vêm em 0..1 e são convertidos para 0..255.
    /// </summary>
    public sealed class FaceStyleColorReader : IFaceColorReader
    {
        public bool TryReadColor(dynamic comFace, dynamic application, out Color color, out string source)
        {
            color = default(Color);
            source = null;

            // Caminho 1/2: cor pintada POR FACE na peça (face.Style.Diffuse / GetDiffuse).
            object styleObj = SafeGet(() => comFace.Style);
            if (TryReadStyleColor(styleObj, out color))
            {
                source = "face.Style.Diffuse";
                return true;
            }

            // Caminho 3: Face.GetRGBAVals — cor do CORPO (não cobre pintura por feature,
            // que é camada de exibição; o mapa de features no FaceSelector cobre esse caso).
            if (TryGetRGBAVals(comFace, out color))
            {
                source = "Face.GetRGBAVals";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Lê a cor de um objeto Style (de face OU de feature — <c>feature.GetStyle()</c>
        /// devolve o mesmo tipo): canais Diffuse{Red,Green,Blue} 0..1, ou GetDiffuse.
        /// </summary>
        public static bool TryReadStyleColor(object styleObj, out Color color)
        {
            color = default(Color);
            if (styleObj == null) return false;

            if (TryDouble(styleObj, "DiffuseRed", out double r)
                && TryDouble(styleObj, "DiffuseGreen", out double g)
                && TryDouble(styleObj, "DiffuseBlue", out double b))
            {
                color = FromUnit(r, g, b);
                return true;
            }
            return TryGetDiffuse(styleObj, out color);
        }

        /// <summary>
        /// Cor efetiva via Face.GetRGBAVals([out] R,G,B,A) — 4 doubles (0..1). Os args
        /// são [out]: precisam de ParameterModifier by-ref, senão voltam 0 (mesmo bug do
        /// GetRange). Cobre pintura por FEATURE (onde face.Style é null).
        /// </summary>
        private static bool TryGetRGBAVals(object comFace, out Color color)
        {
            color = default(Color);
            try
            {
                object[] args = { 0.0, 0.0, 0.0, 0.0 };
                var mod = new ParameterModifier(4);
                mod[0] = mod[1] = mod[2] = mod[3] = true;
                comFace.GetType().InvokeMember("GetRGBAVals", BindingFlags.InvokeMethod,
                    null, comFace, args, new[] { mod }, CultureInfo.InvariantCulture, null);
                color = FromUnit(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToDouble(args[2]));
                return true;
            }
            catch { return false; }
        }

        private static Color FromUnit(double r, double g, double b)
            => Color.FromArgb(ToByte(r), ToByte(g), ToByte(b));

        private static int ToByte(double v)
        {
            // Diffuse vem em 0..1; se por acaso vier em 0..255, usa como está.
            double s = v <= 1.0 ? v * 255.0 : v;
            int i = (int)Math.Round(s);
            return i < 0 ? 0 : (i > 255 ? 255 : i);
        }

        private static bool TryDouble(object obj, string prop, out double value)
        {
            value = 0;
            try
            {
                object raw = obj.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, obj, null);
                if (raw == null) return false;
                value = Convert.ToDouble(raw);
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetDiffuse(object styleObj, out Color color)
        {
            color = default(Color);
            try
            {
                object[] args = { 0.0, 0.0, 0.0 };
                var mod = new ParameterModifier(3);
                mod[0] = mod[1] = mod[2] = true; // [out] by-ref, senão não populam em late binding
                styleObj.GetType().InvokeMember("GetDiffuse", BindingFlags.InvokeMethod,
                    null, styleObj, args, new[] { mod }, CultureInfo.InvariantCulture, null);
                color = FromUnit(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToDouble(args[2]));
                return true;
            }
            catch { return false; }
        }

        private static object SafeGet(Func<object> accessor)
        {
            try { return accessor(); }
            catch { return null; }
        }
    }
}
