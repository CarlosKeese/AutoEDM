using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
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

        /// <summary>
        /// "SPY" ao vivo: loga o TIPO e TODAS as propriedades (nome = VALOR) de um objeto COM,
        /// recursando nos filhos que também são COM até <paramref name="maxDepth"/>. É a
        /// versão genérica do <see cref="LogColorDiscovery"/> — a mesma ideia do Solid Edge
        /// Spy (IDispatch→ITypeInfo), mas dirigida a UM objeto (ex.: uma feature de furo que o
        /// usuário criou na mão), para descobrir a API real sem adivinhar. Best-effort: membros
        /// que são MÉTODO (ou pedem args) são pulados; leituras que lançam são ignoradas.
        /// </summary>
        public static void DumpObject(string label, object comObject, int maxDepth = 1)
        {
            Log.Info($"[SPY] ===== {label} =====");
            DumpObjectInner(comObject, maxDepth, 0);
        }

        private static void DumpObjectInner(object obj, int maxDepth, int depth)
        {
            string pad = new string(' ', 2 + depth * 2);
            if (obj == null) { Log.Info($"[SPY]{pad}(null)"); return; }
            if (!Marshal.IsComObject(obj)) { Log.Info($"[SPY]{pad}= {obj} ({obj.GetType().Name})"); return; }

            var members = GetMemberNames(obj);
            Log.Info($"[SPY]{pad}COM: {{ {string.Join(", ", members)} }}");
            foreach (var m in members)
            {
                object val;
                try { val = obj.GetType().InvokeMember(m, BindingFlags.GetProperty, null, obj, null); }
                catch { continue; } // é método, ou pede argumentos — não é uma propriedade simples
                if (val == null) { Log.Info($"[SPY]{pad}  {m} = null"); continue; }
                if (Marshal.IsComObject(val))
                {
                    if (depth < maxDepth) { Log.Info($"[SPY]{pad}  {m} ->"); DumpObjectInner(val, maxDepth, depth + 2); }
                    else Log.Info($"[SPY]{pad}  {m} -> (COM; profundidade máx.)");
                }
                else Log.Info($"[SPY]{pad}  {m} = {val} ({val.GetType().Name})");
            }
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
        /// </summary>
        public static void DumpTypeLibraries(string outPath, IEnumerable<object> seeds)
        {
            var seenLibs = new HashSet<Guid>();
            int libCount = 0;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                // AutoFlush: escreve incremental. Se a introspecção travar (ex.: AV
                // em ponteiro COM), o que já saiu fica salvo em disco.
                using (var w = new StreamWriter(outPath, false, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    w.WriteLine("# Solid Edge COM type-library dump (AutoEDM ComDiagnostics)");
                    w.WriteLine("# API de geometria = METROS. Coleções = 1-based.");
                    w.WriteLine("# Formato: [TYPEKIND Nome] / método([dir]param: tipo, ...) -> ret [N params] / CONST = valor");
                    w.WriteLine();

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
                            if (!seenLibs.Add(guid)) continue; // lib já dumpada por outro seed
                            DumpOneLib(lib, libName, w);
                            libCount++;
                        }
                        catch (Exception ex) { w.WriteLine($"# ERRO em seed: {ex.Message}"); }
                        finally
                        {
                            if (lib != null) Marshal.ReleaseComObject(lib);
                            if (ti != null) Marshal.ReleaseComObject(ti);
                        }
                    }
                }
                Log.Info($"Typelib dump: {libCount} lib(s) -> {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"Falha ao gravar o dump em {outPath}.", ex);
            }
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

        private static void DumpOneLib(ITypeLib lib, string libName, TextWriter w)
        {
            int n = lib.GetTypeInfoCount();
            w.WriteLine($"########## TYPELIB {libName} — {n} tipo(s) ##########");
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
