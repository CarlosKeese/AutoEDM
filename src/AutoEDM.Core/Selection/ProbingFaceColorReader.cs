using System;
using System.Drawing;
using AutoEDM.Diagnostics;

namespace AutoEDM.Selection
{
    /// <summary>
    /// Best-effort face color reader that probes the most likely COM property
    /// paths in order and returns the first that yields a usable color. Because
    /// everything is late-bound, unsupported paths simply throw and are skipped.
    ///
    /// Probe order (most direct first):
    ///   1. face.Style              -> Style object or OLE_COLOR or style-name string
    ///   2. face.FaceStyle          -> same shapes as above (some SE versions)
    ///   3. face.Color              -> OLE_COLOR directly (rare, but cheap to try)
    ///
    /// When a Style *object* is found, these color members are probed on it:
    ///   .FillColor, .FaceColor, .Color, .DiffuseColor
    ///
    /// When a style *name* (string) is found, it is resolved against
    ///   application.StyleTables / application.Styles.
    ///
    /// The first successful path is logged once at Info so you can pin it down for
    /// your environment and replace this probing reader with a direct one.
    /// </summary>
    public sealed class ProbingFaceColorReader : IFaceColorReader
    {
        private static readonly string[] StyleColorMembers =
            { "FillColor", "FaceColor", "Color", "DiffuseColor" };

        public bool TryReadColor(dynamic comFace, dynamic application, out Color color, out string source)
        {
            color = default(Color);
            source = null;

            // 1. face.Style
            if (TryFromCandidate(SafeGet(() => comFace.Style), application, "face.Style", out color, out source))
                return true;

            // 2. face.FaceStyle
            if (TryFromCandidate(SafeGet(() => comFace.FaceStyle), application, "face.FaceStyle", out color, out source))
                return true;

            // 3. face.Color (direct OLE_COLOR)
            if (TryFromCandidate(SafeGet(() => comFace.Color), application, "face.Color", out color, out source))
                return true;

            return false;
        }

        /// <summary>
        /// Interprets a candidate value that may be: a numeric OLE_COLOR, a Style
        /// COM object exposing a color member, or a style-name string.
        /// </summary>
        private bool TryFromCandidate(object candidate, dynamic application, string pathLabel,
            out Color color, out string source)
        {
            color = default(Color);
            source = null;
            if (candidate == null) return false;

            // (a) Numeric OLE_COLOR.
            if (TryAsOleColor(candidate, out color))
            {
                source = pathLabel;
                return true;
            }

            // (b) Style-name string -> resolve via style tables.
            if (candidate is string styleName && !string.IsNullOrWhiteSpace(styleName))
            {
                if (TryResolveNamedStyle(application, styleName, out color))
                {
                    source = $"{pathLabel} -> Styles[\"{styleName}\"]";
                    return true;
                }
                return false;
            }

            // (c) Style COM object -> probe known color members.
            foreach (var member in StyleColorMembers)
            {
                object raw = SafeGetMember(candidate, member);
                if (raw != null && TryAsOleColor(raw, out color))
                {
                    source = $"{pathLabel}.{member}";
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveNamedStyle(dynamic application, string styleName, out Color color)
        {
            color = default(Color);
            try
            {
                // SolidEdgeFramework.Application.StyleTables[type].Item(name)
                dynamic styleTables = SafeGet(() => application.StyleTables);
                if (styleTables == null) return false;

                // Face styles live in a dedicated table; type constants vary, so
                // iterate tables and look the name up in each.
                int count = (int)styleTables.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic table = styleTables.Item(i);
                    dynamic style = SafeGet(() => table.Item(styleName));
                    if (style == null) continue;

                    foreach (var member in StyleColorMembers)
                    {
                        object raw = SafeGetMember(style, member);
                        if (raw != null && TryAsOleColor(raw, out color))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Named-style resolution failed for \"{styleName}\": {ex.Message}");
            }
            return false;
        }

        private static bool TryAsOleColor(object value, out Color color)
        {
            color = default(Color);
            try
            {
                switch (value)
                {
                    case int i:
                        color = OleColor.FromOle(i);
                        return true;
                    case uint ui:
                        color = OleColor.FromOle(ui);
                        return true;
                    case long l:
                        color = OleColor.FromOle(l);
                        return true;
                    case double d:
                        color = OleColor.FromOle((long)d);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Evaluate a late-bound accessor, swallowing COM/binder failures.</summary>
        private static object SafeGet(Func<object> accessor)
        {
            try { return accessor(); }
            catch { return null; }
        }

        /// <summary>Read a named member off a late-bound object without knowing its type.</summary>
        private static object SafeGetMember(object obj, string memberName)
        {
            try
            {
                dynamic d = obj;
                switch (memberName)
                {
                    case "FillColor":    return d.FillColor;
                    case "FaceColor":    return d.FaceColor;
                    case "Color":        return d.Color;
                    case "DiffuseColor": return d.DiffuseColor;
                    default:             return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
