using Microsoft.UI.Xaml.Controls;
using SkiaSharp.Views.Windows;
using System;

namespace grid_image_viewer.Controls
{
    public sealed partial class ViewerPanel : UserControl
    {
        public Grid[] PagesGrids { get; private set; }

        // Data per buffer
        public Grid[][] PageGridsBuffer { get; private set; }
        public Image[][] PageImagesBuffer { get; private set; }
        public SKXamlCanvas[][] PageCanvasesBuffer { get; private set; }
        public ProgressRing[][] PageLoadingRingsBuffer { get; private set; }
        public Border[][] FocusBordersBuffer { get; private set; }
        public ColumnDefinition[][] ColsBuffer { get; private set; }
        public RowDefinition[][] RowsBuffer { get; private set; }

        public int CurrentBufferIndex { get; set; } = 0;
        public int InactiveBufferIndex => (CurrentBufferIndex + 1) % 2;

        public Grid CurrentBuffer => PagesGrids[CurrentBufferIndex];
        public Grid InactiveBuffer => PagesGrids[InactiveBufferIndex];

        public ScrollViewer ScrollViewer => ImageScrollViewer;
        public Grid RootPagesGrid => PagesGrid1; // Keep for backward compatibility if needed, but we should use CurrentBuffer
        public Grid Overlay => OverlayGrid;

        public ViewerPanel()
        {
            this.InitializeComponent();

            PagesGrids = new Grid[] { PagesGrid1, PagesGrid2 };

            PageGridsBuffer = new Grid[][]
            {
                new Grid[] { B1_PageGrid1, B1_PageGrid2, B1_PageGrid3, B1_PageGrid4 },
                new Grid[] { B2_PageGrid1, B2_PageGrid2, B2_PageGrid3, B2_PageGrid4 }
            };

            PageImagesBuffer = new Image[][]
            {
                new Image[] { B1_Image1, B1_Image2, B1_Image3, B1_Image4 },
                new Image[] { B2_Image1, B2_Image2, B2_Image3, B2_Image4 }
            };

            PageCanvasesBuffer = new SKXamlCanvas[][]
            {
                new SKXamlCanvas[] { B1_Canvas1, B1_Canvas2, B1_Canvas3, B1_Canvas4 },
                new SKXamlCanvas[] { B2_Canvas1, B2_Canvas2, B2_Canvas3, B2_Canvas4 }
            };

            PageLoadingRingsBuffer = new ProgressRing[][]
            {
                new ProgressRing[] { B1_LoadingRing1, B1_LoadingRing2, B1_LoadingRing3, B1_LoadingRing4 },
                new ProgressRing[] { B2_LoadingRing1, B2_LoadingRing2, B2_LoadingRing3, B2_LoadingRing4 }
            };

            FocusBordersBuffer = new Border[][]
            {
                new Border[] { B1_FocusBorder1, B1_FocusBorder2, B1_FocusBorder3, B1_FocusBorder4 },
                new Border[] { B2_FocusBorder1, B2_FocusBorder2, B2_FocusBorder3, B2_FocusBorder4 }
            };

            ColsBuffer = new ColumnDefinition[][]
            {
                new ColumnDefinition[] { B1_Col0, B1_Col1, B1_Col2, B1_Col3 },
                new ColumnDefinition[] { B2_Col0, B2_Col1, B2_Col2, B2_Col3 }
            };

            RowsBuffer = new RowDefinition[][]
            {
                new RowDefinition[] { B1_Row0, B1_Row1 },
                new RowDefinition[] { B2_Row0, B2_Row1 }
            };

            // Register PaintSurface events for all buffers
            for (int b = 0; b < 2; b++)
            {
                int bufferIndex = b;
                for (int p = 0; p < 4; p++)
                {
                    int pageIndex = p;
                    PageCanvasesBuffer[b][p].PaintSurface += (s, e) => OnPaintSurface(bufferIndex, pageIndex, e);
                }
            }
        }

        private void OnPaintSurface(int bufferIndex, int pageIndex, SKPaintSurfaceEventArgs e)
        {
            PaintSurfaceRequested?.Invoke(this, (bufferIndex, pageIndex, e));
        }

        public event EventHandler<(int bufferIndex, int pageIndex, SKPaintSurfaceEventArgs args)>? PaintSurfaceRequested;
    }
}