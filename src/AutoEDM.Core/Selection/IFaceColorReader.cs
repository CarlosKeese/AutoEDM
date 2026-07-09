using System.Drawing;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Reads the display color of a single Solid Edge face.
    ///
    /// This is deliberately an interface with one small responsibility because the
    /// exact COM property path for a per-face color differs between Solid Edge
    /// versions and between "Face Style" vs "Face Color Override" workflows. When
    /// you validate against a real SE 2023/2026 install, lock the working path
    /// into <see cref="ProbingFaceColorReader"/> (or write a dedicated reader).
    /// </summary>
    public interface IFaceColorReader
    {
        /// <summary>
        /// Attempts to read the face color. Returns true and sets
        /// <paramref name="color"/> + <paramref name="source"/> on success.
        /// </summary>
        /// <param name="comFace">Live SolidEdgeGeometry.Face COM object.</param>
        /// <param name="application">Live Solid Edge Application (for style lookups).</param>
        /// <param name="color">Detected color when the return value is true.</param>
        /// <param name="source">Label describing which property path matched.</param>
        bool TryReadColor(dynamic comFace, dynamic application, out Color color, out string source);
    }
}
