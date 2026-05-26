using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.ViewModels;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace quick_image_viewer.Interfaces
{
    public interface IGridHost
    {
        DispatcherQueue DispatcherQueue { get; }
        GridView ImageGridView { get; }
        MainViewModel ViewModel { get; }
        ObservableCollection<string> Playlist { get; }
        int CurrentIndex { get; set; }
        bool IsGridMode { get; set; }

        void UpdatePageIndicator();
        void SetGridLoading(bool isLoading, bool isBackground = false);
        Task UpdateDisplayAsync();
    }
}
