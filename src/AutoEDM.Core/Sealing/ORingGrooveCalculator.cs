using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Sealing
{
    /// <summary>Um candidato avaliado: o anel e o alojamento que ele geraria.</summary>
    public sealed class ORingCandidate
    {
        public ORingSize Ring { get; set; }
        public ORingGrooveSpec Spec { get; set; }

        /// <summary>Quanto este candidato se afasta do ideal — menor é melhor. Só serve para
        /// ordenar; o que vale para decidir são os apontamentos do <see cref="Spec"/>.</summary>
        public double Score { get; set; }
    }

    /// <summary>
    /// Dimensiona o alojamento e escolhe o anel. Puro cálculo — nenhuma linha de COM aqui, o
    /// que deixa esta parte inteiramente coberta por teste automatizado (o Solid Edge não
    /// participa de decidir a cota do canal).
    ///
    /// Todas as cotas em MILÍMETROS; frações (esmagamento, estiramento, preenchimento) são
    /// 0..1, não porcentagem.
    /// </summary>
    public static class ORingGrooveCalculator
    {
        /// <summary>
        /// Dimensiona o canal para um anel escolhido. Não decide nada sozinho: devolve o canal
        /// COM os apontamentos, e quem decide se corta é o operador (foi o que o Carlos pediu —
        /// avisar em vez de recusar).
        /// </summary>
        public static ORingGrooveSpec Compute(ORingSize ring, GrooveKind kind, double sealingDiameterMm,
            SealMotion motion, Elastomer elastomer, FacePressure pressure = FacePressure.Internal)
        {
            if (ring == null) throw new ArgumentNullException(nameof(ring));

            double d2 = ring.CrossSection;
            double sqMin, sqTarget, sqMax;
            ORingGrooveRules.Squeeze(motion, out sqMin, out sqTarget, out sqMax);

            // TABELA PRIMEIRO. A cota do alojamento é publicada por seção e por movimento
            // (catálogo Parker 001-5 BR), e a tabela sabe coisas que a fórmula não sabia: o
            // esmagamento cai conforme a seção engrossa, e a largura do canal NÃO depende do
            // tipo de vedação. Só se a seção não estiver tabelada — ou se a vedação for
            // ROTATIVA, que o catálogo não cobre — é que se calcula.
            var housing = ORingHousingTable.Find(d2, motion);
            double depth, width;
            if (housing != null)
            {
                depth = housing.Depth;
                width = housing.Width;
                sqMin = housing.SqueezeMinPct / 100.0;
                sqMax = housing.SqueezeMaxPct / 100.0;
            }
            else
            {
                // Profundidade sai do ESMAGAMENTO; largura sai do PREENCHIMENTO. Nessa ordem: é
                // o esmagamento que veda, e a largura é a folga que sobra para a borracha escoar.
                // As duas saem dos mesmos pontos únicos que o resto do módulo consulta.
                depth = d2 * (1.0 - sqTarget);
                width = GrooveWidth(d2, motion, elastomer);
            }

            var spec = new ORingGrooveSpec
            {
                Ring = ring,
                Kind = kind,
                Motion = motion,
                Elastomer = elastomer,
                Pressure = pressure,
                SealingDiameter = sealingDiameterMm,
                Depth = depth,
                Width = width,
                BottomRadius = housing != null ? housing.Radius : ORingGrooveRules.BottomRadius(d2),
                EdgeBreak = ORingGrooveRules.EdgeBreak(d2),
                Fill = ring.SectionArea / (width * depth),
                Source = housing != null
                    ? $"tabela de alojamento {(motion == SealMotion.Static ? "ESTÁTICO" : "DINÂMICO")} " +
                      "(catálogo Parker 001-5 BR, p. 5)"
                    : (motion == SealMotion.Rotary
                        ? "cálculo — vedação ROTATIVA não é tabelada no catálogo"
                        : $"cálculo — seção {d2:0.00} mm fora das cinco tabeladas"),
                DepthMin = housing != null ? housing.DepthMin : 0.0,
                DepthMax = housing != null ? housing.DepthMax : 0.0,
                WidthMin = housing != null ? housing.WidthMin : 0.0,
                WidthMax = housing != null ? housing.WidthMax : 0.0,
                ClearanceMin = housing != null ? housing.ClearanceMin : 0.0,
                ClearanceMax = housing != null ? housing.ClearanceMax : 0.0,
                Eccentricity = housing != null ? housing.Eccentricity : 0.0
            };

            switch (kind)
            {
                case GrooveKind.RadialExternal: // canal no EIXO: o anel é esticado por cima
                    spec.GrooveBottomDiameter = sealingDiameterMm - 2.0 * depth;
                    spec.Stretch = ring.InnerDiameter > 0
                        ? (spec.GrooveBottomDiameter - ring.InnerDiameter) / ring.InnerDiameter : 0.0;
                    break;

                case GrooveKind.RadialInternal: // canal no FURO: o anel entra comprimido
                    spec.GrooveBottomDiameter = sealingDiameterMm + 2.0 * depth;
                    spec.Stretch = ring.OuterDiameter > 0
                        ? (ring.OuterDiameter - spec.GrooveBottomDiameter) / ring.OuterDiameter : 0.0;
                    break;

                default: // AxialFace — o anel deita no canal ENCOSTADO na parede que segura a pressão
                    double fit = ORingGrooveRules.FaceSeatFit;
                    if (pressure == FacePressure.External)
                    {
                        // Empurrado para dentro: o d1 abraça a parede INTERNA, um pouco esticado.
                        spec.GrooveInnerDiameter = ring.InnerDiameter * (1.0 + fit);
                        spec.GrooveOuterDiameter = spec.GrooveInnerDiameter + 2.0 * width;
                        spec.Stretch = ring.InnerDiameter > 0
                            ? (spec.GrooveInnerDiameter - ring.InnerDiameter) / ring.InnerDiameter : 0.0;
                    }
                    else
                    {
                        // Empurrado para fora: o Ø externo encosta na parede EXTERNA, um pouco comprimido.
                        spec.GrooveOuterDiameter = ring.OuterDiameter * (1.0 - fit);
                        spec.GrooveInnerDiameter = spec.GrooveOuterDiameter - 2.0 * width;
                        spec.Stretch = ring.OuterDiameter > 0
                            ? (ring.OuterDiameter - spec.GrooveOuterDiameter) / ring.OuterDiameter : 0.0;
                    }
                    break;
            }

            // Esticar AFINA o cordão, e cordão mais fino é menos esmagamento — o número que
            // interessa é o depois, não o de catálogo.
            double effectiveD2 = !spec.StretchIsOuterCompression
                ? ORingGrooveRules.EffectiveCrossSection(d2, spec.Stretch)
                : d2;
            spec.Squeeze = effectiveD2 > 0 ? (effectiveD2 - depth) / effectiveD2 : 0.0;

            Review(spec, sqMin, sqMax, motion, elastomer, sealingDiameterMm);
            return spec;
        }

        private static void Review(ORingGrooveSpec spec, double sqMin, double sqMax,
            SealMotion motion, Elastomer elastomer, double sealingDiameterMm)
        {
            void Add(GrooveIssueLevel level, string msg) =>
                spec.Issues.Add(new GrooveIssue { Level = level, Message = msg });

            if (spec.Kind == GrooveKind.RadialExternal)
            {
                double maxStretch = ORingGrooveRules.MaxStretch(motion, elastomer);
                if (motion == SealMotion.Rotary && spec.Stretch > 0.0)
                    Add(GrooveIssueLevel.Error,
                        $"vedação ROTATIVA com o anel esticado {Pct(spec.Stretch)}: borracha esticada contrai ao " +
                        "esquentar (efeito Gow-Joule) e o anel aperta o eixo até queimar. Use um anel de d1 maior.");
                else if (spec.Stretch > maxStretch)
                    Add(GrooveIssueLevel.Error,
                        $"estiramento {Pct(spec.Stretch)} acima do limite de {Pct(maxStretch)} para " +
                        $"{(elastomer == Elastomer.Fkm ? "FKM" : "NBR")} — o anel perde seção e vida útil.");
                else if (spec.Stretch < -ORingGrooveRules.MaxSlack)
                    Add(GrooveIssueLevel.Warning,
                        $"anel sobrando {Pct(-spec.Stretch)} no fundo do canal: pode torcer na montagem.");

                if (spec.GrooveBottomDiameter <= 0)
                    Add(GrooveIssueLevel.Error, "o canal come o eixo inteiro — seção do anel grande demais para este diâmetro.");
                else if (spec.GrooveBottomDiameter < 0.5 * sealingDiameterMm)
                    Add(GrooveIssueLevel.Warning, "o canal tira mais da metade do diâmetro do eixo: confira a resistência.");
            }
            else if (spec.Kind == GrooveKind.RadialInternal)
            {
                double maxComp = ORingGrooveRules.MaxOuterCompression(elastomer);
                if (spec.Stretch > maxComp * ORingGrooveRules.OuterCompressionTolerance)
                    Add(GrooveIssueLevel.Error,
                        $"o anel entra comprimido {Pct(spec.Stretch)} no diâmetro externo, muito acima do limite de " +
                        $"{Pct(maxComp)}: ele enruga dentro do canal em vez de encostar liso.");
                else if (spec.Stretch > maxComp)
                    Add(GrooveIssueLevel.Warning,
                        $"o anel entra comprimido {Pct(spec.Stretch)} no diâmetro externo, pouco acima do limite de " +
                        $"{Pct(maxComp)} — preferível a um anel frouxo, mas confira na montagem.");
                else if (spec.Stretch < -0.005)
                    Add(GrooveIssueLevel.Warning,
                        $"o anel fica folgado {Pct(-spec.Stretch)} no canal do furo: pode não assentar no fundo.");
            }
            else
            {
                double center = FaceGrooveCenter(spec);
                double deviation = center - sealingDiameterMm;
                if (Math.Abs(deviation) > 0.01)
                    Add(GrooveIssueLevel.Note,
                        $"o canal ficou com o centro no Ø {center:0.00}, " +
                        $"{Math.Abs(deviation):0.00} mm {(deviation > 0 ? "fora" : "dentro")} do Ø de referência " +
                        $"({sealingDiameterMm:0.00}): num canal de face é o anel que manda no diâmetro — o canal " +
                        $"encosta no Ø {(spec.Pressure == FacePressure.Internal ? "EXTERNO" : "INTERNO")} dele.");
                if (spec.Pressure == FacePressure.External && spec.Stretch > ORingGrooveRules.MaxStretch(motion, elastomer))
                    Add(GrooveIssueLevel.Error,
                        $"o anel fica esticado {Pct(spec.Stretch)} na parede interna, acima do limite de " +
                        $"{Pct(ORingGrooveRules.MaxStretch(motion, elastomer))} para esta vedação.");
                if (spec.Pressure == FacePressure.Internal && spec.Stretch > ORingGrooveRules.MaxOuterCompression(elastomer))
                    Add(GrooveIssueLevel.Error,
                        $"o anel entra comprimido {Pct(spec.Stretch)} na parede externa, acima do limite de " +
                        $"{Pct(ORingGrooveRules.MaxOuterCompression(elastomer))}: ele enruga dentro do canal.");
                if (spec.GrooveInnerDiameter <= 0)
                    Add(GrooveIssueLevel.Error, "o canal fecha no centro — anel pequeno demais para um canal de face.");
            }

            if (spec.Fill > ORingGrooveRules.MaxFill)
                Add(GrooveIssueLevel.Error,
                    $"preenchimento {Pct(spec.Fill)} acima de {Pct(ORingGrooveRules.MaxFill)}: sem espaço para a " +
                    "borracha dilatar, o próprio anel arrebenta o alojamento.");

            if (spec.Squeeze < sqMin)
                Add(GrooveIssueLevel.Warning,
                    $"esmagamento efetivo {Pct(spec.Squeeze)} abaixo do mínimo de {Pct(sqMin)} para esta vedação: risco de vazar.");
            else if (spec.Squeeze > sqMax)
                Add(GrooveIssueLevel.Warning,
                    $"esmagamento efetivo {Pct(spec.Squeeze)} acima do máximo de {Pct(sqMax)}: anel deforma permanente.");

            if (!spec.Ring.Verified)
                Add(GrooveIssueLevel.Note,
                    "a medida deste anel ainda não foi conferida contra o catálogo do fornecedor " +
                    $"(edite {ORingCatalog.FileName} em %LOCALAPPDATA%\\AutoEDM).");
        }

        /// <summary>
        /// Avalia TODOS os anéis do catálogo (opcionalmente só de uma seção) e devolve-os
        /// ordenados do melhor para o pior. Devolve a lista inteira de propósito: a janela
        /// mostra as alternativas, e quem escolhe é o operador.
        /// </summary>
        public static IReadOnlyList<ORingCandidate> Rank(ORingCatalog catalog, GrooveKind kind,
            double sealingDiameterMm, SealMotion motion, Elastomer elastomer, double? crossSectionMm = null,
            FacePressure pressure = FacePressure.Internal, double sectionTolerance = 0.0)
        {
            if (catalog == null || catalog.Count == 0) return new List<ORingCandidate>();

            // sectionTolerance > 0: aceita seções VIZINHAS (fração da pedida) — é o que deixa os
            // anéis métricos (1,5 / 2,0 / 2,5…) entrarem quando a seção sugerida é a AS568 (1,78…).
            IEnumerable<ORingSize> pool = !crossSectionMm.HasValue ? catalog.Sizes
                : sectionTolerance > 0
                    ? catalog.Sizes.Where(s => Math.Abs(s.CrossSection - crossSectionMm.Value) <= sectionTolerance * crossSectionMm.Value + 1e-9)
                    : catalog.WithCrossSection(crossSectionMm.Value);
            // Anel de catálogo POR COMPOSTO (DL Seals) só vale no elastômero dele.
            pool = pool.Where(s => !s.Material.HasValue || s.Material.Value == elastomer);

            double targetStretch = ORingGrooveRules.TargetStretch(motion);
            var ranked = new List<ORingCandidate>();
            foreach (var ring in pool)
            {
                ORingGrooveSpec spec;
                try { spec = Compute(ring, kind, sealingDiameterMm, motion, elastomer, pressure); }
                catch { continue; }

                double score;
                if (kind == GrooveKind.AxialFace)
                    score = Math.Abs(FaceGrooveCenter(spec) - sealingDiameterMm);
                else
                {
                    // Desvio do alvo; do lado FROUXO pesa mais (LoosePenalty) — no eixo e no furo,
                    // Stretch MENOR que o alvo é o anel mais solto.
                    double target = kind == GrooveKind.RadialInternal ? ORingGrooveRules.TargetOuterCompression : targetStretch;
                    double dev = spec.Stretch - target;
                    score = Math.Abs(dev) * (dev < 0 ? ORingGrooveRules.LoosePenalty : 1.0) * 1000.0;
                }

                // Um candidato reprovado vai para o fim da fila, mas continua na lista: o
                // operador pode ter motivo para aceitar, e ver o "quase" ajuda a decidir.
                if (!spec.IsWithinStandard) score += 1e6;

                ranked.Add(new ORingCandidate { Ring = ring, Spec = spec, Score = score });
            }
            return ranked.OrderBy(c => c.Score).ToList();
        }

        /// <summary>
        /// O d1 do anel IDEAL para este diâmetro — o que responder quando o catálogo não tem
        /// nada que sirva ("compre um anel de d1 ≈ X"). Nenhuma tabela envolvida.
        /// </summary>
        public static double IdealInnerDiameter(GrooveKind kind, double sealingDiameterMm,
            double crossSectionMm, SealMotion motion, Elastomer elastomer = Elastomer.Nbr,
            FacePressure pressure = FacePressure.Internal)
        {
            double depth = GrooveDepth(crossSectionMm, motion);

            switch (kind)
            {
                case GrooveKind.RadialExternal:
                    return (sealingDiameterMm - 2.0 * depth) / (1.0 + ORingGrooveRules.TargetStretch(motion));
                case GrooveKind.RadialInternal:
                    return (sealingDiameterMm + 2.0 * depth) / (1.0 - ORingGrooveRules.TargetOuterCompression)
                           - 2.0 * crossSectionMm;
                default:
                {
                    // Canal de face: o Ø pedido é o CENTRO do canal; o anel encosta numa das paredes.
                    double width = GrooveWidth(crossSectionMm, motion, elastomer);
                    double fit = ORingGrooveRules.FaceSeatFit;
                    return pressure == FacePressure.External
                        ? (sealingDiameterMm - width) / (1.0 + fit)
                        : (sealingDiameterMm + width) / (1.0 - fit) - 2.0 * crossSectionMm;
                }
            }
        }

        /// <summary>Ø do CENTRO do canal de face — é por ele que o canal é posicionado em
        /// relação à aresta de referência (<see cref="FaceSealingDiameter"/>).</summary>
        public static double FaceGrooveCenter(ORingGrooveSpec spec) =>
            spec == null ? 0.0 : (spec.GrooveInnerDiameter + spec.GrooveOuterDiameter) / 2.0;

        /// <summary>
        /// Profundidade do canal para uma seção e um movimento — tabela quando existe, cálculo
        /// quando não. Ponto único: se <see cref="Compute"/> e <see cref="IdealInnerDiameter"/>
        /// usassem profundidades diferentes, o "anel ideal" não bateria com o canal que sai.
        /// </summary>
        public static double GrooveDepth(double crossSectionMm, SealMotion motion)
        {
            var housing = ORingHousingTable.Find(crossSectionMm, motion);
            if (housing != null) return housing.Depth;

            double sqMin, sqTarget, sqMax;
            ORingGrooveRules.Squeeze(motion, out sqMin, out sqTarget, out sqMax);
            return crossSectionMm * (1.0 - sqTarget);
        }

        /// <summary>
        /// Largura do canal para uma seção e um movimento — tabela quando existe, cálculo
        /// quando não. Mesmo papel de ponto único que <see cref="GrooveDepth"/>: a largura
        /// depende só do CORDÃO, nunca do diâmetro em que ele corre, e é isso que permite
        /// posicionar o canal antes de escolher o anel (ver <see cref="FaceSealingDiameter"/>).
        /// </summary>
        public static double GrooveWidth(double crossSectionMm, SealMotion motion, Elastomer elastomer)
        {
            var housing = ORingHousingTable.Find(crossSectionMm, motion);
            if (housing != null) return housing.Width;

            double depth = GrooveDepth(crossSectionMm, motion);
            double area = Math.PI / 4.0 * crossSectionMm * crossSectionMm;   // = ORingSize.SectionArea
            double fillTarget = ORingGrooveRules.TargetFill(motion, elastomer);
            double width = depth > 0 ? area / (fillTarget * depth) : 0.0;
            return Clamp(width, ORingGrooveRules.MinWidthFactor * crossSectionMm,
                                ORingGrooveRules.MaxWidthFactor * crossSectionMm);
        }

        /// <summary>
        /// Num canal de FACE, o Ø do CENTRO do canal, a partir da aresta de referência
        /// e da PAREDE que se quer entre ela e o canal.
        ///
        /// A parede é medida até a borda do canal MAIS PRÓXIMA da aresta, não até o centro dele
        /// — é assim que se garante que o alojamento não coma a aresta: soma-se a parede (dos
        /// dois lados, daí o 2×) e a largura inteira do canal, que no diâmetro entra uma vez só
        /// porque metade dela fica de cada lado do cordão.
        ///
        /// <paramref name="outward"/> diz de que lado da aresta o canal fica. Vedar em volta da
        /// boca de um FURO empurra o canal para fora (Ø maior); vedar no topo de um EIXO, onde
        /// fora da aresta não há material, puxa o canal para dentro (Ø menor). É a mesma parede,
        /// medida para o outro lado.
        /// </summary>
        public static double FaceSealingDiameter(double referenceDiameterMm, double wallMm,
            double crossSectionMm, SealMotion motion, Elastomer elastomer, bool outward = true)
        {
            double step = 2.0 * wallMm + GrooveWidth(crossSectionMm, motion, elastomer);
            return outward ? referenceDiameterMm + step : referenceDiameterMm - step;
        }

        /// <summary>
        /// A PAREDE que sobrou entre a aresta de referência e a borda do canal (mm, no raio) —
        /// o número que diz se o alojamento respeitou a distância pedida. Negativo = o canal
        /// passou por cima da aresta.
        /// </summary>
        public static double FaceGrooveWall(ORingGrooveSpec spec, double referenceDiameterMm, bool outward)
        {
            if (spec == null) return 0;
            return outward
                ? (spec.GrooveInnerDiameter - referenceDiameterMm) / 2.0
                : (referenceDiameterMm - spec.GrooveOuterDiameter) / 2.0;
        }

        /// <summary>
        /// Seção sugerida para um diâmetro de vedação — regra de bolso: quanto maior a peça,
        /// mais grosso o cordão (anel fino em diâmetro grande não acompanha a folga e a
        /// dilatação). O operador troca na janela se quiser.
        /// </summary>
        public static double SuggestCrossSection(double sealingDiameterMm, IReadOnlyList<double> available)
        {
            double wanted = sealingDiameterMm < 12.0 ? 1.78
                          : sealingDiameterMm < 40.0 ? 2.62
                          : sealingDiameterMm < 100.0 ? 3.53
                          : 5.33;
            if (available == null || available.Count == 0) return wanted;
            return available.OrderBy(d => Math.Abs(d - wanted)).First();
        }

        private static string Pct(double v) => (v * 100.0).ToString("0.0") + "%";
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
