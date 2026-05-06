using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace quick_image_viewer.Views.Controls
{
    public sealed partial class WelcomePanelControl : UserControl
    {
        public event EventHandler? OpenFolderRequested;

        public WelcomePanelControl()
        {
            this.InitializeComponent();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}