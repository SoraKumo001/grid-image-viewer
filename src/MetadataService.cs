using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using System;
using System.IO;
using System.Linq;

namespace grid_image_viewer
{
    public class ImageMetadata
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileSize { get; set; } = "";
        public string Dimensions { get; set; } = "";
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? LensModel { get; set; }
        public string? ExposureTime { get; set; }
        public string? FNumber { get; set; }
        public string? Iso { get; set; }
        public string? FocalLength { get; set; }
        public string? DateTaken { get; set; }

        public bool HasExif => !string.IsNullOrEmpty(Model) || !string.IsNullOrEmpty(ExposureTime);
    }

    public static class MetadataService
    {
        public static ImageMetadata GetMetadata(string filePath)
        {
            var info = new ImageMetadata { FilePath = filePath };
            try
            {
                if (!File.Exists(filePath)) return info;

                var fileInfo = new FileInfo(filePath);
                info.FileName = fileInfo.Name;
                info.FileSize = FormatBytes(fileInfo.Length);

                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // 1. 基本的な解像度の取得 (ディレクトリによってタグが異なるため優先順位をつけて取得)
                int width = 0, height = 0;

                // JPEG / EXIF
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                if (subIfd != null)
                {
                    info.ExposureTime = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                    info.FNumber = subIfd.GetDescription(ExifDirectoryBase.TagFNumber);
                    info.Iso = subIfd.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
                    info.FocalLength = subIfd.GetDescription(ExifDirectoryBase.TagFocalLength);
                    info.DateTaken = subIfd.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                    info.LensModel = subIfd.GetDescription(ExifDirectoryBase.TagLensModel);

                    if (int.TryParse(subIfd.GetString(ExifDirectoryBase.TagExifImageWidth), out var w)) width = w;
                    if (int.TryParse(subIfd.GetString(ExifDirectoryBase.TagExifImageHeight), out var h)) height = h;
                }

                if (ifd0 != null)
                {
                    info.Make = ifd0.GetDescription(ExifDirectoryBase.TagMake);
                    info.Model = ifd0.GetDescription(ExifDirectoryBase.TagModel);

                    if (width == 0 && int.TryParse(ifd0.GetString(ExifDirectoryBase.TagImageWidth), out var w)) width = w;
                    if (height == 0 && int.TryParse(ifd0.GetString(ExifDirectoryBase.TagImageHeight), out var h)) height = h;
                }

                // PNG, BMP, etc (解像度がまだ取れていない場合)
                if (width == 0)
                {
                    var jpegDir = directories.OfType<JpegDirectory>().FirstOrDefault();
                    if (jpegDir != null)
                    {
                        width = jpegDir.GetInt32(JpegDirectory.TagImageWidth);
                        height = jpegDir.GetInt32(JpegDirectory.TagImageHeight);
                    }
                    else
                    {
                        var pngDir = directories.OfType<PngDirectory>().FirstOrDefault();
                        if (pngDir != null)
                        {
                            var wTag = pngDir.Tags.FirstOrDefault(t => t.Name == "Image Width");
                            var hTag = pngDir.Tags.FirstOrDefault(t => t.Name == "Image Height");
                            if (wTag != null) int.TryParse(wTag.Description, out width);
                            if (hTag != null) int.TryParse(hTag.Description, out height);
                        }
                    }
                }

                if (width > 0 && height > 0)
                {
                    info.Dimensions = $"{width} x {height}";
                }
            }
            catch (Exception)
            {
                // 解析エラー時は基本情報のみで返す
            }
            return info;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblSByte = bytes;
            while (dblSByte >= 1024 && i < suffix.Length - 1)
            {
                i++;
                dblSByte /= 1024;
            }
            return $"{dblSByte:0.##} {suffix[i]}";
        }
    }
}
