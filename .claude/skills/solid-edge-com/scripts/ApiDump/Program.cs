// ApiDump — inventário estático da API de automação do Solid Edge.
//
// Lê as TYPE LIBRARIES instaladas (.tlb) direto via ITypeLib/ITypeInfo e emite JSON com
// interfaces, coclasses, métodos, propriedades, parâmetros (tipo + direção + opcional +
// default), DISPIDs, offsets de vtable, enums com valores e interfaces de evento.
//
// POR QUE ITypeLib e não reflection sobre os interops:
//   A typelib é a única fonte que carrega a DIREÇÃO do parâmetro ([in]/[out]/[in,out]/
//   [opt]/[retval]) e o DISPID exatamente como o servidor COM os declara. O interop
//   gerenciado já traduziu isso para a convenção do C#, e essa tradução é justamente
//   onde as duas fontes divergem (há caso conhecido de um [in,out] importado como `out`,
//   o que faria o dado de entrada ser descartado no caminho). Aqui queremos a verdade
//   do servidor, para depois cruzar com a visão do interop (REA).
//
// Nada é registrado no sistema: LoadTypeLibEx usa REGKIND_NONE.
// Nada é executado: a typelib é metadata, não código.
//
// Uso:
//   ApiDump --dir "C:\Program Files\Siemens\Solid Edge 2026\Program" \
//           --out api.json --typelibs typelibs.json
//   ApiDump --file "...\Part.tlb" --out part.json

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeApiRecon.ApiDump;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options is null)
            {
                Console.Error.WriteLine(CliOptions.Usage);
                return 2;
            }

            var files = options.ResolveFiles();
            if (files.Count == 0)
            {
                Console.Error.WriteLine("ApiDump: nenhuma .tlb encontrada nos caminhos informados.");
                return 1;
            }

            Console.Error.WriteLine($"ApiDump: lendo {files.Count} type librar{(files.Count == 1 ? "y" : "ies")}...");

            var libs = new List<TypeLibRecord>(files.Count);
            foreach (var file in files)
            {
                var record = TypeLibDumper.Dump(file);
                libs.Add(record);
                var status = record.Error is null
                    ? $"{record.TypeCount} tipos, {record.Types.Sum(t => t.Members.Count)} membros"
                    : $"ERRO: {record.Error}";
                Console.Error.WriteLine($"  {Path.GetFileName(file),-28} {status}");
            }

            var root = new DumpRoot
            {
                GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Tool = "ApiDump (SE-API-Recon) — ITypeLib/ITypeInfo, REGKIND_NONE",
                Host = new HostInfo
                {
                    Os = Environment.OSVersion.VersionString,
                    Is64BitProcess = Environment.Is64BitProcess,
                    Runtime = RuntimeInformation.FrameworkDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                },
                Totals = Totals.From(libs),
                Typelibs = libs,
            };

            var json = JsonSerializer.Serialize(root, JsonOpts);
            WriteOut(options.OutPath, json);

            if (options.TypelibsPath is not null)
            {
                var summary = libs.Select(l => new TypeLibSummary
                {
                    File = l.File,
                    FileName = l.FileName,
                    Sha256 = l.Sha256,
                    Bytes = l.Bytes,
                    FileVersion = l.FileVersion,
                    FileLastWriteUtc = l.FileLastWriteUtc,
                    Name = l.Name,
                    Doc = l.Doc,
                    Libid = l.Libid,
                    Major = l.Major,
                    Minor = l.Minor,
                    Lcid = l.Lcid,
                    Flags = l.Flags,
                    TypeCount = l.TypeCount,
                    MemberCount = l.Types.Sum(t => t.Members.Count),
                    Error = l.Error,
                }).ToList();

                var summaryRoot = new TypelibsRoot
                {
                    GeneratedAtUtc = root.GeneratedAtUtc,
                    SourceDirectory = options.Directory,
                    Count = summary.Count,
                    Typelibs = summary,
                };
                WriteOut(options.TypelibsPath, JsonSerializer.Serialize(summaryRoot, JsonOpts));
            }

            var t = root.Totals;
            Console.Error.WriteLine(
                $"ApiDump: {t.Typelibs} typelibs lidas ({t.TypelibsFailed} com erro), " +
                $"{t.Types} tipos, {t.Interfaces} interfaces/dispinterfaces, {t.CoClasses} coclasses, " +
                $"{t.Enums} enums ({t.EnumValues} valores), {t.Members} membros, {t.Parameters} parâmetros.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ApiDump: falha inesperada: {ex}");
            return 1;
        }
    }

    private static void WriteOut(string? path, string json)
    {
        if (path is null)
        {
            Console.Out.Write(json);
            return;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, json);
        Console.Error.WriteLine($"ApiDump: escrito {path} ({new FileInfo(path).Length:N0} bytes)");
    }

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

internal sealed class CliOptions
{
    public const string Usage = """
        Uso:
          ApiDump --dir <pasta com .tlb> [--out api.json] [--typelibs typelibs.json]
          ApiDump --file <arquivo.tlb> [--file ...] [--out api.json]

        Sem --out, o JSON vai para stdout. Mensagens de progresso vão para stderr.
        """;

    public string? Directory { get; private init; }
    public List<string> Files { get; private init; } = [];
    public string? OutPath { get; private init; }
    public string? TypelibsPath { get; private init; }

    public static CliOptions? Parse(string[] args)
    {
        string? dir = null, outPath = null, tlPath = null;
        var files = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Next()
            {
                if (i + 1 >= args.Length) throw new ArgumentException($"{a} exige um valor.");
                return args[++i];
            }

            switch (a)
            {
                case "--dir": dir = Next(); break;
                case "--file": files.Add(Next()); break;
                case "--out": outPath = Next(); break;
                case "--typelibs": tlPath = Next(); break;
                case "-h" or "--help": return null;
                default:
                    Console.Error.WriteLine($"ApiDump: argumento desconhecido: {a}");
                    return null;
            }
        }

        if (dir is null && files.Count == 0) return null;
        return new CliOptions { Directory = dir, Files = files, OutPath = outPath, TypelibsPath = tlPath };
    }

    public List<string> ResolveFiles()
    {
        var result = new List<string>();
        if (Directory is not null && System.IO.Directory.Exists(Directory))
        {
            result.AddRange(System.IO.Directory
                .EnumerateFiles(Directory, "*.tlb", SearchOption.TopDirectoryOnly)
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase));
        }

        foreach (var f in Files)
        {
            if (File.Exists(f)) result.Add(Path.GetFullPath(f));
            else Console.Error.WriteLine($"ApiDump: aviso — não existe: {f}");
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

// ---------------------------------------------------------------------------
// Leitura da typelib
// ---------------------------------------------------------------------------

[SupportedOSPlatform("windows")]
internal static class TypeLibDumper
{
    private const int RegkindNone = 2;
    private const int MemberIdNil = -1;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void LoadTypeLibEx(
        string szFile, int regkind, [MarshalAs(UnmanagedType.Interface)] out ITypeLib pptlib);

    public static TypeLibRecord Dump(string path)
    {
        var fi = new FileInfo(path);
        var record = new TypeLibRecord
        {
            File = path,
            FileName = fi.Name,
            Bytes = fi.Exists ? fi.Length : 0,
            Sha256 = fi.Exists ? Sha256File(path) : null,
            FileLastWriteUtc = fi.Exists ? fi.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture) : null,
            FileVersion = TryFileVersion(path),
        };

        ITypeLib? lib = null;
        try
        {
            // REGKIND_NONE: carrega sem registrar nada no sistema.
            LoadTypeLibEx(path, RegkindNone, out lib);
        }
        catch (Exception ex)
        {
            record.Error = $"LoadTypeLibEx: 0x{ex.HResult:X8} {ex.Message}";
            return record;
        }

        try
        {
            lib.GetDocumentation(MemberIdNil, out var libName, out var libDoc, out _, out _);
            record.Name = libName;
            record.Doc = NullIfBlank(libDoc);

            var attrPtr = IntPtr.Zero;
            try
            {
                lib.GetLibAttr(out attrPtr);
                var attr = Marshal.PtrToStructure<TYPELIBATTR>(attrPtr);
                record.Libid = attr.guid.ToString("B");
                record.Major = attr.wMajorVerNum;
                record.Minor = attr.wMinorVerNum;
                record.Lcid = attr.lcid;
                record.Flags = Names.LibFlags((short)attr.wLibFlags);
            }
            finally
            {
                if (attrPtr != IntPtr.Zero) lib.ReleaseTLibAttr(attrPtr);
            }

            var count = lib.GetTypeInfoCount();
            record.TypeCount = count;

            for (var i = 0; i < count; i++)
            {
                try
                {
                    lib.GetTypeInfo(i, out var ti);
                    record.Types.Add(DumpType(ti));
                }
                catch (Exception ex)
                {
                    record.Types.Add(new TypeRecord
                    {
                        Name = $"<índice {i}>",
                        Kind = "error",
                        Error = $"0x{ex.HResult:X8} {ex.Message}",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            record.Error = $"leitura: 0x{ex.HResult:X8} {ex.Message}";
        }
        finally
        {
            if (lib is not null) Marshal.ReleaseComObject(lib);
        }

        return record;
    }

    private static TypeRecord DumpType(ITypeInfo ti)
    {
        ti.GetDocumentation(MemberIdNil, out var name, out var doc, out _, out _);
        var rec = new TypeRecord { Name = name, Doc = NullIfBlank(doc) };

        var attrPtr = IntPtr.Zero;
        try
        {
            ti.GetTypeAttr(out attrPtr);
            var attr = Marshal.PtrToStructure<TYPEATTR>(attrPtr);

            rec.Kind = Names.TypeKind(attr.typekind);
            rec.Guid = attr.guid == Guid.Empty ? null : attr.guid.ToString("B");
            rec.MajorVersion = attr.wMajorVerNum;
            rec.MinorVersion = attr.wMinorVerNum;
            rec.Flags = Names.TypeFlags(attr.wTypeFlags);
            rec.SizeInstance = attr.cbSizeInstance;
            rec.VtableSize = attr.cbSizeVft;

            if (attr.typekind == TYPEKIND.TKIND_ALIAS)
                rec.AliasFor = TypeNames.Of(attr.tdescAlias, ti);

            // Interfaces herdadas / implementadas. Numa coclass, a flag `source`
            // marca a interface de EVENTOS — é por aí que se descobre o que dá
            // para assinar com um sink.
            for (var i = 0; i < attr.cImplTypes; i++)
            {
                try
                {
                    ti.GetRefTypeOfImplType(i, out var href);
                    ti.GetRefTypeInfo(href, out var impl);
                    impl.GetDocumentation(MemberIdNil, out var implName, out _, out _, out _);
                    ti.GetImplTypeFlags(i, out var implFlags);
                    rec.Implements.Add(new ImplRecord
                    {
                        Name = implName,
                        Flags = Names.ImplTypeFlags(implFlags),
                    });
                    Marshal.ReleaseComObject(impl);
                }
                catch (Exception ex)
                {
                    rec.Implements.Add(new ImplRecord { Name = $"<impl {i}>", Flags = [$"error:0x{ex.HResult:X8}"] });
                }
            }

            for (var i = 0; i < attr.cFuncs; i++)
                rec.Members.Add(DumpFunc(ti, i));

            for (var i = 0; i < attr.cVars; i++)
                rec.Vars.Add(DumpVar(ti, i, attr.typekind));
        }
        catch (Exception ex)
        {
            rec.Error ??= $"0x{ex.HResult:X8} {ex.Message}";
        }
        finally
        {
            if (attrPtr != IntPtr.Zero) ti.ReleaseTypeAttr(attrPtr);
        }

        return rec;
    }

    private static MemberRecord DumpFunc(ITypeInfo ti, int index)
    {
        var fdPtr = IntPtr.Zero;
        var m = new MemberRecord();
        try
        {
            ti.GetFuncDesc(index, out fdPtr);
            var fd = Marshal.PtrToStructure<FUNCDESC>(fdPtr);

            m.DispId = fd.memid;
            m.Kind = Names.InvokeKind(fd.invkind);
            m.FuncKind = Names.FuncKind(fd.funckind);
            m.CallConv = fd.callconv.ToString();
            m.VtblOffset = fd.oVft;
            m.Flags = Names.FuncFlags(fd.wFuncFlags);
            m.OptionalParams = fd.cParamsOpt;
            m.Returns = TypeNames.Of(fd.elemdescFunc.tdesc, ti);

            // Nomes: índice 0 é o nome do membro, os demais são os parâmetros.
            var names = new string[fd.cParams + 1];
            try
            {
                ti.GetNames(fd.memid, names, names.Length, out var fetched);
                m.Name = fetched > 0 ? names[0] : $"<memid {fd.memid}>";
            }
            catch
            {
                ti.GetDocumentation(fd.memid, out var docName, out _, out _, out _);
                m.Name = docName ?? $"<memid {fd.memid}>";
            }

            try
            {
                ti.GetDocumentation(fd.memid, out _, out var doc, out _, out _);
                m.Doc = NullIfBlank(doc);
            }
            catch { /* documentação é opcional */ }

            var elemSize = Marshal.SizeOf<ELEMDESC>();
            for (var p = 0; p < fd.cParams; p++)
            {
                var elem = Marshal.PtrToStructure<ELEMDESC>(IntPtr.Add(fd.lprgelemdescParam, p * elemSize));
                var flags = elem.desc.paramdesc.wParamFlags;

                var pr = new ParamRecord
                {
                    Name = p + 1 < names.Length && !string.IsNullOrEmpty(names[p + 1])
                        ? names[p + 1]
                        : $"arg{p + 1}",
                    Type = TypeNames.Of(elem.tdesc, ti),
                    Flags = Names.ParamFlags(flags),
                };

                if ((flags & PARAMFLAG.PARAMFLAG_FHASDEFAULT) != 0 &&
                    elem.desc.paramdesc.lpVarValue != IntPtr.Zero)
                {
                    // PARAMDESCEX { ULONG cBytes; VARIANTARG varDefaultValue; }
                    // O VARIANT fica alinhado em 8 bytes depois do cBytes.
                    pr.Default = TryVariant(IntPtr.Add(elem.desc.paramdesc.lpVarValue, 8));
                }

                if ((flags & PARAMFLAG.PARAMFLAG_FRETVAL) != 0)
                    m.EffectiveReturns = pr.Type.TrimEnd('*');

                m.Params.Add(pr);
            }
        }
        catch (Exception ex)
        {
            m.Name ??= $"<func {index}>";
            m.Error = $"0x{ex.HResult:X8} {ex.Message}";
        }
        finally
        {
            if (fdPtr != IntPtr.Zero) ti.ReleaseFuncDesc(fdPtr);
        }

        m.Name ??= $"<func {index}>";
        m.Kind ??= "unknown";
        return m;
    }

    private static VarRecord DumpVar(ITypeInfo ti, int index, TYPEKIND kind)
    {
        var vdPtr = IntPtr.Zero;
        var v = new VarRecord();
        try
        {
            ti.GetVarDesc(index, out vdPtr);
            var vd = Marshal.PtrToStructure<VARDESC>(vdPtr);

            v.DispId = vd.memid;
            v.VarKind = vd.varkind.ToString();
            v.Flags = Names.VarFlags(vd.wVarFlags);
            v.Type = TypeNames.Of(vd.elemdescVar.tdesc, ti);

            var names = new string[1];
            try
            {
                ti.GetNames(vd.memid, names, 1, out var fetched);
                v.Name = fetched > 0 ? names[0] : $"<memid {vd.memid}>";
            }
            catch { v.Name = $"<memid {vd.memid}>"; }

            try
            {
                ti.GetDocumentation(vd.memid, out _, out var doc, out _, out _);
                v.Doc = NullIfBlank(doc);
            }
            catch { /* opcional */ }

            if (vd.varkind == VARKIND.VAR_CONST)
                v.Value = TryVariant(vd.desc.lpvarValue);
            else
                v.Offset = vd.desc.oInst;
        }
        catch (Exception ex)
        {
            v.Name ??= $"<var {index}>";
            v.Error = $"0x{ex.HResult:X8} {ex.Message}";
        }
        finally
        {
            if (vdPtr != IntPtr.Zero) ti.ReleaseVarDesc(vdPtr);
        }

        v.Name ??= $"<var {index}>";
        return v;
    }

    private static object? TryVariant(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        try
        {
            var value = Marshal.GetObjectForNativeVariant(ptr);
            return value switch
            {
                null => null,
                string or bool or byte or sbyte or short or ushort
                    or int or uint or long or ulong or float or double or decimal => value,
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryFileVersion(string path)
    {
        try
        {
            var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            return NullIfBlank(vi.FileVersion);
        }
        catch { return null; }
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ---------------------------------------------------------------------------
// Nomes de tipo
// ---------------------------------------------------------------------------

[SupportedOSPlatform("windows")]
internal static class TypeNames
{
    public static string Of(TYPEDESC td, ITypeInfo ti, int depth = 0)
    {
        if (depth > 12) return "…";
        var vt = (VarEnum)(ushort)td.vt;

        switch (vt)
        {
            case VarEnum.VT_PTR:
                return Of(Marshal.PtrToStructure<TYPEDESC>(td.lpValue), ti, depth + 1) + "*";

            case VarEnum.VT_SAFEARRAY:
                return $"SAFEARRAY({Of(Marshal.PtrToStructure<TYPEDESC>(td.lpValue), ti, depth + 1)})";

            case VarEnum.VT_CARRAY:
                return $"{Of(Marshal.PtrToStructure<TYPEDESC>(td.lpValue), ti, depth + 1)}[]";

            case VarEnum.VT_USERDEFINED:
                try
                {
                    ti.GetRefTypeInfo((int)td.lpValue, out var refTi);
                    refTi.GetDocumentation(-1, out var name, out _, out _, out _);
                    Marshal.ReleaseComObject(refTi);
                    return name ?? "UserDefined";
                }
                catch { return "UserDefined"; }

            default:
                return Scalar(vt);
        }
    }

    private static string Scalar(VarEnum vt) => vt switch
    {
        VarEnum.VT_EMPTY => "void",
        VarEnum.VT_NULL => "NULL",
        VarEnum.VT_VOID => "void",
        VarEnum.VT_HRESULT => "HRESULT",
        VarEnum.VT_I1 => "char",
        VarEnum.VT_UI1 => "byte",
        VarEnum.VT_I2 => "short",
        VarEnum.VT_UI2 => "ushort",
        VarEnum.VT_I4 => "int",
        VarEnum.VT_UI4 => "uint",
        VarEnum.VT_I8 => "long",
        VarEnum.VT_UI8 => "ulong",
        VarEnum.VT_INT => "int",
        VarEnum.VT_UINT => "uint",
        VarEnum.VT_R4 => "float",
        VarEnum.VT_R8 => "double",
        VarEnum.VT_CY => "CURRENCY",
        VarEnum.VT_DATE => "DATE",
        VarEnum.VT_BSTR => "BSTR",
        VarEnum.VT_BOOL => "VARIANT_BOOL",
        VarEnum.VT_VARIANT => "VARIANT",
        VarEnum.VT_DECIMAL => "DECIMAL",
        VarEnum.VT_DISPATCH => "IDispatch",
        VarEnum.VT_UNKNOWN => "IUnknown",
        VarEnum.VT_LPSTR => "LPSTR",
        VarEnum.VT_LPWSTR => "LPWSTR",
        VarEnum.VT_RECORD => "RECORD",
        _ => vt.ToString().Replace("VT_", string.Empty),
    };
}

// ---------------------------------------------------------------------------
// Tradução de flags para nomes legíveis
// ---------------------------------------------------------------------------

internal static class Names
{
    public static string TypeKind(TYPEKIND k) => k switch
    {
        TYPEKIND.TKIND_ENUM => "enum",
        TYPEKIND.TKIND_RECORD => "struct",
        TYPEKIND.TKIND_MODULE => "module",
        TYPEKIND.TKIND_INTERFACE => "interface",
        TYPEKIND.TKIND_DISPATCH => "dispinterface",
        TYPEKIND.TKIND_COCLASS => "coclass",
        TYPEKIND.TKIND_ALIAS => "typedef",
        TYPEKIND.TKIND_UNION => "union",
        _ => k.ToString(),
    };

    public static string InvokeKind(INVOKEKIND k) => k switch
    {
        INVOKEKIND.INVOKE_FUNC => "method",
        INVOKEKIND.INVOKE_PROPERTYGET => "propget",
        INVOKEKIND.INVOKE_PROPERTYPUT => "propput",
        INVOKEKIND.INVOKE_PROPERTYPUTREF => "propputref",
        _ => k.ToString(),
    };

    public static string FuncKind(FUNCKIND k) => k switch
    {
        FUNCKIND.FUNC_VIRTUAL => "virtual",
        FUNCKIND.FUNC_PUREVIRTUAL => "purevirtual",
        FUNCKIND.FUNC_NONVIRTUAL => "nonvirtual",
        FUNCKIND.FUNC_STATIC => "static",
        FUNCKIND.FUNC_DISPATCH => "dispatch",
        _ => k.ToString(),
    };

    public static List<string> ParamFlags(PARAMFLAG f)
    {
        var r = new List<string>(4);
        if ((f & PARAMFLAG.PARAMFLAG_FIN) != 0) r.Add("in");
        if ((f & PARAMFLAG.PARAMFLAG_FOUT) != 0) r.Add("out");
        if ((f & PARAMFLAG.PARAMFLAG_FLCID) != 0) r.Add("lcid");
        if ((f & PARAMFLAG.PARAMFLAG_FRETVAL) != 0) r.Add("retval");
        if ((f & PARAMFLAG.PARAMFLAG_FOPT) != 0) r.Add("optional");
        if ((f & PARAMFLAG.PARAMFLAG_FHASDEFAULT) != 0) r.Add("hasdefault");
        if ((f & PARAMFLAG.PARAMFLAG_FHASCUSTDATA) != 0) r.Add("hascustdata");
        return r;
    }

    public static List<string> FuncFlags(short f)
    {
        var r = new List<string>();
        void T(int bit, string n) { if ((f & bit) != 0) r.Add(n); }
        T(0x0001, "restricted");
        T(0x0002, "source");
        T(0x0004, "bindable");
        T(0x0008, "requestedit");
        T(0x0010, "displaybind");
        T(0x0020, "defaultbind");
        T(0x0040, "hidden");
        T(0x0080, "usesgetlasterror");
        T(0x0100, "defaultcollelem");
        T(0x0200, "uidefault");
        T(0x0400, "nonbrowsable");
        T(0x0800, "replaceable");
        T(0x1000, "immediatebind");
        return r;
    }

    public static List<string> TypeFlags(TYPEFLAGS f)
    {
        var r = new List<string>();
        void T(TYPEFLAGS bit, string n) { if ((f & bit) != 0) r.Add(n); }
        T(TYPEFLAGS.TYPEFLAG_FAPPOBJECT, "appobject");
        T(TYPEFLAGS.TYPEFLAG_FCANCREATE, "cancreate");
        T(TYPEFLAGS.TYPEFLAG_FLICENSED, "licensed");
        T(TYPEFLAGS.TYPEFLAG_FPREDECLID, "predeclid");
        T(TYPEFLAGS.TYPEFLAG_FHIDDEN, "hidden");
        T(TYPEFLAGS.TYPEFLAG_FCONTROL, "control");
        T(TYPEFLAGS.TYPEFLAG_FDUAL, "dual");
        T(TYPEFLAGS.TYPEFLAG_FNONEXTENSIBLE, "nonextensible");
        T(TYPEFLAGS.TYPEFLAG_FOLEAUTOMATION, "oleautomation");
        T(TYPEFLAGS.TYPEFLAG_FRESTRICTED, "restricted");
        T(TYPEFLAGS.TYPEFLAG_FAGGREGATABLE, "aggregatable");
        T(TYPEFLAGS.TYPEFLAG_FREPLACEABLE, "replaceable");
        T(TYPEFLAGS.TYPEFLAG_FDISPATCHABLE, "dispatchable");
        T(TYPEFLAGS.TYPEFLAG_FREVERSEBIND, "reversebind");
        T(TYPEFLAGS.TYPEFLAG_FPROXY, "proxy");
        return r;
    }

    public static List<string> ImplTypeFlags(IMPLTYPEFLAGS f)
    {
        var r = new List<string>();
        if ((f & IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULT) != 0) r.Add("default");
        if ((f & IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE) != 0) r.Add("source");
        if ((f & IMPLTYPEFLAGS.IMPLTYPEFLAG_FRESTRICTED) != 0) r.Add("restricted");
        if ((f & IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULTVTABLE) != 0) r.Add("defaultvtable");
        return r;
    }

    public static List<string> VarFlags(short f)
    {
        var r = new List<string>();
        void T(int bit, string n) { if ((f & bit) != 0) r.Add(n); }
        T(0x0001, "readonly");
        T(0x0002, "source");
        T(0x0004, "bindable");
        T(0x0008, "requestedit");
        T(0x0010, "displaybind");
        T(0x0020, "defaultbind");
        T(0x0040, "hidden");
        T(0x0080, "restricted");
        T(0x0100, "defaultcollelem");
        T(0x0200, "uidefault");
        T(0x0400, "nonbrowsable");
        T(0x0800, "replaceable");
        T(0x1000, "immediatebind");
        return r;
    }

    public static List<string> LibFlags(short f)
    {
        var r = new List<string>();
        void T(int bit, string n) { if ((f & bit) != 0) r.Add(n); }
        T(0x01, "restricted");
        T(0x02, "control");
        T(0x04, "hidden");
        T(0x08, "hasdiskimage");
        return r;
    }
}

// ---------------------------------------------------------------------------
// Modelo de saída
// ---------------------------------------------------------------------------

internal sealed class DumpRoot
{
    public string GeneratedAtUtc { get; set; } = "";
    public string Tool { get; set; } = "";
    public HostInfo Host { get; set; } = new();
    public Totals Totals { get; set; } = new();
    public List<TypeLibRecord> Typelibs { get; set; } = [];
}

internal sealed class HostInfo
{
    public string Os { get; set; } = "";
    public bool Is64BitProcess { get; set; }
    public string Runtime { get; set; } = "";
    public string Architecture { get; set; } = "";
}

internal sealed class Totals
{
    public int Typelibs { get; set; }
    public int TypelibsFailed { get; set; }
    public int Types { get; set; }
    public int Interfaces { get; set; }
    public int CoClasses { get; set; }
    public int Enums { get; set; }
    public int EnumValues { get; set; }
    public int Structs { get; set; }
    public int Modules { get; set; }
    public int Typedefs { get; set; }
    public int Members { get; set; }
    public int Methods { get; set; }
    public int PropertyGets { get; set; }
    public int PropertyPuts { get; set; }
    public int Parameters { get; set; }
    public int OutParameters { get; set; }
    public int OptionalParameters { get; set; }
    public int EventInterfaceLinks { get; set; }

    public static Totals From(List<TypeLibRecord> libs)
    {
        var t = new Totals { Typelibs = libs.Count, TypelibsFailed = libs.Count(l => l.Error is not null) };
        foreach (var lib in libs)
        {
            foreach (var ty in lib.Types)
            {
                t.Types++;
                switch (ty.Kind)
                {
                    case "interface" or "dispinterface": t.Interfaces++; break;
                    case "coclass": t.CoClasses++; break;
                    case "enum": t.Enums++; t.EnumValues += ty.Vars.Count; break;
                    case "struct" or "union": t.Structs++; break;
                    case "module": t.Modules++; break;
                    case "typedef": t.Typedefs++; break;
                }

                t.EventInterfaceLinks += ty.Implements.Count(i => i.Flags.Contains("source"));

                foreach (var m in ty.Members)
                {
                    t.Members++;
                    switch (m.Kind)
                    {
                        case "method": t.Methods++; break;
                        case "propget": t.PropertyGets++; break;
                        case "propput" or "propputref": t.PropertyPuts++; break;
                    }

                    foreach (var p in m.Params)
                    {
                        t.Parameters++;
                        if (p.Flags.Contains("out")) t.OutParameters++;
                        if (p.Flags.Contains("optional")) t.OptionalParameters++;
                    }
                }
            }
        }

        return t;
    }
}

internal sealed class TypeLibRecord
{
    public string File { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Sha256 { get; set; }
    public long Bytes { get; set; }
    public string? FileVersion { get; set; }
    public string? FileLastWriteUtc { get; set; }
    public string? Name { get; set; }
    public string? Doc { get; set; }
    public string? Libid { get; set; }
    public short Major { get; set; }
    public short Minor { get; set; }
    public int Lcid { get; set; }
    public List<string> Flags { get; set; } = [];
    public int TypeCount { get; set; }
    public string? Error { get; set; }
    public List<TypeRecord> Types { get; set; } = [];
}

internal sealed class TypeRecord
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Guid { get; set; }
    public short MajorVersion { get; set; }
    public short MinorVersion { get; set; }
    public List<string> Flags { get; set; } = [];
    public string? Doc { get; set; }
    public int SizeInstance { get; set; }
    public short VtableSize { get; set; }
    public string? AliasFor { get; set; }
    public string? Error { get; set; }
    public List<ImplRecord> Implements { get; set; } = [];
    public List<MemberRecord> Members { get; set; } = [];
    public List<VarRecord> Vars { get; set; } = [];
}

internal sealed class ImplRecord
{
    public string Name { get; set; } = "";
    public List<string> Flags { get; set; } = [];
}

internal sealed class MemberRecord
{
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public int DispId { get; set; }
    public string? FuncKind { get; set; }
    public string? CallConv { get; set; }
    public short VtblOffset { get; set; }
    public string? Returns { get; set; }
    public string? EffectiveReturns { get; set; }
    public short OptionalParams { get; set; }
    public List<string> Flags { get; set; } = [];
    public string? Doc { get; set; }
    public string? Error { get; set; }
    public List<ParamRecord> Params { get; set; } = [];
}

internal sealed class ParamRecord
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Flags { get; set; } = [];
    public object? Default { get; set; }
}

internal sealed class VarRecord
{
    public string? Name { get; set; }
    public int DispId { get; set; }
    public string? Type { get; set; }
    public string? VarKind { get; set; }
    public object? Value { get; set; }
    public int? Offset { get; set; }
    public List<string> Flags { get; set; } = [];
    public string? Doc { get; set; }
    public string? Error { get; set; }
}

internal sealed class TypelibsRoot
{
    public string GeneratedAtUtc { get; set; } = "";
    public string? SourceDirectory { get; set; }
    public int Count { get; set; }
    public List<TypeLibSummary> Typelibs { get; set; } = [];
}

internal sealed class TypeLibSummary
{
    public string File { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Sha256 { get; set; }
    public long Bytes { get; set; }
    public string? FileVersion { get; set; }
    public string? FileLastWriteUtc { get; set; }
    public string? Name { get; set; }
    public string? Doc { get; set; }
    public string? Libid { get; set; }
    public short Major { get; set; }
    public short Minor { get; set; }
    public int Lcid { get; set; }
    public List<string> Flags { get; set; } = [];
    public int TypeCount { get; set; }
    public int MemberCount { get; set; }
    public string? Error { get; set; }
}
