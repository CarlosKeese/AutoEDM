using System;
using System.Collections.Generic;
using System.Reflection;
using AutoEDM.Com;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Selection;

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
            string ignored;
            return Cut((object)app, target, spec, grooveOffsetMm, out ignored);
        }

        /// <summary>Estilos de face tentados para pintar o canal, em ordem: o laranja da
        /// biblioteca padrão da SE (nome em inglês e em português). É a cor que o Carlos usa
        /// para vedação.</summary>
        private static readonly string[] SealStyles = { "Orange", "Laranja" };

        /// <summary>Criado só se a peça não tiver nenhum de <see cref="SealStyles"/>.</summary>
        private const string SealFallbackStyle = "AutoEDM_Vedacao";
        private static readonly System.Drawing.Color SealFallbackColor = System.Drawing.Color.FromArgb(255, 128, 0);

        /// <summary>Igual ao <see cref="Cut(object, ORingTarget, ORingGrooveSpec, double)"/>, e
        /// devolve o nome que a feature recebeu na árvore (null se não deu para renomear).</summary>
        public static bool Cut(dynamic app, ORingTarget target, ORingGrooveSpec spec, double grooveOffsetMm, out string featureName)
        {
            featureName = null;
            if (target == null || !target.Ok) { Log.Warn("Canal de O'ring: seleção inválida."); return false; }
            if (spec == null) { Log.Warn("Canal de O'ring: sem alojamento calculado."); return false; }

            dynamic doc0;
            try { doc0 = app.ActiveDocument; }
            catch (Exception e) { Log.Warn("Canal de O'ring: sem documento ativo — " + e.GetBaseException().Message); return false; }

            // AMBIENTE (premissa de 2026-09-08, ver ModelingEnvironment): este recurso é um
            // esboço + corte revolvido, e `ProfileSets` só produz esboço ORDENADO. Numa peça
            // SÍNCRONA o esboço fica órfão no nó "Ordenado" e o usuário não consegue apagar —
            // foi o que sujou a peça do Carlos. Em ORDENADO o esboço é filho legítimo do
            // recurso: some junto quando o usuário apaga o canal. Por isso o comando exige
            // ORDENADO em vez de tentar limpar a sujeira depois.
            if (!ModelingEnvironment.Require(doc0, ModelingEnv.Ordered, "Canal de O'ring")) return false;

            bool cutOk = false;
            int facesAfterCut = -1;          // conferido no finally: o canal sobreviveu à limpeza?
            dynamic modelRef = null;
            object refAxisMade = null;       // o eixo de revolução vira objeto próprio no documento
            var scope = new SketchScope(doc0, "Canal de O'ring");
            AxisSketch sketch = null;
            try
            {
                dynamic doc = doc0;
                const int mode = 2; // garantido pelo Require acima
                dynamic model = doc.Models.Item(1);
                modelRef = model;
                int facesBefore = FaceCount(model);
                Log.Info($"Canal de O'ring: modelagem ORDENADA, {facesBefore} face(s) no corpo antes.");

                double[] axisDir = ProfilePlaneFrame.AxisVector(target.AxisIndex);

                // CAMINHO PRINCIPAL: coroa circular CONCÊNTRICA à aresta, extrudada a partir de
                // uma face plana da peça — é o que acompanha o furo quando ele MUDA DE Ø (ver
                // ExtrudedGroove). O corte revolvido abaixo fica só como reserva.
                AxisSketch exSketch;
                bool exFeatureMade;
                object cut = ExtrudedGroove(doc, scope, model, target, spec, grooveOffsetMm, axisDir,
                                            out exSketch, out exFeatureMade);
                if (cut != null) sketch = exSketch;
                else if (exFeatureMade)
                {
                    Log.Warn("Canal de O'ring: o recorte extrudado nasceu com falha — não tento o revolvido em cima " +
                             "(apagar a feature falhada costuma desconectar o documento).");
                    return false;
                }
                else
                {
                Log.Warn("  [extrudado] indisponível para esta aresta — usando o corte REVOLVIDO (acompanha o furo " +
                         "movido, mas NÃO a mudança de Ø).");
                // TIPADO de propósito: o retorno de um método chamado com argumento `dynamic`
                // sai dynamic, e aí o compilador perde a análise dos parâmetros [out] adiante.
                // PRIMEIRO o plano AMARRADO à aresta que o usuário clicou; o plano solto (base ou
                // paralelo a ela por distância fixa) só como último recurso — ver OpenSketchOnEdge.
                sketch = OpenSketchOnEdge(doc, scope, target.Edge, target.CenterMm, axisDir)
                      ?? OpenSketchThroughAxis(doc, scope, target.CenterMm, axisDir);
                if (sketch == null) { Log.Warn("Canal de O'ring: não achei um plano de esboço que contenha o eixo."); return false; }
                if (!sketch.Associative)
                    Log.Warn("  ATENÇÃO: esboço num plano SEM vínculo com a peça — o canal NÃO vai acompanhar " +
                             "edições no síncrono (mover/redimensionar o furo deixa o canal para trás ou o quebra).");

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
                var rectLines = DrawClosedPolygon(profile, frame, corners);
                if (rectLines == null) return false;
                // Cantos LIGADOS: desenhadas ponta com ponta por coordenada, as linhas NÃO ficam
                // conectadas — na 1ª edição o perfil abre ("elementos desconectados", visto ao vivo).
                object rel2d = Prop((object)profile, "Relations2d");
                int corners2d = 0;
                if (rel2d != null)
                    for (int i = 0; i < 4; i++)
                        if (Relate(rel2d, "AddKeypoint", rectLines[i], igLineEnd, rectLines[(i + 1) % 4], igLineStart)) corners2d++;
                Log.Info($"  cantos do perfil ligados: {corners2d}/4.");

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
                refAxisMade = refAxis;

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

                cut = Revolve(model, (object)profile, refAxis, mode)
                   ?? SubtractRevolvedBody(doc, model, (object)profile, refAxis, mode);
                }

                int facesAfter = FaceCount(model);
                facesAfterCut = facesAfter;
                bool ok = cut != null && !FeatureFailed(cut) && facesAfter > facesBefore;
                Log.Info($"  {facesAfter} face(s) no corpo depois (antes {facesBefore}); Status={StatusOf(cut)}.");
                if (ok) Log.Info("Canal de O'ring CRIADO ✓");
                else Log.Warn("Canal de O'ring: o corte não vingou — nenhuma forma de corte do modo ORDENADO foi " +
                              "aceita. O AutoEDM NÃO tenta os métodos do outro modo de propósito: isso criaria um " +
                              "recurso no outro ambiente, que a SE não deixa apagar.");
                cutOk = ok;
                if (ok)
                {
                    featureName = NameFeature((object)doc, (object)model, cut, spec);
                    PaintFeature((object)doc, cut);
                    HideFeatureSketch(cut);
                }
                return ok;
            }
            catch (Exception e)
            {
                Log.Warn("Canal de O'ring falhou: " + e.GetBaseException().Message);
                return false;
            }
            finally
            {
                // Corte OK: aposentar o esboço e o plano (ver RetireSketch). Corte falhou:
                // ninguém é dono de nada, o escopo apaga tudo e CONFERE.
                //
                // A limpeza é ENGOLIDA de propósito: ela é cosmética (esconder curvas), e o canal
                // já está na peça quando ela roda. Deixá-la propagar foi o que fez o comando
                // relatar "0 criado(s), 1 falha(s)" sobre três canais que tinham sido cortados
                // com Status=igFeatureOK — o erro da limpeza virou o veredito da operação.
                if (cutOk && sketch != null)
                {
                    try { RetireSketch(doc0, scope, sketch, refAxisMade, modelRef, facesAfterCut); }
                    catch (Exception e)
                    {
                        Log.Warn("  o canal FOI criado; só a limpeza do esboço falhou — " +
                                 e.GetBaseException().Message + " (o esboço pode ter ficado visível sobre o canal).");
                    }
                }

                try { scope.Dispose(); }   // sondagens (e, se o corte falhou, o esboço definitivo)
                catch (Exception e) { Log.Warn("  limpeza dos esboços de sondagem falhou — " + e.GetBaseException().Message); }
            }
        }

        /// <summary>
        /// O QUE FAZER COM O ESBOÇO DEPOIS QUE O CANAL NASCEU (Carlos, 2026-09-08, 2ª rodada).
        ///
        /// A 1ª rodada simplesmente DEIXAVA o esboço na peça, por ser filho do recurso em
        /// modelagem ordenada. Só que na tela ele fica como um punhado de curvas pretas em cima
        /// do canal — e <b>o usuário não consegue ocultá-lo</b>: <c>ProfileSet</c> NÃO tem
        /// propriedade <c>Visible</c> (conferido no dump da typelib, SE 223). Quem tem
        /// <c>Visible</c> é o <c>Profile</c> lá dentro, e é ele que desenha as curvas. Por isso
        /// esconder o PERFIL vem primeiro, e sempre: é o único passo que resolve o que se vê.
        ///
        /// O esboço NÃO é apagado — e é aqui que estava o bug de 2026-09-10 ("as features são
        /// criadas mas com erro e não aparecem no modelo"). O <c>Cut</c> EXIGE modelagem
        /// ORDENADA justamente para o esboço ter dono: ele é filho do corte revolvido. Apagá-lo
        /// arranca o perfil de baixo do recurso que acabou de nascer, e o canal vira uma feature
        /// com erro que não desenha nada — exatamente o sintoma relatado. O log ainda dizia
        /// "CRIADO ✓" porque o corte REALMENTE tinha dado certo (Status=igFeatureOK, faces
        /// 3 → 7); quem o destruía era esta limpeza, logo depois.
        ///
        /// A regra do projeto já era essa desde 2026-09-08 — "em ORDENADO o esboço é filho do
        /// recurso, NÃO apagar" — e o comentário no topo do <c>Cut</c> a repete. Só esta função
        /// não tinha sido alinhada. Esconder o perfil resolve o que incomodava de verdade (as
        /// curvas pretas sobre o canal), e o esboço some sozinho quando o usuário apaga o canal.
        ///
        /// O plano temporário, pela mesma razão, é só ESCONDIDO: ele é referência do recurso
        /// (mesma lição já registrada no BlankModeler).
        /// </summary>
        private static void RetireSketch(dynamic doc, SketchScope scope, AxisSketch sketch,
            object refAxis, dynamic model, int facesAfterCut)
        {
            // PRIMEIRA coisa, antes de qualquer passo cosmético: tirar o esboço do escopo. O que
            // sobra no escopo é APAGADO no Dispose, e este esboço tem dono (o corte ordenado).
            // Se um dos passos abaixo estourasse antes disto, o Dispose levaria o canal junto —
            // e o passo que pode estourar é exatamente o que mexe em proxy COM recém-regenerado.
            //
            // `(object)` de propósito: `AxisSketch.ProfileSet` é `dynamic`, e passar dynamic a um
            // método liga um call site que precisa referenciar o proxy COM na hora de amarrar;
            // com o proxy desconectado isso lança RPC_E_DISCONNECTED ANTES de o método rodar.
            scope.Release((object)sketch.ProfileSet);

            try { sketch.Profile.Visible = false; Log.Info("  esboço do canal: perfil ESCONDIDO (Profile.Visible = false)."); }
            catch (Exception e) { Log.Warn("  esboço do canal: não deu para esconder o perfil — " + e.GetBaseException().Message); }

            // O eixo de revolução não é só uma linha do esboço: `SetAxisOfRevolution` devolve um
            // RefAxis, objeto PRÓPRIO do documento (e com `Visible` próprio, conferido no dump).
            // Esconder só o perfil deixaria esse traço na tela.
            if (refAxis != null)
            {
                try { ((dynamic)refAxis).Visible = false; Log.Info("  esboço do canal: eixo de revolução (RefAxis) ESCONDIDO."); }
                catch (Exception e) { Log.Warn("  esboço do canal: não deu para esconder o RefAxis — " + e.GetBaseException().Message); }
            }

            Log.Info("  esboço do canal MANTIDO (é filho do corte ordenado — apagá-lo quebraria o canal) " +
                     "e ESCONDIDO; some junto quando você apagar o canal.");

            // Rede de segurança: se por algum caminho o canal tiver sumido entre o corte e aqui,
            // isso tem de gritar, não passar por "CRIADO ✓".
            int now = FaceCount(model);
            if (facesAfterCut > 0 && now >= 0 && now < facesAfterCut)
                Log.Error($"  ATENÇÃO: o canal perdeu faces depois do corte ({facesAfterCut} → {now}). " +
                          "Desfaça (Ctrl+Z) e me mande este log.");

            if (sketch.PlaneIsTemporary) scope.HideTempPlane((object)sketch.Plane);
        }

        /// <summary>
        /// Dá à feature o nome do anel com número de instância (ver <see cref="ORingGrooveNaming"/>).
        /// Os nomes em uso saem da árvore INTEIRA da peça, lidos na hora — é o que numera certo
        /// mesmo quando o usuário renomeou ou apagou canais entre uma criação e outra.
        /// Cosmético: falha só no log.
        /// </summary>
        private static string NameFeature(object doc, object model, object feature, ORingGrooveSpec spec)
        {
            string name = null;
            try
            {
                name = ORingGrooveNaming.NextName(spec.Ring, ExistingFeatureNames(doc, model));
                ((dynamic)feature).Name = name;
                Log.Info($"  feature renomeada: '{name}'.");
                return name;
            }
            catch (Exception e)
            {
                Log.Warn($"  não deu para renomear a feature{(name != null ? " para '" + name + "'" : "")} (cosmético, segue) — " +
                         e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>Nomes de todas as features da peça — o da árvore (<c>EdgebarName</c>) e o
        /// interno (<c>Name</c>), porque é o <c>Name</c> que a gente escreve.</summary>
        private static List<string> ExistingFeatureNames(object doc, object model)
        {
            // As duas coleções: a da árvore e a do modelo. Um nome visto duas vezes não atrapalha
            // (só o MAIOR número importa), e um que faltasse numa delas daria nome repetido.
            var names = new List<string>();
            int collections = 0;
            foreach (object features in new[] { Prop(doc, "DesignEdgebarFeatures"), Prop(model, "Features") })
            {
                if (features == null) continue;
                collections++;
                int count = 0;
                try { count = Convert.ToInt32(Prop(features, "Count")); } catch { }
                for (int i = 1; i <= count; i++)
                {
                    object f;
                    try { f = features.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, features, new object[] { i }); }
                    catch { continue; }
                    foreach (string prop in new[] { "Name", "EdgebarName" })
                        if (Prop(f, prop) is string s && s.Length > 0) names.Add(s);
                }
            }
            if (collections == 0) Log.Warn("  sem coleção de features para numerar o nome — sai com o número 1.");
            return names;
        }

        /// <summary>Propriedade COM por nome; null se o objeto não a expõe.</summary>
        private static object Prop(object com, string name)
        {
            if (com == null) return null;
            try { return com.GetType().InvokeMember(name, BindingFlags.GetProperty, null, com, null); }
            catch { return null; }
        }

        /// <summary>Pinta as faces do canal com o laranja de vedação. Cosmético: falha só no log.</summary>
        private static void PaintFeature(object doc, object feature)
        {
            try
            {
                object[] faces = AutoEDM.Electrode.ModelingHelpers.GetFeatureFaces(feature);
                if (faces.Length == 0) { Log.Warn("  canal sem faces legíveis — não foi pintado."); return; }
                AutoEDM.Electrode.FaceColorPainter.PaintWithNamedStyle(
                    doc, faces, SealStyles, SealFallbackStyle, SealFallbackColor);
            }
            catch (Exception e) { Log.Warn("  pintura do canal falhou (cosmético, segue) — " + e.GetBaseException().Message); }
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

        /// <summary>Desenha o contorno fechado e devolve as linhas (null se um canto não converteu).</summary>
        private static List<object> DrawClosedPolygon(dynamic profile, ProfilePlaneFrame frame, double[][] corners3d)
        {
            var xs = new double[corners3d.Length];
            var ys = new double[corners3d.Length];
            for (int i = 0; i < corners3d.Length; i++)
                if (!frame.TryTo2dMm((object)profile, corners3d[i], out xs[i], out ys[i]))
                { Log.Warn($"Canal de O'ring: canto {i} não converteu para o esboço."); return null; }

            dynamic lines = profile.Lines2d;
            var made = new List<object>(corners3d.Length);
            for (int i = 0; i < corners3d.Length; i++)
            {
                int j = (i + 1) % corners3d.Length;
                made.Add((object)lines.AddBy2Points(Units.MmToM(xs[i]), Units.MmToM(ys[i]), Units.MmToM(xs[j]), Units.MmToM(ys[j])));
            }
            return made;
        }

        // ================================================================== corte EXTRUDADO

        private const int igPlaneGeom = -1909484335;   // GNTTypePropertyConstants.igPlane

        /// <summary>
        /// O CANAL COMO COROA CIRCULAR EXTRUDADA — o caminho que acompanha a peça quando o furo
        /// MUDA DE DIÂMETRO (Carlos, 2026-09-21, 4 rodadas ao vivo com o corte revolvido).
        ///
        /// Por que não o revolvido: ele exige um esboço num plano que CONTENHA o eixo. O único
        /// plano desses que se consegue amarrar à aresta é o normal à curva, com origem EM CIMA
        /// da aresta — e quando o Ø muda, esse ponto (e o referencial do plano) sai do lugar.
        /// Nem amarrando o esboço à mão o Carlos conseguiu manter o canal no eixo. Amarras por
        /// API (colinear + AddSet) não seguraram; cotas de distância nasceram com o valor errado
        /// e ENTORTARAM o canal.
        ///
        /// Aqui o esboço fica num plano PERPENDICULAR ao eixo, preso a uma face plana da peça
        /// que toca a aresta (a própria face, no canal de face; um plano paralelo a ela, no de
        /// eixo/furo). A aresta incluída nesse plano é um CÍRCULO, e os dois círculos do canal
        /// ficam CONCÊNTRICOS a ele (Relations2d.AddConcentric) com cota de diâmetro
        /// (Dimensions.AddCircularDiameter — mede o próprio círculo, não há ponto de escolha
        /// para errar). É o jeito clássico de fazer isso à mão na SE.
        ///
        /// Devolve a feature criada e boa, ou null. <paramref name="featureCreated"/> = a SE
        /// chegou a criar uma feature (mesmo falhada) — nesse caso não se tenta outro caminho.
        /// </summary>
        private static object ExtrudedGroove(dynamic doc, SketchScope scope, dynamic model, ORingTarget target,
            ORingGrooveSpec spec, double grooveOffsetMm, double[] axisDir, out AxisSketch sketch, out bool featureCreated)
        {
            sketch = null;
            featureCreated = false;
            if (target.Edge == null) { Log.Warn("  [extrudado] sem a aresta COM."); return null; }

            object face = PlanarFaceAtEdge(model, target, axisDir);
            if (face == null) { Log.Warn("  [extrudado] a aresta não toca nenhuma face PLANA perpendicular ao eixo."); return null; }

            // Onde o esboço fica (coordenada no eixo), quanto extruda, e os dois raios.
            int dir = target.MaterialDirection;
            double margin = Math.Max(1.0, spec.Ring.CrossSection);   // folga "no ar", fora do material
            double start, depth, rIn, rOut;
            switch (spec.Kind)
            {
                case GrooveKind.AxialFace:
                    start = target.EdgeAxialMm; depth = spec.Depth;
                    rIn = spec.GrooveInnerDiameter / 2.0; rOut = spec.GrooveOuterDiameter / 2.0;
                    break;
                case GrooveKind.RadialExternal:   // eixo: do fundo até passar do costado
                    // Esboço no CENTRO do canal e extrusão SIMÉTRICA: a largura sai sem depender
                    // de sentido (ver a escolha do lado, adiante).
                    start = target.EdgeAxialMm + dir * grooveOffsetMm; depth = spec.Width;
                    rIn = spec.GrooveBottomDiameter / 2.0; rOut = spec.SealingDiameter / 2.0 + margin;
                    break;
                default:                          // furo: de dentro do furo até o fundo
                    start = target.EdgeAxialMm + dir * grooveOffsetMm; depth = spec.Width;
                    rIn = Math.Max(0.1, spec.SealingDiameter / 2.0 - margin); rOut = spec.GrooveBottomDiameter / 2.0;
                    break;
            }
            Log.Info($"  [extrudado] coroa Ø {2 * rIn:0.000} a {2 * rOut:0.000} mm, esboço em {target.AxisName}={start:0.000}, " +
                     $"corte de {depth:0.000} mm para {(dir > 0 ? "+" : "−")}{target.AxisName}.");
            if (rOut - rIn <= 1e-6 || depth <= 1e-6) { Log.Warn("  [extrudado] coroa degenerada."); return null; }

            // O plano do esboço: a própria face, ou um paralelo a ela afastado até o início do canal.
            double[] zero = new double[3];
            double[] center = Point(target, axisDir, zero, start, 0.0);
            Func<ProfilePlaneFrame, bool> right = f =>
                Math.Abs(ProfilePlaneFrame.Dot(f.Normal, axisDir)) > 0.999 && Math.Abs(f.SignedDistance(center)) < 0.01;

            double offset = Math.Abs(start - target.EdgeAxialMm);
            if (offset < 1e-4)
            {
                sketch = TryOpenSketchWhere(doc, scope, face, false, right);
                if (sketch != null) Log.Info("  [extrudado] esboço na PRÓPRIA face plana.");
            }
            if (sketch == null)
            {
                foreach (int side in new[] { igRight, igLeft })
                {
                    object plane = null;
                    try
                    {
                        plane = (object)doc.RefPlanes.AddParallelByDistance(face, Units.MmToM(offset), side,
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                    }
                    catch (Exception e) { Log.Warn($"  [extrudado] AddParallelByDistance(face, {offset:0.###}, {side}): " + e.GetBaseException().Message); }
                    if (plane == null) continue;
                    scope.TrackTempPlane(plane);
                    sketch = TryOpenSketchWhere(doc, scope, plane, true, right);
                    if (sketch != null) { Log.Info($"  [extrudado] esboço num plano paralelo à face, a {offset:0.###} mm (lado {side})."); break; }
                    // Lado errado: apagar JÁ, com o documento são. Deixado para a limpeza final,
                    // ele ficava VISÍVEL na peça (a exclusão tardia deu RPC_E_DISCONNECTED ao vivo).
                    scope.Release(plane);
                    try { ((dynamic)plane).Delete(); Log.Info($"  [extrudado] plano do lado {side} caiu no lugar errado — apagado."); }
                    catch (Exception e)
                    {
                        Log.Warn($"  [extrudado] plano do lado {side} (lugar errado) não apagou: " + e.GetBaseException().Message + " — escondendo.");
                        try { ((dynamic)plane).Visible = false; } catch { }
                    }
                }
            }
            if (sketch == null) { Log.Warn("  [extrudado] não consegui um plano de esboço preso à face."); return null; }
            sketch.Associative = true;

            object profile = (object)sketch.Profile;
            ProfilePlaneFrame frame = sketch.Frame;

            // A aresta dentro do esboço — achada pela DIFERENÇA (o [out] do IncludeEdge vem vazio).
            var before = SketchElements(profile);
            try
            {
                object[] args = { target.Edge, null };
                var mod = new ParameterModifier(2);
                mod[1] = true;
                profile.GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, profile, args, new[] { mod }, null, null);
            }
            catch (Exception e) { Log.Warn("  [extrudado] IncludeEdge falhou: " + e.GetBaseException().Message); }
            var added = NewElements(before, SketchElements(profile));
            Log.Info($"  [extrudado] aresta incluída: {added.Count} elemento(s) — " +
                     string.Join(", ", added.ConvertAll(a => a.Key + ":" + (AutoEDM.Com.ComDiagnostics.TypeNameOf(a.Value) ?? "?"))) + ".");
            object refCircle = added.Count == 1 && (added[0].Key == "Circles2d" || added[0].Key == "Arcs2d") ? added[0].Value : null;
            if (refCircle != null)
            {
                try { ((dynamic)profile).ToggleConstruction(refCircle); }
                catch (Exception e) { Log.Warn("  [extrudado] a aresta incluída não virou construção: " + e.GetBaseException().Message); }
            }
            else
            {
                if (added.Count > 0) DeleteAll(added.ConvertAll(a => a.Value));
                Log.Warn("  [extrudado] a aresta não entrou como UM círculo — o canal fica preso à face, mas NÃO ao Ø.");
            }

            // Os dois círculos do canal.
            double cx, cy;
            if (!frame.TryTo2dMm(profile, center, out cx, out cy)) { Log.Warn("  [extrudado] centro não converteu para o esboço."); return null; }
            object inner, outer;
            try
            {
                dynamic circles = ((dynamic)profile).Circles2d;
                inner = (object)circles.AddByCenterRadius(Units.MmToM(cx), Units.MmToM(cy), Units.MmToM(rIn));
                outer = (object)circles.AddByCenterRadius(Units.MmToM(cx), Units.MmToM(cy), Units.MmToM(rOut));
            }
            catch (Exception e) { Log.Warn("  [extrudado] círculos não criados: " + e.GetBaseException().Message); return null; }

            if (refCircle != null)
            {
                object relations = Prop(profile, "Relations2d");
                int conc = 0;
                if (relations != null)
                {
                    if (Relate(relations, "AddConcentric", inner, refCircle)) conc++;
                    if (Relate(relations, "AddConcentric", outer, refCircle)) conc++;
                }
                Log.Info($"  [extrudado] concêntricos à aresta: {conc}/2.");
            }

            object dims = Prop(profile, "Dimensions");
            if (dims != null)
            {
                try { dims.GetType().InvokeMember("Constraint", BindingFlags.SetProperty, null, dims, new object[] { true }); }
                catch (Exception e) { Log.Warn("  [extrudado] Dimensions.Constraint = true falhou: " + e.GetBaseException().Message); }
                DiameterDim(dims, inner, 2 * rIn);
                DiameterDim(dims, outer, 2 * rOut);
            }

            // TRAVA: o contorno tem de ser os 2 círculos e mais nada.
            var strays = new List<string>();
            foreach (var el in SketchElements(profile))
            {
                if (el.Key == "Points2d" || SameCom(el.Value, inner) || SameCom(el.Value, outer)) continue;
                bool construction = false;
                try { construction = Convert.ToBoolean(profile.GetType().InvokeMember("IsConstructionElement", BindingFlags.InvokeMethod, null, profile, new[] { el.Value })); }
                catch { }
                if (!construction) strays.Add(el.Key);
            }
            if (strays.Count > 0)
            {
                Log.Warn($"  [extrudado] {strays.Count} elemento(s) estranho(s) no contorno ({string.Join(", ", strays)}) — desisto do extrudado.");
                return null;
            }

            object under = Prop((object)sketch.ProfileSet, "IsUnderDefined");
            // A coroa é um laço DENTRO do outro: sem igProfileAllowNested (8192) a SE recusa o
            // perfil (End(9) = −113, ao vivo em 2026-09-21) e o recorte nasce igFeatureFailed —
            // qualquer que seja o lado. Fechado (1) + sem auto-interseção (8) + aninhado (8192).
            const int criteria = 1 | 8 | 8192;
            int validation = -99;
            try { validation = Convert.ToInt32(((dynamic)profile).End(criteria)); }
            catch (Exception e) { Log.Warn("  [extrudado] Profile.End falhou: " + e.GetBaseException().Message); }
            Log.Info($"  [extrudado] perfil End({criteria}) = {validation}{(validation == 0 ? " (válido)" : " (NÃO válido)")}; sub-definido: {under ?? "ilegível"}.");

            // SENTIDO: a 1ª rodada ao vivo (2026-09-21) escolheu o lado pela normal do referencial
            // do esboço e o recorte nasceu igFeatureFailed nos dois casos — o lado 1/2 da SE não
            // é, comprovadamente, o que eu deduzia. Em vez de adivinhar, extrusão SIMÉTRICA
            // (lado 3): no canal de eixo/furo o esboço está no centro do canal; no de face, a
            // metade de fora corta ar. Fica logada a normal do plano segundo a própria SE.
            const int igSymmetric = 3;
            LogPlaneNormal(sketch.Plane, frame);
            bool faceGroove = spec.Kind == GrooveKind.AxialFace;
            double distance = faceGroove ? 2.0 * depth : depth;   // hipótese: distância simétrica = TOTAL

            object cut = null;
            try
            {
                var arr = new[] { (SolidEdgePart.Profile)profile };
                object cutouts = (object)model.ExtrudedCutouts;
                cut = cutouts.GetType().InvokeMember("AddFiniteMulti", BindingFlags.InvokeMethod, null, cutouts,
                    new object[] { 1, arr, igSymmetric, Units.MmToM(distance) });
            }
            catch (Exception e) { Log.Warn("  [extrudado] ExtrudedCutouts.AddFiniteMulti falhou: " + e.GetBaseException().Message); return null; }
            if (cut == null) { Log.Warn("  [extrudado] AddFiniteMulti devolveu nulo."); return null; }

            featureCreated = true;
            if (FeatureFailed(cut))
            {
                // Apagar a FEATURE leva o esboço junto (é filho dela). Antes, o esboço era
                // apagado pela limpeza do escopo e a feature ficava na árvore dizendo "o perfil
                // não existe mais" — lixo que o usuário tinha de tirar à mão.
                Log.Warn($"  [extrudado] feature com Status de falha ({StatusOf(cut)}) — apagando a feature (com o esboço).");
                scope.Release((object)sketch.ProfileSet);
                try { ((dynamic)cut).Delete(); }
                catch (Exception e) { Log.Warn("  [extrudado] não deu para apagar a feature falhada: " + e.GetBaseException().Message); }
                return null;
            }

            // Conferência: extensão AXIAL real das faces novas contra o esperado — é o que diz se a
            // distância simétrica é o total (hipótese) ou por lado.
            double expected = depth;
            double got = AxialExtentMm(cut, target.AxisIndex);
            string verdict = double.IsNaN(got) ? "ilegível"
                : Math.Abs(got - expected) < 0.02 ? "✓ distância simétrica = TOTAL"
                : Math.Abs(got - 2 * expected) < 0.02 ? "✗ saiu o DOBRO — a distância simétrica é POR LADO"
                : "✗ não bate";
            Log.Info($"  [extrudado] recorte extrudado criado (simétrico, {distance:0.000} mm); extensão axial medida " +
                     $"{got:0.000} mm, esperado {expected:0.000} mm — {verdict}.");
            return cut;
        }

        /// <summary>Extensão ao longo do eixo das faces da feature (mm); NaN se ilegível.</summary>
        private static double AxialExtentMm(object feature, int axis)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (object f in AutoEDM.Electrode.ModelingHelpers.GetFeatureFaces(feature))
            {
                double[] mn, mx;
                if (!FaceGeometry.TryGetRangeMm(f, out mn, out mx)) continue;
                lo = Math.Min(lo, mn[axis]);
                hi = Math.Max(hi, mx[axis]);
            }
            return hi >= lo ? hi - lo : double.NaN;
        }

        /// <summary>Normal do plano segundo a SE (<c>RefPlane.GetNormal</c>) ao lado da do
        /// referencial do esboço — só diagnóstico, para a regra do lado 1/2.</summary>
        private static void LogPlaneNormal(object plane, ProfilePlaneFrame frame)
        {
            double[] n;
            string why;
            string se = FaceGeometry.TryOneArrayOut(plane, "GetNormal", out n, out why) && n != null && n.Length >= 3
                ? $"({n[0]:0.###}, {n[1]:0.###}, {n[2]:0.###})" : "ilegível (" + why + ")";
            Log.Info($"  [extrudado] normal do plano: SE {se}; referencial do esboço " +
                     $"({frame.Normal[0]:0.###}, {frame.Normal[1]:0.###}, {frame.Normal[2]:0.###}).");
        }

        /// <summary>
        /// Esconde o esboço PELA FEATURE. No recorte extrudado, <c>Profile.Visible = false</c> no
        /// proxy que desenhou o esboço foi aceito e o esboço continuou na tela (círculo Ø17,434
        /// selecionado ao vivo, 2026-09-21). O que resolveu, ao vivo no mesmo dia, foi
        /// <c>ShowDimensions = false</c> na feature; <c>feature.Profile</c> veio nulo e
        /// <c>GetProfiles</c> deu DISP_E_TYPEMISMATCH — ficam como tentativa, sem custo.
        /// Cosmético: falha só no log.
        /// </summary>
        private static void HideFeatureSketch(object feature)
        {
            var profiles = new List<object>();
            object single = Prop(feature, "Profile");
            if (single != null) profiles.Add(single);
            try
            {
                // Marcador TIPADO: object[] vira SAFEARRAY(VARIANT) e dá DISP_E_TYPEMISMATCH (errors.md).
                object[] args = { 0, new SolidEdgePart.Profile[0] };
                var mod = new ParameterModifier(2);
                mod[0] = true;
                mod[1] = true;
                feature.GetType().InvokeMember("GetProfiles", BindingFlags.InvokeMethod, null, feature, args,
                    new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                if (args[1] is Array arr)
                    foreach (object p in arr)
                        if (p != null && !profiles.Exists(q => SameCom(q, p))) profiles.Add(p);
            }
            catch (Exception e) { Log.Info("  esboço da feature: GetProfiles indisponível — " + e.GetBaseException().Message); }

            int hidden = 0;
            foreach (object p in profiles)
            {
                try
                {
                    p.GetType().InvokeMember("Visible", BindingFlags.SetProperty, null, p, new object[] { false });
                    if (Prop(p, "Visible") is bool v && !v) hidden++;
                }
                catch (Exception e) { Log.Warn("  esboço da feature: Visible = false falhou — " + e.GetBaseException().Message); }
            }
            try { feature.GetType().InvokeMember("ShowDimensions", BindingFlags.SetProperty, null, feature, new object[] { false }); }
            catch (Exception e) { Log.Info("  esboço da feature: ShowDimensions indisponível — " + e.GetBaseException().Message); }
            Log.Info($"  esboço da feature: {hidden}/{profiles.Count} perfil(is) escondido(s) pela feature (conferido); cotas ocultas.");
        }

        /// <summary>Cota de diâmetro do círculo, conferida contra o esperado (mm).</summary>
        private static void DiameterDim(object dims, object circle, double expectedMm)
        {
            try
            {
                object d = dims.GetType().InvokeMember("AddCircularDiameter", BindingFlags.InvokeMethod, null, dims, new[] { circle });
                object v = Prop(d, "Value");
                double got = v == null ? double.NaN : Convert.ToDouble(v) * 1000.0;
                if (double.IsNaN(got) || Math.Abs(got - expectedMm) > 0.005)
                    Log.Warn($"  [extrudado] cota de Ø nasceu com {got:0.###} mm, esperado {expectedMm:0.###} mm.");
                else
                    Log.Info($"  [extrudado] cota Ø {got:0.###} mm ✓");
            }
            catch (Exception e) { Log.Warn("  [extrudado] Dimensions.AddCircularDiameter falhou: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// A face PLANA perpendicular ao eixo, no plano da aresta. Em ordem: a face que o usuário
        /// clicou (canal de face); a face do corpo que está no plano da aresta e cobre o círculo
        /// (pela caixa envolvente — canal de eixo/furo); e por último <c>Edge.GetFaces</c>, que
        /// ao vivo (2026-09-21) deu DISP_E_TYPEMISMATCH e fez o extrudado nunca rodar.
        /// </summary>
        private static object PlanarFaceAtEdge(dynamic model, ORingTarget target, double[] axisDir)
        {
            if (target.Face != null && IsPlanarNormalTo(target.Face, axisDir))
            {
                Log.Info("  [extrudado] face plana: a que você clicou.");
                return target.Face;
            }

            object faces = null;
            try { faces = (object)model.Body.Faces[1]; }   // igQueryAll
            catch (Exception e) { Log.Warn("  [extrudado] Body.Faces ilegível: " + e.GetBaseException().Message); }
            int n = 0;
            try { n = Convert.ToInt32(Prop(faces, "Count")); } catch { }
            double r = target.EdgeDiameterMm / 2.0;
            int a = target.AxisIndex, u = (a + 1) % 3, v = (a + 2) % 3;
            for (int i = 1; i <= n; i++)
            {
                object f;
                try { f = faces.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, faces, new object[] { i }); }
                catch { continue; }
                if (!IsPlanarNormalTo(f, axisDir)) continue;
                double[] mn, mx;
                if (!FaceGeometry.TryGetRangeMm(f, out mn, out mx)) continue;
                const double tol = 0.02;
                bool onEdgePlane = Math.Abs(mn[a] - target.EdgeAxialMm) < tol && Math.Abs(mx[a] - target.EdgeAxialMm) < tol;
                bool coversCircle = mn[u] <= target.CenterMm[u] - r + tol && mx[u] >= target.CenterMm[u] + r - tol &&
                                    mn[v] <= target.CenterMm[v] - r + tol && mx[v] >= target.CenterMm[v] + r - tol;
                if (onEdgePlane && coversCircle)
                {
                    Log.Info($"  [extrudado] face plana: Body.Faces item {i} (no plano da aresta, cobre o círculo).");
                    return f;
                }
            }

            return PlanarFaceOfEdge(target.Edge, axisDir);
        }

        private static bool IsPlanarNormalTo(object face, double[] axisDir)
        {
            try
            {
                object geom = Prop(face, "Geometry");
                if (geom == null || Convert.ToInt32(Prop(geom, "Type")) != igPlaneGeom) return false;
                double[] nv;
                string why;
                if (!FaceGeometry.TryOneArrayOut(geom, "GetNormalVector", out nv, out why) || nv == null || nv.Length < 3) return false;
                double len = Math.Sqrt(nv[0] * nv[0] + nv[1] * nv[1] + nv[2] * nv[2]);
                return len > 1e-12 && Math.Abs(ProfilePlaneFrame.Dot(nv, axisDir)) / len > 0.999;
            }
            catch { return false; }
        }

        /// <summary>A face PLANA, perpendicular ao eixo, entre as faces que a aresta toca
        /// (<c>Edge.GetFaces</c>, mesmo marshal do SharpCornerProbe).</summary>
        private static object PlanarFaceOfEdge(object edge, double[] axisDir)
        {
            Array faces;
            try
            {
                // Marcador TIPADO (errors.md): com object[0] deu DISP_E_TYPEMISMATCH ao vivo.
                object[] args = { 0, new SolidEdgeGeometry.Face[0] };
                var mod = new ParameterModifier(2);
                mod[0] = true;
                mod[1] = true;
                edge.GetType().InvokeMember("GetFaces", BindingFlags.InvokeMethod, null, edge, args,
                    new[] { mod }, System.Globalization.CultureInfo.InvariantCulture, null);
                faces = args[1] as Array;
            }
            catch (Exception e) { Log.Warn("  [extrudado] Edge.GetFaces falhou: " + e.GetBaseException().Message); return null; }
            if (faces == null) return null;

            foreach (object f in faces)
            {
                try
                {
                    object geom = Prop(f, "Geometry");
                    if (geom == null || Convert.ToInt32(Prop(geom, "Type")) != igPlaneGeom) continue;
                    double[] n;
                    string why;
                    if (!FaceGeometry.TryOneArrayOut(geom, "GetNormalVector", out n, out why) || n == null || n.Length < 3) continue;
                    double len = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
                    if (len > 1e-12 && Math.Abs(ProfilePlaneFrame.Dot(n, axisDir)) / len > 0.999) return f;
                }
                catch { }
            }
            return null;
        }

        /// <summary>Abre um esboço no plano (ou face) e só o devolve se o referencial passar no
        /// teste; senão desfaz o esboço.</summary>
        private static AxisSketch TryOpenSketchWhere(dynamic doc, SketchScope scope, object plane, bool planeIsTemporary,
            Func<ProfilePlaneFrame, bool> accept)
        {
            dynamic ps = null;
            try
            {
                ps = scope.AddProfileSet();
                dynamic profile = ps.Profiles.Add(plane);
                var frame = ProfilePlaneFrame.Discover((object)profile);
                if (frame != null && accept(frame))
                    return new AxisSketch { ProfileSet = ps, Profile = profile, Plane = plane, Frame = frame, PlaneIsTemporary = planeIsTemporary };
            }
            catch (Exception e) { Log.Warn("  [extrudado] não deu para abrir o esboço: " + e.GetBaseException().Message); }
            if (ps != null) scope.DropProfileSet((object)ps);
            return null;
        }

        private const int igLineStart = 0, igLineEnd = 1;   // KeypointIndexConstants (reflexão)

        private static readonly string[] SketchCollections =
            { "Lines2d", "Arcs2d", "Circles2d", "Ellipses2d", "EllipticalArcs2d", "BSplineCurves2d", "Points2d" };

        /// <summary>Todos os elementos 2D do esboço, por coleção.</summary>
        private static List<KeyValuePair<string, object>> SketchElements(object profile)
        {
            var all = new List<KeyValuePair<string, object>>();
            foreach (string name in SketchCollections)
            {
                object col = Prop(profile, name);
                if (col == null) continue;
                int n = 0;
                try { n = Convert.ToInt32(Prop(col, "Count")); } catch { }
                for (int i = 1; i <= n; i++)
                {
                    try { all.Add(new KeyValuePair<string, object>(name, col.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, col, new object[] { i }))); }
                    catch { }
                }
            }
            return all;
        }

        /// <summary>O que está em <paramref name="after"/> e não estava em <paramref name="before"/> (identidade COM).</summary>
        private static List<KeyValuePair<string, object>> NewElements(List<KeyValuePair<string, object>> before,
            List<KeyValuePair<string, object>> after)
        {
            var added = new List<KeyValuePair<string, object>>();
            foreach (var a in after)
                if (!before.Exists(b => SameCom(a.Value, b.Value))) added.Add(a);
            return added;
        }

        private static bool SameCom(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            IntPtr pa = IntPtr.Zero, pb = IntPtr.Zero;
            try
            {
                pa = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(a);
                pb = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(b);
                return pa == pb;
            }
            catch { return false; }
            finally
            {
                if (pa != IntPtr.Zero) System.Runtime.InteropServices.Marshal.Release(pa);
                if (pb != IntPtr.Zero) System.Runtime.InteropServices.Marshal.Release(pb);
            }
        }

        private static void DeleteAll(List<object> items)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                try { items[i].GetType().InvokeMember("Delete", BindingFlags.InvokeMethod, null, items[i], null); }
                catch (Exception e) { Log.Warn("  [vínculo] não deu para apagar um elemento da amarra: " + e.GetBaseException().Message); }
            }
            items.Clear();
        }

        private static string Describe(object o)
        {
            if (o == null) return "vazio";
            if (o is Array a) return $"array[{a.Length}]";
            return AutoEDM.Com.ComDiagnostics.TypeNameOf(o) ?? o.GetType().Name;
        }

        private static bool Relate(object relations, string method, params object[] args)
        {
            var full = new object[args.Length + 1];
            Array.Copy(args, full, args.Length);
            full[args.Length] = Type.Missing;   // guaranteed_ok
            try
            {
                relations.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, relations, full);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"  [vínculo] Relations2d.{method} falhou: " + e.GetBaseException().Message);
                return false;
            }
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

            /// <summary>O plano nasce DA ARESTA da peça (e não de um plano base a uma distância
            /// fixa) — é o que faz o canal andar junto quando a peça é editada.</summary>
            public bool Associative;
        }

        private const int igCurveStart = 14, igPivotStart = 3;   // ReferenceElementConstants (dump)

        /// <summary>
        /// ESBOÇO ASSOCIATIVO (Carlos, 2026-09-21: "apesar do recurso ser ordenado, não existe
        /// vínculo do esboço com o modelo, e os alojamentos são destruídos se o modelo for
        /// alterado no síncrono").
        ///
        /// Ser ORDENADO não basta. Um recurso ordenado só se refaz a partir das REFERÊNCIAS que
        /// o esboço dele guarda — e o esboço antigo não guardava nenhuma: plano base (ou paralelo
        /// a ele por uma distância FIXA) e linhas soltas em coordenada absoluta. Quando o furo
        /// anda no síncrono, nada no canal sabe disso: ele fica onde estava, cortando o vazio ou
        /// falhando.
        ///
        /// Aqui o plano nasce da ARESTA circular clicada: <c>RefPlanes.AddNormalToCurve</c> no
        /// início da curva. Um plano normal a um círculo, num ponto dele, CONTÉM o eixo do
        /// círculo — é exatamente o plano de que o corte revolvido precisa. E como o plano
        /// pertence à aresta, o esboço (cujas coordenadas são locais ao plano) vai junto quando
        /// o furo é movido. LIMITE conhecido: o esboço anda RÍGIDO com o ponto da aresta; se o
        /// DIÂMETRO do furo mudar, o eixo desenhado fica fora do eixo real — prender o
        /// retângulo à aresta projetada (<c>Profile.IncludeEdge</c> + <c>Relations2d</c>) é o
        /// passo seguinte, e depende de sondar ao vivo os índices de keypoint.
        ///
        /// ASSINATURA do dump/reflexão (SE 2023), VALIDADA AO VIVO em 2026-09-21 (canal de face
        /// num cilindro Ø20, 1ª tentativa com orientação RefPlanes.Item(1): origem do plano em
        /// cima da aresta, eixo contido, corte igFeatureOK):
        ///   AddNormalToCurve(Curve, PlanePoint, OrientationPlaneOrPivot, PivotOrigin, [Local], [ParentCurve])
        /// Por isso cada tentativa é CONFERIDA pelo referencial do próprio esboço (o plano tem de
        /// conter o eixo), e qualquer falha devolve null para o caminho antigo, com aviso no log.
        /// </summary>
        private static AxisSketch OpenSketchOnEdge(dynamic doc, SketchScope scope, object edge,
            double[] axisPointMm, double[] axisDir)
        {
            if (edge == null) { Log.Warn("  [plano] sem a aresta COM — não dá para ancorar o esboço nela."); return null; }

            object refPlanes;
            try { refPlanes = (object)doc.RefPlanes; }
            catch (Exception e) { Log.Warn("  [plano] RefPlanes inacessível: " + e.GetBaseException().Message); return null; }

            // O plano de orientação só decide para onde aponta o X do esboço; qualquer plano base
            // que o SE aceite serve. Tenta os três até um dar um plano que contenha o eixo.
            for (int i = 1; i <= 3; i++)
            {
                object orient;
                try { orient = (object)doc.RefPlanes.Item(i); } catch { continue; }

                object plane = null;
                try
                {
                    plane = refPlanes.GetType().InvokeMember("AddNormalToCurve", BindingFlags.InvokeMethod, null, refPlanes,
                        new object[] { edge, igCurveStart, orient, igPivotStart, Type.Missing, Type.Missing });
                }
                catch (Exception e)
                {
                    Log.Warn($"  [plano] AddNormalToCurve(aresta, início, RefPlanes.Item({i})): " + e.GetBaseException().Message);
                    if (IsDisconnected(e)) return null;
                }
                if (plane == null) continue;
                scope.TrackTempPlane(plane);   // some no fim se o corte não usar

                AxisSketch s = TryOpenSketch(doc, scope, plane, true, axisPointMm, axisDir);
                if (s != null)
                {
                    s.Associative = true;
                    Log.Info($"  [plano] ASSOCIATIVO: normal à aresta clicada, no início da curva (orientação RefPlanes.Item({i})).");
                    return s;
                }
                Log.Warn($"  [plano] o plano normal à aresta (orientação Item({i})) não contém o eixo — descartado.");
            }
            return null;
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
        private static AxisSketch OpenSketchThroughAxis(dynamic doc, SketchScope scope, double[] axisPointMm, double[] axisDir)
        {
            // UM ESBOÇO POR SONDAGEM — não um esboço para as três (regressão de 2026-09-08,
            // corrigida no mesmo dia com log real): tentei economizar abrindo um único
            // ProfileSet e acrescentando um Profile por plano-base. O primeiro entra; o
            // SEGUNDO `Profiles.Add` no MESMO set devolve E_FAIL. Resultado: os planos 2 e 3
            // nunca eram lidos, nenhum plano continha o eixo e NENHUM canal era criado
            // ("[plano] sondagem falhou: E_FAIL" ×2, em todos os cliques do log 083000).
            // Na prática o ProfileSet do SE é de um perfil só. O que valia da ideia — não
            // apagar esboço em catch mudo — continua: cada sondagem nasce no escopo e morre
            // conferida, logo depois de responder onde o plano está.
            for (int i = 1; i <= 3; i++)
            {
                dynamic basePlane;
                try { basePlane = doc.RefPlanes.Item(i); } catch { continue; }

                var frame = FrameOf(doc, scope, basePlane);
                if (frame == null || !frame.IsParallelToLine(axisDir)) continue;

                double d = frame.SignedDistance(axisPointMm);
                if (Math.Abs(d) < 0.01)
                {
                    var onBase = TryOpenSketch(doc, scope, basePlane, false, axisPointMm, axisDir);
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
                    scope.TrackTempPlane((object)candidate);   // some no fim se o corte não usar

                    var s = TryOpenSketch(doc, scope, candidate, true, axisPointMm, axisDir);
                    if (s != null)
                    {
                        Log.Info($"  [plano] criado a partir de RefPlanes.Item({i}), afastamento {Math.Abs(d):0.###} mm, lado {side}.");
                        return s;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Abre o esboço DEFINITIVO no plano e só o devolve se o referencial confirmar que o
        /// eixo do anel está contido nele. Se não estiver, desfaz o esboço e deixa o chamador
        /// tentar o outro lado.
        /// </summary>
        private static AxisSketch TryOpenSketch(dynamic doc, SketchScope scope, dynamic plane, bool planeIsTemporary,
            double[] axisPointMm, double[] axisDir)
        {
            dynamic ps = null;
            try
            {
                ps = scope.AddProfileSet();
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

            if (ps != null) scope.DropProfileSet((object)ps);
            return null;
        }

        /// <summary>
        /// Referencial de um plano — abre um esboço DESCARTÁVEL, pergunta ao SE onde o plano
        /// está e apaga em seguida. Sondar um plano BASE assim é seguro: plano base não some.
        /// O esboço é próprio (não compartilhado) porque um ProfileSet aceita UM perfil: o
        /// segundo `Profiles.Add` no mesmo set devolve E_FAIL. E a exclusão vai pelo escopo,
        /// que confere pela contagem — era o `catch { }` mudo daqui que deixava esboço na peça
        /// do usuário sem uma linha no log.
        /// </summary>
        private static ProfilePlaneFrame FrameOf(dynamic doc, SketchScope scope, dynamic plane)
        {
            dynamic probe = null;
            try
            {
                probe = scope.AddProfileSet();
                dynamic p = probe.Profiles.Add(plane);
                return ProfilePlaneFrame.Discover((object)p);
            }
            catch (Exception e) { Log.Warn("  [plano] sondagem falhou: " + e.GetBaseException().Message); return null; }
            finally { if (probe != null) scope.DropProfileSet((object)probe); }
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
