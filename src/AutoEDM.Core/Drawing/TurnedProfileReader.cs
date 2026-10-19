using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Drawing
{
    /// <summary>
    /// Lê o corpo de uma PEÇA e o descreve como peça torneada (<see cref="TurnedProfile"/>):
    /// cada face vira plano / cilindro / cone / toro no sistema do eixo, com a cor resolvida pela
    /// tabela da ferramentaria. SÓ LEITURA.
    ///
    /// Caminhos COM validados ao vivo no SE 2023 (15142.101-CDT, 2026-10-19), todos com os [out]
    /// marcados por <see cref="ParameterModifier"/>:
    ///   Face.Geometry → Cylinder.GetCylinderData / Cone.GetConeData / Torus.GetTorusData /
    ///   Plane.GetPlaneData; Edge.Geometry → Circle.GetCircleData; Face.GetParamRange +
    ///   GetPointAtParam + GetNormal (a normal sai do MATERIAL — é o que diz se a face é de eixo
    ///   ou de furo). Unidades em metros, convertidas para mm aqui.
    /// </summary>
    public static class TurnedProfileReader
    {
        private const double AxisTol = 1e-4;   // paralelismo (cos)
        private const double PointTolMm = 0.01;

        public static TurnedProfile Read(object partDoc, FinishCatalog catalog)
        {
            if (catalog == null) catalog = new FinishCatalog();
            var profile = new TurnedProfile();
            dynamic doc = partDoc;
            dynamic body = doc.Models.Item(1).Body;
            dynamic faces = body.Faces[1]; // igQueryAll

            Dictionary<int, FaceLook> featureLook = FaceLookReader.ReadFeatureStyles((object)doc);

            var raw = new List<RawFace>();
            int n = (int)faces.Count;
            for (int i = 1; i <= n; i++)
            {
                object face = faces.Item(i);
                raw.Add(ReadRaw(face, featureLook));
            }

            // Eixo: o do primeiro cilindro/cone/toro. Peça torneada tem todos no mesmo.
            RawFace seed = raw.FirstOrDefault(r => r.Axis != null && r.Kind != RevolvedKind.Plane);
            if (seed == null)
            {
                profile.Rejected.Add("nenhuma face cilíndrica, cônica ou toroidal — não é peça torneada");
                return profile;
            }
            // Sentido estável: componente dominante positivo. Senão o Z do eixo sairia invertido
            // conforme a face que por acaso veio primeiro (cone dá +Z, cilindro dá −Z nesta peça).
            double[] dir = Normalize(seed.Axis);
            int dom = Math.Abs(dir[0]) >= Math.Abs(dir[1]) && Math.Abs(dir[0]) >= Math.Abs(dir[2]) ? 0
                    : Math.Abs(dir[1]) >= Math.Abs(dir[2]) ? 1 : 2;
            if (dir[dom] < 0) dir = new[] { -dir[0], -dir[1], -dir[2] };
            profile.AxisDir = dir;
            profile.AxisPoint = seed.AxisOrigin;

            foreach (RawFace r in raw)
            {
                string why = CheckRevolved(r, profile);
                if (why != null) { profile.Rejected.Add($"face {r.Id} ({r.Kind}): {why}"); continue; }

                RevolvedFace f = ToAxisFrame(r, profile);
                f.Finish = catalog.Resolve(r.StyleName, r.Color);
                profile.Faces.Add(f);
            }

            Log.Info($"Peça torneada: {profile.Faces.Count} face(s) de revolução, {profile.Rejected.Count} recusada(s); " +
                     $"eixo ({Fmt(profile.AxisDir)}) por ({Fmt(profile.AxisPoint)}) mm; comprimento {profile.Length:0.###} mm, Ø máx {2 * profile.RMax:0.###} mm.");
            foreach (RevolvedFace f in profile.Faces.OrderBy(x => x.Z0)) Log.Info("  " + f);
            foreach (string r in profile.Rejected) Log.Warn("  recusada: " + r);
            return profile;
        }

        // ------------------------------------------------------------------ leitura crua

        private sealed class RawFace
        {
            public int Id;
            public RevolvedKind Kind;
            public double[] Axis;          // direção (unitária ou não)
            public double[] AxisOrigin;    // mm
            public double[] PlaneNormal;
            public double[] PlaneRoot;     // mm
            public double HalfAngleDeg;
            public double MinorRadius;     // mm
            public double MajorRadius;     // mm
            public List<double[]> Circles = new List<double[]>(); // centro(3) + eixo(3) + raio, mm
            public double[] MidPoint;      // mm
            public double[] MidNormal;
            public string StyleName;
            public Color? Color;
        }

        private static RawFace ReadRaw(object face, Dictionary<int, FaceLook> featureLook)
        {
            dynamic f = face;
            var r = new RawFace();
            try { r.Id = (int)f.ID; } catch { }

            object geo = null;
            try { geo = f.Geometry; } catch { }
            if (geo != null)
            {
                object[] a;
                if ((a = TryCall(geo, "GetCylinderData", new object[] { new double[3], new double[3], 0.0 }, 0, 1, 2)) != null)
                {
                    r.Kind = RevolvedKind.Cylinder; r.AxisOrigin = Mm(a[0]); r.Axis = Arr(a[1]);
                }
                else if ((a = TryCall(geo, "GetConeData", new object[] { new double[3], new double[3], 0.0, 0.0, false }, 0, 1, 2, 3, 4)) != null)
                {
                    r.Kind = RevolvedKind.Cone; r.AxisOrigin = Mm(a[0]); r.Axis = Arr(a[1]);
                    r.HalfAngleDeg = Convert.ToDouble(a[3]) * 180.0 / Math.PI;
                }
                else if ((a = TryCall(geo, "GetTorusData", new object[] { new double[3], new double[3], 0.0, 0.0 }, 0, 1, 2, 3)) != null)
                {
                    r.Kind = RevolvedKind.Torus; r.AxisOrigin = Mm(a[0]); r.Axis = Arr(a[1]);
                    r.MajorRadius = Convert.ToDouble(a[2]) * 1000.0;
                    r.MinorRadius = Convert.ToDouble(a[3]) * 1000.0;
                }
                else if ((a = TryCall(geo, "GetPlaneData", new object[] { new double[3], new double[3] }, 0, 1)) != null)
                {
                    r.Kind = RevolvedKind.Plane; r.PlaneRoot = Mm(a[0]); r.PlaneNormal = Arr(a[1]);
                }
                ComLifetime.Release(geo);
            }

            // Arestas circulares: dão o raio e a posição no eixo de cada ponta da face.
            try
            {
                dynamic edges = f.Edges;
                int ne = (int)edges.Count;
                for (int i = 1; i <= ne; i++)
                {
                    object e = edges.Item(i);
                    object eg = null;
                    try { eg = ((dynamic)e).Geometry; } catch { }
                    object[] c = eg == null ? null
                        : TryCall(eg, "GetCircleData", new object[] { new double[3], new double[3], 0.0 }, 0, 1, 2);
                    if (c != null)
                    {
                        double[] ctr = Mm(c[0]); double[] ax = Arr(c[1]);
                        r.Circles.Add(new[] { ctr[0], ctr[1], ctr[2], ax[0], ax[1], ax[2], Convert.ToDouble(c[2]) * 1000.0 });
                    }
                    ComLifetime.Release(eg);
                    ComLifetime.Release(e);
                }
            }
            catch (Exception ex) { Log.Warn($"Face {r.Id}: arestas ilegíveis ({ex.GetBaseException().Message})."); }

            // Normal no meio da face (sai do material).
            object[] pr = TryCall(face, "GetParamRange", new object[] { new double[2], new double[2] }, 0, 1);
            if (pr != null)
            {
                double[] mn = Arr(pr[0]), mx = Arr(pr[1]);
                var par = new[] { (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2 };
                object[] pt = TryCall(face, "GetPointAtParam", new object[] { 1, par, new double[3] }, 1, 2);
                object[] nr = TryCall(face, "GetNormal", new object[] { 1, (double[])par.Clone(), new double[3] }, 1, 2);
                if (pt != null) r.MidPoint = Mm(pt[2]);
                if (nr != null) r.MidNormal = Arr(nr[2]);
            }

            // Cor: estilo POR FACE primeiro; sem ele, o da feature (pintura padrão do SE).
            FaceLook look = FaceLookReader.Resolve(face, featureLook);
            if (look != null) { r.StyleName = look.StyleName; r.Color = look.Color; }
            return r;
        }

        // ------------------------------------------------------------------ sistema do eixo

        private static string CheckRevolved(RawFace r, TurnedProfile p)
        {
            double[] d = p.AxisDir;
            switch (r.Kind)
            {
                case RevolvedKind.Plane:
                    if (r.PlaneNormal == null) return "plano sem normal";
                    if (Math.Abs(Math.Abs(Dot(Normalize(r.PlaneNormal), d)) - 1) > AxisTol) return "plano não perpendicular ao eixo";
                    return null;
                case RevolvedKind.Cylinder:
                case RevolvedKind.Cone:
                case RevolvedKind.Torus:
                    if (Math.Abs(Math.Abs(Dot(Normalize(r.Axis), d)) - 1) > AxisTol) return "eixo próprio diferente do eixo da peça";
                    if (DistToAxis(r.AxisOrigin, p) > PointTolMm) return "eixo deslocado do eixo da peça";
                    if (r.Circles.Count == 0) return "sem aresta circular";
                    return null;
                default:
                    return "superfície que não é de revolução";
            }
        }

        private static RevolvedFace ToAxisFrame(RawFace r, TurnedProfile p)
        {
            var f = new RevolvedFace
            {
                Id = r.Id, Kind = r.Kind, StyleName = r.StyleName,
                HalfAngleDeg = r.HalfAngleDeg, MinorRadius = r.MinorRadius, MajorRadius = r.MajorRadius,
            };
            if (r.Kind == RevolvedKind.Torus) f.CenterZ = AxialZ(r.AxisOrigin, p);

            // Pontas pelas arestas circulares: (z, r) no sistema do eixo.
            var ends = r.Circles.Select(c => new[] { AxialZ(new[] { c[0], c[1], c[2] }, p), c[6] }).ToList();

            if (r.Kind == RevolvedKind.Plane)
            {
                double z = AxialZ(r.PlaneRoot, p);
                f.Z0 = f.Z1 = z;
                if (ends.Count == 0) { f.R0 = 0; f.R1 = 0; }
                else if (ends.Count == 1) { f.R0 = 0; f.R1 = ends[0][1]; }
                else { f.R0 = ends.Min(e => e[1]); f.R1 = ends.Max(e => e[1]); }
            }
            else
            {
                double[] lo = ends.OrderBy(e => e[0]).First();
                double[] hi = ends.OrderBy(e => e[0]).Last();
                f.Z0 = lo[0]; f.R0 = lo[1];
                f.Z1 = hi[0]; f.R1 = hi[1];
                if (r.Kind == RevolvedKind.Cylinder) { double rr = (f.R0 + f.R1) / 2; f.R0 = f.R1 = rr; }
            }

            // Eixo ou furo: a normal (que sai do material) aponta para longe do eixo?
            if (r.Kind != RevolvedKind.Plane && r.MidPoint != null && r.MidNormal != null)
            {
                double[] radial = RadialVector(r.MidPoint, p);
                f.External = Dot(radial, r.MidNormal) >= 0;
            }
            return f;
        }

        private static double AxialZ(double[] ptMm, TurnedProfile p) =>
            Dot(Sub(ptMm, p.AxisPoint), p.AxisDir);

        private static double DistToAxis(double[] ptMm, TurnedProfile p)
        {
            double[] v = RadialVector(ptMm, p);
            return Math.Sqrt(Dot(v, v));
        }

        private static double[] RadialVector(double[] ptMm, TurnedProfile p)
        {
            double[] v = Sub(ptMm, p.AxisPoint);
            double t = Dot(v, p.AxisDir);
            return new[] { v[0] - t * p.AxisDir[0], v[1] - t * p.AxisDir[1], v[2] - t * p.AxisDir[2] };
        }

        // ------------------------------------------------------------------ COM / vetores

        /// <summary>Chama um método com [out] by-ref. Null quando o objeto não tem o método.</summary>
        internal static object[] TryCall(object obj, string method, object[] args, params int[] outIdx)
        {
            try
            {
                var pm = new ParameterModifier(args.Length);
                foreach (int i in outIdx) pm[i] = true;
                obj.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, obj, args,
                    new[] { pm }, CultureInfo.InvariantCulture, null);
                return args;
            }
            catch { return null; }
        }

        private static double[] Arr(object o)
        {
            var a = o as Array;
            if (a == null) return new double[3];
            var d = new double[a.Length];
            for (int i = 0; i < a.Length; i++) d[i] = Convert.ToDouble(a.GetValue(i));
            return d;
        }

        private static double[] Mm(object o) => Arr(o).Select(v => v * 1000.0).ToArray();
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        private static double[] Normalize(double[] v)
        {
            double n = Math.Sqrt(Dot(v, v));
            return n < 1e-12 ? new double[] { 0, 0, 1 } : new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        private static string Fmt(double[] v) =>
            string.Join("; ", v.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture)));
    }
}
