using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Reverse
{
    public sealed class PrismaticBuildResult
    {
        public int BandsBuilt;
        public int BandsFailed;
        public readonly List<string> Messages = new List<string>();

        /// <summary>Volume do corpo de projeto no fim, em mm³ (NaN se não deu para ler).</summary>
        public double BodyVolumeMm3 = double.NaN;
        public double[] BodyMinMm, BodyMaxMm;

        public string DocumentName;
    }

    /// <summary>
    /// Constrói na Solid Edge o <see cref="PrismaticPlan"/>: uma extrusão por faixa Z, com o
    /// contorno em retas, arcos e círculos EXATOS.
    ///
    /// Toda receita aqui foi provada na Solid Edge 226 em 2026-10-09 antes de virar código:
    ///   - plano de esboço em qualquer altura: <c>CoordinateSystems.AddByMatrix</c> (16 doubles,
    ///     linhas = eixos, translação em [12..14], metros) → <c>cs.Plane(4)</c> (que é uma FACE,
    ///     não serve para <c>Profiles.Add</c>) → <c>RefPlanes.AddParallelByDistance(face, 0, 2)</c>;
    ///   - o RefPlane derivado NÃO herda o eixo X do CS: todo ponto passa por
    ///     <c>Profile.Convert3DCoordinate</c>, nunca por suposição de eixos;
    ///   - perfil de elementos soltos devolve <c>End = −103</c>; com <c>AddKeypoint</c> entre as
    ///     pontas, 0. Reta: início 0, fim 1. Arco: início 1, fim 2 — e o índice é escolhido pela
    ///     POSIÇÃO lida de volta, porque o arco pode nascer normalizado no outro sentido;
    ///   - arco por 3 pontos (<c>AddByStartAlongEnd</c>), para o sentido nunca ser ambíguo.
    ///
    /// Sempre numa PEÇA NOVA, em modelagem ORDENADA: a peça do usuário nunca tem o ambiente
    /// trocado (regra do projeto), e é numa peça nova que a troca é inofensiva. Nada é salvo.
    /// </summary>
    public static class PrismaticBuilder
    {
        private const int IgProfileClosedAllowNested = 1 | 8192;
        private const int IgRight = 2; // a favor da normal do plano (+Z)

        public static PrismaticBuildResult Build(object application, PrismaticPlan plan, string partName)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var result = new PrismaticBuildResult();
            dynamic app = application;
            dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
            doc.ModelingMode = 2; // ordenado — peça NOVA, criada aqui
            result.DocumentName = doc.Name;
            Log.Info($"[ENG.REVERSA] Construção prismática em '{doc.Name}' ({plan.Bands.Count} faixas){(string.IsNullOrEmpty(partName) ? "" : " — " + partName)}.");

            for (int i = 0; i < plan.Bands.Count; i++)
            {
                SectionBand band = plan.Bands[i];
                string label = string.Format(CultureInfo.InvariantCulture, "faixa {0} (Z {1:0.00} → {2:0.00})", i + 1, band.Z0, band.Z1);
                try
                {
                    string err = BuildBand(doc, band);
                    if (err == null) { result.BandsBuilt++; result.Messages.Add(label + ": ok"); }
                    else { result.BandsFailed++; result.Messages.Add(label + ": " + err); }
                }
                catch (Exception ex)
                {
                    result.BandsFailed++;
                    result.Messages.Add(label + ": ERRO " + ex.GetBaseException().Message);
                    Log.Warn("[ENG.REVERSA] " + label + ": " + ex.GetBaseException());
                }
            }

            ReadBody(doc, result);
            return result;
        }

        private static string BuildBand(dynamic doc, SectionBand band)
        {
            double z0 = band.Z0;
            dynamic profile = SketchAt(doc, z0);
            var typed = (SolidEdgePart.Profile)profile;

            foreach (SectionLoop loop in band.Loops)
            {
                var drawn = new List<object>();
                var kinds = new List<string>();
                var starts = new List<double[]>();
                var ends = new List<double[]>();

                foreach (SectionElement e in loop.Elements)
                {
                    if (e.Kind == "circulo")
                    {
                        double[] c = To2D(typed, e.Center, z0);
                        typed.Circles2d.AddByCenterRadius(c[0], c[1], e.RadiusMm / 1000.0);
                        continue;
                    }

                    double[] s = To2D(typed, e.Start, z0), en = To2D(typed, e.End, z0);
                    if (e.Kind == "arco")
                    {
                        double[] m = To2D(typed, e.Mid, z0);
                        drawn.Add(typed.Arcs2d.AddByStartAlongEnd(s[0], s[1], m[0], m[1], en[0], en[1]));
                    }
                    else
                    {
                        drawn.Add(typed.Lines2d.AddBy2Points(s[0], s[1], en[0], en[1]));
                    }
                    kinds.Add(e.Kind);
                    starts.Add(s);
                    ends.Add(en);
                }

                // Pontas ligadas: sem isto o perfil é "aberto" (End = −103) mesmo com os pontos
                // coincidindo.
                for (int k = 0; k < drawn.Count; k++)
                {
                    int next = (k + 1) % drawn.Count;
                    int ia = KeypointNear(drawn[k], kinds[k], ends[k]);
                    int ib = KeypointNear(drawn[next], kinds[next], starts[next]);
                    try { ((dynamic)typed.Relations2d).AddKeypoint(drawn[k], ia, drawn[next], ib); }
                    catch (Exception ex) { Log.Warn("[ENG.REVERSA] relação de ponto-chave: " + ex.GetBaseException().Message); }
                }
            }

            int end = typed.End((SolidEdgePart.ProfileValidationType)IgProfileClosedAllowNested);
            if (end != 0) return "perfil recusado (End = " + end + ")";

            var arr = new SolidEdgePart.Profile[] { typed };
            object models = doc.Models;
            object feature = models.GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod,
                null, models, new object[] { 1, arr, IgRight, (band.Z1 - band.Z0) / 1000.0 });
            if (feature == null) return "a extrusão não devolveu feature";
            return null;
        }

        /// <summary>Esboço num plano horizontal na altura z (mm).</summary>
        private static dynamic SketchAt(dynamic doc, double zMm)
        {
            Array m = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, zMm / 1000.0, 1 };
            dynamic cs = doc.CoordinateSystems.AddByMatrix(ref m);
            dynamic face = cs.Plane(4); // XY do CS — é uma Face
            dynamic plane = doc.RefPlanes.AddParallelByDistance(face, 0.0, 2);
            try { cs.Visible = false; } catch { }
            try { plane.Visible = false; } catch { }
            dynamic ps = doc.ProfileSets.Add();
            return ps.Profiles.Add(plane);
        }

        private static double[] To2D(SolidEdgePart.Profile profile, double[] xyMm, double zMm)
        {
            double u, v;
            profile.Convert3DCoordinate(xyMm[0] / 1000.0, xyMm[1] / 1000.0, zMm / 1000.0, out u, out v);
            return new[] { u, v };
        }

        /// <summary>Índice do ponto-chave (início/fim) do elemento mais perto de p (coordenadas
        /// do esboço). Lido de volta da Solid Edge: o arco pode nascer no sentido contrário.</summary>
        private static int KeypointNear(object element, string kind, double[] p)
        {
            double sx, sy, ex, ey;
            if (kind == "arco")
            {
                var a = (SolidEdgeFrameworkSupport.Arc2d)element;
                a.GetStartPoint(out sx, out sy);
                a.GetEndPoint(out ex, out ey);
                return Sq(sx - p[0], sy - p[1]) <= Sq(ex - p[0], ey - p[1]) ? 1 : 2;
            }
            var l = (SolidEdgeFrameworkSupport.Line2d)element;
            l.GetStartPoint(out sx, out sy);
            l.GetEndPoint(out ex, out ey);
            return Sq(sx - p[0], sy - p[1]) <= Sq(ex - p[0], ey - p[1]) ? 0 : 1;
        }

        private static double Sq(double a, double b) { return a * a + b * b; }

        private static void ReadBody(dynamic doc, PrismaticBuildResult result)
        {
            try
            {
                if ((int)doc.Models.Count < 1) return;
                dynamic body = doc.Models.Item(1).Body;
                result.BodyVolumeMm3 = (double)body.Volume * 1e9;
                Array mn = new double[3], mx = new double[3];
                body.GetExactRange(ref mn, ref mx);
                result.BodyMinMm = new[] { (double)mn.GetValue(0) * 1000, (double)mn.GetValue(1) * 1000, (double)mn.GetValue(2) * 1000 };
                result.BodyMaxMm = new[] { (double)mx.GetValue(0) * 1000, (double)mx.GetValue(1) * 1000, (double)mx.GetValue(2) * 1000 };
            }
            catch (Exception ex)
            {
                result.Messages.Add("leitura do corpo: " + ex.GetBaseException().Message);
            }
        }
    }
}
