using Microsoft.UI.Xaml.Controls;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using System;
using System.Threading.Tasks;

namespace quick_image_viewer.Services
{
    internal class FileOperationService : IFileOperationService
    {
        private readonly IMainView _window;
        private readonly ISettingsManager _settings;

        public FileOperationService(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
        }

        public async Task HandleDeleteFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var dialog = new ContentDialog
            {
                Title = _settings.GetString("DeleteDialog_Title"),
                Content = string.Format(_settings.GetString("DeleteDialog_Content"), System.IO.Path.GetFileName(path)),
                PrimaryButtonText = _settings.GetString("DeleteDialog_Primary"),
                CloseButtonText = _settings.GetString("DeleteDialog_Close"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _window.Content?.XamlRoot
            };

            _window.IsDialogOpen = true;
            var result = await dialog.ShowAsync();
            _window.IsDialogOpen = false;

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                        _window.PlaylistManager.RemoveFromPlaylist(path);
                        _window.ShowNotification(_settings.GetString("Notification_FileDeleted"));
                    }
                }
                catch (Exception ex)
                {
                    _window.ShowNotification("Error: " + ex.Message);
                }
            }
        }

        public async Task HandleRenameFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var textBox = new TextBox
            {
                Text = System.IO.Path.GetFileNameWithoutExtension(path),
                Header = _settings.GetString("RenameDialog_Label"),
                SelectionStart = 0,
                SelectionLength = System.IO.Path.GetFileNameWithoutExtension(path).Length
            };

            var dialog = new ContentDialog
            {
                Title = _settings.GetString("RenameDialog_Title"),
                Content = textBox,
                PrimaryButtonText = _settings.GetString("RenameDialog_PrimaryButton"),
                CloseButtonText = _settings.GetString("RenameDialog_CloseButton"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _window.Content?.XamlRoot
            };

            _window.IsDialogOpen = true;
            var result = await dialog.ShowAsync();
            _window.IsDialogOpen = false;

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    string oldName = System.IO.Path.GetFileName(path);
                    string newName = textBox.Text + System.IO.Path.GetExtension(path);
                    if (oldName == newName) return;

                    string directory = System.IO.Path.GetDirectoryName(path)!;
                    string newPath = System.IO.Path.Combine(directory, newName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Move(path, newPath);
                        _window.ImageEditService.RenameSession(path, newPath);
                        _window.ViewerManager.ReplacePath(path, newPath);
                        _window.PlaylistManager.ReplaceInPlaylist(path, newPath);
                        _window.ShowNotification(_settings.GetString("Notification_Renamed"));
                    }
                }
                catch (Exception ex)
                {
                    _window.ShowNotification("Error: " + ex.Message);
                }
            }
        }

        public async Task HandleMoveFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || ArchiveManager.IsArchivePath(path)) return;

            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _window.WindowHandle);
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    string destPath = System.IO.Path.Combine(folder.Path, fileName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Move(path, destPath);
                        _window.PlaylistManager.RemoveFromPlaylist(path);
                        _window.ShowNotification(_settings.GetString("Notification_Moved"));
                    }
                }
                catch (Exception ex)
                {
                    _window.ShowNotification("Error: " + ex.Message);
                }
            }
        }
    }
}