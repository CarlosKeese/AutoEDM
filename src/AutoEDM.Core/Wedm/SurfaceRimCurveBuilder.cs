using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Selection;

namespace AutoEDM.Wedm
{
    /// <summary>
    /// As duas maneiras de reconhecer a MESMA aresta depois de o modelo regenerar: o
    /// <c>Edge.ID</c> e a geometria. Guardadas juntas porque nenhuma das duas serve sempre — no
    /// log `112004` a releitura por ID não reencontrou NENHUMA das 24 arestas do rim de topo (o SE
    /// renumerou), e a geometria, que não muda, é quem salva.
    /// </summary>
    internal sealed class EdgeKeys
    {
        /// <summary>"#123" quando o SE dá o ID; null quando não dá.</summary>
        public string Id;

        /// <summary>Pontas + bbox arredondados a 0,001 mm.</summary>
        public string Geometry;
    }

    /// <summary>Uma curva derivada criada na peça (uma extremidade de uma superfície).</summary>
    public sealed class RimCurveCreated
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public bool IsTop { get; set; }
        public int EdgeCount { get; set; }
        /// <summary>Quantas features saíram deste contorno: 1 quando a curva composta foi aceita, N quando foi preciso fatiar por aresta.</summary>
        public int PieceCount { get; set; } = 1;
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
        /// <summary>Contornos que o SE recusou inteiros e saíram como uma curva por aresta.</summary>
        public int LoopsSplit { get; set; }
        /// <summary>Alguma curva não ficou com o nome "WEDM Z = …" — a próxima rodada não vai substituí-la.</summary>
        public bool RenameFailed { get; set; }
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
    /// VALIDADO no SE 2026 em 2026-09-16: <c>DerivedCurves.Add</c> aceita as arestas de uma
    /// superfície em peça SÍNCRONA — 4 curvas em 2 superfícies, 0 falhas, e o "Exportar perfis
    /// (IGES)" leu as 4 na sequência, todas como B-spline 126. A cascata de tentativas
    /// (composta → curva única, array by-ref → por valor) fica de pé de propósito: qual delas
    /// pega não foi fixado, e o log diz em cada rodada qual respondeu.
    ///
    /// O que fez a 1ª rodada não achar extremidade nenhuma num loft entre duas splines NÃO era o
    /// ângulo da superfície: <c>Edge.GetRange</c> devolve caixa INFLADA em aresta B-spline
    /// (±0,005 mm medidos), e o rim plano era reprovado contra a tolerância de 1 µm. Daí o
    /// <see cref="FaceGeometry.TryGetExactRangeMm"/> aqui — exato para DECIDIR planaridade,
    /// folgado só para agrupar por proximidade.
    /// </summary>
    public static class SurfaceRimCurveBuilder
    {
        private const int IgQueryAll = 1;
        private const int IgDCComposite = 1;   // DerivedCurveTypeConstants
        private const int IgDCCurve = 2;

        /// <summary>Prefixo do nome das curvas criadas aqui — é por ele que a rodada seguinte as reconhece e substitui.</summary>
        public const string NamePrefix = "WEDM Z = ";

        /// <summary>
        /// Alguma renomeação não pegou nesta rodada. Estático porque quem cria a curva
        /// (<see cref="AddDerivedCurve"/>) não carrega o resultado; zerado no início de cada
        /// <see cref="Build"/> e copiado para o <see cref="SurfaceRimResult"/> no fim. O add-in é
        /// STA e roda um comando por vez, então não há duas execuções disputando o campo.
        /// </summary>
        private static bool RenameFailed;

        public static SurfaceRimResult Build(object partDoc)
        {
            var res = new SurfaceRimResult();
            RenameFailed = false;
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
                var identity = new Dictionary<OpenEdgeSegment, EdgeKeys>();
                List<OpenEdgeSegment> edges = ReadEdges(surf, label, res, identity);
                if (edges.Count == 0)
                {
                    res.SurfacesWithoutRim++;
                    res.Warnings.Add($"{label}: nenhuma aresta legível — veja no log o que essa construção é.");
                    continue;
                }

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
                    CreateRimCurves(partDoc, surf, rim, label, identity, res, ref index);
            }

            res.RenameFailed = RenameFailed;
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
                if (item == null) { Log.Warn($"WEDM rim: construção {i} não devolveu item."); continue; }

                string what = Describe(item, i);
                object visible = Get(item, "Visible");
                if (visible is bool && !(bool)visible) { Log.Info($"WEDM rim: {what} — OCULTA, fora."); continue; }
                if (HasFaces(item)) { surfaces.Add(item); Log.Info($"WEDM rim: {what} — superfície, ENTRA."); }
                else Log.Info($"WEDM rim: {what} — sem face, fora (curva ou outro tipo).");
            }
            source = $"construções da peça ({surfaces.Count} de {c})";
            return surfaces;
        }

        /// <summary>
        /// O que ESTE item de construção é, por todas as rotas de uma vez (Carlos, 2026-09-16,
        /// log `093112`): uma das duas construções entrou na lista como superfície e depois deu
        /// "0 aresta(s)" — com o contador sozinho não dá para saber se é um tipo mal classificado,
        /// se a coleção responde <c>Count</c> mas não entrega item, ou se as arestas estão noutra
        /// rota. Uma linha por construção resolve isso sem gastar outra rodada.
        /// </summary>
        private static string Describe(object item, int ordinal)
        {
            string name = Get(item, "Name") as string;
            string display = Get(item, "DisplayName") as string ?? Get(item, "EdgebarName") as string;
            object body = Get(item, "Body");
            return string.Format(CultureInfo.InvariantCulture,
                "construção {0} '{1}'{2} Type={3} | faces: item[1]={4} item={5} body[1]={6} | arestas: item[1]={7} body[1]={8}{9}",
                ordinal, string.IsNullOrWhiteSpace(name) ? "(sem nome)" : name,
                string.IsNullOrWhiteSpace(display) ? "" : " (" + display + ")",
                Get(item, "Type") ?? "?",
                Count(Indexed(item, "Faces", IgQueryAll)), Count(Get(item, "Faces")), Count(Indexed(body, "Faces", IgQueryAll)),
                Count(Indexed(item, "Edges", IgQueryAll)), Count(Indexed(body, "Edges", IgQueryAll)),
                body == null ? " | sem Body" : "");
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
        private static List<OpenEdgeSegment> ReadEdges(object surf, string label, SurfaceRimResult res,
            Dictionary<OpenEdgeSegment, EdgeKeys> identity)
        {
            string route;
            List<object> edges = ReadRawEdges(surf, out route);

            var segments = new List<OpenEdgeSegment>();
            var seen = new HashSet<string>();
            int noRange = 0, noEnds = 0, repeated = 0;
            foreach (object e in edges)
            {
                // GetExactRange, NÃO GetRange: aqui o bbox decide se a aresta é PLANA, e a caixa
                // inflada que o GetRange devolve em spline (±0,005 mm medidos) reprovava rim
                // perfeitamente horizontal contra a tolerância de 1 µm. Ver TryGetExactRangeMm.
                double[] min, max;
                if (!FaceGeometry.TryGetExactRangeMm(e, out min, out max)) { noRange++; continue; }

                double[] start, end; string why;
                if (!EdgeGeometry.TryGetEndPointsMm(e, out start, out end, out why)) { noEnds++; continue; }

                // Identidade: o ID quando o SE dá, e SEMPRE a geometria. Serve para não contar a
                // mesma aresta 2× (ela vem uma vez por face) E para reencontrá-la depois, já que o
                // proxy da aresta envelhece a cada Add.
                EdgeKeys key = EdgeIdentity(e, start, end, min, max);
                if (!seen.Add(key.Id ?? key.Geometry)) { repeated++; continue; }

                var seg = new OpenEdgeSegment
                {
                    Com = e,
                    StartMm = start,
                    EndMm = end,
                    ZMinMm = min[2],
                    ZMaxMm = max[2],
                    IsVertical = (max[2] - min[2]) > WedmLevels.PlanarToleranceMm,
                };
                segments.Add(seg);
                if (identity != null) identity[seg] = key;
            }

            Log.Info($"WEDM rim: '{label}': {edges.Count} aresta(s) por {route}, {segments.Count} legível(is)" +
                     (repeated > 0 ? $", {repeated} repetida(s)" : "") +
                     (noRange > 0 ? $", {noRange} sem bbox" : "") +
                     (noEnds > 0 ? $", {noEnds} sem extremidades" : "") + ".");
            LogZSpans(segments, label);
            if (noRange + noEnds > 0)
                res.Warnings.Add($"{label}: {noRange + noEnds} aresta(s) não puderam ser lidas — veja o log.");
            return segments;
        }

        /// <summary>
        /// As arestas CRUAS da superfície, com a rota que as achou no log — "0 aresta(s)" sem dizer
        /// por onde se tentou não deixa consertar nada (log `093112`: a 2ª construção entrou como
        /// superfície e não deu aresta nenhuma).
        /// </summary>
        private static List<object> ReadRawEdges(object surf, out string route)
        {
            var edges = new List<object>();
            object body = Get(surf, "Body");

            foreach (var source in new[]
            {
                new { Name = "item.Edges[1]", Col = Indexed(surf, "Edges", IgQueryAll) },
                new { Name = "body.Edges[1]", Col = Indexed(body, "Edges", IgQueryAll) },
                new { Name = "item.Edges",    Col = Get(surf, "Edges") },
            })
            {
                int n = Count(source.Col);
                if (n == 0) continue;
                for (int i = 1; i <= n; i++) { object e = Item(source.Col, i); if (e != null) edges.Add(e); }
                if (edges.Count > 0) { route = $"{source.Name} ({n})"; return edges; }
            }

            // Sem coleção de arestas no item: face a face (cada aresta vem uma vez por face, e a
            // repetição é filtrada pela identidade depois).
            object faces = FacesOf(surf);
            int nf = Count(faces), noEdges = 0;
            for (int i = 1; i <= nf; i++)
            {
                object face = Item(faces, i);
                if (face == null) { noEdges++; continue; }
                object fedges = Get(face, "Edges") ?? Indexed(face, "Edges", IgQueryAll);
                int ne = Count(fedges);
                if (ne == 0) { noEdges++; continue; }
                for (int k = 1; k <= ne; k++) { object e = Item(fedges, k); if (e != null) edges.Add(e); }
            }
            route = $"{nf} face(s)" + (noEdges > 0 ? $", {noEdges} sem arestas legíveis" : "");
            return edges;
        }

        /// <summary>Identidade da aresta: o <c>Edge.ID</c> (quando o SE dá) e a geometria, sempre as duas.</summary>
        private static EdgeKeys EdgeIdentity(object edge, double[] start, double[] end, double[] min, double[] max)
        {
            int id;
            return new EdgeKeys
            {
                Id = TryEdgeId(edge, out id) ? "#" + id.ToString(CultureInfo.InvariantCulture) : null,
                Geometry = EdgeKey(start, end, min, max),
            };
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

        /// <summary>
        /// Encadeia o rim em contornos e cria uma curva derivada por contorno. O que o contorno É
        /// (arestas, fechado, identidades) vai para o log ANTES do <c>Add</c>: na 1ª versão só o
        /// SUCESSO era descrito, então o contorno que falhava não deixava rastro nenhum
        /// (log `093112`: o rim de topo recusado com E_INVALIDARG e nem o nº de arestas dele se
        /// sabia). Antes de cada <c>Add</c> as arestas são RELIDAS da peça — ver
        /// <see cref="RefreshEdges"/>.
        /// </summary>
        private static void CreateRimCurves(object partDoc, object surf, SurfaceRim rim, string surface,
            Dictionary<OpenEdgeSegment, EdgeKeys> identity, SurfaceRimResult res, ref int index)
        {
            List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(rim.Edges, OpenEdgeLoops.DefaultJoinToleranceMm);
            foreach (OpenEdgeLoop loop in loops)
            {
                var comEdges = new List<object>();
                var keys = new List<EdgeKeys>();
                foreach (OpenEdgeSegment s in loop.Segments)
                {
                    if (s.Com == null) continue;
                    comEdges.Add(s.Com);
                    EdgeKeys key;
                    keys.Add(identity != null && identity.TryGetValue(s, out key) ? key : null);
                }
                if (comEdges.Count == 0) continue;

                string name = $"{NamePrefix}{rim.Label} ({index + 1})";
                Log.Info($"WEDM rim: contorno {(rim.IsTop ? "de topo" : "de fundo")} de '{surface}' em Z = {rim.Label}: " +
                         $"{comEdges.Count} aresta(s) [{string.Join(" ", keys.Select(k => k?.Id ?? "?"))}], " +
                         $"{(loop.Closed ? "FECHADO" : "ABERTO")} — criando '{name}'.");

                RefreshEdges(surf, keys, comEdges, name);

                bool split;
                List<object> created = AddRimCurve(partDoc, comEdges, name, out split);
                if (created.Count == 0)
                {
                    res.LoopsFailed++;
                    res.Warnings.Add($"{surface}: contorno em Z = {rim.Label} não virou curva — veja o log.");
                    continue;
                }
                index++;
                if (split) res.LoopsSplit++;

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
                    PieceCount = created.Count,
                    Closed = loop.Closed,
                    Surface = surface,
                });
                Log.Info($"WEDM rim: '{name}' — {comEdges.Count} aresta(s), {(loop.Closed ? "FECHADO" : "ABERTO")}, " +
                         $"{(rim.IsTop ? "topo" : "fundo")} de '{surface}'" +
                         (split ? $" — FATIADO em {created.Count} curva(s), uma por aresta." : "") + ".");
            }
        }

        /// <summary>
        /// Troca as arestas em mãos pelas MESMAS arestas relidas agora da peça, casando pelo
        /// <c>Edge.ID</c>/geometria (<see cref="EdgeIdentity"/>).
        ///
        /// Por quê: as arestas são lidas uma vez por superfície, mas cada <c>DerivedCurves.Add</c>
        /// regenera o modelo — e no log `093112` o rim de FUNDO virou curva e o de TOPO, da mesma
        /// superfície e com a mesma forma de chamada, foi recusado com E_INVALIDARG nas quatro
        /// tentativas. Proxy envelhecido é a explicação que cabe: o que mudou entre as duas
        /// chamadas foi só o Add que aconteceu no meio. Releitura barata (uma varredura de arestas)
        /// contra um erro que custa a curva inteira. Se a releitura não achar alguma aresta, as
        /// antigas ficam — pior que hoje não fica.
        ///
        /// Casa primeiro por ID e, quando ele não bate, PELA GEOMETRIA: no log `112004` a releitura
        /// do rim de topo não reencontrou nenhuma das 24 arestas por ID (o SE renumerou depois do
        /// Add anterior), e a geometria é o que não muda entre uma leitura e outra.
        /// </summary>
        private static void RefreshEdges(object surf, List<EdgeKeys> keys, List<object> comEdges, string name)
        {
            if (keys.Count != comEdges.Count || keys.All(k => k == null)) return;

            string route;
            var byId = new Dictionary<string, object>();
            var byGeometry = new Dictionary<string, object>();
            foreach (object e in ReadRawEdges(surf, out route))
            {
                double[] min, max, start, end; string why;
                if (!FaceGeometry.TryGetExactRangeMm(e, out min, out max)) continue;
                if (!EdgeGeometry.TryGetEndPointsMm(e, out start, out end, out why)) continue;

                EdgeKeys key = EdgeIdentity(e, start, end, min, max);
                if (key.Id != null && !byId.ContainsKey(key.Id)) byId[key.Id] = e;
                if (!byGeometry.ContainsKey(key.Geometry)) byGeometry[key.Geometry] = e;
            }

            int byIdCount = 0, byGeometryCount = 0, missing = 0;
            for (int i = 0; i < comEdges.Count; i++)
            {
                EdgeKeys k = keys[i];
                object e;
                if (k?.Id != null && byId.TryGetValue(k.Id, out e)) { comEdges[i] = e; byIdCount++; }
                else if (k?.Geometry != null && byGeometry.TryGetValue(k.Geometry, out e)) { comEdges[i] = e; byGeometryCount++; }
                else missing++;
            }

            string how = $"{byIdCount} por ID, {byGeometryCount} pela geometria";
            if (missing > 0)
                Log.Warn($"WEDM rim: '{name}' — releitura: {how}, {missing} não reencontrada(s) (essas seguem com o proxy antigo).");
            else
                Log.Info($"WEDM rim: '{name}' — {comEdges.Count} aresta(s) relida(s) antes do Add ({how}).");
        }

        /// <summary>
        /// Cria a curva do contorno: UMA curva composta com todas as arestas e, se o SE recusar,
        /// uma curva POR ARESTA (Carlos, 2026-09-16). O fatiado é feio na árvore, mas o perfil sai
        /// — e para a exportação tanto faz quantas features são, ela lê as arestas de todas.
        /// </summary>
        private static List<object> AddRimCurve(object partDoc, List<object> comEdges, string name, out bool split)
        {
            split = false;
            var created = new List<object>();

            object whole = AddDerivedCurve(partDoc, comEdges, name, quiet: false);
            if (whole != null) { created.Add(whole); return created; }
            if (comEdges.Count < 2) return created;

            Log.Warn($"WEDM rim: '{name}' — curva composta recusada; tentando uma curva por aresta.");
            for (int i = 0; i < comEdges.Count; i++)
            {
                string pieceName = $"{name.Substring(0, name.Length - 1)}.{i + 1})";
                object piece = AddDerivedCurve(partDoc, new List<object> { comEdges[i] }, pieceName, quiet: true);
                if (piece != null) created.Add(piece);
                else Log.Warn($"WEDM rim: '{pieceName}' — aresta {i + 1} também foi recusada sozinha.");
            }
            split = created.Count > 0;
            if (split) Log.Info($"WEDM rim: '{name}' — {created.Count} de {comEdges.Count} aresta(s) viraram curva separada.");
            return created;
        }

        /// <summary>
        /// <c>Constructions.DerivedCurves.Add(nArestas, ArestasArray, CurveType)</c> — a coleção é
        /// buscada FRESCA a cada curva (depois de um Add o modelo regenera e a referência velha
        /// pode estar morta, 0x80010114). Tenta composta e, se falhar, curva única; o array vai
        /// by-ref (a PIA 219 declara arrays de entrada como <c>out Array&amp;</c>) e, se não colar,
        /// por valor. Nunca lança: falha vira log + aviso.
        ///
        /// <paramref name="quiet"/> cala as tentativas individuais: no fallback por aresta são
        /// 4 tentativas × N arestas, e o log viraria despejo — quem chama resume o resultado.
        /// </summary>
        private static object AddDerivedCurve(object partDoc, List<object> comEdges, string name, bool quiet)
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
                            if (!quiet) Log.Warn($"WEDM rim: '{name}' — DerivedCurves.Add devolveu nulo ({how}).");
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
                        if (!quiet) Log.Info($"WEDM rim: '{name}' — DerivedCurves.Add aceitou ({how}), Status {status}.");
                        return curve;
                    }
                    catch (Exception ex)
                    {
                        if (!quiet) Log.Warn($"WEDM rim: '{name}' — DerivedCurves.Add ({how}) falhou: {ex.GetBaseException().Message}");
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

        /// <summary>
        /// Nomeia a curva e CONFERE lendo o nome de volta (Carlos, 2026-09-16): se a gravação não
        /// pegar sem lançar, a curva fica com o nome de fábrica do SE ("Derived Curve_1"), a
        /// <see cref="DeletePreviousCurves"/> nunca a reconhece e cada rodada empilha um perfil
        /// duplicado na peça — exatamente o sintoma "0 curva(s) anterior(es) apagada(s)" do log
        /// `093112`, que o aviso de exceção sozinho não explicava.
        /// </summary>
        private static void Rename(object curve, string name)
        {
            bool set = TrySet(curve, "Name", name);
            string readBack = Get(curve, "Name") as string;
            if (set && string.Equals(readBack, name, StringComparison.Ordinal)) return;

            RenameFailed = true;
            Log.Warn($"WEDM rim: a curva criada não ficou com o nome '{name}' " +
                     $"(gravação {(set ? "aceita" : "recusada")}, nome agora: '{readBack ?? "?"}') — " +
                     "a próxima rodada não vai reconhecê-la para substituir; apague à mão antes de rodar de novo.");
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
