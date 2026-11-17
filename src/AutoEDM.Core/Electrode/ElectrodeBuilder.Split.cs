using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Machinability;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// T2 do "Criar Eletrodos" (docs/INSTRUCOES_OPUS_CRIAR_ELETRODOS.md): o eletrodo que a fresa não faz é dividido em
    /// pedaços. A lógica da divisão é pura (<see cref="ElectrodeSplitter"/>); aqui fica só o que lê o Solid Edge.
    /// </summary>
    public sealed partial class ElectrodeBuilder
    {
        /// <summary>
        /// Prevê, pela PEÇA, os raios que a fresa não faria no eletrodo (o raio CONVEXO da peça é o raio interno do
        /// negativo) e divide cada eletrodo do plano. O eletrodo sem achado, ou cuja divisão não sai, fica como está.
        /// </summary>
        private List<SmartElectrode> SplitWhatTheMillCannotDo(dynamic app, List<SmartElectrode> list, SmartElectrodeOptions o, SmartElectrodeReport report)
        {
            var result = new List<SmartElectrode>();
            foreach (SmartElectrode e in list)
            {
                try
                {
                    var owner = (OccurrenceInfo)e.OwnerTag;
                    OccurrenceTransform pose = owner != null ? AssemblyContext.TryGetPose(owner) : null;
                    if (pose == null) { result.Add(e); continue; }
                    double closing = ClosingZMm(app, owner, pose, e.Notes);
                    if (double.IsNaN(closing)) { result.Add(e); continue; }

                    BoundingBox fp = e.PlanFootprint(o.Plan.MarginMm);
                    fp.MinZ = e.DeepestZMm; fp.MaxZ = closing;
                    List<SplitFinding> findings = PartFindings(owner, pose, fp, closing, e.DeepestZMm, o.SplitRules.AlignToleranceDeg);
                    e.PredictedFindings = findings;
                    Log.Info($"Dividir E{e.Index:00}: {findings.Count} raio(s) que a fresa não faz, previstos pela peça.");
                    if (findings.Count == 0) { result.Add(e); continue; }

                    List<BoundingBox> burnBoxes = BurnBoxes(e);
                    double topBurn = e.Box.MaxZ;
                    SplitPlan plan = ElectrodeSplitter.Plan(fp, findings, burnBoxes,
                        r => SmartElectrodePlanner.BlankForFootprint(r, topBurn - DeepestIn(r, burnBoxes, e.DeepestZMm), _blankLibrary, o.Plan) != null,
                        o.SplitRules);
                    e.Split = plan;
                    foreach (SplitFinding f in findings) Log.Info("  [dividir] " + f.Describe());
                    Log.Info($"Dividir E{e.Index:00}: " + plan.Describe());

                    if (o.Split == SplitMode.Off)
                    {
                        e.Notes.Add($"{findings.Count} raio(s) abaixo da menor fresa previstos pela peça (sem o GAP); eletrodo inteiro — o juiz confere no construído. " +
                                    "Divisão só com \"dividir\": \"quina\" ou \"guilhotina\" (experimental).");
                        result.Add(e);
                        continue;
                    }
                    // T5 (Carlos, 2026-11-16): sem "dividir", a divisão PADRÃO é por REGIÃO e acontece na construção — o
                    // eletrodo inteiro é medido (raios e cantos vivos) e as regiões-problema são destacadas. A guilhotina
                    // de borda a borda fica só com "dividir": true.
                    if (o.Split == SplitMode.Auto)
                    {
                        e.Notes.Add($"{findings.Count} raio(s) que a fresa não faz, previstos pela peça: na construção o inteiro é medido e, se não sair fresável, separado por REGIÃO.");
                        result.Add(e);
                        continue;
                    }
                    if (!SplitDecision.ApplyPlan(plan, o.Split, out string why))
                    {
                        e.WireCut = why;
                        e.Notes.Add(why + " (" + plan.Describe() + ")");
                        report.Notes.Add($"E{e.Index:00} inteiro: {why}.");
                        result.Add(e);
                        continue;
                    }

                    report.Notes.Add($"E{e.Index:00} dividido: " + plan.Describe() + ".");
                    result.AddRange(PiecesOf(e, plan, o));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Dividir E{e.Index:00}: falhou — {ex.GetBaseException().Message}");
                    e.Notes.Add("divisão não avaliada: " + ex.GetBaseException().Message);
                    result.Add(e);
                }
            }
            for (int i = 0; i < result.Count; i++) result[i].Index = i + 1;
            return result;
        }

        /// <summary>Os pedaços de um plano de divisão como eletrodos: pegada exata, fundo próprio, barra e as posições do pai.</summary>
        private List<SmartElectrode> PiecesOf(SmartElectrode e, SplitPlan plan, SmartElectrodeOptions o)
        {
            List<BoundingBox> burnBoxes = BurnBoxes(e);
            double topBurn = e.Box.MaxZ;
            var pieces = new List<SmartElectrode>();
            foreach (SplitPiece piece in plan.Pieces)
            {
                var pe = new SmartElectrode
                {
                    Owner = e.Owner, OwnerTag = e.OwnerTag, Footprint = piece.BuildFootprint, PieceOf = e.PieceOf ?? $"E{e.Index:00}",
                    Parent = e.Parent ?? e,
                    Piece = piece, Split = plan, PredictedFindings = e.PredictedFindings,
                    PieceDeepestZMm = DeepestIn(piece.Footprint, burnBoxes, e.DeepestZMm),
                    ParentDeepestZMm = e.Footprint.HasValue ? e.ParentDeepestZMm : e.DeepestZMm
                };
                pe.Regions.AddRange(e.Regions);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "  [pedaço] x {0:0.######}…{1:0.######}, y {2:0.######}…{3:0.######}, fundo Z {4:0.######}",
                    piece.Footprint.MinX, piece.Footprint.MaxX, piece.Footprint.MinY, piece.Footprint.MaxY, pe.PieceDeepestZMm));
                pe.Blank = SmartElectrodePlanner.BlankForFootprint(piece.Footprint, topBurn - pe.PieceDeepestZMm, _blankLibrary, o.Plan);
                if (pe.Blank == null)
                    pe.Notes.Add(string.Format(CultureInfo.InvariantCulture, "pegada {0:0.0} × {1:0.0} mm não cabe em nenhuma barra.", piece.Footprint.SizeX, piece.Footprint.SizeY));
                // A mesma contagem da construção (DescribeRemaining): todo raio sem corte cuja face entra no pedaço, menos
                // as pontas que saem pela caixa do enchimento (toro de canto de rebaixo além de uma parede).
                var all = e.PredictedFindings ?? new List<SplitFinding>();
                var cleared = new HashSet<SplitFinding>(ElectrodeSplitter.FillerBoxes(piece, all, -1e6, 1e6, 0.0, o.SplitRules)
                    .Where(c => c.Finding != null).Select(c => c.Finding));
                // Uma lasca de face com menos de 0,02 mm dentro do pedaço não aparece no construído (medido: o R0,2 da face
                // 1497 entra 0,01 mm no E01 do 15428.100 e a construção não o vê).
                List<SplitFinding> left = all.Where(f => !f.Chosen.HasValue && !cleared.Contains(f) &&
                    Math.Min(f.MaxX, piece.Footprint.MaxX) - Math.Max(f.MinX, piece.Footprint.MinX) > 0.02 &&
                    Math.Min(f.MaxY, piece.Footprint.MaxY) - Math.Max(f.MinY, piece.Footprint.MinY) > 0.02).ToList();
                if (left.Count > 0)
                    pe.WireCut = "previstos " + DescribeRemaining(e.PredictedFindings, left, true);
                // As posições do pai, deslocadas até o centro do pedaço (girando junto quando o pai é usado girado).
                double dx = pe.CenterXMm - e.CenterXMm, dy = pe.CenterYMm - e.CenterYMm, dz = pe.DeepestZMm - e.DeepestZMm;
                foreach (SmartPlacement at in e.Placements)
                {
                    double rad = at.RotZDeg * Math.PI / 180.0, c = Math.Round(Math.Cos(rad), 12), s = Math.Round(Math.Sin(rad), 12);
                    pe.Placements.Add(new SmartPlacement { XMm = at.XMm + c * dx - s * dy, YMm = at.YMm + s * dx + c * dy, ZMm = at.ZMm + dz, RotZDeg = at.RotZDeg, Owner = at.Owner });
                }
                pieces.Add(pe);
            }
            return pieces;
        }

        private static List<BoundingBox> BurnBoxes(SmartElectrode e) =>
            e.Faces.Select(f => new BoundingBox { MinX = f.MinMm[0], MaxX = f.MaxMm[0], MinY = f.MinMm[1], MaxY = f.MaxMm[1], MinZ = f.MinMm[2], MaxZ = f.MaxMm[2] }).ToList();

        /// <summary>
        /// Passo 5 da T2: divide de novo a pegada de <paramref name="e"/> pelos achados MEDIDOS no eletrodo construído.
        /// Devolve o plano só se ele dividir (mais de um pedaço); senão null.
        /// </summary>
        private SplitPlan ResplitPlan(SmartElectrode e, List<SplitFinding> measured, SmartElectrodeOptions o)
        {
            List<BoundingBox> burnBoxes = BurnBoxes(e);
            double topBurn = e.Box.MaxZ;
            BoundingBox fp = e.PlanFootprint(o.Plan.MarginMm);
            SplitPlan plan = ElectrodeSplitter.Plan(fp, measured, burnBoxes,
                r => SmartElectrodePlanner.BlankForFootprint(r, topBurn - DeepestIn(r, burnBoxes, e.DeepestZMm), _blankLibrary, o.Plan) != null,
                o.SplitRules);
            foreach (SplitFinding f in measured) Log.Info("  [redividir] " + f.Describe());
            Log.Info($"Redividir E{e.Index:00}: " + plan.Describe());
            return plan.IsSplit ? plan : null;
        }

        private static bool Touches(SplitFinding f, BoundingBox r) =>
            Math.Min(f.MaxX, r.MaxX) - Math.Max(f.MinX, r.MinX) > 1e-3 && Math.Min(f.MaxY, r.MaxY) - Math.Max(f.MinY, r.MinY) > 1e-3;

        /// <summary>O fundo da queima dentro de um retângulo: o menor Z das faces de queima que o tocam.</summary>
        private static double DeepestIn(BoundingBox r, List<BoundingBox> burn, double fallback)
        {
            double z = double.MaxValue;
            foreach (BoundingBox b in burn)
                if (Math.Min(b.MaxX, r.MaxX) - Math.Max(b.MinX, r.MinX) > 1e-3 && Math.Min(b.MaxY, r.MaxY) - Math.Max(b.MinY, r.MinY) > 1e-3)
                    z = Math.Min(z, b.MinZ);
            return z == double.MaxValue ? fallback : z;
        }

        /// <summary>
        /// Os raios CONVEXOS da peça abaixo da menor fresa, levados à montagem e filtrados: dentro da pegada (em planta)
        /// e abaixo do fechamento. São os raios internos que o eletrodo terá.
        /// </summary>
        private static List<SplitFinding> PartFindings(OccurrenceInfo owner, OccurrenceTransform pose, BoundingBox fp, double closingZ, double deepestZ, double alignTolDeg)
        {
            var list = new List<SplitFinding>();
            object body = TryGetSolidBody(owner.OccurrenceDocument);
            if (body == null) return list;
            foreach (SmallRadiusHit h in BRepRadiusProbe.ProbeConvex(body, ToolLadder.Shop().SmallestRadiusMm))
            {
                if (h.FaceMinMm == null) continue;
                OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), h.FaceMinMm, h.FaceMaxMm, out double[] mn, out double[] mx);
                if (Math.Min(mx[0], fp.MaxX) - Math.Max(mn[0], fp.MinX) <= 1e-3) continue;
                if (Math.Min(mx[1], fp.MaxY) - Math.Max(mn[1], fp.MinY) <= 1e-3) continue;
                if (mn[2] >= closingZ - 0.01 || mx[2] <= deepestZ - 0.01) continue;
                double[] dir = null, point = null;
                if (h.AxisDir != null && h.AxisPointMm != null)
                {
                    point = MapPointMm(pose, h.AxisPointMm);
                    double[] tip = MapPointMm(pose, new[] { h.AxisPointMm[0] + h.AxisDir[0], h.AxisPointMm[1] + h.AxisDir[1], h.AxisPointMm[2] + h.AxisDir[2] });
                    dir = new[] { tip[0] - point[0], tip[1] - point[1], tip[2] - point[2] };
                }
                list.Add(SplitFinding.From(h.FaceId, h.Kind == "raio de canto", h.RadiusMm, h.MajorRadiusMm, dir, point, mn, mx, alignTolDeg));
            }
            return list;
        }

        /// <summary>
        /// Os raios internos do ELETRODO construído (côncavos, abaixo do fechamento), levados à montagem: a peça do
        /// eletrodo não gira, então é só somar a origem dela (centro da pegada, fundo da queima).
        /// </summary>
        private static List<SplitFinding> ElectrodeFindings(object partDoc, SmartElectrode e, double closingLocalZ, double alignTolDeg)
        {
            var list = new List<SplitFinding>();
            object body = TryGetSolidBody(partDoc);
            if (body == null) return list;
            double[] o = { e.CenterXMm, e.CenterYMm, e.DeepestZMm };
            foreach (SmallRadiusHit h in BRepRadiusProbe.Probe(body, ToolLadder.Shop()).Where(h => h.MinZmm < closingLocalZ - 0.01))
            {
                double[] mn = h.FaceMinMm != null ? new[] { h.FaceMinMm[0] + o[0], h.FaceMinMm[1] + o[1], h.FaceMinMm[2] + o[2] } : new[] { o[0], o[1], h.MinZmm + o[2] };
                double[] mx = h.FaceMaxMm != null ? new[] { h.FaceMaxMm[0] + o[0], h.FaceMaxMm[1] + o[1], h.FaceMaxMm[2] + o[2] } : new[] { o[0], o[1], h.MaxZmm + o[2] };
                double[] p = h.AxisPointMm != null ? new[] { h.AxisPointMm[0] + o[0], h.AxisPointMm[1] + o[1], h.AxisPointMm[2] + o[2] } : null;
                SplitFinding f = SplitFinding.From(h.FaceId, h.Kind == "raio de canto", h.RadiusMm, h.MajorRadiusMm, h.AxisDir, p, mn, mx, alignTolDeg);
                if (h.Verdict != MachinabilityVerdict.BelowMinimumRadius) f.Kind += " (alcance)";
                list.Add(f);
            }
            return list;
        }

        /// <summary>
        /// T2c: os achados que restam no eletrodo construído, agrupados pelo motivo — o do achado previsto pela peça que
        /// bate com ele (mesmo raio, caixas que se tocam): "enchimento na divisa" se ele tinha corte, senão o motivo de não
        /// ter. Null se não resta nenhum.
        /// </summary>
        internal static string DescribeRemaining(IList<SplitFinding> predicted, IList<SplitFinding> measured, bool isPiece, double tolMm = 0.05)
        {
            if (measured == null || measured.Count == 0) return null;
            var reasons = new List<string>();
            foreach (SplitFinding m in measured)
            {
                // o previsto mais perto (centro da caixa), entre os de mesmo raio cujas caixas tocam a do medido
                SplitFinding q = predicted?.Where(x => Math.Abs(x.RadiusMm - m.RadiusMm) < 0.005 &&
                    x.MinX - tolMm <= m.MaxX && m.MinX - tolMm <= x.MaxX && x.MinY - tolMm <= m.MaxY && m.MinY - tolMm <= x.MaxY &&
                    x.MinZ - tolMm <= m.MaxZ && m.MinZ - tolMm <= x.MaxZ)
                    .OrderBy(x => Math.Pow((x.MinX + x.MaxX - m.MinX - m.MaxX) / 2, 2) + Math.Pow((x.MinY + x.MaxY - m.MinY - m.MaxY) / 2, 2) +
                                  Math.Pow((x.MinZ + x.MaxZ - m.MinZ - m.MaxZ) / 2, 2))
                    .FirstOrDefault();
                string r = q == null ? "não previsto pela peça (" + m.Kind + ")"
                    : q.Chosen.HasValue ? (isPiece ? "enchimento na divisa " : "com corte possível em ") + q.Chosen.Value
                    : "sem corte: " + (q.Unresolved ?? q.NoCut ?? "?");
                reasons.Add(string.Format(CultureInfo.InvariantCulture, "R{0:0.###} {1}", m.RadiusMm, r));
            }
            return $"{measured.Count} raio(s) que a fresa não faz: " +
                   string.Join("; ", reasons.GroupBy(x => x).Select(g => g.Count() + "× " + g.Key));
        }

        /// <summary>
        /// T5: o eletrodo inteiro que não saiu fresável vira o PRINCIPAL (sem as pegadas das regiões, recortadas do fundo ao
        /// fechamento) mais um eletrodo por região. Geometria idêntica por giro (regra do planejador) = o mesmo eletrodo em
        /// mais posições; espelhada = outro eletrodo.
        /// </summary>
        private List<SmartElectrode> RegionElectrodes(SmartElectrode e, SmartBuilt b, SmartElectrodeOptions o, List<string> notes)
        {
            List<EdmRegion> regs = b.Regions;
            var twin = Enumerable.Repeat(-1, regs.Count).ToArray();
            var rot = new int[regs.Count];
            for (int i = 0; i < regs.Count; i++)
                for (int j = i + 1; j < regs.Count; j++)
                {
                    string c = RegionFinder.Compare(b.RegionFaces, regs[i], regs[j]);
                    if (c == "diferente") continue;
                    string verdict = c == "espelhada" ? " → eletrodos diferentes" : "";
                    if (c.StartsWith("idêntica") && twin[i] < 0 && twin[j] < 0)
                    {
                        int deg = int.Parse(c.Substring(c.IndexOf("giro ", StringComparison.Ordinal) + 5).TrimEnd('°'), CultureInfo.InvariantCulture);
                        // o mesmo eletrodo só serve se a pegada do original, girada, cair na pegada do outro
                        BoundingBox rf = Rotated(regs[i].Footprint, Center(regs[i].Box), Center(regs[j].Box), deg);
                        BoundingBox fj = regs[j].Footprint;
                        if (Math.Abs(rf.MinX - fj.MinX) < 0.02 && Math.Abs(rf.MaxX - fj.MaxX) < 0.02 && Math.Abs(rf.MinY - fj.MinY) < 0.02 && Math.Abs(rf.MaxY - fj.MaxY) < 0.02)
                        { twin[j] = i; rot[j] = deg; verdict = " → mesmo eletrodo nas duas posições"; }
                        else verdict = string.Format(CultureInfo.InvariantCulture, " — mas as pegadas não se encaixam (girada x {0:0.##}…{1:0.##} y {2:0.##}…{3:0.##} contra x {4:0.##}…{5:0.##} y {6:0.##}…{7:0.##}): eletrodos separados",
                            rf.MinX, rf.MaxX, rf.MinY, rf.MaxY, fj.MinX, fj.MaxX, fj.MinY, fj.MaxY);
                    }
                    notes.Add($"E{e.Index:00}: regiões {regs[i].Name} × {regs[j].Name}: {c}{verdict}");
                }

            var parts = new List<SmartElectrode>();
            var main = new SmartElectrode
            {
                Owner = e.Owner, OwnerTag = e.OwnerTag, Blank = e.Blank, PieceOf = $"E{e.Index:00}", PredictedFindings = e.PredictedFindings,
                CutOutLocal = regs.Select(r => r.Footprint).ToList()
            };
            main.Regions.AddRange(e.Regions);
            main.Placements.AddRange(e.Placements);
            main.Notes.Add($"principal: sem {regs.Count} região(ões) destacada(s)");
            parts.Add(main);
            notes.Add($"E{e.Index:00} separado por região: principal + {regs.Count - twin.Count(t => t >= 0)} eletrodo(s) de região ({regs.Count} região(ões)).");

            List<BoundingBox> burnBoxes = BurnBoxes(e);
            double topBurn = e.Box.MaxZ;
            for (int i = 0; i < regs.Count; i++)
            {
                if (twin[i] >= 0) continue;
                EdmRegion r = regs[i];
                BoundingBox fp = Shift(r.Footprint, e.CenterXMm, e.CenterYMm);
                var pe = new SmartElectrode
                {
                    Owner = e.Owner, OwnerTag = e.OwnerTag, Footprint = fp, PieceOf = $"E{e.Index:00}", Region = r, PredictedFindings = e.PredictedFindings,
                    PieceDeepestZMm = DeepestIn(fp, burnBoxes, e.DeepestZMm), ParentDeepestZMm = e.DeepestZMm
                };
                pe.Regions.AddRange(e.Regions);
                pe.Blank = SmartElectrodePlanner.BlankForFootprint(fp, topBurn - pe.PieceDeepestZMm, _blankLibrary, o.Plan);
                pe.Notes.Add("região " + r.Describe());
                var copies = new List<(BoundingBox fp, int rot)> { (fp, 0) };
                for (int j = 0; j < regs.Count; j++) if (twin[j] == i) copies.Add((Shift(regs[j].Footprint, e.CenterXMm, e.CenterYMm), rot[j]));
                foreach (var cp in copies)
                {
                    double dx = (cp.fp.MinX + cp.fp.MaxX) / 2 - e.CenterXMm, dy = (cp.fp.MinY + cp.fp.MaxY) / 2 - e.CenterYMm, dz = pe.DeepestZMm - e.DeepestZMm;
                    foreach (SmartPlacement at in e.Placements)
                    {
                        double rad = at.RotZDeg * Math.PI / 180.0, c = Math.Round(Math.Cos(rad), 12), s = Math.Round(Math.Sin(rad), 12);
                        pe.Placements.Add(new SmartPlacement { XMm = at.XMm + c * dx - s * dy, YMm = at.YMm + s * dx + c * dy, ZMm = at.ZMm + dz, RotZDeg = (at.RotZDeg + cp.rot) % 360, Owner = at.Owner });
                    }
                }
                parts.Add(pe);
            }
            return parts;
        }

        private static double[] Center(BoundingBox b) => new[] { (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2 };

        private static BoundingBox Shift(BoundingBox b, double dx, double dy) =>
            new BoundingBox { MinX = b.MinX + dx, MaxX = b.MaxX + dx, MinY = b.MinY + dy, MaxY = b.MaxY + dy, MinZ = b.MinZ, MaxZ = b.MaxZ };

        /// <summary>A caixa <paramref name="f"/> (em volta do centro <paramref name="from"/>) girada <paramref name="deg"/> (múltiplo de 90°) e levada ao centro <paramref name="to"/>.</summary>
        internal static BoundingBox Rotated(BoundingBox f, double[] from, double[] to, int deg)
        {
            double x0 = f.MinX - from[0], x1 = f.MaxX - from[0], y0 = f.MinY - from[1], y1 = f.MaxY - from[1];
            int k = ((deg / 90) % 4 + 4) % 4;
            double a0, a1, b0, b1;
            switch (k)
            {
                case 1: a0 = -y1; a1 = -y0; b0 = x0; b1 = x1; break;
                case 2: a0 = -x1; a1 = -x0; b0 = -y1; b1 = -y0; break;
                case 3: a0 = y0; a1 = y1; b0 = -x1; b1 = -x0; break;
                default: a0 = x0; a1 = x1; b0 = y0; b1 = y1; break;
            }
            return new BoundingBox { MinX = to[0] + a0, MaxX = to[0] + a1, MinY = to[1] + b0, MaxY = to[1] + b1, MinZ = f.MinZ, MaxZ = f.MaxZ };
        }

        /// <summary>Confronta os achados previstos pela peça com os medidos no eletrodo: mesmo raio e caixas que se tocam.</summary>
        internal static string CompareFindings(IList<SplitFinding> predicted, IList<SplitFinding> measured, double tolMm = 0.05)
        {
            var usedP = new HashSet<SplitFinding>();
            int matched = 0;
            var onlyMeasured = new List<SplitFinding>();
            foreach (SplitFinding m in measured)
            {
                SplitFinding p = predicted.FirstOrDefault(q => !usedP.Contains(q) && Math.Abs(q.RadiusMm - m.RadiusMm) < 0.005 &&
                    q.MinX - tolMm <= m.MaxX && m.MinX - tolMm <= q.MaxX && q.MinY - tolMm <= m.MaxY && m.MinY - tolMm <= q.MaxY &&
                    q.MinZ - tolMm <= m.MaxZ && m.MinZ - tolMm <= q.MaxZ);
                if (p == null) { onlyMeasured.Add(m); continue; }
                usedP.Add(p); matched++;
            }
            List<SplitFinding> onlyPredicted = predicted.Where(q => !usedP.Contains(q)).ToList();
            foreach (SplitFinding m in onlyMeasured) Log.Info("  [conferência] só no eletrodo: " + m.Describe());
            foreach (SplitFinding q in onlyPredicted) Log.Info("  [conferência] só na peça: " + q.Describe());
            return $"previstos pela peça {predicted.Count}, medidos no eletrodo {measured.Count}: {matched} batem, " +
                   $"{onlyMeasured.Count} só no eletrodo, {onlyPredicted.Count} só na peça";
        }

        /// <summary>
        /// Z (mm, montagem) do plano de fechamento da peça dona: o plano horizontal exato mais alto do corpo; mas, se a
        /// peça tem algo ACIMA da face pintada "10 - FECHAM." (regra 8 do Carlos, 2026-11-12), vale a face pintada.
        /// NaN se nem a caixa do corpo se lê.
        /// </summary>
        private double ClosingZMm(dynamic app, OccurrenceInfo owner, OccurrenceTransform pose, List<string> warnings)
        {
            object body = TryGetSolidBody(owner.OccurrenceDocument);
            if (body == null || !FaceGeometry.TryGetBodyRangeMm(body, out double[] bmin, out double[] bmax)) return double.NaN;
            OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), bmin, bmax, out bmin, out bmax);
            // A caixa do Body.GetRange é FOLGADA (medido no 15428.100: +0,024 mm acima do aço). Um prisma que passa do
            // aço por micrômetros deixa uma aba da pegada inteira sobre a ponta: vale o plano exato.
            double top = TopPlaneZMm(body, pose, bmax[2]);
            if (double.IsNaN(top))
            {
                top = bmax[2];
                warnings?.Add("não achei a face plana do topo do aço: o fechamento saiu da caixa do corpo (pode deixar uma aba fina).");
            }
            double painted = PaintedClosingZMm(app, owner.OccurrenceDocument, pose);
            Log.Info(string.Format(CultureInfo.InvariantCulture, "Fechamento de '{0}': topo exato Z {1:0.######}, face \"10 - FECHAM.\" Z {2}.",
                owner.Name, top, double.IsNaN(painted) ? "—" : painted.ToString("0.######", CultureInfo.InvariantCulture)));
            if (!double.IsNaN(painted) && painted < top - 0.01)
            {
                string msg = string.Format(CultureInfo.InvariantCulture,
                    "o ponto mais alto da peça (Z {0:0.###}) não é o fechamento: usei a face \"10 - FECHAM.\" (Z {1:0.###}).", top, painted);
                Log.Info("Fechamento: " + msg);
                warnings?.Add(msg);
                return painted;
            }
            return top;
        }

        /// <summary>Z (mm, montagem) da face plana mais alta pintada "10 - FECHAM." com a normal para +Z. NaN se não houver.</summary>
        private double PaintedClosingZMm(dynamic app, object partDoc, OccurrenceTransform pose)
        {
            double best = double.NaN;
            try
            {
                foreach (SelectedFace sf in _faceSelector.EnumerateColoredFaces(partDoc, app))
                {
                    try
                    {
                        if (!OleColor.Matches(sf.DetectedColor, EdmAssemblyBuilder.ClosingColor, 8)) continue;
                        object geo = (object)((dynamic)sf.ComFace).Geometry;
                        if ((int)((dynamic)geo).Type != igPlaneGeometry) continue;
                        if (!EdmAssemblyBuilder.TryOutwardNormal(sf.ComFace, out double[] n)) continue;
                        double[] p0 = MapPointMm(pose, new[] { 0.0, 0.0, 0.0 }), p1 = MapPointMm(pose, new[] { Units.MToMm(n[0]), Units.MToMm(n[1]), Units.MToMm(n[2]) });
                        double nz = Units.MmToM(p1[2] - p0[2]);
                        if (nz < 0.9999) continue;
                        if (!FaceGeometry.TryOneArrayOut(geo, "GetRootPoint", out double[] rp, out _)) continue;
                        double z = MapPointMm(pose, new[] { Units.MToMm(rp[0]), Units.MToMm(rp[1]), Units.MToMm(rp[2]) })[2];
                        if (double.IsNaN(best) || z > best) best = z;
                    }
                    catch { }
                    finally { ComLifetime.Release(sf.ComFace); }
                }
            }
            catch (Exception ex) { Log.Info("Fechamento pintado: " + ex.GetBaseException().Message); }
            return best;
        }
    }
}
