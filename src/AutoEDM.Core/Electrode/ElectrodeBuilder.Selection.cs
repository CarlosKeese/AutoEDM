using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    /// <summary>T9: uma face escolhida para o modo semiautomático, guardada pela identidade (a seleção morre quando a _EDM abre).</summary>
    public sealed class SelectedBurnFace
    {
        public int FaceId;
        public double AreaMm2;
        /// <summary>Arquivo da peça dona (a ocorrência, ou a peça aberta sozinha).</summary>
        public string OwnerPath;
        public string OccurrenceName;
    }

    /// <summary>
    /// T9 — modo semiautomático do "Criar eletrodos": com faces selecionadas, UM eletrodo só com elas (pegada = retângulo
    /// delas + a margem), com os avisos de faces rosa não selecionadas dentro da pegada e de selecionadas que não são rosa.
    /// </summary>
    public sealed partial class ElectrodeBuilder
    {
        /// <summary>As faces selecionadas no documento ativo (peça sozinha ou montagem), por ID + área + arquivo dono.</summary>
        public static List<SelectedBurnFace> ReadSelectionForSmart(dynamic activeDoc, List<string> notes)
        {
            var list = new List<SelectedBurnFace>();
            int n = 0;
            try { n = (int)activeDoc.SelectSet.Count; } catch { }
            if (n == 0) return list;
            List<PickedFace> picks = ReadSelectedFaces(activeDoc, out int skipped);
            string activePath = null; int type = 0;
            try { activePath = (string)activeDoc.FullName; } catch { }
            try { type = (int)activeDoc.Type; } catch { }
            foreach (PickedFace pk in picks)
            {
                var s = new SelectedBurnFace { OccurrenceName = pk.OccurrenceName };
                try { s.FaceId = (int)((dynamic)pk.Face).ID; } catch { }
                try { s.AreaMm2 = (double)((dynamic)pk.Face).Area * 1e6; } catch { }
                if (pk.Occurrence != null) s.OwnerPath = TryGetComPropertyQuiet(pk.Occurrence, "OccurrenceFileName") as string;
                else if (type == 1) s.OwnerPath = activePath;   // peça aberta sozinha
                if (s.FaceId == 0 || string.IsNullOrEmpty(s.OwnerPath))
                {
                    notes.Add($"face selecionada sem peça dona identificável ({(s.FaceId == 0 ? "sem ID" : "sem ocorrência")}) — ignorada.");
                    continue;
                }
                list.Add(s);
            }
            if (skipped > 0) notes.Add($"{skipped} item(ns) selecionado(s) não são faces — ignorados.");
            return list;
        }

        /// <summary>
        /// A região única das faces selecionadas, na montagem <paramref name="ctx"/>; <paramref name="burnBoxes"/> recebe as
        /// faces de queima (rosa e cor de Ra) da mesma peça (mm, montagem) para os avisos.
        /// </summary>
        private List<SmartRegion> SelectedRegions(dynamic app, AssemblyContext ctx, List<SelectedBurnFace> sel, SmartElectrodeOptions o,
            SmartElectrodeReport report, out List<(int id, BoundingBox box)> burnBoxes)
        {
            burnBoxes = new List<(int, BoundingBox)>();
            var regions = new List<SmartRegion>();
            string ownerPath = sel[0].OwnerPath;
            if (sel.Any(s => !string.Equals(s.OwnerPath, ownerPath, StringComparison.OrdinalIgnoreCase)))
                report.Notes.Add("as faces selecionadas são de mais de uma peça: uso só as da primeira (" + System.IO.Path.GetFileName(ownerPath) + ").");
            var mine = sel.Where(s => string.Equals(s.OwnerPath, ownerPath, StringComparison.OrdinalIgnoreCase)).ToList();

            OccurrenceInfo occ = null;
            foreach (OccurrenceInfo oc in ctx.GetOccurrences())
            {
                string f = null; try { f = (string)oc.ComOccurrence.OccurrenceFileName; } catch { }
                if (!string.Equals(f, ownerPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (occ == null || (mine[0].OccurrenceName != null && string.Equals(oc.Name, mine[0].OccurrenceName, StringComparison.OrdinalIgnoreCase))) occ = oc;
            }
            if (occ == null) { report.Notes.Add("a peça das faces selecionadas não está na montagem de erosão."); return regions; }
            OccurrenceTransform pose = AssemblyContext.TryGetPose(occ);
            dynamic doc = occ.OccurrenceDocument;

            // queima da peça (rosa / cor de Ra), para o Ra das selecionadas e os avisos
            var burnRa = new Dictionary<int, (BurnKind kind, double ra)>();
            foreach (SelectedFace sf in _faceSelector.EnumerateColoredFaces(doc, app))
            {
                try
                {
                    Color c = sf.DetectedColor;
                    int id = 0; try { id = (int)((dynamic)sf.ComFace).ID; } catch { }
                    BurnKind? kind = null; double ra = 0;
                    if (_raColorMap.TryGetRa(c, out double r, out Color _)) { kind = BurnKind.RaColor; ra = r; }
                    else if (OleColor.Matches(c, ErodedCavityColor, 8)) { kind = BurnKind.ErodedCavity; ra = o.ErodedCavityRa; }
                    if (kind == null || id == 0) continue;
                    burnRa[id] = (kind.Value, ra);
                    if (FaceGeometry.TryGetRangeMm(sf.ComFace, out double[] mn, out double[] mx))
                    {
                        if (pose != null) OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), mn, mx, out mn, out mx);
                        burnBoxes.Add((id, new BoundingBox { MinX = mn[0], MaxX = mx[0], MinY = mn[1], MaxY = mx[1], MinZ = mn[2], MaxZ = mx[2] }));
                    }
                }
                finally { ComLifetime.Release(sf.ComFace); }
            }

            var region = new SmartRegion { Owner = occ.Name, OwnerTag = occ };
            var ids = new HashSet<int>(mine.Select(s => s.FaceId));
            object body = TryGetSolidBody(doc);
            if (body == null) { report.Notes.Add("o corpo da peça das faces selecionadas não se lê."); return regions; }
            dynamic faces = ((dynamic)body).Faces(1);
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                object face = faces.Item(i);
                int id = 0; try { id = (int)((dynamic)face).ID; } catch { }
                if (!ids.Contains(id)) continue;
                if (!FaceGeometry.TryGetRangeMm(face, out double[] mn, out double[] mx)) { report.Notes.Add($"face {id}: caixa ilegível — fora do eletrodo."); continue; }
                if (pose != null) OccurrenceTransform.MapBoxMm(pose, OccurrenceTransform.Identity(pose.Layout), mn, mx, out mn, out mx);
                double area = 0; try { area = (double)((dynamic)face).Area * 1e6; } catch { }
                (BurnKind kind, double ra) k = burnRa.TryGetValue(id, out var kr) ? kr : (BurnKind.ErodedCavity, o.ErodedCavityRa);
                region.Faces.Add(new SmartFace { FaceId = id, Kind = k.kind, Ra = k.ra, AreaMm2 = area, MinMm = mn, MaxMm = mx, ComFace = face });
            }
            int missing = ids.Count - region.Faces.Select(f => f.FaceId).Distinct().Count();
            if (missing > 0) report.Notes.Add($"{missing} face(s) selecionada(s) não achada(s) no corpo da peça (ID mudou?).");
            report.Scan.Add($"{occ.Name}: {region.Faces.Count} face(s) SELECIONADA(S) → 1 região (modo semiautomático)");
            if (region.Faces.Count > 0) regions.Add(region);
            return regions;
        }

        /// <summary>Os avisos da seleção contra a pegada do eletrodo planejado.</summary>
        private static void SelectionWarnings(SmartElectrode e, SmartElectrodeOptions o, List<(int id, BoundingBox box)> burn, SmartElectrodeReport report)
        {
            BoundingBox fp = e.PlanFootprint(o.Plan.MarginMm);
            var selected = new HashSet<int>(e.Faces.Select(f => f.FaceId));
            var (inside, notBurn) = SelectionCheck.Warnings(fp, burn, selected);
            var inv = CultureInfo.InvariantCulture;
            if (inside.Count > 0)
            {
                var boxes = burn.Where(b => inside.Contains(b.id)).Select(b => string.Format(inv, "{0} (x {1:0.##}…{2:0.##}, y {3:0.##}…{4:0.##})", b.id, b.box.MinX, b.box.MaxX, b.box.MinY, b.box.MaxY)).Distinct();
                string msg = $"ATENÇÃO: {inside.Count} face(s) de queima NÃO selecionada(s) caem dentro da pegada — o eletrodo vai queimá-las também: " + string.Join("; ", boxes);
                report.Notes.Add(msg); e.Notes.Add(msg);
            }
            if (notBurn.Count > 0)
            {
                string msg = $"ATENÇÃO: {notBurn.Count} face(s) selecionada(s) não são de queima (nem rosa nem cor de Ra): IDs {string.Join(", ", notBurn)} — entram com Ra {o.ErodedCavityRa:0.0}.";
                report.Notes.Add(msg); e.Notes.Add(msg);
            }
            if (inside.Count == 0 && notBurn.Count == 0) report.Notes.Add("seleção conferida: nenhuma face de queima fora da seleção dentro da pegada; todas as selecionadas são de queima.");
        }
    }
}
