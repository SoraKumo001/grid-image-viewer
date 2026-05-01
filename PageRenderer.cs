using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    /// <summary>
    /// 1ページ分のSkiaSharp画像・アニメーション状態を管理するクラス。
    /// MainWindowは右ページ用・左ページ用に2つインスタンスを持つ。
    /// </summary>
    public class PageRenderer : IDisposable
    {
        public SKData? Data { get; private set; }
        public SKCodec? Codec { get; private set; }
        public SKBitmap? Bitmap { get; set; }
        public int CurrentFrame { get; set; } = -1;
        public int PriorFrame { get; set; } = -1;
        public int FrameCount { get; private set; } = 0;

        public bool IsAnimated => Codec != null && FrameCount > 1;

        /// <summary>
        /// SkiaSharp用のファイルを読み込み、コーデックとビットマップを初期化する。
        /// バックグラウンドスレッドから呼ぶこと。
        /// </summary>
        public void LoadSkia(string filePath, CancellationToken token)
        {
            var bytes = File.ReadAllBytes(filePath);
            if (token.IsCancellationRequested) return;

            var data = SKData.CreateCopy(bytes);
            if (data == null) return;
            if (token.IsCancellationRequested) { data.Dispose(); return; }

            var codec = SKCodec.Create(data);
            if (codec == null) 
            { 
                data.Dispose(); 
                var bmpBytes = ImageProcessor.DecodeToBmpBytes(filePath);
                if (bmpBytes != null)
                {
                    data = SKData.CreateCopy(bmpBytes);
                    codec = SKCodec.Create(data);
                }
                if (codec == null) 
                {
                    data?.Dispose();
                    return;
                }
            }
            if (token.IsCancellationRequested) { codec.Dispose(); data.Dispose(); return; }

            var bitmap = new SKBitmap(codec.Info);
            int frameCount = codec.FrameCount;

            Data = data;
            Codec = codec;
            Bitmap = bitmap;
            FrameCount = frameCount;
            CurrentFrame = -1;
            PriorFrame = -1;

            if (frameCount <= 1)
            {
                var decoded = SKBitmap.Decode(codec);
                if (decoded != null)
                {
                    Bitmap = decoded;
                }
            }
        }

        /// <summary>
        /// アニメーションフレームを1つ進め、次のフレームのタイマー間隔(ms)を返す。
        /// </summary>
        public int AdvanceFrame()
        {
            if (Codec == null || FrameCount <= 1) return 100;

            CurrentFrame = (CurrentFrame + 1) % FrameCount;
            var frameInfo = Codec.FrameInfo[CurrentFrame];
            return frameInfo.Duration > 0 ? frameInfo.Duration : 100;
        }

        /// <summary>
        /// キャンバスにビットマップを描画する。
        /// </summary>
        /// <param name="canvas">描画対象のSKCanvas</param>
        /// <param name="info">キャンバスの描画情報</param>
        /// <param name="horizontalAlignment">0=Left, 1=Center, 2=Right</param>
        public void Paint(SKCanvas canvas, SKImageInfo info, int horizontalAlignment)
        {
            if (Codec != null && FrameCount > 1)
            {
                var imageInfo = new SKImageInfo(Codec.Info.Width, Codec.Info.Height, Codec.Info.ColorType, Codec.Info.AlphaType);
                if (Bitmap == null || Bitmap.Width != imageInfo.Width || Bitmap.Height != imageInfo.Height)
                {
                    Bitmap?.Dispose();
                    Bitmap = new SKBitmap(imageInfo);
                    PriorFrame = -1;
                }
                
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
            }

            if (Bitmap != null)
            {
                float scale = Math.Min((float)info.Width / Bitmap.Width, (float)info.Height / Bitmap.Height);
                float x = (info.Width - Bitmap.Width * scale) / 2;
                if (horizontalAlignment == 0) x = 0;
                else if (horizontalAlignment == 2) x = info.Width - Bitmap.Width * scale;
                float y = (info.Height - Bitmap.Height * scale) / 2;

                var destRect = new SKRect(x, y, x + Bitmap.Width * scale, y + Bitmap.Height * scale);
                canvas.DrawBitmap(Bitmap, destRect);
            }
        }

        /// <summary>
        /// リソースを解放する。
        /// </summary>
        public void Reset()
        {
            Codec?.Dispose(); Codec = null;
            Data?.Dispose(); Data = null;
            Bitmap?.Dispose(); Bitmap = null;
            FrameCount = 0;
            CurrentFrame = -1;
            PriorFrame = -1;
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
