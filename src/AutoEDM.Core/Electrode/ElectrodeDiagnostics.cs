using System;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Diagnóstico do caso "nenhuma queima detectada" — extraído de <see cref="ElectrodeBuilder"/>
    /// (revisão 2026-07-23, docs/REVISAO-AutoEDM.md P2.1): ~150 linhas puramente de depuração,
    /// sem acoplamento com o fluxo principal de criação de eletrodo.
    /// </summary>
    internal static class ElectrodeDiagnostics
    {
        /// <summary>
        /// Diagnóstico do caso "nenhuma queima encontrada": dumpa a estrutura da
        /// montagem (ocorrências, corpos, faces) e, na 1ª face da 1ª peça, a cor por
        /// VÁRIAS fontes — face.Style (peça), Face.GetRGBAVals e Occurrence.GetFaceStyle2
        /// (estilo no nível da ocorrência, comum quando a cor é pintada no contexto da
        /// montagem, não na peça). Revela como a montagem codifica a cor de queima.
        /// </summary>
        public static void DiagnoseNoBurn(AssemblyContext ctx)
        {
            Log.Warn("Nenhuma face de queima detectada — DIAGNÓSTICO da montagem:");
            int occN = 0;
            bool faceDumped = false;
            foreach (var occ in ctx.GetOccurrences())
            {
                occN++;
                dynamic doc = null; string docType = "?";
                try { doc = occ.OccurrenceDocument; docType = Convert.ToString((int)doc.Type); } catch { }

                int bodies = 0, faces = 0;
                dynamic firstBody = null;
                try
                {
                    dynamic models = doc.Models;
                    int mc = (int)models.Count;
                    for (int i = 1; i <= mc; i++)
                    {
                        try
                        {
                            dynamic body = models.Item(i).Body;
                            if (body == null) continue;
                            bodies++;
                            if (firstBody == null) firstBody = body;
                            faces += (int)body.Faces[1].Count;
                        }
                        catch { }
                    }
                }
                catch { }

                Log.Info($"  Occ '{occ.Name}': doc Type={docType} (1=peça,4=submontagem), {bodies} corpo(s), {faces} face(s).");

                if (!faceDumped && faces > 0 && firstBody != null)
                {
                    faceDumped = true;
                    object firstFace = null;
                    try { firstFace = (object)firstBody.Faces[1].Item(1); } catch { }
                    if (firstFace != null)
                    {
                        try { DumpFaceColorSources(firstFace, occ.ComOccurrence); }
                        catch (Exception e) { Log.Warn("  [DIAG] cor da 1ª face falhou: " + e.GetBaseException().Message); }
                        try { DumpFeatureInfo(doc, firstFace); }
                        catch (Exception e) { Log.Warn("  [DIAG] features falhou: " + e.GetBaseException().Message); }
                    }
                }
            }
            Log.Info($"  Total: {occN} ocorrência(s). Se a cor de queima EXISTE mas não foi lida, " +
                     "veja as fontes acima — ajusto o leitor p/ a fonte certa.");
        }

        private static void DumpFaceColorSources(object face, dynamic comOcc)
        {
            Log.Info("  [DIAG] Cor da 1ª face por FONTE:");

            object style = null;
            try { style = ((dynamic)face).Style; } catch { }
            Log.Info("    face.Style (peça) = " + (style == null ? "null (sem estilo por-face na peça)" : "PRESENTE"));

            // Face.GetRGBAVals — fonte alternativa direta na face.
            try { ComDiagnostics.LogSignatures(face, "GetRGBAVals"); } catch { }
            try
            {
                object[] a = { 0.0, 0.0, 0.0, 0.0 };
                var mod = new System.Reflection.ParameterModifier(4);
                mod[0] = mod[1] = mod[2] = mod[3] = true; // [out] by-ref, senão volta 0
                face.GetType().InvokeMember("GetRGBAVals", System.Reflection.BindingFlags.InvokeMethod,
                    null, face, a, new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                Log.Info($"    Face.GetRGBAVals -> R={a[0]} G={a[1]} B={a[2]} A={a[3]} (×255 ≈ {(int)(Convert.ToDouble(a[0])*255)},{(int)(Convert.ToDouble(a[1])*255)},{(int)(Convert.ToDouble(a[2])*255)})");
            }
            catch (Exception e) { Log.Info("    Face.GetRGBAVals falhou: " + e.GetBaseException().Message); }

            // Occurrence.GetFaceStyle2 — estilo no nível da ocorrência (cor pintada no contexto).
            if (comOcc != null)
            {
                try { ComDiagnostics.LogSignatures((object)comOcc, "GetFaceStyle2"); } catch { }
                try
                {
                    dynamic st = comOcc.GetFaceStyle2(face);
                    Log.Info("    Occurrence.GetFaceStyle2(face) = " + (st == null ? "null" : "PRESENTE (cor no nível da ocorrência!)"));
                }
                catch (Exception e) { Log.Info("    Occurrence.GetFaceStyle2 falhou: " + e.GetBaseException().Message); }
            }
        }

        /// <summary>
        /// Introspecção da cor pintada por FEATURE: GetRGBAVals dá a cor do CORPO, não a
        /// pintura de feature (camada de exibição). Aqui dumpamos a estrutura para achar
        /// como ler a cor da feature: FeatureIDsAndNames da face, os membros de Model[1]
        /// (achar a coleção de features) e os membros da 1ª feature (achar Style/cor).
        /// </summary>
        private static void DumpFeatureInfo(dynamic partDoc, object firstFace)
        {
            Log.Info("  [DIAG] FEATURES (cor pintada por feature — GetRGBAVals dá a cor do corpo, não a da feature):");

            // Como a face aponta p/ suas features.
            try { ComDiagnostics.LogSignatures(firstFace, "FeatureIDsAndNames"); } catch { }
            try
            {
                object[] a = { null, null };
                var mod = new System.Reflection.ParameterModifier(2); mod[0] = mod[1] = true;
                firstFace.GetType().InvokeMember("FeatureIDsAndNames", System.Reflection.BindingFlags.InvokeMethod,
                    null, firstFace, a, new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                Log.Info($"    Face.FeatureIDsAndNames -> [0]={Fmt(a[0])} [1]={Fmt(a[1])}");
            }
            catch (Exception e) { Log.Info("    Face.FeatureIDsAndNames falhou: " + e.GetBaseException().Message); }

            dynamic model = null;
            try { model = partDoc.Models.Item(1); } catch { }
            if (model == null) { Log.Info("    partDoc.Models.Item(1) indisponível."); return; }

            // Dump dos membros do Model p/ achar a coleção de features e cor.
            try { ComDiagnostics.LogMembers("Model[1]", (object)model); } catch { }

            // Tenta a coleção Features e o Style/cor da 1ª feature.
            try
            {
                dynamic feats = model.Features;
                int fc = (int)feats.Count;
                Log.Info($"    Model[1].Features = {fc} feature(s).");
                if (fc > 0) ComDiagnostics.LogMembers("Feature[1]", (object)feats.Item(1));
            }
            catch (Exception e) { Log.Info("    Model[1].Features indisponível: " + e.GetBaseException().Message); }
        }

        private static string Fmt(object o)
        {
            if (o == null) return "null";
            if (o is Array arr)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var x in arr) parts.Add(Convert.ToString(x));
                return "[" + string.Join(",", parts) + "]";
            }
            return Convert.ToString(o);
        }
    }
}
