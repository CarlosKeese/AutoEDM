using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>
    /// Botão "Porta-molde" (Carlos, 2026-10-23) — a regra, sem CAD. Da caixa das peças dispostas na montagem
    /// escolhe a série Polimold, as espessuras de P1/P2 (sub-série) e o curso, monta a pilha de placas com os
    /// furos das coordenadas gerais e confere área útil, fundo das placas cavidade, curso, espaço de
    /// refrigeração e a flexão da placa suporte.
    ///
    /// Como a Polimold manda escolher (CATPM2P p.2): a SÉRIE pela área útil X×Y; a SUB-SÉRIE pela altura do
    /// produto e da refrigeração; o CURSO pela altura a extrair. Os números da casa (margem, fundo, folga do
    /// curso, canal) são opções — nenhum fabricante os fixa.
    /// </summary>
    public static class MoldBasePlanner
    {
        /// <summary>Folga entre a placa extratora e a placa base inferior (encosto padrão de 4 mm, CATPM2P p.3).</summary>
        public const double StopButtonHeight = 4.0;

        public static MoldBasePlan Plan(MoldBaseContent content, MoldBaseOptions options)
        {
            MoldBaseOptions opt = (options ?? new MoldBaseOptions()).Clone();
            var plan = new MoldBasePlan { Options = opt };
            if (content == null || content.Count == 0 || content.Max1 < content.Min1)
            {
                plan.Problem = "Nenhuma peça para dimensionar o porta-molde.";
                return plan;
            }

            plan.Center1 = opt.Centering == MoldBaseCentering.ContentCenter ? (content.Min1 + content.Max1) / 2 : 0;
            plan.Center2 = opt.Centering == MoldBaseCentering.ContentCenter ? (content.Min2 + content.Max2) / 2 : 0;
            double h1 = Math.Max(Math.Abs(content.Min1 - plan.Center1), Math.Abs(content.Max1 - plan.Center1));
            double h2 = Math.Max(Math.Abs(content.Min2 - plan.Center2), Math.Abs(content.Max2 - plan.Center2));
            double fixedH = Math.Max(0, content.MaxW), movingH = Math.Max(0, -content.MinW);

            double extract = opt.ProductHeightMm > 0 ? opt.ProductHeightMm
                           : content.ProductHeight > 0 ? content.ProductHeight : fixedH;
            plan.NeededStroke = extract + opt.StrokeClearanceMm;
            if (opt.ProductHeightMm <= 0 && content.ProductHeight <= 0)
                plan.Notes.Add($"Altura a extrair estimada pela altura das peças acima da partição ({F(fixedH)} mm) — nenhuma peça foi marcada como produto; confira o curso.");

            if (opt.Family != MoldBaseFamily.Polimold)
                return PlanUnit(plan, content, h1, h2, fixedH, movingH, extract);

            // Série: a menor placa (A·C) que comporta as peças + margem, a espessura e o curso. Testa a planta girada.
            var rejected = new List<string>();
            Candidate best = null;
            PolimoldSeries forced = string.IsNullOrWhiteSpace(opt.ForcedSeries) ? null : PolimoldCatalog.Find(opt.ForcedSeries);
            if (!string.IsNullOrWhiteSpace(opt.ForcedSeries) && forced == null)
            {
                plan.Problem = $"Série '{opt.ForcedSeries}' não existe no catálogo Polimold de 2 placas.";
                return plan;
            }

            foreach (bool rotated in opt.AllowRotation ? new[] { false, true } : new[] { false })
            {
                double ha = rotated ? h2 : h1, hc = rotated ? h1 : h2;
                double needX = 2 * (ha + opt.SideMarginMm), needY = 2 * (hc + opt.SideMarginMm);
                IEnumerable<PolimoldSeries> pool = forced != null ? (IEnumerable<PolimoldSeries>)new[] { forced }
                    : PolimoldCatalog.All.OrderBy(s => s.PlateArea).ThenBy(s => s.A);
                foreach (PolimoldSeries s in pool)
                {
                    string why = null;
                    if (forced == null && (s.X < needX - 1e-6 || s.Y < needY - 1e-6))
                        why = $"área útil {F(s.X)}×{F(s.Y)} < {F(needX)}×{F(needY)}";
                    double p1 = opt.ForcedP1 > 0 ? opt.ForcedP1 : Pick(s.Thicknesses, fixedH + opt.FloorMm);
                    double p2 = opt.ForcedP2 > 0 ? opt.ForcedP2 : Pick(s.Thicknesses, movingH + opt.FloorMm);
                    if (why == null && forced == null && (opt.ForcedP1 > 0 && !s.Thicknesses.Contains(opt.ForcedP1) ||
                                                          opt.ForcedP2 > 0 && !s.Thicknesses.Contains(opt.ForcedP2)))
                        why = "P1/P2 escolhidas não existem na faixa da série";
                    if (why == null && forced == null && (double.IsNaN(p1) || double.IsNaN(p2)))
                        why = $"nenhuma espessura de P1/P2 até {F(s.Thicknesses.Last())} comporta {F(Math.Max(fixedH, movingH) + opt.FloorMm)}";
                    StrokeOption st = opt.ForcedStroke > 0
                        ? s.Strokes.FirstOrDefault(o => o.Index == opt.ForcedStroke)
                        : s.Strokes.FirstOrDefault(o => o.Stroke >= plan.NeededStroke - 1e-6);
                    if (why == null && forced == null && (st == null || st.Stroke < plan.NeededStroke - 1e-6))
                        why = st == null ? $"curso máximo {F(s.Strokes.Last().Stroke)} < {F(plan.NeededStroke)}"
                                         : $"curso {st.Index} = {F(st.Stroke)} < {F(plan.NeededStroke)}";
                    if (why != null)
                    {
                        if (!rotated && s.Name != null) rejected.Add($"{s.Name}: {why}");
                        continue;
                    }
                    var c = new Candidate
                    {
                        Series = s, Rotated = rotated, NeedX = needX, NeedY = needY,
                        P1 = double.IsNaN(p1) ? s.Thicknesses.Last() : p1,
                        P2 = double.IsNaN(p2) ? s.Thicknesses.Last() : p2,
                        Stroke = st ?? s.Strokes.Last(),
                        Fits = s.X >= needX - 1e-6 && s.Y >= needY - 1e-6,
                    };
                    if (best == null || (c.Fits && !best.Fits) ||
                        (c.Fits == best.Fits && c.Series.PlateArea < best.Series.PlateArea - 1e-6)) best = c;
                    break;
                }
            }

            if (best == null)
            {
                plan.Problem = "Nenhuma série do catálogo Polimold de 2 placas comporta as peças: " +
                               string.Join("; ", rejected.Skip(Math.Max(0, rejected.Count - 3))) + ".";
                return plan;
            }

            plan.Series = best.Series;
            plan.Rotated = best.Rotated;
            plan.NeededX = best.NeedX;
            plan.NeededY = best.NeedY;
            plan.P1 = best.P1;
            plan.P2 = best.P2;
            plan.Stroke = best.Stroke;
            plan.SubSeries = PolimoldCatalog.SubSeries(best.Series, best.P1, best.P2);
            string smaller = rejected.LastOrDefault(r => PolimoldCatalog.Find(r.Substring(0, r.IndexOf(':')))?.PlateArea < best.Series.PlateArea - 1e-6);
            if (forced == null && smaller != null)
                plan.Notes.Add("Série menor descartada — " + smaller + ".");
            if (best.Rotated)
                plan.Notes.Add("Porta-molde girado 90° na planta: o comprimento C corre no 1º eixo de planta da montagem.");

            BuildPlates(plan);
            BuildPolimoldComponents(plan);
            Check(plan, content, fixedH, movingH, extract);
            return plan;
        }

        // ------------------------------------------------------------------ MUD (gaveta) e Babyplast

        /// <summary>
        /// Gaveta, Babyplast e Zamak têm UM tamanho (a unidade 170 × 246 do quadro 7160; as metades 76 × 76; a matriz
        /// MINI 160 × 160) e as espessuras do padrão da casa — nada a escolher na planta. Testa a planta girada na
        /// gaveta (unidade não quadrada).
        /// </summary>
        private static MoldBasePlan PlanUnit(MoldBasePlan plan, MoldBaseContent content, double h1, double h2,
            double fixedH, double movingH, double extract)
        {
            MoldBaseOptions o = plan.Options;
            PolimoldSeries s = MoldBaseStandards.UnitSeries(o.Family);
            bool mud = o.Family == MoldBaseFamily.Mud;
            Candidate best = null;
            foreach (bool rotated in o.AllowRotation && mud ? new[] { false, true } : new[] { false })
            {
                double ha = rotated ? h2 : h1, hc = rotated ? h1 : h2;
                var c = new Candidate { Series = s, Rotated = rotated, NeedX = 2 * (ha + o.SideMarginMm), NeedY = 2 * (hc + o.SideMarginMm) };
                c.Fits = s.X >= c.NeedX - 1e-6 && s.Y >= c.NeedY - 1e-6;
                if (best == null || (c.Fits && !best.Fits)) best = c;
            }
            plan.Series = s;
            plan.Rotated = best.Rotated;
            plan.NeededX = best.NeedX;
            plan.NeededY = best.NeedY;
            if (best.Rotated) plan.Notes.Add("Unidade girada 90° na planta: o comprimento corre no 1º eixo de planta da montagem.");

            var checks = plan.Checks;
            bool areaOk = best.Fits;
            checks.Add(new PlanCheck("Área", areaOk ? CheckLevel.Ok : CheckLevel.Fail,
                $"peças + {F(o.SideMarginMm)} mm por lado pedem {F(plan.NeededX)} × {F(plan.NeededY)}; a {s.Name} tem {F(s.A)} × {F(s.C)}" +
                (areaOk ? $" (sobra {F(s.A - plan.NeededX)} × {F(s.C - plan.NeededY)})." : " — NÃO cabe.")));
            plan.SubSeries = 0;
            if (mud) PlanMud(plan, fixedH, movingH);
            else if (o.Family == MoldBaseFamily.Zamak) PlanZamak(plan, fixedH, movingH);
            else PlanBabyplast(plan, fixedH, movingH);
            s.Strokes = new[] { plan.Stroke };

            foreach (PlateSpec p in new[] { plan.Plates.First(x => x.Plate == MoldPlate.P1), plan.Plates.First(x => x.Plate == MoldPlate.P2) })
            {
                double ha = plan.Rotated ? Half(content.Min2, content.Max2, plan.Center2) : Half(content.Min1, content.Max1, plan.Center1);
                double hc = plan.Rotated ? Half(content.Min1, content.Max1, plan.Center1) : Half(content.Min2, content.Max2, plan.Center2);
                string acrossA = CoolingWindow(p, ha, p.Width / 2, true, o, out bool okA);
                string alongC = CoolingWindow(p, hc, p.Length / 2, false, o, out bool okC);
                checks.Add(new PlanCheck("Refrigeração em " + (p.Code ?? p.Plate.ToString()), okA && okC ? CheckLevel.Ok : CheckLevel.Warning,
                    $"canal Ø{F(o.CoolingDiameterMm)} paralelo ao comprimento: {acrossA}; paralelo à largura: {alongC}."));
            }
            return plan;
        }

        /// <summary>
        /// A gaveta (padrão TIPO GAVETA): fixa "100" de 46; móvel "200" (32, ou mais grossa se as peças pedirem)
        /// com o ressalto 186 × 110 × 5 no alojamento passante da "201", e 200 + 201 = 87; o curso é o que sobra
        /// na 201 — por isso a 200 mais grossa come curso. 100 e 200 podem ser trocadas (Carlos, 2026-10-23).
        /// </summary>
        private static void PlanMud(MoldBasePlan plan, double fixedH, double movingH)
        {
            MoldBaseOptions o = plan.Options;
            plan.P1 = o.ForcedP1 > 0 ? o.ForcedP1 : MoldBaseStandards.MudFixedThickness;
            plan.P2 = o.ForcedP2 > 0 ? o.ForcedP2 : Math.Max(MoldBaseStandards.MudCavityPlate, Math.Ceiling(movingH + o.FloorMm));
            double stroke = MoldBaseStandards.MudStroke(plan.P2);
            plan.Stroke = new StrokeOption(1, Math.Max(0, stroke), 0);
            BuildMud(plan);

            var checks = plan.Checks;
            PlanCheck fixedCheck = Floor("Placa fixa (100)", plan.P1, fixedH, o);
            if (Math.Abs(plan.P1 - MoldBaseStandards.MudFixedThickness) > 1e-6)
                fixedCheck.Detail += $" (o padrão é {F(MoldBaseStandards.MudFixedThickness)}).";
            checks.Add(fixedCheck);
            checks.Add(Floor("Placa móvel (200)", plan.P2, movingH, o));
            double ps = MoldBaseStandards.MudMovingThickness - plan.P2;
            bool strokeOk = stroke >= plan.NeededStroke - 1e-6;
            checks.Add(new PlanCheck("Curso de extração", strokeOk ? CheckLevel.Ok : CheckLevel.Fail,
                $"a 201 de {F(ps)} deixa {F(stroke)} (− {F(MoldBaseStandards.MudBossHeight)} do ressalto da 200, − 2 × {F(MoldBaseStandards.MudEjectorThickness)} das extratoras, " +
                $"− {F(MoldBaseStandards.MudEjectorRest)} de fundo); pedido {F(plan.NeededStroke)}" +
                (strokeOk ? "." : $" — NÃO dá: a 200 de {F(plan.P2)} come o curso (200 + 201 = {F(MoldBaseStandards.MudMovingThickness)}, o canal do quadro).")));
            if (o.WithGuides)
                plan.Notes.Add($"Quadro {MoldBaseStandards.MudFrameName}, guias, puxadores, batente e os 6 M8 entram por referência à biblioteca, " +
                               "na posição do padrão. Engates e bujões da refrigeração ficam com o molde.");
            else
                plan.Notes.Add($"Sem as peças de biblioteca: a unidade {F(MoldBaseStandards.MudWidth)} × {F(MoldBaseStandards.MudLength)} entra no canal das guias do quadro {MoldBaseStandards.MudFrameName}.");
        }

        private static void PlanBabyplast(MoldBasePlan plan, double fixedH, double movingH)
        {
            MoldBaseOptions o = plan.Options;
            plan.P1 = o.ForcedP1 > 0 ? o.ForcedP1 : MoldBaseStandards.BabyFixedHalf;
            plan.P2 = o.ForcedP2 > 0 ? o.ForcedP2 : MoldBaseStandards.BabyMovingHalf;
            plan.Stroke = new StrokeOption(1, Math.Ceiling(plan.NeededStroke), 0);
            BuildBabyplast(plan);

            var checks = plan.Checks;
            foreach (var side in new[] { Tuple.Create("Metade fixa (100)", plan.P1, fixedH, MoldBaseStandards.BabyFixedHalf),
                                         Tuple.Create("Metade móvel (200)", plan.P2, movingH, MoldBaseStandards.BabyMovingHalf) })
            {
                PlanCheck c = Floor(side.Item1, side.Item2, side.Item3, o);
                if (Math.Abs(side.Item2 - side.Item4) > 1e-6)
                {
                    if (c.Level == CheckLevel.Ok) c.Level = CheckLevel.Warning;
                    c.Detail += $" O padrão tem {F(side.Item4)}.";
                }
                checks.Add(c);
            }
            checks.Add(new PlanCheck("Curso de extração", CheckLevel.Ok,
                $"altura a extrair + folga = {F(plan.NeededStroke)} → vão de {F(plan.Stroke.Stroke)} entre a placa de apoio móvel e a extratora (o padrão tem 26)."));
            plan.Notes.Add("As metades se alinham pelas travas dos 4 cantos (ressalto 12 × 4,1 na fixa, rebaixo 12,1 × 4,2 na móvel): o padrão não tem pino-guia nem pino de retorno.");
            if (o.WithGuides) plan.Notes.Add("Engate de extração 8760.019 por referência à biblioteca, no centro da extratora.");
        }

        /// <summary>
        /// As placas da gaveta, como no 000.asm: 100 (rasgos das guias), 200 (ressalto, rosca dos M8), 201
        /// (alojamento passante 186 × 110, M8 com rebaixo por baixo, rasgos), 301/302 a 0,7 do fundo da 201, a
        /// 302 com o rebaixo do batente.
        /// </summary>
        private static void BuildMud(MoldBasePlan plan)
        {
            MoldBaseOptions o = plan.Options;
            double w = MoldBaseStandards.MudWidth, l = MoldBaseStandards.MudLength, t = MoldBaseStandards.MudEjectorThickness;
            double ew = MoldBaseStandards.MudEjectorWidth, el = MoldBaseStandards.MudEjectorLength;
            double total = MoldBaseStandards.MudMovingThickness, psT = total - plan.P2;
            plan.Plates.Clear();
            plan.Components.Clear();
            plan.LibraryParts.Clear();
            PlateSpec p1 = Plate(MoldPlate.P1, "Placa Fixa", w, l, plan.P1, 0, "100");
            PlateSpec p2 = Plate(MoldPlate.P2, "Placa Móvel", w, l, plan.P2, -plan.P2, "200");
            PlateSpec ps = Plate(MoldPlate.PS, "Placa Suporte", w, l, psT, -total, "201");
            double peBottom = -total + MoldBaseStandards.MudEjectorRest;
            PlateSpec pe = Plate(MoldPlate.PE, "Placa Extratora", ew, el, t, peBottom, "302");
            PlateSpec cpe = Plate(MoldPlate.CPE, "Placa Porta-Extratores", ew, el, t, peBottom + t, "301");

            p2.Boss = new BossSpec { Width = MoldBaseStandards.MudBossWidth, Length = MoldBaseStandards.MudBossLength, Height = MoldBaseStandards.MudBossHeight, OnBottom = true };
            ps.Pocket = new PocketSpec { Width = MoldBaseStandards.MudBossWidth, Length = MoldBaseStandards.MudBossLength, Depth = psT, FromBottom = true };
            pe.Pocket = new PocketSpec { Width = MoldBaseStandards.MudCouplingPocket, Length = MoldBaseStandards.MudCouplingPocket, Depth = MoldBaseStandards.MudCouplingDepth, FromBottom = true };

            HolePair m8 = MoldBaseStandards.MudScrews;
            p2.Holes.Add(Middle(Hole(HoleGroup.ParafusosMolde, m8, HoleDepthKind.BlindFromBottom, MoldBaseStandards.MudScrewThread)));
            ps.Holes.Add(Middle(Hole(HoleGroup.ParafusosMolde, new HolePair(m8.AcrossA, m8.AlongC, MoldBaseStandards.MudScrewClearance), HoleDepthKind.Through)));
            ps.Holes.Add(Middle(Hole(HoleGroup.ParafusosMoldeCabeca, new HolePair(m8.AcrossA, m8.AlongC, MoldBaseStandards.MudScrewHead),
                HoleDepthKind.BlindFromBottom, MoldBaseStandards.MudScrewHeadDepth)));

            // Rasgos das guias: a faixa é da unidade (a partir da partição) e cai em quem estiver nela.
            foreach (PlateSpec p in new[] { p1, p2, ps })
                foreach (double[] band in new[] { MoldBaseStandards.MudFixedGroove, MoldBaseStandards.MudMovingGroove })
                {
                    double z0 = band[0] - p.BottomW, z1 = band[1] - p.BottomW;
                    if (Math.Min(z1, p.Thickness) - Math.Max(z0, 0) <= 1e-6) continue;
                    p.Groove = new GrooveSpec { Depth = MoldBaseStandards.MudGrooveDepth, Z0 = Math.Max(0, z0), Z1 = z1 >= p.Thickness - 1e-6 ? p.Thickness : z1 };
                }

            plan.Plates.AddRange(new[] { p1, p2, ps, cpe, pe });
            if (o.WithGuides) plan.LibraryParts.AddRange(MoldBaseStandards.LibraryParts(MoldBaseFamily.Mud));
        }

        /// <summary>
        /// Babyplast (padrão BABY PLAST): metades 100/200 de 76 × 76 com o degrau para 75 × 75 nos 21 de trás e as
        /// travas de canto, placas de apoio 101/201 presas por 4 M6, extratoras 300/301 a um curso da placa de apoio
        /// móvel.
        /// </summary>
        private static void BuildBabyplast(MoldBasePlan plan)
        {
            MoldBaseOptions o = plan.Options;
            double side = MoldBaseStandards.BabySide, bs = MoldBaseStandards.BabyBackingSide, bt = MoldBaseStandards.BabyBackingThickness;
            double es = MoldBaseStandards.BabyEjectorSide, et = MoldBaseStandards.BabyEjectorThickness;
            plan.Plates.Clear();
            plan.Components.Clear();
            plan.LibraryParts.Clear();
            PlateSpec p1 = Plate(MoldPlate.P1, "Metade Fixa", side, side, plan.P1, 0, "100");
            PlateSpec pbs = Plate(MoldPlate.PBS, "Placa de Apoio Fixa", bs, bs, bt, plan.P1, "101");
            PlateSpec p2 = Plate(MoldPlate.P2, "Metade Móvel", side, side, plan.P2, -plan.P2, "200");
            PlateSpec ps = Plate(MoldPlate.PS, "Placa de Apoio Móvel", bs, bs, bt, -plan.P2 - bt, "201");
            PlateSpec cpe = Plate(MoldPlate.CPE, "Placa Porta-Extratores", es, es, et, ps.BottomW - plan.Stroke.Stroke - et, "300");
            PlateSpec pe = Plate(MoldPlate.PE, "Placa Extratora", es, es, et, cpe.BottomW - et, "301");

            p1.Corners = new CornerSpec { Size = MoldBaseStandards.BabyLockBoss, Height = MoldBaseStandards.BabyLockBossHeight, Recess = false, OnBottom = true };
            p2.Corners = new CornerSpec { Size = MoldBaseStandards.BabyLockRecess, Height = MoldBaseStandards.BabyLockRecessDepth, Recess = true, OnBottom = false };
            double inset = (MoldBaseStandards.BabySide - MoldBaseStandards.BabyBackSide) / 2;
            p1.Step = new StepSpec { Inset = inset, Depth = MoldBaseStandards.BabyStepDepth, FromBottom = false };   // costas da fixa = em cima
            p2.Step = new StepSpec { Inset = inset, Depth = MoldBaseStandards.BabyStepDepth, FromBottom = true };
            HolePair back = MoldBaseStandards.BabyBackScrews;
            p1.Holes.Add(Hole(HoleGroup.ParafusosFixacao, back, HoleDepthKind.BlindFromTop, MoldBaseStandards.BabyBackScrewDepth));
            p2.Holes.Add(Hole(HoleGroup.ParafusosFixacao, back, HoleDepthKind.BlindFromBottom, MoldBaseStandards.BabyBackScrewDepth));

            HolePair m6 = MoldBaseStandards.BabyBackingScrews;
            p1.Holes.Add(Hole(HoleGroup.ParafusosMolde, m6, HoleDepthKind.BlindFromTop, MoldBaseStandards.BabyBackingThread));
            p2.Holes.Add(Hole(HoleGroup.ParafusosMolde, m6, HoleDepthKind.BlindFromBottom, MoldBaseStandards.BabyBackingThread));
            foreach (PlateSpec p in new[] { pbs, ps })
                p.Holes.Add(Hole(HoleGroup.ParafusosMolde, new HolePair(m6.AcrossA, m6.AlongC, MoldBaseStandards.BabyBackingClearance), HoleDepthKind.Through));
            HolePair ej = MoldBaseStandards.BabyEjectorScrews;
            cpe.Holes.Add(Hole(HoleGroup.ParafusosExtratora, ej, HoleDepthKind.Through));
            pe.Holes.Add(Hole(HoleGroup.ParafusosExtratora, new HolePair(ej.AcrossA, ej.AlongC, MoldBaseStandards.BabyEjectorClearance), HoleDepthKind.Through));
            pe.Holes.Add(Hole(HoleGroup.ParafusosExtratoraCabeca, new HolePair(ej.AcrossA, ej.AlongC, MoldBaseStandards.BabyEjectorHead),
                HoleDepthKind.BlindFromBottom, MoldBaseStandards.BabyEjectorHeadDepth));

            plan.Plates.AddRange(new[] { pbs, p1, p2, ps, cpe, pe });
            if (o.WithGuides) plan.LibraryParts.AddRange(MoldBaseStandards.LibraryParts(MoldBaseFamily.Babyplast, pe.TopW));
        }

        private static HoleSpec Middle(HoleSpec h) { h.Middle = true; return h; }

        private static HoleSpec Points(HoleGroup group, double[][] points, double diameter, HoleDepthKind depth, double depthMm = 0) =>
            new HoleSpec { Group = group, Points = points.Select(p => new[] { p[0], p[1] }).ToList(), Diameter = diameter, Depth = depth, DepthMm = depthMm };

        /// <summary>
        /// Zamak (padrão ZAMAK\MINI): FIXO 39 e MÓVEL 40 por padrão — mais grossas se as peças pedirem; paralelos de
        /// 2 × 16 + curso (o padrão tem curso 13: é o mínimo).
        /// </summary>
        private static void PlanZamak(MoldBasePlan plan, double fixedH, double movingH)
        {
            MoldBaseOptions o = plan.Options;
            plan.P1 = o.ForcedP1 > 0 ? o.ForcedP1 : Math.Max(MoldBaseStandards.ZamakFixed, Math.Ceiling(fixedH + o.FloorMm));
            plan.P2 = o.ForcedP2 > 0 ? o.ForcedP2 : Math.Max(MoldBaseStandards.ZamakMoving, Math.Ceiling(movingH + o.FloorMm));
            plan.Stroke = new StrokeOption(1, Math.Max(MoldBaseStandards.ZamakStroke, Math.Ceiling(plan.NeededStroke)), 0);
            BuildZamak(plan);

            var checks = plan.Checks;
            foreach (var side in new[] { Tuple.Create("Fixo", plan.P1, fixedH, MoldBaseStandards.ZamakFixed),
                                         Tuple.Create("Móvel", plan.P2, movingH, MoldBaseStandards.ZamakMoving) })
            {
                PlanCheck c = Floor(side.Item1, side.Item2, side.Item3, o);
                if (Math.Abs(side.Item2 - side.Item4) > 1e-6) c.Detail += $" (o padrão é {F(side.Item4)}).";
                checks.Add(c);
            }
            double parallel = 2 * MoldBaseStandards.ZamakEjectorThickness + plan.Stroke.Stroke;
            checks.Add(new PlanCheck("Curso de extração", CheckLevel.Ok,
                $"altura a extrair + folga = {F(plan.NeededStroke)} → curso {F(plan.Stroke.Stroke)}: paralelos de {F(parallel)} " +
                $"(2 × {F(MoldBaseStandards.ZamakEjectorThickness)} das extratoras + curso; o padrão tem {F(MoldBaseStandards.ZamakStroke)} de curso, paralelos de 45)."));
            plan.Notes.Add("As guias Ø12 e os parafusos das extratoras ficam 1 mm fora de centro em C, como no padrão (anti-inversão). " +
                           "O padrão não traz colunas como peças — só os furos.");
        }

        /// <summary>
        /// A matriz MINI de Zamak, como no "Matriz - via metal 2.asm": cavalete fixo (PBS) com a janela 91 × 91,
        /// FIXO (P1), MÓVEL (P2), paralelos (ESP ×2) nas bordas de A, extratoras 1/2 (CPE/PE) sobre o cavalete móvel
        /// (PBI), que também tem a janela. Pinos de retorno Ø8 sem cabeça cravados na extratora de cima.
        /// </summary>
        private static void BuildZamak(MoldBasePlan plan)
        {
            MoldBaseOptions o = plan.Options;
            double side = MoldBaseStandards.ZamakSide, ct = MoldBaseStandards.ZamakClampThickness, et = MoldBaseStandards.ZamakEjectorThickness;
            double parallel = 2 * et + plan.Stroke.Stroke;
            plan.Plates.Clear();
            plan.Components.Clear();
            plan.LibraryParts.Clear();
            PlateSpec pbs = Plate(MoldPlate.PBS, "Placa Cavalete Fixa", MoldBaseStandards.ZamakClampWidth, MoldBaseStandards.ZamakClampLength, ct, plan.P1);
            PlateSpec p1 = Plate(MoldPlate.P1, "Fixo", side, side, plan.P1, 0);
            PlateSpec p2 = Plate(MoldPlate.P2, "Móvel", side, side, plan.P2, -plan.P2);
            double pw = MoldBaseStandards.ZamakParallelWidth;
            PlateSpec esp = Plate(MoldPlate.ESP, "Paralelo", pw, side, parallel, -plan.P2 - parallel);
            esp.Count = 2;
            esp.OffsetA = side / 2 - pw / 2;
            PlateSpec pe = Plate(MoldPlate.PE, "Extratora 2", MoldBaseStandards.ZamakEjectorWidth, MoldBaseStandards.ZamakEjectorLength, et, esp.BottomW);
            PlateSpec cpe = Plate(MoldPlate.CPE, "Extratora 1", MoldBaseStandards.ZamakEjectorWidth, MoldBaseStandards.ZamakEjectorLength, et, esp.BottomW + et);
            PlateSpec pbi = Plate(MoldPlate.PBI, "Placa Cavalete Móvel", MoldBaseStandards.ZamakClampWidth, MoldBaseStandards.ZamakClampLength, ct, esp.BottomW - ct);

            pbs.Pocket = new PocketSpec { Width = MoldBaseStandards.ZamakWindowA, Length = MoldBaseStandards.ZamakWindowCFixed, Depth = ct, FromBottom = true };
            pbi.Pocket = new PocketSpec { Width = MoldBaseStandards.ZamakWindowA, Length = MoldBaseStandards.ZamakWindowCMoving, Depth = ct, FromBottom = true };

            // Guias (furos): cegas na FIXO pela partição, passantes na MÓVEL.
            p1.Holes.Add(Points(HoleGroup.Colunas, MoldBaseStandards.ZamakGuides, MoldBaseStandards.ZamakGuide, HoleDepthKind.BlindFromBottom,
                plan.P1 - MoldBaseStandards.ZamakGuideBack));
            p2.Holes.Add(Points(HoleGroup.Colunas, MoldBaseStandards.ZamakGuides, MoldBaseStandards.ZamakGuide, HoleDepthKind.Through));

            // M8 pela partição: FIXO → cavalete fixo, MÓVEL → paralelos.
            HolePair m8 = MoldBaseStandards.ZamakPlateScrews;
            var clearance = new HolePair(m8.AcrossA, m8.AlongC, MoldBaseStandards.ZamakScrewClearance);
            var head = new HolePair(m8.AcrossA, m8.AlongC, MoldBaseStandards.ZamakScrewHead);
            p1.Holes.Add(Hole(HoleGroup.ParafusosMolde, clearance, HoleDepthKind.Through));
            p1.Holes.Add(Hole(HoleGroup.ParafusosMoldeCabeca, head, HoleDepthKind.BlindFromBottom, MoldBaseStandards.ZamakScrewHeadDepth));
            p2.Holes.Add(Hole(HoleGroup.ParafusosMolde, clearance, HoleDepthKind.Through));
            p2.Holes.Add(Hole(HoleGroup.ParafusosMoldeCabeca, head, HoleDepthKind.BlindFromTop, MoldBaseStandards.ZamakScrewHeadDepth));
            pbs.Holes.Add(Hole(HoleGroup.ParafusosMolde, m8, HoleDepthKind.Through));
            HoleSpec top = SpacerHole(HoleGroup.ParafusosMolde, m8, esp.OffsetA);
            top.Depth = HoleDepthKind.BlindFromTop;
            top.DepthMm = MoldBaseStandards.ZamakParallelThread;
            esp.Holes.Add(top);

            // 6 M8 do cavalete móvel nos paralelos (no padrão o paralelo não tem a rosca — ela entra aqui).
            HolePair m8b = MoldBaseStandards.ZamakBaseScrews;
            pbi.Holes.Add(Middle(Hole(HoleGroup.ParafusosBase, new HolePair(m8b.AcrossA, m8b.AlongC, MoldBaseStandards.ZamakScrewClearance), HoleDepthKind.Through)));
            pbi.Holes.Add(Middle(Hole(HoleGroup.ParafusosMoldeCabeca, new HolePair(m8b.AcrossA, m8b.AlongC, MoldBaseStandards.ZamakScrewHead),
                HoleDepthKind.BlindFromBottom, MoldBaseStandards.ZamakScrewHeadDepth)));
            HoleSpec bottom = Middle(SpacerHole(HoleGroup.ParafusosBase, m8b, esp.OffsetA));
            bottom.Depth = HoleDepthKind.BlindFromBottom;
            bottom.DepthMm = MoldBaseStandards.ZamakBaseThread;
            esp.Holes.Add(bottom);

            // Pinos de retorno: passantes na MÓVEL, cravados 13,4 na extratora de cima.
            HolePair rp = MoldBaseStandards.ZamakReturnPins;
            p2.Holes.Add(Hole(HoleGroup.PinosRetorno, rp, HoleDepthKind.Through));
            cpe.Holes.Add(Hole(HoleGroup.PinosRetorno, rp, HoleDepthKind.BlindFromTop, MoldBaseStandards.ZamakReturnPinSeat));

            // Parafusos das extratoras (fora de centro como no padrão).
            cpe.Holes.Add(Points(HoleGroup.ParafusosExtratora, MoldBaseStandards.ZamakEjectorScrews, 8, HoleDepthKind.Through));
            pe.Holes.Add(Points(HoleGroup.ParafusosExtratora, MoldBaseStandards.ZamakEjectorScrews, MoldBaseStandards.ZamakEjectorClearance, HoleDepthKind.Through));
            pe.Holes.Add(Points(HoleGroup.ParafusosExtratoraCabeca, MoldBaseStandards.ZamakEjectorScrews, MoldBaseStandards.ZamakEjectorHead,
                HoleDepthKind.BlindFromBottom, MoldBaseStandards.ZamakEjectorHeadDepth));

            if (o.WithReturnPins)
            {
                double seatBottom = cpe.TopW - MoldBaseStandards.ZamakReturnPinSeat, len = -seatBottom;   // até a partição
                var pin = new ComponentSpec { Code = $"PR {F(MoldBaseStandards.ZamakReturnPin).PadLeft(2, '0')} x {F(len)}", Kind = "Pino de retorno" };
                pin.Solids.Add(new RoundSegment(MoldBaseStandards.ZamakReturnPin, 0, len));
                AddCorners(pin, rp, seatBottom);
                plan.Components.Add(pin);
            }
            plan.Plates.AddRange(new[] { pbs, p1, p2, esp, cpe, pe, pbi });
        }

        // ------------------------------------------------------------------ Polimold: colunas, buchas, pinos

        /// <summary>
        /// Colunas (C/CG) com a cabeça rebaixada no topo da P1, buchas (B/BG) com a cabeça no fundo da P2 — a de
        /// anti-inversão com a ponta menor, no canto (+A, −C), como no 3035 da biblioteca — e pinos de retorno com a
        /// cabeça rebaixada no fundo da porta-extratores. As placas ganham os rebaixos.
        /// </summary>
        public static void BuildPolimoldComponents(MoldBasePlan plan)
        {
            PolimoldSeries s = plan.Series;
            MoldBaseOptions o = plan.Options;
            plan.Components.Clear();
            PlateSpec p1 = plan.Plates.First(p => p.Plate == MoldPlate.P1);
            PlateSpec p2 = plan.Plates.First(p => p.Plate == MoldPlate.P2);
            PlateSpec cpe = plan.Plates.First(p => p.Plate == MoldPlate.CPE);
            char g = char.ToUpperInvariant(o.Guiding);

            if (o.WithGuides)
            {
                GuideSize gs = MoldBaseStandards.GuideFor(s.Columns.Diameter);
                double tip = MoldBaseStandards.ColumnTip(plan.P2);
                double guideS = g == 'A' ? 0 : gs.S;
                p1.Holes.Add(Hole(HoleGroup.ColunasCabeca, new HolePair(s.Columns.AcrossA, s.Columns.AlongC, gs.D3), HoleDepthKind.BlindFromTop, gs.Head));
                p2.Holes.Add(Hole(HoleGroup.BuchasCabeca, new HolePair(s.Columns.AcrossA, s.Columns.AlongC, gs.D3), HoleDepthKind.BlindFromBottom, gs.Head));
                foreach (bool anti in new[] { false, true })
                {
                    double d1 = anti ? gs.D1Anti : gs.D1;
                    var col = new ComponentSpec { Code = $"{(g == 'A' ? "C" : "CG")} {F(d1)} {Pad3(plan.P1)} {Pad3(tip)}", Kind = "Coluna guia" };
                    if (guideS > 0) col.Solids.Add(new RoundSegment(gs.D2, 0, guideS));
                    col.Solids.Add(new RoundSegment(gs.D3, -gs.Head, 0));
                    col.Solids.Add(new RoundSegment(gs.D2, -plan.P1, -gs.Head));
                    col.Solids.Add(new RoundSegment(d1, -plan.P1 - tip, -plan.P1));
                    var bush = new ComponentSpec { Code = $"{(g == 'A' ? "B" : "BG")} {F(d1)} {Pad3(plan.P2)}", Kind = "Bucha guia" };
                    bush.Solids.Add(new RoundSegment(gs.D2, -plan.P2 - guideS, 0));
                    bush.Solids.Add(new RoundSegment(gs.D3, -plan.P2, -plan.P2 + gs.Head));
                    bush.Bore = new RoundSegment(d1, -plan.P2 - guideS, 0);
                    double x = s.Columns.AcrossA / 2, y = s.Columns.AlongC / 2;
                    if (anti)
                    {
                        col.Positions.Add(new[] { x, -y, p1.TopW });
                        bush.Positions.Add(new[] { x, -y, 0.0 });
                    }
                    else
                        foreach (double[] c in new[] { new[] { -x, -y }, new[] { -x, y }, new[] { x, y } })
                        {
                            col.Positions.Add(new[] { c[0], c[1], p1.TopW });
                            bush.Positions.Add(new[] { c[0], c[1], 0.0 });
                        }
                    plan.Components.Add(col);
                    plan.Components.Add(bush);
                }
            }

            if (o.WithReturnPins && o.Mounting >= 4)
            {
                double d = s.ReturnPins.Diameter, head = MoldBaseStandards.ReturnPinHead(d), hh = MoldBaseStandards.ReturnPinHeadHeight;
                cpe.Holes.Add(Hole(HoleGroup.PinosRetornoCabeca, new HolePair(s.ReturnPins.AcrossA, s.ReturnPins.AlongC, head + 1), HoleDepthKind.BlindFromBottom, hh));
                double len = -cpe.BottomW;   // da cabeça (fundo da CPE) até a partição = CPE + curso + PS + P2
                var pin = new ComponentSpec { Code = $"PR {F(d)} x {F(len)}", Kind = "Pino de retorno" };
                pin.Solids.Add(new RoundSegment(head, 0, hh));
                pin.Solids.Add(new RoundSegment(d, hh, len));
                AddCorners(pin, s.ReturnPins, cpe.BottomW);
                plan.Components.Add(pin);
            }
        }

        private static void AddCorners(ComponentSpec c, HolePair pair, double z)
        {
            double x = pair.AcrossA / 2, y = pair.AlongC / 2;
            foreach (double[] p in new[] { new[] { -x, -y }, new[] { x, -y }, new[] { x, y }, new[] { -x, y } })
                c.Positions.Add(new[] { p[0], p[1], z });
        }

        private static string Pad3(double v) => F(v).PadLeft(3, '0');

        private static double PickOrCeil(IReadOnlyList<double> set, double need)
        {
            double p = Pick(set, need);
            return double.IsNaN(p) ? Math.Ceiling(need) : p;
        }

        /// <summary>A pilha de placas e os furos de cada uma, conforme a montagem (1–6) e o colunamento (A/B/C).</summary>
        public static void BuildPlates(MoldBasePlan plan)
        {
            PolimoldSeries s = plan.Series;
            MoldBaseOptions o = plan.Options;
            double baseWidth = o.Type == 4 ? s.B : s.A;
            double es = plan.Stroke.SpacerHeight;
            double bottom = -plan.P2 - s.PS - es;
            plan.Plates.Clear();

            PlateSpec pbs = Plate(MoldPlate.PBS, "Placa Base Superior", baseWidth, s.C, s.PBS, plan.P1);
            PlateSpec p1 = Plate(MoldPlate.P1, "Placa Cavidade Superior", s.A, s.C, plan.P1, 0);
            PlateSpec p2 = Plate(MoldPlate.P2, "Placa Cavidade Inferior", s.A, s.C, plan.P2, -plan.P2);
            PlateSpec ps = Plate(MoldPlate.PS, "Placa Suporte", s.A, s.C, s.PS, -plan.P2 - s.PS);
            PlateSpec esp = Plate(MoldPlate.ESP, "Espaçador", s.ESP, s.C, es, bottom);
            esp.OffsetA = s.A / 2 - s.ESP / 2;
            esp.Count = 2;
            PlateSpec pe = Plate(MoldPlate.PE, "Placa Extratora", s.P, s.C, s.PE, bottom + StopButtonHeight);
            PlateSpec cpe = Plate(MoldPlate.CPE, "Placa Porta-Extratores", s.P, s.C, s.CPE, pe.TopW);
            PlateSpec pbi = Plate(MoldPlate.PBI, "Placa Base Inferior", baseWidth, s.C, s.PBI, bottom - s.PBI);

            // Colunas e buchas: A = só nas cavidades; B = + placa base superior e suporte; C = em todas.
            char g = char.ToUpperInvariant(o.Guiding);
            var withColumns = new List<PlateSpec> { p1, p2 };
            if (g == 'B' || g == 'C') { withColumns.Add(pbs); withColumns.Add(ps); }
            if (g == 'C') withColumns.Add(pbi);
            foreach (PlateSpec p in withColumns) p.Holes.Add(Hole(HoleGroup.Colunas, s.Columns, HoleDepthKind.Through));

            // Fixação do molde (Pf1 PBS→P1, Pf2 PBI→espaçador→PS): passante nas placas base (e no espaçador, abaixo),
            // cego (2·d) na P1 e na PS.
            double d12 = s.MoldScrews.Diameter;
            pbs.Holes.Add(Hole(HoleGroup.ParafusosMolde, s.MoldScrews, HoleDepthKind.Through));
            p1.Holes.Add(Hole(HoleGroup.ParafusosMolde, s.MoldScrews, HoleDepthKind.BlindFromTop, 2 * d12));
            pbi.Holes.Add(Hole(HoleGroup.ParafusosMolde, s.MoldScrews, HoleDepthKind.Through));
            ps.Holes.Add(Hole(HoleGroup.ParafusosMolde, s.MoldScrews, HoleDepthKind.BlindFromBottom, 2 * d12));

            // Guias da extratora: montagens 2 e 4 saem da PBI; 3 e 5 saem da PS. Atravessam as duas extratoras.
            int m = o.Mounting;
            if (m >= 2 && m <= 5)
            {
                foreach (PlateSpec p in new[] { cpe, pe }) p.Holes.Add(Hole(HoleGroup.GuiasExtratora, s.EjectorGuides, HoleDepthKind.Through));
                (m == 2 || m == 4 ? pbi : ps).Holes.Add(Hole(HoleGroup.GuiasExtratora, s.EjectorGuides, HoleDepthKind.Through));
            }

            // Pinos de retorno (montagens 4, 5 e 6): P2, PS e a porta-extratores.
            if (m >= 4)
                foreach (PlateSpec p in new[] { p2, ps, cpe }) p.Holes.Add(Hole(HoleGroup.PinosRetorno, s.ReturnPins, HoleDepthKind.Through));

            // Fixação da extratora (Pf3): passante na PE, cego (1,5·d) na porta-extratores, por baixo — passante
            // quando o cego não deixa 2 mm de aço (CPE de 9/12 mm nas séries pequenas).
            double d3 = s.EjectorScrews.Diameter;
            pe.Holes.Add(Hole(HoleGroup.ParafusosExtratora, s.EjectorScrews, HoleDepthKind.Through));
            cpe.Holes.Add(1.5 * d3 > s.CPE - 2
                ? Hole(HoleGroup.ParafusosExtratora, s.EjectorScrews, HoleDepthKind.Through)
                : Hole(HoleGroup.ParafusosExtratora, s.EjectorScrews, HoleDepthKind.BlindFromBottom, 1.5 * d3));

            // Espaçadores: o Pf2 sempre os atravessa e, no colunamento C, as colunas também. O furo cai fora do
            // centro da peça (x = F/2 − OffsetA); a ocorrência do outro lado vai girada 180°.
            esp.Holes.Add(SpacerHole(HoleGroup.ParafusosMolde, s.MoldScrews, esp.OffsetA));
            if (g == 'C') esp.Holes.Add(SpacerHole(HoleGroup.Colunas, s.Columns, esp.OffsetA));

            plan.Plates.AddRange(new[] { pbs, p1, p2, ps, esp, cpe, pe, pbi });
        }

        private static void Check(MoldBasePlan plan, MoldBaseContent content, double fixedH, double movingH, double extract)
        {
            PolimoldSeries s = plan.Series;
            MoldBaseOptions o = plan.Options;
            var checks = plan.Checks;

            // 1) Área útil.
            bool areaOk = s.X >= plan.NeededX - 1e-6 && s.Y >= plan.NeededY - 1e-6;
            checks.Add(new PlanCheck("Área útil", areaOk ? CheckLevel.Ok : CheckLevel.Fail,
                $"peças + {F(o.SideMarginMm)} mm por lado pedem {F(plan.NeededX)} × {F(plan.NeededY)}; a {s.Name} tem X×Y {F(s.X)} × {F(s.Y)}" +
                (areaOk ? $" (sobra {F(s.X - plan.NeededX)} × {F(s.Y - plan.NeededY)})." : " — NÃO cabe.")));

            // 2) Espessuras das placas cavidade.
            checks.Add(Floor("P1 (lado fixo)", plan.P1, fixedH, o));
            checks.Add(Floor("P2 (lado móvel)", plan.P2, movingH, o));
            if (plan.SubSeries < 0)
                checks.Add(new PlanCheck("Sub-série", CheckLevel.Fail, $"P1 {F(plan.P1)} / P2 {F(plan.P2)} não estão na faixa da série ({string.Join(", ", s.Thicknesses.Select(F))}) — placa fora do catálogo."));

            // 3) Curso de extração.
            bool strokeOk = plan.Stroke.Stroke >= plan.NeededStroke - 1e-6;
            checks.Add(new PlanCheck("Curso de extração", strokeOk ? CheckLevel.Ok : CheckLevel.Fail,
                $"altura a extrair {F(extract)} + folga {F(o.StrokeClearanceMm)} = {F(plan.NeededStroke)}; curso {plan.Stroke.Index} da série = {F(plan.Stroke.Stroke)} " +
                $"(espaçadores {F(plan.Stroke.SpacerHeight)})" + (strokeOk ? "." : " — INSUFICIENTE.")));

            // 4) Refrigeração: há onde passar um canal reto, de borda a borda, em volta das peças?
            PlateSpec p1 = plan.Plates.First(p => p.Plate == MoldPlate.P1);
            PlateSpec p2 = plan.Plates.First(p => p.Plate == MoldPlate.P2);
            double ha = plan.Rotated ? Half(content.Min2, content.Max2, plan.Center2) : Half(content.Min1, content.Max1, plan.Center1);
            double hc = plan.Rotated ? Half(content.Min1, content.Max1, plan.Center1) : Half(content.Min2, content.Max2, plan.Center2);
            foreach (PlateSpec p in new[] { p1, p2 })
            {
                // Tipo 2: o rasgo de fixação (W de fundo) é usinado na borda da P1 — o canal não pode chegar lá.
                double edgeA = s.A / 2 - (o.Type == 2 && p.Plate == MoldPlate.P1 ? s.SlotW : 0);
                string acrossA = CoolingWindow(p, ha, edgeA, true, o, out bool okA);
                string alongC = CoolingWindow(p, hc, s.C / 2, false, o, out bool okC);
                checks.Add(new PlanCheck("Refrigeração em " + p.Plate, okA && okC ? CheckLevel.Ok : CheckLevel.Warning,
                    $"canal Ø{F(o.CoolingDiameterMm)} paralelo a C: {acrossA}; paralelo a A: {alongC}."));
            }

            // 5) Flexão da placa suporte entre os espaçadores (MISUMI #017): δ = 5·p·b·L⁴ / (32·E·B·h³).
            double span = s.A - 2 * s.ESP;
            // A pressão age na área do PRODUTO; sem produto marcado, na caixa das peças (mais conservador).
            double loaded = Math.Min(content.ProductAlongC(plan) ?? 2 * hc, s.C);
            double e = 206000.0;   // MPa, aço (2,1×10⁶ kgf/cm²)
            double delta = 5 * o.CavityPressureMpa * loaded * Math.Pow(span, 4) / (32 * e * s.C * Math.Pow(s.PS, 3));
            checks.Add(new PlanCheck("Flexão da placa suporte", delta <= o.MaxSupportDeflectionMm ? CheckLevel.Ok : CheckLevel.Warning,
                string.Format(CultureInfo.InvariantCulture, "estimativa {0:0.000} mm (PS {1}, vão {2} entre espaçadores, {3} MPa sobre {4} mm de C; MISUMI #017){5}",
                    delta, F(s.PS), F(span), F(o.CavityPressureMpa), F(loaded),
                    delta <= o.MaxSupportDeflectionMm ? "." : $" — acima de {F(o.MaxSupportDeflectionMm)} mm: avalie pilares de apoio ou PS mais grossa.")));
        }

        private static PlanCheck Floor(string name, double plate, double height, MoldBaseOptions o)
        {
            double floor = plate - height;
            CheckLevel level = height > plate + 1e-6 ? CheckLevel.Fail : floor < o.FloorMm - 1e-6 ? CheckLevel.Warning : CheckLevel.Ok;
            return new PlanCheck(name, level, height <= 1e-6
                ? $"{F(plate)} mm — nada deste lado da partição."
                : $"{F(plate)} mm para {F(height)} de peças → fundo de {F(floor)} mm (mínimo da casa {F(o.FloorMm)})" +
                  (level == CheckLevel.Fail ? " — as peças ATRAVESSAM a placa." : "."));
        }

        /// <summary>
        /// Faixas onde cabe o eixo de um canal reto que atravessa a placa de borda a borda: entre as peças
        /// (+ aço mínimo) e a face externa (− 15 mm), fora de qualquer furo da placa (+ aço mínimo).
        /// <paramref name="acrossA"/> = canal paralelo a C, deslocado no sentido de A.
        /// </summary>
        public static string CoolingWindow(PlateSpec plate, double contentHalf, double plateHalf, bool acrossA, MoldBaseOptions o, out bool ok)
        {
            double r = o.CoolingDiameterMm / 2;
            double lo = contentHalf + o.CoolingClearanceMm + r;
            double hi = plateHalf - o.CoolingEdgeMm - r;
            var free = new List<double[]>();
            if (hi > lo) free.Add(new[] { lo, hi });
            foreach (HoleSpec h in plate.Holes)
            {
                double c = (acrossA ? h.AcrossA : h.AlongC) / 2;
                double reach = h.Diameter / 2 + o.CoolingClearanceMm + r;
                free = Subtract(free, c - reach, c + reach);
            }
            ok = free.Count > 0;
            if (!ok) return hi <= lo ? $"sem espaço entre as peças ({F(lo)}) e a borda ({F(hi)})" : "bloqueado pelos furos";
            return "eixo a " + string.Join(" ou ", free.Select(f => Math.Abs(f[1] - f[0]) < 0.05 ? F(f[0]) : $"{F(f[0])}…{F(f[1])}")) + " mm do centro";
        }

        private static List<double[]> Subtract(List<double[]> intervals, double a, double b)
        {
            var result = new List<double[]>();
            foreach (double[] iv in intervals)
            {
                if (b <= iv[0] || a >= iv[1]) { result.Add(iv); continue; }
                if (a > iv[0]) result.Add(new[] { iv[0], a });
                if (b < iv[1]) result.Add(new[] { b, iv[1] });
            }
            return result;
        }

        private static double Half(double min, double max, double center) => Math.Max(Math.Abs(min - center), Math.Abs(max - center));

        private static double Pick(IReadOnlyList<double> set, double need)
        {
            foreach (double t in set) if (t >= need - 1e-6) return t;
            return double.NaN;
        }

        private static PlateSpec Plate(MoldPlate plate, string title, double width, double length, double thickness, double bottomW, string code = null) =>
            new PlateSpec { Plate = plate, Title = title, Width = width, Length = length, Thickness = thickness, BottomW = bottomW, Code = code };

        private static HoleSpec SpacerHole(HoleGroup group, HolePair pair, double offsetA)
        {
            double x0 = pair.AcrossA / 2 - offsetA;
            bool center = Math.Abs(x0) < 0.5;
            return new HoleSpec
            {
                Group = group, AcrossA = center ? 0 : 2 * Math.Abs(x0), AlongC = pair.AlongC, Diameter = pair.Diameter,
                Depth = HoleDepthKind.Through, Side = center ? 0 : Math.Sign(x0),
            };
        }

        private static HoleSpec Hole(HoleGroup group, HolePair pair, HoleDepthKind depth, double depthMm = 0) =>
            new HoleSpec { Group = group, AcrossA = pair.AcrossA, AlongC = pair.AlongC, Diameter = pair.Diameter, Depth = depth, DepthMm = depthMm };

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private sealed class Candidate
        {
            public PolimoldSeries Series;
            public bool Rotated, Fits;
            public double NeedX, NeedY, P1, P2;
            public StrokeOption Stroke;
        }
    }
}
