using System;
using System.Collections.Generic;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Assembly
{
    /// <summary>A part placed in the assembly, with the info we care about.</summary>
    public sealed class OccurrenceInfo
    {
        public dynamic ComOccurrence { get; }
        public string Name { get; }
        public dynamic OccurrenceDocument { get; }

        public OccurrenceInfo(dynamic comOccurrence, string name, dynamic occurrenceDocument)
        {
            ComOccurrence = comOccurrence;
            Name = name;
            OccurrenceDocument = occurrenceDocument;
        }
    }

    /// <summary>
    /// Works with an open Solid Edge assembly (.asm): enumerates occurrences,
    /// resolves the underlying part document, and reads placement transforms.
    ///
    /// This matches the real workflow: the assembly origin is the machine zero,
    /// the cavity to erode is an occurrence in it, and each electrode is a new
    /// in-context part. Occurrence placement (origin) is also the raw material for
    /// the future burn-coordinate report.
    ///
    /// COM (SolidEdgeAssembly):
    ///   AssemblyDocument.Occurrences        -> Occurrences
    ///     Occurrence.Name                   -> string
    ///     Occurrence.OccurrenceDocument     -> referenced PartDocument/SubAssembly
    ///     Occurrence.GetTransform(out x,y,z, out ax,ay,az)  (angles in radians)
    /// </summary>
    public sealed class AssemblyContext
    {
        public dynamic AssemblyDocument { get; }

        public AssemblyContext(dynamic assemblyDocument)
        {
            AssemblyDocument = assemblyDocument ?? throw new ArgumentNullException(nameof(assemblyDocument));
        }

        /// <summary>Top-level occurrences of the assembly.</summary>
        public IReadOnlyList<OccurrenceInfo> GetOccurrences()
        {
            var result = new List<OccurrenceInfo>();
            dynamic occurrences;
            try { occurrences = AssemblyDocument.Occurrences; }
            catch (Exception ex)
            {
                Log.Warn($"AssemblyDocument.Occurrences unavailable: {ex.Message}");
                return result;
            }

            int count;
            try { count = (int)occurrences.Count; }
            catch { count = 0; }

            for (int i = 1; i <= count; i++) // 1-based
            {
                try
                {
                    dynamic occ = occurrences.Item(i);
                    string name = SafeName(occ);
                    dynamic doc = SafeDoc(occ);
                    result.Add(new OccurrenceInfo(occ, name, doc));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Occurrence[{i}] skipped: {ex.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Best-effort read of an occurrence origin (mm) relative to the assembly.
        /// Uses the late-bound GetTransform out-parameters. Returns false if the
        /// method shape isn't as expected on this SE version.
        /// </summary>
        public bool TryGetOrigin(OccurrenceInfo occ, out double x, out double y, out double z)
        {
            x = y = z = 0;
            try
            {
                // out params on a late-bound COM method: pass a filled args array,
                // then read the mutated slots back out.
                object[] args = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
                object target = occ.ComOccurrence;
                target.GetType().InvokeMember(
                    "GetTransform",
                    BindingFlags.InvokeMethod,
                    null, target, args);

                x = Convert.ToDouble(args[0]);
                y = Convert.ToDouble(args[1]);
                z = Convert.ToDouble(args[2]);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"GetTransform failed for '{occ.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Best-effort read of an occurrence placement: origin (metros) + rotation
        /// angles (radianos), via GetTransform. Usado pelo relatório de coordenadas
        /// para (a) transladar para o zero-máquina e (b) sinalizar se há rotação.
        /// </summary>
        public bool TryGetPlacement(OccurrenceInfo occ,
            out double x, out double y, out double z,
            out double ax, out double ay, out double az)
        {
            x = y = z = ax = ay = az = 0;
            try
            {
                object[] args = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
                object target = occ.ComOccurrence;
                target.GetType().InvokeMember(
                    "GetTransform",
                    BindingFlags.InvokeMethod,
                    null, target, args);

                x = Convert.ToDouble(args[0]); y = Convert.ToDouble(args[1]); z = Convert.ToDouble(args[2]);
                ax = Convert.ToDouble(args[3]); ay = Convert.ToDouble(args[4]); az = Convert.ToDouble(args[5]);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"GetTransform (placement) failed for '{occ.Name}': {ex.Message}");
                return false;
            }
        }

        private static string SafeName(dynamic occ)
        {
            try { return occ.Name; } catch { return "<unnamed>"; }
        }

        private static dynamic SafeDoc(dynamic occ)
        {
            try { return occ.OccurrenceDocument; } catch { return null; }
        }
    }
}
