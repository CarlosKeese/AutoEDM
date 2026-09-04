using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Uma aresta ABERTA (laminar — pertence a UMA só face) da superfície de queima, já com a
    /// geometria lida do COM. É o insumo do encadeamento em contornos
    /// (<see cref="OpenEdgeLoops"/>); os testes montam estes objetos à mão, sem Solid Edge.
    /// </summary>
    public sealed class OpenEdgeSegment
    {
        /// <summary>A aresta COM (SolidEdgeGeometry.Edge). Null nos testes.</summary>
        public object Com;

        /// <summary>Extremidades da aresta (mm, sistema local da peça) — <c>Edge.GetEndPoints</c>.</summary>
        public double[] StartMm, EndMm;

        /// <summary>Extensão em Z da aresta (mm) — do bbox, não das extremidades (cobre arco curvo).</summary>
        public double ZMinMm, ZMaxMm;

        /// <summary>Z varia ao longo da aresta ⇒ é lateral (parede do vão X,Y), não rim horizontal.</summary>
        public bool IsVertical;
    }

    /// <summary>Um contorno formado por arestas abertas encadeadas ponta a ponta.</summary>
    public sealed class OpenEdgeLoop
    {
        public readonly List<OpenEdgeSegment> Segments = new List<OpenEdgeSegment>();

        /// <summary>O contorno voltou ao ponto de partida (fechado) — só assim vira patch.</summary>
        public bool Closed;

        /// <summary>Quantas arestas do contorno são verticais.</summary>
        public int VerticalCount;

        public double XMinMm, XMaxMm, YMinMm, YMaxMm, ZMinMm, ZMaxMm;

        /// <summary>
        /// É um VÃO LATERAL (X,Y) a fechar: contorno FECHADO com pelo menos uma aresta vertical.
        /// O rim de topo/fundo da superfície também é um contorno fechado, mas todo horizontal —
        /// fechá-lo TAPARIA a superfície de queima (justo o que não se quer), daí o filtro.
        /// </summary>
        public bool IsSideGap { get { return Closed && VerticalCount > 0; } }

        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} aresta(s) ({1} vertical(is)), {2}, X {3:0.0}→{4:0.0} Y {5:0.0}→{6:0.0} Z {7:0.0}→{8:0.0} mm",
                Segments.Count, VerticalCount, Closed ? "FECHADO" : "ABERTO",
                XMinMm, XMaxMm, YMinMm, YMaxMm, ZMinMm, ZMaxMm);
        }
    }

    /// <summary>
    /// Encadeia arestas abertas (laminares) em CONTORNOS — o passo que falta para automatizar o
    /// "Limite" que o Carlos faz à mão: o <c>Constructions.SurfaceByBoundaries.Add</c> recebe as
    /// arestas de UM contorno fechado por chamada, então não basta ter a lista solta de arestas
    /// abertas, é preciso saber quais formam cada vão.
    ///
    /// Lógica PURA (sem COM), separada como <see cref="Selection.SectionAreaCalculator"/> faz com
    /// a varredura — dá para testar sem Solid Edge instalado (<c>OpenEdgeLoopsTests</c>).
    /// </summary>
    public static class OpenEdgeLoops
    {
        /// <summary>Distância (mm) abaixo da qual duas extremidades são o MESMO ponto.</summary>
        public const double DefaultJoinToleranceMm = 0.01;

        /// <summary>
        /// Agrupa as arestas em contornos, seguindo ponta a ponta. Cada aresta entra em
        /// exatamente um contorno; a ORDEM e a ORIENTAÇÃO da lista de entrada não importam (uma
        /// aresta é invertida na hora de encadear se for o seu ponto final que encosta na ponta
        /// atual). Contornos que não voltam ao ponto de partida saem com
        /// <see cref="OpenEdgeLoop.Closed"/> = false — geometria que o patch não fecharia.
        /// </summary>
        public static List<OpenEdgeLoop> Chain(IReadOnlyList<OpenEdgeSegment> segments, double tolMm)
        {
            var loops = new List<OpenEdgeLoop>();
            if (segments == null || segments.Count == 0) return loops;
            if (tolMm <= 0) tolMm = DefaultJoinToleranceMm;

            bool[] used = new bool[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i]) continue;
                OpenEdgeSegment first = segments[i];
                if (first == null || first.StartMm == null || first.EndMm == null) { used[i] = true; continue; }

                used[i] = true;
                var loop = new OpenEdgeLoop();
                loop.Segments.Add(first);

                double[] loopStart = first.StartMm;
                double[] tip = first.EndMm;

                while (true)
                {
                    // Uma aresta pode ser fechada em si (círculo): início == fim já fecha o contorno.
                    if (Near(tip, loopStart, tolMm)) { loop.Closed = true; break; }

                    int best = -1;
                    bool flip = false;
                    double bestDist = tolMm;
                    for (int j = 0; j < segments.Count; j++)
                    {
                        if (used[j]) continue;
                        OpenEdgeSegment s = segments[j];
                        if (s == null || s.StartMm == null || s.EndMm == null) continue;

                        double d1 = Dist(tip, s.StartMm);
                        if (d1 <= bestDist) { best = j; flip = false; bestDist = d1; }
                        double d2 = Dist(tip, s.EndMm);
                        if (d2 < bestDist) { best = j; flip = true; bestDist = d2; }
                    }
                    if (best < 0) break; // ponta solta: contorno não fecha

                    used[best] = true;
                    OpenEdgeSegment next = segments[best];
                    loop.Segments.Add(next);
                    tip = flip ? next.StartMm : next.EndMm;
                }

                Summarize(loop);
                loops.Add(loop);
            }
            return loops;
        }

        private static void Summarize(OpenEdgeLoop loop)
        {
            double xmn = double.MaxValue, xmx = double.MinValue;
            double ymn = double.MaxValue, ymx = double.MinValue;
            double zmn = double.MaxValue, zmx = double.MinValue;
            int vert = 0;
            foreach (OpenEdgeSegment s in loop.Segments)
            {
                if (s.IsVertical) vert++;
                foreach (double[] p in new[] { s.StartMm, s.EndMm })
                {
                    if (p == null || p.Length < 3) continue;
                    if (p[0] < xmn) xmn = p[0];
                    if (p[0] > xmx) xmx = p[0];
                    if (p[1] < ymn) ymn = p[1];
                    if (p[1] > ymx) ymx = p[1];
                }
                if (s.ZMinMm < zmn) zmn = s.ZMinMm;
                if (s.ZMaxMm > zmx) zmx = s.ZMaxMm;
            }
            loop.VerticalCount = vert;
            loop.XMinMm = xmn; loop.XMaxMm = xmx;
            loop.YMinMm = ymn; loop.YMaxMm = ymx;
            loop.ZMinMm = zmn; loop.ZMaxMm = zmx;
        }

        private static bool Near(double[] a, double[] b, double tolMm)
        {
            return Dist(a, b) <= tolMm;
        }

        private static double Dist(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length < 3 || b.Length < 3) return double.MaxValue;
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
