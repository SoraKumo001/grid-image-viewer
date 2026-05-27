using quick_image_viewer.Common;
using quick_image_viewer.Managers;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace quick_image_viewer.Helpers
{
    /// <summary>
    /// Manages SkiaSharp image data and animation state for a single page.
    /// </summary>
    public class PageRenderer : IDisposable
    {
        private readonly object _stateLock = new();
        private SKImage? _displayImage;
        private SKBitmap? _lastImageBitmap;

        public SKData? Data { get; internal set; }
        public SKCodec? Codec { get; internal set; }
        public SKBitmap? Bitmap { get; set; }
        public int CurrentFrame { get; set; } = -1;
        public int PriorFrame { get; set; } = -1;
        public string? CurrentFilePath { get; set; }
        public int StretchMode { get; set; } = 2; // 0: None, 2: Uniform, 3: UniformToFill
        public int FrameCount { get; internal set; } = 0;
        public int CurrentFrameDuration { get; internal set; } = 100;
        public bool UseHighQualityScaling { get; set; } = true;
        public bool EnableAnime4K { get; set; } = false;
        public double Anime4KStrength { get; set; } = 1.0;
        public bool EnablePanAnimation { get; set; } = false;
        public double PanAnimationSpeed { get; set; } = 1.0;

        // Slide Animation States
        private bool _panInitialized = false;
        private float _panStartX = 0.5f;
        private float _panStartY = 0.5f;
        private float _panEndX = 0.5f;
        private float _panEndY = 0.5f;
        private DateTime _panStartTime;
        private double _panDurationMs = 10000;
        private static readonly Random _panRand = new Random();

        public bool IsAnimated => Codec != null && FrameCount > 1;

        /// <summary>
        /// Loads a file for SkiaSharp and initializes codec and bitmap.
        /// Should be called from a background thread.
        /// </summary>
        public void LoadSkia(string filePath, CancellationToken token)
        {
            lock (_stateLock)
            {
                if (token.IsCancellationRequested) return;

                SKData? data = null;
                if (ArchiveManager.IsArchivePath(filePath))
                {
                    var (arc, entry) = ArchiveManager.SplitArchivePath(filePath);
                    byte[]? bytes = ArchiveManager.GetEntryBytes(arc, entry);
                    if (bytes != null) data = SKData.CreateCopy(bytes);
                }
                else
                {
                    data = SKData.Create(filePath);
                }

                if (data == null)
                {
                    var bmpBytes = ImageProcessor.DecodeToBmpBytes(filePath);
                    if (bmpBytes != null)
                    {
                        data = SKData.CreateCopy(bmpBytes);
                    }
                }

                if (data == null) return;
                if (token.IsCancellationRequested) { data.Dispose(); return; }

                var codec = SKCodec.Create(data);
                if (codec == null)
                {
                    data.Dispose();
                    return;
                }
                if (token.IsCancellationRequested) { codec.Dispose(); data.Dispose(); return; }

                var bitmap = new SKBitmap(codec.Info);
                int frameCount = codec.FrameCount;

                Data = data;
                Codec = codec;
                Bitmap = bitmap;
                FrameCount = frameCount;

                if (frameCount <= 1)
                {
                    var decoded = SKBitmap.Decode(codec);
                    if (decoded != null)
                    {
                        Bitmap = decoded;
                    }
                    CurrentFrame = 0;
                    PriorFrame = -1;
                }
                else
                {
                    // For animated images, decode the first frame.
                    var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, codec.Info.ColorType, codec.Info.AlphaType);
                    var options = new SKCodecOptions { FrameIndex = 0 };
                    codec.GetPixels(imageInfo, bitmap.GetPixels(), options);

                    CurrentFrame = 0;
                    PriorFrame = 0;
                    CurrentFrameDuration = codec.FrameInfo[0].Duration > 0 ? codec.FrameInfo[0].Duration : 100;
                }

                // Initialize drawing buffer image
                var targetBitmap = EditedBitmap ?? Bitmap;
                if (targetBitmap != null)
                {
                    _displayImage?.Dispose();
                    _displayImage = SKImage.FromBitmap(targetBitmap);
                    _lastImageBitmap = targetBitmap;
                }
            }
        }

        /// <summary>
        /// Advances the animation by one frame and returns the next frame's duration (ms).
        /// Decoding is done here to reduce Paint load and smooth out animations.
        /// </summary>
        private bool _isDecodingFrame = false;

        /// <summary>
        /// Advances the animation by one frame and returns the next frame's duration (ms).
        /// Decoding is done on a background thread to keep UI responsive.
        /// </summary>
        public int AdvanceFrame()
        {
            if (Codec == null || FrameCount <= 1 || Bitmap == null || _isDecodingFrame)
                return CurrentFrameDuration > 0 ? CurrentFrameDuration : 100;

            _isDecodingFrame = true;
            int nextFrame = (CurrentFrame + 1) % FrameCount;

            // We calculate the duration immediately to allow the timer to schedule the next tick.
            var frameInfo = Codec.FrameInfo[nextFrame];
            int duration = frameInfo.Duration > 0 ? frameInfo.Duration : 100;
            CurrentFrameDuration = duration;

            Task.Run(() =>
            {
                try
                {
                    SKImage? newImage = null;
                    lock (_stateLock)
                    {
                        if (Codec == null || Bitmap == null) return;

                        CurrentFrame = nextFrame;
                        var imageInfo = new SKImageInfo(Codec.Info.Width, Codec.Info.Height, Codec.Info.ColorType, Codec.Info.AlphaType);

                        if (PriorFrame == -1 || CurrentFrame == 0)
                        {
                            Bitmap.Erase(SKColors.Transparent);
                            PriorFrame = -1;
                        }

                        var options = new SKCodecOptions
                        {
                            FrameIndex = CurrentFrame,
                            PriorFrame = PriorFrame
                        };

                        Codec.GetPixels(imageInfo, Bitmap.GetPixels(), options);

                        PriorFrame = CurrentFrame;
                        newImage = SKImage.FromBitmap(Bitmap);
                    }

                    if (newImage != null)
                    {
                        SKImage? oldImage;
                        lock (_stateLock)
                        {
                            oldImage = _displayImage;
                            _displayImage = newImage;
                            _lastImageBitmap = Bitmap;
                        }
                        oldImage?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("PageRenderer", "AdvanceFrame decode failed", ex);
                }
                finally
                {
                    _isDecodingFrame = false;
                }
            });

            return duration;
        }

        /// <summary>
        /// Paints the bitmap onto the canvas.
        /// </summary>
        public void Paint(SKCanvas canvas, SKImageInfo info, int horizontalAlignment, int verticalAlignment = 1)
        {
            SKImage? imgToDraw = null;
            lock (_stateLock)
            {
                var expectedBitmap = EditedBitmap ?? Bitmap;
                if (expectedBitmap != null && (_displayImage == null || _lastImageBitmap != expectedBitmap))
                {
                    _displayImage?.Dispose();
                    _displayImage = SKImage.FromBitmap(expectedBitmap);
                    _lastImageBitmap = expectedBitmap;
                }
                imgToDraw = _displayImage;
            }

            if (imgToDraw != null)
            {
                float scale;
                if (StretchMode == 3) // UniformToFill
                    scale = Math.Max((float)info.Width / imgToDraw.Width, (float)info.Height / imgToDraw.Height);
                else if (StretchMode == 2) // Uniform
                    scale = Math.Min((float)info.Width / imgToDraw.Width, (float)info.Height / imgToDraw.Height);
                else // None (Original)
                    scale = 1.0f;

                float x = 0;
                float y = 0;

                if (StretchMode == 3 && EnablePanAnimation)
                {
                    float diffX = imgToDraw.Width * scale - info.Width;
                    float diffY = imgToDraw.Height * scale - info.Height;

                    bool canPanX = diffX > 0.5f;
                    bool canPanY = diffY > 0.5f;

                    if (canPanX || canPanY)
                    {
                        if (!_panInitialized)
                        {
                            _panStartX = (float)_panRand.NextDouble();
                            _panStartY = (float)_panRand.NextDouble();
                            _panEndX = (float)_panRand.NextDouble();
                            _panEndY = (float)_panRand.NextDouble();
                            _panStartTime = DateTime.Now;
                            _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / PanAnimationSpeed; // 8 to 15 seconds divided by speed multiplier
                            _panInitialized = true;
                        }

                        double elapsed = (DateTime.Now - _panStartTime).TotalMilliseconds;
                        double t = elapsed / _panDurationMs;

                        if (t >= 1.0)
                        {
                            _panStartX = _panEndX;
                            _panStartY = _panEndY;
                            _panEndX = (float)_panRand.NextDouble();
                            _panEndY = (float)_panRand.NextDouble();
                            _panStartTime = DateTime.Now;
                            _panDurationMs = (8000 + _panRand.NextDouble() * 7000) / PanAnimationSpeed;
                            t = 0.0;
                        }

                        // Linear interpolation to keep moving without pauses at the ends
                        double easedT = t;

                        float curX = _panStartX + (float)(easedT * (_panEndX - _panStartX));
                        float curY = _panStartY + (float)(easedT * (_panEndY - _panStartY));

                        x = canPanX ? -diffX * curX : -diffX * 0.5f;
                        y = canPanY ? -diffY * curY : -diffY * 0.5f;
                    }
                    else
                    {
                        x = (info.Width - imgToDraw.Width * scale) / 2;
                        y = (info.Height - imgToDraw.Height * scale) / 2;
                    }
                }
                else
                {
                    x = (info.Width - imgToDraw.Width * scale) / 2;
                    if (StretchMode != 3) // Not Cover
                    {
                        if (horizontalAlignment == 0) x = 0;
                        else if (horizontalAlignment == 2) x = info.Width - imgToDraw.Width * scale;
                    }

                    y = (info.Height - imgToDraw.Height * scale) / 2;
                    if (StretchMode != 3) // Not Cover
                    {
                        if (verticalAlignment == 0) y = 0;
                        else if (verticalAlignment == 2) y = info.Height - imgToDraw.Height * scale;
                    }
                }

                var destRect = new SKRect(x, y, x + imgToDraw.Width * scale, y + imgToDraw.Height * scale);

                var sampling = (scale != 1.0f && UseHighQualityScaling)
                    ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                    : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

                using var paint = new SKPaint();
                SKShader? effectShader = null;
                if (EnableAnime4K)
                {
                    effectShader = Anime4KEffect.CreateShader(imgToDraw, (float)Anime4KStrength);
                    if (effectShader != null)
                    {
                        paint.Shader = effectShader;
                    }
                }

                canvas.DrawImage(imgToDraw, destRect, sampling, paint);
                effectShader?.Dispose();
            }
        }

        private SKBitmap? _editedBitmap;
        public SKBitmap? EditedBitmap
        {
            get => _editedBitmap;
            set
            {
                lock (_stateLock)
                {
                    _editedBitmap = value;
                    _displayImage?.Dispose();
                    _displayImage = value != null ? SKImage.FromBitmap(value) : (Bitmap != null ? SKImage.FromBitmap(Bitmap) : null);
                    _lastImageBitmap = value ?? Bitmap;
                }
            }
        }

        public void Reset()
        {
            lock (_stateLock)
            {
                Codec?.Dispose(); Codec = null;
                Data?.Dispose(); Data = null;
                Bitmap?.Dispose(); Bitmap = null;
                _displayImage?.Dispose(); _displayImage = null;
                _lastImageBitmap = null;
                _editedBitmap = null;
                CurrentFilePath = null;
                FrameCount = 0;
                CurrentFrame = -1;
                PriorFrame = -1;
                _panInitialized = false;
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
