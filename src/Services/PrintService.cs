using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using quick_image_viewer.Interfaces;
using quick_image_viewer.Managers;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
namespace quick_image_viewer.Services
{
    public class PrintService : IPrintService
    {
        private IMainView _window;
        private ISettingsManager _settings;
        private PrintManager _printManager = null!;
        private PrintDocument _printDocument = null!;
        private IPrintDocumentSource _printDocumentSource = null!;
        private string _currentPrintImagePath = null!;
        private BitmapImage? _printImage;
        private PrintPageDescription _pageDescription;

        public PrintService(IMainView window, ISettingsManager settings)
        {
            _window = window;
            _settings = settings;
            RegisterForPrinting();
        }

        private void RegisterForPrinting()
        {
            var hwnd = _window.WindowHandle;
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

                if (ArchiveManager.IsArchivePath(imagePath))
                {
                    var (arc, ent) = ArchiveManager.SplitArchivePath(imagePath);
                    byte[]? bytes = ArchiveManager.GetEntryBytes(arc, ent);
                    if (bytes != null)
                    {
                        using (var ms = new System.IO.MemoryStream(bytes))
                        {
                            await _printImage.SetSourceAsync(ms.AsRandomAccessStream());
                        }
                    }
                }
                else
                {
                    using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        await _printImage.SetSourceAsync(fs.AsRandomAccessStream());
                    }
                }

                var hwnd = _window.WindowHandle;
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            }
            catch (Exception ex)
            {
                _window.ShowNotification(string.Format(_settings.GetString("Notification_PrintError"), ex.Message));
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
            catch (Exception) { }
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

