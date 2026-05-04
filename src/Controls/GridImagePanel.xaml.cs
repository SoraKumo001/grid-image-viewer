using Microsoft.UI.Xaml.Controls;

namespace grid_image_viewer.Controls
{
    public sealed partial class GridImagePanel : UserControl
    {
        public GridView GridView => ImageGridView;

        public GridImagePanel()
        {
            this.InitializeComponent();
        }
    }
}