using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Leitura de geometria de face via COM (late-bound). A API de geometria do
    /// Solid Edge trabalha em METROS; aqui convertemos para mm.
    /// </summary>
    public static class FaceGeometry
    {
        private static bool _diagLogged;

        /// <summary>
        /// Bounding box (AABB) da face no sistema local da peça, em mm. Tenta
        /// GetRange e, como alternativa, GetExactRange. Best-effort.
        ///
        /// GetRange/GetExactRange existem na Face (confirmado no dump da typelib,
        /// cParams=2) e devolvem os cantos por PARÂMETRO DE SAÍDA (SAFEARRAY de 3
        /// doubles). Em late binding é preciso marcar os args como by-ref com um
        /// <see cref="ParameterModifier"/>; sem isso o InvokeMember não popula os
        /// [out] e a leitura volta vazia (era o bug do Log 8/9).
        /// </summary>
        public static bool TryGetRangeMm(object comFace, out double[] minMm, out double[] maxMm)
        {
            string err1 = null, err2 = null, err3 = null;
            if (TryRange(comFace, "GetRange", out minMm, out maxMm, ref err1)) return true;
            if (TryRange(comFace, "GetExactRange", out minMm, out maxMm, ref err2)) return true;
            // Fallback (sugestão do Gemini): se o binder COM não popular o [out] do
            // range, varre os vértices da face (Vertex.GetPointData) e monta a AABB.
            // Cobre curvas de contorno; basta para agrupar detalhes por proximidade.
            if (TryRangeFromVertices(comFace, out minMm, out maxMm, ref err3)) return true;

            if (!_diagLogged)
            {
                _diagLogged = true;
                Log.Warn($"FaceGeometry: range indisponível — GetRange: {err1} | " +
                         $"GetExactRange: {err2} | Vertices: {err3}");
            }
            return false;
        }

        private static bool TryRangeFromVertices(object comFace,
            out double[] minMm, out double[] maxMm, ref string error)
        {
            minMm = null;
            maxMm = null;
            try
            {
                dynamic face = comFace;
                dynamic verts = face.Vertices;
                int count = (int)verts.Count;
                if (count == 0) { error = "0 vértices"; return false; }

                double[] mn = { double.MaxValue, double.MaxValue, double.MaxValue };
                double[] mx = { double.MinValue, double.MinValue, double.MinValue };
                int read = 0;
                var mod = new ParameterModifier(1);
                mod[0] = true;

                for (int i = 1; i <= count; i++) // 1-based
                {
                    object vtx = verts.Item(i);
                    object[] args = { new double[0] }; // [out] SAFEARRAY(double)
                    vtx.GetType().InvokeMember(
                        "GetPointData", BindingFlags.InvokeMethod, null, vtx, args,
                        new[] { mod }, null, null);

                    double[] p = ToDoubles(args[0]);
                    if (p == null || p.Length < 3) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        if (p[k] < mn[k]) mn[k] = p[k];
                        if (p[k] > mx[k]) mx[k] = p[k];
                    }
                    read++;
                }

                if (read == 0) { error = "nenhum ponto de vértice lido"; return false; }
                minMm = new[] { mn[0] * 1000, mn[1] * 1000, mn[2] * 1000 };
                maxMm = new[] { mx[0] * 1000, mx[1] * 1000, mx[2] * 1000 };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryRange(object comFace, string method,
            out double[] minMm, out double[] maxMm, ref string error)
        {
            minMm = null;
            maxMm = null;
            try
            {
                // Os dois cantos são [out] SAFEARRAY(double). Em late binding com
                // este runtime, arrays vazios funcionam melhor como placeholder [out];
                // double[3] pré-semeado causava DISP_E_TYPEMISMATCH (Log 10/11).
                object[] args = { new double[0], new double[0] };
                var mod = new ParameterModifier(2);
                mod[0] = true;
                mod[1] = true;

                comFace.GetType().InvokeMember(
                    method, BindingFlags.InvokeMethod, null, comFace, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                double[] mn = ToDoubles(args[0]);
                double[] mx = ToDoubles(args[1]);
                if (mn == null || mx == null || mn.Length < 3 || mx.Length < 3)
                {
                    error = $"out vazio (min={Describe(args[0])}, max={Describe(args[1])})";
                    return false;
                }

                minMm = new[] { mn[0] * 1000, mn[1] * 1000, mn[2] * 1000 };
                maxMm = new[] { mx[0] * 1000, mx[1] * 1000, mx[2] * 1000 };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        private static string Describe(object o)
            => o == null ? "null" : o.GetType().Name;

        private static double[] ToDoubles(object arr)
        {
            if (arr is double[] d) return d;
            if (arr is Array a)
            {
                var list = new List<double>();
                foreach (var v in a) list.Add(Convert.ToDouble(v));
                return list.ToArray();
            }
            return null;
        }
    }
}
