using quick_image_viewer.Helpers;
using quick_image_viewer.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace quick_image_viewer.Services
{
    public class FolderDiscoveryService
    {
        public static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico",
            ".dng", ".nef", ".cr2", ".arw", ".tga", ".pcx"
        };

        public static bool IsSupportedExtension(string extension)
        {
            string ext = extension.ToLowerInvariant();
            // We also check archive extensions if ArchiveManager is available
            return SupportedExtensions.Contains(ext) || ArchiveManager.ArchiveExtensions.Contains(ext);
        }

        public static async Task DiscoverFilesAsync(
            string path,
            bool includeSiblings,
            bool includeSubfolders,
            Action<List<string>> onBatchLoaded,
            CancellationToken token)
        {
            try
            {
                List<string> targetDirs = new List<string>();

                if (includeSiblings)
                {
                    string cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string? parent = Path.GetDirectoryName(cleanPath);
                    if (parent != null)
                    {
                        try
                        {
                            if (Directory.Exists(parent))
                            {
                                foreach (var d in Directory.EnumerateDirectories(parent))
                                {
                                    if (token.IsCancellationRequested) return;
                                    if (d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) != cleanPath)
                                    {
                                        targetDirs.Add(d);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (includeSubfolders)
                {
                    // For the primary path, if we are doing recursive search, we might need to explore its subfolders.
                    // However, LoadDirectory already loaded the top-level files.
                    // LoadAdditionalFilesAsync logic in original code seemed to only add sibling directories' contents.
                    // Wait, if includeSubfolders is true, GetFilesFromDirectory(dir, true) is called for each sibling.
                    // What about subfolders of the ORIGINAL path?
                    // The original code:
                    // if (!ArchiveManager.IsArchive(path) && (includeSiblings || includeSubfolders))
                    // {
                    //     _ = Task.Run(() => LoadAdditionalFilesAsync(path, initialFile, includeSiblings, includeSubfolders, token));
                    // }
                    // LoadAdditionalFilesAsync:
                    // foreach (var dir in targetDirs) { GetFilesFromDirectory(dir, includeSubfolders) }
                    // It doesn't seem to recursively search the ORIGINAL path if siblings are not included?
                    // Actually, if includeSubfolders is true but includeSiblings is false, targetDirs is empty.

                    // Let's add the original path to targetDirs if includeSubfolders is true, 
                    // but we should avoid re-adding the files already found.
                    // Actually, the original GetFilesFromDirectory(path, false) was used for initial load.
                    // So if includeSubfolders is true, we should search subfolders of 'path'.

                    if (includeSubfolders)
                    {
                        // Add subfolders of the original path
                        try
                        {
                            if (Directory.Exists(path))
                            {
                                foreach (var d in Directory.EnumerateDirectories(path))
                                {
                                    if (token.IsCancellationRequested) return;
                                    targetDirs.Add(d);
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (targetDirs.Count == 0) return;

                foreach (var dir in targetDirs)
                {
                    if (token.IsCancellationRequested) return;

                    var folderFiles = await Task.Run(() => GetFilesFromDirectory(dir, includeSubfolders), token);
                    if (folderFiles.Count > 0)
                    {
                        onBatchLoaded?.Invoke(folderFiles);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FolderDiscoveryService Error: {ex.Message}");
            }
        }

        public static List<string> GetFilesFromDirectory(string dir, bool recursive)
        {
            List<string> files = new List<string>();
            try
            {
                if (!Directory.Exists(dir)) return files;

                var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var f in Directory.EnumerateFiles(dir, "*", options))
                {
                    if (IsSupportedExtension(Path.GetExtension(f)))
                    {
                        files.Add(f);
                    }
                }
            }
            catch { }
            return files;
        }

        public static List<string> GetInitialPlaylist(string path)
        {
            List<string> files;
            if (ArchiveManager.IsArchive(path))
            {
                files = ArchiveManager.GetArchiveImages(path);
            }
            else
            {
                files = GetFilesFromDirectory(path, false);
            }
            return files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();
        }
    }
}
