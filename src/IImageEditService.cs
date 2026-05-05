using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace grid_image_viewer
{
    public interface IImageEditService
    {
        void AddPendingEdit(string path, SKBitmap bitmap);
        void ClearEdits(string path);
        void ClearAll();
        void UndoEdit(string path);
        void RedoEdit(string path);
        Task RotateAsync(string path, float degrees);
        Task FlipAsync(string path, bool horizontal);
        Task FilterAsync(string path, string filterType);
        Task ResizeAsync(string path, int width, int height);
        Task CropAsync(string path, SKRectI region);
        SKBitmap? GetCurrentBitmap(string path);
        EditSession? GetSession(string path);
        (int width, int height) GetImageSize(string path);
        void CleanupSessions(IEnumerable<string> activePaths);
        Task ApplyTransformationAsync(string path, System.Func<SKBitmap?, SKBitmap?> transform);
    }
}
