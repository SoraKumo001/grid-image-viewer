using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    internal class ImageEditService
    {
        private readonly MainWindow _window;
        private readonly Dictionary<string, EditSession> _pendingEdits = new Dictionary<string, EditSession>();

        public ImageEditService(MainWindow window)
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
                    _window.ViewerManager?.StopAnimation();
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public void RedoEdit(string path)
        {
            if (_pendingEdits.TryGetValue(path, out var session))
            {
                if (session.Redo())
                {
                    _window.ViewerManager?.StopAnimation();
                    _ = _window.UpdateDisplayAsync();
                }
            }
        }

        public async Task ApplyTransformationAsync(string path, Func<SKBitmap?, SKBitmap?> transform)
        {
            _window.ViewerManager?.StopAnimation();

            // Clear current image sources to prevent access violations during update
            if (_window.ViewerManager != null)
            {
                foreach (var img in _window.ViewerManager.PageImages) img.Source = null;
            }

            await Task.Run(() =>
            {
                SKBitmap? baseBmp = null;
                if (_pendingEdits.TryGetValue(path, out var session))
                {
                    baseBmp = session.Current;
                }

                var newBmp = transform(baseBmp);
                if (newBmp != null)
                {
                    _window.DispatcherQueue.TryEnqueue(() =>
                    {
                        AddPendingEdit(path, newBmp);

                        // Force refresh of the relevant page
                        if (_window.ViewerManager != null)
                        {
                            for (int i = 0; i < _window.ViewerManager.Pages.Length; i++)
                            {
                                if (_window.ViewerManager.Pages[i].CurrentFilePath == path)
                                {
                                    _window.ViewerManager.Pages[i].EditedBitmap = null; // Force LoadPageAsync logic or direct update
                                }
                            }
                        }
                        _ = _window.UpdateDisplayAsync();
                    });
                }
            });
        }
    }

    internal class EditSession : IDisposable
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
