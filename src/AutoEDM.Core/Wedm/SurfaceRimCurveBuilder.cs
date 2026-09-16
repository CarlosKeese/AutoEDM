using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Selection;

namespace AutoEDM.Wedm
{
    /// <summary>Uma curva derivada criada na peça (uma extremidade de uma superfície).</summary>
    public sealed class RimCurveCreated
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public bool IsTop { get; set; }
        public int EdgeCount { get; set; }
        /// <summary>O contorno fechou (voltou ao ponto de partida) — perfil de corte inteiro.</summary>
        public bool Closed { get; set; }
        public string Surface { get; set; }
    }

    public sealed class SurfaceRimResult
    {
        public bool Ok { get; set; }
        /// <summary>Motivo, quando não criou nada.</summary>
        public string Message { get; set; }

        /// <summary>De onde saíram as superfícies (seleção do usuário ou varredura das construções).</summary>
        public string Source { get; set; }

        public int SurfacesRead { get; set; }
        public int SurfacesWithoutRim { get; set; }
        public int EdgesDropped { get; set; }
        public int LoopsOpen { get; set; }
        public int LoopsFailed { get; set; }
        /// <summary>Curvas de uma rodada anterior deste mesmo botão, apagadas antes de recriar.</summary>
        public int Deleted { get; set; }

        public List<RimCurveCreated> Curves { get; } = new List<RimCurveCreated>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Botão "Curvas das superfícies" do grupo WEDM (PEÇA síncrona, Carlos, 2026-09-15): reconhece
    /// as SUPERFÍCIES da peça e cria, na própria peça, uma CURVA DERIVADA sobre cada extremidade
    /// PARALELA AO PLANO XY delas — o rim de FUNDO (Z mínimo) e o de TOPO (Z máximo), que é por
    /// onde o fio corta. Não exporta nada: as curvas ficam na árvore para você conferir e o botão
    /// "Exportar perfis (IGES)" as lê depois como qualquer outra curva de construção.
    ///
    /// Quais superfícies: as SELECIONADAS, se houver seleção; senão todas as construções da peça
    /// que tenham FACE (corpo com face é superfície; sem face é curva — a mesma divisão que o
    /// <see cref="WedmCurveReader"/> já usava). O sólido da peça fica de fora.
    ///
    /// Como: as arestas de cada superfície viram <see cref="OpenEdgeSegment"/> (bbox +
    /// extremidades, em mm), <see cref="SurfaceRims"/> escolhe os dois níveis extremos e
    /// <see cref="OpenEdgeLoops"/> encadeia cada nível em contornos — um contorno, uma
    /// <c>Constructions.DerivedCurves.Add</c> composta, nomeada "WEDM Z = XX.XX (n)".
    ///
    /// A VALIDAR no SE: se <c>DerivedCurves.Add</c> aceita as arestas de uma superfície em peça
    /// SÍNCRONA (nunca foi chamado pelo AutoEDM) e como o array de arestas quer ser marshalado.
    /// </summary>
    public static class SurfaceRimCurveBuilder
    {
        private const int IgQueryAll = 1;
        private const int IgDCComposite = 1;   // DerivedCurveTypeConstants
        private const int IgDCCurve = 2;

        /// <summary>Prefixo do nome das curvas criadas aqui — é por ele que a rodada seguinte as reconhece e substitui.</summary>
        public const string NamePrefix = "WEDM Z = ";

        public static SurfaceRimResult Build(object partDoc)
        {
            var res = new SurfaceRimResult();
            string source;
            List<object> surfaces = CollectSurfaces(partDoc, out source);
            res.Source = source;
            if (surfaces.Count == 0)
            {
                res.Message = "Nenhuma superfície encontrada na peça.\n\n" +
                              "Selecione a(s) superfície(s) antes de clicar, ou deixe a seleção vazia para o AutoEDM varrer todas as superfícies de construção da peça.";
                return res;
            }
            Log.Info("WEDM rim: " + surfaces.Count + " superfície(s) — " + source + ".");

            res.Deleted = DeletePreviousCurves(partDoc);

            int index = 0;
            foreach (object surf in surfaces)
            {
                res.SurfacesRead++;
                string label = SurfaceName(surf, res.SurfacesRead);
                List<OpenEdgeSegment> edges = ReadEdges(surf, label, res);
                if (edges.Count == 0) { res.SurfacesWithoutRim++; continue; }

                SurfaceRimPick pick = SurfaceRims.Pick(edges);
                res.EdgesDropped += pick.EdgesDropped;
                if (pick.Rims.Count == 0)
                {
                    res.SurfacesWithoutRim++;
                    res.Warnings.Add($"{label}: nenhuma aresta paralela ao plano XY ({pick.EdgesNotHorizontal} aresta(s) sobem em Z).");
                    Log.Warn($"WEDM rim: '{label}' sem extremidade horizontal — {pick.EdgesNotHorizontal} aresta(s) fora de plano XY.");
                    // O bbox do CORPO diz se a superfície inteira é fina em Z (loft entre dois
                    // planos próximos) ou alta — junto com os Δ por aresta, separa "inclinada" de
                    // "tolerância apertada" sem precisar de outra rodada.
                    double[] bmin, bmax;
                    if (FaceGeometry.TryGetBodyRangeMm(Get(surf, "Body") ?? surf, out bmin, out bmax))
                        Log.Info(string.Format(CultureInfo.InvariantCulture,
                            "WEDM rim: '{0}' bbox do corpo: X {1:0.000}..{2:0.000}, Y {3:0.000}..{4:0.000}, Z {5:0.000}..{6:0.000} (altura {7:0.000} mm).",
                            label, bmin[0], bmax[0], bmin[1], bmax[1], bmin[2], bmax[2], bmax[2] - bmin[2]));
                    continue;
                }
                if (pick.Flat)
                    Log.Info($"WEDM rim: '{label}' está toda no plano Z = {pick.Rims[0].Label} — fundo e topo são o mesmo contorno.");
                if (pick.LevelCount > 2)
                    Log.Info($"WEDM rim: '{label}' tem {pick.LevelCount} níveis horizontais; só o Z mínimo e o máximo viram curva ({pick.EdgesDropped} aresta(s) intermediária(s) fora).");

                foreach (SurfaceRim rim in pick.Rims)
                    CreateRimCurves(partDoc, rim, label, res, ref index);
            }

            if (res.Curves.Count == 0)
            {
                res.Message = BuildEmptyMessage(res);
                return res;
            }
            res.Ok = true;
            Log.Info($"WEDM rim: {res.Curves.Count} curva(s) criada(s) em {res.SurfacesRead} superfície(s) " +
                     $"({res.LoopsOpen} contorno(s) aberto(s), {res.LoopsFailed} falha(s), {res.Deleted} curva(s) anterior(es) apagada(s)).");
            return res;
        }

        private static string BuildEmptyMessage(SurfaceRimResult res)
        {
            return $"Nenhuma curva foi criada a partir das {res.SurfacesRead} superfície(s) lidas." +
                   (res.LoopsFailed > 0
                       ? $"\n\n{res.LoopsFailed} contorno(s) foram achados, mas a criação da curva derivada falhou — veja o log."
                       : "\n\nNenhuma extremidade paralela ao plano XY foi encontrada — veja o log.");
        }

        // ------------------------------------------------------------ superfícies

        /// <summary>SELEÇÃO do usuário; sem seleção, todas as construções que têm FACE (= superfície).</summary>
        private static List<object> CollectSurfaces(object partDoc, out string source)
        {
            var surfaces = new List<object>();
            object selectSet = Get(partDoc, "SelectSet");
            int n = Count(selectSet);
            for (int i = 1; i <= n; i++)
            {
                object item = Unwrap(Item(selectSet, i));
                if (item != null && HasFaces(item)) surfaces.Add(item);
            }
            if (surfaces.Count > 0) { source = $"seleção ({surfaces.Count} de {n} item(ns))"; return surfaces; }
            if (n > 0) Log.Warn($"WEDM rim: os {n} item(ns) selecionados não são superfícies — varrendo as construções da peça.");

            object constructions = Get(partDoc, "Constructions");
            int c = Count(constructions);
            for (int i = 1; i <= c; i++)
            {
                object item = Item(constructions, i);
                if (item == null) continue;
                object visible = Get(item, "Visible");
                if (visible is bool && !(bool)visible) continue;
                if (HasFaces(item)) surfaces.Add(item);
            }
            source = $"construções da peça ({surfaces.Count} de {c})";
            return surfaces;
        }

        /// <summary>A superfície tem face? (corpo com face é superfície; sem face é curva).</summary>
        private static bool HasFaces(object item)
        {
            return FacesOf(item) != null;
        }

        /// <summary>A coleção de faces do item — direto, pelo corpo, ou nada.</summary>
        private static object FacesOf(object item)
        {
            foreach (object owner in new[] { item, Get(item, "Body") })
            {
                if (owner == null) continue;
                object faces = Indexed(owner, "Faces", IgQueryAll) ?? Get(owner, "Faces");
                if (Count(faces) > 0) return faces;
            }
            return null;
        }

        private static string SurfaceName(object surf, int ordinal)
        {
            string name = Get(surf, "Name") as string;
            return string.IsNullOrWhiteSpace(name) ? "superfície " + ordinal : name;
        }

        /// <summary>Item de SelectSet pode ser um INVÓLUCRO — o objeto de verdade está em <c>.Object</c>.</summary>
        private static object Unwrap(object item)
        {
            if (item == null) return null;
            if (HasFaces(item)) return item;
            object inner = Get(item, "Object");
            return inner != null && HasFaces(inner) ? inner : item;
        }

        // ------------------------------------------------------------ arestas

        /// <summary>
        /// Arestas da superfície com bbox e extremidades em mm. Ordem: as arestas do próprio item
        /// (uma vez cada); na falta delas, as das FACES — aí a mesma aresta aparece 2× (uma por
        /// face) e a repetição QUEBRARIA o encadeamento, por isso o filtro por <c>Edge.ID</c> e,
        /// quando ele não responde, pelas próprias extremidades.
        /// </summary>
        private static List<OpenEdgeSegment> ReadEdges(object surf, string label, SurfaceRimResult res)
        {
            var edges = new List<object>();
            object direct = Indexed(surf, "Edges", IgQueryAll) ?? Indexed(Get(surf, "Body"), "Edges", IgQueryAll);
            int n = Count(direct);
            for (int i = 1; i <= n; i++) { object e = Item(direct, i); if (e != null) edges.Add(e); }

            if (edges.Count == 0)
            {
                object faces = FacesOf(surf);
                int nf = Count(faces);
                for (int i = 1; i <= nf; i++)
                {
                    object face = Item(faces, i);
                    object fedges = Indexed(face, "Edges", IgQueryAll) ?? Get(face, "Edges");
                    int ne = Count(fedges);
                    for (int k = 1; k <= ne; k++) { object e = Item(fedges, k); if (e != null) edges.Add(e); }
                }
            }

            var segments = new List<OpenEdgeSegment>();
            var seenIds = new HashSet<int>();
            var seenEnds = new HashSet<string>();
            int noRange = 0, noEnds = 0;
            foreach (object e in edges)
            {
                int id;
                if (TryEdgeId(e, out id) && !seenIds.Add(id)) continue;

                // GetExactRange, NÃO GetRange: aqui o bbox decide se a aresta é PLANA, e a caixa
                // inflada que o GetRange devolve em spline (±0,005 mm medidos) reprovava rim
                // perfeitamente horizontal contra a tolerância de 1 µm. Ver TryGetExactRangeMm.
                double[] min, max;
                if (!FaceGeometry.TryGetExactRangeMm(e, out min, out max)) { noRange++; continue; }

                double[] start, end; string why;
                if (!EdgeGeometry.TryGetEndPointsMm(e, out start, out end, out why)) { noEnds++; continue; }
                if (!seenEnds.Add(EdgeKey(start, end, min, max))) continue;

                segments.Add(new OpenEdgeSegment
                {
                    Com = e,
                    StartMm = start,
                    EndMm = end,
                    ZMinMm = min[2],
                    ZMaxMm = max[2],
                    IsVertical = (max[2] - min[2]) > WedmLevels.PlanarToleranceMm,
                });
            }

            Log.Info($"WEDM rim: '{label}': {edges.Count} aresta(s), {segments.Count} legível(is)" +
                     (noRange > 0 ? $", {noRange} sem bbox" : "") +
                     (noEnds > 0 ? $", {noEnds} sem extremidades" : "") + ".");
            LogZSpans(segments, label);
            if (noRange + noEnds > 0)
                res.Warnings.Add($"{label}: {noRange + noEnds} aresta(s) não puderam ser lidas — veja o log.");
            return segments;
        }

        /// <summary>
        /// DIAGNÓSTICO (Carlos, 2026-09-15, log `224311`): "sem extremidade horizontal — 4 aresta(s)
        /// fora de plano XY" numa superfície feita por loft entre DUAS SPLINES HORIZONTAIS, onde
        /// duas arestas têm de ser planas. O contador de rejeitadas sozinho não distingue as duas
        /// causas possíveis, que pedem consertos opostos: superfície realmente inclinada (span de
        /// MILÍMETROS) ou <see cref="WedmLevels.PlanarToleranceMm"/> apertada demais para aresta
        /// B-spline refeita pela operação de superfície (span de MICRONS). Então mede e registra.
        ///
        /// Traz também o <c>GetExactRange</c> ao lado do <c>GetRange</c>: a ordem em
        /// <see cref="FaceGeometry.TryGetRangeMm"/> tenta o folgado primeiro, e em spline os dois
        /// podem divergir — se divergirem, o span verdadeiro é o do EXATO.
        /// </summary>
        private static void LogZSpans(List<OpenEdgeSegment> segments, string label)
        {
            const int Max = 12;   // superfície com muitas arestas não vira despejo de log
            int shown = 0;
            foreach (OpenEdgeSegment s in segments)
            {
                if (shown++ >= Max) { Log.Info($"WEDM rim: '{label}': (+{segments.Count - Max} aresta(s) não listada(s))"); break; }

                double span = s.ZMaxMm - s.ZMinMm;
                string exact = "";
                double[] a, b; string err;
                if (s.Com != null && FaceGeometry.TryTwoPointOutMm(s.Com, "GetRange", out a, out b, out err))
                {
                    double spanLoose = b[2] - a[2];
                    if (Math.Abs(spanLoose - span) > 1e-6)
                        exact = string.Format(CultureInfo.InvariantCulture, "  [GetRange inflaria para Δ={0:0.00000} mm]", spanLoose);
                }

                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "WEDM rim: '{0}' aresta {1}: Z {2:0.0000}..{3:0.0000}, Δ={4:0.00000} mm — {5} (tol {6} mm){7}",
                    label, shown, s.ZMinMm, s.ZMaxMm, span,
                    span > WedmLevels.PlanarToleranceMm ? "REJEITADA" : "horizontal",
                    WedmLevels.PlanarToleranceMm.ToString("0.00000", CultureInfo.InvariantCulture), exact));
            }
        }

        /// <summary>
        /// Chave da aresta (0,001 mm) — rede contra a MESMA aresta vinda por duas faces quando o
        /// <c>Edge.ID</c> não responde. Leva o bbox junto de propósito: as duas metades de um
        /// círculo têm as MESMAS duas pontas, e só pelas pontas uma delas seria jogada fora como
        /// se fosse repetida (o contorno ficaria aberto).
        /// </summary>
        private static string EdgeKey(double[] a, double[] b, double[] min, double[] max)
        {
            string ka = PointKey(a), kb = PointKey(b);
            string ends = string.CompareOrdinal(ka, kb) <= 0 ? ka + "|" + kb : kb + "|" + ka;
            return ends + "|" + PointKey(min) + "|" + PointKey(max);
        }

        private static string PointKey(double[] p)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.000};{1:0.000};{2:0.000}", p[0], p[1], p[2]);
        }

        private static bool TryEdgeId(object edge, out int id)
        {
            id = 0;
            try
            {
                object v = Get(edge, "ID");
                if (v == null) return false;
                id = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ curvas derivadas

        /// <summary>Encadeia o rim em contornos e cria uma curva derivada por contorno.</summary>
        private static void CreateRimCurves(object partDoc, SurfaceRim rim, string surface, SurfaceRimResult res, ref int index)
        {
            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(rim.Edges, OpenEdgeLoops.DefaultJoinToleranceMm);
            foreach (OpenEdgeLoop loop in loops)
            {
                var comEdges = new List<object>();
                foreach (OpenEdgeSegment s in loop.Segments) if (s.Com != null) comEdges.Add(s.Com);
                if (comEdges.Count == 0) continue;

                string name = $"{NamePrefix}{rim.Label} ({index + 1})";
                object curve = AddDerivedCurve(partDoc, comEdges, name);
                if (curve == null)
                {
                    res.LoopsFailed++;
                    res.Warnings.Add($"{surface}: contorno em Z = {rim.Label} não virou curva — veja o log.");
                    continue;
                }
                index++;

                if (!loop.Closed)
                {
                    res.LoopsOpen++;
                    Log.Warn($"WEDM rim: '{name}' ({surface}) é um contorno ABERTO — o perfil não fecha sozinho.");
                }
                res.Curves.Add(new RimCurveCreated
                {
                    Name = name,
                    Label = rim.Label,
                    IsTop = rim.IsTop,
                    EdgeCount = comEdges.Count,
                    Closed = loop.Closed,
                    Surface = surface,
                });
                Log.Info($"WEDM rim: '{name}' — {comEdges.Count} aresta(s), {(loop.Closed ? "FECHADO" : "ABERTO")}, " +
                         $"{(rim.IsTop ? "topo" : "fundo")} de '{surface}'.");
            }
        }

        /// <summary>
        /// <c>Constructions.DerivedCurves.Add(nArestas, ArestasArray, CurveType)</c> — a coleção é
        /// buscada FRESCA a cada curva (depois de um Add o modelo regenera e a referência velha
        /// pode estar morta, 0x80010114). Tenta composta e, se falhar, curva única; o array vai
        /// by-ref (a PIA 219 declara arrays de entrada como <c>out Array&amp;</c>) e, se não colar,
        /// por valor. Nunca lança: falha vira log + aviso.
        /// </summary>
        private static object AddDerivedCurve(object partDoc, List<object> comEdges, string name)
        {
            foreach (int curveType in new[] { IgDCComposite, IgDCCurve })
            {
                foreach (bool byRef in new[] { true, false })
                {
                    object collection = Get(Get(partDoc, "Constructions"), "DerivedCurves");
                    if (collection == null)
                    {
                        Log.Warn("WEDM rim: Constructions.DerivedCurves inacessível.");
                        return null;
                    }

                    // ARMADILHA do array `ref`: reusar o MESMO System.Array numa 2ª chamada
                    // corrompe a chamada — um array NOVO por tentativa.
                    Array edges = ToTypedEdgeArray(comEdges);
                    if (edges.Length == 0)
                    {
                        Log.Warn($"WEDM rim: '{name}' — nenhuma aresta expõe a interface Edge (E_NOINTERFACE).");
                        return null;
                    }

                    var args = new object[] { edges.Length, edges, curveType };
                    string how = $"tipo {curveType}, array {(byRef ? "by-ref" : "por valor")}";
                    try
                    {
                        var mod = new ParameterModifier(args.Length);
                        mod[1] = byRef;
                        object curve = collection.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null,
                            collection, args, new[] { mod }, CultureInfo.InvariantCulture, null);
                        if (curve == null)
                        {
                            Log.Warn($"WEDM rim: '{name}' — DerivedCurves.Add devolveu nulo ({how}).");
                            continue;
                        }

                        string status = FeatureStatus(curve);
                        if (status == "FALHOU")
                        {
                            Log.Warn($"WEDM rim: '{name}' criada com Status FALHOU ({how}) — apagando.");
                            TryDelete(curve);
                            continue;
                        }
                        Rename(curve, name);
                        TrySet(curve, "Visible", true);
                        return curve;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"WEDM rim: '{name}' — DerivedCurves.Add ({how}) falhou: {ex.GetBaseException().Message}");
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Apaga as curvas de uma rodada ANTERIOR deste botão (nome com o prefixo) — sem isso, a
        /// segunda rodada deixaria o perfil duplicado na peça e o .igs sairia com cada curva 2×.
        /// </summary>
        private static int DeletePreviousCurves(object partDoc)
        {
            object collection = Get(Get(partDoc, "Constructions"), "DerivedCurves");
            int n = Count(collection);
            var old = new List<object>();
            for (int i = 1; i <= n; i++)
            {
                object curve = Item(collection, i);
                string name = Get(curve, "Name") as string;
                if (name != null && name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase)) old.Add(curve);
            }

            int deleted = 0;
            foreach (object curve in old) if (TryDelete(curve)) deleted++;
            if (deleted > 0) Log.Info($"WEDM rim: {deleted} curva(s) de uma rodada anterior apagada(s) antes de recriar.");
            if (deleted < old.Count) Log.Warn($"WEDM rim: {old.Count - deleted} curva(s) anterior(es) não puderam ser apagadas — podem sair duplicadas na exportação.");
            return deleted;
        }

        private static bool TryDelete(object feature)
        {
            try
            {
                feature.GetType().InvokeMember("Delete", BindingFlags.InvokeMethod, null, feature, null, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("WEDM rim: falha ao apagar curva — " + ex.GetBaseException().Message);
                return false;
            }
        }

        private static void Rename(object curve, string name)
        {
            if (!TrySet(curve, "Name", name))
                Log.Warn($"WEDM rim: curva criada mas não foi possível nomeá-la '{name}' — a próxima rodada não vai reconhecê-la para substituir.");
        }

        private static bool TrySet(object com, string property, object value)
        {
            try
            {
                com.GetType().InvokeMember(property, BindingFlags.SetProperty, null, com, new[] { value }, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        /// <summary>O SE NÃO lança quando um recurso falha — ele põe o motivo no Status (igFeatureOK = 1216476310).</summary>
        private static string FeatureStatus(object feature)
        {
            try
            {
                object s = Get(feature, "Status");
                if (s == null) return "?";
                long v = Convert.ToInt64(s, CultureInfo.InvariantCulture);
                return v == 1216476310L ? "OK" : v == 1216476311L ? "FALHOU" : v.ToString(CultureInfo.InvariantCulture);
            }
            catch { return "?"; }
        }

        private static Array ToTypedEdgeArray(List<object> edges)
        {
            var list = new List<SolidEdgeGeometry.Edge>(edges.Count);
            int fail = 0;
            foreach (object e in edges) { try { list.Add((SolidEdgeGeometry.Edge)e); } catch { fail++; } }
            if (fail > 0) Log.Warn($"WEDM rim: {fail}/{edges.Count} aresta(s) não expõem a interface Edge (E_NOINTERFACE) — fora do contorno.");
            return list.ToArray();
        }

        // ------------------------------------------------------------ COM (late binding)

        private static object Get(object com, string property)
        {
            if (com == null) return null;
            try { return com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, null, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        /// <summary>Propriedade indexada por tipo de consulta (<c>Edges[1]</c>, <c>Faces[1]</c>).</summary>
        private static object Indexed(object com, string property, int index)
        {
            if (com == null) return null;
            try { return com.GetType().InvokeMember(property, BindingFlags.GetProperty, null, com, new object[] { index }, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        private static int Count(object collection)
        {
            try { return collection == null ? 0 : Convert.ToInt32(Get(collection, "Count"), CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static object Item(object collection, int index)
        {
            if (collection == null) return null;
            try { return collection.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { index }, CultureInfo.InvariantCulture); }
            catch { return null; }
        }
    }
}
