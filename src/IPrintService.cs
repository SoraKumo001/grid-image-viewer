using System.Threading.Tasks;

namespace grid_image_viewer
{
    public interface IPrintService
    {
        Task PrintImageAsync(string imagePath);
        void UnregisterForPrinting();
    }
}
