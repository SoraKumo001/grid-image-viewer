using CommunityToolkit.Mvvm.Messaging;
using quick_image_viewer.Common;
using quick_image_viewer.Helpers;
using quick_image_viewer.Interfaces;
using quick_image_viewer.ViewModels;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
namespace quick_image_viewer.Services
{
    internal class ImageEditService : IImageEditService
    {
        private readonly IImageEditHost _window;
        private readonly Dictionary<string, EditSession> _pendingEdits = new Dictionary<string, EditSession>();

        public ImageEditService(IImageEditHost window)
        {
            _window = window;
        }

        public Dictionary<string, EditSession> PendingEdits => _pendingEdits;

        public void AddPendingEdit(string path, SKBitmap bitmap)
        {
            if (!_pendingEdits.TryGetValue(path, out var session))
            {
                session = new EditSession();
                _pendingEdits[path] = session;
            }
            session.AddState(bitmap);
        }

        public void ReplaceCurrentEdit(string path, SKBitmap bitmap)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                session.ReplaceCurrentState(bitmap);
            }
            else
            {
                AddPendingEdit(path, bitmap);
            }
        }

        public void RenameSession(string oldPath, string newPath)
        {
            if (_pendingEdits.TryGetValue(oldPath, out var session))
            {
                _pendingEdits.Remove(oldPath);
                _pendingEdits[newPath] = session;
            }
        }

        public void ClearEdits(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                session.Dispose();
                _pendingEdits.Remove(path);
            }
        }

        public void ClearAll()
        {
            foreach (var session in _pendingEdits.Values) session.Dispose();
            _pendingEdits.Clear();
        }

        public void UndoEdit(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                if (session.Undo())
                {
                    WeakReferenceMessenger.Default.Send(new EditActionCompletedMessage(path, session.Current));
                }
            }
        }

        public void RedoEdit(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                if (session.Redo())
                {
                    WeakReferenceMessenger.Default.Send(new EditActionCompletedMessage(path, session.Current));
                }
            }
        }

        public async Task RotateAsync(string path, float degrees)
        {
            await ApplyTransformationAsync(path, (current) => ImageProcessor.GetRotatedBitmap(path, degrees, current));
        }

        public async Task FlipAsync(string path, bool horizontal)
        {
            await ApplyTransformationAsync(path, (current) => ImageProcessor.GetFlippedBitmap(path, horizontal, current));
        }

        public async Task FilterAsync(string path, string filterType)
        {
            await ApplyTransformationAsync(path, (current) => ImageProcessor.ApplyFilter(path, filterType, current));
        }

        public async Task ResizeAsync(string path, int width, int height)
        {
            await ApplyTransformationAsync(path, (current) => ImageProcessor.GetResizedBitmap(path, width, height, current));
        }

        public async Task CropAsync(string path, SKRectI region)
        {
            await ApplyTransformationAsync(path, (current) => ImageProcessor.GetCroppedBitmap(path, region, current));
        }

        public SKBitmap? GetCurrentBitmap(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                return session.Current;
            }
            return null;
        }

        public EditSession? GetSession(string path)
        {
            _pendingEdits.TryGetValue(path, out var session);
            return session;
        }

        public (int width, int height) GetImageSize(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session) && session.Current != null)
            {
                return (session.Current.Width, session.Current.Height);
            }
            return ImageProcessor.GetImageSize(path);
        }

        public void CleanupSessions(IEnumerable<string> activePaths)
        {
            var activeSet = new HashSet<string>(activePaths);
            var keysToRemove = new List<string>();
            foreach (var key in _pendingEdits.Keys)
            {
                if (!activeSet.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _pendingEdits[key].Dispose();
                _pendingEdits.Remove(key);
            }
        }

        private async Task<SKBitmap?> LoadOriginalBitmapAsync(string path)
        {
            try
            {
                if (quick_image_viewer.Managers.PdfManager.IsPdfPath(path))
                {
                    var (actualPath, pageIndex) = quick_image_viewer.Managers.PdfManager.SplitVirtualPath(path);
                    var stream = await quick_image_viewer.Managers.PdfManager.RenderPageToStreamAsync(actualPath, pageIndex);
                    if (stream != null)
                    {
                        using (stream)
                        using (var netStream = stream.AsStreamForRead())
                        using (var ms = new MemoryStream())
                        {
                            await netStream.CopyToAsync(ms);
                            return SKBitmap.Decode(ms.ToArray());
                        }
                    }
                    return null;
                }
                if (quick_image_viewer.Managers.ArchiveManager.IsArchivePath(path))
                {
                    var (arc, ent) = quick_image_viewer.Managers.ArchiveManager.SplitArchivePath(path);
                    var bytesArc = quick_image_viewer.Managers.ArchiveManager.GetEntryBytes(arc, ent);
                    if (bytesArc != null) return SKBitmap.Decode(bytesArc);
                    return null;
                }

                byte[] bytes = await System.IO.File.ReadAllBytesAsync(path);
                return SKBitmap.Decode(bytes);
            }
            catch (Exception ex)
            {
                AppLog.Error("ImageEditService", $"LoadOriginalBitmapAsync failed for '{path}'", ex);
                return null;
            }
        }

        public async Task ApplyTransformationAsync(string path, Func<SKBitmap?, SKBitmap?> transform)
        {
            _window.ViewerManager?.StopAnimation();

            // Clear current image sources to prevent access violations during update
            _window.ViewerManager?.ClearPageImageForPath(path);

            SKBitmap? baseBmp = GetCurrentBitmap(path);
            bool isNewSession = baseBmp == null;
            if (isNewSession)
            {
                baseBmp = await LoadOriginalBitmapAsync(path);
            }

            if (baseBmp == null) return;

            var newBmp = await Task.Run(() => transform(baseBmp));

            if (newBmp != null)
            {
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    if (isNewSession)
                    {
                        // Add original so we can undo back to it
                        AddPendingEdit(path, baseBmp);
                    }

                    AddPendingEdit(path, newBmp);
                    WeakReferenceMessenger.Default.Send(new EditActionCompletedMessage(path, newBmp));
                });
            }
        }
    }

    public class EditSession : IDisposable
    {
        private readonly List<SKBitmap> _history = new List<SKBitmap>();
        private int _currentIndex = -1;

        public SKBitmap? Current => (_currentIndex >= 0 && _currentIndex < _history.Count) ? _history[_currentIndex] : null;
        public bool CanUndo => _currentIndex > 0;
        public bool CanRedo => _currentIndex < _history.Count - 1;

        public void AddState(SKBitmap bitmap)
        {
            // Remove redo history
            while (_history.Count > _currentIndex + 1)
            {
                _history[^1].Dispose();
                _history.RemoveAt(_history.Count - 1);
            }

            _history.Add(bitmap);
            _currentIndex++;

            // Limit history size
            if (_history.Count > 10)
            {
                _history[0].Dispose();
                _history.RemoveAt(0);
                _currentIndex--;
            }
        }

        public void ReplaceCurrentState(SKBitmap bitmap)
        {
            if (_currentIndex >= 0 && _currentIndex < _history.Count)
            {
                _history[_currentIndex].Dispose();
                _history[_currentIndex] = bitmap;
            }
            else
            {
                AddState(bitmap);
            }
        }

        public bool Undo()
        {
            if (CanUndo)
            {
                _currentIndex--;
                return true;
            }
            return false;
        }

        public bool Redo()
        {
            if (CanRedo)
            {
                _currentIndex++;
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            foreach (var bmp in _history) bmp.Dispose();
            _history.Clear();
            _currentIndex = -1;
        }
    }
}
