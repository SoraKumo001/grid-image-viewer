using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;

namespace quick_image_viewer.ViewModels
{
    public partial class ViewerViewModel : ObservableObject
    {
        public ICommand ToggleGridModeCommand { get; }
        public ICommand ToggleFullscreenCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomResetCommand { get; }
        public ICommand ToggleMetadataCommand { get; }
        public ICommand ToggleMangaModeCommand { get; }
        public ICommand ToggleStretchModeCommand { get; }
        public ICommand ViewModeCommand { get; }
        public ICommand LayoutModeCommand { get; }
        public ICommand StretchModeCommand { get; }

        [ObservableProperty] public partial bool IsViewSingle { get; set; }
        [ObservableProperty] public partial bool IsViewDouble { get; set; }
        [ObservableProperty] public partial bool IsViewQuad { get; set; }

        [ObservableProperty] public partial bool IsLayoutAuto { get; set; }
        [ObservableProperty] public partial bool IsLayoutHorz { get; set; }
        [ObservableProperty] public partial bool IsLayoutGrid { get; set; }

        [ObservableProperty] public partial bool IsStretchOriginal { get; set; }
        [ObservableProperty] public partial bool IsStretchContain { get; set; }
        [ObservableProperty] public partial bool IsStretchCover { get; set; }

        [ObservableProperty] public partial bool IsMetadataVisible { get; set; }

        public ViewerViewModel()
        {
            ToggleGridModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleGridMessage>());
            ToggleFullscreenCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<FullscreenMessage>());
            ZoomInCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.2f)));
            ZoomOutCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(1.0f / 1.2f)));
            ZoomResetCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ZoomMessage(0)));
            ToggleMetadataCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleMetadataMessage>());
            ToggleMangaModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleMangaMessage>());
            ToggleStretchModeCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send<ToggleStretchMessage>());
            ViewModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("ViewMode", int.Parse(val ?? "1"))));
            LayoutModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("LayoutMode", int.Parse(val ?? "0"))));
            StretchModeCommand = new RelayCommand<string>(val => WeakReferenceMessenger.Default.Send(new ViewActionMessage("StretchMode", int.Parse(val ?? "2"))));
        }
    }
}
