using System;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Experiments
{
    /// <summary>
    /// VALIDAÇÃO da modelagem do BLOCO DO BLANK (ideia do Carlos: gerar o sólido do
    /// blank na peça do eletrodo p/ extrair por subtração, sem cópia de faces).
    ///
    /// Cria uma PEÇA NOVA (standalone, descartável) e tenta modelar um bloco 30×20×15mm
    /// por SKETCH + EXTRUSÃO — o caminho confiável (a skill marca AddBox* como finicky;
    /// nas tentativas antigas dava DISP_E_TYPEMISMATCH). Loga CADA passo COM e a
    /// assinatura real dos métodos, p/ sabermos exatamente onde funciona/falha.
    ///
    /// Modelar no documento da PEÇA é operação standalone — NÃO precisa de edição
    /// in-place (que está bloqueada). É isso que torna a abordagem viável.
    ///
    /// Não salva nada. O usuário fecha a peça de teste depois.
    /// </summary>
    public sealed class BlankBoxProbe
    {
        public void Run(SolidEdgeConnector connector)
        {
            Log.Info("===== VALIDAÇÃO: modelar BLOCO do blank (sketch + extrusão) =====");
            dynamic app = connector.Application;
            if (app == null) { Log.Warn("Sem conexão com o Solid Edge."); return; }

            dynamic partDoc = null;
            try
            {
                Log.Info("Criando peça de teste (Documents.Add SolidEdge.PartDocument)...");
                partDoc = app.Documents.Add("SolidEdge.PartDocument");
                Log.Info("Peça de teste criada (não será salva).");

                // Plano de sketch. As 3 primeiras RefPlanes são os planos base; a Item(1)
                // costuma ser o plano onde a extrusão sobe em +normal. Logamos e usamos.
                dynamic refPlanes = partDoc.RefPlanes;
                Log.Info($"RefPlanes.Count = {SafeInt(() => refPlanes.Count)}.");
                dynamic plane = refPlanes.Item(1);

                // Introspecção-primeiro: assinaturas reais antes de chamar.
                TryLogSig((object)partDoc.ProfileSets, "Add");

                Log.Info("ProfileSets.Add()...");
                dynamic profileSet = partDoc.ProfileSets.Add();
                TryLogSig((object)profileSet.Profiles, "Add");

                Log.Info("Profiles.Add(refPlane)...");
                dynamic profile = profileSet.Profiles.Add(plane);

                dynamic lines = profile.Lines2d;
                TryLogSig((object)lines, "AddBy2Points");

                // Retângulo 30×20 mm centrado na origem (METROS). 4 linhas conectadas.
                Log.Info("Desenhando retângulo 30×20 mm (Lines2d.AddBy2Points ×4)...");
                double hx = 0.015, hy = 0.010;
                lines.AddBy2Points(-hx, -hy, hx, -hy);
                lines.AddBy2Points(hx, -hy, hx, hy);
                lines.AddBy2Points(hx, hy, -hx, hy);
                lines.AddBy2Points(-hx, hy, -hx, -hy);
                Log.Info($"Perfil: {SafeInt(() => lines.Count)} linha(s). Fechando o perfil (End)...");
                TryEndProfile(profile);

                dynamic models = partDoc.Models;
                TryLogSig((object)models, "AddFiniteExtrudedProtrusion");

                // AddFiniteExtrudedProtrusion(NumberOfProfiles, ProfileArray:SAFEARRAY(IDispatch),
                //   ProfilePlaneSide, ExtrusionDistance, ...). ProfileArray tipado (como no
                // CopySurfaces) p/ virar SAFEARRAY(IDispatch); object[] vira VARIANT e falha.
                double depth = 0.015; // 15 mm
                Log.Info("Extrudando 15 mm (AddFiniteExtrudedProtrusion)...");
                bool ok = TryExtrude(models, profile, depth);
                if (ok)
                {
                    Log.Info("BLOCO CRIADO ✓ — sketch+extrusão funciona. Caminho do blank validado.");
                    // Ressalva do Carlos: sketch criada por código fica travada p/ o
                    // usuário; em modelagem SÍNCRONA o bloco não depende dela, então
                    // apagamos a sketch (ProfileSet) após a extrusão.
                    TryDeleteSketch(profileSet, profile);

                    // FUROS de fixação: valida a receita círculo + corte extrudado.
                    TryHoles(partDoc);
                }
                else
                {
                    Log.Warn("Extrusão falhou — ver erros acima; próximo run ajusta os args/tipos.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Validação do bloco falhou.", ex);
            }
            finally
            {
                Log.Info("===== FIM (validação do bloco) — feche a peça de teste sem salvar. =====");
            }
        }

        /// <summary>
        /// Valida a receita de FURO: círculo 2D + corte extrudado. Descobre as APIs de
        /// peça que faltam no dump (Profile.Circles2d.AddByCenterRadius e o corte —
        /// Models.AddFiniteExtrudedCutout, por analogia com AddFiniteExtrudedProtrusion,
        /// ou Model.ExtrudedCutouts.Add). Loga assinaturas + tenta; apaga a sketch.
        /// </summary>
        private static void TryHoles(dynamic partDoc)
        {
            Log.Info("--- FUROS: descoberta + tentativa (círculo Ø5 + corte 8mm) ---");
            dynamic ps = null;
            try
            {
                dynamic plane = partDoc.RefPlanes.Item(1);
                ps = partDoc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);

                // Achar a coleção de círculos no Profile (Lines2d é conhecida; Circles2d?).
                try { ComDiagnostics.LogMembers("Profile (furos: achar Circles2d)", (object)prof); } catch { }

                dynamic circles;
                try { circles = prof.Circles2d; }
                catch (Exception e) { Log.Warn("prof.Circles2d indisponível: " + e.GetBaseException().Message); return; }
                try { ComDiagnostics.LogSignatures((object)circles, "AddByCenterRadius"); } catch { }

                // Círculo Ø5 mm (broca de M6) no centro. METROS -> raio 0.0025.
                Log.Info("Desenhando círculo Ø5 mm (Circles2d.AddByCenterRadius)...");
                circles.AddByCenterRadius(0.0, 0.0, 0.0025);
                try { prof.End(1); Log.Info("Profile.End(1) OK."); }
                catch (Exception e) { Log.Warn("End(1): " + e.GetBaseException().Message); }

                // Assinaturas dos candidatos a corte.
                try { ComDiagnostics.LogSignatures((object)partDoc.Models, "AddFiniteExtrudedCutout"); } catch { }
                try { ComDiagnostics.LogSignatures((object)partDoc.Models.Item(1).ExtrudedCutouts, "Add"); }
                catch (Exception e) { Log.Info("Model.ExtrudedCutouts indisponível: " + e.GetBaseException().Message); }

                // Tenta AddFiniteExtrudedCutout(1, Profile[] tipado, side, profundidade 8mm).
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                bool cut = false;
                foreach (int side in new[] { 1, 2, 3 })
                {
                    try
                    {
                        ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedCutout",
                            BindingFlags.InvokeMethod, null, partDoc.Models, new object[] { 1, arr, side, 0.008 });
                        Log.Info($"FURO CRIADO ✓ (AddFiniteExtrudedCutout, side={side}).");
                        cut = true; break;
                    }
                    catch (Exception e) { Log.Warn($"Cutout side={side} falhou: " + e.GetBaseException().Message); }
                }
                if (!cut) Log.Warn("Nenhuma variante de corte passou — ver assinaturas logadas acima p/ ajustar.");

                try { ps.Delete(); } catch (Exception e) { Log.Warn("ProfileSet.Delete (furo): " + e.GetBaseException().Message); }
            }
            catch (Exception ex)
            {
                Log.Error("Etapa de furos falhou.", ex);
                try { if (ps != null) ps.Delete(); } catch { }
            }
        }

        /// <summary>Apaga a sketch (ProfileSet) após a extrusão. Válido em modelagem síncrona.</summary>
        private static void TryDeleteSketch(dynamic profileSet, dynamic profile)
        {
            try { ComDiagnostics.LogSignatures((object)profileSet, "Delete"); } catch { }
            if (TryDel("ProfileSet.Delete()", () => profileSet.Delete())) return;
            if (TryDel("Profile.Delete()", () => profile.Delete())) return;

            // Se nenhum funcionou, logamos os membros p/ achar o método certo no próximo run.
            Log.Warn("Não apaguei a sketch — dump de membros p/ achar o método:");
            try { ComDiagnostics.LogMembers("ProfileSet", (object)profileSet); } catch { }
            try { ComDiagnostics.LogMembers("Profile", (object)profile); } catch { }
        }

        private static bool TryDel(string label, Action del)
        {
            try { del(); Log.Info($"Sketch apagada via {label}."); return true; }
            catch (Exception e) { Log.Warn($"{label} falhou: {e.GetBaseException().Message}"); return false; }
        }

        /// <summary>Fecha o perfil (obrigatório antes de extrudar). Best-effort.</summary>
        private static void TryEndProfile(dynamic profile)
        {
            // Profile.End(ProfileValidationType) — igProfileClosed costuma ser 1.
            foreach (int mode in new[] { 1, 0 })
            {
                try { profile.End(mode); Log.Info($"Profile.End({mode}) OK."); return; }
                catch (Exception e) { Log.Warn($"Profile.End({mode}) falhou: {e.GetBaseException().Message}"); }
            }
            try { profile.End(); Log.Info("Profile.End() OK."); }
            catch (Exception e) { Log.Warn($"Profile.End() falhou: {e.GetBaseException().Message}"); }
        }

        /// <summary>Tenta a extrusão com array tipado e, em fallback, object[]/aridades.</summary>
        private static bool TryExtrude(dynamic models, dynamic profile, double depth)
        {
            // ProfilePlaneSide: valores de FeaturePropertyConstants (igLeft/igRight/igSymmetric)
            // testados; distância em metros.
            foreach (int side in new[] { 1, 2, 3 })
            {
                // 1) array TIPADO SolidEdgePart.Profile[] -> SAFEARRAY(IDispatch)
                try
                {
                    var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
                    object m = ((object)models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                        BindingFlags.InvokeMethod, null, models, new object[] { 1, arr, side, depth });
                    Log.Info($"Extrusão OK (Profile[] tipado, side={side}).");
                    return true;
                }
                catch (Exception e) { Log.Warn($"Extrusão side={side} (Profile[] tipado) falhou: {e.GetBaseException().Message}"); }

                // 2) object[] (marshala como SAFEARRAY(VARIANT) — pode não servir)
                try
                {
                    object m = ((object)models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                        BindingFlags.InvokeMethod, null, models, new object[] { 1, new object[] { profile }, side, depth });
                    Log.Info($"Extrusão OK (object[], side={side}).");
                    return true;
                }
                catch (Exception e) { Log.Warn($"Extrusão side={side} (object[]) falhou: {e.GetBaseException().Message}"); }
            }
            return false;
        }

        private static void TryLogSig(object obj, string method)
        {
            try { ComDiagnostics.LogSignatures(obj, method); } catch { }
        }

        private static string SafeInt(Func<object> f)
        {
            try { return Convert.ToString(f()); } catch { return "?"; }
        }
    }
}
