using System.Drawing;

namespace AutoEDM.Model
{
    /// <summary>
    /// A face matched by the <see cref="AutoEDM.Selection.FaceSelector"/>, together
    /// with the metadata used to match it. Wraps the live COM face object so
    /// downstream steps (copy, offset) can operate on it while keeping the
    /// diagnostic info that explains why it was selected.
    /// </summary>
    public sealed class SelectedFace
    {
        /// <summary>The live SolidEdgeGeometry.Face COM object (late-bound).</summary>
        public dynamic ComFace { get; }

        /// <summary>The color detected on the face.</summary>
        public Color DetectedColor { get; }

        /// <summary>Which strategy read the color (for diagnostics).</summary>
        public string ColorSource { get; }

        /// <summary>Zero-based index of the owning body within the model.</summary>
        public int BodyIndex { get; }

        /// <summary>Zero-based index of the face within the body.</summary>
        public int FaceIndex { get; }

        public SelectedFace(dynamic comFace, Color detectedColor, string colorSource, int bodyIndex, int faceIndex)
        {
            ComFace = comFace;
            DetectedColor = detectedColor;
            ColorSource = colorSource;
            BodyIndex = bodyIndex;
            FaceIndex = faceIndex;
        }

        public override string ToString() =>
            $"Body[{BodyIndex}].Face[{FaceIndex}] " +
            $"RGB({DetectedColor.R},{DetectedColor.G},{DetectedColor.B}) via {ColorSource}";
    }
}
