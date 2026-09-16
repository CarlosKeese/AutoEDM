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

    /// <summary>
    /// Uma superfície e COMO REENCONTRÁ-LA depois de o modelo regenerar: o índice na coleção de
    /// construções e a caixa do corpo. O objeto COM sozinho não serve — ele morre no primeiro Add.
    /// </summary>
    internal sealed class SurfaceRef
    {
        public object Com;
        public string Label;

        /// <summary>Posição em <c>Constructions</c>; −1 quando veio da seleção do usuário.</summary>
        public int ConstructionIndex = -1;

        /// <summary>Caixa do corpo (mm) e nº de faces — a impressão digital que sobrevive à regeneração.</summary>
        public double[] BoxMin, BoxMax;
        public int FaceCount;
    }

    /// <summary>Um contorno já decidido na leitura, à espera de virar curva.</summary>
    internal sealed class RimLoopPlan
    {
        public SurfaceRef Surface;
        public string Label;
        public bool IsTop;
        public bool Closed;

        /// <summary>Distância entre as duas pontas soltas do contorno (mm).</summary>
        public double GapMm;

        /// <summary>Distância até a ponta solta MAIS PRÓXIMA de outro contorno do mesmo rim (mm).</summary>
        public double NeighbourMm = double.MaxValue;

        /// <summary>
        /// Aberto de um jeito SUSPEITO: alguma dessas distâncias é pequena demais para ser uma
        /// abertura de projeto. Contorno de corte reto tem as pontas longe (nas bordas da peça);
        /// pontas a centésimos de milímetro querem dizer que o encadeamento quebrou por tolerância.
        /// </summary>
        public bool Suspect;

        /// <summary>Identidade de cada aresta do contorno, na ordem do encadeamento.</summary>
        public List<EdgeKeys> Keys = new List<EdgeKeys>();

        /// <summary>Os proxies lidos na fase 1 — reserva para quando a releitura não achar as arestas.</summary>
        public List<object> Cached = new List<object>();
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
        /// <summary>Contornos que não fecham — normal num corte reto, que entra e sai da peça.</summary>
        public int LoopsOpen { get; set; }

        /// <summary>
        /// Dos abertos, os que abriram por POUCO (pontas a ≤ 1 mm, ou encostando em outro contorno):
        /// esses provavelmente são um perfil só que não encadeou, e é só desses que a janela avisa.
        /// </summary>
        public int LoopsSuspect { get; set; }

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
    /// VALIDADO no SE em 2026-09-16: <c>DerivedCurves.Add</c> aceita as arestas de uma superfície
    /// em peça SÍNCRONA, e o "Exportar perfis (IGES)" lê as curvas criadas aqui. A cascata de
    /// tentativas (composta → curva única, array by-ref → por valor) fica de pé de propósito: qual
    /// delas pega não foi fixado, e o log diz em cada rodada qual respondeu.
    ///
    /// O QUE CUSTOU CARO: cada <c>Add</c> invalida a superfície de origem e as arestas dela. Numa
    /// rodada só o PRIMEIRO Add funcionava; os seguintes levavam E_INVALIDARG mesmo com uma aresta
    /// só, e as superfícies lidas depois dele vinham com arestas sem sentido. Daí a divisão em
    /// duas fases de <see cref="Build"/> — e por isso nada aqui guarda proxy COM entre um Add e o
    /// próximo: guarda-se a IDENTIDADE (índice + caixa da superfície, ID + geometria da aresta) e
    /// reencontra-se tudo na hora. CONFIRMADO no SE (log `123058`): 7 curvas na mesma rodada,
    /// TODAS aceitas, 0 falhas — contra 1 curva e o resto em E_INVALIDARG antes da separação.
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

        /// <summary>
        /// DUAS FASES, e a separação não é estética (Carlos, 2026-09-16, log `114300`): o PRIMEIRO
        /// <c>DerivedCurves.Add</c> da rodada mata a superfície que está na mão. Depois dele a
        /// releitura não reencontrava NENHUMA aresta — nem por ID nem por geometria —, todo Add
        /// seguinte levava E_INVALIDARG (até com uma aresta só) e, pior, as superfícies lidas
        /// DEPOIS do primeiro Add vinham com arestas sem pé nem cabeça, gerando contorno aberto de
        /// uma aresta. Por isso: PLANEJA lendo tudo com o modelo parado, guardando só a identidade
        /// geométrica de cada aresta; e só então CRIA, reencontrando documento, superfície e
        /// arestas a cada curva.
        /// </summary>
        public static SurfaceRimResult Build(object partDoc)
        {
            var res = new SurfaceRimResult();
            RenameFailed = false;
            string source;
            List<SurfaceRef> surfaces = CollectSurfaces(partDoc, out source);
            res.Source = source;
            if (surfaces.Count == 0)
            {
                res.Message = "Nenhuma superfície encontrada na peça.\n\n" +
                              "Selecione a(s) superfície(s) antes de clicar, ou deixe a seleção vazia para o AutoEDM varrer todas as superfícies de construção da peça.";
                return res;
            }
            Log.Info("WEDM rim: " + surfaces.Count + " superfície(s) — " + source + ".");

            // ---- fase 1: ler. Nada aqui altera o modelo, então tudo que é lido vale. ----
            var plans = new List<RimLoopPlan>();
            foreach (SurfaceRef surf in surfaces) PlanSurface(surf, res, plans);

            if (plans.Count == 0)
            {
                res.RenameFailed = RenameFailed;
                res.Message = BuildEmptyMessage(res);
                return res;
            }

            // ---- fase 2: criar. A partir do 1º Add, nada do que foi lido continua vivo. ----
            res.Deleted = DeletePreviousCurves(partDoc);
            int index = 0;
            foreach (RimLoopPlan plan in plans) CreateCurve(partDoc, plan, res, ref index);

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
        private static List<SurfaceRef> CollectSurfaces(object partDoc, out string source)
        {
            var surfaces = new List<SurfaceRef>();
            object selectSet = Get(partDoc, "SelectSet");
            int n = Count(selectSet);
            for (int i = 1; i <= n; i++)
            {
                object item = Unwrap(Item(selectSet, i));
                if (item != null && HasFaces(item)) surfaces.Add(Describe(item, surfaces.Count + 1, -1));
            }
            if (surfaces.Count > 0)
            {
                // Veio da seleção: procura a mesma superfície na coleção de construções, porque é
                // por LÁ que ela vai ser reencontrada depois de cada Add (a seleção não sobrevive).
                foreach (SurfaceRef s in surfaces) s.ConstructionIndex = LocateInConstructions(partDoc, s);
                source = $"seleção ({surfaces.Count} de {n} item(ns))";
                return surfaces;
            }
            if (n > 0) Log.Warn($"WEDM rim: os {n} item(ns) selecionados não são superfícies — varrendo as construções da peça.");

            object constructions = Get(partDoc, "Constructions");
            int c = Count(constructions);
            for (int i = 1; i <= c; i++)
            {
                object item = Item(constructions, i);
                if (item == null) { Log.Warn($"WEDM rim: construção {i} não devolveu item."); continue; }

                object visible = Get(item, "Visible");
                if (visible is bool && !(bool)visible) { Log.Info($"WEDM rim: construção {i} — OCULTA, fora."); continue; }
                if (!HasFaces(item)) { Log.Info($"WEDM rim: {Report(item, i)} — sem face, fora (curva ou outro tipo)."); continue; }

                SurfaceRef surf = Describe(item, surfaces.Count + 1, i);
                surfaces.Add(surf);
                Log.Info($"WEDM rim: {Report(item, i)} — superfície, ENTRA como '{surf.Label}'{Box(surf)}.");
            }
            source = $"construções da peça ({surfaces.Count} de {c})";
            return surfaces;
        }

        /// <summary>Monta a referência da superfície: rótulo + a impressão digital que sobrevive à regeneração.</summary>
        private static SurfaceRef Describe(object item, int ordinal, int constructionIndex)
        {
            var surf = new SurfaceRef
            {
                Com = item,
                Label = SurfaceName(item, ordinal),
                ConstructionIndex = constructionIndex,
                FaceCount = Count(FacesOf(item)),
            };
            double[] min, max;
            if (FaceGeometry.TryGetBodyRangeMm(Get(item, "Body") ?? item, out min, out max))
            {
                surf.BoxMin = min;
                surf.BoxMax = max;
            }
            return surf;
        }

        private static string Box(SurfaceRef surf)
        {
            if (surf.BoxMin == null) return "";
            return string.Format(CultureInfo.InvariantCulture,
                " (bbox X {0:0.00}..{1:0.00} Y {2:0.00}..{3:0.00} Z {4:0.00}..{5:0.00}, {6} face(s))",
                surf.BoxMin[0], surf.BoxMax[0], surf.BoxMin[1], surf.BoxMax[1], surf.BoxMin[2], surf.BoxMax[2], surf.FaceCount);
        }

        /// <summary>Onde, na coleção de construções, está a superfície que o usuário selecionou (−1 = não achada).</summary>
        private static int LocateInConstructions(object partDoc, SurfaceRef surf)
        {
            object constructions = Get(partDoc, "Constructions");
            int c = Count(constructions);
            for (int i = 1; i <= c; i++)
            {
                object item = Item(constructions, i);
                if (item != null && SameSurface(item, surf)) return i;
            }
            Log.Warn($"WEDM rim: '{surf.Label}' (da seleção) não foi localizada em Constructions — " +
                     "se o Solid Edge invalidar a seleção no 1º Add, essa superfície pode falhar.");
            return -1;
        }

        /// <summary>Mesma superfície? Caixa do corpo (0,001 mm) e nº de faces — o que não muda quando o SE regenera.</summary>
        private static bool SameSurface(object item, SurfaceRef surf)
        {
            if (surf.BoxMin == null) return false;
            if (Count(FacesOf(item)) != surf.FaceCount) return false;

            double[] min, max;
            if (!FaceGeometry.TryGetBodyRangeMm(Get(item, "Body") ?? item, out min, out max)) return false;
            for (int k = 0; k < 3; k++)
                if (Math.Abs(min[k] - surf.BoxMin[k]) > 0.001 || Math.Abs(max[k] - surf.BoxMax[k]) > 0.001) return false;
            return true;
        }

        /// <summary>
        /// O que ESTE item de construção é, por todas as rotas de uma vez (Carlos, 2026-09-16,
        /// log `093112`): uma das duas construções entrou na lista como superfície e depois deu
        /// "0 aresta(s)" — com o contador sozinho não dá para saber se é um tipo mal classificado,
        /// se a coleção responde <c>Count</c> mas não entrega item, ou se as arestas estão noutra
        /// rota. Uma linha por construção resolve isso sem gastar outra rodada.
        /// </summary>
        private static string Report(object item, int ordinal)
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

        // ------------------------------------------------------------ fase 1: planejar

        /// <summary>Lê UMA superfície e guarda os contornos que ela deve gerar. NÃO altera o modelo.</summary>
        private static void PlanSurface(SurfaceRef surf, SurfaceRimResult res, List<RimLoopPlan> plans)
        {
            res.SurfacesRead++;
            var identity = new Dictionary<OpenEdgeSegment, EdgeKeys>();
            List<OpenEdgeSegment> edges = ReadEdges(surf.Com, surf.Label, res, identity);
            if (edges.Count == 0)
            {
                res.SurfacesWithoutRim++;
                res.Warnings.Add($"{surf.Label}: nenhuma aresta legível — veja no log o que essa construção é.");
                return;
            }

            SurfaceRimPick pick = SurfaceRims.Pick(edges);
            res.EdgesDropped += pick.EdgesDropped;
            if (pick.Rims.Count == 0)
            {
                res.SurfacesWithoutRim++;
                res.Warnings.Add($"{surf.Label}: nenhuma aresta paralela ao plano XY ({pick.EdgesNotHorizontal} aresta(s) sobem em Z).");
                Log.Warn($"WEDM rim: '{surf.Label}' sem extremidade horizontal — {pick.EdgesNotHorizontal} aresta(s) fora de plano XY.{Box(surf)}");
                return;
            }
            if (pick.Flat)
                Log.Info($"WEDM rim: '{surf.Label}' está toda no plano Z = {pick.Rims[0].Label} — fundo e topo são o mesmo contorno.");
            if (pick.LevelCount > 2)
                Log.Info($"WEDM rim: '{surf.Label}' tem {pick.LevelCount} níveis horizontais; só o Z mínimo e o máximo viram curva ({pick.EdgesDropped} aresta(s) intermediária(s) fora).");

            foreach (SurfaceRim rim in pick.Rims)
            {
                List<OpenEdgeLoop> loops = OpenEdgeLoops.Chain(rim.Edges, OpenEdgeLoops.DefaultJoinToleranceMm);
                var made = new List<RimLoopPlan>();

                foreach (OpenEdgeLoop loop in loops)
                {
                    var plan = new RimLoopPlan
                    {
                        Surface = surf,
                        Label = rim.Label,
                        IsTop = rim.IsTop,
                        Closed = loop.Closed,
                        GapMm = loop.GapMm,
                        NeighbourMm = NearestLooseEnd(loop, loops),
                    };
                    foreach (OpenEdgeSegment s in loop.Segments)
                    {
                        if (s.Com == null) continue;
                        plan.Cached.Add(s.Com);
                        EdgeKeys key;
                        plan.Keys.Add(identity.TryGetValue(s, out key) ? key : null);
                    }
                    if (plan.Cached.Count == 0) continue;

                    plan.Suspect = !loop.Closed &&
                                   (plan.GapMm <= SuspectGapMm || plan.NeighbourMm <= SuspectGapMm);
                    plans.Add(plan);
                    made.Add(plan);

                    string shape = loop.Closed ? "FECHADO" : $"ABERTO ({DescribeOpening(plan)})";
                    string line = $"WEDM rim: contorno {(rim.IsTop ? "de topo" : "de fundo")} de '{surf.Label}' em Z = {rim.Label}: " +
                                  $"{plan.Cached.Count} aresta(s) [{string.Join(" ", plan.Keys.Select(k => k?.Id ?? "?"))}], {shape}.";
                    if (plan.Suspect) Log.Warn(line);
                    else Log.Info(line);
                }

                int suspect = made.Count(p => p.Suspect);
                if (suspect > 0)
                    res.Warnings.Add($"{surf.Label}, Z = {rim.Label}: {suspect} contorno(s) abertos com as pontas MUITO perto " +
                                     $"(≤ {SuspectGapMm:0.0} mm) — provavelmente é um perfil só que não encadeou.");
            }
        }

        /// <summary>
        /// Contorno aberto com pontas a menos disto (mm) é SUSPEITO: corte reto de verdade tem as
        /// pontas lá nas bordas da peça, a dezenas de milímetros. Perto disso, o que houve foi uma
        /// folga entre arestas maior que a tolerância de encadeamento (0,01 mm) partindo um perfil
        /// só em pedaços.
        /// </summary>
        private const double SuspectGapMm = 1.0;

        /// <summary>Ponta solta mais próxima, entre os OUTROS contornos do mesmo rim (mm).</summary>
        private static double NearestLooseEnd(OpenEdgeLoop loop, List<OpenEdgeLoop> loops)
        {
            if (loop.Closed) return double.MaxValue;
            double best = double.MaxValue;
            foreach (OpenEdgeLoop other in loops)
            {
                if (ReferenceEquals(other, loop) || other.Closed) continue;
                foreach (double[] mine in new[] { loop.StartMm, loop.EndMm })
                    foreach (double[] theirs in new[] { other.StartMm, other.EndMm })
                        best = Math.Min(best, OpenEdgeLoops.Dist(mine, theirs));
            }
            return best;
        }

        /// <summary>Como o contorno está aberto, em números — é o que separa corte reto de encadeamento quebrado.</summary>
        private static string DescribeOpening(RimLoopPlan plan)
        {
            string gap = string.Format(CultureInfo.InvariantCulture, "pontas a {0:0.000} mm uma da outra", plan.GapMm);
            string neighbour = plan.NeighbourMm == double.MaxValue
                ? "único contorno aberto deste nível"
                : string.Format(CultureInfo.InvariantCulture, "outro contorno a {0:0.000} mm", plan.NeighbourMm);
            return plan.Suspect ? $"{gap}; {neighbour} — SUSPEITO, devia ter encadeado" : $"{gap}; {neighbour}";
        }

        // ------------------------------------------------------------ fase 2: criar

        /// <summary>
        /// Cria a curva de UM contorno, reencontrando tudo primeiro: o documento pela Application
        /// (que nunca se desconecta), a superfície pela coleção de construções e as arestas pela
        /// identidade. É o que faltava quando só o 1º Add da rodada funcionava.
        /// </summary>
        private static void CreateCurve(object partDoc, RimLoopPlan plan, SurfaceRimResult res, ref int index)
        {
            string name = $"{NamePrefix}{plan.Label} ({index + 1})";
            bool split = false;
            var created = new List<object>();
            var comEdges = new List<object>(plan.Cached);

            // Duas tentativas, cada uma reencontrando TUDO do zero: se o Add anterior tinha acabado
            // de mexer no modelo, a primeira resolução pode ter pegado o documento no meio da
            // regeneração. A segunda custa uma varredura de arestas e só acontece na falha.
            for (int attempt = 1; attempt <= 2 && created.Count == 0; attempt++)
            {
                object doc = FreshDoc(partDoc);
                object surf = ResolveSurface(doc, plan.Surface, name);
                comEdges = MatchFreshEdges(surf, plan, name);
                created = AddRimCurve(doc, comEdges, name, out split);
                if (created.Count == 0 && attempt == 1)
                    Log.Warn($"WEDM rim: '{name}' — 1ª tentativa falhou; reencontrando documento e superfície para tentar de novo.");
            }

            if (created.Count == 0)
            {
                res.LoopsFailed++;
                res.Warnings.Add($"{plan.Surface.Label}: contorno em Z = {plan.Label} não virou curva — veja o log.");
                return;
            }
            index++;
            if (split) res.LoopsSplit++;

            if (!plan.Closed)
            {
                // Aberto NÃO é defeito (Carlos, 2026-09-16): corte reto entra e sai da peça e não
                // tem por que fechar. Só vira aviso quando as pontas estão perto demais para isso.
                res.LoopsOpen++;
                string line = $"WEDM rim: '{name}' ({plan.Surface.Label}) é um contorno ABERTO — {DescribeOpening(plan)}.";
                if (plan.Suspect) { res.LoopsSuspect++; Log.Warn(line); }
                else Log.Info(line);
            }
            res.Curves.Add(new RimCurveCreated
            {
                Name = name,
                Label = plan.Label,
                IsTop = plan.IsTop,
                EdgeCount = comEdges.Count,
                PieceCount = created.Count,
                Closed = plan.Closed,
                Surface = plan.Surface.Label,
            });
            Log.Info($"WEDM rim: '{name}' — {comEdges.Count} aresta(s), {(plan.Closed ? "FECHADO" : "ABERTO")}, " +
                     $"{(plan.IsTop ? "topo" : "fundo")} de '{plan.Surface.Label}'" +
                     (split ? $" — FATIADO em {created.Count} curva(s), uma por aresta." : "") + ".");
        }

        /// <summary>
        /// O documento AGORA. A <c>Application</c> é a única referência que não se desconecta
        /// depois de um recurso ser criado; o documento em mãos pode estar morto.
        /// </summary>
        private static object FreshDoc(object partDoc)
        {
            object app = Get(partDoc, "Application");
            object doc = Get(app, "ActiveDocument");
            return doc ?? partDoc;
        }

        /// <summary>
        /// A superfície AGORA: pelo índice em <c>Constructions</c> e, se a caixa não bater (o SE
        /// reordenou), procurando pela impressão digital. Sem achar, devolve o objeto antigo — o
        /// Add vai falhar, mas com a falha registrada em vez de um proxy morto silencioso.
        /// </summary>
        private static object ResolveSurface(object doc, SurfaceRef surf, string name)
        {
            object constructions = Get(doc, "Constructions");
            int c = Count(constructions);

            if (surf.ConstructionIndex >= 1 && surf.ConstructionIndex <= c)
            {
                object item = Item(constructions, surf.ConstructionIndex);
                if (item != null && SameSurface(item, surf)) return item;
            }
            for (int i = 1; i <= c; i++)
            {
                object item = Item(constructions, i);
                if (item == null || !SameSurface(item, surf)) continue;
                Log.Info($"WEDM rim: '{name}' — '{surf.Label}' reencontrada na construção {i} (estava na {surf.ConstructionIndex}).");
                surf.ConstructionIndex = i;
                return item;
            }

            Log.Warn($"WEDM rim: '{name}' — '{surf.Label}' não foi reencontrada entre as {c} construções; " +
                     "seguindo com a referência antiga.");
            return surf.Com;
        }

        /// <summary>
        /// As arestas do contorno lidas AGORA da superfície, casadas por ID e, quando ele não bate,
        /// pela geometria — que é o que não muda (no log `114300` o SE renumerou tudo depois do 1º
        /// Add). O que não for reencontrado segue com o proxy da leitura, e o log diz quantos.
        /// </summary>
        private static List<object> MatchFreshEdges(object surf, RimLoopPlan plan, string name)
        {
            var comEdges = new List<object>(plan.Cached);
            if (surf == null) return comEdges;

            string route;
            var byId = new Dictionary<string, object>();
            var byGeometry = new Dictionary<string, object>();
            int read = 0;
            foreach (object e in ReadRawEdges(surf, out route))
            {
                double[] min, max, start, end; string why;
                if (!FaceGeometry.TryGetExactRangeMm(e, out min, out max)) continue;
                if (!EdgeGeometry.TryGetEndPointsMm(e, out start, out end, out why)) continue;
                read++;

                EdgeKeys key = EdgeIdentity(e, start, end, min, max);
                if (key.Id != null && !byId.ContainsKey(key.Id)) byId[key.Id] = e;
                if (!byGeometry.ContainsKey(key.Geometry)) byGeometry[key.Geometry] = e;
            }

            int byIdCount = 0, byGeometryCount = 0, missing = 0;
            for (int i = 0; i < comEdges.Count; i++)
            {
                EdgeKeys k = plan.Keys[i];
                object e;
                if (k?.Id != null && byId.TryGetValue(k.Id, out e)) { comEdges[i] = e; byIdCount++; }
                else if (k?.Geometry != null && byGeometry.TryGetValue(k.Geometry, out e)) { comEdges[i] = e; byGeometryCount++; }
                else missing++;
            }

            string how = $"{read} aresta(s) relidas por {route}: {byIdCount} casada(s) por ID, {byGeometryCount} pela geometria";
            if (missing > 0) Log.Warn($"WEDM rim: '{name}' — {how}, {missing} não reencontrada(s) (seguem com o proxy da leitura).");
            else Log.Info($"WEDM rim: '{name}' — {how}.");
            return comEdges;
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
