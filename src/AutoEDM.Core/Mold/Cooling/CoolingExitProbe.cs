using System;
using System.Collections.Generic;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// "Deste ponto, nesta direção, quanto material falta até a face?" — medido na PEÇA, para o
    /// planejador prolongar até a face o trecho de canal que acaba num canto dentro da placa.
    ///
    /// Mesmo raio do "Criar eletrodo (manual)", validado ao vivo: <c>Body.FacesByRay</c> reduz as
    /// faces candidatas e a malha de cada uma (<c>Face.GetFacetData</c>) dá o impacto exato. Aqui o
    /// raio sai de DENTRO do material, então o primeiro impacto é a saída. Só leitura.
    ///
    /// Face EXTERNA = o impacto está na caixa envolvente do corpo (lateral da placa). Saída que não
    /// está na caixa é um bolsão/furo interno — o planejador prefere a externa e avisa quando não há.
    /// </summary>
    public sealed class CoolingExitProbe
    {
        private const double FacetToleranceM = 0.00005;
        private readonly object _body;
        private readonly double[] _minMm, _maxMm;
        public int Queries { get; private set; }

        private CoolingExitProbe(object body, double[] minMm, double[] maxMm)
        {
            _body = body; _minMm = minMm; _maxMm = maxMm;
        }

        /// <summary>Sonda do corpo de projeto da peça; null (com log) se não houver corpo legível.</summary>
        public static CoolingExitProbe For(dynamic partDoc)
        {
            try
            {
                object body = (object)partDoc.Models.Item(1).Body;
                if (!FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx))
                {
                    Log.Warn("Refrigeração: caixa do corpo ilegível — sem prolongamento automático.");
                    return null;
                }
                return new CoolingExitProbe(body, mn, mx);
            }
            catch (Exception e)
            {
                Log.Warn("Refrigeração: corpo da peça inacessível — sem prolongamento automático. " + e.GetBaseException().Message);
                return null;
            }
        }

        public CoolingExit Exit(double[] pointMm, double[] dir)
        {
            Queries++;
            double[] o = { Units.MmToM(pointMm[0]), Units.MmToM(pointMm[1]), Units.MmToM(pointMm[2]) };
            List<object> faces = VisibleFacePicker.FacesByRay(_body, o, dir, out bool ok);
            if (!ok) faces = VisibleFacePicker.FacesCrossedByBox(_body, o, dir);

            double best = double.NaN;
            foreach (object f in faces)
            {
                if (!SectionAreaCalculator.TryGetFacetPointsM(f, FacetToleranceM, out double[] pts, out _)) continue;
                double t = RayMath.NearestHit(o, dir, pts);
                if (!double.IsNaN(t) && (double.IsNaN(best) || t < best)) best = t;
            }
            if (double.IsNaN(best)) return new CoolingExit();            // já está na face ou fora

            double mm = Units.MToMm(best);
            double[] hit = { pointMm[0] + dir[0] * mm, pointMm[1] + dir[1] * mm, pointMm[2] + dir[2] * mm };
            bool outer = false;
            for (int k = 0; k < 3; k++)
                if (Math.Abs(hit[k] - _minMm[k]) < 0.5 || Math.Abs(hit[k] - _maxMm[k]) < 0.5) outer = true;
            return new CoolingExit { DistanceMm = mm, OuterFace = outer };
        }
    }
}
