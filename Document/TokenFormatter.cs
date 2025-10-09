using System;
using System.Text.RegularExpressions;

namespace PdfBuilder.Document
{
    internal static class TokenFormatter
    {
        private static readonly Regex DateRx = new(@"{date:([^}]+)}", RegexOptions.Compiled);
        private static readonly Regex TimeRx = new(@"{time:([^}]+)}", RegexOptions.Compiled);

        public sealed record Context(int Page, int Pages, string? Title, DateTime Now);

        public static string Apply(string? template, Context ctx)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            string s = template
                .Replace("{page}", ctx.Page.ToString())
                .Replace("{pages}", ctx.Pages.ToString())
                .Replace("{title}", ctx.Title ?? string.Empty);

            s = DateRx.Replace(s, m =>
            {
                var fmt = m.Groups[1].Value;
                return ctx.Now.ToString(fmt);
            });
            s = TimeRx.Replace(s, m =>
            {
                var fmt = m.Groups[1].Value;
                return ctx.Now.ToString(fmt);
            });

            return s;
        }
    }
}
