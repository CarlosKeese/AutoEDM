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

        /// <summary>
        /// FAIXA DE MEDIÇÃO (anatomia real, [[electrode-anatomy]]): um degrau um pouco
        /// MENOR que o blank, <paramref name="bandHeightMm"/> mm de altura (default 5),
        /// logo ABAIXO do bloco (topo da faixa = base do bloco), com um CHANFRO de
        /// orientação 1×45° no canto X+ Y− para o operador medir/orientar o eletrodo.
        ///
        /// O chanfro é desenhado DIRETO no perfil 2D (corta o canto X+ Y− com uma linha a
        /// 45°) e extrudado — MUITO mais robusto que a feature de Chamfer 3D, que exigiria
        /// achar a aresta vertical certa (frágil por COM). Mesma receita validada de
        /// sketch+extrusão (<see cref="CreateBox"/>). Extrusão +Z (side=2) a partir de um
        /// plano na base da faixa; funde com o bloco (protrusão). Devolve o handle.
        ///
        /// NUNCA lança: a faixa é secundária ao bloco.
        /// </summary>
        public static dynamic AddMeasurementBand(dynamic partDoc,
            double blockXmm, double blockYmm, double bandTopZmm,
            double bandHeightMm = 5.0, double marginMm = 0.5, double chamferLegMm = 3.0,
            double centerXmm = 0.0, double centerYmm = 0.0)
        {
            object ext = null;
            try
            {
                double bandBaseZmm = bandTopZmm - bandHeightMm;
                double hx = (blockXmm / 2.0 - marginMm) / 1000.0; // meia-seção (m)
                double hy = (blockYmm / 2.0 - marginMm) / 1000.0;
                double cx = centerXmm / 1000.0, cy = centerYmm / 1000.0;
                double h = bandHeightMm / 1000.0;

                // Perna do chanfro limitada a ~40% do menor lado (não engolir a seção).
                double leg = chamferLegMm / 1000.0;
                double maxLeg = Math.Min(2 * hx, 2 * hy) * 0.4;
                if (leg > maxLeg) leg = maxLeg;
                if (leg < 1e-5 || hx <= 0 || hy <= 0)
                {
                    Log.Warn($"Faixa de medição: seção {blockXmm - 2 * marginMm:0.0}×{blockYmm - 2 * marginMm:0.0} pequena demais — pulada.");
                    return null;
                }

                dynamic plane = partDoc.RefPlanes.Item(1);
                bool lifted = false;
                if (Math.Abs(bandBaseZmm) > 1e-6)
                {
                    try
                    {
                        plane = partDoc.RefPlanes.AddParallelByDistance(
                            partDoc.RefPlanes.Item(1), bandBaseZmm / 1000.0, 2,
                            Type.Missing, Type.Missing, Type.Missing);
                        lifted = true;
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Plano da faixa (Z={bandBaseZmm:0.0}mm) falhou: {e.GetBaseException().Message}");
                        plane = partDoc.RefPlanes.Item(1);
                    }
                }

                Log.Info($"Faixa de medição: {blockXmm - 2 * marginMm:0.0}×{blockYmm - 2 * marginMm:0.0}×{bandHeightMm:0.0} mm " +
                         $"(margem {marginMm:0.0}/lado), chanfro 45° {leg * 1000:0.0}mm no canto X+ Y−, base Z={bandBaseZmm:0.0}, topo Z={bandTopZmm:0.0}.");

                dynamic profileSet = partDoc.ProfileSets.Add();
                dynamic profile = profileSet.Profiles.Add(plane);
                dynamic lines = profile.Lines2d;
                // Retângulo centrado com o canto X+ Y− (cx+hx, cy−hy) cortado a 45°.
                lines.AddBy2Points(cx - hx,       cy - hy,       cx + hx - leg, cy - hy);        // base (até o chanfro)
                lines.AddBy2Points(cx + hx - leg, cy - hy,       cx + hx,       cy - hy + leg);  // chanfro 45° (canto X+ Y−)
                lines.AddBy2Points(cx + hx,       cy - hy + leg, cx + hx,       cy + hy);        // lado direito (X+)
                lines.AddBy2Points(cx + hx,       cy + hy,       cx - hx,       cy + hy);        // topo (Y+)
                lines.AddBy2Points(cx - hx,       cy + hy,       cx - hx,       cy - hy);        // lado esquerdo (X−)
                profile.End(1);

                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
                ext = RetryStaleCom(() =>
                    ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                        BindingFlags.InvokeMethod, null, partDoc.Models,
                        new object[] { 1, arr, 2, h }), "Faixa de medição (extrusão)");

                try { profileSet.Delete(); } catch (Exception e) { Log.Warn($"ProfileSet.Delete() (faixa): {e.GetBaseException().Message}"); }
                if (lifted) { try { plane.Visible = false; } catch { } }
                Log.Info(FeatureFailed(ext) ? "  Faixa: feature com Status FALHA." : "  Faixa de medição criada ✓.");
            }
            catch (Exception ex) { Log.Warn($"Faixa de medição falhou (bloco preservado): {ex.GetBaseException().Message}"); }
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
        /// O M6 central sai ROSCADO pelo recurso de furação (Carlos, 2026-07-15): HoleData
        /// igTappedHole=37 + props de rosca preenchidas NA MÃO (M6×1,0) + AddSync — Receita A,
        /// validada nos Logs 55/58 ("FURO CRIADO ✓"). O E_FAIL antigo vinha de HoleData de
        /// rosca INCOMPLETO (`ThreadDataByDescription` falhava); com os valores na mão o furo
        /// é criado. Se ainda assim não vingar, cai no FALLBACK Ø5 simples (broca de rosca; o
        /// operador tapea) em doc fresco — nunca perde o furo central. Ver [[electrode-anatomy]].
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

                double topZmm = baseLiftMm + holderHmm; // topo do bloco (cada furo cria seu plano)

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
                Log.Info($"Furos (ModelingMode={mode}): M6 ROSCADO (broca Ø{fix.CenterTapDrillDiameter:0.#})×{fix.CenterHoleDepth:0.#} + " +
                         $"2×Ø{fix.DowelDiameter:0.#}×{fix.DowelDepth:0.#} @ {fix.DowelCenterDistance:0.#} ({layout}); bloco {blockXmm:0.0}×{blockYmm:0.0}.");

                // RAIZ dos furos (log 2026-07-15): o `Holes.AddSync` do M6 DESCONECTA o proxy
                // da peça (RPC_E_DISCONNECTED 0x80010108) — nem retry nem DelayCompute salvam
                // (o objeto morre; bloco/faixa por AddFiniteExtrudedProtrusion NÃO desconectam).
                // SOLUÇÃO: cada furo RE-ADQUIRE o documento FRESCO de Application.ActiveDocument
                // (o Application sobrevive) e cria plano/HoleData/perfil próprios. Assim a
                // desconexão do furo anterior não contamina o próximo.
                dynamic app = null; try { app = partDoc.Application; } catch { }
                if (app == null) { Log.Warn("  Fixação: sem Application para re-adquirir o doc por furo — abortando furos."); return created; }

                LogBodiesDiag(partDoc); // diagnóstico: quantos corpos (surface copiada + bloco?)

                // Central = M6 ROSCADO (Receita A). Se a rosca não vingar, refaz como Ø5 simples
                // (broca de rosca; o operador tapea) em doc FRESCO — nunca perde o furo central.
                object m6 = HoleAt(app, topZmm, fix.CenterTapDrillDiameter, fix.CenterHoleDepth, centerXmm + 0.0, centerYmm + 0.0, "M6 roscado", "M6x1.0");
                if (m6 == null || FeatureFailed(m6))
                {
                    Log.Warn("  M6 roscado não vingou — refazendo o furo central como Ø5 simples (broca de rosca).");
                    m6 = HoleAt(app, topZmm, fix.CenterTapDrillDiameter, fix.CenterHoleDepth, centerXmm + 0.0, centerYmm + 0.0, "M6 (Ø5 simples)");
                }
                created.Add(m6);
                created.Add(HoleAt(app, topZmm, fix.DowelDiameter, fix.DowelDepth, centerXmm + d1x, centerYmm + d1y, "Ø4 #1"));
                created.Add(HoleAt(app, topZmm, fix.DowelDiameter, fix.DowelDepth, centerXmm + d2x, centerYmm + d2y, "Ø4 #2"));
                created.RemoveAll(f => f == null);

                // Verificação numérica: onde os furos REALMENTE caíram (doc fresco).
                try
                {
                    VerifyHoles((dynamic)app.ActiveDocument.Models.Item(1),
                        new[] { centerXmm + 0.0, centerXmm + d1x, centerXmm + d2x }, new[] { centerYmm + 0.0, centerYmm + d1y, centerYmm + d2y },
                        new[] { fix.CenterTapDrillDiameter, fix.DowelDiameter, fix.DowelDiameter },
                        new[] { "M6 (Ø5)", "Ø4 #1", "Ø4 #2" });
                }
                catch (Exception e) { Log.Warn("  [verif] falhou: " + e.GetBaseException().Message); }
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
        /// Um furo cego. RE-ADQUIRE o documento FRESCO de <c>app.ActiveDocument</c> e cria
        /// plano/HoleData/perfil próprios — porque o <c>AddSync</c> do furo ANTERIOR desconecta
        /// o proxy da peça (RPC_E_DISCONNECTED). Corta side=1 (–normal, p/ dentro). Loga a
        /// coordenada e o Status. Apaga o esboço. Não lança (furo é secundário ao bloco).
        /// </summary>
        private static object HoleAt(dynamic app, double planeZmm, double diaMm, double depthMm,
            double cxMm, double cyMm, string label, string threadDesc = null)
        {
            object hole = null;
            dynamic ps = null;
            try
            {
                dynamic doc = app.ActiveDocument; // RCW novo — sobrevive à desconexão do furo anterior
                int mode = 1; try { mode = (int)doc.ModelingMode; } catch { }

                dynamic plane = doc.RefPlanes.AddParallelByDistance(
                    doc.RefPlanes.Item(1), planeZmm / 1000.0, 2, Type.Missing, Type.Missing, Type.Missing);

                object holeData;
                if (threadDesc != null)
                {
                    // Furo ROSCADO: o RECURSO DE FURAÇÃO renderiza a rosca (Carlos, 2026-07-15:
                    // "o furo central é M6, use a rosca"). HoleData igTappedHole=37, Ø = broca de
                    // rosca. As props de rosca vão preenchidas NA MÃO: `ThreadDataByDescription`
                    // falha e deixa o HoleData incompleto → E_FAIL no AddSync; o preenchimento
                    // manual CRIOU o furo OK (Receita A dos Logs 55/58). Ver [[electrode-anatomy]].
                    dynamic hd = doc.HoleDataCollection.Add(37, diaMm / 1000.0);
                    FillThreadDataManual(hd, threadDesc, diaMm / 1000.0);
                    holeData = (object)hd;
                }
                else holeData = doc.HoleDataCollection.Add(33, diaMm / 1000.0); // 33 = igRegularHole

                ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);
                prof.Holes2d.Add(cxMm / 1000.0, cyMm / 1000.0);
                prof.End(1);

                double depthM = depthMm / 1000.0;
                dynamic holes = doc.Models.Item(1).Holes;
                if (mode == 2)
                    hole = (object)holes.AddFinite(prof, 1, depthM, holeData);
                else
                {
                    var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                    hole = (object)holes.AddSync(1, arr, 1, 13, (object)depthM, holeData); // side=1 (–normal), 13 = igFinite
                }

                try { plane.Visible = false; } catch { }
                if (FeatureFailed(hole)) Log.Warn($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm — feature com Status FALHA.");
                else                     Log.Info($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm — ok.");
            }
            catch (Exception e) { Log.Warn($"  Furo {label} @ ({cxMm:0.0}, {cyMm:0.0}) mm falhou: {e.GetBaseException().Message}"); }
            finally { if (ps != null) { try { ps.Delete(); } catch { } } }
            return hole;
        }

        /// <summary>
        /// Preenche as props de rosca do HoleData NA MÃO (M&lt;nominal&gt;×&lt;passo&gt;, ISO
        /// métrica). Necessário porque <c>ThreadDataByDescription("M6")</c> falha e deixa o
        /// HoleData incompleto (Size=7/Ønom=0) → E_FAIL no AddSync; com os valores na mão o
        /// furo roscado é criado (Receita A, Logs 55/58). Cada set é tolerante (COM varia por
        /// versão). Diâmetro menor interno ISO: D1 = D − 1,0825·P. Ver [[electrode-anatomy]].
        /// </summary>
        private static void FillThreadDataManual(dynamic hd, string desc, double tapDrillM)
        {
            // (1) VIA TABELA DE ROSCAS DO SE — `ThreadDataByDescription` é PROPRIEDADE settable
            //     (write-only String). O BlankBoxProbe a chamava como MÉTODO
            //     (`hd.ThreadDataByDescription("M6")`) → "erro ao chamar"; o CERTO é ATRIBUIR.
            //     Atribuir popula o FORM métrico correto + Standard/SubType/diâmetros da tabela.
            //     (O fill manual só de diâmetros, SEM o standard, saía TRAPEZOIDAL + Ø10 — Log
            //     2026-07-15.) Testa variantes de designação até uma popular. Ver [[electrode-anatomy]].
            bool populated = false;
            foreach (var d in new[] { desc, "M6x1", "M6 x 1", "M6x1.0", "M6 x 1.0", "M6" })
            {
                if (string.IsNullOrEmpty(d)) continue;
                try
                {
                    hd.ThreadDataByDescription = d;                       // ATRIBUIÇÃO (não chamada)
                    double nom = 0; try { nom = (double)hd.ThreadNominalDiameter; } catch { }
                    string td = ""; try { td = (string)hd.ThreadDescription; } catch { }
                    if (nom > 0 || !string.IsNullOrEmpty(td))
                    {
                        Log.Info($"  Rosca via tabela do SE: '{d}' → desc='{td}', Ønom={nom * 1000:0.#} mm (form métrico correto).");
                        populated = true; break;
                    }
                }
                catch (Exception e) { Log.Warn($"  Rosca: ThreadDataByDescription='{d}' falhou: {e.GetBaseException().Message}"); }
            }

            // (2) FALLBACK manual (M6×1,0 ISO) — sem a tabela, o FORM pode não sair métrico.
            if (!populated)
            {
                double nominalMm = 6.0, pitchMm = 1.0;
                double minorMm = nominalMm - 1.0825 * pitchMm; // D1 interno ISO
                try { hd.Standard = "ISO Metric"; } catch { }
                try { hd.SubType = "M6"; } catch { }
                try { hd.Size = "M6"; } catch { }
                try { hd.ThreadDescription = "M6x1.0"; } catch { }
                try { hd.ThreadNominalDiameter = nominalMm / 1000.0; } catch { }
                try { hd.ThreadTapDrillDiameter = tapDrillM; } catch { }
                try { hd.ThreadMinorDiameter = minorMm / 1000.0; } catch { }
                Log.Warn($"  Rosca: tabela do SE não aceitou a descrição — preenchi na mão (M6×1,0, broca {tapDrillM * 1000:0.#}); CONFIRA o form na tela.");
            }

            // (3) LIGA a rosca — equivale ao checkbox "Rosca" da tela (Carlos, 2026-07-15: os
            //     dados saíam certos mas o furo NÃO renderizava roscado, checkbox desmarcado).
            //     ThreadSetting = igRegularThread(164) = "Standard Thread"; igNone(44) = sem rosca.
            try { hd.ThreadSetting = 164; Log.Info("  Rosca LIGADA (ThreadSetting=igRegularThread)."); }
            catch (Exception e) { Log.Warn("  Rosca: ThreadSetting=igRegularThread falhou: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// Diagnóstico: lista os corpos (Models) da peça com nome/tipo/ativo — para entender a
        /// desconexão ao furar (a peça "bloco sobre superfícies" tem a SUPERFÍCIE copiada + o
        /// bloco sólido; furar o corpo errado, ou com o corpo inativo, pode desconectar).
        /// </summary>
        private static void LogBodiesDiag(dynamic partDoc)
        {
            try
            {
                dynamic models = partDoc.Models;
                int n = 0; try { n = (int)models.Count; } catch { }
                Log.Info($"  [corpos] Models.Count = {n}");
                for (int i = 1; i <= n; i++)
                {
                    try
                    {
                        dynamic m = models.Item(i);
                        string bn = "?"; int bt = -1; bool active = false;
                        try { bn = (string)m.BodyName; } catch { }
                        try { bt = (int)m.BodyType; } catch { }
                        try { active = (bool)m.IsBodyActive; } catch { }
                        Log.Info($"  [corpos]   Model[{i}]: Body='{bn}', BodyType={bt}, IsBodyActive={active}");
                    }
                    catch (Exception e) { Log.Info($"  [corpos]   Model[{i}]: {e.GetBaseException().Message}"); }
                }
            }
            catch (Exception e) { Log.Info("  [corpos] indisponível: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// Uma chamada COM logo após um Feature.Add (AddSync/AddFinite) pode falhar
        /// transitoriamente com 0x80010114 ("O objeto solicitado não existe") enquanto o
        /// modelo ainda está regenerando — visto no Log 50 mesmo com o M6 tendo sido criado
        /// SEM erro (a causa não é exclusiva de furo roscado com E_FAIL, como se pensava
        /// nos Logs 46/47). Uma ÚNICA re-tentativa de 250ms às vezes não bastava (Log de
        /// 2026-07-14: M6 ok e o 1º Ø4 morria); agora tenta VÁRIAS vezes com backoff
        /// crescente (o modelo pode levar >250ms para reconectar o proxy) antes de propagar.
        /// </summary>
        private static readonly int[] StaleBackoffMs = { 200, 400, 800, 1500 };

        private static T RetryStaleCom<T>(Func<T> action, string what)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return action(); }
                catch (Exception ex) when (IsStaleComError(ex) && attempt < StaleBackoffMs.Length)
                {
                    int ms = StaleBackoffMs[attempt];
                    Log.Warn($"  {what}: 0x80010114 (modelo regenerando) — tentativa {attempt + 2}/{StaleBackoffMs.Length + 1} em {ms}ms...");
                    System.Threading.Thread.Sleep(ms);
                }
            }
        }

        private static bool IsStaleComError(Exception ex)
            => ex.GetBaseException() is COMException ce && unchecked((uint)ce.ErrorCode) == 0x80010114u;

        /// <summary>
        /// Liga/desliga <c>Application.DelayCompute</c> — suspende a regeneração do modelo
        /// entre operações (evita o proxy da peça ficar stale/0x80010114 após um Feature.Add).
        /// Ao desligar, chama <c>DoIdle()</c> para forçar a regeneração pendente. Devolve true
        /// se o estado foi aplicado (para o chamador saber se precisa restaurar no finally).
        /// Reusado no Cleanup do preview (deletar features sem regenerar entre elas).
        /// </summary>
        internal static bool SetDelayCompute(dynamic app, bool on)
        {
            if (app == null) return false;
            try
            {
                app.DelayCompute = on;
                if (!on) { try { app.DoIdle(); } catch { } }
                Log.Info(on
                    ? "  DelayCompute=true (suspende regeneração — evita proxy stale entre operações)."
                    : "  DelayCompute=false (regeneração retomada + DoIdle).");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"  DelayCompute({on}) indisponível: {e.GetBaseException().Message}");
                return false;
            }
        }

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
