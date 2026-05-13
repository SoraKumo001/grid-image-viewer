using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
namespace quick_image_viewer.Managers
{
    internal class ViewerLayoutManager : IViewerLayoutManager
    {
        private readonly ISettingsManager _settings;

        public ViewerLayoutManager(ISettingsManager settings)
        {
            _settings = settings;
        }

        public void UpdateLayoutGrid(
            ColumnDefinition[] cols,
            RowDefinition[] rows,
            ViewerPageControl[] pageGrids,
            int splitCount,
            int effectiveSplitCount,
            int currentQuadLayout,
            bool isSlideshowRunning)
        {
            bool uniformToFill = (isSlideshowRunning && (_settings.SlideshowUniformToFill || _settings.ImageStretchMode == 3));


            for (int i = 0; i < 4; i++) { cols[i].Width = new GridLength(0); rows[0].Height = new GridLength(0); rows[1].Height = new GridLength(0); pageGrids[i].Visibility = Visibility.Collapsed; Grid.SetColumnSpan(pageGrids[i], 1); Grid.SetRowSpan(pageGrids[i], 1); pageGrids[i].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center); }

            if (effectiveSplitCount == 1)
            {
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(0); cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                Grid.SetColumn(pageGrids[0], 0); Grid.SetRow(pageGrids[0], 0);
                Grid.SetColumnSpan(pageGrids[0], 4); Grid.SetRowSpan(pageGrids[0], 2);
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Collapsed;
                pageGrids[2].Visibility = Visibility.Collapsed;
                pageGrids[3].Visibility = Visibility.Collapsed;
            }
            else if (effectiveSplitCount == 2)
            {
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(1, GridUnitType.Star);
                cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                bool rtl = _settings.IsRightToLeft;
                Grid.SetColumn(pageGrids[0], rtl ? 1 : 0); Grid.SetRow(pageGrids[0], 0);
                Grid.SetColumnSpan(pageGrids[0], 1); Grid.SetRowSpan(pageGrids[0], 2);
                Grid.SetColumn(pageGrids[1], rtl ? 0 : 1); Grid.SetRow(pageGrids[1], 0);
                Grid.SetColumnSpan(pageGrids[1], 1); Grid.SetRowSpan(pageGrids[1], 2);
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Collapsed;
                pageGrids[3].Visibility = Visibility.Collapsed;
                if (!uniformToFill)
                {
                    pageGrids[0].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Center);
                    pageGrids[1].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
                }
            }
            else if (effectiveSplitCount == 3)
            {
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Visible;
                pageGrids[3].Visibility = Visibility.Collapsed;

                if (currentQuadLayout == 2)
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star);
                    rows[1].Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(pageGrids[0], 0); Grid.SetRow(pageGrids[0], 0);
                    Grid.SetColumnSpan(pageGrids[0], 2); Grid.SetRowSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 1); Grid.SetRow(pageGrids[1], 1);
                    Grid.SetColumnSpan(pageGrids[1], 1); Grid.SetRowSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 0); Grid.SetRow(pageGrids[2], 1);
                    Grid.SetColumnSpan(pageGrids[2], 1); Grid.SetRowSpan(pageGrids[2], 1);

                    if (!uniformToFill)
                    {
                        pageGrids[0].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Bottom);
                        pageGrids[1].SetContentAlignment(HorizontalAlignment.Left, VerticalAlignment.Top);
                        pageGrids[2].SetContentAlignment(HorizontalAlignment.Right, VerticalAlignment.Top);
                    }
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(1, GridUnitType.Star);
                    cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    bool rtl = _settings.IsRightToLeft;
                    Grid.SetColumn(pageGrids[0], rtl ? 2 : 0); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], rtl ? 0 : 2); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                    if (!uniformToFill)
                    {
                        pageGrids[0].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Center);
                        pageGrids[1].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
                        pageGrids[2].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
                    }
                }
            }
            else
            {
                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Visible;
                pageGrids[3].Visibility = Visibility.Visible;

                if (currentQuadLayout == 1 || currentQuadLayout == 0)
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(1, GridUnitType.Star);
                    cols[3].Width = new GridLength(1, GridUnitType.Star);
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    bool rtl = _settings.IsRightToLeft;
                    Grid.SetColumn(pageGrids[0], rtl ? 3 : 0); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], rtl ? 2 : 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], rtl ? 1 : 2); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], rtl ? 0 : 3); Grid.SetRow(pageGrids[3], 0); Grid.SetRowSpan(pageGrids[3], 2); Grid.SetColumnSpan(pageGrids[3], 1);
                    if (!uniformToFill)
                    {
                        pageGrids[0].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Center);
                        pageGrids[1].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
                        pageGrids[2].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Center);
                        pageGrids[3].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
                    }
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star);
                    rows[1].Height = new GridLength(1, GridUnitType.Star);

                    bool rtl = _settings.IsRightToLeft;
                    Grid.SetColumn(pageGrids[0], rtl ? 1 : 0); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 1); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], rtl ? 0 : 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 1); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], rtl ? 1 : 0); Grid.SetRow(pageGrids[2], 1); Grid.SetRowSpan(pageGrids[2], 1); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], rtl ? 0 : 1); Grid.SetRow(pageGrids[3], 1); Grid.SetRowSpan(pageGrids[3], 1); Grid.SetColumnSpan(pageGrids[3], 1);
                    if (!uniformToFill)
                    {
                        pageGrids[0].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Bottom);
                        pageGrids[1].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Bottom);
                        pageGrids[2].SetContentAlignment(rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Top);
                        pageGrids[3].SetContentAlignment(rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Top);
                    }
                }
            }
        }

        public int GetEffectiveQuadLayout(int currentIndex, System.Collections.Generic.IList<string> playlist, double windowWidth, double windowHeight)
        {
            int layout = _settings.QuadLayoutMode;
            if (_settings.MangaSplitCount != 4 || layout != 0) return layout;

            double avgRatio = 0;
            int validCount = 0;
            int landscapeCount = 0;
            int portraitCount = 0;

            for (int i = 0; i < 4; i++)
            {
                int indexToLoad = -1;
                if (i == 0) indexToLoad = currentIndex;
                else if (currentIndex + i < playlist.Count) indexToLoad = currentIndex + i;

                if (indexToLoad != -1 && indexToLoad < playlist.Count)
                {
                    try
                    {
                        var (w, h) = ImageProcessor.GetImageSize(playlist[indexToLoad]);
                        if (w > 0 && h > 0)
                        {
                            avgRatio += (double)w / h;
                            validCount++;

                            if (w > h) landscapeCount++;
                            else portraitCount++;
                        }
                    }
                    catch { }
                }
            }

            // Priority 1: Majority rule
            if (landscapeCount > portraitCount) return 2; // Grid (2x2) is better for wide images
            if (portraitCount > landscapeCount) return 1; // Horizontal (1x4) is often preferred for tall images

            // Priority 2: Fallback to mathematical best fit if tied or no valid counts
            double a = validCount > 0 ? avgRatio / validCount : 0.75; // Default to portrait ratio
            if (windowHeight <= 0) windowHeight = 1;
            if (windowWidth <= 0) windowWidth = 1;

            double s1 = System.Math.Min(windowWidth / (4 * a), windowHeight);
            double s2 = System.Math.Min(windowWidth / (2 * a), windowHeight / 2);

            return s1 > s2 ? 1 : 2;
        }
    }
}


