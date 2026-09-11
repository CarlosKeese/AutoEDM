using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Leitura de geometria de face via COM (late-bound). A API de geometria do
    /// Solid Edge trabalha em METROS; aqui convertemos para mm.
    /// </summary>
    public static class FaceGeometry
    {
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

            // Achado 2026-07-22 (log `073330`, "Criar eletrodo manual" ainda não reconhecia a
            // face selecionada): isto ANTES só logava a 1ª falha de TODO o processo (flag
            // static `_diagLogged`) — qualquer falha seguinte (ex.: a face DESEMBRULHADA de
            // CollectSelectedFaces, chamada logo depois da falha esperada na seleção CRUA)
            // ficava muda, escondendo exatamente o diagnóstico que precisávamos. Falha total
            // (os 3 métodos falharam) é sempre um evento excepcional — vale sempre logar.
            Log.Warn($"FaceGeometry: range indisponível — GetRange: {err1} | " +
                     $"GetExactRange: {err2} | Vertices: {err3}");
            return false;
        }

        /// <summary>
        /// Bounding box de um CORPO/superfície inteiro (Body/CopySurface item), em mm — mesma
        /// receita de <see cref="TryGetRangeMm"/> (Body.GetRange/GetExactRange têm a MESMA forma
        /// [in,out] SAFEARRAY(double)×2, confirmada no dump). REDE DE SEGURANÇA (achado
        /// 2026-07-17): o bbox por-FACE perde qualquer face cujo GetRange/GetExactRange/Vertices
        /// falhem TODOS (visto numa face curva sem propriedade Vertices) — isso subestimou o topo
        /// Z de uma superfície com calota/nariz arredondado (a face de referência da faixa saiu
        /// 0,2mm DENTRO da geometria real). O bbox do CORPO inteiro não depende de nenhuma face
        /// individual, então é usado para EXPANDIR (nunca encolher) o bbox por-face.
        /// </summary>
        public static bool TryGetBodyRangeMm(object comBody, out double[] minMm, out double[] maxMm)
        {
            string err1 = null, err2 = null;
            if (TryRange(comBody, "GetRange", out minMm, out maxMm, ref err1)) return true;
            if (TryRange(comBody, "GetExactRange", out minMm, out maxMm, ref err2)) return true;
            Log.Warn($"FaceGeometry (Body): range indisponível — GetRange: {err1} | GetExactRange: {err2}");
            return false;
        }

        /// <summary>
        /// Executa um método COM que devolve DOIS pontos por parâmetro de SAÍDA — SAFEARRAY de 3
        /// doubles em METROS, convertidos aqui para mm. É a forma de <c>GetRange</c>,
        /// <c>GetExactRange</c> (Face/Body) e também de <c>Edge.GetEndPoints</c>, daí ser público:
        /// <see cref="EdgeGeometry"/> reusa este executor em vez de reescrever o
        /// <see cref="ParameterModifier"/> by-ref (sem ele os [out] voltam vazios — Log 8/9).
        /// </summary>
        public static bool TryTwoPointOutMm(object com, string method,
            out double[] aMm, out double[] bMm, out string error)
        {
            error = null;
            return TryRange(com, method, out aMm, out bMm, ref error);
        }

        /// <summary>
        /// Mesmo executor de <see cref="TryTwoPointOutMm"/>, SEM converter de metros para mm —
        /// para os métodos dessa mesma forma cujos dois arrays NÃO são comprimento.
        /// <c>Face.GetParamRange</c> é o caso: devolve parâmetros de superfície (num cilindro, U
        /// é ÂNGULO em radianos), e passar isso por <see cref="Units.MToMm"/> daria um número
        /// 1000× maior sem estourar nada — exatamente o tipo de erro plausível-e-errado que a
        /// classe <see cref="Units"/> existe para evitar.
        /// </summary>
        public static bool TryTwoArrayOut(object com, string method,
            out double[] a, out double[] b, out string error)
        {
            error = null;
            return TryTwoArrayOutCore(com, method, out a, out b, ref error);
        }

        /// <summary>
        /// Mesma receita by-ref para os métodos de UM só array de saída —
        /// <c>Plane.GetNormalVector([out] NormalVector)</c>, <c>Vertex.GetPointData([out] Point)</c>.
        /// Cru, sem conversão: um vetor NORMAL é adimensional (não é comprimento, não passa por
        /// <see cref="Units"/>); um PONTO está em metros e quem chama converte.
        /// </summary>
        public static bool TryOneArrayOut(object com, string method, out double[] a, out string error)
        {
            a = null;
            error = null;
            try
            {
                object[] args = { new double[0] };
                var mod = new ParameterModifier(1);
                mod[0] = true;

                com.GetType().InvokeMember(
                    method, BindingFlags.InvokeMethod, null, com, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                a = ToDoubles(args[0]);
                if (a == null || a.Length < 3)
                {
                    error = $"out vazio ({Describe(args[0])})";
                    a = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
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
                minMm = new[] { Units.MToMm(mn[0]), Units.MToMm(mn[1]), Units.MToMm(mn[2]) };
                maxMm = new[] { Units.MToMm(mx[0]), Units.MToMm(mx[1]), Units.MToMm(mx[2]) };
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
            if (!TryTwoArrayOutCore(comFace, method, out double[] mn, out double[] mx, ref error)) return false;
            // O executor cru aceita 2 componentes (GetParamRange devolve U,V); um RANGE precisa
            // dos 3 do ponto, então a conferência de tamanho fica aqui, e não lá.
            if (mn.Length < 3 || mx.Length < 3)
            {
                error = $"range com menos de 3 componentes (min={mn.Length}, max={mx.Length})";
                return false;
            }
            minMm = new[] { Units.MToMm(mn[0]), Units.MToMm(mn[1]), Units.MToMm(mn[2]) };
            maxMm = new[] { Units.MToMm(mx[0]), Units.MToMm(mx[1]), Units.MToMm(mx[2]) };
            return true;
        }

        /// <summary>
        /// O executor cru dos métodos "dois SAFEARRAY(double) por parâmetro de saída"
        /// (<c>GetRange</c>, <c>GetExactRange</c>, <c>GetEndPoints</c>, <c>GetParamRange</c>).
        /// Devolve os arrays COMO VIERAM — quem chama decide se aquilo é comprimento e precisa
        /// converter de metros.
        /// </summary>
        private static bool TryTwoArrayOutCore(object comFace, string method,
            out double[] a, out double[] b, ref string error)
        {
            a = null;
            b = null;
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

                a = ToDoubles(args[0]);
                b = ToDoubles(args[1]);
                if (a == null || b == null || a.Length < 2 || b.Length < 2)
                {
                    error = $"out vazio (a={Describe(args[0])}, b={Describe(args[1])})";
                    a = null; b = null;
                    return false;
                }
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
