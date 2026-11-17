using System.Collections.Generic;
using System.Linq;
using AutoEDM.Electrode;
using Xunit;

namespace AutoEDM.Core.Tests
{
    /// <summary>T9: parse do "dividir", avisos da seleção, numeração dos EE e a lista do juiz na montagem.</summary>
    public class SemiAutoTests
    {
        [Theory]
        [InlineData(null, SplitMode.Off)]
        [InlineData("false", SplitMode.Off)]
        [InlineData("quina", SplitMode.Auto)]
        [InlineData("QUINA", SplitMode.Auto)]
        [InlineData("true", SplitMode.Force)]
        [InlineData("guilhotina", SplitMode.Force)]
        [InlineData("sim", SplitMode.Off)]
        public void Dividir_IsParsed_WholeByDefault(string token, SplitMode expected)
        {
            Assert.Equal(expected, SplitModes.Parse(token, out string note));
            if (token == "sim") Assert.Contains("não reconhecido", note);
            if (expected != SplitMode.Off) Assert.Contains("experimental", note);
        }

        private static BoundingBox B(double x0, double x1, double y0, double y1) => new BoundingBox { MinX = x0, MaxX = x1, MinY = y0, MaxY = y1 };

        [Fact]
        public void Selection_WarnsUnselectedBurnInsideTheFootprint_AndSelectedThatAreNotBurn()
        {
            var foot = B(-11, 17, -40, -30);
            var burn = new List<(int, BoundingBox)>
            {
                (1, B(-10, 0, -39, -31)),      // selecionada
                (2, B(5, 6, -35, -34)),        // não selecionada, dentro: aviso
                (3, B(-10, 0, -20, -10)),      // não selecionada, fora
                (4, B(16.995, 18, -35, -34))   // encosta 0,005 na borda: não conta
            };
            var (inside, notBurn) = SelectionCheck.Warnings(foot, burn, new HashSet<int> { 1, 9 });
            Assert.Equal(new[] { 2 }, inside);
            Assert.Equal(new[] { 9 }, notBurn);
        }

        [Fact]
        public void NextIndex_FollowsOccurrencesAndFilesAlreadyThere()
        {
            Assert.Equal(1, ElectrodeNamingLogic.NextIndex(new string[0], "15428.100_EDM_EE"));
            Assert.Equal(2, ElectrodeNamingLogic.NextIndex(new[] { "15428.100.par:1", "15428.100_EDM_EE01.par:1" }, "15428.100_EDM_EE"));
            Assert.Equal(4, ElectrodeNamingLogic.NextIndex(new[] { "15428.100_EDM_EE01:1", "15428.100_EDM_EE03" }, "15428.100_EDM_EE"));
        }

        [Fact]
        public void JudgeLines_GivePositionsInTheAssembly_AndOneLineForSlivers()
        {
            var r = new MillJudgeResult();
            r.Failed.Add(new MillHit { Kind = MillHitKind.SharpVertical, Mid = new[] { 1.0, 2, 3 }, LengthMm = 4.64, EmptyDeg = 90, TiltDeg = 2, Why = "canto vivo" });
            r.Slivers.Add(new MillHit { Kind = MillHitKind.SharpInclined, Mid = new[] { 0, 0, 0.0 }, LengthMm = 0.1 });
            r.Slivers.Add(new MillHit { Kind = MillHitKind.SharpInclined, Mid = new[] { 0, 0, 0.0 }, LengthMm = 0.1 });
            List<string> lines = MillJudge.ReportLines(r, new[] { 10.0, -20, -7.149 });
            Assert.Equal(2, lines.Count);
            Assert.Contains("x 11, y -18, z -4.149", lines[0]);
            Assert.StartsWith("2 lasca(s)", lines[1]);
            Assert.StartsWith("precisa dividir: 1 achado(s)", MillJudge.Verdict(r));
            Assert.DoesNotContain("fio", MillJudge.Verdict(r));
        }
    }
}
