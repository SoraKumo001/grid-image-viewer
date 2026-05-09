using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using System;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class TopOperationPanelControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(MainViewModel), typeof(TopOperationPanelControl), new PropertyMetadata(null));

        public MainViewModel ViewModel
        {
            get => (MainViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public event RoutedEventHandler? OpenFolderRequested;
        public event EventHandler? PanelHoverStarted;
        public event EventHandler? PanelHoverEnded;


        public TopOperationPanelControl()
        {
            this.InitializeComponent();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderRequested?.Invoke(this, e);
        }

        private void Border_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PanelHoverStarted?.Invoke(this, EventArgs.Empty);
        }

        private void Border_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PanelHoverEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
