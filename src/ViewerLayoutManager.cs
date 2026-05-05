using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace grid_image_viewer
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
            Controls.ViewerPageControl[] pageGrids,
            int splitCount,
            int effectiveSplitCount,
            int currentQuadLayout,
            bool isSlideshowRunning)
        {
            bool uniformToFill = (isSlideshowRunning && _settings.SlideshowUniformToFill);


            for (int i = 0; i < 4; i++)
            {
                pageGrids[i].PageImage.HorizontalAlignment = HorizontalAlignment.Center;
                pageGrids[i].PageImage.VerticalAlignment = VerticalAlignment.Center;
            }

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

                Grid.SetColumn(pageGrids[0], 1); Grid.SetRow(pageGrids[0], 0);
                Grid.SetColumnSpan(pageGrids[0], 1); Grid.SetRowSpan(pageGrids[0], 2);
                Grid.SetColumn(pageGrids[1], 0); Grid.SetRow(pageGrids[1], 0);
                Grid.SetColumnSpan(pageGrids[1], 1); Grid.SetRowSpan(pageGrids[1], 2);

                pageGrids[0].Visibility = Visibility.Visible;
                pageGrids[1].Visibility = Visibility.Visible;
                pageGrids[2].Visibility = Visibility.Collapsed;
                pageGrids[3].Visibility = Visibility.Collapsed;

                if (!uniformToFill)
                {
                    pageGrids[0].PageImage.HorizontalAlignment = HorizontalAlignment.Left;
                    pageGrids[1].PageImage.HorizontalAlignment = HorizontalAlignment.Right;
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
                        pageGrids[0].PageImage.VerticalAlignment = VerticalAlignment.Bottom;
                        pageGrids[1].PageImage.HorizontalAlignment = HorizontalAlignment.Left; pageGrids[1].PageImage.VerticalAlignment = VerticalAlignment.Top;
                        pageGrids[2].PageImage.HorizontalAlignment = HorizontalAlignment.Right; pageGrids[2].PageImage.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(1, GridUnitType.Star);
                    cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    Grid.SetColumn(pageGrids[0], 2); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 0); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
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

                    Grid.SetColumn(pageGrids[0], 3); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 2); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 1); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], 0); Grid.SetRow(pageGrids[3], 0); Grid.SetRowSpan(pageGrids[3], 2); Grid.SetColumnSpan(pageGrids[3], 1);
                }
                else
                {
                    cols[0].Width = new GridLength(1, GridUnitType.Star);
                    cols[1].Width = new GridLength(1, GridUnitType.Star);
                    cols[2].Width = new GridLength(0); cols[3].Width = new GridLength(0);
                    rows[0].Height = new GridLength(1, GridUnitType.Star);
                    rows[1].Height = new GridLength(1, GridUnitType.Star);

                    Grid.SetColumn(pageGrids[0], 1); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 1); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], 0); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 1); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], 1); Grid.SetRow(pageGrids[2], 1); Grid.SetRowSpan(pageGrids[2], 1); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], 0); Grid.SetRow(pageGrids[3], 1); Grid.SetRowSpan(pageGrids[3], 1); Grid.SetColumnSpan(pageGrids[3], 1);

                    if (!uniformToFill)
                    {
                        pageGrids[0].PageImage.HorizontalAlignment = HorizontalAlignment.Left; pageGrids[0].PageImage.VerticalAlignment = VerticalAlignment.Bottom;
                        pageGrids[1].PageImage.HorizontalAlignment = HorizontalAlignment.Right; pageGrids[1].PageImage.VerticalAlignment = VerticalAlignment.Bottom;
                        pageGrids[2].PageImage.HorizontalAlignment = HorizontalAlignment.Left; pageGrids[2].PageImage.VerticalAlignment = VerticalAlignment.Top;
                        pageGrids[3].PageImage.HorizontalAlignment = HorizontalAlignment.Right; pageGrids[3].PageImage.VerticalAlignment = VerticalAlignment.Top;
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
                        }
                    }
                    catch { }
                }
            }

            double a = validCount > 0 ? avgRatio / validCount : 0.75; // Default to portrait ratio if no valid images
            if (windowHeight <= 0) windowHeight = 1;
            if (windowWidth <= 0) windowWidth = 1;

            // Calculate scaled dimension for Horizontal (1)
            double s1 = System.Math.Min(windowWidth / (4 * a), windowHeight);

            // Calculate scaled dimension for 2x2 Grid (2)
            double s2 = System.Math.Min(windowWidth / (2 * a), windowHeight / 2);

            return s1 > s2 ? 1 : 2;
        }
    }
}
