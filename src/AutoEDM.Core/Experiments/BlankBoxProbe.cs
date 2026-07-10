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
    /// Após o bloco+furo, faz a DESCOBERTA DA API DE ROSCA (TryThreadApi): dumpa a
    /// assinatura completa de HoleDataCollection.Add, os membros do HoleData vivo e a
    /// coleção Model.Threads — p/ decidir como criar o M6 ROSCADO sem chutar.
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
            Log.Info("--- FURO CEGO Ø5×8mm via FEATURE DE FUROS (Holes.AddFinite) ---");
            try
            {
                dynamic plane = partDoc.RefPlanes.Item(1);
                dynamic ps = partDoc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);

                // CENTRO do furo: Profile.Holes2d.Add(x,y). A feature de furos lê estes
                // marcadores como centros — Circles2d (círculo comum) NÃO serve (o
                // AddFinite "passa" mas cria 0 furos). METROS.
                prof.Holes2d.Add(0.0, 0.0);
                prof.End(1);
                Log.Info("Centro do furo marcado (Holes2d.Add) + Profile.End(1) OK.");

                // HoleData: furo simples Ø5 (igRegularHole=33). Ø em METROS.
                dynamic holeData = partDoc.HoleDataCollection.Add(33, 0.005);
                Log.Info("HoleData Ø5 (igRegularHole=33) criado.");

                // Furo CEGO 8mm. Em SÍNCRONO (template padrão), usar AddSync p/ virar feature
                // no PathFinder; AddFinite é o método ORDENADO (cria a geometria mas a feature
                // não entra na árvore de uma peça síncrona).
                //   AddSync(NumProfiles, Profile[], ProfilePlaneSide, ExtentType=igFinite(13), FiniteDepth, HoleData)
                //   AddFinite(Profile, ProfilePlaneSide, FiniteDepth, HoleData)
                dynamic model = partDoc.Models.Item(1);
                dynamic holes = model.Holes;
                int mode = 1; try { mode = (int)partDoc.ModelingMode; } catch { }
                Log.Info($"ModelingMode = {mode} (1=síncrono, 2=ordenado).");
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };

                bool holed = false;
                foreach (int side in new[] { 1, 2 })
                {
                    try
                    {
                        if (mode == 2) holes.AddFinite(prof, side, 0.008, holeData);
                        else holes.AddSync(1, arr, side, 13, (object)0.008, holeData); // 13 = igFinite (cego)
                        Log.Info($"FURO CEGO CRIADO ✓ ({(mode == 2 ? "AddFinite" : "AddSync")}, side={side}, Ø5×8mm).");
                        holed = true; break;
                    }
                    catch (Exception e) { Log.Warn($"Furo side={side} falhou: " + e.GetBaseException().Message); }
                }
                if (!holed) Log.Warn("Furo não criado — ver erros acima.");

                // Apaga o esboço ORDENADO do furo por código (ProfileSet.Delete). Em
                // síncrono a feature de furo sobrevive, e assim não sobra esboço ordenado
                // travado p/ o usuário (mesma lição do bloco).
                try { ps.Delete(); Log.Info("Esboço do furo apagado (ProfileSet.Delete)."); }
                catch (Exception e) { Log.Warn("Não apaguei o esboço do furo: " + e.GetBaseException().Message); }

                // DESCOBERTA da API de ROSCA (M6) — resolve o furo roscado de vez.
                TryThreadApi(partDoc, model, (object)holeData);
            }
            catch (Exception ex) { Log.Error("Etapa de furos falhou.", ex); }
        }

        /// <summary>
        /// Valida a API de ROSCA do M6 na PEÇA DESCARTÁVEL (aqui uma falha/E_FAIL não custa
        /// nada; no fluxo do eletrodo a rosca tapada envenenava o proxy COM e zerava a
        /// entrega — Log 047). A descoberta anterior (Log 047) já deu:
        ///   • `HoleDataCollection.Add` tem 21 params (idx 0=HoleType, 1=HoleDiameter,
        ///     10=ThreadMinorDiameter, 12=ThreadDepth, 18=ThreadExternalDiameter,
        ///     19=ThreadDescription);
        ///   • `HoleData` tem API de rosca rica: método `ThreadDataByDescription` +
        ///     props Standard/Size/ThreadNominalDiameter/ThreadTapDrillDiameter...;
        ///   • `Model.Threads.Add(HoleData, NumberOfCylinders, CylinderArray,
        ///     CylinderEndArray, ...)` (feature de rosca separada).
        /// Este run testa DUAS receitas e diz qual RENDERIZA a rosca:
        ///   (A) furo TAPPED numa feature só: HoleData(igTappedHole) + ThreadDataByDescription
        ///       ("M6") + Holes.AddSync;
        ///   (B) furo simples + Model.Threads.Add na face cilíndrica do furo.
        /// A que funcionar entra no BlankModeler.AddFixationHoles.
        /// </summary>
        private static void TryThreadApi(dynamic partDoc, dynamic model, object holeData)
        {
            Log.Info("===== TESTE DA API DE ROSCA (M6) — peça descartável =====");

            // Assinaturas que faltaram no Log 047 (métodos p/ POPULAR a rosca). Introspecta
            // ANTES de qualquer criação — é o valor garantido do run.
            try
            {
                ComDiagnostics.LogSignatures(holeData,
                    "ThreadDataByDescription", "ThreadDataByStandard", "Standard", "Size", "SubType", "Fit",
                    "ThreadNominalDiameter", "ThreadTapDrillDiameter", "ThreadMinorDiameter",
                    "ThreadDepthMethod", "ThreadDepth", "InternalThreadDescription");
            }
            catch (Exception e) { Log.Warn("LogSignatures(HoleData rosca) falhou: " + e.GetBaseException().Message); }

            try { ComDiagnostics.LogSignatures((object)model.Threads, "Add", "AddEx"); }
            catch (Exception e) { Log.Warn("LogSignatures(Threads) falhou: " + e.GetBaseException().Message); }

            TryTappedHoleOneShot(partDoc, model); // Receita A
            TryThreadFeature(partDoc, model);      // Receita B

            Log.Info("===== FIM (rosca) — diga qual (A/B) renderizou a rosca; levo pro eletrodo. =====");
        }

        /// <summary>Receita A: furo roscado numa feature só (HoleData tapped + ThreadDataByDescription + AddSync).</summary>
        private static void TryTappedHoleOneShot(dynamic partDoc, dynamic model)
        {
            Log.Info("--- RECEITA A: furo TAPPED (HoleData igTappedHole + ThreadDataByDescription('M6') + AddSync) ---");
            dynamic ps = null;
            try
            {
                dynamic prof = (ps = partDoc.ProfileSets.Add()).Profiles.Add(partDoc.RefPlanes.Item(1));
                prof.Holes2d.Add(0.012, 0.012); // longe do furo simples anterior
                prof.End(1);

                dynamic hd = partDoc.HoleDataCollection.Add(37, 0.005); // 37 = igTappedHole, Ø5 broca
                try { hd.ThreadDataByDescription("M6"); Log.Info("  ThreadDataByDescription('M6') OK."); }
                catch (Exception e) { Log.Warn("  ThreadDataByDescription('M6') falhou: " + e.GetBaseException().Message); }
                LogThreadState(hd);

                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                try
                {
                    model.Holes.AddSync(1, arr, 1, 13, (object)0.008, hd); // cego 8mm, igFinite=13
                    Log.Info("  RECEITA A: FURO CRIADO ✓ — CONFIRME NA TELA se saiu ROSCADO.");
                }
                catch (Exception e) { Log.Warn("  RECEITA A: AddSync tapped falhou: " + e.GetBaseException().Message); }
            }
            catch (Exception ex) { Log.Warn("  RECEITA A falhou: " + ex.GetBaseException().Message); }
            finally { try { if (ps != null) ps.Delete(); } catch { } }
        }

        /// <summary>Receita B: furo simples + feature de rosca separada Model.Threads.Add na face cilíndrica.</summary>
        private static void TryThreadFeature(dynamic partDoc, dynamic model)
        {
            Log.Info("--- RECEITA B: furo simples + Model.Threads.Add(HoleData, 1, cyl[], cylEnd[]) ---");
            dynamic ps = null;
            try
            {
                dynamic prof = (ps = partDoc.ProfileSets.Add()).Profiles.Add(partDoc.RefPlanes.Item(1));
                prof.Holes2d.Add(-0.012, -0.012);
                prof.End(1);
                dynamic hdPlain = partDoc.HoleDataCollection.Add(33, 0.005);
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                model.Holes.AddSync(1, arr, 1, 13, (object)0.008, hdPlain);
                try { ps.Delete(); ps = null; } catch { }
                Log.Info("  furo simples Ø5 criado; procurando face cilíndrica (Body.Faces[igQueryCylinder=10])...");

                dynamic cyls = model.Body.Faces[10];
                int nc = 0; try { nc = (int)cyls.Count; } catch { }
                Log.Info($"  faces cilíndricas no corpo: {nc}.");
                if (nc < 1) { Log.Warn("  sem face cilíndrica — RECEITA B abortada."); return; }
                var cylArr = new SolidEdgeGeometry.Face[] { (SolidEdgeGeometry.Face)cyls.Item(nc) };

                dynamic hdThread = partDoc.HoleDataCollection.Add(37, 0.005);
                try { hdThread.ThreadDataByDescription("M6"); } catch (Exception e) { Log.Warn("  (B) ThreadDataByDescription: " + e.GetBaseException().Message); }

                // CylinderEndArray: formato incerto — tenta o mesmo array de faces e um vazio.
                dynamic threads = model.Threads;
                foreach (var tag in new[] { "cylEnd=cyl[]", "cylEnd=empty" })
                {
                    try
                    {
                        object endArg = tag == "cylEnd=cyl[]" ? (object)cylArr : (object)new SolidEdgeGeometry.Face[0];
                        threads.Add(hdThread, 1, cylArr, endArg);
                        Log.Info($"  RECEITA B: THREADS.ADD OK ✓ ({tag}) — CONFIRME a rosca na tela.");
                        return;
                    }
                    catch (Exception e) { Log.Warn($"  RECEITA B Threads.Add ({tag}) falhou: " + e.GetBaseException().Message); }
                }
            }
            catch (Exception ex) { Log.Warn("  RECEITA B falhou: " + ex.GetBaseException().Message); }
            finally { try { if (ps != null) ps.Delete(); } catch { } }
        }

        /// <summary>Lê de volta algumas props de rosca do HoleData p/ ver se ThreadDataByDescription populou.</summary>
        private static void LogThreadState(dynamic hd)
        {
            var props = new[] { "HoleType", "ThreadDescription", "InternalThreadDescription",
                "Standard", "Size", "ThreadNominalDiameter", "ThreadMinorDiameter", "ThreadTapDrillDiameter" };
            var sb = new System.Text.StringBuilder("  HoleData rosca: ");
            foreach (var p in props)
            {
                try { object v = ((object)hd).GetType().InvokeMember(p, BindingFlags.GetProperty, null, hd, null); sb.Append($"{p}={v}; "); }
                catch { }
            }
            Log.Info(sb.ToString());
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
