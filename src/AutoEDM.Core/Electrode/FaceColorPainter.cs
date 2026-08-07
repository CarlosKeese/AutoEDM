using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using AutoEDM.Diagnostics;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Pinta faces de queima com a cor do Ra escolhido — via <c>Body.SetFacesStyle</c>, NÃO
    /// <c>Face.Style.Diffuse* = valor</c> direto.
    ///
    /// CORREÇÃO 2026-07-21 (Carlos: "Aplicar GAP não muda a cor da superfície"): o log real
    /// mostrou "0/20 face(s) pintada(s)" com <c>Face.Style (0):</c> — ou seja, <c>Face.Style</c>
    /// vinha <c>null</c> em TODAS as faces (elas não têm override de cor PRÓPRIO — herdam do
    /// corpo/feature), e escrever <c>DiffuseRed</c> num <c>Style</c> nulo falha silenciosamente
    /// (capturado pelo try/catch, cada face vira "não pintada" sem erro visível). O GAP em si
    /// (FaceOffsets.AddEx) aplicou OK no mesmo log — só a cor que não pegava.
    ///
    /// Fix: <c>Body.SetFacesStyle(NumberOfFaces, FacesArray, FaceStyle)</c> — confirmado no dump
    /// da typelib SE 2023 (interface <c>_IDMDBody</c>) — é o comando que de fato CRIA o override
    /// por face (mesmo efeito da pintura manual no SE), em vez de exigir que o override já
    /// exista. Precisa de um objeto <see cref="FaceStyle"/> pronto: cria/reusa um estilo NOMEADO
    /// por Ra (<c>Document.FaceStyles.Add(Name, Parent)</c>, nome determinístico
    /// <c>AutoEDM_Ra_X_X</c>) em vez de acumular um estilo novo a cada clique.
    ///
    /// PRIMEIRA VEZ que este código toca <c>FaceStyles</c>/<c>SetFacesStyle</c> — compilado OK,
    /// AINDA NÃO testado ao vivo. NUNCA lança: cor é secundária ao GAP/posicionamento, que já
    /// funcionam sem ela.
    /// </summary>
    public static class FaceColorPainter
    {
        /// <summary>Pinta as faces dadas com a cor de <paramref name="ra"/>. Faces devem
        /// pertencer todas ao MESMO corpo (usa <c>faces[0].Body</c> como alvo do
        /// SetFacesStyle).</summary>
        public static void Paint(dynamic partDoc, IReadOnlyList<object> faces, Color color, double ra)
        {
            if (faces == null || faces.Count == 0) { Log.Warn("Cor: sem faces p/ pintar."); return; }

            dynamic faceStyle = GetOrCreateRaFaceStyle(partDoc, color, ra);
            if (faceStyle == null) { Log.Warn("Cor: sem FaceStyle utilizável — pintura pulada (GAP/posicionamento não são afetados)."); return; }

            System.Array farr = ToTypedFaceArray(faces);
            if (farr.Length == 0) { Log.Warn("Cor: nenhuma face tipável (E_NOINTERFACE) p/ SetFacesStyle."); return; }

            try
            {
                dynamic body = ((dynamic)faces[0]).Body;
                body.SetFacesStyle(farr.Length, farr, faceStyle);
                Log.Info($"Cor: {farr.Length}/{faces.Count} face(s) pintada(s) via Body.SetFacesStyle ✓ (RGB {color.R},{color.G},{color.B}).");
            }
            catch (Exception e)
            {
                Log.Warn("Cor: Body.SetFacesStyle falhou — " + e.GetBaseException().Message);
            }
        }

        /// <summary>Acha (por nome) ou cria o FaceStyle nomeado deste Ra no documento, e garante
        /// que o Diffuse dele está no RGB pedido (caso um Ra tenha mudado de cor no mapa).</summary>
        private static dynamic GetOrCreateRaFaceStyle(dynamic partDoc, Color color, double ra)
        {
            string name = "AutoEDM_Ra_" + ra.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', '_');

            dynamic styles;
            try { styles = partDoc.FaceStyles; }
            catch (Exception e) { Log.Warn("Cor: Document.FaceStyles inacessível — " + e.GetBaseException().Message); return null; }

            dynamic style = null;
            try { style = styles.Item(name); } catch { style = null; }
            if (style == null)
            {
                try { style = styles.Add(name, ""); Log.Info($"Cor: FaceStyle '{name}' criado."); }
                catch (Exception e)
                {
                    Log.Warn($"Cor: criar FaceStyle '{name}' falhou — " + e.GetBaseException().Message);
                    return null;
                }
            }

            try
            {
                style.DiffuseRed = color.R / 255f;
                style.DiffuseGreen = color.G / 255f;
                style.DiffuseBlue = color.B / 255f;
            }
            catch (Exception e) { Log.Warn($"Cor: ajustar Diffuse* de '{name}' falhou (segue com a cor atual do estilo) — " + e.GetBaseException().Message); }

            return style;
        }

        private static System.Array ToTypedFaceArray(IReadOnlyList<object> faces)
        {
            var list = new List<SolidEdgeGeometry.Face>(faces.Count);
            int fail = 0;
            foreach (var f in faces) { try { list.Add((SolidEdgeGeometry.Face)f); } catch { fail++; } }
            if (fail > 0) Log.Warn($"Cor: {fail}/{faces.Count} face(s) não expõem a interface Face (E_NOINTERFACE) — ignoradas.");
            return list.ToArray();
        }
    }
}
