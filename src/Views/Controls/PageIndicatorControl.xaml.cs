using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class PageIndicatorControl : UserControl
    {
        public MainViewModel ViewModel
        {
            get => (MainViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(PageIndicatorControl), new PropertyMetadata(null));

        public PageIndicatorControl()
        {
            this.InitializeComponent();
        }
    }
}