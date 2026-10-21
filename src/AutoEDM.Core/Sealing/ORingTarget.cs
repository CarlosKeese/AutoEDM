using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;
using AutoEDM.Model;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// O que a FACE + a ARESTA selecionadas dizem sobre onde vai o canal. Puro dado: quem lê o
    /// COM é <see cref="ORingTargetReader"/>, quem corta é <see cref="ORingGrooveModeler"/>.
    /// Tudo em MILÍMETROS, no sistema da peça.
    /// </summary>
    public sealed class ORingTarget
    {
        public bool Ok { get; set; }
        public string Error { get; set; }

        /// <summary>A aresta COM que o usuário clicou. O modelador ancora nela o plano do esboço
        /// — é o que faz o canal ACOMPANHAR a peça quando ela é editada no síncrono.</summary>
        public object Edge { get; set; }

        /// <summary>A face COM que o usuário clicou (a plana, num canal de face, é o próprio
        /// apoio do esboço do canal extrudado).</summary>
        public object Face { get; set; }

        /// <summary>0=X, 1=Y, 2=Z — o eixo de revolução do canal, deduzido da aresta circular.</summary>
        public int AxisIndex { get; set; }

        /// <summary>Centro da aresta circular (mm) — um ponto do eixo.</summary>
        public double[] CenterMm { get; set; }

        /// <summary>Ø da ARESTA selecionada (mm).</summary>
        public double EdgeDiameterMm { get; set; }

        /// <summary>Ø da FACE cilíndrica (mm); 0 quando a face é plana.</summary>
        public double FaceDiameterMm { get; set; }

        public bool FaceIsPlanar { get; set; }
        public bool FaceIsCylindrical { get; set; }

        /// <summary>Tipo de canal deduzido da geometria. O operador pode trocar na janela —
        /// é palpite bem fundamentado, não veredito.</summary>
        public GrooveKind SuggestedKind { get; set; }

        /// <summary>Ø que alimenta o cálculo: o da face cilíndrica (canal radial) ou o da
        /// aresta (canal de face).</summary>
        public double SealingDiameterMm { get; set; }

        /// <summary>Coordenada da aresta ao longo do eixo (mm).</summary>
        public double EdgeAxialMm { get; set; }

        /// <summary>+1 ou −1: para que lado do eixo, a partir da aresta, está o MATERIAL. É a
        /// direção em que o canal se afasta da aresta (canal radial) ou em que ele afunda
        /// (canal de face).</summary>
        public int MaterialDirection { get; set; } = 1;

        /// <summary>Canal de FACE: o canal se afasta da aresta para FORA (aresta = boca de um
        /// furo) ou para DENTRO (aresta = contorno externo da face, como o topo de um eixo)?</summary>
        public bool FaceGrooveOutward { get; set; } = true;

        /// <summary>Faixa da face ao longo do eixo (mm) — para o canal não cair fora dela.</summary>
        public double FaceAxialMinMm { get; set; }
        public double FaceAxialMaxMm { get; set; }

        public string Description { get; set; }

        public static ORingTarget Fail(string error)
        {
            Log.Warn("Alojamento de O'ring: " + error);
            return new ORingTarget { Ok = false, Error = error };
        }

        public string AxisName => AxisIndex == 0 ? "X" : AxisIndex == 1 ? "Y" : "Z";
    }

    /// <summary>
    /// Lê a face e a aresta selecionadas e devolve um <see cref="ORingTarget"/>.
    ///
    /// POSIÇÃO E EIXO saem da CAIXA ENVOLVENTE (<c>GetRange</c>), que numa aresta CIRCULAR
    /// entrega tudo de uma vez: o eixo é a direção em que a caixa tem espessura ZERO, e o
    /// centro é o meio da caixa.
    ///
    /// TIPO E DIÂMETRO saem do objeto de GEOMETRIA (<c>Face.Geometry</c> / <c>Edge.Geometry</c>):
    /// é ele que diz plano/cilindro/círculo e traz o <c>Radius</c> exato. O <c>.Type</c> da
    /// face/aresta NÃO serve para isso — ver <see cref="ORingTargetReader.GeomTypeOf"/>.
    ///
    /// LIMITAÇÃO ASSUMIDA: o eixo do canal tem de ser paralelo a X, Y ou Z. Um anel num furo
    /// inclinado cai fora — e o comando DIZ isso em vez de cortar torto. É a mesma restrição
    /// que o resto do AutoEDM já tem para ocorrência inclinada.
    /// </summary>
    public static class ORingTargetReader
    {
        // GNTTypePropertyConstants (dump SE 2023) — ATENÇÃO: NÃO são os mesmos valores do
        // enum de CONSULTA de topologia (igQueryPlane=6 / igQueryCylinder=10), que é o que se
        // usa em Body.Faces[...]. São dois enums diferentes com nomes parecidos.
        private const int igPlane = -1909484335;
        private const int igCylinder = -114972029;
        private const int igCone = -114972031;
        private const int igSphere = -114972027;
        private const int igTorus = -114972025;
        private const int igBSplineSurface = 1465959633;
        private const int igCircle = 167551105;
        private const int igLine = 167551109;
        private const int igEllipse = 167551107;
        private const int igBSplineCurve = 167551103;

        // O TIPO DO OBJETO topológico — NÃO é o tipo da superfície/curva. Ver GeomTypeOf():
        // Face.Type responde SEMPRE igFace e Edge.Type SEMPRE igEdge (log 2026-09-04, o bug
        // que fazia toda face plana ser recusada com "Tipo lido: 167551075").
        private const int igFace = 167551075;
        private const int igEdge = 167551093;

        /// <summary>Tolerância (mm) para "esta dimensão da caixa é zero" e para "estes dois
        /// lados são iguais". Frouxa o bastante para facetamento, apertada para não confundir
        /// um furo oblongo com um circular.</summary>
        private const double Tol = 0.005;

        public static ORingTarget Read(object comFace, object comEdge)
        {
            if (comFace == null) return ORingTarget.Fail("nenhuma FACE selecionada.");
            if (comEdge == null) return ORingTarget.Fail("nenhuma ARESTA selecionada.");

            int axis;
            double[] center;
            double diameter;
            string edgeError;
            if (!ReadCircle(comEdge, out axis, out center, out diameter, out edgeError))
                return ORingTarget.Fail(edgeError);
            int r1 = (axis + 1) % 3, r2 = (axis + 2) % 3;

            var t = new ORingTarget
            {
                Ok = true,
                Edge = comEdge,
                Face = comFace,
                AxisIndex = axis,
                CenterMm = center,
                EdgeDiameterMm = diameter
            };
            t.EdgeAxialMm = t.CenterMm[axis];
            LogEdgeType(comEdge);

            // Raio exato do círculo, quando a SE der (a caixa envolvente já concorda num
            // círculo inteiro; o raio é que não erra se a aresta for facetada na leitura).
            double edgeRadiusMm = RadiusMm(comEdge);
            if (edgeRadiusMm > 0)
            {
                if (Math.Abs(2.0 * edgeRadiusMm - t.EdgeDiameterMm) > 0.01)
                    Log.Info($"  [O'ring] Ø da aresta: {2.0 * edgeRadiusMm:0.000} mm pelo raio do círculo " +
                             $"(a caixa envolvente dizia {t.EdgeDiameterMm:0.000} mm) — vale o raio.");
                t.EdgeDiameterMm = 2.0 * edgeRadiusMm;
            }

            double[] fMin, fMax;
            if (!FaceGeometry.TryGetRangeMm(comFace, out fMin, out fMax))
                return ORingTarget.Fail("não deu para ler a caixa envolvente da face selecionada.");
            t.FaceAxialMinMm = fMin[axis];
            t.FaceAxialMaxMm = fMax[axis];

            int faceType = GeomTypeOf(comFace);
            t.FaceIsPlanar = faceType == igPlane;
            t.FaceIsCylindrical = faceType == igCylinder;
            if (!t.FaceIsPlanar && !t.FaceIsCylindrical)
                return ORingTarget.Fail(
                    "a face selecionada não é PLANA nem CILÍNDRICA (o AutoEDM só aloja O'ring nessas duas). " +
                    $"A face selecionada é um(a) {GeomName(faceType)}.");

            if (t.FaceIsCylindrical)
            {
                // Raio do cilindro na frente da caixa envolvente: numa face cilíndrica parcial
                // a caixa mede a corda, não o diâmetro.
                double faceRadiusMm = RadiusMm(comFace);
                t.FaceDiameterMm = faceRadiusMm > 0
                    ? 2.0 * faceRadiusMm
                    : ((fMax[r1] - fMin[r1]) + (fMax[r2] - fMin[r2])) / 2.0;
                t.SealingDiameterMm = t.FaceDiameterMm;
                t.SuggestedKind = GuessShaftOrBore(comFace, t, r1, r2);
                // O canal anda da aresta PARA DENTRO da face — senão sai pela boca do cilindro.
                double toMax = t.FaceAxialMaxMm - t.EdgeAxialMm, toMin = t.EdgeAxialMm - t.FaceAxialMinMm;
                t.MaterialDirection = toMax >= toMin ? 1 : -1;
            }
            else
            {
                t.SealingDiameterMm = t.EdgeDiameterMm;
                t.SuggestedKind = GrooveKind.AxialFace;
                t.MaterialDirection = MaterialSideOfPlanarFace(comFace, axis, t.EdgeAxialMm);
                t.FaceGrooveOutward = GrooveGoesOutward(t, fMin, fMax, r1, r2);
            }

            t.Description = Describe(t);
            Log.Info("Alojamento de O'ring — " + t.Description);
            return t;
        }

        /// <summary>
        /// Eixo, centro e Ø da aresta circular. Primeiro pela CAIXA envolvente (o caminho de
        /// sempre); se a SE recusar a caixa — visto ao vivo em 2026-10-21, <c>GetRange</c> e
        /// <c>GetExactRange</c> com E_FAIL em arestas de face plana, e o operador tinha de clicar
        /// de novo até "pegar" —, pelo próprio CÍRCULO: <c>Edge.Geometry.GetCircleData</c> dá
        /// centro, normal e raio sem depender da caixa.
        /// </summary>
        private static bool ReadCircle(object comEdge, out int axis, out double[] centerMm, out double diameterMm, out string error)
        {
            axis = -1; centerMm = null; diameterMm = 0; error = null;
            double[] eMin, eMax;
            if (FaceGeometry.TryGetRangeMm(comEdge, out eMin, out eMax))
            {
                // O eixo é a direção em que a aresta circular não tem espessura.
                var span = new[] { eMax[0] - eMin[0], eMax[1] - eMin[1], eMax[2] - eMin[2] };
                for (int i = 0; i < 3; i++) if (Math.Abs(span[i]) <= Tol) { axis = i; break; }
                if (axis < 0)
                {
                    error = "a aresta selecionada não está num plano paralelo a XY, XZ ou YZ. " +
                            "O alojamento de O'ring só é criado com o eixo paralelo a X, Y ou Z.";
                    return false;
                }
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                if (Math.Abs(span[a1] - span[a2]) > Math.Max(Tol, 0.01 * span[a1]))
                {
                    error = $"a aresta não é circular (caixa {span[a1]:0.###} × {span[a2]:0.###} mm). " +
                            "Selecione a aresta CIRCULAR que dá o diâmetro de referência.";
                    return false;
                }
                centerMm = new[] { (eMin[0] + eMax[0]) / 2.0, (eMin[1] + eMax[1]) / 2.0, (eMin[2] + eMax[2]) / 2.0 };
                diameterMm = (span[a1] + span[a2]) / 2.0;
                return true;
            }

            // Plano B: o círculo.
            object geo = GeometryOf(comEdge);
            var args = new object[] { new double[3], new double[3], 0.0 };
            if (geo == null || GeomTypeOf(comEdge) != igCircle || AutoEDM.Drawing.TurnedProfileReader.TryCall(geo, "GetCircleData", args, 0, 1, 2) == null)
            {
                error = "não deu para ler a aresta (nem a caixa envolvente nem o círculo) — clique numa aresta CIRCULAR.";
                return false;
            }
            double[] c = ToDoubles(args[0]), n = ToDoubles(args[1]);
            double r = Convert.ToDouble(args[2]) * 1000.0;
            if (c == null || n == null || c.Length < 3 || n.Length < 3 || r <= 0)
            {
                error = "o círculo da aresta veio vazio — clique de novo na aresta circular.";
                return false;
            }
            double nLen = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
            for (int i = 0; i < 3; i++) if (nLen > 0 && Math.Abs(n[i]) / nLen > 0.9999) { axis = i; break; }
            if (axis < 0)
            {
                error = "a aresta selecionada não está num plano paralelo a XY, XZ ou YZ. " +
                        "O alojamento de O'ring só é criado com o eixo paralelo a X, Y ou Z.";
                return false;
            }
            centerMm = new[] { c[0] * 1000.0, c[1] * 1000.0, c[2] * 1000.0 };
            diameterMm = 2.0 * r;
            Log.Info($"  [O'ring] caixa da aresta indisponível — lida pelo círculo: Ø{diameterMm:0.000}, eixo {"XYZ"[axis]}.");
            return true;
        }

        /// <summary>
        /// O tipo de canal que a FACE sozinha pede — para a janela acertar o tipo já no clique
        /// da face, antes de qualquer aresta: plana = canal de face; cilíndrica = eixo ou furo
        /// pela normal (aponta para fora do material). null quando a face não é nenhuma das duas
        /// ou a normal não pôde ser lida.
        /// </summary>
        public static GrooveKind? KindOfFace(object comFace)
        {
            if (comFace == null) return null;
            int type = GeomTypeOf(comFace);
            if (type == igPlane) return GrooveKind.AxialFace;
            if (type != igCylinder) return null;

            object geo = GeometryOf(comFace);
            var args = new object[] { new double[3], new double[3], 0.0 };
            if (geo == null || AutoEDM.Drawing.TurnedProfileReader.TryCall(geo, "GetCylinderData", args, 0, 1, 2) == null) return null;
            double[] b = ToDoubles(args[0]), u = ToDoubles(args[1]);
            if (b == null || u == null || b.Length < 3 || u.Length < 3) return null;
            int axis = -1;
            double uLen = Math.Sqrt(u[0] * u[0] + u[1] * u[1] + u[2] * u[2]);
            for (int i = 0; i < 3; i++) if (uLen > 0 && Math.Abs(u[i]) / uLen > 0.9999) { axis = i; break; }
            if (axis < 0) return null;   // eixo inclinado: o Read recusa de qualquer jeito

            var probe = new ORingTarget { AxisIndex = axis, CenterMm = new[] { b[0] * 1000.0, b[1] * 1000.0, b[2] * 1000.0 } };
            bool? away = NormalPointsAwayFromAxis(comFace, probe);
            return away.HasValue ? (away.Value ? GrooveKind.RadialExternal : GrooveKind.RadialInternal) : (GrooveKind?)null;
        }

        /// <summary>
        /// As arestas CIRCULARES da face, uma por círculo (lidas pelo mesmo <see cref="ReadCircle"/>).
        /// Com uma só — a boca do único furo numa face plana, por exemplo — a janela nem precisa
        /// pedir a aresta.
        /// </summary>
        public static List<object> CircularEdgesOf(object comFace)
        {
            var result = new List<object>();
            var seen = new List<double[]>();
            object edges = null;
            try { edges = comFace?.GetType().InvokeMember("Edges", System.Reflection.BindingFlags.GetProperty, null, comFace, null); }
            catch { }
            int n = 0;
            try { n = Convert.ToInt32(edges?.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, edges, null)); }
            catch { }
            for (int i = 1; i <= n; i++)
            {
                object e;
                try { e = edges.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, edges, new object[] { i }); }
                catch { continue; }
                if (GeomTypeOf(e) != igCircle) continue;
                int axis; double[] c; double d; string err;
                if (!ReadCircle(e, out axis, out c, out d, out err)) continue;
                var key = new[] { c[0], c[1], c[2], d };
                if (seen.Any(k => Math.Abs(k[0] - key[0]) < 0.01 && Math.Abs(k[1] - key[1]) < 0.01 &&
                                  Math.Abs(k[2] - key[2]) < 0.01 && Math.Abs(k[3] - key[3]) < 0.01)) continue;
                seen.Add(key);
                result.Add(e);
            }
            return result;
        }

        /// <summary>
        /// Num canal de FACE, para que lado do RAIO o canal se afasta da aresta marcada.
        ///
        /// A aresta pode ser a boca de um FURO (a face continua para fora dela: o canal tem de
        /// ir para FORA, num diâmetro maior) ou o contorno EXTERNO da face — o topo de um eixo,
        /// por exemplo, onde fora da aresta não há mais material e o canal tem de ir para
        /// DENTRO. Quem responde é o tamanho da própria face: se ela mal passa da aresta, a
        /// aresta É o contorno dela.
        /// </summary>
        private static bool GrooveGoesOutward(ORingTarget t, double[] fMin, double[] fMax, int r1, int r2)
        {
            double faceSpan = Math.Max(fMax[r1] - fMin[r1], fMax[r2] - fMin[r2]);
            // 1% de folga cobre facetamento e um chanfro pequeno em volta da aresta.
            bool outward = faceSpan > t.EdgeDiameterMm * 1.01;
            Log.Info($"  [O'ring] face plana de {faceSpan:0.000} mm × aresta Ø{t.EdgeDiameterMm:0.000} → " +
                     $"canal para {(outward ? "FORA (a aresta é a boca de um furo)" : "DENTRO (a aresta é o contorno da face)")}.");
            return outward;
        }

        private static string Describe(ORingTarget t)
        {
            string face = t.FaceIsCylindrical
                ? $"face CILÍNDRICA Ø{t.FaceDiameterMm:0.000}"
                : "face PLANA";
            string kind = t.SuggestedKind == GrooveKind.AxialFace ? "canal anular na face"
                        : t.SuggestedKind == GrooveKind.RadialExternal ? "canal em EIXO"
                        : "canal em FURO";
            return $"{face}, aresta circular Ø{t.EdgeDiameterMm:0.000} em {t.AxisName}={t.EdgeAxialMm:0.000}, " +
                   $"eixo paralelo a {t.AxisName} por ({t.CenterMm[0]:0.00}, {t.CenterMm[1]:0.00}, {t.CenterMm[2]:0.00}) " +
                   $"→ sugestão: {kind}.";
        }

        /// <summary>
        /// Eixo ou furo? PRIMEIRO pela normal da própria face: numa peça sólida a normal aponta
        /// para FORA do material — para longe do eixo num EIXO, para o eixo num FURO. Não
        /// depende do resto da peça.
        ///
        /// A regra antiga (raio do cilindro × alcance do CORPO inteiro) fica só de reserva: ela
        /// errou ao vivo (2026-09-21) numa peça ESCALONADA — um eixo Ø22 sobre uma aba maior
        /// virou "furo", porque o corpo passava do cilindro por causa da aba, e o canal foi
        /// cortado para o lado errado com um anel de furo, que ficou solto.
        /// </summary>
        private static GrooveKind GuessShaftOrBore(object comFace, ORingTarget t, int r1, int r2)
        {
            bool? away = NormalPointsAwayFromAxis(comFace, t);
            if (away.HasValue)
                return away.Value ? GrooveKind.RadialExternal : GrooveKind.RadialInternal;

            Log.Warn("  [O'ring] a normal da face não pôde ser lida — eixo/furo pela regra antiga (alcance do " +
                     "corpo), que ERRA em peça escalonada. Confira o tipo de canal na janela.");
            try
            {
                object body = ((dynamic)comFace).Body;
                double[] bMin, bMax;
                if (body != null && FaceGeometry.TryGetBodyRangeMm(body, out bMin, out bMax))
                {
                    double c1 = t.CenterMm[r1], c2 = t.CenterMm[r2];
                    double bodyReach = Math.Max(
                        Math.Max(Math.Abs(bMax[r1] - c1), Math.Abs(c1 - bMin[r1])),
                        Math.Max(Math.Abs(bMax[r2] - c2), Math.Abs(c2 - bMin[r2])));
                    double faceRadius = t.FaceDiameterMm / 2.0;
                    bool isShaft = faceRadius >= 0.98 * bodyReach;
                    Log.Info($"  [O'ring] raio do cilindro {faceRadius:0.000} × alcance do corpo {bodyReach:0.000} " +
                             $"→ {(isShaft ? "EIXO" : "FURO")}.");
                    return isShaft ? GrooveKind.RadialExternal : GrooveKind.RadialInternal;
                }
            }
            catch (Exception e) { Log.Warn("  [O'ring] eixo-ou-furo indeterminado: " + e.GetBaseException().Message); }
            return GrooveKind.RadialInternal; // furo é o caso mais comum em molde
        }

        /// <summary>
        /// A normal da face, no meio do seu intervalo de parâmetros, aponta para longe do eixo?
        /// <c>Face.GetParamRange</c> (cru, validado ao vivo no BRepRadiusProbe) dá o (u, v) do
        /// meio; <c>Face.GetPointAtParam</c> e <c>Face.GetNormal</c> — mesma forma do
        /// <c>Edge.GetPointAtParam</c> validado no WEDM: (n, [in,out] params, [out] resultado)
        /// by-ref — dão o ponto e a normal ali. null = não deu para ler.
        /// </summary>
        private static bool? NormalPointsAwayFromAxis(object face, ORingTarget t)
        {
            double[] pMin, pMax;
            string why;
            if (!FaceGeometry.TryTwoArrayOut(face, "GetParamRange", out pMin, out pMax, out why) ||
                pMin == null || pMax == null || pMin.Length < 2 || pMax.Length < 2)
            { Log.Warn("  [O'ring] GetParamRange da face: " + (why ?? "vazio")); return null; }
            var uv = new[] { (pMin[0] + pMax[0]) / 2.0, (pMin[1] + pMax[1]) / 2.0 };

            var ptArgs = new object[] { 1, uv.Clone(), new double[0] };
            var nArgs = new object[] { 1, uv.Clone(), new double[0] };
            if (!InvokeByRef(face, "GetPointAtParam", ptArgs, out why)) { Log.Warn("  [O'ring] Face.GetPointAtParam: " + why); return null; }
            if (!InvokeByRef(face, "GetNormal", nArgs, out why)) { Log.Warn("  [O'ring] Face.GetNormal: " + why); return null; }
            double[] p = ToDoubles(ptArgs[2]), n = ToDoubles(nArgs[2]);
            if (p == null || p.Length < 3 || n == null || n.Length < 3) { Log.Warn("  [O'ring] ponto/normal da face vieram vazios."); return null; }

            // Radial = do eixo até o ponto (componente perpendicular ao eixo), em mm.
            var radial = new double[3];
            for (int i = 0; i < 3; i++) radial[i] = i == t.AxisIndex ? 0.0 : p[i] * 1000.0 - t.CenterMm[i];
            double rLen = Math.Sqrt(radial[0] * radial[0] + radial[1] * radial[1] + radial[2] * radial[2]);
            double nLen = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
            if (rLen < 1e-6 || nLen < 1e-12) { Log.Warn("  [O'ring] ponto no eixo ou normal nula."); return null; }
            double cos = (radial[0] * n[0] + radial[1] * n[1] + radial[2] * n[2]) / (rLen * nLen);
            Log.Info($"  [O'ring] normal da face a {rLen:0.000} mm do eixo, cos(normal, radial) = {cos:0.000} → " +
                     $"{(cos > 0 ? "aponta para FORA: EIXO" : "aponta para o eixo: FURO")}.");
            if (Math.Abs(cos) < 0.5) { Log.Warn("  [O'ring] normal quase tangente — inconclusivo."); return null; }
            return cos > 0;
        }

        private static bool InvokeByRef(object com, string method, object[] args, out string why)
        {
            why = null;
            try
            {
                var mod = new System.Reflection.ParameterModifier(args.Length);
                for (int i = 1; i < args.Length; i++) mod[i] = true;
                com.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, com, args,
                    new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception e) { why = e.GetBaseException().Message; return false; }
        }

        private static double[] ToDoubles(object o)
        {
            if (!(o is Array a)) return null;
            var d = new double[a.Length];
            for (int i = 0; i < a.Length; i++) d[i] = Convert.ToDouble(a.GetValue(i));
            return d;
        }

        /// <summary>
        /// De que lado de uma face PLANA está o material — o canal tem de afundar para dentro.
        /// Se a face está no topo do corpo, afunda para −eixo; se está na base, para +eixo.
        /// </summary>
        private static int MaterialSideOfPlanarFace(object comFace, int axis, double faceAxial)
        {
            try
            {
                object body = ((dynamic)comFace).Body;
                double[] bMin, bMax;
                if (body != null && FaceGeometry.TryGetBodyRangeMm(body, out bMin, out bMax))
                    return (faceAxial - bMin[axis]) >= (bMax[axis] - faceAxial) ? -1 : 1;
            }
            catch (Exception e) { Log.Warn("  [O'ring] lado do material indeterminado: " + e.GetBaseException().Message); }
            return -1; // face de topo é o caso comum
        }

        /// <summary>
        /// Tipo da SUPERFÍCIE (plano/cilindro/cone…) ou da CURVA (círculo/reta…) por trás da
        /// face ou aresta.
        ///
        /// ARMADILHA (custou o log de 2026-09-04): <c>Face.Type</c> devolve SEMPRE
        /// <c>igFace</c> e <c>Edge.Type</c> SEMPRE <c>igEdge</c> — é o tipo do objeto de
        /// TOPOLOGIA, não o da geometria. Quem sabe se é plano ou cilindro é
        /// <c>Face.Geometry.Type</c> (o objeto Plane/Cylinder/Cone/…). Ler o <c>.Type</c> da
        /// própria face fazia TODA face plana ser recusada com "Tipo lido: 167551075".
        /// </summary>
        private static int GeomTypeOf(object com)
        {
            object geom = GeometryOf(com);
            if (geom != null)
            {
                try { return Convert.ToInt32(((dynamic)geom).Type); }
                catch (Exception e) { Log.Warn("  [O'ring] Geometry.Type ilegível: " + e.GetBaseException().Message); }
            }
            // Sem geometria: o .Type do próprio objeto ainda serve para o log dizer O QUE veio.
            try { return Convert.ToInt32(((dynamic)com).Type); }
            catch (Exception e) { Log.Warn("  [O'ring] .Type ilegível: " + e.GetBaseException().Message); return 0; }
        }

        /// <summary>Objeto de geometria (Plane, Cylinder, Circle, …) de uma face/aresta.</summary>
        private static object GeometryOf(object com)
        {
            try { return ((dynamic)com).Geometry; }
            catch (Exception e) { Log.Warn("  [O'ring] Geometry indisponível: " + e.GetBaseException().Message); return null; }
        }

        /// <summary>
        /// Raio EXATO (mm) de um cilindro ou círculo — <c>Cylinder.Radius</c> e
        /// <c>Circle.Radius</c> são propriedades simples, sem parâmetro de saída. Vale mais que
        /// a caixa envolvente: numa face cilíndrica PARCIAL (cortada por um rasgo, por exemplo)
        /// a caixa mente o diâmetro, o raio não. Devolve 0 quando não há raio a ler.
        /// </summary>
        private static double RadiusMm(object com)
        {
            object geom = GeometryOf(com);
            if (geom == null) return 0;
            try
            {
                double r = Convert.ToDouble(((dynamic)geom).Radius);
                return r > 0 ? Units.MToMm(r) : 0;
            }
            catch { return 0; } // Plane não tem Radius — caminho normal, não é erro
        }

        /// <summary>Nome legível de um tipo de geometria, para as mensagens não serem só números.</summary>
        private static string GeomName(int type)
        {
            switch (type)
            {
                case igPlane: return "plano";
                case igCylinder: return "cilindro";
                case igCone: return "cone";
                case igSphere: return "esfera";
                case igTorus: return "toro";
                case igBSplineSurface: return "superfície B-spline (livre)";
                case igCircle: return "círculo";
                case igLine: return "reta";
                case igEllipse: return "elipse";
                case igBSplineCurve: return "curva B-spline";
                case igFace: return "face (geometria ilegível)";
                case igEdge: return "aresta (geometria ilegível)";
                default: return "tipo " + type;
            }
        }

        private static void LogEdgeType(object comEdge)
        {
            int type = GeomTypeOf(comEdge);
            if (type != 0 && type != igCircle)
                Log.Warn($"  [O'ring] a aresta é {GeomName(type)}, não um círculo; " +
                         "a caixa envolvente ficou circular, então sigo — confira o diâmetro na janela.");
        }
    }
}
