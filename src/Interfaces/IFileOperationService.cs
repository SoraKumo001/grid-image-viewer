using System.Threading.Tasks;

namespace quick_image_viewer.Interfaces
{
    public interface IFileOperationService
    {
        Task HandleDeleteFileAsync(string path);
        Task HandleRenameFileAsync(string path);
        Task HandleMoveFileAsync(string path);
    }
}
