using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Selection;

namespace AutoEDM.Sealing
{
    /// <summary>Uma face cilíndrica de OUTRA peça, já no espaço da peça em que vai o canal.</summary>
    public sealed class MatingCylinder
    {
        public string Owner { get; set; }

        /// <summary>Ø da face (mm).</summary>
        public double DiameterMm { get; set; }

        /// <summary>Distância do eixo dela ao eixo do canal (mm) — coaxial é ~0.</summary>
        public double AxisOffsetMm { get; set; }

        /// <summary>|cos| entre os dois eixos — paralelo é ~1.</summary>
        public double AxisCos { get; set; }

        /// <summary>A face cobre a posição axial do canal?</summary>
        public bool CoversGroove { get; set; }
    }

    /// <summary>
    /// Acha o Ø do CONJUGADO quando a peça está em edição em contexto de montagem: o furo que
    /// recebe o eixo, ou o eixo que entra no furo. SÓ LEITURA — nada é aberto, copiado,
    /// salvo ou fechado (a sonda que fez SaveCopyAs/Open/Close com uma peça em contexto
    /// apagou o corpo dela, 2026-10-19).
    ///
    /// Caminho: a peça diz se está em contexto (<c>PartDocument.InPlaceActivated</c>); a
    /// montagem é o documento aberto com <c>InPlaceActivated</c>; a(s) ocorrência(s) da peça
    /// dão a pose (<c>GetMatrix</c>); cada OUTRA ocorrência cuja caixa toca o ponto do canal
    /// tem as faces cilíndricas lidas e levadas para o espaço da peça. Fica o cilindro
    /// coaxial, no lado certo do anel e cobrindo a posição do canal.
    /// </summary>
    public static class ORingMatingReader
    {
        private const int IgAssemblyDocument = 3;   // DocumentTypeConstants (dump SE 223)
        private const int IgQueryCylinder = 10;     // FeatureTopologyQueryTypeConstants

        /// <summary>Tolerâncias do "é o conjugado": eixo a menos de 0,05 mm e paralelo a 0,5°.</summary>
        public const double CoaxialToleranceMm = 0.05;
        public const double ParallelCos = 0.99996;

        /// <summary>
        /// Escolhe o conjugado entre os cilindros achados — parte pura, coberta por teste.
        /// Num canal de EIXO o conjugado é o menor Ø que ainda envolve o eixo; num de FURO, o
        /// maior Ø que ainda cabe no furo. Só vale um cilindro coaxial, cobrindo o canal e a
        /// uma distância plausível de folga (até 10 % do Ø, mínimo 1 mm no diâmetro).
        /// </summary>
        public static MatingCylinder Pick(GrooveKind kind, double ourDiameterMm, IEnumerable<MatingCylinder> found)
        {
            if (kind == GrooveKind.AxialFace || found == null) return null;
            double reach = Math.Max(1.0, 0.10 * ourDiameterMm);
            var ok = found.Where(c => c.CoversGroove && c.AxisCos >= ParallelCos && c.AxisOffsetMm <= CoaxialToleranceMm);
            return kind == GrooveKind.RadialExternal
                ? ok.Where(c => c.DiameterMm >= ourDiameterMm - 0.005 && c.DiameterMm <= ourDiameterMm + reach)
                    .OrderBy(c => c.DiameterMm).FirstOrDefault()
                : ok.Where(c => c.DiameterMm <= ourDiameterMm + 0.005 && c.DiameterMm >= ourDiameterMm - reach)
                    .OrderByDescending(c => c.DiameterMm).FirstOrDefault();
        }

        /// <summary>
        /// Tenta ler o Ø conjugado. null quando a peça não está em contexto, quando nada
        /// coaxial cobre o canal, ou quando a peça aparece mais de uma vez na montagem com
        /// conjugados diferentes (não há como saber qual instância está em edição).
        /// </summary>
        /// <param name="grooveAxialMm">Posição do centro do canal no eixo, no espaço da peça.</param>
        public static bool TryRead(object app, object partDoc, ORingTarget target, GrooveKind kind,
            double grooveAxialMm, out double diameterMm, out string source)
        {
            diameterMm = 0; source = null;
            if (app == null || partDoc == null || target == null || !target.Ok || kind == GrooveKind.AxialFace) return false;

            int docType = 0;
            try { docType = Convert.ToInt32(Get(partDoc, "Type")); } catch { }
            if (docType == IgAssemblyDocument)
            {
                // Não confirmado ao vivo: se a SE entregar a MONTAGEM como documento ativo durante
                // a edição em contexto, este log diz — e o leitor precisa partir da face.
                Log.Warn("[conjugado] o documento ativo é a MONTAGEM, não a peça em edição — leitura do conjugado não implementada para este caso.");
                return false;
            }
            bool inPlace = false;
            try { inPlace = Convert.ToBoolean(Get(partDoc, "InPlaceActivated")); } catch { }
            if (!inPlace) return false;   // peça aberta sozinha: o normal, nada a logar
            Log.Info($"[conjugado] peça em contexto (tipo {docType}, InPlaceActivated=True) — procurando a peça coaxial.");

            string partPath = Str(Get(partDoc, "FullName"));
            object asm = FindInPlaceAssembly(app, partDoc);
            if (asm == null) { Log.Warn("[conjugado] peça em contexto, mas a montagem dona não foi achada entre os documentos abertos."); return false; }

            var ctx = new AssemblyContext((dynamic)asm);
            var all = ctx.GetOccurrences();
            var own = all.Where(o => SamePath(DocPath(o), partPath)).ToList();
            if (own.Count == 0)
            {
                Log.Warn($"[conjugado] '{System.IO.Path.GetFileName(partPath)}' não está no 1º nível da montagem — subconjunto ainda não é lido.");
                return false;
            }

            // Ponto e direção do canal no espaço da PEÇA (mm → m na hora de transformar).
            int ax = target.AxisIndex;
            var pLocal = (double[])target.CenterMm.Clone();
            pLocal[ax] = grooveAxialMm;
            var dLocal = new double[3]; dLocal[ax] = 1.0;

            var picks = new List<MatingCylinder>();
            foreach (var mine in own)
            {
                var pose = AssemblyContext.TryGetPose(mine);
                if (pose == null) { Log.Warn($"[conjugado] pose de '{mine.Name}' ilegível."); return false; }
                double px, py, pz;
                pose.TransformPointM(pLocal[0] / 1000.0, pLocal[1] / 1000.0, pLocal[2] / 1000.0, out px, out py, out pz);
                var dAsm = Rotate(pose, dLocal);

                var found = new List<MatingCylinder>();
                foreach (var other in all)
                {
                    if (SamePath(DocPath(other), partPath)) continue;
                    if (!BoxNear(other, px, py, pz, target.SealingDiameterMm / 1000.0)) continue;
                    found.AddRange(Cylinders(other, px, py, pz, dAsm));
                }
                var pick = Pick(kind, target.SealingDiameterMm, found);
                Log.Info($"[conjugado] instância '{mine.Name}': {found.Count} cilindro(s) perto do canal" +
                         (pick != null ? $" → Ø{pick.DiameterMm:0.000} de '{pick.Owner}'." : " → nenhum coaxial cobrindo o canal."));
                if (pick == null) return false;
                picks.Add(pick);
            }

            if (picks.Any(p => Math.Abs(p.DiameterMm - picks[0].DiameterMm) > 0.005))
            {
                Log.Warn("[conjugado] a peça aparece mais de uma vez com conjugados diferentes — não dá para saber qual está em edição; informe a folga na janela.");
                return false;
            }
            diameterMm = Math.Round(picks[0].DiameterMm, 3);
            source = $"lido em contexto: {picks[0].Owner}";
            return true;
        }

        private static object FindInPlaceAssembly(object app, object partDoc)
        {
            string hint = null;
            try
            {
                var args = new object[] { "" };
                var pm = new ParameterModifier(1); pm[0] = true;
                partDoc.GetType().InvokeMember("GetInContextAssemblyNameForInterpartLinks", BindingFlags.InvokeMethod,
                    null, partDoc, args, new[] { pm }, CultureInfo.InvariantCulture, null);
                hint = Str(args[0]);
            }
            catch { }

            // 1º teste ao vivo (2026-10-21, 15:19): a peça disse InPlaceActivated=True, mas nenhuma
            // montagem aberta respondeu InPlaceActivated — a busca exigia isso e não achou nada.
            // Agora vale, em ordem: o nome que a própria peça informa; uma montagem com
            // InPlaceActivated ou ModelingInAssembly; a ÚNICA montagem aberta. E o log lista o que
            // havia, para o próximo caso que falhar.
            object docs = Get(app, "Documents");
            int n = 0;
            try { n = Convert.ToInt32(Get(docs, "Count")); } catch { }
            var assemblies = new List<object>();
            object flagged = null;
            var seen = new List<string>();
            for (int i = 1; i <= n; i++)
            {
                object d;
                try { d = docs.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, docs, new object[] { i }); }
                catch { continue; }
                int type = 0;
                try { type = Convert.ToInt32(Get(d, "Type")); } catch { }
                string name = Str(Get(d, "FullName")) ?? Str(Get(d, "Name")) ?? "?";
                bool inPlace = false, modeling = false;
                try { inPlace = Convert.ToBoolean(Get(d, "InPlaceActivated")); } catch { }
                try { modeling = Convert.ToBoolean(Get(d, "ModelingInAssembly")); } catch { }
                seen.Add($"{System.IO.Path.GetFileName(name)} (tipo {type}, InPlace={inPlace}, ModelingInAssembly={modeling})");
                if (type != IgAssemblyDocument) continue;
                if (!string.IsNullOrEmpty(hint) && (SamePath(name, hint) ||
                    name.EndsWith(hint, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(name), System.IO.Path.GetFileName(hint), StringComparison.OrdinalIgnoreCase)))
                    return d;
                assemblies.Add(d);
                if (flagged == null && (inPlace || modeling)) flagged = d;
            }
            if (flagged != null) return flagged;
            if (assemblies.Count == 1) return assemblies[0];
            Log.Warn($"[conjugado] montagem dona não identificada. A peça informa '{hint ?? "(nada)"}'; abertos: " +
                     string.Join("; ", seen));
            return null;
        }

        /// <summary>A caixa da ocorrência (montagem, metros) passa perto do ponto do canal?</summary>
        private static bool BoxNear(OccurrenceInfo occ, double px, double py, double pz, double ourDiameterM)
        {
            try
            {
                object o = occ.ComOccurrence;
                var args = new object[] { new double[3], new double[3] };
                var pm = new ParameterModifier(2); pm[0] = true; pm[1] = true;
                o.GetType().InvokeMember("GetRangeBox", BindingFlags.InvokeMethod, null, o, args, new[] { pm },
                    CultureInfo.InvariantCulture, null);
                var lo = ToDoubles(args[0]); var hi = ToDoubles(args[1]);
                if (lo == null || hi == null || lo.All(v => v == 0) && hi.All(v => v == 0)) return true;   // ilegível: confere as faces
                double m = ourDiameterM + 0.002;
                return px >= lo[0] - m && px <= hi[0] + m && py >= lo[1] - m && py <= hi[1] + m && pz >= lo[2] - m && pz <= hi[2] + m;
            }
            catch { return true; }
        }

        /// <summary>Faces cilíndricas da ocorrência, medidas contra o eixo do canal (montagem, metros).</summary>
        private static IEnumerable<MatingCylinder> Cylinders(OccurrenceInfo occ, double px, double py, double pz, double[] dAsm)
        {
            var result = new List<MatingCylinder>();
            var pose = AssemblyContext.TryGetPose(occ);
            if (pose == null) return result;

            // Ponto e direção do canal no espaço LOCAL da outra peça: as faces ficam como estão.
            double qx, qy, qz, ex, ey, ez;
            pose.InverseTransformPointM(px, py, pz, out qx, out qy, out qz);
            pose.InverseRotate(dAsm[0], dAsm[1], dAsm[2], out ex, out ey, out ez);

            object faces;
            try
            {
                dynamic doc = occ.OccurrenceDocument;
                object body = doc.Models.Item(1).Body;
                faces = body.GetType().InvokeMember("Faces", BindingFlags.GetProperty, null, body, new object[] { IgQueryCylinder });
            }
            catch { return result; }   // não é peça (subconjunto) ou sem corpo

            int n = 0;
            try { n = Convert.ToInt32(Get(faces, "Count")); } catch { }
            for (int i = 1; i <= n; i++)
            {
                object face;
                try { face = faces.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, faces, new object[] { i }); }
                catch { continue; }
                object geo = Get(face, "Geometry");
                if (geo == null) continue;
                var a = Call(geo, "GetCylinderData", new object[] { new double[3], new double[3], 0.0 }, 0, 1, 2);
                if (a == null) continue;
                var b = ToDoubles(a[0]); var u = Unit(ToDoubles(a[1]));
                double r = Convert.ToDouble(a[2]);
                if (b == null || u == null || r <= 0) continue;

                double cos = Math.Abs(u[0] * ex + u[1] * ey + u[2] * ez);
                // Distância do ponto do canal ao eixo da face (m).
                double vx = qx - b[0], vy = qy - b[1], vz = qz - b[2];
                double along = vx * u[0] + vy * u[1] + vz * u[2];
                double dx = vx - along * u[0], dy = vy - along * u[1], dz = vz - along * u[2];
                double offset = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                // Cobre o canal? A faixa axial da face sai da caixa dela projetada no eixo.
                bool covers = true;
                double[] lo, hi;
                if (FaceGeometry.TryGetRangeMm(face, out lo, out hi))
                {
                    double min = double.MaxValue, max = double.MinValue;
                    for (int k = 0; k < 8; k++)
                    {
                        double cx = ((k & 1) == 0 ? lo[0] : hi[0]) / 1000.0 - b[0];
                        double cy = ((k & 2) == 0 ? lo[1] : hi[1]) / 1000.0 - b[1];
                        double cz = ((k & 4) == 0 ? lo[2] : hi[2]) / 1000.0 - b[2];
                        double t = cx * u[0] + cy * u[1] + cz * u[2];
                        min = Math.Min(min, t); max = Math.Max(max, t);
                    }
                    covers = along >= min - 1e-5 && along <= max + 1e-5;
                }

                result.Add(new MatingCylinder
                {
                    Owner = occ.Name, DiameterMm = 2000.0 * r, AxisOffsetMm = offset * 1000.0, AxisCos = cos, CoversGroove = covers
                });
            }
            return result;
        }

        private static double[] Rotate(OccurrenceTransform pose, double[] v) => new[]
        {
            pose.R(0, 0) * v[0] + pose.R(0, 1) * v[1] + pose.R(0, 2) * v[2],
            pose.R(1, 0) * v[0] + pose.R(1, 1) * v[1] + pose.R(1, 2) * v[2],
            pose.R(2, 0) * v[0] + pose.R(2, 1) * v[1] + pose.R(2, 2) * v[2]
        };

        private static double[] Unit(double[] v)
        {
            if (v == null || v.Length < 3) return null;
            double n = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            return n < 1e-12 ? null : new[] { v[0] / n, v[1] / n, v[2] / n };
        }

        private static string DocPath(OccurrenceInfo o)
        {
            try { return Str(Get((object)o.OccurrenceDocument, "FullName")); } catch { return null; }
        }

        private static bool SamePath(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static object Get(object o, string prop)
        {
            if (o == null) return null;
            try { return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null); }
            catch { return null; }
        }

        private static object[] Call(object obj, string method, object[] args, params int[] outIdx)
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

        private static double[] ToDoubles(object o)
        {
            var a = o as Array;
            if (a == null || a.Length < 3) return null;
            var d = new double[a.Length];
            for (int i = 0; i < a.Length; i++) d[i] = Convert.ToDouble(a.GetValue(i));
            return d;
        }

        private static string Str(object o) => o == null ? null : Convert.ToString(o, CultureInfo.InvariantCulture);
    }
}
