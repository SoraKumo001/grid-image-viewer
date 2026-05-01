using SkiaSharp;
using System;
using System.IO;

namespace grid_image_viewer
{
    public static class ImageProcessor
    {
        public static SKEncodedImageFormat GetSKEncodedImageFormat(string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                ".bmp" => SKEncodedImageFormat.Bmp,
                _ => SKEncodedImageFormat.Png
            };
        }

        /// <summary>
        /// 画像を指定フォーマットで保存する。
        /// </summary>
        public static void SaveImage(string sourcePath, string destPath, string targetExtension)
        {
            byte[] fileBytes = File.ReadAllBytes(sourcePath);
            using var data = SKData.CreateCopy(fileBytes);
            using var codec = SKCodec.Create(data);
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var skData = image.Encode(GetSKEncodedImageFormat(targetExtension), 100);
                using var stream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                skData.SaveTo(stream);
            }
        }

        /// <summary>
        /// 画像をリサイズして上書き保存する。
        /// </summary>
        public static void ResizeImage(string sourcePath, int newWidth, int newHeight)
        {
            byte[] fileBytes = File.ReadAllBytes(sourcePath);
            using var data = SKData.CreateCopy(fileBytes);
            using var codec = SKCodec.Create(data);
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap != null)
            {
                using var resizedBitmap = bitmap.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
                using var image = SKImage.FromBitmap(resizedBitmap);
                using var skData = image.Encode(GetSKEncodedImageFormat(Path.GetExtension(sourcePath)), 100);
                using var stream = File.Open(sourcePath, FileMode.Create, FileAccess.Write);
                skData.SaveTo(stream);
            }
        }

        /// <summary>
        /// 画像をクロップして上書き保存する。
        /// </summary>
        public static void CropImage(string sourcePath, SKRectI cropRect)
        {
            byte[] fileBytes = File.ReadAllBytes(sourcePath);
            using var data = SKData.CreateCopy(fileBytes);
            using var codec = SKCodec.Create(data);
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap != null)
            {
                using var croppedBitmap = new SKBitmap(cropRect.Width, cropRect.Height);
                bitmap.ExtractSubset(croppedBitmap, cropRect);
                using var image = SKImage.FromBitmap(croppedBitmap);
                using var skData = image.Encode(GetSKEncodedImageFormat(Path.GetExtension(sourcePath)), 100);
                using var stream = File.Open(sourcePath, FileMode.Create, FileAccess.Write);
                skData.SaveTo(stream);
            }
        }

        /// <summary>
        /// 画像の元のサイズを取得する。
        /// </summary>
        public static (int width, int height) GetImageSize(string sourcePath)
        {
            byte[] fileBytes = File.ReadAllBytes(sourcePath);
            using var data = SKData.CreateCopy(fileBytes);
            using var codec = SKCodec.Create(data);
            if (codec != null)
            {
                return (codec.Info.Width, codec.Info.Height);
            }
            return (0, 0);
        }
    }
}
