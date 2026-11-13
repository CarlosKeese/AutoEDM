using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Drawing
{
    public sealed class SheetMetalDrawingOptions
    {
        /// <summary>Template de desenho. Null/inexistente = o da casa (<see cref="HouseTemplates"/>).</summary>
        public string TemplatePath { get; set; }

        /// <summary>Cria o padrão plano quando a peça não tem (a peça fica alterada, sem salvar).</summary>
        public bool CreateFlatPatternIfMissing { get; set; } = true;
    }

    public sealed class SheetMetalDrawingResult
    {
        public bool Ok { get; set; }
        public object Document { get; set; }
        public double Scale { get; set; }
        public int Bends { get; set; }
        public int Created { get; set; }
        public bool FlatPatternCreated { get; set; }
        public double FlatLengthMm { get; set; }
        public double FlatWidthMm { get; set; }
        public List<string> Lines { get; } = new List<string>();
        public List<string> Failed { get; } = new List<string>();
        public string Summary { get; set; }
    }

    /// <summary>Uma aresta 2D de uma vista do desenho, em coordenadas da FOLHA (metros).</summary>
    public sealed class SheetLine
    {
        public object Com;
        public double X1, Y1, X2, Y2;
        public int EdgeType;
        public double BendAngle;
        public double MX => (X1 + X2) / 2;
        public double MY => (Y1 + Y2) / 2;
        public bool Vertical => Math.Abs(X1 - X2) < 1e-6;
        public bool Horizontal => Math.Abs(Y1 - Y2) < 1e-6;
        public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    /// <summary>Um par de cliques para <c>Dimensions.AddDistanceBetweenObjects</c>.</summary>
    public struct DimPick
    {
        public double AX, AY, BX, BY;
        public bool AKey, BKey;
    }

    /// <summary>
    /// Regras puras do desenho de chapa (testáveis sem a Solid Edge): escala e a ordem dos cliques
    /// de uma cota entre duas linhas paralelas.
    /// </summary>
    public static class SheetMetalDrawingRules
    {
        public static readonly double[] Scales = { 5, 2, 1, 0.5, 0.25, 0.2, 0.1 };

        /// <summary>
        /// A maior escala em que a planificada (deitada) cabe na metade de cima da folha e a peça dobrada
        /// na metade de baixo, com espaço para as cotas. Medidas em METROS.
        /// </summary>
        public static double ChooseScale(double flatLong, double flatShort, double foldLong, double foldMid,
                                         double sheetW, double sheetH)
        {
            foreach (double s in Scales)
                if (flatLong * s <= 0.50 * sheetW && flatShort * s <= 0.22 * sheetH &&
                    foldLong * s <= 0.28 * sheetW && foldMid * s <= 0.30 * sheetH)
                    return s;
            return Scales[Scales.Length - 1];
        }

        /// <summary>
        /// Cliques a tentar, em ordem, para cotar a distância entre duas linhas paralelas.
        /// A Solid Edge escolhe o EIXO da cota pela posição dos cliques (medido em 2026-11-12: a mesma linha
        /// de dobra deu o valor certo por um ponto e 25 mm por outro): linhas verticais clicadas na MESMA
        /// altura, horizontais na MESMA abscissa, no meio da sobreposição. Depois, linha até o PONTO FINAL
        /// da outra (keyPoint) — distância perpendicular à linha, logo no eixo certo. Nunca ponto com ponto
        /// (sairia inclinada). Quem chama aceita só o par cujo valor bate com o esperado.
        /// </summary>
        public static List<DimPick> PickOrder(SheetLine a, SheetLine b)
        {
            var list = new List<DimPick>();
            if (a.Vertical && b.Vertical)
            {
                double lo = Math.Max(Math.Min(a.Y1, a.Y2), Math.Min(b.Y1, b.Y2));
                double hi = Math.Min(Math.Max(a.Y1, a.Y2), Math.Max(b.Y1, b.Y2));
                double y = hi > lo ? (lo + hi) / 2 : a.MY;
                list.Add(new DimPick { AX = a.MX, AY = y, BX = b.MX, BY = y });
            }
            else if (a.Horizontal && b.Horizontal)
            {
                double lo = Math.Max(Math.Min(a.X1, a.X2), Math.Min(b.X1, b.X2));
                double hi = Math.Min(Math.Max(a.X1, a.X2), Math.Max(b.X1, b.X2));
                double x = hi > lo ? (lo + hi) / 2 : a.MX;
                list.Add(new DimPick { AX = x, AY = a.MY, BX = x, BY = b.MY });
            }
            list.Add(new DimPick { AX = a.MX, AY = a.MY, BX = b.X1, BY = b.Y1, BKey = true });
            list.Add(new DimPick { AX = a.MX, AY = a.MY, BX = b.X2, BY = b.Y2, BKey = true });
            list.Add(new DimPick { AX = a.X1, AY = a.Y1, AKey = true, BX = b.MX, BY = b.MY });
            list.Add(new DimPick { AX = a.X2, AY = a.Y2, AKey = true, BX = b.MX, BY = b.MY });
            return list;
        }

        /// <summary>Linhas paralelas à direção dada que se sobrepõem no eixo perpendicular?</summary>
        public static bool OverlapAcross(SheetLine a, SheetLine b)
        {
            if (a.Horizontal && b.Horizontal)
                return Math.Min(Math.Max(a.X1, a.X2), Math.Max(b.X1, b.X2)) > Math.Max(Math.Min(a.X1, a.X2), Math.Min(b.X1, b.X2));
            if (a.Vertical && b.Vertical)
                return Math.Min(Math.Max(a.Y1, a.Y2), Math.Max(b.Y1, b.Y2)) > Math.Max(Math.Min(a.Y1, a.Y2), Math.Min(b.Y1, b.Y2));
            return false;
        }
    }

    /// <summary>
    /// Desenho de chapa dobrada (botão "Des. 2D" › Chaparia, Carlos, 2026-11-12): planificada deitada e
    /// cotada (comprimento, largura e posição de cada dobra a partir da ponta), tabela de dobras em
    /// português (sequência, raio, ângulo, sentido), peça dobrada na vista que mostra as dobras de perfil
    /// (comprimento e largura) e na lateral (altura), isométrica, carimbo PanFlight com a escala certa.
    ///
    /// RECEITA COM (medida ao vivo no SE 2023 v223, 2026-11-12, peças 502.02 / 501.02 / 502.01 do SaltSpray):
    /// • Sem padrão plano a vista planificada não existe: <c>FlatPatternModels.Add(Models.Item(1))</c> →
    ///   <c>FlatPatterns.Add(aresta, face, vértice, 0)</c> (maior face plana, a aresta reta mais longa dela,
    ///   o vértice inicial dessa aresta) → <c>Update()</c>. O corpo plano bateu com o DXF ao centésimo.
    /// • <c>AddSheetMetalView(link, 1, escala, x, y, 2 = seSheetMetalFlatView)</c>; deitada com
    ///   <c>SetRotationAngle(π/2)</c> + <c>Update</c> + <c>SetOrigin</c>.
    /// • Linha de dobra = <c>DVLine2d</c> com <c>EdgeType = 6</c> e <c>BendAngle</c> preenchido; contorno = 1.
    /// • <c>DraftBendTables.Add(vista plana, "", 1, 1)</c>: cabeçalhos em inglês → <c>HeaderRowValue</c>;
    ///   coluna inútil → <c>Show = false</c>; <c>Update()</c>; <c>SetOrigin</c> (canto superior esquerdo).
    /// • Vista principal da dobrada = a orientação padrão (4, 1 ou 2) com mais <c>DVArcs2d</c>.
    /// • Cotas: ver <see cref="SheetMetalDrawingRules.PickOrder"/>; aceitas só com |valor − esperado| ≤ 0,02 mm
    ///   e posicionadas (TrackDistance a partir do ponto-chave 1; k4/k5 no alvo E alinhados) — a cota que
    ///   mede certo mas sai desenhada no eixo errado é apagada e tenta-se o próximo par.
    /// • Altura de perfil em U (abas que não se sobrepõem em X à base) não sai na principal
    ///   (<c>MeasurementAxis</c> não muda nada) — vai na lateral, onde topo e base se sobrepõem.
    /// O desenho fica aberto e SEM salvar, como no Des. 2D do torno.
    /// </summary>
    public static class SheetMetalDrawingBuilder
    {
        private const int ViewTop = 1, ViewRight = 2, ViewFront = 4, ViewIso = 9;
        private const int FlatView = 2, DesignedView = 0;
        private const int FoldRight = 3;
        private const int EdgeBend = 6;
        private const double CheckTolM = 0.00002;

        public static SheetMetalDrawingResult Create(object app, object psm, SheetMetalDrawingOptions options = null)
        {
            options = options ?? new SheetMetalDrawingOptions();
            var r = new SheetMetalDrawingResult();
            dynamic seApp = app;
            dynamic part = psm;
            string path = (string)part.FullName;

            // --- padrão plano
            dynamic fpms = part.FlatPatternModels;
            if ((int)fpms.Count == 0)
            {
                if (!options.CreateFlatPatternIfMissing) { r.Summary = "A peça não tem padrão plano."; return r; }
                string err = CreateFlatPattern(part);
                if (err != null) { r.Summary = "Não consegui criar o padrão plano: " + err; return r; }
                r.FlatPatternCreated = true;
            }
            double[] flatMin, flatMax, foldMin, foldMax;
            if (!FaceGeometry.TryGetBodyRangeMm((object)part.FlatPatternModels.Item(1).Body, out flatMin, out flatMax) ||
                !FaceGeometry.TryGetBodyRangeMm((object)part.Models.Item(1).Body, out foldMin, out foldMax))
            { r.Summary = "Não consegui medir a peça (envelope do corpo)."; return r; }
            double[] flat = Enumerable.Range(0, 3).Select(i => (flatMax[i] - flatMin[i]) / 1000).OrderByDescending(v => v).ToArray();
            double[] fold = Enumerable.Range(0, 3).Select(i => (foldMax[i] - foldMin[i]) / 1000).OrderByDescending(v => v).ToArray();

            // --- documento
            dynamic draft = File.Exists(options.TemplatePath)
                ? seApp.Documents.Add("SolidEdge.DraftDocument", options.TemplatePath)
                : HouseTemplates.Add((object)seApp, HouseDocumentKind.Draft);
            r.Document = (object)draft;
            dynamic sheet = draft.ActiveSheet;
            double W = (double)sheet.SheetSetup.SheetWidth, H = (double)sheet.SheetSetup.SheetHeight;
            double scale = SheetMetalDrawingRules.ChooseScale(flat[0], flat[1], fold[0], fold[1], W, H);
            r.Scale = scale;
            dynamic link = draft.ModelLinks.Add(path);
            dynamic views = sheet.DrawingViews;
            var dims = new DimWriter((object)sheet, r);

            // --- planificada, deitada
            double fx = 0.31 * W, fy = 0.72 * H;
            dynamic fv = views.AddSheetMetalView(link, ViewTop, scale, fx, fy, FlatView);
            List<SheetLine> ls = Lines((object)fv);
            Box e = Box.Of(ls);
            if (e.H > e.W)
            {
                fv.SetRotationAngle(Math.PI / 2); fv.Update(); fv.SetOrigin(fx, fy);
                ls = Lines((object)fv); e = Box.Of(ls);
            }
            List<SheetLine> outline = ls.Where(l => l.EdgeType != EdgeBend).ToList();
            List<SheetLine> bends = ls.Where(l => l.EdgeType == EdgeBend).OrderBy(l => l.MX).ThenBy(l => l.MY).ToList();
            r.Bends = bends.Count;
            r.FlatLengthMm = e.W / scale * 1000; r.FlatWidthMm = e.H / scale * 1000;
            Extremes x = Extremes.Of(outline, e);
            if (x.Left != null && x.Right != null)
                dims.Between(x.Left, x.Right, e.W / scale, Side.Above, e.Y1 + 0.010 + 0.007 * bends.Count, "planificada: comprimento");
            if (x.Bottom != null && x.Top != null)
                dims.Between(x.Bottom, x.Top, e.H / scale, Side.Left, e.X0 - 0.010, "planificada: largura");
            int k = 0;
            foreach (SheetLine b in bends)
            {
                if (b.Vertical && x.Left != null)
                { dims.Between(x.Left, b, (b.MX - e.X0) / scale, Side.Above, e.Y1 + 0.010 + 0.007 * k, $"dobra a {Mm((b.MX - e.X0) / scale)}"); k++; }
                else if (b.Horizontal && x.Bottom != null)
                { dims.Between(x.Bottom, b, (b.MY - e.Y0) / scale, Side.Right, e.X1 + 0.010 + 0.007 * k, $"dobra a {Mm((b.MY - e.Y0) / scale)}"); k++; }
                else r.Failed.Add("dobra inclinada na planificada — cotar à mão");
            }

            // --- tabela de dobras, no canto superior direito
            try
            {
                dynamic bt = draft.DraftBendTables.Add(fv, "", 1, 1);
                var names = new Dictionary<string, string>
                { { "Sequence", "SEQ." }, { "Feature", null }, { "Radius", "RAIO" }, { "Angle", "ÂNGULO" }, { "Direction", "SENTIDO" }, { "Included Angle", null } };
                for (int c = 1; c <= (int)bt.Columns.Count; c++)
                {
                    dynamic col = bt.Columns.Item(c);
                    string hdr = (string)col.Header;
                    string pt;
                    if (hdr != null && names.TryGetValue(hdr, out pt)) { if (pt != null) col.HeaderRowValue = pt; else col.Show = false; }
                }
                bt.Update();
                bt.SetOrigin(0.68 * W, 0.86 * H);
            }
            catch (Exception ex) { r.Failed.Add("tabela de dobras: " + Msg(ex)); }

            // --- dobrada: a orientação que mostra as dobras de perfil
            double my = 0.33 * H;
            dynamic main = null; int bestArcs = -1; int bestOrient = 0;
            foreach (int o in new[] { ViewFront, ViewTop, ViewRight })
            {
                dynamic v = views.AddSheetMetalView(link, o, scale, 0.22 * W, my, DesignedView);
                int arcs = 0; try { arcs = (int)v.DVArcs2d.Count; } catch { }
                if (arcs > bestArcs) { if (main != null) main.Delete(); main = v; bestArcs = arcs; bestOrient = o; }
                else v.Delete();
            }
            List<SheetLine> ml = Lines((object)main); Box me = Box.Of(ml); Extremes mx = Extremes.Of(ml, me);
            if (mx.Left != null && mx.Right != null)
                dims.Between(mx.Left, mx.Right, me.W / scale, Side.Below, me.Y0 - 0.010, "dobrada: comprimento");
            bool heightDone = mx.Bottom != null && mx.Top != null && SheetMetalDrawingRules.OverlapAcross(mx.Bottom, mx.Top) &&
                              dims.Between(mx.Bottom, mx.Top, me.H / scale, Side.Left, me.X0 - 0.010, "dobrada: altura");
            try
            {
                dynamic side = views.AddByFold(main, FoldRight, me.X1 + 0.035 + fold[2] * scale / 2, my);
                List<SheetLine> sl = Lines((object)side); Box se = Box.Of(sl); Extremes sx = Extremes.Of(sl, se);
                if (sx.Left != null && sx.Right != null)
                    dims.Between(sx.Left, sx.Right, se.W / scale, Side.Below, se.Y0 - 0.010, "dobrada: largura");
                if (!heightDone && sx.Bottom != null && sx.Top != null)
                    dims.Between(sx.Bottom, sx.Top, se.H / scale, Side.Right, se.X1 + 0.010, "dobrada: altura (lateral)");
            }
            catch (Exception ex) { r.Failed.Add("vista lateral: " + Msg(ex)); }
            try { views.AddSheetMetalView(link, ViewIso, scale, 0.80 * W, 0.50 * H, DesignedView); }
            catch (Exception ex) { r.Failed.Add("isométrica: " + Msg(ex)); }

            // --- carimbo
            try { draft.UpdatePropertyTextCacheAndDisplay(); } catch { }
            TurnedDrawingBuilder.SetScaleText((object)sheet, scale);

            r.Ok = true;
            r.Summary = Summarize(r, bestOrient);
            Log.Info(r.Summary);
            return r;
        }

        /// <summary>Padrão plano pela maior face plana; devolve null se deu certo, ou o erro.</summary>
        internal static string CreateFlatPattern(dynamic part)
        {
            try
            {
                dynamic body = part.Models.Item(1).Body;
                dynamic faces = body.Faces(6);                 // igQueryPlane
                dynamic best = null; double area = 0;
                for (int i = 1; i <= (int)faces.Count; i++) { dynamic f = faces.Item(i); double a = (double)f.Area; if (a > area) { area = a; best = f; } }
                if (best == null) return "nenhuma face plana";
                dynamic edge = null; double len = 0;
                dynamic edges = best.Edges;
                for (int i = 1; i <= (int)edges.Count; i++)
                {
                    dynamic ed = edges.Item(i);
                    double[] p0 = Point((object)ed.StartVertex), p1 = Point((object)ed.EndVertex);
                    if (p0 == null || p1 == null) continue;
                    if (!IsLine((object)ed)) continue;
                    double l = Math.Sqrt(Enumerable.Range(0, 3).Sum(k => (p1[k] - p0[k]) * (p1[k] - p0[k])));
                    if (l > len) { len = l; edge = ed; }
                }
                if (edge == null) return "a maior face plana não tem aresta reta";
                dynamic fpm = part.FlatPatternModels.Add(part.Models.Item(1));
                fpm.FlatPatterns.Add(edge, best, edge.StartVertex, 0);
                fpm.Update();
                Log.Info($"Chaparia: padrão plano criado (face de {area * 1e6:0} mm², aresta de {len * 1000:0.#} mm).");
                return null;
            }
            catch (Exception ex) { return Msg(ex); }
        }

        private const int IgLine = 167551109;   // Edge.Geometry.Type de reta (mesmo valor do SharpCornerProbe)

        private static bool IsLine(object edge)
        {
            try { return (int)((dynamic)edge).Geometry.Type == IgLine; }
            catch { return false; }
        }

        private static double[] Point(object vertex)
        {
            try
            {
                object[] a = TurnedDrawingBuilder.Call(vertex, "GetPointData", new object[] { new double[3] }, 0);
                var arr = a[0] as Array;
                return arr == null ? null : new[] { Convert.ToDouble(arr.GetValue(0)), Convert.ToDouble(arr.GetValue(1)), Convert.ToDouble(arr.GetValue(2)) };
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ geometria das vistas

        internal static List<SheetLine> Lines(object view)
        {
            var list = new List<SheetLine>();
            dynamic v = view;
            dynamic ls = v.DVLines2d;
            int n = (int)ls.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic l = ls.Item(i);
                object[] s = TurnedDrawingBuilder.Call((object)l, "GetStartPoint", new object[] { 0.0, 0.0 }, 0, 1);
                object[] t = TurnedDrawingBuilder.Call((object)l, "GetEndPoint", new object[] { 0.0, 0.0 }, 0, 1);
                double[] a = ToSheet(view, Convert.ToDouble(s[0]), Convert.ToDouble(s[1]));
                double[] b = ToSheet(view, Convert.ToDouble(t[0]), Convert.ToDouble(t[1]));
                int edge = 0; try { edge = (int)l.EdgeType; } catch { }
                double ang = 0; try { ang = (double)l.BendAngle; } catch { }
                list.Add(new SheetLine { Com = (object)l, X1 = a[0], Y1 = a[1], X2 = b[0], Y2 = b[1], EdgeType = edge, BendAngle = ang });
            }
            return list;
        }

        private static double[] ToSheet(object view, double x, double y)
        {
            object[] r = TurnedDrawingBuilder.Call(view, "ViewToSheet", new object[] { x, y, 0.0, 0.0 }, 2, 3);
            return new[] { Convert.ToDouble(r[2]), Convert.ToDouble(r[3]) };
        }

        internal struct Box
        {
            public double X0, X1, Y0, Y1;
            public double W => X1 - X0;
            public double H => Y1 - Y0;
            public static Box Of(List<SheetLine> ls) => new Box
            {
                X0 = ls.Min(l => Math.Min(l.X1, l.X2)), X1 = ls.Max(l => Math.Max(l.X1, l.X2)),
                Y0 = ls.Min(l => Math.Min(l.Y1, l.Y2)), Y1 = ls.Max(l => Math.Max(l.Y1, l.Y2)),
            };
        }

        /// <summary>As linhas de contorno nas quatro bordas da vista (a mais longa de cada lado).</summary>
        internal sealed class Extremes
        {
            public SheetLine Left, Right, Bottom, Top;
            public static Extremes Of(List<SheetLine> ls, Box e)
            {
                const double tol = 1e-5;
                return new Extremes
                {
                    Left = ls.Where(l => l.Vertical && Math.Abs(l.MX - e.X0) < tol).OrderByDescending(l => l.Length).FirstOrDefault(),
                    Right = ls.Where(l => l.Vertical && Math.Abs(l.MX - e.X1) < tol).OrderByDescending(l => l.Length).FirstOrDefault(),
                    Bottom = ls.Where(l => l.Horizontal && Math.Abs(l.MY - e.Y0) < tol).OrderByDescending(l => l.Length).FirstOrDefault(),
                    Top = ls.Where(l => l.Horizontal && Math.Abs(l.MY - e.Y1) < tol).OrderByDescending(l => l.Length).FirstOrDefault(),
                };
            }
        }

        internal enum Side { Above, Below, Left, Right }

        /// <summary>Cria a cota, confere o valor e o posicionamento; o que não bate é apagado e relatado.</summary>
        private sealed class DimWriter
        {
            private readonly dynamic _sheet;
            private readonly SheetMetalDrawingResult _r;
            public DimWriter(object sheet, SheetMetalDrawingResult r) { _sheet = sheet; _r = r; }

            public bool Between(SheetLine a, SheetLine b, double expectedM, Side side, double target, string name)
            {
                var seen = new List<string>();
                foreach (DimPick p in SheetMetalDrawingRules.PickOrder(a, b))
                {
                    dynamic d;
                    try
                    {
                        d = _sheet.Dimensions.AddDistanceBetweenObjects(((dynamic)a.Com).Reference, p.AX, p.AY, 0.0, p.AKey,
                                                                        ((dynamic)b.Com).Reference, p.BX, p.BY, 0.0, p.BKey);
                    }
                    catch { continue; }
                    double v = 0; try { v = (double)d.Value; } catch { }
                    if (Math.Abs(v - expectedM) > CheckTolM) { seen.Add(Mm(v)); TryDelete(d); continue; }
                    if (!Place(d, side, target)) { seen.Add(Mm(v) + "(posição)"); TryDelete(d); continue; }
                    _r.Created++;
                    _r.Lines.Add($"{name} = {Mm(v)}");
                    return true;
                }
                _r.Failed.Add($"{name}: esperado {Mm(expectedM)}, mediu {(seen.Count == 0 ? "nada" : string.Join("/", seen))} — cotar à mão");
                return false;
            }

            private static bool Place(dynamic d, Side side, double target)
            {
                bool horiz = side == Side.Above || side == Side.Below;
                foreach (int sign in new[] { 1, -1 })
                {
                    double[] k1 = Key(d, 1);
                    d.TrackDistance = sign * (horiz ? target - k1[1] : target - k1[0]);
                    double[] k4 = Key(d, 4), k5 = Key(d, 5);
                    bool ok = horiz
                        ? Math.Abs(k4[1] - target) < 0.002 && Math.Abs(k4[1] - k5[1]) < 1e-5
                        : Math.Abs(k4[0] - target) < 0.002 && Math.Abs(k4[0] - k5[0]) < 1e-5;
                    if (ok) return true;
                }
                return false;
            }

            private static double[] Key(dynamic d, int i)
            {
                object[] a = TurnedDrawingBuilder.Call((object)d, "GetKeyPoint", new object[] { i, 0.0, 0.0, 0.0, 0, 0 }, 1, 2, 3, 4, 5);
                return new[] { Convert.ToDouble(a[1]), Convert.ToDouble(a[2]) };
            }

            private static void TryDelete(dynamic d) { try { d.Delete(); } catch { } }
        }

        private static string Summarize(SheetMetalDrawingResult r, int orientation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Desenho de chapa criado: escala {TurnedDrawingBuilder.ScaleText(r.Scale)}, planificada {Mm(r.FlatLengthMm / 1000)} × {Mm(r.FlatWidthMm / 1000)} mm, " +
                          $"{r.Bends} dobra(s), {r.Created} cota(s) conferida(s).");
            if (r.FlatPatternCreated) sb.AppendLine("A peça não tinha padrão plano: ele foi criado e a peça ficou ALTERADA, sem salvar.");
            foreach (string l in r.Lines) sb.AppendLine("  • " + l);
            if (r.Failed.Count > 0)
            {
                sb.AppendLine($"Não saíram ({r.Failed.Count}):");
                foreach (string f in r.Failed) sb.AppendLine("  • " + f);
            }
            return sb.ToString();
        }

        private static string Mm(double meters) => (meters * 1000).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        private static string Msg(Exception ex) => (ex.InnerException ?? ex).Message;
    }
}
