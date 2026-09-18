using System.Collections.Generic;
using System.Linq;
using AutoEDM.Modeling;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>
    /// Testes das primitivas de modelagem. Nada aqui toca o Solid Edge: cobre a VALIDAÇÃO (que é
    /// o que impede meio-modelo na peça do usuário) e a geometria do carrinho de brinquedo, que é
    /// a carga de teste canônica do caminho de escrita da ponte.
    /// </summary>
    public class PrimitiveModelTests
    {
        // ------------------------------------------------------------------ validação

        [Fact]
        public void Box_WithGoodNumbers_IsValid()
        {
            var p = new Primitive { Kind = "caixa", Name = "bloco", SizeXMm = 50, SizeYMm = 30, HeightMm = 20 };
            Assert.Null(p.Validate());
        }

        [Fact]
        public void Cylinder_WithGoodNumbers_IsValid()
        {
            var p = new Primitive { Kind = "cilindro", Name = "pino", DiameterMm = 12, HeightMm = 25 };
            Assert.Null(p.Validate());
        }

        [Theory]
        [InlineData("esfera")]
        [InlineData("")]
        [InlineData(null)]
        public void UnknownKind_IsRejected(string kind)
        {
            var p = new Primitive { Kind = kind, Name = "x", HeightMm = 10, SizeXMm = 1, SizeYMm = 1 };
            Assert.Contains("desconhecido", p.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void NonPositiveHeight_IsRejected(double h)
        {
            var p = new Primitive { Kind = "caixa", Name = "x", SizeXMm = 10, SizeYMm = 10, HeightMm = h };
            Assert.Contains("altura", p.Validate());
        }

        [Fact]
        public void Box_WithoutSides_IsRejected()
        {
            var p = new Primitive { Kind = "caixa", Name = "x", HeightMm = 10 };
            Assert.Contains("sizeXMm", p.Validate());
        }

        [Fact]
        public void Cylinder_WithoutDiameter_IsRejected()
        {
            var p = new Primitive { Kind = "cilindro", Name = "x", HeightMm = 10 };
            Assert.Contains("diameterMm", p.Validate());
        }

        [Fact]
        public void NegativeLift_IsRejected_AndSaysToUseLiftSide()
        {
            // O ponto todo do liftSide: a DISTÂNCIA de AddParallelByDistance é positiva e a
            // direção é um argumento à parte. Aceitar lift negativo aqui deixaria a chamada COM
            // com um valor cujo comportamento ninguém mediu.
            var p = new Primitive { Kind = "caixa", Name = "x", SizeXMm = 10, SizeYMm = 10, HeightMm = 10, LiftMm = -5 };

            string why = p.Validate();
            Assert.Contains("negativo", why);
            Assert.Contains("liftSide", why);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void BadExtrudeSide_IsRejected(int side)
        {
            var p = new Primitive { Kind = "caixa", Name = "x", SizeXMm = 10, SizeYMm = 10, HeightMm = 10, ExtrudeSide = side };
            Assert.Contains("extrudeSide", p.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void BadLiftSide_IsRejected(int side)
        {
            var p = new Primitive { Kind = "caixa", Name = "x", SizeXMm = 10, SizeYMm = 10, HeightMm = 10, LiftSide = side };
            Assert.Contains("liftSide", p.Validate());
        }

        [Fact]
        public void ZeroBasedPlaneIndex_IsRejected()
        {
            // RefPlanes.Item é 1-based; um 0 aqui viraria erro COM obscuro lá dentro.
            var p = new Primitive { Kind = "caixa", Name = "x", SizeXMm = 10, SizeYMm = 10, HeightMm = 10, PlaneIndex = 0 };
            Assert.Contains("1-based", p.Validate());
        }

        [Fact]
        public void BuildWithNoPrimitives_SaysSo_WithoutTouchingTheDocument()
        {
            // partDoc null: se a lista vazia não fosse tratada ANTES, isto estouraria em COM.
            PrimitiveBuildResult r = PrimitiveModeler.Build(null, new List<Primitive>());

            Assert.False(r.Ok);
            Assert.Equal(0, r.Created);
            Assert.Contains("Nenhum primitivo", r.Message);
        }

        [Fact]
        public void BuildValidatesEverythingBeforeCreatingAnything()
        {
            // A primeira primitiva é boa e a segunda não. Com partDoc null, criar a primeira
            // estouraria — então o teste só passa se a validação recusar o LOTE INTEIRO antes de
            // tocar no documento. É essa ordem que evita meio-modelo na peça do usuário.
            var lote = new List<Primitive>
            {
                new Primitive { Kind = "caixa", Name = "boa", SizeXMm = 10, SizeYMm = 10, HeightMm = 10 },
                new Primitive { Kind = "cilindro", Name = "ruim", DiameterMm = 0, HeightMm = 10 }
            };

            PrimitiveBuildResult r = PrimitiveModeler.Build(null, lote);

            Assert.False(r.Ok);
            Assert.Equal(0, r.Created);
            Assert.Contains("RECUSADO antes de tocar na peça", r.Message);
            Assert.Contains("ruim", r.Message);
        }

        [Fact]
        public void NullEntryInTheList_IsRejected_NotDereferenced()
        {
            PrimitiveBuildResult r = PrimitiveModeler.Build(null, new List<Primitive> { null });

            Assert.False(r.Ok);
            Assert.Contains("nulo", r.Message);
        }

        // ------------------------------------------------------------- carrinho de brinquedo

        [Fact]
        public void ToyCar_IsEntirelyValid()
        {
            foreach (Primitive p in PrimitiveModeler.ToyCar())
                Assert.Null(p.Validate());
        }

        [Fact]
        public void ToyCar_HasABodyACabinAndFourWheels()
        {
            List<Primitive> car = PrimitiveModeler.ToyCar();

            Assert.Equal(6, car.Count);
            Assert.Equal(2, car.Count(p => !p.IsCylinder));   // chassi + cabine
            Assert.Equal(4, car.Count(p => p.IsCylinder));    // rodas
            Assert.Contains(car, p => p.Name == "chassi");
            Assert.Contains(car, p => p.Name == "cabine");
        }

        [Fact]
        public void ToyCar_WheelsGrowOutward_NotIntoTheChassis()
        {
            // O erro fácil aqui é deslocar o plano das quatro rodas para o MESMO lado e só
            // inverter a extrusão — duas rodas nasceriam dentro do chassi. Cada lado tem de ter
            // o deslocamento da base e a extrusão no MESMO sentido, e os dois lados opostos.
            List<Primitive> wheels = PrimitiveModeler.ToyCar().Where(p => p.IsCylinder).ToList();

            Assert.All(wheels, w => Assert.Equal(w.LiftSide, w.ExtrudeSide));
            Assert.Equal(2, wheels.Count(w => w.LiftSide == 2));
            Assert.Equal(2, wheels.Count(w => w.LiftSide == 1));
        }

        [Fact]
        public void ToyCar_WheelsTouchTheGround()
        {
            // A altura do eixo tem de ser o RAIO da roda, senão o carrinho flutua ou afunda.
            // No plano das rodas o eixo V é o Z global, então a altura do eixo é CenterYMm.
            foreach (Primitive w in PrimitiveModeler.ToyCar().Where(p => p.IsCylinder))
                Assert.Equal(w.DiameterMm / 2.0, w.CenterYMm, 6);
        }

        [Fact]
        public void ToyCar_HasOneWheelPerCorner()
        {
            // Duas posições ao longo de X (eixo dianteiro e traseiro) × dois lados = 4 cantos
            // distintos. Se duas rodas caíssem no mesmo lugar, o corpo sairia com 3 rodas e
            // ninguém veria pelo número de primitivas.
            var cantos = PrimitiveModeler.ToyCar()
                .Where(p => p.IsCylinder)
                .Select(w => (w.CenterXMm, w.LiftSide))
                .Distinct()
                .ToList();

            Assert.Equal(4, cantos.Count);
            Assert.Equal(2, cantos.Select(c => c.CenterXMm).Distinct().Count());
        }

        [Fact]
        public void ToyCar_CabinSitsOnTopOfTheChassis()
        {
            List<Primitive> car = PrimitiveModeler.ToyCar();
            Primitive body = car.First(p => p.Name == "chassi");
            Primitive cabin = car.First(p => p.Name == "cabine");

            // A base da cabine coincide com o topo do chassi: encostadas, então a booleana funde.
            Assert.Equal(body.LiftMm + body.HeightMm, cabin.LiftMm, 6);
            Assert.Equal(body.LiftSide, cabin.LiftSide);
            // E a cabine é mais estreita que o chassi, senão não parece um carrinho.
            Assert.True(cabin.SizeXMm < body.SizeXMm);
            Assert.True(cabin.SizeYMm < body.SizeYMm);
        }

        [Fact]
        public void ToyCar_ChassisClearsTheGround_SoTheWheelsShow()
        {
            List<Primitive> car = PrimitiveModeler.ToyCar();
            Primitive body = car.First(p => p.Name == "chassi");
            Primitive wheel = car.First(p => p.IsCylinder);

            // Vão livre > 0 e menor que o diâmetro da roda: o chassi não encosta no chão nem
            // fica acima do topo das rodas (aí elas não tocariam o corpo e o sólido sairia solto).
            Assert.True(body.LiftMm > 0);
            Assert.True(body.LiftMm < wheel.DiameterMm);
        }

        [Fact]
        public void ToyCar_UsesTheGivenPlaneIndexes()
        {
            // Os índices de plano NÃO são adivinhados: 'se_planos' os mede e eles entram aqui.
            List<Primitive> car = PrimitiveModeler.ToyCar(planeXY: 3, planeXZ: 1);

            Assert.All(car.Where(p => !p.IsCylinder), p => Assert.Equal(3, p.PlaneIndex));
            Assert.All(car.Where(p => p.IsCylinder), p => Assert.Equal(1, p.PlaneIndex));
        }
    }
}
