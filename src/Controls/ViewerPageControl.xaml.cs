using Microsoft.UI.Xaml.Controls;

namespace grid_image_viewer.Controls
{
    public sealed partial class ViewerPageControl : UserControl
    {
        public Grid RootGrid => InternalRootGrid;
        public Image PageImage => InternalPageImage;
        public SkiaSharp.Views.Windows.SKXamlCanvas PageCanvas => InternalPageCanvas;
        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        public ViewerPageControl()
        {
            this.InitializeComponent();
        }
    }
}