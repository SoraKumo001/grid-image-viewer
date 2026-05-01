using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;

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

        /// <summary>
        /// 現在のフレームを WriteableBitmap に描画して Thumbnail を更新する
        /// </summary>
        public void AdvanceFrame(int decodeWidth = 200)
        {
            if (Codec == null || FrameCount <= 1) return;

            CurrentFrame = (CurrentFrame + 1) % FrameCount;

            try
            {
                var info = Codec.Info;
                var imageInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                using var bitmap = new SKBitmap(imageInfo);
                var options = new SKCodecOptions { FrameIndex = CurrentFrame };
                Codec.GetPixels(imageInfo, bitmap.GetPixels(), options);

                // デコードサイズを制限してリサイズ
                float scale = Math.Min((float)decodeWidth / info.Width, (float)decodeWidth / info.Height);
                scale = Math.Min(scale, 1.0f); // 元サイズより大きくしない
                int w = (int)(info.Width * scale);
                int h = (int)(info.Height * scale);

                using var resized = bitmap.Resize(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear));
                if (resized == null) return;

                var wb = new WriteableBitmap(w, h);
                using (var stream = wb.PixelBuffer.AsStream())
                {
                    var pixels = resized.GetPixelSpan();
                    stream.Write(pixels.ToArray(), 0, pixels.Length);
                }
                wb.Invalidate();
                Thumbnail = wb;
            }
            catch { }
        }

        public void DisposeCodec()
        {
            Codec?.Dispose();
            Codec = null;
            CodecData?.Dispose();
            CodecData = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
