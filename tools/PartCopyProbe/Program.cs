using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AutoEDM.Com;
using AutoEDM.Modeling;
using AutoEDM.Selection;

namespace AutoEDM.Tools.PartCopyProbe
{
    /// <summary>
    /// Sonda da CÓPIA DE PEÇA vinculada (Desenho de processo, Carlos 2026-10-22). Só arquivos
    /// descartáveis na pasta temporária: A (cilindro) → B ordenada com <c>Models.AddCopiedPart(A)</c>
    /// → A alterada e salva → B reaberta: o vínculo trouxe a alteração?
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            if (Array.IndexOf(args, "--processo") >= 0) return Process();
            string dir = Path.Combine(Path.GetTempPath(), "AutoEDM_PartCopyProbe_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);
            string a = Path.Combine(dir, "A.par"), b = Path.Combine(dir, "B_CDU.par");
            Console.WriteLine("pasta: " + dir);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                try
                {
                    // A: cilindro Ø20 × 10.
                    dynamic pa = app.Documents.Add("SolidEdge.PartDocument");
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "a", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 } });
                    pa.SaveAs(a); pa.Close();

                    // B: ordenada, cópia de A.
                    dynamic pb = app.Documents.Add("SolidEdge.PartDocument");
                    try { pb.ModelingMode = 2; } catch (Exception ex) { Console.WriteLine("ModelingMode=2: " + Msg(ex)); }
                    Console.WriteLine($"B ModelingMode = {(int)pb.ModelingMode}");
                    object model = pb.Models.AddCopiedPart(a);
                    Console.WriteLine($"AddCopiedPart: {(model == null ? "null" : "Model")}; Models.Count = {(int)pb.Models.Count}");
                    dynamic cp = pb.Models.Item(1).CopiedParts.Item(1);
                    Console.WriteLine($"CopiedPart: Status {(uint)(int)cp.Status}; FileName {(string)cp.FileName}; IsUpToDate {Try(() => cp.IsUpToDate)}; LinkMaterial {Try(() => cp.LinkMaterial)}");
                    Console.WriteLine("B corpo: " + Range(pb));
                    pb.SaveAs(b); pb.Close();

                    // A alterada: cilindro Ø10 × 5 em cima.
                    pa = app.Documents.Open(a);
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "topo", DiameterMm = 10, HeightMm = 5, PlaneIndex = 1, LiftMm = 10 } });
                    Console.WriteLine("A depois da alteração: " + Range(pa));
                    pa.Save(); pa.Close();

                    // B reaberta.
                    pb = app.Documents.Open(b);
                    cp = pb.Models.Item(1).CopiedParts.Item(1);
                    Console.WriteLine($"B reaberta: {Range(pb)}; IsUpToDate {Try(() => cp.IsUpToDate)}");
                    try { cp.Update(); Console.WriteLine("Update(): OK"); } catch (Exception ex) { Console.WriteLine("Update(): " + Msg(ex)); }
                    Console.WriteLine($"B depois do Update: {Range(pb)}; IsUpToDate {Try(() => cp.IsUpToDate)}");
                    pb.Close(false);
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            }
        }

        /// <summary>
        /// O ProcessDrawingBuilder de verdade num molde FALSO na pasta temporária: 1ª rodada só CDU
        /// (cria _CDU + _WP.asm), 2ª rodada CDU+CDT (cria só _CDT e acrescenta na montagem existente).
        /// </summary>
        private static int Process()
        {
            string root = Path.Combine(Path.GetTempPath(), "AutoEDM_ProcessProbe_" + DateTime.Now.ToString("HHmmss"), "MD-TESTE");
            Directory.CreateDirectory(Path.Combine(root, "02 - PROCESSO"));
            string a = Path.Combine(root, "99999.101.par");
            Console.WriteLine("molde falso: " + root);

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                try
                {
                    dynamic pa = app.Documents.Add("SolidEdge.PartDocument");
                    PrimitiveModeler.Build(pa, new List<Primitive> { new Primitive { Kind = "cilindro", Name = "a", DiameterMm = 20, HeightMm = 10, PlaneIndex = 1 } });
                    pa.SaveAs(a); pa.Close();

                    foreach (var m in new[] { AutoEDM.Mold.ProcessMachine.Cdu, AutoEDM.Mold.ProcessMachine.Cdu | AutoEDM.Mold.ProcessMachine.Cdt })
                    {
                        var plan = AutoEDM.Mold.ProcessDrawingBuilder.Plan(a, m);
                        Console.WriteLine($"--- {m}: criar [{string.Join(", ", System.Linq.Enumerable.Select(plan.ToCreate, c => Path.GetFileName(c.Path)))}], " +
                                          $"montagem {(plan.AssemblyExists ? "EXISTE" : "nova")}, Ready {plan.Ready}");
                        bool ok = AutoEDM.Mold.ProcessDrawingBuilder.Build((object)app, plan, out string err);
                        Console.WriteLine(ok ? "Build OK" : "Build FALHOU: " + err);
                        dynamic asm = app.ActiveDocument;
                        dynamic occs = asm.Occurrences;
                        for (int i = 1; i <= (int)occs.Count; i++)
                            Console.WriteLine($"  ocorrência: {Path.GetFileName((string)occs.Item(i).OccurrenceFileName)}");
                        asm.Close(false);
                    }

                    foreach (string suffix in new[] { "_CDU", "_CDT" })
                    {
                        dynamic p = app.Documents.Open(Path.Combine(root, "02 - PROCESSO", "99999.101", "99999.101" + suffix + ".par"));
                        dynamic cp = p.Models.Item(1).CopiedParts.Item(1);
                        Console.WriteLine($"{suffix}: ModelingMode {(int)p.ModelingMode}; Status {(uint)(int)cp.Status}; vinculada a {Path.GetFileName((string)cp.FileName)}; " +
                                          $"LinkMaterial {Try(() => cp.LinkMaterial)}; corpo {Range(p)}");
                        p.Close(false);
                    }
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("FALHOU: " + Msg(ex)); return 8; }
            }
        }

        private static string Range(dynamic doc)
        {
            try
            {
                object body = doc.Models.Item(1).Body;
                return FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)
                    ? $"X {mn[0]:0.##}..{mx[0]:0.##}  Y {mn[1]:0.##}..{mx[1]:0.##}  Z {mn[2]:0.##}..{mx[2]:0.##}" : "sem caixa";
            }
            catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Try(Func<object> f)
        {
            try { return Convert.ToString(f(), CultureInfo.InvariantCulture); } catch (Exception ex) { return "? " + Msg(ex); }
        }

        private static string Msg(Exception ex) => ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
    }
}
