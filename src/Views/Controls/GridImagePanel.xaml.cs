using Microsoft.UI.Xaml.Controls;
namespace quick_image_viewer.Views.Controls
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