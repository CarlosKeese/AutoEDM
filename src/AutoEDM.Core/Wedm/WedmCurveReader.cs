using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Wedm
{
    /// <summary>O que a leitura das curvas de construção achou — números para o log e para a mensagem final.</summary>
    public sealed class WedmCurveReadResult
    {
        public List<WireCurve> Curves { get; } = new List<WireCurve>();

        /// <summary>De onde as curvas saíram (coleções de features de curva ou, na falta delas, corpos de construção).</summary>
        public string Source { get; set; }

        public int FeaturesRead { get; set; }
        public int FeaturesHidden { get; set; }
        public int EdgesFailed { get; set; }

        /// <summary>Arestas exportadas como polilinha de 0,001 mm (sem leitura exata).</summary>
        public int EdgesStroked { get; set; }

        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Lê as CURVAS DE CONSTRUÇÃO visíveis de uma peça (Carlos, 2026-09-14: o sólido fica de fora —
    /// exporta só o que ele preparou, como no fluxo manual). SOMENTE-LEITURA.
    ///
    /// Onde estão: cada tipo de curva é uma coleção em <c>PartDocument.Constructions</c>
    /// (<c>IntersectionCurves</c>, <c>ProjectCurves</c>, <c>DerivedCurves</c>…); a feature expõe
    /// <c>Name</c>, <c>Visible</c>, <c>Suppress</c> e <c>Edges[igQueryAll]</c>. Se nenhuma coleção
    /// responder, cai nos corpos de construção (<c>Constructions.Item(i).Body</c>) que não têm FACE
    /// — corpo de arame é curva; com face é superfície e fica de fora.
    ///
    /// Cada aresta vira <see cref="WireCurve"/> EXATA quando dá: reta pelas pontas; arco por
    /// <c>Circle</c> (centro, raio, eixo) OU por <c>Ellipse</c> que se PROVE circular medindo o
    /// raio ao longo da aresta — é como o SE devolve o RAIO DE CANTO, ver <see cref="TryArc"/> —
    /// sempre com um ponto no meio da aresta para saber o sentido; B-spline por <c>GetBSplineInfo</c>/<c>GetBSplineData</c>, CONFERIDA
    /// avaliando a spline nas pontas do trecho da aresta. O que não passa (elipse de verdade,
    /// spline periódica, conferência que não bate) sai como polilinha pelo <c>GetStrokeData</c>
    /// a 0,001 mm — e é contado e avisado.
    /// </summary>
    public static class WedmCurveReader
    {
        private const int IgQueryAll = 1;

        // GNTTypePropertyConstants (dump SE 2023, mesmos valores de ORingTargetReader).
        private const int IgCircle = 167551105;
        private const int IgEllipse = 167551107;
        private const int IgLine = 167551109;
        private const int IgBSplineCurve = 167551103;

        private const double StrokeToleranceM = 0.000001;   // 0,001 mm
        private const double CheckToleranceMm = 0.001;

        private static readonly string[] CurveCollections =
        {
            "IntersectionCurves", "ProjectCurves", "DerivedCurves", "KeyPointCurves", "CurvesByTables",
            "ContourCurves", "CrossCurves", "IsoclineCurves", "HelicalCurves", "Sketch3DFeatures",
        };

        public static WedmCurveReadResult Read(object partDoc)
        {
            var r = new WedmCurveReadResult();
            object constructions = Get(partDoc, "Constructions");
            if (constructions == null)
            {
                r.Warnings.Add("PartDocument.Constructions inacessível");
                return r;
            }

            foreach (string collectionName in CurveCollections)
            {
                object collection = Get(constructions, collectionName);
                int n = Count(collection);
                if (n == 0) continue;
                Log.Info($"WEDM: {collectionName}: {n} feature(s).");

                for (int i = 1; i <= n; i++)
                {
                    object feature = Item(collection, i);
                    if (feature == null) continue;
                    string name = Get(feature, "Name") as string ?? $"{collectionName}[{i}]";
                    if (Get(feature, "Suppress") is bool suppressed && suppressed ||
                        Get(feature, "Visible") is bool visible && !visible)
                    {
                        r.FeaturesHidden++;
                        Log.Info($"WEDM: '{name}' oculta ou suprimida — fora da exportação.");
                        continue;
                    }
                    r.FeaturesRead++;
                    ReadEdges(Indexed(feature, "Edges", IgQueryAll), name, r);
                }
            }
            r.Source = "features de curva";

            if (r.Curves.Count == 0 && r.EdgesFailed == 0)
                ReadConstructionBodies(constructions, r);

            Log.Info($"WEDM: {r.Curves.Count} curva(s) de {r.FeaturesRead} fonte(s) ({r.Source}); " +
                     $"{r.FeaturesHidden} oculta(s), {r.EdgesStroked} como polilinha, {r.EdgesFailed} aresta(s) não lida(s).");
            return r;
        }

        /// <summary>Reserva: corpos de construção SEM face (arame).</summary>
        private static void ReadConstructionBodies(object constructions, WedmCurveReadResult r)
        {
            int n = Count(constructions);
            Log.Info($"WEDM: nenhuma curva pelas coleções — tentando os {n} corpo(s) de construção.");
            for (int i = 1; i <= n; i++)
            {
                object body = Get(Item(constructions, i), "Body");
                if (body == null) continue;
                if (Count(Indexed(body, "Faces", IgQueryAll)) > 0) continue; // superfície, não curva
                if (Get(body, "Visible") is bool visible && !visible) { r.FeaturesHidden++; continue; }
                r.FeaturesRead++;
                ReadEdges(Indexed(body, "Edges", IgQueryAll), $"corpo de construção {i}", r);
            }
            r.Source = "corpos de construção";
        }

        private static void ReadEdges(object edges, string source, WedmCurveReadResult r)
        {
            int n = Count(edges);
            for (int i = 1; i <= n; i++)
            {
                object edge = Item(edges, i);
                try
                {
                    bool stroked = false;
                    string why = "aresta nula";
                    WireCurve c = edge == null ? null : ReadEdge(edge, out stroked, out why);
                    if (c == null)
                    {
                        r.EdgesFailed++;
                        r.Warnings.Add($"{source}: aresta {i} não lida");
                        Log.Warn($"WEDM: '{source}' aresta {i} não lida.");
                        continue;
                    }
                    c.Source = source;
                    r.Curves.Add(c);
                    if (stroked)
                    {
                        r.EdgesStroked++;
                        Log.Warn($"WEDM: '{source}' aresta {i} exportada como polilinha de 0,001 mm — {why}.");
                    }
                }
                catch (Exception ex)
                {
                    r.EdgesFailed++;
                    r.Warnings.Add($"{source}: aresta {i} — {ex.GetBaseException().Message}");
                    Log.Warn($"WEDM: '{source}' aresta {i} — {ex.GetBaseException().Message}");
                }
            }
        }

        /// <summary>Curva exata quando dá; senão polilinha (<paramref name="stroked"/> = true, com o motivo em <paramref name="why"/>).</summary>
        private static WireCurve ReadEdge(object edge, out bool stroked, out string why)
        {
            stroked = false;
            if (!FaceGeometry.TryTwoPointOutMm(edge, "GetEndPoints", out double[] a, out double[] b, out why))
            {
                why = "GetEndPoints: " + why;
                return null;
            }

            object geom = Get(edge, "Geometry");
            int type = 0;
            try { type = Convert.ToInt32(Get(geom, "Type")); } catch { }

            WireCurve exact = null;
            switch (type)
            {
                case IgLine: return WireCurve.Line(a, b);
                case IgCircle: exact = TryArc(edge, geom, a, b, fromEllipse: false, why: out why); break;
                case IgEllipse: exact = TryArc(edge, geom, a, b, fromEllipse: true, why: out why); break;
                case IgBSplineCurve: exact = TryBSpline(edge, geom, a, b, out why); break;
                default: why = $"curva do tipo {type} sem leitura exata"; break;
            }
            if (exact != null) return exact;

            List<double[]> pts = TryStrokeMm(edge, out string strokeWhy);
            if (pts == null)
            {
                why = $"{why}; polilinha: {strokeWhy}";
                return null;
            }
            stroked = true;
            return WireCurve.Polyline(pts);
        }

        /// <summary>
        /// Arco horizontal (IGES 100) a partir de um <c>Circle</c> ou de uma <c>Ellipse</c>.
        ///
        /// A ELIPSE existe aqui por causa de um achado real (Carlos, 2026-09-16, log `093112`): os
        /// RAIOS DE CANTO do perfil chegam como <c>igEllipse</c> (167551107), não como
        /// <c>igCircle</c> — por isso todo raio saía como polilinha pelo botão, enquanto o "Salvar
        /// como" do próprio SE exportava o arco perfeito. Elipse de razão menor/maior 1 É um arco
        /// circular; só o nome do tipo difere.
        ///
        /// No caminho da elipse o raio é MEDIDO nas pontas (a <c>Ellipse</c> não tem
        /// <c>Radius</c>, e o comprimento do <c>GetMajorAxis</c> não é confiável sem validação) e
        /// conferido no PONTO DO MEIO: é isso que separa um raio de canto de uma elipse de
        /// verdade cujas pontas por acaso equidistam do centro. Não passando na conferência, a
        /// aresta cai na polilinha de sempre — nunca sai um arco errado.
        /// </summary>
        private static WireCurve TryArc(object edge, object geom, double[] a, double[] b, bool fromEllipse, out string why)
        {
            string what = fromEllipse ? "Ellipse" : "Circle";
            if (!FaceGeometry.TryOneArrayOut(geom, "GetCenterPoint", out double[] centerM, out why))
            {
                why = what + ".GetCenterPoint: " + why;
                return null;
            }

            if (FaceGeometry.TryOneArrayOut(geom, "GetAxisVector", out double[] axis, out string _) &&
                Math.Abs(Math.Abs(axis[2]) - 1.0) > 1e-9)
            {
                why = "arco fora de um plano horizontal";
                return null;
            }

            double[] center = ToMm(centerM);
            double radiusMm;
            if (fromEllipse)
            {
                // A razão declarada NÃO decide: o SE devolveu 0,9999 nos raios do perfil (log
                // `112004`) e, exigindo 1, todo raio continuava virando polilinha. Ela entra só no
                // aviso. Quem decide é o raio MEDIDO nas pontas e em 1/4, 1/2 e 3/4 da aresta — a
                // Ellipse não tem Radius, e o comprimento do GetMajorAxis não é confiável sem
                // validação.
                double[][] inner = PointsAlongMm(edge, new[] { 0.25, 0.5, 0.75 }, out why);
                if (inner == null) return null;

                var samples = new List<double[]> { a, b };
                samples.AddRange(inner);

                double deviation;
                radiusMm = WireCurve.TryCircularRadiusMm(center, samples, CheckToleranceMm, out deviation);
                if (double.IsNaN(radiusMm))
                {
                    double ratio;
                    string ratioText = TryDouble(geom, "MinorMajorRatio", out ratio)
                        ? $", razão menor/maior {ratio:0.000000}" : "";
                    why = $"elipse de verdade: o raio varia {deviation:0.0000} mm entre as amostras " +
                          $"(tolerância {CheckToleranceMm:0.0000} mm){ratioText}";
                    return null;
                }
                return WireCurve.HorizontalArc(center, radiusMm, a, b, inner[1]);
            }

            if (!TryDouble(geom, "Radius", out double radiusM)) { why = "Circle.Radius indisponível"; return null; }
            radiusMm = Units.MToMm(radiusM);
            double off = Math.Abs(WireCurve.RadiusXY(center, a) - radiusMm);
            if (off > CheckToleranceMm)
            {
                why = $"ponta do arco a {off:0.0000} mm do círculo";
                return null;
            }

            double[] mid = null;
            if (WireCurve.Distance(a, b) > 1e-4)
            {
                mid = MidPointMm(edge, out why);
                if (mid == null) return null;
            }
            return WireCurve.HorizontalArc(center, radiusMm, a, b, mid ?? a);
        }

        private static bool TryDouble(object com, string property, out double value)
        {
            value = 0;
            try
            {
                object v = Get(com, property);
                if (v == null) return false;
                value = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        private static WireCurve TryBSpline(object edge, object spline, double[] a, double[] b, out string why)
        {
            why = null;
            var info = new object[] { 0, 0, 0, false, false, false, false, new double[0] };
            if (!Invoke(spline, "GetBSplineInfo", info, Enumerable.Repeat(true, 8).ToArray(), out why))
            {
                why = "GetBSplineInfo: " + why;
                return null;
            }
            int order = Convert.ToInt32(info[0]), poleCount = Convert.ToInt32(info[1]), knotCount = Convert.ToInt32(info[2]);
            bool rational = Convert.ToBoolean(info[3]), periodic = Convert.ToBoolean(info[5]);
            if (periodic) { why = "B-spline periódica"; return null; }

            object[] data = null;
            foreach (int weightCount in rational ? new[] { poleCount } : new[] { 0, poleCount })
            {
                data = new object[] { poleCount, knotCount, weightCount, new double[0], new double[0], new double[0] };
                if (Invoke(spline, "GetBSplineData", data, new[] { false, false, false, true, true, true }, out why)) break;
                data = null;
            }
            if (data == null) { why = "GetBSplineData: " + why; return null; }

            double[] polesM = ToDoubles(data[3]), knots = ToDoubles(data[4]), weights = ToDoubles(data[5]);
            if (polesM == null || polesM.Length < 3 * poleCount) { why = $"GetBSplineData devolveu {polesM?.Length ?? 0} coordenada(s) de polo"; return null; }
            var poles = new double[poleCount][];
            for (int i = 0; i < poleCount; i++)
                poles[i] = new[] { Units.MToMm(polesM[3 * i]), Units.MToMm(polesM[3 * i + 1]), Units.MToMm(polesM[3 * i + 2]) };
            if (!rational || weights == null || weights.Length != poleCount) weights = null;

            if (!TryParamExtents(edge, out double t0, out double t1, out why)) { why = "GetParamExtents: " + why; return null; }

            WireCurve sp;
            try { sp = WireCurve.BSpline(order - 1, knots, weights, poles, t0, t1); }
            catch (ArgumentException ex) { why = "B-spline: " + ex.Message; return null; }

            // O trecho [t0, t1] da aresta tem de começar e terminar nas pontas dela — senão os nós
            // não estão no espaço de parâmetro da aresta e a curva sairia deslocada.
            double[] p0 = sp.Evaluate(t0), p1 = sp.Evaluate(t1);
            double dev = Math.Min(Math.Max(WireCurve.Distance(p0, a), WireCurve.Distance(p1, b)),
                                  Math.Max(WireCurve.Distance(p0, b), WireCurve.Distance(p1, a)));
            if (dev > CheckToleranceMm) { why = $"B-spline não confere com as pontas da aresta (desvio {dev:0.0000} mm)"; return null; }
            return sp;
        }

        /// <summary>Ponto no meio do parâmetro da aresta; reserva: o vértice do meio do <c>GetStrokeData</c>.</summary>
        private static double[] MidPointMm(object edge, out string why)
        {
            double[][] p = PointsAlongMm(edge, new[] { 0.5 }, out why);
            return p == null ? null : p[0];
        }

        /// <summary>
        /// Pontos AO LONGO da aresta, nas frações dadas do parâmetro (0,5 = meio) — é a amostragem
        /// que prova se o "raio" é mesmo circular. Reserva quando o <c>GetPointAtParam</c> não
        /// responde: os vértices correspondentes da polilinha do <c>GetStrokeData</c>, que tem
        /// resolução de sobra (0,001 mm) para a conferência.
        /// </summary>
        private static double[][] PointsAlongMm(object edge, double[] fractions, out string why)
        {
            if (TryParamExtents(edge, out double t0, out double t1, out why))
            {
                var points = new double[fractions.Length][];
                bool ok = true;
                for (int i = 0; i < fractions.Length && ok; i++)
                {
                    var args = new object[] { 1, new[] { t0 + (t1 - t0) * fractions[i] }, new double[0] };
                    if (!Invoke(edge, "GetPointAtParam", args, new[] { false, true, true }, out why)) { ok = false; break; }
                    double[] p = ToDoubles(args[2]);
                    if (p == null || p.Length < 3) { why = "GetPointAtParam sem ponto"; ok = false; break; }
                    points[i] = ToMm(p);
                }
                if (ok) return points;
            }

            List<double[]> stroke = TryStrokeMm(edge, out string strokeWhy);
            if (stroke != null && stroke.Count >= 3)
            {
                var points = new double[fractions.Length][];
                for (int i = 0; i < fractions.Length; i++)
                {
                    int k = (int)Math.Round(fractions[i] * (stroke.Count - 1));
                    points[i] = stroke[Math.Max(1, Math.Min(stroke.Count - 2, k))];
                }
                return points;
            }
            why = $"pontos ao longo da aresta: {why}; polilinha: {strokeWhy}";
            return null;
        }

        private static bool TryParamExtents(object edge, out double t0, out double t1, out string why)
        {
            t0 = t1 = 0;
            var args = new object[] { 0.0, 0.0 };
            if (!Invoke(edge, "GetParamExtents", args, new[] { true, true }, out why)) return false;
            t0 = Convert.ToDouble(args[0]);
            t1 = Convert.ToDouble(args[1]);
            if (t1 > t0) return true;
            why = $"extensão de parâmetro vazia ({t0}..{t1})";
            return false;
        }

        private static List<double[]> TryStrokeMm(object edge, out string why)
        {
            var args = new object[] { StrokeToleranceM, 0, new double[0], new double[0] };
            if (!Invoke(edge, "GetStrokeData", args, new[] { false, true, true, true }, out why)) return null;
            double[] m = ToDoubles(args[2]);
            if (m == null || m.Length < 6) { why = $"{m?.Length ?? 0} coordenada(s)"; return null; }
            var pts = new List<double[]>(m.Length / 3);
            for (int i = 0; i + 2 < m.Length; i += 3)
                pts.Add(new[] { Units.MToMm(m[i]), Units.MToMm(m[i + 1]), Units.MToMm(m[i + 2]) });
            return pts;
        }

        // ------------------------------------------------------------ COM (late binding)

        /// <summary>Método com parâmetros de saída: sem o <see cref="ParameterModifier"/> by-ref os [out] voltam vazios.</summary>
        private static bool Invoke(object com, string method, object[] args, bool[] byRef, out string why)
        {
            why = null;
            if (com == null) { why = "objeto nulo"; return false; }
            try
            {
                var mod = new ParameterModifier(args.Length);
                for (int i = 0; i < args.Length; i++) mod[i] = byRef[i];
                com.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, com, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception ex)
            {
                why = ex.GetBaseException().Message;
                return false;
            }
        }

        private static object Get(object com, string property)
        {
            if (com == null) return null;
            try { return com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, null, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        /// <summary>Propriedade indexada por tipo de consulta (<c>Edges[1]</c>, <c>Faces[1]</c>).</summary>
        private static object Indexed(object com, string property, int index)
        {
            if (com == null) return null;
            try { return com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, new object[] { index }, CultureInfo.InvariantCulture); }
            catch
            {
                try
                {
                    dynamic d = com;
                    return property == "Edges" ? (object)d.Edges[index] : (object)d.Faces[index];
                }
                catch { return null; }
            }
        }

        private static int Count(object collection)
        {
            try { return collection == null ? 0 : Convert.ToInt32(Get(collection, "Count")); }
            catch { return 0; }
        }

        private static object Item(object collection, int index)
        {
            if (collection == null) return null;
            try { return collection.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { index }, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        private static double[] ToDoubles(object value)
        {
            if (value is double[] d) return d;
            if (value is Array arr)
            {
                var result = new double[arr.Length];
                int i = 0;
                foreach (object o in arr) result[i++] = Convert.ToDouble(o, CultureInfo.InvariantCulture);
                return result;
            }
            return null;
        }

        private static double[] ToMm(double[] m) => new[] { Units.MToMm(m[0]), Units.MToMm(m[1]), Units.MToMm(m[2]) };

        private static double Sq(double v) => v * v;
    }
}
