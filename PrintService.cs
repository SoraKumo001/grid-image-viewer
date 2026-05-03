using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using WinRT.Interop;

namespace grid_image_viewer
{
    public class PrintService
    {
        private MainWindow _window;
        private PrintManager _printManager = null!;
        private PrintDocument _printDocument = null!;
        private IPrintDocumentSource _printDocumentSource = null!;
        private string _currentPrintImagePath = null!;
        private BitmapImage? _printImage;
        private PrintPageDescription _pageDescription;

        public PrintService(MainWindow window)
        {
            _window = window;
            RegisterForPrinting();
        }

        private void RegisterForPrinting()
        {
            var hwnd = WindowNative.GetWindowHandle(_window);
            _printManager = PrintManagerInterop.GetForWindow(hwnd);
            _printManager.PrintTaskRequested += PrintManager_PrintTaskRequested;
        }

        public void UnregisterForPrinting()
        {
            if (_printManager != null)
            {
                _printManager.PrintTaskRequested -= PrintManager_PrintTaskRequested;
            }
        }

        public async Task PrintImageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            _currentPrintImagePath = imagePath;

            try
            {
                _printImage = new BitmapImage();
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath);
                using (var stream = await file.OpenReadAsync())
                {
                    await _printImage.SetSourceAsync(stream);
                }

                var hwnd = WindowNative.GetWindowHandle(_window);
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            }
            catch (Exception ex)
            {
                _window.ShowNotification($"Print error: {ex.Message}");
            }
        }

        private void PrintManager_PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            var printTask = args.Request.CreatePrintTask("Image Print", sourceRequestedArgs =>
            {
                var deferral = sourceRequestedArgs.GetDeferral();
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    _printDocument = new PrintDocument();
                    _printDocumentSource = _printDocument.DocumentSource;

                    _printDocument.Paginate += PrintDocument_Paginate;
                    _printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
                    _printDocument.AddPages += PrintDocument_AddPages;

                    sourceRequestedArgs.SetSource(_printDocumentSource);
                    deferral.Complete();
                });
            });
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            try
            {
                _pageDescription = e.PrintTaskOptions.GetPageDescription(1); // Page starts at 1
                _printDocument.SetPreviewPageCount(1, PreviewPageCountType.Final);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paginate Error: {ex.Message}");
            }
        }

        private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            var printArea = CreatePrintUIElement(_pageDescription);
            _printDocument.SetPreviewPage(e.PageNumber, printArea);
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            var printArea = CreatePrintUIElement(_pageDescription);
            _printDocument.AddPage(printArea);
            _printDocument.AddPagesComplete();
        }

        private UIElement CreatePrintUIElement(PrintPageDescription pageDescription)
        {
            var grid = new Grid
            {
                Width = pageDescription.PageSize.Width,
                Height = pageDescription.PageSize.Height,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
            };

            // Image size adjustment: Use Stretch.Uniform to fit within the paper margins.
            var image = new Image
            {
                Source = _printImage,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(
                    pageDescription.ImageableRect.Left,
                    pageDescription.ImageableRect.Top,
                    pageDescription.PageSize.Width - pageDescription.ImageableRect.Right,
                    pageDescription.PageSize.Height - pageDescription.ImageableRect.Bottom)
            };

            grid.Children.Add(image);

            grid.Measure(new Windows.Foundation.Size(pageDescription.PageSize.Width, pageDescription.PageSize.Height));
            grid.Arrange(new Windows.Foundation.Rect(0, 0, pageDescription.PageSize.Width, pageDescription.PageSize.Height));

            return grid;
        }
    }
}