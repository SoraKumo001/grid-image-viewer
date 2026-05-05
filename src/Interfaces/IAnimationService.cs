namespace quick_image_viewer.Interfaces
{
    public interface IAnimationService
    {
        void StartGridCrossfade(Microsoft.UI.Xaml.Controls.Grid currentGrid, Microsoft.UI.Xaml.Controls.Grid prevGrid);
        void StartAnimation();
        void StopAnimation();
        void StopCrossfade();
    }
}
