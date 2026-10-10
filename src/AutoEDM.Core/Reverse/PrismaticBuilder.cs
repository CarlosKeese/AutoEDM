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
        public int CorrectionsBuilt;
        public int CorrectionsFailed;
        public readonly List<string> Messages = new List<string>();

        /// <summary>Volume do corpo de projeto no fim, em mm³ (NaN se não deu para ler).</summary>
        public double BodyVolumeMm3 = double.NaN;
        public double[] BodyMinMm, BodyMaxMm;

        public string DocumentName;

        /// <summary>O documento da peça criada (COM), para quem precisa salvar ou montar.</summary>
        public object Document;

        /// <summary>Desvio geométrico, quando a malha veio junto: cada vértice da malha até a
        /// superfície do sólido, e cada vértice do sólido (re-tesselado) até a malha. null = não
        /// medido.</summary>
        public DeviationResult MeshToSolid, SolidToMesh;
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
        private const int IgSymmetric = 3;

        public static PrismaticBuildResult Build(object application, PrismaticPlan plan, string partName)
        {
            return Build(application, plan, null, partName);
        }

        /// <param name="corrections">Correções de fronteira (chanfro, raio, face inclinada) a
        /// aplicar depois das faixas; null = só o prismático.</param>
        public static PrismaticBuildResult Build(object application, PrismaticPlan plan, CorrectionPlan corrections,
            string partName)
        {
            return Build(application, plan, corrections, partName, null);
        }

        /// <param name="meshPointsMm">A malha de origem: com ela, o resultado traz o desvio
        /// geométrico do sólido construído — a conferência que o volume não faz.</param>
        public static PrismaticBuildResult Build(object application, PrismaticPlan plan, CorrectionPlan corrections,
            string partName, double[] meshPointsMm)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var result = new PrismaticBuildResult();
            dynamic app = application;
            dynamic doc = app.Documents.Add("SolidEdge.PartDocument");
            doc.ModelingMode = 2; // ordenado — peça NOVA, criada aqui
            result.DocumentName = doc.Name;
            result.Document = doc;
            Log.Info($"[ENG.REVERSA] Construção prismática em '{doc.Name}' ({plan.Bands.Count} faixas){(string.IsNullOrEmpty(partName) ? "" : " — " + partName)}.");

            for (int i = 0; i < plan.Bands.Count; i++)
            {
                SectionBand band = plan.Bands[i];
                string label = string.Format(CultureInfo.InvariantCulture, "faixa {0} (Z {1:0.00} → {2:0.00})", i + 1, band.Z0, band.Z1);
                try
                {
                    string err = BuildBand(doc, band);
                    foreach (string note in band.Notes) result.Messages.Add(label + ": " + note);
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

            if (corrections != null && result.BandsBuilt > 0) ApplyCorrections(doc, corrections, result);

            HideSketches(doc, result);
            ReadBody(doc, result);
            if (meshPointsMm != null && result.BandsBuilt > 0) MeasureDeviation(doc, meshPointsMm, result);
            return result;
        }

        // ------------------------------------------------------------------ correções

        /// <summary>
        /// Uma feature por região de <see cref="BoundaryCorrections"/>: protrusão ou recorte, por
        /// revolução (<c>Profile.SetAxisOfRevolution</c> + <c>Revolved*.AddFinite(perfil, eixo,
        /// 2, 2, 2π)</c>, provado na SE 226 em 2026-10-10: o chanfro Ø4→Ø6 tirou os 7,330 mm³
        /// previstos) ou por extrusão ao longo da normal do esboço. Feature com falha é apagada e
        /// relatada — nunca fica na árvore estragando a seguinte.
        /// </summary>
        private static void ApplyCorrections(dynamic doc, CorrectionPlan plan, PrismaticBuildResult result)
        {
            dynamic model = doc.Models.Item(1);
            foreach (CorrectionRegion c in plan.Regions)
            {
                string label = (c.Add ? "+ " : "− ") + c.Label;
                try
                {
                    string err = c.Kind == "revolucao" ? Revolve(doc, model, c) : Extrude(doc, model, c);
                    if (err == null) result.CorrectionsBuilt++;
                    else { result.CorrectionsFailed++; result.Messages.Add("correção " + label + ": " + err); }
                }
                catch (Exception ex)
                {
                    result.CorrectionsFailed++;
                    result.Messages.Add("correção " + label + ": ERRO " + ex.GetBaseException().Message);
                    Log.Warn("[ENG.REVERSA] correção " + label + ": " + ex.GetBaseException());
                }
            }
        }

        private static string Revolve(dynamic doc, dynamic model, CorrectionRegion c)
        {
            SolidEdgePart.Profile p = DrawRegion(doc, c);
            double vMin = double.MaxValue, vMax = double.MinValue;
            foreach (ProfileEdge e in c.Edges)
            {
                vMin = Math.Min(vMin, Math.Min(e.Start[1], e.End[1]));
                vMax = Math.Max(vMax, Math.Max(e.Start[1], e.End[1]));
            }
            double[] a0 = ProfilePoint(p, c, 0, vMin - 1), a1 = ProfilePoint(p, c, 0, vMax + 1);
            object axisLine = p.Lines2d.AddBy2Points(a0[0], a0[1], a1[0], a1[1]);
            object refAxis = p.SetAxisOfRevolution(axisLine);
            int end = p.End((SolidEdgePart.ProfileValidationType)IgProfileClosedAllowNested);
            if (end != 0) return "perfil recusado (End = " + end + ")";

            // Revolução parcial (toro de 180° da ponta da alça): esboço no meio do arco e extensão
            // SIMÉTRICA, para o sentido de giro não depender de que lado a SE acha que é a direita.
            bool partial = c.SweepRad < 2 * Math.PI - 1e-9;
            int planeSide = partial ? IgSymmetric : IgRight;
            dynamic feature = c.Add
                ? model.RevolvedProtrusions.AddFinite(p, refAxis, IgRight, planeSide, c.SweepRad)
                : model.RevolvedCutouts.AddFinite(p, refAxis, IgRight, planeSide, c.SweepRad);
            return CheckOrDelete(feature, c.Add ? "revolução (protrusão)" : "revolução (recorte)");
        }

        private static string Extrude(dynamic doc, dynamic model, CorrectionRegion c)
        {
            SolidEdgePart.Profile p = DrawRegion(doc, c);
            int end = p.End((SolidEdgePart.ProfileValidationType)IgProfileClosedAllowNested);
            if (end != 0) return "perfil recusado (End = " + end + ")";

            double depthM = c.DepthMm / 1000.0;
            dynamic feature = c.Add
                ? model.ExtrudedProtrusions.AddFinite(p, 1 /*igLeft*/, IgRight, depthM)
                : model.ExtrudedCutouts.AddFinite(p, 1 /*igLeft*/, IgRight, depthM);
            return CheckOrDelete(feature, c.Add ? "extrusão (protrusão)" : "extrusão (recorte)");
        }

        private static string CheckOrDelete(dynamic feature, string what)
        {
            string bad = Status(feature, what);
            if (bad != null && feature != null)
            {
                try { feature.Delete(); } catch (Exception ex) { Log.Warn("[ENG.REVERSA] apagar " + what + " falhado: " + ex.GetBaseException().Message); }
            }
            return bad;
        }

        /// <summary>Esboço no plano (U, V) da região, com as retas e arcos dela ligados ponta a
        /// ponta. Os pontos vão para o esboço por <c>Convert3DCoordinate</c> a partir do mundo:
        /// o RefPlane derivado não herda os eixos do CS.</summary>
        private static SolidEdgePart.Profile DrawRegion(dynamic doc, CorrectionRegion c)
        {
            double[] w = c.W;
            Array m = new double[]
            {
                c.U[0], c.U[1], c.U[2], 0,
                c.V[0], c.V[1], c.V[2], 0,
                w[0], w[1], w[2], 0,
                c.Origin[0] / 1000.0, c.Origin[1] / 1000.0, c.Origin[2] / 1000.0, 1
            };
            dynamic cs = doc.CoordinateSystems.AddByMatrix(ref m);
            dynamic face = cs.Plane(4);
            dynamic plane = doc.RefPlanes.AddParallelByDistance(face, 0.0, 2);
            try { cs.Visible = false; } catch { }
            try { plane.Visible = false; } catch { }
            dynamic ps = doc.ProfileSets.Add();
            var typed = (SolidEdgePart.Profile)ps.Profiles.Add(plane);

            // Sentido ANTI-HORÁRIO nas coordenadas do ESBOÇO (não nas de U, V: o esboço pode vir
            // espelhado). Com o triângulo da rampa da malha 1 desenhado horário, o lado "igLeft"
            // do recorte era o de FORA e a feature levou a fatia inteira (−17.700 mm³).
            var edges = new List<ProfileEdge>(c.Edges);
            double twiceArea = 0;
            foreach (ProfileEdge e in edges)
            {
                var chain = new List<double[]> { ProfilePoint(typed, c, e.Start[0], e.Start[1]) };
                if (e.Kind == "arco") chain.Add(ProfilePoint(typed, c, e.Mid[0], e.Mid[1]));
                chain.Add(ProfilePoint(typed, c, e.End[0], e.End[1]));
                for (int k = 0; k + 1 < chain.Count; k++)
                    twiceArea += chain[k][0] * chain[k + 1][1] - chain[k + 1][0] * chain[k][1];
            }
            if (twiceArea < 0)
            {
                edges.Reverse();
                edges = edges.ConvertAll(e => new ProfileEdge { Kind = e.Kind, Start = e.End, End = e.Start, Mid = e.Mid });
            }

            var drawn = new List<object>();
            var kinds = new List<string>();
            var starts = new List<double[]>();
            var ends = new List<double[]>();
            foreach (ProfileEdge e in edges)
            {
                double[] s = ProfilePoint(typed, c, e.Start[0], e.Start[1]);
                double[] en = ProfilePoint(typed, c, e.End[0], e.End[1]);
                if (e.Kind == "arco")
                {
                    double[] mid = ProfilePoint(typed, c, e.Mid[0], e.Mid[1]);
                    drawn.Add(typed.Arcs2d.AddByStartAlongEnd(s[0], s[1], mid[0], mid[1], en[0], en[1]));
                }
                else drawn.Add(typed.Lines2d.AddBy2Points(s[0], s[1], en[0], en[1]));
                kinds.Add(e.Kind);
                starts.Add(s);
                ends.Add(en);
            }
            for (int k = 0; k < drawn.Count; k++)
            {
                int next = (k + 1) % drawn.Count;
                int ia = KeypointNear(drawn[k], kinds[k], ends[k]);
                int ib = KeypointNear(drawn[next], kinds[next], starts[next]);
                try { ((dynamic)typed.Relations2d).AddKeypoint(drawn[k], ia, drawn[next], ib); }
                catch (Exception ex) { Log.Warn("[ENG.REVERSA] relação de ponto-chave: " + ex.GetBaseException().Message); }
            }
            return typed;
        }

        private static double[] ProfilePoint(SolidEdgePart.Profile profile, CorrectionRegion c, double u, double v)
        {
            double[] p = c.ToWorld(u, v, 0);
            double pu, pv;
            profile.Convert3DCoordinate(p[0] / 1000.0, p[1] / 1000.0, p[2] / 1000.0, out pu, out pv);
            return new[] { pu, pv };
        }

        private const int IgFeatureOK = 1216476310;

        /// <summary>Furo = laço de um círculo só, com área negativa (horário: tira material).</summary>
        private static bool IsHole(SectionLoop l)
        {
            return l.Elements.Count == 1 && l.Elements[0].Kind == "circulo" && l.SignedAreaMm2 < 0;
        }

        /// <summary>
        /// Uma faixa: protrusão com os contornos (sem os furos), depois os furos como UM recorte
        /// na mesma altura. Por que separar: furo TANGENTE a outro contorno (o Ø2 encostado na
        /// parede do bolsão, malha 6 do Rack_Anghinoni) faz o perfil ter laços que se tocam num
        /// ponto — a protrusão foi criada sem erro e sem material nenhum (−3.216 mm³, a faixa
        /// inteira). Recorte booleano tolera a tangência, e o furo nasce como feature própria,
        /// que é como ele foi desenhado.
        /// </summary>
        private static string BuildBand(dynamic doc, SectionBand band)
        {
            var outlines = band.Loops.FindAll(l => !IsHole(l));
            var holes = band.Loops.FindAll(IsHole);
            double depthM = (band.Z1 - band.Z0) / 1000.0;

            if (outlines.Count > 0)
            {
                SolidEdgePart.Profile p = Draw(doc, band.Z0, outlines);
                int end = p.End((SolidEdgePart.ProfileValidationType)IgProfileClosedAllowNested);
                if (end != 0) return "perfil dos contornos recusado (End = " + end + ")";

                var arr = new SolidEdgePart.Profile[] { p };
                object models = doc.Models;
                models.GetType().InvokeMember("AddFiniteExtrudedProtrusion", BindingFlags.InvokeMethod,
                    null, models, new object[] { 1, arr, IgRight, depthM });
                // AddFiniteExtrudedProtrusion devolve o MODEL, não a feature: o status se lê na
                // última protrusão da coleção.
                dynamic protrusions = doc.Models.Item(1).ExtrudedProtrusions;
                string bad = Status(protrusions.Item(protrusions.Count), "protrusão");
                if (bad != null) return bad;
            }

            if (holes.Count == 0) return null;

            // Todos os furos da faixa num recorte só; se ele falhar, um a um — e o relatório diz
            // QUAL furo não entrou, em vez de só "falhou".
            if (CutHoles(doc, band, holes) == null) return null;

            var failed = new List<string>();
            var widened = new List<string>();
            foreach (SectionLoop h in holes)
            {
                SectionElement c = h.Elements[0];
                string where = string.Format(CultureInfo.InvariantCulture, "Ø{0:0.00} em ({1:0.00}; {2:0.00})",
                    2 * c.RadiusMm, c.Center[0], c.Center[1]);
                if (CutHoles(doc, band, new List<SectionLoop> { h }) == null) continue;

                // Furo TANGENTE a outro contorno (espessura zero entre os dois — o Ø2 encostado
                // no bolsão da malha 6): o Parasolid recusa o recorte até como booleana. Abrir o
                // furo +0,002 mm no Ø (5× abaixo do passo de 0,01) é a leitura física de
                // "espessura zero"; encolher deixaria uma parede de 0,001 mm.
                if (CutHoles(doc, band, new List<SectionLoop> { Widened(h, TangencyWideningMm) }) == null)
                    widened.Add(where);
                else
                    failed.Add(where);
            }
            if (widened.Count > 0)
                band.Notes.Add("aberto +0,002 mm no Ø por tangência: " + string.Join(", ", widened));
            return failed.Count == 0 ? null
                : failed.Count + " de " + holes.Count + " furo(s) não recortaram: " + string.Join(", ", failed);
        }

        private const double TangencyWideningMm = 0.002;

        private static SectionLoop Widened(SectionLoop hole, double extraDiameterMm)
        {
            SectionElement c = hole.Elements[0];
            var copy = new SectionLoop { SignedAreaMm2 = hole.SignedAreaMm2 };
            copy.Elements.Add(new SectionElement
            {
                Kind = "circulo", Center = c.Center, RadiusMm = c.RadiusMm + extraDiameterMm / 2,
                Region = c.Region, Exact = false
            });
            return copy;
        }

        /// <summary>Recorta os furos na altura da faixa. Recorte com falha é APAGADO: feature
        /// falhada que fica na árvore estraga o que vier depois.</summary>
        private static string CutHoles(dynamic doc, SectionBand band, List<SectionLoop> holes)
        {
            SolidEdgePart.Profile p = Draw(doc, band.Z0, holes);
            int end = p.End((SolidEdgePart.ProfileValidationType)IgProfileClosedAllowNested);
            if (end != 0) return "perfil dos furos recusado (End = " + end + ")";

            dynamic model = doc.Models.Item(1);
            dynamic cut = model.ExtrudedCutouts.AddFinite(p, 1 /*igLeft*/, IgRight, (band.Z1 - band.Z0) / 1000.0);
            string bad = Status(cut, "recorte dos furos");
            if (bad != null)
            {
                try { cut.Delete(); } catch (Exception ex) { Log.Warn("[ENG.REVERSA] apagar recorte falhado: " + ex.GetBaseException().Message); }
            }
            return bad;
        }

        private static string Status(dynamic feature, string what)
        {
            if (feature == null) return what + ": a Solid Edge não devolveu feature";
            try
            {
                // ExtrudedProtrusion não tem GetStatusEx (o recorte tem); a propriedade Status
                // existe nos dois.
                int st = (int)feature.Status;
                if (st != IgFeatureOK) return what + ": feature com falha (status " + st + ")";
            }
            catch (Exception ex) { Log.Warn("[ENG.REVERSA] status de " + what + ": " + ex.GetBaseException().Message); }
            return null;
        }

        private static SolidEdgePart.Profile Draw(dynamic doc, double z0, List<SectionLoop> loops)
        {
            dynamic profile = SketchAt(doc, z0);
            var typed = (SolidEdgePart.Profile)profile;

            foreach (SectionLoop loop in loops)
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

            return typed;
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

        /// <summary>Limite de desvio que separa "o sólido é a malha" de "o sólido diverge": a
        /// flecha da tesselação dos dois lados fica em ~0,02 mm nas malhas reais.</summary>
        public const double DeviationLimitMm = 0.05;

        private static void MeasureDeviation(dynamic doc, double[] meshPointsMm, PrismaticBuildResult result)
        {
            try
            {
                MeshData built = MeshReader.Read((object)doc.Models.Item(1).Body, 0.005);
                if (!built.Ok) { result.Messages.Add("desvio não medido: " + built.Error); return; }
                result.MeshToSolid = MeshDeviation.Compute(meshPointsMm, built.PointsMm, DeviationLimitMm);
                result.SolidToMesh = MeshDeviation.Compute(built.PointsMm, meshPointsMm, DeviationLimitMm);
            }
            catch (Exception ex)
            {
                result.Messages.Add("desvio não medido: " + ex.GetBaseException().Message);
            }
        }

        /// <summary>Oculta os perfis no fim, como a SE faz quando a feature nasce pela interface.
        /// Feature criada pela API deixa o esboço aceso: com uma faixa por nível e uma correção
        /// por chanfro, a peça construída ficava coberta de linhas (Carlos, 2026-10-10). O CS e o
        /// plano de referência já nascem ocultos em <see cref="SketchAt"/>/<see cref="DrawRegion"/>.</summary>
        private static void HideSketches(dynamic doc, PrismaticBuildResult result)
        {
            int failed = 0;
            try
            {
                dynamic sets = doc.ProfileSets;
                for (int i = 1; i <= (int)sets.Count; i++)
                {
                    dynamic profiles = sets.Item(i).Profiles;
                    for (int k = 1; k <= (int)profiles.Count; k++)
                    {
                        try { profiles.Item(k).Visible = false; }
                        catch { failed++; }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Messages.Add("esboços não ocultados: " + ex.GetBaseException().Message);
                return;
            }
            if (failed > 0) result.Messages.Add(failed + " esboço(s) não ocultado(s)");
        }

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
