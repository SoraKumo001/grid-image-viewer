using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace grid_image_viewer
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
            new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" }, 
            StringComparer.OrdinalIgnoreCase);

        public static string? FindNextImageFolder(string currentPath, int offset)
        {
            string? node = currentPath;

            int maxIterations = 1000;
            for (int i = 0; i < maxIterations; i++)
            {
                node = offset == 1 ? GetNextNodeDFS(node) : GetPrevNodeDFS(node);
                if (string.IsNullOrEmpty(node)) break;

                try
                {
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

        private static string? GetNextNodeDFS(string current)
        {
            try 
            {
                var dirs = Directory.GetDirectories(current).OrderBy(d => d, new NaturalStringComparer()).ToArray();
                if (dirs.Length > 0) return dirs[0];
            } catch {}

            string node = current;
            while(true)
            {
                var parent = Directory.GetParent(node);
                if (parent == null) return null;

                try 
                {
                    var siblings = parent.GetDirectories().Select(d => d.FullName).OrderBy(d => d, new NaturalStringComparer()).ToList();
                    int idx = siblings.FindIndex(d => string.Equals(d, node, StringComparison.OrdinalIgnoreCase));
                    if (idx != -1 && idx + 1 < siblings.Count)
                    {
                        return siblings[idx + 1];
                    }
                } catch {}
                
                node = parent.FullName;
            }
        }

        private static string? GetPrevNodeDFS(string current)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) return null;

            try 
            {
                var siblings = parent.GetDirectories().Select(d => d.FullName).OrderBy(d => d, new NaturalStringComparer()).ToList();
                int idx = siblings.FindIndex(d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase));
                if (idx > 0)
                {
                    string node = siblings[idx - 1];
                    while(true)
                    {
                        try 
                        {
                            var children = Directory.GetDirectories(node).OrderBy(d => d, new NaturalStringComparer()).ToArray();
                            if (children.Length == 0) return node;
                            node = children[children.Length - 1];
                        } catch {
                            return node;
                        }
                    }
                }
                else if (idx == 0)
                {
                    return parent.FullName;
                }
            } catch {}
            
            return parent.FullName;
        }
    }
}
