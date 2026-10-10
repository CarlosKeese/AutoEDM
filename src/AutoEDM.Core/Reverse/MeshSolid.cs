using System;

namespace AutoEDM.Reverse
{
    /// <summary>
    /// "Este ponto está dentro da peça?" respondido pela própria malha, sem kernel nenhum.
    ///
    /// Número de enrolamento generalizado: a soma dos ângulos sólidos com que cada triângulo é
    /// visto do ponto, dividida por 4π, dá 1 dentro de uma casca fechada e 0 fora. Diferente do
    /// raio contra triângulos, não tem caso de borda (raio passando por aresta ou vértice) e
    /// tolera furinho na malha — o número só fica fracionário perto do defeito. O sinal depende
    /// da orientação das normais, então o teste usa o módulo.
    /// </summary>
    public static class MeshSolid
    {
        /// <param name="pointsMm">Sopa de triângulos, 9 doubles por triângulo, em mm.</param>
        public static double Winding(double[] pointsMm, double x, double y, double z)
        {
            double sum = 0;
            for (int b = 0; b + 8 < pointsMm.Length; b += 9)
            {
                double ax = pointsMm[b] - x, ay = pointsMm[b + 1] - y, az = pointsMm[b + 2] - z;
                double bx = pointsMm[b + 3] - x, by = pointsMm[b + 4] - y, bz = pointsMm[b + 5] - z;
                double cx = pointsMm[b + 6] - x, cy = pointsMm[b + 7] - y, cz = pointsMm[b + 8] - z;

                double la = Math.Sqrt(ax * ax + ay * ay + az * az);
                double lb = Math.Sqrt(bx * bx + by * by + bz * bz);
                double lc = Math.Sqrt(cx * cx + cy * cy + cz * cz);
                if (la < 1e-12 || lb < 1e-12 || lc < 1e-12) continue;

                // Van Oosterom & Strackee: tan(Ω/2) = a·(b×c) / (|a||b||c| + (a·b)|c| + (a·c)|b| + (b·c)|a|).
                double det = ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
                double den = la * lb * lc
                             + (ax * bx + ay * by + az * bz) * lc
                             + (ax * cx + ay * cy + az * cz) * lb
                             + (bx * cx + by * cy + bz * cz) * la;
                sum += 2 * Math.Atan2(det, den);
            }
            return sum / (4 * Math.PI);
        }

        public static bool Contains(double[] pointsMm, double[] p)
        {
            return Math.Abs(Winding(pointsMm, p[0], p[1], p[2])) > 0.5;
        }
    }
}
