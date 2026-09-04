using System;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Model;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// SONDA DA ROSCA M6 — RODADA 2, agora com a receita DECODIFICADA de um furo M6
    /// roscado que o Carlos fez À MÃO no Solid Edge (SPY de 2026-09-03, log
    /// <c>AutoEDM_20260903_085129</c>). Roda numa peça DESCARTÁVEL: um
    /// <c>Holes.AddSync</c> que falha envenena o proxy do documento e derrubaria a
    /// furação que já funciona no fluxo do eletrodo.
    ///
    /// A DESCOBERTA da rodada 1 — o furo "M6" nunca foi um furo roscado:
    ///   * pedimos <c>HoleType = igTappedHole (37)</c> e o HoleData voltou com
    ///     <c>HoleType = 36 (igCounterdrillHole)</c>, <c>TreatmentType = 44 (igNone)</c>,
    ///     Ø 6,4 mm — o SE recusou o 37 como HoleType e caiu nos defaults salvos do diálogo;
    ///   * a verificação numérica do "Criar Base" (mesmo log) mostra o M6 saindo como
    ///     Ø6,0 com um rebaixo Ø6,9 no topo — um furo IMPOSSÍVEL de macho M6.
    ///
    /// O furo MANUAL (que é o certo) tem:
    ///   HoleType=33 (igRegularHole)   TreatmentType=37 (igTappedHole)  ← a rosca mora AQUI
    ///   Standard="ISO Metric"  SubType="Standard Thread"  Size="M6"  Fit="Close (H12)"
    ///   ThreadDiameterOption=0 (broca)  ThreadTapDrillDiameter=5 mm  HoleDiameter=6 mm
    ///   ThreadMinorDiameter=4,917  ThreadExternalDiameter=4,773  ThreadDescription="M6"
    ///   ThreadSetting=164 (igRegularThread)  ThreadDepthMethod=13 (igFinite)  ThreadDepth=14
    ///   CreatePhysicalThread=False   ← nem o furo manual tem hélice cortada
    /// Ou seja: <b><c>HoleType</c> continua igRegularHole; quem liga a rosca é
    /// <c>TreatmentType = igTappedHole (37)</c></b>. E a exibição de rosca está DESLIGADA
    /// nesta instalação (<c>seApplicationGlobalEnableThreadedDisplay = False</c>), então
    /// mesmo um furo roscado CORRETO aparece liso na tela.
    ///
    /// NOTA (2026-09-03, depois desta sonda): a receita de produção passou a viver em
    /// <see cref="BlankModeler"/>.NewTappedHoleData, e ganhou a FORMA do furo (ponta de broca,
    /// rosca mais curta que o furo, chanfro de entrada) que esta sonda não modela — ela ficou
    /// como o diagnóstico que respondeu "a rosca chega por onde?", não como referência de furo.
    /// A rosca FÍSICA não é necessária no fluxo do Carlos (rosca cosmética + cota bastam).
    ///
    /// Esta rodada testa 4 receitas lado a lado e MEDE os cilindros do corpo no fim — a
    /// prova numérica de que o furo saiu no Ø da broca (5 mm) e não no nominal (6 mm).
    /// </summary>
    public static class ThreadProbe
    {
        // FeaturePropertyConstants (dump SE 2023)
        private const int igLeft = 1;              // lado do perfil: −normal (fura p/ dentro)
        private const int igFinite = 13;           // furo cego / profundidade de rosca finita
        private const int igRegularHole = 33;      // HoleType de um furo roscado TAMBÉM é 33
        private const int igTappedHole = 37;       // vale como TreatmentType, NÃO como HoleType
        private const int igRegularThread = 164;   // ThreadSetting ("Rosca" marcado no diálogo)

        // ThreadDiameterOptionConstants
        private const int seTapDrillDiameter = 0;

        // ApplicationGlobalConstants
        private const int seApplicationGlobalEnableThreadedDisplay = 30;
        private const int seApplicationGlobalHoleSizeFile = 61;         // HOLES.TXT (tabela LEGADA)
        private const int seApplicationGlobalThreadDisplayMode = 406;
        private const int seApplicationGlobalHolesDatabaseFolder = 498; // pasta dos .xlsx (base NOVA)

        // M6 conforme Preferences\Holes\ISO Metric.xlsx (aba Threaded) + o furo manual
        private const string IsoStandard = "ISO Metric";
        private const string IsoThreadSubType = "Standard Thread";
        private const string IsoSizeM6 = "M6";
        private const string IsoFit = "Close (H12)";
        private const double M6NominalMm = 6.0;
        private const double M6TapDrillMm = 5.0;
        private const double M6MinorMm = 4.917;
        private const double M6ExternalMinorMm = 4.773;
        private const double M6DepthMm = 10.0;

        private static readonly string[] PhysicalThreadError =
        {
            "sePhysicalThreadNoError", "sePhysicalThreadUnknownError", "sePhysicalThreadProfileCreationError",
            "sePhysicalThreadHelixCreationError", "sePhysicalThreadBooleanOperationError",
            "sePhysicalThreadInvalidThreadTypeError", "sePhysicalThreadInvalidPitchValueError",
            "sePhysicalThreadDisabledByAdminError"
        };

        private enum Recipe
        {
            /// <summary>AddEx (base de furos) + AddSyncEx(rosca física = false) — só anotação.</summary>
            DatabaseCosmetic,
            /// <summary>AddEx + AddSyncEx(rosca física = true) — hélice pedida na criação.</summary>
            DatabasePhysicalAtCreation,
            /// <summary>AddEx + AddSyncEx(false) e depois CreatePhysicalThreadAndReturnStatus(true).</summary>
            DatabasePhysicalAfterwards,
            /// <summary>Add (21 params, sem a base de furos) com os mesmos valores na mão.</summary>
            ManualValuesNoDatabase
        }

        /// <summary>
        /// Roda a matriz numa peça NOVA (descartável, não salva). Nunca lança: é diagnóstico.
        /// O documento fica ABERTO para inspeção visual.
        /// </summary>
        public static void RunM6Matrix(dynamic app)
        {
            Log.Info("===== SONDA DE ROSCA M6 (rodada 2 — receita do furo manual) =====");
            LogThreadGlobals(app);

            dynamic doc;
            try { doc = app.Documents.Add("SolidEdge.PartDocument"); }
            catch (Exception e) { Log.Error("Sonda de rosca: não deu para criar a peça descartável.", e); return; }

            int mode = 1; try { mode = (int)doc.ModelingMode; } catch { }
            Log.Info("Peça descartável criada (ModelingMode=" + mode + " — 1=síncrono, 2=ordenado).");

            try { BlankModeler.CreateBox(doc, 120.0, 40.0, 15.0); }
            catch (Exception e) { Log.Error("Sonda de rosca: bloco 120×40×15 não foi criado.", e); return; }

            const double topZmm = 15.0; // origem na base do bloco; ele sobe +Z
            // Cada furo RE-ADQUIRE app.ActiveDocument: o AddSync anterior desconecta o proxy
            // da peça (RPC_E_DISCONNECTED) — o Application é o único que sobrevive.
            // Sem trocar ModelingMode entre receitas: na rodada 1, ir p/ ordenado e voltar
            // deixou a receita seguinte com "nome de propriedade inválido".
            TryVariant(app, "1  base de furos + AddSyncEx(fisica=false)   [so anotacao]", topZmm, -42.0, Recipe.DatabaseCosmetic);
            TryVariant(app, "2  base de furos + AddSyncEx(fisica=true)    [helice na criacao]", topZmm, -14.0, Recipe.DatabasePhysicalAtCreation);
            TryVariant(app, "3  base de furos + CreatePhysicalThread DEPOIS", topZmm, 14.0, Recipe.DatabasePhysicalAfterwards);
            TryVariant(app, "4  Add (21 params) com os valores na mao, SEM a base de furos", topZmm, 42.0, Recipe.ManualValuesNoDatabase);

            MeasureCylinders(app, new[] { -42.0, -14.0, 14.0, 42.0 }, new[] { "1", "2", "3", "4" });
            Log.Info("Posições (X, mm): 1=-42  2=-14  3=+14  4=+42. Ø esperado da broca = 5,0 mm (NÃO 6,0).");
            Log.Info("===== FIM (SONDA DE ROSCA M6) =====");
        }

        private static void TryVariant(dynamic app, string label, double planeZmm, double cxMm, Recipe recipe)
        {
            Log.Info("--- receita " + label + "  @ X=" + cxMm.ToString("0.#") + " mm");
            dynamic ps = null;
            try
            {
                dynamic doc = app.ActiveDocument; // RCW novo (o furo anterior matou o antigo)

                dynamic plane = doc.RefPlanes.AddParallelByDistance(
                    doc.RefPlanes.Item(1), Units.MmToM(planeZmm), 2, Type.Missing, Type.Missing, Type.Missing);

                dynamic hd = recipe == Recipe.ManualValuesNoDatabase
                    ? NewHoleDataByValues(doc)
                    : NewHoleDataFromDatabase(doc);
                if (hd == null) { Log.Warn("  HoleData não criado — receita abortada."); return; }
                LogHoleData("  HoleData", hd);

                ps = doc.ProfileSets.Add();
                dynamic prof = ps.Profiles.Add(plane);
                prof.Holes2d.Add(Units.MmToM(cxMm), 0.0);
                prof.End(1);

                double depthM = Units.MmToM(M6DepthMm);
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)prof };
                bool physicalAtCreation = recipe == Recipe.DatabasePhysicalAtCreation;
                dynamic holes = doc.Models.Item(1).Holes;
                object hole = (object)holes.AddSyncEx(1, arr, igLeft, igFinite, (object)depthM, (object)hd, physicalAtCreation);
                try { plane.Visible = false; } catch { }

                Log.Info("  Furo criado: Status=" + StatusOf(hole) +
                         ", CreatePhysicalThread=" + ReadBool(hole, "CreatePhysicalThread"));
                if (recipe == Recipe.DatabasePhysicalAfterwards) LogPhysicalThreadCall(hole);
            }
            catch (Exception e) { Log.Warn("  receita FALHOU: " + e.GetBaseException().Message); }
            finally { if (ps != null) { try { ps.Delete(); } catch { } } }
        }

        /// <summary>
        /// HoleData pela BASE DE FUROS (<c>Preferences\Holes\ISO Metric.xlsx</c>), copiando
        /// exatamente o que o SPY leu do furo M6 feito à mão. <c>AddEx</c> tem 37 params, todos
        /// [opt] depois do HoleType (dump linha 15227) — chamada por <c>InvokeMember</c>, que é
        /// o caminho que o IDispatch coage com ~27 <c>Type.Missing</c>.
        ///
        /// Ordem: 1 HoleType, 2 Standard, 3 SubType, 4 Size, 5 Fit, 6 HoleDiameter,
        /// 7 CounterboreDiameter, 8 CounterboreDepth, 9 CountersinkDiameter, 10 CountersinkAngle,
        /// 11 BottomAngle, 12 TreatmentType, 13 TaperMethod, 14 Taper, 15 ThreadMinorDiameter,
        /// 16 ThreadDepthMethod, 17 ThreadDepth, 18 VBottomDimType, 19 TaperDimType,
        /// 20 CounterboreProfileLocationType, 21 TaperLValue, 22 TaperRValue,
        /// 23 ThreadExternalDiameter, 24 ThreadDescription, 25 IgnoreSavedDefaultValues,
        /// 26 ThreadDiameterOption, 27 ThreadTapDrillDiameter, ...
        ///
        /// <c>IgnoreSavedDefaultValues = true</c> é obrigatório: sem ele o SE mistura os
        /// defaults SALVOS do diálogo de furo do usuário (foi de lá que veio o rebaixo Ø6,9
        /// e o HoleType=36 da rodada 1).
        /// </summary>
        private static dynamic NewHoleDataFromDatabase(dynamic doc)
        {
            try
            {
                object hdc = (object)doc.HoleDataCollection;
                var args = new object[27];
                for (int i = 0; i < args.Length; i++) args[i] = Type.Missing;
                args[0] = igRegularHole;                    // HoleType — 33, NÃO 37
                args[1] = IsoStandard;
                args[2] = IsoThreadSubType;
                args[3] = IsoSizeM6;
                args[4] = IsoFit;
                args[5] = Units.MmToM(M6NominalMm);         // HoleDiameter = nominal (o SE fura no Ø da broca)
                args[11] = igTappedHole;                    // TreatmentType — AQUI mora a rosca
                args[15] = igFinite;                        // ThreadDepthMethod
                args[16] = Units.MmToM(M6DepthMm);          // ThreadDepth
                args[24] = true;                            // IgnoreSavedDefaultValues
                args[25] = seTapDrillDiameter;              // ThreadDiameterOption
                args[26] = Units.MmToM(M6TapDrillMm);       // ThreadTapDrillDiameter
                object hd = hdc.GetType().InvokeMember("AddEx", BindingFlags.InvokeMethod, null, hdc, args);
                if (hd == null) { Log.Warn("  AddEx devolveu null."); return null; }
                dynamic d = hd;
                try { d.ThreadSetting = igRegularThread; }
                catch (Exception e) { Log.Warn("  ThreadSetting: " + e.GetBaseException().Message); }
                return d;
            }
            catch (Exception e) { Log.Warn("  HoleDataCollection.AddEx: " + e.GetBaseException().Message); return null; }
        }

        /// <summary>
        /// Mesmo furo, mas pelo <c>Add</c> de 21 params + propriedades na mão — sem depender
        /// das strings da base de furos. Serve para saber se dá para roscar num SE cujo
        /// Standard/SubType/Size seja outro (ANSI, DIN…) ou tenha sido customizado.
        /// Ordem: 1 HoleType, 2 HoleDiameter, 3..7 counterbore/countersink/bottom,
        /// 8 TreatmentType, 9 TaperMethod, 10 Taper, 11 ThreadMinorDiameter,
        /// 12 ThreadDepthMethod, 13 ThreadDepth, 14..18 …, 19 ThreadExternalDiameter,
        /// 20 ThreadDescription, 21 IgnoreSavedDefaultValues.
        /// </summary>
        private static dynamic NewHoleDataByValues(dynamic doc)
        {
            try
            {
                object hdc = (object)doc.HoleDataCollection;
                var args = new object[21];
                for (int i = 0; i < args.Length; i++) args[i] = Type.Missing;
                args[0] = igRegularHole;                        // HoleType
                args[1] = Units.MmToM(M6NominalMm);             // HoleDiameter
                args[7] = igTappedHole;                         // TreatmentType
                args[10] = Units.MmToM(M6MinorMm);              // ThreadMinorDiameter
                args[11] = igFinite;                            // ThreadDepthMethod
                args[12] = Units.MmToM(M6DepthMm);              // ThreadDepth
                args[18] = Units.MmToM(M6ExternalMinorMm);      // ThreadExternalDiameter
                args[19] = IsoSizeM6;                           // ThreadDescription
                args[20] = true;                                // IgnoreSavedDefaultValues
                object hd = hdc.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, hdc, args);
                if (hd == null) { Log.Warn("  Add devolveu null."); return null; }
                dynamic d = hd;
                try { d.ThreadDiameterOption = seTapDrillDiameter; } catch (Exception e) { Log.Warn("  ThreadDiameterOption: " + e.GetBaseException().Message); }
                try { d.ThreadTapDrillDiameter = Units.MmToM(M6TapDrillMm); } catch (Exception e) { Log.Warn("  ThreadTapDrillDiameter: " + e.GetBaseException().Message); }
                try { d.ThreadSetting = igRegularThread; } catch (Exception e) { Log.Warn("  ThreadSetting: " + e.GetBaseException().Message); }
                return d;
            }
            catch (Exception e) { Log.Warn("  HoleDataCollection.Add: " + e.GetBaseException().Message); return null; }
        }

        /// <summary>
        /// Mede os cilindros do corpo (<c>Body.Faces[10]</c> = igQueryCylinder) — a prova
        /// NUMÉRICA do Ø real de cada furo, que dispensa inspeção visual para saber se o furo
        /// saiu no Ø da broca (5,0) ou no nominal (6,0). Um furo com hélice CORTADA não tem
        /// uma parede cilíndrica limpa, então a ausência de cilindro num X também informa.
        /// </summary>
        private static void MeasureCylinders(dynamic app, double[] expXmm, string[] labels)
        {
            try
            {
                dynamic model = app.ActiveDocument.Models.Item(1);
                dynamic cyls = model.Body.Faces[10];
                int n = 0; try { n = (int)cyls.Count; } catch { }
                Log.Info("  [medida] " + n + " face(s) cilíndrica(s) no corpo:");
                for (int i = 1; i <= n; i++)
                {
                    object f; try { f = cyls.Item(i); } catch { continue; }
                    double[] mn, mx;
                    if (!AutoEDM.Selection.FaceGeometry.TryGetRangeMm(f, out mn, out mx)) continue;
                    double x = (mn[0] + mx[0]) / 2.0, dia = mx[0] - mn[0];
                    string near = "";
                    for (int k = 0; k < expXmm.Length; k++)
                        if (Math.Abs(x - expXmm[k]) < 1.0) near = " → receita " + labels[k];
                    Log.Info("  [medida]   cilindro " + i + ": X=" + x.ToString("0.00") +
                             " Ø" + dia.ToString("0.000") + " Z ∈ [" + mn[2].ToString("0.0") + ", " + mx[2].ToString("0.0") + "]" + near);
                }
            }
            catch (Exception e) { Log.Warn("  [medida] leitura dos cilindros falhou: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// Chama <c>CreatePhysicalThreadAndReturnStatus(true, [out] código)</c> e DECODIFICA
        /// o código (PhysicalThreadErrorCode). O [out] só é populado com
        /// <see cref="ParameterModifier"/> by-ref — sem ele volta 0, que significa "sem erro"
        /// e mente sobre o resultado.
        /// </summary>
        private static void LogPhysicalThreadCall(object hole)
        {
            if (hole == null) return;
            try
            {
                object[] args = { true, 0 };
                var mod = new ParameterModifier(2);
                mod[1] = true; // [out] enumPhysicalThreadErrorCode
                hole.GetType().InvokeMember("CreatePhysicalThreadAndReturnStatus",
                    BindingFlags.InvokeMethod, null, hole, args, new[] { mod }, null, null);
                int code = 0; try { code = Convert.ToInt32(args[1]); } catch { }
                string name = code >= 0 && code < PhysicalThreadError.Length ? PhysicalThreadError[code] : "?";
                Log.Info("  CreatePhysicalThreadAndReturnStatus(true) → " + code + " (" + name + "); " +
                         "CreatePhysicalThread agora=" + ReadBool(hole, "CreatePhysicalThread"));
            }
            catch (Exception e) { Log.Warn("  CreatePhysicalThreadAndReturnStatus: " + e.GetBaseException().Message); }
        }

        /// <summary>
        /// Fotografia COMPLETA do HoleData. O par que decide tudo é
        /// <c>HoleType=33</c> + <c>TreatmentType=37</c>; se o TreatmentType voltar 44 (igNone),
        /// o furo NÃO é roscado, por mais que Standard/SubType/Size estejam preenchidos.
        /// </summary>
        private static void LogHoleData(string tag, dynamic hd)
        {
            Log.Info(tag + ": HoleType=" + ReadNum(hd, "HoleType") + " (33=comum, 36=rebaixado, 37=NÃO use aqui)" +
                     " TreatmentType=" + ReadNum(hd, "TreatmentType") + " (37=ROSCADO, 44=nenhum)" +
                     " Ø=" + Mm(hd, "HoleDiameter"));
            Log.Info(tag + ": Standard='" + ReadStr(hd, "Standard") + "' SubType='" + ReadStr(hd, "SubType") +
                     "' Size='" + ReadStr(hd, "Size") + "' Fit='" + ReadStr(hd, "Fit") + "'" +
                     " ThreadSetting=" + ReadNum(hd, "ThreadSetting") + " (164=rosca padrão, 44=sem rosca)" +
                     " ThreadDiameterOption=" + ReadNum(hd, "ThreadDiameterOption") + " (0=broca, 1=menor interno, 2=nominal)");
            Log.Info(tag + ": desc='" + ReadStr(hd, "ThreadDescription") + "' Ønom=" + Mm(hd, "ThreadNominalDiameter") +
                     " Ømenor=" + Mm(hd, "ThreadMinorDiameter") + " Øbroca=" + Mm(hd, "ThreadTapDrillDiameter") +
                     " Øext=" + Mm(hd, "ThreadExternalDiameter") +
                     " profRosca=" + Mm(hd, "ThreadDepth") + " (método=" + ReadNum(hd, "ThreadDepthMethod") + ", 13=finita)");
        }

        /// <summary>
        /// Opções GLOBAIS do SE que decidem se uma rosca CORRETA aparece na tela e de qual
        /// tabela ela vem. <c>GetGlobalParameter</c> devolve por <c>[in,out] VARIANT</c> —
        /// exige o by-ref do <see cref="ParameterModifier"/>, igual ao GetRange.
        /// Na máquina do Carlos (2026-09-03) ExibirRosca veio <b>False</b>: com isso, mesmo o
        /// furo roscado CERTO desenha liso.
        /// </summary>
        private static void LogThreadGlobals(dynamic app)
        {
            Log.Info("Opções globais do SE: ExibirRosca(30)=" + Global(app, seApplicationGlobalEnableThreadedDisplay) +
                     "  ModoExibiçãoRosca(406)=" + Global(app, seApplicationGlobalThreadDisplayMode));
            Log.Info("  tabela LEGADA HOLES.TXT (61) = " + Global(app, seApplicationGlobalHoleSizeFile));
            Log.Info("  base NOVA de furos      (498) = " + Global(app, seApplicationGlobalHolesDatabaseFolder));
        }

        private static string Global(dynamic app, int parameter)
        {
            try
            {
                object[] args = { parameter, null };
                var mod = new ParameterModifier(2);
                mod[1] = true; // [in,out] Value
                object o = (object)app;
                o.GetType().InvokeMember("GetGlobalParameter", BindingFlags.InvokeMethod, null, o, args,
                    new[] { mod }, null, null);
                return args[1] == null ? "(vazio)" : Convert.ToString(args[1]);
            }
            catch (Exception e) { return "erro: " + e.GetBaseException().Message; }
        }

        /// <summary>
        /// Liga a exibição de rosca do SE (opção global do aplicativo, não do documento).
        /// Chamada só a pedido do usuário — é uma preferência dele, e mexer nela sem avisar
        /// mudaria o SE inteiro por conta de um comando do add-in.
        /// </summary>
        public static bool EnableThreadedDisplay(dynamic app)
        {
            try
            {
                object o = (object)app;
                o.GetType().InvokeMember("SetGlobalParameter", BindingFlags.InvokeMethod, null, o,
                    new object[] { seApplicationGlobalEnableThreadedDisplay, true });
                Log.Info("Exibição de rosca LIGADA (seApplicationGlobalEnableThreadedDisplay=True); agora lê " +
                         Global(app, seApplicationGlobalEnableThreadedDisplay) + ".");
                return true;
            }
            catch (Exception e) { Log.Warn("Não deu para ligar a exibição de rosca: " + e.GetBaseException().Message); return false; }
        }

        /// <summary>true se a opção global de exibição de rosca está desligada.</summary>
        public static bool ThreadedDisplayIsOff(dynamic app)
        {
            string v = Global(app, seApplicationGlobalEnableThreadedDisplay);
            return string.Equals(v, "False", StringComparison.OrdinalIgnoreCase);
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

        private static string ReadStr(dynamic o, string prop)
        {
            try { return Convert.ToString(((object)o).GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null)); }
            catch { return "?"; }
        }

        private static string ReadNum(dynamic o, string prop)
        {
            try { return Convert.ToString(Convert.ToInt64(((object)o).GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null))); }
            catch { return "?"; }
        }

        private static string ReadBool(object o, string prop)
        {
            try { return Convert.ToString(o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null)); }
            catch { return "?"; }
        }

        /// <summary>Lê uma propriedade em METROS e devolve em mm (a API é toda em metros).</summary>
        private static string Mm(dynamic o, string prop)
        {
            try
            {
                double m = Convert.ToDouble(((object)o).GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null));
                return Units.MToMm(m).ToString("0.###") + "mm";
            }
            catch { return "?"; }
        }
    }
}
