using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using quick_image_viewer.Common;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
namespace quick_image_viewer.Models
{
    public class ImageItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public bool IsVideo => quick_image_viewer.Helpers.MediaHelper.IsVideo(FilePath);

        private string _metadata = string.Empty;
        public string Metadata
        {
            get => _metadata;
            set
            {
                if (_metadata != value)
                {
                    _metadata = value;
                    OnPropertyChanged();
                }
            }
        }

        // Image aspect ratio (Width / Height). 1.0 = Square, >1 = Landscape, <1 = Portrait
        public double AspectRatio { get; set; } = 1.0;

        // For animation
        public SKCodec? Codec { get; set; }
        public SKData? CodecData { get; set; }
        public int FrameCount { get; set; } = 0;
        public int CurrentFrame { get; set; } = 0;
        public bool IsAnimated => FrameCount > 1;

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _retryCount = 0;
        public int RetryCount
        {
            get => _retryCount;
            set
            {
                if (_retryCount != value)
                {
                    _retryCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged();
            }
        }
        private SKBitmap? _animationBuffer;
        private int _priorFrameIndex = -1;
        private WriteableBitmap? _cachedWb;

        private bool _isDecodingFrame = false;

        /// <summary>
        /// Asynchronously draws the current frame to a WriteableBitmap and updates the Thumbnail.
        /// </summary>
        public async void AdvanceFrame(int decodeWidth, Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            if (Codec == null || FrameCount <= 1 || _isDecodingFrame) return;

            _isDecodingFrame = true;

            int previousFrame = _priorFrameIndex;
            CurrentFrame = (CurrentFrame + 1) % FrameCount;

            // If looping back to the start, redraw without a base frame.
            if (CurrentFrame == 0) previousFrame = -1;

            try
            {
                var (pixelBytes, w, h) = await Task.Run<(byte[]? pixelBytes, int w, int h)>(() =>
                {
                    lock (this)
                    {
                        if (Codec == null) return (null, 0, 0);

                        var info = Codec.Info;
                        if (_animationBuffer == null)
                        {
                            _animationBuffer = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                            previousFrame = -1;
                        }

                        if (previousFrame == -1)
                        {
                            _animationBuffer.Erase(SKColors.Transparent);
                        }

                        var options = new SKCodecOptions
                        {
                            FrameIndex = CurrentFrame,
                            PriorFrame = previousFrame
                        };

                        Codec.GetPixels(_animationBuffer.Info, _animationBuffer.GetPixels(), options);
                        _priorFrameIndex = CurrentFrame;

                        // Resize with limited decode size
                        float scale = Math.Min((float)decodeWidth / info.Width, (float)decodeWidth / info.Height);
                        scale = Math.Min(scale, 1.0f); // Do not exceed original size
                        int targetW = Math.Max(1, (int)(info.Width * scale));
                        int targetH = Math.Max(1, (int)(info.Height * scale));

                        using var resized = _animationBuffer.Resize(new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear));
                        if (resized == null) return (null, 0, 0);

                        return (resized.GetPixelSpan().ToArray(), targetW, targetH);
                    }
                });

                if (pixelBytes == null || dispatcher == null) return;

                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (_cachedWb == null || _cachedWb.PixelWidth != w || _cachedWb.PixelHeight != h)
                        {
                            _cachedWb = new WriteableBitmap(w, h);
                        }

                        using (var stream = _cachedWb.PixelBuffer.AsStream())
                        {
                            stream.Write(pixelBytes, 0, pixelBytes.Length);
                        }
                        _cachedWb.Invalidate();
                        Thumbnail = _cachedWb;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("ImageItem", "AdvanceFrame UI update failed", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                AppLog.Error("ImageItem", $"AdvanceFrame failed for '{FilePath}'", ex);
            }
            finally
            {
                _isDecodingFrame = false;
            }
        }

        public void DisposeCodec()
        {
            lock (this)
            {
                Codec?.Dispose();
                Codec = null;
                CodecData?.Dispose();
                CodecData = null;
                _animationBuffer?.Dispose();
                _animationBuffer = null;
                _cachedWb = null;
                _priorFrameIndex = -1;

                if (Thumbnail is SoftwareBitmapSource sbs)
                {
                    sbs.Dispose();
                }
                Thumbnail = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
