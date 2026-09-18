using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

namespace AutoEDM.Reverse
{
    public sealed class MeshProbeResult
    {
        /// <summary>Texto curto para a caixa de diálogo. O detalhe todo vai para o log.</summary>
        public string Summary { get; set; }

        /// <summary>true = achou pelo menos um corpo de facetas / face de malha na peça.</summary>
        public bool FoundMesh { get; set; }
    }

    /// <summary>
    /// SONDA DE ENGENHARIA REVERSA, rodada 1. SÓ LEITURA por padrão: não cria feature, não
    /// altera a peça, não salva.
    ///
    /// POR QUE ELA EXISTE. O dump da typelib (docs/api) responde o que a Solid Edge expõe por
    /// COM, e o resultado é assimétrico:
    ///
    ///   EXISTE  — MeshSurface.GetTriangleData/GetTrianglePoints/GetTriangleNormals,
    ///             Body.GetFacetData, Model.IsFacetBody/IsMixedFacetBody,
    ///             Model.HealAndOptimizeWithMeshOptions(..., bFillHoles, FillHoleType),
    ///             Models.AddBodyByMeshFacets, DoRemesh, ConvertToMeshes,
    ///             Sketches.CreateSectionSketches(..., bRecognizeLines/Arcs/Circles/Ellipses),
    ///             BSplineSurfaces.Add(poles, weights, knots...), StitchSurfaces, booleanas.
    ///
    ///   NÃO EXISTE — NENHUM ajuste de plano/cilindro/cone/esfera sobre região de malha,
    ///             nenhuma segmentação de malha, nenhuma seleção de região. Os comandos da aba
    ///             NATIVA de Engenharia Reversa não estão no modelo de objetos, e
    ///             Application.StartCommand só ABRE o comando interativo (espera o mouse), o
    ///             que não serve para script.
    ///
    /// Ou seja: o ajuste terá de ser nosso. A escolha entre a rota PRISMÁTICA (seccionar por Z
    /// reconhecendo retas/arcos/círculos e reconstruir como features editáveis) e um kernel
    /// geral de free-form não deve ser feita por aposta — e é isso que esta sonda mede, na malha
    /// REAL, antes de uma linha de kernel ser escrita.
    ///
    /// Segue a receita de descoberta do projeto: nenhuma assinatura é inventada. Presença de
    /// membro é CONFERIDA por introspecção (<see cref="ComDiagnostics.GetMemberNames"/>) antes de
    /// qualquer chamada, e toda chamada é defensiva — o que falha é registrado com o erro exato,
    /// que é justamente o dado que a sonda existe para trazer.
    /// </summary>
    public static class MeshProbe
    {
        /// <summary>Constante de tipo de geometria de malha (SolidEdgeGeometry: <c>igMesh</c>).</summary>
        private const int IgMesh = -2071771273;

        /// <summary>Membros que decidem qual rota de reconstrução é viável. A sonda confere se
        /// cada um EXISTE nesta versão da Solid Edge, sem chamar.</summary>
        private static readonly string[] InterestingModelMembers =
        {
            "IsFacetBody", "IsMixedFacetBody", "HealAndOptimizeBody", "HealAndOptimizeBodyEx",
            "HealAndOptimizeWithMeshOptions", "Body", "ConvertToMeshes", "DeleteRegions"
        };

        private static readonly string[] InterestingModelsMembers =
        {
            "AddBodyByMeshFacets", "DoRemesh", "ConvertToMeshes"
        };

        public static MeshProbeResult Run(object partDoc)
        {
            return Run(partDoc, allowSectionTest: false);
        }

        /// <param name="allowSectionTest">
        /// true = também testa <c>CreateSectionSketches</c>, que é o ÚNICO teste desta sonda que
        /// precisa ESCREVER (ele cria esboços). A sonda tenta apagar o que criou e nunca salva,
        /// mas quem autoriza é o usuário — por isso é um parâmetro, e não o padrão.
        /// </param>
        public static MeshProbeResult Run(object partDoc, bool allowSectionTest)
        {
            var log = new StringBuilder();
            var result = new MeshProbeResult();

            Log.Info("===== SONDA DE MALHA (ENG. REVERSA) — rodada 1, SÓ LEITURA =====");

            dynamic doc = partDoc;
            string docName = Safe(() => (string)doc.Name, "(sem nome)");
            Line(log, $"Peça: {docName}");

            // ---------------------------------------------------------------- 1. corpos
            dynamic models = null;
            try { models = doc.Models; }
            catch (Exception ex) { Log.Warn("doc.Models inacessível: " + ex.GetBaseException().Message); }

            int modelCount = 0;
            if (models != null) { try { modelCount = (int)models.Count; } catch { } }
            Line(log, $"Corpos (Models): {modelCount}");

            if (models != null)
            {
                Log.Info("--- membros da coleção Models (o que existe nesta versão da SE) ---");
                ReportPresence("Models", (object)models, InterestingModelsMembers);
            }

            int facetBodies = 0, meshFaces = 0, trianglesTotal = 0;

            for (int i = 1; i <= modelCount; i++)
            {
                dynamic model = null;
                try { model = models.Item(i); }
                catch (Exception ex) { Log.Warn($"Models.Item({i}) falhou: {ex.GetBaseException().Message}"); continue; }

                string name = Safe(() => (string)model.Name, $"Model[{i}]");
                bool isFacet = Flag(() => (bool)model.IsFacetBody);
                bool isMixed = Flag(() => (bool)model.IsMixedFacetBody);
                if (isFacet || isMixed) facetBodies++;

                Line(log, $"  [{i}] {name} — {(isFacet ? "CORPO DE FACETAS" : isMixed ? "MISTO (facetas + B-rep)" : "B-rep (sólido comum)")}");

                Log.Info($"--- Model[{i}] '{name}': membros de interesse ---");
                ReportPresence($"Model[{i}]", (object)model, InterestingModelMembers);

                // Caixa envolvente do corpo, em mm.
                object body = null;
                try { body = model.Body; } catch { }
                if (body != null)
                {
                    double[] min, max;
                    if (FaceGeometry.TryGetBodyRangeMm(body, out min, out max))
                        Line(log, $"       extensão: {F(max[0] - min[0])} × {F(max[1] - min[1])} × {F(max[2] - min[2])} mm");

                    int tri = ProbeBodyFacets(body, log);
                    if (tri > 0) trianglesTotal += tri;

                    meshFaces += ProbeMeshFaces(body, log, ref trianglesTotal);
                }
            }

            result.FoundMesh = facetBodies > 0 || meshFaces > 0;

            // ------------------------------------------------- 2. seccionamento (a alavanca)
            Line(log, "");
            if (allowSectionTest) ProbeSectionSketches(doc, log);
            else Line(log, "Teste de seccionamento: NÃO executado (ele cria esboços; precisa da sua autorização).");

            // ---------------------------------------------------------------- 3. veredito
            Line(log, "");
            Line(log, "— O que isto decide —");
            if (!result.FoundMesh)
            {
                Line(log, "Nenhum corpo de facetas nesta peça. Importe a malha (STL/OBJ) e rode a sonda sobre ela:");
                Line(log, "sem malha real medida, escolher entre a rota prismática e o kernel free-form seria aposta.");
            }
            else
            {
                Line(log, $"Malha encontrada: {facetBodies} corpo(s) de facetas, {meshFaces} face(s) de malha, " +
                          $"{trianglesTotal} triângulo(s) lidos.");
                Line(log, "Se os triângulos leram e o seccionamento reconheceu retas/arcos/círculos, a ROTA PRISMÁTICA");
                Line(log, "está viável: secciona por Z, agrupa contornos iguais, e furo sai como furo (eixo + Ø reais).");
            }

            Log.Info("===== FIM (SONDA DE MALHA) =====");

            result.Summary = log.ToString() +
                Environment.NewLine +
                "TODO o detalhe técnico (membros existentes, assinaturas, erros exatos) está no log — " +
                "é ele que interessa, não esta janela.";
            return result;
        }

        // ------------------------------------------------------------------ sub-sondas

        /// <summary>
        /// <c>Body.GetFacetData(Tolerance, [out] FacetCount, [out] Points, ...)</c> — a rota de
        /// tesselação que funciona até em corpo B-rep. Mede também <c>FacetCount[Tolerance]</c>,
        /// que é propriedade INDEXADA (e por isso não se lê como método).
        /// </summary>
        private static int ProbeBodyFacets(object body, StringBuilder log)
        {
            List<string> members = ComDiagnostics.GetMemberNames(body);
            bool hasGetFacetData = Has(members, "GetFacetData");
            bool hasFacetCount = Has(members, "FacetCount");
            Log.Info($"[MALHA] Body: GetFacetData={hasGetFacetData}, FacetCount={hasFacetCount}");

            if (!hasGetFacetData) return 0;

            // Tolerância em METROS (toda a API de geometria é metro): 0,01 mm.
            double toleranceM = Units.MmToM(0.01);
            try
            {
                // GetFacetData(Tolerance, [out] FacetCount, [out] Points, [opt out] Normals, ...)
                // Só os dois primeiros [out] são pedidos: os opcionais ficam de fora, e é a
                // presença/ausência deles que a sonda reporta se isto falhar.
                object[] args = { toleranceM, 0, new double[0] };
                var mod = new ParameterModifier(3);
                mod[1] = true;
                mod[2] = true;

                body.GetType().InvokeMember("GetFacetData", BindingFlags.InvokeMethod, null, body, args,
                    new[] { mod }, CultureInfo.InvariantCulture, null);

                int count = args[1] is int ? (int)args[1] : 0;
                var pts = args[2] as double[];
                Log.Info($"[MALHA] GetFacetData(tol=0,01 mm) OK: FacetCount={count}, " +
                         $"Points={(pts == null ? "null" : pts.Length + " doubles (" + (pts.Length / 3) + " pontos)")}");
                Line(log, $"       GetFacetData: {count} faceta(s) a 0,01 mm de tolerância");
                return count;
            }
            catch (Exception ex)
            {
                Log.Warn("[MALHA] GetFacetData falhou: " + ex.GetBaseException().Message);
                Line(log, "       GetFacetData: FALHOU (erro exato no log)");
                return 0;
            }
        }

        /// <summary>
        /// Procura faces cuja geometria é <c>igMesh</c> e, nelas, lê os triângulos por
        /// <c>MeshSurface.GetTrianglePoints</c> / <c>GetTriangleData</c>.
        /// </summary>
        private static int ProbeMeshFaces(object body, StringBuilder log, ref int trianglesTotal)
        {
            dynamic b = body;
            dynamic faces = null;
            try { faces = b.Faces[SolidEdgeGeometryFaceTypeAll]; }
            catch { }
            if (faces == null) { try { faces = b.Faces; } catch { } }
            if (faces == null) { Log.Warn("[MALHA] Body.Faces inacessível."); return 0; }

            int n = 0; try { n = (int)faces.Count; } catch { }
            int found = 0;

            for (int i = 1; i <= n && i <= 2000; i++)
            {
                dynamic face = null;
                try { face = faces.Item(i); } catch { continue; }

                object geom = null;
                try { geom = face.Geometry; } catch { }
                if (geom == null) continue;

                int type = 0;
                try { type = (int)((dynamic)geom).Type; } catch { continue; }
                if (type != IgMesh) continue;

                found++;
                if (found == 1)
                {
                    // A PRIMEIRA face de malha vai para o dump completo: é assim que a API real
                    // de MeshSurface entra na skill, sem ninguém adivinhar assinatura.
                    Log.Info("[MALHA] Primeira face de malha encontrada — dump completo abaixo.");
                    ComDiagnostics.DumpObject("MeshSurface (1ª face de malha)", geom, 1);
                }

                int tri = ReadTriangles(geom);
                if (tri > 0) { trianglesTotal += tri; Line(log, $"       face de malha [{i}]: {tri} triângulo(s)"); }
            }

            if (found > 0) Log.Info($"[MALHA] {found} face(s) de malha (igMesh) no corpo.");
            return found;
        }

        /// <summary>Constante de <c>FeatureTopologyQueryTypeConstants</c> para "todas as faces".</summary>
        private const int SolidEdgeGeometryFaceTypeAll = 0;

        private static int ReadTriangles(object meshSurface)
        {
            List<string> members = ComDiagnostics.GetMemberNames(meshSurface);
            Log.Info("[MALHA] MeshSurface: GetTrianglePoints=" + Has(members, "GetTrianglePoints") +
                     ", GetTriangleData=" + Has(members, "GetTriangleData") +
                     ", GetTriangleNormals=" + Has(members, "GetTriangleNormals"));

            // GetTrianglePoints([out] FacetCount, [out] Points) — a forma mais simples, e por
            // isso a primeira tentativa.
            if (Has(members, "GetTrianglePoints"))
            {
                try
                {
                    object[] args = { 0, new double[0] };
                    var mod = new ParameterModifier(2);
                    mod[0] = true; mod[1] = true;

                    meshSurface.GetType().InvokeMember("GetTrianglePoints", BindingFlags.InvokeMethod, null,
                        meshSurface, args, new[] { mod }, CultureInfo.InvariantCulture, null);

                    int count = args[0] is int ? (int)args[0] : 0;
                    var pts = args[1] as double[];
                    Log.Info($"[MALHA] GetTrianglePoints OK: FacetCount={count}, " +
                             $"Points={(pts == null ? "null" : pts.Length.ToString(CultureInfo.InvariantCulture) + " doubles")}");

                    // Uma amostra em mm prova que a leitura é geometria de verdade, e não lixo.
                    if (pts != null && pts.Length >= 9)
                        Log.Info("[MALHA] 1º triângulo (mm): " +
                                 $"({F(Units.MToMm(pts[0]))}, {F(Units.MToMm(pts[1]))}, {F(Units.MToMm(pts[2]))}) " +
                                 $"({F(Units.MToMm(pts[3]))}, {F(Units.MToMm(pts[4]))}, {F(Units.MToMm(pts[5]))}) " +
                                 $"({F(Units.MToMm(pts[6]))}, {F(Units.MToMm(pts[7]))}, {F(Units.MToMm(pts[8]))})");
                    return count;
                }
                catch (Exception ex) { Log.Warn("[MALHA] GetTrianglePoints falhou: " + ex.GetBaseException().Message); }
            }

            if (Has(members, "GetTriangleData"))
            {
                try
                {
                    object[] args = { 0, new double[0], new double[0] };
                    var mod = new ParameterModifier(3);
                    mod[0] = true; mod[1] = true; mod[2] = true;

                    meshSurface.GetType().InvokeMember("GetTriangleData", BindingFlags.InvokeMethod, null,
                        meshSurface, args, new[] { mod }, CultureInfo.InvariantCulture, null);

                    int count = args[0] is int ? (int)args[0] : 0;
                    Log.Info($"[MALHA] GetTriangleData OK: FacetCount={count}");
                    return count;
                }
                catch (Exception ex) { Log.Warn("[MALHA] GetTriangleData falhou: " + ex.GetBaseException().Message); }
            }
            return 0;
        }

        /// <summary>
        /// O TESTE QUE DECIDE A ROTA PRISMÁTICA. <c>CreateSectionSketches</c> corta o corpo com
        /// planos e devolve esboços já com retas, arcos, círculos e elipses RECONHECIDOS — se
        /// isto funcionar sobre malha, o caminho "secciona por Z e reconstrói como feature" está
        /// aberto e não precisamos escrever reconhecimento de primitiva 2D.
        ///
        /// ESCREVE (cria esboços). Tenta apagar o que criou e NUNCA salva.
        /// </summary>
        private static void ProbeSectionSketches(dynamic doc, StringBuilder log)
        {
            dynamic sketches = null;
            try { sketches = doc.Sketches; }
            catch (Exception ex) { Log.Warn("doc.Sketches inacessível: " + ex.GetBaseException().Message); }
            if (sketches == null) { Line(log, "Seccionamento: doc.Sketches inacessível (veja o log)."); return; }

            List<string> members = ComDiagnostics.GetMemberNames((object)sketches);
            if (!Has(members, "CreateSectionSketches"))
            {
                Log.Warn("[MALHA] Sketches NÃO expõe CreateSectionSketches nesta versão da SE.");
                Line(log, "Seccionamento: esta versão da Solid Edge não expõe CreateSectionSketches.");
                return;
            }

            object body = null;
            try { body = doc.Models.Item(1).Body; } catch { }
            if (body == null) { Line(log, "Seccionamento: sem corpo para cortar."); return; }

            object plane = null;
            try { plane = doc.RefPlanes.Item(1); } catch { }
            if (plane == null) { Line(log, "Seccionamento: não achei plano de referência."); return; }

            Log.Info("[MALHA] Tentando CreateSectionSketches (3 planos, reconhecendo retas/arcos/círculos/elipses)...");
            try
            {
                // CreateSectionSketches(psaObjects, RefPlane, [out] SketchesGenerated,
                //   [out] SketchCount, [out] enumErrorCode, [opt] NumOfPlanes,
                //   [opt] enumReferenceSide, [opt] dPlaneOffset,
                //   [opt] bRecognizeLines, bRecognizeArcs, bRecognizeCircles, bRecognizeEllipses)
                object[] args =
                {
                    new object[] { body },      // psaObjects
                    plane,                      // RefPlane
                    new object[0],              // [out] SketchesGenerated
                    0,                          // [out] SketchCount
                    0,                          // [out] enumErrorCode
                    3,                          // NumOfPlanes
                    0,                          // enumReferenceSide
                    Units.MmToM(5.0),           // dPlaneOffset (METROS)
                    1, 1, 1, 1                  // reconhecer retas, arcos, círculos, elipses
                };
                var mod = new ParameterModifier(args.Length);
                mod[2] = true; mod[3] = true; mod[4] = true;

                sketches.GetType().InvokeMember("CreateSectionSketches", BindingFlags.InvokeMethod, null,
                    (object)sketches, args, new[] { mod }, CultureInfo.InvariantCulture, null);

                int made = args[3] is int ? (int)args[3] : 0;
                int err = args[4] is int ? (int)args[4] : -1;
                Log.Info($"[MALHA] CreateSectionSketches devolveu: SketchCount={made}, enumErrorCode={err}");
                Line(log, $"Seccionamento: {made} esboço(s) gerado(s), código de erro {err}.");

                // Dump do 1º esboço: é onde se vê SE as primitivas saíram reconhecidas (Lines2d,
                // Arcs2d, Circles2d) ou se veio tudo como polilinha — a diferença entre a rota
                // prismática ser viável e não ser.
                var generated = args[2] as object[];
                if (generated != null && generated.Length > 0 && generated[0] != null)
                    ComDiagnostics.DumpObject("Esboço de seção [1] (procure Lines2d/Arcs2d/Circles2d)", generated[0], 2);

                RemoveSketches(generated, log);
            }
            catch (Exception ex)
            {
                Log.Warn("[MALHA] CreateSectionSketches falhou: " + ex.GetBaseException().Message);
                Line(log, "Seccionamento: FALHOU — " + ex.GetBaseException().Message);
            }
        }

        /// <summary>Desfaz o único efeito colateral da sonda. Se algum esboço não sair, diz —
        /// ficar em silêncio deixaria lixo na árvore da peça do Carlos sem ele saber.</summary>
        private static void RemoveSketches(object[] generated, StringBuilder log)
        {
            if (generated == null || generated.Length == 0) return;
            int removed = 0, failed = 0;
            foreach (object sk in generated)
            {
                if (sk == null) continue;
                try { ((dynamic)sk).Delete(); removed++; }
                catch (Exception ex) { failed++; Log.Warn("[MALHA] não apagou um esboço de seção: " + ex.GetBaseException().Message); }
            }
            Log.Info($"[MALHA] limpeza dos esboços da sonda: {removed} apagado(s), {failed} restante(s).");
            Line(log, failed == 0
                ? $"Limpeza: os {removed} esboço(s) da sonda foram apagados (a peça NÃO foi salva)."
                : $"ATENÇÃO: {failed} esboço(s) da sonda ficaram na árvore — apague à mão. A peça NÃO foi salva.");
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Confere e REGISTRA quais membros de interesse existem, sem chamar nenhum.
        /// É o passo que evita adivinhar API: presença primeiro, chamada depois.</summary>
        private static void ReportPresence(string label, object com, string[] wanted)
        {
            List<string> members = ComDiagnostics.GetMemberNames(com);
            if (members.Count == 0) { Log.Warn($"[MALHA] {label}: sem type info (não é IDispatch?)."); return; }

            var present = new List<string>();
            var missing = new List<string>();
            foreach (string w in wanted) (Has(members, w) ? present : missing).Add(w);

            Log.Info($"[MALHA] {label} TEM: {(present.Count == 0 ? "(nada da lista)" : string.Join(", ", present.ToArray()))}");
            Log.Info($"[MALHA] {label} NÃO tem: {(missing.Count == 0 ? "(nada faltando)" : string.Join(", ", missing.ToArray()))}");
        }

        private static bool Has(List<string> members, string name)
        {
            if (members == null) return false;
            foreach (string m in members)
                if (string.Equals(m, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Line(StringBuilder sb, string text) => sb.AppendLine(text);

        private static string Safe(Func<string> read, string fallback)
        {
            try { string s = read(); return string.IsNullOrEmpty(s) ? fallback : s; }
            catch { return fallback; }
        }

        private static bool Flag(Func<bool> read)
        {
            try { return read(); } catch { return false; }
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
