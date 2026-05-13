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

                // Get the next candidate node in the tree
                node = offset == 1 ? GetNextNodeDFS(node, allowedExtensions) : GetPrevNodeDFS(node, allowedExtensions);
                if (string.IsNullOrEmpty(node)) break;

                try
                {
                    // Check if the node is an archive or a folder containing images
                    if (ArchiveManager.IsArchive(node, allowedExtensions) || PdfManager.IsPdfPath(node))
                    {
                        bool hasContent = false;
                        if (ArchiveManager.IsArchive(node, allowedExtensions))
                            hasContent = ArchiveManager.HasArchiveImages(node, allowedExtensions);
                        else
                            hasContent = PdfManager.GetPageCountAsync(node).GetAwaiter().GetResult() > 0;

                        if (hasContent) return node;
                        continue;
                    }

                    if (Directory.Exists(node))
                    {
                        bool hasImages = Directory.EnumerateFiles(node)
                                                  .Any(f => FolderDiscoveryService.IsSupportedExtension(Path.GetExtension(f), allowedExtensions));
                        if (hasImages)
                        {
                            return node;
                        }
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
                    .Where(e => Directory.Exists(e) || ArchiveManager.IsArchive(e, allowedExtensions) || PdfManager.IsPdfPath(e))
                    .OrderBy(e => e, new NaturalStringComparer())
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private static string? GetNextNodeDFS(string current, IEnumerable<string>? allowedExtensions = null)
        {
            // 1. Dive into the first child if exists
            var children = GetChildNodes(current, allowedExtensions);
            if (children.Length > 0) return children[0];

            // 2. Otherwise, find the next sibling. If none, go up to parent and find its next sibling.
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
            string? parentPath = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parentPath)) return null;

            try
            {
                var siblings = GetChildNodes(parentPath, allowedExtensions);
                int idx = Array.FindIndex(siblings, d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase));

                if (idx > 0)
                {
                    // 1. Move to the previous sibling
                    string node = siblings[idx - 1];
                    // 2. Dive into its deepest last child
                    while (true)
                    {
                        var children = GetChildNodes(node, allowedExtensions);
                        if (children.Length == 0) return node;
                        node = children[children.Length - 1];
                    }
                }
                else
                {
                    // 3. No previous sibling, return the parent itself
                    return parentPath;
                }
            }
            catch { return parentPath; }
        }
    }
}
