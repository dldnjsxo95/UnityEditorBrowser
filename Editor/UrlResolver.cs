using System;
using System.Linq;

namespace EditorBrowser
{
    /// <summary>
    /// Turns user input into a URL: explicit http(s) stays as-is, host-like
    /// strings get an https:// prefix, anything else becomes a Google search.
    /// Pure — no Unity dependencies; unit-testable.
    /// </summary>
    internal static class UrlResolver
    {
        internal const string DefaultHomepage = "https://www.google.com/";
        private const string GoogleSearchPrefix = "https://www.google.com/search?q=";

        public static string Resolve(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return DefaultHomepage;

            var trimmed = input.Trim();

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return trimmed;
            }

            if (LooksLikeHost(trimmed))
                return "https://" + trimmed;

            return GoogleSearchPrefix + Uri.EscapeDataString(trimmed);
        }

        private static bool LooksLikeHost(string s)
        {
            if (s.IndexOf(' ') >= 0) return false;

            if (s == "localhost" || s.StartsWith("localhost:", StringComparison.Ordinal)) return true;

            var hostPart = s;
            var slashIdx = hostPart.IndexOf('/');
            if (slashIdx >= 0) hostPart = hostPart.Substring(0, slashIdx);

            var lastDot = hostPart.LastIndexOf('.');
            if (lastDot < 0 || lastDot == hostPart.Length - 1) return false;

            var tldPart = hostPart.Substring(lastDot + 1);
            var colonIdx = tldPart.IndexOf(':');
            if (colonIdx >= 0) tldPart = tldPart.Substring(0, colonIdx);

            return tldPart.Length >= 2 && tldPart.All(char.IsLetter);
        }
    }
}
