using AutoEDM.Diagnostics;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Leitura de geometria de ARESTA (Edge) via COM, em mm — irmã de <see cref="FaceGeometry"/>.
    /// Mesma unidade de origem (METROS) e a MESMA armadilha dos parâmetros de saída: sem
    /// <c>ParameterModifier</c> by-ref os [out] voltam vazios.
    /// </summary>
    public static class EdgeGeometry
    {
        /// <summary>
        /// Extremidades da aresta (mm, sistema local da peça):
        /// <c>Edge.GetEndPoints([out] StartPoint, [out] EndPoint)</c> — dois SAFEARRAY(double) de
        /// 3, exatamente a forma de <c>Face.GetRange</c> (confirmado no dump da typelib,
        /// <c>SolidEdgeGeometry.Edge</c>), por isso reusa o mesmo executor.
        ///
        /// É o que permite ENCADEAR as arestas abertas em contornos
        /// (<see cref="AutoEDM.Electrode.OpenEdgeLoops"/>): o bbox sozinho não diz qual aresta
        /// encosta em qual.
        /// </summary>
        public static bool TryGetEndPointsMm(object comEdge,
            out double[] startMm, out double[] endMm, out string error)
        {
            error = null;
            if (FaceGeometry.TryTwoPointOutMm(comEdge, "GetEndPoints", out startMm, out endMm, out error))
                return true;

            Log.Warn("EdgeGeometry: GetEndPoints indisponível — " + error);
            return false;
        }
    }
}
