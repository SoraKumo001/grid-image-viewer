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
        private ObservableCollection<string> _playlist = new ObservableCollection<string>();

        [ObservableProperty]
        private int _currentIndex = -1;

        [ObservableProperty]
        private string _currentDirectory = string.Empty;

        [ObservableProperty]
        private bool _isGridMode = false;

        [ObservableProperty]
        private bool _isSlideshowRunning = false;
    }
}
