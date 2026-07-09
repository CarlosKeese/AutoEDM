using System.Drawing;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Conversions for Solid Edge / OLE color values.
    ///
    /// OLE_COLOR is a 32-bit value packed as 0x00BBGGRR (blue high, red low) —
    /// the opposite channel order from GDI+ ARGB. All Solid Edge color properties
    /// (FillColor, style colors, etc.) use this packing.
    /// </summary>
    public static class OleColor
    {
        public static Color FromOle(long oleColor)
        {
            int r = (int)(oleColor & 0xFF);
            int g = (int)((oleColor >> 8) & 0xFF);
            int b = (int)((oleColor >> 16) & 0xFF);
            return Color.FromArgb(r, g, b);
        }

        public static long ToOle(Color c) => c.R | (c.G << 8) | (c.B << 16);

        /// <summary>True if two colors match within a per-channel tolerance.</summary>
        public static bool Matches(Color a, Color b, int tolerance)
        {
            return System.Math.Abs(a.R - b.R) <= tolerance
                && System.Math.Abs(a.G - b.G) <= tolerance
                && System.Math.Abs(a.B - b.B) <= tolerance;
        }
    }
}
