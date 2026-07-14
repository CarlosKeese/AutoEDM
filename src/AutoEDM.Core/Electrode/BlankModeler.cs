using System;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoEDM.Diagnostics;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Modela um BLOCO sólido (blank) num PartDocument via sketch + extrusão — a receita
    /// validada nos Logs 33/34. É operação STANDALONE (no documento da peça), então NÃO
    /// precisa de edição in-place (que está bloqueada por COM).
    ///
    /// Receita: ProfileSets.Add() → Profiles.Add(RefPlanes.Item(planeIndex)) →
    /// Lines2d.AddBy2Points×4 → Profile.End(1) → Models.AddFiniteExtrudedProtrusion(
    /// 1, SolidEdgePart.Profile[] TIPADO, side, distMetros) → ProfileSet.Delete().
    /// O array de perfis precisa ser tipado (SAFEARRAY(IDispatch)); object[] falha.
    /// A sketch é criada por código (fica travada p/ o usuário) e apagada ao fim —
    /// válido em modelagem SÍNCRONA, onde o bloco não depende da sketch.
    ///
    /// Origem da peça = CENTRO da seção, na BASE do bloco (Z=0 local); o bloco sobe +Z.
    /// Assim, ao posicionar a ocorrência com PutOrigin(centro XY, fundo Z), a base do
    /// bloco encosta no fundo do bolsão e sobe em direção à superfície.
    /// </summary>
    public static class BlankModeler
    {
        private static bool _holeApiLogged;

        /// <summary>
        /// Cria o bloco (dimensões em mm) no <paramref name="partDoc"/>. Lança se a
        /// extrusão falhar. <paramref name="planeIndex"/>/<paramref name="extrudeSide"/>
        /// são calibráveis para acertar a orientação (footprint no XY, altura em +Z).
        /// </summary>
        // extrudeSide: 1=igLeft (−normal), 2=igRight (+normal), 3=igSymmetric. Para o
        // eletrodo, o bloco sobe da base (fundo) em +Z, então side=2 (Log 35: side=1 ia
        // para −Z, invertido).
        //
        // baseLiftMm: LEVANTA o bloco dentro da própria peça (2º deslocamento, pedido do
        // Carlos). A origem (Z=0) fica na SUPERFÍCIE de queima; o bloco é modelado
        // 'baseLiftMm' acima, deixando espaço para a forma copiada e o corpo do eletrodo
        // entre a superfície e a base do holder. 0 = bloco colado na origem (comportamento
        // antigo). O plano-base é criado por offset (AddParallelByDistance, mesma receita
        // validada do plano de topo dos furos) e escondido depois.
        // centerXmm/centerYmm: DESLOCA o centro da seção no plano (mm). Default 0 = na
        // origem (fluxo da montagem, onde a origem já é o centro da queima). No fluxo
        // "bloco sobre superfícies" (peça), a forma copiada pode estar longe da origem,
        // então o bloco é centrado na PEGADA das superfícies. Devolve a feature de
        // protrusão criada (p/ o Preview poder apagar).
        public static dynamic CreateBox(dynamic partDoc, double sizeXmm, double sizeYmm, double heightMm,
            int planeIndex = 1, int extrudeSide = 2, double baseLiftMm = 0.0,
            double centerXmm = 0.0, double centerYmm = 0.0)
        {
            double hx = sizeXmm / 2000.0, hy = sizeYmm / 2000.0; // metade da seção, em METROS
            double h = heightMm / 1000.0;                        // altura em METROS
            double cx = centerXmm / 1000.0, cy = centerYmm / 1000.0; // centro em METROS

            dynamic plane = partDoc.RefPlanes.Item(planeIndex);
            bool liftedPlane = false;
            if (Math.Abs(baseLiftMm) > 1e-6)
            {
                try
                {
                    plane = partDoc.RefPlanes.AddParallelByDistance(
                        partDoc.RefPlanes.Item(planeIndex), baseLiftMm / 1000.0, 2, // 2 = igRight (+normal, sobe)
                        Type.Missing, Type.Missing, Type.Missing);
                    liftedPlane = true;
                }
                catch (Exception e)
                {
                    Log.Warn($"Plano-base do bloco (lift {baseLiftMm:0.0}mm) falhou; usando Item({planeIndex}): {e.GetBaseException().Message}");
                    plane = partDoc.RefPlanes.Item(planeIndex);
                }
            }
            Log.Info($"Bloco: sketch no plano '{SafeName(plane)}' (lift {baseLiftMm:0.0}mm acima da superfície), " +
                     $"{sizeXmm:0.0}×{sizeYmm:0.0}×{heightMm:0.0} mm, side={extrudeSide}.");

            dynamic profileSet = partDoc.ProfileSets.Add();
            dynamic profile = profileSet.Profiles.Add(plane);

            // Introspecção 1-shot p/ os FUROS de fixação (M6 + 2×Ø4): descobrir a API de
            // círculos (Profile.Circles2d?) e de corte (Models.AddFiniteExtrudedCutout?).
            if (!_holeApiLogged)
            {
                _holeApiLogged = true;
                try { AutoEDM.Com.ComDiagnostics.LogMembers("Profile (furos: achar Circles2d)", (object)profile); } catch { }
                try { AutoEDM.Com.ComDiagnostics.LogMembers("Models (furos: achar cutout/hole)", (object)partDoc.Models); } catch { }
            }

            dynamic lines = profile.Lines2d;
            lines.AddBy2Points(cx - hx, cy - hy, cx + hx, cy - hy);
            lines.AddBy2Points(cx + hx, cy - hy, cx + hx, cy + hy);
            lines.AddBy2Points(cx + hx, cy + hy, cx - hx, cy + hy);
            lines.AddBy2Points(cx - hx, cy + hy, cx - hx, cy - hy);
            profile.End(1); // 1 = perfil fechado

            var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
            object ext = ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                BindingFlags.InvokeMethod, null, partDoc.Models,
                new object[] { 1, arr, extrudeSide, h });

            // Sketch criada por código -> apagar (síncrono; o bloco sobrevive).
            try { profileSet.Delete(); }
            catch (Exception e) { Log.Warn($"ProfileSet.Delete(): {e.GetBaseException().Message}"); }

            // O plano-base do lift é construção; some da árvore (só invisível, apagá-lo
            // poderia invalidar a feature em ordenado — lição do fix #4).
            if (liftedPlane) { try { plane.Visible = false; } catch { } }
            return ext;
        }

        /// <summary>
        /// Bloco REDONDO (blank cilíndrico do catálogo, ex.: RED) — círculo por sketch +
        /// extrusão, mesma receita validada do <see cref="CreateBox"/> (Circles2d.AddByCenterRadius
        /// é validado, Log dos furos). <paramref name="baseLiftMm"/> levanta a base como no box.
        /// </summary>
        public static dynamic CreateCylinder(dynamic partDoc, double diameterMm, double heightMm,
            int planeIndex = 1, int extrudeSide = 2, double baseLiftMm = 0.0,
            double centerXmm = 0.0, double centerYmm = 0.0)
        {
            double r = diameterMm / 2000.0; // raio em METROS
            double h = heightMm / 1000.0;
            double cx = centerXmm / 1000.0, cy = centerYmm / 1000.0; // centro em METROS

            dynamic plane = partDoc.RefPlanes.Item(planeIndex);
            bool liftedPlane = false;
            if (Math.Abs(baseLiftMm) > 1e-6)
            {
                try
                {
                    plane = partDoc.RefPlanes.AddParallelByDistance(
                        partDoc.RefPlanes.Item(planeIndex), baseLiftMm / 1000.0, 2,
                        Type.Missing, Type.Missing, Type.Missing);
                    liftedPlane = true;
                }
                catch (Exception e)
                {
                    Log.Warn($"Plano-base do cilindro (lift {baseLiftMm:0.0}mm) falhou: {e.GetBaseException().Message}");
                    plane = partDoc.RefPlanes.Item(planeIndex);
                }
            }
            Log.Info($"Bloco REDONDO: sketch no plano '{SafeName(plane)}' (lift {baseLiftMm:0.0}mm), " +
                     $"Ø{diameterMm:0.0}×{heightMm:0.0} mm, side={extrudeSide}.");

            dynamic profileSet = partDoc.ProfileSets.Add();
            dynamic profile = profileSet.Profiles.Add(plane);
            profile.Circles2d.AddByCenterRadius(cx, cy, r);
            profile.End(1);

            var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
            object ext = ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                BindingFlags.InvokeMethod, null, partDoc.Models,
                new object[] { 1, arr, extrudeSide, h });

            try { profileSet.Delete(); }
            catch (Exception e) { Log.Warn($"ProfileSet.Delete(): {e.GetBaseException().Message}"); }
            if (liftedPlane) { try { plane.Visible = false; } catch { } }
            return ext;
        }

        private static string SafeName(dynamic o) { try { return (string)o.Name; } catch { return "?"; } }

        /// <summary>
        /// Os furos de fixação (M6 central + 2×Ø4 a DowelCenterDistance) CABEM no bloco
        /// (com folga <paramref name="fixEdge"/> até a borda)? Considera as duas colocações:
        /// ao longo do maior lado (eixo) OU na diagonal a 45° (bloco pequeno/quadrado). Se
        /// nenhuma couber, o chamador usa o EIXO de fixação (<see cref="AddShaft"/>).
        /// </summary>
        public static bool FixationHolesFit(double blockXmm, double blockYmm, FixationPattern fix, double fixEdge = 2.0)
        {
            fix = fix ?? new FixationPattern();
            double span  = fix.DowelCenterDistance + fix.DowelDiameter + 2 * fixEdge;                 // eixo dos 2×Ø4
            double cross = fix.DowelDiameter + 2 * fixEdge;                                            // largura
            double diag  = fix.DowelCenterDistance / Math.Sqrt(2.0) + fix.DowelDiameter + 2 * fixEdge; // 45°
            double lng = Math.Max(blockXmm, blockYmm), sht = Math.Min(blockXmm, blockYmm);
            bool axisFits = lng >= span && sht >= cross;
            bool diagFits = sht >= diag;
            return axisFits || diagFits;
        }

        /// <summary>
        /// Fixação ALTERNATIVA por EIXO: quando os furos M6+2×Ø4 não cabem, modela um EIXO
        /// cilíndrico no TOPO do bloco (para prender num suporte com furo + parafuso lateral).
        /// Usa Ø <see cref="FixationPattern.ShaftDiameterLarge"/> (9,6) quando cabe no topo com
        /// folga; senão o menor (6,1). Cilindro por sketch(círculo)+extrusão +Z, no plano de topo.
        /// NUNCA lança (fixação é secundária ao bloco).
        /// </summary>
        public static dynamic AddShaft(dynamic partDoc, double blockXmm, double blockYmm, double blockTopZmm,
            FixationPattern fix = null, double centerXmm = 0.0, double centerYmm = 0.0)
        {
            fix = fix ?? new FixationPattern();
            object shaft = null;
            try
            {
                double margin = 2.0; // folga do eixo até a borda do topo (mm)
                double maxDia = Math.Min(blockXmm, blockYmm) - 2 * margin;
                double dia = fix.ShaftDiameterLarge <= maxDia ? fix.ShaftDiameterLarge : fix.ShaftDiameterSmall;
                bool tight = dia > maxDia;
                double cx = centerXmm / 1000.0, cy = centerYmm / 1000.0; // centro do eixo (= centro do bloco), METROS
                Log.Info($"Eixo de fixação Ø{dia:0.#}×{fix.ShaftHeight:0.0} no topo (bloco {blockXmm:0.0}×{blockYmm:0.0}, topo Z={blockTopZmm:0.0})" +
                         (tight ? " [APERTADO na borda — confira]" : "") + ".");

                dynamic plane;
                try
                {
                    plane = partDoc.RefPlanes.AddParallelByDistance(
                        partDoc.RefPlanes.Item(1), blockTopZmm / 1000.0, 2,
                        Type.Missing, Type.Missing, Type.Missing);
                }
                catch (Exception e) { Log.Warn($"Plano do topo p/ eixo falhou: {e.GetBaseException().Message}"); return null; }

                dynamic ps = partDoc.ProfileSets.Add();
                try
                {
                    dynamic prof = ps.Profiles.Add(plane);
                    prof.Circles2d.AddByCenterRadius(cx, cy, dia / 2000.0);
                    prof.End(1);
                    var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                    // side=2: sobe +Z a partir do topo do bloco (protrusão, funde com o bloco).
                    shaft = RetryStaleCom(() =>
                    {
                        return ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                            BindingFlags.InvokeMethod, null, partDoc.Models,
                            new object[] { 1, arr, 2, fix.ShaftHeight / 1000.0 });
                    }, "Eixo (extrusão)");
                    Log.Info(FeatureFailed(shaft) ? "  Eixo: feature com Status FALHA." : $"  Eixo Ø{dia:0.#} criado ✓.");
                }
                finally { try { ps.Delete(); } catch { } }
                try { plane.Visible = false; } catch { }
            }
            catch (Exception ex) { Log.Warn($"Eixo de fixação falhou (bloco preservado): {ex.GetBaseException().Message}"); }
            return shaft;
        }

        /// <summary>
        /// Fura a fixação no TOPO da base, cega: M6 central (Ø5×8) + 2×Ø4×9 a 15 mm,
        /// alinhados ao MAIOR lado do blank (quadrado => X). Os perfis são desenhados num
        /// PLANO OFFSET no topo (AddParallelByDistance, aridade 6) — desenhar direto na
        /// face de topo dava InvalidCastException no cast do Profile (Log 49). Cada furo
        /// é 1 perfil (2 Holes2d num perfil só fazia 1); corta side=1 (–normal, p/ dentro).
        ///
        /// AUTO-VERIFICAÇÃO: após furar, lê as faces CILÍNDRICAS do corpo e loga o centro
        /// XY/Ø/faixa Z reais de cada furo, comparando com o pedido — se o frame 2D do
        /// plano offset estiver deslocado (suspeita do Log 48, Ø4 fora do lugar), o log
        /// mostra o delta exato em vez de depender de inspeção visual.
        ///
        /// O M6 é o FURO DA BROCA DE ROSCA (Ø5 SIMPLES) — o operador tapea a rosca M6. A
        /// renderização da rosca via COM (HoleData tapped / Model.Threads.Add) está em
        /// validação no BlankBoxProbe e NÃO entra aqui: um HoleData tapped (igTappedHole)
        /// fazia Holes.AddSync retornar E_FAIL, que ENVENENAVA o proxy COM (a chamada
        /// seguinte estourava 0x80010114) e abortava o eletrodo inteiro (Log 047: 0/3).
        ///
        /// NUNCA lança: furos são secundários ao bloco, então qualquer falha é logada e
        /// o bloco segue para o SaveAs.
        /// </summary>
        public static System.Collections.Generic.List<object> AddFixationHoles(dynamic partDoc, double blockXmm, double blockYmm, double holderHmm,
            double baseLiftMm = 0.0, FixationPattern fix = null, double centerXmm = 0.0, double centerYmm = 0.0)
        {
            fix = fix ?? new FixationPattern();
            var created = new System.Collections.Generic.List<object>();
            try
            {
                int mode = 1; try { mode = (int)partDoc.ModelingMode; } catch { }
                dynamic model = partDoc.Models.Item(1);

                // Diagnóstico da direção/posição da extrusão: com a origem na SUPERFÍCIE e o
                // bloco levantado, esperado Z ∈ [baseLift, baseLift+H]. Se sair [-H,...], o
                // side do CreateBox está invertido.
                FindTopPlanarFace(model, out double bzMin, out double bzMax);
                Log.Info($"  Bloco Z ∈ [{bzMin:0.0}, {bzMax:0.0}] mm (esperado [{baseLiftMm:0.0}, {baseLiftMm + holderHmm:0.0}]: origem na superfície de QUEIMA, bloco levantado {baseLiftMm:0.0}mm).");

                dynamic topPlane;
                try
                {
                    // Assinatura real (6 params): AddParallelByDistance(ParentPlane, Distance,
                    // NormalSide, [opt]Pivot, [opt]pivotorigin, [opt]Local). NormalSide=2
                    // (igRight, +normal) — validado no Log 48 (furos saíram no topo do bloco).
                    // Offset = topo do bloco = baseLift + holderH (o bloco foi levantado).
                    topPlane = partDoc.RefPlanes.AddParallelByDistance(
                        partDoc.RefPlanes.Item(1), (baseLiftMm + holderHmm) / 1000.0, 2,
                        Type.Missing, Type.Missing, Type.Missing);
                }
                catch (Exception e) { Log.Warn($"Plano de topo (AddParallelByDistance) falhou: {e.GetBaseException().Message}"); return created; }

                // Orientação dos 2×Ø4: ao longo do MAIOR lado SE couber (borda >= fixEdge);
                // senão (bloco pequeno, ex. QUAD 19) na DIAGONAL a 45°, que aproveita mais
                // espaço até a borda (regra do Carlos: "blocos 19×19 -> furos a 45°").
                double half = fix.DowelCenterDistance / 2.0;
                double fixEdge = 2.0; // folga mínima do furo até a borda do bloco (mm)
                double longDim = Math.Max(blockXmm, blockYmm), shortDim = Math.Min(blockXmm, blockYmm);
                bool axisFits = longDim  >= fix.DowelCenterDistance + fix.DowelDiameter + 2 * fixEdge
                             && shortDim >= fix.DowelDiameter + 2 * fixEdge;
                double d1x, d1y, d2x, d2y; string layout;
                if (axisFits)
                {
                    bool alongX = blockXmm >= blockYmm;
                    d1x = alongX ? -half : 0.0; d1y = alongX ? 0.0 : -half;
                    d2x = alongX ? half : 0.0;  d2y = alongX ? 0.0 : half;
                    layout = "eixo " + (alongX ? "X" : "Y");
                }
                else
                {
                    double c = half / Math.Sqrt(2.0); // projeção do meio-vão na diagonal
                    d1x = -c; d1y = -c; d2x = c; d2y = c;
                    layout = "diagonal 45°";
                }
                Log.Info($"Furos (ModelingMode={mode}): M6 Ø{fix.CenterTapDrillDiameter:0.#}×{fix.CenterHoleDepth:0.#} (broca de rosca) + " +
                         $"2×Ø{fix.DowelDiameter:0.#}×{fix.DowelDepth:0.#} @ {fix.DowelCenterDistance:0.#} ({layout}); bloco {blockXmm:0.0}×{blockYmm:0.0}.");

                // Cria TODOS os HoleData ANTES de qualquer furo. O 0x80010114 estourava no
                // HoleDataCollection.Add do Ø4 logo APÓS o AddSync do M6 — o proxy da peça
                // fica momentaneamente desconectado enquanto o modelo regenera. Criando os
                // dois HoleData com o modelo ainda estável, esse ponto de falha some.
                // (33 = igRegularHole; M6 é a broca de rosca Ø5 simples — rosca só no probe.)
                object m6 = partDoc.HoleDataCollection.Add(33, fix.CenterTapDrillDiameter / 1000.0);
                object hd4 = partDoc.HoleDataCollection.Add(33, fix.DowelDiameter / 1000.0);

                // M6 central + 2×Ø4 (perfis SEPARADOS — 1 furo por AddSync). Cada furo
                // re-obtém a coleção Holes (invalida após regeneração) e re-tenta em
                // 0x80010114 — ver HoleAt/RetryStaleCom. Os furos são deslocados pelo
                // centro do bloco (centerX/Y), = centro da pegada no fluxo de superfícies.
                created.Add(HoleAt(partDoc, mode, topPlane, centerXmm + 0.0, centerYmm + 0.0, m6, fix.CenterHoleDepth, "M6 (Ø5)"));
                created.Add(HoleAt(partDoc, mode, topPlane, centerXmm + d1x, centerYmm + d1y, hd4, fix.DowelDepth, "Ø4 #1"));
                created.Add(HoleAt(partDoc, mode, topPlane, centerXmm + d2x, centerYmm + d2y, hd4, fix.DowelDepth, "Ø4 #2"));
                created.RemoveAll(f => f == null);

                try { topPlane.Visible = false; } catch { } // plano de construção fica, só invisível

                // Verificação numérica: onde os furos REALMENTE caíram (model fresco).
                VerifyHoles(partDoc.Models.Item(1),
                    new[] { centerXmm + 0.0, centerXmm + d1x, centerXmm + d2x }, new[] { centerYmm + 0.0, centerYmm + d1y, centerYmm + d2y },
                    new[] { fix.CenterTapDrillDiameter, fix.DowelDiameter, fix.DowelDiameter },
                    new[] { "M6 (Ø5)", "Ø4 #1", "Ø4 #2" });
            }
            catch (Exception ex)
            {
                Log.Warn($"Furação de fixação falhou (bloco preservado): {ex.GetBaseException().Message}");
            }
            return created;
        }

        /// <summary>
        /// Lê as faces CILÍNDRICAS do corpo (Body.Faces[10] = igQueryCylinder) e loga o
        /// centro XY / Ø / faixa Z reais; casa cada furo pedido com o cilindro mais próximo
        /// e avisa se o desvio passa de 0,5 mm ou se o furo não foi achado. É a prova
        /// numérica de onde a furação caiu (dispensa inspeção visual p/ diagnosticar).
        /// </summary>
        private static void VerifyHoles(dynamic model, double[] expX, double[] expY, double[] expDia, string[] labels)
        {
            try
            {
                var cx = new System.Collections.Generic.List<double>();
                var cy = new System.Collections.Generic.List<double>();
                var cd = new System.Collections.Generic.List<double>();
                dynamic cyls = model.Body.Faces[10]; // igQueryCylinder
                int n = 0; try { n = (int)cyls.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f;
                    try { f = cyls.Item(i); } catch { continue; }
                    if (!AutoEDM.Selection.FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    double x = (mn[0] + mx[0]) / 2.0, y = (mn[1] + mx[1]) / 2.0, dia = mx[0] - mn[0];
                    cx.Add(x); cy.Add(y); cd.Add(dia);
                    Log.Info($"  [verif] cilindro {i}: centro ({x:0.00}, {y:0.00}) mm, Ø{dia:0.0}, Z ∈ [{mn[2]:0.0}, {mx[2]:0.0}].");
                }
                for (int k = 0; k < expX.Length; k++)
                {
                    int best = -1; double bd = double.MaxValue;
                    for (int i = 0; i < cx.Count; i++)
                    {
                        double d = Math.Sqrt((cx[i] - expX[k]) * (cx[i] - expX[k]) + (cy[i] - expY[k]) * (cy[i] - expY[k]));
                        if (d < bd) { bd = d; best = i; }
                    }
                    if (best < 0) { Log.Warn($"  [verif] {labels[k]}: NENHUM cilindro no corpo — furo não criado."); continue; }
                    if (bd > 0.5)
                        Log.Warn($"  [verif] {labels[k]}: pedido ({expX[k]:0.0}, {expY[k]:0.0}), mais próximo ({cx[best]:0.00}, {cy[best]:0.00}) — DESVIO {bd:0.00} mm!");
                    else
                        Log.Info($"  [verif] {labels[k]}: OK em ({cx[best]:0.00}, {cy[best]:0.00}) mm (Ø{cd[best]:0.0}).");
                }
            }
            catch (Exception e) { Log.Warn("  [verif] leitura dos cilindros falhou: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// Acha a face planar de MAIOR Z do corpo (a face de topo, onde vão os furos) e
        /// devolve, por out, o Z-range global do bloco (mm). Usa a leitura de bbox de face
        /// já validada (<see cref="AutoEDM.Selection.FaceGeometry.TryGetRangeMm"/>).
        /// </summary>
        private static dynamic FindTopPlanarFace(dynamic model, out double blockZminMm, out double blockZmaxMm)
        {
            blockZminMm = 0; blockZmaxMm = 0;
            dynamic best = null; double bestZ = double.NegativeInfinity;
            double gmin = double.MaxValue, gmax = double.MinValue;
            try
            {
                dynamic planar = model.Body.Faces[6]; // 6 = igQueryPlane
                int n = 0; try { n = (int)planar.Count; } catch { }
                for (int i = 1; i <= n; i++)
                {
                    object f;
                    try { f = planar.Item(i); } catch { continue; }
                    if (!AutoEDM.Selection.FaceGeometry.TryGetRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mn[2] < gmin) gmin = mn[2];
                    if (mx[2] > gmax) gmax = mx[2];
                    if (Math.Abs(mx[2] - mn[2]) > 0.01) continue; // face não-horizontal (lateral): pula
                    if (mx[2] > bestZ) { bestZ = mx[2]; best = (dynamic)f; }
                }
            }
            catch (Exception e) { Log.Warn("FindTopPlanarFace: " + e.GetBaseException().Message); }
            if (gmin <= gmax) { blockZminMm = gmin; blockZmaxMm = gmax; }
            return best;
        }

        /// <summary>
        /// Um furo cego no ponto (mm) do plano/face, cortando p/ dentro da base (side=1,
        /// –normal). Loga a coordenada e o Status da feature (pega falha silenciosa —
        /// SE não lança em furo que falha, só marca igFeatureFailed). Apaga o esboço.
        /// </summary>
        private static object HoleAt(dynamic partDoc, int mode, dynamic plane,
            double cxMm, double cyMm, object holeData, double depthMm, string label)
        {
            object hole = null;
            dynamic ps = null;
            try
            {
                // ProfileSets.Add() é a PRIMEIRA chamada COM após o AddSync do furo
                // anterior — o modelo ainda está regenerando e o proxy da peça fica
                // stale (0x80010114). Antes ela ficava FORA do try e a exceção escapava
                // p/ o catch de AddFixationHoles, abortando os furos restantes (Log 50/57/58:
                // M6 ok e os 2×Ø4 morriam). Agora vai com retry, dentro do try.
                ps = RetryStaleCom(() => (object)partDoc.ProfileSets.Add(), $"ProfileSet (furo {label})");
                dynamic prof = ps.Profiles.Add(plane);
                prof.Holes2d.Add(cxMm / 1000.0, cyMm / 1000.0);
                prof.End(1);
                double depthM = depthMm / 1000.0;
                // A coleção Holes é RE-OBTIDA dentro do retry: após um AddSync o modelo
                // regenera e a referência antiga (model.Holes) pode desconectar (0x80010114).
                hole = RetryStaleCom(() =>
                {
                    dynamic holes = partDoc.Models.Item(1).Holes; // ref fresca a cada tentativa
                    if (mode == 2)
                        return (object)holes.AddFinite(prof, 1, depthM, holeData);
                    var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                    return (object)holes.AddSync(1, arr, 1, 13, (object)depthM, holeData); // side=1 (–normal), 13 = igFinite
                }, $"Furo {label}");
                bool failed = FeatureFailed(hole);
                if (failed) Log.Warn($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm — feature com Status FALHA.");
                else        Log.Info($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm — ok.");
            }
            catch (Exception e) { Log.Warn($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm falhou: {e.GetBaseException().Message}"); }
            finally { if (ps != null) { try { ps.Delete(); } catch { } } }
            return hole;
        }

        /// <summary>
        /// Uma chamada COM logo após um Feature.Add (AddSync/AddFinite) pode falhar
        /// transitoriamente com 0x80010114 ("O objeto solicitado não existe") enquanto o
        /// modelo ainda está regenerando — visto no Log 50 mesmo com o M6 tendo sido criado
        /// SEM erro (a causa não é exclusiva de furo roscado com E_FAIL, como se pensava
        /// nos Logs 46/47). Tenta de novo 1x após um respiro antes de propagar.
        /// </summary>
        private static T RetryStaleCom<T>(Func<T> action, string what)
        {
            try { return action(); }
            catch (Exception ex) when (IsStaleComError(ex))
            {
                Log.Warn($"  {what}: 0x80010114 (modelo ainda regenerando) — nova tentativa em 250ms...");
                System.Threading.Thread.Sleep(250);
                return action();
            }
        }

        private static bool IsStaleComError(Exception ex)
            => ex.GetBaseException() is COMException ce && unchecked((uint)ce.ErrorCode) == 0x80010114u;

        /// <summary>true se a feature reporta igFeatureFailed (1216476311). SE não lança em
        /// feature que falha — só marca .Status; sem Status legível assume ok.</summary>
        private static bool FeatureFailed(dynamic feature)
        {
            try
            {
                object s = feature.Status;
                if (s == null) return false;
                return Convert.ToInt64(s) == 1216476311L; // igFeatureFailed (igFeatureOK = 1216476310)
            }
            catch { return false; }
        }
    }
}
