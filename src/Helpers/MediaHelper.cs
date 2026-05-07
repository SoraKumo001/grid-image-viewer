using System;
using System.IO;
using System.Linq;

namespace quick_image_viewer.Helpers
{
    public static class MediaHelper
    {
        public static readonly string[] VideoExtensions = { ".webm", ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv" };

        public static bool IsVideo(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return VideoExtensions.Contains(ext);
        }
    }
}
