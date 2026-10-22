using System;
using System.Collections.Generic;

namespace AutoEDM.Electrode
{
    /// <summary>Um trecho da curva de seção, já em coordenadas da peça do eletrodo (mm).</summary>
    public sealed class SectionEdge
    {
        public double[] StartMm;
        public double[] EndMm;
        public double[] MinMm;
        public double[] MaxMm;
        /// <summary>A aresta COM de onde o trecho saiu (null nos testes).</summary>
        public object Com;
    }

    /// <summary>
    /// A conta do ELETRODO DE INTERPOLAÇÃO (Carlos, 2026-10-22), sem COM, para ser testada.
    ///
    /// O eletrodo é redondo: gira no próprio eixo e percorre o perímetro da peça para erodir uma
    /// região NEGATIVA (rebaixo na parede). O perfil sai da seção da superfície escolhida por um
    /// plano VERTICAL que contém a normal da face; esse perfil é revolvido em torno de um eixo Z
    /// afastado dele, e o afastamento vem do Ø informado.
    ///
    /// Referencial da peça do eletrodo: X = componente horizontal da normal da face (do material
    /// para o vazio), Z = Z da montagem, Y = Z × X. O plano da seção é então o XZ da peça. A
    /// origem fica no EIXO de revolução, na menor altura da superfície — o perfil fica em x &lt; 0.
    /// </summary>
    public static class InterpolationGeometry
    {
        /// <summary>
        /// Abaixo disto a face é quase horizontal (fundo ou teto): a normal não diz para que lado
        /// o eletrodo trabalha, e o plano da seção ficaria indefinido.
        /// </summary>
        public const double MinHorizontalNormal = 0.2;

        /// <summary>Tolerância para dois trechos da seção se tocarem (mm).</summary>
        public const double JoinToleranceMm = 0.01;

        /// <summary>
        /// O X da peça: a normal projetada no plano XY e normalizada. False quando a face é quase
        /// horizontal (<see cref="MinHorizontalNormal"/>).
        /// </summary>
        public static bool TryHorizontalAxis(double[] normal, out double[] xAxis)
        {
            xAxis = null;
            if (normal == null || normal.Length < 3) return false;
            double len = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            if (len < 1e-12) return false;
            double hx = normal[0] / len, hy = normal[1] / len;
            double h = Math.Sqrt(hx * hx + hy * hy);
            if (h < MinHorizontalNormal) return false;
            xAxis = new[] { hx / h, hy / h, 0.0 };
            return true;
        }

        /// <summary>
        /// Rotação linha-a-linha (convenção de <see cref="AutoEDM.Assembly.OccurrenceTransform"/>,
        /// p_montagem = R·p_local) cujas COLUNAS são X = <paramref name="xAxis"/>, Y = Z × X, Z = Z.
        /// </summary>
        public static double[] RotationRows(double[] xAxis)
        {
            double xx = xAxis[0], xy = xAxis[1];
            double yx = -xy, yy = xx;       // Z × X com Z = (0,0,1) e X horizontal
            return new[]
            {
                xx, yx, 0.0,
                xy, yy, 0.0,
                0.0, 0.0, 1.0
            };
        }

        /// <summary>
        /// Onde fica o eixo, no X da peça, para o Ø pedido: o ponto mais fundo do perfil (o de
        /// menor X, mais dentro do material) fica a Ø/2 do eixo.
        /// </summary>
        public static double AxisXmm(double profileMinXmm, double diameterMm) => profileMinXmm + diameterMm / 2.0;

        /// <summary>
        /// O Ø serve? O perfil inteiro tem de ficar do mesmo lado do eixo, senão a revolução se
        /// cruza. Devolve null quando serve, ou a explicação com o Ø mínimo.
        /// </summary>
        public static string CheckDiameter(double profileMinXmm, double profileMaxXmm, double diameterMm)
        {
            if (!(diameterMm > 0)) return "Informe um diâmetro maior que zero.";
            double width = profileMaxXmm - profileMinXmm;
            double minDiameter = 2.0 * width;
            if (diameterMm / 2.0 <= width + JoinToleranceMm)
                return $"Ø {diameterMm:0.00} mm é pequeno demais: o perfil tem {width:0.00} mm de profundidade " +
                       $"na direção da normal, então o eixo cairia dentro dele. Use Ø maior que {minDiameter:0.00} mm.";
            return null;
        }

        /// <summary>
        /// Os trechos da seção agrupados em cadeias conectadas (índices em <paramref name="edges"/>).
        /// Um plano que passa pelo eixo de um furo corta o rebaixo DOS DOIS LADOS: são duas cadeias,
        /// e só uma é a do lado escolhido.
        /// </summary>
        public static List<List<int>> Chains(IList<SectionEdge> edges, double tolMm = JoinToleranceMm)
        {
            int n = edges.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Touch(edges[i], edges[j], tolMm)) parent[Find(i)] = Find(j);

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out List<int> g)) groups[r] = g = new List<int>();
                g.Add(i);
            }
            return new List<List<int>>(groups.Values);
        }

        /// <summary>A cadeia mais perto do ponto de referência (o ponto da face onde a normal foi lida).</summary>
        public static List<int> NearestChain(IList<List<int>> chains, IList<SectionEdge> edges, double[] refPointMm)
        {
            List<int> best = null;
            double bestD = double.MaxValue;
            foreach (List<int> chain in chains)
                foreach (int i in chain)
                {
                    double d = BoxDistance(edges[i], refPointMm);
                    if (d < bestD) { bestD = d; best = chain; }
                }
            return best;
        }

        /// <summary>
        /// As duas pontas LIVRES da cadeia (as que não encostam em outro trecho), a de cima
        /// primeiro. False se a cadeia for fechada ou ramificada — aí não há como ligá-la ao eixo.
        /// </summary>
        public static bool TryOpenEnds(IList<SectionEdge> edges, IList<int> chain, out double[] top, out double[] bottom,
            double tolMm = JoinToleranceMm)
        {
            top = bottom = null;
            var free = new List<double[]>();
            foreach (int i in chain)
                foreach (double[] p in new[] { edges[i].StartMm, edges[i].EndMm })
                {
                    int touching = 0;
                    foreach (int j in chain)
                    {
                        if (Near(p, edges[j].StartMm, tolMm)) touching++;
                        if (Near(p, edges[j].EndMm, tolMm)) touching++;
                    }
                    if (touching == 1) free.Add(p);   // só ele mesmo
                }
            if (free.Count != 2) return false;
            if (free[0][2] >= free[1][2]) { top = free[0]; bottom = free[1]; }
            else { top = free[1]; bottom = free[0]; }
            return true;
        }

        /// <summary>Caixa da cadeia: [0] = mínimo, [1] = máximo (mm).</summary>
        public static double[][] ChainBox(IList<SectionEdge> edges, IList<int> chain)
        {
            var mn = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var mx = new[] { double.MinValue, double.MinValue, double.MinValue };
            foreach (int i in chain)
                for (int k = 0; k < 3; k++)
                {
                    mn[k] = Math.Min(mn[k], Math.Min(edges[i].MinMm[k], Math.Min(edges[i].StartMm[k], edges[i].EndMm[k])));
                    mx[k] = Math.Max(mx[k], Math.Max(edges[i].MaxMm[k], Math.Max(edges[i].StartMm[k], edges[i].EndMm[k])));
                }
            return new[] { mn, mx };
        }

        /// <summary>
        /// O fechamento do perfil até o eixo (x = <paramref name="axisXmm"/>, y = 0): da ponta de cima
        /// na horizontal até o eixo, descendo pelo eixo, e de volta na horizontal até a ponta de
        /// baixo. Quatro pontos, três segmentos — o segmento do meio é o próprio eixo de revolução.
        /// </summary>
        public static double[][] ClosingPoints(double[] top, double[] bottom, double axisXmm = 0.0) => new[]
        {
            top,
            new[] { axisXmm, 0.0, top[2] },
            new[] { axisXmm, 0.0, bottom[2] },
            bottom
        };

        private static bool Touch(SectionEdge a, SectionEdge b, double tol) =>
            Near(a.StartMm, b.StartMm, tol) || Near(a.StartMm, b.EndMm, tol) ||
            Near(a.EndMm, b.StartMm, tol) || Near(a.EndMm, b.EndMm, tol);

        private static bool Near(double[] a, double[] b, double tol)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return dx * dx + dy * dy + dz * dz <= tol * tol;
        }

        private static double BoxDistance(SectionEdge e, double[] p)
        {
            double s = 0;
            for (int k = 0; k < 3; k++)
            {
                double lo = Math.Min(e.MinMm[k], Math.Min(e.StartMm[k], e.EndMm[k]));
                double hi = Math.Max(e.MaxMm[k], Math.Max(e.StartMm[k], e.EndMm[k]));
                double d = p[k] < lo ? lo - p[k] : p[k] > hi ? p[k] - hi : 0.0;
                s += d * d;
            }
            return Math.Sqrt(s);
        }
    }
}
