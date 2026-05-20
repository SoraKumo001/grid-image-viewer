using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Views.Controls;
using System.Collections.Generic;
namespace quick_image_viewer.Interfaces
{
    public interface IViewerLayoutManager
    {
        void UpdateLayoutGrid(
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
            bool isSlideshowRunning);

        int GetEffectiveQuadLayout(int currentIndex, IList<string> playlist, double windowWidth, double windowHeight);
    }
}
