using System.Collections.Generic;
using System.Linq;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Converts a pass into the signed offset applied to the burn faces.
    ///
    /// Sign convention: the electrode must be UNDERSIZED relative to the final
    /// cavity by the spark gap, so the offset moves the burn faces INTO the
    /// electrode body. This interface returns the magnitude (mm); the builder
    /// applies the inward direction.
    /// </summary>
    public interface IOffsetPolicy
    {
        double GetInwardOffsetMm(ElectrodePass pass, string material);
    }

    /// <summary>
    /// Offset por faixa de Ra, conforme a tabela do usuário:
    ///   Ra 0,2–0,8 µm -> 0,05 mm
    ///   Ra 1,6 µm     -> 0,10 mm
    ///   Ra 3,2 µm     -> 0,20 mm
    ///   Ra 6,3 µm     -> 0,30 mm
    ///
    /// Regras: se o passe traz <see cref="ElectrodePass.OffsetOverrideMm"/> &gt; 0,
    /// ele prevalece (ajustes manuais caso a caso). Ra é comparado contra o limite
    /// SUPERIOR de cada faixa; Ra acima de 6,3 usa a maior faixa (0,30).
    /// </summary>
    public sealed class RaOffsetTablePolicy : IOffsetPolicy
    {
        /// <summary>(RaUpper µm, offset mm), ordenado por RaUpper crescente.</summary>
        private readonly List<(double RaUpper, double Offset)> _bands;

        public RaOffsetTablePolicy(IEnumerable<(double RaUpper, double Offset)> bands = null)
        {
            _bands = (bands ?? DefaultBands()).OrderBy(b => b.RaUpper).ToList();
        }

        public double GetInwardOffsetMm(ElectrodePass pass, string material)
        {
            if (pass.OffsetOverrideMm > 0) return pass.OffsetOverrideMm;

            foreach (var band in _bands)
                if (pass.Ra <= band.RaUpper)
                    return band.Offset;

            // Ra acima da última faixa: usa a maior.
            return _bands.Last().Offset;
        }

        private static IEnumerable<(double, double)> DefaultBands() => new[]
        {
            (0.8, 0.05),
            (1.6, 0.10),
            (3.2, 0.20),
            (6.3, 0.30),
        };
    }
}
