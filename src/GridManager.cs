using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace grid_image_viewer
{
    internal class GridManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        private DispatcherTimer? _gridAnimationTimer;
        private int _gridDecodeSize = 300;
        private CancellationTokenSource? _gridCts;

        public GridManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public async Task LoadThumbnailsAsync(CancellationToken token)
        {
            StopGridAnimation();
            foreach (var old in _window.GridItems) old.DisposeCodec();

            // グリッドセルサイズに基づいてデコード解像度を決定（DPIスケーリング考慮）
            int decodeSize = 300;
            if (_window.ImageGridView.ItemsPanelRoot is ItemsWrapGrid wg && wg.ItemWidth > 0)
            {
                decodeSize = (int)Math.Max(wg.ItemWidth, wg.ItemHeight) * 2; // Retina対応
            }
            else
            {
                // まだレイアウトされていない場合はウィンドウサイズから推定
                double maxDim = Math.Max(_window.ImageGridView.ActualWidth, _window.ImageGridView.ActualHeight);
                if (maxDim > 0)
                {
                    int count = _window.GridItems.Count;
                    int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
                    decodeSize = (int)(maxDim / cols) * 2;
                }
            }
            decodeSize = Math.Clamp(decodeSize, 300, 1200);
            _gridDecodeSize = decodeSize;

            var items = _window.GridItems.ToList();
            var semaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));
            var tasks = items.Select(item => Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;
                // Add a small initial delay to prioritize main image loading
                await Task.Delay(100, token);
                await semaphore.WaitAsync(token);
                try
                {
                    await LoadSingleThumbnailAsync(item, decodeSize, token);
                }
                finally
                {
                    semaphore.Release();
                }
            }, token)).ToArray();

            try
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // 全読み込み後にレイアウト再計算 & アニメーション開始
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateGridLayout();
                    StartGridAnimation();
                });
            }
            finally
            {
            }
        }

        private async Task LoadSingleThumbnailAsync(ImageItem item, int decodeSize, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                bool mightBeAnimated = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (mightBeAnimated)
                {
                    byte[]? bytes = null;
                    if (ArchiveManager.IsArchivePath(item.FilePath))
                    {
                        var (arc, ent) = ArchiveManager.SplitArchivePath(item.FilePath);
                        bytes = ArchiveManager.GetEntryBytes(arc, ent);
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                using (var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var ms = new MemoryStream((int)fs.Length))
                                {
                                    await fs.CopyToAsync(ms, token);
                                    bytes = ms.ToArray();
                                }
                                break;
                            }
                            catch (IOException)
                            {
                                await Task.Delay(100, token);
                            }
                        }
                    }

                    if (bytes == null || token.IsCancellationRequested) return;

                    var skData = SKData.CreateCopy(bytes);
                    var codec = SKCodec.Create(skData);

                    if (codec != null)
                    {
                        item.AspectRatio = (double)codec.Info.Width / codec.Info.Height;
                    }

                    if (codec != null && codec.FrameCount > 1)
                    {
                        if (token.IsCancellationRequested) { codec.Dispose(); skData.Dispose(); return; }

                        item.CodecData = skData;
                        item.Codec = codec;
                        item.FrameCount = codec.FrameCount;
                        item.CurrentFrame = 0;
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!token.IsCancellationRequested)
                                item.AdvanceFrame(decodeSize, _window.DispatcherQueue);
                        });
                        return;
                    }

                    codec?.Dispose();
                    if (token.IsCancellationRequested) { skData.Dispose(); return; }

                    using var skBitmap = SKBitmap.Decode(skData);
                    skData.Dispose();

                    ProcessDecodedBitmap(skBitmap, item, decodeSize, token);
                }
                else
                {
                    SKBitmap? decoded = null;
                    if (ArchiveManager.IsArchivePath(item.FilePath))
                    {
                        var (arc, ent) = ArchiveManager.SplitArchivePath(item.FilePath);
                        byte[]? bytes = ArchiveManager.GetEntryBytes(arc, ent);
                        if (bytes != null)
                        {
                            using var skData = SKData.CreateCopy(bytes);
                            using var codec = SKCodec.Create(skData);
                            if (codec != null)
                            {
                                item.AspectRatio = (double)codec.Info.Width / codec.Info.Height;
                            }
                            decoded = SKBitmap.Decode(skData);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                using (var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    using var codec = SKCodec.Create(fs);
                                    if (codec != null)
                                    {
                                        item.AspectRatio = (double)codec.Info.Width / codec.Info.Height;
                                    }
                                    fs.Position = 0;
                                    decoded = SKBitmap.Decode(fs);
                                }
                                break;
                            }
                            catch (IOException)
                            {
                                await Task.Delay(100, token);
                            }
                        }
                    }

                    if (decoded == null)
                    {
                        var bmpBytes = ImageProcessor.DecodeToBmpBytes(item.FilePath);
                        if (bmpBytes != null && !token.IsCancellationRequested)
                        {
                            using var skData = SKData.CreateCopy(bmpBytes);
                            decoded = SKBitmap.Decode(skData);
                        }
                    }

                    using var skBitmapToDispose = decoded;
                    if (token.IsCancellationRequested) return;

                    ProcessDecodedBitmap(decoded, item, decodeSize, token);
                }
            }
            catch { }
            finally
            {
                _window.DispatcherQueue.TryEnqueue(() => item.IsLoading = false);
            }
        }

        private void ProcessDecodedBitmap(SKBitmap? skBitmap, ImageItem item, int decodeSize, CancellationToken token)
        {
            if (skBitmap != null)
            {
                if (token.IsCancellationRequested) return;

                float scale = Math.Min((float)decodeSize / skBitmap.Width, (float)decodeSize / skBitmap.Height);
                scale = Math.Min(scale, 1.0f);
                int w = Math.Max(1, (int)(skBitmap.Width * scale));
                int h = Math.Max(1, (int)(skBitmap.Height * scale));

                var resizeInfo = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var resized = skBitmap.Resize(resizeInfo, new SKSamplingOptions(SKFilterMode.Linear));

                if (resized != null)
                {
                    if (token.IsCancellationRequested) return;

                    var pixelBytes = new byte[resized.ByteCount];
                    System.Runtime.InteropServices.Marshal.Copy(resized.GetPixels(), pixelBytes, 0, pixelBytes.Length);

                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            var wb = new WriteableBitmap(w, h);
                            using (var stream = wb.PixelBuffer.AsStream())
                            {
                                stream.Write(pixelBytes, 0, pixelBytes.Length);
                            }
                            wb.Invalidate();
                            item.Thumbnail = wb;
                        }
                        catch { }
                    });
                }
            }
            else
            {
                // Fallback to native BitmapImage
                _window.DispatcherQueue.TryEnqueue(async () =>
                {
                    if (token.IsCancellationRequested) return;
                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                        using var stream = await file.OpenReadAsync();
                        var bitmapImage = new BitmapImage();
                        bitmapImage.DecodePixelWidth = decodeSize;
                        await bitmapImage.SetSourceAsync(stream);
                        item.Thumbnail = bitmapImage;
                    }
                    catch { }
                });
            }
        }

        public void StartGridAnimation()
        {
            if (_gridAnimationTimer != null) return;
            // アニメーション画像が1つもなければタイマー不要
            if (!_window.GridItems.Any(i => i.IsAnimated)) return;

            _gridAnimationTimer = new DispatcherTimer();
            _gridAnimationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _gridAnimationTimer.Tick += GridAnimationTimer_Tick;
            _gridAnimationTimer.Start();
        }

        public void StopGridAnimation()
        {
            if (_gridAnimationTimer != null)
            {
                _gridAnimationTimer.Stop();
                _gridAnimationTimer.Tick -= GridAnimationTimer_Tick;
                _gridAnimationTimer = null;
            }
        }

        private void GridAnimationTimer_Tick(object? sender, object e)
        {
            if (!_window.IsGridMode) return;
            foreach (var item in _window.GridItems)
            {
                if (item.IsAnimated)
                {
                    // 表示範囲内（またはバッファ内）でコンテナが実体化されている場合のみアニメーションを進める
                    var container = _window.ImageGridView.ContainerFromItem(item);
                    if (container != null)
                    {
                        if (item.IsAnimated)
                        {
                            item.AdvanceFrame(_gridDecodeSize, _window.DispatcherQueue);
                        }
                        else if (item.Thumbnail == null && !item.IsLoading && item.RetryCount < 5)
                        {
                            // 読み込み失敗＆現在見えている要素の場合、リトライを実行
                            item.RetryCount++;
                            item.IsLoading = true;
                            _ = Task.Run(() => LoadSingleThumbnailAsync(item, _gridDecodeSize, _gridCts?.Token ?? CancellationToken.None));
                        }
                    }
                }
            }
        }

        public void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ImageItem item)
            {
                int idx = _window.Playlist.IndexOf(item.FilePath);
                if (idx >= 0)
                {
                    _window.CurrentIndex = idx;
                    _window.IsGridMode = false;
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public void ImageGridView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGridLayout();
        }

        public void UpdateGridLayout()
        {
            if (_window.ImageGridView.ItemsPanelRoot is not ItemsWrapGrid wrapGrid) return;

            int totalItems = _window.GridItems.Count;
            if (totalItems == 0) return;

            double W = _window.ImageGridView.ActualWidth - _window.ImageGridView.Padding.Left - _window.ImageGridView.Padding.Right - 24;
            double H = _window.ImageGridView.ActualHeight - _window.ImageGridView.Padding.Top - _window.ImageGridView.Padding.Bottom - 8;
            if (W <= 0 || H <= 0) return;

            // 最頻値のアスペクト比を算出（最も多い比率を基準にする）
            var ratios = _window.GridItems.Select(x => x.AspectRatio).Where(r => r > 0).ToList();
            double modeAspect = 1.0;
            if (ratios.Count > 0)
            {
                // 小数2桁で丸めてグループ化し、最も多い比率を採用
                modeAspect = ratios
                    .GroupBy(r => Math.Round(r, 2))
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Average();
            }

            // 最適な列数を探索: セル内に収まる画像面積が最大になる組み合わせを選ぶ
            double bestArea = 0;
            int bestCols = 1;

            for (int cols = 1; cols <= totalItems; cols++)
            {
                int rows = (int)Math.Ceiling((double)totalItems / cols);
                double cellW = W / cols;
                double cellH = H / rows;

                // セル内でアスペクト比を保持して収まる画像サイズを計算
                double imgW, imgH;
                if (modeAspect >= cellW / cellH)
                {
                    // 幅に合わせる
                    imgW = cellW;
                    imgH = cellW / modeAspect;
                }
                else
                {
                    // 高さに合わせる
                    imgH = cellH;
                    imgW = cellH * modeAspect;
                }

                double area = imgW * imgH;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestCols = cols;
                }
            }

            // フォールバック: セルが小さすぎる場合は従来のスクロールモード
            int bestRows = (int)Math.Ceiling((double)totalItems / bestCols);
            double finalCellW = Math.Floor(W / bestCols);
            double finalCellH = Math.Floor(H / bestRows);
            double minDim = Math.Min(finalCellW, finalCellH);

            if (minDim < 120)
            {
                // スクロールモード: 幅ベースで列数を決め、高さはアスペクト比に従う
                int cols = Math.Max(1, (int)(W / 150));
                double scrollW = Math.Floor(W / cols);
                double scrollH = Math.Floor(scrollW / modeAspect);
                wrapGrid.ItemWidth = scrollW;
                wrapGrid.ItemHeight = scrollH;
            }
            else
            {
                wrapGrid.ItemWidth = finalCellW;
                wrapGrid.ItemHeight = finalCellH;
            }
        }

        public void RefreshThumbnails()
        {
            _gridCts?.Cancel();
            _gridCts?.Dispose();
            _gridCts = new CancellationTokenSource();
            _ = LoadThumbnailsAsync(_gridCts.Token);
        }

        public void Dispose()
        {
            StopGridAnimation();
            _gridCts?.Cancel();
            _gridCts?.Dispose();
        }
    }
}
