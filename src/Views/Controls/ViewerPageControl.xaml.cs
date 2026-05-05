using Microsoft.UI.Xaml.Controls;
namespace quick_image_viewer.Views.Controls
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