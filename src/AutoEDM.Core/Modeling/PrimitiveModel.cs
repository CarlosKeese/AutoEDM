using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;

namespace AutoEDM.Modeling
{
    /// <summary>
    /// Um sólido primitivo a ser criado numa peça. Tudo em MILÍMETROS na fronteira (a conversão
    /// para metros é do <see cref="BlankModeler"/>, que é quem fala com o COM).
    /// </summary>
    public sealed class Primitive
    {
        /// <summary>"caixa" ou "cilindro".</summary>
        public string Kind { get; set; }

        /// <summary>Rótulo só para o log e o relatório — ajuda a achar qual peça falhou.</summary>
        public string Name { get; set; }

        /// <summary>Caixa: lado no eixo U do plano. Ignorado no cilindro.</summary>
        public double SizeXMm { get; set; }

        /// <summary>Caixa: lado no eixo V do plano. Ignorado no cilindro.</summary>
        public double SizeYMm { get; set; }

        /// <summary>Cilindro: diâmetro. Ignorado na caixa.</summary>
        public double DiameterMm { get; set; }

        /// <summary>Altura da extrusão, ao longo da NORMAL do plano.</summary>
        public double HeightMm { get; set; }

        /// <summary>Qual <c>RefPlanes.Item(n)</c> recebe o esboço. É ele que define o EIXO da
        /// extrusão — use 'se_planos' para descobrir qual índice é qual plano nesta peça, em vez
        /// de supor.</summary>
        public int PlaneIndex { get; set; } = 1;

        /// <summary>1 = contra a normal, 2 = a favor, 3 = simétrico.</summary>
        public int ExtrudeSide { get; set; } = 2;

        /// <summary>Distância que a BASE se desloca ao longo da normal do plano (o "Z" local).
        /// Sempre positiva — quem escolhe o SENTIDO é <see cref="LiftSide"/>.</summary>
        public double LiftMm { get; set; }

        /// <summary>Sentido do deslocamento da base: 2 = a favor da normal, 1 = contra.
        /// Existe porque a distância de <c>AddParallelByDistance</c> é positiva e a direção é um
        /// argumento à parte — deslocar para o lado negativo é <c>LiftSide = 1</c>, não distância
        /// negativa.</summary>
        public int LiftSide { get; set; } = 2;

        /// <summary>Centro da seção no eixo U do plano.</summary>
        public double CenterXMm { get; set; }

        /// <summary>Centro da seção no eixo V do plano.</summary>
        public double CenterYMm { get; set; }

        /// <summary>Motivo pelo qual este primitivo é inválido, ou null se estiver bom.</summary>
        public string Validate()
        {
            string kind = (Kind ?? "").Trim().ToLowerInvariant();
            if (kind != "caixa" && kind != "cilindro")
                return $"'{Name ?? "(sem nome)"}': tipo '{Kind}' desconhecido — use \"caixa\" ou \"cilindro\".";

            if (HeightMm <= 0) return $"'{Name}': altura tem de ser maior que zero (veio {HeightMm}).";

            if (kind == "caixa")
            {
                if (SizeXMm <= 0 || SizeYMm <= 0)
                    return $"'{Name}': caixa precisa de sizeXMm e sizeYMm maiores que zero (veio {SizeXMm}×{SizeYMm}).";
            }
            else if (DiameterMm <= 0)
                return $"'{Name}': cilindro precisa de diameterMm maior que zero (veio {DiameterMm}).";

            if (PlaneIndex < 1) return $"'{Name}': planeIndex é 1-based (veio {PlaneIndex}).";
            if (ExtrudeSide < 1 || ExtrudeSide > 3)
                return $"'{Name}': extrudeSide tem de ser 1, 2 ou 3 (veio {ExtrudeSide}).";
            if (LiftSide != 1 && LiftSide != 2)
                return $"'{Name}': liftSide tem de ser 1 ou 2 (veio {LiftSide}).";
            if (LiftMm < 0)
                return $"'{Name}': liftMm é uma DISTÂNCIA e não aceita negativo (veio {LiftMm}) — " +
                       "para o outro lado use liftSide = 1.";
            return null;
        }

        public bool IsCylinder => string.Equals((Kind ?? "").Trim(), "cilindro", StringComparison.OrdinalIgnoreCase);

        public override string ToString() => IsCylinder
            ? $"{Name}: cilindro Ø{F(DiameterMm)}×{F(HeightMm)} mm"
            : $"{Name}: caixa {F(SizeXMm)}×{F(SizeYMm)}×{F(HeightMm)} mm";

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public sealed class PrimitiveBuildResult
    {
        public bool Ok { get; set; }
        public int Created { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; }
        public List<string> Lines { get; } = new List<string>();
    }

    /// <summary>
    /// Cria sólidos primitivos numa peça a partir de uma lista declarativa, reusando VERBATIM as
    /// receitas do <see cref="BlankModeler"/> — <c>CreateBox</c> e <c>CreateCylinder</c>, ambas já
    /// validadas no Solid Edge pelo botão "Criar Base". Nenhuma receita de modelagem nova é
    /// inventada aqui: esta classe só posiciona e orquestra.
    ///
    /// Protrusões sucessivas (<c>AddFiniteExtrudedProtrusion</c>) FUNDEM no mesmo corpo, então uma
    /// lista de caixas e cilindros que se tocam sai como um sólido único.
    ///
    /// Isto não é só para desenhar: é a peça que falta para a rota PRISMÁTICA da engenharia
    /// reversa, onde o resultado de seccionar a malha vira exatamente esta lista — extrusão por
    /// contorno, cilindro por furo.
    /// </summary>
    public static class PrimitiveModeler
    {
        public static PrimitiveBuildResult Build(dynamic partDoc, IList<Primitive> primitives)
        {
            var result = new PrimitiveBuildResult();

            if (primitives == null || primitives.Count == 0)
            {
                result.Message = "Nenhum primitivo na lista — nada a criar.";
                return result;
            }

            // Valida TUDO antes de criar qualquer coisa: meio-modelo na peça do usuário é pior
            // que recusa, porque ele tem de descobrir e desfazer à mão o que entrou.
            var problems = new List<string>();
            for (int i = 0; i < primitives.Count; i++)
            {
                string bad = primitives[i] == null
                    ? $"primitivo [{i}] é nulo."
                    : primitives[i].Validate();
                if (bad != null) problems.Add(bad);
            }
            if (problems.Count > 0)
            {
                result.Message = "RECUSADO antes de tocar na peça — " + problems.Count +
                                 " primitivo(s) inválido(s):\n  " + string.Join("\n  ", problems.ToArray());
                return result;
            }

            Log.Info($"===== MODELAR PRIMITIVAS ({primitives.Count}) =====");

            foreach (Primitive p in primitives)
            {
                try
                {
                    if (p.IsCylinder)
                    {
                        BlankModeler.CreateCylinder(partDoc, p.DiameterMm, p.HeightMm,
                            planeIndex: p.PlaneIndex, extrudeSide: p.ExtrudeSide,
                            baseLiftMm: p.LiftMm, centerXmm: p.CenterXMm, centerYmm: p.CenterYMm,
                            liftSide: p.LiftSide);
                    }
                    else
                    {
                        BlankModeler.CreateBox(partDoc, p.SizeXMm, p.SizeYMm, p.HeightMm,
                            planeIndex: p.PlaneIndex, extrudeSide: p.ExtrudeSide,
                            baseLiftMm: p.LiftMm, centerXmm: p.CenterXMm, centerYmm: p.CenterYMm,
                            liftSide: p.LiftSide);
                    }
                    result.Created++;
                    result.Lines.Add("  OK    " + p);
                    Log.Info("Primitivo criado — " + p);
                }
                catch (Exception ex)
                {
                    // Um primitivo que falha não aborta o resto: num carrinho, perder uma roda e
                    // saber QUAL perdeu vale mais que perder o modelo inteiro.
                    result.Failed++;
                    string why = ex.GetBaseException().Message;
                    result.Lines.Add("  FALHOU " + p + "  —  " + why);
                    Log.Error("Primitivo falhou — " + p, ex);
                }
            }

            Log.Info($"===== FIM (MODELAR PRIMITIVAS: {result.Created} criado(s), {result.Failed} falha(s)) =====");

            result.Ok = result.Created > 0;
            var sb = new StringBuilder();
            sb.AppendLine($"{result.Created} primitivo(s) criado(s), {result.Failed} falha(s).");
            foreach (string l in result.Lines) sb.AppendLine(l);
            if (result.Failed > 0)
                sb.AppendLine("\nO erro exato de cada falha está no log ('se_log').");
            result.Message = sb.ToString();
            return result;
        }

        /// <summary>
        /// CARRINHO DE BRINQUEDO — a carga de teste canônica do caminho de ESCRITA da ponte.
        /// Existe para provar, numa peça descartável, que um agente consegue fazer geometria
        /// aparecer no Solid Edge: seis primitivos (chassi, cabine e quatro rodas), dois tipos, posicionados nos três eixos.
        /// Se isto sai certo, a mecânica de posicionamento está certa.
        ///
        /// <paramref name="planeXY"/> é o plano cuja normal é Z (chassi e cabine sobem nele) e
        /// <paramref name="planeXZ"/> é o plano cuja normal é Y (o eixo das rodas). Os índices
        /// NÃO são adivinhados aqui de propósito: quem os descobre é a ferramenta 'se_planos',
        /// lendo a normal de cada RefPlane na peça real. Os defaults são só um ponto de partida.
        /// </summary>
        public static List<Primitive> ToyCar(int planeXY = 1, int planeXZ = 2)
        {
            // Proporções de carrinho de madeira: chassi comprido e baixo, cabine recuada,
            // quatro rodas para fora da carroceria.
            const double bodyL = 90, bodyW = 40, bodyH = 18;   // chassi
            const double cabL = 40, cabW = 34, cabH = 16;      // cabine
            const double wheelD = 26, wheelT = 8;              // roda
            const double axleZ = wheelD / 2.0;                 // altura do eixo = raio: roda toca o chão
            const double bodyZ = 12;                           // vão livre sob o chassi
            const double axleX = 28;                           // meia-distância entre eixos
            const double wheelY = bodyW / 2.0;                 // roda encostada na laseral, indo para fora

            var list = new List<Primitive>
            {
                new Primitive
                {
                    Name = "chassi", Kind = "caixa", PlaneIndex = planeXY, ExtrudeSide = 2,
                    SizeXMm = bodyL, SizeYMm = bodyW, HeightMm = bodyH, LiftMm = bodyZ
                },
                new Primitive
                {
                    // Recuada: a cabine fica atrás do meio, deixando capô na frente.
                    Name = "cabine", Kind = "caixa", PlaneIndex = planeXY, ExtrudeSide = 2,
                    SizeXMm = cabL, SizeYMm = cabW, HeightMm = cabH,
                    LiftMm = bodyZ + bodyH, CenterXMm = -12
                }
            };

            // As quatro rodas: cilindro no plano de normal Y. Cada lado tem o plano-base deslocado
            // para o SEU lado (liftSide) e a extrusão apontando para FORA do carro (extrudeSide) —
            // é o par de sinais que faz a roda crescer para fora em vez de para dentro do chassi.
            // No plano XZ o eixo U é X e o V é Z, por isso a altura do eixo entra em CenterYMm.
            int[] liftSides = { 2, 1 };      // direita: +Y; esquerda: −Y
            int[] growSides = { 2, 1 };      // e cada uma cresce continuando para fora
            string[] ladoNome = { "dir", "esq" };
            for (int s = 0; s < 2; s++)
            {
                for (int f = 0; f < 2; f++)
                {
                    double x = f == 0 ? axleX : -axleX;
                    list.Add(new Primitive
                    {
                        Name = $"roda {(f == 0 ? "diant" : "tras")}-{ladoNome[s]}",
                        Kind = "cilindro", PlaneIndex = planeXZ,
                        ExtrudeSide = growSides[s], LiftSide = liftSides[s],
                        DiameterMm = wheelD, HeightMm = wheelT,
                        LiftMm = wheelY, CenterXMm = x, CenterYMm = axleZ
                    });
                }
            }
            return list;
        }
    }
}
