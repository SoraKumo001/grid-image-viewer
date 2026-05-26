using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Common;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Views.Controls;
using System;
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
            Grid grid,
            ColumnDefinition[] cols,
            RowDefinition[] rows,
            ViewerPageControl[] pageGrids,
            double[] aspectRatios,
            double viewportWidth,
            double viewportHeight,
            int splitCount,
            int effectiveSplitCount,
            int currentQuadLayout,
            bool isSlideshowRunning)
        {
            bool uniformToFill = (isSlideshowRunning && (_settings.SlideshowUniformToFill || _settings.ImageStretchMode == 3));

            // Default alignment and width for normal layout
            grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            grid.Width = double.NaN;

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
                    rows[0].Height = new GridLength(1, GridUnitType.Star); rows[1].Height = new GridLength(0);

                    bool rtl = _settings.IsRightToLeft;
                    Grid.SetColumn(pageGrids[0], rtl ? 3 : 0); Grid.SetRow(pageGrids[0], 0); Grid.SetRowSpan(pageGrids[0], 2); Grid.SetColumnSpan(pageGrids[0], 1);
                    Grid.SetColumn(pageGrids[1], rtl ? 2 : 1); Grid.SetRow(pageGrids[1], 0); Grid.SetRowSpan(pageGrids[1], 2); Grid.SetColumnSpan(pageGrids[1], 1);
                    Grid.SetColumn(pageGrids[2], rtl ? 1 : 2); Grid.SetRow(pageGrids[2], 0); Grid.SetRowSpan(pageGrids[2], 2); Grid.SetColumnSpan(pageGrids[2], 1);
                    Grid.SetColumn(pageGrids[3], rtl ? 0 : 3); Grid.SetRow(pageGrids[3], 0); Grid.SetRowSpan(pageGrids[3], 2); Grid.SetColumnSpan(pageGrids[3], 1);

                    if (!uniformToFill)
                    {
                        // Reset image alignments inside grid cells to center, as spacing is handled by column widths.
                        pageGrids[0].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
                        pageGrids[1].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
                        pageGrids[2].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
                        pageGrids[3].SetContentAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

                        double rSum = 0;
                        for (int i = 0; i < effectiveSplitCount; i++)
                        {
                            double ratio = (aspectRatios != null && i < aspectRatios.Length) ? aspectRatios[i] : 0.75;
                            rSum += ratio;
                        }
                        if (rSum <= 0) rSum = 0.75 * effectiveSplitCount;

                        double wView = viewportWidth;
                        double hView = viewportHeight;
                        if (wView <= 0) wView = 1;
                        if (hView <= 0) hView = 1;

                        bool isHeightLimited = (wView / hView) > rSum;

                        if (isHeightLimited)
                        {
                            // Height limited: Align grid to center and set explicit width based on total image width at viewport height.
                            grid.HorizontalAlignment = HorizontalAlignment.Center;
                            grid.Width = hView * rSum;

                            for (int i = 0; i < 4; i++)
                            {
                                if (i < effectiveSplitCount)
                                {
                                    double ratio = (aspectRatios != null && i < aspectRatios.Length) ? aspectRatios[i] : 0.75;
                                    cols[i].Width = new GridLength(hView * ratio);
                                }
                                else
                                {
                                    cols[i].Width = new GridLength(0);
                                }
                            }
                        }
                        else
                        {
                            // Width limited: Let grid stretch to fill width and allocate column widths proportionally using Star.
                            grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                            grid.Width = double.NaN;

                            for (int i = 0; i < 4; i++)
                            {
                                if (i < effectiveSplitCount)
                                {
                                    double ratio = (aspectRatios != null && i < aspectRatios.Length) ? aspectRatios[i] : 0.75;
                                    cols[i].Width = new GridLength(ratio, GridUnitType.Star);
                                }
                                else
                                {
                                    cols[i].Width = new GridLength(0);
                                }
                            }
                        }
                    }
                    else
                    {
                        cols[0].Width = new GridLength(1, GridUnitType.Star);
                        cols[1].Width = new GridLength(1, GridUnitType.Star);
                        cols[2].Width = new GridLength(1, GridUnitType.Star);
                        cols[3].Width = new GridLength(1, GridUnitType.Star);
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

            int validCount = 0;
            int horizontalVotes = 0;
            int gridVotes = 0;
            double horizontalAreaTotal = 0;
            double gridAreaTotal = 0;

            if (windowHeight <= 0) windowHeight = 1;
            if (windowWidth <= 0) windowWidth = 1;

            double horizontalCellWidth = windowWidth / 4;
            double horizontalCellHeight = windowHeight;
            double gridCellWidth = windowWidth / 2;
            double gridCellHeight = windowHeight / 2;

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
                            double ratio = (double)w / h;
                            double horizontalArea = GetContainedArea(ratio, horizontalCellWidth, horizontalCellHeight);
                            double gridArea = GetContainedArea(ratio, gridCellWidth, gridCellHeight);

                            horizontalAreaTotal += horizontalArea;
                            gridAreaTotal += gridArea;
                            validCount++;

                            if (horizontalArea > gridArea) horizontalVotes++;
                            else if (gridArea > horizontalArea) gridVotes++;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("ViewerLayoutManager", $"GetImageSize failed for '{playlist[indexToLoad]}'", ex);
                    }
                }
            }

            if (horizontalVotes > gridVotes) return 1;
            if (gridVotes > horizontalVotes) return 2;

            if (validCount > 0)
            {
                return horizontalAreaTotal >= gridAreaTotal ? 1 : 2;
            }

            // Default to portrait-oriented content when no dimensions are available.
            double defaultRatio = 0.75;
            double defaultHorizontalArea = GetContainedArea(defaultRatio, horizontalCellWidth, horizontalCellHeight);
            double defaultGridArea = GetContainedArea(defaultRatio, gridCellWidth, gridCellHeight);
            return defaultHorizontalArea >= defaultGridArea ? 1 : 2;
        }

        private static double GetContainedArea(double imageRatio, double cellWidth, double cellHeight)
        {
            double renderedWidth = System.Math.Min(cellWidth, cellHeight * imageRatio);
            double renderedHeight = renderedWidth / imageRatio;
            return renderedWidth * renderedHeight;
        }
    }
}


