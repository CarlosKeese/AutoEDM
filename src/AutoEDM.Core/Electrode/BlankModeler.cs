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
    }
}
