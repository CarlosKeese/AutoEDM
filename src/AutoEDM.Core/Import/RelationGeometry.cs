using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoEDM.Import
{
    /// <summary>O que uma face pode ser, para efeito de relação de montagem.</summary>
    public enum RelationFaceKind
    {
        Plane = 0,
        Cylinder = 1
    }

    /// <summary>
    /// Uma face já lida e levada para as COORDENADAS DA MONTAGEM, em milímetros, reduzida ao
    /// mínimo de que a dedução de relações precisa. Dado puro: quem leu o COM foi
    /// <see cref="RelationInferrer"/>, e quem decide o que casa com o quê é
    /// <see cref="RelationCandidates"/> — que por isso é testável sem Solid Edge nenhum.
    ///
    /// RESTRIÇÃO DECLARADA: só faces cujo eixo/normal é PARALELO a X, Y ou Z da montagem. Não é
    /// preguiça — é o limite do que se pode deduzir com a API confirmada neste projeto (o eixo
    /// sai da direção em que a caixa envolvente da aresta circular tem espessura zero, a mesma
    /// técnica já validada no alojamento de O'ring). Face inclinada entra no relatório como
    /// "não proposta", nunca como palpite. Numa base de molde — placas, pinos, buchas, colunas
    /// — isso cobre praticamente tudo.
    /// </summary>
    public sealed class RelationFace
    {
        public RelationFaceKind Kind { get; set; }

        /// <summary>O objeto COM da face. É dele que sai a <c>CreateReference</c> na hora de
        /// criar a relação — e ele tem de ser RELIDO na hora, porque proxy de face envelhece
        /// (armadilha 5 da skill).</summary>
        public object Com { get; set; }

        /// <summary>Índice da ocorrência dona, dentro da lista da varredura.</summary>
        public int OwnerIndex { get; set; }

        /// <summary>
        /// <c>Face.ID</c> — a identidade ESTÁVEL da face dentro do corpo. É por ela que a face é
        /// reconquistada (<c>Body.GetFaceByFaceID</c>) na hora de criar cada relação: o objeto
        /// COM guardado em <see cref="Com"/> envelhece quando a montagem se regenera (e ela se
        /// regenera a cada relação criada), e <c>CreateReference</c> com proxy velho devolve
        /// E_FAIL. Zero quando a face não expôs ID — aí só resta o proxy guardado.
        /// </summary>
        public int FaceId { get; set; }

        /// <summary>0=X, 1=Y, 2=Z — a normal (plano) ou o eixo (cilindro), na montagem.</summary>
        public int Axis { get; set; }

        /// <summary>Plano: a coordenada do plano ao longo da normal (mm). Cilindro: não usado.</summary>
        public double PlaneAtMm { get; set; }

        /// <summary>Caixa envolvente da face na montagem (mm), min e max nos 3 eixos.</summary>
        public double[] MinMm { get; set; }
        public double[] MaxMm { get; set; }

        /// <summary>Área da face (mm²) — usada para preferir as faces grandes, que são as que
        /// de fato apoiam a peça.</summary>
        public double AreaMm2 { get; set; }

        /// <summary>Cilindro: raio (mm).</summary>
        public double RadiusMm { get; set; }

        /// <summary>Cilindro: o ponto do eixo nos dois eixos que não são o <see cref="Axis"/>
        /// (mm, coordenadas da montagem).</summary>
        public double[] AxisPointMm { get; set; }

        /// <summary>Um ponto real da face (mm, montagem) para servir de ConstrainingPoint.
        /// Vértice de verdade, nunca centro de caixa envolvente — centro de caixa pode cair
        /// dentro de um furo (nota da skill).</summary>
        public double[] AnchorMm { get; set; }

        public double ExtentMm(int axis) => MaxMm[axis] - MinMm[axis];

        public string Describe() => Kind == RelationFaceKind.Plane
            ? string.Format(CultureInfo.InvariantCulture, "plano {0} = {1:0.###} mm, {2:0.#} mm2",
                AxisName(Axis), PlaneAtMm, AreaMm2)
            : string.Format(CultureInfo.InvariantCulture, "cilindro Ø{0:0.###} mm, eixo {1}",
                2.0 * RadiusMm, AxisName(Axis));

        public static string AxisName(int a) => a == 0 ? "X" : a == 1 ? "Y" : "Z";
    }

    /// <summary>O tipo de relação proposta.</summary>
    public enum ProposedRelationKind
    {
        /// <summary>Eixos coincidentes: pino no furo, bucha no alojamento, coluna na guia.</summary>
        Axial = 0,

        /// <summary>Faces em CONTATO (encostadas uma na outra).</summary>
        PlanarContact = 1
    }

    /// <summary>Uma relação que a geometria atual já satisfaz, e que portanto pode ser criada
    /// sem mover nada.</summary>
    public sealed class ProposedRelation
    {
        public ProposedRelationKind Kind { get; set; }

        /// <summary>A ocorrência que fica parada (a referência).</summary>
        public int FixedIndex { get; set; }

        /// <summary>A ocorrência que passa a ser governada pela relação.</summary>
        public int MovingIndex { get; set; }

        public RelationFace FixedFace { get; set; }
        public RelationFace MovingFace { get; set; }

        /// <summary>Quão boa é a evidência: área de contato (mm²) ou comprimento de encaixe
        /// (mm). Ordena as propostas — relação sobre face grande é mais provavelmente a
        /// intenção do projeto do que sobre uma lasca.</summary>
        public double Strength { get; set; }

        /// <summary>O quanto a geometria já está fora do ideal (mm) — folga do encaixe, ou
        /// distância entre as faces. Entra no relatório para o usuário julgar.</summary>
        public double FitMm { get; set; }

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} / {2} ({3}, folga {4:0.###} mm)",
            Kind == ProposedRelationKind.Axial ? "eixo" : "contato",
            FixedFace.Describe(), MovingFace.Describe(),
            RelationFace.AxisName(FixedFace.Axis), FitMm);
    }

    /// <summary>
    /// A dedução das relações ÓBVIAS: dadas as faces já em coordenadas da montagem, quais pares
    /// estão em contato ou coaxiais NA POSIÇÃO ATUAL. Lógica pura — nenhuma chamada COM aqui.
    ///
    /// O princípio que torna isto seguro: a montagem importada já está na posição certa, então
    /// uma relação só é proposta quando a geometria ATUAL já a satisfaz. Uma relação assim, ao
    /// ser criada, não tem para onde mover a peça. É o que permite verificar depois
    /// (<see cref="RelationInferrer"/>) que nada se moveu e desfazer o que mover.
    /// </summary>
    public static class RelationCandidates
    {
        /// <summary>Folga máxima (mm) entre duas faces para chamá-las de "em contato". Encosto
        /// de placa num molde é contato de verdade; 0,02 mm cobre o arredondamento do tradutor
        /// sem inventar contato onde há folga de projeto.</summary>
        public const double ContactGapMm = 0.02;

        /// <summary>Desalinhamento máximo (mm) entre dois eixos para chamá-los de coaxiais.</summary>
        public const double AxisToleranceMm = 0.02;

        /// <summary>Diferença máxima de RAIO (mm) entre pino e furo. Um Ø8 h6 num Ø8 H7 difere
        /// uns 0,01 mm no raio; 0,15 mm cobre ajuste folgado sem casar Ø8 com Ø10.</summary>
        public const double RadiusToleranceMm = 0.15;

        /// <summary>Área mínima (mm²) para uma face plana valer como apoio. Abaixo disso é
        /// chanfro, lasca ou face de alívio — relação ali é ruído, não intenção.</summary>
        public const double MinContactAreaMm2 = 25.0;

        /// <summary>
        /// Todas as relações que a posição atual já satisfaz, entre as faces de duas ocorrências.
        /// <paramref name="fixedIndex"/> é a que fica parada.
        /// </summary>
        public static List<ProposedRelation> Between(
            int fixedIndex, IList<RelationFace> fixedFaces,
            int movingIndex, IList<RelationFace> movingFaces)
        {
            var found = new List<ProposedRelation>();
            if (fixedFaces == null || movingFaces == null) return found;

            foreach (RelationFace a in fixedFaces)
            {
                foreach (RelationFace b in movingFaces)
                {
                    if (a.Kind != b.Kind) continue;
                    if (a.Axis != b.Axis) continue;

                    ProposedRelation p = a.Kind == RelationFaceKind.Cylinder
                        ? Coaxial(fixedIndex, a, movingIndex, b)
                        : Contact(fixedIndex, a, movingIndex, b);
                    if (p != null) found.Add(p);
                }
            }

            found.Sort((x, y) => y.Strength.CompareTo(x.Strength));
            return found;
        }

        /// <summary>Dois cilindros do mesmo eixo, já concêntricos e de raio compatível.</summary>
        private static ProposedRelation Coaxial(int fi, RelationFace a, int mi, RelationFace b)
        {
            double dr = Math.Abs(a.RadiusMm - b.RadiusMm);
            if (dr > RadiusToleranceMm) return null;

            // Distância entre os dois eixos, medida nos dois eixos que não são o axial.
            double d1 = a.AxisPointMm[0] - b.AxisPointMm[0];
            double d2 = a.AxisPointMm[1] - b.AxisPointMm[1];
            double off = Math.Sqrt(d1 * d1 + d2 * d2);
            if (off > AxisToleranceMm) return null;

            // Os dois tubos têm de se sobrepor AO LONGO do eixo: dois furos coaxiais em placas
            // distantes não são um encaixe, e relacioná-los seria inventar uma intenção.
            double lo = Math.Max(a.MinMm[a.Axis], b.MinMm[b.Axis]);
            double hi = Math.Min(a.MaxMm[a.Axis], b.MaxMm[b.Axis]);
            double overlap = hi - lo;
            if (overlap <= 0.1) return null;

            return new ProposedRelation
            {
                Kind = ProposedRelationKind.Axial,
                FixedIndex = fi,
                MovingIndex = mi,
                FixedFace = a,
                MovingFace = b,
                Strength = overlap * Math.Min(a.RadiusMm, b.RadiusMm),
                FitMm = Math.Max(off, dr)
            };
        }

        /// <summary>Duas faces planas paralelas, encostadas e com sobreposição real.</summary>
        private static ProposedRelation Contact(int fi, RelationFace a, int mi, RelationFace b)
        {
            if (a.AreaMm2 < MinContactAreaMm2 || b.AreaMm2 < MinContactAreaMm2) return null;

            double gap = Math.Abs(a.PlaneAtMm - b.PlaneAtMm);
            if (gap > ContactGapMm) return null;

            // Sobreposição NO PLANO: duas placas no mesmo Z mas lado a lado não se encostam.
            int u = (a.Axis + 1) % 3, v = (a.Axis + 2) % 3;
            double ou = Overlap(a.MinMm[u], a.MaxMm[u], b.MinMm[u], b.MaxMm[u]);
            double ov = Overlap(a.MinMm[v], a.MaxMm[v], b.MinMm[v], b.MaxMm[v]);
            if (ou <= 0.1 || ov <= 0.1) return null;

            double area = ou * ov;
            if (area < MinContactAreaMm2) return null;

            return new ProposedRelation
            {
                Kind = ProposedRelationKind.PlanarContact,
                FixedIndex = fi,
                MovingIndex = mi,
                FixedFace = a,
                MovingFace = b,
                Strength = area,
                FitMm = gap
            };
        }

        private static double Overlap(double aMin, double aMax, double bMin, double bMax) =>
            Math.Min(aMax, bMax) - Math.Max(aMin, bMin);

        /// <summary>
        /// Escolhe, entre as propostas, um conjunto que PRENDE a peça sem sobredeterminá-la.
        ///
        /// Por que limitar. A armadilha registrada na skill é justamente esta: uma montagem de
        /// base de molde com 45 relações (três planares por placa) relatou tudo "resolvido" e
        /// não governava nada, e pontos ruins deixaram placas OverDefined. Sobra de relação não
        /// é segurança — é o caminho para o solver escolher a solução errada. Então: um eixo
        /// prende 4 graus e dispensa dois planares; cada planar prende 3; e o teto é
        /// <paramref name="maxPerOccurrence"/>.
        /// </summary>
        public static List<ProposedRelation> Choose(IList<ProposedRelation> all, int maxPerOccurrence = 3)
        {
            var chosen = new List<ProposedRelation>();
            if (all == null) return chosen;

            bool hasAxial = false;
            var usedAxes = new HashSet<int>();

            foreach (ProposedRelation p in all)
            {
                if (chosen.Count >= maxPerOccurrence) break;

                if (p.Kind == ProposedRelationKind.Axial)
                {
                    // Dois eixos na mesma peça só fazem sentido se forem eixos diferentes; dois
                    // pinos paralelos prendem a rotação, mas a segunda relação coaxial no MESMO
                    // eixo é redundante por construção.
                    if (hasAxial && usedAxes.Contains(p.FixedFace.Axis)) continue;
                    hasAxial = true;
                    usedAxes.Add(p.FixedFace.Axis);
                    chosen.Add(p);
                    continue;
                }

                // Um planar por DIREÇÃO: dois planos com a mesma normal prendem o mesmo grau de
                // liberdade, e é exatamente aí que nasce o OverDefined.
                if (usedAxes.Contains(p.FixedFace.Axis)) continue;
                usedAxes.Add(p.FixedFace.Axis);
                chosen.Add(p);
            }
            return chosen;
        }
    }
}
