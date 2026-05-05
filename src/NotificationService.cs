using Microsoft.UI.Xaml;
using System;

namespace grid_image_viewer
{
    internal class NotificationService : INotificationService
    {
        private readonly IMainView _window;
        private readonly DispatcherTimer _timer;

        public NotificationService(IMainView window)
        {
            _window = window;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                _window.NotificationOverlay.Visibility = Visibility.Collapsed;
            };
        }

        public void Show(string message)
        {
            _window.NotificationText.Text = message;
            _window.NotificationOverlay.Visibility = Visibility.Visible;
            _timer.Stop();
            _timer.Start();
        }
    }
}
