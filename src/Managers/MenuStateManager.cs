using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Services;
using quick_image_viewer.ViewModels;

using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace quick_image_viewer.Managers
{
    public class MenuStateManager : IMenuStateManager, IRecipient<EditMessage>, IRecipient<EditActionMessage>, IRecipient<ViewActionMessage>
    {
        private readonly IViewerStateService _state;
        private readonly ISettingsManager _settings;
        private readonly IImageEditService _imageEdit;
        private readonly IDialogService _dialog;
        private readonly IPrintService _print;
        private readonly INotificationService _notification;

        // These are still somewhat UI-coupled but can be injected or handled via messaging
        private IViewerManager _viewerManager;
        private IPlaylistManager _playlist;

        private Microsoft.Windows.ApplicationModel.Resources.ResourceManager _resourceManager;
        private Microsoft.Windows.ApplicationModel.Resources.ResourceContext _resourceContext;
        private readonly Dictionary<string, string> _stringCache = new Dictionary<string, string>();
        private int _contextTargetIndex = -1;
        private string _contextTargetPath = string.Empty;

        public string ContextTargetPath { get => _contextTargetPath; set => _contextTargetPath = value; }

        public MenuStateManager(
            IViewerStateService state,
            ISettingsManager settings,
            IImageEditService imageEdit,
            IDialogService dialog,
            IPrintService print,
            INotificationService notification,
            IViewerManager viewerManager,
            IPlaylistManager playlist)
        {
            _state = state;
            _settings = settings;
            _imageEdit = imageEdit;
            _dialog = dialog;
            _print = print;
            _notification = notification;
            _viewerManager = viewerManager;
            _playlist = playlist;

            _resourceManager = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();

            WeakReferenceMessenger.Default.Register<EditMessage>(this);
            WeakReferenceMessenger.Default.Register<EditActionMessage>(this);
            WeakReferenceMessenger.Default.Register<ViewActionMessage>(this);
        }

        public void Receive(EditMessage message)
        {
            if (message.Type == "Rotate")
            {
                if (message.Value is int degrees)
                {
                    _contextTargetPath = _state.CurrentImagePath;
                    MenuRotate_Click(new MenuFlyoutItem { Tag = degrees.ToString() }, new RoutedEventArgs());
                }
            }
        }

        public void Receive(EditActionMessage message)
        {
            _contextTargetPath = !string.IsNullOrEmpty(message.Path) ? message.Path : _state.CurrentImagePath;

            switch (message.Action)
            {
                case "Undo": MenuUndo_Click(null!, null!); break;
                case "Redo": MenuRedo_Click(null!, null!); break;
                case "Overwrite": MenuOverwrite_Click(null!, null!); break;
                case "SaveAs": MenuSaveAs_Click(null!, null!); break;
                case "Print": MenuPrint_Click(null!, null!); break;
                case "Crop": MenuCrop_Click(null!, null!); break;
                case "Resize": MenuResize_Click(null!, null!); break;
                case "Tone": MenuTone_Click(null!, null!); break;
                case "Filter": MenuFilter_Click(new MenuFlyoutItem { Tag = message.Value?.ToString() }, null!); break;
                case "Flip": MenuFlip_Click(new MenuFlyoutItem { Tag = message.Value?.ToString() }, null!); break;
                case "Rotate": MenuRotate_Click(new MenuFlyoutItem { Tag = message.Value?.ToString() }, null!); break;
            }
        }

        public void Receive(ViewActionMessage message)
        {
            switch (message.Action)
            {
                case "ViewMode": MenuViewMode_Click(new ToggleMenuFlyoutItem { Tag = message.Value.ToString() }, null!); break;
                case "LayoutMode": MenuLayoutMode_Click(new ToggleMenuFlyoutItem { Tag = message.Value.ToString() }, null!); break;
                case "StretchMode": MenuStretchMode_Click(new ToggleMenuFlyoutItem { Tag = message.Value.ToString() }, null!); break;
            }
        }

        public void PagesGrid_PointerPressed(object sender, PointerRoutedEventArgs e) { }
        public void PagesGrid_PointerMoved(object sender, PointerRoutedEventArgs e) { _viewerManager.HandlePointerMoved(e); }
        public void PagesGrid_PointerReleased(object sender, PointerRoutedEventArgs e) { }

        public void EditMenuFlyout_Opening(object sender, object e)
        {
            if (_contextTargetIndex == -1)
            {
                _contextTargetPath = _state.CurrentImagePath;
            }
            WeakReferenceMessenger.Default.Send(new BookmarksChangedMessage()); // Triggers ViewModel update
            UpdateMenuStates();
        }

        public void UpdateTargetIndexAtPoint(Point p)
        {
            if (_viewerManager?.PageControls == null) return;
            var pagesGrid = ((App)Application.Current).MainView?.PagesGrid;
            if (pagesGrid == null) return;

            for (int i = 0; i < _viewerManager.PageControls.Length; i++)
            {
                var ctrl = _viewerManager.PageControls[i];
                if (ctrl.Visibility == Visibility.Visible)
                {
                    try
                    {
                        var transform = ctrl.TransformToVisual(pagesGrid);
                        var bounds = transform.TransformBounds(new Rect(0, 0, ctrl.ActualWidth, ctrl.ActualHeight));
                        if (bounds.Contains(p))
                        {
                            var path = _viewerManager.GetPathForPage(i);
                            if (!string.IsNullOrEmpty(path))
                            {
                                _contextTargetPath = path;
                                return;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public void UpdateMenuStates()
        {
            string path = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            bool hasPath = !string.IsNullOrEmpty(path);
            bool isVideo = MediaHelper.IsVideo(path);
            bool isArchive = ArchiveManager.IsArchivePath(path);

            bool canUndo = false;
            bool canRedo = false;

            if (hasPath)
            {
                var session = _imageEdit.GetSession(path);
                if (session != null)
                {
                    canUndo = session.CanUndo;
                    canRedo = session.CanRedo;
                }
            }

            bool isExportable = hasPath && !isVideo;
            bool isManageable = hasPath && !isArchive;
            bool isEditable = hasPath && !isArchive && !isVideo;

            // Send message to update ViewModel instead of direct access        
            WeakReferenceMessenger.Default.Send(new UpdateMenuStatesMessage(path, canUndo, canRedo, isExportable, isManageable, isEditable));
        }

        public async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync(null, false);
        }

        public async void MenuOverwrite_Click(object sender, RoutedEventArgs e)
        {
            string path = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            await SaveImageAsync(Path.GetExtension(path), true);
        }

        public async Task SaveImageAsync(string? targetExtension, bool overwrite)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string destPath;
            if (overwrite)
            {
                destPath = sourcePath;
            }
            else
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = ((App)Application.Current).MainView?.WindowHandle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero) return;
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

                if (string.IsNullOrEmpty(targetExtension))
                {
                    picker.FileTypeChoices.Add("JPEG", new List<string>() { ".jpg", ".jpeg" });
                    picker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
                    picker.FileTypeChoices.Add("WebP", new List<string>() { ".webp" });
                    picker.FileTypeChoices.Add("BMP", new List<string>() { ".bmp" });

                    var origExt = Path.GetExtension(sourcePath).ToLower();
                    picker.DefaultFileExtension = origExt == ".png" || origExt == ".webp" || origExt == ".bmp" ? origExt : ".jpg";
                }
                else
                {
                    picker.FileTypeChoices.Add(targetExtension.Trim('.').ToUpper(), new List<string>() { targetExtension });
                    picker.DefaultFileExtension = targetExtension;
                }

                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(sourcePath);

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                destPath = file.Path;
                targetExtension = Path.GetExtension(destPath).ToLower();
            }

            try
            {
                if (overwrite)
                {
                    _viewerManager.StopAnimation();
                    WeakReferenceMessenger.Default.Send(new ClearImageSourceMessage(sourcePath));
                }

                await Task.Run(() =>
                {
                    int quality = _settings.JpegQuality;
                    var current = _imageEdit.GetCurrentBitmap(sourcePath);
                    if (current != null)
                    {
                        ImageProcessor.SaveBitmap(current, destPath, targetExtension!, quality);
                    }
                    else
                    {
                        ImageProcessor.SaveImage(sourcePath, destPath, targetExtension!, quality);
                    }
                });

                if (overwrite)
                {
                    _imageEdit.ClearEdits(sourcePath);
                    WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
                }
            }
            catch { }
        }

        public void MenuCrop_Click(object sender, RoutedEventArgs e)
        {
            _dialog.ShowCropOverlay();
        }

        public async void ExecuteCropWithRect(Rect selectionRect)
        {
            // Similar to UpdateTargetIndexAtPoint, this is UI-heavy.
            // It might be better to move this logic to the View or a View-aware helper.
        }

        public void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            _dialog.ShowSettingsOverlay(_settings, 0); // Open with Display tab
        }

        public async void MenuSupport_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/SoraKumo001/quick-image-viewer"));
        }

        public void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath)) _imageEdit.UndoEdit(sourcePath);
        }

        public void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (!string.IsNullOrEmpty(sourcePath)) _imageEdit.RedoEdit(sourcePath);
        }

        public void MenuMetadata_Click(object sender, RoutedEventArgs e)
        {
            _viewerManager.ToggleMetadataPanel(cycle: false);
        }

        public void MenuPageIndicatorToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowPageIndicator = !_settings.ShowPageIndicator;
            _settings.SaveSettings();
            // ViewModel will be updated via message or direct binding if possible
            UpdateMenuStates();
        }

        public void MenuResize_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            var (origW, origH) = _imageEdit.GetImageSize(sourcePath);
            _dialog.ShowResizeOverlay(sourcePath, origW, origH);
        }

        public void MenuTone_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            SKBitmap? baseBmp = _imageEdit.GetCurrentBitmap(sourcePath)?.Copy();
            if (baseBmp == null) { try { baseBmp = SKBitmap.Decode(sourcePath); } catch { } }

            _dialog.ShowToneAdjustmentOverlay(sourcePath, baseBmp);
        }

        public async void MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string filterType)
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;
                await _imageEdit.FilterAsync(sourcePath, filterType);
            }
        }

        public async void MenuRotate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && float.TryParse(tagStr, out float degrees))
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;
                await _imageEdit.RotateAsync(sourcePath, degrees);
            }
        }

        public async void MenuFlip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string flipMode)
            {
                string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
                if (string.IsNullOrEmpty(sourcePath)) return;
                await _imageEdit.FlipAsync(sourcePath, flipMode == "Horz");
            }
        }

        public async void MenuPrint_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;
            await _print.PrintImageAsync(sourcePath);
        }

        public async void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = !string.IsNullOrEmpty(_contextTargetPath) ? _contextTargetPath : _state.CurrentImagePath;
            if (string.IsNullOrEmpty(sourcePath)) return;

            string targetPath = sourcePath;
            if (ArchiveManager.IsArchivePath(sourcePath))
                targetPath = ArchiveManager.SplitArchivePath(sourcePath).archivePath;

            if (!File.Exists(targetPath)) return;

            try
            {
                var folderPath = Path.GetDirectoryName(targetPath);
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{targetPath}\"");
            }
            catch { }
        }

        public void MenuViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int count))
            {
                _settings.MangaSplitCount = count;
                _settings.SaveMangaMode();
                WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
            }
        }

        public void MenuLayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.QuadLayoutMode = mode;
                _settings.SaveMangaMode();
                if (_settings.MangaSplitCount == 4) WeakReferenceMessenger.Default.Send(new RefreshDisplayMessage());
            }
        }

        public void MenuStretchMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                _settings.ImageStretchMode = mode;
                _settings.SaveSettings();
                _viewerManager.UpdateStretch(true);
                UpdateMenuStates();
            }
        }

        public void MenuKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            _dialog.ShowSettingsOverlay(_settings, 3); // Open with Shortcuts tab
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