using ImageMagick;
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

        private static byte[]? ReadAllBytes(string path)
        {
            try
            {
                if (ArchiveManager.IsArchivePath(path))
                {
                    var (arc, ent) = ArchiveManager.SplitArchivePath(path);
                    return ArchiveManager.GetEntryBytes(arc, ent);
                }
                return File.ReadAllBytes(path);
            }
            catch { return null; }
        }

        private static SKBitmap? LoadBitmap(string path)
        {
            byte[]? bytes = ReadAllBytes(path);
            if (bytes == null) return null;
            return SKBitmap.Decode(bytes);
        }

        public static byte[]? DecodeToBmpBytes(string filePath)
        {
            try
            {
                byte[]? bytes = ReadAllBytes(filePath);
                if (bytes == null) return null;

                using var image = new MagickImage(bytes);
                image.Format = MagickFormat.Bmp;
                return image.ToByteArray();
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// 画像を指定フォーマットで保存する。
        /// </summary>
        public static void SaveImage(string sourcePath, string destPath, string targetExtension, int quality = 100)
        {
            byte[]? fileBytes = null;
            if (ArchiveManager.IsArchivePath(sourcePath))
            {
                var (arc, ent) = ArchiveManager.SplitArchivePath(sourcePath);
                fileBytes = ArchiveManager.GetEntryBytes(arc, ent);
            }
            else
            {
                fileBytes = File.ReadAllBytes(sourcePath);
            }

            if (fileBytes == null) return;

            using var data = SKData.CreateCopy(fileBytes);
            using var codec = SKCodec.Create(data);
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var skData = image.Encode(GetSKEncodedImageFormat(targetExtension), quality);
                using var stream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                skData.SaveTo(stream);
            }
        }

        /// <summary>
        /// 画像をリサイズして上書き保存する。
        /// </summary>
        public static SKBitmap? GetResizedBitmap(string sourcePath, int newWidth, int newHeight, SKBitmap? currentBmp)
        {
            SKBitmap? bitmap = currentBmp;
            bool disposeBitmap = false;

            if (bitmap == null)
            {
                bitmap = LoadBitmap(sourcePath);
                disposeBitmap = true;
            }

            if (bitmap != null)
            {
                var resizedBitmap = bitmap.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (disposeBitmap) bitmap.Dispose();
                return resizedBitmap;
            }
            return null;
        }

        public static SKBitmap? GetRotatedBitmap(string sourcePath, float degrees, SKBitmap? currentBmp)
        {
            SKBitmap? bitmap = currentBmp;
            bool disposeBitmap = false;

            if (bitmap == null)
            {
                bitmap = LoadBitmap(sourcePath);
                disposeBitmap = true;
            }

            if (bitmap != null)
            {
                var rotatedBitmap = new SKBitmap(Math.Abs(degrees) == 90 || Math.Abs(degrees) == 270 ? bitmap.Height : bitmap.Width, Math.Abs(degrees) == 90 || Math.Abs(degrees) == 270 ? bitmap.Width : bitmap.Height);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Clear();
                    canvas.Translate(rotatedBitmap.Width / 2f, rotatedBitmap.Height / 2f);
                    canvas.RotateDegrees(degrees);
                    canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                if (disposeBitmap) bitmap.Dispose();
                return rotatedBitmap;
            }
            return null;
        }

        public static SKBitmap? ApplyToneAdjustment(string path, float brightness, float contrast, float saturation, SKBitmap? currentBitmap = null)
        {
            try
            {
                SKBitmap? source = currentBitmap;
                bool disposeSource = false;

                if (source == null)
                {
                    source = LoadBitmap(path);
                    disposeSource = true;
                }

                if (source == null) return null;

                var result = new SKBitmap(source.Width, source.Height);
                using (var canvas = new SKCanvas(result))
                {
                    canvas.Clear(SKColors.Transparent);

                    // 1. Brightness/Contrast Matrix
                    float bNormalized = brightness / 255f;
                    float offsetNormalized = 0.5f * (1f - contrast) + bNormalized;
                    var bcMatrix = new float[]
                    {
                        contrast, 0, 0, 0, offsetNormalized,
                        0, contrast, 0, 0, offsetNormalized,
                        0, 0, contrast, 0, offsetNormalized,
                        0, 0, 0, 1, 0
                    };

                    // 2. Saturation Matrix (Luminance weights: R=0.2126, G=0.7152, B=0.0722)
                    float s = saturation;
                    float invS = 1.0f - s;
                    float r = 0.2126f * invS;
                    float g = 0.7152f * invS;
                    float b = 0.0722f * invS;

                    var satMatrix = new float[]
                    {
                        r + s, g,     b,     0, 0,
                        r,     g + s, b,     0, 0,
                        r,     g,     b + s, 0, 0,
                        0,     0,     0,     1, 0
                    };

                    using (var bcFilter = SKColorFilter.CreateColorMatrix(bcMatrix))
                    using (var satFilter = SKColorFilter.CreateColorMatrix(satMatrix))
                    using (var combinedFilter = SKColorFilter.CreateCompose(bcFilter, satFilter))
                    using (var paint = new SKPaint { ColorFilter = combinedFilter })
                    {
                        canvas.DrawBitmap(source, 0, 0, paint);
                    }
                }

                if (disposeSource) source.Dispose();
                return result;
            }
            catch { return null; }
        }

        public static SKBitmap? ApplyFilter(string path, string filterType, SKBitmap? currentBitmap = null)
        {
            try
            {
                SKBitmap? source = currentBitmap;
                bool disposeSource = false;

                if (source == null)
                {
                    source = LoadBitmap(path);
                    disposeSource = true;
                }

                if (source == null) return null;

                var result = new SKBitmap(source.Width, source.Height);
                using (var canvas = new SKCanvas(result))
                {
                    canvas.Clear(SKColors.Transparent);
                    using (var paint = new SKPaint())
                    {
                        float[]? matrix = null;
                        if (filterType == "Grayscale")
                        {
                            matrix = new float[]
                            {
                                0.2126f, 0.7152f, 0.0722f, 0, 0,
                                0.2126f, 0.7152f, 0.0722f, 0, 0,
                                0.2126f, 0.7152f, 0.0722f, 0, 0,
                                0, 0, 0, 1, 0
                            };
                        }
                        else if (filterType == "Sepia")
                        {
                            matrix = new float[]
                            {
                                0.393f, 0.769f, 0.189f, 0, 0,
                                0.349f, 0.686f, 0.168f, 0, 0,
                                0.272f, 0.534f, 0.131f, 0, 0,
                                0, 0, 0, 1, 0
                            };
                        }
                        else if (filterType == "Invert")
                        {
                            matrix = new float[]
                            {
                                -1, 0, 0, 0, 1,
                                0, -1, 0, 0, 1,
                                0, 0, -1, 0, 1,
                                0, 0, 0, 1, 0
                            };
                        }

                        if (matrix != null)
                        {
                            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        }
                        canvas.DrawBitmap(source, 0, 0, paint);
                    }
                }

                if (disposeSource) source.Dispose();
                return result;
            }
            catch { return null; }
        }

        public static SKBitmap? GetFlippedBitmap(string sourcePath, bool horizontal, SKBitmap? currentBmp)
        {
            SKBitmap? bitmap = currentBmp;
            bool disposeBitmap = false;

            if (bitmap == null)
            {
                bitmap = LoadBitmap(sourcePath);
                disposeBitmap = true;
            }

            if (bitmap != null)
            {
                var flippedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(flippedBitmap))
                {
                    canvas.Clear();
                    canvas.Scale(horizontal ? -1 : 1, horizontal ? 1 : -1, horizontal ? bitmap.Width / 2f : 0, horizontal ? 0 : bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                if (disposeBitmap) bitmap.Dispose();
                return flippedBitmap;
            }
            return null;
        }

        public static SKBitmap? GetCroppedBitmap(string sourcePath, SKRectI cropRect, SKBitmap? currentBmp)
        {
            SKBitmap? bitmap = currentBmp;
            bool disposeBitmap = false;

            if (bitmap == null)
            {
                bitmap = LoadBitmap(sourcePath);
                disposeBitmap = true;
            }

            if (bitmap != null)
            {
                var croppedBitmap = new SKBitmap(new SKImageInfo(cropRect.Width, cropRect.Height, bitmap.ColorType, bitmap.AlphaType));
                using (var canvas = new SKCanvas(croppedBitmap))
                {
                    canvas.DrawBitmap(bitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                }
                if (disposeBitmap) bitmap.Dispose();
                return croppedBitmap;
            }
            return null;
        }

        public static void SaveBitmap(SKBitmap bitmap, string destPath, string targetExtension, int quality = 100)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var skData = image.Encode(GetSKEncodedImageFormat(targetExtension), quality);
            using var stream = File.Open(destPath, FileMode.Create, FileAccess.Write);
            skData.SaveTo(stream);
        }

        /// <summary>
        /// 画像の元のサイズを取得する。
        /// </summary>

        /// <summary>
        /// 画像の元のサイズを取得する。
        /// </summary>
        public static (int width, int height) GetImageSize(string sourcePath)
        {
            try
            {
                if (ArchiveManager.IsArchivePath(sourcePath))
                {
                    var (arc, ent) = ArchiveManager.SplitArchivePath(sourcePath);
                    byte[]? bytes = ArchiveManager.GetEntryBytes(arc, ent);
                    if (bytes == null) return (0, 0);
                    using var data = SKData.CreateCopy(bytes);
                    using var arcCodec = SKCodec.Create(data);
                    return arcCodec != null ? (arcCodec.Info.Width, arcCodec.Info.Height) : (0, 0);
                }

                using var stream = File.OpenRead(sourcePath);
                using var codec = SKCodec.Create(stream);
                if (codec != null)
                {
                    return (codec.Info.Width, codec.Info.Height);
                }
            }
            catch { }
            return (0, 0);
        }
    }
}
