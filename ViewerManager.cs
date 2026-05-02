using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Windows.ApplicationModel.Resources;

namespace grid_image_viewer
{
    internal class ViewerManager
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;

        private PageRenderer[] _pages = new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() };
        private Grid[] _pageGrids;
        private Microsoft.UI.Xaml.Controls.Image[] _pageImages;
        private SkiaSharp.Views.Windows.SKXamlCanvas[] _pageCanvases;
        private Microsoft.UI.Xaml.Controls.ProgressRing[] _pageLoadingRings;
        private DispatcherTimer _animationTimer;
        private DispatcherTimer _notificationTimer;
        private CancellationTokenSource? _displayCts;
        private ResourceLoader _resourceLoader = new ResourceLoader();

        public ViewerManager(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;

            _pageGrids = new Grid[] { _window.PageGrid1, _window.PageGrid2, _window.PageGrid3, _window.PageGrid4 };
            _pageImages = new Microsoft.UI.Xaml.Controls.Image[] { _window.Image1, _window.Image2, _window.Image3, _window.Image4 };
            _pageCanvases = new SkiaSharp.Views.Windows.SKXamlCanvas[] { _window.Canvas1, _window.Canvas2, _window.Canvas3, _window.Canvas4 };
            _pageLoadingRings = new Microsoft.UI.Xaml.Controls.ProgressRing[] { _window.LoadingRing1, _window.LoadingRing2, _window.LoadingRing3, _window.LoadingRing4 };

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(30);
            _animationTimer.Tick += AnimationTimer_Tick;

            _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notificationTimer.Tick += (s, e) =>
            {
                _notificationTimer.Stop();
                _window.NotificationOverlay.Visibility = Visibility.Collapsed;
            };
        }

        public PageRenderer[] Pages => _pages;
        public Microsoft.UI.Xaml.Controls.Image[] PageImages => _pageImages;

        public async Task UpdateDisplayAsync()
        {
            if (_window.Playlist.Count == 0 || _window.CurrentIndex < 0 || _window.CurrentIndex >= _window.Playlist.Count) return;

            if (_window.IsGridMode)
            {
                _window.ImageScrollViewer.Visibility = Visibility.Collapsed;
                _window.ImageGridView.Visibility = Visibility.Visible;
                StopAnimation();
                _window.GridManager.StartGridAnimation();
                
                _window.ImageGridView.SelectedIndex = _window.CurrentIndex;
                _window.ImageGridView.ScrollIntoView(_window.ImageGridView.SelectedItem);
                _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (_window.ImageGridView.SelectedItem != null)
                    {
                        var container = _window.ImageGridView.ContainerFromItem(_window.ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null)
                        {
                            container.Focus(FocusState.Programmatic);
                        }
                        else
                        {
                            _window.ImageGridView.Focus(FocusState.Programmatic);
                        }
                    }
                });
                return;
            }
            else
            {
                _window.ImageScrollViewer.Visibility = Visibility.Visible;
                _window.ImageGridView.Visibility = Visibility.Collapsed;
                _window.GridManager.StopGridAnimation();
                _window.RootGrid.Focus(FocusState.Programmatic);
            }

            StopAnimation();
            UpdateStretch();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            try
            {
                int splitCount = _settings.MangaSplitCount;
                int remaining = _window.Playlist.Count - _window.CurrentIndex;
                int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

                // Layout Configuration
                int currentQuadLayout = GetEffectiveQuadLayout();
                UpdateLayoutGrid(splitCount, effectiveSplitCount, currentQuadLayout);

                var loadTasks = new List<Task>();
                for (int i = 0; i < effectiveSplitCount; i++)
                {
                    int indexToLoad = -1;
                    if (i == 0) indexToLoad = _window.CurrentIndex;
                    else
                    {
                        if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowRandom)
                        {
                            if (_window.SlideshowManager.SlideshowRandomIndices[i] != -1) 
                                indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                            else if (_window.CurrentIndex + i < _window.Playlist.Count) 
                                indexToLoad = _window.CurrentIndex + i;
                        }
                        else if (_window.CurrentIndex + i < _window.Playlist.Count)
                        {
                            indexToLoad = _window.CurrentIndex + i;
                        }
                    }

                    if (indexToLoad != -1)
                    {
                        loadTasks.Add(LoadPageAsync(_window.Playlist[indexToLoad], _pageImages[i], _pageCanvases[i], _pageLoadingRings[i], i, token));
                    }
                }

                try
                {
                    await Task.WhenAll(loadTasks);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                
                for (int i = effectiveSplitCount; i < 4; i++) {
                     _pageImages[i].Source = null;
                     _pages[i].Reset();
                     _pageCanvases[i].Invalidate();
                     _pageLoadingRings[i].IsActive = false;
                }
            }
            finally
            {
            }

            if (_pages.Any(p => p.IsAnimated))
            {
                _animationTimer.Start();
            }
        }

        private void UpdateLayoutGrid(int splitCount, int effectiveSplitCount, int currentQuadLayout)
        {
            // Reset vertical alignment for all
            for (int i = 0; i < 4; i++) _pageImages[i].VerticalAlignment = VerticalAlignment.Center;

            if (effectiveSplitCount == 1)
            {
                _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                _window.Col1.Width = new GridLength(0); _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);
                
                Grid.SetColumn(_window.PageGrid1, 0); Grid.SetRow(_window.PageGrid1, 0);
                Grid.SetColumnSpan(_window.PageGrid1, 4); Grid.SetRowSpan(_window.PageGrid1, 2);
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Collapsed;
                _window.PageGrid3.Visibility = Visibility.Collapsed;
                _window.PageGrid4.Visibility = Visibility.Collapsed;
                _window.Image1.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if (effectiveSplitCount == 2)
            {
                _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                Grid.SetColumn(_window.PageGrid1, 1); Grid.SetRow(_window.PageGrid1, 0);
                Grid.SetColumnSpan(_window.PageGrid1, 1); Grid.SetRowSpan(_window.PageGrid1, 2);
                Grid.SetColumn(_window.PageGrid2, 0); Grid.SetRow(_window.PageGrid2, 0);
                Grid.SetColumnSpan(_window.PageGrid2, 1); Grid.SetRowSpan(_window.PageGrid2, 2);
                
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Collapsed;
                _window.PageGrid4.Visibility = Visibility.Collapsed;
                _window.Image1.HorizontalAlignment = HorizontalAlignment.Left;
                _window.Image2.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else if (effectiveSplitCount == 3)
            {
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Visible;
                _window.PageGrid4.Visibility = Visibility.Collapsed;

                if (currentQuadLayout == 2) // Grid mode (1 top, 2 bottom)
                {
                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star);
                    _window.Row1.Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(_window.PageGrid1, 0); Grid.SetRow(_window.PageGrid1, 0);
                    Grid.SetColumnSpan(_window.PageGrid1, 2); Grid.SetRowSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 1); Grid.SetRow(_window.PageGrid2, 1);
                    Grid.SetColumnSpan(_window.PageGrid2, 1); Grid.SetRowSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 0); Grid.SetRow(_window.PageGrid3, 1);
                    Grid.SetColumnSpan(_window.PageGrid3, 1); Grid.SetRowSpan(_window.PageGrid3, 1);

                    _window.Image1.HorizontalAlignment = HorizontalAlignment.Center; _window.Image1.VerticalAlignment = VerticalAlignment.Bottom;
                    _window.Image2.HorizontalAlignment = HorizontalAlignment.Left;   _window.Image2.VerticalAlignment = VerticalAlignment.Top;
                    _window.Image3.HorizontalAlignment = HorizontalAlignment.Right;  _window.Image3.VerticalAlignment = VerticalAlignment.Top;
                }
                else // Horizontal 1x3
                {
                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                    Grid.SetColumn(_window.PageGrid1, 2); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 2); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 1); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 2); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 0); Grid.SetRow(_window.PageGrid3, 0); Grid.SetRowSpan(_window.PageGrid3, 2); Grid.SetColumnSpan(_window.PageGrid3, 1);
                    
                    _window.Image1.HorizontalAlignment = HorizontalAlignment.Center;
                    _window.Image2.HorizontalAlignment = HorizontalAlignment.Center;
                    _window.Image3.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            else // 4
            {
                _window.PageGrid1.Visibility = Visibility.Visible;
                _window.PageGrid2.Visibility = Visibility.Visible;
                _window.PageGrid3.Visibility = Visibility.Visible;
                _window.PageGrid4.Visibility = Visibility.Visible;

                if (currentQuadLayout == 1 || currentQuadLayout == 0) // Horizontal 1x4
                {
                    _window.Image1.HorizontalAlignment = HorizontalAlignment.Center;
                    _window.Image2.HorizontalAlignment = HorizontalAlignment.Center;
                    _window.Image3.HorizontalAlignment = HorizontalAlignment.Center;
                    _window.Image4.HorizontalAlignment = HorizontalAlignment.Center;

                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col3.Width = new GridLength(1, GridUnitType.Star);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star); _window.Row1.Height = new GridLength(0);

                    Grid.SetColumn(_window.PageGrid1, 3); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 2); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 2); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 2); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 1); Grid.SetRow(_window.PageGrid3, 0); Grid.SetRowSpan(_window.PageGrid3, 2); Grid.SetColumnSpan(_window.PageGrid3, 1);
                    Grid.SetColumn(_window.PageGrid4, 0); Grid.SetRow(_window.PageGrid4, 0); Grid.SetRowSpan(_window.PageGrid4, 2); Grid.SetColumnSpan(_window.PageGrid4, 1);
                }
                else // Grid 2x2
                {
                    _window.Image1.HorizontalAlignment = HorizontalAlignment.Left;
                    _window.Image2.HorizontalAlignment = HorizontalAlignment.Right;
                    _window.Image3.HorizontalAlignment = HorizontalAlignment.Left;
                    _window.Image4.HorizontalAlignment = HorizontalAlignment.Right;
                    
                    _window.Image1.VerticalAlignment = VerticalAlignment.Bottom;
                    _window.Image2.VerticalAlignment = VerticalAlignment.Bottom;
                    _window.Image3.VerticalAlignment = VerticalAlignment.Top;
                    _window.Image4.VerticalAlignment = VerticalAlignment.Top;

                    _window.Col0.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col1.Width = new GridLength(1, GridUnitType.Star);
                    _window.Col2.Width = new GridLength(0); _window.Col3.Width = new GridLength(0);
                    _window.Row0.Height = new GridLength(1, GridUnitType.Star);
                    _window.Row1.Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(_window.PageGrid1, 1); Grid.SetRow(_window.PageGrid1, 0); Grid.SetRowSpan(_window.PageGrid1, 1); Grid.SetColumnSpan(_window.PageGrid1, 1);
                    Grid.SetColumn(_window.PageGrid2, 0); Grid.SetRow(_window.PageGrid2, 0); Grid.SetRowSpan(_window.PageGrid2, 1); Grid.SetColumnSpan(_window.PageGrid2, 1);
                    Grid.SetColumn(_window.PageGrid3, 1); Grid.SetRow(_window.PageGrid3, 1); Grid.SetRowSpan(_window.PageGrid3, 1); Grid.SetColumnSpan(_window.PageGrid3, 1);
                    Grid.SetColumn(_window.PageGrid4, 0); Grid.SetRow(_window.PageGrid4, 1); Grid.SetRowSpan(_window.PageGrid4, 1); Grid.SetColumnSpan(_window.PageGrid4, 1);
                }
            }
        }

        private async Task LoadPageAsync(string filePath, Microsoft.UI.Xaml.Controls.Image imageCtrl, SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl, Microsoft.UI.Xaml.Controls.ProgressRing loadingRing, int pageIndex, CancellationToken token)
        {
            _pages[pageIndex].CurrentFilePath = filePath;
            loadingRing.IsActive = true;
            try
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                bool useSkia = ext == ".webp" || ext == ".gif" || ext == ".avis";

                if (useSkia)
                {
                    imageCtrl.Visibility = Visibility.Collapsed;
                    canvasCtrl.Visibility = Visibility.Visible;

                    try
                    {
                        var page = _pages[pageIndex];
                        await Task.Run(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            page.LoadSkia(filePath, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                _window.DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
                            }
                        }, token);
                    }
                    catch { }
                }
                else
                {
                    canvasCtrl.Visibility = Visibility.Collapsed;
                    imageCtrl.Visibility = Visibility.Visible;

                    bool nativeDecodeFailed = false;
                    try
                    {
                        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                        if (token.IsCancellationRequested) return;

                        using var stream = await file.OpenReadAsync();
                        if (token.IsCancellationRequested) return;

                        var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        await bitmapImage.SetSourceAsync(stream);
                        
                        if (!token.IsCancellationRequested)
                        {
                            imageCtrl.Source = bitmapImage;
                        }
                    }
                    catch { nativeDecodeFailed = true; }

                    if (nativeDecodeFailed)
                    {
                        var bmpBytes = ImageProcessor.DecodeToBmpBytes(filePath);
                        if (bmpBytes != null)
                        {
                            try
                            {
                                var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                using var ms = new System.IO.MemoryStream(bmpBytes);
                                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                                
                                if (!token.IsCancellationRequested)
                                {
                                    imageCtrl.Source = bitmapImage;
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            imageCtrl.Visibility = Visibility.Collapsed;
                            canvasCtrl.Visibility = Visibility.Visible;

                            try
                            {
                                var page = _pages[pageIndex];
                                await Task.Run(() =>
                                {
                                    if (token.IsCancellationRequested) return;
                                    page.LoadSkia(filePath, token);
                                    
                                    if (!token.IsCancellationRequested)
                                    {
                                        _window.DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
                                    }
                                }, token);
                            }
                            catch { }
                        }
                    }
                }
            }
            finally
            {
                loadingRing.IsActive = false;
            }
        }

        public void StopAnimation()
        {
            _animationTimer.Stop();
            foreach (var p in _pages) p.Reset();
        }

        public void UpdateStretch()
        {
            var stretch = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill) 
                ? Microsoft.UI.Xaml.Media.Stretch.UniformToFill 
                : Microsoft.UI.Xaml.Media.Stretch.Uniform;
            
            bool uniformToFill = (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowUniformToFill);

            for (int i = 0; i < 4; i++)
            {
                PageImages[i].Stretch = stretch;
                _pages[i].UniformToFill = uniformToFill;
                _pageCanvases[i].Invalidate();
            }
        }

        private void AnimationTimer_Tick(object? sender, object e)
        {
            int minInterval = 100;
            bool anyAnimated = false;
            for (int i = 0; i < 4; i++)
            {
                if (_pages[i].IsAnimated)
                {
                    int interval = _pages[i].AdvanceFrame();
                    if (!anyAnimated || interval < minInterval) minInterval = interval;
                    anyAnimated = true;
                    _pageCanvases[i].Invalidate();
                }
            }
            if (anyAnimated) _animationTimer.Interval = TimeSpan.FromMilliseconds(minInterval);
        }

        private int GetEffectiveQuadLayout()
        {
            int layout = _settings.QuadLayoutMode;
            if (_settings.MangaSplitCount != 4 || layout != 0) return layout;

            int wideCount = 0;
            int tallCount = 0;
            for (int i = 0; i < 4; i++)
            {
                int indexToLoad = -1;
                if (i == 0) indexToLoad = _window.CurrentIndex;
                else
                {
                    if (_window.SlideshowManager.IsSlideshowRunning && _settings.SlideshowRandom)
                    {
                        if (_window.SlideshowManager.SlideshowRandomIndices[i] != -1)
                            indexToLoad = _window.SlideshowManager.SlideshowRandomIndices[i];
                        else if (_window.CurrentIndex + i < _window.Playlist.Count)
                            indexToLoad = _window.CurrentIndex + i;
                    }
                    else if (_window.CurrentIndex + i < _window.Playlist.Count)
                    {
                        indexToLoad = _window.CurrentIndex + i;
                    }
                }

                if (indexToLoad != -1 && indexToLoad < _window.Playlist.Count)
                {
                    try
                    {
                        var (w, h) = ImageProcessor.GetImageSize(_window.Playlist[indexToLoad]);
                        if (w > 0 && h > 0)
                        {
                            if ((double)w / h > 1.2) wideCount++;
                            else tallCount++;
                        }
                    }
                    catch { }
                }
            }
            if (wideCount == 0 && tallCount == 0) return 1;
            return wideCount >= tallCount ? 2 : 1;
        }

        public void PaintCanvas(int index, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            
            int hAlign = 1; // Center
            int vAlign = 1; // Center

            int remaining = _window.Playlist.Count - _window.CurrentIndex;
            int splitCount = _settings.MangaSplitCount;
            int effectiveSplitCount = Math.Max(1, Math.Min(splitCount, remaining));

            if (effectiveSplitCount == 2) 
            {
                hAlign = index == 0 ? 0 : 2; // Index 0 (Right) -> Left-aligned, Index 1 (Left) -> Right-aligned
            } 
            else if (effectiveSplitCount == 3)
            {
                int layout = GetEffectiveQuadLayout();
                if (layout == 2) // Grid mode (1 top, 2 bottom)
                {
                    if (index == 0) { hAlign = 1; vAlign = 2; } // Top center (Bottom-aligned)
                    else if (index == 1) { hAlign = 2; vAlign = 0; } // Bottom right (Right/Top-aligned)
                    else if (index == 2) { hAlign = 0; vAlign = 0; } // Bottom left (Left/Top-aligned)
                }
            }
            else if (effectiveSplitCount == 4) 
            {
                int layout = GetEffectiveQuadLayout();

                if (layout == 2) // Grid 2x2
                {
                    hAlign = (index == 0 || index == 2) ? 0 : 2;
                    vAlign = (index == 0 || index == 1) ? 2 : 0;
                }
            }
            
            _pages[index].Paint(canvas, e.Info, hAlign, vAlign);
        }

        public async void NavigateFolder(int offset)
        {
            if (string.IsNullOrEmpty(_window.CurrentDirectory) || _window.IsSearchingFolder) return;

            _window.IsSearchingFolder = true;
            _window.FolderSearchingOverlay.Visibility = Visibility.Visible;

            try
            {
                string currentDir = _window.CurrentDirectory;
                string? nextImageFolder = await Task.Run(() => FileNavigator.FindNextImageFolder(currentDir, offset));
                
                if (!string.IsNullOrEmpty(nextImageFolder))
                {
                    _window.LoadDirectory(nextImageFolder);
                }
            }
            finally
            {
                _window.IsSearchingFolder = false;
                _window.FolderSearchingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void Navigate(int offset, bool forceSingleStep = false)
        {
            for (int i=0; i<4; i++) _window.SlideshowManager.SlideshowRandomIndices[i] = -1;
            if (_window.Playlist.Count == 0) return;

            int step = forceSingleStep ? 1 : _settings.MangaSplitCount;
            bool looped = false;

            int newIndex = _window.CurrentIndex;
            if (offset > 0)
            {
                newIndex += step;
                if (newIndex >= _window.Playlist.Count)
                {
                    newIndex = 0;
                    looped = true;
                }
            }
            else
            {
                newIndex -= step;
                if (newIndex < 0)
                {
                    int remainder = _window.Playlist.Count % step;
                    newIndex = _window.Playlist.Count - (remainder == 0 ? step : remainder);
                    if (newIndex < 0) newIndex = 0;
                    looped = true;
                }
            }
            _window.CurrentIndex = newIndex;

            if (looped)
            {
                string resKey = offset > 0 ? "Notification_LoopedStart" : "Notification_LoopedEnd";
                ShowNotification(_resourceLoader.GetString(resKey));
            }

            _ = UpdateDisplayAsync();
        }

        public void ShowNotification(string message)
        {
            _window.NotificationText.Text = message;
            _window.NotificationOverlay.Visibility = Visibility.Visible;
            _notificationTimer.Stop();
            _notificationTimer.Start();
        }

        public void Dispose()
        {
            StopAnimation();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
        }
    }
}
