using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoEDM.Com;
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
    /// <c>SetFacesStyle</c> + <c>FaceStyles.Add/Item(nome)</c> validados ao vivo em 2026-09-21
    /// (ver <see cref="Paint"/>). NUNCA lança: cor é secundária ao GAP/posicionamento, que já
    /// funcionam sem ela.
    /// </summary>
    public static class FaceColorPainter
    {
        /// <summary>Pinta as faces dadas com a cor de <paramref name="ra"/>. Faces devem
        /// pertencer todas ao MESMO corpo (usa <c>faces[0].Body</c> como alvo do
        /// SetFacesStyle).</summary>
        ///
        /// CORREÇÃO 2026-09-21 (Carlos: "não pinta na maioria dos casos"; logs de 07-21 a 09-21):
        /// (1) o corpo vinha de <c>faces[0]</c> CRU — quando a seleção trazia 1 item que não é Face
        /// (o caso mais comum no log: "1/N não expõem a interface Face") e ele caía em primeiro,
        /// <c>.Body</c> estourava ("não contém uma definição para 'Body'") e NENHUMA face era
        /// pintada (49 ocorrências). Agora o corpo sai de uma face JÁ filtrada, por InvokeMember.
        /// (2) Todas as faces iam para o corpo da primeira — faces de corpos diferentes deram
        /// E_FAIL (10 ocorrências). Agora é uma chamada por corpo.
        /// (3) O "✓" só queria dizer "não lançou". Agora confere <c>Face.Style.StyleName</c> de
        /// volta e loga quantas ficaram de fato com o estilo do Ra.
        /// Validado ao vivo em peça de teste: SetFacesStyle pinta, e a pintura sobrevive ao
        /// FaceOffset do GAP (ordem pintar→offset não é o problema).
        public static void Paint(object partDoc, IReadOnlyList<object> faces, Color color, double ra)
        {
            if (faces == null || faces.Count == 0) { Log.Warn("Cor: sem faces p/ pintar."); return; }

            object faceStyle = GetOrCreateRaFaceStyle(partDoc, color, ra);
            if (faceStyle == null) { Log.Warn("Cor: sem FaceStyle utilizável — pintura pulada (GAP/posicionamento não são afetados)."); return; }
            ApplyStyle(faces, faceStyle, color);
        }

        /// <summary>
        /// Pinta as faces com um estilo que JÁ EXISTE na peça, pelo nome — o primeiro da lista
        /// que existir (ex.: "Orange", o laranja da biblioteca padrão da SE, e "Laranja", o nome
        /// numa instalação em português). O estilo achado é usado COMO ESTÁ: é a cor do usuário,
        /// e mexer no Diffuse dele repintaria tudo que já usa esse estilo. Só se nenhum existir é
        /// que se cria <paramref name="fallbackName"/> com <paramref name="fallbackColor"/>.
        /// Devolve o nome do estilo aplicado (null se nada foi pintado). NUNCA lança.
        /// </summary>
        public static string PaintWithNamedStyle(object partDoc, IReadOnlyList<object> faces,
            IEnumerable<string> preferredStyles, string fallbackName, Color fallbackColor)
        {
            try
            {
                if (faces == null || faces.Count == 0) { Log.Warn("Cor: sem faces p/ pintar."); return null; }

                object styles;
                try { styles = Get(partDoc, "FaceStyles"); }
                catch (Exception e) { Log.Warn("Cor: Document.FaceStyles inacessível — " + e.GetBaseException().Message); return null; }

                object style = null;
                foreach (string name in preferredStyles ?? new string[0])
                {
                    try { style = Call(styles, "Item", name); } catch { style = null; } // inexistente: HRESULT 0x80040B50
                    if (style != null) { Log.Info($"Cor: usando o estilo de face '{name}' da peça."); break; }
                }

                Color logged = fallbackColor;
                if (style == null)
                {
                    try { style = Call(styles, "Item", fallbackName); } catch { style = null; }
                    if (style == null)
                    {
                        try { style = Call(styles, "Add", fallbackName, ""); Log.Info($"Cor: FaceStyle '{fallbackName}' criado."); }
                        catch (Exception e) { Log.Warn($"Cor: criar FaceStyle '{fallbackName}' falhou — " + e.GetBaseException().Message); return null; }
                    }
                    try
                    {
                        Put(style, "DiffuseRed", fallbackColor.R / 255f);
                        Put(style, "DiffuseGreen", fallbackColor.G / 255f);
                        Put(style, "DiffuseBlue", fallbackColor.B / 255f);
                    }
                    catch (Exception e) { Log.Warn($"Cor: ajustar Diffuse* de '{fallbackName}' falhou — " + e.GetBaseException().Message); }
                }
                else logged = ReadDiffuse(style, fallbackColor);

                ApplyStyle(faces, style, logged);
                return ReadStyleName(style);
            }
            catch (Exception e)
            {
                Log.Warn("Cor: pintura falhou (cosmético, segue) — " + e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>O Diffuse de um estilo, só para o log dizer que cor foi aplicada.</summary>
        private static Color ReadDiffuse(object style, Color fallback)
        {
            try
            {
                int C(string p) => (int)Math.Round(Convert.ToDouble(Get(style, p)) * 255.0);
                return Color.FromArgb(C("DiffuseRed"), C("DiffuseGreen"), C("DiffuseBlue"));
            }
            catch { return fallback; }
        }

        /// <summary><c>Body.SetFacesStyle</c> por corpo, conferindo de volta face a face.</summary>
        private static void ApplyStyle(IReadOnlyList<object> faces, object faceStyle, Color color)
        {
            string styleName = ReadStyleName(faceStyle);

            List<SolidEdgeGeometry.Face> typed = OnlyFaces(faces, "Cor");
            if (typed.Count == 0) { Log.Warn("Cor: nenhuma face utilizável na seleção p/ SetFacesStyle."); return; }

            int groups = 0, failedGroups = 0;
            foreach (var group in GroupByBody(typed))
            {
                groups++;
                System.Array farr = group.Value.ToArray();
                try
                {
                    // FacesArray é SAFEARRAY(IDispatch)* — by-ref, como no teste ao vivo que pintou.
                    object[] args = { farr.Length, farr, faceStyle };
                    var mod = new ParameterModifier(args.Length);
                    mod[1] = true;
                    group.Key.GetType().InvokeMember("SetFacesStyle", BindingFlags.InvokeMethod, null, group.Key, args,
                        new[] { mod }, CultureInfo.InvariantCulture, null);
                }
                catch (Exception e)
                {
                    failedGroups++;
                    Log.Warn($"Cor: Body.SetFacesStyle falhou em {farr.Length} face(s) — " + e.GetBaseException().Message);
                }
            }

            int painted = 0;
            foreach (var f in typed)
                if (styleName != null && string.Equals(ReadStyleName(Get(f, "Style")), styleName, StringComparison.Ordinal)) painted++;

            string msg = $"Cor: {painted}/{typed.Count} face(s) com o estilo '{styleName}' conferido de volta " +
                         $"({groups} corpo(s){(failedGroups > 0 ? $", {failedGroups} com falha" : "")}; RGB {color.R},{color.G},{color.B}).";
            if (painted == typed.Count) Log.Info(msg + " ✓");
            else Log.Warn(msg);
        }

        /// <summary>
        /// Só as FACES de verdade da lista. Item que não é Face tenta o desembrulho da seleção
        /// (<c>.Object</c> — o mesmo embrulho já visto no SelectSet de montagem); o que sobrar é
        /// descartado com o TIPO COM no log, para a próxima rodada dizer o que era.
        /// </summary>
        public static List<SolidEdgeGeometry.Face> OnlyFaces(IReadOnlyList<object> items, string logTag)
        {
            var list = new List<SolidEdgeGeometry.Face>(items.Count);
            var dropped = new List<string>();
            int unwrapped = 0;
            foreach (var item in items)
            {
                if (item == null) continue;
                SolidEdgeGeometry.Face face = item as SolidEdgeGeometry.Face;
                if (face == null)
                {
                    object inner = null;
                    try { inner = Get(item, "Object"); } catch { }
                    face = inner as SolidEdgeGeometry.Face;
                    if (face != null) unwrapped++;
                }
                if (face != null) list.Add(face);
                else dropped.Add(ComDiagnostics.TypeNameOf(item) ?? "?");
            }
            if (unwrapped > 0) Log.Info($"{logTag}: {unwrapped} item(ns) da seleção desembrulhado(s) via .Object.");
            if (dropped.Count > 0)
                Log.Warn($"{logTag}: {dropped.Count}/{items.Count} item(ns) não são Face e foram ignorados — tipo(s): {string.Join(", ", dropped)}.");
            return list;
        }

        /// <summary>Agrupa as faces pelo corpo dono (identidade COM do Body).</summary>
        private static List<KeyValuePair<object, List<SolidEdgeGeometry.Face>>> GroupByBody(List<SolidEdgeGeometry.Face> faces)
        {
            var groups = new List<KeyValuePair<object, List<SolidEdgeGeometry.Face>>>();
            foreach (var f in faces)
            {
                object body;
                try { body = Get(f, "Body"); }
                catch (Exception e) { Log.Warn("Cor: Face.Body ilegível — face ignorada: " + e.GetBaseException().Message); continue; }
                if (body == null) continue;

                var match = groups.FindIndex(g => SameComObject(g.Key, body));
                if (match < 0) groups.Add(new KeyValuePair<object, List<SolidEdgeGeometry.Face>>(body, new List<SolidEdgeGeometry.Face> { f }));
                else groups[match].Value.Add(f);
            }
            return groups;
        }

        private static bool SameComObject(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            IntPtr pa = IntPtr.Zero, pb = IntPtr.Zero;
            try
            {
                pa = Marshal.GetIUnknownForObject(a);
                pb = Marshal.GetIUnknownForObject(b);
                return pa == pb;
            }
            catch { return false; }
            finally
            {
                if (pa != IntPtr.Zero) Marshal.Release(pa);
                if (pb != IntPtr.Zero) Marshal.Release(pb);
            }
        }

        private static object Get(object com, string property)
            => com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, null);

        private static object Call(object com, string method, params object[] args)
            => com.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, com, args);

        private static void Put(object com, string property, object value)
            => com.GetType().InvokeMember(property, BindingFlags.SetProperty, null, com, new[] { value });

        /// <summary>O nome do FaceStyle é <c>StyleName</c> — o objeto NÃO tem <c>Name</c> (DISP_E_UNKNOWNNAME, visto ao vivo).</summary>
        private static string ReadStyleName(object style)
        {
            if (style == null) return null;
            try { return Get(style, "StyleName") as string; } catch { return null; }
        }

        /// <summary>Acha (por nome) ou cria o FaceStyle nomeado deste Ra no documento, e garante
        /// que o Diffuse dele está no RGB pedido (caso um Ra tenha mudado de cor no mapa).</summary>
        private static object GetOrCreateRaFaceStyle(object partDoc, Color color, double ra)
        {
            string name = "AutoEDM_Ra_" + ra.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', '_');

            object styles;
            try { styles = Get(partDoc, "FaceStyles"); }
            catch (Exception e) { Log.Warn("Cor: Document.FaceStyles inacessível — " + e.GetBaseException().Message); return null; }

            object style = null;
            try { style = Call(styles, "Item", name); } catch { style = null; } // inexistente: HRESULT 0x80040B50
            if (style == null)
            {
                try { style = Call(styles, "Add", name, ""); Log.Info($"Cor: FaceStyle '{name}' criado."); }
                catch (Exception e)
                {
                    Log.Warn($"Cor: criar FaceStyle '{name}' falhou — " + e.GetBaseException().Message);
                    return null;
                }
            }

            try
            {
                Put(style, "DiffuseRed", color.R / 255f);
                Put(style, "DiffuseGreen", color.G / 255f);
                Put(style, "DiffuseBlue", color.B / 255f);
            }
            catch (Exception e) { Log.Warn($"Cor: ajustar Diffuse* de '{name}' falhou (segue com a cor atual do estilo) — " + e.GetBaseException().Message); }

            return style;
        }

    }
}
