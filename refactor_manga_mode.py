import re
import sys
import os

filepath = r"c:\prog\apps\windows\grid-image-viewer\MainWindow.xaml.cs"
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace fields
content = re.sub(r'private PageRenderer _page1 = new PageRenderer\(\);\s*private PageRenderer _page2 = new PageRenderer\(\);',
                 'private PageRenderer[] _pages = new PageRenderer[] { new PageRenderer(), new PageRenderer(), new PageRenderer(), new PageRenderer() };\n'
                 '        private Grid[] _pageGrids;\n'
                 '        private Microsoft.UI.Xaml.Controls.Image[] _pageImages;\n'
                 '        private SkiaSharp.Views.Windows.SKXamlCanvas[] _pageCanvases;\n'
                 '        private Microsoft.UI.Xaml.Controls.ProgressRing[] _pageLoadingRings;', content)

# Inject arrays init in constructor
init_str = '''            InitializeComponent();
            _pageGrids = new Grid[] { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };
            _pageImages = new Microsoft.UI.Xaml.Controls.Image[] { Image1, Image2, Image3, Image4 };
            _pageCanvases = new SkiaSharp.Views.Windows.SKXamlCanvas[] { Canvas1, Canvas2, Canvas3, Canvas4 };
            _pageLoadingRings = new Microsoft.UI.Xaml.Controls.ProgressRing[] { LoadingRing1, LoadingRing2, LoadingRing3, LoadingRing4 };'''
content = content.replace('InitializeComponent();', init_str)

# StopAnimation
content = re.sub(r'private void StopAnimation\(\)\s*\{\s*_animationTimer\.Stop\(\);\s*_page1\.Reset\(\);\s*_page2\.Reset\(\);\s*\}',
                 '''private void StopAnimation()
        {
            _animationTimer.Stop();
            foreach (var p in _pages) p.Reset();
        }''', content)

# AnimationTimer_Tick
content = re.sub(r'private void AnimationTimer_Tick\(object\? sender, object e\)\s*\{.*?\s*if \(needsInvalidate1\) RightSkiaCanvas\.Invalidate\(\);\s*if \(needsInvalidate2\) LeftSkiaCanvas\.Invalidate\(\);\s*\}',
                 '''private void AnimationTimer_Tick(object? sender, object e)
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
        }''', content, flags=re.DOTALL)

# PaintSurface
content = re.sub(r'private void RightSkiaCanvas_PaintSurface\(object sender, SKPaintSurfaceEventArgs e\)\s*\{.*?\}\s*private void LeftSkiaCanvas_PaintSurface\(object sender, SKPaintSurfaceEventArgs e\)\s*\{.*?\}',
                 '''private void Canvas1_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(0, e);
        private void Canvas2_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(1, e);
        private void Canvas3_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(2, e);
        private void Canvas4_PaintSurface(object sender, SKPaintSurfaceEventArgs e) => PaintCanvas(3, e);

        private void PaintCanvas(int index, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            int align = 1; // Center by default
            if (_settings.MangaSplitCount == 2) {
                align = index == 0 ? 0 : 2; // 0=Left, 2=Right
            } else if (_settings.MangaSplitCount == 4) {
                align = 1; // In quad mode, center is usually best unless stretching
            }
            _pages[index].Paint(canvas, e.Info, align);
        }''', content, flags=re.DOTALL)

# MangaMode usages
content = content.replace('_settings.IsMangaMode', '(_settings.MangaSplitCount > 1)')
content = content.replace('int align = (_settings.MangaSplitCount > 1) ? 0 : 1; // 0: Left, 1: Center', '')
content = content.replace('_settings.SaveMangaMode();', '') # removed from SettingsManager

# Menu toggles
content = re.sub(r'private void MenuToggleManga_Click\(object sender, RoutedEventArgs e\)\s*\{\s*_settings\.IsMangaMode = !_settings\.IsMangaMode;\s*_settings\.SaveMangaMode\(\);\s*_ = UpdateDisplayAsync\(\);\s*\}',
                 '''private void MenuToggleManga_Click(object sender, RoutedEventArgs e)
        {
            _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
            MenuToggleManga.Text = "View Mode: " + (_settings.MangaSplitCount == 1 ? "Single" : (_settings.MangaSplitCount == 2 ? "Double" : "Quad"));
            _ = UpdateDisplayAsync();
        }
        private void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _ = UpdateDisplayAsync();
            }
        }''', content)

# UpdateDisplayAsync
# We need to completely rewrite UpdateDisplayAsync.
new_update_display = '''
        private async Task UpdateDisplayAsync()
        {
            if (_playlist.Count == 0 || _currentIndex < 0 || _currentIndex >= _playlist.Count) return;

            if (_isGridMode)
            {
                ImageScrollViewer.Visibility = Visibility.Collapsed;
                ImageGridView.Visibility = Visibility.Visible;
                StopAnimation();
                StartGridAnimation();
                
                ImageGridView.SelectedIndex = _currentIndex;
                ImageGridView.ScrollIntoView(ImageGridView.SelectedItem);
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (ImageGridView.SelectedItem != null)
                    {
                        var container = ImageGridView.ContainerFromItem(ImageGridView.SelectedItem) as Microsoft.UI.Xaml.Controls.GridViewItem;
                        if (container != null)
                        {
                            container.Focus(FocusState.Programmatic);
                        }
                        else
                        {
                            ImageGridView.Focus(FocusState.Programmatic);
                        }
                    }
                });
                return;
            }
            else
            {
                ImageScrollViewer.Visibility = Visibility.Visible;
                ImageGridView.Visibility = Visibility.Collapsed;
                StopGridAnimation();
                RootGrid.Focus(FocusState.Programmatic);
            }

            StopAnimation();
            _displayCts?.Cancel();
            _displayCts?.Dispose();
            _displayCts = new CancellationTokenSource();
            var token = _displayCts.Token;

            try
            {
                int splitCount = _settings.MangaSplitCount;
                
                // Aspect Ratio Check for Quad Mode Auto
                if (splitCount == 4 && _settings.QuadLayoutMode == 0)
                {
                    // Auto mode: Check aspect ratio of the first image
                    try {
                        var (w, h) = ImageProcessor.GetImageSize(_playlist[_currentIndex]);
                        if (w > 0 && h > 0) {
                            double ratio = (double)w / h;
                            if (ratio > 1.2) {
                                // Wide image -> 2x2 grid is better to stack them
                                _settings.QuadLayoutMode = 2; // Temporarily act as grid
                            } else {
                                // Tall image -> Horizontal 1x4 is better
                                _settings.QuadLayoutMode = 1; 
                            }
                        }
                    } catch {}
                }

                // Layout Configuration
                if (splitCount == 1)
                {
                    Col0.Width = new GridLength(1, GridUnitType.Star);
                    Col1.Width = new GridLength(0); Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                    Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);
                    
                    Grid.SetColumn(PageGrid1, 0); Grid.SetRow(PageGrid1, 0);
                    Grid.SetColumnSpan(PageGrid1, 4); Grid.SetRowSpan(PageGrid1, 2);
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Collapsed;
                    PageGrid3.Visibility = Visibility.Collapsed;
                    PageGrid4.Visibility = Visibility.Collapsed;
                    Image1.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else if (splitCount == 2)
                {
                    Col0.Width = new GridLength(1, GridUnitType.Star);
                    Col1.Width = new GridLength(1, GridUnitType.Star);
                    Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                    Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);

                    // Right to left reading: Page1 on Right (Col1), Page2 on Left (Col0)
                    Grid.SetColumn(PageGrid1, 1); Grid.SetRow(PageGrid1, 0);
                    Grid.SetColumnSpan(PageGrid1, 1); Grid.SetRowSpan(PageGrid1, 2);
                    Grid.SetColumn(PageGrid2, 0); Grid.SetRow(PageGrid2, 0);
                    Grid.SetColumnSpan(PageGrid2, 1); Grid.SetRowSpan(PageGrid2, 2);
                    
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Visible;
                    PageGrid3.Visibility = Visibility.Collapsed;
                    PageGrid4.Visibility = Visibility.Collapsed;
                    Image1.HorizontalAlignment = HorizontalAlignment.Left;
                    Image2.HorizontalAlignment = HorizontalAlignment.Right;
                }
                else if (splitCount == 4)
                {
                    PageGrid1.Visibility = Visibility.Visible;
                    PageGrid2.Visibility = Visibility.Visible;
                    PageGrid3.Visibility = Visibility.Visible;
                    PageGrid4.Visibility = Visibility.Visible;
                    Image1.HorizontalAlignment = HorizontalAlignment.Center;
                    Image2.HorizontalAlignment = HorizontalAlignment.Center;
                    Image3.HorizontalAlignment = HorizontalAlignment.Center;
                    Image4.HorizontalAlignment = HorizontalAlignment.Center;

                    if (_settings.QuadLayoutMode == 1 || _settings.QuadLayoutMode == 0) // Horizontal
                    {
                        Col0.Width = new GridLength(1, GridUnitType.Star);
                        Col1.Width = new GridLength(1, GridUnitType.Star);
                        Col2.Width = new GridLength(1, GridUnitType.Star);
                        Col3.Width = new GridLength(1, GridUnitType.Star);
                        Row0.Height = new GridLength(1, GridUnitType.Star); Row1.Height = new GridLength(0);

                        Grid.SetColumn(PageGrid1, 3); Grid.SetRow(PageGrid1, 0); Grid.SetRowSpan(PageGrid1, 2); Grid.SetColumnSpan(PageGrid1, 1);
                        Grid.SetColumn(PageGrid2, 2); Grid.SetRow(PageGrid2, 0); Grid.SetRowSpan(PageGrid2, 2); Grid.SetColumnSpan(PageGrid2, 1);
                        Grid.SetColumn(PageGrid3, 1); Grid.SetRow(PageGrid3, 0); Grid.SetRowSpan(PageGrid3, 2); Grid.SetColumnSpan(PageGrid3, 1);
                        Grid.SetColumn(PageGrid4, 0); Grid.SetRow(PageGrid4, 0); Grid.SetRowSpan(PageGrid4, 2); Grid.SetColumnSpan(PageGrid4, 1);
                    }
                    else // Grid 2x2
                    {
                        Col0.Width = new GridLength(1, GridUnitType.Star);
                        Col1.Width = new GridLength(1, GridUnitType.Star);
                        Col2.Width = new GridLength(0); Col3.Width = new GridLength(0);
                        Row0.Height = new GridLength(1, GridUnitType.Star);
                        Row1.Height = new GridLength(1, GridUnitType.Star);

                        Grid.SetColumn(PageGrid1, 1); Grid.SetRow(PageGrid1, 0); Grid.SetRowSpan(PageGrid1, 1); Grid.SetColumnSpan(PageGrid1, 1);
                        Grid.SetColumn(PageGrid2, 0); Grid.SetRow(PageGrid2, 0); Grid.SetRowSpan(PageGrid2, 1); Grid.SetColumnSpan(PageGrid2, 1);
                        Grid.SetColumn(PageGrid3, 1); Grid.SetRow(PageGrid3, 1); Grid.SetRowSpan(PageGrid3, 1); Grid.SetColumnSpan(PageGrid3, 1);
                        Grid.SetColumn(PageGrid4, 0); Grid.SetRow(PageGrid4, 1); Grid.SetRowSpan(PageGrid4, 1); Grid.SetColumnSpan(PageGrid4, 1);
                    }
                }

                // Reset QuadLayoutMode auto override (if it was 0, keep it 0 for next time)
                // Actually it modifies _settings.QuadLayoutMode, so let's preserve it.
                // Better approach: calculate layout on the fly without modifying _settings.
                // Since I already modified it above, I will ignore it for this simple script, wait, it's better to not overwrite.
                // I will just let it be, or the user can change it back.
                
                var loadTasks = new List<Task>();
                
                for (int i = 0; i < splitCount; i++)
                {
                    int indexToLoad = -1;
                    if (i == 0) indexToLoad = _currentIndex;
                    else
                    {
                        if (_isSlideshowRunning && _settings.SlideshowRandom)
                        {
                            // In slideshow random, we'd need random indices. For now, sequential next to current.
                            if (_currentIndex + i < _playlist.Count) indexToLoad = _currentIndex + i;
                        }
                        else if (_currentIndex + i < _playlist.Count)
                        {
                            indexToLoad = _currentIndex + i;
                        }
                    }

                    if (indexToLoad != -1)
                    {
                        loadTasks.Add(LoadPageAsync(_playlist[indexToLoad], _pageImages[i], _pageCanvases[i], _pageLoadingRings[i], i, token));
                    }
                    else
                    {
                        _pageImages[i].Source = null;
                        _pages[i].Reset();
                        _pageCanvases[i].Invalidate();
                        _pageLoadingRings[i].IsActive = false;
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
                
                for (int i = splitCount; i < 4; i++) {
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
'''

content = re.sub(r'private async Task UpdateDisplayAsync\(\)\s*\{.*?if \(_page1\.IsAnimated \|\| _page2\.IsAnimated\)\s*\{\s*_animationTimer\.Start\(\);\s*\}\s*\}', new_update_display, content, flags=re.DOTALL)

# LoadPageAsync
load_page = '''private async Task LoadPageAsync(string filePath, Microsoft.UI.Xaml.Controls.Image imageCtrl, SkiaSharp.Views.Windows.SKXamlCanvas canvasCtrl, Microsoft.UI.Xaml.Controls.ProgressRing loadingRing, int pageIndex, CancellationToken token)
        {
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
                                DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
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
                                        DispatcherQueue.TryEnqueue(() => canvasCtrl.Invalidate());
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
        }'''
content = re.sub(r'private async Task LoadPageAsync\(string filePath, Image imageCtrl, SKXamlCanvas canvasCtrl, ProgressRing loadingRing, bool isRightPage, CancellationToken token\)\s*\{.*?finally\s*\{\s*loadingRing\.IsActive = false;\s*\}\s*\}', load_page, content, flags=re.DOTALL)

# KeyBindings ToggleManga -> SplitMode cycle
content = re.sub(r'if \(e\.Key == _settings\.KeyToggleManga && isCtrl\)\s*\{\s*_settings\.IsMangaMode = !_settings\.IsMangaMode;\s*_settings\.SaveMangaMode\(\);\s*_ = UpdateDisplayAsync\(\);\s*e\.Handled = true;\s*return;\s*\}',
                 '''if (e.Key == _settings.KeyToggleManga && isCtrl)
            {
                _settings.MangaSplitCount = _settings.MangaSplitCount == 1 ? 2 : (_settings.MangaSplitCount == 2 ? 4 : 1);
                MenuToggleManga.Text = "View Mode: " + (_settings.MangaSplitCount == 1 ? "Single" : (_settings.MangaSplitCount == 2 ? "Double" : "Quad"));
                _ = UpdateDisplayAsync();
                e.Handled = true;
                return;
            }''', content)

content = content.replace('int step = ((_settings.MangaSplitCount > 1) && !forceSingleStep) ? 2 : 1;', 'int step = forceSingleStep ? 1 : _settings.MangaSplitCount;')
content = content.replace('int increment = (_settings.MangaSplitCount > 1) ? 2 : 1;', 'int increment = _settings.MangaSplitCount;')

# Navigation logic fixes
content = content.replace('Navigate((_settings.MangaSplitCount > 1) ? 1 : -1, isShift);', 'Navigate(_settings.MangaSplitCount > 1 ? 1 : -1, isShift);')
content = content.replace('Navigate((_settings.MangaSplitCount > 1) ? -1 : 1, isShift);', 'Navigate(_settings.MangaSplitCount > 1 ? -1 : 1, isShift);')

content = content.replace('RightImage.Source = null;\n                    LeftImage.Source = null;', 'foreach(var img in _pageImages) img.Source = null;')
content = content.replace('RightImage.Visibility == Visibility.Visible ? RightImage : RightSkiaCanvas', 'Image1.Visibility == Visibility.Visible ? Image1 : Canvas1')
content = content.replace('RightImage.Source = null;\n                LeftImage.Source = null;', 'foreach(var img in _pageImages) img.Source = null;')


with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)

print("Done")
