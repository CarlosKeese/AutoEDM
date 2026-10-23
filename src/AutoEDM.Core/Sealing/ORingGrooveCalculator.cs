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
            SealMotion motion, Elastomer elastomer, FacePressure pressure = FacePressure.Internal,
            ORingGlandContext context = null)
        {
            if (ring == null) throw new ArgumentNullException(nameof(ring));
            var ctx = context ?? ORingGlandContext.Default;

            double d2 = ring.CrossSection;
            double sqMin, sqMax;
            SqueezeRange(d2, motion, out sqMin, out sqMax);
            double sqTarget, ignoreMin, ignoreMax;
            ORingGrooveRules.Squeeze(motion, out ignoreMin, out sqTarget, out ignoreMax);

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
            }
            else
            {
                // Profundidade sai do ESMAGAMENTO; largura sai do PREENCHIMENTO. Nessa ordem: é
                // o esmagamento que veda, e a largura é a folga que sobra para a borracha escoar.
                // As duas saem dos mesmos pontos únicos que o resto do módulo consulta.
                depth = d2 * (1.0 - sqTarget);
                width = GrooveWidth(d2, motion, elastomer);
            }

            // "depth" daqui em diante é a ALTURA DO ALOJAMENTO (L): do fundo do canal até o
            // conjugado. A profundidade CORTADA na peça sai dela descontando a folga de montagem.
            var spec = new ORingGrooveSpec
            {
                Ring = ring,
                Kind = kind,
                Motion = motion,
                Elastomer = elastomer,
                Pressure = pressure,
                SealingDiameter = sealingDiameterMm,
                Depth = depth,
                GlandDepth = depth,
                Width = width,
                Sizing = ctx.Sizing,
                MatingSource = ctx.MatingSource,
                PressureBar = ctx.PressureBar,
                BottomRadius = housing != null ? housing.Radius : ORingGrooveRules.BottomRadius(d2),
                EdgeBreak = ORingGrooveRules.EdgeBreak(d2),
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

            bool fromRing = ctx.Sizing == GrooveSizing.FromRing;
            switch (kind)
            {
                case GrooveKind.RadialExternal: // canal no EIXO: o anel é esticado por cima
                {
                    // A altura do alojamento é medida a partir do FURO conjugado: com folga
                    // grande, o fundo sobe e o canal cortado no eixo fica mais raso.
                    double bore = ctx.MatingDiameterMm ?? sealingDiameterMm;
                    double bottom = bore - 2.0 * depth;
                    if (fromRing && ring.InnerDiameter > 0)
                        // Pelo anel: o fundo vai até onde o anel aceita (estiramento entre a folga
                        // máxima e o limite do elastômero) e, dentro disso, o mais perto do
                        // esmagamento de projeto.
                        bottom = Clamp(bottom, ring.InnerDiameter * (1.0 - ORingGrooveRules.MaxSlack),
                                               ring.InnerDiameter * (1.0 + ORingGrooveRules.MaxStretch(motion, elastomer)));
                    spec.MatingDiameter = bore;
                    spec.NominalClearance = bore - sealingDiameterMm;
                    spec.GrooveBottomDiameter = bottom;
                    spec.GlandDepth = (bore - bottom) / 2.0;
                    spec.Depth = (sealingDiameterMm - bottom) / 2.0;
                    spec.Stretch = ring.InnerDiameter > 0
                        ? (spec.GrooveBottomDiameter - ring.InnerDiameter) / ring.InnerDiameter : 0.0;
                    break;
                }

                case GrooveKind.RadialInternal: // canal no FURO: o anel entra comprimido
                {
                    double shaft = ctx.MatingDiameterMm ?? sealingDiameterMm;
                    double bottom = shaft + 2.0 * depth;
                    if (fromRing && ring.OuterDiameter > 0)
                        bottom = Clamp(bottom, ring.OuterDiameter * (1.0 - ORingGrooveRules.MaxOuterCompression(elastomer)),
                                               ring.OuterDiameter * (1.0 + ORingGrooveRules.MaxOuterSlack));
                    spec.MatingDiameter = shaft;
                    spec.NominalClearance = sealingDiameterMm - shaft;
                    spec.GrooveBottomDiameter = bottom;
                    spec.GlandDepth = (bottom - shaft) / 2.0;
                    spec.Depth = (bottom - sealingDiameterMm) / 2.0;
                    spec.Stretch = ring.OuterDiameter > 0
                        ? (ring.OuterDiameter - spec.GrooveBottomDiameter) / ring.OuterDiameter : 0.0;
                    break;
                }

                default: // AxialFace — o anel deita no canal ENCOSTADO na parede que segura a pressão
                    // O vão entre as faces montadas entra na altura do alojamento: corta-se menos.
                    spec.FaceGap = Math.Max(0.0, ctx.FaceGapMm);
                    spec.Depth = depth - spec.FaceGap;
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

            // Pelo anel, a largura acompanha a altura do alojamento: canal mais raso pede canal
            // mais largo para a borracha ter para onde escoar (preenchimento de projeto).
            if (fromRing && kind != GrooveKind.AxialFace && spec.GlandDepth > 0)
            {
                double forFill = ring.SectionArea / (ORingGrooveRules.TargetFill(motion, elastomer) * spec.GlandDepth);
                spec.Width = Math.Max(width, Math.Min(forFill, ORingGrooveRules.MaxWidthFactor * d2));
            }
            if (fromRing)
                spec.Source += kind == GrooveKind.AxialFace
                    ? " — posição pelo anel escolhido"
                    : " — fundo desenhado PELO ANEL escolhido (estiramento dentro do limite)";

            spec.Fill = spec.GlandDepth > 0 ? ring.SectionArea / (spec.Width * spec.GlandDepth) : 1.0;

            // Esticar AFINA o cordão, e cordão mais fino é menos esmagamento — o número que
            // interessa é o depois, não o de catálogo. Quem esmaga é a altura do ALOJAMENTO.
            double effectiveD2 = !spec.StretchIsOuterCompression
                ? ORingGrooveRules.EffectiveCrossSection(d2, spec.Stretch)
                : d2;
            spec.Squeeze = effectiveD2 > 0 ? (effectiveD2 - spec.GlandDepth) / effectiveD2 : 0.0;

            spec.WorstCase = ComputeWorstCase(spec, ctx);
            Review(spec, sqMin, sqMax, motion, elastomer, sealingDiameterMm, housing);
            return spec;
        }

        /// <summary>Faixa de esmagamento admissível: a publicada na tabela, ou a de projeto fora dela.</summary>
        private static void SqueezeRange(double crossSectionMm, SealMotion motion, out double min, out double max)
        {
            var housing = ORingHousingTable.Find(crossSectionMm, motion);
            if (housing != null)
            {
                min = housing.SqueezeMinPct / 100.0;
                max = housing.SqueezeMaxPct / 100.0;
                return;
            }
            double target;
            ORingGrooveRules.Squeeze(motion, out min, out target, out max);
        }

        /// <summary>
        /// Empilha as tolerâncias do ajuste (furo/eixo), das cotas do canal e do anel para o
        /// lado ruim de cada critério. As cotas do canal levam ± <see cref="ORingGlandContext.GrooveToleranceMm"/>
        /// cada uma, como saem no desenho (Ø do fundo, largura, profundidade).
        /// </summary>
        private static GrooveWorstCase ComputeWorstCase(ORingGrooveSpec spec, ORingGlandContext ctx)
        {
            string holeZone, shaftZone;
            if (!IsoFits.TrySplitFit(ctx.Fit, out holeZone, out shaftZone)) return null;

            var ring = spec.Ring;
            double t = Math.Max(0.0, ctx.GrooveToleranceMm);
            double d2 = ring.CrossSection;
            double td2 = ORingGrooveRules.CrossSectionTolerance(d2);
            double td1 = ORingGrooveRules.InnerDiameterTolerance(ring);
            double d1Min = ring.InnerDiameter - td1, d1Max = ring.InnerDiameter + td1;
            double odMin = d1Min + 2.0 * (d2 - td2), odMax = d1Max + 2.0 * (d2 + td2);

            var wc = new GrooveWorstCase
            {
                Fit = ctx.Fit, GrooveTolerance = t, RingSectionTolerance = td2, RingInnerTolerance = td1
            };
            double lMin, lMax;
            double bottomMin = spec.GrooveBottomDiameter - t, bottomMax = spec.GrooveBottomDiameter + t;

            if (spec.Kind == GrooveKind.AxialFace)
            {
                lMin = spec.GlandDepth - t;
                lMax = spec.GlandDepth + t;
                if (spec.Pressure == FacePressure.External)
                {
                    wc.StretchMin = (spec.GrooveInnerDiameter - t - d1Max) / d1Max;
                    wc.StretchMax = (spec.GrooveInnerDiameter + t - d1Min) / d1Min;
                }
                else
                {
                    wc.StretchMin = (odMin - (spec.GrooveOuterDiameter + t)) / odMin;
                    wc.StretchMax = (odMax - (spec.GrooveOuterDiameter - t)) / odMax;
                }
            }
            else
            {
                // A peça leva a zona do seu papel: eixo (g6) no canal de eixo, furo (H7) no de furo.
                bool external = spec.Kind == GrooveKind.RadialExternal;
                double boreNom = external ? spec.MatingDiameter : spec.SealingDiameter;
                double shaftNom = external ? spec.SealingDiameter : spec.MatingDiameter;
                double hLo, hHi, sLo, sHi;
                if (!IsoFits.TryDeviations(holeZone, boreNom, out hLo, out hHi) ||
                    !IsoFits.TryDeviations(shaftZone, shaftNom, out sLo, out sHi)) return null;
                double boreMin = boreNom + hLo, boreMax = boreNom + hHi;
                double shaftMin = shaftNom + sLo, shaftMax = shaftNom + sHi;
                wc.ClearanceMin = boreMin - shaftMax;
                wc.ClearanceMax = boreMax - shaftMin;

                if (external)
                {
                    lMin = (boreMin - bottomMax) / 2.0;
                    lMax = (boreMax - bottomMin) / 2.0;
                    wc.StretchMin = (bottomMin - d1Max) / d1Max;
                    wc.StretchMax = (bottomMax - d1Min) / d1Min;
                }
                else
                {
                    lMin = (bottomMin - shaftMax) / 2.0;
                    lMax = (bottomMax - shaftMin) / 2.0;
                    wc.StretchMin = (odMin - bottomMax) / odMin;
                    wc.StretchMax = (odMax - bottomMin) / odMax;
                }
            }

            // Menos esmagamento: cordão fino, mais esticado, alojamento alto. Mais: o contrário.
            bool compression = spec.StretchIsOuterCompression;
            double thin = compression ? d2 - td2 : ORingGrooveRules.EffectiveCrossSection(d2 - td2, wc.StretchMax);
            double thick = compression ? d2 + td2 : ORingGrooveRules.EffectiveCrossSection(d2 + td2, wc.StretchMin);
            wc.SqueezeMin = thin > 0 ? (thin - lMax) / thin : 0.0;
            wc.SqueezeMax = thick > 0 ? (thick - lMin) / thick : 0.0;
            double widthMin = spec.Width - t;
            wc.FillMax = lMin > 0 && widthMin > 0 ? Math.PI / 4.0 * (d2 + td2) * (d2 + td2) / (widthMin * lMin) : 1.0;
            return wc;
        }

        /// <summary>
        /// Confere se o canal CABE na face escolhida, com o afastamento pedido — antes de cortar.
        /// No canal radial, o canal tem de ficar inteiro dentro da faixa axial da face (afastamento
        /// menor que meia largura abre o canal na aresta; maior que a face o leva para fora dela,
        /// e o recorte nasce com falha). No canal de face, não pode passar por cima da aresta.
        /// Acrescenta os apontamentos ao <paramref name="spec"/>.
        /// </summary>
        public static void CheckPlacement(ORingGrooveSpec spec, ORingTarget target, double offsetMm)
        {
            if (spec == null || target == null || !target.Ok) return;
            void Add(GrooveIssueLevel level, string msg) =>
                spec.Issues.Add(new GrooveIssue { Level = level, Message = msg });

            if (spec.Kind == GrooveKind.AxialFace)
            {
                double land = FaceGrooveWall(spec, target.EdgeDiameterMm, target.FaceGrooveOutward);
                if (land < 0)
                    Add(GrooveIssueLevel.Error,
                        $"o canal passa POR CIMA da aresta em {-land:0.00} mm de raio — aumente a parede ou escolha outro anel.");
                return;
            }

            double half = spec.Width / 2.0;
            double center = target.EdgeAxialMm + target.MaterialDirection * offsetMm;
            if (offsetMm < half - 0.005)
                Add(GrooveIssueLevel.Error,
                    $"afastamento {offsetMm:0.00} mm menor que meia largura do canal ({half:0.00} mm): o canal sai " +
                    $"{half - offsetMm:0.00} mm pela aresta e fica ABERTO na ponta. Use pelo menos {half:0.00} mm.");

            if (target.FaceAxialMaxMm - target.FaceAxialMinMm > 1e-6)
            {
                double lo = center - half, hi = center + half;
                double past = Math.Max(target.FaceAxialMinMm - lo, hi - target.FaceAxialMaxMm);
                if (past > 0.005 && offsetMm >= half - 0.005)
                    Add(GrooveIssueLevel.Error,
                        $"o canal passa {past:0.00} mm do fim da face cilíndrica (face de {target.FaceAxialMaxMm - target.FaceAxialMinMm:0.00} mm " +
                        $"no eixo): diminua o afastamento para no máximo " +
                        $"{Math.Max(half, offsetMm - past):0.00} mm.");
            }
        }

        private static void Review(ORingGrooveSpec spec, double sqMin, double sqMax,
            SealMotion motion, Elastomer elastomer, double sealingDiameterMm, ORingHousing housing)
        {
            void Add(GrooveIssueLevel level, string msg) =>
                spec.Issues.Add(new GrooveIssue { Level = level, Message = msg });

            if (spec.Kind != GrooveKind.AxialFace)
            {
                string mating = spec.Kind == GrooveKind.RadialExternal ? "furo" : "eixo";
                if (spec.NominalClearance < -0.0005)
                    Add(GrooveIssueLevel.Error,
                        $"o {mating} conjugado (Ø {spec.MatingDiameter:0.00}) INTERFERE com a peça (Ø {sealingDiameterMm:0.00}).");
                else if (spec.GlandDepth <= 0)
                    Add(GrooveIssueLevel.Error, $"o fundo do canal passa do {mating} conjugado — o anel não tem onde ficar.");
                else if (spec.Depth <= 0.005)
                    Add(GrooveIssueLevel.Error,
                        $"a folga até o {mating} conjugado (Ø {spec.MatingDiameter:0.00}) já é maior que a altura do " +
                        "alojamento: não sobra canal para cortar e o anel não seria esmagado. Reduza a folga ou use um cordão mais grosso.");

                // Limite da folga: o da tabela, ou interpolado pela seção quando o anel não é
                // tabelado (métricos) — antes esses ficavam sem aviso nenhum.
                double clearanceMax = housing?.ClearanceMax ?? ORingHousingTable.ClearanceLimit(spec.Ring.CrossSection);
                string limitFrom = housing != null ? "da tabela" : "estimado pela seção";
                if (spec.NominalClearance > clearanceMax + 0.0005)
                {
                    string over = $"jogo diametral {spec.NominalClearance:0.00} mm acima do {clearanceMax:0.00} {limitFrom}";
                    // Com o eixo encostado num lado, o outro fica com o jogo INTEIRO — o canal só
                    // compensa a metade (folga centrada).
                    string offCenter = $" Com o eixo encostado, o lado oposto fica {spec.NominalClearance / 2:0.00} mm mais " +
                                       "alto que o calculado e o esmagamento cai ali.";
                    if (motion != SealMotion.Static)
                        Add(GrooveIssueLevel.Error,
                            $"{over}: vedação DINÂMICA com folga grande — o anel é arrastado para a folga e " +
                            $"morde/rasga (mesmo a {spec.PressureBar:0} bar). Reduza a folga para no máximo {clearanceMax:0.00} mm." + offCenter);
                    else
                    {
                        bool high = spec.PressureBar > ORingGrooveRules.BackupRingPressureBar;
                        Add(GrooveIssueLevel.Warning,
                            $"{over}: " +
                            (high
                                ? $"a {spec.PressureBar:0} bar o anel EXTRUDA pela folga — use ANEL DE APOIO (back-up) ou reduza a folga."
                                : $"a {spec.PressureBar:0} bar o risco de extrusão é baixo, mas a folga é maior que a recomendada.") +
                            offCenter);
                    }
                }
            }

            if (spec.Kind == GrooveKind.RadialExternal)
            {
                double maxStretch = ORingGrooveRules.MaxStretch(motion, elastomer);
                if (motion == SealMotion.Rotary && spec.Stretch > 0.0)
                    Add(GrooveIssueLevel.Error,
                        $"vedação ROTATIVA com o anel esticado {Pct(spec.Stretch)}: borracha esticada contrai ao " +
                        "esquentar (efeito Gow-Joule) e o anel aperta o eixo até queimar. Use um anel de d1 maior.");
                // Margem numérica: o canal "pelo anel" para EXATAMENTE no limite.
                else if (spec.Stretch > maxStretch + 1e-6)
                    Add(GrooveIssueLevel.Error,
                        $"estiramento {Pct(spec.Stretch)} acima do limite de {Pct(maxStretch)} para " +
                        $"{(elastomer == Elastomer.Fkm ? "FKM" : "NBR")} — o anel perde seção e vida útil.");
                else if (spec.Stretch < -ORingGrooveRules.MaxSlack - 1e-6)
                    Add(GrooveIssueLevel.Error,
                        $"anel FOLGADO {Pct(-spec.Stretch)} no fundo do canal (limite {Pct(ORingGrooveRules.MaxSlack)}): " +
                        "fica solto, torce na montagem e não veda. Use um anel de d1 menor.");

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
                if (spec.Depth <= 0.005)
                    Add(GrooveIssueLevel.Error,
                        $"o vão entre as faces ({spec.FaceGap:0.00} mm) já é maior que a altura do alojamento " +
                        $"({spec.GlandDepth:0.00} mm): o anel não seria esmagado.");
            }

            if (spec.Fill > ORingGrooveRules.MaxFill)
                Add(GrooveIssueLevel.Error,
                    $"preenchimento {Pct(spec.Fill)} acima de {Pct(ORingGrooveRules.MaxFill)}: sem espaço para a " +
                    "borracha dilatar, o próprio anel arrebenta o alojamento.");

            if (spec.Squeeze < 0.5 * sqMin)
                Add(GrooveIssueLevel.Error,
                    $"esmagamento efetivo {Pct(spec.Squeeze)}, menos da metade do mínimo de {Pct(sqMin)}: o anel " +
                    "praticamente não é apertado e não veda.");
            else if (spec.Squeeze < sqMin)
                Add(GrooveIssueLevel.Warning,
                    $"esmagamento efetivo {Pct(spec.Squeeze)} abaixo do mínimo de {Pct(sqMin)} para esta vedação: risco de vazar.");
            else if (spec.Squeeze > sqMax)
                Add(GrooveIssueLevel.Warning,
                    $"esmagamento efetivo {Pct(spec.Squeeze)} acima do máximo de {Pct(sqMax)}: anel deforma permanente.");

            // Pior caso: só avisa o que o nominal ainda não acusou (um erro nominal já basta).
            var wc = spec.WorstCase;
            if (wc != null && spec.IsWithinStandard)
            {
                string why = $"no pior caso ({wc.Fit}, canal ±{wc.GrooveTolerance:0.00})";
                if (wc.SqueezeMin < sqMin && spec.Squeeze >= sqMin)
                    Add(GrooveIssueLevel.Warning, $"{why} o esmagamento cai para {Pct(wc.SqueezeMin)} (mínimo {Pct(sqMin)}): pode vazar.");
                if (wc.SqueezeMax > sqMax && spec.Squeeze <= sqMax)
                    Add(GrooveIssueLevel.Warning, $"{why} o esmagamento sobe para {Pct(wc.SqueezeMax)} (máximo {Pct(sqMax)}).");
                if (wc.FillMax > ORingGrooveRules.MaxFill && spec.Fill <= ORingGrooveRules.MaxFill)
                    Add(GrooveIssueLevel.Warning, $"{why} o preenchimento chega a {Pct(wc.FillMax)} (máximo {Pct(ORingGrooveRules.MaxFill)}).");
                if (spec.Kind == GrooveKind.RadialExternal)
                {
                    double maxStretch = ORingGrooveRules.MaxStretch(motion, elastomer);
                    if (wc.StretchMax > maxStretch && motion != SealMotion.Rotary)
                        Add(GrooveIssueLevel.Warning, $"{why} o anel estica até {Pct(wc.StretchMax)} (limite {Pct(maxStretch)}).");
                    if (wc.StretchMin < -ORingGrooveRules.MaxSlack)
                        Add(GrooveIssueLevel.Warning, $"{why} o anel pode ficar folgado {Pct(-wc.StretchMin)} no fundo.");
                }
                if (spec.Kind != GrooveKind.AxialFace && wc.ClearanceMin < 0)
                    Add(GrooveIssueLevel.Warning, $"{why} o conjugado pode interferir ({wc.ClearanceMin:0.000} mm no diâmetro).");
            }

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
            FacePressure pressure = FacePressure.Internal, double sectionTolerance = 0.0,
            ORingGlandContext context = null)
        {
            if (catalog == null || catalog.Count == 0) return new List<ORingCandidate>();

            // A fila é sempre pela TABELA: é ela que diz qual anel serve ao canal de projeto. O
            // modo "pelo anel" entra depois, só no anel que o operador escolher à mão.
            var ctx = (context ?? ORingGlandContext.Default).Clone();
            ctx.Sizing = GrooveSizing.Table;

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
                try { spec = Compute(ring, kind, sealingDiameterMm, motion, elastomer, pressure, ctx); }
                catch { continue; }

                // Canal de tabela reprovado para este anel? Tenta o canal desenhado PARA ele —
                // num diâmetro em que a série não tem o anel certo (Ø6 com cordão 1,78, por
                // exemplo), é o que deixa um anel de prateleira vedar dentro da norma.
                if (!spec.IsWithinStandard && kind != GrooveKind.AxialFace)
                {
                    var ringCtx = ctx.Clone();
                    ringCtx.Sizing = GrooveSizing.FromRing;
                    try
                    {
                        var alt = Compute(ring, kind, sealingDiameterMm, motion, elastomer, pressure, ringCtx);
                        if (alt.IsWithinStandard) spec = alt;
                    }
                    catch { /* fica o da tabela, reprovado */ }
                }

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

                // Esmagamento fora da faixa pesa mais que qualquer desvio de estiramento: é o que veda.
                double sqLo, sqHi;
                SqueezeRange(ring.CrossSection, motion, out sqLo, out sqHi);
                double outside = spec.Squeeze < sqLo ? sqLo - spec.Squeeze : spec.Squeeze > sqHi ? spec.Squeeze - sqHi : 0.0;
                score += outside * 1e4;

                // Empate técnico: o canal de tabela (norma) vem antes do desenhado pelo anel.
                if (spec.Sizing == GrooveSizing.FromRing) score += 50.0;

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
            FacePressure pressure = FacePressure.Internal, double? matingDiameterMm = null)
        {
            double depth = GrooveDepth(crossSectionMm, motion);
            double mating = matingDiameterMm ?? sealingDiameterMm;

            switch (kind)
            {
                case GrooveKind.RadialExternal:
                    return (mating - 2.0 * depth) / (1.0 + ORingGrooveRules.TargetStretch(motion));
                case GrooveKind.RadialInternal:
                    return (mating + 2.0 * depth) / (1.0 - ORingGrooveRules.TargetOuterCompression)
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
