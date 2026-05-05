using System.Threading.Tasks;
namespace quick_image_viewer.Interfaces
{
    public interface IPrintService
    {
        Task PrintImageAsync(string imagePath);
        void UnregisterForPrinting();
    }
}
