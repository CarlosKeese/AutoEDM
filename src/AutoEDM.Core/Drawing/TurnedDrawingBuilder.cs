using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Drawing
{
    public sealed class TurnedDrawingOptions
    {
        /// <summary>Template de desenho. Null/inexistente = o da casa (<see cref="HouseTemplates"/>).</summary>
        public string TemplatePath { get; set; }

        /// <summary>Escalas aceitas, da maior para a menor (ISO 5455).</summary>
        public double[] Scales { get; set; } = { 10, 5, 2, 1, 0.5, 0.2, 0.1 };

        /// <summary>Fração da folha que a vista lateral pode ocupar (largura, altura).</summary>
        public double MaxWidthFraction { get; set; } = 0.45;
        public double MaxHeightFraction { get; set; } = 0.30;

        /// <summary>Diferença aceita entre a cota criada e o valor que o 3D manda (mm).</summary>
        public double CheckToleranceMm { get; set; } = 0.01;
    }

    public sealed class TurnedDrawingResult
    {
        public bool Ok { get; set; }
        public object Document { get; set; }
        public double Scale { get; set; }
        public int Created { get; set; }
        public List<string> Failed { get; } = new List<string>();
        public string Summary { get; set; }
    }

    /// <summary>
    /// Monta o desenho 2D de uma peça torneada a partir do <see cref="TurnedDimensionPlan"/>:
    /// documento novo do template da casa, vista lateral com o eixo na horizontal, vista de topo
    /// por rebatimento e isométrica, e as cotas com a tolerância da cor.
    ///
    /// RECEITA COM (descoberta ao vivo no SE 2023, 2026-10-19 — cada item custou uma rodada):
    /// • A cota se prende à <c>Reference</c> da aresta da vista (<c>DVLine2d.Reference</c>), NÃO
    ///   ao DVLine2d: com o objeto cru a Solid Edge lê as coordenadas LOCAIS da vista como se
    ///   fossem da folha, e a cota vai parar no canto inferior esquerdo com valor sem sentido.
    ///   Os pontos de pega são em coordenadas da FOLHA (metros) = origem da vista + escala × local.
    /// • <c>DVLine2d.GetStartPoint/GetEndPoint</c> e <c>DrawingView.ModelToView</c> falam o mesmo
    ///   espaço LOCAL da vista (metros do modelo, sem escala) — é por eles que cada estação/Ø do
    ///   plano acha a sua aresta 2D, pela geometria.
    /// • <c>DrawingView.SetViewOrientation(vetor de vista, eixo X local)</c> põe o eixo da peça na
    ///   horizontal qualquer que seja o eixo no modelo.
    /// • Cota coordenada: <c>AddCoordinateOriginEx(ref, pega, texto)</c> — o 3º ponto (texto da
    ///   origem) ACIMA da vista faz a medida correr ao longo do eixo; ao lado dela mediria na
    ///   vertical. As demais: <c>AddCoordinateEx(origem, ref, pega, texto)</c>.
    /// • Ø simétrico: <c>AddSymmetricalDiameter(linhaDeCentro, ..., ref)</c> nasce medindo na
    ///   direção errada; inverter <c>MeasurementAxisDirection</c> dá o Ø. A linha de centro sai
    ///   de <c>CenterLines.Add(pontos)</c> — o <c>AddBetweenTwoLines</c> recusa (E_FAIL).
    /// • Ângulo entre as geratrizes do cone nasce suplementar (180,5°); inverter
    ///   <c>MeasureLineDirection</c> dá o incluído (0,5°).
    /// • Chanfro: <c>AddChamfer(linha vizinha, ..., linha do chanfro com keypoint)</c> — na
    ///   ordem contrária mede a vizinha. Nasce com <c>TrackDistance</c> = −123456 (sentinela) e o
    ///   texto vai parar fora da folha; 6 mm o põe junto do canto.
    /// LAYOUT = o do desenho à mão do Carlos (15142.101-CDT, lido ponto a ponto em 2026-10-19):
    /// • Ø: linha de cota VERTICAL fora da peça, em escadinha de 9 mm — as feições da metade direita
    ///   à direita da ponta, as da esquerda à esquerda, o Ø menor mais perto. O lado sai da pega na
    ///   linha de centro (a ponta dela daquele lado) e a distância é o <c>TrackDistance</c>, medido da
    ///   pega na feição. O Ø maior (flange) vai na vista de topo, como diâmetro circular.
    /// • Raios: pegam o arco de BAIXO; o rótulo desce em diagonal, ~11 mm por raio, da direita para
    ///   a esquerda (<c>SetKeyPoint</c> no joelho da linha de chamada).
    /// • Ângulo: arco no meio do cone (<c>TrackDistance</c> 0). Chanfro: chamada vertical para baixo.
    /// • Isométrica na mesma escala, com o balão de propriedades da peça.
    /// • Estilo: <c>dim.Style.Name = "PAN (mm)"</c> troca pelo estilo do template (o PAN mostra
    ///   0,5°; o ISO arredonda ângulo para grau inteiro). Tolerância com classe de ajuste usa
    ///   DisplayType 14 — o 9 (o do desenho à mão) grava os números mas não os mostra no PAN.
    /// • Carimbo: <c>UpdatePropertyTextCacheAndDisplay</c> depois do vínculo, senão "Erro: Sem
    ///   referência" em todo campo.
    /// Como a Solid Edge não acusa erro quando a cota pega a coisa errada, TODA cota é conferida
    /// contra o valor do 3D; a que não bate depois das inversões conhecidas é apagada e
    /// relatada — o desenho nunca sai com cota de valor errado.
    /// </summary>
    public static class TurnedDrawingBuilder
    {
        private const int DraftViewFront = 4;          // igFrontView
        private const int DraftViewIso = 9;            // igTopFrontRightView
        private const int FoldLeft = 4;                // igFoldLeft
        private const int DisplayTolerance = 2;        // igDimDisplayTypeTolerance
        private const int DisplayHoleShaftWithTol = 14; // igDimDisplayTypeHoleShaftWithTol: "Ø27 g6 (-0,01/-0,02)"
        private const int ChamferAlongAxis = 0;        // igDimChamferModeAlongAxis

        public static TurnedDrawingResult Create(object app, object part, TurnedProfile profile,
                                                 TurnedDimensionPlan plan, TurnedDrawingOptions options)
        {
            options = options ?? new TurnedDrawingOptions();
            var result = new TurnedDrawingResult();
            dynamic seApp = app;
            string partPath = (string)((dynamic)part).FullName;

            // --- documento
            dynamic draft = File.Exists(options.TemplatePath)
                ? seApp.Documents.Add("SolidEdge.DraftDocument", options.TemplatePath)
                : HouseTemplates.Add((object)seApp, HouseDocumentKind.Draft);
            result.Document = (object)draft;
            dynamic sheet = draft.ActiveSheet;
            double sheetW = (double)sheet.SheetSetup.SheetWidth, sheetH = (double)sheet.SheetSetup.SheetHeight;
            dynamic link = draft.ModelLinks.Add(partPath);

            // --- escala: a maior da lista em que a vista lateral cabe
            double lenM = profile.Length / 1000.0, diaM = 2 * profile.RMax / 1000.0;
            double scale = options.Scales.FirstOrDefault(s =>
                lenM * s <= options.MaxWidthFraction * sheetW && diaM * s <= options.MaxHeightFraction * sheetH);
            if (scale <= 0) scale = options.Scales.Last();
            result.Scale = scale;

            GdtPlan gdt = GdtPlan.Build(profile);

            // --- vista lateral: eixo na horizontal e a CABEÇA (o Ø maior) à esquerda, como no desenho
            // à mão — vale para qualquer eixo e posição da peça no modelo.
            var frame = ViewFrame.For(profile, HeadAtMaxZ(profile));
            double sideX = 0.64 * sheetW, sideY = 0.556 * sheetH;   // onde o Carlos pôs a vista lateral
            dynamic views = sheet.DrawingViews;
            dynamic side = views.AddPartView(link, DraftViewFront, scale, sideX, sideY, 0);
            side.SetViewOrientation(frame.View[0], frame.View[1], frame.View[2],
                                    frame.LocalX[0], frame.LocalX[1], frame.LocalX[2]);
            // Furo/alojamento só aparece na vista lateral como aresta OCULTA — liga, senão as cotas
            // dos Ø internos não têm onde pegar.
            if (profile.Faces.Any(f => !f.External && f.Kind != RevolvedKind.Plane))
                try { side.Defaults_ShowHiddenEdges = true; } catch (Exception ex) { result.Failed.Add("arestas ocultas: " + Msg(ex)); }
            side.Update();
            // A orientação nova recentra a geometria; reposiciona a vista no ponto pedido.
            try { side.SetOrigin(sideX, sideY); } catch { }

            var map = new SideViewMap((object)side, profile, frame);
            Log.Info($"Desenho: escala {ScaleText(scale)}, folha {sheetW * 1000:0}×{sheetH * 1000:0} mm, " +
                     $"vista lateral com {map.Lines.Count} linha(s) e {map.Arcs.Count} arco(s).");

            // --- vista de topo (rebatida à esquerda, deixando lugar para a escadinha de Ø da esquerda) e isométrica
            object endView = null, isoView = null;
            try
            {
                int leftDiameters = Math.Max(plan.Diameters.Count / 2, 1);
                double leftEdge = map.SheetX(map.LeftLocalX) - DimensionWriter.FirstStep - leftDiameters * DimensionWriter.Pitch;
                double endX = leftEdge - diaM * scale / 2 - 0.03;
                endView = (object)views.AddByFold(side, FoldLeft, Math.Max(endX, diaM * scale / 2 + 0.025), sideY);
            }
            catch (Exception ex) { result.Failed.Add("vista de topo: " + Msg(ex)); }
            try
            {
                // Isométrica deitada, como a do Carlos — no 15142.101-CDT dá (0,577; 0,577; −0,577) / (−0,707; 0; −0,707).
                dynamic iso = views.AddPartView(link, DraftViewIso, scale, 0.187 * sheetW, 0.184 * sheetH, 0);
                // Em termos do X local da lateral (que já aponta da cabeça para a ponta): olhar =
                // vista + cima + X, X local = X − vista. Assim a isométrica acompanha a lateral em
                // qualquer eixo e com a cabeça de qualquer lado no modelo.
                double[] lx = frame.LocalX;
                double[] iv = Normalize(new[] { frame.View[0] + frame.Up[0] + lx[0], frame.View[1] + frame.Up[1] + lx[1], frame.View[2] + frame.Up[2] + lx[2] });
                double[] ix = Normalize(new[] { lx[0] - frame.View[0], lx[1] - frame.View[1], lx[2] - frame.View[2] });
                iso.SetViewOrientation(iv[0], iv[1], iv[2], ix[0], ix[1], ix[2]);
                iso.Update();
                try { iso.SetOrigin(0.187 * sheetW, 0.184 * sheetH); } catch { }
                isoView = (object)iso;
            }
            catch (Exception ex) { result.Failed.Add("isométrica: " + Msg(ex)); }

            // --- cotas
            var dims = new DimensionWriter((object)sheet, map, options, result) { EndView = endView, IsoView = isoView, AllFaces = profile.Faces, DatumA = gdt.DatumA };
            dims.CenterLine();
            dims.Stations(plan, OriginZ(profile, plan, gdt, map));
            dims.Diameters(plan);
            dims.Chamfers(plan);
            dims.Angles(plan);
            dims.Gdt(gdt);
            dims.Radii(plan);   // depois do GD&T: o rótulo desce além dos quadros de baixo
            dims.PropertyBalloon();

            // Carimbo: os campos ligados às propriedades da peça só resolvem depois do vínculo.
            try { draft.UpdatePropertyTextCacheAndDisplay(); } catch { }
            SetScaleText((object)sheet, scale);

            result.Ok = true;
            result.Summary = Summarize(result, plan);
            Log.Info(result.Summary);
            return result;
        }

        /// <summary>
        /// "SCALE: 1:2" no carimbo é TEXTO FIXO do fundo da folha no template PanFlight — o Carlos
        /// corrige à mão para a escala real. Aqui ele sai certo: só o texto deste documento muda.
        /// </summary>
        private static void SetScaleText(object sheet, double scale)
        {
            try
            {
                dynamic boxes = ((dynamic)sheet).Background.TextBoxes;
                for (int i = 1; i <= (int)boxes.Count; i++)
                {
                    dynamic t = boxes.Item(i);
                    string text = (string)t.Text;
                    if (text == null || !text.TrimStart().StartsWith("SCALE:", StringComparison.OrdinalIgnoreCase)) continue;
                    // Trocar .Text perde o formato (a caixa volta com a fonte padrão, maior, e sobe
                    // por cima do "NOME:"). Guarda tamanho e fonte e reaplica no texto todo.
                    double size = 0; string font = null; int vAlign = -1, just = -1;
                    try { dynamic e0 = t.Edit; e0.SetSelect(0, text.Length, 5); size = (double)e0.TextSize; font = (string)e0.Font; } catch { }
                    try { vAlign = (int)t.VerticalAlignment; just = (int)t.Justification; } catch { }
                    t.Text = "SCALE: " + ScaleText(scale);
                    // .Text zera o alinhamento (VerticalAlignment 1 / Justification 4 no template) e o
                    // texto sobe para a linha do "NOME:".
                    try { if (vAlign >= 0) t.VerticalAlignment = vAlign; if (just >= 0) t.Justification = just; } catch { }
                    try
                    {
                        dynamic e1 = t.Edit;
                        e1.SetSelect(0, ((string)t.Text).Length, 5);   // seTextSelectAll
                        if (size > 0) e1.TextSize = size;
                        if (!string.IsNullOrEmpty(font)) e1.Font = font;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log.Warn("Escala do carimbo não atualizada: " + Msg(ex)); }
        }

        /// <summary>A cabeça (face de maior raio) fica do lado de Z máximo do eixo?</summary>
        internal static bool HeadAtMaxZ(TurnedProfile p)
        {
            RevolvedFace head = p.Faces.Where(f => f.Kind != RevolvedKind.Plane)
                                       .OrderByDescending(f => f.RMax).ThenByDescending(GdtPlan.Area).FirstOrDefault();
            if (head == null) return true;
            return (head.Z0 + head.Z1) / 2 >= (p.ZMin + p.ZMax) / 2;
        }

        /// <summary>
        /// Origem das cotas no eixo: a referência B quando ela é uma das pontas (cota funcional, a
        /// partir do encosto); sem B, a ponta da DIREITA, como no desenho à mão.
        /// </summary>
        private static double OriginZ(TurnedProfile p, TurnedDimensionPlan plan, GdtPlan gdt, SideViewMap map)
        {
            if (gdt.DatumB != null && (Math.Abs(gdt.DatumB.Z0 - p.ZMin) < 0.01 || Math.Abs(gdt.DatumB.Z0 - p.ZMax) < 0.01))
                return gdt.DatumB.Z0;
            if (plan.Stations.Count == 0) return p.ZMin;
            return plan.Stations.OrderByDescending(st => map.LocalXOf(st.Z)).First().Z;
        }

        private static string Summarize(TurnedDrawingResult r, TurnedDimensionPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Desenho criado: escala {ScaleText(r.Scale)}, {r.Created} cota(s).");
            if (r.Failed.Count > 0)
            {
                sb.AppendLine($"Não saíram ({r.Failed.Count}) — cotar à mão:");
                foreach (string f in r.Failed) sb.AppendLine("  • " + f);
            }
            if (plan.Skipped.Count > 0)
                sb.AppendLine($"Faces sem regra de cota ({plan.Skipped.Count}) — cotar à mão.");
            return sb.ToString();
        }

        public static string ScaleText(double s) =>
            s >= 1 ? $"{s.ToString("0.##", CultureInfo.InvariantCulture)}:1" : $"1:{(1 / s).ToString("0.##", CultureInfo.InvariantCulture)}";

        internal static string Msg(Exception ex) => (ex.InnerException ?? ex).Message;

        // ======================================================================= geometria da vista

        /// <summary>Como o sistema do eixo vira a vista lateral: direção de olhar, X local e "para cima".</summary>
        internal sealed class ViewFrame
        {
            public double[] View, LocalX, Up;

            public static ViewFrame For(TurnedProfile p, bool headAtMaxZ = true)
            {
                double[] a = p.AxisDir;
                // Olha por um eixo do modelo perpendicular à peça (o "menos alinhado" com ela).
                double[][] cands = { new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }, new double[] { 0, 0, 1 } };
                double[] e = cands.OrderBy(c => Math.Abs(Dot(c, a))).First();
                double[] v = Normalize(Sub(e, Mul(a, Dot(e, a))));
                double[] x = Mul(a, headAtMaxZ ? -1 : 1);   // cabeça à esquerda
                return new ViewFrame { View = v, LocalX = x, Up = Cross(v, x) };
            }
        }

        internal sealed class DvLine
        {
            public object Ref;
            public double X1, Y1, X2, Y2;   // local da vista, mm
            public bool Vertical => Math.Abs(X1 - X2) < 1e-3;
            public bool Horizontal => Math.Abs(Y1 - Y2) < 1e-3;
            public double MinX => Math.Min(X1, X2);
            public double MaxX => Math.Max(X1, X2);
            public double MinY => Math.Min(Y1, Y2);
            public double MaxY => Math.Max(Y1, Y2);
        }

        internal sealed class DvArc
        {
            public object Ref;
            public double Cx, Cy, R;       // local, mm
        }

        /// <summary>
        /// A vista lateral lida uma vez: as arestas 2D com coordenadas locais (mm) e a conversão
        /// eixo → local → folha. Toda busca de aresta é geométrica.
        /// </summary>
        internal sealed class SideViewMap
        {
            private readonly object _view;
            private readonly TurnedProfile _p;
            private readonly ViewFrame _f;
            public readonly double OriginX, OriginY, Scale;   // folha (m)
            public readonly List<DvLine> Lines = new List<DvLine>();
            public readonly List<DvArc> Arcs = new List<DvArc>();
            public const double Tol = 0.005; // mm

            public SideViewMap(object view, TurnedProfile p, ViewFrame f)
            {
                _view = view; _p = p; _f = f;
                dynamic v = view;
                object[] o = Call(view, "GetOrigin", new object[] { 0.0, 0.0 }, 0, 1);
                OriginX = (double)o[0]; OriginY = (double)o[1];
                Scale = (double)v.ScaleFactor;

                dynamic lines = v.DVLines2d;
                int n = (int)lines.Count;
                for (int i = 1; i <= n; i++)
                {
                    object l = lines.Item(i);
                    object[] s = Call(l, "GetStartPoint", new object[] { 0.0, 0.0 }, 0, 1);
                    object[] e = Call(l, "GetEndPoint", new object[] { 0.0, 0.0 }, 0, 1);
                    Lines.Add(new DvLine
                    {
                        Ref = (object)((dynamic)l).Reference,
                        X1 = (double)s[0] * 1000, Y1 = (double)s[1] * 1000, X2 = (double)e[0] * 1000, Y2 = (double)e[1] * 1000,
                    });
                }
                dynamic arcs = v.DVArcs2d;
                n = (int)arcs.Count;
                for (int i = 1; i <= n; i++)
                {
                    object a = arcs.Item(i);
                    object[] c = Call(a, "GetCenterPoint", new object[] { 0.0, 0.0 }, 0, 1);
                    Arcs.Add(new DvArc
                    {
                        Ref = (object)((dynamic)a).Reference,
                        Cx = (double)c[0] * 1000, Cy = (double)c[1] * 1000, R = (double)((dynamic)a).Radius * 1000,
                    });
                }
            }

            /// <summary>Ponto (z no eixo, r para cima) → local da vista (mm).</summary>
            public void Local(double z, double r, out double lx, out double ly)
            {
                double[] pt = new double[3];
                for (int k = 0; k < 3; k++) pt[k] = (_p.AxisPoint[k] + z * _p.AxisDir[k] + r * _f.Up[k]) / 1000.0;
                object[] res = Call(_view, "ModelToView", new object[] { pt[0], pt[1], pt[2], 0.0, 0.0 }, 3, 4);
                lx = (double)res[3] * 1000; ly = (double)res[4] * 1000;
            }

            /// <summary>Local (mm) → folha (m), o espaço dos pontos de pega das cotas.</summary>
            public double SheetX(double lx) => OriginX + Scale * lx / 1000.0;
            public double SheetY(double ly) => OriginY + Scale * ly / 1000.0;

            public double LocalXOf(double z) { Local(z, 0, out double lx, out _); return lx; }

            /// <summary>Aresta vertical (perfil de plano ou transição) na posição z: a mais comprida.</summary>
            public DvLine VerticalAt(double z)
            {
                double lx = LocalXOf(z);
                return Lines.Where(l => l.Vertical && Math.Abs(l.X1 - lx) < Tol)
                            .OrderByDescending(l => l.MaxY - l.MinY).FirstOrDefault();
            }

            /// <summary>Geratriz de cima (y = +r) que cobre a posição z.</summary>
            public DvLine UpperGeneratrix(double z0, double z1, double rAt0, double rAt1)
            {
                Local(z0, rAt0, out double ax, out double ay);
                Local(z1, rAt1, out double bx, out double by);
                return Lines.FirstOrDefault(l => !l.Vertical &&
                    ((Near(l.X1, ax) && Near(l.Y1, ay) && Near(l.X2, bx) && Near(l.Y2, by)) ||
                     (Near(l.X1, bx) && Near(l.Y1, by) && Near(l.X2, ax) && Near(l.Y2, ay))));
            }

            /// <summary>Geratriz de cima (não vertical) com uma ponta na posição z.</summary>
            public DvLine GeneratrixEndingAt(double z)
            {
                double lx = LocalXOf(z);
                return Lines.Where(l => !l.Vertical && ((Near(l.X1, lx) && l.Y1 > 0) || (Near(l.X2, lx) && l.Y2 > 0)))
                            .OrderByDescending(l => Math.Max(l.Y1, l.Y2)).FirstOrDefault();
            }

            public DvLine LowerGeneratrix(double z0, double z1, double rAt0, double rAt1) =>
                UpperGeneratrix(z0, z1, -rAt0, -rAt1);

            /// <summary>Arco do arredondamento (metade de cima).</summary>
            public DvArc FilletArc(RevolvedFace torus, bool below = false)
            {
                Local(torus.CenterZ, below ? -torus.MajorRadius : torus.MajorRadius, out double cx, out double cy);
                return Arcs.FirstOrDefault(a => Near(a.Cx, cx) && Near(a.Cy, cy) && Math.Abs(a.R - torus.MinorRadius) < Tol);
            }

            public double TopLocalY => Lines.Count == 0 ? 0 : Lines.Max(l => l.MaxY);
            public double BottomLocalY => Lines.Count == 0 ? 0 : Lines.Min(l => l.MinY);
            public double LeftLocalX => Lines.Count == 0 ? 0 : Lines.Min(l => l.MinX);
            public double RightLocalX => Lines.Count == 0 ? 0 : Lines.Max(l => l.MaxX);

            private static bool Near(double a, double b) => Math.Abs(a - b) < Tol;
        }

        // ======================================================================= cotas

        private sealed class DimensionWriter
        {
            private readonly dynamic _sheet;
            private readonly dynamic _dims;
            private readonly SideViewMap _m;
            private readonly TurnedDrawingOptions _o;
            private readonly TurnedDrawingResult _r;
            private object _centerLine;
            // Fileira das cotas no eixo ~36 mm acima da peça, como no desenho à mão: o vão entre as
            // duas fica para os ângulos (logo acima da peça) e para a chamada do chanfro de cima.
            private const double TextGap = 0.036;
            /// <summary>Escadinha de Ø (m de folha): 1ª linha a partir da ponta da peça, depois o passo.</summary>
            public const double FirstStep = 0.014, Pitch = 0.009;
            public object EndView, IsoView;
            /// <summary>Cota de Ø de cada face (para pendurar o GD&amp;T e o símbolo de referência nela).</summary>
            private readonly Dictionary<RevolvedFace, object> _diamDims = new Dictionary<RevolvedFace, object>();
            private readonly HashSet<object> _endViewDims = new HashSet<object>();

            public DimensionWriter(object sheet, SideViewMap map, TurnedDrawingOptions o, TurnedDrawingResult r)
            {
                _sheet = sheet; _dims = ((dynamic)sheet).Dimensions; _m = map; _o = o; _r = r;
            }

            /// <summary>
            /// Linha de centro PRESA às arestas das duas pontas da peça (AddByStartEnd): acompanha a
            /// vista e a peça. O estilo já a prolonga ~3,5 mm além de cada ponta (medido 2026-10-20).
            /// Sem as arestas, cai na linha solta de antes.
            /// </summary>
            public void CenterLine()
            {
                double y = _m.SheetY(0);
                DvLine l = EndEdge(_m.LeftLocalX), r = EndEdge(_m.RightLocalX);
                if (l != null && r != null)
                {
                    try
                    {
                        _centerLine = (object)_sheet.CenterLines.AddByStartEnd(
                            l.Ref, _m.SheetX(l.X1), y, 0.0, false, r.Ref, _m.SheetX(r.X1), y, 0.0, false);
                        return;
                    }
                    catch (Exception ex) { _r.Failed.Add("linha de centro presa às pontas (ficou solta): " + Msg(ex)); }
                }
                try
                {
                    _centerLine = (object)_sheet.CenterLines.Add(
                        _m.SheetX(_m.LeftLocalX) - 0.004, y, 0.0, _m.SheetX(_m.RightLocalX) + 0.004, y, 0.0);
                }
                catch (Exception ex) { _r.Failed.Add("linha de centro: " + Msg(ex)); }
            }

            /// <summary>Aresta vertical mais comprida na ponta (x local) que atravessa o eixo.</summary>
            private DvLine EndEdge(double localX) =>
                _m.Lines.Where(e => e.Vertical && Math.Abs(e.X1 - localX) < SideViewMap.Tol && e.MinY < 0 && e.MaxY > 0)
                        .OrderByDescending(e => e.MaxY - e.MinY).FirstOrDefault();

            // ---------------------------------------------------------------- estações no eixo

            public void Stations(TurnedDimensionPlan plan, double originZ)
            {
                if (plan.Stations.Count == 0) return;
                double textY = _m.SheetY(_m.TopLocalY) + TextGap;
                AxialStation o = plan.Stations.OrderBy(st => Math.Abs(st.Z - originZ)).First();
                StationRef originRef = StationEdge(o);
                if (originRef == null) { _r.Failed.Add($"origem das cotas no eixo (Z={N(o.Z)}): aresta não achada"); return; }

                dynamic origin;
                try
                {
                    // Texto da origem ACIMA da vista: é isso que faz a medida correr ao longo do eixo.
                    origin = _dims.AddCoordinateOriginEx(originRef.Ref, originRef.X, originRef.Y, 0.0, originRef.KeyPoint,
                                                         originRef.X, textY, 0.0);
                    _r.Created++;
                }
                catch (Exception ex) { _r.Failed.Add("origem das cotas no eixo: " + Msg(ex)); return; }

                foreach (AxialStation s in plan.Stations.Where(st => !ReferenceEquals(st, o)))
                {
                    string what = $"cota no eixo {N(Math.Abs(s.Z - o.Z))}";
                    StationRef e = StationEdge(s);
                    if (e == null) { _r.Failed.Add(what + ": aresta não achada na vista"); continue; }
                    dynamic d;
                    try { d = _dims.AddCoordinateEx(origin, e.Ref, e.X, e.Y, 0.0, e.KeyPoint, e.X, textY, 0.0); }
                    catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); continue; }
                    if (!Check((object)d, Math.Abs(s.Z - o.Z), what)) continue;
                    // Posição de face colorida leva a tolerância da cor (coluna de eixos/ressaltos) —
                    // só os afastamentos: classe de ajuste (g6/H7) é de Ø, não de comprimento.
                    ApplyTolerance((object)d, s.Finish, true, withFit: false);
                }
            }

            private sealed class StationRef { public object Ref; public double X, Y; public bool KeyPoint; }

            /// <summary>
            /// Onde pegar a estação: a aresta vertical (perfil de plano/transição) ou, quando a mudança
            /// é só de COR no mesmo diâmetro (faixa verde do pino), a PONTA da geratriz — ali não há
            /// aresta visível na vista, mas cada face tem a sua linha de silhueta.
            /// </summary>
            private StationRef StationEdge(AxialStation s)
            {
                DvLine v = _m.VerticalAt(s.Z);
                if (v != null) return new StationRef { Ref = v.Ref, X = _m.SheetX(v.X1), Y = _m.SheetY(v.MaxY) };
                DvLine g = _m.GeneratrixEndingAt(s.Z);
                if (g == null) return null;
                double lx = _m.LocalXOf(s.Z);
                double ly = Math.Abs(g.X1 - lx) < SideViewMap.Tol ? g.Y1 : g.Y2;
                return new StationRef { Ref = g.Ref, X = _m.SheetX(lx), Y = _m.SheetY(ly), KeyPoint = true };
            }

            // ---------------------------------------------------------------- diâmetros

            public void Diameters(TurnedDimensionPlan plan)
            {
                if (_centerLine == null) { foreach (DiameterSpec d in plan.Diameters) _r.Failed.Add($"Ø{N(d.Diameter)}: sem linha de centro"); return; }

                // O Ø do flange (maior cilindro) vai na vista de topo, como no desenho à mão.
                // Só quando o trecho é curto demais para uma cota na lateral (o flange de 2,65 mm do
                // CDT); um pino de Ø constante fica com o Ø na lateral, onde o GD&T dele cabe.
                DiameterSpec flange = plan.Diameters.Where(d => d.Face.Kind == RevolvedKind.Cylinder)
                                                    .OrderByDescending(d => d.Radius).FirstOrDefault();
                if (flange != null && flange.Face.Length * _m.Scale > 8.0) flange = null;
                if (flange != null && EndView != null && EndViewDiameter(flange)) { }
                else flange = null;

                double center = _m.SheetX((_m.LeftLocalX + _m.RightLocalX) / 2);
                double left = _m.SheetX(_m.LeftLocalX), right = _m.SheetX(_m.RightLocalX);
                var specs = plan.Diameters.Where(d => !ReferenceEquals(d, flange)).ToList();
                var rightSide = specs.Where(d => FaceCenterX(d.Face) >= center).OrderBy(d => d.Diameter).ToList();
                var leftSide = specs.Where(d => FaceCenterX(d.Face) < center).OrderBy(d => d.Diameter).ToList();
                Ladder(rightSide, right, +1);
                Ladder(leftSide, left, -1);
            }

            /// <summary>Face da referência A: o símbolo pendura na ponta da cota de Ø dela.</summary>
            public RevolvedFace DatumA { get; set; }

            /// <summary>
            /// Escadinha de Ø para fora da ponta. Depois da cota da referência A o passo cresce:
            /// o símbolo pendura do lado de fora da linha (~6 mm) e cairia em cima do texto da próxima.
            /// </summary>
            private void Ladder(List<DiameterSpec> specs, double edge, int side)
            {
                double offset = FirstStep;
                foreach (DiameterSpec spec in specs)
                {
                    SideDiameter(spec, edge + side * offset, side);
                    offset += Pitch + (ReferenceEquals(spec.Face, DatumA) ? DatumRoom : 0);
                }
            }

            private const double DatumRoom = 0.007;

            /// <summary>Guarda a cota para a face e para as faces iguais que ela representa ("2x").</summary>
            private void Remember(DiameterSpec spec, object dim)
            {
                _diamDims[spec.Face] = dim;
                if (spec.Count > 1)
                    foreach (RevolvedFace f in AllFaces)
                        if (f.Kind == RevolvedKind.Cylinder && Math.Abs(f.R0 - spec.Radius) < 0.005 && ReferenceEquals(f.Finish, spec.Finish))
                            if (!_diamDims.ContainsKey(f)) _diamDims[f] = dim;
            }

            public List<RevolvedFace> AllFaces { get; set; } = new List<RevolvedFace>();

            private double FaceCenterX(RevolvedFace f) => (_m.SheetX(_m.LocalXOf(f.Z0)) + _m.SheetX(_m.LocalXOf(f.Z1))) / 2;

            /// <summary>Ø simétrico com a linha de cota em <paramref name="lineX"/> (folha), do lado <paramref name="side"/>.</summary>
            private void SideDiameter(DiameterSpec spec, double lineX, int side)
            {
                string what = $"Ø{N(spec.Diameter)}";
                RevolvedFace f = spec.Face;
                DvLine line = _m.UpperGeneratrix(f.Z0, f.Z1, f.R0, f.R1);
                if (line == null) { _r.Failed.Add(what + ": geratriz não achada na vista"); return; }

                double pickZ; bool keyPoint;
                if (f.Kind == RevolvedKind.Cylinder) { pickZ = (f.Z0 + f.Z1) / 2; keyPoint = false; }
                else { pickZ = Math.Abs(spec.Z - f.Z0) < Math.Abs(spec.Z - f.Z1) ? f.Z0 : f.Z1; keyPoint = true; }
                double rAtPick = f.Kind == RevolvedKind.Cylinder ? f.R0 : (pickZ == f.Z0 ? f.R0 : f.R1);
                _m.Local(pickZ, rAtPick, out double lx, out double ly);
                double pickX = _m.SheetX(lx);

                // A pega na linha de centro decide o LADO: a ponta dela do lado da escadinha.
                // Dentro do prolongamento de ~3,5 mm da linha de centro presa às pontas.
                double clX = side > 0 ? _m.SheetX(_m.RightLocalX) + 0.002 : _m.SheetX(_m.LeftLocalX) - 0.002;
                dynamic d;
                try
                {
                    d = _dims.AddSymmetricalDiameter(_centerLine, clX, _m.SheetY(0), 0.0, false,
                                                     line.Ref, pickX, _m.SheetY(ly), 0.0, keyPoint);
                }
                catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); return; }

                if (!Check((object)d, 2 * rAtPick, what, dd => dd.MeasurementAxisDirection = !(bool)dd.MeasurementAxisDirection)) return;
                if (spec.Count > 1) TrySet(() => d.PrefixString = spec.Count + "x ");
                SetStyle((object)d, StylePan);
                ApplyTolerance((object)d, spec.Finish, f.External);
                TrySet(() => d.DisplayHalf = true);   // linha de cota inteira, com as duas setas
                Remember(spec, (object)d);
                // Positivo leva a linha para a ESQUERDA da pega; negativo, para a direita.
                // A SE prende a pega na PONTA da geratriz (k1), não onde clicamos: a distância sai dela.
                double attachX = KeyX((object)d, 1);
                if (double.IsNaN(attachX)) attachX = pickX;
                // k4 = ponta da linha de cota; confere só o LADO e inverte se precisar.
                PlaceAndVerify((object)d, attachX - lineX, kd => Math.Sign(KeyX(kd, 4) - attachX) == Math.Sign(lineX - attachX));
            }

            /// <summary>Ø do flange na vista de topo: diâmetro circular no círculo de fora, acima dele.</summary>
            private bool EndViewDiameter(DiameterSpec spec)
            {
                string what = $"Ø{N(spec.Diameter)} (vista de topo)";
                try
                {
                    dynamic ev = EndView;
                    dynamic circles = ev.DVCircles2d;
                    object best = null;
                    for (int i = 1; i <= (int)circles.Count; i++)
                    {
                        dynamic c = circles.Item(i);
                        if (Math.Abs((double)c.Radius * 1000 - spec.Radius) < SideViewMap.Tol) { best = (object)c.Reference; break; }
                    }
                    if (best == null) return false;
                    dynamic d = _dims.AddCircularDiameter(best);
                    if (!Check((object)d, spec.Diameter, what)) return false;
                    SetStyle((object)d, StylePan);
                    ApplyTolerance((object)d, spec.Finish, spec.Face.External);
                    Remember(spec, (object)d);
                    _endViewDims.Add((object)d);
                    double r = spec.Radius / 1000.0 * (double)ev.ScaleFactor;
                    object[] o = Call(EndView, "GetOrigin", new object[] { 0.0, 0.0 }, 0, 1);
                    double cy = (double)o[1];
                    TrySet(() => d.TrackDistance = r + 0.034);
                    TrySet(() => d.TrackAngle = Math.PI);           // horizontal, como no desenho à mão
                    if (KeyY((object)d, 6) < cy) TrySet(() => d.TrackAngle = 0.0); // caiu embaixo: o outro lado
                    return true;
                }
                catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); return false; }
            }

            // ---------------------------------------------------------------- raios, chanfros, ângulos

            public void Radii(TurnedDimensionPlan plan)
            {
                // Arcos de BAIXO, da direita para a esquerda; cada rótulo ~11 mm abaixo do anterior.
                var items = new List<Tuple<RadiusSpec, DvArc>>();
                foreach (RadiusSpec spec in plan.Radii)
                {
                    DvArc arc = _m.FilletArc(spec.Face, below: true);
                    if (arc == null) { _r.Failed.Add($"R{N(spec.Radius)}: arco não achado na vista"); continue; }
                    items.Add(Tuple.Create(spec, arc));
                }
                double bottom = _m.SheetY(_m.BottomLocalY);
                int k = 0;
                foreach (var it in items.OrderByDescending(t => t.Item2.Cx))
                {
                    RadiusSpec spec = it.Item1;
                    string what = $"R{N(spec.Radius)}";
                    dynamic d;
                    try { d = _dims.AddRadius(it.Item2.Ref); }
                    catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); continue; }
                    if (!Check((object)d, spec.Radius, what)) continue;
                    if (spec.Count > 1) TrySet(() => d.PrefixString = spec.Count + "x ");
                    SetStyle((object)d, StylePanH);
                    ApplyTolerance((object)d, spec.Finish, spec.Face.External);

                    // Chamada com joelho (Leader + BreakPosition 3); o joelho (ponto-chave 3) vai para uma
                    // escada que desce ~10,5 mm por raio, da direita para a esquerda.
                    // TrackDistance aqui NÃO é a inclinação da chamada (o Carlos tem 0,61 em todas, com
                    // inclinações diferentes) — mover o ponto-chave é o controle que funciona.
                    TrySet(() => d.Leader = true);
                    TrySet(() => d.TerminatorPosition = false);
                    TrySet(() => d.BreakPosition = 3);
                    double ax = KeyX((object)d, 0), ay = KeyY((object)d, 0);   // onde a chamada toca o arco
                    if (double.IsNaN(ax)) { ax = _m.SheetX(it.Item2.Cx); ay = _m.SheetY(it.Item2.Cy - it.Item2.R); }
                    // Bem abaixo da peça: o vão logo embaixo é das chamadas do GD&T das faces planas da direita.
                    double y = Math.Min(bottom - 0.020 - k * 0.0105, ay - 0.008);
                    // O texto fica ~4 mm acima do patamar; a caixa de baixo vai até 3 mm sob o centro.
                    if (!double.IsNaN(LowestFrameY)) y = Math.Min(y, LowestFrameY - 0.011 - k * 0.0105);
                    double x = ax + Math.Max(0.010, (ay - y) * 0.5);
                    object rd = d;
                    TrySet(() => Call(rd, "SetKeyPoint", new object[] { 3, x, y, 0.0 }));
                    k++;
                }
            }

            public void Chamfers(TurnedDimensionPlan plan)
            {
                // O chanfro da ponta esquerda desce (a escada de raios fica à direita); os outros sobem,
                // entre a peça e a fileira de cotas no eixo — como no desenho à mão.
                // Chanfros iguais saem numa cota só ("2x"): a representante é o de FORA mais à esquerda
                // (o do flange) — o rótulo desce do lado oposto à escada de raios.
                foreach (ChamferSpec c in plan.Chamfers.Where(c => c.Count > 1))
                {
                    RevolvedFace best = AllFaces.Where(f => f.IsChamfer(2.0) && f.External && Math.Abs(f.Length - c.Leg) < 0.005)
                                                .OrderBy(f => _m.SheetX(_m.LocalXOf((f.Z0 + f.Z1) / 2))).FirstOrDefault();
                    if (best != null) c.Face = best;
                }
                var ordered = plan.Chamfers.OrderBy(c => _m.SheetX(_m.LocalXOf((c.Face.Z0 + c.Face.Z1) / 2))).ToList();
                for (int ci = 0; ci < ordered.Count; ci++)
                {
                    ChamferSpec spec = ordered[ci];
                    bool down = ci == 0;
                    double sgn = down ? -1 : 1;
                    RevolvedFace f = spec.Face;
                    string what = $"chanfro {N(spec.Leg)}x45°";
                    DvLine ch = down ? _m.LowerGeneratrix(f.Z0, f.Z1, f.R0, f.R1) : _m.UpperGeneratrix(f.Z0, f.Z1, f.R0, f.R1);
                    // Vizinha: a aresta VERTICAL na ponta de raio menor do chanfro (a face plana).
                    double zFlat = f.R0 < f.R1 ? f.Z0 : f.Z1;
                    DvLine flat = _m.VerticalAt(zFlat);
                    if (ch == null || flat == null) { _r.Failed.Add(what + ": arestas não achadas na vista"); continue; }

                    double rFlat = Math.Min(f.R0, f.R1);
                    _m.Local(zFlat, sgn * rFlat, out double fx, out double fy);
                    _m.Local((f.Z0 + f.Z1) / 2, sgn * (f.R0 + f.R1) / 2, out double mx, out double my);
                    dynamic d;
                    try
                    {
                        d = _dims.AddChamfer(flat.Ref, _m.SheetX(fx), _m.SheetY(fy), 0.0, false,
                                             ch.Ref, _m.SheetX(mx), _m.SheetY(my), 0.0, true, ChamferAlongAxis);
                    }
                    catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); continue; }
                    if (!Check((object)d, spec.Leg, what)) continue;
                    SetStyle((object)d, StylePanOneDecimal);   // "0,4 X 45,0°", como no desenho à mão
                    if (spec.Count > 1) TrySet(() => d.PrefixString = spec.Count + "x ");
                    // Chamada vertical para fora da peça, com joelho curto (como no desenho à mão).
                    double chY = _m.SheetY(fy);
                    double drop = down ? Math.Max(chY - (_m.SheetY(_m.BottomLocalY) - 0.015), 0.010)
                                       : Math.Max((_m.SheetY(_m.TopLocalY) + 0.025) - chY, 0.010);
                    TrySet(() => d.TerminatorPosition = true);
                    // Texto para FORA da peça: o de baixo com 3 já cai à esquerda; o de cima, com 3,
                    // cairia à direita, em cima das linhas de chamada das cotas no eixo — com 1 vai à
                    // esquerda (medido na SE em 2026-10-20).
                    TrySet(() => d.BreakPosition = down ? 3 : 1);
                    TrySet(() => d.BreakDistance = 0.004);
                    PlaceAndVerify((object)d, drop, kd => down ? KeyY(kd, 4) < chY : KeyY(kd, 4) > chY);
                }
            }

            public void Angles(TurnedDimensionPlan plan)
            {
                foreach (AngleSpec spec in plan.Angles)
                {
                    RevolvedFace f = spec.Face;
                    string what = $"ângulo {N(spec.IncludedDeg)}°";
                    DvLine top = _m.UpperGeneratrix(f.Z0, f.Z1, f.R0, f.R1);
                    DvLine bottom = _m.LowerGeneratrix(f.Z0, f.Z1, f.R0, f.R1);
                    if (top == null || bottom == null) { _r.Failed.Add(what + ": geratrizes não achadas na vista"); continue; }
                    double zMid = (f.Z0 + f.Z1) / 2, rMid = (f.R0 + f.R1) / 2;
                    _m.Local(zMid, rMid, out double tx, out double ty);
                    _m.Local(zMid, -rMid, out double bx, out double by);
                    dynamic d;
                    try
                    {
                        d = _dims.AddAngleBetweenObjects(top.Ref, _m.SheetX(tx), _m.SheetY(ty), 0.0, false,
                                                         bottom.Ref, _m.SheetX(bx), _m.SheetY(by), 0.0, false);
                    }
                    catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); continue; }
                    // Valor em radianos; nasce suplementar.
                    if (Check((object)d, spec.IncludedDeg * Math.PI / 180.0 * 1000.0, what,
                          dd => dd.MeasureLineDirection = !(bool)dd.MeasureLineDirection,
                          tolerance: 0.5 * Math.PI / 180.0 * 1000.0 / 60.0)) // 0,5' de arco
                    {
                        SetStyle((object)d, StylePan);
                        // Texto (ponto-chave 6) acima da peça, no meio do cone — o arco vai junto.
                        double textX = _m.SheetX(_m.LocalXOf(zMid)), textY = _m.SheetY(_m.TopLocalY) + 0.002;
                        object dimObj = d;
                        TrySet(() => Call(dimObj, "SetKeyPoint", new object[] { 6, textX, textY, 0.0 }));
                    }
                }
            }

            /// <summary>
            /// GD&amp;T: só tolerância de posição. As referências levam o símbolo na própria feição
            /// (A na ponta de baixo da cota de Ø, B no pé da aresta); cada quadro tem chamada
            /// VERTICAL saindo da pega e linha de quebra (patamar) até a caixa.
            /// </summary>
            public void Gdt(GdtPlan gdt)
            {
                if (gdt.DatumA != null) DatumSymbol(gdt.DatumA, "A");
                if (gdt.DatumB != null) DatumSymbol(gdt.DatumB, "B");

                // Um quadro por (alvo, texto): o Ø "3x" é uma cota só e duas faces planas na mesma
                // aresta da vista pegam a mesma linha — sem isso saíam quadros repetidos.
                var items = new List<FrameItem>();
                foreach (GdtFrame f in gdt.Frames)
                {
                    FrameItem it = Anchor(f);
                    if (it == null) continue;
                    if (items.Any(o => ReferenceEquals(o.Target, it.Target) && o.Text == it.Text)) continue;
                    items.Add(it);
                }

                double left = _m.SheetX(_m.LeftLocalX), right = _m.SheetX(_m.RightLocalX), mid = (left + right) / 2;
                foreach (FrameItem it in items.Where(i => i.Kind == AnchorKind.EndView))
                    // Ø da vista de topo: logo acima da própria cota (a chamada não atravessa a folha).
                    Place(it, -1, KeyX(it.Target, 6), KeyY(it.Target, 6) + 0.006, vertical: false);

                // Ø da lateral: escada ACIMA da escadinha de Ø (a SE prende a chamada na ponta de CIMA
                // da linha de cota, qualquer que seja o ponto pedido). Caixa do lado de fora da pega,
                // a pega mais de fora no 1º degrau: a chamada vertical de um degrau nunca atravessa
                // a caixa de outro. Se a caixa de fora passaria da margem da folha, o lado inteiro
                // vira para dentro (caixa antes da pega, a pega mais de dentro no 1º degrau).
                double sheetRight = SheetWidth() - SheetMargin;
                var diam = items.Where(i => i.Kind == AnchorKind.Diameter).ToList();
                double y = Math.Max(DiameterEnds().DefaultIfEmpty(_m.SheetY(_m.TopLocalY)).Max(), _m.SheetY(_m.TopLocalY)) + FrameGap + 0.006;
                var rightDiam = diam.Where(i => i.Ax >= mid).ToList();
                if (rightDiam.Any(i => i.Ax + BreakLeg + i.Width > sheetRight))
                    Staircase(rightDiam.OrderBy(i => i.Ax), -1, y, +FrameRow);
                else
                    Staircase(rightDiam.OrderByDescending(i => i.Ax), +1, y, +FrameRow);
                var leftDiam = diam.Where(i => i.Ax < mid).ToList();
                if (leftDiam.Any(i => i.Ax - BreakLeg - i.Width < SheetMargin))
                    Staircase(leftDiam.OrderByDescending(i => i.Ax), +1, y, +FrameRow);
                else
                    Staircase(leftDiam.OrderBy(i => i.Ax), -1, y, +FrameRow);

                // Faces planas: em cima a chamada vertical cairia sobre a linha de chamada da cota no
                // eixo da mesma face, então todas descem — pega no pé da aresta, escada abaixo da
                // escadinha de Ø, caixa à direita, a pega mais à direita no 1º degrau. As duas metades
                // da peça começam no mesmo degrau: as da esquerda não empurram as da direita para baixo.
                var planes = items.Where(i => i.Kind == AnchorKind.Plane).ToList();
                foreach (FrameItem it in planes)
                    if (AnchorOnFace(it.Frame.Face, below: true, out object t, out double bx, out double by)) { it.Target = t; it.Ax = bx; it.Ay = by; }
                y = Math.Min(LowestDiameterEnd(), _m.SheetY(_m.BottomLocalY)) - FrameGap - 0.006;
                double a = Staircase(planes.Where(i => i.Ax >= mid).OrderByDescending(i => i.Ax), +1, y, -FrameRow);
                double b = Staircase(planes.Where(i => i.Ax < mid).OrderByDescending(i => i.Ax), +1, y, -FrameRow);
                LowestFrameY = double.IsNaN(a) ? b : double.IsNaN(b) ? a : Math.Min(a, b);
            }

            /// <summary>Margem da moldura da folha (m): o template da casa desenha a borda a 10 mm.</summary>
            private const double SheetMargin = 0.012;

            private double SheetWidth()
            {
                try { return (double)_sheet.SheetSetup.SheetWidth; } catch { return 0.420; }
            }

            /// <summary>Y (folha) do degrau mais baixo usado pelo GD&amp;T embaixo da peça — os raios descem além dele.</summary>
            public double LowestFrameY { get; private set; } = double.NaN;

            private enum AnchorKind { EndView, Diameter, Plane }

            private sealed class FrameItem
            {
                public GdtFrame Frame; public object Target; public double Ax, Ay, Width;
                public string Text; public AnchorKind Kind;
            }

            private const double FrameRow = 0.008, FrameGap = 0.004;
            /// <summary>Patamar da linha de quebra entre a chamada e a caixa.</summary>
            private const double BreakLeg = 0.005;

            private void DatumSymbol(RevolvedFace face, string letter)
            {
                try
                {
                    double x, y; object target;
                    if (_diamDims.TryGetValue(face, out object dim))
                    { target = dim; x = KeyX(dim, 5); y = KeyY(dim, 5); }
                    else if (!AnchorOnFace(face, below: true, out target, out x, out y)) { _r.Failed.Add($"referência {letter}: aresta não achada"); return; }

                    dynamic df = _sheet.DatumFrames.AddByTerminator(target, x, y, 0.0, false);
                    df.Datum = letter;
                    _r.Created++;
                }
                catch (Exception ex) { _r.Failed.Add($"referência {letter}: {Msg(ex)}"); }
            }

            /// <summary>
            /// Onde a chamada do quadro pega: Ø da lateral na ponta de CIMA da linha de cota (é o
            /// eixo que se controla), Ø da vista de topo na própria cota, face plana na aresta.
            /// </summary>
            private FrameItem Anchor(GdtFrame f)
            {
                // Largura da caixa (m de folha) no estilo da casa: ~6,5 mm por célula.
                // Largura medida da caixa no estilo da casa: 3 células ~20 mm, com o Ø ~27 mm.
                var it = new FrameItem { Frame = f, Text = FrameText(f), Width = f.DiameterZone ? 0.027 : f.Datum == null ? 0.014 : 0.020 };
                if (_diamDims.TryGetValue(f.Face, out object dim))
                {
                    it.Target = dim;
                    if (_endViewDims.Contains(dim)) { it.Kind = AnchorKind.EndView; it.Ax = KeyX(dim, 4); it.Ay = KeyY(dim, 4); return it; }
                    it.Kind = AnchorKind.Diameter;
                    int high = KeyY(dim, 4) >= KeyY(dim, 5) ? 4 : 5;
                    it.Ax = KeyX(dim, high); it.Ay = KeyY(dim, high);
                    return it;
                }
                if (!AnchorOnFace(f.Face, below: false, out object target, out double ax, out double ay))
                { _r.Failed.Add("GD&T " + f + ": aresta não achada"); return null; }
                it.Target = target; it.Ax = ax; it.Ay = ay; it.Kind = AnchorKind.Plane;
                return it;
            }

            /// <summary>
            /// Degraus a partir de <paramref name="y"/>, andando <paramref name="step"/> por quadro;
            /// caixa do lado <paramref name="side"/> da pega. Devolve o último Y usado.
            /// </summary>
            private double Staircase(IEnumerable<FrameItem> ordered, int side, double y, double step)
            {
                double last = double.NaN;
                foreach (FrameItem it in ordered)
                {
                    Place(it, side, it.Ax, y, vertical: true);
                    last = y;
                    y += step;
                }
                return last;
            }

            /// <summary>Pontas (Y de folha) das linhas de cota de Ø da vista lateral.</summary>
            private IEnumerable<double> DiameterEnds() =>
                _diamDims.Values.Distinct().Where(d => !_endViewDims.Contains(d))
                         .SelectMany(d => new[] { KeyY(d, 4), KeyY(d, 5) }).Where(v => !double.IsNaN(v));

            private double LowestDiameterEnd() => DiameterEnds().DefaultIfEmpty(_m.SheetY(_m.BottomLocalY)).Min();

            /// <summary>
            /// Cria o quadro com linha de quebra: chamada da pega até (<paramref name="x"/>, y), patamar
            /// e a caixa do lado <paramref name="side"/>. Com <paramref name="vertical"/>, a ponta da
            /// chamada fica em x exato: o deslocamento entre o vértice pedido e onde a SE põe a ponta
            /// depende do lado e da largura real da caixa, então mede-se (k1) e, se errou, recria-se
            /// corrigido — o AddVertex é uma translação, uma correção basta.
            /// </summary>
            private void Place(FrameItem it, int side, double x, double y, bool vertical)
            {
                string what = "GD&T " + it.Frame;
                try
                {
                    // Palpite medido em 2026-10-20: caixa à direita, a ponta sai ~meia caixa antes do
                    // vértice; à esquerda o vértice é o centro da caixa.
                    double vx = side > 0 ? x + it.Width / 2 : x - BreakLeg - it.Width / 2;
                    dynamic fcf = Frame(it, vx, y, side);
                    if (vertical && fcf != null)
                    {
                        double err = x - KeyX((object)fcf, 1);
                        if (!double.IsNaN(err) && Math.Abs(err) > 0.0003)
                        {
                            try { fcf.Delete(); } catch { }
                            fcf = Frame(it, vx + err, y, side);
                        }
                    }
                    if (fcf != null) _r.Created++;
                }
                catch (Exception ex) { _r.Failed.Add(what + ": " + Msg(ex)); }
            }

            private dynamic Frame(FrameItem it, double vx, double y, int side)
            {
                // Ordem que funciona (2026-10-19): criar PRESO à cota, preencher, e só então
                // AddVertex — a caixa vai para o vértice e a chamada dobra até a cota. Com Add +
                // SetTerminator a caixa salta para cima da ponta da chamada e ignora a posição.
                dynamic fcf = _sheet.FeatureControlFrames.AddByTerminator(it.Target, it.Ax, it.Ay, 0.0, false);
                fcf.PrimaryFrame = it.Text;
                fcf.AddVertex(vx, y, 0.0);
                // Linha de quebra (patamar entre a chamada e a caixa); a direção põe a caixa à
                // esquerda (true) ou à direita (false) da ponta da chamada.
                TrySet(() => fcf.BreakLine = true);
                TrySet(() => fcf.BreakLineDistance = BreakLeg);
                TrySet(() => fcf.BreakLineDirection = side < 0);
                return fcf;
            }

            /// <summary>Texto do quadro na sintaxe da SE: símbolo, separador de célula e referência.</summary>
            internal static string FrameText(GdtFrame f)
            {
                string sym;
                switch (f.Symbol)
                {
                    // Códigos da própria SE (Template\Reports\DraftFCF.txt): %PO posição, %DI Ø,
                    // %CY cilindricidade, %PP perpendicularidade, %PR paralelismo; células separadas por "|".
                    case GdtSymbol.Position: sym = "%PO"; break;
                    case GdtSymbol.Cylindricity: sym = "%CY"; break;
                    case GdtSymbol.Flatness: sym = "%FL"; break;
                    case GdtSymbol.Perpendicularity: sym = "%PP"; break;
                    case GdtSymbol.Parallelism: sym = "%PR"; break;
                    default: sym = "%CR"; break;
                }
                string t = (f.DiameterZone ? "%DI" : "") + f.Tolerance.ToString("0.0##", CultureInfo.GetCultureInfo("pt-BR"));
                return f.Datum == null ? $"{sym}|{t}" : $"{sym}|{t}|{f.Datum}";
            }

            /// <summary>Ponto na aresta da face (plano: a vertical; cilindro: a geratriz) para prender anotação.</summary>
            private bool AnchorOnFace(RevolvedFace face, bool below, out object target, out double x, out double y)
            {
                target = null; x = y = 0;
                if (face.Kind == RevolvedKind.Plane)
                {
                    DvLine v = _m.VerticalAt(face.Z0);
                    if (v == null) return false;
                    target = v.Ref; x = _m.SheetX(v.X1); y = _m.SheetY(below ? v.MinY : v.MaxY);
                    return true;
                }
                DvLine g = below ? _m.LowerGeneratrix(face.Z0, face.Z1, face.R0, face.R1) : _m.UpperGeneratrix(face.Z0, face.Z1, face.R0, face.R1);
                if (g == null) return false;
                target = g.Ref; x = _m.SheetX((g.X1 + g.X2) / 2); y = _m.SheetY((g.Y1 + g.Y2) / 2);
                return true;
            }

            /// <summary>
            /// Balão de propriedades na isométrica, como no desenho à mão: o texto é PROPERTY TEXT
            /// ("%{Massa|GP}" = propriedade da peça ligada ao balão), então acompanha a peça. Os nomes
            /// são os da instalação pt-BR da SE.
            /// </summary>
            public void PropertyBalloon()
            {
                if (IsoView == null) return;
                try
                {
                    dynamic iso = IsoView;
                    dynamic lines = iso.DVLines2d;
                    if ((int)lines.Count == 0) return;
                    // Ponta do balão na aresta mais à direita da isométrica (a ponta do eixo, como no desenho à mão).
                    object l = null; object[] a = null, b = null; double best = double.MinValue;
                    for (int i = 1; i <= (int)lines.Count; i++)
                    {
                        object li = lines.Item(i);
                        object[] ai = Call(li, "GetStartPoint", new object[] { 0.0, 0.0 }, 0, 1);
                        object[] bi = Call(li, "GetEndPoint", new object[] { 0.0, 0.0 }, 0, 1);
                        double mx = ((double)ai[0] + (double)bi[0]) / 2;
                        if (mx > best) { best = mx; l = li; a = ai; b = bi; }
                    }
                    object[] o = Call(IsoView, "GetOrigin", new object[] { 0.0, 0.0 }, 0, 1);
                    double sc = (double)iso.ScaleFactor;
                    double px = (double)o[0] + sc * ((double)a[0] + (double)b[0]) / 2;
                    double py = (double)o[1] + sc * ((double)a[1] + (double)b[1]) / 2;
                    // Texto logo à direita da isométrica, um pouco acima da ponta; a chamada vem por último
                    // (SetTerminator depois das propriedades — na ordem contrária a ponta se perde).
                    double textX = px + 0.02;
                    try
                    {
                        object[] rg = Call(IsoView, "Range", new object[] { 0.0, 0.0, 0.0, 0.0 }, 0, 1, 2, 3);
                        textX = Math.Max(textX, (double)rg[2] + 0.004);
                    }
                    catch { }
                    dynamic balloon = _sheet.Balloons.Add(textX, py + 0.017, 0.0);
                    TrySet(() => balloon.Leader = true);
                    TrySet(() => balloon.Callout = 1);
                    TrySet(() => balloon.BalloonType = 0);
                    balloon.BalloonText = "%{Nome do Arquivo|GP}\rMaterial: %{Material|GP}\rÁrea de superfície: %{Área de superfície|GP}" +
                                          "\rPeso: %{Massa|GP}\rVolume: %{Volume|GP}";
                    balloon.SetTerminator((object)((dynamic)l).Reference, px, py, 0.0, false);
                }
                catch (Exception ex) { _r.Failed.Add("balão de propriedades: " + Msg(ex)); }
            }

            // ---------------------------------------------------------------- conferência e tolerância

            /// <summary>
            /// A cota bate com o 3D? Senão tenta as inversões conhecidas; se nada der, apaga e relata.
            /// <paramref name="expectedMm"/> é comparado com <c>Value × 1000</c> (metros → mm; para
            /// ângulo o chamador já passa rad × 1000).
            /// </summary>
            private bool Check(object dimObj, double expectedMm, string what, Action<dynamic> fix = null, double? tolerance = null)
            {
                dynamic d = dimObj;
                double tol = tolerance ?? _o.CheckToleranceMm;
                double got = Value(d);
                if (Math.Abs(got - expectedMm) > tol && fix != null)
                {
                    try { fix(d); } catch { }
                    got = Value(d);
                }
                if (Math.Abs(got - expectedMm) <= tol) { _r.Created++; return true; }

                _r.Failed.Add($"{what}: a cota mediu {N(got)} e o 3D diz {N(expectedMm)} — apagada");
                try { d.Delete(); } catch { }
                return false;
            }

            private static double Value(dynamic d)
            {
                try { return (double)d.Value * 1000.0; } catch { return double.NaN; }
            }

            private void ApplyTolerance(object dimObj, FinishClass finish, bool external, bool withFit = true)
            {
                if (finish == null) return;
                dynamic d = dimObj;
                ToleranceBand band = finish.For(external);
                if (band == null) return; // "Verificar": sem número na tabela
                string fit = withFit ? finish.FitFor(external) : null;
                try
                {
                    d.DisplayType = fit != null ? DisplayHoleShaftWithTol : DisplayTolerance;
                    if (fit != null)
                    {
                        if (external) d.ShaftClassString = fit; else d.HoleClassString = fit;
                    }
                    d.PrimaryUpperTolerance = Tol(band.Upper);
                    d.PrimaryLowerTolerance = Tol(band.Lower);
                }
                catch (Exception ex) { _r.Failed.Add($"tolerância {band} ({finish.ColorName}): {Msg(ex)}"); }
            }

            private static void TrySet(Action a) { try { a(); } catch { } }

            private const double LeaderAngle = 0.61;   // rad, a chamada dos raios no desenho à mão

            /// <summary>
            /// Põe a cota a <paramref name="track"/> e confere pelos pontos-chave se caiu do lado
            /// certo; se não, inverte o sinal. A Solid Edge não documenta o sentido do TrackDistance
            /// e ele muda com a ordem das pegas — conferir é mais seguro que decorar.
            /// </summary>
            private static void PlaceAndVerify(object dimObj, double track, Func<object, bool> ok)
            {
                dynamic d = dimObj;
                try
                {
                    d.TrackDistance = track;
                    if (!ok(dimObj)) d.TrackDistance = -track;
                }
                catch { }
            }

            private static double KeyX(object dimObj, int i) => Key(dimObj, i)[0];
            private static double KeyY(object dimObj, int i) => Key(dimObj, i)[1];

            private static double[] Key(object dimObj, int i)
            {
                try
                {
                    object[] r = Call(dimObj, "GetKeyPoint", new object[] { i, 0.0, 0.0, 0.0, 0, 0 }, 1, 2, 3, 4, 5);
                    return new[] { (double)r[1], (double)r[2] };
                }
                catch { return new[] { double.NaN, double.NaN }; }
            }

            private const string StylePan = "PAN (mm)";
            private const string StylePanOneDecimal = "PAN (x,x mm)";
            private const string StylePanH = "PAN-H (mm)";

            /// <summary>Estilo de cota do template da casa; sem ele (outro template) fica o padrão.</summary>
            private static void SetStyle(object dimObj, string name)
            {
                try { ((dynamic)dimObj).Style.Name = name; } catch { }
            }

            /// <summary>Afastamento como a SE espera no campo de texto: "-0,01", e zero como "0".</summary>
            private static string Tol(double v) =>
                Math.Abs(v) < 1e-9 ? "0" : v.ToString("0.0##", CultureInfo.GetCultureInfo("pt-BR"));

        }

        // ======================================================================= utilitários

        internal static object[] Call(object obj, string method, object[] args, params int[] outIdx)
        {
            var pm = new ParameterModifier(args.Length);
            foreach (int i in outIdx) pm[i] = true;
            obj.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, obj, args,
                new[] { pm }, CultureInfo.InvariantCulture, null);
            return args;
        }

        private static string N(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double[] Mul(double[] a, double k) => new[] { a[0] * k, a[1] * k, a[2] * k };
        private static double[] Cross(double[] a, double[] b) =>
            new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double[] Normalize(double[] v)
        {
            double n = Math.Sqrt(Dot(v, v));
            return new[] { v[0] / n, v[1] / n, v[2] / n };
        }
    }
}
