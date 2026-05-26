using quick_image_viewer.Common;
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
            ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".avif", ".avis", ".heic", ".heif", ".jxl", ".tif", ".tiff", ".svg", ".psd", ".ico",
            ".dng", ".nef", ".cr2", ".arw", ".tga", ".pcx"
        };

        public static bool IsSupportedExtension(string extension, IEnumerable<string>? allowedExtensions = null)
        {
            string ext = extension.ToLowerInvariant();
            bool isImageOrVideo = SupportedExtensions.Contains(ext);

            if (allowedExtensions != null)
            {
                return isImageOrVideo && allowedExtensions.Contains(ext);
            }
            return isImageOrVideo;
        }

        public static async Task DiscoverFilesAsync(
            string path,
            bool includeSiblings,
            bool includeSubfolders,
            Action<List<string>> onBatchLoaded,
            CancellationToken token,
            IEnumerable<string>? allowedExtensions = null)
        {
            try
            {
                List<string> targetDirs = new List<string>();

                if (includeSiblings)
                {
                    string cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string? parent = Path.GetDirectoryName(cleanPath);
                    if (parent != null && Directory.Exists(parent))
                    {
                        try
                        {
                            // フォルダ列挙
                            var dirEnum = Directory.EnumerateDirectories(parent).GetEnumerator();
                            while (true)
                            {
                                string? d = null;
                                try { if (!dirEnum.MoveNext()) break; d = dirEnum.Current; }
                                catch (UnauthorizedAccessException) { continue; }
                                catch (Exception ex)
                                {
                                    AppLog.Error("FolderDiscoveryService", "Sibling directory enumeration stopped", ex);
                                    break;
                                }

                                if (d != null && token.IsCancellationRequested) return;
                                if (d != null && d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) != cleanPath)
                                {
                                    targetDirs.Add(d);
                                }
                            }

                            // ファイル列挙
                            var siblingFiles = new List<string>();
                            var fileEnum = Directory.EnumerateFiles(parent).GetEnumerator();
                            while (true)
                            {
                                string? f = null;
                                try { if (!fileEnum.MoveNext()) break; f = fileEnum.Current; }
                                catch (UnauthorizedAccessException) { continue; }
                                catch (Exception ex)
                                {
                                    AppLog.Error("FolderDiscoveryService", "Sibling file enumeration stopped", ex);
                                    break;
                                }

                                if (f != null && token.IsCancellationRequested) return;
                                if (f != null && f != cleanPath && IsSupportedExtension(Path.GetExtension(f), allowedExtensions))
                                {
                                    if (f.ToLower().EndsWith(".pdf"))
                                    {
                                        int count = PdfManager.GetPageCountAsync(f).GetAwaiter().GetResult();
                                        for (int i = 0; i < count; i++)
                                            siblingFiles.Add(PdfManager.CreateVirtualPath(f, i));
                                    }
                                    else
                                    {
                                        siblingFiles.Add(f);
                                    }
                                }
                            }
                            if (siblingFiles.Count > 0) onBatchLoaded?.Invoke(siblingFiles);
                        }
                        catch (Exception ex)
                        {
                            AppLog.Error("FolderDiscoveryService", "Sibling discovery failed", ex);
                        }
                    }
                }

                if (includeSubfolders)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            var dirEnum = Directory.EnumerateDirectories(path).GetEnumerator();
                            while (true)
                            {
                                string? d = null;
                                try { if (!dirEnum.MoveNext()) break; d = dirEnum.Current; }
                                catch (UnauthorizedAccessException) { continue; }
                                catch (Exception ex)
                                {
                                    AppLog.Error("FolderDiscoveryService", "Subfolder enumeration stopped", ex);
                                    break;
                                }

                                if (d != null && token.IsCancellationRequested) return;
                                if (d != null) targetDirs.Add(d);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("FolderDiscoveryService", "Subfolder discovery failed", ex);
                    }
                }

                if (targetDirs.Count == 0) return;

                foreach (var dir in targetDirs)
                {
                    if (token.IsCancellationRequested) return;

                    var folderFiles = await Task.Run(() => GetFilesFromDirectory(dir, includeSubfolders, allowedExtensions), token);
                    if (folderFiles.Count > 0)
                    {
                        onBatchLoaded?.Invoke(folderFiles);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLog.Error("FolderDiscoveryService", "DiscoverFilesAsync failed", ex);
            }
        }

        public static List<string> GetFilesFromDirectory(string dir, bool recursive, IEnumerable<string>? allowedExtensions = null)
        {
            List<string> files = new List<string>();
            try
            {
                if (!Directory.Exists(dir)) return files;
                EnumerateFilesSafe(dir, recursive, files, allowedExtensions);
            }
            catch (Exception ex)
            {
                AppLog.Error("FolderDiscoveryService", "GetFilesFromDirectory failed", ex);
            }
            return files;
        }

        private static void EnumerateFilesSafe(string dir, bool recursive, List<string> files, IEnumerable<string>? allowedExtensions)
        {
            try
            {
                var fileEnum = Directory.EnumerateFiles(dir).GetEnumerator();
                while (true)
                {
                    string? f = null;
                    try { if (!fileEnum.MoveNext()) break; f = fileEnum.Current; }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (Exception ex)
                    {
                        AppLog.Error("FolderDiscoveryService", "File enumeration stopped", ex);
                        break;
                    }

                    if (f != null && IsSupportedExtension(Path.GetExtension(f), allowedExtensions))
                    {
                        if (f.ToLower().EndsWith(".pdf"))
                        {
                            int count = PdfManager.GetPageCountAsync(f).GetAwaiter().GetResult();
                            for (int i = 0; i < count; i++)
                                files.Add(PdfManager.CreateVirtualPath(f, i));
                        }
                        else
                        {
                            files.Add(f);
                        }
                    }
                }

                if (recursive)
                {
                    var dirEnum = Directory.EnumerateDirectories(dir).GetEnumerator();
                    while (true)
                    {
                        string? d = null;
                        try { if (!dirEnum.MoveNext()) break; d = dirEnum.Current; }
                        catch (UnauthorizedAccessException) { continue; }
                        catch (Exception ex)
                        {
                            AppLog.Error("FolderDiscoveryService", "Directory enumeration stopped", ex);
                            break;
                        }

                        if (d != null) EnumerateFilesSafe(d, true, files, allowedExtensions);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("FolderDiscoveryService", "EnumerateFilesSafe failed", ex);
            }
        }

        public static List<string> GetInitialPlaylist(string path, IEnumerable<string>? allowedExtensions = null)
        {
            List<string> files;
            if (path.ToLower().EndsWith(".pdf"))
            {
                files = new List<string>();
                int count = PdfManager.GetPageCountAsync(path).GetAwaiter().GetResult();
                for (int i = 0; i < count; i++)
                    files.Add(PdfManager.CreateVirtualPath(path, i));
            }
            else if (ArchiveManager.IsArchive(path, allowedExtensions))
            {
                files = ArchiveManager.GetArchiveImages(path, allowedExtensions);
            }
            else
            {
                files = GetFilesFromDirectory(path, false, allowedExtensions);
            }
            return files.Distinct().OrderBy(f => f, new NaturalStringComparer()).ToList();
        }
    }
}


