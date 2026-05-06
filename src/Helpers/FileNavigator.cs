using quick_image_viewer.Managers;
using quick_image_viewer.Services;
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
        public static string? FindNextImageFolder(string currentPath, int offset, IEnumerable<string>? allowedExtensions = null, CancellationToken token = default)
        {
            string? node = currentPath;

            int maxIterations = 1000;
            for (int i = 0; i < maxIterations; i++)
            {
                if (token.IsCancellationRequested) return null;
                node = offset == 1 ? GetNextNodeDFS(node, allowedExtensions) : GetPrevNodeDFS(node, allowedExtensions);
                if (string.IsNullOrEmpty(node)) break;

                try
                {
                    if (ArchiveManager.IsArchive(node, allowedExtensions))
                    {
                        if (ArchiveManager.GetArchiveImages(node, allowedExtensions).Any())
                        {
                            return node;
                        }
                        continue;
                    }

                    bool hasImages = Directory.EnumerateFiles(node)
                                              .Any(f => FolderDiscoveryService.IsSupportedExtension(Path.GetExtension(f), allowedExtensions));
                    if (hasImages)
                    {
                        return node;
                    }
                }
                catch { }
            }

            return null;
        }

        private static string[] GetChildNodes(string path, IEnumerable<string>? allowedExtensions = null)
        {
            try
            {
                if (!Directory.Exists(path)) return Array.Empty<string>();
                return Directory.EnumerateFileSystemEntries(path)
                    .Where(e => Directory.Exists(e) || ArchiveManager.IsArchive(e, allowedExtensions))
                    .OrderBy(e => e, new NaturalStringComparer())
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private static string? GetNextNodeDFS(string current, IEnumerable<string>? allowedExtensions = null)
        {
            var children = GetChildNodes(current, allowedExtensions);
            if (children.Length > 0) return children[0];

            string node = current;
            while (true)
            {
                var parent = Directory.GetParent(node);
                if (parent == null) return null;

                try
                {
                    var siblings = GetChildNodes(parent.FullName, allowedExtensions);
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

        private static string? GetPrevNodeDFS(string current, IEnumerable<string>? allowedExtensions = null)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) return null;

            try
            {
                var siblings = GetChildNodes(parent.FullName, allowedExtensions);
                int idx = Array.FindIndex(siblings, d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase));
                if (idx > 0)
                {
                    string node = siblings[idx - 1];
                    while (true)
                    {
                        var children = GetChildNodes(node, allowedExtensions);
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
