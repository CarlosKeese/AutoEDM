using System;
using System.Collections.Generic;
using System.IO;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;

namespace AutoEDM.Experiments
{
    /// <summary>
    /// Copy-test SEGURO: cria UM eletrodo em contexto e copia as faces de queima do
    /// PRIMEIRO detalhe via Inter-Part Copy (CopySurfaces.Add), sem offset/blank/save
    /// final. Objetivo: validar no SE 2023 real se a cópia leva as faces da cavidade
    /// para a peça nova (o maior desbloqueio restante da geometria).
    ///
    /// Não modifica a cavidade. Cria uma peça .par nova em %LOCALAPPDATA%\AutoEDM\
    /// electrodes e a adiciona à montagem — o usuário pode desfazer/apagar.
    /// </summary>
    public sealed class InterPartCopyProbe
    {
        public void Run(SolidEdgeConnector connector, dynamic assemblyDocument)
        {
            Log.Info("===== COPY-TEST (Inter-Part Copy de 1 detalhe, peça in-place) =====");
            OccurrenceInfo target = null;
            try
            {
                var p = new ElectrodeParams
                {
                    ElectrodeName = "ELD_TEST",
                    Material = "Grafite",
                    OutputFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AutoEDM", "electrodes")
                };
                Directory.CreateDirectory(p.OutputFolder);

                var builder = new ElectrodeBuilder(connector);
                if (!builder.TryResolveFirstDetail(assemblyDocument, p,
                        out target, out IReadOnlyList<SelectedFace> faces))
                {
                    Log.Warn("Copy-test abortado: nenhum detalhe de queima resolvido.");
                    return;
                }

                // Cria a peça EM CONTEXTO (Occurrences.AddByTemplate) e copia as faces
                // do 1º detalhe via Inter-Part Copy. Sem offset/blank/save: só valida se
                // a cópia inter-part enfim passa com a peça in-place (antes: E_FAIL por
                // a peça ser standalone — Log 18).
                dynamic electrodeDoc = builder.CreateInContextPart(assemblyDocument, target, faces, p);
                Log.Info("Copy-test OK: eletrodo in-place criado e Inter-Part Copy executado. " +
                         "Confira a CopySurface no eletrodo; desfaça/apague a peça ELD_TEST depois.");
            }
            catch (Exception ex)
            {
                Log.Error("Copy-test falhou.", ex);
                // Se falhar, re-loga a API in-place p/ diagnosticar a próxima tentativa.
                if (target != null)
                {
                    try { DiscoverInPlaceCreationApi(assemblyDocument, target); }
                    catch (Exception e2) { Log.Warn("[DIAG] descoberta falhou: " + e2.Message); }
                }
            }
            Log.Info("===== FIM (COPY-TEST) =====");
        }

        /// <summary>
        /// Loga os membros de AssemblyDocument / Occurrences / Occurrence para achar o
        /// método de criação de peça EM CONTEXTO (in-place) — a única forma em que o
        /// inter-part copy funciona.
        /// </summary>
        private static void DiscoverInPlaceCreationApi(dynamic asmDoc, OccurrenceInfo target)
        {
            Log.Info("===== DESCOBERTA: criar peça EM CONTEXTO (in-place) =====");
            Dump("AssemblyDocument", () => (object)asmDoc);
            Dump("Occurrences", () => (object)asmDoc.Occurrences);
            Dump("Occurrence(alvo)", () => (object)target.ComOccurrence);
            Dump("Application", () => (object)asmDoc.Application);
        }

        private static void Dump(string label, Func<object> get)
        {
            try { Com.ComDiagnostics.LogMembers(label, get()); }
            catch (Exception e) { Log.Warn($"[DIAG] {label}: {e.GetBaseException().Message}"); }
        }
    }
}
