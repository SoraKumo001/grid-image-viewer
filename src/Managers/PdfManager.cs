using quick_image_viewer.Common;
using System;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace quick_image_viewer.Managers
{
    public static class PdfManager
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PdfDocument> _documentCache = new();

        public static async Task<int> GetPageCountAsync(string filePath)
        {
            try
            {
                var doc = await GetDocumentAsync(filePath);
                return (int)(doc?.PageCount ?? 0);
            }
            catch (Exception ex)
            {
                AppLog.Error("PdfManager", $"GetPageCountAsync failed for '{filePath}'", ex);
                return 0;
            }
        }

        public static async Task<PdfDocument?> GetDocumentAsync(string filePath)
        {
            if (_documentCache.TryGetValue(filePath, out var cached)) return cached;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var doc = await PdfDocument.LoadFromFileAsync(file);
                _documentCache.TryAdd(filePath, doc);
                return doc;
            }
            catch (Exception ex)
            {
                AppLog.Error("PdfManager", $"GetDocumentAsync failed for '{filePath}'", ex);
                return null;
            }
        }

        public static string CreateVirtualPath(string filePath, int pageIndex)
        {
            return $"{filePath}[page={pageIndex}]";
        }

        public static (string filePath, int pageIndex) SplitVirtualPath(string virtualPath)
        {
            int start = virtualPath.LastIndexOf("[page=");
            if (start == -1) return (virtualPath, 0);

            string filePath = virtualPath.Substring(0, start);
            string pagePart = virtualPath.Substring(start + 6).TrimEnd(']');
            int.TryParse(pagePart, out int pageIndex);

            return (filePath, pageIndex);
        }

        public static bool IsPdfPath(string path)
        {
            return path.ToLower().EndsWith(".pdf") || path.Contains("[page=");
        }

        public static bool IsVirtualPath(string path)
        {
            return path.Contains("[page=");
        }

        public static async Task<IRandomAccessStream?> RenderPageToStreamAsync(string filePath, int pageIndex)
        {
            try
            {
                var doc = await GetDocumentAsync(filePath);
                if (doc == null || pageIndex >= doc.PageCount) return null;

                using var page = doc.GetPage((uint)pageIndex);
                var stream = new InMemoryRandomAccessStream();

                var options = new PdfPageRenderOptions();
                // 変換品質向上のため、少し大きめにレンダリング（必要に応じて調整）
                // デフォルトのDPI設定で十分な場合は指定不要

                await page.RenderToStreamAsync(stream, options);
                stream.Seek(0);
                return stream;
            }
            catch (Exception ex)
            {
                AppLog.Error("PdfManager", $"RenderPageToStreamAsync failed for '{filePath}' page {pageIndex}", ex);
                return null;
            }
        }

        public static void ClearCache()
        {
            _documentCache.Clear();
        }
    }
}
