using Microsoft.UI.Xaml.Controls;
namespace quick_image_viewer.Views.Controls
{
    public sealed partial class ViewerPageControl : UserControl
    {
        public Grid RootGrid => InternalRootGrid;
        public Image PageImage => InternalPageImage;
        public SkiaSharp.Views.Windows.SKXamlCanvas PageCanvas => InternalPageCanvas;
        public MediaPlayerElement PagePlayer => InternalMediaPlayer;
        public ProgressRing LoadingRing => InternalLoadingRing;
        public Border FocusBorder => InternalFocusBorder;

        public ViewerPageControl()
        {
            this.InitializeComponent();
        }

        public void ResetPlayback()
        {
            if (InternalMediaPlayer.MediaPlayer != null)
            {
                InternalMediaPlayer.MediaPlayer.Pause();
                InternalMediaPlayer.Source = null;
            }
            InternalMediaPlayer.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }
}