using System;
using System.Collections.Generic;
using System.Linq;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Machinability
{
    /// <summary>Um canto VIVO (R0) entre duas paredes — fresa nenhuma faz, é erosão.</summary>
    public sealed class SharpCornerHit
    {
        /// <summary><c>Edge.ID</c> quando legível. Não sobrevive a rebuild — serve para achar na sessão.</summary>
        public int EdgeId { get; set; }

        public double MinZmm { get; set; }
        public double MaxZmm { get; set; }

        /// <summary>Comprimento da aresta (mm) — a altura do canto.</summary>
        public double LengthMm { get; set; }

        /// <summary>Profundidade (mm) do ponto mais fundo do canto abaixo do topo do corpo.</summary>
        public double DepthMm { get; set; }

        /// <summary>Ângulo entre as duas paredes (graus). 90° = canto de bolsão retangular.</summary>
        public double WallAngleDeg { get; set; }

        /// <summary>Inclinação da aresta em relação a Z (graus). 0 = canto perfeitamente vertical.</summary>
        public double TiltDeg { get; set; }

        /// <summary>Linha pronta para o usuário (pt-BR, vírgula decimal e unidade explícita).</summary>
        public string Describe()
        {
            string where = EdgeId != 0 ? $"aresta {EdgeId}" : "aresta";
            string tilt = TiltDeg >= 1.0 ? $", inclinada {TiltDeg:0.0}°" : "";
            return $"{where}: canto VIVO de {WallAngleDeg:0.#}° entre duas paredes, " +
                   $"{LengthMm:0.0} mm de altura, Z de {MinZmm:0.0} a {MaxZmm:0.0} mm " +
                   $"({DepthMm:0.0} mm abaixo do topo){tilt} — fresa deixa raio, aqui não há. Só EDM.";
        }
    }

    /// <summary>
    /// CANTO VIVO: a outra metade do nível 1, e a que o 1º run não via (Carlos, 2026-09-11:
    /// *"não está reconhecendo os cantos; mesmo que uma usinagem seja possível, se não haver raio
    /// no canto da cavidade é necessário erosão"*).
    ///
    /// **Por que escapava:** <see cref="BRepRadiusProbe"/> mede FACES curvas — cilindro e toro. Um
    /// canto vivo não tem face curva nenhuma: é só uma ARESTA onde duas paredes planas se
    /// encontram, raio ZERO. Procurar raio pequeno demais nunca ia achá-lo, porque não há raio
    /// nenhum para medir. Quem responde é a topologia, não a superfície.
    ///
    /// **A regra:** toda fresa deixa, no canto vertical parede↔parede, um raio igual ao SEU raio —
    /// no mínimo R0,5 com a Ø1. Se o projeto pede canto vivo, nenhuma fresa entrega, em
    /// profundidade nenhuma. É EDM por definição, e não adianta ferramenta mais longa.
    ///
    /// **Só canto VERTICAL conta.** O canto piso↔parede também é vivo e é fresado todo dia — a
    /// fresa de topo reto varre o fundo e encosta na parede. Filtrar por arestas ~paralelas a Z é
    /// o que separa os dois casos, e é a mesma distinção do diagrama do plano.
    ///
    /// **Concavidade: calibrada, não chutada.** Uma aresta viva CONVEXA (quina externa do postiço,
    /// canto de um macho) não é problema nenhum — só a CÔNCAVA é. O sinal de
    /// <c>(n1 × n2) · t</c> separa as duas, mas depende da convenção de orientação do Solid Edge,
    /// que não está no dump. Em vez de assumir, o sinal é CALIBRADO na própria peça: as arestas
    /// verticais que caem na borda da caixa envolvente são convexas por construção (é a quina
    /// externa do bloco). Se elas não concordarem entre si, a análise se recusa a classificar e
    /// diz isso no log — mesma disciplina de <c>OccurrenceTransform</c>, que detecta a arrumação
    /// da matriz em vez de chutar.
    ///
    /// SOMENTE LEITURA. Nunca lança.
    /// </summary>
    public static class SharpCornerProbe
    {
        // FeatureTopologyQueryTypeConstants / GNTTypePropertyConstants (dump da typelib)
        private const int igQueryAll = 1;
        private const int igLine = 167551109;
        private const int igPlane = -1909484335;

        /// <summary>Tolerância (mm) para dizer que a aresta encosta na borda da caixa envolvente.</summary>
        private const double OuterTolMm = 0.01;

        /// <summary>Abaixo disto as duas faces são tangentes/coplanares — não há canto ali.</summary>
        private const double MinWallAngleDeg = 1.0;

        /// <summary>
        /// Cantos vivos verticais e CÔNCAVOS de <paramref name="comBody"/>.
        /// </summary>
        /// <param name="maxTiltDeg">Quanto a aresta pode fugir de Z e ainda contar como canto
        /// vertical. O default cobre saída de molde (1–3°) com folga larga.</param>
        public static IReadOnlyList<SharpCornerHit> Probe(object comBody, double maxTiltDeg = 20.0)
        {
            var hits = new List<SharpCornerHit>();
            if (comBody == null) { Log.Warn("Cantos vivos: corpo nulo — análise não executada."); return hits; }

            if (!FaceGeometry.TryGetBodyRangeMm(comBody, out double[] bodyMin, out double[] bodyMax))
            {
                Log.Warn("Cantos vivos: range do corpo ilegível — sem ele não há como calibrar o sinal de concavidade. Análise pulada.");
                return hits;
            }

            dynamic edges;
            try { edges = ((dynamic)comBody).Edges[igQueryAll]; }
            catch (Exception ex)
            {
                Log.Warn("Cantos vivos: coleção de arestas inacessível — " + ex.GetBaseException().Message);
                return hits;
            }

            int n = 0;
            try { n = (int)edges.Count; } catch { }

            var candidates = new List<Candidate>();
            int notStraight = 0, notVertical = 0, notTwoPlanes = 0, tangent = 0, unreadable = 0;

            for (int i = 1; i <= n; i++)
            {
                object edge;
                try { edge = edges.Item(i); } catch { unreadable++; continue; }

                if (!IsLine(edge)) { notStraight++; continue; }

                if (!EdgeGeometry.TryGetEndPointsMm(edge, out double[] a, out double[] b, out string _))
                { unreadable++; continue; }

                double[] t = { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
                double len = Norm(t);
                if (len <= 1e-9) { unreadable++; continue; }
                for (int k = 0; k < 3; k++) t[k] /= len;

                // Inclinação em relação a Z: |t.z| = cos(ângulo com Z).
                double tiltDeg = Math.Acos(Math.Min(1.0, Math.Abs(t[2]))) * 180.0 / Math.PI;
                if (tiltDeg > maxTiltDeg) { notVertical++; continue; }

                if (!TryTwoPlaneNormals(edge, out double[] n1, out double[] n2)) { notTwoPlanes++; continue; }

                // Paredes tangentes/coplanares não formam canto — e uma parede que encontra um
                // raio de concordância cai aqui, que é justamente o caso JÁ arredondado.
                double dot = Math.Max(-1.0, Math.Min(1.0, Dot(n1, n2)));
                double wallAngleDeg = 180.0 - Math.Acos(dot) * 180.0 / Math.PI;
                if (wallAngleDeg > 180.0 - MinWallAngleDeg || wallAngleDeg < MinWallAngleDeg) { tangent++; continue; }

                double sign = Dot(Cross(n1, n2), t);
                if (Math.Abs(sign) < 1e-9) { tangent++; continue; }

                double midX = 0.5 * (a[0] + b[0]), midY = 0.5 * (a[1] + b[1]);
                bool onOuterBox =
                    Math.Abs(midX - bodyMin[0]) <= OuterTolMm || Math.Abs(midX - bodyMax[0]) <= OuterTolMm ||
                    Math.Abs(midY - bodyMin[1]) <= OuterTolMm || Math.Abs(midY - bodyMax[1]) <= OuterTolMm;

                candidates.Add(new Candidate
                {
                    Edge = edge,
                    Sign = sign > 0 ? 1 : -1,
                    OnOuterBox = onOuterBox,
                    MinZmm = Math.Min(a[2], b[2]),
                    MaxZmm = Math.Max(a[2], b[2]),
                    LengthMm = len,
                    WallAngleDeg = wallAngleDeg,
                    TiltDeg = tiltDeg
                });
            }

            Log.Info($"Cantos vivos: {n} aresta(s) — {candidates.Count} candidata(s) (viva, reta, vertical, entre 2 planos). " +
                     $"Descartadas: {notStraight} não retas, {notVertical} não verticais, {notTwoPlanes} sem 2 faces planas, " +
                     $"{tangent} tangentes/coplanares" + (unreadable > 0 ? $", {unreadable} ilegíveis" : "") + ".");

            if (candidates.Count == 0) return hits;

            int convexSign;
            if (!TryCalibrateConvexSign(candidates, out convexSign)) return hits;

            double topZmm = bodyMax[2];
            foreach (var c in candidates.Where(c => c.Sign != convexSign))
            {
                hits.Add(new SharpCornerHit
                {
                    EdgeId = TryEdgeId(c.Edge),
                    MinZmm = c.MinZmm,
                    MaxZmm = c.MaxZmm,
                    LengthMm = c.LengthMm,
                    DepthMm = Math.Max(0.0, topZmm - c.MinZmm),
                    WallAngleDeg = c.WallAngleDeg,
                    TiltDeg = c.TiltDeg
                });
            }

            Log.Info($"Cantos vivos: {hits.Count} canto(s) vivo(s) CÔNCAVO(s) — nenhuma fresa faz, é EDM.");
            foreach (var h in hits.OrderByDescending(h => h.LengthMm)) Log.Info("  • " + h.Describe());
            return hits;
        }

        /// <summary>
        /// Descobre qual sinal de <c>(n1 × n2) · t</c> significa CONVEXO **nesta peça**, usando as
        /// arestas verticais que encostam na borda da caixa envolvente: a quina externa de um bloco
        /// é convexa por construção. Se elas divergirem, não há calibração confiável e a análise
        /// recusa em vez de classificar errado.
        /// </summary>
        private static bool TryCalibrateConvexSign(List<Candidate> candidates, out int convexSign)
        {
            convexSign = 0;
            var outer = candidates.Where(c => c.OnOuterBox).ToList();
            if (outer.Count == 0)
            {
                Log.Warn("Cantos vivos: nenhuma aresta viva na borda da caixa envolvente para calibrar o sinal de " +
                         "concavidade (peça sem quina externa reta?). Nada classificado — a convenção de orientação " +
                         "do SE não é documentada e não vai ser chutada.");
                return false;
            }

            int plus = outer.Count(c => c.Sign > 0);
            int minus = outer.Count - plus;
            if (plus > 0 && minus > 0)
            {
                Log.Warn($"Cantos vivos: as {outer.Count} aresta(s) externa(s) de calibração DIVERGEM ({plus} com sinal +, " +
                         $"{minus} com −) — ou a caixa envolvente pegou aresta interna, ou a convenção não é uniforme. " +
                         "Nada classificado nesta rodada; o número acima é a evidência para ajustar o critério.");
                return false;
            }

            convexSign = plus > 0 ? 1 : -1;
            Log.Info($"Cantos vivos: sinal CONVEXO calibrado em {convexSign:+0;-0} por {outer.Count} aresta(s) " +
                     "na borda da caixa envolvente (quina externa do bloco).");
            return true;
        }

        private sealed class Candidate
        {
            public object Edge;
            public int Sign;
            public bool OnOuterBox;
            public double MinZmm, MaxZmm, LengthMm, WallAngleDeg, TiltDeg;
        }

        private static bool IsLine(object comEdge)
        {
            try { return (int)((dynamic)comEdge).Geometry.Type == igLine; }
            catch { return false; }
        }

        /// <summary>
        /// As DUAS faces da aresta, ambas planas, com suas normais.
        ///
        /// <c>Edge.GetFaces([out] NumFaces: Int32, [out] Faces: Array)</c> — o contador é um [out]
        /// ESCALAR, que é o que não popula confiável em late binding; quem manda é o tamanho do
        /// array, mesma regra do <c>FacetCount</c>. A normal vem de
        /// <c>Plane.GetNormalVector([out] NormalVector)</c>: um array só, adimensional — NÃO passa
        /// por metros→mm.
        /// </summary>
        private static bool TryTwoPlaneNormals(object comEdge, out double[] n1, out double[] n2)
        {
            n1 = null; n2 = null;
            object[] faces;
            try
            {
                object[] args = { 0, new object[0] };
                var mod = new System.Reflection.ParameterModifier(2);
                mod[0] = true;
                mod[1] = true;
                comEdge.GetType().InvokeMember("GetFaces",
                    System.Reflection.BindingFlags.InvokeMethod, null, comEdge, args,
                    new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);

                var arr = args[1] as Array;
                if (arr == null || arr.Length != 2) return false;
                faces = new[] { arr.GetValue(0), arr.GetValue(1) };
            }
            catch { return false; }

            return TryPlaneNormal(faces[0], out n1) && TryPlaneNormal(faces[1], out n2);
        }

        private static bool TryPlaneNormal(object comFace, out double[] normal)
        {
            normal = null;
            try
            {
                dynamic geometry = ((dynamic)comFace).Geometry;
                if ((int)geometry.Type != igPlane) return false;
                if (!FaceGeometry.TryOneArrayOut((object)geometry, "GetNormalVector", out double[] v, out string _))
                    return false;
                double len = Norm(v);
                if (len <= 1e-12) return false;
                normal = new[] { v[0] / len, v[1] / len, v[2] / len };
                return true;
            }
            catch { return false; }
        }

        private static int TryEdgeId(object comEdge)
        {
            try { return (int)((dynamic)comEdge).ID; } catch { return 0; }
        }

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };

        private static double Norm(double[] v) => Math.Sqrt(Dot(v, v));
    }
}
