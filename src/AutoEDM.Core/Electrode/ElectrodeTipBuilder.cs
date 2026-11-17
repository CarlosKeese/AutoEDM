using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Model;
using AutoEDM.Sealing;
using AutoEDM.Selection;

namespace AutoEDM.Electrode
{
    public sealed class TipRequest
    {
        /// <summary>O .par da peça a erodir. A cópia do corpo LÊ ESTE ARQUIVO (tem de estar salvo).</summary>
        public string CavityFile;
        /// <summary>Local da peça a erodir → local da peça do eletrodo.</summary>
        public OccurrenceTransform CavityInElectrode;
        /// <summary>Meia pegada do prisma (já com a margem), centrada na origem da peça do eletrodo (mm).</summary>
        public double HalfXMm, HalfYMm;
        /// <summary>Z do plano de fechamento (topo do aço) no referencial do eletrodo; a origem é o fundo da queima.</summary>
        public double RefZMm;
        /// <summary>Altura do pescoço acima do fechamento: é o que mantém a base sem encostar na peça.</summary>
        public double NeckMm = 1.0;
        /// <summary>
        /// Onde o prisma começa (≤ 0, mm, referencial do eletrodo). 0 = na origem. O pedaço de um eletrodo dividido
        /// começa um pouco abaixo do fundo do eletrodo INTEIRO: com o fundo do prisma no fundo do próprio pedaço, ou
        /// coplanar com o piso mais fundo, a booleana deu E_NOINTERFACE em 2 dos 9 pedaços do 15428.100 (medido em
        /// 2026-11-13). O que fica abaixo do pedaço é aço, que a subtração tira.
        /// </summary>
        public double BottomZMm = 0.0;
        public string Label;
        /// <summary>
        /// T2c/C1: caixas a tirar da ponta antes do pescoço (mm, referencial do eletrodo) — o enchimento dos raios
        /// cortados pela divisa e a lâmina da sobreposição. Cada uma em duas versões: rente e, se a rente falhar, com folga.
        /// </summary>
        public List<TipCut> Cuts;
        /// <summary>T6: semiespaços a tirar da ponta (divisão NA QUINA), mm, referencial do eletrodo; o lado da normal sai.</summary>
        public List<CutPlane> Planes;
    }

    /// <summary>Uma caixa a tirar (T2c/C1), mm, referencial do eletrodo.</summary>
    public sealed class TipCut
    {
        public string What;
        public BoundingBox Flush;
        public BoundingBox Clear;
        /// <summary>Lâmina da sobreposição (e não enchimento de raio): entra na conta à parte.</summary>
        public bool IsStrip;
        /// <summary>T5: alívio de uma região destacada (o principal perde a pegada dela).</summary>
        public bool IsRelief;
        /// <summary>T7: enchimento como PRISMA (polígono 3D num plano normal a <see cref="Axis"/>, extrudado simétrico
        /// <see cref="LengthMm"/>) — o triângulo canto–tangentes de um raio inclinado em planta, que a caixa cortaria demais.
        /// Null: vale a caixa.</summary>
        public double[][] Prism;
        public double[] Axis;
        public double LengthMm;

        public TipCut Shifted(double dx, double dy, double dz)
        {
            BoundingBox S(BoundingBox b) => new BoundingBox { MinX = b.MinX + dx, MaxX = b.MaxX + dx, MinY = b.MinY + dy, MaxY = b.MaxY + dy, MinZ = b.MinZ + dz, MaxZ = b.MaxZ + dz };
            return new TipCut
            {
                What = What, Flush = S(Flush), Clear = S(Clear), IsStrip = IsStrip, IsRelief = IsRelief, Axis = Axis, LengthMm = LengthMm,
                Prism = Prism?.Select(p => new[] { p[0] + dx, p[1] + dy, p[2] + dz }).ToArray()
            };
        }
    }

    public sealed class TipResult
    {
        public bool Ok;
        public string Failure;
        public double VolumeMm3;
        /// <summary>Só a ponta (prisma − peça, abaixo do fechamento), antes do pescoço.</summary>
        public double TipVolumeMm3;
        /// <summary>Topo da ponta (fechamento + pescoço): é de onde a faixa de medição parte.</summary>
        public double TopZMm;
        /// <summary>T2c/C1: a ponta antes de tirar as caixas, e o que saiu (enchimentos e lâminas), em mm³.</summary>
        public double TipBeforeCutsMm3, FillerRemovedMm3, StripRemovedMm3, ReliefRemovedMm3, NeckReliefMm3, NeckExpectedMm3;
        /// <summary>T6: o que os semiespaços da divisão na quina tiraram da ponta (mm³).</summary>
        public double PlaneRemovedMm3;
        /// <summary>T2c/C1: uma linha por caixa (o que era, rente ou com folga, volume tirado, status).</summary>
        public readonly List<string> CutLog = new List<string>();
        public int CutsFailed;
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// A PONTA do eletrodo como sólido, tirada do corpo real da peça por booleana:
    /// <c>prisma sobre a pegada − corpo da cavidade</c>. Nada de copiar faces soltas e costurar: nervuras,
    /// degraus e raios saem do sólido de origem (rota provada em 2026-11-10, SE 2023, síncrono — ver
    /// docs/PLANO_CRIAR_ELETRODOS.md e a skill solid-edge-com, modeling-recipes).
    ///
    /// Em DUAS etapas, porque acima do plano de fechamento não existe aço e o negativo ali seria a pegada
    /// inteira — uma aba apoiada na face de fechamento, que a queimaria:
    /// <list type="number">
    /// <item><b>Ponta</b>: prisma do fundo da queima até o fechamento (<see cref="TipRequest.RefZMm"/>, que tem de
    /// ser o plano EXATO do aço) menos a cavidade no lugar. Termina numa face plana em RefZ com o contorno da boca.</item>
    /// <item><b>Pescoço</b>: essa seção extrudada RETA para cima <see cref="TipRequest.NeckMm"/> (esboço com as
    /// arestas dela + extrusão). Até 2026-11-12 o pescoço era um prisma menos a cavidade DESLOCADA 1 mm para cima,
    /// que copiava para cima o primeiro milímetro da peça (quebras de canto, degraus rasos, saídas): entalhes.</item>
    /// </list>
    /// A base (faixa + bloco + fixação) vem depois, a partir de <see cref="TipResult.TopZMm"/>.
    /// </summary>
    public static class ElectrodeTipBuilder
    {
        private const int seBooleanSubtract = 2;
        private const uint igFeatureOK = 1216476310;

        public static TipResult Build(object partDoc, TipRequest r)
        {
            var res = new TipResult();
            dynamic part = partDoc;
            string tag = string.IsNullOrEmpty(r.Label) ? "Ponta" : r.Label;
            try
            {
                if (string.IsNullOrEmpty(r.CavityFile) || !System.IO.File.Exists(r.CavityFile))
                    return Fail(res, $"o arquivo da peça a erodir não existe no disco ({r.CavityFile ?? "sem nome"}) — salve a peça.");
                if (r.RefZMm < 0.02)
                    return Fail(res, string.Format(CultureInfo.InvariantCulture,
                        "a queima não fica abaixo do plano de fechamento (altura {0:0.###} mm) — nada a preencher.", r.RefZMm));

                int plane = PlaneWithNormalUp(part);
                if (plane < 1)
                    return Fail(res, "não achei, entre os planos-base da peça, um de normal +Z para desenhar o prisma.");

                double sx = 2.0 * r.HalfXMm, sy = 2.0 * r.HalfYMm;

                // ---- etapa 1: ponta (prisma do fundo da queima até o fechamento − cavidade no lugar)
                double below = Math.Max(0.0, -r.BottomZMm);
                if (below > 1e-9) BlankModeler.CreateBox(part, sx, sy, r.RefZMm + below, plane, 2, below, 0.0, 0.0, 1);
                else BlankModeler.CreateBox(part, sx, sy, r.RefZMm, plane, 2, 0.0, 0.0, 0.0, 2);
                double withPrism = Volume(part);
                string why = Subtract(part, r.CavityFile, r.CavityInElectrode, tag + " (ponta)");
                if (why != null)
                {
                    Log.Warn(string.Format(CultureInfo.InvariantCulture, "{0}: prisma {1:0.##} × {2:0.##} × {3:0.###} mm ({4:0.##} mm³, de Z {5:0.###}) — {6}",
                        tag, sx, sy, r.RefZMm - Math.Min(0.0, r.BottomZMm), withPrism, Math.Min(0.0, r.BottomZMm), why));
                    return Fail(res, "ponta: " + why);
                }
                double tipVolume = Volume(part);
                Log.Info(string.Format(CultureInfo.InvariantCulture, "{0}: ponta {1:0.##} mm³ (prisma {2:0.##} mm³).", tag, tipVolume, withPrism));
                if (double.IsNaN(tipVolume) || tipVolume < 1e-3) return Fail(res, "a booleana consumiu o corpo inteiro.");
                if (tipVolume > withPrism - 1e-3)
                    res.Warnings.Add("a subtração da cavidade não tirou nada do prisma — confira a posição do eletrodo.");
                // ---- etapa 1a (T6): divisão NA QUINA — tira cada semiespaço (o plano da parede, com a saída dela)
                if (r.Planes != null && r.Planes.Count > 0)
                {
                    double ext = 2.0 * (r.HalfXMm + r.HalfYMm + r.RefZMm - Math.Min(0.0, r.BottomZMm)) + 10.0;
                    foreach (CutPlane cp in r.Planes)
                    {
                        // plano herdado que não passa pelo prisma desta parte: nada a tirar (o recorte vazio dá RPC_E_SERVERFAULT)
                        bool reaches = false;
                        for (int k = 0; k < 8 && !reaches; k++)
                            reaches = cp.Distance((k & 1) == 0 ? -r.HalfXMm : r.HalfXMm, (k & 2) == 0 ? -r.HalfYMm : r.HalfYMm,
                                                  (k & 4) == 0 ? Math.Min(0.0, r.BottomZMm) : r.RefZMm) > 1e-4;
                        if (!reaches) { res.CutLog.Add("quina: " + cp.Describe() + " — fora do prisma desta parte, nada a tirar"); continue; }
                        double before = Volume(part);
                        string w; uint st = 0;
                        try { w = CutHalfSpace(part, cp, ext, tag, out st); }
                        catch (Exception ex) { Log.Warn($"{tag}: [quina] exceção fora do recorte — {ex}"); w = Describe(ex); }
                        double removed = before - Volume(part);
                        // O plano da parede é COPLANAR com a parede que ficou (é o que se quer): a booleana às vezes recusa
                        // (RPC_E_SERVERFAULT). Afasta o plano 1 µm, depois 5 µm, para o lado que sai — sobra uma lâmina
                        // desse tanto sobre a divisão (sobreposição, nunca vão), menor que a amostra do juiz (0,02 mm).
                        foreach (double off in new[] { 0.001, 0.005 })
                        {
                            if (w == null || removed > 1e-6) break;
                            CutPlane moved = new CutPlane { Point = new[] { cp.Point[0] + off * cp.Normal[0], cp.Point[1] + off * cp.Normal[1], cp.Point[2] + off * cp.Normal[2] }, Normal = cp.Normal };
                            res.CutLog.Add(string.Format(CultureInfo.InvariantCulture, "quina: rente falhou ({0}) — de novo afastado {1:0.###} mm", w, off));
                            try { w = CutHalfSpace(part, moved, ext, tag, out st); } catch (Exception ex) { w = Describe(ex); }
                            removed = before - Volume(part);
                        }
                        string line = string.Format(CultureInfo.InvariantCulture, "quina: {0} — {1}", cp.Describe(),
                            w == null ? string.Format(CultureInfo.InvariantCulture, "tirou {0:0.####} mm³ (status {1}, {2} corpo(s))", removed, st, ModelsCount(part)) : "FALHOU: " + w);
                        res.CutLog.Add(line);
                        Log.Info($"{tag}: [quina] {line}");
                        if (w != null && !(removed > 1e-6))
                        {
                            // o plano passa pelo prisma mas não pelo corpo (o recorte vazio é recusado): confere pela caixa do corpo
                            int side = BodySide(part, cp);
                            if (side < 0) { res.CutLog.Add("quina: " + cp.Describe() + " — o corpo inteiro fica do lado que fica: nada a tirar"); continue; }
                            return Fail(res, "divisão na quina: " + w + (side > 0 ? " (o corpo inteiro fica do lado que sai)" : ""));
                        }
                        if (removed < 1e-6) res.Warnings.Add("divisão na quina: o corte não tirou nada (" + cp.Describe() + ").");
                        res.PlaneRemovedMm3 += removed;
                    }
                    tipVolume = Volume(part);
                    if (double.IsNaN(tipVolume) || tipVolume < 1e-3) return Fail(res, "a divisão na quina consumiu a ponta inteira.");
                }
                res.TipBeforeCutsMm3 = tipVolume;

                // ---- etapa 1b (T2c/C1): o enchimento dos raios cortados pela divisa sai pela caixa da face — antes do
                //      pescoço, para que ele suba a seção já sem o enchimento.
                if (r.Cuts != null && r.Cuts.Count > 0)
                {
                    RemoveCuts(part, plane, r, tag, res);
                    tipVolume = Volume(part);
                    if (ModelsCount(part) != 1) res.Warnings.Add($"depois de tirar o enchimento a ponta ficou com {ModelsCount(part)} corpos.");
                }
                res.TipVolumeMm3 = tipVolume;


                // ---- etapa 2: pescoço RETO — a seção da ponta no fechamento, extrudada na vertical
                bool relief = r.Cuts != null && r.Cuts.Any(c => c.IsRelief);
                why = RaiseNeck(part, plane, r, tag, res, deferCheck: relief);
                if (why != null) return Fail(res, "pescoço: " + why);
                if (relief)
                {
                    // T5: o pescoço sobe das arestas do topo e PREENCHE os furos das regiões destacadas (medido: +461 mm³,
                    // também com igProfileAllowNested). Tira de novo as pegadas das regiões, só na camada do pescoço.
                    var neckCuts = r.Cuts.Where(c => c.IsRelief).Select(c => new TipCut
                    {
                        What = c.What + " (pescoço)", IsRelief = true,
                        Flush = new BoundingBox { MinX = c.Flush.MinX, MaxX = c.Flush.MaxX, MinY = c.Flush.MinY, MaxY = c.Flush.MaxY, MinZ = r.RefZMm - 0.5, MaxZ = r.RefZMm + r.NeckMm + 1.0 },
                    }).ToList();
                    foreach (TipCut c in neckCuts) c.Clear = c.Flush;
                    double before = res.ReliefRemovedMm3;
                    RemoveCuts(part, plane, new TipRequest { Cuts = neckCuts }, tag, res);
                    res.NeckReliefMm3 = res.ReliefRemovedMm3 - before;
                    res.ReliefRemovedMm3 = before;   // o alívio da PONTA fica separado do que saiu do pescoço
                    double neck = Volume(part) - tipVolume;
                    Log.Info(string.Format(CultureInfo.InvariantCulture, "{0}: pescoço depois do alívio {1:0.###} mm³ (esperado {2:0.###}).", tag, neck, res.NeckExpectedMm3));
                    if (Math.Abs(neck - res.NeckExpectedMm3) > 0.005 * res.NeckExpectedMm3)
                        return Fail(res, string.Format(CultureInfo.InvariantCulture, "pescoço: depois do alívio saiu com {0:0.##} mm³, esperado {1:0.##}.", neck, res.NeckExpectedMm3));
                    if (ModelsCount(part) != 1) res.Warnings.Add($"depois do alívio o eletrodo ficou com {ModelsCount(part)} corpos.");
                }

                res.VolumeMm3 = Volume(part);
                res.TopZMm = r.RefZMm + r.NeckMm;
                Log.Info(string.Format(CultureInfo.InvariantCulture,
                    "{0}: sólido {1:0.##} mm³ (ponta {2:0.##} + pescoço {3:0.##} mm³), topo Z = {4:0.###} mm.",
                    tag, res.VolumeMm3, tipVolume, res.VolumeMm3 - tipVolume, res.TopZMm));
                if (ModelsCount(part) != 1)
                    res.Warnings.Add($"a ponta saiu em {ModelsCount(part)} corpos — a região tem partes que não se ligam pelo pescoço.");
                res.Ok = true;
                return res;
            }
            catch (Exception ex)
            {
                Log.Warn($"{tag}: falhou — {ex.GetBaseException().Message}");
                return Fail(res, ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Sobe RETO a seção da ponta no fechamento: as arestas da(s) face(s) de topo da ponta (Z = RefZ) entram por
        /// <c>Profile.IncludeEdge</c> num esboço no plano RefZ, e <c>AddFiniteExtrudedProtrusion</c> extruda o perfil
        /// <see cref="TipRequest.NeckMm"/> para +Z. Rota 1 da T1, medida no SE 2023 em 2026-11-12: o
        /// <c>Profile.End(1)</c> devolve −103 (as arestas incluídas não ficam amarradas por relação), e mesmo assim a
        /// extrusão sai com Status OK e o volume exato (área × altura, −0,001 %). Por isso o veredito não é o End: é a
        /// conferência do volume e do número de corpos depois. Devolve null se deu certo, ou o motivo.
        /// </summary>
        private static string RaiseNeck(dynamic part, int planeIndex, TipRequest r, string tag, TipResult res, bool deferCheck = false)
        {
            var tops = new List<object>();
            double area = 0.0;
            int modelsBefore = ModelsCount(part);
            for (int m = 1; m <= modelsBefore; m++)
            {
                dynamic faces = part.Models.Item(m).Body.Faces(1);   // igQueryAll
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    object f = faces.Item(i);
                    if (!FaceGeometry.TryGetExactRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mx[2] - mn[2] > 1e-4 || Math.Abs(mx[2] - r.RefZMm) > 1e-3) continue;
                    tops.Add(f);
                    area += (double)((dynamic)f).Area * 1e6;
                }
            }
            if (tops.Count == 0 || area < 1e-3)
                return "o bolsão não abre no plano de fechamento dentro da pegada (a ponta não tem face no fechamento).";
            if (area > 4.0 * r.HalfXMm * r.HalfYMm - 1e-2)
                res.Warnings.Add("o pescoço ficou com a pegada inteira: não há aço no plano de fechamento dentro dela — confira a base sobre a peça.");

            // T5: com regiões destacadas a seção do principal pode sair em VÁRIAS faces separadas; um perfil só com os laços
            // de todas extrudou 51 de 1.568 mm³ (medido): uma extrusão por face.
            double before = Volume(part);
            int totalEdges = 0, end = 0;
            // As faces lidas morrem a cada extrusão: com várias, extruda a primeira e relê as que sobraram no fechamento.
            for (int k = 0; k < tops.Count; k++)
            {
                List<object> rest = k == 0 ? null : TopFaces((object)part, r.RefZMm, modelsBefore);
                List<object> group = k == 0 ? (tops.Count > 1 ? new List<object> { tops[0] } : tops) : rest.Take(1).ToList();
                if (group.Count == 0) break;
                var edges = new List<object>();
                var seen = new HashSet<int>();
                foreach (object f in group)
                {
                    dynamic es = ((dynamic)f).Edges;
                    for (int j = 1; j <= (int)es.Count; j++)
                    {
                        object e = es.Item(j);
                        int id = 0; try { id = (int)((dynamic)e).ID; } catch { }
                        if (id != 0 && !seen.Add(id)) continue;
                        edges.Add(e);
                    }
                }
                totalEdges += edges.Count;
                string why = ExtrudeNeck(part, planeIndex, r, tag, edges, out end, out uint status);
                if (why != null) return why;
                if (status != igFeatureOK) return $"a extrusão do pescoço não vingou (status {status}, End {end}).";
            }
            double neck = Volume(part) - before, want = area * r.NeckMm;
            Log.Info(string.Format(CultureInfo.InvariantCulture,
                "{0}: pescoço reto — {1} face(s) no fechamento, {2:0.###} mm², {3} aresta(s), End = {4}; volume {5:0.###} mm³ (esperado {6:0.###}).",
                tag, tops.Count, area, totalEdges, end, neck, want));
            if (ModelsCount(part) > modelsBefore) return $"o pescoço saiu solto da ponta ({ModelsCount(part)} corpos).";
            res.NeckExpectedMm3 = want;
            if (!deferCheck && Math.Abs(neck - want) > 0.005 * want)
                return string.Format(CultureInfo.InvariantCulture, "o pescoço saiu com {0:0.##} mm³, esperado {1:0.##} (seção {2:0.##} mm² × {3:0.##} mm).", neck, want, area, r.NeckMm);
            return null;
        }

        /// <summary>As faces planas horizontais no plano RefZ (o topo da ponta que ainda não subiu).</summary>
        private static List<object> TopFaces(object partObj, double refZ, int models)
        {
            dynamic part = partObj;
            var tops = new List<object>();
            for (int m = 1; m <= models; m++)
            {
                dynamic faces = part.Models.Item(m).Body.Faces(1);
                for (int i = 1; i <= (int)faces.Count; i++)
                {
                    object f = faces.Item(i);
                    if (!FaceGeometry.TryGetExactRangeMm(f, out double[] mn, out double[] mx)) continue;
                    if (mx[2] - mn[2] > 1e-4 || Math.Abs(mx[2] - refZ) > 1e-3) continue;
                    tops.Add(f);
                }
            }
            return tops;
        }

        /// <summary>Um perfil no plano RefZ com as arestas dadas (<c>Profile.IncludeEdge</c>) extrudado NeckMm para +Z.</summary>
        private static string ExtrudeNeck(dynamic part, int planeIndex, TipRequest r, string tag, List<object> edges, out int end, out uint status)
        {
            end = 0; status = 0;
            dynamic plane = part.RefPlanes.AddParallelByDistance(part.RefPlanes.Item(planeIndex), Units.MmToM(r.RefZMm), 2,
                Type.Missing, Type.Missing, Type.Missing);
            dynamic ps = part.ProfileSets.Add();
            try
            {
                dynamic profile = ps.Profiles.Add(plane);
                int failed = 0; string firstError = null;
                foreach (object e in edges)
                {
                    // [opt][out] Geometry2d volta VAZIO mesmo dando certo (skill, modeling-recipes): não é conferido.
                    object[] args = { e, null };
                    var mod = new ParameterModifier(2); mod[1] = true;
                    try { ((object)profile).GetType().InvokeMember("IncludeEdge", BindingFlags.InvokeMethod, null, (object)profile, args, new[] { mod }, null, null); }
                    catch (Exception ex) { failed++; if (firstError == null) firstError = Describe(ex); }
                }
                if (failed > 0) return $"{failed} de {edges.Count} aresta(s) do fechamento não entraram no esboço ({firstError}).";
                try { end = (int)profile.End(1); } catch (Exception ex) { Log.Info($"{tag}: Profile.End — {Describe(ex)}"); }
                var arr = new SolidEdgePart.Profile[] { (SolidEdgePart.Profile)profile };
                Invoke((object)part.Models, "AddFiniteExtrudedProtrusion", 1, arr, 2, Units.MmToM(r.NeckMm));
                try
                {
                    dynamic eps = part.Models.Item(1).ExtrudedProtrusions;
                    status = Convert.ToUInt32(eps.Item((int)eps.Count).Status);
                }
                catch { }
                return null;
            }
            finally
            {
                AutoEDM.Com.SketchScope.DeleteVerified(part, (object)ps, tag + " (pescoço)");
                try { plane.Visible = false; } catch { }
            }
        }

        /// <summary>
        /// T2c/C1: tira cada caixa de <see cref="TipRequest.Cuts"/> por RECORTE EXTRUDADO (perfil retangular no plano do
        /// meio da caixa, <c>ExtrudedCutouts.AddFiniteMulti</c> simétrico com a altura total). Primeiro rente; se não
        /// vingar (exceção, status ruim ou nada tirado), com folga. Os lados das caixas vêm do Core já em coordenadas
        /// REAIS e vão para o esboço por <c>Convert3DCoordinate</c>.
        /// </summary>
        private static void RemoveCuts(dynamic part, int planeIndex, TipRequest r, string tag, TipResult res)
        {
            // A lâmina que falhar antes do enchimento é tentada de novo depois dele (medido no 15428.100: E_FAIL na
            // faixa da divisa de um R0,1 com o enchimento ainda no lugar).
            var queue = new List<TipCut>(r.Cuts);
            var retried = new HashSet<TipCut>();
            for (int qi = 0; qi < queue.Count; qi++)
            {
                TipCut c = queue[qi];
                double before = Volume(part);
                uint status = 0;
                string why = c.Prism != null ? CutPolygon(part, c.Prism, c.Axis, c.LengthMm, tag, out status) : CutBox(part, planeIndex, c.Flush, tag, out status);
                double removed = before - Volume(part);
                if (c.Prism != null && why != null && !(removed > 1e-6))
                {
                    res.CutLog.Add($"{c.What}: prisma falhou ({why}) — tentando a caixa");
                    why = CutBox(part, planeIndex, c.Flush, tag, out status);
                    removed = before - Volume(part);
                }
                // Medido no 15428.100: o recorte rente na ponta de um toro devolve RPC_E_SERVERFAULT e, mesmo assim, a
                // feature entra e o corpo muda. Vale o corpo: se ele perdeu material e segue um só, deu certo.
                if (why != null && removed > 1e-6 && ModelsCount(part) == 1)
                {
                    res.CutLog.Add($"{c.What}: o recorte respondeu '{why}', mas o corpo mudou (−{removed:0.#####} mm³, 1 corpo): aceito");
                    why = null;
                }
                string how = retried.Contains(c) ? "rente, depois do enchimento" : "rente";
                if (why != null && c.IsStrip && retried.Add(c))
                {
                    res.CutLog.Add($"{c.What}: falhou ({why}) — tento de novo depois do enchimento");
                    queue.Add(c);
                    continue;
                }
                if (why != null && !c.IsStrip && c.Clear.MaxX > c.Clear.MinX)
                {
                    res.CutLog.Add($"{c.What}: rente falhou ({why}) — tentando com folga");
                    before = Volume(part);
                    why = CutBox(part, planeIndex, c.Clear, tag, out status);
                    removed = before - Volume(part);
                    how = "com folga";
                    if (why != null && removed > 1e-6 && ModelsCount(part) == 1)
                    {
                        res.CutLog.Add($"{c.What}: com folga respondeu '{why}', mas o corpo mudou (−{removed:0.#####} mm³, 1 corpo): aceito");
                        why = null;
                    }
                }
                double boxVol = (c.Flush.MaxX - c.Flush.MinX) * (c.Flush.MaxY - c.Flush.MinY) * (c.Flush.MaxZ - c.Flush.MinZ);
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1}, caixa x {2:0.####}…{3:0.####} y {4:0.####}…{5:0.####} z {6:0.####}…{7:0.####} — {8}",
                    c.What, how, c.Flush.MinX, c.Flush.MaxX, c.Flush.MinY, c.Flush.MaxY, c.Flush.MinZ, c.Flush.MaxZ,
                    why == null ? string.Format(CultureInfo.InvariantCulture, "tirou {0:0.#####} mm³ (status {1})", removed, status) : "FALHOU: " + why);
                res.CutLog.Add(line);
                Log.Info($"{tag}: [enchimento] {line}");
                if (why != null) { res.CutsFailed++; continue; }
                if (c.IsStrip) res.StripRemovedMm3 += removed; else if (c.IsRelief) res.ReliefRemovedMm3 += removed; else res.FillerRemovedMm3 += removed;
                if (!c.IsStrip && !c.IsRelief && removed > boxVol + 1e-6)
                    res.Warnings.Add($"{c.What}: o recorte tirou mais que a caixa ({removed:0.####} > {boxVol:0.####} mm³).");
            }
        }

        /// <summary>Um recorte extrudado com a caixa (mm, referencial da peça). Devolve null se a feature saiu OK.</summary>
        private static string CutBox(dynamic part, int planeIndex, BoundingBox b, string tag, out uint status)
        {
            status = 0;
            double zMid = (b.MinZ + b.MaxZ) / 2.0, h = b.MaxZ - b.MinZ;
            dynamic basePlane = part.RefPlanes.Item(planeIndex);
            dynamic plane = basePlane;
            bool created = false;
            dynamic ps = null;
            try
            {
                if (Math.Abs(zMid) > 1e-6)
                {
                    plane = part.RefPlanes.AddParallelByDistance(basePlane, Units.MmToM(Math.Abs(zMid)), zMid > 0 ? 2 : 1,
                        Type.Missing, Type.Missing, Type.Missing);
                    created = true;
                }
                ps = part.ProfileSets.Add();
                var profile = (SolidEdgePart.Profile)ps.Profiles.Add(plane);
                double z = Units.MmToM(zMid);
                double[][] corners =
                {
                    new[] { Units.MmToM(b.MinX), Units.MmToM(b.MinY) }, new[] { Units.MmToM(b.MaxX), Units.MmToM(b.MinY) },
                    new[] { Units.MmToM(b.MaxX), Units.MmToM(b.MaxY) }, new[] { Units.MmToM(b.MinX), Units.MmToM(b.MaxY) }
                };
                var uv = new double[4][];
                for (int i = 0; i < 4; i++)
                {
                    profile.Convert3DCoordinate(corners[i][0], corners[i][1], z, out double u, out double v);
                    uv[i] = new[] { u, v };
                }
                dynamic lines = ((dynamic)profile).Lines2d;
                for (int i = 0; i < 4; i++)
                    lines.AddBy2Points(uv[i][0], uv[i][1], uv[(i + 1) % 4][0], uv[(i + 1) % 4][1]);
                int end = 0; try { end = Convert.ToInt32(((dynamic)profile).End(1)); } catch { }
                object cutouts = (object)part.Models.Item(1).ExtrudedCutouts;
                object cut = Invoke(cutouts, "AddFiniteMulti", 1, new[] { profile }, 3 /* igSymmetric: a altura é a TOTAL */, Units.MmToM(h));
                try { status = Convert.ToUInt32(((dynamic)cut).Status); } catch { }
                if (cut == null) return $"AddFiniteMulti devolveu nulo (End {end})";
                if (status != igFeatureOK) return $"status {status} (End {end})";
                return null;
            }
            catch (Exception ex) { return Describe(ex); }
            finally
            {
                if (ps != null) AutoEDM.Com.SketchScope.DeleteVerified(part, (object)ps, tag + " (enchimento)");
                if (created) { try { plane.Visible = false; } catch { } }
            }
        }

        /// <summary>
        /// T6: tira o semiespaço do lado da normal de <paramref name="cp"/> (mm, referencial da peça). Esboço num plano
        /// PERPENDICULAR à parede (rota do PrismaticBuilder, provada em 2026-10-09: <c>CoordinateSystems.AddByMatrix</c> →
        /// <c>cs.Plane(4)</c> = XY do CS → <c>RefPlanes.AddParallelByDistance(face, 0, 2)</c>, pontos por
        /// <c>Profile.Convert3DCoordinate</c>): um retângulo com um lado NO plano da parede (inclinado, se ela tem saída) e o
        /// resto do lado que sai, extrudado simétrico ao longo da parede (<c>AddFiniteMulti</c>, como o <see cref="CutBox"/>).
        /// </summary>
        private static string CutHalfSpace(dynamic part, CutPlane cp, double extentMm, string tag, out uint status)
        {
            status = 0;
            double[] n = cp.Normal;
            double[] t = { n[1], -n[0], 0.0 };   // n × Z: horizontal, ao longo da parede
            double lt = Math.Sqrt(t[0] * t[0] + t[1] * t[1]);
            if (lt < 0.1) return "o plano não é parede (normal quase vertical).";
            t = new[] { t[0] / lt, t[1] / lt, 0.0 };
            double[] up = { n[1] * t[2] - n[2] * t[1], n[2] * t[0] - n[0] * t[2], n[0] * t[1] - n[1] * t[0] };   // n × t: no plano da parede
            double[] p0 = cp.Point;
            double L = extentMm;
            double[][] pts = new[] { new[] { -L, 0.0 }, new[] { L, 0.0 }, new[] { L, L }, new[] { -L, L } }   // (ao longo de up, ao longo de n)
                .Select(ab => new[] { p0[0] + ab[0] * up[0] + ab[1] * n[0], p0[1] + ab[0] * up[1] + ab[1] * n[1], p0[2] + ab[0] * up[2] + ab[1] * n[2] }).ToArray();
            return CutPolygon(part, pts, t, 2 * L, tag, out status);
        }

        /// <summary>
        /// Recorte de um PRISMA: o polígono <paramref name="pts"/> (mm, 3D, num plano normal a <paramref name="axis"/>) extrudado
        /// simétrico <paramref name="lengthMm"/> ao longo do eixo. Esboço pela rota do PrismaticBuilder (CS → <c>cs.Plane(4)</c>
        /// → <c>AddParallelByDistance(face, 0, 2)</c> → <c>Convert3DCoordinate</c>), recorte por <c>AddFiniteMulti</c> simétrico.
        /// </summary>
        private static string CutPolygon(dynamic part, double[][] pts, double[] axis, double lengthMm, string tag, out uint status)
        {
            status = 0;
            double[] w = axis;
            double[] a0 = Math.Abs(w[2]) < 0.9 ? new[] { 0.0, 0, 1 } : new[] { 1.0, 0, 0 };
            double[] u = { a0[1] * w[2] - a0[2] * w[1], a0[2] * w[0] - a0[0] * w[2], a0[0] * w[1] - a0[1] * w[0] };
            double lu = Math.Sqrt(u[0] * u[0] + u[1] * u[1] + u[2] * u[2]);
            u = new[] { u[0] / lu, u[1] / lu, u[2] / lu };
            double[] v = { w[1] * u[2] - w[2] * u[1], w[2] * u[0] - w[0] * u[2], w[0] * u[1] - w[1] * u[0] };   // W × U: U × V = W
            double[] p0 = pts[0];
            dynamic cs = null, plane = null, ps = null;
            try
            {
                Array m = new double[]
                {
                    u[0], u[1], u[2], 0,
                    v[0], v[1], v[2], 0,
                    w[0], w[1], w[2], 0,
                    p0[0] / 1000.0, p0[1] / 1000.0, p0[2] / 1000.0, 1
                };
                cs = part.CoordinateSystems.AddByMatrix(ref m);
                plane = part.RefPlanes.AddParallelByDistance(cs.Plane(4), 0.0, 2);
                ps = part.ProfileSets.Add();
                var profile = (SolidEdgePart.Profile)ps.Profiles.Add(plane);
                int k = pts.Length;
                var uv = new double[k][];
                for (int i = 0; i < k; i++)
                {
                    profile.Convert3DCoordinate(Units.MmToM(pts[i][0]), Units.MmToM(pts[i][1]), Units.MmToM(pts[i][2]), out double pu, out double pv);
                    uv[i] = new[] { pu, pv };
                }
                // Sentido ANTI-HORÁRIO nas coordenadas do esboço (PrismaticBuilder, 2026-10-09: desenhado horário, o recorte
                // levou o lado de FORA; medido de novo na T7: o triângulo do enchimento tirou 146 mm³ em vez de ~1)
                double twice = 0;
                for (int i = 0; i < k; i++) twice += uv[i][0] * uv[(i + 1) % k][1] - uv[(i + 1) % k][0] * uv[i][1];
                if (twice < 0) Array.Reverse(uv);
                dynamic lines = ((dynamic)profile).Lines2d;
                for (int i = 0; i < k; i++)
                    lines.AddBy2Points(uv[i][0], uv[i][1], uv[(i + 1) % k][0], uv[(i + 1) % k][1]);
                int end = 0; try { end = Convert.ToInt32(((dynamic)profile).End(1)); } catch { }
                object cutouts = (object)part.Models.Item(1).ExtrudedCutouts;
                object cut = Invoke(cutouts, "AddFiniteMulti", 1, new[] { profile }, 3 /* igSymmetric */, Units.MmToM(lengthMm));
                try { status = Convert.ToUInt32(((dynamic)cut).Status); } catch { }
                if (cut == null) return $"AddFiniteMulti devolveu nulo (End {end})";
                if (status != igFeatureOK) return $"status {status} (End {end})";
                return null;
            }
            catch (Exception ex) { Log.Info($"{tag}: [quina] {ex}"); return Describe(ex); }
            finally
            {
                try { if (ps != null) AutoEDM.Com.SketchScope.DeleteVerified(part, (object)ps, tag + " (quina)"); } catch (Exception ex) { Log.Info($"{tag}: [quina] esboço ficou — {ex.GetBaseException().Message}"); }
                try { if (plane != null) plane.Visible = false; } catch { }
                try { if (cs != null) cs.Visible = false; } catch { }
            }
        }

        /// <summary>−1: a caixa do corpo inteira do lado que fica; +1: inteira do lado que sai; 0: o plano a atravessa (ou não se leu).</summary>
        private static int BodySide(dynamic part, CutPlane cp)
        {
            try
            {
                object body = (object)part.Models.Item(1).Body;
                if (!FaceGeometry.TryGetBodyRangeMm(body, out double[] mn, out double[] mx)) return 0;
                bool pos = false, neg = false;
                for (int k = 0; k < 8; k++)
                {
                    double d = cp.Distance((k & 1) == 0 ? mn[0] : mx[0], (k & 2) == 0 ? mn[1] : mx[1], (k & 4) == 0 ? mn[2] : mx[2]);
                    if (d > 1e-4) pos = true; else neg = true;
                }
                return pos && neg ? 0 : (pos ? 1 : -1);
            }
            catch { return 0; }
        }

        private static TipResult Fail(TipResult res, string why) { res.Ok = false; res.Failure = why; return res; }

        /// <summary>
        /// Traz o corpo da cavidade (corpo de construção, na pose dada) e o subtrai do corpo de projeto.
        /// Devolve null se deu certo, ou o motivo.
        /// </summary>
        private static string Subtract(dynamic part, string file, OccurrenceTransform pose, string tag)
        {
            object cs = null, cc = null;
            string step = "sistema de coordenadas";
            try
            {
                cs = Invoke((object)part.CoordinateSystems, "AddByMatrix", pose.ToMatrix());
                step = "cópia do corpo";
                // 14 [in]: arquivo, escalas, plano de espelho, membro de família, SISTEMA DE COORDENADAS,
                // corpo de projeto = sim, construções = não, …, copiar cores = sim (mesma chamada do BurnSurfaceCopier).
                cc = Invoke((object)part.Constructions.CopyConstructions, "Add",
                    file, 1.0, 1.0, 1.0, Type.Missing, Type.Missing, cs,
                    true, false, false, false, Type.Missing, Type.Missing, true);
                if (cc == null) return "a cópia do corpo da peça não foi criada.";

                // O corpo fica no ConstructionModel (Constructions.Item), não na feature CopyConstruction.
                step = "corpo da cópia";
                int n = (int)part.Constructions.Count;
                if (n < 1) return "a cópia do corpo não apareceu em Constructions.";
                object toolBody = (object)part.Constructions.Item(n).Body;
                double toolVolume = (double)((dynamic)toolBody).Volume * 1e9;   // m³ → mm³
                if (toolVolume < 1e-3) return "a cópia da peça não é um sólido (volume zero).";
                step = "booleana";

                // Só o Body, num array TIPADO, é aceito como ferramenta (CopyConstruction[]/ConstructionModel[] dão
                // E_NOINTERFACE/E_FAIL; Subtracts.Add dá E_INVALIDARG) — medido em 2026-11-10.
                var tools = new SolidEdgeGeometry.Body[] { (SolidEdgeGeometry.Body)toolBody };
                object model = (object)part.Models.Item(1);
                object booleans = model.GetType().InvokeMember("BooleanFeatures", BindingFlags.GetProperty, null, model, null);
                object feature = Invoke(booleans, "Add", tools.Length, tools, seBooleanSubtract, Type.Missing);
                uint status = 0;
                try { status = Convert.ToUInt32(((dynamic)feature).Status); } catch { }
                if (feature == null || status != igFeatureOK) return $"a booleana de subtração não vingou (status {status}).";
                return null;
            }
            catch (Exception ex) { Log.Warn($"{tag}: subtração falhou em '{step}' — {Describe(ex)}"); return step + ": " + Describe(ex); }
            finally
            {
                // Em síncrono o resultado da booleana já está no corpo: a cópia e o sistema de coordenadas saem.
                if (cc != null) { try { ((dynamic)cc).Delete(); } catch (Exception e) { Log.Info($"{tag}: a cópia do corpo ficou na árvore — {e.GetBaseException().Message}"); } }
                if (cs != null) { try { ((dynamic)cs).Delete(); } catch (Exception e) { Log.Info($"{tag}: o sistema de coordenadas ficou — {e.GetBaseException().Message}"); } }
            }
        }

        /// <summary>Índice (1..3) do plano-base de normal +Z; 0 se não houver. O índice → plano não é fixo.</summary>
        private static int PlaneWithNormalUp(dynamic part)
        {
            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    ProfilePlaneFrame f = ProfilePlaneFrame.OfRefPlane((object)part.RefPlanes.Item(i));
                    if (f != null && f.Normal[2] > 0.999) return i;
                }
                catch { }
            }
            return 0;
        }

        internal static double Volume(dynamic part)
        {
            try
            {
                if ((int)part.Models.Count < 1) return 0.0;
                double total = 0.0;
                for (int i = 1; i <= (int)part.Models.Count; i++) total += (double)part.Models.Item(i).Body.Volume * 1e9;
                return total;
            }
            catch { return double.NaN; }
        }

        private static int ModelsCount(dynamic part)
        {
            try { return (int)part.Models.Count; } catch { return -1; }
        }

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args, CultureInfo.InvariantCulture);

        private static string Describe(Exception ex)
        {
            Exception b = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex.GetBaseException();
            return b is System.Runtime.InteropServices.COMException ce ? $"{b.Message.Trim()} (0x{ce.ErrorCode:X8})" : b.Message;
        }
    }
}
