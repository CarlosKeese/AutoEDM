using System;
using System.Collections.Generic;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Model;
using AutoEDM.Modeling;
using AutoEDM.Selection;

namespace AutoEDM.Tools.UndoProbe
{
    /// <summary>
    /// Furos IGUAIS num recurso só, no SÍNCRONO (Carlos, 2026-11-09: "no síncrono, furos iguais ficam
    /// agrupados num recurso, e dá para separar um deles"). O Criar Base faz os 2 pinos Ø4 com um
    /// <c>Holes.AddSync(1, …)</c> cada. O dump diz <c>AddSync(NumberOfProfiles, ProfilesArray,
    /// ProfilePlaneSide, ExtentType, FiniteDepth, HoleData)</c> — aceita vários perfis?
    /// Peça DESCARTÁVEL síncrona, fechada sem salvar. <c>UndoProbe --furosync controle | sets | um-set</c>.
    /// Cuidado já documentado: o AddSync DESCONECTA o proxy da peça — cada leitura depois relê
    /// <c>app.ActiveDocument</c>.
    /// </summary>
    internal static class SyncHoleProbe
    {
        private const double TopMm = 40;
        private static readonly double[][] Pins = { new[] { -15.0, 0 }, new[] { 15.0, 0 } };

        public static bool Keep;

        public static int Run(string variant)
        {
            bool kept = false;
            Console.WriteLine($"===== furo síncrono: {variant} =====");
            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                dynamic original = null;
                try { original = app.ActiveDocument; } catch { }
                dynamic doc0 = app.Documents.Add("SolidEdge.PartDocument");
                try
                {
                    doc0.ModelingMode = 1;
                    PrimitiveModeler.Build(doc0, new List<Primitive> {
                        new Primitive { Kind = "caixa", Name = "placa", SizeXMm = 80, SizeYMm = 60, HeightMm = TopMm, PlaneIndex = 1 } });
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine($"ModelingMode = {Convert.ToString(doc0.ModelingMode)}; recursos antes: {Features(app)}");

                    switch (variant)
                    {
                        case "controle":
                            foreach (double[] c in Pins) AddSync(app, new[] { c }, separateSets: true);
                            break;
                        case "sets": AddSync(app, Pins, separateSets: true); break;
                        case "um-set": AddSync(app, Pins, separateSets: false); break;
                        default: Console.WriteLine("variante desconhecida"); return 3;
                    }
                    try { app.DoIdle(); } catch { }
                    Console.WriteLine($"furos Ø4 no corpo: {CountHoles(app)} de 2");
                    Console.WriteLine($"Model.Holes.Count = {Try(() => Convert.ToString(app.ActiveDocument.Models.Item(1).Holes.Count))}");
                    Console.WriteLine($"recursos depois: {Features(app)}");
                    dynamic holes = app.ActiveDocument.Models.Item(1).Holes;
                    for (int i = 1; i <= (int)holes.Count; i++)
                        Console.WriteLine($"  {Try(() => (string)holes.Item(i).Name)}: HoleData = {Try(() => (string)holes.Item(i).HoleData.Name)}");
                    if (Keep) { Console.WriteLine("peça MANTIDA aberta (sem salvar) para conferir o PathFinder."); kept = true; }
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
                finally
                {
                    if (!kept) { try { app.ActiveDocument.Close(false); } catch (Exception ex) { Console.WriteLine("Close: " + Msg(ex)); }
                    try { if (original != null) original.Activate(); } catch { } }
                }
            }
        }

        /// <summary>Um AddSync com os furos dados — receita do BlankModeler (plano no topo lado 2, furo lado 1, igFinite 13).</summary>
        private static void AddSync(dynamic app, double[][] centers, bool separateSets)
        {
            dynamic doc = app.ActiveDocument;                    // fresco: o AddSync anterior mata o proxy
            dynamic plane = doc.RefPlanes.AddParallelByDistance(doc.RefPlanes.Item(1), TopMm / 1000, 2, Type.Missing, Type.Missing, Type.Missing);
            object hdc = (object)doc.HoleDataCollection;
            var a = new object[21];
            for (int i = 0; i < a.Length; i++) a[i] = Type.Missing;
            a[0] = 33; a[1] = Units.MmToM(4); a[20] = true;
            object hd = hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, a);

            var profs = new List<SolidEdgePart.Profile>();
            dynamic shared = separateSets ? null : doc.ProfileSets.Add();
            foreach (double[] c in centers)
            {
                dynamic set = separateSets ? doc.ProfileSets.Add() : shared;
                dynamic prof;
                try { prof = set.Profiles.Add(plane); }
                catch (Exception ex) { Console.WriteLine($"  Profiles.Add ({profs.Count + 1}º, {(separateSets ? "set próprio" : "mesmo set")}): {Msg(ex)}"); return; }
                prof.Holes2d.Add(c[0] / 1000, c[1] / 1000);
                Console.WriteLine($"  perfil em X={c[0]}: End(1) = {Try(() => Convert.ToString(prof.End(1)))}");
                profs.Add((SolidEdgePart.Profile)(object)prof);
            }
            Console.WriteLine($"  AddSync({profs.Count}, Profile[], lado 1, igFinite, 9 mm, Ø4) …");
            Console.Out.Flush();
            object h = doc.Models.Item(1).Holes.AddSync(profs.Count, profs.ToArray(), 1, 13, (object)0.009, hd);
            Console.WriteLine($"  → {Try(() => Convert.ToString(((dynamic)h).Name))}, Status {Try(() => Convert.ToString(((dynamic)h).Status))}");
        }

        private static string CountHoles(dynamic app)
        {
            var hit = new HashSet<int>();
            try
            {
                dynamic faces = app.ActiveDocument.Models.Item(1).Body.Faces[1];
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    if (!FaceGeometry.TryGetRangeMm((object)faces.Item(i), out double[] mn, out double[] mx)) continue;
                    if (Math.Abs(mx[0] - mn[0] - 4) > 0.05 || Math.Abs(mx[1] - mn[1] - 4) > 0.05) continue;
                    for (int k = 0; k < Pins.Length; k++)
                        if (Math.Abs((mx[0] + mn[0]) / 2 - Pins[k][0]) < 0.05 && Math.Abs((mx[1] + mn[1]) / 2 - Pins[k][1]) < 0.05) hit.Add(k);
                }
            }
            catch (Exception ex) { return "corpo ilegível (" + Msg(ex) + ")"; }
            return hit.Count.ToString();
        }

        private static string Features(dynamic app)
        {
            var names = new List<string>();
            try
            {
                dynamic feats = app.ActiveDocument.DesignEdgebarFeatures;
                for (int i = 1; i <= (int)feats.Count; i++)
                {
                    dynamic f = feats.Item(i);
                    string n = Try(() => (string)f.Name);
                    if (n.StartsWith("RefPlane")) continue;
                    names.Add($"{n}[{Try(() => Convert.ToString(f.Type))}]");
                }
            }
            catch (Exception ex) { names.Add("ilegível: " + Msg(ex)); }
            return string.Join(", ", names);
        }

        private static string Try(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "?" + Msg(ex); } }
        private static string Msg(Exception ex) => (ex.InnerException ?? ex).GetBaseException().Message;
    }
}
