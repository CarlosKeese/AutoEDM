using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Drawing
{
    /// <summary>
    /// A face "zero" do Desenho 2D (Torno) (Carlos, 2026-10-29): a face plana de onde partem as cotas
    /// de comprimento. Na vista lateral ela fica virada para a DIREITA, como a peça vista do torno
    /// (placa à esquerda, ferramenta faceando à direita). Candidatas: as faces planas perpendiculares
    /// ao eixo — as pontas e os ressaltos.
    /// </summary>
    public static class TurnedZeroFace
    {
        private const double Eps = 0.01;
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

        /// <summary>Faces planas da peça, da ponta oposta à cabeça para a cabeça (a ordem da lista na janela).</summary>
        public static List<RevolvedFace> Candidates(TurnedProfile p)
        {
            bool headAtMax = TurnedDrawingBuilder.HeadAtMaxZ(p);
            IEnumerable<RevolvedFace> planes = p.Faces.Where(f => f.Kind == RevolvedKind.Plane);
            return (headAtMax ? planes.OrderBy(f => f.Z0) : planes.OrderByDescending(f => f.Z0))
                .ThenByDescending(f => f.R1).ToList();
        }

        /// <summary>
        /// Sem escolha: a ponta oposta à cabeça (a maior face nela) — o mesmo desenho de antes, com a
        /// cabeça à esquerda e as cotas partindo da ponta da direita. Null = peça sem face plana.
        /// </summary>
        public static RevolvedFace Default(TurnedProfile p)
        {
            double tip = TurnedDrawingBuilder.HeadAtMaxZ(p) ? p.ZMin : p.ZMax;
            return Candidates(p).Where(f => Math.Abs(f.Z0 - tip) < Eps).OrderByDescending(f => f.R1).FirstOrDefault()
                ?? Candidates(p).FirstOrDefault();
        }

        /// <summary>A face plana de <paramref name="id"/>; null se não for uma das candidatas.</summary>
        public static RevolvedFace Find(TurnedProfile p, int id) =>
            p.Faces.FirstOrDefault(f => f.Kind == RevolvedKind.Plane && f.Id == id);

        /// <summary>
        /// Para que lado do eixo (+1 = Z crescente) a face zero olha — é esse lado que vai para a
        /// direita na vista. Ponta da peça olha para fora dela; ressalto, pela normal lida do 3D
        /// (sem ela, para a ponta mais perto).
        /// </summary>
        public static int Facing(TurnedProfile p, RevolvedFace zero)
        {
            if (Math.Abs(zero.Z0 - p.ZMax) < Eps) return +1;
            if (Math.Abs(zero.Z0 - p.ZMin) < Eps) return -1;
            if (zero.AxialFacing != 0) return zero.AxialFacing;
            return zero.Z0 >= (p.ZMin + p.ZMax) / 2 ? +1 : -1;
        }

        /// <summary>Texto da face para a lista: onde está, o anel que ela forma e a cor.</summary>
        public static string Describe(TurnedProfile p, RevolvedFace f)
        {
            bool headAtMax = TurnedDrawingBuilder.HeadAtMaxZ(p);
            double headEnd = headAtMax ? p.ZMax : p.ZMin, tipEnd = headAtMax ? p.ZMin : p.ZMax;
            string where;
            if (Math.Abs(f.Z0 - tipEnd) < Eps) where = "Ponta oposta à cabeça";
            else if (Math.Abs(f.Z0 - headEnd) < Eps) where = "Ponta da cabeça (Ø maior)";
            else
            {
                bool towardHead = Facing(p, f) == (headAtMax ? +1 : -1);
                where = $"Ressalto a {N(Math.Abs(f.Z0 - tipEnd))} da ponta, virado para " +
                        (towardHead ? "a cabeça" : "a ponta");
            }
            string ring = f.R0 > Eps ? $"anel Ø{N(2 * f.R0)}–Ø{N(2 * f.R1)}" : $"Ø{N(2 * f.R1)}";
            return $"{where} — {ring}" + (f.Finish != null ? $" ({f.Finish.ColorName})" : "") + $"  [face {f.Id}]";
        }

        private static string N(double v) => v.ToString("0.##", Br);
    }
}
