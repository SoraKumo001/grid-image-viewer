using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace grid_image_viewer
{
    public interface IViewerStateService
    {
        ObservableCollection<string> Playlist { get; set; }
        int CurrentIndex { get; set; }
        string CurrentDirectory { get; set; }
        bool IsGridMode { get; set; }
        bool IsSlideshowRunning { get; set; }
    }

    public partial class ViewerStateService : ObservableObject, IViewerStateService
    {
        [ObservableProperty]
        public partial ObservableCollection<string> Playlist { get; set; } = new ObservableCollection<string>();

        [ObservableProperty]
        public partial int CurrentIndex { get; set; } = -1;

        [ObservableProperty]
        public partial string CurrentDirectory { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsGridMode { get; set; } = false;

        [ObservableProperty]
        public partial bool IsSlideshowRunning { get; set; } = false;
    }
}
