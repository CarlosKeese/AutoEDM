using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using AutoEDM.Diagnostics;

namespace AutoEDM.Com
{
    /// <summary>
    /// Introspecção de objetos COM (IDispatch) para descobrir, em tempo de
    /// execução, quais propriedades/métodos um objeto realmente expõe — útil quando
    /// a documentação não está acessível e não sabemos o nome exato do membro
    /// (ex.: como ler a cor de uma Face no Solid Edge 2023).
    /// </summary>
    public static class ComDiagnostics
    {
        // IDispatch: só precisamos de GetTypeInfo (slot 4, após os 3 do IUnknown).
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("00020400-0000-0000-C000-000000000046")]
        private interface IDispatchLite
        {
            [PreserveSig] int GetTypeInfoCount(out uint pctinfo);
            [PreserveSig] int GetTypeInfo(uint iTInfo, int lcid, out ITypeInfo ppTInfo);
        }

        /// <summary>
        /// Devolve os nomes de propriedades/métodos da interface primária do objeto
        /// COM. Retorna lista vazia se o objeto não for IDispatch ou não expuser
        /// type info.
        /// </summary>
        public static List<string> GetMemberNames(object comObject)
        {
            var names = new List<string>();
            if (comObject == null || !Marshal.IsComObject(comObject)) return names;

            ITypeInfo ti = null;
            try
            {
                var disp = comObject as IDispatchLite;
                if (disp == null) return names;

                uint count;
                if (disp.GetTypeInfoCount(out count) != 0 || count == 0) return names;
                if (disp.GetTypeInfo(0, 0, out ti) != 0 || ti == null) return names;

                IntPtr pAttr;
                ti.GetTypeAttr(out pAttr);
                int funcs;
                try
                {
                    var attr = (System.Runtime.InteropServices.ComTypes.TYPEATTR)
                        Marshal.PtrToStructure(pAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPEATTR));
                    funcs = attr.cFuncs;
                }
                finally { ti.ReleaseTypeAttr(pAttr); }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < funcs; i++)
                {
                    IntPtr pFunc;
                    ti.GetFuncDesc(i, out pFunc);
                    try
                    {
                        var fd = (System.Runtime.InteropServices.ComTypes.FUNCDESC)
                            Marshal.PtrToStructure(pFunc, typeof(System.Runtime.InteropServices.ComTypes.FUNCDESC));
                        var buf = new string[1];
                        int got;
                        ti.GetNames(fd.memid, buf, 1, out got);
                        if (got > 0 && !string.IsNullOrEmpty(buf[0]) && seen.Add(buf[0]))
                            names.Add(buf[0]);
                    }
                    finally { ti.ReleaseFuncDesc(pFunc); }
                }
            }
            catch
            {
                // Introspecção é best-effort; ignore falhas.
            }
            finally
            {
                if (ti != null) Marshal.ReleaseComObject(ti);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Loga os membros de um objeto COM com um rótulo.</summary>
        public static void LogMembers(string label, object comObject)
        {
            var members = GetMemberNames(comObject);
            Log.Info($"[DIAG] {label} ({members.Count}): {string.Join(", ", members)}");
        }

        /// <summary>Um membro (propriedade OU método) de um objeto COM, com o tipo de invocação e
        /// a assinatura já formatada (via ITypeInfo) — a base para o SPY mostrar tanto VALORES
        /// (propriedades sem parâmetro) quanto ASSINATURAS (métodos, indexadas, setters) numa
        /// passada só, em vez de simplesmente pular tudo que não é uma getter de 0 args.</summary>
        private struct ComMemberInfo
        {
            public string Name;
            public System.Runtime.InteropServices.ComTypes.INVOKEKIND Kind;
            public int ParamCount;
            public string Signature;
        }

        /// <summary>Walk de ITypeInfo (mesma mecânica de <see cref="LogSignatures"/> e
        /// <see cref="DumpTypeInfo"/>) que devolve TODO membro com tipo de invocação + assinatura
        /// — inclusive métodos e propriedades indexadas, que <see cref="GetMemberNames"/> não
        /// distingue (e o SPY antigo simplesmente descartava).</summary>
        private static List<ComMemberInfo> GetMemberSchema(object comObject)
        {
            var result = new List<ComMemberInfo>();
            if (comObject == null || !Marshal.IsComObject(comObject)) return result;

            ITypeInfo ti = null;
            try
            {
                var disp = comObject as IDispatchLite;
                if (disp == null) return result;
                if (disp.GetTypeInfoCount(out uint cnt) != 0 || cnt == 0) return result;
                if (disp.GetTypeInfo(0, 0, out ti) != 0 || ti == null) return result;

                IntPtr pAttr;
                ti.GetTypeAttr(out pAttr);
                int funcs;
                try
                {
                    var attr = (System.Runtime.InteropServices.ComTypes.TYPEATTR)Marshal.PtrToStructure(pAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPEATTR));
                    funcs = attr.cFuncs;
                }
                finally { ti.ReleaseTypeAttr(pAttr); }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < funcs; i++)
                {
                    IntPtr pFunc;
                    ti.GetFuncDesc(i, out pFunc);
                    try
                    {
                        var fd = (System.Runtime.InteropServices.ComTypes.FUNCDESC)Marshal.PtrToStructure(pFunc, typeof(System.Runtime.InteropServices.ComTypes.FUNCDESC));
                        var buf = new string[64];
                        int got;
                        ti.GetNames(fd.memid, buf, buf.Length, out got);
                        if (got == 0 || string.IsNullOrEmpty(buf[0])) continue;
                        string name = buf[0];
                        if (!seen.Add(name + "#" + fd.invkind)) continue; // dedup por (nome, invkind)

                        int elemSize = Marshal.SizeOf(typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                        bool hasElems = fd.lprgelemdescParam != IntPtr.Zero;
                        var ps = new List<string>();
                        for (int k = 0; k < fd.cParams; k++)
                        {
                            string pn = (k + 1 < got) ? buf[k + 1] : $"p{k}";
                            if (!hasElems) { ps.Add(pn); continue; }
                            string pt = "?", dir = "";
                            try
                            {
                                IntPtr pElem = fd.lprgelemdescParam + k * elemSize;
                                var ed = (System.Runtime.InteropServices.ComTypes.ELEMDESC)Marshal.PtrToStructure(pElem, typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                                pt = TypeName(ti, ed.tdesc, 0);
                                dir = ParamDir((short)ed.desc.paramdesc.wParamFlags);
                            }
                            catch { }
                            ps.Add($"{dir}{pn}: {pt}");
                        }
                        string ret = "";
                        try { ret = " -> " + TypeName(ti, fd.elemdescFunc.tdesc, 0); } catch { }
                        string kindTag =
                            fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYGET ? "get " :
                            fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUT ? "put " :
                            fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUTREF ? "putref " : "";
                        result.Add(new ComMemberInfo
                        {
                            Name = name,
                            Kind = fd.invkind,
                            ParamCount = fd.cParams,
                            Signature = $"{kindTag}{name}({string.Join(", ", ps)}){ret}"
                        });
                    }
                    finally { ti.ReleaseFuncDesc(pFunc); }
                }
            }
            catch { /* introspecção é best-effort */ }
            finally { if (ti != null) Marshal.ReleaseComObject(ti); }

            return result;
        }

        /// <summary>
        /// "SPY" ao vivo: loga o TIPO e TODAS as propriedades (nome = VALOR) de um objeto COM,
        /// as ASSINATURAS de todo método/propriedade-indexada/setter, e expande coleções (Count +
        /// Item) em vez de tratá-las como opacas — recursando nos filhos que também são COM até
        /// <paramref name="maxDepth"/>. É a versão genérica do <see cref="LogColorDiscovery"/> —
        /// a mesma ideia do Solid Edge Spy (IDispatch→ITypeInfo), mas dirigida a UM objeto (ex.:
        /// uma feature que o usuário criou na mão), para descobrir a API real sem adivinhar.
        /// Como efeito colateral, TODO objeto visitado (inclusive os de navegação, que não
        /// recursam) alimenta o dump acumulado do SDK (<see cref="HarvestTypeLibs"/>) — cada
        /// clique no SPY também engorda o mapa persistente da API COM. Best-effort: leituras que
        /// lançam são logadas (não escondidas) e nunca interrompem o dump.
        /// </summary>
        public static void DumpObject(string label, object comObject, int maxDepth = 1)
        {
            Log.Info($"[SPY] ===== {label} =====");
            var seeds = new List<object>();
            DumpObjectInner(comObject, maxDepth, 0, seeds);
            HarvestTypeLibs(seeds);
        }

        private static void DumpObjectInner(object obj, int maxDepth, int depth, List<object> harvestSeeds, int maxItems = 5)
        {
            string pad = new string(' ', 2 + depth * 2);
            if (obj == null) { Log.Info($"[SPY]{pad}(null)"); return; }
            if (!Marshal.IsComObject(obj)) { Log.Info($"[SPY]{pad}= {obj} ({obj.GetType().Name})"); return; }

            harvestSeeds?.Add(obj);

            var schema = GetMemberSchema(obj);
            var names = schema.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                               .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            Log.Info($"[SPY]{pad}COM: {{ {string.Join(", ", names)} }}");

            var getters = schema.Where(m => m.Kind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYGET)
                                 .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

            // Coleção (tem Count sem parâmetro + Item)? Expande os primeiros itens em vez de
            // tratar como "profundidade máxima" — é onde vivem Faces/Edges/HoleData/features etc.
            bool isCollection = getters.Any(m => m.Name.Equals("Count", StringComparison.OrdinalIgnoreCase) && m.ParamCount == 0)
                              && names.Any(n => n.Equals("Item", StringComparison.OrdinalIgnoreCase));
            if (isCollection)
            {
                int count = 0;
                try { count = (int)((dynamic)obj).Count; } catch { }
                Log.Info($"[SPY]{pad}  Count = {count}");
                if (count > 0)
                {
                    if (depth < maxDepth)
                    {
                        int shown = Math.Min(count, maxItems);
                        for (int i = 1; i <= shown; i++)
                        {
                            object it;
                            try { it = ((dynamic)obj).Item(i); }
                            catch (Exception ex) { Log.Info($"[SPY]{pad}  Item({i}) -> (leitura falhou: {ex.GetBaseException().Message})"); continue; }
                            Log.Info($"[SPY]{pad}  Item({i}) ->");
                            DumpObjectInner(it, maxDepth, depth + 1, harvestSeeds, maxItems);
                        }
                        if (count > shown) Log.Info($"[SPY]{pad}  ... +{count - shown} item(ns) não mostrados (maxItems={maxItems})");
                    }
                    else Log.Info($"[SPY]{pad}  Item(i) -> (COM; profundidade máx.)");
                }
            }

            foreach (var m in getters)
            {
                if (m.Name.Equals("Count", StringComparison.OrdinalIgnoreCase) || m.Name.Equals("Item", StringComparison.OrdinalIgnoreCase))
                    continue; // já tratado acima quando é coleção
                if (m.ParamCount > 0) { Log.Info($"[SPY]{pad}  {m.Signature}  (propriedade indexada — não lida automaticamente)"); continue; }

                object val;
                try { val = obj.GetType().InvokeMember(m.Name, BindingFlags.GetProperty, null, obj, null); }
                catch (Exception ex) { Log.Info($"[SPY]{pad}  {m.Name} (leitura falhou: {ex.GetBaseException().Message})"); continue; }

                if (val == null) { Log.Info($"[SPY]{pad}  {m.Name} = null"); continue; }
                if (Marshal.IsComObject(val))
                {
                    // Não recursa em membros de NAVEGAÇÃO (poluem com a árvore inteira do SE) —
                    // só o objeto em si + seus filhos DIRETOS úteis interessam.
                    bool noise = m.Name == "Application" || m.Name == "Parent" || m.Name == "Document" ||
                                 m.Name == "Documents" || m.Name == "ActiveDocument";
                    if (depth < maxDepth && !noise)
                    {
                        Log.Info($"[SPY]{pad}  {m.Name} ->");
                        DumpObjectInner(val, maxDepth, depth + 1, harvestSeeds, maxItems);
                    }
                    else
                    {
                        harvestSeeds?.Add(val); // não recursa, mas ainda alimenta o dump acumulado da lib
                        Log.Info($"[SPY]{pad}  {m.Name} -> (COM{(noise ? "; navegação" : "; profundidade máx.")})");
                    }
                }
                else Log.Info($"[SPY]{pad}  {m.Name} = {val} ({val.GetType().Name})");
            }

            var methods = schema.Where(m => m.Kind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_FUNC)
                                 .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            if (methods.Count > 0)
                Log.Info($"[SPY]{pad}  métodos: {string.Join("; ", methods.Select(m => m.Signature))}");

            var setters = schema.Where(m => m.Kind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUT || m.Kind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUTREF)
                                 .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                                 .Where(m => !getters.Any(g => g.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase))) // já coberto (get+put)
                                 .ToList();
            if (setters.Count > 0)
                Log.Info($"[SPY]{pad}  setters: {string.Join("; ", setters.Select(m => m.Signature))}");
        }

        // ===================== Gravador de ação manual (Carlos) ======================
        // "Iniciar leitura" snapshota os NOMES dos itens de TODAS as coleções de Constructions e
        // do Model (enumeração GENÉRICA via IDispatch — não uma lista fixa, que perdia a coleção
        // onde a feature caía, Log 2026-07-16 "nada mudou"); "Gravar log" diffa por nome e dumpa
        // [SPY] as NOVAS. Nomes (não contagens) porque a costura CONSOME as "Limite" (count cai).

        /// <summary>Todas as coleções (com .Count) de Constructions + Model 1 + Models, por rótulo.
        /// Enumeração GENÉRICA via IDispatch — pega qualquer coleção sem lista fixa.</summary>
        private static Dictionary<string, object> CollectionsMap(object doc)
        {
            var map = new Dictionary<string, object>();
            dynamic d = doc;
            try { AddCollectionMembers((object)d.Constructions, "Constructions", map); } catch { }
            try { AddCollectionMembers((object)d.Models.Item(1), "Model", map); } catch { }
            try { map["Models(corpos)"] = (object)d.Models; } catch { }
            // Árvore do PathFinder (features síncronas E ordenadas aparecem aqui) + esboços.
            try { map["DesignEdgebarFeatures"] = (object)d.DesignEdgebarFeatures; } catch { }
            try { map["Sketches"] = (object)d.Sketches; } catch { }
            try { map["ProfileSets"] = (object)d.ProfileSets; } catch { }
            return map;
        }

        /// <summary>Adiciona ao mapa todo membro-PROPRIEDADE do objeto que é uma COLEÇÃO (tem .Count).</summary>
        private static void AddCollectionMembers(object owner, string prefix, Dictionary<string, object> map)
        {
            if (owner == null) return;
            foreach (var m in GetMemberNames(owner))
            {
                object val;
                try { val = owner.GetType().InvokeMember(m, BindingFlags.GetProperty, null, owner, null); }
                catch { continue; }                              // é método, ou pede argumentos
                if (val == null || !Marshal.IsComObject(val)) continue;
                try { int _ = (int)((dynamic)val).Count; }       // só coleções têm .Count
                catch { continue; }
                map[$"{prefix}.{m}"] = val;
            }
        }

        /// <summary>Snapshot dos NOMES dos itens de cada coleção + o ModelingMode (início da leitura).</summary>
        public static Dictionary<string, List<string>> SnapshotInventory(object doc)
        {
            var snap = new Dictionary<string, List<string>>();
            dynamic d = doc;
            try { snap["__ModelingMode"] = new List<string> { ((int)d.ModelingMode).ToString() }; } catch { }
            foreach (var kv in CollectionsMap(doc)) snap[kv.Key] = ItemNames((dynamic)kv.Value);
            Log.Info($"[REC] Leitura INICIADA — snapshot de {Math.Max(0, snap.Count - 1)} coleção(ões). Faça o processo COMPLETO e clique 'Gravar log da leitura'.");
            LogDocIdentity(doc, "(início)");
            LogInventory("[REC] (início) não-vazias:", snap);
            return snap;
        }

        /// <summary>Diffa por NOME contra o snapshot e DUMPA (SPY) as features NOVAS; nomeia as removidas.</summary>
        public static void DumpNewSince(object doc, Dictionary<string, List<string>> baseline)
        {
            if (baseline == null) { Log.Warn("[REC] Sem snapshot inicial — clique 'Iniciar leitura de ação manual' antes de fazer a ação."); return; }
            dynamic d = doc;
            Log.Info("[REC] ===== GRAVAR LOG DA LEITURA (diff por nome) =====");
            LogDocIdentity(doc, "(fim)");
            try { int m = (int)d.ModelingMode; string m0 = baseline.TryGetValue("__ModelingMode", out var l) && l.Count > 0 ? l[0] : null; if (m0 != null && m0 != m.ToString()) Log.Info($"[REC] ModelingMode mudou: {m0} -> {m}."); } catch { }

            var nowMap = CollectionsMap(doc);
            var nowSnap = new Dictionary<string, List<string>>();
            foreach (var kv in nowMap) nowSnap[kv.Key] = ItemNames((dynamic)kv.Value);

            int changed = 0;
            // coleções presentes AGORA (adições/consumos) + as que sumiram por completo.
            var keys = new HashSet<string>(nowMap.Keys);
            foreach (var k in baseline.Keys) if (k != "__ModelingMode") keys.Add(k);
            foreach (var key in keys)
            {
                var nowNames = nowSnap.TryGetValue(key, out var nn) ? nn : new List<string>();
                var baseNames = baseline.TryGetValue(key, out var bn) ? bn : new List<string>();
                var added = nowNames.Where(x => !baseNames.Contains(x)).ToList();
                var removed = baseNames.Where(x => !nowNames.Contains(x)).ToList();
                if (added.Count == 0 && removed.Count == 0) continue;
                changed++;
                Log.Info($"[REC] {key}: +{added.Count} nova(s)" + (added.Count > 0 ? $" [{string.Join(", ", added)}]" : "") +
                         (removed.Count > 0 ? $"  −{removed.Count} removida(s) [{string.Join(", ", removed)}]" : ""));
                if (added.Count == 0 || !nowMap.TryGetValue(key, out var colObj)) continue;
                dynamic col = colObj; int c = 0; try { c = (int)col.Count; } catch { }
                for (int i = 1; i <= c; i++)
                {
                    object it; try { it = col.Item(i); } catch { continue; }
                    string nm = ItemName(it, i);
                    if (added.Contains(nm)) DumpObject($"[REC] {key} '{nm}'", it, 1);
                }
            }
            if (changed == 0)
            {
                LogInventory("[REC] (fim) não-vazias:", nowSnap);
                Log.Info("[REC] Nada mudou nas coleções observadas. Se você criou features, elas caíram FORA das coleções varridas — compare os inventários (início vs fim) acima e me diga o nome da feature no PathFinder.");
            }
            else Log.Info($"[REC] Fim da leitura — {changed} coleção(ões) mudaram (veja [REC]/[SPY] acima).");
        }

        private static List<string> ItemNames(dynamic col)
        {
            var names = new List<string>();
            int c = 0; try { c = (int)col.Count; } catch { }
            for (int i = 1; i <= c; i++)
            {
                object it; try { it = col.Item(i); } catch { names.Add($"#{i}"); continue; }
                names.Add(ItemName(it, i));
            }
            return names;
        }

        private static string ItemName(object item, int idx)
        {
            try { string n = (string)((dynamic)item).Name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
            try { string n = (string)((dynamic)item).DisplayName; if (!string.IsNullOrEmpty(n)) return n; } catch { }
            return $"#{idx}";
        }

        /// <summary>Loga QUAL documento o gravador está lendo (nome/tipo/corpos/construções) — para
        /// flagrar quando o `ActiveDocument` não é a peça que o usuário está editando.</summary>
        private static void LogDocIdentity(object doc, string when)
        {
            dynamic d = doc;
            string name = "?"; int type = -1, mode = -1, models = -1, consTotal = -1;
            try { name = (string)d.Name; } catch { }
            try { type = (int)d.Type; } catch { }
            try { mode = (int)d.ModelingMode; } catch { }
            try { models = (int)d.Models.Count; } catch { }
            try { consTotal = (int)d.Constructions.Count; } catch { }
            Log.Info($"[REC] {when} documento='{name}' Type={type} (1=peça) ModelingMode={mode} Models.Count={models} Constructions.Count={consTotal}");
        }

        /// <summary>Loga (compacto) as coleções NÃO-vazias do snapshot — p/ comparar início vs fim.</summary>
        private static void LogInventory(string prefix, Dictionary<string, List<string>> snap)
        {
            var parts = snap.Where(k => k.Key != "__ModelingMode" && k.Value != null && k.Value.Count > 0)
                            .OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value.Count}");
            string s = string.Join(", ", parts);
            Log.Info(prefix + " " + (string.IsNullOrEmpty(s) ? "(todas vazias)" : s));
        }

        /// <summary>
        /// Loga a assinatura (nome + nomes de parâmetros + nº de params) dos métodos
        /// indicados, lidos via ITypeInfo. Útil para descobrir como chamar métodos
        /// de modelagem (AddBoxByTwoPoints, CopySurfaces, ...) sem adivinhar.
        /// </summary>
        public static void LogSignatures(object comObject, params string[] methodNames)
        {
            if (comObject == null || !Marshal.IsComObject(comObject)) return;
            var wanted = new HashSet<string>(methodNames, StringComparer.OrdinalIgnoreCase);

            ITypeInfo ti = null;
            try
            {
                var disp = comObject as IDispatchLite;
                if (disp == null) return;
                if (disp.GetTypeInfoCount(out uint cnt) != 0 || cnt == 0) return;
                if (disp.GetTypeInfo(0, 0, out ti) != 0 || ti == null) return;

                IntPtr pAttr;
                ti.GetTypeAttr(out pAttr);
                int funcs;
                try
                {
                    var attr = (System.Runtime.InteropServices.ComTypes.TYPEATTR)
                        Marshal.PtrToStructure(pAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPEATTR));
                    funcs = attr.cFuncs;
                }
                finally { ti.ReleaseTypeAttr(pAttr); }

                for (int i = 0; i < funcs; i++)
                {
                    IntPtr pFunc;
                    ti.GetFuncDesc(i, out pFunc);
                    try
                    {
                        var fd = (System.Runtime.InteropServices.ComTypes.FUNCDESC)
                            Marshal.PtrToStructure(pFunc, typeof(System.Runtime.InteropServices.ComTypes.FUNCDESC));
                        var buf = new string[64];
                        int got;
                        ti.GetNames(fd.memid, buf, buf.Length, out got);
                        if (got > 0 && wanted.Contains(buf[0]))
                        {
                            // Nome + TIPO + direção de cada parâmetro (via ELEMDESC),
                            // igual ao dump — é o que revela o tipo real do arg errado.
                            int elemSize = Marshal.SizeOf(typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                            bool hasElems = fd.lprgelemdescParam != IntPtr.Zero;
                            var ps = new List<string>();
                            for (int k = 0; k < fd.cParams; k++)
                            {
                                string pn = (k + 1 < got) ? buf[k + 1] : $"p{k}";
                                if (!hasElems) { ps.Add(pn); continue; }
                                string pt = "?", dir = "";
                                try
                                {
                                    IntPtr pElem = fd.lprgelemdescParam + k * elemSize;
                                    var ed = (System.Runtime.InteropServices.ComTypes.ELEMDESC)
                                        Marshal.PtrToStructure(pElem, typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                                    pt = TypeName(ti, ed.tdesc, 0);
                                    dir = ParamDir((short)ed.desc.paramdesc.wParamFlags);
                                }
                                catch { }
                                ps.Add($"{dir}{pn}: {pt}");
                            }
                            string ret = "";
                            try { ret = " -> " + TypeName(ti, fd.elemdescFunc.tdesc, 0); } catch { }
                            Log.Info($"[DIAG] {buf[0]}({string.Join(", ", ps)}){ret}  cParams={fd.cParams}");
                        }
                    }
                    finally { ti.ReleaseFuncDesc(pFunc); }
                }
            }
            catch { }
            finally { if (ti != null) Marshal.ReleaseComObject(ti); }
        }

        // ------------------------------------------------------------------
        //  DUMP COMPLETO DA TYPE LIBRARY (o "SDK offline")
        // ------------------------------------------------------------------
        // Sobe de um objeto vivo (IDispatch -> ITypeInfo -> ITypeLib) e enumera
        // TODAS as coclasses/interfaces/enums da lib que o contém, com nomes de
        // método, nomes de parâmetro e VALORES de enum. Semeado com objetos de
        // libs diferentes (Application, Assembly, Part, Geometry) cobre o SDK
        // inteiro numa passada. Dedup por GUID de lib. Grava um .txt para consulta
        // offline — elimina o loop de descoberta um-método-por-vez.

        /// <summary>
        /// Dumpa as type libraries que contêm os objetos COM dados (dedup por GUID)
        /// para <paramref name="outPath"/>. Cada objeto serve só para alcançar a lib
        /// dele; passe objetos de módulos diferentes do SE para cobrir tudo.
        ///
        /// CUMULATIVO: se <paramref name="outPath"/> já existe, os GUIDs de lib já
        /// presentes (marcados no cabeçalho `[guid]` de cada seção) são lidos primeiro e o
        /// arquivo é ABERTO EM APPEND — reexecutar (com os mesmos seeds ou outros, de outra
        /// sessão) só ACRESCENTA libs novas, nunca perde o que já foi capturado. É o que
        /// permite o dump crescer clique a clique via <see cref="HarvestTypeLibs"/> em vez de
        /// depender de UMA sonda com seeds hardcoded cobrindo tudo de uma vez (o motivo de
        /// SolidEdgeGeometry/SolidEdgeAssembly terem ficado de fora do dump original — os seeds
        /// daquela sonda nunca alcançaram uma Face/Edge/Occurrence VIVA).
        /// </summary>
        public static void DumpTypeLibraries(string outPath, IEnumerable<object> seeds)
        {
            var seenLibs = new HashSet<Guid>();
            bool merging = File.Exists(outPath);
            if (merging) foreach (var g in ReadKnownLibGuids(outPath)) seenLibs.Add(g);
            int libsBefore = seenLibs.Count;
            int newLibs = 0;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                // AutoFlush: escreve incremental. Se a introspecção travar (ex.: AV
                // em ponteiro COM), o que já saiu fica salvo em disco.
                using (var w = new StreamWriter(outPath, append: merging, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    if (!merging)
                    {
                        w.WriteLine("# Solid Edge COM type-library dump (AutoEDM ComDiagnostics)");
                        w.WriteLine("# API de geometria = METROS. Coleções = 1-based.");
                        w.WriteLine("# Formato: [TYPEKIND Nome] / método([dir]param: tipo, ...) -> ret [N params] / CONST = valor");
                        w.WriteLine("# Dump CUMULATIVO: reexecutar só ACRESCENTA libs novas (não perde libs já capturadas antes).");
                        w.WriteLine();
                    }
                    else
                    {
                        w.WriteLine();
                        w.WriteLine($"# ----- merge em {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({libsBefore} lib(s) já conhecida(s) antes deste run) -----");
                    }

                    foreach (var seed in seeds)
                    {
                        if (seed == null || !Marshal.IsComObject(seed)) continue;
                        ITypeInfo ti = null;
                        ITypeLib lib = null;
                        try
                        {
                            if (!(seed is IDispatchLite disp)) continue;
                            if (disp.GetTypeInfoCount(out uint c) != 0 || c == 0) continue;
                            if (disp.GetTypeInfo(0, 0, out ti) != 0 || ti == null) continue;

                            ti.GetContainingTypeLib(out lib, out _);
                            if (lib == null) continue;

                            Guid guid = GetLibGuid(lib, out string libName);
                            if (!seenLibs.Add(guid)) continue; // lib já dumpada por outro seed (neste run ou num anterior)
                            DumpOneLib(lib, libName, guid, w);
                            newLibs++;
                        }
                        catch (Exception ex) { w.WriteLine($"# ERRO em seed: {ex.Message}"); }
                        finally
                        {
                            if (lib != null) Marshal.ReleaseComObject(lib);
                            if (ti != null) Marshal.ReleaseComObject(ti);
                        }
                    }
                }
                Log.Info($"Typelib dump: +{newLibs} lib(s) nova(s), {seenLibs.Count} total -> {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"Falha ao gravar o dump em {outPath}.", ex);
            }
        }

        /// <summary>Lê os GUIDs de type library já presentes num dump anterior (cabeçalho
        /// `[guid]`), para o merge não duplicar seções. Dumps antigos (sem `[guid]` no
        /// cabeçalho) não contribuem nenhum GUID — suas libs são re-dumpadas de novo, o que é
        /// inofensivo (só duplica a seção, não quebra nada). Nunca lança.</summary>
        private static List<Guid> ReadKnownLibGuids(string path)
        {
            var result = new List<Guid>();
            var rx = new Regex(@"^##########\s+TYPELIB\s+.+?\s+\[([0-9a-fA-F-]{36})\]");
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    var m = rx.Match(line);
                    if (m.Success && Guid.TryParse(m.Groups[1].Value, out var g)) result.Add(g);
                }
            }
            catch { /* dump ilegível/ausente — trata como se nenhuma lib fosse conhecida ainda */ }
            return result;
        }

        /// <summary>Soma os objetos visitados pelo SPY (<see cref="DumpObject"/>) — e as type
        /// libraries que eles alcançam — ao dump acumulado do SDK em
        /// `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_&lt;versão&gt;.txt`, sem o chamador precisar
        /// saber caminho nem versão do SE. Cada clique em "Inspecionar seleção" (ou feature nova
        /// pega pelo Gravador) tende a alcançar UMA lib diferente da já coberta pelas sondas
        /// estáticas (Geometry via uma Face, Assembly via uma Occurrence, ...) — é assim que o
        /// mapa cresce até cobrir tudo que o Carlos realmente usa. Best-effort: nunca lança.</summary>
        private static void HarvestTypeLibs(List<object> seeds)
        {
            if (seeds == null || seeds.Count == 0) return;
            try
            {
                string path = ResolveDumpPath(seeds);
                if (path == null) return;
                DumpTypeLibraries(path, seeds);
            }
            catch { /* o SPY nunca deve quebrar por causa do harvest */ }
        }

        /// <summary>Acha `%LOCALAPPDATA%\AutoEDM\logs\SE_API_dump_&lt;versão&gt;.txt` perguntando
        /// a versão do SE a QUALQUER seed que exponha `.Application.Version` (ou `.Version`, se o
        /// próprio seed já for o Application) — a maioria dos objetos do SE expõe `.Application`
        /// (é por isso que o dump SEMPRE consegue achar o caminho certo, mesmo semeado só com uma
        /// Face ou Feature qualquer).</summary>
        private static string ResolveDumpPath(List<object> seeds)
        {
            string ver = null;
            foreach (var seed in seeds)
            {
                if (seed == null) continue;
                try { ver = (string)((dynamic)seed).Application.Version; if (!string.IsNullOrEmpty(ver)) break; } catch { }
                try { ver = (string)((dynamic)seed).Version; if (!string.IsNullOrEmpty(ver)) break; } catch { }
            }
            if (string.IsNullOrEmpty(ver)) ver = "unknown";
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoEDM", "logs");
            return Path.Combine(dir, $"SE_API_dump_{ver}.txt");
        }

        private static Guid GetLibGuid(ITypeLib lib, out string name)
        {
            lib.GetDocumentation(-1, out name, out _, out _, out _);
            lib.GetLibAttr(out IntPtr p);
            try
            {
                var attr = (System.Runtime.InteropServices.ComTypes.TYPELIBATTR)
                    Marshal.PtrToStructure(p, typeof(System.Runtime.InteropServices.ComTypes.TYPELIBATTR));
                return attr.guid;
            }
            finally { lib.ReleaseTLibAttr(p); }
        }

        private static void DumpOneLib(ITypeLib lib, string libName, Guid guid, TextWriter w)
        {
            int n = lib.GetTypeInfoCount();
            // O [guid] no cabeçalho é o que permite o merge (ReadKnownLibGuids) detectar,
            // num run futuro, que esta lib já foi capturada — não remover/reformatar sem
            // atualizar o regex lá E o parser em tools/generate_api_docs.py.
            w.WriteLine($"########## TYPELIB {libName} [{guid:D}] — {n} tipo(s) ##########");
            for (int i = 0; i < n; i++)
            {
                ITypeInfo ti = null;
                try
                {
                    lib.GetTypeInfoType(i, out System.Runtime.InteropServices.ComTypes.TYPEKIND kind);
                    lib.GetTypeInfo(i, out ti);
                    lib.GetDocumentation(i, out string tName, out _, out _, out _);
                    DumpTypeInfo(ti, tName, kind, w);
                }
                catch (Exception ex) { w.WriteLine($"# ERRO tipo[{i}]: {ex.Message}"); }
                finally { if (ti != null) Marshal.ReleaseComObject(ti); }
            }
            w.WriteLine();
        }

        private static void DumpTypeInfo(ITypeInfo ti, string name,
            System.Runtime.InteropServices.ComTypes.TYPEKIND kind, TextWriter w)
        {
            ti.GetTypeAttr(out IntPtr pAttr);
            int cFuncs, cVars;
            try
            {
                var attr = (System.Runtime.InteropServices.ComTypes.TYPEATTR)
                    Marshal.PtrToStructure(pAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPEATTR));
                cFuncs = attr.cFuncs;
                cVars = attr.cVars;
            }
            finally { ti.ReleaseTypeAttr(pAttr); }

            if (cFuncs == 0 && cVars == 0) return; // pula tipos vazios (alias etc.)
            w.WriteLine($"[{kind} {name}]");

            // Variáveis: para enums, o valor da constante é o que precisamos
            // (constantes como igQueryAll variam por versão do SE).
            for (int v = 0; v < cVars; v++)
            {
                ti.GetVarDesc(v, out IntPtr pVar);
                try
                {
                    var vd = (System.Runtime.InteropServices.ComTypes.VARDESC)
                        Marshal.PtrToStructure(pVar, typeof(System.Runtime.InteropServices.ComTypes.VARDESC));
                    var nb = new string[1];
                    ti.GetNames(vd.memid, nb, 1, out int got);
                    string vn = got > 0 ? nb[0] : "?";
                    if (vd.varkind == System.Runtime.InteropServices.ComTypes.VARKIND.VAR_CONST
                        && vd.desc.lpvarValue != IntPtr.Zero)
                    {
                        object val = Marshal.GetObjectForNativeVariant(vd.desc.lpvarValue);
                        w.WriteLine($"    {vn} = {val}");
                    }
                    else w.WriteLine($"    .{vn}");
                }
                catch { }
                finally { ti.ReleaseVarDesc(pVar); }
            }

            for (int f = 0; f < cFuncs; f++)
            {
                ti.GetFuncDesc(f, out IntPtr pFunc);
                try
                {
                    var fd = (System.Runtime.InteropServices.ComTypes.FUNCDESC)
                        Marshal.PtrToStructure(pFunc, typeof(System.Runtime.InteropServices.ComTypes.FUNCDESC));
                    var nb = new string[64];
                    ti.GetNames(fd.memid, nb, nb.Length, out int got);
                    if (got == 0) continue;

                    // Junta nome + tipo + direção (in/out) de cada parâmetro. Os tipos
                    // vêm do array de ELEMDESC (lprgelemdescParam); a direção, dos
                    // wParamFlags. É o que faltava para não caçar tipo em runtime.
                    int elemSize = Marshal.SizeOf(typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                    bool hasElems = fd.lprgelemdescParam != IntPtr.Zero; // senão, sem tipos (evita AV)
                    var ps = new List<string>();
                    for (int k = 0; k < fd.cParams; k++)
                    {
                        string pn = (k + 1 < got) ? nb[k + 1] : $"p{k}";
                        string pt = "?";
                        string dir = "";
                        try
                        {
                            if (!hasElems) { ps.Add(pn); continue; }
                            IntPtr pElem = fd.lprgelemdescParam + k * elemSize;
                            var ed = (System.Runtime.InteropServices.ComTypes.ELEMDESC)
                                Marshal.PtrToStructure(pElem, typeof(System.Runtime.InteropServices.ComTypes.ELEMDESC));
                            pt = TypeName(ti, ed.tdesc, 0);
                            dir = ParamDir((short)ed.desc.paramdesc.wParamFlags);
                        }
                        catch { }
                        ps.Add($"{dir}{pn}: {pt}");
                    }
                    string ret = "";
                    try { ret = " -> " + TypeName(ti, fd.elemdescFunc.tdesc, 0); } catch { }
                    string inv =
                        fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYGET ? "get " :
                        fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUT ? "put " :
                        fd.invkind == System.Runtime.InteropServices.ComTypes.INVOKEKIND.INVOKE_PROPERTYPUTREF ? "putref " : "";
                    w.WriteLine($"    {inv}{nb[0]}({string.Join(", ", ps)}){ret} [{fd.cParams} params]");
                }
                catch { }
                finally { ti.ReleaseFuncDesc(pFunc); }
            }
        }

        // VARTYPE (VARENUM) mais comuns nas typelibs do SE.
        private const short VT_PTR = 26, VT_SAFEARRAY = 27, VT_CARRAY = 28, VT_USERDEFINED = 29;

        /// <summary>Nome legível de um TYPEDESC (resolve ponteiro, SAFEARRAY e tipos definidos).</summary>
        private static string TypeName(ITypeInfo ti, System.Runtime.InteropServices.ComTypes.TYPEDESC td, int depth)
        {
            if (depth > 6) return "?";
            short vt = td.vt;
            if ((vt == VT_PTR || vt == VT_SAFEARRAY) && td.lpValue == IntPtr.Zero)
                return Vt(vt); // ponteiro nulo: não desreferencia (evitaria AccessViolation)
            if (vt == VT_PTR || vt == VT_SAFEARRAY)
            {
                var inner = (System.Runtime.InteropServices.ComTypes.TYPEDESC)
                    Marshal.PtrToStructure(td.lpValue, typeof(System.Runtime.InteropServices.ComTypes.TYPEDESC));
                string s = TypeName(ti, inner, depth + 1);
                return vt == VT_PTR ? s + "*" : "SAFEARRAY(" + s + ")";
            }
            if (vt == VT_CARRAY) return "carray[]";
            if (vt == VT_USERDEFINED)
            {
                try
                {
                    ti.GetRefTypeInfo((int)td.lpValue, out ITypeInfo rti);
                    rti.GetDocumentation(-1, out string un, out _, out _, out _);
                    Marshal.ReleaseComObject(rti);
                    return un;
                }
                catch { return "userdefined"; }
            }
            return Vt(vt);
        }

        private static string Vt(short vt)
        {
            switch (vt)
            {
                case 2: return "short";     case 3: return "int";
                case 4: return "float";     case 5: return "double";
                case 6: return "currency";  case 7: return "date";
                case 8: return "BSTR";      case 9: return "IDispatch";
                case 11: return "bool";     case 12: return "VARIANT";
                case 13: return "IUnknown"; case 16: return "sbyte";
                case 17: return "byte";     case 18: return "ushort";
                case 19: return "uint";     case 22: return "int";
                case 23: return "uint";     case 24: return "void";
                case 25: return "HRESULT";  case 30: return "LPSTR";
                case 31: return "LPWSTR";
                default: return "vt" + vt;
            }
        }

        /// <summary>Prefixo de direção do parâmetro a partir dos PARAMFLAGs.</summary>
        private static string ParamDir(short flags)
        {
            bool fin = (flags & 1) != 0, fout = (flags & 2) != 0, fopt = (flags & 16) != 0;
            string d = fin && fout ? "[in,out] " : fout ? "[out] " : "";
            return fopt ? "[opt]" + d : d;
        }

        private static readonly string[] GeometryKeywords =
            { "range", "box", "bound", "extent", "geom", "point", "param", "area", "normal", "vertex" };

        /// <summary>
        /// Loga o esquema de uma Face e as ASSINATURAS dos métodos ligados a
        /// geometria (range/box/bound/extent/geom/point/param/area/normal/vertex).
        /// Objetivo: descobrir a API real de bounding box de face no Solid Edge
        /// instalado, já que GetRange/GetExactRange não populam nada (Log 8).
        /// </summary>
        public static void LogGeometryDiscovery(object comFace)
        {
            var members = GetMemberNames(comFace);
            Log.Info($"[DIAG] Face expõe {members.Count} membros: {string.Join(", ", members)}");

            var wanted = members
                .Where(m => GeometryKeywords.Any(k => m.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            if (wanted.Length > 0)
            {
                Log.Info($"[DIAG] Face: assinaturas de geometria ({wanted.Length}) ---");
                LogSignatures(comFace, wanted);
            }
            else
            {
                Log.Warn("[DIAG] Face não expõe nenhum membro de geometria reconhecível.");
            }
        }

        private static readonly string[] ColorKeywords =
            { "style", "color", "colour", "diffuse", "appear", "fill", "paint", "render", "material" };

        /// <summary>
        /// Loga o esquema de uma Face (todos os membros) e sonda os membros que
        /// parecem relacionados a cor/estilo — lendo o valor e, se for outro objeto
        /// COM, listando os membros dele também. Objetivo: descobrir de uma vez qual
        /// é a API real de cor de face no Solid Edge instalado.
        /// </summary>
        public static void LogColorDiscovery(object comFace)
        {
            var members = GetMemberNames(comFace);
            Log.Info($"[DIAG] Face expõe {members.Count} membros: {string.Join(", ", members)}");

            foreach (var m in members)
            {
                if (!ColorKeywords.Any(k => m.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                try
                {
                    object val = comFace.GetType().InvokeMember(
                        m, BindingFlags.GetProperty, null, comFace, null);

                    if (val == null)
                        Log.Info($"[DIAG]   {m} = null");
                    else if (Marshal.IsComObject(val))
                        Log.Info($"[DIAG]   {m} -> objeto COM {{ {string.Join(", ", GetMemberNames(val))} }}");
                    else
                        Log.Info($"[DIAG]   {m} = {val} ({val.GetType().Name})");
                }
                catch (Exception ex)
                {
                    // Provavelmente é método (não propriedade) — o nome já ajuda.
                    Log.Info($"[DIAG]   {m} (get falhou: {ex.GetBaseException().Message})");
                }
            }
        }
    }
}
