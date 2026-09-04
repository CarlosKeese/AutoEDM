using System;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Sealing
{
    /// <summary>
    /// Corta o alojamento do anel na peça, por COM.
    ///
    /// UM SÓ CAMINHO PARA OS TRÊS CASOS: canal de face, canal de eixo e canal de furo são
    /// todos o MESMO recurso — um retângulo revolvido 360° em torno do eixo do anel. Muda só
    /// onde o retângulo fica (afastamento radial e posição axial). Por isso aqui não há três
    /// receitas de modelagem para dar manutenção, só três jeitos de montar quatro pontos.
    ///
    /// O retângulo é montado em COORDENADAS 3D REAIS da peça e convertido para o esboço pelo
    /// próprio Solid Edge (<see cref="ProfilePlaneFrame"/>) — nada de supor qual RefPlane é
    /// qual, nem qual eixo local do plano é o radial.
    ///
    /// Assinaturas confirmadas no dump da typelib (SE 2023):
    ///   RevolvedCutouts.AddFiniteSync(Profile, RefAxis, ProfileSide, [ProfilePlaneSide], [Ângulo])
    ///   RevolvedCutouts.AddFinite(...) — mesma forma, para peça ORDENADA
    ///   Profile.SetAxisOfRevolution(LineForAxis) -> RefAxis   (RefAxes NÃO tem Add)
    ///   Profile.ToggleConstruction(Element)
    /// </summary>
    public static class ORingGrooveModeler
    {
        /// <summary>Quanto o perfil de corte ultrapassa a superfície (mm) — sem esta sobra, o
        /// corte fica tangente à face e o modelador pode não abrir o canal.</summary>
        private const double OvershootMm = 0.05;

        private const int igLeft = 1, igRight = 2;

        /// <summary>
        /// Corta o canal. <paramref name="grooveOffsetMm"/> é a distância da ARESTA
        /// selecionada até o CENTRO do canal, medida ao longo do eixo, para dentro do
        /// material — só vale para canal de eixo/furo. No canal de FACE o canal afunda a
        /// partir da própria face, e o afastamento radial já veio embutido na escolha do anel
        /// (<see cref="ORingGrooveCalculator.FaceSealingDiameter"/>): aqui ele chega pronto,
        /// nos diâmetros interno/externo do <paramref name="spec"/>.
        /// Nunca lança: devolve false e explica no log.
        /// </summary>
        public static bool Cut(dynamic app, ORingTarget target, ORingGrooveSpec spec, double grooveOffsetMm)
        {
            if (target == null || !target.Ok) { Log.Warn("Canal de O'ring: seleção inválida."); return false; }
            if (spec == null) { Log.Warn("Canal de O'ring: sem alojamento calculado."); return false; }

            dynamic ps = null, plane = null;
            bool planeIsTemporary = false;
            try
            {
                dynamic doc = app.ActiveDocument;
                int mode = 1; try { mode = (int)doc.ModelingMode; } catch { }
                dynamic model = doc.Models.Item(1);
                int facesBefore = FaceCount(model);
                Log.Info($"Canal de O'ring: ModelingMode={mode}, {facesBefore} face(s) no corpo antes.");

                double[] axisDir = ProfilePlaneFrame.AxisVector(target.AxisIndex);
                // TIPADO de propósito: o retorno de um método chamado com argumento `dynamic`
                // sai dynamic, e aí o compilador perde a análise dos parâmetros [out] adiante.
                AxisSketch sketch = OpenSketchThroughAxis(doc, target.CenterMm, axisDir);
                if (sketch == null) { Log.Warn("Canal de O'ring: não achei um plano de esboço que contenha o eixo."); return false; }

                ps = sketch.ProfileSet;
                plane = sketch.Plane;
                planeIsTemporary = sketch.PlaneIsTemporary;
                dynamic profile = sketch.Profile;
                ProfilePlaneFrame frame = sketch.Frame;

                double[] radial = ProfilePlaneFrame.Normalize(ProfilePlaneFrame.Cross(frame.Normal, axisDir));
                if (radial == null) { Log.Warn("Canal de O'ring: direção radial degenerada."); return false; }

                double a0, a1, rInner, rOuter;
                BuildRectangle(target, spec, grooveOffsetMm, out a0, out a1, out rInner, out rOuter);
                Log.Info($"  perfil do canal: eixo {target.AxisName} de {a0:0.000} a {a1:0.000} mm, " +
                         $"raio de {rInner:0.000} a {rOuter:0.000} mm (Ø {2 * rInner:0.000} a {2 * rOuter:0.000}).");
                if (rOuter - rInner <= 1e-6 || Math.Abs(a1 - a0) <= 1e-6)
                { Log.Warn("Canal de O'ring: retângulo do canal degenerado — nada a cortar."); return false; }

                var corners = new[]
                {
                    Point(target, axisDir, radial, a0, rInner),
                    Point(target, axisDir, radial, a1, rInner),
                    Point(target, axisDir, radial, a1, rOuter),
                    Point(target, axisDir, radial, a0, rOuter)
                };
                if (!DrawClosedPolygon(profile, frame, corners)) return false;

                // Eixo de revolução: uma linha de CONSTRUÇÃO sobre o próprio eixo do anel,
                // esticada para além do canal (não pode fazer parte do contorno fechado).
                double pad = Math.Abs(a1 - a0) + 5.0;
                double x1, y1, x2, y2;
                if (!frame.TryTo2dMm((object)profile, Point(target, axisDir, radial, Math.Min(a0, a1) - pad, 0.0), out x1, out y1) ||
                    !frame.TryTo2dMm((object)profile, Point(target, axisDir, radial, Math.Max(a0, a1) + pad, 0.0), out x2, out y2))
                { Log.Warn("Canal de O'ring: não deu para posicionar o eixo de revolução."); return false; }

                dynamic axisLine = profile.Lines2d.AddBy2Points(Units.MmToM(x1), Units.MmToM(y1), Units.MmToM(x2), Units.MmToM(y2));
                try { profile.ToggleConstruction(axisLine); }
                catch (Exception e) { Log.Warn("  ToggleConstruction no eixo falhou: " + e.GetBaseException().Message); }

                // O contorno tem de ser 4 linhas + 1 linha de CONSTRUÇÃO. Se o eixo não virar
                // construção, ele entra no contorno, o perfil deixa de ser uma região fechada
                // simples e o corte falha com um E_FAIL que não explica nada.
                Log.Info($"  esboço: {Count(profile.Lines2d)} linha(s); eixo é construção: {IsConstruction(profile, axisLine)}.");

                object refAxis;
                try { refAxis = (object)profile.SetAxisOfRevolution(axisLine); }
                catch (Exception e) { Log.Warn("Canal de O'ring: SetAxisOfRevolution falhou: " + e.GetBaseException().Message); return false; }
                if (refAxis == null) { Log.Warn("Canal de O'ring: SetAxisOfRevolution devolveu nulo."); return false; }

                // O que se PEDE ao SE que valide. Para revolver não basta "fechado": tem de
                // haver eixo (16) e o perfil não pode cruzá-lo (32) — pedir isso é o que faz o
                // SE amarrar o eixo ao perfil. E o retorno de End() é a resposta
                // (ProfileValidationStatus: 0 = válido, −1 = inválido), que antes se jogava
                // fora: um perfil inválido só reaparecia lá na frente como E_FAIL mudo.
                const int criteria = 1 | 4 | 8 | 16 | 32;
                int validation = -99;
                try { validation = Convert.ToInt32(profile.End(criteria)); }
                catch (Exception e) { Log.Warn("  Profile.End(" + criteria + ") falhou: " + e.GetBaseException().Message); }
                Log.Info($"  perfil validado: End({criteria}) = {validation} " +
                         $"({(validation == 0 ? "válido" : validation == -1 ? "INVÁLIDO" : "status desconhecido")}).");

                object cut = Revolve(model, (object)profile, refAxis, mode)
                          ?? SubtractRevolvedBody(doc, model, (object)profile, refAxis, mode);
                // Só o plano que ESTE comando criou pode sumir — esconder um plano-base que o
                // usuário deixou visível seria mexer no documento dele sem ele pedir.
                if (planeIsTemporary) { try { plane.Visible = false; } catch { } }

                int facesAfter = FaceCount(model);
                bool ok = cut != null && !FeatureFailed(cut) && facesAfter > facesBefore;
                Log.Info($"  {facesAfter} face(s) no corpo depois (antes {facesBefore}); Status={StatusOf(cut)}.");
                if (ok) Log.Info("Canal de O'ring CRIADO ✓");
                else Log.Warn($"Canal de O'ring: o corte não vingou — nenhuma forma de corte do modo " +
                              $"{(mode == 2 ? "ORDENADO" : "SÍNCRONO")} foi aceita. O AutoEDM NÃO tenta os métodos do " +
                              "outro modo de propósito: isso criaria um recurso no outro ambiente, que a SE não " +
                              "deixa apagar. Se precisar, troque o modo do documento e repita.");
                return ok;
            }
            catch (Exception e)
            {
                Log.Warn("Canal de O'ring falhou: " + e.GetBaseException().Message);
                return false;
            }
            finally
            {
                // O esboço SEMPRE é apagado, dê certo ou não. `ProfileSets.Add()` cria um
                // esboço ORDENADO mesmo numa peça síncrona e, depois que um recurso o consome,
                // ele fica TRAVADO: o usuário não consegue mais apagá-lo pela interface. Deixá-lo
                // para trás foi o que sujou a peça do Carlos em 2026-09-04 — e a regra já estava
                // escrita na skill do projeto. O recurso sobrevive à exclusão do esboço (é o que
                // o bloco do eletrodo faz desde sempre).
                if (ps != null)
                {
                    try { ps.Delete(); }
                    catch (Exception e) { Log.Warn("  o esboço do canal não pôde ser apagado: " + e.GetBaseException().Message); }
                }
                if (planeIsTemporary && plane != null) { try { plane.Visible = false; } catch { } }
            }
        }

        /// <summary>
        /// Os quatro cantos do canal, em (coordenada ao longo do eixo, raio). É aqui — e só
        /// aqui — que os três tipos de canal se distinguem.
        /// </summary>
        private static void BuildRectangle(ORingTarget target, ORingGrooveSpec spec, double grooveOffsetMm,
            out double a0, out double a1, out double rInner, out double rOuter)
        {
            if (spec.Kind == GrooveKind.AxialFace)
            {
                // Canal anular afundando na face plana, a partir do plano dela.
                double dir = target.MaterialDirection;
                a0 = target.EdgeAxialMm - dir * OvershootMm;
                a1 = target.EdgeAxialMm + dir * spec.Depth;
                rInner = spec.GrooveInnerDiameter / 2.0;
                rOuter = spec.GrooveOuterDiameter / 2.0;
                return;
            }

            double center = target.EdgeAxialMm + target.MaterialDirection * grooveOffsetMm;
            a0 = center - spec.Width / 2.0;
            a1 = center + spec.Width / 2.0;

            if (spec.Kind == GrooveKind.RadialExternal)
            {
                // Eixo: o canal come PARA DENTRO, do costado até o fundo.
                rInner = spec.GrooveBottomDiameter / 2.0;
                rOuter = spec.SealingDiameter / 2.0 + OvershootMm;
            }
            else
            {
                // Furo: o canal come PARA FORA, da parede do furo até o fundo.
                rInner = spec.SealingDiameter / 2.0 - OvershootMm;
                rOuter = spec.GrooveBottomDiameter / 2.0;
            }
        }

        /// <summary>Ponto 3D (mm) a uma coordenada <paramref name="axial"/> do eixo e a um
        /// <paramref name="radius"/> dele, dentro do plano do esboço.</summary>
        private static double[] Point(ORingTarget target, double[] axisDir, double[] radial, double axial, double radius)
        {
            double along = axial - target.CenterMm[target.AxisIndex];
            var p = ProfilePlaneFrame.Add(target.CenterMm, axisDir, along);
            return ProfilePlaneFrame.Add(p, radial, radius);
        }

        private static bool DrawClosedPolygon(dynamic profile, ProfilePlaneFrame frame, double[][] corners3d)
        {
            var xs = new double[corners3d.Length];
            var ys = new double[corners3d.Length];
            for (int i = 0; i < corners3d.Length; i++)
                if (!frame.TryTo2dMm((object)profile, corners3d[i], out xs[i], out ys[i]))
                { Log.Warn($"Canal de O'ring: canto {i} não converteu para o esboço."); return false; }

            dynamic lines = profile.Lines2d;
            for (int i = 0; i < corners3d.Length; i++)
            {
                int j = (i + 1) % corners3d.Length;
                lines.AddBy2Points(Units.MmToM(xs[i]), Units.MmToM(ys[i]), Units.MmToM(xs[j]), Units.MmToM(ys[j]));
            }
            return true;
        }

        /// <summary>
        /// Revolve o corte 360°, tentando as formas de chamada em ordem de aposta.
        ///
        /// A forma MULTI vem primeiro porque é a que este projeto já tem provada em campo: o
        /// bloco do eletrodo nasce de <c>Models.AddFiniteExtrudedProtrusion(1, Profile[], …)</c>,
        /// com o perfil num ARRAY TIPADO <c>SolidEdgePart.Profile[]</c>. Um <c>object[]</c>
        /// marshala como SAFEARRAY(VARIANT) e o SE quer SAFEARRAY(IDispatch) — a mesma pegadinha
        /// que já está na tabela de erros do projeto. Some-se a isso que a forma MULTI não pede
        /// <c>ProfileSide</c>: num perfil FECHADO não existe "lado", e foi justamente a forma de
        /// perfil único (que exige o lado) que devolveu E_FAIL nos dois lados, nos dois modos,
        /// em duas peças diferentes (logs de 2026-09-04).
        ///
        /// O ângulo também entra nas tentativas com e sem valor: 2π é "360°" explícito, e
        /// <c>Type.Missing</c> deixa o SE usar o padrão dele para volta inteira.
        /// </summary>
        private static object Revolve(dynamic model, object profile, object refAxis, int modelingMode)
        {
            bool sync = modelingMode != 2;
            double fullTurn = 2.0 * Math.PI;

            // Array TIPADO: é isto, e não object[], que marshala como SAFEARRAY(IDispatch).
            SolidEdgePart.Profile[] arr = null;
            try { arr = new[] { (SolidEdgePart.Profile)profile }; }
            catch (Exception e) { Log.Warn("  perfil não converteu para SolidEdgePart.Profile: " + e.GetBaseException().Message); }

            // SÓ os métodos do modo do documento. Misturar era o que fazia um recurso ORDENADO
            // nascer numa peça SÍNCRONA (relato do Carlos, 2026-09-04): a SE aceita a chamada,
            // cria o esboço/recurso no outro ambiente e o resultado é um recurso que o usuário
            // não consegue apagar. Um fallback que suja a peça do usuário não é fallback.
            var attempts = new System.Collections.Generic.List<Attempt>();
            string multi = sync ? "AddFiniteMultiSync" : "AddFiniteMulti";
            string single = sync ? "AddFiniteSync" : "AddFinite";

            if (arr != null)
            {
                // AddFiniteMulti*(NumberOfProfiles, ProfileArray, RefAxis, [PlaneSide], [Ângulo])
                // ÂNGULO PADRÃO PRIMEIRO: é a única forma que funcionou em peça real
                // (log 2026-09-04, ordenado). Com 2π explícito a SE devolve E_FAIL — e uma
                // tentativa falha custa caro, porque apagar a feature falhada pode desconectar
                // o documento e derrubar todo o resto da fila.
                attempts.Add(new Attempt(multi, "ângulo padrão", new object[] { 1, arr, refAxis, Type.Missing, Type.Missing }));
                attempts.Add(new Attempt(multi, "360°", new object[] { 1, arr, refAxis, Type.Missing, fullTurn }));
            }
            // AddFinite*(Profile, RefAxis, ProfileSide, [PlaneSide], [Ângulo])
            foreach (int side in new[] { igRight, igLeft })
                attempts.Add(new Attempt(single, $"lado {side}, ângulo padrão", new object[] { profile, refAxis, side, Type.Missing, Type.Missing }));

            Log.Info($"  modo do documento: {(sync ? "SÍNCRONO" : "ORDENADO")} — usando só os métodos deste modo.");

            foreach (var a in attempts)
            {
                try
                {
                    object cutouts = (object)model.RevolvedCutouts;
                    object cut = cutouts.GetType().InvokeMember(a.Method, BindingFlags.InvokeMethod, null, cutouts, a.Args);
                    if (cut != null && !FeatureFailed(cut))
                    {
                        Log.Info($"  corte revolvido por {a.Method}({a.How}).");
                        return cut;
                    }
                    if (cut != null)
                    {
                        // Uma tentativa que falha DEIXA a feature falhada na árvore. Se ela
                        // ficar, a próxima tentativa empilha outra e a peça acaba com um
                        // rastro de recursos vermelhos que ninguém pediu.
                        Log.Warn($"  {a.Method}({a.How}) devolveu feature com Status de falha — apagando.");
                        try { ((dynamic)cut).Delete(); }
                        catch (Exception de) { Log.Warn("  não deu para apagar a feature falhada: " + de.GetBaseException().Message); }
                    }
                    else Log.Warn($"  {a.Method}({a.How}): devolveu nulo.");
                }
                catch (Exception e)
                {
                    Log.Warn($"  {a.Method}({a.How}): {e.GetBaseException().Message}");
                    if (IsDisconnected(e))
                    {
                        // Apagar a feature falhada mata o proxy do documento; daí para a frente
                        // TODA chamada erra igual. Insistir só enche o log de ruído.
                        Log.Warn("  o documento desconectou — parando as tentativas (refaça a operação).");
                        return null;
                    }
                }
            }
            return null;
        }

        /// <summary>RPC_E_DISCONNECTED: o objeto COM do outro lado deixou de existir.</summary>
        private static bool IsDisconnected(Exception e)
        {
            for (Exception x = e; x != null; x = x.InnerException)
                if ((uint)x.HResult == 0x80010108u) return true;
            return false;
        }

        /// <summary>
        /// PLANO B do corte: em vez de pedir um "corte revolvido", cria o canal como um CORPO
        /// (anel maciço, protrusão revolvida do mesmo perfil) e SUBTRAI esse corpo da peça.
        ///
        /// As duas metades já são caminhos provados neste projeto: a protrusão é a irmã do
        /// <c>AddFiniteExtrudedProtrusion</c> que levanta o bloco do eletrodo, e a booleana é a
        /// mesma <c>Model.BooleanFeatures.Add</c> que une a superfície de queima ao bloco (lá com
        /// <c>seBooleanUnite</c>, aqui com <c>seBooleanSubtract</c>). Existe porque
        /// <c>RevolvedCutouts</c> devolveu E_FAIL em toda forma de chamada, em peças diferentes.
        /// </summary>
        private static object SubtractRevolvedBody(dynamic doc, dynamic model, object profile, object refAxis, int modelingMode)
        {
            const int seBooleanSubtract = 2;   // BooleanFeatureConstants
            bool sync = modelingMode != 2;

            SolidEdgePart.Profile[] arr;
            try { arr = new[] { (SolidEdgePart.Profile)profile }; }
            catch (Exception e) { Log.Warn("  [plano B] perfil não converteu: " + e.GetBaseException().Message); return null; }

            object ring = null;
            string used = null;
            // AddFiniteRevolvedProtrusion*(nProfiles, ProfileArray, RefAxis, ProfilePlaneSide, Ângulo)
            // — ProfilePlaneSide NÃO é opcional aqui, então os dois lados entram na tentativa.
            // Só o método do modo do documento — mesma regra do corte: um recurso criado no
            // outro ambiente nasce impossível de apagar.
            foreach (string name in new[] { sync ? "AddFiniteRevolvedProtrusionSync" : "AddFiniteRevolvedProtrusion" })
            {
                foreach (int side in new[] { igRight, igLeft })
                {
                    try
                    {
                        object models = (object)doc.Models;
                        ring = models.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, models,
                            new object[] { 1, arr, refAxis, side, 2.0 * Math.PI });
                        if (ring != null) { used = $"{name}(lado {side})"; break; }
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"  [plano B] {name}(lado {side}): " + e.GetBaseException().Message);
                        if (IsDisconnected(e)) { Log.Warn("  [plano B] documento desconectado — parando."); return null; }
                    }
                }
                if (ring != null) break;
            }
            if (ring == null) { Log.Warn("  [plano B] o anel do canal não foi criado como corpo."); return null; }
            Log.Info($"  [plano B] anel do canal criado por {used}; subtraindo da peça.");

            try
            {
                var tools = new SolidEdgePart.Model[] { (SolidEdgePart.Model)ring };
                object boolFeatures = ((object)model).GetType().InvokeMember(
                    "BooleanFeatures", BindingFlags.GetProperty, null, (object)model, null);
                object feature = boolFeatures.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, boolFeatures,
                    new object[] { tools.Length, tools, seBooleanSubtract, Type.Missing });
                if (feature != null && !FeatureFailed(feature))
                {
                    Log.Info("  [plano B] canal subtraído (Model.BooleanFeatures.Add, seBooleanSubtract).");
                    return feature;
                }
                Log.Warn($"  [plano B] a booleana de subtração não vingou (Status={StatusOf(feature)}).");
            }
            catch (Exception e) { Log.Warn("  [plano B] Model.BooleanFeatures.Add falhou: " + e.GetBaseException().Message); }

            // O anel ficou como corpo solto: apagar, senão a peça sai com um sólido a mais.
            try { ((dynamic)ring).Delete(); }
            catch (Exception e) { Log.Warn("  [plano B] o anel sobrou na peça e não deu para apagar: " + e.GetBaseException().Message); }
            return null;
        }

        /// <summary>Uma forma de chamada a tentar, com o rótulo que vai para o log.</summary>
        private sealed class Attempt
        {
            public readonly string Method, How;
            public readonly object[] Args;
            public Attempt(string method, string how, object[] args) { Method = method; How = how; Args = args; }
        }

        /// <summary>O esboço já aberto no plano certo, com o referencial dele.</summary>
        private sealed class AxisSketch
        {
            public dynamic ProfileSet;
            public dynamic Profile;
            public dynamic Plane;
            public ProfilePlaneFrame Frame;
            public bool PlaneIsTemporary;
        }

        /// <summary>
        /// Abre o esboço num plano que CONTENHA o eixo do anel. Testa os três planos base: o
        /// que for PARALELO ao eixo serve — se ainda estiver afastado, cria um paralelo a ele
        /// na distância certa. Cada tentativa é CONFERIDA lendo o referencial do plano pelo
        /// próprio esboço, então um lado trocado no <c>AddParallelByDistance</c> se corrige
        /// sozinho em vez de sair um canal no lugar errado.
        ///
        /// POR QUE O ESBOÇO NASCE AQUI, e não depois de escolher o plano (bug de 2026-09-04,
        /// "o anel era recomendado mas o canal nunca aparecia"): o plano recém-criado só existe
        /// enquanto ALGUÉM o usa. A versão antiga sondava o plano novo com um esboço
        /// DESCARTÁVEL e apagava esse esboço para depois abrir o definitivo — e nesse intervalo
        /// o Solid Edge levava o plano junto. O proxy voltava morto e a primeira chamada
        /// seguinte estourava RPC_E_DISCONNECTED (0x80010108), dentro do binder do
        /// <c>dynamic</c>, sem dizer que o culpado era o plano. Agora o esboço que confere o
        /// plano é o MESMO que desenha o canal: nada é apagado no meio do caminho.
        /// </summary>
        private static AxisSketch OpenSketchThroughAxis(dynamic doc, double[] axisPointMm, double[] axisDir)
        {
            for (int i = 1; i <= 3; i++)
            {
                dynamic basePlane;
                try { basePlane = doc.RefPlanes.Item(i); } catch { continue; }

                // Sondar um plano BASE com esboço descartável é seguro: plano base não some.
                var frame = FrameOf(doc, basePlane);
                if (frame == null || !frame.IsParallelToLine(axisDir)) continue;

                double d = frame.SignedDistance(axisPointMm);
                if (Math.Abs(d) < 0.01)
                {
                    var onBase = TryOpenSketch(doc, basePlane, false, axisPointMm, axisDir);
                    if (onBase != null)
                    {
                        Log.Info($"  [plano] RefPlanes.Item({i}) já contém o eixo.");
                        return onBase;
                    }
                    continue;
                }

                foreach (int side in new[] { igRight, igLeft })
                {
                    dynamic candidate = null;
                    try
                    {
                        candidate = doc.RefPlanes.AddParallelByDistance(
                            basePlane, Units.MmToM(Math.Abs(d)), side, Type.Missing, Type.Missing, Type.Missing);
                    }
                    catch (Exception e) { Log.Warn($"  [plano] AddParallelByDistance(Item({i}), {Math.Abs(d):0.###}, {side}): " + e.GetBaseException().Message); }
                    if (candidate == null) continue;

                    var s = TryOpenSketch(doc, candidate, true, axisPointMm, axisDir);
                    if (s != null)
                    {
                        Log.Info($"  [plano] criado a partir de RefPlanes.Item({i}), afastamento {Math.Abs(d):0.###} mm, lado {side}.");
                        return s;
                    }
                    try { candidate.Delete(); } catch { try { candidate.Visible = false; } catch { } }
                }
            }
            return null;
        }

        /// <summary>
        /// Abre o esboço DEFINITIVO no plano e só o devolve se o referencial confirmar que o
        /// eixo do anel está contido nele. Se não estiver, desfaz o esboço e deixa o chamador
        /// tentar o outro lado.
        /// </summary>
        private static AxisSketch TryOpenSketch(dynamic doc, dynamic plane, bool planeIsTemporary,
            double[] axisPointMm, double[] axisDir)
        {
            dynamic ps = null;
            try
            {
                ps = doc.ProfileSets.Add();
                dynamic profile = ps.Profiles.Add(plane);
                var frame = ProfilePlaneFrame.Discover((object)profile);
                if (frame != null && frame.ContainsLine(axisPointMm, axisDir))
                    return new AxisSketch
                    {
                        ProfileSet = ps, Profile = profile, Plane = plane,
                        Frame = frame, PlaneIsTemporary = planeIsTemporary
                    };
            }
            catch (Exception e) { Log.Warn("  [plano] não deu para abrir o esboço: " + e.GetBaseException().Message); }

            if (ps != null) { try { ps.Delete(); } catch { } }
            return null;
        }

        /// <summary>Referencial de um plano — abre um perfil descartável só para perguntar ao
        /// SE onde o plano está, e apaga em seguida.</summary>
        private static ProfilePlaneFrame FrameOf(dynamic doc, dynamic plane)
        {
            dynamic probe = null;
            try
            {
                probe = doc.ProfileSets.Add();
                dynamic p = probe.Profiles.Add(plane);
                return ProfilePlaneFrame.Discover((object)p);
            }
            catch (Exception e) { Log.Warn("  [plano] sondagem falhou: " + e.GetBaseException().Message); return null; }
            finally { if (probe != null) { try { probe.Delete(); } catch { } } }
        }

        private static string Count(dynamic collection)
        {
            try { return ((int)collection.Count).ToString(); } catch { return "?"; }
        }

        private static string IsConstruction(dynamic profile, object element)
        {
            try { return ((bool)profile.IsConstructionElement(element)) ? "sim" : "NÃO"; }
            catch (Exception e) { return "ilegível (" + e.GetBaseException().Message + ")"; }
        }

        private static int FaceCount(dynamic model)
        {
            try { return (int)model.Body.Faces[1].Count; } catch { return -1; }
        }

        private static bool FeatureFailed(object feature)
        {
            try
            {
                object s = ((dynamic)feature).Status;
                return s != null && Convert.ToInt64(s) == 1216476311L; // igFeatureFailed
            }
            catch { return false; }
        }

        private static string StatusOf(object feature)
        {
            if (feature == null) return "(null)";
            try
            {
                object s = ((dynamic)feature).Status;
                if (s == null) return "(sem Status)";
                long v = Convert.ToInt64(s);
                return v == 1216476310L ? "igFeatureOK" : v == 1216476311L ? "igFeatureFailed" : v.ToString();
            }
            catch { return "(sem Status)"; }
        }
    }
}
