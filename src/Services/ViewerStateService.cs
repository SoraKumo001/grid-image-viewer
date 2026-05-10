using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
namespace quick_image_viewer.Services
{
    public interface IViewerStateService
    {
        ObservableCollection<string> Playlist { get; set; }
        int CurrentIndex { get; set; }
        string CurrentDirectory { get; set; }
        bool IsGridMode { get; set; }
        bool IsSlideshowRunning { get; set; }
        bool IsSearchingFolder { get; set; }
        bool IsDisplayUpdating { get; set; }
        string CurrentImagePath { get; }
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

        [ObservableProperty]
        public partial bool IsSearchingFolder { get; set; } = false;

        [ObservableProperty]
        public partial bool IsDisplayUpdating { get; set; } = false;

        public string CurrentImagePath => (Playlist != null && CurrentIndex >= 0 && CurrentIndex < Playlist.Count) ? Playlist[CurrentIndex] : string.Empty;
    }
}
