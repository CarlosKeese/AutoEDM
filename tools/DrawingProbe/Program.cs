using System;
using System.Globalization;
using System.Linq;
using AutoEDM.Com;
using AutoEDM.Drawing;

namespace AutoEDM.Tools.DrawingProbe
{
    /// <summary>
    /// Sonda externa do "Desenho 2D automático". Por padrão SÓ LÊ: descreve a peça torneada e o
    /// plano de cotas. Com <c>--gerar</c> cria um desenho NOVO, sem salvar, a partir da peça —
    /// nunca mexe no desenho nem na peça que já existem.
    ///
    /// Aceita como documento ativo a PEÇA ou um DESENHO dela (usa o 1º ModelLink), para poder
    /// comparar com o desenho feito à mão sem trocar de janela.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            bool generate = args.Any(a => a.Equals("--gerar", StringComparison.OrdinalIgnoreCase));

            using (var connector = new SolidEdgeConnector())
            {
                dynamic app = connector.Connect(startIfNotRunning: false, makeVisible: false);
                if (app == null) { Console.WriteLine("Nenhuma Solid Edge aberta."); return 1; }
                // --peca=NOME escolhe um documento ABERTO pelo nome (sem mexer na janela ativa).
                string wanted = args.Where(a => a.StartsWith("--peca=", StringComparison.OrdinalIgnoreCase))
                                    .Select(a => a.Substring(7)).FirstOrDefault();
                dynamic doc = null;
                string testPlane = args.Where(a => a.StartsWith("--peca-teste=", StringComparison.OrdinalIgnoreCase))
                                       .Select(a => a.Substring(13)).FirstOrDefault();
                if (testPlane != null)
                {
                    doc = BuildTestPart(app, int.Parse(testPlane));
                    if (doc == null) return 5;
                }
                else if (wanted != null)
                {
                    foreach (dynamic d in app.Documents)
                        if (((string)d.Name).IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 && (int)d.Type == 1) doc = d;
                }
                else doc = connector.GetActiveDocument();
                if (doc == null) { Console.WriteLine("Documento não encontrado."); return 1; }

                int type = (int)doc.Type;
                dynamic part = type == 2 ? doc.ModelLinks.Item(1).ModelDocument : doc;
                Console.WriteLine($"Peça: {(string)part.FullName}");

                TurnedProfile profile = TurnedProfileReader.Read((object)part, new FinishCatalog());
                if (!profile.IsTurned) { Console.WriteLine("Não é peça torneada."); return 3; }

                TurnedDimensionPlan plan = TurnedDimensionPlan.Build(profile);
                Console.WriteLine();
                Console.WriteLine(TurnedPlanFormatter.Describe(profile, plan));

                if (!generate) return 0;

                bool keep = args.Any(a => a.Equals("--manter", StringComparison.OrdinalIgnoreCase));
                string pdf = args.Where(a => a.StartsWith("--pdf=", StringComparison.OrdinalIgnoreCase))
                                 .Select(a => a.Substring(6)).FirstOrDefault();

                TurnedDrawingResult result;
                try { result = TurnedDrawingBuilder.Create((object)app, (object)part, profile, plan, new TurnedDrawingOptions()); }
                catch (Exception ex) { Console.WriteLine("ERRO ao gerar: " + ex); return 4; }
                Console.WriteLine();
                Console.WriteLine(result.Summary);

                dynamic draft = result.Document;
                if (pdf != null)
                {
                    try { draft.SaveAs(pdf); Console.WriteLine("PDF: " + pdf); }
                    catch (Exception ex) { Console.WriteLine("PDF falhou: " + ex.GetBaseException().Message); }
                }
                // Sonda não deixa documento órfão: o desenho de teste fecha SEM salvar.
                if (!keep) { try { draft.Close(false); } catch { } }
                if (testPlane != null && !keep) { try { part.Close(false); } catch { } }
                return result.Ok ? 0 : 4;
            }
        }

        /// <summary>
        /// Peça de TESTE (nova, salva só na pasta temporária): eixo escalonado no RefPlane
        /// <paramref name="planeIndex"/> — então o eixo da peça é a normal desse plano — com o centro
        /// fora da origem, e as faces pintadas com os estilos da tabela. Serve para provar o desenho
        /// em orientação que as peças reais não têm.
        /// </summary>
        private static dynamic BuildTestPart(dynamic app, int planeIndex)
        {
            dynamic part = app.Documents.Add("SolidEdge.PartDocument");
            try { if ((int)part.ModelingMode != 1) part.ModelingMode = 1; } catch { }
            var prims = new System.Collections.Generic.List<AutoEDM.Modeling.Primitive>
            {
                new AutoEDM.Modeling.Primitive { Kind = "cilindro", Name = "flange", DiameterMm = 30, HeightMm = 5,  PlaneIndex = planeIndex, LiftMm = 0,  CenterXMm = 30, CenterYMm = -20 },
                new AutoEDM.Modeling.Primitive { Kind = "cilindro", Name = "corpo",  DiameterMm = 20, HeightMm = 40, PlaneIndex = planeIndex, LiftMm = 5,  CenterXMm = 30, CenterYMm = -20 },
                new AutoEDM.Modeling.Primitive { Kind = "cilindro", Name = "ponta",  DiameterMm = 12, HeightMm = 15, PlaneIndex = planeIndex, LiftMm = 45, CenterXMm = 30, CenterYMm = -20 },
            };
            var built = AutoEDM.Modeling.PrimitiveModeler.Build(part, prims);
            Console.WriteLine("Peça de teste: " + built.Message);
            if (!built.Ok) return null;

            TurnedProfile p = TurnedProfileReader.Read((object)part, new FinishCatalog());
            Console.WriteLine($"Eixo da peça de teste: ({string.Join("; ", p.AxisDir.Select(v => v.ToString("0.###")))})");
            Paint(part, p.Faces.Where(f => f.Kind == RevolvedKind.Cylinder && Math.Abs(f.R0 - 10) < 0.01), "11 - MED CALIB (F H7 / E g6)");
            Paint(part, p.Faces.Where(f => f.Kind == RevolvedKind.Cylinder && Math.Abs(f.R0 - 6) < 0.01), "16 - USI. CAVIDADES (±0,005)");
            Paint(part, p.Faces.Where(f => f.Kind == RevolvedKind.Plane && Math.Abs(f.R1 - 15) < 0.01 && Math.Abs(f.R0) < 0.01), "10 - FECHAM. (F -0,01 / E +0,01)");
            Paint(part, p.Faces.Where(f => f.Kind == RevolvedKind.Plane && Math.Abs(f.R0 - 10) < 0.01), "20 - VEDAÇÃO (±0,1)");

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude", "u--Code-AutoEDM",
                "f1d6bd47-5bee-4af4-b7eb-39eecfdbf7e3", "scratchpad", $"teste_plano{planeIndex}.par");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            part.SaveAs(path);
            Console.WriteLine("Peça de teste salva: " + path);
            return part;
        }

        private static void Paint(dynamic part, System.Collections.Generic.IEnumerable<RevolvedFace> faces, string style)
        {
            var ids = new System.Collections.Generic.HashSet<int>(faces.Select(f => f.Id));
            var list = new System.Collections.Generic.List<object>();
            dynamic all = part.Models.Item(1).Body.Faces[1];
            for (int i = 1; i <= (int)all.Count; i++) { dynamic f = all.Item(i); if (ids.Contains((int)f.ID)) list.Add((object)f); }
            string got = AutoEDM.Electrode.FaceColorPainter.PaintWithNamedStyle((object)part, list, new[] { style }, style, System.Drawing.Color.Gray);
            Console.WriteLine($"  pintura {style}: {list.Count} face(s) -> {got}");
        }
    }
}
