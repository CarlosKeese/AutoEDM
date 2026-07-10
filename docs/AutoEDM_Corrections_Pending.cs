// =============================================================================
// AutoEDM — Correções pendentes sugeridas por Kimi (2026-07-10)
// =============================================================================
//
// Este arquivo NÃO compila. Ele é um documento de sugestões para o Claude
// retomar assim que estiver disponível. Cada região indica:
//   - qual arquivo modificar
//   - o problema encontrado
//   - o código atual (para localização)
//   - a sugestão de correção
//   - o porquê
//
// As sugestões baseiam-se na revisão de código em docs/CODE_REVIEW_Kimi_2026-07-10.md
// e nos logs consolidados docs/AutoEDM_Logs_Consolidated_Analysis.md.
// =============================================================================

namespace AutoEDM.Corrections
{
    public static class PendingCorrections
    {
        // -------------------------------------------------------------------------
        // 1. src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: Bug de unidade no posicionamento in-context.
        // AssemblyContext.TryGetOrigin já retorna METROS (GetTransform da COM usa metros),
        // mas CreateInContextPart divide novamente por 1000.0 antes de PutOrigin.
        // IMPACTO: eletrodo fica 1000x mais perto da origem do que deveria.
        //
        // CÓDIGO ATUAL (linha ~694):
        //     if (ctx.TryGetOrigin(target, out double ox, out double oy, out double oz))
        //         occurrence.PutOrigin(ox / 1000.0, oy / 1000.0, oz / 1000.0);
        //
        // SUGESTÃO:
        //     if (ctx.TryGetOrigin(target, out double ox, out double oy, out double oz))
        //         occurrence.PutOrigin(ox, oy, oz);   // <-- já está em metros
        //
        // POR QUÊ: PutOrigin espera metros; TryGetOrigin já devolve metros.
        //          A divisão foi induzida por um comentário incorreto em AssemblyContext
        //          (ver correção 6 abaixo).
        // -------------------------------------------------------------------------
        public static void ElectordeBuilder_CreateInContextPart_PutOrigin() { }

        // -------------------------------------------------------------------------
        // 2. src/AutoEDM.Core/Assembly/AssemblyContext.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: comentário enganoso de TryGetOrigin diz que retorna mm,
        // mas a implementação retorna metros (o que está correto).
        // IMPACTO: induziu o bug de unidade em ElectrodeBuilder.cs:694.
        //
        // CÓDIGO ATUAL (linhas ~80-84):
        //     /// <summary>
        //     /// Best-effort read of an occurrence origin (mm) relative to the assembly.
        //     ...
        //     /// </summary>
        //
        // SUGESTÃO:
        //     /// <summary>
        //     /// Best-effort read of an occurrence origin (METROS) relative to the assembly.
        //     /// GetTransform devolve metros na API COM do Solid Edge.
        //     ...
        //     /// </summary>
        //
        // BÔNUS (robustez): TryGetOrigin e TryGetPlacement usam InvokeMember sem
        // ParameterModifier para parâmetros [out]. Funcionou nos logs, mas é frágil.
        // Sugestão: marcar os 6 argumentos como by-ref via ParameterModifier(6),
        // igual já feito em ElectrodeBuilder.ReadGlobal.
        //
        // POR QUÊ: padronizar unidades evita regressão; ParameterModifier deixa o
        //          marshaling explícito e menos dependente do binder dinâmico.
        // -------------------------------------------------------------------------
        public static void AssemblyContext_TryGetOrigin_DocumentationAndRobustness() { }

        // -------------------------------------------------------------------------
        // 3. src/AutoEDM.Core/Electrode/BlankModeler.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: AddParallelByDistance recebe 7 argumentos, mas a assinatura real
        // tem 6: (ParentPlane, Distance, NormalSide, [Pivot], [pivotorigin], [Local]).
        // IMPACTO: provavelmente DISP_E_BADPARAMCOUNT ou DISP_E_TYPEMISMATCH.
        //
        // CÓDIGO ATUAL (linhas ~102-104):
        //     topPlane = partDoc.RefPlanes.AddParallelByDistance(
        //         partDoc.RefPlanes.Item(1), holderHmm / 1000.0, 2,
        //         Type.Missing, Type.Missing, Type.Missing, Type.Missing);
        //
        // SUGESTÃO:
        //     topPlane = partDoc.RefPlanes.AddParallelByDistance(
        //         partDoc.RefPlanes.Item(1), holderHmm / 1000.0, 2,
        //         Type.Missing, Type.Missing, Type.Missing);
        //
        // POR QUÊ: só 3 parâmetros são obrigatórios; os 3 opcionais bastam.
        // -------------------------------------------------------------------------
        public static void BlankModeler_AddParallelByDistance_Arity() { }

        // -------------------------------------------------------------------------
        // 4. src/AutoEDM.Core/Electrode/BlankModeler.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: apagar o plano de referência usado pelos furos pode invalidar
        // as features de furo no modo ordenado.
        //
        // CÓDIGO ATUAL (linha ~126):
        //     try { topPlane.Delete(); } catch { }
        //
        // SUGESTÃO:
        //     - Confirmar no SE real se os furos sobrevivem à deleção do plano.
        //     - Se não sobreviverem: manter o plano oculto/suprimido em vez de deletar.
        //     - Se sobreviverem (síncrono): adicionar log de confirmação.
        //
        // POR QUÊ: em modelagem ordenada, features dependem do sketch/plano pai.
        // -------------------------------------------------------------------------
        public static void BlankModeler_TopPlaneDelete_Safety() { }

        // -------------------------------------------------------------------------
        // 5. src/AutoEDM.Core/Electrode/ElectrodeBuilder.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: CreateBlankAndHolder usa AddBoxByTwoPoints, primitivo direto que
        // falha recorrentemente com DISP_E_TYPEMISMATCH no late binding.
        //
        // CÓDIGO ATUAL (linhas ~890-906):
        //     ModelingHelpers.AddBoxByTwoPoints(models, x0, y0, z0, x1, y1, z1,
        //         blankHeight, plane, extentSide: 1);
        //     ...
        //     ModelingHelpers.AddBoxByTwoPoints(models, hx0, hy0, hz0, hx1, hy1, hz1,
        //         holderHeight, plane, extentSide: 1);
        //
        // SUGESTÃO: usar BlankModeler.CreateBox (sketch + extrusão), que está validado.
        //   - O blank pode ser criado chamando BlankModeler.CreateBox(...).
        //   - Para o holder, criar um segundo bloco abaixo ou parametrizar CreateBox
        //     para aceitar posição Z inicial.
        //
        // POR QUÊ: sketch + extrusão foi validado nos logs 033-035 e 045.
        //          AddBoxByTwoPoints é instável no late binding (logs 008-014).
        // -------------------------------------------------------------------------
        public static void ElectrodeBuilder_CreateBlankAndHolder_UseSketchExtrude() { }

        // -------------------------------------------------------------------------
        // 6. src/AutoEDM.Core/Electrode/ModelingHelpers.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: AddFaceOffset e AddOffsetSurface recebem object[] onde a API COM
        // espera um FaceSet/IDispatch.
        //
        // CÓDIGO ATUAL (linhas ~46-64 e ~69-82):
        //     object[] args = { faces, 0, 0, offsetMeters, Type.Missing, ... };
        //     return InvokeMethod(constructions.FaceOffsets, "Add", args);
        //
        //     object[] args = { 0, offsetMeters, faces, Type.Missing };
        //     return InvokeMethod(constructions.OffsetSurfaces, "Add", args);
        //
        // SUGESTÃO:
        //   - Descobrir como criar um FaceSet COM no Solid Edge (provavelmente via
        //     SelectSet ou coleção específica de faces).
        //   - Ou usar AddEx se existir, que aceite SAFEARRAY(IDispatch)* de faces.
        //   - Converter object[] faces para Face[] tipado (SAFEARRAY(IDispatch)).
        //
        // POR QUÊ: object[] marshala como SAFEARRAY(VARIANT), rejeitado onde a COM
        //          espera SAFEARRAY(IDispatch) ou um único IDispatch (FaceSet).
        // -------------------------------------------------------------------------
        public static void ModelingHelpers_FaceOffsetAndOffsetSurface_FaceSet() { }

        // -------------------------------------------------------------------------
        // 7. src/AutoEDM.Core/Electrode/ModelingHelpers.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: GetFeatureFaces chama feature.Faces(0), mas 0 não é um valor
        // válido de FeatureTopologyQueryTypeConstants. igQueryAll = 1.
        //
        // CÓDIGO ATUAL (linha ~138):
        //     dynamic faces = feature.Faces(0); // 0 = all faces?
        //
        // SUGESTÃO:
        //     dynamic faces = feature.Faces[1]; // 1 = igQueryAll
        //
        // POR QUÊ: Face.Faces(queryType) espera queryType=1 (igQueryAll). Com 0,
        //          pode retornar coleção vazia ou falhar.
        // -------------------------------------------------------------------------
        public static void ModelingHelpers_GetFeatureFaces_QueryType() { }

        // -------------------------------------------------------------------------
        // 8. src/AutoEDM.Core/Selection/FaceStyleColorReader.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: GetDiffuse tem parâmetros [out], mas o código não usa
        // ParameterModifier by-ref nem CultureInfo.InvariantCulture.
        //
        // CÓDIGO ATUAL (linhas ~109-120):
        //     object[] args = { 0.0, 0.0, 0.0 };
        //     styleObj.InvokeMember("GetDiffuse", ..., args);
        //
        // SUGESTÃO:
        //     object[] args = { 0.0, 0.0, 0.0 };
        //     var mod = new ParameterModifier(3);
        //     mod[0] = mod[1] = mod[2] = true;
        //     styleObj.GetType().InvokeMember("GetDiffuse",
        //         BindingFlags.InvokeMethod, null, styleObj, args,
        //         new[] { mod }, CultureInfo.InvariantCulture, null);
        //
        // POR QUÊ: sem ParameterModifier, os valores [out] podem não ser populados
        //          em late binding (mesmo padrão que Face.GetRange).
        // -------------------------------------------------------------------------
        public static void FaceStyleColorReader_GetDiffuse_OutParams() { }

        // -------------------------------------------------------------------------
        // 9. src/AutoEDM.Core/Electrode/InterPartCopier.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: Inter-Part Copy via CopySurfaces.Add está bloqueado porque
        // Occurrence.Activate=true não entra em edição in-place.
        //
        // SUGESTÃO DE PRÓXIMA HIPÓTESE:
        //   - Usar Face.GetReferenceKey(out key) na face da cavidade.
        //   - Criar TopologyReference via Occurrence.CreateTopologyReference(key).
        //   - Tentar usar esse TopologyReference como AsmSource em
        //     InterpartConstructions.Add/Add2 ou como substituto direto.
        //
        // CÓDIGO EXEMPLO (a validar no SE real):
        //     object[] keyArgs = { new byte[0] };
        //     var keyMod = new ParameterModifier(1); keyMod[0] = true;
        //     face.GetType().InvokeMember("GetReferenceKey",
        //         BindingFlags.InvokeMethod, null, face, keyArgs,
        //         new[] { keyMod }, CultureInfo.InvariantCulture, null);
        //     byte[] key = (byte[])keyArgs[0];
        //     dynamic topoRef = occurrence.CreateTopologyReference(key);
        //
        // POR QUÊ: CreateTopologyReference é a API dedicada de referência de
        //          topologia in-context e não depende de edição in-place real.
        // -------------------------------------------------------------------------
        public static void InterPartCopier_TopologyReference_Fallback() { }

        // -------------------------------------------------------------------------
        // 10. src/AutoEDM.Core/Assembly/EditInPlaceScope.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: a classe foi construída sobre a premissa falsa de que
        // Occurrence.Activate=true entra em edição in-place.
        //
        // SUGESTÃO:
        //   - Atualizar o comentário/documentação para deixar claro que Activate
        //     é apenas flag de ativação da ocorrência.
        //   - Adicionar leitura de AssemblyDocument.ModelingInAssembly e
        //     InPlaceActivated para diagnóstico.
        //   - Considerar deprecar/marcar EditInPlaceScope como não operacional
        //     para Inter-Part Copy até encontrar o gesto correto.
        //
        // POR QUÊ: evita que futuros desenvolvedores repitam a mesma suposição.
        // -------------------------------------------------------------------------
        public static void EditInPlaceScope_DocumentReality() { }

        // -------------------------------------------------------------------------
        // 11. src/AutoEDM.Core/Electrode/ModelingHelpers.cs
        // -------------------------------------------------------------------------
        // PROBLEMA: InvokeMethod desencapsula TargetInvocationException, perdendo
        // o stack trace original.
        //
        // CÓDIGO ATUAL (linhas ~154-169):
        //     catch (TargetInvocationException tie) when (tie.InnerException != null)
        //     {
        //         throw tie.InnerException;
        //     }
        //
        // SUGESTÃO:
        //     catch (TargetInvocationException tie) when (tie.InnerException != null)
        //     {
        //         throw new InvalidOperationException(
        //             $"Falha ao chamar {methodName}: {tie.InnerException.Message}", tie);
        //     }
        //
        // POR QUÊ: preservar a exceção externa facilita diagnóstico de erros COM.
        // -------------------------------------------------------------------------
        public static void ModelingHelpers_InvokeMethod_PreserveStackTrace() { }

        // -------------------------------------------------------------------------
        // 12. src/AutoEDM.Core/AutoEDM.Core.csproj
        // -------------------------------------------------------------------------
        // PROBLEMA: pacote Interop.SolidEdge versão 219.0.0 refere-se ao SE 2021 (ST11),
        // mas o projeto roda no SE 2023 (223.00.13.05).
        //
        // SUGESTÃO:
        //   - Monitorar casts para SolidEdgeGeometry.Face / SolidEdgePart.Profile.
        //   - Se InvalidCastException aparecer, atualizar para o pacote da versão 223
        //     ou gerar interop localmente a partir da TLB do SE 2023.
        //
        // POR QUÊ: GUIDs/tipos podem divergir entre versões do Solid Edge.
        // -------------------------------------------------------------------------
        public static void Csproj_InteropVersion() { }

        // -------------------------------------------------------------------------
        // 13. src/AutoEDM.Core/Electrode/BlankModeler.cs / FixationPattern.cs
        // -------------------------------------------------------------------------
        // CONTEXTO: Logs 046-047 mostraram que igTappedHole=37 falha no síncrono e
        // corrompe o proxy COM (0x80010114). O fallback para furo simples Ø5
        // (broca de rosca) funciona (Log 045).
        //
        // SUGESTÃO:
        //   - Manter M6 como furo simples Ø5 em produção.
        //   - Deixar a renderização da rosca para operação manual ou para futura
        //     validação de Model.Threads.Add (assinatura descoberta no Log 047).
        //   - Em BlankBoxProbe, manter o experimento com igTappedHole isolado.
        //
        // POR QUÊ: furos simples não corrompem o proxy e já entregam geometria usinável.
        // -------------------------------------------------------------------------
        public static void BlankModeler_TappedHole_Strategy() { }
    }
}
