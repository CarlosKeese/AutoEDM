using System;
using System.Collections.Generic;
using AutoEDM.Assembly;
using AutoEDM.Import;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// A peneira geométrica: ela tem de JUNTAR o que é a mesma peça e SEPARAR o que não é, sem
    /// nunca ser tratada como prova (quem prova é o <see cref="RigidFit"/>). O caso que mais
    /// importa aqui é o do espelho: ele passa pela peneira de propósito, porque é indistinguível
    /// por medidas — e é justamente por isso que a peneira não pode ser a última palavra.
    /// </summary>
    public class GeometrySignatureTests
    {
        private static GeometrySignature Sig(double vol, int faces, int edges, int verts,
            double[] areas = null, double[] radii = null)
        {
            double total = 0;
            if (areas != null) foreach (double a in areas) total += a;
            return new GeometrySignature
            {
                VolumeMm3 = vol,
                FaceCount = faces,
                EdgeCount = edges,
                VertexCount = verts,
                SortedFaceAreasMm2 = areas ?? new double[0],
                SortedCylinderRadiiMm = radii ?? new double[0],
                AreaMm2 = total,
                BoxMm = new[] { 10.0, 20.0, 30.0 }
            };
        }

        [Fact]
        public void SameGeometry_SharesCoarseKey()
        {
            Assert.Equal(Sig(48000, 6, 12, 8).CoarseKey(), Sig(48000, 6, 12, 8).CoarseKey());
        }

        [Fact]
        public void DifferentTopology_DoesNotShareCoarseKey()
        {
            Assert.NotEqual(Sig(48000, 6, 12, 8).CoarseKey(), Sig(48000, 7, 15, 10).CoarseKey());
        }

        /// <summary>
        /// O volume NÃO entra na chave de agrupamento — ele separa no crivo fino, com tolerância.
        /// Duas peças de volume muito diferente caem no mesmo balde e são separadas por
        /// <see cref="GeometrySignature.Matches"/>; o balde serve só para não comparar todas
        /// contra todas.
        /// </summary>
        [Fact]
        public void DifferentVolume_SharesCoarseKey_ButIsRejectedByFineSieve()
        {
            var a = Sig(48000, 6, 12, 8, new[] { 1000.0, 1000, 2000, 2000, 3000, 3000 });
            var b = Sig(96000, 6, 12, 8, new[] { 1000.0, 1000, 2000, 2000, 3000, 3000 });

            Assert.Equal(a.CoarseKey(), b.CoarseKey());
            Assert.False(a.Matches(b, 0.01));
        }

        /// <summary>
        /// REGRESSÃO do achado de 2026-10-18, com os números medidos. `Corpo do Desenho_279` e
        /// `Corpo do Desenho_356` são a mesma peça (mesma caixa 128×83,44×49,58, mesma topologia
        /// 50F 120A 76V, mesmos Ø 3,513/21,2/24, mesma área) e os volumes diferiam em 0,48 mm³
        /// de 99 623 — 4,8 PPM, arredondamento do tradutor de STEP.
        ///
        /// A chave antiga levava 6 dígitos significativos do volume e punha as duas em baldes
        /// DIFERENTES: elas nunca eram comparadas, e o crivo fino — que tolera 191 mm³ nessa
        /// peça — nunca era alcançado. A lição que este teste tranca: chave de agrupamento não
        /// se faz de grandeza contínua, porque sempre há dois valores quase iguais em lados
        /// opostos de uma fronteira.
        /// </summary>
        [Fact]
        public void TranslatorRounding_StillGroupsTogether()
        {
            var areas = new double[50];
            for (int i = 0; i < areas.Length; i++) areas[i] = 100.0 + i;

            var a = Sig(99622.7357, 50, 120, 76, areas, new[] { 1.7565, 10.6, 12.0 });
            var b = Sig(99623.2167, 50, 120, 76, areas, new[] { 1.7565, 10.6, 12.0 });

            Assert.Equal(a.CoarseKey(), b.CoarseKey());
            Assert.True(a.Matches(b, 0.01), "0,48 mm³ em 99 623 é arredondamento, não peça diferente");
        }

        [Fact]
        public void Matches_AcceptsSamePart()
        {
            var a = Sig(48000, 6, 12, 8, new[] { 200.0, 200.0, 600.0, 600.0, 1200.0, 1200.0 });
            var b = Sig(48000, 6, 12, 8, new[] { 200.0, 200.0, 600.0, 600.0, 1200.0, 1200.0 });
            Assert.True(a.Matches(b, 0.01));
        }

        [Fact]
        public void Matches_RejectsDifferentFaceAreas()
        {
            var a = Sig(48000, 6, 12, 8, new[] { 200.0, 200.0, 600.0, 600.0, 1200.0, 1200.0 });
            var b = Sig(48000, 6, 12, 8, new[] { 100.0, 300.0, 600.0, 600.0, 1200.0, 1200.0 });
            Assert.False(a.Matches(b, 0.01));
        }

        [Fact]
        public void Matches_RejectsDifferentCylinderRadii()
        {
            var a = Sig(48000, 8, 16, 12, new[] { 500.0, 500.0 }, new[] { 4.0 });
            var b = Sig(48000, 8, 16, 12, new[] { 500.0, 500.0 }, new[] { 5.0 });
            Assert.False(a.Matches(b, 0.01));
        }

        /// <summary>Leitura incompleta NUNCA casa. Um corpo lido pela metade que "bate" com outro
        /// é coincidência, não igualdade — e unificar nessa base trocaria peças erradas.</summary>
        [Fact]
        public void Matches_RejectsPartialReads()
        {
            var a = Sig(48000, 6, 12, 8);
            var b = Sig(48000, 6, 12, 8);
            b.Partial = true;
            Assert.False(a.Matches(b, 0.01));
            Assert.False(b.Matches(a, 0.01));
        }
    }

    /// <summary>
    /// A composição de poses: a conta que preserva a posição na troca. Um erro aqui não estoura
    /// — põe a peça no lugar errado, ou pior, apenas GIRADA no lugar certo.
    /// </summary>
    public class ComposedPoseTests
    {
        private static OccurrenceTransform Pose(double[] r9, double[] tM) =>
            OccurrenceTransform.FromMatrix(new[]
            {
                r9[0], r9[3], r9[6], 0,
                r9[1], r9[4], r9[7], 0,
                r9[2], r9[5], r9[8], 0,
                tM[0], tM[1], tM[2], 1
            }, tM);

        private static readonly double[] I9 = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        private static double[] RotZ90 => new double[] { 0, -1, 0, 1, 0, 0, 0, 0, 1 };

        /// <summary>Caso "geometria assada": ocorrência na identidade, o encaixe carrega tudo.
        /// Compor tem de devolver exatamente o encaixe.</summary>
        [Fact]
        public void IdentityPose_ComposedWithFit_YieldsFit()
        {
            OccurrenceTransform p = OccurrenceTransform.Identity();
            OccurrenceTransform c = p.ComposedWith(RotZ90, new[] { 0.1, 0.2, 0.3 });

            Assert.Equal(0.1, c.TxM, 9);
            Assert.Equal(0.2, c.TyM, 9);
            Assert.Equal(0.3, c.TzM, 9);
            Assert.Equal(0.0, c.R(0, 0), 9);
            Assert.Equal(-1.0, c.R(0, 1), 9);
            Assert.Equal(1.0, c.R(1, 0), 9);
        }

        /// <summary>Caso "peça na origem": encaixe identidade, a pose carrega tudo. Compor tem de
        /// devolver exatamente a pose.</summary>
        [Fact]
        public void Pose_ComposedWithIdentityFit_YieldsPose()
        {
            OccurrenceTransform p = Pose(RotZ90, new[] { 0.5, -0.25, 0.125 });
            OccurrenceTransform c = p.ComposedWith(I9, new double[] { 0, 0, 0 });

            Assert.Equal(p.TxM, c.TxM, 9);
            Assert.Equal(p.TyM, c.TyM, 9);
            Assert.Equal(p.TzM, c.TzM, 9);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.Equal(p.R(i, j), c.R(i, j), 9);
        }

        /// <summary>
        /// A propriedade que de fato importa, medida em vez de inspecionada: para um ponto p da
        /// peça-mestra, a pose composta tem de levá-lo exatamente onde a pose da duplicada leva
        /// o ponto correspondente T(p). É a definição de "não perdeu a localização".
        /// </summary>
        [Fact]
        public void ComposedPose_PlacesMasterGeometryWhereDuplicateWas()
        {
            // Pose da ocorrência duplicada, com rotação e translação quaisquer.
            double[] rp = { 0, 0, 1, 0, 1, 0, -1, 0, 0 };            // 90° em Y
            OccurrenceTransform dup = Pose(rp, new[] { 0.3, -0.1, 0.05 });

            // Encaixe mestra -> duplicada (em metros, como ComposedWith espera).
            double[] rf = { 0, -1, 0, 1, 0, 0, 0, 0, 1 };            // 90° em Z
            double[] tf = { 0.02, 0.04, -0.01 };

            OccurrenceTransform placed = dup.ComposedWith(rf, tf);

            // Um ponto qualquer da mestra, em metros.
            double[] p = { 0.011, -0.023, 0.037 };

            // Caminho 1: pelo encaixe e depois pela pose da duplicada.
            double[] tp =
            {
                rf[0] * p[0] + rf[1] * p[1] + rf[2] * p[2] + tf[0],
                rf[3] * p[0] + rf[4] * p[1] + rf[5] * p[2] + tf[1],
                rf[6] * p[0] + rf[7] * p[1] + rf[8] * p[2] + tf[2]
            };
            dup.TransformPointM(tp[0], tp[1], tp[2], out double x1, out double y1, out double z1);

            // Caminho 2: direto pela pose composta.
            placed.TransformPointM(p[0], p[1], p[2], out double x2, out double y2, out double z2);

            Assert.Equal(x1, x2, 12);
            Assert.Equal(y1, y2, 12);
            Assert.Equal(z1, z2, 12);
        }

        /// <summary>A pose composta tem de sair no MESMO layout de matriz da original — escrever
        /// na arrumação errada transporia a rotação em silêncio (ver OccurrenceTransform).</summary>
        [Fact]
        public void ComposedPose_KeepsMatrixLayout()
        {
            OccurrenceTransform p = Pose(RotZ90, new[] { 0.5, 0.0, 0.0 });
            OccurrenceTransform c = p.ComposedWith(I9, new double[] { 0, 0, 0 });
            Assert.Equal(p.Layout, c.Layout);
        }
    }

    /// <summary>
    /// A dedução das relações óbvias. O princípio sob teste: só é proposta a relação que a
    /// posição ATUAL já satisfaz — é isso que garante que criá-la não move nada.
    /// </summary>
    public class RelationCandidatesTests
    {
        private static RelationFace Plane(int axis, double at, double[] min, double[] max,
            double area = 1000, int owner = 0)
        {
            var mn = (double[])min.Clone();
            var mx = (double[])max.Clone();
            mn[axis] = at; mx[axis] = at;
            return new RelationFace
            {
                Kind = RelationFaceKind.Plane,
                Axis = axis,
                PlaneAtMm = at,
                MinMm = mn,
                MaxMm = mx,
                AreaMm2 = area,
                OwnerIndex = owner,
                AnchorMm = new[] { mn[0], mn[1], mn[2] }
            };
        }

        private static RelationFace Cyl(int axis, double r, double[] axisPoint,
            double axMin, double axMax, int owner = 0)
        {
            var mn = new double[3];
            var mx = new double[3];
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            mn[axis] = axMin; mx[axis] = axMax;
            mn[u] = axisPoint[0] - r; mx[u] = axisPoint[0] + r;
            mn[v] = axisPoint[1] - r; mx[v] = axisPoint[1] + r;
            return new RelationFace
            {
                Kind = RelationFaceKind.Cylinder,
                Axis = axis,
                RadiusMm = r,
                AxisPointMm = axisPoint,
                MinMm = mn,
                MaxMm = mx,
                AreaMm2 = 500,
                OwnerIndex = owner
            };
        }

        /// <summary>Duas placas encostadas em Z: contato óbvio, é o que o usuário espera.</summary>
        [Fact]
        public void TouchingPlates_ProposeContact()
        {
            var a = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 200.0, 200.0, 0.0 }) };
            var b = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 200.0, 200.0, 0.0 }, 1000, 1) };

            List<ProposedRelation> found = RelationCandidates.Between(0, a, 1, b);

            Assert.Single(found);
            Assert.Equal(ProposedRelationKind.PlanarContact, found[0].Kind);
            Assert.Equal(2, found[0].FixedFace.Axis);
        }

        /// <summary>Faces no mesmo plano mas LADO A LADO não se encostam. Propor contato aqui
        /// inventaria uma intenção que o projeto não tem.</summary>
        [Fact]
        public void CoplanarButDisjoint_ProposeNothing()
        {
            var a = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 100.0, 100.0, 0.0 }) };
            var b = new[] { Plane(2, 50.0, new[] { 500.0, 0.0, 0.0 }, new[] { 600.0, 100.0, 0.0 }, 1000, 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>Placas separadas por folga real: não é contato.</summary>
        [Fact]
        public void PlatesWithGap_ProposeNothing()
        {
            var a = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 200.0, 200.0, 0.0 }) };
            var b = new[] { Plane(2, 52.0, new[] { 0.0, 0.0, 0.0 }, new[] { 200.0, 200.0, 0.0 }, 1000, 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>Face pequena (chanfro, alívio) não vale apoio — relação ali é ruído.</summary>
        [Fact]
        public void TinyFaces_ProposeNothing()
        {
            var a = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 2.0, 2.0, 0.0 }, area: 4) };
            var b = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 2.0, 2.0, 0.0 }, area: 4, owner: 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>Pino Ø8 num furo Ø8 H7: raios diferem por centésimos e os eixos coincidem.
        /// É a relação axial mais comum de um molde.</summary>
        [Fact]
        public void PinInHole_ProposeAxial()
        {
            var hole = new[] { Cyl(2, 4.006, new[] { 100.0, 50.0 }, 0, 30) };
            var pin = new[] { Cyl(2, 4.000, new[] { 100.0, 50.0 }, 5, 60, owner: 1) };

            List<ProposedRelation> found = RelationCandidates.Between(0, hole, 1, pin);

            Assert.Single(found);
            Assert.Equal(ProposedRelationKind.Axial, found[0].Kind);
            Assert.True(found[0].FitMm < 0.01);
        }

        /// <summary>Dois furos coaxiais em placas DISTANTES não são um encaixe — sem sobreposição
        /// axial, relacioná-los seria invenção.</summary>
        [Fact]
        public void CoaxialButApart_ProposeNothing()
        {
            var a = new[] { Cyl(2, 4.0, new[] { 100.0, 50.0 }, 0, 20) };
            var b = new[] { Cyl(2, 4.0, new[] { 100.0, 50.0 }, 200, 220, owner: 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>Eixos paralelos mas DESLOCADOS não são coaxiais.</summary>
        [Fact]
        public void OffsetAxes_ProposeNothing()
        {
            var a = new[] { Cyl(2, 4.0, new[] { 100.0, 50.0 }, 0, 30) };
            var b = new[] { Cyl(2, 4.0, new[] { 100.5, 50.0 }, 0, 30, owner: 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>Ø8 e Ø10 não são um ajuste — são peças diferentes.</summary>
        [Fact]
        public void MismatchedDiameters_ProposeNothing()
        {
            var a = new[] { Cyl(2, 4.0, new[] { 100.0, 50.0 }, 0, 30) };
            var b = new[] { Cyl(2, 5.0, new[] { 100.0, 50.0 }, 0, 30, owner: 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }

        /// <summary>
        /// A escolha tem de EVITAR sobredeterminar: dois planos com a mesma normal prendem o
        /// mesmo grau de liberdade, e é daí que vem o OverDefined registrado em campo.
        /// </summary>
        [Fact]
        public void Choose_RejectsRedundantParallelPlanes()
        {
            var all = new List<ProposedRelation>
            {
                new ProposedRelation { Kind = ProposedRelationKind.PlanarContact, Strength = 1000,
                    FixedFace = Plane(2, 0, new[] {0.0,0,0}, new[] {10.0,10,0}), MovingFace = Plane(2, 0, new[] {0.0,0,0}, new[] {10.0,10,0}) },
                new ProposedRelation { Kind = ProposedRelationKind.PlanarContact, Strength = 900,
                    FixedFace = Plane(2, 20, new[] {0.0,0,0}, new[] {10.0,10,0}), MovingFace = Plane(2, 20, new[] {0.0,0,0}, new[] {10.0,10,0}) }
            };

            List<ProposedRelation> chosen = RelationCandidates.Choose(all);

            Assert.Single(chosen);
            Assert.Equal(1000, chosen[0].Strength);
        }

        /// <summary>Três planos de normais diferentes prendem uma placa por inteiro — esse
        /// conjunto é legítimo e tem de passar.</summary>
        [Fact]
        public void Choose_AcceptsThreeOrthogonalPlanes()
        {
            var all = new List<ProposedRelation>();
            for (int axis = 0; axis < 3; axis++)
                all.Add(new ProposedRelation
                {
                    Kind = ProposedRelationKind.PlanarContact,
                    Strength = 1000 - axis,
                    FixedFace = Plane(axis, 0, new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 }),
                    MovingFace = Plane(axis, 0, new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 })
                });

            Assert.Equal(3, RelationCandidates.Choose(all).Count);
        }

        /// <summary>O teto por peça é respeitado: sobra de relação não é segurança.</summary>
        [Fact]
        public void Choose_HonoursCap()
        {
            var all = new List<ProposedRelation>();
            for (int axis = 0; axis < 3; axis++)
                all.Add(new ProposedRelation
                {
                    Kind = ProposedRelationKind.PlanarContact,
                    Strength = 1000 - axis,
                    FixedFace = Plane(axis, 0, new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 }),
                    MovingFace = Plane(axis, 0, new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 })
                });

            Assert.Equal(1, RelationCandidates.Choose(all, maxPerOccurrence: 1).Count);
        }

        /// <summary>Plano e cilindro nunca se relacionam entre si neste deduzidor.</summary>
        [Fact]
        public void MixedKinds_ProposeNothing()
        {
            var a = new[] { Plane(2, 50.0, new[] { 0.0, 0.0, 0.0 }, new[] { 200.0, 200.0, 0.0 }) };
            var b = new[] { Cyl(2, 4.0, new[] { 100.0, 50.0 }, 0, 30, owner: 1) };

            Assert.Empty(RelationCandidates.Between(0, a, 1, b));
        }
    }
}
