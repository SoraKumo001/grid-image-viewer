using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace grid_image_viewer
{
    public static class ArchiveManager
    {
        public static readonly string[] ArchiveExtensions = { ".zip", ".cbz", ".rar", ".cbr", ".7z" };

        public static bool IsArchive(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ArchiveExtensions.Contains(ext);
        }

        public static bool IsArchivePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Contains("|");
        }

        public static (string archivePath, string entryName) SplitArchivePath(string path)
        {
            int idx = path.IndexOf('|');
            if (idx != -1)
            {
                return (path.Substring(0, idx), path.Substring(idx + 1));
            }
            return (path, string.Empty);
        }

        private static IArchive OpenArchive(string path)
        {
            // Use ReaderFactory to see if it works, or try ArchiveFactory again with full namespace
            return SharpCompress.Archives.ArchiveFactory.OpenArchive(path);
        }

        public static List<string> GetArchiveImages(string archivePath)
        {
            var images = new List<string>();
            try
            {
                using (var archive = OpenArchive(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry != null && !entry.IsDirectory && MainWindow.IsSupportedExtension(Path.GetExtension(entry.Key) ?? ""))
                        {
                            images.Add($"{archivePath}|{entry.Key}");
                        }
                    }
                }
            }
            catch { }
            // Sort by entry name (Natural Sort)
            return images.OrderBy(f => f, new NaturalStringComparer()).ToList();
        }

        public static byte[]? GetEntryBytes(string archivePath, string entryName)
        {
            try
            {
                using (var archive = OpenArchive(archivePath))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.Key == entryName);
                    if (entry == null) return null;

                    using (var stream = entry.OpenEntryStream())
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        public static Stream? GetEntryStream(string archivePath, string entryName)
        {
            try
            {
                byte[]? bytes = GetEntryBytes(archivePath, entryName);
                if (bytes != null) return new MemoryStream(bytes);
            }
            catch { }
            return null;
        }
    }
}
