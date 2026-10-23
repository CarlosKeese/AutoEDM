using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>
    /// Uma placa do porta-molde numa peça ORDENADA, toda guiada por VARIÁVEIS da peça (Carlos, 2026-10-23:
    /// "aprenda a usar variáveis do Solid Edge"). Trocar de porta-molde = trocar os valores das variáveis;
    /// o recurso só é refeito quando muda a forma do grupo de furos (4 simétricos ↔ 2 na linha de centro,
    /// passante ↔ cego) ou o grupo aparece/some.
    ///
    /// Receita medida ao vivo pela sonda tools/MoldBaseProbe (SE 2023, 2026-10-23), skill
    /// <c>references/variables.md</c>:
    /// • retângulo CENTRADO: 4 linhas com os cantos amarrados (AddKeypoint), horizontais/verticais, e uma
    ///   diagonal de construção cujo MEIO fica sobre os dois eixos projetados (ProjectRefPlane 2 e 3);
    /// • cotas por <c>Dimensions.AddLength(linha)</c> (sem ponto de escolha) com <c>Formula = variável</c>;
    /// • profundidade da extrusão / do recorte e distância do plano ligadas pelo NOME DE SISTEMA da cota
    ///   nova (não localizado), achada pela DIFERENÇA da tabela antes/depois — nunca pelo valor;
    /// • furos: retângulo de construção nos centros + círculos com <c>AddEqual</c> + uma cota de Ø.
    ///
    /// Referencial da peça: origem no CENTRO da face de baixo, placa extrudada para +Z. Sem giro, a largura
    /// (A) corre em X e o comprimento (C) em Y; girada, o contrário.
    /// </summary>
    public static class MoldBasePlateModeler
    {
        public const string VarWidth = "Largura";
        public const string VarLength = "Comprimento";
        public const string VarThickness = "Espessura";
        public const string PlateFeatureName = "PM_Placa";
        private const int UnitDistance = 1, UnitScalar = 58;
        private const int IgLeft = 1, IgRight = 2, IgSymmetric = 3;

        public static string FeatureName(HoleGroup g) => "PM_" + g;
        public static string VarAcrossA(HoleGroup g) => g + "_EntreA";
        public static string VarAlongC(HoleGroup g) => g + "_EntreC";
        public static string VarDiameter(HoleGroup g) => g + "_D";
        public static string VarDepth(HoleGroup g) => g + "_Prof";
        /// <summary>A FORMA do grupo, para saber se dá para só trocar valores: 4 ou 2 furos × passante/cego.</summary>
        public static string VarShape(HoleGroup g) => g + "_Forma";
        /// <summary>Número do nome de sistema do plano na face de cima do grupo (furo cego por cima).</summary>
        public static string VarPlane(HoleGroup g) => g + "_Plano";

        /// <summary>"RefPlane_7" → 7 (o número do nome de SISTEMA, que não é localizado); 0 se não houver.</summary>
        public static double PlaneNumber(object plane)
        {
            string sys = null;
            try { sys = (string)((dynamic)plane).SystemName; } catch { }
            if (sys == null) try { sys = (string)((dynamic)plane).Name; } catch { }
            int us = sys == null ? -1 : sys.LastIndexOf('_');
            return us >= 0 && int.TryParse(sys.Substring(us + 1), out int n) ? n : 0;
        }

        /// <summary>A forma do grupo; com centros dados um a um, também uma soma deles (outro desenho = refazer).</summary>
        public static double ShapeCode(HoleSpec h)
        {
            double code = (h.Side + 1) * 100 + h.Count * 10 + (int)h.Depth + (h.Middle ? 5 : 0);
            if (h.Points != null)
                code += 1000 * (1 + Math.Round(h.Points.Sum(p => 3 * Math.Abs(p[0]) + 7 * Math.Abs(p[1]) + p[0] + 2 * p[1]) * 10) % 99991);
            return code;
        }

        /// <summary>Peça NOVA (vazia, criada pelo chamador): ordenada, variáveis, placa e furos. Confere caixa e volume.</summary>
        public static bool Create(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            try { doc.ModelingMode = 2; } catch (Exception e) { error = "não deu para pôr a peça nova em ordenado: " + Msg(e); return false; }
            dynamic vars = doc.Variables;
            SetVar(vars, VarWidth, plate.Width, UnitDistance);
            SetVar(vars, VarLength, plate.Length, UnitDistance);
            SetVar(vars, VarThickness, plate.Thickness, UnitDistance);

            if (!CreateSlab(doc, plate, rotated, out error)) return false;
            foreach (HoleSpec h in plate.Holes)
                if (!CreateHoles(doc, plate, h, rotated, out error)) return false;
            if (plate.Pocket != null && !CreatePocket(doc, plate, rotated, out error)) return false;
            if (plate.Boss != null && !CreateBoss(doc, plate, rotated, out error)) return false;
            if (plate.Corners != null && !CreateCorners(doc, plate, rotated, out error)) return false;
            if (plate.Groove != null && !CreateGroove(doc, plate, rotated, out error)) return false;
            if (plate.Step != null && !CreateStep(doc, plate, rotated, out error)) return false;
            return Verify(doc, plate, rotated, out error);
        }

        /// <summary>
        /// Peça EXISTENTE: troca os valores; refaz só os grupos cuja forma mudou, cria os novos e apaga os que
        /// saíram. <paramref name="doc"/> tem de estar EDITÁVEL (aberto por Documents.Open) quando há recurso a
        /// refazer — o da ocorrência é somente-leitura.
        /// </summary>
        public static bool Update(dynamic doc, PlateSpec plate, bool rotated, out string error, out int rebuilt)
        {
            error = null;
            rebuilt = 0;
            // Antes de escrever qualquer coisa: o objeto é o mesmo que a montagem carregou, e um Close(false)
            // não desfaz o que foi mudado nele.
            if (FindFeature(doc, PlateFeatureName) == null) { error = $"a peça não tem o recurso '{PlateFeatureName}' — não foi criada pelo AutoEDM; nada foi mudado nela."; return false; }
            dynamic vars = doc.Variables;
            SetVar(vars, VarWidth, plate.Width, UnitDistance);
            SetVar(vars, VarLength, plate.Length, UnitDistance);
            SetVar(vars, VarThickness, plate.Thickness, UnitDistance);

            var wanted = new HashSet<HoleGroup>(plate.Holes.Select(h => h.Group));
            foreach (HoleGroup g in Enum.GetValues(typeof(HoleGroup)))
                if (!wanted.Contains(g) && FindFeature(doc, FeatureName(g)) != null)
                {
                    DeleteGroup(doc, g);
                    rebuilt++;
                }

            foreach (HoleSpec h in plate.Holes)
            {
                dynamic feature = FindFeature(doc, FeatureName(h.Group));
                double shape = GetVar(vars, VarShape(h.Group));
                if (feature != null && Math.Abs(shape - ShapeCode(h)) < 1e-6)
                {
                    SetHoleVars(vars, h);
                    continue;
                }
                if (feature != null) DeleteGroup(doc, h.Group);
                if (!CreateHoles(doc, plate, h, rotated, out error)) return false;
                rebuilt++;
            }

            dynamic pocket = FindFeature(doc, PocketFeatureName);
            if (plate.Pocket == null && pocket != null) { TryDelete(pocket); rebuilt++; }
            else if (plate.Pocket != null && pocket != null && Math.Abs(GetVar(vars, VarPocketSide) - (plate.Pocket.FromBottom ? 1 : 2)) < 1e-6)
                SetPocketVars(vars, plate.Pocket);
            else if (plate.Pocket != null)
            {
                if (pocket != null) TryDelete(pocket);
                if (!CreatePocket(doc, plate, rotated, out error)) return false;
                rebuilt++;
            }

            dynamic boss = FindFeature(doc, BossFeatureName);
            if (plate.Boss == null && boss != null) { TryDelete(boss); rebuilt++; }
            else if (plate.Boss != null && boss != null && Math.Abs(GetVar(vars, VarBossSide) - (plate.Boss.OnBottom ? 1 : 2)) < 1e-6)
                SetBossVars(vars, plate.Boss);
            else if (plate.Boss != null)
            {
                if (boss != null) TryDelete(boss);
                if (!CreateBoss(doc, plate, rotated, out error)) return false;
                rebuilt++;
            }

            // Travas e rasgo não seguem as variáveis da placa: refeitos quando a medida gravada não é a do plano.
            dynamic corners = FindFeature(doc, CornersFeatureName);
            if (corners != null && !SameSnapshot(vars, CornersVars, CornersSnapshot(plate)))
            {
                TryDelete(corners);
                corners = null;
                rebuilt++;
            }
            if (plate.Corners != null && corners == null)
            {
                if (!CreateCorners(doc, plate, rotated, out error)) return false;
                rebuilt++;
            }
            dynamic groove = FindFeature(doc, GrooveFeatureName);
            if (groove != null && !SameSnapshot(vars, GrooveVars, GrooveSnapshot(plate)))
            {
                TryDelete(groove);
                groove = null;
                rebuilt++;
            }
            if (plate.Groove != null && groove == null)
            {
                if (!CreateGroove(doc, plate, rotated, out error)) return false;
                rebuilt++;
            }
            dynamic step = FindFeature(doc, StepFeatureName);
            if (step != null && !SameSnapshot(vars, StepVars, StepSnapshot(plate)))
            {
                TryDelete(step);
                step = null;
                rebuilt++;
            }
            if (plate.Step != null && step == null)
            {
                if (!CreateStep(doc, plate, rotated, out error)) return false;
                rebuilt++;
            }
            return Verify(doc, plate, rotated, out error);
        }

        // ------------------------------------------------------------------ ressalto

        public const string BossFeatureName = "PM_Ressalto";
        private const string VarBossWidth = "Ressalto_Largura", VarBossLength = "Ressalto_Comprimento", VarBossHeight = "Ressalto_Altura", VarBossSide = "Ressalto_Lado";

        private static void SetBossVars(dynamic vars, BossSpec b)
        {
            SetVar(vars, VarBossWidth, b.Width, UnitDistance);
            SetVar(vars, VarBossLength, b.Length, UnitDistance);
            SetVar(vars, VarBossHeight, b.Height, UnitDistance);
            SetVar(vars, VarBossSide, b.OnBottom ? 1 : 2, UnitScalar);
        }

        /// <summary>
        /// Ressalto retangular centrado para FORA da placa (o encaixe da 200 da gaveta): retângulo guiado por
        /// variáveis na face de baixo ou num plano na Espessura; o lado conferido pelo volume somado — para o lado
        /// errado a extrusão cai dentro do aço e não soma nada.
        /// </summary>
        private static bool CreateBoss(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            BossSpec b = plate.Boss;
            dynamic vars = doc.Variables;
            SetBossVars(vars, b);
            double expected = b.Width * b.Length * b.Height;
            foreach (int side in b.OnBottom ? new[] { IgLeft, IgRight } : new[] { IgRight, IgLeft })
            {
                object top = b.OnBottom ? null : TopPlane(doc, plate, "ressalto (plano)");
                dynamic ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(top ?? doc.RefPlanes.Item(1));
                double hx = Units.MmToM(rotated ? b.Length : b.Width) / 2, hy = Units.MmToM(rotated ? b.Width : b.Length) / 2;
                CenteredRect(doc, prof, hx, hy, rotated ? VarBossLength : VarBossWidth, rotated ? VarBossWidth : VarBossLength, construction: false);
                int end = Convert.ToInt32(prof.End(1));
                if (end != 0) { error = $"ressalto: perfil recusado (End = {end})."; TryDelete(ps); TryDelete(top); return false; }
                double before = Volume(doc);
                HashSet<string> b1 = DimensionNames(vars);
                object feature;
                try { feature = doc.Models.Item(1).ExtrudedProtrusions.AddFinite((SolidEdgePart.Profile)prof, IgLeft, side, Units.MmToM(b.Height)); }
                catch (Exception e) { error = "ressalto: " + Msg(e); TryDelete(ps); TryDelete(top); return false; }
                double added = Volume(doc) - before;
                bool ok = !Failed(feature) && (double.IsNaN(added) || Math.Abs(added - expected) <= Math.Max(1.0, expected * 0.01));
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] ressalto {1:0.##} × {2:0.##} × {3:0.##}: lado {4}, somou {5:0} mm³ (esperado {6:0}){7}",
                    plate.Plate, b.Width, b.Length, b.Height, side, added, expected, ok ? " ✓" : " — lado errado"));
                if (ok)
                {
                    if (!Name(feature, BossFeatureName)) { error = $"o ressalto não aceitou o nome '{BossFeatureName}'."; return false; }
                    LinkNew(vars, b1, VarBossHeight, "ressalto (altura)");
                    Hide(prof);
                    if (top != null) Hide(top);
                    return true;
                }
                TryDelete(feature);
                TryDelete(top);
            }
            error = "ressalto: a extrusão não somou o volume esperado por nenhum dos lados.";
            return false;
        }

        /// <summary>Plano paralelo à face de baixo na Espessura (a face de cima), com a distância ligada à variável.</summary>
        private static object TopPlane(dynamic doc, PlateSpec plate, string what)
        {
            HashSet<string> b0 = DimensionNames(doc.Variables);
            object top = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), Units.MmToM(plate.Thickness), IgRight,
                Type.Missing, Type.Missing, Type.Missing, Type.Missing);
            LinkNew(doc.Variables, b0, VarThickness, what);
            return top;
        }

        // ------------------------------------------------------------------ travas de canto e rasgo das guias

        public const string CornersFeatureName = "PM_Cantos", GrooveFeatureName = "PM_Rasgos", StepFeatureName = "PM_Degrau";
        private static readonly string[] StepVars = { "Degrau_Recuo", "Degrau_Prof", "Degrau_Lado", "Degrau_Largura", "Degrau_Comprimento", "Degrau_Espessura" };

        private static double[] StepSnapshot(PlateSpec p) => p.Step == null ? null : new[]
        {
            p.Step.Inset, p.Step.Depth, p.Step.FromBottom ? 1 : 2, p.Width, p.Length, p.Thickness,
        };

        /// <summary>
        /// Degrau em volta da placa (o Babyplast: 76 perto da partição, 75 atrás): um anel — o retângulo de fora,
        /// 1 mm além das bordas, e o de dentro, <see cref="StepSpec.Inset"/> para dentro — cortado da face de baixo
        /// ou de cima. O lado é conferido pelo volume tirado.
        /// </summary>
        private static bool CreateStep(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            StepSpec st = plate.Step;
            double expected = (plate.Width * plate.Length - (plate.Width - 2 * st.Inset) * (plate.Length - 2 * st.Inset)) * st.Depth;
            foreach (int side in st.FromBottom ? new[] { IgRight, IgLeft } : new[] { IgLeft, IgRight })
            {
                object top = st.FromBottom ? null : TopPlane(doc, plate, "degrau (plano)");
                dynamic ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(top ?? doc.RefPlanes.Item(1));
                double hx = Units.MmToM(rotated ? plate.Length : plate.Width) / 2, hy = Units.MmToM(rotated ? plate.Width : plate.Length) / 2;
                double one = Units.MmToM(1), i = Units.MmToM(st.Inset);
                Rect(prof, -hx - one, -hy - one, hx + one, hy + one);
                Rect(prof, -hx + i, -hy + i, hx - i, hy - i);
                // Anel = perfil ANINHADO: sem igProfileAllowNested (8192) o End devolve -113 (skill, modeling-recipes).
                int end = Convert.ToInt32(prof.End(1 | 8192));
                if (end != 0) { error = $"degrau: perfil recusado (End = {end})."; TryDelete(ps); TryDelete(top); return false; }
                double before = Volume(doc);
                object cut;
                try
                {
                    object cutouts = (object)doc.Models.Item(1).ExtrudedCutouts;
                    cut = cutouts.GetType().InvokeMember("AddFiniteMulti", BindingFlags.InvokeMethod, null, cutouts,
                        new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, side, Units.MmToM(st.Depth) });
                }
                catch (Exception e) { error = "degrau: " + Msg(e); TryDelete(ps); TryDelete(top); return false; }
                double removed = before - Volume(doc);
                bool ok = !Failed(cut) && (double.IsNaN(removed) || Math.Abs(removed - expected) <= Math.Max(1.0, expected * 0.01));
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] degrau {1:0.##} por lado × {2:0.##}: lado {3}, tirou {4:0} mm³ (esperado {5:0}){6}",
                    plate.Plate, st.Inset, st.Depth, side, removed, expected, ok ? " ✓" : " — lado errado"));
                if (ok)
                {
                    if (!Name(cut, StepFeatureName)) { error = $"o degrau não aceitou o nome '{StepFeatureName}'."; return false; }
                    WriteSnapshot(doc.Variables, StepVars, StepSnapshot(plate));
                    Hide(prof);
                    if (top != null) Hide(top);
                    return true;
                }
                TryDelete(cut);
                TryDelete(top);
            }
            error = "degrau: o recorte não tirou o volume esperado por nenhum dos lados.";
            return false;
        }
        private static readonly string[] CornersVars = { "Cantos_Lado", "Cantos_Altura", "Cantos_Tipo", "Cantos_Largura", "Cantos_Comprimento", "Cantos_Espessura" };
        private static readonly string[] GrooveVars = { "Rasgo_Prof", "Rasgo_Z0", "Rasgo_Z1", "Rasgo_Largura", "Rasgo_Comprimento", "Rasgo_Espessura" };

        private static double[] CornersSnapshot(PlateSpec p) => p.Corners == null ? null : new[]
        {
            p.Corners.Size, p.Corners.Height, (p.Corners.Recess ? 10 : 0) + (p.Corners.OnBottom ? 1 : 2), p.Width, p.Length, p.Thickness,
        };

        private static double[] GrooveSnapshot(PlateSpec p) => p.Groove == null ? null : new[]
        {
            p.Groove.Depth, p.Groove.Z0, p.Groove.Z1, p.Width, p.Length, p.Thickness,
        };

        /// <summary>A medida com que o recurso foi feito, gravada em variáveis escalares (mm), é a do plano?</summary>
        private static bool SameSnapshot(dynamic vars, string[] names, double[] want)
        {
            if (want == null) return false;
            for (int i = 0; i < names.Length; i++)
                if (!(Math.Abs(GetVar(vars, names[i]) - want[i]) < 1e-6)) return false;
            return true;
        }

        private static void WriteSnapshot(dynamic vars, string[] names, double[] values)
        {
            for (int i = 0; i < names.Length; i++) SetVar(vars, names[i], values[i], UnitScalar);
        }

        /// <summary>
        /// Travas dos 4 cantos (Babyplast): quadrados encostados nas duas bordas, na face de baixo ou de cima;
        /// ressalto para fora ou rebaixo para dentro (o rebaixo passa 1 mm das bordas para abrir limpo). O lado é
        /// conferido pelo volume, como no ressalto.
        /// </summary>
        private static bool CreateCorners(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            CornerSpec c = plate.Corners;
            double expected = 4 * c.Size * c.Size * c.Height;
            int[] sides = c.OnBottom == c.Recess ? new[] { IgRight, IgLeft } : new[] { IgLeft, IgRight };
            foreach (int side in sides)
            {
                object top = c.OnBottom ? null : TopPlane(doc, plate, "travas (plano)");
                dynamic ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(top ?? doc.RefPlanes.Item(1));
                double hx = Units.MmToM(rotated ? plate.Length : plate.Width) / 2, hy = Units.MmToM(rotated ? plate.Width : plate.Length) / 2;
                double s = Units.MmToM(c.Size), over = c.Recess ? Units.MmToM(1) : 0;
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sy in new[] { -1, 1 })
                        Rect(prof, sx * (hx - s), sy * (hy - s), sx * (hx + over), sy * (hy + over));
                int end = Convert.ToInt32(prof.End(1));
                if (end != 0) { error = $"travas de canto: perfil recusado (End = {end})."; TryDelete(ps); TryDelete(top); return false; }
                double before = Volume(doc);
                object feature;
                try
                {
                    dynamic model = doc.Models.Item(1);
                    feature = c.Recess
                        ? model.ExtrudedCutouts.AddFinite((SolidEdgePart.Profile)prof, IgLeft, side, Units.MmToM(c.Height))
                        : model.ExtrudedProtrusions.AddFinite((SolidEdgePart.Profile)prof, IgLeft, side, Units.MmToM(c.Height));
                }
                catch (Exception e) { error = "travas de canto: " + Msg(e); TryDelete(ps); TryDelete(top); return false; }
                double change = Math.Abs(Volume(doc) - before);
                bool ok = !Failed(feature) && (double.IsNaN(change) || Math.Abs(change - expected) <= Math.Max(1.0, expected * 0.01));
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] travas {1} {2:0.##} × {3:0.##}: lado {4}, {5:0} mm³ (esperado {6:0}){7}",
                    plate.Plate, c.Recess ? "rebaixo" : "ressalto", c.Size, c.Height, side, change, expected, ok ? " ✓" : " — lado errado"));
                if (ok)
                {
                    if (!Name(feature, CornersFeatureName)) { error = $"as travas não aceitaram o nome '{CornersFeatureName}'."; return false; }
                    WriteSnapshot(doc.Variables, CornersVars, CornersSnapshot(plate));
                    Hide(prof);
                    if (top != null) Hide(top);
                    return true;
                }
                TryDelete(feature);
                TryDelete(top);
            }
            error = "travas de canto: o recurso não mudou o volume esperado por nenhum dos lados.";
            return false;
        }

        /// <summary>
        /// Rasgo das guias da gaveta nas duas bordas de A, ao longo de todo o C: um esboço no plano que contém o eixo
        /// de A e o de altura, desenhado em coordenadas REAIS da peça (Convert3DCoordinate — a skill manda não
        /// deduzir os eixos do esboço), e um recorte SIMÉTRICO de C + 2. Passa 1 mm da borda (e da face de cima,
        /// quando o rasgo é aberto em cima) para abrir limpo.
        /// </summary>
        private static bool CreateGroove(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            GrooveSpec g = plate.Groove;
            double t = plate.Thickness;
            double z0 = Math.Max(0, g.Z0), z1 = Math.Min(t, g.Z1);
            double expected = 2 * g.Depth * (z1 - z0) * plate.Length;
            double half = plate.Width / 2, top = g.Z1 >= t - 1e-6 ? t + 1 : z1;
            // Sem giro, A corre em X e o rasgo atravessa Y (plano XZ); girada, o contrário.
            object plane = null;
            dynamic ps = null, prof = null;
            foreach (int i in new[] { 3, 2 })
            {
                ps = doc.ProfileSets.Add();
                prof = ps.Profiles.Add(doc.RefPlanes.Item(i));
                double[] n = Normal((SolidEdgePart.Profile)prof);
                if (n != null && Math.Abs(n[rotated ? 0 : 1]) > 0.99) { plane = doc.RefPlanes.Item(i); break; }
                TryDelete(ps);
                ps = prof = null;
            }
            if (plane == null) { error = "rasgo das guias: nenhum plano de referência é normal ao comprimento da placa."; return false; }
            foreach (int s in new[] { -1, 1 })
            {
                double a0 = s * (half - g.Depth), a1 = s * (half + 1);
                var corners = new[]
                {
                    Pt(rotated, a0, z0), Pt(rotated, a1, z0), Pt(rotated, a1, top), Pt(rotated, a0, top),
                };
                var uv = corners.Select(p => To2D((SolidEdgePart.Profile)prof, p)).ToArray();
                Rect(prof, uv);
            }
            int end = Convert.ToInt32(prof.End(1));
            if (end != 0) { error = $"rasgo das guias: perfil recusado (End = {end})."; TryDelete(ps); return false; }
            double before = Volume(doc);
            object cut;
            try
            {
                object cutouts = (object)doc.Models.Item(1).ExtrudedCutouts;
                cut = cutouts.GetType().InvokeMember("AddFiniteMulti", BindingFlags.InvokeMethod, null, cutouts,
                    new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, IgSymmetric, Units.MmToM(plate.Length + 2) });
            }
            catch (Exception e) { error = "rasgo das guias: " + Msg(e); TryDelete(ps); return false; }
            double removed = before - Volume(doc);
            bool ok = !Failed(cut) && (double.IsNaN(removed) || Math.Abs(removed - expected) <= Math.Max(1.0, expected * 0.01));
            Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] rasgo das guias {1:0.##} de fundo, Z {2:0.##}..{3:0.##}: tirou {4:0} mm³ (esperado {5:0}){6}",
                plate.Plate, g.Depth, z0, z1, removed, expected, ok ? " ✓" : " ✗"));
            if (!ok) { error = "rasgo das guias: o recorte não tirou o volume esperado (veja o log)."; TryDelete(cut); return false; }
            if (!Name(cut, GrooveFeatureName)) { error = $"o rasgo não aceitou o nome '{GrooveFeatureName}'."; return false; }
            WriteSnapshot(doc.Variables, GrooveVars, GrooveSnapshot(plate));
            Hide(prof);
            return true;
        }

        /// <summary>Ponto (m) da peça a partir de (posição no sentido de A, altura), no meio do comprimento.</summary>
        private static double[] Pt(bool rotated, double aMm, double zMm) =>
            rotated ? new[] { 0.0, Units.MmToM(aMm), Units.MmToM(zMm) } : new[] { Units.MmToM(aMm), 0.0, Units.MmToM(zMm) };

        private static double[] To2D(SolidEdgePart.Profile prof, double[] p)
        {
            prof.Convert3DCoordinate(p[0], p[1], p[2], out double u, out double v);
            return new[] { u, v };
        }

        /// <summary>Normal do plano do esboço, pelos três pontos (0,0), (1,0), (0,1) levados a 3D.</summary>
        private static double[] Normal(SolidEdgePart.Profile prof)
        {
            try
            {
                prof.Convert2DCoordinate(0, 0, out double ox, out double oy, out double oz);
                prof.Convert2DCoordinate(1, 0, out double ux, out double uy, out double uz);
                prof.Convert2DCoordinate(0, 1, out double vx, out double vy, out double vz);
                double[] u = { ux - ox, uy - oy, uz - oz }, v = { vx - ox, vy - oy, vz - oz };
                double[] n = { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
                double len = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
                return len < 1e-12 ? null : new[] { n[0] / len, n[1] / len, n[2] / len };
            }
            catch (Exception e) { Log.Warn("  normal do esboço: " + Msg(e)); return null; }
        }

        /// <summary>Retângulo de lados paralelos aos eixos do esboço entre (x0,y0) e (x1,y1), cantos amarrados.</summary>
        private static void Rect(dynamic prof, double x0, double y0, double x1, double y1) =>
            Rect(prof, new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } });

        /// <summary>Quadrilátero fechado pelos 4 cantos dados (coordenadas do esboço), cantos amarrados.</summary>
        private static void Rect(dynamic prof, double[][] c)
        {
            dynamic lines = prof.Lines2d;
            var l = new dynamic[4];
            for (int k = 0; k < 4; k++)
                l[k] = lines.AddBy2Points(c[k][0], c[k][1], c[(k + 1) % 4][0], c[(k + 1) % 4][1]);
            dynamic rel = prof.Relations2d;
            for (int k = 0; k < 4; k++) rel.AddKeypoint(l[k], 1, l[(k + 1) % 4], 0);
        }

        // ------------------------------------------------------------------ bolsão

        public const string PocketFeatureName = "PM_Bolsao";
        private const string VarPocketWidth = "Bolsao_Largura", VarPocketLength = "Bolsao_Comprimento", VarPocketDepth = "Bolsao_Prof", VarPocketSide = "Bolsao_Lado";

        private static void SetPocketVars(dynamic vars, PocketSpec p)
        {
            SetVar(vars, VarPocketWidth, p.Width, UnitDistance);
            SetVar(vars, VarPocketLength, p.Length, UnitDistance);
            SetVar(vars, VarPocketDepth, p.Depth, UnitDistance);
            SetVar(vars, VarPocketSide, p.FromBottom ? 1 : 2, UnitScalar);
        }

        /// <summary>
        /// Bolsão retangular centrado (o alojamento da extratora na unidade MUD): o retângulo guiado por
        /// variáveis, na face de baixo ou num plano na Espessura, e um recorte finito — o lado conferido pelo
        /// volume tirado, descontados os furos que já atravessam a região.
        /// </summary>
        private static bool CreatePocket(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            PocketSpec pk = plate.Pocket;
            dynamic vars = doc.Variables;
            SetPocketVars(vars, pk);
            double expected = pk.Width * pk.Length * pk.Depth - plate.Holes.Sum(h => PocketOverlap(plate, h));
            foreach (int side in pk.FromBottom ? new[] { IgRight, IgLeft } : new[] { IgLeft, IgRight })
            {
                object plane = doc.RefPlanes.Item(1);
                object top = null;
                if (!pk.FromBottom)
                {
                    HashSet<string> b0 = DimensionNames(vars);
                    top = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), Units.MmToM(plate.Thickness), IgRight,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                    LinkNew(vars, b0, VarThickness, "bolsão (plano)");
                    plane = top;
                }
                dynamic ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);
                double hx = Units.MmToM(rotated ? pk.Length : pk.Width) / 2, hy = Units.MmToM(rotated ? pk.Width : pk.Length) / 2;
                CenteredRect(doc, prof, hx, hy, rotated ? VarPocketLength : VarPocketWidth, rotated ? VarPocketWidth : VarPocketLength, construction: false);
                int end = Convert.ToInt32(prof.End(1));
                if (end != 0) { error = $"bolsão: perfil recusado (End = {end})."; TryDelete(ps); TryDelete(top); return false; }
                double before = Volume(doc);
                HashSet<string> b1 = DimensionNames(vars);
                object cut;
                try
                {
                    object cutouts = (object)doc.Models.Item(1).ExtrudedCutouts;
                    cut = cutouts.GetType().InvokeMember("AddFiniteMulti", BindingFlags.InvokeMethod, null, cutouts,
                        new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, side, Units.MmToM(pk.Depth) });
                }
                catch (Exception e) { error = "bolsão: " + Msg(e); TryDelete(ps); TryDelete(top); return false; }
                double removed = before - Volume(doc);
                bool ok = !Failed(cut) && (double.IsNaN(removed) || Math.Abs(removed - expected) <= Math.Max(1.0, expected * 0.01));
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] bolsão {1:0.##} × {2:0.##} × {3:0.##}: lado {4}, tirou {5:0} mm³ (esperado {6:0}){7}",
                    plate.Plate, pk.Width, pk.Length, pk.Depth, side, removed, expected, ok ? " ✓" : " — lado errado"));
                if (ok)
                {
                    if (!Name(cut, PocketFeatureName)) { error = $"o bolsão não aceitou o nome '{PocketFeatureName}'."; return false; }
                    LinkNew(vars, b1, VarPocketDepth, "bolsão (profundidade)");
                    Hide(prof);
                    if (top != null) Hide(top);
                    return true;
                }
                TryDelete(cut);
                TryDelete(top);
            }
            error = "bolsão: o recorte não tirou o volume esperado por nenhum dos lados.";
            return false;
        }

        /// <summary>Volume (mm³) de um grupo de furos que cai dentro do bolsão — tirado uma vez só.</summary>
        public static double PocketOverlap(PlateSpec plate, HoleSpec h)
        {
            PocketSpec pk = plate.Pocket;
            if (pk == null) return 0;
            double t = plate.Thickness;
            double depth = h.Depth == HoleDepthKind.Through ? t : Math.Min(h.DepthMm, t);
            double hz0 = h.Depth == HoleDepthKind.BlindFromTop ? t - depth : 0, hz1 = h.Depth == HoleDepthKind.BlindFromTop ? t : depth;
            double pz0 = pk.FromBottom ? 0 : t - pk.Depth, pz1 = pk.FromBottom ? pk.Depth : t;
            double band = Math.Max(0, Math.Min(hz1, pz1) - Math.Max(hz0, pz0));
            if (band <= 0) return 0;
            double r = h.Diameter / 2;
            int inside = HolePositions(h).Count(p => Math.Abs(p[0]) + r <= pk.Width / 2 && Math.Abs(p[1]) + r <= pk.Length / 2);
            return inside * Math.PI * r * r * band;
        }

        /// <summary>
        /// Volume (mm³) que dois grupos de furos COAXIAIS tiram em comum — o rebaixo da cabeça sobre o furo
        /// passante: em cada centro comum, o Ø menor na faixa de altura em que os dois existem.
        /// </summary>
        public static double CoaxialOverlap(PlateSpec plate, HoleSpec a, HoleSpec b)
        {
            double t = plate.Thickness;
            double[] za = Band(a, t), zb = Band(b, t);
            double band = Math.Max(0, Math.Min(za[1], zb[1]) - Math.Max(za[0], zb[0]));
            if (band <= 0) return 0;
            List<double[]> pb = HolePositions(b);
            int common = HolePositions(a).Count(p => pb.Any(q => Math.Abs(p[0] - q[0]) < 0.01 && Math.Abs(p[1] - q[1]) < 0.01));
            double d = Math.Min(a.Diameter, b.Diameter);
            return common * Math.PI * d * d / 4 * band;
        }

        private static double[] Band(HoleSpec h, double t)
        {
            double depth = h.Depth == HoleDepthKind.Through ? t : Math.Min(h.DepthMm, t);
            return h.Depth == HoleDepthKind.BlindFromTop ? new[] { t - depth, t } : new[] { 0, depth };
        }

        /// <summary>Centros (sentido de A, sentido de C) dos furos de um grupo.</summary>
        public static List<double[]> HolePositions(HoleSpec h)
        {
            double a = h.AcrossA / 2, c = h.AlongC / 2;
            if (h.Points != null) return h.Points.Select(p => new[] { p[0], p[1] }).ToList();
            if (h.OnCenterLine)
                return h.Middle ? new List<double[]> { new[] { 0, -c }, new[] { 0, c }, new[] { 0.0, 0 } } : new List<double[]> { new[] { 0, -c }, new[] { 0, c } };
            if (h.Side != 0) return new List<double[]> { new[] { h.Side * a, -c }, new[] { h.Side * a, c } };
            var all = new List<double[]> { new[] { -a, -c }, new[] { a, -c }, new[] { a, c }, new[] { -a, c } };
            if (h.Middle) { all.Add(new[] { -a, 0.0 }); all.Add(new[] { a, 0.0 }); }
            return all;
        }

        // ------------------------------------------------------------------ placa

        private static bool CreateSlab(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            dynamic ps = doc.ProfileSets.Add();
            dynamic prof = ps.Profiles.Add(doc.RefPlanes.Item(1));
            double hx = Units.MmToM(rotated ? plate.Length : plate.Width) / 2, hy = Units.MmToM(rotated ? plate.Width : plate.Length) / 2;
            CenteredRect(doc, prof, hx, hy, rotated ? VarLength : VarWidth, rotated ? VarWidth : VarLength, construction: false);
            int end = Convert.ToInt32(prof.End(1));
            if (end != 0) { error = $"perfil da placa recusado (End = {end})."; TryDelete(ps); return false; }

            HashSet<string> before = DimensionNames(doc.Variables);
            object feature;
            try
            {
                feature = ((object)doc.Models).GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod, null, doc.Models,
                    new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, IgRight, Units.MmToM(plate.Thickness) });
            }
            catch (Exception e) { error = "extrusão da placa: " + Msg(e); TryDelete(ps); return false; }
            // AddFiniteExtrudedProtrusion devolve o MODEL, não o recurso (medido em 2026-10-23): o recurso é o
            // último da coleção.
            dynamic protrusions = doc.Models.Item(1).ExtrudedProtrusions;
            object slab = protrusions.Item((int)protrusions.Count);
            if (Failed(slab)) { error = "extrusão da placa nasceu com falha."; TryDelete(slab); return false; }
            if (!Name(slab, PlateFeatureName)) { error = $"o recurso da placa não aceitou o nome '{PlateFeatureName}' — sem ele a troca de porta-molde não acha a placa."; return false; }
            LinkNew(doc.Variables, before, VarThickness, "espessura");
            Hide(prof);
            return true;
        }

        // ------------------------------------------------------------------ furos

        private static bool CreateHoles(dynamic doc, PlateSpec plate, HoleSpec h, bool rotated, out string error)
        {
            error = null;
            dynamic vars = doc.Variables;
            SetHoleVars(vars, h);
            SetVar(vars, VarShape(h.Group), ShapeCode(h), UnitScalar);

            double depth = h.Depth == HoleDepthKind.Through ? plate.Thickness : Math.Min(h.DepthMm, plate.Thickness);
            int count = h.Count;
            double expected = count * Math.PI * h.Diameter * h.Diameter / 4 * depth;
            if (FindFeature(doc, PocketFeatureName) != null) expected -= PocketOverlap(plate, h);
            // Rebaixo de cabeça sobre um furo passante já feito (coaxial): só sai a coroa.
            foreach (HoleSpec other in plate.Holes)
                if (!ReferenceEquals(other, h) && other.Group != h.Group && FindFeature(doc, FeatureName(other.Group)) != null)
                    expected -= CoaxialOverlap(plate, h, other);
            // Lado do corte, medido pelo volume tirado: passante e "por baixo" entram para +Z (lado 2, medido em
            // 2026-10-23); "por cima", lado 1. O outro lado fica de reserva — o esboço é refeito, porque apagar
            // o recurso leva o esboço junto.
            int[] sides = h.Depth == HoleDepthKind.BlindFromTop ? new[] { IgLeft, IgRight } : new[] { IgRight, IgLeft };
            foreach (int side in sides)
            {
                if (!HoleProfile(doc, plate, h, rotated, out dynamic ps, out dynamic prof, out object topPlane, out error)) return false;
                double before = Volume(doc);
                HashSet<string> b1 = DimensionNames(vars);
                object cut;
                try
                {
                    object cutouts = (object)doc.Models.Item(1).ExtrudedCutouts;
                    cut = h.Depth == HoleDepthKind.Through
                        ? cutouts.GetType().InvokeMember("AddThroughAllMulti", BindingFlags.InvokeMethod, null, cutouts,
                            new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, side })
                        : cutouts.GetType().InvokeMember("AddFiniteMulti", BindingFlags.InvokeMethod, null, cutouts,
                            new object[] { 1, new[] { (SolidEdgePart.Profile)prof }, side, Units.MmToM(depth) });
                }
                catch (Exception e) { error = $"{h.Group}: recorte — {Msg(e)}"; TryDelete(ps); TryDelete(topPlane); return false; }

                double removed = before - Volume(doc);
                bool measured = !double.IsNaN(removed);
                // Volume ilegível (visto em peça em contexto): vale o Status — apagar um recorte bom por não
                // conseguir medi-lo é pior que aceitá-lo com aviso.
                bool ok = !Failed(cut) && (!measured || Math.Abs(removed - expected) <= Math.Max(1.0, expected * 0.02));
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] {1}: lado {2}, tirou {3:0} mm³ (esperado {4:0}){5}",
                    plate.Plate, h.Group, side, removed, expected, !measured ? " — volume ilegível, aceito pelo Status" : ok ? " ✓" : " — lado errado"));
                if (ok)
                {
                    if (!Name(cut, FeatureName(h.Group))) { error = $"{h.Group}: o recorte não aceitou o nome '{FeatureName(h.Group)}'."; return false; }
                    if (h.Depth != HoleDepthKind.Through) LinkNew(vars, b1, VarDepth(h.Group), h.Group + " (profundidade)");
                    Hide(prof);
                    if (topPlane != null) Hide(topPlane);
                    return true;
                }
                TryDelete(cut);
                TryDelete(topPlane);
            }
            error = $"{h.Group}: o recorte não tirou o volume esperado por nenhum dos lados.";
            return false;
        }

        /// <summary>
        /// O esboço do grupo: na face de baixo (RefPlane 1) ou num plano paralelo na ESPESSURA, ligado à
        /// variável; 4 furos nos cantos de um retângulo de construção centrado, ou 2 numa linha de centro.
        /// </summary>
        private static bool HoleProfile(dynamic doc, PlateSpec plate, HoleSpec h, bool rotated,
            out dynamic ps, out dynamic prof, out object topPlane, out string error)
        {
            ps = prof = null;
            topPlane = null;
            error = null;
            dynamic vars = doc.Variables;
            object plane = doc.RefPlanes.Item(1);
            if (h.Depth == HoleDepthKind.BlindFromTop)
            {
                HashSet<string> b0 = DimensionNames(vars);
                try
                {
                    topPlane = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), Units.MmToM(plate.Thickness), IgRight,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                }
                catch (Exception e) { error = $"{h.Group}: plano na face de cima — {Msg(e)}"; return false; }
                LinkNew(vars, b0, VarThickness, h.Group + " (plano)");
                // RefPlane.Name é "rw" na typelib mas dá E_FAIL ao vivo: o plano é achado depois pelo número do
                // nome de SISTEMA, guardado numa variável escalar.
                SetVar(vars, VarPlane(h.Group), PlaneNumber(topPlane), UnitScalar);
                plane = topPlane;
            }

            ps = doc.ProfileSets.Add();
            prof = ps.Profiles.Add(plane);
            var circles = new List<object>();
            double r = Units.MmToM(h.Diameter) / 2;
            double a = Units.MmToM(h.AcrossA) / 2, c = Units.MmToM(h.AlongC) / 2;
            dynamic rel = prof.Relations2d;
            if (h.Points != null)
            {
                // Centros dados um a um (padrão fora de centro): no lugar, sem cota de posição.
                foreach (double[] p in h.Points)
                    circles.Add(prof.Circles2d.AddByCenterRadius(Units.MmToM(rotated ? p[1] : p[0]), Units.MmToM(rotated ? p[0] : p[1]), r));
            }
            else if (h.OnCenterLine)
            {
                // 2 furos sobre a linha de centro, no sentido de C.
                bool vertical = !rotated;
                dynamic line = CenterLine(doc, prof, c, vertical, VarAlongC(h.Group));
                for (int k = 0; k < 2; k++)
                {
                    double x = vertical ? 0 : (k == 0 ? -c : c), y = vertical ? (k == 0 ? -c : c) : 0;
                    dynamic ci = prof.Circles2d.AddByCenterRadius(x, y, r);
                    rel.AddKeypoint(ci, 0, line, k);
                    circles.Add(ci);
                }
                if (h.Middle)
                {
                    dynamic ci = prof.Circles2d.AddByCenterRadius(0.0, 0.0, r);
                    rel.AddKeypoint(ci, 0, line, 2);
                    circles.Add(ci);
                }
            }
            else
            {
                double hx = rotated ? c : a, hy = rotated ? a : c;
                dynamic[] l = CenteredRect(doc, prof, hx, hy, rotated ? VarAlongC(h.Group) : VarAcrossA(h.Group),
                    rotated ? VarAcrossA(h.Group) : VarAlongC(h.Group), construction: true);
                double[,] corner = { { -hx, -hy }, { hx, -hy }, { hx, hy }, { -hx, hy } };   // início de l[0..3]
                for (int k = 0; k < 4; k++)
                {
                    // Lado único (espaçador): só os cantos do lado pedido no sentido de A.
                    double cornerA = rotated ? corner[k, 1] : corner[k, 0];
                    if (h.Side != 0 && Math.Sign(cornerA) != h.Side) continue;
                    dynamic ci = prof.Circles2d.AddByCenterRadius(corner[k, 0], corner[k, 1], r);
                    rel.AddKeypoint(ci, 0, l[k], 0);
                    circles.Add(ci);
                }
                if (h.Middle && h.Side == 0)
                {
                    // No meio dos dois lados paralelos a C (ponto-chave 2 = meio da linha): sem giro, os verticais.
                    foreach (int k in rotated ? new[] { 0, 2 } : new[] { 1, 3 })
                    {
                        double mx = (corner[k, 0] + corner[(k + 1) % 4, 0]) / 2, my = (corner[k, 1] + corner[(k + 1) % 4, 1]) / 2;
                        dynamic ci = prof.Circles2d.AddByCenterRadius(mx, my, r);
                        rel.AddKeypoint(ci, 0, l[k], 2);
                        circles.Add(ci);
                    }
                }
            }
            for (int k = 1; k < circles.Count; k++) rel.AddEqual(circles[0], circles[k]);
            dynamic dia = prof.Dimensions.AddCircularDiameter(circles[0]);
            dia.Constraint = true;
            dia.Formula = VarDiameter(h.Group);

            int end = Convert.ToInt32(prof.End(1));
            if (end != 0) { error = $"{h.Group}: perfil recusado (End = {end})."; TryDelete(ps); TryDelete(topPlane); return false; }
            return true;
        }

        private static void DeleteGroup(dynamic doc, HoleGroup g)
        {
            TryDelete(FindFeature(doc, FeatureName(g)));
            double number = GetVar(doc.Variables, VarPlane(g));
            if (!double.IsNaN(number) && number > 0)
                try
                {
                    dynamic planes = doc.RefPlanes;
                    for (int i = (int)planes.Count; i >= 1; i--)
                    {
                        dynamic p = planes.Item(i);
                        if (Math.Abs(PlaneNumber(p) - number) < 0.5) { TryDelete(p); break; }
                    }
                }
                catch (Exception e) { Log.Warn($"  plano do grupo {g}: {Msg(e)}"); }
            Log.Info($"  grupo {g} apagado para ser refeito.");
        }

        private static void SetHoleVars(dynamic vars, HoleSpec h)
        {
            if (h.Points == null)
            {
                if (!h.OnCenterLine) SetVar(vars, VarAcrossA(h.Group), h.AcrossA, UnitDistance);
                SetVar(vars, VarAlongC(h.Group), h.AlongC, UnitDistance);
            }
            SetVar(vars, VarDiameter(h.Group), h.Diameter, UnitDistance);
            if (h.Depth != HoleDepthKind.Through) SetVar(vars, VarDepth(h.Group), h.DepthMm, UnitDistance);
        }

        // ------------------------------------------------------------------ esboço

        /// <summary>Retângulo centrado na origem do esboço, lados guiados por variáveis. Devolve as 4 linhas (l[k] começa no canto k).</summary>
        private static dynamic[] CenteredRect(dynamic doc, dynamic prof, double hx, double hy, string varX, string varY, bool construction)
        {
            dynamic lines = prof.Lines2d;
            dynamic l1 = lines.AddBy2Points(-hx, -hy, hx, -hy);
            dynamic l2 = lines.AddBy2Points(hx, -hy, hx, hy);
            dynamic l3 = lines.AddBy2Points(hx, hy, -hx, hy);
            dynamic l4 = lines.AddBy2Points(-hx, hy, -hx, -hy);
            dynamic rel = prof.Relations2d;
            rel.AddKeypoint(l1, 1, l2, 0); rel.AddKeypoint(l2, 1, l3, 0); rel.AddKeypoint(l3, 1, l4, 0); rel.AddKeypoint(l4, 1, l1, 0);
            rel.AddHorizontal(l1); rel.AddHorizontal(l3); rel.AddVertical(l2); rel.AddVertical(l4);
            dynamic ax2 = prof.ProjectRefPlane(doc.RefPlanes.Item(2));
            dynamic ax3 = prof.ProjectRefPlane(doc.RefPlanes.Item(3));
            dynamic diag = lines.AddBy2Points(-hx, -hy, hx, hy);
            prof.ToggleConstruction(diag);
            rel.AddKeypoint(diag, 0, l1, 0); rel.AddKeypoint(diag, 1, l3, 0);
            rel.AddPointOn(diag, 2, ax2, -1); rel.AddPointOn(diag, 2, ax3, -1);
            if (construction) { prof.ToggleConstruction(l1); prof.ToggleConstruction(l2); prof.ToggleConstruction(l3); prof.ToggleConstruction(l4); }
            dynamic dims = prof.Dimensions;
            dims.Constraint = true;
            dynamic dx = dims.AddLength(l1); dx.Constraint = true; dx.Formula = varX;
            dynamic dy = dims.AddLength(l2); dy.Constraint = true; dy.Formula = varY;
            return new[] { l1, l2, l3, l4 };
        }

        /// <summary>Linha de construção centrada na origem (vertical = no Y do esboço), comprimento guiado pela variável.</summary>
        private static dynamic CenterLine(dynamic doc, dynamic prof, double half, bool vertical, string varLength)
        {
            dynamic line = vertical ? prof.Lines2d.AddBy2Points(0.0, -half, 0.0, half) : prof.Lines2d.AddBy2Points(-half, 0.0, half, 0.0);
            prof.ToggleConstruction(line);
            dynamic rel = prof.Relations2d;
            if (vertical) rel.AddVertical(line); else rel.AddHorizontal(line);
            dynamic ax2 = prof.ProjectRefPlane(doc.RefPlanes.Item(2));
            dynamic ax3 = prof.ProjectRefPlane(doc.RefPlanes.Item(3));
            rel.AddPointOn(line, 2, ax2, -1); rel.AddPointOn(line, 2, ax3, -1);
            dynamic dims = prof.Dimensions;
            dims.Constraint = true;
            dynamic d = dims.AddLength(line); d.Constraint = true; d.Formula = varLength;
            return line;
        }

        // ------------------------------------------------------------------ variáveis

        /// <summary>Cria ou atualiza. Distância em mm (gravada em metros pelo Value — sem depender da unidade do documento).</summary>
        public static void SetVar(dynamic vars, string name, double value, int unitsType)
        {
            double v = unitsType == UnitDistance ? Units.MmToM(value) : value;
            dynamic existing = null;
            try { existing = vars.Item(name); } catch { }
            if (existing != null) { existing.Value = v; return; }
            string formula = unitsType == UnitDistance
                ? value.ToString("0.####", CultureInfo.InvariantCulture) + " mm"
                : value.ToString("0.####", CultureInfo.InvariantCulture);
            vars.Add(name, formula, unitsType);
        }

        /// <summary>Valor da variável (mm se distância), NaN se não existe.</summary>
        public static double GetVar(dynamic vars, string name)
        {
            try
            {
                dynamic v = vars.Item(name);
                double value = Convert.ToDouble(v.Value);
                return Convert.ToInt32(v.UnitsType) == UnitDistance ? Units.MToMm(value) : value;
            }
            catch { return double.NaN; }
        }

        /// <summary>Nomes de sistema das COTAS na tabela agora (Query dimensões, nome de sistema).</summary>
        private static HashSet<string> DimensionNames(dynamic vars)
        {
            var set = new HashSet<string>();
            try
            {
                dynamic list = vars.Query("*", 2, 1, false);
                for (int i = 1; i <= (int)list.Count; i++)
                    try { set.Add((string)list.Item(i).SystemName); } catch { }
            }
            catch (Exception e) { Log.Warn("  tabela de variáveis ilegível: " + Msg(e)); }
            return set;
        }

        /// <summary>Liga à variável a(s) cota(s) que nasceram desde <paramref name="before"/> e ainda não têm fórmula.</summary>
        private static void LinkNew(dynamic vars, HashSet<string> before, string variable, string what)
        {
            int linked = 0;
            if (before.Count == 0) { Log.Warn($"  {what}: tabela ilegível antes — '{variable}' NÃO ligada (ligar tudo que não tem fórmula seria pior)."); return; }
            try
            {
                dynamic list = vars.Query("*", 2, 1, false);
                for (int i = 1; i <= (int)list.Count; i++)
                {
                    dynamic v = list.Item(i);
                    string sys = (string)v.SystemName;
                    if (before.Contains(sys) || !string.IsNullOrEmpty((string)v.Formula)) continue;
                    vars.Edit(sys, variable);
                    linked++;
                }
            }
            catch (Exception e) { Log.Warn($"  {what}: ligar a '{variable}' falhou — {Msg(e)}"); }
            if (linked != 1) Log.Warn($"  {what}: {linked} cota(s) nova(s) ligada(s) a '{variable}' (esperado 1).");
        }

        // ------------------------------------------------------------------ conferência e apoio

        private static bool Verify(dynamic doc, PlateSpec plate, bool rotated, out string error)
        {
            error = null;
            try
            {
                object body = doc.Models.Item(1).Body;
                if (!AutoEDM.Selection.FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)) { Log.Warn($"  [{plate.Plate}] caixa ilegível."); return true; }
                double ex = rotated ? plate.Length : plate.Width, ey = rotated ? plate.Width : plate.Length;
                double below = BelowBottom(plate), above = AboveTop(plate);
                double[] got = { mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2] };
                double ez = plate.Thickness + below + above;
                bool ok = Math.Abs(got[0] - ex) < 0.01 && Math.Abs(got[1] - ey) < 0.01 && Math.Abs(got[2] - ez) < 0.01
                          && Math.Abs(mn[0] + mx[0]) < 0.01 && Math.Abs(mn[1] + mx[1]) < 0.01 && Math.Abs(mn[2] + below) < 0.01;
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] caixa {1:0.##} × {2:0.##} × {3:0.##} (esperado {4:0.##} × {5:0.##} × {6:0.##}), centro ({7:0.##}; {8:0.##}), base Z {9:0.##}{10}",
                    plate.Plate, got[0], got[1], got[2], ex, ey, ez, (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2], ok ? " ✓" : " ✗"));
                if (!ok) { error = $"{plate.Plate}: a placa não ficou com a medida do plano (veja o log)."; return false; }

                // Caixa certa não prova os furos: volume = placa − furos, e nenhum recurso PM_* com falha.
                foreach (HoleSpec h in plate.Holes)
                {
                    dynamic f = FindFeature(doc, FeatureName(h.Group));
                    if (f == null || Failed(f)) { error = $"{plate.Plate}: o grupo de furos {h.Group} {(f == null ? "sumiu" : "está com falha")}."; return false; }
                }
                double expected = plate.Width * plate.Length * plate.Thickness - plate.Holes.Sum(h =>
                    h.Count * Math.PI * h.Diameter * h.Diameter / 4 * (h.Depth == HoleDepthKind.Through ? plate.Thickness : Math.Min(h.DepthMm, plate.Thickness)));
                for (int i = 0; i < plate.Holes.Count; i++)
                    for (int j = i + 1; j < plate.Holes.Count; j++)
                        expected += CoaxialOverlap(plate, plate.Holes[i], plate.Holes[j]);
                if (plate.Pocket != null)
                {
                    dynamic pf = FindFeature(doc, PocketFeatureName);
                    if (pf == null || Failed(pf)) { error = $"{plate.Plate}: o bolsão {(pf == null ? "sumiu" : "está com falha")}."; return false; }
                    expected -= plate.Pocket.Width * plate.Pocket.Length * plate.Pocket.Depth - plate.Holes.Sum(h => PocketOverlap(plate, h));
                }
                foreach (string name in new[] { BossFeatureName, CornersFeatureName, GrooveFeatureName, StepFeatureName })
                {
                    bool wanted = name == BossFeatureName ? plate.Boss != null : name == CornersFeatureName ? plate.Corners != null
                                : name == GrooveFeatureName ? plate.Groove != null : plate.Step != null;
                    if (!wanted) continue;
                    dynamic f = FindFeature(doc, name);
                    if (f == null || Failed(f)) { error = $"{plate.Plate}: o recurso {name} {(f == null ? "sumiu" : "está com falha")}."; return false; }
                }
                expected += FeatureVolume(plate);
                double vol = Volume(doc);
                if (double.IsNaN(vol)) { Log.Warn($"  [{plate.Plate}] volume ilegível — furos conferidos só pelo Status."); return true; }
                bool volOk = Math.Abs(vol - expected) <= Math.Max(1.0, expected * 0.002);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [{0}] volume {1:0} mm³ (esperado {2:0}){3}", plate.Plate, vol, expected, volOk ? " ✓" : " ✗"));
                if (!volOk) error = $"{plate.Plate}: o volume da placa não bate com o plano — algum furo saiu fora do lugar (veja o log).";
                return volOk;
            }
            catch (Exception e) { Log.Warn($"  [{plate.Plate}] conferência: {Msg(e)}"); return true; }
        }

        /// <summary>Quanto o ressalto/as travas saem abaixo da face de baixo (mm).</summary>
        public static double BelowBottom(PlateSpec p) => Math.Max(
            p.Boss != null && p.Boss.OnBottom ? p.Boss.Height : 0,
            p.Corners != null && !p.Corners.Recess && p.Corners.OnBottom ? p.Corners.Height : 0);

        /// <summary>Quanto o ressalto/as travas saem acima da face de cima (mm).</summary>
        public static double AboveTop(PlateSpec p) => Math.Max(
            p.Boss != null && !p.Boss.OnBottom ? p.Boss.Height : 0,
            p.Corners != null && !p.Corners.Recess && !p.Corners.OnBottom ? p.Corners.Height : 0);

        /// <summary>Volume (mm³) que ressalto, travas e rasgo somam (+) ou tiram (−) da placa.</summary>
        public static double FeatureVolume(PlateSpec p)
        {
            double v = 0;
            if (p.Boss != null) v += p.Boss.Width * p.Boss.Length * p.Boss.Height;
            if (p.Corners != null) v += (p.Corners.Recess ? -4 : 4) * p.Corners.Size * p.Corners.Size * p.Corners.Height;
            if (p.Groove != null) v -= 2 * p.Groove.Depth * Math.Max(0, Math.Min(p.Thickness, p.Groove.Z1) - Math.Max(0, p.Groove.Z0)) * p.Length;
            if (p.Step != null) v -= (p.Width * p.Length - (p.Width - 2 * p.Step.Inset) * (p.Length - 2 * p.Step.Inset)) * Math.Min(p.Thickness, p.Step.Depth);
            return v;
        }

        private static double Volume(dynamic doc)
        {
            try { return Convert.ToDouble(doc.Models.Item(1).Body.Volume) * 1e9; } catch { return double.NaN; }
        }

        /// <summary>O recurso com esse nome (placa ou grupo de furos), ou null.</summary>
        public static dynamic FindFeature(dynamic doc, string name)
        {
            try
            {
                dynamic model = doc.Models.Item(1);
                foreach (string coll in new[] { "ExtrudedProtrusions", "ExtrudedCutouts" })
                {
                    dynamic items = ((object)model).GetType().InvokeMember(coll, BindingFlags.GetProperty, null, model, null);
                    for (int i = 1; i <= (int)items.Count; i++)
                    {
                        dynamic f = items.Item(i);
                        string n = null;
                        try { n = (string)f.Name; } catch { }
                        if (n == name) return f;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool Failed(object feature)
        {
            if (feature == null) return true;
            try { return unchecked((uint)Convert.ToInt32(((dynamic)feature).Status)) != 1216476310u; }
            catch { return false; }
        }

        /// <summary>Nomeia e LÊ DE VOLTA — o nome é a chave da troca de porta-molde.</summary>
        private static bool Name(object o, string name)
        {
            try
            {
                ((dynamic)o).Name = name;
                return (string)((dynamic)o).Name == name;
            }
            catch (Exception e) { Log.Warn($"  nomear '{name}': {Msg(e)}"); return false; }
        }

        private static void Hide(object o)
        {
            try { ((dynamic)o).Visible = false; } catch { }
        }

        private static void TryDelete(object o)
        {
            if (o == null) return;
            try { ((dynamic)o).Delete(); } catch (Exception e) { Log.Warn("  apagar: " + Msg(e)); }
        }

        private static string Msg(Exception e) => e.GetBaseException().Message;
    }
}
