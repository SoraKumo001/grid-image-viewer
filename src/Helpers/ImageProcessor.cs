using FFmpegInteropX;
using ImageMagick;
using quick_image_viewer.Managers;
using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace quick_image_viewer.Helpers
{
    public static class ImageProcessor
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int width, int height)> _sizeCache = new();
        private static readonly SemaphoreSlim _videoThumbnailSemaphore = new(1, 1);

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
        /// Saves an image in the specified format.
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
        /// Resizes and saves an image, overwriting the destination.
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

        public static SKBitmap? LoadThumbnail(string path, int maxDim)
        {
            try
            {
                byte[]? bytes = ReadAllBytes(path);
                if (bytes == null) return null;

                using var data = SKData.CreateCopy(bytes);
                using var codec = SKCodec.Create(data);
                if (codec == null) return null;

                float scale = Math.Min((float)maxDim / codec.Info.Width, (float)maxDim / codec.Info.Height);
                scale = Math.Min(scale, 1.0f);

                int w = Math.Max(1, (int)(codec.Info.Width * scale));
                int h = Math.Max(1, (int)(codec.Info.Height * scale));

                // Get the supported dimensions for the scale
                var supportedDim = codec.GetScaledDimensions(scale);
                var info = new SKImageInfo(supportedDim.Width, supportedDim.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                var bitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, bitmap.GetPixels());
                if (result == SKCodecResult.Success)
                {
                    if (supportedDim.Width != w || supportedDim.Height != h)
                    {
                        var resized = bitmap.Resize(new SKImageInfo(w, h), SKSamplingOptions.Default);
                        bitmap.Dispose();
                        return resized;
                    }
                    return bitmap;
                }
                bitmap.Dispose();
            }
            catch { }
            return null;
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
        /// Retrieves the original size of the image.
        /// </summary>
        public static (int width, int height) GetImageSize(string sourcePath)
        {
            if (_sizeCache.TryGetValue(sourcePath, out var size)) return size;

            try
            {
                (int width, int height) result = (0, 0);
                if (ArchiveManager.IsArchivePath(sourcePath))
                {
                    var (arc, ent) = ArchiveManager.SplitArchivePath(sourcePath);
                    byte[]? bytes = ArchiveManager.GetEntryBytes(arc, ent);
                    if (bytes != null)
                    {
                        using var data = SKData.CreateCopy(bytes);
                        using var arcCodec = SKCodec.Create(data);
                        if (arcCodec != null) result = (arcCodec.Info.Width, arcCodec.Info.Height);
                    }
                }
                else if (PdfManager.IsPdfPath(sourcePath))
                {
                    var (actualPath, pageIndex) = PdfManager.SplitVirtualPath(sourcePath);
                    var doc = PdfManager.GetDocumentAsync(actualPath).GetAwaiter().GetResult();
                    if (doc != null && pageIndex < doc.PageCount)
                    {
                        using var page = doc.GetPage((uint)pageIndex);
                        // Convert points to roughly pixels (assuming 96 DPI if we don't know better)
                        result = ((int)page.Size.Width, (int)page.Size.Height);
                    }
                }
                else
                {
                    if (MediaHelper.IsVideo(sourcePath))
                    {
                        try
                        {
                            var resultTask = Task.Run(async () =>
                            {
                                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
                                var props = await file.Properties.GetVideoPropertiesAsync();
                                int w = (int)props.Width;
                                int h = (int)props.Height;
                                if (props.Orientation == Windows.Storage.FileProperties.VideoOrientation.Rotate90 || props.Orientation == Windows.Storage.FileProperties.VideoOrientation.Rotate270)
                                {
                                    return (h, w);
                                }
                                return (w, h);
                            });
                            var dim = resultTask.GetAwaiter().GetResult();
                            if (dim.Item1 > 0 && dim.Item2 > 0)
                            {
                                return dim;
                            }
                        }
                        catch { }
                        return (1920, 1080);
                    }

                    // Fallback to direct FileStream
                    try
                    {
                        using var stream = File.OpenRead(sourcePath);
                        using var codec = SKCodec.Create(stream);
                        if (codec != null) result = (codec.Info.Width, codec.Info.Height);
                    }
                    catch { }
                }

                if (result.width > 0)
                {
                    _sizeCache.TryAdd(sourcePath, result);
                }
                return result;
            }
            catch { }
            return (0, 0);
        }

        public static async Task<SoftwareBitmap?> ExtractVideoThumbnailAsync(string filePath, uint maxDim = 1280, CancellationToken token = default)
        {
            await _videoThumbnailSemaphore.WaitAsync(token);
            try
            {
                const int maxAttempts = 3;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    token.ThrowIfCancellationRequested();

                    var bitmap = await TryExtractVideoFrameWithFFmpegAsync(filePath, maxDim, token)
                        ?? await TryExtractVideoThumbnailAsync(filePath, maxDim, token);
                    if (bitmap != null)
                    {
                        return bitmap;
                    }

                    if (attempt < maxAttempts - 1)
                    {
                        await Task.Delay(150 * (attempt + 1), token);
                    }
                }
            }
            finally
            {
                _videoThumbnailSemaphore.Release();
            }

            return null;
        }

        private static async Task<SoftwareBitmap?> TryExtractVideoFrameWithFFmpegAsync(string filePath, uint maxDim, CancellationToken token)
        {
            try
            {
                FrameGrabber? frameGrabber = null;
                Stream? archiveStream = null;
                Windows.Storage.Streams.IRandomAccessStream? randomAccessStream = null;

                try
                {
                    if (ArchiveManager.IsArchivePath(filePath))
                    {
                        var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                        archiveStream = ArchiveManager.GetEntryStream(arc, entry);
                        if (archiveStream == null) return null;

                        randomAccessStream = archiveStream.AsRandomAccessStream();
                        frameGrabber = await FrameGrabber.CreateFromStreamAsync(randomAccessStream);
                    }
                    else
                    {
                        frameGrabber = await FrameGrabber.CreateFromFileAsync(filePath);
                    }

                    token.ThrowIfCancellationRequested();
                    if (frameGrabber == null) return null;

                    var (decodeWidth, decodeHeight) = GetVideoThumbnailDecodeSize(frameGrabber, maxDim);
                    frameGrabber.DecodePixelWidth = decodeWidth;
                    frameGrabber.DecodePixelHeight = decodeHeight;

                    var position = frameGrabber.Duration > TimeSpan.FromSeconds(1)
                        ? TimeSpan.FromSeconds(1)
                        : TimeSpan.Zero;

                    var frame = await frameGrabber.ExtractVideoFrameAsync(position, exactSeek: false);
                    token.ThrowIfCancellationRequested();
                    if (frame == null) return null;

                    try
                    {
                        using var encodedStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                        await frame.EncodeAsJpegAsync(encodedStream);
                        token.ThrowIfCancellationRequested();

                        encodedStream.Seek(0);
                        var decoder = await BitmapDecoder.CreateAsync(encodedStream);
                        token.ThrowIfCancellationRequested();
                        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    }
                    finally
                    {
                        (frame as IDisposable)?.Dispose();
                    }
                }
                finally
                {
                    (frameGrabber as IDisposable)?.Dispose();
                    randomAccessStream?.Dispose();
                    archiveStream?.Dispose();
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return null;
        }

        private static (int width, int height) GetVideoThumbnailDecodeSize(FrameGrabber frameGrabber, uint maxDim)
        {
            int maxSize = (int)Math.Clamp(maxDim, 1, 4096);

            try
            {
                var stream = frameGrabber.CurrentVideoStream;
                if (stream != null)
                {
                    double sourceW = stream.PixelWidth;
                    double sourceH = stream.PixelHeight;

                    if (sourceW > 0 && sourceH > 0)
                    {
                        double scale = Math.Min(maxSize / sourceW, maxSize / sourceH);
                        scale = Math.Min(scale, 1.0);

                        return (
                            Math.Max(1, (int)Math.Round(sourceW * scale)),
                            Math.Max(1, (int)Math.Round(sourceH * scale)));
                    }
                }
            }
            catch { }

            return (maxSize, maxSize);
        }

        private static async Task<SoftwareBitmap?> TryExtractVideoThumbnailAsync(string filePath, uint maxDim, CancellationToken token)
        {
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                token.ThrowIfCancellationRequested();

                using var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.VideosView, maxDim, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
                token.ThrowIfCancellationRequested();

                if (thumbnail != null && thumbnail.Size > 0)
                {
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(thumbnail);
                    token.ThrowIfCancellationRequested();
                    return await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                token.ThrowIfCancellationRequested();

                var clip = await Windows.Media.Editing.MediaClip.CreateFromFileAsync(file);
                var composition = new Windows.Media.Editing.MediaComposition();
                composition.Clips.Add(clip);

                var time = TimeSpan.Zero;
                if (clip.OriginalDuration.TotalSeconds > 1)
                {
                    time = TimeSpan.FromSeconds(1);
                }

                using var stream = await composition.GetThumbnailAsync(time, (int)maxDim, (int)maxDim, Windows.Media.Editing.VideoFramePrecision.NearestFrame);
                token.ThrowIfCancellationRequested();

                if (stream != null && stream.Size > 0)
                {
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                    token.ThrowIfCancellationRequested();
                    return await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return null;
        }
    }
}
