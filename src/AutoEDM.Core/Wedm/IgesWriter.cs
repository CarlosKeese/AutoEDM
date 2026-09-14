using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoEDM.Wedm
{
    /// <summary>
    /// Grava curvas de perfil em IGES 5.3 (texto de 80 colunas), em MILÍMETROS e nas coordenadas da
    /// peça, sem matriz de transformação: o Pitágoras recebe cada ponto exatamente onde está no
    /// modelo. Entidades: 110 (reta), 100 (arco/círculo, anti-horário visto de +Z), 126 (B-spline
    /// com os polos e nós originais) e a polilinha como sequência de 110 — a entidade que todo
    /// importador lê. Só ASCII (acentos são removidos). Lógica pura, sem COM.
    /// </summary>
    public static class IgesWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Entity
        {
            public int Type;
            public string Label;
            public List<string> Params = new List<string>();
        }

        /// <param name="fileName">Nome do arquivo (vai para a seção global).</param>
        /// <param name="description">Texto livre da seção de início (S).</param>
        public static string Write(IEnumerable<WireCurve> curves, string fileName, string description, DateTime when)
        {
            var entities = new List<Entity>();
            double maxCoord = 0;
            foreach (WireCurve c in curves ?? Enumerable.Empty<WireCurve>())
                AddEntities(c, entities, ref maxCoord);

            var records = new StringBuilder();
            var start = Chunk(Ascii(description ?? ""), 72);
            if (start.Count == 0) start.Add("");
            int sCount = AppendSection(records, start, 'S');

            string stamp = when.ToString("yyyyMMdd.HHmmss", Inv);
            var global = new List<string>
            {
                "1H,", "1H;",
                H("AutoEDM"),                  // 3 produto (origem)
                H(Ascii(fileName ?? "")),       // 4 nome do arquivo
                H("AutoEDM"),                  // 5 sistema
                H("AutoEDM WEDM 1.0"),         // 6 versão do pré-processador
                "32", "38", "6", "308", "15",  // 7-11 precisão de inteiros e reais
                H("AutoEDM"),                  // 12 produto (destino)
                "1.0",                         // 13 escala do modelo
                "2", H("MM"),                  // 14-15 unidade: milímetro
                "1", "0.01",                   // 16-17 espessura de linha
                H(stamp),                      // 18 data da geração
                "0.0001",                      // 19 resolução mínima (mm)
                R(Math.Max(maxCoord, 1.0)),    // 20 maior coordenada
                H("AutoEDM"), "",              // 21-22 autor, organização
                "11", "0",                     // 23 IGES 5.3, 24 sem norma de desenho
                H(stamp),                      // 25 data do modelo
            };
            int gCount = AppendSection(records, Pack(global, 72), 'G');

            var dLines = new List<string>();
            var pLines = new List<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                Entity e = entities[i];
                int deSeq = 2 * i + 1;
                int pStart = pLines.Count + 1;
                List<string> lines = Pack(e.Params, 64);
                foreach (string l in lines) pLines.Add(l.PadRight(64) + " " + Field(deSeq, 7));

                dLines.Add(Field(e.Type) + Field(pStart) + Field(0) + Field(0) + Field(0) + Field(0) + Field(0) + Field(0) + "00000000");
                dLines.Add(Field(e.Type) + Field(0) + Field(0) + Field(lines.Count) + Field(0) /* forma 0 em todas as entidades usadas */ + Field("") + Field("") + Field(e.Label) + Field(0));
            }
            int dCount = AppendSection(records, dLines, 'D');
            int pCount = AppendSection(records, pLines, 'P');

            string terminate = $"S{Field(sCount, 7)}G{Field(gCount, 7)}D{Field(dCount, 7)}P{Field(pCount, 7)}";
            AppendSection(records, new List<string> { terminate }, 'T');
            return records.ToString();
        }

        private static void AddEntities(WireCurve c, List<Entity> into, ref double maxCoord)
        {
            switch (c.Kind)
            {
                case WireCurveKind.Line:
                    into.Add(LineEntity(c.Start, c.End, ref maxCoord));
                    break;

                case WireCurveKind.Arc:
                {
                    var e = new Entity { Type = 100, Label = "ARC" };
                    e.Params.AddRange(new[]
                    {
                        "100", R(c.Center[2]), R(c.Center[0]), R(c.Center[1]),
                        R(c.Start[0]), R(c.Start[1]), R(c.End[0]), R(c.End[1]),
                    });
                    Track(ref maxCoord, c.Center[0] + c.Radius, c.Center[1] + c.Radius, c.Center[0] - c.Radius, c.Center[1] - c.Radius, c.Center[2]);
                    into.Add(e);
                    break;
                }

                case WireCurveKind.BSpline:
                {
                    int k = c.Poles.Length - 1;
                    double[] w = c.Weights ?? Enumerable.Repeat(1.0, c.Poles.Length).ToArray();
                    bool polynomial = w.All(x => Math.Abs(x - w[0]) < 1e-12);
                    bool closed = WireCurve.Distance(c.Evaluate(c.ParamStart), c.Evaluate(c.ParamEnd)) < 1e-6;

                    var e = new Entity { Type = 126, Label = "SPLINE" };
                    e.Params.Add("126");
                    e.Params.Add(k.ToString(Inv));
                    e.Params.Add(c.Degree.ToString(Inv));
                    e.Params.Add("1");                        // PROP1: plana (só nível horizontal chega aqui)
                    e.Params.Add(closed ? "1" : "0");        // PROP2: fechada
                    e.Params.Add(polynomial ? "1" : "0");    // PROP3: polinomial
                    e.Params.Add("0");                        // PROP4: não periódica
                    e.Params.AddRange(c.Knots.Select(R));
                    e.Params.AddRange(w.Select(R));
                    foreach (double[] p in c.Poles)
                    {
                        e.Params.Add(R(p[0])); e.Params.Add(R(p[1])); e.Params.Add(R(p[2]));
                        Track(ref maxCoord, p[0], p[1], p[2]);
                    }
                    e.Params.Add(R(Math.Min(c.ParamStart, c.ParamEnd)));
                    e.Params.Add(R(Math.Max(c.ParamStart, c.ParamEnd)));
                    e.Params.AddRange(new[] { "0.0", "0.0", "1.0" }); // normal do plano
                    into.Add(e);
                    break;
                }

                default:
                    for (int i = 1; i < c.Points.Count; i++)
                        if (WireCurve.Distance(c.Points[i - 1], c.Points[i]) > 1e-9)
                            into.Add(LineEntity(c.Points[i - 1], c.Points[i], ref maxCoord));
                    break;
            }
        }

        private static Entity LineEntity(double[] a, double[] b, ref double maxCoord)
        {
            var e = new Entity { Type = 110, Label = "LINE" };
            e.Params.AddRange(new[] { "110", R(a[0]), R(a[1]), R(a[2]), R(b[0]), R(b[1]), R(b[2]) });
            Track(ref maxCoord, a[0], a[1], a[2], b[0], b[1], b[2]);
            return e;
        }

        private static void Track(ref double max, params double[] values)
        {
            foreach (double v in values) max = Math.Max(max, Math.Abs(v));
        }

        /// <summary>Real com ponto decimal, sem expoente, até 12 casas (bem abaixo de 1 µm).</summary>
        private static string R(double v) =>
            Math.Abs(v) < 5e-13 ? "0.0" : v.ToString("0.0###########", Inv);

        /// <summary>Texto Hollerith ("7HAutoEDM").</summary>
        private static string H(string s) => s.Length.ToString(Inv) + "H" + s;

        private static string Field(int value, int width = 8) => value.ToString(Inv).PadLeft(width);

        private static string Field(string value) => (value ?? "").PadLeft(8);

        /// <summary>Junta os parâmetros ("a," … "z;") em linhas de até <paramref name="width"/> colunas, sem quebrar um parâmetro no meio.</summary>
        private static List<string> Pack(IList<string> values, int width)
        {
            var lines = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                string token = values[i] + (i == values.Count - 1 ? ";" : ",");
                if (current.Length > 0 && current.Length + token.Length > width)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }
                while (token.Length > width) // só um texto enorme cai aqui
                {
                    lines.Add(token.Substring(0, width));
                    token = token.Substring(width);
                }
                current.Append(token);
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }

        private static List<string> Chunk(string s, int width)
        {
            var list = new List<string>();
            for (int i = 0; i < s.Length; i += width) list.Add(s.Substring(i, Math.Min(width, s.Length - i)));
            return list;
        }

        private static int AppendSection(StringBuilder sb, List<string> lines, char letter)
        {
            for (int i = 0; i < lines.Count; i++)
                sb.Append(lines[i].PadRight(72)).Append(letter).Append(Field(i + 1, 7)).Append("\r\n");
            return lines.Count;
        }

        /// <summary>Tira acentos e troca o que sobrar fora do ASCII por "?": IGES é texto de 7 bits.</summary>
        private static string Ascii(string s)
        {
            string decomposed = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(ch < 128 && !char.IsControl(ch) ? ch : '?');
            }
            return sb.ToString();
        }
    }
}
