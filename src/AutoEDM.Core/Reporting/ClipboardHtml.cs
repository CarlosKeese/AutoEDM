using System.Globalization;
using System.Text;

namespace AutoEDM.Reporting
{
    /// <summary>
    /// O formato "HTML Format" da área de transferência do Windows, que o Word, o Excel, o Outlook e
    /// o Google Sheets esperam para colar HTML como TABELA em vez de texto cru: um cabeçalho com os
    /// OFFSETS EM BYTES UTF-8 do documento e do fragmento.
    ///
    /// Quem põe na área de transferência tem de gravar os BYTES de <see cref="Bytes"/> (como
    /// MemoryStream), não a string: dentro do Edge.exe (processo nativo) o WinForms cai no modo de
    /// compatibilidade antigo e <c>DataObject.SetData(DataFormats.Html, string)</c> grava ANSI — os
    /// offsets deixam de bater e o Excel mostra "Posi綷s" (Carlos, 2026-09-14). Ver [[saw-cut-list]].
    /// </summary>
    public static class ClipboardHtml
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Envelopa o fragmento com o cabeçalho e os offsets.</summary>
        public static string Wrap(string htmlFragment)
        {
            const string header =
                "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            const string pre = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
            const string post = "<!--EndFragment--></body></html>";
            string fragment = htmlFragment ?? "";

            var utf8 = Encoding.UTF8;
            int startHtml = utf8.GetByteCount(string.Format(Inv, header, 0, 0, 0, 0)); // D10: tamanho fixo
            int startFragment = startHtml + utf8.GetByteCount(pre);
            int endFragment = startFragment + utf8.GetByteCount(fragment);
            int endHtml = endFragment + utf8.GetByteCount(post);
            return string.Format(Inv, header, startHtml, endHtml, startFragment, endFragment) + pre + fragment + post;
        }

        /// <summary><see cref="Wrap"/> já em bytes UTF-8 SEM BOM, para gravar como stream.</summary>
        public static byte[] Bytes(string htmlFragment) =>
            new UTF8Encoding(false).GetBytes(Wrap(htmlFragment));

        /// <summary>Escapa o texto para HTML. Não usa WebUtility.HtmlEncode: ele troca "ç"/"é" por
        /// &amp;#231; — válido, mas ilegível para quem lê o HTML colado.</summary>
        public static string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
