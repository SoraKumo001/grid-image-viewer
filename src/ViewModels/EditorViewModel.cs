using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;

namespace quick_image_viewer.ViewModels
{
    public partial class EditorViewModel : ObservableObject
    {
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand OverwriteCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CropCommand { get; }
        public ICommand ResizeCommand { get; }
        public ICommand ToneCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand FlipHorzCommand { get; }
        public ICommand FlipVertCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand Rotate180Command { get; }
        public ICommand DeleteFileCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand RenameFileCommand { get; }
        public ICommand MoveFileCommand { get; }
        public ICommand OpenExplorerCommand { get; }

        [ObservableProperty] public partial bool CanUndo { get; set; }
        [ObservableProperty] public partial bool CanRedo { get; set; }
        [ObservableProperty] public partial bool HasValidPath { get; set; }
        [ObservableProperty] public partial bool IsImageEditable { get; set; }
        [ObservableProperty] public partial string ContextPath { get; set; } = string.Empty;

        public EditorViewModel()
        {
            UndoCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Undo", path ?? string.Empty)));
            RedoCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Redo", path ?? string.Empty)));
            OverwriteCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Overwrite", path ?? string.Empty)));
            SaveAsCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("SaveAs", path ?? string.Empty)));
            PrintCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Print", path ?? string.Empty)));
            CropCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Crop", path ?? string.Empty)));
            ResizeCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Resize", path ?? string.Empty)));
            ToneCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Tone", path ?? string.Empty)));
            FilterCommand = new RelayCommand<string>(filter => WeakReferenceMessenger.Default.Send(new EditActionMessage("Filter", ContextPath, filter)));
            FlipHorzCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Flip", path ?? string.Empty, "Horz")));
            FlipVertCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Flip", path ?? string.Empty, "Vert")));
            RotateRightCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, 90)));
            RotateLeftCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, -90)));
            Rotate180Command = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new EditActionMessage("Rotate", path ?? string.Empty, 180)));
            DeleteFileCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new DeleteFileMessage(path ?? string.Empty)));
            CopyPathCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new CopyPathMessage(path ?? string.Empty)));
            RenameFileCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new RenameFileMessage(path ?? string.Empty)));
            MoveFileCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new MoveFileMessage(path ?? string.Empty)));
            OpenExplorerCommand = new RelayCommand<string>(path => WeakReferenceMessenger.Default.Send(new ShellActionMessage("OpenExplorer", path ?? string.Empty)));
        }
    }
}
