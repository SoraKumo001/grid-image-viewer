using quick_image_viewer.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
namespace quick_image_viewer.Helpers
{
    public class NaturalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            return StrCmpLogicalW(x, y);
        }
    }

    public static class FileNavigator
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(
            new[] {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico",
                ".dng", ".nef", ".cr2", ".arw", ".tga", ".pcx"
            },
            StringComparer.OrdinalIgnoreCase);

        public static string? FindNextImageFolder(string currentPath, int offset, CancellationToken token = default)
        {
            string? node = currentPath;

            int maxIterations = 1000;
            for (int i = 0; i < maxIterations; i++)
            {
                if (token.IsCancellationRequested) return null;
                node = offset == 1 ? GetNextNodeDFS(node) : GetPrevNodeDFS(node);
                if (string.IsNullOrEmpty(node)) break;

                try
                {
                    if (ArchiveManager.IsArchive(node))
                    {
                        if (ArchiveManager.GetArchiveImages(node).Any())
                        {
                            return node;
                        }
                        continue;
                    }

                    bool hasImages = Directory.EnumerateFiles(node)
                                              .Any(f => ImageExtensions.Contains(Path.GetExtension(f)));
                    if (hasImages)
                    {
                        return node;
                    }
                }
                catch { }
            }

            return null;
        }

        private static string[] GetChildNodes(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return Array.Empty<string>();
                return Directory.EnumerateFileSystemEntries(path)
                    .Where(e => Directory.Exists(e) || ArchiveManager.IsArchive(e))
                    .OrderBy(e => e, new NaturalStringComparer())
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private static string? GetNextNodeDFS(string current)
        {
            var children = GetChildNodes(current);
            if (children.Length > 0) return children[0];

            string node = current;
            while (true)
            {
                var parent = Directory.GetParent(node);
                if (parent == null) return null;

                try
                {
                    var siblings = GetChildNodes(parent.FullName);
                    int idx = Array.FindIndex(siblings, d => string.Equals(d, node, StringComparison.OrdinalIgnoreCase));
                    if (idx != -1 && idx + 1 < siblings.Length)
                    {
                        return siblings[idx + 1];
                    }
                }
                catch { }

                node = parent.FullName;
            }
        }

        private static string? GetPrevNodeDFS(string current)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) return null;

            try
            {
                var siblings = GetChildNodes(parent.FullName);
                int idx = Array.FindIndex(siblings, d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase));
                if (idx > 0)
                {
                    string node = siblings[idx - 1];
                    while (true)
                    {
                        var children = GetChildNodes(node);
                        if (children.Length == 0) return node;
                        node = children[children.Length - 1];
                    }
                }
                else if (idx == 0)
                {
                    return parent.FullName;
                }
            }
            catch { }

            return parent.FullName;
        }
    }
}
