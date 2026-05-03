using SkiaSharp;
using System;
using System.Threading;

namespace grid_image_viewer
{
    /// <summary>
    /// 1ページ分のSkiaSharp画像・アニメーション状態を管理するクラス。
    /// </summary>
    public class PageRenderer : IDisposable
    {
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

        public bool IsAnimated => Codec != null && FrameCount > 1;

        /// <summary>
        /// SkiaSharp用のファイルを読み込み、コーデックとビットマップを初期化する。
        /// バックグラウンドスレッドから呼ぶこと。
        /// </summary>
        public void LoadSkia(string filePath, CancellationToken token)
        {
            lock (this)
            {
                if (token.IsCancellationRequested) return;

                var data = SKData.Create(filePath);
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
                    // アニメーションの場合、最初のフレームをデコードしておく
                    var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, codec.Info.ColorType, codec.Info.AlphaType);
                    var options = new SKCodecOptions { FrameIndex = 0 };
                    codec.GetPixels(imageInfo, bitmap.GetPixels(), options);

                    CurrentFrame = 0;
                    PriorFrame = 0;
                    CurrentFrameDuration = codec.FrameInfo[0].Duration > 0 ? codec.FrameInfo[0].Duration : 100;
                }
            }
        }

        /// <summary>
        /// アニメーションフレームを1つ進め、次のフレームのタイマー間隔(ms)を返す。
        /// 実際のデコード処理もここで行うことでPaintの負荷を下げ、アニメーションを滑らかにする。
        /// </summary>
        public int AdvanceFrame()
        {
            lock (this)
            {
                if (Codec == null || FrameCount <= 1 || Bitmap == null) return 100;

                CurrentFrame = (CurrentFrame + 1) % FrameCount;
                var frameInfo = Codec.FrameInfo[CurrentFrame];

                var imageInfo = new SKImageInfo(Codec.Info.Width, Codec.Info.Height, Codec.Info.ColorType, Codec.Info.AlphaType);

                // フレーム0または前のフレーム情報がない場合はバッファをクリア
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
                CurrentFrameDuration = frameInfo.Duration > 0 ? frameInfo.Duration : 100;

                return CurrentFrameDuration;
            }
        }

        /// <summary>
        /// キャンバスにビットマップを描画する。
        /// </summary>
        public void Paint(SKCanvas canvas, SKImageInfo info, int horizontalAlignment, int verticalAlignment = 1)
        {
            lock (this)
            {
                var bmpToDraw = EditedBitmap ?? Bitmap;
                if (bmpToDraw != null)
                {
                    float scale;
                    if (StretchMode == 3) // UniformToFill
                        scale = Math.Max((float)info.Width / bmpToDraw.Width, (float)info.Height / bmpToDraw.Height);
                    else if (StretchMode == 2) // Uniform
                        scale = Math.Min((float)info.Width / bmpToDraw.Width, (float)info.Height / bmpToDraw.Height);
                    else // None (Original)
                        scale = 1.0f;

                    float x = (info.Width - bmpToDraw.Width * scale) / 2;
                    if (StretchMode != 3) // Not Cover
                    {
                        if (horizontalAlignment == 0) x = 0;
                        else if (horizontalAlignment == 2) x = info.Width - bmpToDraw.Width * scale;
                    }

                    float y = (info.Height - bmpToDraw.Height * scale) / 2;
                    if (StretchMode != 3) // Not Cover
                    {
                        if (verticalAlignment == 0) y = 0;
                        else if (verticalAlignment == 2) y = info.Height - bmpToDraw.Height * scale;
                    }

                    var destRect = new SKRect(x, y, x + bmpToDraw.Width * scale, y + bmpToDraw.Height * scale);

                    if (scale != 1.0f)
                    {
                        var sampling = UseHighQualityScaling
                            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

                        using (var image = SKImage.FromBitmap(bmpToDraw))
                        {
                            canvas.DrawImage(image, destRect, sampling, null);
                        }
                    }
                    else
                    {
                        canvas.DrawBitmap(bmpToDraw, destRect);
                    }
                }
            }
        }

        /// <summary>
        /// リソースを解放する。
        /// </summary>
        public SKBitmap? EditedBitmap { get; set; }

        public void Reset()
        {
            lock (this)
            {
                Codec?.Dispose(); Codec = null;
                Data?.Dispose(); Data = null;
                Bitmap?.Dispose(); Bitmap = null;
                EditedBitmap = null;
                CurrentFilePath = null;
                FrameCount = 0;
                CurrentFrame = -1;
                PriorFrame = -1;
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
