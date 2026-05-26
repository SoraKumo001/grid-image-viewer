using Microsoft.UI.Xaml;
using quick_image_viewer.Common;
using quick_image_viewer.Interfaces;
using System;
namespace quick_image_viewer.Services
{
    internal class MetadataDisplayService : IMetadataDisplayService
    {
        private readonly IMetadataHost _window;
        private readonly ISettingsManager _settings;
        private int _focusedPageIndex = 0;

        public MetadataDisplayService(IMetadataHost window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public void ToggleMetadataPanel(bool cycle = true)
        {
            if (_window.ViewModel.Viewer.IsMetadataVisible)
            {
                int total = 0;
                int splitCount = _settings.MangaSplitCount;
                for (int i = 0; i < splitCount; i++)
                {
                    if (_window.CurrentIndex + i < _window.Playlist.Count) total++;
                }

                if (total > 1 && cycle)
                {
                    _focusedPageIndex++;
                    if (_focusedPageIndex >= total)
                    {
                        _focusedPageIndex = 0;
                        _window.ViewModel.Viewer.IsMetadataVisible = false;
                        UpdateFocusBorders();
                    }
                    else
                    {
                        UpdateMetadataPanel();
                    }
                }
                else
                {
                    _window.ViewModel.Viewer.IsMetadataVisible = false;
                    UpdateFocusBorders();
                }
            }
            else
            {
                _focusedPageIndex = 0;
                _window.ViewModel.Viewer.IsMetadataVisible = true;
                UpdateMetadataPanel();
            }
        }

        public async void UpdateMetadataPanel()
        {
            try
            {
                if (!_window.ViewModel.Viewer.IsMetadataVisible || _window.Playlist.Count == 0)
                {
                    UpdateFocusBorders();
                    return;
                }

                int index = _window.CurrentIndex + _focusedPageIndex;
                if (index >= _window.Playlist.Count) index = _window.CurrentIndex;

                UpdateFocusBorders();

                int total = 0;
                int splitCount = _settings.MangaSplitCount;
                for (int i = 0; i < splitCount; i++)
                {
                    if (_window.CurrentIndex + i < _window.Playlist.Count) total++;
                }

                string title = "IMAGE INFO";
                if (total > 1)
                {
                    _window.TxtMetaTitle.Text = $"{title} ({_focusedPageIndex + 1}/{total})";
                }
                else
                {
                    _window.TxtMetaTitle.Text = title;
                }

                string filePath = _window.Playlist[index];

                // オフロードしてUIスレッドをブロックしないようにする
                var meta = await System.Threading.Tasks.Task.Run(() => MetadataService.GetMetadata(filePath));

                // 非同期処理中に表示状態やインデックスが変わった場合は破棄
                if (!_window.ViewModel.Viewer.IsMetadataVisible || _window.Playlist.Count == 0) return;
                int currentIndexAtReturn = _window.CurrentIndex + _focusedPageIndex;
                if (currentIndexAtReturn >= _window.Playlist.Count) currentIndexAtReturn = _window.CurrentIndex;
                if (index != currentIndexAtReturn) return;

                _window.TxtMetaFileName.Text = meta.FileName;
                _window.TxtMetaDimensions.Text = meta.Dimensions;
                _window.TxtMetaFileSize.Text = meta.FileSize;

                if (meta.HasExif)
                {
                    _window.ExifDivider.Visibility = Visibility.Visible;
                    _window.ExifGrid.Visibility = Visibility.Visible;
                    _window.TxtMetaCamera.Text = $"{meta.Make} {meta.Model}".Trim();
                    _window.TxtMetaLens.Text = meta.LensModel ?? "-";
                    _window.TxtMetaSettings.Text = $"{meta.FNumber}  {meta.ExposureTime}  ISO {meta.Iso}  {meta.FocalLength}".Trim();
                    _window.TxtMetaDate.Text = meta.DateTaken ?? "-";
                }
                else
                {
                    _window.ExifDivider.Visibility = Visibility.Collapsed;
                    _window.ExifGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                AppLog.Error("MetadataDisplayService", $"COMException in UpdateMetadataPanel: 0x{ex.HResult:X}", ex);
            }
            catch (Exception ex)
            {
                AppLog.Error("MetadataDisplayService", "Exception in UpdateMetadataPanel", ex);
            }
        }

        public void UpdateFocusBorders()
        {
            bool panelVisible = _window.ViewModel.Viewer.IsMetadataVisible;
            var focusControls = _window.ViewerControl.PageControlsBuffer[_window.ViewerControl.CurrentBufferIndex];

            for (int i = 0; i < 4; i++)
            {
                focusControls[i].FocusBorder.Visibility = (panelVisible && i == _focusedPageIndex) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Also hide borders in the inactive buffer
            var inactiveControls = _window.ViewerControl.PageControlsBuffer[_window.ViewerControl.InactiveBufferIndex];
            for (int i = 0; i < 4; i++)
            {
                inactiveControls[i].FocusBorder.Visibility = Visibility.Collapsed;
            }
        }

        public void HandlePointerMoved(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_window.ViewModel.Viewer.IsMetadataVisible || _window.Playlist.Count == 0) return;

            var pagesGrid = _window.PagesGrid; // Active buffer grid
            var point = e.GetCurrentPoint(pagesGrid).Position;
            int splitCount = _settings.MangaSplitCount;

            var pageGrids = _window.ViewerControl.PageControlsBuffer[_window.ViewerControl.CurrentBufferIndex];

            int hoveredIndex = -1;
            for (int i = 0; i < splitCount; i++)
            {
                if (_window.CurrentIndex + i >= _window.Playlist.Count) break;

                var grid = pageGrids[i];
                if (grid.Visibility == Visibility.Visible)
                {
                    try
                    {
                        var ttv = pagesGrid.TransformToVisual(grid);
                        var p = ttv.TransformPoint(point);
                        if (p.X >= 0 && p.X <= grid.ActualWidth && p.Y >= 0 && p.Y <= grid.ActualHeight)
                        {
                            hoveredIndex = i;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("MetadataDisplayService", "TransformToVisual failed", ex);
                    }
                }
            }

            if (hoveredIndex != -1 && hoveredIndex != _focusedPageIndex)
            {
                _focusedPageIndex = hoveredIndex;
                UpdateMetadataPanel();
            }
        }
    }
}
