using System;
using System.Collections.Generic;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Copia as faces de queima da peça-mãe (cavidade) para o eletrodo, dentro da
    /// montagem, via Inter-Part Copy associativo. DEVE rodar com o eletrodo ATIVO
    /// para edição in-place (ver <see cref="EditInPlaceScope"/>).
    ///
    /// Assinatura real (typelib, Log 17):
    ///   CopySurfaces.Add(NumberOfFaces: int, FaceArray: SAFEARRAY(IDispatch)*,
    ///     [opt] InternalBoundary: VARIANT, [opt] ExternalBoundary: VARIANT) -> CopySurface*
    ///
    /// O FaceArray precisa ser SAFEARRAY(IDispatch): um object[] marshala como
    /// SAFEARRAY(VARIANT) e o SE rejeita (DISP_E_TYPEMISMATCH). Por isso montamos um
    /// array TIPADO de SolidEdgeGeometry.Face, que marshala como SAFEARRAY(IDispatch).
    /// </summary>
    public sealed class InterPartCopier
    {
        public IReadOnlyList<dynamic> CopyBurnFaces(
            dynamic assemblyDocument,
            OccurrenceInfo sourcePartOcc,
            IReadOnlyList<SelectedFace> sourceFaces,
            dynamic targetElectrodeDocument)
        {
            if (sourcePartOcc == null) throw new ArgumentNullException(nameof(sourcePartOcc));
            if (sourceFaces == null || sourceFaces.Count == 0)
                throw new ArgumentException("Nenhuma face de origem para copiar.", nameof(sourceFaces));

            Log.Info($"Inter-Part Copy de {sourceFaces.Count} face(s) de " +
                     $"'{sourcePartOcc.Name}' para o eletrodo.");

            // Diagnóstico decisivo (memória SE-COM §3): CopySurfaces.Add ACEITA Face[] mas
            // dá E_FAIL. A hipótese é que Occurrence.Activate=true NÃO entra em edição
            // in-place (só carrega a ocorrência). ActiveDocument=asm NÃO prova isso — os
            // sinais autoritativos são AssemblyDocument.ModelingInAssembly/InPlaceActivated.
            try { LogInPlaceState(assemblyDocument); } catch { }

            // Face[] TIPADO -> SAFEARRAY(IDispatch), como CopySurfaces.Add exige.
            var faces = new SolidEdgeGeometry.Face[sourceFaces.Count];
            for (int i = 0; i < sourceFaces.Count; i++)
                faces[i] = (SolidEdgeGeometry.Face)sourceFaces[i].ComFace;

            dynamic copySurfaces = targetElectrodeDocument.Constructions.CopySurfaces;
            object ipc = null;
            try { ipc = targetElectrodeDocument.Constructions.InterpartConstructions; } catch { }

            // Última bateria (Log 27): as variantes ainda plausíveis, cada uma logada.
            // 1) CopySurfaces.Add com UMA face só (isola nº de faces vs. contexto).
            var oneFace = new SolidEdgeGeometry.Face[] { faces[0] };
            if (TryCom(copySurfaces, "Add", new object[] { 1, oneFace, Type.Missing, Type.Missing },
                    "CopySurfaces.Add(1 face)", out dynamic r1))
                return new[] { r1 };

            // 2) CopySurfaces.Add com todas as faces.
            if (TryCom(copySurfaces, "Add", new object[] { faces.Length, faces, Type.Missing, Type.Missing },
                    "CopySurfaces.Add(todas as faces)", out dynamic r2))
                return new[] { r2 };

            // 3) InterpartConstructions com Face[] como AsmSource (hipótese Kimi: AsmSource
            //    seria a própria seleção de faces).
            if (ipc != null)
            {
                if (TryCom(ipc, "Add2", new object[] { (object)targetElectrodeDocument, faces },
                        "InterpartConstructions.Add2(eletrodo, Face[])", out dynamic r3))
                    return new[] { r3 };
                if (TryCom(ipc, "Add", new object[] { faces },
                        "InterpartConstructions.Add(Face[])", out dynamic r4))
                    return new[] { r4 };
            }

            throw new InvalidOperationException(
                "Inter-Part Copy não resolvida via COM (todas as variantes falharam — ver log). " +
                "Alternativa pendente: comando nativo via SelectSet + StartCommand.");
        }

        /// <summary>
        /// Diagnóstico do contexto in-place. Loga o documento ativo (contexto) E os sinais
        /// AUTORITATIVOS de edição in-place da montagem — ModelingInAssembly e
        /// InPlaceActivated. Se ModelingInAssembly=False durante a cópia, o
        /// <see cref="EditInPlaceScope"/> (Occurrence.Activate=true) NÃO entrou em edição
        /// in-place, e esse é o motivo mais provável do E_FAIL do CopySurfaces.Add.
        /// </summary>
        private static void LogInPlaceState(dynamic assemblyDocument)
        {
            // Documento ativo (proxy fraco — em in-place a janela hospedeira continua sendo o .asm).
            try
            {
                dynamic app = assemblyDocument.Application;
                dynamic doc = app.ActiveDocument;
                string name = "?"; try { name = doc.Name; } catch { }
                string type = "?"; try { type = Convert.ToString(app.ActiveDocumentType); } catch { }
                Log.Info($"[DIAG] Documento ativo na cópia: '{name}' (ActiveDocumentType={type}).");
            }
            catch (Exception e) { Log.Warn($"[DIAG] ActiveDocument indisponível: {e.GetBaseException().Message}"); }

            // Sinais autoritativos (dump: AssemblyDocument.ModelingInAssembly / InPlaceActivated).
            string modeling = ReadBoolFlag(assemblyDocument, "ModelingInAssembly");
            string inPlace = ReadBoolFlag(assemblyDocument, "InPlaceActivated");
            Log.Info($"[DIAG] Edição in-place REAL? ModelingInAssembly={modeling}, InPlaceActivated={inPlace}. " +
                     "(Se ModelingInAssembly=False, o Activate=true NÃO entrou em edição in-place.)");
        }

        /// <summary>Lê uma propriedade booleana COM e devolve "True"/"False" ou "erro: ...".</summary>
        private static string ReadBoolFlag(dynamic comObj, string prop)
        {
            try
            {
                object val = ((object)comObj).GetType().InvokeMember(
                    prop, BindingFlags.GetProperty, null, comObj, null);
                return Convert.ToString(val);
            }
            catch (Exception e) { return "erro: " + e.GetBaseException().Message; }
        }

        /// <summary>Chama um método COM via InvokeMember (marshaling robusto) e loga.</summary>
        private static bool TryCom(object comObj, string method, object[] args, string label, out dynamic result)
        {
            result = null;
            try
            {
                Log.Info($"Fallback: {label}...");
                result = comObj.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, comObj, args);
                Log.Info($"{label} OK — cópia inter-part criada.");
                return true;
            }
            catch (Exception e) { Log.Warn($"{label} falhou: {e.GetBaseException().Message}"); return false; }
        }
    }
}
