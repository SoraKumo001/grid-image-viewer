using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using System.Threading.Tasks;

namespace quick_image_viewer.Interfaces
{
    public interface ISlideshowHost
    {
        MainViewModel ViewModel { get; }
        UIElement Content { get; }
        bool IsFullscreen { get; set; }

        IViewerManager ViewerManager { get; }

        ContentDialog SlideshowDialog { get; }
        ComboBox SlideshowMangaSplitCount { get; }
        CheckBox SlideshowFullscreen { get; }
        CheckBox SlideshowRandom { get; }
        CheckBox SlideshowLoop { get; }
        CheckBox SlideshowNextFolder { get; }
        CheckBox SlideshowIncludeSiblings { get; }
        CheckBox SlideshowCurrentFolderOnly { get; }
        ComboBox SlideshowStretchMode { get; }
        CheckBox SlideshowCrossfade { get; }
        NumberBox SlideshowInterval { get; }
        NumberBox SlideshowCrossfadeDuration { get; }

        Task UpdateDisplayAsync();
    }
}
