using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// T7 do "Criar Eletrodos" (docs/INSTRUCOES_OPUS_CRIAR_ELETRODOS.md): o eletrodo inteiro reprovado pelo juiz é dividido
    /// NA QUINA segundo um PLANO feito antes de construir (<see cref="CornerPlanner"/>); cada parte é construída e julgada.
    /// Parte com ponta &lt; <see cref="MinTipMm3"/> é reunida à vizinha (o plano é refeito e só as partes novas são construídas).
    /// </summary>
    public sealed partial class ElectrodeBuilder
    {
        /// <summary>Ponta menor que isto (mm³) não vira eletrodo (T7, item 4).</summary>
        public const double MinTipMm3 = 5.0;

        private sealed class PartBuild
        {
            public CornerCell Cell;
            public SmartElectrode E;
            public SmartBuilt B;
            public int Number;
        }

        /// <summary>Constrói as partes do plano <paramref name="b"/>.CornerPlan do inteiro <paramref name="whole"/>. Devolve o próximo número livre.</summary>
        private int BuildPlannedParts(dynamic app, dynamic asmDoc, SmartElectrode whole, SmartBuilt b, int number, int first,
            Func<int, string> pathOf, ElectrodeParams p, SmartElectrodeOptions o, SmartElectrodeReport report, List<SmartElectrode> final)
        {
            string label = $"E{number:00}";
            List<CornerCell> cells = b.CornerPlan;
            report.Notes.Add($"{label}: plano da divisão na quina — {cells.Count} parte(s).");
            foreach (string l in b.CornerLog) report.Notes.Add($"{label} [plano] {l}");

            var parts = new List<PartBuild>();
            int next = number;
            foreach (CornerCell cell in cells) parts.Add(BuildCell(app, asmDoc, whole, cell, label, next++, first, pathOf, p, o));

            // parte com ponta pequena demais: reunida à vizinha (refaz o plano forçando a reunião)
            var small = new HashSet<string>(parts.Where(x => x.B.Ok && x.B.TipVolumeMm3 < MinTipMm3).Select(x => x.Cell.Label));
            if (small.Count > 0 && b.Replan != null)
            {
                foreach (PartBuild x in parts.Where(x => small.Contains(x.Cell.Label)))
                    report.Notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1}: ponta {2:0.###} mm³ < {3} mm³ — reunida à vizinha.", label, x.Cell.Label, x.B.TipVolumeMm3, MinTipMm3));
                var (cells2, log2) = b.Replan(small);
                foreach (string l in log2.Where(l => l.Contains("reunid") || l.StartsWith("partes:"))) report.Notes.Add($"{label} [replano] {l}");
                var keep = new List<PartBuild>();
                var labels2 = new HashSet<string>(cells2.Select(c => c.Label));
                foreach (PartBuild x in parts)
                {
                    if (labels2.Contains(x.Cell.Label)) { keep.Add(x); continue; }
                    try { if (x.B.Ok && System.IO.File.Exists(x.B.Path)) System.IO.File.Delete(x.B.Path); }
                    catch (Exception ex) { Log.Warn($"Reunir: '{x.B.Path}' ficou — {ex.GetBaseException().Message}"); }
                }
                foreach (CornerCell cell in cells2.Where(c => !keep.Any(k => k.Cell.Label == c.Label)))
                    keep.Add(BuildCell(app, asmDoc, whole, cell, label, next++, first, pathOf, p, o));
                parts = keep;
            }

            // números em sequência a partir de 'number' (os arquivos ainda não estão na montagem: basta renomear)
            parts = parts.OrderBy(x => x.Number).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                int want = number + i;
                PartBuild x = parts[i];
                if (x.Number != want && x.B.Ok)
                {
                    string to = pathOf(want);
                    try { if (System.IO.File.Exists(to)) System.IO.File.Delete(to); System.IO.File.Move(x.B.Path, to); x.B.Path = to; }
                    catch (Exception ex) { x.B.Warnings.Add($"renumerar para {System.IO.Path.GetFileName(to)} falhou — {ex.GetBaseException().Message}"); }
                }
                x.Number = want;
                x.B.Index = want;
                x.E.Index = want - first + 1;
            }
            foreach (PartBuild x in parts)
            {
                if (x.B.Ok) PlaceElectrode((object)asmDoc, x.E, x.B);
                else if (x.E.Footprint.HasValue)
                {
                    BoundingBox f = x.E.Footprint.Value;
                    report.Notes.Add(string.Format(CultureInfo.InvariantCulture, "ATENÇÃO: a parte E{0:00} ({1}) falhou — a queima em x {2:0.##}…{3:0.##}, y {4:0.##}…{5:0.##} ficou SEM eletrodo.",
                        x.Number, x.E.PieceOf, f.MinX, f.MaxX, f.MinY, f.MaxY));
                }
                final.Add(x.E);
                report.Built.Add(x.B);
            }

            double sum = parts.Sum(x => x.B.TipVolumeMm3), fill = parts.Sum(x => x.B.FillerRemovedMm3);
            report.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} dividido na quina em {1} eletrodo(s): {2} fresável(is), {3} para fio, {4} falha(s). Ponta inteira {5:0.###} mm³; Σ pontas {6:0.###}; Σ enchimentos {7:0.###}; inteiro − enchimentos − Σ = {8:0.###} mm³ (recuo vertical das faces que não queimam + arredondamento).",
                label, parts.Count, parts.Count(x => x.B.Ok && x.B.Judge != null && x.B.Judge.Millable), parts.Count(x => x.B.Ok && x.B.WireCut != null),
                parts.Count(x => !x.B.Ok), b.TipVolumeMm3, sum, fill, b.TipVolumeMm3 - fill - sum));
            return number + parts.Count;
        }

        private PartBuild BuildCell(dynamic app, dynamic asmDoc, SmartElectrode whole, CornerCell cell, string label, int n, int first,
            Func<int, string> pathOf, ElectrodeParams p, SmartElectrodeOptions o)
        {
            double cx = whole.CenterXMm, cy = whole.CenterYMm, cz = whole.DeepestZMm;
            BoundingBox S(BoundingBox q) => new BoundingBox { MinX = q.MinX + cx, MaxX = q.MaxX + cx, MinY = q.MinY + cy, MaxY = q.MaxY + cy, MinZ = q.MinZ + cz, MaxZ = q.MaxZ + cz };
            List<BoundingBox> burnBoxes = BurnBoxes(whole);
            BoundingBox fp = S(cell.Footprint);
            var pe = new SmartElectrode
            {
                Owner = whole.Owner, OwnerTag = whole.OwnerTag, Footprint = fp, PieceOf = label + cell.Label, Cell = cell, CornerDepth = cell.Depth,
                Planes = cell.Planes.Select(c => c.Shifted(cx, cy, cz)).ToList(),
                CornerFillers = cell.Fillers.Select(f => f.Shifted(cx, cy, cz)).ToList(),
                PredictedFindings = whole.PredictedFindings,
                PieceDeepestZMm = DeepestIn(fp, burnBoxes, whole.DeepestZMm), ParentDeepestZMm = whole.DeepestZMm
            };
            pe.Regions.AddRange(whole.Regions);
            pe.Blank = SmartElectrodePlanner.BlankForFootprint(fp, whole.Box.MaxZ - pe.PieceDeepestZMm, _blankLibrary, o.Plan);
            pe.Notes.Add($"parte {pe.PieceOf}: {cell.Planes.Count} plano(s), {cell.Fillers.Count} enchimento(s); previstos {cell.Failed.Count} reprovado(s)" + (cell.MergeNote != null ? "; " + cell.MergeNote : ""));
            double dx = pe.CenterXMm - cx, dy = pe.CenterYMm - cy, dz = pe.DeepestZMm - cz;
            foreach (SmartPlacement at in whole.Placements)
            {
                double rad = at.RotZDeg * Math.PI / 180.0, c = Math.Round(Math.Cos(rad), 12), si = Math.Round(Math.Sin(rad), 12);
                pe.Placements.Add(new SmartPlacement { XMm = at.XMm + c * dx - si * dy, YMm = at.YMm + si * dx + c * dy, ZMm = at.ZMm + dz, RotZDeg = at.RotZDeg, Owner = at.Owner });
            }
            pe.Index = n - first + 1;
            SmartBuilt kb = BuildSmartElectrode(app, asmDoc, pe, n, pathOf(n), p, o, mayResplit: false, place: false);
            kb.CornerLog.Add($"previsto pelo plano: {cell.Failed.Count} reprovado(s)" + (cell.Failed.Count > 0 ? " — " + string.Join("; ", cell.Failed.Select(h => h.KindText).GroupBy(k => k).Select(g => g.Count() + " " + g.Key)) : ""));
            return new PartBuild { Cell = cell, E = pe, B = kb, Number = n };
        }
    }
}
