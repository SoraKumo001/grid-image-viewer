using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public class ImageItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);

        // 画像のアスペクト比 (幅 / 高さ)。1.0 = 正方形、>1 = 横長、<1 = 縦長
        public double AspectRatio { get; set; } = 1.0;

        // アニメーション用
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

        private bool _isDecodingFrame = false;

        /// <summary>
        /// 現在のフレームを非同期で WriteableBitmap に描画して Thumbnail を更新する
        /// </summary>
        public async void AdvanceFrame(int decodeWidth, Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            if (Codec == null || FrameCount <= 1 || _isDecodingFrame) return;

            _isDecodingFrame = true;

            int previousFrame = _priorFrameIndex;
            CurrentFrame = (CurrentFrame + 1) % FrameCount;
            
            // ループして先頭に戻る場合は、ベースフレームなしで描画し直す
            if (CurrentFrame == 0) previousFrame = -1;

            try
            {
                var (pixelBytes, w, h) = await Task.Run(() =>
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

                        // デコードサイズを制限してリサイズ
                        float scale = Math.Min((float)decodeWidth / info.Width, (float)decodeWidth / info.Height);
                        scale = Math.Min(scale, 1.0f); // 元サイズより大きくしない
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
                        var wb = new WriteableBitmap(w, h);
                        using (var stream = wb.PixelBuffer.AsStream())
                        {
                            stream.Write(pixelBytes, 0, pixelBytes.Length);
                        }
                        wb.Invalidate();
                        Thumbnail = wb;
                    }
                    catch { }
                });
            }
            catch { }
            finally
            {
                _isDecodingFrame = false;
            }
        }

        public void DisposeCodec()
        {
            Codec?.Dispose();
            Codec = null;
            CodecData?.Dispose();
            CodecData = null;
            _animationBuffer?.Dispose();
            _animationBuffer = null;
            _priorFrameIndex = -1;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
