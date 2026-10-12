using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Reverse
{
    /// <summary>O plano completo de uma malha: direção escolhida, faixas, correções e a previsão.</summary>
    public sealed class MeshToSolidPlan
    {
        public ExtrusionFrame Frame;
        public PrismaticPlan Plan;
        public CorrectionPlan Corrections;

        /// <summary>Previsão do sólido contra a malha, no frame do plano.</summary>
        public PlanEvaluation Evaluation;

        /// <summary>Uma linha por direção avaliada (erro longe e volume), para o relatório dizer
        /// POR QUE a direção escolhida venceu.</summary>
        public readonly List<string> DirectionNotes = new List<string>();

        /// <summary>true = a direção foi imposta por quem chamou, não escolhida.</summary>
        public bool Forced;

        /// <summary>
        /// Volume de VERDADE da malha: o medido por pontos (enrolamento) quando a previsão rodou,
        /// senão o da fórmula com sinal. A fórmula erra em malha com cascas sobrepostas — no
        /// stack-container um corpo dava 2.367 mm³ pela fórmula e 2.700 por pontos, e o sólido
        /// construído (2.675) parecia ter "13% a mais".
        /// </summary>
        public double RealMeshVolumeMm3 =>
            Evaluation != null && Evaluation.Samples > 0 ? Evaluation.MeshVolumeMm3 : (Plan != null ? Plan.MeshVolumeMm3 : 0);

        /// <summary>A fórmula e os pontos discordam em mais de 3%: a malha tem cascas sobrepostas
        /// ou invertidas, e o volume "da malha" pela fórmula não serve de referência.</summary>
        public bool OverlappingShells =>
            Plan != null && Evaluation != null && Evaluation.Samples > 0 &&
            Math.Abs(Evaluation.MeshVolumeMm3 - Plan.MeshVolumeMm3) > 0.03 * Math.Abs(Evaluation.MeshVolumeMm3);

        /// <summary>
        /// A peça está FORA do alcance da rota 2,5D: mesmo na melhor direção, o erro de forma
        /// previsto passa de <see cref="MeshToSolidPlanner.OutOfScopeFraction"/> do volume. Peça
        /// com detalhes em várias direções (a estrutura do frame-fidget-mini-claw-machine:
        /// ranhuras concêntricas numa face, braços e pinos em outras) — construir dá um sólido que
        /// não se parece com ela, e o relatório tem de dizer isso ANTES.
        /// </summary>
        public bool OutOfScope
        {
            get
            {
                if (Plan == null || Plan.Bands.Count == 0) return true;
                if (Evaluation == null || Evaluation.Samples == 0) return false;
                return Evaluation.FarErrorMm3 > MeshToSolidPlanner.OutOfScopeFraction * Math.Abs(RealMeshVolumeMm3);
            }
        }
    }

    /// <summary>
    /// Fachada da rota 2,5D: escolhe a DIREÇÃO de extrusão e devolve o plano, as correções e a
    /// previsão. Os três caminhos (botão, MCP, lote) passam por aqui, para nunca divergirem.
    ///
    /// A escolha (2026-10-11): Z continua sendo o padrão — placa, postiço e eletrodo são 2,5D
    /// em Z, e os corpos do Rack_Anghinoni saem exatos nele. Só quando a PREVISÃO em Z acusa erro
    /// de forma (discordância longe da superfície da malha acima de
    /// <see cref="AcceptFarFraction"/> do volume) é que X e Y são avaliados, e vence a de menor
    /// erro previsto. Na vector_v2-6 (canal em U dobrado) Z previa ~3.100 mm³ de erro e Y, ~30.
    /// Depois de X e Y vêm as normais dos maiores planos fora dos eixos (peça girada no espaço).
    /// Todas são avaliadas — parar na primeira "aceitável" escolhia, num V girado, a normal de
    /// uma parede lateral (que tem mais área que as tampas e as correções quase consertam).
    /// </summary>
    public static class MeshToSolidPlanner
    {
        /// <summary>Erro previsto longe da superfície, como fração do volume, até onde Z basta sem
        /// olhar as outras direções (com piso de 1 mm³). Era 0,5% e deixava passar um Z de 27
        /// faixas escalonadas no elo do frame-fidget (2,9 mm³) quando Y dava 0,2 em 11.</summary>
        public const double AcceptFarFraction = 0.001;

        /// <summary>Erro previsto, como fração do volume, acima do qual a peça está fora do alcance.</summary>
        public const double OutOfScopeFraction = 0.02;

        /// <summary>Pontos da previsão. 60 mil: em 77 cm³ de caixa, cada ponto vale ~1,3 mm³ —
        /// separa "30" de "3.000" com folga, em poucos segundos.</summary>
        public const int EvaluationSamples = 60000;

        /// <param name="direction">null ou "auto" = escolhe; "X", "Y" ou "Z" = impõe.</param>
        public static MeshToSolidPlan Plan(double[] pointsMm, RecognitionResult recognition, string direction)
        {
            if (pointsMm == null) throw new ArgumentNullException(nameof(pointsMm));
            if (recognition == null) throw new ArgumentNullException(nameof(recognition));

            int forced = ParseAxis(direction);
            if (forced >= 0)
            {
                MeshToSolidPlan only = PlanAlong(pointsMm, recognition, new ExtrusionFrame(forced));
                only.Forced = true;
                only.DirectionNotes.Add(Note(only) + " (imposta)");
                return only;
            }

            MeshToSolidPlan z = PlanAlong(pointsMm, recognition, ExtrusionFrame.Z);
            z.DirectionNotes.Add(Note(z));
            if (Acceptable(z)) return z;

            MeshToSolidPlan best = z;
            var notes = new List<string> { Note(z) };
            var frames = new List<ExtrusionFrame> { new ExtrusionFrame(0), new ExtrusionFrame(1) };
            foreach (double[] d in PlaneDirections(recognition, 4)) frames.Add(ExtrusionFrame.Along(d));
            foreach (ExtrusionFrame f in frames)
            {
                MeshToSolidPlan c = PlanAlong(pointsMm, recognition, f);
                notes.Add(Note(c));
                if (Better(c, best)) best = c;
            }
            best.DirectionNotes.Clear();
            best.DirectionNotes.AddRange(notes);
            return best;
        }

        /// <summary>
        /// c é melhor que best? Menor erro previsto; quase empatados (diferença até 1 mm³ ou 0,1%
        /// do volume), vence a de MENOS faixas e depois a de menos trechos copiados da malha — o
        /// elo do frame-fidget saía em 9 faixas escalonadas numa direção e em 3 limpas na outra,
        /// com erros parecidos.
        /// </summary>
        private static bool Better(MeshToSolidPlan c, MeshToSolidPlan best)
        {
            if (c.Plan.Bands.Count == 0) return false;
            if (best.Plan.Bands.Count == 0) return true;
            double tie = Math.Max(1.0, 0.001 * Math.Abs(c.Plan.MeshVolumeMm3));
            double d = c.Evaluation.FarErrorMm3 - best.Evaluation.FarErrorMm3;
            if (d < -tie) return true;
            if (d > tie) return false;
            if (c.Plan.Bands.Count != best.Plan.Bands.Count) return c.Plan.Bands.Count < best.Plan.Bands.Count;
            return Approximated(c.Plan) < Approximated(best.Plan);
        }

        private static int Approximated(PrismaticPlan p)
        {
            int n = 0;
            foreach (SectionBand b in p.Bands) foreach (SectionLoop l in b.Loops) foreach (SectionElement e in l.Elements) if (!e.Exact) n++;
            return n;
        }

        /// <summary>
        /// Z basta sem olhar as outras? Erro previsto pequeno E sem degrau de faixa: degrau é
        /// faixa escalonada sobre raio ou rampa deitados, consertada por correção — o elo do
        /// frame-fidget saía em Z com 27 faixas e 61 correções e erro 0,5 mm³, e em Y com 11
        /// faixas, 50 correções e 0,2. Com degrau, as outras direções são avaliadas e o
        /// desempate de <see cref="Better"/> (menos faixas) decide.
        /// </summary>
        private static bool Acceptable(MeshToSolidPlan p)
        {
            if (p.Plan.Bands.Count == 0) return false;
            if (p.Corrections.Regions.Exists(r => r.BandStep)) return false;
            double volume = Math.Abs(p.Plan.MeshVolumeMm3);
            return p.Evaluation.FarErrorMm3 <= Math.Max(1.0, AcceptFarFraction * volume);
        }

        private static MeshToSolidPlan PlanAlong(double[] pointsMm, RecognitionResult recognition, ExtrusionFrame frame)
        {
            double[] local = frame.ToLocalPoints(pointsMm);
            RecognitionResult r = frame.ToLocal(recognition);
            var result = new MeshToSolidPlan { Frame = frame };
            result.Plan = PrismaticSection.Plan(local, r, new PrismaticOptions { Frame = frame });
            result.Corrections = BoundaryCorrections.Plan(local, r, result.Plan);
            result.Evaluation = result.Plan.Bands.Count > 0
                ? PlanEvaluator.Evaluate(local, result.Plan, result.Corrections, EvaluationSamples, 1)
                : new PlanEvaluation();
            return result;
        }

        /// <summary>
        /// Direções FORA dos eixos que valem tentar: as normais dos planos que somam mais área
        /// (normal e oposta juntas, a menos de 0,5°). Peça extrudada e girada no espaço (as do
        /// frame-fidget-mini-claw-machine, 30° em volta de Y) tem as duas tampas com a normal
        /// exatamente na direção da extrusão — e são, de longe, os maiores planos dela.
        /// </summary>
        public static List<double[]> PlaneDirections(RecognitionResult r, int count)
        {
            var dirs = new List<double[]>();
            var area = new List<double>();
            double cosTol = Math.Cos(0.5 * Math.PI / 180);
            foreach (RecognizedSurface s in r.Surfaces)
            {
                if (s.Kind != "plano" || s.Normal == null) continue;
                double[] n = s.Normal;
                if (Math.Abs(n[0]) == 1 || Math.Abs(n[1]) == 1 || Math.Abs(n[2]) == 1) continue;
                int hit = -1;
                for (int k = 0; k < dirs.Count && hit < 0; k++)
                    if (Math.Abs(n[0] * dirs[k][0] + n[1] * dirs[k][1] + n[2] * dirs[k][2]) >= cosTol) hit = k;
                if (hit < 0) { dirs.Add(n); area.Add(s.AreaMm2); }
                else area[hit] += s.AreaMm2;
            }
            var order = new List<int>();
            for (int k = 0; k < dirs.Count; k++) order.Add(k);
            order.Sort((a, b) => area[b].CompareTo(area[a]));
            var result = new List<double[]>();
            for (int k = 0; k < order.Count && k < count; k++) result.Add(dirs[order[k]]);
            return result;
        }

        /// <summary>-1 = escolher; 0/1/2 = X/Y/Z.</summary>
        public static int ParseAxis(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction)) return -1;
            switch (direction.Trim().ToUpperInvariant())
            {
                case "X": return 0;
                case "Y": return 1;
                case "Z": return 2;
                default: return -1;
            }
        }

        private static string Note(MeshToSolidPlan p)
        {
            if (p.Plan.Bands.Count == 0) return "extrusão em " + p.Frame.AxisName + ": nenhuma faixa";
            return string.Format(CultureInfo.InvariantCulture,
                "extrusão em {0}: erro de forma previsto {1:0.#} mm³, {2} faixa(s), {3} correção(ões)",
                p.Frame.AxisName, p.Evaluation.FarErrorMm3, p.Plan.Bands.Count, p.Corrections.Regions.Count);
        }
    }
}
