using System;
using AutoEDM.Assembly;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Experiments
{
    /// <summary>
    /// Testes de modelagem automatizada, exploratórios e SEGUROS.
    ///
    /// Fase atual: descobrir as ASSINATURAS exatas dos métodos de modelagem
    /// (AddBoxByTwoPoints, CopySurfaces, AddCylinder..., OffsetSurfaces) via
    /// introspecção, e executar o primeiro DESENHO real — um box — para provar a
    /// cadeia "dirigir modelagem". Nada é salvo; a peça de teste fica aberta.
    ///
    /// A API de geometria do Solid Edge trabalha em METROS.
    /// </summary>
    public sealed class ModelingProbe
    {
        public void Run(SolidEdgeConnector connector, dynamic assemblyDocument)
        {
            Log.Info("===== TESTE DE MODELAGEM (assinaturas + desenho de box) =====");

            var ctx = new AssemblyContext(assemblyDocument);
            var occs = ctx.GetOccurrences();
            Log.Info($"{occs.Count} ocorrência(s) na montagem.");

            try
            {
                Log.Info("Criando peça de teste a partir do template padrão...");
                dynamic newPart = connector.CreateDocumentFromTemplate(null);
                Log.Info("Peça de teste criada (não será salva).");

                dynamic models = TryGetObj(() => newPart.Models);
                dynamic constr = TryGetObj(() => newPart.Constructions);

                if (models != null)
                {
                    Log.Info("--- assinaturas: modelagem (Models) ---");
                    ComDiagnostics.LogSignatures((object)models,
                        "AddBoxByTwoPoints", "AddBoxByCenter", "AddBoxByThreePoints",
                        "AddCylinderByCenterAndRadius", "AddCopiedPart", "AddCopiedPartEx",
                        "AddExtrudedProtrusion", "AddFiniteExtrudedProtrusion");
                }
                if (constr != null)
                {
                    Log.Info("--- assinaturas: superfícies/cópia (Constructions) ---");
                    ComDiagnostics.LogSignatures((object)constr,
                        "CopySurfaces", "CopyConstructions", "OffsetSurfaces", "StitchSurfaces");
                }

                if (models != null) TryDrawTestBox(newPart, models);

                DumpSdk(connector, assemblyDocument, newPart, occs);
            }
            catch (Exception ex)
            {
                Log.Error("Falha na peça de teste.", ex);
            }

            Log.Info("===== FIM DO TESTE DE MODELAGEM =====");
        }

        /// <summary>
        /// Dumpa as type libraries do Solid Edge (Framework/Assembly/Part/Geometry)
        /// para um .txt — o "SDK offline". Semeia com um objeto de cada módulo; a
        /// geometria (Body/Face/Edge, onde mora o range de face) vem do corpo do box
        /// recém-criado, com a montagem como fallback.
        /// </summary>
        private static void DumpSdk(SolidEdgeConnector connector, dynamic assemblyDocument,
            dynamic newPart, System.Collections.Generic.IReadOnlyList<OccurrenceInfo> occs)
        {
            var seeds = new System.Collections.Generic.List<object>();
            void Add(Func<object> get) { try { var o = get(); if (o != null) seeds.Add(o); } catch { } }

            Add(() => connector.Application);          // SolidEdgeFramework
            Add(() => assemblyDocument);               // SolidEdgeAssembly
            Add(() => newPart);                        // SolidEdgePart
            Add(() => newPart.Models);
            Add(() => newPart.Constructions);
            Add(() => newPart.RefPlanes);

            // Geometria: corpo/face/aresta do box (se saiu).
            Add(() => newPart.Models.Item(1).Body);
            Add(() => newPart.Models.Item(1).Body.Faces[1].Item(1));
            Add(() => newPart.Models.Item(1).Body.Edges[1].Item(1));

            // Fallback de geometria: uma face da 1ª ocorrência da montagem.
            Add(() => FirstAssemblyFace(occs));

            string ver = TryGetObj(() => (object)connector.Application.Version) as string ?? "unknown";
            string path = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "logs", $"SE_API_dump_{ver}.txt");

            Log.Info($"Dumpando type libraries do Solid Edge ({seeds.Count} seed(s))...");
            ComDiagnostics.DumpTypeLibraries(path, seeds);
        }

        private static object FirstAssemblyFace(
            System.Collections.Generic.IReadOnlyList<OccurrenceInfo> occs)
        {
            foreach (var occ in occs)
            {
                object face = TryGetObj(() => occ.OccurrenceDocument.Models.Item(1).Body.Faces[1].Item(1));
                if (face != null) return face;
            }
            return null;
        }

        /// <summary>Tenta desenhar um cubo de 20 mm para validar a API de criação de sólido.</summary>
        private static void TryDrawTestBox(dynamic newPart, dynamic models)
        {
            const double s = 0.020; // 20 mm em metros (API de geometria = SI)

            // Assinatura real (Log 8): AddBoxByTwoPoints(x1, y1, z1, x2, y2, z2,
            //   dAngle, dDepth, pPlane, ExtentSide, vbKeyPointExtent,
            //   pKeyPointObj, pKeyPointFlags) -> retângulo em pPlane extrudado dDepth.
            dynamic plane = TryGetObj(() => newPart.RefPlanes.Item(1));
            if (plane == null)
            {
                Log.Warn("Sem RefPlane base para o box (RefPlanes.Item(1) falhou).");
                return;
            }

            // ExtentSide=1 (igLeft) confirmado no dump. O binder dinâmico dava
            // "não foi possível converter argumento 1"; via InvokeMember o IDispatch
            // faz a coerção de VARIANT e costuma aceitar. pKeyPointObj = Type.Missing
            // (opcional) já que vbKeyPointExtent=false.
            object[] args =
            {
                0.0, 0.0, 0.0,      // canto 1 (x,y,z) em metros
                s, s, 0.0,          // canto 2 — retângulo no plano
                0.0,                // dAngle
                s,                  // dDepth (extrusão)
                plane,              // pPlane
                1,                  // ExtentSide (igLeft)
                false,              // vbKeyPointExtent
                null,               // pKeyPointObj (obrigatório mas nulo; Type.Missing deu PARAMNOTOPTIONAL)
                0                   // pKeyPointFlags
            };
            try
            {
                Log.Info("Desenhando box 20×20×20 mm via AddBoxByTwoPoints (InvokeMember, 13 args)...");
                ((object)models).GetType().InvokeMember(
                    "AddBoxByTwoPoints", System.Reflection.BindingFlags.InvokeMethod,
                    null, (object)models, args);
                Log.Info("✓ Box criado (AddBoxByTwoPoints).");
            }
            catch (Exception ex)
            {
                Log.Warn($"AddBoxByTwoPoints falhou: {ex.GetBaseException().Message}. " +
                         "Box é secundário; foco no range/Inter-Part Copy.");
            }
        }

        private static object TryGetObj(Func<object> getter)
        {
            try { return getter(); } catch { return null; }
        }
    }
}
