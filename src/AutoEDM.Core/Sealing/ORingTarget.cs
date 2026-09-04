using System;
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

            double[] eMin, eMax;
            if (!FaceGeometry.TryGetRangeMm(comEdge, out eMin, out eMax))
                return ORingTarget.Fail("não deu para ler a caixa envolvente da aresta — o item selecionado é mesmo uma aresta?");

            // O eixo é a direção em que a aresta circular não tem espessura.
            var span = new[] { eMax[0] - eMin[0], eMax[1] - eMin[1], eMax[2] - eMin[2] };
            int axis = -1;
            for (int i = 0; i < 3; i++) if (Math.Abs(span[i]) <= Tol) { axis = i; break; }
            if (axis < 0)
                return ORingTarget.Fail(
                    "a aresta selecionada não está num plano paralelo a XY, XZ ou YZ. " +
                    "O alojamento de O'ring só é criado com o eixo paralelo a X, Y ou Z.");

            int r1 = (axis + 1) % 3, r2 = (axis + 2) % 3;
            if (Math.Abs(span[r1] - span[r2]) > Math.Max(Tol, 0.01 * span[r1]))
                return ORingTarget.Fail(
                    $"a aresta não é circular (caixa {span[r1]:0.###} × {span[r2]:0.###} mm). " +
                    "Selecione a aresta CIRCULAR que dá o diâmetro de referência.");

            var t = new ORingTarget
            {
                Ok = true,
                AxisIndex = axis,
                CenterMm = new[] { (eMin[0] + eMax[0]) / 2.0, (eMin[1] + eMax[1]) / 2.0, (eMin[2] + eMax[2]) / 2.0 },
                EdgeDiameterMm = (span[r1] + span[r2]) / 2.0
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
        /// Eixo ou furo? Compara o raio do cilindro com o quanto o CORPO se afasta do eixo: se
        /// o cilindro é o contorno externo do corpo, é eixo; se o corpo continua bem além dele,
        /// o cilindro é um furo. Heurística — por isso o resultado é "sugestão" e a janela
        /// deixa trocar.
        /// </summary>
        private static GrooveKind GuessShaftOrBore(object comFace, ORingTarget t, int r1, int r2)
        {
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
