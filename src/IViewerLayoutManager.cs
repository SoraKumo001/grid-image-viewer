using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace grid_image_viewer
{
    public interface IViewerLayoutManager
    {
        void UpdateLayoutGrid(
            ColumnDefinition[] cols,
            RowDefinition[] rows,
            Controls.ViewerPageControl[] pageGrids,
            int splitCount,
            int effectiveSplitCount,
            int currentQuadLayout,
            bool isSlideshowRunning);

        int GetEffectiveQuadLayout(int currentIndex, IList<string> playlist, double windowWidth, double windowHeight);
    }
}
