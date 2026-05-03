using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace grid_image_viewer
{
    internal class MetadataDisplayService
    {
        private readonly MainWindow _window;
        private readonly SettingsManager _settings;
        private int _focusedPageIndex = 0;
        private readonly Border[] _focusBorders;

        public MetadataDisplayService(MainWindow window, SettingsManager settings)
        {
            _window = window;
            _settings = settings;
            _focusBorders = new Border[] { _window.FocusBorder1, _window.FocusBorder2, _window.FocusBorder3, _window.FocusBorder4 };
        }

        public void ToggleMetadataPanel(bool cycle = true)
        {
            if (_window.MetadataPanel.Visibility == Visibility.Visible)
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
                        _window.MetadataPanel.Visibility = Visibility.Collapsed;
                        UpdateFocusBorders();
                    }
                    else
                    {
                        UpdateMetadataPanel();
                    }
                }
                else
                {
                    _window.MetadataPanel.Visibility = Visibility.Collapsed;
                    UpdateFocusBorders();
                }
            }
            else
            {
                _focusedPageIndex = 0;
                _window.MetadataPanel.Visibility = Visibility.Visible;
                UpdateMetadataPanel();
            }
        }

        public void UpdateMetadataPanel()
        {
            if (_window.MetadataPanel.Visibility != Visibility.Visible || _window.Playlist.Count == 0)
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
            var meta = MetadataService.GetMetadata(filePath);

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

        public void UpdateFocusBorders()
        {
            bool panelVisible = _window.MetadataPanel.Visibility == Visibility.Visible;
            for (int i = 0; i < 4; i++)
            {
                _focusBorders[i].Visibility = (panelVisible && i == _focusedPageIndex) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void HandlePointerMoved(Grid[] pageGrids, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_window.MetadataPanel.Visibility != Visibility.Visible || _window.Playlist.Count == 0) return;

            var point = e.GetCurrentPoint(_window.PagesGrid).Position;
            int splitCount = _settings.MangaSplitCount;

            int hoveredIndex = -1;
            for (int i = 0; i < splitCount; i++)
            {
                if (_window.CurrentIndex + i >= _window.Playlist.Count) break;

                var grid = pageGrids[i];
                if (grid.Visibility == Visibility.Visible)
                {
                    try
                    {
                        var ttv = _window.PagesGrid.TransformToVisual(grid);
                        var p = ttv.TransformPoint(point);
                        if (p.X >= 0 && p.X <= grid.ActualWidth && p.Y >= 0 && p.Y <= grid.ActualHeight)
                        {
                            hoveredIndex = i;
                            break;
                        }
                    }
                    catch { }
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
