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
        /// DEITADO: o corte da barra vira uma medida da pegada e a ALTURA vem da seção (por isso
        /// precisa do faceamento — ver <see cref="SawCutPlanner.FacingAllowanceMm"/>). EM PÉ: a
        /// seção comporta a pegada e o corte vira a ALTURA.
        /// </summary>
        public bool LaidDown { get; set; }

        /// <summary>Comprimento do eletrodo no eixo da barra (mm), sem sobremetal. Null = sem corte possível.</summary>
        public double? LengthMm { get; set; }

        /// <summary>Medida na serra (mm) = <see cref="LengthMm"/> arredondado para cima + sobremetal. Null = sem corte possível.</summary>
        public double? CutMm { get; set; }

        /// <summary>True se o perfil veio da identificação automática; false = escolhido à mão.</summary>
        public bool AutoIdentified { get; set; }

        /// <summary>Outras barras que também comportam a peça, da que gasta menos material à que gasta mais.</summary>
        public List<BlankSpec> Alternatives { get; } = new List<BlankSpec>();

        /// <summary>Aviso p/ a coluna de observações (corte maior que a barra, perfil que não comporta...). Null = nada a dizer.</summary>
        public string Note { get; set; }

        /// <summary>
        /// Quanto a barra SOBRA em cima da peça (o que vira cavaco), em texto. Null = a peça preenche
        /// a seção. É informação, NÃO é aviso: quem pinta a linha de laranja na janela é a
        /// <see cref="Note"/>. Serve p/ conferir o perfil contra as medidas (Carlos, 2026-09-17).
        /// </summary>
        public string Fit { get; set; }

        /// <summary>"em pé" / "deitado" — só faz sentido com perfil.</summary>
        public string Orientation => Blank == null ? null : (LaidDown ? "deitado" : "em pé");
    }

    /// <summary>
    /// Lista de corte (Carlos, 2026-09-14): a partir das MEDIDAS da peça do eletrodo (caixa
    /// envolvente do corpo), acha a barra de cobre do estoque que dá para usinar esse eletrodo e a
    /// medida de corte na serra com <see cref="DefaultAllowanceMm"/> de sobremetal.
    ///
    /// REGRA (Carlos, 2026-09-17): o material só não pode ser MENOR que o modelo — sobra vira
    /// cavaco, falta perde o eletrodo. Duas formas de usar a barra (<see cref="BlankChoice"/>):
    /// <list type="bullet">
    ///   <item>EM PÉ: a SEÇÃO comporta a pegada (X×Y da caixa) e o CORTE dá a altura (Z da caixa) —
    ///   o sobremetal de serra já cobre o faceamento das duas pontas.</item>
    ///   <item>DEITADO: uma dimensão da seção comporta um lado da pegada, o CORTE dá o outro lado e a
    ///   ALTURA vem da OUTRA dimensão da seção. Essa altura NÃO passa pela serra, então precisa ter
    ///   pelo menos <see cref="FacingAllowanceMm"/> a mais que o Z da peça p/ facear no centro de
    ///   usinagem (Carlos, 2026-09-17).</item>
    /// </list>
    /// Entre as barras que comportam a peça vence a que gasta MENOS MATERIAL (área da seção × corte)
    /// — mesmo espírito do "mais compacto primeiro" do pop-up do "Criar Base"; antes disso vem o
    /// material da peça (CuW80) e, no empate, em pé antes de deitado. O que sobra da barra vai em
    /// <see cref="SawCut.Fit"/> p/ conferir o perfil contra as medidas.
    ///
    /// O perfil escolhido no "Criar Base" NÃO fica gravado na peça — por isso tudo aqui é geométrico,
    /// em cima da caixa envolvente do corpo. Lógica pura, sem COM.
    /// </summary>
    public static class SawCutPlanner
    {
        /// <summary>Sobremetal de corte na serra (mm), somado à medida do eletrodo.</summary>
        public const double DefaultAllowanceMm = 5.0;

        /// <summary>
        /// Faceamento no centro de usinagem (mm): quando a ALTURA do eletrodo vem da SEÇÃO da barra
        /// (deitado), a seção precisa ter pelo menos isso a mais que o Z da peça — não se faceia
        /// material que não existe (Carlos, 2026-09-17). Em pé não precisa: a altura vem do corte, que
        /// já leva <see cref="DefaultAllowanceMm"/>.
        /// </summary>
        public const double FacingAllowanceMm = 1.0;

        /// <summary>Folga (mm) p/ dizer que uma medida da peça É a medida da barra (separa RED 12 de RED 12,7).</summary>
        public const double MatchToleranceMm = 0.3;

        /// <summary>Comprimento da barra inteira (mm) — ver [[electrode-anatomy]].</summary>
        public const double BarLengthMm = 500.0;

        /// <summary>
        /// Acha a barra pelas medidas da peça (mm, caixa envolvente no sistema da peça) e calcula o
        /// corte. <paramref name="partMaterial"/> (ex.: "CuW80") tem prioridade sobre o tamanho;
        /// null = cobre padrão.
        /// </summary>
        public static SawCut Identify(double sizeXmm, double sizeYmm, double sizeZmm, string partMaterial,
            IEnumerable<BlankSpec> catalog, double allowanceMm = DefaultAllowanceMm)
        {
            double longXY = Math.Max(sizeXmm, sizeYmm);
            double shortXY = Math.Min(sizeXmm, sizeYmm);

            var options = new List<SawCut>();
            foreach (BlankSpec b in catalog ?? Enumerable.Empty<BlankSpec>())
            {
                // Em pé tem preferência POR BARRA: se a seção já comporta a pegada, deitar a mesma
                // barra só gastaria mais (o corte sairia do Z para o lado da pegada).
                if (SectionHolds(b, longXY, shortXY))
                {
                    options.Add(BuildStanding(b, sizeZmm, allowanceMm, longXY, shortXY));
                    continue;
                }
                double inPlane, vertical;
                double? cut = LaidDownCut(b, longXY, shortXY, sizeZmm, out inPlane, out vertical);
                if (cut.HasValue)
                    options.Add(BuildLaidDown(b, cut.Value, allowanceMm, inPlane, vertical, sizeZmm));
            }

            List<SawCut> ranked = options
                .OrderBy(c => MaterialRank(c.Blank, partMaterial))
                .ThenBy(c => MaterialUsed(c))          // menos barra e menos cavaco
                .ThenBy(c => c.LaidDown ? 1 : 0)
                .ToList();

            SawCut chosen = ranked.FirstOrDefault();
            if (chosen == null)
            {
                return new SawCut
                {
                    Note = $"medidas {sizeXmm:0.0} × {sizeYmm:0.0} × {sizeZmm:0.0} mm não cabem em nenhuma barra " +
                           "do estoque — comprar material (ou escolher o perfil à mão)",
                };
            }

            chosen.AutoIdentified = true;
            chosen.Alternatives.AddRange(ranked.Skip(1).Select(c => c.Blank));

            // Empate de verdade (mesmo material, mesmo gasto) é o único caso em que o automático está
            // mesmo na dúvida — as outras alternativas são só barras maiores.
            var tied = ranked.Skip(1)
                .Where(c => MaterialRank(c.Blank, partMaterial) == MaterialRank(chosen.Blank, partMaterial))
                .Where(c => Math.Abs(MaterialUsed(c) - MaterialUsed(chosen)) < 1e-6)
                .Select(c => c.Blank.Name + MaterialSuffix(c.Blank))
                .ToList();
            if (tied.Count > 0)
                chosen.Note = Join(chosen.Note, "gasta o mesmo que " + string.Join(", ", tied) + " — confira");
            return chosen;
        }

        /// <summary>
        /// Corte com um perfil ESCOLHIDO À MÃO: em pé se a seção comporta a pegada, senão deitado (se
        /// um lado da pegada couber numa dimensão da seção), senão sem corte possível (com aviso).
        /// Barra que não dá a altura + faceamento vira AVISO, não silêncio.
        /// </summary>
        public static SawCut ForBlank(BlankSpec blank, double sizeXmm, double sizeYmm, double sizeZmm,
            double allowanceMm = DefaultAllowanceMm)
        {
            if (blank == null) throw new ArgumentNullException(nameof(blank));
            double longXY = Math.Max(sizeXmm, sizeYmm);
            double shortXY = Math.Min(sizeXmm, sizeYmm);
            double t = MatchToleranceMm;

            if (SectionHolds(blank, longXY, shortXY))
                return BuildStanding(blank, sizeZmm, allowanceMm, longXY, shortXY);

            if (blank.Shape != BlankShape.Round)
            {
                double hi, lo;
                SectionSides(blank, out hi, out lo);
                if (shortXY <= hi + t)
                {
                    // Melhor caso: o lado curto cabe na dimensão MENOR e sobra a maior p/ a altura.
                    bool inSmall = shortXY <= lo + t;
                    double inPlane = inSmall ? lo : hi, vertical = inSmall ? hi : lo;
                    double cut = inPlane + t >= longXY ? shortXY : longXY;
                    SawCut laid = BuildLaidDown(blank, cut, allowanceMm, inPlane, vertical, sizeZmm);
                    if (vertical < sizeZmm + FacingAllowanceMm)
                        laid.Note = Join(laid.Note, $"deitado, a seção dá só {vertical:0.#} mm de altura e o eletrodo " +
                                                    $"tem {sizeZmm:0.0} mm + {FacingAllowanceMm:0.#} mm p/ facear");
                    return laid;
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

        /// <summary>Material gasto (mm³): seção × corte. É o critério de "qual barra usar".</summary>
        private static double MaterialUsed(SawCut cut) =>
            cut.Blank == null || !cut.CutMm.HasValue ? double.MaxValue : cut.Blank.SectionArea * cut.CutMm.Value;

        /// <summary>Em pé: a seção comporta a pegada e o corte dá a altura.</summary>
        private static SawCut BuildStanding(BlankSpec blank, double sizeZmm, double allowanceMm,
            double longXY, double shortXY)
        {
            SawCut cut = NewCut(blank, false, sizeZmm, allowanceMm);
            double hi, lo;
            SectionSides(blank, out hi, out lo);
            if (blank.Shape == BlankShape.Round)
            {
                if (hi - longXY > MatchToleranceMm)
                    cut.Fit = $"barra Ø{hi:0.#} p/ pegada de {longXY:0.0} × {shortXY:0.0} mm";
            }
            else if (hi - longXY > MatchToleranceMm || lo - shortXY > MatchToleranceMm)
            {
                cut.Fit = $"sobra {Math.Max(hi - longXY, 0):0.0} × {Math.Max(lo - shortXY, 0):0.0} mm na seção " +
                          $"({hi:0.#} × {lo:0.#} p/ pegada de {longXY:0.0} × {shortXY:0.0})";
            }
            return cut;
        }

        /// <summary>Deitado: a altura vem da seção — por isso o faceamento aparece no <see cref="SawCut.Fit"/>.</summary>
        private static SawCut BuildLaidDown(BlankSpec blank, double lengthMm, double allowanceMm,
            double inPlane, double vertical, double sizeZmm)
        {
            SawCut cut = NewCut(blank, true, lengthMm, allowanceMm);
            cut.Fit = $"altura vem da seção: {vertical:0.#} mm p/ peça de {sizeZmm:0.0} mm " +
                      $"({vertical - sizeZmm:0.0} mm p/ facear), pegada na dimensão de {inPlane:0.#} mm";
            return cut;
        }

        private static SawCut NewCut(BlankSpec blank, bool laidDown, double lengthMm, double allowanceMm)
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

        /// <summary>Lados da seção, maior e menor (quadrado e redondo: a mesma medida nos dois).</summary>
        private static void SectionSides(BlankSpec b, out double hi, out double lo)
        {
            if (b.Shape == BlankShape.Rectangular)
            {
                hi = Math.Max(b.DimA, b.DimB ?? b.DimA);
                lo = Math.Min(b.DimA, b.DimB ?? b.DimA);
            }
            else hi = lo = b.DimA;
        }

        /// <summary>
        /// A seção COMPORTA a pegada (em pé)? Material do tamanho da peça pode (a folga é ruído de
        /// modelo), menor não pode. Redondo: ou a caixa É o cilindro (Ø × Ø, o eletrodo é a própria
        /// barra), ou a pegada cabe no círculo pela DIAGONAL.
        /// </summary>
        private static bool SectionHolds(BlankSpec b, double longXY, double shortXY)
        {
            double t = MatchToleranceMm;
            if (b.Shape == BlankShape.Round)
            {
                bool isTheCylinder = Math.Abs(longXY - b.DimA) <= t && Math.Abs(shortXY - b.DimA) <= t;
                return isTheCylinder || Math.Sqrt(longXY * longXY + shortXY * shortXY) <= b.DimA + t;
            }
            double hi, lo;
            SectionSides(b, out hi, out lo);
            return longXY <= hi + t && shortXY <= lo + t;
        }

        /// <summary>
        /// Deitado: uma dimensão da seção fica no plano (comporta um lado da pegada), o outro lado da
        /// pegada é o CORTE e a outra dimensão da seção é a ALTURA — que precisa do Z da peça MAIS o
        /// faceamento, porque não passa pela serra. Devolve o MENOR corte que serve; null = não serve.
        /// Redondo não deita (mesma regra de <see cref="StandardBlankLibrary.BlankChoices"/>).
        /// </summary>
        private static double? LaidDownCut(BlankSpec b, double longXY, double shortXY, double sizeZmm,
            out double inPlaneUsed, out double verticalUsed)
        {
            inPlaneUsed = verticalUsed = 0;
            if (b.Shape == BlankShape.Round) return null;
            double t = MatchToleranceMm;
            double a = b.DimA, c = b.DimB ?? b.DimA;
            double? best = null;
            foreach (var pair in new[] { new[] { a, c }, new[] { c, a } })
            {
                double inPlane = pair[0], vertical = pair[1];
                if (vertical < sizeZmm + FacingAllowanceMm) continue;   // sem material p/ facear
                double cut;
                if (inPlane + t >= longXY) cut = shortXY;               // a pegada inteira cabe no plano
                else if (inPlane + t >= shortXY) cut = longXY;          // só o lado curto cabe: o longo é o corte
                else continue;
                if (best.HasValue && cut >= best.Value) continue;
                best = cut;
                inPlaneUsed = inPlane;
                verticalUsed = vertical;
            }
            return best;
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
