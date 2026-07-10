using System;
using System.Reflection;
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
        public static void CreateBox(dynamic partDoc, double sizeXmm, double sizeYmm, double heightMm,
            int planeIndex = 1, int extrudeSide = 2)
        {
            double hx = sizeXmm / 2000.0, hy = sizeYmm / 2000.0; // metade da seção, em METROS
            double h = heightMm / 1000.0;                        // altura em METROS

            dynamic plane = partDoc.RefPlanes.Item(planeIndex);
            Log.Info($"Bloco: sketch no plano '{SafeName(plane)}' (Item({planeIndex})), " +
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
            lines.AddBy2Points(-hx, -hy, hx, -hy);
            lines.AddBy2Points(hx, -hy, hx, hy);
            lines.AddBy2Points(hx, hy, -hx, hy);
            lines.AddBy2Points(-hx, hy, -hx, -hy);
            profile.End(1); // 1 = perfil fechado

            var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
            ((object)partDoc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion",
                BindingFlags.InvokeMethod, null, partDoc.Models,
                new object[] { 1, arr, extrudeSide, h });

            // Sketch criada por código -> apagar (síncrono; o bloco sobrevive).
            try { profileSet.Delete(); }
            catch (Exception e) { Log.Warn($"ProfileSet.Delete(): {e.GetBaseException().Message}"); }
        }

        private static string SafeName(dynamic o) { try { return (string)o.Name; } catch { return "?"; } }

        /// <summary>
        /// Fura a fixação no TOPO da base, cega: M6 central (Ø5×8) + 2×Ø4×9 a 15 mm,
        /// alinhados ao MAIOR lado do blank. A base foi extrudada +Z a partir de
        /// Item(1)=origem=superfície; então cria um plano paralelo no topo (offset
        /// +normal por holderH) e fura para dentro (–normal). Cada furo é 1 perfil (o
        /// grupo com 2 Holes2d fazia só 1). Apaga esboços e o plano de topo (ordenados).
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
        public static void AddFixationHoles(dynamic partDoc, double blockXmm, double blockYmm, double holderHmm, FixationPattern fix = null)
        {
            fix = fix ?? new FixationPattern();
            try
            {
                int mode = 1; try { mode = (int)partDoc.ModelingMode; } catch { }

                dynamic topPlane;
                try
                {
                    topPlane = partDoc.RefPlanes.AddParallelByDistance(
                        partDoc.RefPlanes.Item(1), holderHmm / 1000.0, 2,   // 2 = igRight (+normal = topo da base)
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                }
                catch (Exception e) { Log.Warn($"Plano de topo (AddParallelByDistance) falhou: {e.GetBaseException().Message}"); return; }

                bool alongX = blockXmm >= blockYmm;
                Log.Info($"Furos (ModelingMode={mode}): M6 Ø{fix.CenterTapDrillDiameter:0.#}×{fix.CenterHoleDepth:0.#} (broca de rosca) + " +
                         $"2×Ø{fix.DowelDiameter:0.#}×{fix.DowelDepth:0.#} @ {fix.DowelCenterDistance:0.#} (maior lado " +
                         (alongX ? "X" : "Y") + ").");

                dynamic model = partDoc.Models.Item(1);
                dynamic holes = model.Holes;

                // M6 = furo SIMPLES Ø5 (igRegularHole=33), receita validada. (Rosca só no probe.)
                object m6 = partDoc.HoleDataCollection.Add(33, fix.CenterTapDrillDiameter / 1000.0);
                HoleAt(partDoc, holes, mode, topPlane, 0.0, 0.0, m6, fix.CenterHoleDepth, "M6 (Ø5)");

                // 2×Ø4 ao longo do MAIOR lado (perfis SEPARADOS — 1 furo por AddSync).
                double half = fix.DowelCenterDistance / 2.0;
                object hd4 = partDoc.HoleDataCollection.Add(33, fix.DowelDiameter / 1000.0); // 33 = igRegularHole
                HoleAt(partDoc, holes, mode, topPlane, alongX ? -half : 0.0, alongX ? 0.0 : -half, hd4, fix.DowelDepth, "Ø4 #1");
                HoleAt(partDoc, holes, mode, topPlane, alongX ? half : 0.0, alongX ? 0.0 : half, hd4, fix.DowelDepth, "Ø4 #2");

                try { topPlane.Delete(); } catch { }
            }
            catch (Exception ex)
            {
                Log.Warn($"Furação de fixação falhou (bloco preservado): {ex.GetBaseException().Message}");
            }
        }

        /// <summary>Um furo cego no centro (mm) do plano, cortando p/ dentro da base (side=1). Apaga o esboço.</summary>
        private static void HoleAt(dynamic partDoc, dynamic holes, int mode, dynamic plane,
            double cxMm, double cyMm, object holeData, double depthMm, string label)
        {
            dynamic ps = partDoc.ProfileSets.Add();
            try
            {
                dynamic prof = ps.Profiles.Add(plane);
                prof.Holes2d.Add(cxMm / 1000.0, cyMm / 1000.0);
                prof.End(1);
                double depthM = depthMm / 1000.0;
                if (mode == 2)
                    holes.AddFinite(prof, 1, depthM, holeData);
                else
                {
                    var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                    holes.AddSync(1, arr, 1, 13, (object)depthM, holeData); // side=1 (–normal, p/ dentro), 13 = igFinite
                }
                Log.Info($"  Furo {label} — ok.");
            }
            catch (Exception e) { Log.Warn($"  Furo {label} falhou: {e.GetBaseException().Message}"); }
            finally { try { ps.Delete(); } catch { } }
        }
    }
}
