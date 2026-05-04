using Microsoft.UI.Xaml.Controls;
using SkiaSharp.Views.Windows;

namespace grid_image_viewer.Controls
{
    public sealed partial class ViewerPanel : UserControl
    {
        public Grid[] PageGrids { get; private set; }
        public Grid[] PrevContainers { get; private set; }
        public Grid[] CurrentContainers { get; private set; }
        public Image[] PrevImages { get; private set; }
        public Image[] PageImages { get; private set; }
        public SKXamlCanvas[] PageCanvases { get; private set; }
        public ProgressRing[] PageLoadingRings { get; private set; }
        public Border[] FocusBorders { get; private set; }
        public ColumnDefinition[] Cols { get; private set; }
        public RowDefinition[] Rows { get; private set; }

        public ScrollViewer ScrollViewer => ImageScrollViewer;
        public Grid RootPagesGrid => PagesGrid;
        public Grid Overlay => OverlayGrid;

        public ViewerPanel()
        {
            this.InitializeComponent();

            PageGrids = new Grid[] { PageGrid1, PageGrid2, PageGrid3, PageGrid4 };
            PrevContainers = new Grid[] { PrevContainer1, PrevContainer2, PrevContainer3, PrevContainer4 };
            CurrentContainers = new Grid[] { CurrentContainer1, CurrentContainer2, CurrentContainer3, CurrentContainer4 };
            PrevImages = new Image[] { Image1_Prev, Image2_Prev, Image3_Prev, Image4_Prev };
            PageImages = new Image[] { Image1, Image2, Image3, Image4 };
            PageCanvases = new SKXamlCanvas[] { Canvas1, Canvas2, Canvas3, Canvas4 };
            PageLoadingRings = new ProgressRing[] { LoadingRing1, LoadingRing2, LoadingRing3, LoadingRing4 };
            FocusBorders = new Border[] { FocusBorder1, FocusBorder2, FocusBorder3, FocusBorder4 };
            Cols = new ColumnDefinition[] { Col0, Col1, Col2, Col3 };
            Rows = new RowDefinition[] { Row0, Row1 };

            // Register PaintSurface events
            Canvas1.PaintSurface += (s, e) => OnPaintSurface(0, e);
            Canvas2.PaintSurface += (s, e) => OnPaintSurface(1, e);
            Canvas3.PaintSurface += (s, e) => OnPaintSurface(2, e);
            Canvas4.PaintSurface += (s, e) => OnPaintSurface(3, e);
        }

        private void OnPaintSurface(int index, SKPaintSurfaceEventArgs e)
        {
            PaintSurfaceRequested?.Invoke(this, (index, e));
        }

        public event System.EventHandler<(int index, SKPaintSurfaceEventArgs args)>? PaintSurfaceRequested;
    }
}