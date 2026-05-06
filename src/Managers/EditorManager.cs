using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.ViewModels;
using quick_image_viewer.Views.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
namespace quick_image_viewer.Managers
{
    public class EditorManager : IEditorManager, IRecipient<EditMessage>
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;
        private Microsoft.Windows.ApplicationModel.Resources.ResourceManager _resourceManager;
        private Microsoft.Windows.ApplicationModel.Resources.ResourceContext _resourceContext;
        private readonly Dictionary<string, string> _stringCache = new Dictionary<string, string>();
        private int _contextTargetIndex = -1;
        private string _contextTargetPath = string.Empty;

        public string ContextTargetPath { get => _contextTargetPath; set => _contextTargetPath = value; }

        public EditorManager(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
            _resourceManager = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();
            PreloadStrings();

            WeakReferenceMessenger.Default.Register<EditMessage>(this);
        }

        public void Receive(EditMessage message)
        {
            if (message.Type == "Rotate")
            {
                if (message.Value is int degrees)
                {
                    _contextTargetPath = _window.CurrentImagePath;
                    MenuRotate_Click(new MenuFlyoutItem { Tag = degrees.ToString() }, new RoutedEventArgs());
                }
            }
            // Add other edit types as needed
        }

        private void PreloadStrings()
        {
            // Currently no dynamic strings need preloading as we moved to sub-menus and x:Uid
        }


        public void PagesGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Replaced by CropOverlay
        }

        public void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Replaced by CropOverlay
            _window.ViewerManager.HandlePointerMoved(e);
        }

        public void PagesGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Replaced by CropOverlay
        }

        public void EditMenuFlyout_Opening(object sender, object e)
        {
            // If no specific target was set by RightTapped, use current index
            if (_contextTargetIndex == -1)
            {
                _contextTargetPath = _window.CurrentImagePath;
            }

            UpdateMenuStates();
        }

        public void UpdateTargetIndexAtPoint(Point p)
        {
            var pageGrids = _window.GetPageGrids();
            for (int i = 0; i < pageGrids.Count; i++)
            {
                if (pageGrids[i].Visibility != Visibility.Visible) continue;

                var ttv = _window.PagesGrid.TransformToVisual(pageGrids[i]);
                var localPoint = ttv.TransformPoint(p);
                if (localPoint.X >= 0 && localPoint.X <= pageGrids[i].ActualWidth &&
                    localPoint.Y >= 0 && localPoint.Y <= pageGrids[i].ActualHeight)
                {
                    _contextTargetIndex = i;
                    _contextTargetPath = _window.ViewerManager.GetPathForPage(i) ?? string.Empty;
                    return;
                }
            }
            _contextTargetIndex = -1;
        }

        public void UpdateMenuStates()
        {
            string path = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            bool hasPath = !string.IsNullOrEmpty(path);
            bool canUndo = false;
            bool canRedo = false;

            if (hasPath)
            {
                var session = _window.ImageEditService.GetSession(path);
                if (session != null)
                {
                    canUndo = session.CanUndo;
                    canRedo = session.CanRedo;
                }
            }

            var vm = ((MainWindow)_window).ViewModel;

            vm.CanUndo = canUndo;
            vm.CanRedo = canRedo;
            bool isArchiveEntry = ArchiveManager.IsArchivePath(path);
            vm.HasValidPath = hasPath;
            vm.IsImageEditable = hasPath && !isArchiveEntry;

            // Update View Mode checked states
            int splitCount = _settings.MangaSplitCount;
            vm.IsViewSingle = (splitCount == 1);
            vm.IsViewDouble = (splitCount == 2);
            vm.IsViewQuad = (splitCount == 4);

            // Update Quad Layout checked states
            int layoutMode = _settings.QuadLayoutMode;
            vm.IsLayoutAuto = (layoutMode == 0);
            vm.IsLayoutHorz = (layoutMode == 1);
            vm.IsLayoutGrid = (layoutMode == 2);

            // Update Stretch Mode checked states
            int stretchMode = _settings.ImageStretchMode;
            vm.IsStretchOriginal = (stretchMode == 0);
            vm.IsStretchContain = (stretchMode == 2);
            vm.IsStretchCover = (stretchMode == 3);

            // Update Metadata checked state
            vm.ShowPageIndicator = _settings.ShowPageIndicator;

            // Update Bookmark state
            string? dir = !string.IsNullOrEmpty(path) ? Path.GetDirectoryName(path) : _window.CurrentDirectory;
            if (!string.IsNullOrEmpty(dir))
            {
                bool isBookmarked = _settings.Bookmarks.Exists(b => b.Path == dir);
                vm.BookmarkMenuText = isBookmarked ? GetString("MenuBookmark_Remove") : GetString("MenuBookmark_Add");
            }

            // Populate Bookmark List Sub-menu
            _window.MenuBookmarkList.Items.Clear();
            if (_settings.Bookmarks.Count == 0)
            {
                _window.MenuBookmarkList.Items.Add(new MenuFlyoutItem { Text = GetString("Bookmark_Empty"), IsEnabled = false });
            }
            else
            {
                foreach (var bm in _settings.Bookmarks)
                {
                    var bmItem = new MenuFlyoutItem { Text = bm.Name, Tag = bm.Path };
                    bmItem.Click += MenuBookmarkListItem_Click;
                    _window.MenuBookmarkList.Items.Add(bmItem);
                }
            }
        }

        private void MenuBookmarkListItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string path)
            {
                _window.LoadDirectory(path);
            }
        }

        public async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string ext)
            {
                await SaveImageAsync(ext, false);
            }
        }

        public async void MenuOverwrite_Click(object sender, RoutedEventArgs e)
        {
            string path = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            await SaveImageAsync(Path.GetExtension(path), true);
        }

        public async Task SaveImageAsync(string targetExtension, bool overwrite)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string destPath = overwrite ? sourcePath : Path.ChangeExtension(sourcePath, targetExtension);
            if (!overwrite)
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeChoices.Add(targetExtension.Trim('.').ToUpper(), new List<string>() { targetExtension });
                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(sourcePath);

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                destPath = file.Path;
            }

            try
            {
                if (overwrite)
                {
                    _window.ViewerManager.StopAnimation();
                    foreach (var ctrl in _window.ViewerManager.PageControls) ctrl.PageImage.Source = null;
                }

                await Task.Run(() =>
                {
                    int quality = _settings.JpegQuality;
                    var current = _window.ImageEditService.GetCurrentBitmap(sourcePath);
                    if (current != null)
                    {
                        ImageProcessor.SaveBitmap(current, destPath, targetExtension, quality);
                    }
                    else
                    {
                        ImageProcessor.SaveImage(sourcePath, destPath, targetExtension, quality);
                    }
                });

                if (overwrite) _ = _window.UpdateDisplayAsync();
            }
            catch { }
        }

        public void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            _window.DialogService.Show(new CropOverlay(_window) { Name = "CropOverlay" });
        }

        public async void ExecuteCropWithRect(Rect selectionRect)
        {
            try
            {
                // Find target grid based on selection center
                double cx = selectionRect.X + selectionRect.Width / 2;
                double cy = selectionRect.Y + selectionRect.Height / 2;
                var centerPoint = new Windows.Foundation.Point(cx, cy);

                int targetIdx = 0;
                var pageGrids = new Microsoft.UI.Xaml.FrameworkElement[] { _window.PageGrid1, _window.PageGrid2, _window.PageGrid3, _window.PageGrid4 };
                for (int i = 0; i < 4; i++)
                {
                    if (pageGrids[i].Visibility == Visibility.Visible)
                    {
                        var ttvGrid = _window.RootGrid.TransformToVisual(pageGrids[i]);
                        var p = ttvGrid.TransformPoint(centerPoint);
                        if (p.X >= 0 && p.X <= pageGrids[i].ActualWidth && p.Y >= 0 && p.Y <= pageGrids[i].ActualHeight)
                        {
                            targetIdx = i;
                            break;
                        }
                    }
                }

                string? sourcePath = _window.ViewerManager.Pages[targetIdx].CurrentFilePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                var (imgW, imgH) = _window.ImageEditService.GetImageSize(sourcePath);
                if (imgW == 0 || imgH == 0) return;

                FrameworkElement targetElement = _window.ViewerManager.PageControls[targetIdx].PageImage;
                if (targetElement.Visibility != Visibility.Visible)
                {
                    targetElement = _window.ViewerManager.PageControls[targetIdx].PageCanvas;
                }

                double renderRatio = targetElement.ActualWidth / targetElement.ActualHeight;
                double imageRatio = (double)imgW / imgH;

                double imgDisplayWidth = targetElement.ActualWidth;
                double imgDisplayHeight = targetElement.ActualHeight;
                double offsetX = 0;
                double offsetY = 0;

                if (imageRatio > renderRatio)
                {
                    imgDisplayHeight = targetElement.ActualWidth / imageRatio;
                    offsetY = (targetElement.ActualHeight - imgDisplayHeight) / 2;
                }
                else
                {
                    imgDisplayWidth = targetElement.ActualHeight * imageRatio;
                    offsetX = (targetElement.ActualWidth - imgDisplayWidth) / 2;
                }

                var ttv = _window.RootGrid.TransformToVisual(targetElement);
                var rectTopLeft = ttv.TransformPoint(new Windows.Foundation.Point(selectionRect.X, selectionRect.Y));
                var rectBottomRight = ttv.TransformPoint(new Windows.Foundation.Point(selectionRect.X + selectionRect.Width, selectionRect.Y + selectionRect.Height));

                double cropX = (rectTopLeft.X - offsetX) * (imgW / imgDisplayWidth);
                double cropY = (rectTopLeft.Y - offsetY) * (imgH / imgDisplayHeight);
                double cropW = (rectBottomRight.X - rectTopLeft.X) * (imgW / imgDisplayWidth);
                double cropH = (rectBottomRight.Y - rectTopLeft.Y) * (imgH / imgDisplayHeight);

                cropX = Math.Max(0, Math.Min(cropX, imgW));
                cropY = Math.Max(0, Math.Min(cropY, imgH));
                cropW = Math.Max(1, Math.Min(cropW, imgW - cropX));
                cropH = Math.Max(1, Math.Min(cropH, imgH - cropY));

                var cropRect = new SKRectI((int)cropX, (int)cropY, (int)(cropX + cropW), (int)(cropY + cropH));
                await _window.ImageEditService.CropAsync(sourcePath, cropRect);
            }
            catch (Exception ex)
            {
                var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                _window.ViewerManager.ShowNotification(string.Format(loader.GetString("Notification_CropError"), ex.Message));
            }
        }

        public void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            _window.DialogService.Show(new SettingsOverlay(_window, _settings) { Name = "SettingsOverlay" });
        }

        public async void MenuSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/SoraKumo001/quick-image-viewer"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open support link: {ex.Message}");
            }
        }

        public void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath))
                _window.ImageEditService.UndoEdit(sourcePath);
        }

        public void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath))
                _window.ImageEditService.RedoEdit(sourcePath);
        }

        public void MenuMetadata_Click(object sender, RoutedEventArgs e)
        {
            _window.ViewerManager.ToggleMetadataPanel(cycle: false);
        }
        public void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowPageIndicator = !_settings.ShowPageIndicator;
            _settings.SaveSettings();
            _window.ViewModel.ShowPageIndicator = _settings.ShowPageIndicator;
            UpdateMenuStates();
        }

        public void MenuResize_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            var (origW, origH) = _window.ImageEditService.GetImageSize(sourcePath);
            _window.DialogService.Show(new ResizeOverlay(_window, sourcePath, origW, origH) { Name = "ResizeOverlay" });
        }

        public void MenuTone_Click(object sender, RoutedEventArgs e)
        {
            if (_window.ViewerManager == null) return;

            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            // Capture base bitmap for non-cumulative adjustment
            SKBitmap? baseBmp = _window.ImageEditService.GetCurrentBitmap(sourcePath)?.Copy();
            if (baseBmp == null)
            {
                try { baseBmp = SKBitmap.Decode(sourcePath); } catch { }
            }

            _window.DialogService.Show(new ToneAdjustmentOverlay(_window, sourcePath, baseBmp) { Name = "ToneAdjustmentOverlay" });
        }

        public async void MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string filterType)
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                await _window.ImageEditService.FilterAsync(sourcePath, filterType);
            }
        }

        public async void MenuRotate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && float.TryParse(tagStr, out float degrees))
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                await _window.ImageEditService.RotateAsync(sourcePath, degrees);
            }
        }

        public async void MenuFlip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string flipMode)
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;

                bool horizontal = flipMode == "Horz";
                await _window.ImageEditService.FlipAsync(sourcePath, horizontal);
            }
        }

        public async void MenuPrint_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            await _window.PrintService.PrintImageAsync(sourcePath);
        }

        public async void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _window.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string targetPath = sourcePath;
            if (ArchiveManager.IsArchivePath(sourcePath))
            {
                targetPath = ArchiveManager.SplitArchivePath(sourcePath).archivePath;
            }

            if (!File.Exists(targetPath)) return;

            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(targetPath));
                var file = await StorageFile.GetFileFromPathAsync(targetPath);
                var options = new Windows.System.FolderLauncherOptions();
                options.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderAsync(folder, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open explorer: {ex.Message}");
            }
        }

        public void MenuViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int count))
            {
                _settings.MangaSplitCount = count;
                _settings.SaveMangaMode();
                _window.ViewModel.MangaSplitCount = count;

                // Ensure radio behavior
                var vm = ((MainWindow)_window).ViewModel;
                vm.IsViewSingle = (count == 1);
                vm.IsViewDouble = (count == 2);
                vm.IsViewQuad = (count == 4);

                _ = _window.UpdateDisplayAsync();
            }
        }

        public void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _settings.SaveMangaMode();

                // Ensure radio behavior
                var vm = ((MainWindow)_window).ViewModel;
                vm.IsLayoutAuto = (mode == 0);
                vm.IsLayoutHorz = (mode == 1);
                vm.IsLayoutGrid = (mode == 2);

                if (_settings.MangaSplitCount == 4)
                {
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public void MenuStretchMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.ImageStretchMode = mode;
                _settings.SaveSettings();

                // Ensure radio behavior
                var vm = ((MainWindow)_window).ViewModel;
                vm.IsStretchOriginal = (mode == 0);
                vm.IsStretchContain = (mode == 2);
                vm.IsStretchCover = (mode == 3);

                _window.ViewerManager.UpdateStretch();
            }
        }



        public void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            _window.DialogService.Show(new KeyBindingsOverlay(_window, _settings) { Name = "KeyBindingsOverlay" });
        }



        public string GetString(string key)
        {
            if (_stringCache.TryGetValue(key, out var cached)) return cached;
            try
            {
                var resourceKey = "Resources/" + key.Replace(".", "/");
                var candidate = _resourceManager.MainResourceMap.GetValue(resourceKey, _resourceContext);
                var val = candidate?.ValueAsString ?? key;
                _stringCache[key] = val;
                return val;
            }
            catch { return key; }
        }
    }
}
