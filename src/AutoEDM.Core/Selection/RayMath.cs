using System;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Raio de visão contra geometria — a conta que decide QUAL face está à vista sob o clique.
    /// Pura (sem COM) para caber em teste: o Solid Edge só fornece a malha e a câmera.
    ///
    /// Existe porque a localização da SE, dentro de um comando do add-in, não respeita a
    /// profundidade: com uma peça atrás da peça-alvo, o clique devolvia a face MAIOR de trás
    /// (Carlos, 2026-09-24), e o SmartLocate, que respeitaria, não entrega geometria nenhuma ao
    /// nosso comando. Então a profundidade é decidida aqui: primeiro impacto ao longo do raio.
    /// </summary>
    public static class RayMath
    {
        /// <summary>
        /// Raio × triângulo (Möller–Trumbore). Devolve a distância <c>t</c> ao longo de
        /// <paramref name="dir"/> (na unidade dele — normalizado, é distância real) ou
        /// <see cref="double.NaN"/> se não acerta ou acerta atrás da origem. Os dois lados do
        /// triângulo contam: a orientação da malha da SE não é garantida (a secção já aprendeu isso).
        /// </summary>
        public static double IntersectTriangle(double[] origin, double[] dir,
            double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz)
        {
            const double eps = 1e-14;
            double e1x = bx - ax, e1y = by - ay, e1z = bz - az;
            double e2x = cx - ax, e2y = cy - ay, e2z = cz - az;

            double px = dir[1] * e2z - dir[2] * e2y;
            double py = dir[2] * e2x - dir[0] * e2z;
            double pz = dir[0] * e2y - dir[1] * e2x;
            double det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < eps) return double.NaN;          // raio paralelo ao triângulo
            double inv = 1.0 / det;

            double sx = origin[0] - ax, sy = origin[1] - ay, sz = origin[2] - az;
            double u = (sx * px + sy * py + sz * pz) * inv;
            if (u < 0 || u > 1) return double.NaN;

            double qx = sy * e1z - sz * e1y;
            double qy = sz * e1x - sx * e1z;
            double qz = sx * e1y - sy * e1x;
            double v = (dir[0] * qx + dir[1] * qy + dir[2] * qz) * inv;
            if (v < 0 || u + v > 1) return double.NaN;

            double t = (e2x * qx + e2y * qy + e2z * qz) * inv;
            return t > 0 ? t : double.NaN;
        }

        /// <summary>
        /// Primeiro impacto numa malha (9 doubles por faceta, como o <c>GetFacetData</c> devolve).
        /// NaN se nenhuma faceta é atingida.
        /// </summary>
        public static double NearestHit(double[] origin, double[] dir, double[] facets)
        {
            double best = double.NaN;
            if (facets == null) return best;
            for (int i = 0; i + 8 < facets.Length; i += 9)
            {
                double t = IntersectTriangle(origin, dir,
                    facets[i], facets[i + 1], facets[i + 2],
                    facets[i + 3], facets[i + 4], facets[i + 5],
                    facets[i + 6], facets[i + 7], facets[i + 8]);
                if (!double.IsNaN(t) && (double.IsNaN(best) || t < best)) best = t;
            }
            return best;
        }

        /// <summary>
        /// O raio passa pela caixa (método das lajes), com folga <paramref name="pad"/> para os
        /// lados? Filtro barato antes de pedir malha à SE: a caixa de uma peça ou face que o raio
        /// nem cruza não pode conter o impacto.
        /// </summary>
        public static bool HitsBox(double[] origin, double[] dir, double[] min, double[] max, double pad = 0)
        {
            double tNear = double.NegativeInfinity, tFar = double.PositiveInfinity;
            for (int k = 0; k < 3; k++)
            {
                double lo = min[k] - pad, hi = max[k] + pad;
                if (Math.Abs(dir[k]) < 1e-15)
                {
                    if (origin[k] < lo || origin[k] > hi) return false;
                    continue;
                }
                double t1 = (lo - origin[k]) / dir[k];
                double t2 = (hi - origin[k]) / dir[k];
                if (t1 > t2) { double s = t1; t1 = t2; t2 = s; }
                if (t1 > tNear) tNear = t1;
                if (t2 < tFar) tFar = t2;
                if (tNear > tFar || tFar < 0) return false;
            }
            return true;
        }

        /// <summary>
        /// O raio de visão a partir da câmera da janela e do ponto clicado (tudo em metros, no
        /// espaço da montagem). Em PERSPECTIVA o raio sai do olho e passa pelo ponto; em
        /// ORTOGRÁFICA (o normal na SE) todos os raios são paralelos a olho→alvo, e a origem recua
        /// <paramref name="backOffM"/> atrás do ponto para ficar à frente de toda a geometria —
        /// qualquer que seja a profundidade em que a SE pôs o ponto do clique.
        /// Devolve false se a câmera for degenerada.
        /// </summary>
        public static bool TryViewRay(double[] eye, double[] target, bool perspective, double[] clickPoint,
            out double[] origin, out double[] dir, double backOffM = 100.0)
        {
            origin = null; dir = null;
            double[] d = perspective
                ? new[] { clickPoint[0] - eye[0], clickPoint[1] - eye[1], clickPoint[2] - eye[2] }
                : new[] { target[0] - eye[0], target[1] - eye[1], target[2] - eye[2] };
            double n = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (n < 1e-12) return false;
            dir = new[] { d[0] / n, d[1] / n, d[2] / n };
            origin = perspective
                ? (double[])eye.Clone()
                : new[] { clickPoint[0] - dir[0] * backOffM, clickPoint[1] - dir[1] * backOffM, clickPoint[2] - dir[2] * backOffM };
            return true;
        }
    }
}
