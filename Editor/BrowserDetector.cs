using System;
using System.IO;

namespace EditorBrowser
{
    internal enum BrowserKind
    {
        None,
        Chrome,
        Edge,
    }

    internal sealed class BrowserInfo
    {
        public BrowserKind Kind { get; }
        public string ExecutablePath { get; }

        public BrowserInfo(BrowserKind kind, string path)
        {
            Kind = kind;
            ExecutablePath = path;
        }

        public bool IsAvailable => Kind != BrowserKind.None && !string.IsNullOrEmpty(ExecutablePath);
    }

    /// <summary>
    /// Detects an installed Chrome (preferred) or Edge by checking the known
    /// install paths. Avoids registry access because Microsoft.Win32.Registry
    /// requires an extra NuGet under .NET Standard 2.1.
    /// </summary>
    internal static class BrowserDetector
    {
        public static BrowserInfo Detect()
        {
            var chrome = FindFirstExisting(ChromeCandidatePaths());
            if (!string.IsNullOrEmpty(chrome))
                return new BrowserInfo(BrowserKind.Chrome, chrome);

            var edge = FindFirstExisting(EdgeCandidatePaths());
            if (!string.IsNullOrEmpty(edge))
                return new BrowserInfo(BrowserKind.Edge, edge);

            return new BrowserInfo(BrowserKind.None, null);
        }

        private static string[] ChromeCandidatePaths()
        {
            var pf  = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(pf,  "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"),
            };
        }

        private static string[] EdgeCandidatePaths()
        {
            var pf  = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return new[]
            {
                Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pf,  "Microsoft", "Edge", "Application", "msedge.exe"),
            };
        }

        private static string FindFirstExisting(string[] paths)
        {
            foreach (var p in paths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        return p;
                }
                catch
                {
                }
            }
            return null;
        }
    }
}
