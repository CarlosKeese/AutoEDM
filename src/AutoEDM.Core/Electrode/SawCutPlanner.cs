using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Como um eletrodo sai da barra de estoque na SERRA: o perfil (seção fixa) e a medida de
    /// corte já com sobremetal. Produzido por <see cref="SawCutPlanner"/>.
    /// </summary>
    public sealed class SawCut
    {
        /// <summary>Perfil do estoque. Null = não identificado (o usuário escolhe na janela).</summary>
        public BlankSpec Blank { get; set; }

        /// <summary>
        /// DEITADO: o corte da barra vira o lado LONGO da pegada (a outra medida da pegada é uma
        /// dimensão da seção — ver <see cref="BlankChoice"/>). EM PÉ: o corte vira a ALTURA.
        /// </summary>
        public bool LaidDown { get; set; }

        /// <summary>Comprimento do eletrodo no eixo da barra (mm), sem sobremetal. Null = sem corte possível.</summary>
        public double? LengthMm { get; set; }

        /// <summary>Medida na serra (mm) = <see cref="LengthMm"/> arredondado para cima + sobremetal. Null = sem corte possível.</summary>
        public double? CutMm { get; set; }

        /// <summary>True se o perfil veio da identificação automática; false = escolhido à mão.</summary>
        public bool AutoIdentified { get; set; }

        /// <summary>Outros perfis que TAMBÉM batem com as medidas (identificação ambígua).</summary>
        public List<BlankSpec> Alternatives { get; } = new List<BlankSpec>();

        /// <summary>Aviso p/ a coluna de observações (ambiguidade, perfil que não comporta...). Null = nada a dizer.</summary>
        public string Note { get; set; }

        /// <summary>"em pé" / "deitado" — só faz sentido com perfil.</summary>
        public string Orientation => Blank == null ? null : (LaidDown ? "deitado" : "em pé");
    }

    /// <summary>
    /// Lista de corte (Carlos, 2026-09-14): a partir das MEDIDAS da peça do eletrodo (caixa
    /// envolvente do corpo), acha o perfil de cobre do estoque de onde ele saiu e a medida de
    /// corte na serra com <see cref="DefaultAllowanceMm"/> de sobremetal.
    ///
    /// O perfil escolhido no "Criar Base" NÃO fica gravado na peça — por isso a identificação é
    /// geométrica, casando a caixa com as duas formas de usar a barra (<see cref="BlankChoice"/>):
    /// <list type="bullet">
    ///   <item>EM PÉ: o bloco tem a SEÇÃO da barra, então a pegada (X×Y da caixa) = seção, e o
    ///   corte = ALTURA total do eletrodo (Z da caixa: queima + faixa + bloco).</item>
    ///   <item>DEITADO: um lado da pegada = uma dimensão da seção, o outro (maior) é o CORTE, e a
    ///   outra dimensão da seção vira a altura (cabe no Z da caixa).</item>
    /// </list>
    /// Em pé tem prioridade (casa as duas medidas, não só uma). Empate: material da peça, depois a
    /// seção mais compacta — mesmo critério do pop-up do "Criar Base". Lógica pura, sem COM.
    /// </summary>
    public static class SawCutPlanner
    {
        /// <summary>Sobremetal de corte na serra (mm), somado à medida do eletrodo.</summary>
        public const double DefaultAllowanceMm = 5.0;

        /// <summary>Folga (mm) p/ dizer que uma medida da peça É a medida da barra (separa RED 12 de RED 12,7).</summary>
        public const double MatchToleranceMm = 0.3;

        /// <summary>Comprimento da barra inteira (mm) — ver [[electrode-anatomy]].</summary>
        public const double BarLengthMm = 500.0;

        /// <summary>
        /// Identifica o perfil pelas medidas da peça (mm, caixa envolvente no sistema da peça) e
        /// calcula o corte. <paramref name="partMaterial"/> (ex.: "CuW80") só desempata perfis de
        /// mesma seção; null = cobre padrão.
        /// </summary>
        public static SawCut Identify(double sizeXmm, double sizeYmm, double sizeZmm, string partMaterial,
            IEnumerable<BlankSpec> catalog, double allowanceMm = DefaultAllowanceMm)
        {
            double longXY = Math.Max(sizeXmm, sizeYmm);
            double shortXY = Math.Min(sizeXmm, sizeYmm);
            var all = (catalog ?? Enumerable.Empty<BlankSpec>()).ToList();

            List<BlankSpec> standing = all
                .Where(b => SectionEquals(b, longXY, shortXY))
                .OrderBy(b => MaterialRank(b, partMaterial)).ThenBy(b => b.SectionArea)
                .ToList();
            List<BlankSpec> laid = all
                .Where(b => !standing.Contains(b) && LaidDownMatches(b, longXY, shortXY, sizeZmm))
                .OrderBy(b => MaterialRank(b, partMaterial)).ThenBy(b => b.SectionArea)
                .ToList();

            BlankSpec chosen = standing.FirstOrDefault() ?? laid.FirstOrDefault();
            if (chosen == null)
            {
                return new SawCut
                {
                    Note = $"medidas {longXY:0.0} × {shortXY:0.0} mm não batem com nenhum perfil do estoque — escolha o perfil",
                };
            }

            bool laidDown = !standing.Contains(chosen);
            var cut = Build(chosen, laidDown, laidDown ? longXY : sizeZmm, allowanceMm);
            cut.AutoIdentified = true;
            cut.Alternatives.AddRange(standing.Concat(laid).Where(b => b != chosen));
            if (cut.Alternatives.Count > 0)
                cut.Note = Join(cut.Note, "também bate com " + string.Join(", ", cut.Alternatives.Select(b => b.Name + MaterialSuffix(b))) + " — confira");
            return cut;
        }

        /// <summary>
        /// Corte com um perfil ESCOLHIDO À MÃO: em pé se a seção comporta a pegada, senão deitado
        /// (se o lado curto couber numa dimensão da seção), senão sem corte possível (com aviso).
        /// </summary>
        public static SawCut ForBlank(BlankSpec blank, double sizeXmm, double sizeYmm, double sizeZmm,
            double allowanceMm = DefaultAllowanceMm)
        {
            if (blank == null) throw new ArgumentNullException(nameof(blank));
            double longXY = Math.Max(sizeXmm, sizeYmm);
            double shortXY = Math.Min(sizeXmm, sizeYmm);
            double t = MatchToleranceMm;

            if (SectionContains(blank, longXY, shortXY))
                return Build(blank, false, sizeZmm, allowanceMm);

            if (blank.Shape != BlankShape.Round)
            {
                double a = blank.DimA, b = blank.DimB ?? blank.DimA;
                // Deitado: o lado curto da pegada fica numa dimensão da seção e o corte dá o lado longo.
                if (shortXY <= Math.Max(a, b) + t)
                {
                    var cut = Build(blank, true, longXY, allowanceMm);
                    double height = shortXY <= Math.Min(a, b) + t ? Math.Max(a, b) : Math.Min(a, b);
                    if (height < sizeZmm - t)
                        cut.Note = Join(cut.Note, $"deitado, a seção dá só {height:0.#} mm de altura e o eletrodo tem {sizeZmm:0.0} mm");
                    return cut;
                }
            }

            return new SawCut
            {
                Blank = blank,
                Note = $"o eletrodo ({longXY:0.0} × {shortXY:0.0} mm) não cabe na seção do {blank.Name}",
            };
        }

        /// <summary>Medida na serra: comprimento arredondado p/ cima (mm inteiro) + sobremetal.</summary>
        public static double CutLength(double lengthMm, double allowanceMm = DefaultAllowanceMm) =>
            // −0,01: a caixa do SE chega como 42,000000001 e não pode virar 43.
            Math.Ceiling(lengthMm - 0.01) + allowanceMm;

        private static SawCut Build(BlankSpec blank, bool laidDown, double lengthMm, double allowanceMm)
        {
            var cut = new SawCut
            {
                Blank = blank,
                LaidDown = laidDown,
                LengthMm = lengthMm,
                CutMm = CutLength(lengthMm, allowanceMm),
            };
            if (cut.CutMm > BarLengthMm)
                cut.Note = $"corte de {cut.CutMm:0} mm passa da barra de {BarLengthMm:0} mm";
            return cut;
        }

        /// <summary>A pegada TEM a medida da seção (em pé)?</summary>
        private static bool SectionEquals(BlankSpec b, double longXY, double shortXY)
        {
            double t = MatchToleranceMm;
            if (b.Shape == BlankShape.Rectangular)
            {
                double hi = Math.Max(b.DimA, b.DimB ?? b.DimA), lo = Math.Min(b.DimA, b.DimB ?? b.DimA);
                return Math.Abs(longXY - hi) <= t && Math.Abs(shortXY - lo) <= t;
            }
            // Quadrado e redondo: a caixa de um cilindro Ø D também é D × D.
            return Math.Abs(longXY - b.DimA) <= t && Math.Abs(shortXY - b.DimA) <= t;
        }

        /// <summary>A pegada CABE na seção (em pé), sem precisar ser igual?</summary>
        private static bool SectionContains(BlankSpec b, double longXY, double shortXY)
        {
            double t = MatchToleranceMm;
            if (b.Shape == BlankShape.Rectangular)
            {
                double hi = Math.Max(b.DimA, b.DimB ?? b.DimA), lo = Math.Min(b.DimA, b.DimB ?? b.DimA);
                return longXY <= hi + t && shortXY <= lo + t;
            }
            return longXY <= b.DimA + t;
        }

        /// <summary>
        /// Deitado: o lado curto = uma dimensão da seção, o longo passa dela (é o corte) e a outra
        /// dimensão (a altura) cabe no Z da peça. Redondo não deita (mesma regra de <see cref="StandardBlankLibrary.BlankChoices"/>).
        /// </summary>
        private static bool LaidDownMatches(BlankSpec b, double longXY, double shortXY, double sizeZmm)
        {
            if (b.Shape == BlankShape.Round) return false;
            double t = MatchToleranceMm;
            double a = b.DimA, c = b.DimB ?? b.DimA;
            foreach (var pair in new[] { new[] { a, c }, new[] { c, a } })
            {
                double inPlane = pair[0], vertical = pair[1];
                if (Math.Abs(shortXY - inPlane) <= t && longXY > inPlane + t && vertical <= sizeZmm + t)
                    return true;
            }
            return false;
        }

        /// <summary>0 = material especial pedido pela peça; 1 = cobre padrão; 2 = especial não pedido.</summary>
        private static int MaterialRank(BlankSpec b, string partMaterial)
        {
            if (b.Material == null) return 1;
            bool requested = !string.IsNullOrWhiteSpace(partMaterial) &&
                             partMaterial.IndexOf(b.Material, StringComparison.OrdinalIgnoreCase) >= 0;
            return requested ? 0 : 2;
        }

        private static string MaterialSuffix(BlankSpec b) => b.Material != null ? " " + b.Material : "";

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "; " + b;
    }
}
