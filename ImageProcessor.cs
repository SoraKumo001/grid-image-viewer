using ImageMagick;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;

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

        public static byte[]? DecodeToBmpBytes(string filePath)
        {
            try
            {
                using var image = new MagickImage(filePath);
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
        public static SKBitmap? GetResizedBitmap(string sourcePath, int newWidth, int newHeight, SKBitmap? currentBmp)
        {
            SKBitmap? bitmap = currentBmp;
            bool disposeBitmap = false;

            if (bitmap == null)
            {
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                bitmap = SKBitmap.Decode(codec);
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
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                bitmap = SKBitmap.Decode(codec);
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

        public static SKBitmap ApplyToneAdjustment(string path, float brightness, float contrast, float gamma, SKBitmap currentBitmap = null)
        {
            try
            {
                SKBitmap source = currentBitmap;
                bool disposeSource = false;

                if (source == null)
                {
                    source = SKBitmap.Decode(path);
                    disposeSource = true;
                }

                if (source == null) return null;

                var result = new SKBitmap(source.Width, source.Height);
                using (var canvas = new SKCanvas(result))
                {
                    canvas.Clear(SKColors.Transparent);

                    // 1. Apply Brightness/Contrast via Color Matrix
                    float bNormalized = brightness / 255f;
                    float offsetNormalized = 0.5f * (1f - contrast) + bNormalized;

                    var matrix = new float[]
                    {
                        contrast, 0, 0, 0, offsetNormalized,
                        0, contrast, 0, 0, offsetNormalized,
                        0, 0, contrast, 0, offsetNormalized,
                        0, 0, 0, 1, 0
                    };

                    using (var matrixFilter = SKColorFilter.CreateColorMatrix(matrix))
                    using (var paint = new SKPaint { ColorFilter = matrixFilter })
                    {
                        // 2. Apply Gamma via Table Filter
                        // If gamma is 1.0, we can skip this part to optimize, 
                        // but for simplicity we'll create a table.
                        if (Math.Abs(gamma - 1.0f) > 0.001f)
                        {
                            byte[] gammaTable = new byte[256];
                            for (int i = 0; i < 256; i++)
                            {
                                float val = i / 255f;
                                float corrected = (float)Math.Pow(val, 1.0 / gamma);
                                gammaTable[i] = (byte)Math.Clamp(corrected * 255f, 0, 255);
                            }
                            using (var tableFilter = SKColorFilter.CreateTable(null, gammaTable, gammaTable, gammaTable))
                            {
                                // Chain filters: Matrix then Gamma
                                paint.ColorFilter = SKColorFilter.CreateCompose(tableFilter, matrixFilter);
                                canvas.DrawBitmap(source, 0, 0, paint);
                            }
                        }
                        else
                        {
                            canvas.DrawBitmap(source, 0, 0, paint);
                        }
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
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                bitmap = SKBitmap.Decode(codec);
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
                byte[] fileBytes = File.ReadAllBytes(sourcePath);
                using var data = SKData.CreateCopy(fileBytes);
                using var codec = SKCodec.Create(data);
                bitmap = SKBitmap.Decode(codec);
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

        public static void SaveBitmap(SKBitmap bitmap, string destPath, string targetExtension)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var skData = image.Encode(GetSKEncodedImageFormat(targetExtension), 100);
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
