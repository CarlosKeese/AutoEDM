using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoEDM.Mold.Cooling
{
    /// <summary>
    /// Do traçado desenhado no esboço 3D para a lista de furos (Carlos, 2026-09-24). Puro — nada de
    /// COM: a única pergunta que ele faz à peça ("até onde vai o material deste ponto nesta
    /// direção?") chega como delegate, medido pelo <see cref="CoolingExitProbe"/>.
    ///
    /// O Carlos desenha o CAMINHO DA ÁGUA: entra numa face, percorre a placa com cantos dentro
    /// dela e sai. O plano faz o que o ferramenteiro faz com isso (1º run ao vivo, MD-15335):
    /// <list type="bullet">
    /// <item>Linhas colineares encadeadas são UMA passada de broca.</item>
    /// <item>Passada que chega na face entra por lá. Passada que acaba em cantos dentro da placa é
    ///   PROLONGADA até a face mais próxima (medida na peça, preferindo a face externa a um bolsão)
    ///   e furada de fora — a boca do prolongamento leva TAMPÃO.</item>
    /// <item>As pontas do caminho que chegam na face são a entrada e a saída de água: ENGATE.</item>
    /// <item>O fundo: canto/cruzamento = sobrefuro (padrão Ø/2) + ponta de broca 118°; ponta que
    ///   acaba dentro da peça sem cruzar nada = cega, ponta de broca no ponto desenhado; boca na face
    ///   oposta = fundo reto rompendo a face.</item>
    /// <item>Engate e tampão são um furo roscado coaxial em cada boca que os pede.</item>
    /// </list>
    /// O usuário pode trocar a terminação de qualquer boca (chave "L3:F"); o resto é automático.
    /// </summary>
    public static class CoolingPlanner
    {
        private sealed class Run
        {
            public List<CoolingLine> Lines = new List<CoolingLine>();
            public double[] Dir;                                  // unitário, do início ao fim da passada
            public CoolingLine StartLine, EndLine;
            public bool StartAtLineStart, EndAtLineStart;
            public double[] StartMm, EndMm;
            public bool StartTouches, EndTouches;                  // a ponta encosta em outra linha (canto/cruzamento)
            public string Name => string.Join("+", Lines.OrderBy(l => l.Index).Select(l => l.Name));
        }

        /// <summary>Uma ponta da passada, com o que a peça respondeu sobre ela.</summary>
        private sealed class EndInfo
        {
            public CoolingLine Line;
            public bool AtLineStart;
            public double[] Drawn;        // a ponta desenhada
            public double[] Outward;      // para fora da passada
            public bool Touches;          // canto/cruzamento com outra linha
            public CoolingExit Exit;
            public bool AtFace;           // a ponta desenhada já está na face
            public string Key => CoolingEnd.KeyOf(Line.Index, AtLineStart);
        }

        /// <summary>
        /// O plano. <paramref name="overrides"/>: terminação escolhida pelo usuário por boca (chave
        /// "L3:F"); o que não vier é automático. <paramref name="exitProbe"/>: mede a saída do
        /// material; sem ele (teste, ou peça ilegível) a ponta que não encosta em nada conta como
        /// na face e nada é prolongado.
        /// </summary>
        public static CoolingPlan Plan(IList<CoolingLine> lines, IDictionary<string, CoolingTerminal> overrides,
            CoolingOptions opt, Func<double[], double[], CoolingExit> exitProbe = null)
        {
            opt = opt ?? new CoolingOptions();
            var plan = new CoolingPlan();
            if (lines == null || lines.Count == 0) { plan.Problems.Add("Nenhuma linha."); return plan; }

            foreach (CoolingLine l in lines.Where(l => l.LengthMm < opt.EndToleranceMm))
                plan.Problems.Add($"{l.Name} tem comprimento zero — ignorada.");
            List<CoolingLine> valid = lines.Where(l => l.LengthMm >= opt.EndToleranceMm).ToList();
            string d = F(opt.DiameterMm);

            foreach (Run r in BuildRuns(valid, opt.EndToleranceMm))
            {
                EndInfo s = Info(r, true, exitProbe, opt), e = Info(r, false, exitProbe, opt);

                // ---- entrada: uma ponta na face; senão, prolongar a mais barata até a face
                EndInfo entry; double[] entryPoint; bool extended = false; double extension = 0;
                CoolingTerminal Term(EndInfo x, bool ext)
                {
                    if (overrides != null && overrides.TryGetValue(x.Key, out CoolingTerminal o)) return o;
                    return AutoTerminal(x, ext);
                }

                if (s.AtFace && e.AtFace) entry = Rank(Term(s, false)) >= Rank(Term(e, false)) ? s : e;
                else if (s.AtFace) entry = s;
                else if (e.AtFace) entry = e;
                else
                {
                    EndInfo best = Cheapest(s, e);
                    if (best == null)
                    {
                        plan.Problems.Add($"{r.Name}: as duas pontas estão dentro da peça e não achei saída do material em " +
                                          "nenhuma das direções — prolongue a linha até a face à mão.");
                        continue;
                    }
                    entry = best;
                    extended = true;
                    extension = best.Exit.DistanceMm;
                    if (!best.Exit.OuterFace)
                        plan.Notes.Add($"{best.Key}: o prolongamento sai numa face INTERNA da peça (bolsão?), não na lateral — confira.");
                }
                entryPoint = extended ? Vec.Add(entry.Drawn, entry.Outward, extension) : entry.Drawn;
                EndInfo far = ReferenceEquals(entry, s) ? e : s;

                CoolingTerminal entryTerm = Term(entry, extended);
                if (entryTerm == CoolingTerminal.Blind)
                {
                    plan.Problems.Add($"{r.Name}: a boca por onde a broca entra ({entry.Key}) está marcada como CEGA. " +
                                      "Troque para tampão, engate ou passante.");
                    continue;
                }
                AddEnd(plan, entry, entryPoint, extended, extension, AutoTerminal(entry, extended), entryTerm);

                if (extended)
                    foreach (CoolingLine o in valid.Where(o => !r.Lines.Contains(o)))
                        if (SegmentNear(entry.Drawn, entryPoint, o, opt.DiameterMm))
                            plan.Notes.Add($"{entry.Key}: o prolongamento até a face passa a menos de um Ø de {o.Name} — confira se não fura o outro canal.");

                // ---- profundidade e fundo, pela ponta de lá
                double[] dir = Vec.Unit(Vec.Sub(far.Drawn, entry.Drawn));
                double length = Vec.Dist(entryPoint, far.Drawn);
                double depth; bool point;
                CoolingTerminal farTerm = CoolingTerminal.Blind;
                if (far.AtFace)
                {
                    farTerm = Term(far, false);
                    AddEnd(plan, far, far.Drawn, false, 0, AutoTerminal(far, false), farTerm);
                    if (farTerm == CoolingTerminal.Blind) { depth = length; point = true; }
                    else { depth = length + opt.BreakthroughMm; point = false; }
                }
                else if (far.Touches) { depth = length + opt.Overshoot; point = true; }
                else { depth = length; point = true; }                           // acaba dentro, sem cruzar nada

                plan.Holes.Add(new CoolingHolePlan
                {
                    Label = $"Refrigeração Ø{d} - {r.Name}",
                    EntryLineIndex = entry.Line.Index,
                    EntryAtLineStart = entry.AtLineStart,
                    EntryMm = entryPoint,
                    TowardMm = Vec.Add(entryPoint, dir, Math.Min(length / 2.0, 5.0)),   // qualquer ponto do furo serve
                    DirMm = dir,
                    DepthMm = depth,
                    DiameterMm = opt.DiameterMm,
                    DrillPoint = point,
                    Extended = extended,
                });

                // ---- roscas coaxiais nas bocas que pedem
                AddThread(plan, entry.Key, entryTerm, entryPoint, dir, length, opt);
                if (far.AtFace) AddThread(plan, far.Key, farTerm, far.Drawn, Vec.Scale(dir, -1), length, opt);
            }

            plan.FreeEnds.Sort((a, b) => a.LineIndex != b.LineIndex ? a.LineIndex.CompareTo(b.LineIndex) : (a.AtStart ? 0 : 1).CompareTo(b.AtStart ? 0 : 1));
            return plan;
        }

        /// <summary>
        /// Terminação automática de uma boca: boca de PROLONGAMENTO = tampão (o furo de fora só existe
        /// para a broca chegar no canto); ponta do caminho que chega na face = engate (entrada/saída de
        /// água); ponta na face que encosta em outra linha = tampão.
        /// </summary>
        private static CoolingTerminal AutoTerminal(EndInfo x, bool extended)
        {
            if (extended) return CoolingTerminal.Plug;
            return x.Touches ? CoolingTerminal.Plug : CoolingTerminal.Fitting;
        }

        private static EndInfo Info(Run r, bool start, Func<double[], double[], CoolingExit> probe, CoolingOptions opt)
        {
            var x = new EndInfo
            {
                Line = start ? r.StartLine : r.EndLine,
                AtLineStart = start ? r.StartAtLineStart : r.EndAtLineStart,
                Drawn = start ? r.StartMm : r.EndMm,
                Outward = start ? Vec.Scale(r.Dir, -1) : r.Dir,
                Touches = start ? r.StartTouches : r.EndTouches,
            };
            if (probe == null)
            {
                x.AtFace = !x.Touches;                          // sem medir a peça: a regra de antes
                return x;
            }
            x.Exit = probe(x.Drawn, x.Outward) ?? new CoolingExit();
            x.AtFace = double.IsNaN(x.Exit.DistanceMm) || x.Exit.DistanceMm <= opt.FaceToleranceMm;
            return x;
        }

        /// <summary>A ponta com saída mais curta, preferindo a que sai numa face externa.</summary>
        private static EndInfo Cheapest(EndInfo a, EndInfo b)
        {
            var c = new[] { a, b }.Where(x => x.Exit != null && !double.IsNaN(x.Exit.DistanceMm)).ToList();
            if (c.Count == 0) return null;
            return c.OrderBy(x => x.Exit.OuterFace ? 0 : 1).ThenBy(x => x.Exit.DistanceMm).First();
        }

        private static void AddEnd(CoolingPlan plan, EndInfo x, double[] p, bool extended, double ext, CoolingTerminal auto, CoolingTerminal t)
        {
            plan.FreeEnds.Add(new CoolingEnd
            {
                Key = x.Key, LineIndex = x.Line.Index, AtStart = x.AtLineStart, PointMm = p,
                Extended = extended, ExtensionMm = ext, AutoTerminal = auto, Terminal = t,
            });
        }

        private static void AddThread(CoolingPlan plan, string key, CoolingTerminal t, double[] p, double[] inward, double runLength, CoolingOptions opt)
        {
            if (t != CoolingTerminal.Fitting && t != CoolingTerminal.Plug) return;
            PipeThread th = t == CoolingTerminal.Fitting ? opt.FittingThread : opt.PlugThread;
            string what = t == CoolingTerminal.Fitting ? "Engate" : "Tampão";
            if (th == null)
            {
                plan.Problems.Add($"{key}: {what.ToLowerInvariant()} sem rosca escolhida — sem furo roscado nesta boca.");
                return;
            }
            if (th.TapDrillMm <= opt.DiameterMm)
                plan.Notes.Add($"{key}: a broca da rosca {th.Label} (Ø{F(th.TapDrillMm)}) não é maior que o canal (Ø{F(opt.DiameterMm)}) — confira a rosca.");
            double threadDepth = th.InternalLengthMm > 0 ? th.InternalLengthMm : opt.DefaultThreadDepthMm;
            plan.Holes.Add(new CoolingHolePlan
            {
                Label = $"{what} {th.Size.Trim()} - {key}",
                EntryMm = p,
                TowardMm = Vec.Add(p, inward, Math.Min(threadDepth, runLength) / 2.0),
                DirMm = inward,
                DepthMm = threadDepth + 2.0,
                DiameterMm = th.NominalMm,
                DrillPoint = false,
                Thread = th,
                ThreadDepthMm = threadDepth,
                Extended = true,                                  // a boca pode estar fora da linha: plano pelo ponto
            });
        }

        // ------------------------------------------------------------------ rede

        private static List<Run> BuildRuns(IList<CoolingLine> lines, double tol)
        {
            int n = lines.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Parallel(lines[i], lines[j]) && ShareEndpoint(lines[i], lines[j], tol))
                        parent[Find(i)] = Find(j);

            var runs = new List<Run>();
            foreach (var g in Enumerable.Range(0, n).GroupBy(Find))
            {
                var r = new Run { Lines = g.Select(i => lines[i]).OrderBy(l => l.Index).ToList() };
                CoolingLine first = r.Lines[0];
                r.Dir = Vec.Unit(Vec.Sub(first.EndMm, first.StartMm));
                double min = double.MaxValue, max = double.MinValue;
                foreach (CoolingLine l in r.Lines)
                    foreach (bool atStart in new[] { true, false })
                    {
                        double[] p = atStart ? l.StartMm : l.EndMm;
                        double t = Vec.Dot(Vec.Sub(p, first.StartMm), r.Dir);
                        if (t < min) { min = t; r.StartLine = l; r.StartAtLineStart = atStart; r.StartMm = p; }
                        if (t > max) { max = t; r.EndLine = l; r.EndAtLineStart = atStart; r.EndMm = p; }
                    }
                var others = lines.Where(l => !r.Lines.Contains(l)).ToList();
                r.StartTouches = others.Any(o => Touches(o, r.StartMm, tol));
                r.EndTouches = others.Any(o => Touches(o, r.EndMm, tol));
                runs.Add(r);
            }
            return runs.OrderBy(r => r.Lines.Min(l => l.Index)).ToList();
        }

        private static bool Touches(CoolingLine l, double[] p, double tol) => DistToSegment(p, l.StartMm, l.EndMm) <= tol;

        private static double DistToSegment(double[] p, double[] a, double[] b)
        {
            double[] ab = Vec.Sub(b, a);
            double len2 = Vec.Dot(ab, ab);
            if (len2 < 1e-18) return Vec.Dist(a, p);
            double t = Math.Max(0, Math.Min(1, Vec.Dot(Vec.Sub(p, a), ab) / len2));
            return Vec.Dist(Vec.Add(a, ab, t), p);
        }

        /// <summary>O prolongamento (a→b) passa a menos de <paramref name="gap"/> da linha? (amostrado a cada 0,5 mm)</summary>
        private static bool SegmentNear(double[] a, double[] b, CoolingLine o, double gap)
        {
            double len = Vec.Dist(a, b);
            int steps = Math.Max(1, (int)(len / 0.5));
            for (int i = 1; i <= steps; i++)
                if (DistToSegment(Vec.Add(a, Vec.Sub(b, a), (double)i / steps), o.StartMm, o.EndMm) < gap) return true;
            return false;
        }

        private static bool ShareEndpoint(CoolingLine a, CoolingLine b, double tol) =>
            Vec.Dist(a.StartMm, b.StartMm) <= tol || Vec.Dist(a.StartMm, b.EndMm) <= tol ||
            Vec.Dist(a.EndMm, b.StartMm) <= tol || Vec.Dist(a.EndMm, b.EndMm) <= tol;

        /// <summary>Paralelas (+ ponta em comum, testado junto) = mesma reta.</summary>
        private static bool Parallel(CoolingLine a, CoolingLine b) =>
            Vec.Len(Vec.Cross(Vec.Unit(Vec.Sub(a.EndMm, a.StartMm)), Vec.Unit(Vec.Sub(b.EndMm, b.StartMm)))) <= 1e-6;

        private static int Rank(CoolingTerminal t)
        {
            switch (t)
            {
                case CoolingTerminal.Fitting: return 3;
                case CoolingTerminal.Plug: return 2;
                case CoolingTerminal.Through: return 1;
                default: return 0;
            }
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
    }
}
