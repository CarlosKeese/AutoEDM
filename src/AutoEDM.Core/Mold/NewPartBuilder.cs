using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoEDM.Assembly;
using AutoEDM.Diagnostics;
using AutoEDM.Electrode;
using AutoEDM.Model;
using AutoEDM.Revisions;
using AutoEDM.Selection;

namespace AutoEDM.Mold
{
    /// <summary>O que "Nova peça" vai usar: nome e pasta, calculados antes de criar (prévia da janela).</summary>
    public sealed class NewPartNamePlan
    {
        public string Folder { get; set; }
        public string Prefix { get; set; }
        public int Number { get; set; }            // -1 = série cheia
        public string FileName => Prefix == null || Number < 0 ? null : MoldPartNaming.FileName(Prefix, Number);
        public string Problem { get; set; }        // por que não dá para criar (null = dá)
    }

    public sealed class NewPartOptions
    {
        public MoldSection Section { get; set; } = MoldSection.Fixed;
        public HeightAxis HeightAxis { get; set; } = HeightAxis.Z;
        public bool OriginAtTop { get; set; }
    }

    public sealed class NewPartResult
    {
        public bool Created { get; set; }
        public string Message { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    /// Botão "Nova peça" do grupo Molde (Carlos, 2026-09-24): na MONTAGEM, cria uma peça vazia
    /// codificada na série da parte escolhida (fixa .100, móvel .200, extração .300 — próximo
    /// número livre) e a posiciona com a ORIENTAÇÃO DA MONTAGEM, na origem tirada das faces
    /// escolhidas: centro nos dois eixos de planta e o ponto mais baixo ou mais alto no eixo de
    /// altura do projeto (que pode ser X, Y ou Z — depende de como o projetista orientou o molde).
    ///
    /// Diferença para o eletrodo, de propósito: o eletrodo HERDA a orientação da cavidade (desce
    /// no eixo em que ela foi aberta); a peça do molde segue os eixos da montagem.
    /// </summary>
    public static class NewPartBuilder
    {
        /// <summary>
        /// Pasta, código e próximo número. Pasta = a da montagem (é onde o MD-15335 guarda as
        /// peças codificadas). Os números em uso saem da pasta E das ocorrências da montagem —
        /// peça inserida de outra pasta também ocupa o número.
        /// </summary>
        public static NewPartNamePlan PlanName(dynamic asmDoc, MoldSection section)
        {
            var plan = new NewPartNamePlan();
            string asmPath = null;
            try { asmPath = (string)asmDoc.FullName; } catch { }
            if (string.IsNullOrWhiteSpace(asmPath) || !File.Exists(asmPath))
            {
                plan.Problem = "A montagem ainda não foi salva — sem pasta do projeto para gravar a peça.";
                return plan;
            }
            plan.Folder = Path.GetDirectoryName(asmPath);

            var names = new List<string>();
            try { names.AddRange(Directory.GetFiles(plan.Folder).Select(Path.GetFileName)); }
            catch (Exception e) { Log.Warn($"Nova peça: pasta '{plan.Folder}' ilegível — " + e.GetBaseException().Message); }
            foreach (OccurrenceInfo occ in new AssemblyContext(asmDoc).GetOccurrences())
            {
                string f = null;
                try { f = (string)occ.ComOccurrence.OccurrenceFileName; } catch { }
                if (f == null) try { f = (string)occ.OccurrenceDocument.FullName; } catch { }
                if (f != null) names.Add(Path.GetFileName(f));
            }

            plan.Prefix = MoldPartNaming.GuessPrefix(names, ProjectFolder.Parse(asmPath).MoldCode);
            if (plan.Prefix == null)
            {
                plan.Problem = "Não achei o código do molde: nenhuma peça \"XXXXX.NNN.par\" na pasta e a pasta não tem \"MD-XXXXX\" no nome.";
                return plan;
            }
            plan.Number = MoldPartNaming.NextNumber(names, plan.Prefix, section);
            if (plan.Number < 0)
                plan.Problem = $"A série {(int)section}…{(int)section + 99} do {plan.Prefix} está cheia.";
            return plan;
        }

        public static NewPartResult Create(dynamic app, dynamic asmDoc, IList<PickedFace> faces, NewPartOptions opt)
        {
            var res = new NewPartResult();
            if (faces == null || faces.Count == 0) { res.Message = "Nenhuma face escolhida."; return res; }

            // 1) Caixa das faces em coordenadas da MONTAGEM — cada face pela pose da sua ocorrência.
            OccurrenceTransform identity = OccurrenceTransform.FromMatrix(
                new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, new[] { 0.0, 0.0, 0.0 });
            var poses = new Dictionary<string, OccurrenceTransform>();
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            int boxed = 0, raw = 0;
            foreach (PickedFace k in faces)
            {
                if (k?.Face == null || !FaceGeometry.TryGetRangeMm(k.Face, out double[] mn, out double[] mx)) continue;
                OccurrenceTransform pose = null;
                if (k.Occurrence != null)
                {
                    string key = k.OccurrenceName ?? "?";
                    if (!poses.TryGetValue(key, out pose))
                        poses[key] = pose = AssemblyContext.TryGetPose(new OccurrenceInfo(k.Occurrence, key, null));
                }
                if (pose != null) OccurrenceTransform.MapBoxMm(pose, identity, mn, mx, out mn, out mx);
                else raw++;
                for (int i = 0; i < 3; i++) { min[i] = Math.Min(min[i], mn[i]); max[i] = Math.Max(max[i], mx[i]); }
                boxed++;
            }
            if (boxed == 0) { res.Message = "Não consegui ler a geometria (caixa) das faces escolhidas."; return res; }
            if (raw > 0)
                Log.Warn($"Nova peça: {raw} face(s) sem pose da ocorrência — entraram como se já estivessem nas coordenadas da montagem.");

            double[] originMm = NewPartPlacement.OriginMm(min, max, opt.HeightAxis, opt.OriginAtTop);
            Log.Info(string.Format(CultureInfo.InvariantCulture,
                "Nova peça: {0} face(s), caixa na montagem X {1:0.0}…{2:0.0}  Y {3:0.0}…{4:0.0}  Z {5:0.0}…{6:0.0} mm; " +
                "altura em {7}, origem no ponto mais {8} → ({9:0.0}, {10:0.0}, {11:0.0}) mm.",
                boxed, min[0], max[0], min[1], max[1], min[2], max[2], opt.HeightAxis,
                opt.OriginAtTop ? "ALTO" : "BAIXO", originMm[0], originMm[1], originMm[2]));

            // 2) Nome: recalculado AGORA (outra peça pode ter nascido desde a prévia).
            NewPartNamePlan name = PlanName(asmDoc, opt.Section);
            if (name.Problem != null) { res.Message = name.Problem; return res; }
            string path = Path.Combine(name.Folder, name.FileName);
            if (File.Exists(path)) { res.Message = $"'{name.FileName}' já existe na pasta — nada foi criado."; return res; }

            // 3) Peça vazia → salva → insere → posiciona (orientação da montagem).
            dynamic partDoc = null;
            try
            {
                partDoc = app.Documents.Add("SolidEdge.PartDocument");
                partDoc.SaveAs(path);
                partDoc.Close();
                partDoc = null;

                dynamic occ = asmDoc.Occurrences.AddByFilename(path);
                double xM = Units.MmToM(originMm[0]), yM = Units.MmToM(originMm[1]), zM = Units.MmToM(originMm[2]);
                if (!TryPutMatrix(occ, identity.WithTranslationM(xM, yM, zM)))
                    occ.PutOrigin(xM, yM, zM);

                res.Created = true;
                res.Path = path;
                res.Message = string.Format(CultureInfo.InvariantCulture,
                    "{0} criada ({1}) com a origem em ({2:0.0}; {3:0.0}; {4:0.0}) mm da montagem — ponto mais {5} em {6}, orientação da montagem.",
                    name.FileName, MoldPartNaming.SectionLabel(opt.Section), originMm[0], originMm[1], originMm[2],
                    opt.OriginAtTop ? "alto" : "baixo", opt.HeightAxis);
                Log.Info("Nova peça: " + res.Message + " → " + path);
                return res;
            }
            catch (Exception e)
            {
                try { if (partDoc != null) partDoc.Close(); } catch { }
                res.Message = $"Falha ao criar '{name.FileName}': {e.GetBaseException().Message}";
                Log.Warn("Nova peça: " + res.Message);
                return res;
            }
        }

        /// <summary><c>Occurrence.PutMatrix(Matrix, Replace:true)</c> — a mesma chamada que o eletrodo
        /// usa; aqui com rotação identidade, que garante os eixos da montagem.</summary>
        private static bool TryPutMatrix(object occ, OccurrenceTransform pose)
        {
            try
            {
                occ.GetType().InvokeMember("PutMatrix", BindingFlags.InvokeMethod, null, occ,
                    new object[] { pose.ToMatrix(), true }, null, CultureInfo.InvariantCulture, null);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Nova peça: PutMatrix falhou (" + e.GetBaseException().Message + ") — caindo para PutOrigin.");
                return false;
            }
        }
    }
}
