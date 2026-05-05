using Microsoft.UI.Xaml.Controls;
using SkiaSharp.Views.Windows;
using System;

namespace grid_image_viewer.Controls
{
    public sealed partial class ViewerPanel : UserControl
    {
        public Grid[] PagesGrids { get; private set; }

        // Data per buffer
        public ViewerPageControl[][] PageControlsBuffer { get; private set; }

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

            PageControlsBuffer = new ViewerPageControl[][]
            {
                new ViewerPageControl[] { B1_Page1, B1_Page2, B1_Page3, B1_Page4 },
                new ViewerPageControl[] { B2_Page1, B2_Page2, B2_Page3, B2_Page4 }
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
                    PageControlsBuffer[b][p].PageCanvas.PaintSurface += (s, e) => OnPaintSurface(bufferIndex, pageIndex, e);
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