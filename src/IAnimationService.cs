namespace grid_image_viewer
{
    public interface IAnimationService
    {
        void StartGridCrossfade(Microsoft.UI.Xaml.Controls.Grid currentGrid, Microsoft.UI.Xaml.Controls.Grid prevGrid);
        void StartAnimation();
        void StopAnimation();
        void StopCrossfade();
    }
}
