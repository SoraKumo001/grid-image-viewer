namespace grid_image_viewer
{
    public interface ISlideshowService
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
        void UpdateInterval(double seconds);
        int[] CurrentRandomIndices { get; }
    }

    public record SlideshowNextRequestedMessage();
    public record SlideshowStateChangedMessage(bool IsRunning);
}
