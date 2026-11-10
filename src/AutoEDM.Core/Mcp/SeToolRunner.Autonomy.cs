using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AutoEDM.Com;
using AutoEDM.Diagnostics;

namespace AutoEDM.Mcp
{
    /// <summary>
    /// As ferramentas que dão AUTONOMIA ao agente (Carlos, 2026-11-10: "a API, o COM e o MCP
    /// como olhos e ouvidos"): ver a tela, saber e trocar o documento, selecionar, e andar pela
    /// API COM inteira por um caminho escrito — sem depender de o usuário clicar, selecionar ou
    /// de existir uma ferramenta dedicada para cada pergunta.
    ///
    /// A fronteira continua a mesma do resto da ponte: LER (propriedades, tela, lista de
    /// documentos, seleção) funciona em somente-leitura; CHAMAR método, definir propriedade,
    /// abrir e fechar documento exigem a escrita liberada.
    /// </summary>
    public sealed partial class SeToolRunner
    {
        /// <summary>Imagem produzida pela ferramenta em curso; o <see cref="Execute"/> a anexa à resposta.</summary>
        private byte[] _imageBytes;
        private string _imageMime;

        /// <summary>Objetos COM devolvidos por chamadas anteriores, endereçáveis como <c>$1</c>, <c>$2</c>…
        /// É o que permite usar o retorno de um método (um documento novo, uma feature criada) no
        /// passo seguinte, já que cada pedido da ponte é independente.</summary>
        private readonly List<object> _handles = new List<object>();
        private const int MaxHandles = 200;

        /// <summary>Devolve null quando a ferramenta não é desta família.</summary>
        private string DispatchAutonomy(string tool, dynamic app, string argsJson, List<string> captured)
        {
            switch (tool.ToLowerInvariant())
            {
                case "se_documentos": return Documents(app);
                case "se_ativar_documento": return ActivateDocument(app, argsJson);
                case "se_abrir_documento": return OpenDocument(app, argsJson);
                case "se_fechar_documento": return CloseDocument(app, argsJson);
                case "se_captura": return Capture(app, argsJson);
                case "se_selecionar": return Select(app, argsJson);
                case "se_com_ler": return ComRead(app, argsJson, captured);
                case "se_com_chamar": return ComCall(app, argsJson);
                default: return null;
            }
        }

        // ------------------------------------------------------------------ documentos

        private static string KindOf(int type) =>
            type == 1 ? "peça" : type == 3 ? "montagem" : type == 2 ? "desenho" : type == 4 ? "chapa" : $"tipo {type}";

        private static string Documents(dynamic app)
        {
            int n = 0; try { n = (int)app.Documents.Count; } catch { }
            if (n == 0) return "Nenhum documento aberto na Solid Edge.";

            string active = null;
            try { active = (string)app.ActiveDocument.FullName; } catch { }

            var sb = new StringBuilder();
            sb.AppendLine($"{n} documento(s) aberto(s)  (► = ativo):");
            for (int i = 1; i <= n; i++)
            {
                dynamic d = null;
                try { d = app.Documents.Item(i); } catch { sb.AppendLine($"  [{i}] inacessível."); continue; }
                string name = Str(() => (string)d.Name, "(sem nome)");
                string full = Str(() => (string)d.FullName, "");
                int type = -1; try { type = (int)d.Type; } catch { }
                bool saved = full.IndexOf('\\') >= 0 || full.IndexOf('/') >= 0;
                string dirty = ""; try { if ((bool)d.Dirty) dirty = "  [ALTERADO, não salvo]"; } catch { }
                string ro = ""; try { if ((bool)d.ReadOnly) ro = "  [somente leitura]"; } catch { }
                string mark = active != null && string.Equals(active, full, StringComparison.OrdinalIgnoreCase) ? "►" : " ";
                sb.AppendLine($" {mark}[{i}] {name}  —  {KindOf(type)}{dirty}{ro}");
                sb.AppendLine($"       {(saved ? full : "(nunca salvo)")}");
            }
            sb.AppendLine();
            sb.Append("Documentos abertos EM CONTEXTO pela montagem também aparecem aqui. 'se_ativar_documento' troca o ativo.");
            return sb.ToString();
        }

        /// <summary>Acha um documento aberto por nome, caminho completo ou índice da lista.</summary>
        private static object FindDocument(dynamic app, string key, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(key)) { problem = "Faltou 'nome' (nome, caminho ou índice de 'se_documentos')."; return null; }
            int n = 0; try { n = (int)app.Documents.Count; } catch { }

            if (int.TryParse(key.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            {
                if (index < 1 || index > n) { problem = $"Índice {index} fora da lista (há {n} documento(s))."; return null; }
                return app.Documents.Item(index);
            }

            var names = new List<string>();
            for (int i = 1; i <= n; i++)
            {
                dynamic d = null; try { d = app.Documents.Item(i); } catch { continue; }
                string name = Str(() => (string)d.Name, "");
                string full = Str(() => (string)d.FullName, "");
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(name), key, StringComparison.OrdinalIgnoreCase))
                    return d;
                names.Add(name);
            }
            problem = $"Nenhum documento aberto chamado '{key}'. Abertos: {string.Join("; ", names)}.";
            return null;
        }

        private static string ActivateDocument(dynamic app, string argsJson)
        {
            dynamic d = FindDocument(app, ReadString(argsJson, "nome"), out string problem);
            if (d == null) return "RECUSADO: " + problem;

            string wanted = Str(() => (string)d.FullName, "");
            d.Activate();   // SolidEdgeDocument.Activate() — 0 parâmetros (typelib)
            string now = Str(() => (string)app.ActiveDocument.FullName, "?");
            if (!string.Equals(now, wanted, StringComparison.OrdinalIgnoreCase))
                return $"Activate() não levantou erro, mas o documento ativo é '{now}' e não '{wanted}'. " +
                       "Documento aberto em contexto por uma montagem nem sempre tem janela própria.";

            int type = -1; try { type = (int)d.Type; } catch { }
            return $"Ativo agora: {Str(() => (string)d.Name, "?")}  —  {KindOf(type)}. A seleção anterior não acompanha a troca.";
        }

        private static string OpenDocument(dynamic app, string argsJson)
        {
            string file = ReadString(argsJson, "arquivo");
            if (string.IsNullOrWhiteSpace(file)) return "RECUSADO: faltou 'arquivo' (caminho completo).";
            if (!File.Exists(file)) return $"RECUSADO: o arquivo não existe: {file}";

            dynamic already = FindDocument(app, file, out string _);
            if (already != null)
            {
                already.Activate();
                return $"'{Path.GetFileName(file)}' já estava aberto; foi ativado.";
            }

            // DisplayAlerts desligado: um aviso de vínculo quebrado abriria uma caixa modal que
            // ninguém vê e a chamada ficaria pendurada até o teto de 5 min da ponte.
            bool alerts = true; try { alerts = (bool)app.DisplayAlerts; } catch { }
            try
            {
                try { app.DisplayAlerts = false; } catch { }
                dynamic doc = app.Documents.Open(file);   // Open(Filename, [opt]…5) — só o 1º é obrigatório
                int type = -1; try { type = (int)doc.Type; } catch { }
                Log.Info($"MCP: documento aberto pelo agente — {file}");
                return $"Aberto e ativo: {Str(() => (string)doc.Name, Path.GetFileName(file))}  —  {KindOf(type)}.";
            }
            finally { try { app.DisplayAlerts = alerts; } catch { } }
        }

        private static string CloseDocument(dynamic app, string argsJson)
        {
            dynamic d = FindDocument(app, ReadString(argsJson, "nome"), out string problem);
            if (d == null) return "RECUSADO: " + problem;

            bool save = ReadBool(argsJson, "salvar", false);
            bool discard = ReadBool(argsJson, "descartar", false);
            string name = Str(() => (string)d.Name, "?");
            string full = Str(() => (string)d.FullName, "");
            bool everSaved = full.IndexOf('\\') >= 0 || full.IndexOf('/') >= 0;
            bool dirty = false; try { dirty = (bool)d.Dirty; } catch { }

            if (save && discard) return "RECUSADO: 'salvar' e 'descartar' juntos — escolha um.";
            if (dirty && !save && !discard)
                return $"RECUSADO: '{name}' tem alterações não salvas. Repita com \"salvar\": true ou, para jogá-las fora, " +
                       "\"descartar\": true.";
            if (save && !everSaved)
                return $"RECUSADO: '{name}' nunca foi salvo, então não há onde salvar. Grave com 'se_com_chamar' " +
                       "(membro SaveAs, args [\"C:\\\\pasta\\\\arquivo.par\"]) e feche depois; ou feche com \"descartar\": true.";

            bool alerts = true; try { alerts = (bool)app.DisplayAlerts; } catch { }
            try
            {
                try { app.DisplayAlerts = false; } catch { }
                d.Close(save);   // Close([opt] SaveChanges, [opt] FileName, [opt] RouteWorkbook)
            }
            finally { try { app.DisplayAlerts = alerts; } catch { } }

            Log.Info($"MCP: documento fechado pelo agente — {name} ({(save ? "salvo" : dirty ? "alterações descartadas" : "sem alterações")}).");
            return $"Fechado: {name} ({(save ? "salvo antes" : dirty ? "alterações DESCARTADAS" : "sem alterações")}). " +
                   $"Ativo agora: {Str(() => (string)app.ActiveDocument.Name, "nenhum")}.";
        }

        // ------------------------------------------------------------------ olhos

        private string Capture(dynamic app, string argsJson)
        {
            int width = ReadInt(argsJson, "largura", 1280, 320, 3840);
            int height = ReadInt(argsJson, "altura", 800, 240, 2160);
            bool fit = ReadBool(argsJson, "ajustar", false);

            dynamic window = null; try { window = app.ActiveWindow; } catch { }
            if (window == null) return "RECUSADO: não há janela ativa na Solid Edge para capturar.";

            dynamic view = null;
            try { view = window.View; } catch { }
            if (view == null)
                return "RECUSADO: a janela ativa não tem uma vista 3D (Window.View) — é a de um desenho (.dft)? " +
                       "A captura hoje cobre peça e montagem.";

            if (fit) { try { view.Fit(); } catch (Exception ex) { Log.Warn("MCP: View.Fit falhou — " + ex.GetBaseException().Message); } }

            // JPG, e numa pasta de caminho LONGO: medido em 2026-11-10 (SE 223.00.13.05),
            // View.SaveAsImage com extensão .png volta sem erro e NÃO grava arquivo; .jpg e .bmp gravam.
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoEDM", "logs");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "captura_mcp.jpg");
            try { if (File.Exists(file)) File.Delete(file); } catch { }

            view.SaveAsImage(file, width, height);   // SaveAsImage(Filename, [opt]Width, [opt]Height, …)

            if (!File.Exists(file) || new FileInfo(file).Length == 0)
                return "A Solid Edge aceitou View.SaveAsImage mas não gravou a imagem (falha silenciosa). Caminho tentado: " + file;

            _imageBytes = File.ReadAllBytes(file);
            _imageMime = "image/jpeg";
            try { File.Delete(file); } catch { }

            return $"Captura da janela '{Str(() => (string)window.Caption, "?")}' — {width}×{height} px" +
                   (fit ? ", vista ajustada ao modelo (Fit)." : ", na vista em que o usuário a deixou.");
        }

        // ------------------------------------------------------------------ seleção

        private string Select(dynamic app, string argsJson)
        {
            dynamic doc = null; try { doc = app.ActiveDocument; } catch { }
            if (doc == null) return NoActiveDocument;
            dynamic ss = doc.SelectSet;

            var paths = new List<string>();
            var occurrences = new List<string>();
            bool clear = true;
            try
            {
                using (JsonDocument j = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson))
                {
                    JsonElement r = j.RootElement;
                    if (r.TryGetProperty("limpar", out JsonElement c) && c.ValueKind == JsonValueKind.False) clear = false;
                    if (r.TryGetProperty("caminhos", out JsonElement p) && p.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement e in p.EnumerateArray()) if (e.ValueKind == JsonValueKind.String) paths.Add(e.GetString());
                    if (r.TryGetProperty("ocorrencias", out JsonElement o) && o.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement e in o.EnumerateArray())
                            occurrences.Add(e.ValueKind == JsonValueKind.Number ? e.GetRawText() : e.GetString());
                }
            }
            catch (Exception ex) { return "RECUSADO: argumentos ilegíveis — " + ex.GetBaseException().Message; }

            if (clear) ss.RemoveAll();

            var failures = new List<string>();
            int added = 0;

            foreach (string key in occurrences)
            {
                try
                {
                    object occ = FindOccurrence(doc, key);
                    if (occ == null) { failures.Add($"ocorrência '{key}': não encontrada no 1º nível"); continue; }
                    ss.Add(occ);
                    added++;
                }
                catch (Exception ex) { failures.Add($"ocorrência '{key}': {ex.GetBaseException().Message}"); }
            }

            foreach (string path in paths)
            {
                try
                {
                    object target = ComPath.Evaluate(name => ResolveRoot(app, name), path, readOnly: true);
                    if (target == null || !System.Runtime.InteropServices.Marshal.IsComObject(target))
                    { failures.Add($"'{path}': não é um objeto COM selecionável"); continue; }
                    ss.Add(target);
                    added++;
                }
                catch (Exception ex) { failures.Add($"'{path}': {ex.GetBaseException().Message}"); }
            }

            int count = -1; try { count = (int)ss.Count; } catch { }
            var sb = new StringBuilder();
            sb.Append($"Seleção: {count} objeto(s) agora ({added} adicionado(s) nesta chamada{(clear ? ", seleção anterior limpa" : "")}).");
            foreach (string f in failures) sb.AppendLine().Append("  ✗ ").Append(f);
            return sb.ToString();
        }

        /// <summary>Ocorrência do 1º nível por índice (1-based) ou pelo nome ("15428.203.par:2").</summary>
        private static object FindOccurrence(dynamic doc, string key)
        {
            dynamic occs = doc.Occurrences;
            int n = (int)occs.Count;
            if (int.TryParse((key ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                return index >= 1 && index <= n ? (object)occs.Item(index) : null;
            for (int i = 1; i <= n; i++)
            {
                dynamic o = occs.Item(i);
                if (string.Equals(Str(() => (string)o.Name, ""), key, StringComparison.OrdinalIgnoreCase)) return o;
            }
            return null;
        }

        // ------------------------------------------------------------------ COM genérico

        /// <summary>Raízes de um caminho: app, doc, sel, janela, docs e os identificadores $N.</summary>
        private object ResolveRoot(dynamic app, string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "app": return app;
                case "doc": try { return app.ActiveDocument; } catch { return null; }
                case "sel": try { return app.ActiveDocument.SelectSet; } catch { return null; }
                case "janela": try { return app.ActiveWindow; } catch { return null; }
                case "docs": try { return app.Documents; } catch { return null; }
            }
            if (name != null && name.Length > 1 && name[0] == '$' &&
                int.TryParse(name.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int h) &&
                h >= 1 && h <= _handles.Count)
                return _handles[h - 1];
            throw new InvalidOperationException(
                $"Raiz desconhecida '{name}'. Use app (Application), doc (documento ativo), sel (SelectSet), janela " +
                "(janela ativa), docs (Documents) ou um identificador $N devolvido por uma chamada anterior.");
        }

        /// <summary>Anda um caminho de LEITURA. Caminho malformado ou passo que falha volta como
        /// texto para o agente (com o passo que falhou), sem rastro de pilha no log — errar um
        /// nome de propriedade faz parte de explorar a API, não é defeito do add-in.</summary>
        private bool TryEvaluate(object app, string path, out object value, out string failure)
        {
            value = null;
            failure = null;
            try
            {
                value = ComPath.Evaluate(name => ResolveRoot(app, name), path, readOnly: true);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidOperationException)
            {
                string hr = ex.InnerException is System.Runtime.InteropServices.COMException ce ? $"  (HRESULT 0x{ce.ErrorCode:X8})" : "";
                failure = "FALHOU: " + ex.Message + hr;
                return false;
            }
        }

        private string Remember(object com)
        {
            for (int i = 0; i < _handles.Count; i++) if (ReferenceEquals(_handles[i], com)) return "$" + (i + 1);
            if (_handles.Count >= MaxHandles) _handles.Clear();   // os $N antigos deixam de valer
            _handles.Add(com);
            return "$" + _handles.Count;
        }

        /// <summary>Um valor vindo do COM, em texto que o agente consegue usar no passo seguinte.</summary>
        private string DescribeValue(object v)
        {
            if (v == null) return "(vazio / Nothing)";
            if (v is string s) return "\"" + s + "\"  (texto)";
            if (v is bool b) return b ? "true" : "false";
            if (v is Array a)
            {
                var parts = new List<string>();
                int shown = 0;
                foreach (object item in a)
                {
                    if (shown++ >= 60) { parts.Add("…"); break; }
                    parts.Add(item == null ? "null" : item is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : item.ToString());
                }
                return $"[{string.Join(", ", parts)}]  ({a.Length} item(ns), {a.GetType().GetElementType()?.Name})";
            }
            if (System.Runtime.InteropServices.Marshal.IsComObject(v))
            {
                string count = "";
                try { count = $", Count = {(int)((dynamic)v).Count}"; } catch { }
                return $"objeto COM {ComDiagnostics.TypeNameOf(v)}{count}  →  guardado como {Remember(v)}";
            }
            if (v is IFormattable fmt) return fmt.ToString(null, CultureInfo.InvariantCulture) + $"  ({v.GetType().Name})";
            return v + $"  ({v.GetType().Name})";
        }

        private string ComRead(dynamic app, string argsJson, List<string> captured)
        {
            string path = ReadString(argsJson, "caminho");
            if (string.IsNullOrWhiteSpace(path)) return "RECUSADO: faltou 'caminho' (ex.: \"doc.Models.Item(1).Body\").";
            int depth = ReadInt(argsJson, "profundidade", -1, -1, 2);
            List<string> signatures = ReadStringArray(argsJson, "assinaturas");

            if (!TryEvaluate(app, path, out object value, out string failure)) return failure;

            var sb = new StringBuilder();
            sb.Append(path).Append("  =  ").AppendLine(DescribeValue(value));

            bool isCom = value != null && System.Runtime.InteropServices.Marshal.IsComObject(value);
            if (isCom && (depth >= 0 || signatures.Count > 0))
            {
                lock (captured) captured.Clear();
                if (depth >= 0) ComDiagnostics.DumpObject(path, value, depth);
                if (signatures.Count > 0) ComDiagnostics.LogSignatures(value, signatures.ToArray());
                sb.AppendLine().Append(Join(captured));
            }
            else if (isCom)
                sb.AppendLine().Append("Para ver propriedades e métodos: \"profundidade\": 0 (só o objeto) a 2. " +
                                       "Para os parâmetros de um método: \"assinaturas\": [\"Nome\"].");
            return sb.ToString();
        }

        private string ComCall(dynamic app, string argsJson)
        {
            string path = ReadString(argsJson, "caminho");
            string member = ReadString(argsJson, "membro");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(member))
                return "RECUSADO: faltou 'caminho' (o objeto) ou 'membro' (o método ou propriedade).";
            bool set = ReadBool(argsJson, "definir", false);

            if (!TryEvaluate(app, path, out object target, out string failure)) return failure;
            if (target == null) return $"RECUSADO: '{path}' devolveu vazio — não há objeto onde chamar '{member}'.";

            List<int> byRef;
            object[] args;
            try { args = BuildArgs(app, argsJson, out byRef); }
            catch (Exception ex) { return "RECUSADO: argumentos inválidos — " + ex.GetBaseException().Message; }

            Log.Info($"MCP: se_com_chamar {path}.{member}{(set ? " = " : "")}({args.Length} arg(s))");

            object result;
            try
            {
                if (set)
                {
                    if (args.Length != 1) return "RECUSADO: com \"definir\": true passe exatamente um valor em 'args'.";
                    target.GetType().InvokeMember(member, BindingFlags.SetProperty, null, target, args, CultureInfo.InvariantCulture);
                    return $"{path}.{member} definido.";
                }

                ParameterModifier[] mods = null;
                if (byRef.Count > 0)
                {
                    // Sem o ParameterModifier o InvokeMember devolve os [out] zerados, em silêncio.
                    var mod = new ParameterModifier(args.Length);
                    foreach (int i in byRef) mod[i] = true;
                    mods = new[] { mod };
                }
                result = target.GetType().InvokeMember(member, BindingFlags.InvokeMethod | BindingFlags.GetProperty,
                    null, target, args, mods, CultureInfo.InvariantCulture, null);
            }
            catch (Exception ex)
            {
                Exception b = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex.GetBaseException();
                string hr = b is System.Runtime.InteropServices.COMException ce ? $"  (HRESULT 0x{ce.ErrorCode:X8})" : "";
                Log.Warn($"MCP: {path}.{member} falhou — {b.Message}{hr}");
                return $"FALHOU: {path}.{member} — {b.Message}{hr}\n\n" +
                       "Confira a assinatura real com 'se_com_ler' (\"assinaturas\": [\"" + member + "\"]) antes de tentar de novo.";
            }

            var sb = new StringBuilder();
            sb.Append($"{path}.{member}(…)  →  ").Append(DescribeValue(result));
            foreach (int i in byRef) sb.AppendLine().Append($"  saída do argumento {i + 1}: ").Append(DescribeValue(args[i]));
            return sb.ToString();
        }

        /// <summary>
        /// Converte 'args' (JSON) nos argumentos da chamada. Número inteiro vira int, decimal vira
        /// double, null = parâmetro opcional omitido; objeto = forma explícita:
        /// {"ref":"caminho"} objeto COM · {"int":n} · {"double":x} · {"ints":[…]} · {"doubles":[…]} ·
        /// {"textos":[…]} · {"nulo":true} · saída: {"out":"double"} / "int" / "bool" / "texto" (escalar tipado), {"out":"doubles","n":3},
        /// {"out":"ints","n":2}, {"out":"valor"} (VARIANT/objeto).
        /// </summary>
        private object[] BuildArgs(dynamic app, string argsJson, out List<int> byRef)
        {
            byRef = new List<int>();
            var list = new List<object>();
            using (JsonDocument j = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson))
            {
                if (!j.RootElement.TryGetProperty("args", out JsonElement arr) || arr.ValueKind == JsonValueKind.Null) return new object[0];
                if (arr.ValueKind != JsonValueKind.Array) throw new FormatException("'args' tem de ser uma lista.");

                foreach (JsonElement e in arr.EnumerateArray())
                {
                    switch (e.ValueKind)
                    {
                        case JsonValueKind.Number:
                            if (e.TryGetInt32(out int n) && e.GetRawText().IndexOfAny(new[] { '.', 'e', 'E' }) < 0) list.Add(n);
                            else list.Add(e.GetDouble());
                            break;
                        case JsonValueKind.String: list.Add(e.GetString()); break;
                        case JsonValueKind.True: list.Add(true); break;
                        case JsonValueKind.False: list.Add(false); break;
                        case JsonValueKind.Null: list.Add(Type.Missing); break;
                        case JsonValueKind.Array: list.Add(Doubles(e)); break;
                        case JsonValueKind.Object:
                            if (e.TryGetProperty("ref", out JsonElement r))
                            {
                                string refPath = r.GetString();
                                object o = ComPath.Evaluate(name => ResolveRoot(app, name), refPath, readOnly: true);
                                if (o == null) throw new FormatException($"{{\"ref\":\"{refPath}\"}} devolveu vazio.");
                                list.Add(o);
                            }
                            else if (e.TryGetProperty("int", out JsonElement i1)) list.Add(i1.GetInt32());
                            else if (e.TryGetProperty("double", out JsonElement d1)) list.Add(d1.GetDouble());
                            else if (e.TryGetProperty("doubles", out JsonElement ds)) list.Add(Doubles(ds));
                            else if (e.TryGetProperty("ints", out JsonElement isx))
                            {
                                var ints = new List<int>();
                                foreach (JsonElement x in isx.EnumerateArray()) ints.Add(x.GetInt32());
                                list.Add(ints.ToArray());
                            }
                            else if (e.TryGetProperty("textos", out JsonElement ts))
                            {
                                var texts = new List<string>();
                                foreach (JsonElement x in ts.EnumerateArray()) texts.Add(x.GetString());
                                list.Add(texts.ToArray());
                            }
                            else if (e.TryGetProperty("nulo", out JsonElement _)) list.Add(null);
                            else if (e.TryGetProperty("out", out JsonElement outKind))
                            {
                                int size = e.TryGetProperty("n", out JsonElement sz) ? sz.GetInt32() : 0;
                                switch (outKind.GetString())
                                {
                                    // [in,out] exige o array já ALOCADO (Face.GetRange); [out] puro aceita vazio.
                                    case "doubles": list.Add(new double[size]); break;
                                    case "ints": list.Add(new int[size]); break;
                                    // Saída ESCALAR tem de ir com o tipo certo: um VARIANT vazio por
                                    // referência num parâmetro double* dá DISP_E_TYPEMISMATCH
                                    // (Occurrence.GetTransform, medido em 2026-11-10).
                                    case "double": list.Add(0.0); break;
                                    case "int": list.Add(0); break;
                                    case "bool": list.Add(false); break;
                                    case "texto": list.Add(""); break;
                                    case "valor": list.Add(null); break;   // VARIANT* / objeto de saída
                                    default: throw new FormatException("\"out\" aceita \"double\", \"int\", \"bool\", \"texto\", \"doubles\", \"ints\" ou \"valor\".");
                                }
                                byRef.Add(list.Count - 1);
                            }
                            else throw new FormatException("Objeto de argumento desconhecido: " + e.GetRawText());
                            break;
                        default: throw new FormatException("Argumento não suportado: " + e.GetRawText());
                    }
                }
            }
            return list.ToArray();
        }

        private static double[] Doubles(JsonElement array)
        {
            var values = new List<double>();
            foreach (JsonElement x in array.EnumerateArray())
            {
                if (x.ValueKind != JsonValueKind.Number) throw new FormatException("Lista de argumento só aceita números (vira double[]).");
                values.Add(x.GetDouble());
            }
            return values.ToArray();
        }
    }
}
