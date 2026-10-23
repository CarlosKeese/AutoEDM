using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using AutoEDM.Mold.MoldBase;

namespace AutoEDM.AddIn.UI
{
    /// <summary>
    /// O porta-molde desenhado na janela (Carlos, 2026-10-23: "a janela precisa ser mais visual, mostrando partes
    /// do porta-molde e configurações conforme as caixas de seleção"). À esquerda o CORTE (largura A × altura, com
    /// a partição, as peças, os componentes e o curso); à direita a PLANTA (A × C, área útil, peças, componentes e
    /// furos). Passar o mouse sobre uma placa mostra nome e medidas. Só desenha o plano — não fala com o CAD.
    /// </summary>
    public sealed class MoldBaseView : Control
    {
        private MoldBasePlan _plan;
        private double[] _box;   // peças no referencial do molde: a0, a1, c0, c1, w0, w1
        private readonly List<KeyValuePair<RectangleF, string>> _hits = new List<KeyValuePair<RectangleF, string>>();
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 150, ReshowDelay = 50 };
        private string _hover;

        private static readonly Color Fixed = Color.FromArgb(173, 205, 235);
        private static readonly Color Moving = Color.FromArgb(190, 220, 180);
        private static readonly Color Ejector = Color.FromArgb(245, 200, 140);
        private static readonly Color Base = Color.FromArgb(205, 205, 205);
        private static readonly Color Parts = Color.FromArgb(220, 60, 60);
        private static readonly Color Comp = Color.FromArgb(70, 70, 90);

        public MoldBaseView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 8F);
        }

        /// <summary>O plano a desenhar e a caixa das peças (no referencial de planta da montagem, como sai do construtor).</summary>
        public void Show(MoldBasePlan plan, MoldBaseContent content)
        {
            _plan = plan;
            _box = null;
            if (plan != null && plan.Ready && content != null && content.Count > 0)
            {
                double a0 = plan.Rotated ? content.Min2 - plan.Center2 : content.Min1 - plan.Center1;
                double a1 = plan.Rotated ? content.Max2 - plan.Center2 : content.Max1 - plan.Center1;
                double c0 = plan.Rotated ? content.Min1 - plan.Center1 : content.Min2 - plan.Center2;
                double c1 = plan.Rotated ? content.Max1 - plan.Center1 : content.Max2 - plan.Center2;
                _box = new[] { a0, a1, c0, c1, content.MinW, content.MaxW };
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            g.DrawRectangle(SystemPens.ControlDark, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            _hits.Clear();
            if (_plan == null || !_plan.Ready)
            {
                TextRenderer.DrawText(g, _plan?.Problem ?? "Sem plano.", Font, ClientRectangle, Color.DarkRed,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            int gap = 16, w = ClientSize.Width, h = ClientSize.Height;
            var section = new RectangleF(8, 22, (w - gap) * 0.55f - 8, h - 30);
            var planRect = new RectangleF(section.Right + gap, 22, w - section.Right - gap - 8, h - 30);
            TextRenderer.DrawText(g, "Corte (largura × altura)", Font, new Point((int)section.X, 4), Color.DimGray);
            TextRenderer.DrawText(g, "Planta (largura × comprimento)", Font, new Point((int)planRect.X, 4), Color.DimGray);
            DrawSection(g, section);
            DrawPlan(g, planRect);
        }

        // ------------------------------------------------------------------ corte

        private void DrawSection(Graphics g, RectangleF r)
        {
            double halfA = _plan.Plates.Max(p => p.Width) / 2;
            double top = _plan.Plates.Max(p => p.TopW + MoldBasePlateModeler.AboveTop(p));
            double bottom = _plan.Plates.Min(p => p.BottomW - MoldBasePlateModeler.BelowBottom(p));
            foreach (ComponentSpec c in _plan.Components)
                foreach (double[] pos in c.Positions) { top = Math.Max(top, pos[2] + c.MaxZ); bottom = Math.Min(bottom, pos[2] + c.MinZ); }
            float labels = 92, left = 48;
            float sx = (float)((r.Width - labels - left) / (2 * halfA)), sy = (float)(r.Height / (top - bottom));
            float s = Math.Min(sx, sy);
            float cx = r.X + left + (r.Width - labels - left) / 2, cy = r.Y + (float)(top * s) + (r.Height - (float)((top - bottom) * s)) / 2;
            PointF P(double a, double z) => new PointF(cx + (float)(a * s), cy - (float)(z * s));

            foreach (PlateSpec p in _plan.Plates)
            {
                Color fill = p.Plate == MoldPlate.CPE || p.Plate == MoldPlate.PE ? Ejector
                           : p.TopW <= 1e-6 ? (p.Plate == MoldPlate.PBI ? Base : Moving)
                           : p.Plate == MoldPlate.PBS ? Base : Fixed;
                for (int k = 0; k < p.Count; k++)
                {
                    double off = p.Count > 1 ? (k == 0 ? -p.OffsetA : p.OffsetA) : 0;
                    PointF a = P(off - p.Width / 2, p.TopW), b = P(off + p.Width / 2, p.BottomW);
                    var rect = new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y);
                    using (var br = new SolidBrush(_hover == Key(p) ? ControlPaint.Light(fill, 0.4f) : fill)) g.FillRectangle(br, rect);
                    g.DrawRectangle(Pens.Gray, rect.X, rect.Y, rect.Width, rect.Height);
                    if (p.Pocket != null)
                    {
                        double z0 = p.Pocket.FromBottom ? p.BottomW : p.TopW - p.Pocket.Depth;
                        PointF pa = P(off - p.Pocket.Width / 2, z0 + p.Pocket.Depth), pb = P(off + p.Pocket.Width / 2, z0);
                        g.FillRectangle(Brushes.White, pa.X, pa.Y, pb.X - pa.X, pb.Y - pa.Y);
                        g.DrawRectangle(Pens.Gray, pa.X, pa.Y, pb.X - pa.X, pb.Y - pa.Y);
                    }
                    if (p.Boss != null)
                    {
                        double z0 = p.Boss.OnBottom ? p.BottomW - p.Boss.Height : p.TopW;
                        PointF ba = P(off - p.Boss.Width / 2, z0 + p.Boss.Height), bb = P(off + p.Boss.Width / 2, z0);
                        using (var br = new SolidBrush(fill)) g.FillRectangle(br, ba.X, ba.Y, bb.X - ba.X, bb.Y - ba.Y);
                        g.DrawRectangle(Pens.Gray, ba.X, ba.Y, bb.X - ba.X, bb.Y - ba.Y);
                    }
                    if (p.Corners != null)
                        foreach (int side in new[] { -1, 1 })
                        {
                            CornerSpec c = p.Corners;
                            double a0 = off + side * (p.Width / 2 - c.Size), a1 = off + side * p.Width / 2;
                            double z0 = c.Recess ? (c.OnBottom ? p.BottomW : p.TopW - c.Height) : (c.OnBottom ? p.BottomW - c.Height : p.TopW);
                            PointF ca = P(Math.Min(a0, a1), z0 + c.Height), cb = P(Math.Max(a0, a1), z0);
                            using (var br = new SolidBrush(c.Recess ? Color.White : fill)) g.FillRectangle(br, ca.X, ca.Y, cb.X - ca.X, cb.Y - ca.Y);
                            g.DrawRectangle(Pens.Gray, ca.X, ca.Y, cb.X - ca.X, cb.Y - ca.Y);
                        }
                    if (p.Step != null)
                        foreach (int side in new[] { -1, 1 })
                        {
                            double a0 = off + side * (p.Width / 2 - p.Step.Inset), a1 = off + side * p.Width / 2;
                            double z0 = p.Step.FromBottom ? p.BottomW : p.TopW - p.Step.Depth;
                            PointF sa = P(Math.Min(a0, a1), z0 + p.Step.Depth), sb = P(Math.Max(a0, a1), z0);
                            g.FillRectangle(Brushes.White, sa.X, sa.Y, Math.Max(1, sb.X - sa.X), sb.Y - sa.Y);
                        }
                    if (p.Groove != null)
                        foreach (int side in new[] { -1, 1 })
                        {
                            double a0 = off + side * (p.Width / 2 - p.Groove.Depth), a1 = off + side * p.Width / 2;
                            double z1 = Math.Min(p.Thickness, p.Groove.Z1);
                            PointF ga = P(Math.Min(a0, a1), p.BottomW + z1), gb = P(Math.Max(a0, a1), p.BottomW + p.Groove.Z0);
                            g.FillRectangle(Brushes.White, ga.X, ga.Y, gb.X - ga.X, gb.Y - ga.Y);
                            g.DrawRectangle(Pens.Gray, ga.X, ga.Y, gb.X - ga.X, gb.Y - ga.Y);
                        }
                    _hits.Add(new KeyValuePair<RectangleF, string>(rect, Key(p)));
                }
                // Rótulo à direita; o dos espaçadores à esquerda (eles dividem a altura com as extratoras).
                string text = $"{p.Code ?? p.Plate.ToString()} {F(p.Thickness)}";
                if (p.Thickness * s >= 9)
                {
                    if (p.Plate == MoldPlate.ESP)
                    {
                        PointF l = P(-halfA, (p.TopW + p.BottomW) / 2);
                        int tw = TextRenderer.MeasureText(text, Font).Width;
                        TextRenderer.DrawText(g, text, Font, new Point((int)l.X - tw - 4, (int)l.Y - 7), Color.Black);
                    }
                    else
                    {
                        PointF lab = P(halfA, (p.TopW + p.BottomW) / 2);
                        TextRenderer.DrawText(g, text, Font, new Point((int)lab.X + 6, (int)lab.Y - 7), Color.Black);
                    }
                }
            }

            if (_box != null)
                using (var hb = new HatchBrush(HatchStyle.BackwardDiagonal, Parts, Color.Transparent))
                using (var pen = new Pen(Parts, 1.5f))
                {
                    PointF a = P(_box[0], _box[5]), b = P(_box[1], _box[4]);
                    g.FillRectangle(hb, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                    g.DrawRectangle(pen, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                }

            // Componentes: os que estão no lado −C, vistos de frente (corte no meio de cada um).
            foreach (ComponentSpec c in _plan.Components)
                foreach (double[] pos in c.Positions.Where(q => q[1] <= 0).GroupBy(q => Math.Round(q[0], 1)).Select(q => q.First()))
                {
                    foreach (RoundSegment seg in c.Solids)
                    {
                        PointF a = P(pos[0] - seg.Diameter / 2, pos[2] + seg.Z1), b = P(pos[0] + seg.Diameter / 2, pos[2] + seg.Z0);
                        using (var br = new SolidBrush(Color.FromArgb(150, Comp))) g.FillRectangle(br, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                    }
                    if (c.Bore != null)
                    {
                        PointF a = P(pos[0] - c.Bore.Diameter / 2, pos[2] + c.Bore.Z1), b = P(pos[0] + c.Bore.Diameter / 2, pos[2] + c.Bore.Z0);
                        g.FillRectangle(Brushes.White, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                    }
                }

            // Guias da gaveta (biblioteca): nos rasgos, saindo 9 mm da unidade para o canal do quadro.
            if (_plan.LibraryParts.Any(x => x.FileName.StartsWith("8760.001", StringComparison.OrdinalIgnoreCase)))
                foreach (double[] band in new[] { MoldBaseStandards.MudFixedGroove, MoldBaseStandards.MudMovingGroove })
                    foreach (int side in new[] { -1, 1 })
                    {
                        double a0 = side * (halfA - MoldBaseStandards.MudGrooveDepth), a1 = side * (halfA + 9);
                        PointF ra = P(Math.Min(a0, a1), band[1]), rb = P(Math.Max(a0, a1), band[0]);
                        using (var br = new SolidBrush(Color.FromArgb(200, Comp))) g.FillRectangle(br, ra.X, ra.Y, rb.X - ra.X, rb.Y - ra.Y);
                    }
            if (_plan.LibraryParts.Count > 0)
                TextRenderer.DrawText(g, $"+ {_plan.LibraryParts.Count} peça(s) de biblioteca", Font,
                    new Point((int)r.X + TextRenderer.MeasureText("Corte (largura × altura)", Font).Width + 12, 4), Color.DimGray);

            using (var pl = new Pen(Color.Red, 1.2f) { DashStyle = DashStyle.Dash })
            {
                PointF a = P(-halfA - 6, 0), b = P(halfA + 6, 0);
                g.DrawLine(pl, a, b);
                TextRenderer.DrawText(g, "partição", Font, new Point((int)b.X + 2, (int)b.Y - 14), Color.Red);
            }

            // Curso: do topo da porta-extratores até a placa de cima dela (suporte / apoio / fundo do alojamento).
            PlateSpec cpe = _plan.Plates.FirstOrDefault(p => p.Plate == MoldPlate.CPE);
            if (cpe != null && _plan.Stroke != null)
            {
                double from = cpe.TopW, to = from + _plan.Stroke.Stroke;
                PointF a = P(cpe.Width / 2 - 8, from), b = P(cpe.Width / 2 - 8, to);
                using (var pen = new Pen(Color.DarkOrange, 1.6f) { CustomEndCap = new AdjustableArrowCap(3, 4) })
                    g.DrawLine(pen, a, b);
                TextRenderer.DrawText(g, $"curso {F(_plan.Stroke.Stroke)}", Font, new Point((int)a.X - 70, (int)((a.Y + b.Y) / 2) - 7), Color.DarkOrange);
            }
        }

        // ------------------------------------------------------------------ planta

        private void DrawPlan(Graphics g, RectangleF r)
        {
            double halfA = _plan.Plates.Max(p => p.Width) / 2, halfC = _plan.Plates.Max(p => p.Length) / 2;
            float s = (float)Math.Min(r.Width / (2 * halfA + 10), (r.Height - 18) / (2 * halfC + 10));
            float cx = r.X + r.Width / 2, cy = r.Y + (r.Height - 18) / 2;
            PointF P(double a, double c) => new PointF(cx + (float)(a * s), cy - (float)(c * s));
            RectangleF Rect(double a0, double a1, double c0, double c1) { PointF p = P(a0, c1), q = P(a1, c0); return new RectangleF(p.X, p.Y, q.X - p.X, q.Y - p.Y); }

            PlateSpec widest = _plan.Plates.OrderByDescending(p => p.Width * p.Length).First();
            PlateSpec cavity = _plan.Plates.First(p => p.Plate == MoldPlate.P1);
            RectangleF outer = Rect(-widest.Width / 2, widest.Width / 2, -widest.Length / 2, widest.Length / 2);
            using (var br = new SolidBrush(Base)) g.FillRectangle(br, outer);
            g.DrawRectangle(Pens.Gray, outer.X, outer.Y, outer.Width, outer.Height);
            RectangleF cav = Rect(-cavity.Width / 2, cavity.Width / 2, -cavity.Length / 2, cavity.Length / 2);
            using (var br = new SolidBrush(_hover == Key(cavity) ? ControlPaint.Light(Fixed, 0.4f) : Fixed)) g.FillRectangle(br, cav);
            g.DrawRectangle(Pens.Gray, cav.X, cav.Y, cav.Width, cav.Height);
            _hits.Add(new KeyValuePair<RectangleF, string>(cav, Key(cavity)));

            // Área útil (Polimold) / margem pedida.
            PolimoldSeries s0 = _plan.Series;
            using (var pen = new Pen(Color.SteelBlue, 1f) { DashStyle = DashStyle.Dash })
            {
                RectangleF ua = Rect(-s0.X / 2, s0.X / 2, -s0.Y / 2, s0.Y / 2);
                g.DrawRectangle(pen, ua.X, ua.Y, ua.Width, ua.Height);
            }
            using (var pen = new Pen(Color.FromArgb(160, Parts), 1f) { DashStyle = DashStyle.Dot })
            {
                RectangleF need = Rect(-_plan.NeededX / 2, _plan.NeededX / 2, -_plan.NeededY / 2, _plan.NeededY / 2);
                g.DrawRectangle(pen, need.X, need.Y, need.Width, need.Height);
            }
            if (_box != null)
                using (var hb = new HatchBrush(HatchStyle.BackwardDiagonal, Parts, Color.Transparent))
                using (var pen = new Pen(Parts, 1.5f))
                {
                    RectangleF b = Rect(_box[0], _box[1], _box[2], _box[3]);
                    g.FillRectangle(hb, b);
                    g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height);
                }

            // Furos das placas cavidade (parafusos, guias…), finos; componentes por cima, cheios.
            foreach (HoleSpec hs in _plan.Plates.Where(p => p.Plate == MoldPlate.P1 || p.Plate == MoldPlate.P2 || p.Plate == MoldPlate.PS).SelectMany(p => p.Holes))
                foreach (double[] c in MoldBasePlateModeler.HolePositions(hs))
                {
                    PointF p = P(c[0], c[1]);
                    float rr = (float)(hs.Diameter / 2 * s);
                    g.DrawEllipse(Pens.DimGray, p.X - rr, p.Y - rr, 2 * rr, 2 * rr);
                }
            foreach (ComponentSpec comp in _plan.Components)
                foreach (double[] c in comp.Positions)
                {
                    PointF p = P(c[0], c[1]);
                    float rr = (float)(comp.MaxDiameter / 2 * s);
                    using (var br = new SolidBrush(comp.Kind == "Pino de retorno" ? Color.FromArgb(200, Color.DarkOrange) : Color.FromArgb(200, Comp)))
                        g.FillEllipse(br, p.X - rr, p.Y - rr, 2 * rr, 2 * rr);
                }
            // Travas de canto (Babyplast) e guias da gaveta (biblioteca), na planta.
            if (cavity.Corners != null)
                foreach (int sa in new[] { -1, 1 })
                    foreach (int sc in new[] { -1, 1 })
                    {
                        double z = cavity.Corners.Size;
                        RectangleF k = Rect(Math.Min(sa * cavity.Width / 2, sa * (cavity.Width / 2 - z)), Math.Max(sa * cavity.Width / 2, sa * (cavity.Width / 2 - z)),
                                            Math.Min(sc * cavity.Length / 2, sc * (cavity.Length / 2 - z)), Math.Max(sc * cavity.Length / 2, sc * (cavity.Length / 2 - z)));
                        using (var br = new SolidBrush(Color.FromArgb(200, Comp))) g.FillRectangle(br, k);
                    }
            bool rails = _plan.LibraryParts.Any(x => x.FileName.StartsWith("8760.001", StringComparison.OrdinalIgnoreCase));
            if (rails)
                foreach (int sa in new[] { -1, 1 })
                {
                    double a0 = sa * (cavity.Width / 2 - MoldBaseStandards.MudGrooveDepth), a1 = sa * (cavity.Width / 2 + 9);
                    using (var br = new SolidBrush(Color.FromArgb(200, Comp)))
                        g.FillRectangle(br, Rect(Math.Min(a0, a1), Math.Max(a0, a1), -cavity.Length / 2, cavity.Length / 2));
                }

            // Legenda.
            float ly = r.Bottom - 14, lx = r.X;
            Legend(g, ref lx, ly, Parts, "peças");
            if (_plan.Components.Any(c => c.Kind != "Pino de retorno") || cavity.Corners != null || rails)
                Legend(g, ref lx, ly, Comp, _plan.Family == MoldBaseFamily.Polimold ? "guias" : _plan.Family == MoldBaseFamily.Mud ? "guias da gaveta" : "travas de canto");
            else if (_plan.Family == MoldBaseFamily.Zamak)
                Legend(g, ref lx, ly, Color.DimGray, "furos");
            if (_plan.Components.Any(c => c.Kind == "Pino de retorno")) Legend(g, ref lx, ly, Color.DarkOrange, "pinos de retorno");
            using (var pen = new Pen(Color.SteelBlue) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, lx, ly + 6, lx + 14, ly + 6);
            TextRenderer.DrawText(g, "área útil", Font, new Point((int)lx + 16, (int)ly), Color.Black);
        }

        private void Legend(Graphics g, ref float x, float y, Color c, string text)
        {
            using (var br = new SolidBrush(c)) g.FillRectangle(br, x, y + 2, 10, 9);
            TextRenderer.DrawText(g, text, Font, new Point((int)x + 12, (int)y), Color.Black);
            x += 16 + TextRenderer.MeasureText(text, Font).Width;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string hit = _hits.LastOrDefault(h => h.Key.Contains(e.Location)).Value;
            if (hit == _hover) return;
            _hover = hit;
            PlateSpec p = hit == null ? null : _plan?.Plates.FirstOrDefault(x => Key(x) == hit);
            _tip.SetToolTip(this, p == null ? "" :
                $"{p.Code ?? p.Plate.ToString()} — {p.Title}\n{F(p.Width)} × {F(p.Length)} × {F(p.Thickness)}" +
                (p.Holes.Count > 0 ? "\n" + string.Join("\n", p.Holes.Select(h => $"{h.Group}: {h.Count}× Ø{F(h.Diameter)}")) : "") +
                (p.Pocket != null ? $"\nalojamento {F(p.Pocket.Width)} × {F(p.Pocket.Length)} × {F(p.Pocket.Depth)}" : "") +
                (p.Boss != null ? $"\nressalto {F(p.Boss.Width)} × {F(p.Boss.Length)} × {F(p.Boss.Height)}" : "") +
                (p.Corners != null ? $"\n{(p.Corners.Recess ? "rebaixos" : "travas")} de canto {F(p.Corners.Size)} × {F(p.Corners.Height)}" : "") +
                (p.Groove != null ? $"\nrasgos das guias {F(p.Groove.Depth)} em {F(p.Groove.Z0)}..{F(p.Groove.Z1)}" : "") +
                (p.Step != null ? $"\ndegrau {F(p.Step.Inset)} por lado nos {F(p.Step.Depth)} {(p.Step.FromBottom ? "de baixo" : "de cima")}" : ""));
            Invalidate();
        }

        private static string Key(PlateSpec p) => p.Plate + "|" + p.Title;
        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
