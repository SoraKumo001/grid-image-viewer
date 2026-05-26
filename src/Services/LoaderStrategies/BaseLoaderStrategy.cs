using quick_image_viewer.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace quick_image_viewer.Services.LoaderStrategies
{
    internal abstract class BaseLoaderStrategy
    {
        protected readonly IViewerLoaderHost _window;

        protected BaseLoaderStrategy(IViewerLoaderHost window)
        {
            _window = window;
        }

        protected Task EnqueueOnDispatcherAsync(
            Microsoft.UI.Dispatching.DispatcherQueue dispatcher,
            Func<Task> action,
            CancellationToken token)
        {
            if (dispatcher.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await action();
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException ex)
                {
                    tcs.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                tcs.TrySetCanceled(token);
            }

            return tcs.Task;
        }
    }
}
