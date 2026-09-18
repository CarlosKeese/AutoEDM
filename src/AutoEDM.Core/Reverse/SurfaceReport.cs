using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoEDM.Reverse
{
    /// <summary>
    /// Transforma o resultado do <see cref="SurfaceRecognizer"/> em texto para o agente.
    ///
    /// É PURO de propósito (nenhuma dependência de COM): o formato do relatório é a parte que
    /// mais muda, e tem de poder ser testado sem a Solid Edge aberta.
    ///
    /// A ordem do relatório não é estética. Primeiro o que foi medido (triângulos, extensão),
    /// depois cada superfície com o RMS do próprio ajuste, e só no fim o veredito — porque o
    /// veredito é leitura dos números, e quem lê tem de poder discordar dele olhando os mesmos
    /// números.
    /// </summary>
    public static class SurfaceReport
    {
        /// <summary>Superfícies listadas uma a uma antes de o relatório passar a resumir.</summary>
        public const int MaxListed = 40;

        public static string Format(RecognitionResult r, string route, string bodyName)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var sb = new StringBuilder();

            sb.AppendLine("Reconhecimento de superfícies sobre malha" +
                          (string.IsNullOrEmpty(bodyName) ? "" : " — corpo '" + bodyName + "'"));
            if (!string.IsNullOrEmpty(route)) sb.AppendLine("Leitura: " + route);

            sb.AppendLine($"Triângulos: {r.TriangleCount}" +
                          (r.DegenerateTriangles > 0 ? $" ({r.DegenerateTriangles} degenerado(s), descartado(s))" : ""));
            sb.AppendLine($"Área total: {F(r.TotalAreaMm2)} mm²");

            if (r.MinMm != null && r.MaxMm != null)
                sb.AppendLine($"Extensão: {F(r.MaxMm[0] - r.MinMm[0])} × {F(r.MaxMm[1] - r.MinMm[1])} × " +
                              $"{F(r.MaxMm[2] - r.MinMm[2])} mm   " +
                              $"(de {P(r.MinMm)} a {P(r.MaxMm)})");

            if (r.TriangleCount == 0 || r.Surfaces.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Nenhuma superfície reconhecida — a malha está vazia ou toda degenerada.");
                return sb.ToString();
            }

            // ------------------------------------------------------------------ contagem
            int planes = 0, cylinders = 0, free = 0;
            double planeArea = 0, cylArea = 0, freeArea = 0;
            foreach (RecognizedSurface s in r.Surfaces)
            {
                if (s.Kind == "plano") { planes++; planeArea += s.AreaMm2; }
                else if (s.Kind == "cilindro") { cylinders++; cylArea += s.AreaMm2; }
                else { free++; freeArea += s.AreaMm2; }
            }

            sb.AppendLine();
            sb.AppendLine($"{planes} plano(s), {cylinders} cilindro(s), {free} região(ões) livre(s).");
            sb.AppendLine($"Da área: {Pct(planeArea, r.TotalAreaMm2)} em plano, " +
                          $"{Pct(cylArea, r.TotalAreaMm2)} em cilindro, " +
                          $"{Pct(freeArea, r.TotalAreaMm2)} em forma livre.");

            // ------------------------------------------------------------------ a lista
            sb.AppendLine();
            sb.AppendLine("— Superfícies, da maior área para a menor —");

            int listed = 0;
            foreach (RecognizedSurface s in r.Surfaces)
            {
                if (listed >= MaxListed) break;
                listed++;
                sb.AppendLine("  " + Describe(s, listed, r.TotalAreaMm2));
            }

            if (r.Surfaces.Count > listed)
            {
                int rest = r.Surfaces.Count - listed;
                double restArea = 0;
                for (int i = listed; i < r.Surfaces.Count; i++) restArea += r.Surfaces[i].AreaMm2;
                sb.AppendLine($"  … e mais {rest} região(ões) menor(es), somando {F(restArea)} mm² " +
                              $"({Pct(restArea, r.TotalAreaMm2)} da área).");
            }

            // ------------------------------------------------------------------ veredito
            sb.AppendLine();
            sb.AppendLine("— O que isto decide —");
            double recognised = planeArea + cylArea;
            sb.AppendLine($"Plano + cilindro cobrem {Pct(recognised, r.TotalAreaMm2)} da área da malha.");

            // O mosaico ANULA o veredito, não convive com ele: dizer "atenção, isto pode ser
            // curvo" e em seguida "a peça é prismática" seria o relatório se contradizendo no
            // mesmo parágrafo, e quem lê ficaria com a segunda frase.
            if (IsMosaic(r, planeArea))
            {
                sb.AppendLine("MOSAICO — veredito suspenso. A maior parte da área \"plana\" está em muitas plaquinhas " +
                              "pequenas, não em faces inteiras. É o que acontece quando uma superfície CURVA cabe " +
                              "dentro da tolerância de planaridade: o reconhecimento está seguindo a tolerância, não a " +
                              "peça. Não dá para chamar isto de prismático.");
                sb.AppendLine("Baixe 'distanciaPlanoMm' (ou 'anguloPlanoGraus') e rode de novo: se as plaquinhas " +
                              "sumirem e a área migrar para 'livre', a peça é de forma livre; se elas virarem faces " +
                              "grandes, aí sim era prismática.");
                return sb.ToString();
            }

            double share = r.TotalAreaMm2 > 0 ? recognised / r.TotalAreaMm2 : 0;
            if (share >= 0.95)
                sb.AppendLine("A peça é PRISMÁTICA: a reconstrução por primitivas (extrusão + furo de eixo e Ø reais) " +
                              "cobre praticamente tudo, e o kernel free-form não se justifica para esta geometria.");
            else if (share >= 0.70)
                sb.AppendLine("A peça é MISTA: o grosso sai por primitiva, mas sobra forma livre relevante. " +
                              "Reconstruir o prismático primeiro e tratar o resto como superfície é o caminho barato.");
            else
                sb.AppendLine("A peça é predominantemente de FORMA LIVRE: reconstruir por primitivas deixaria de fora " +
                              "a maior parte da área. Aqui um kernel de superfície é inevitável — ou o caminho é manter " +
                              "a malha e trabalhar por seções.");

            return sb.ToString();
        }

        /// <summary>
        /// Detecta o MOSAICO: área plana que não vem de faces, e sim de muitas plaquinhas dentro
        /// da tolerância. Uma esfera fina, medida a 0,05 mm, se deixa cobrir por centenas de
        /// pedacinhos "planos" — e sem este aviso o relatório chamaria de prismática uma peça que
        /// não tem um plano sequer. O critério é o que distingue os dois casos: face de verdade é
        /// GRANDE em relação à peça; plaquinha de tolerância é pequena e vem às centenas.
        /// </summary>
        internal static bool IsMosaic(RecognitionResult r, double planeArea)
        {
            if (planeArea <= 0 || r.TotalAreaMm2 <= 0) return false;

            double cutoff = r.TotalAreaMm2 * 0.01;   // 1% da área da malha
            double smallArea = 0;
            int smallCount = 0;

            foreach (RecognizedSurface s in r.Surfaces)
            {
                if (s.Kind != "plano" || s.AreaMm2 >= cutoff) continue;
                smallArea += s.AreaMm2;
                smallCount++;
            }

            return smallCount >= 8 && smallArea > planeArea * 0.5;
        }

        private static string Describe(RecognizedSurface s, int index, double totalArea)
        {
            string head = $"[{index}] {s.Kind,-9} {F(s.AreaMm2),10} mm² ({Pct(s.AreaMm2, totalArea),5})  " +
                          $"{s.TriangleCount,5} tri";

            if (s.Kind == "plano")
                return head + $"  normal {P(s.Normal)} {AxisName(s.Normal)}  em {P(s.PointMm)}  " +
                       $"RMS {F3(s.FitRmsMm)} mm";

            if (s.Kind == "cilindro")
                return head + $"  Ø{F(s.RadiusMm * 2)} × {F(s.LengthMm)} mm  eixo {P(s.AxisMm)} {AxisName(s.AxisMm)}  " +
                       $"centro {P(s.PointMm)}  volta {F0(s.SweepDeg)}°  RMS {F3(s.FitRmsMm)} mm";

            return head + $"  caixa {F(s.MaxMm[0] - s.MinMm[0])} × {F(s.MaxMm[1] - s.MinMm[1])} × " +
                   $"{F(s.MaxMm[2] - s.MinMm[2])} mm";
        }

        /// <summary>Nomeia a direção quando ela é um eixo do sistema — quem lê procura "Z+", não
        /// "(0, 0, 1)". Fora dos eixos, fica em branco em vez de inventar nome.</summary>
        internal static string AxisName(double[] v)
        {
            if (v == null || v.Length < 3) return "";
            const double tol = 0.02;

            if (Math.Abs(Math.Abs(v[0]) - 1) < tol && Math.Abs(v[1]) < tol && Math.Abs(v[2]) < tol)
                return v[0] > 0 ? "(X+)" : "(X−)";
            if (Math.Abs(Math.Abs(v[1]) - 1) < tol && Math.Abs(v[0]) < tol && Math.Abs(v[2]) < tol)
                return v[1] > 0 ? "(Y+)" : "(Y−)";
            if (Math.Abs(Math.Abs(v[2]) - 1) < tol && Math.Abs(v[0]) < tol && Math.Abs(v[1]) < tol)
                return v[2] > 0 ? "(Z+)" : "(Z−)";
            return "";
        }

        private static string P(double[] v)
        {
            if (v == null || v.Length < 3) return "(?)";
            return $"({F(v[0])}, {F(v[1])}, {F(v[2])})";
        }

        private static string Pct(double part, double whole)
        {
            if (whole <= 0) return "0%";
            return (part / whole * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string F3(double v) => double.IsNaN(v) ? "—" : v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
