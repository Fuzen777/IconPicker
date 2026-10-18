using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace IconPicker.App.Services;

/// <summary>Draws icons off the UI thread, a few at a time, and turns them into images the UI can show.</summary>
internal sealed class ThumbnailLoader
{
    private readonly SemaphoreSlim _throttle = new(6);

    /// <summary>Must be called on the UI thread, which the image is created on.</summary>
    public Task<ImageSource?> LoadAsync(Func<Task<SoftwareBitmap?>> render)
    {
        return LoadAsync(render, CancellationToken.None);
    }

    /// <summary>Must be called on the UI thread. A render still waiting its turn when cancelled is skipped and gives null, so tiles scrolled away don't hold up the visible ones.</summary>
    public async Task<ImageSource?> LoadAsync(Func<Task<SoftwareBitmap?>> render, CancellationToken cancellationToken)
    {
        SoftwareBitmap? bitmap;
        try
        {
            await _throttle.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        try
        {
            bitmap = cancellationToken.IsCancellationRequested ? null : await Task.Run(render);
        }
        finally
        {
            _throttle.Release();
        }
        if (bitmap is null || cancellationToken.IsCancellationRequested)
        {
            bitmap?.Dispose();
            return null;
        }
        var imageSource = new SoftwareBitmapSource();
        await imageSource.SetBitmapAsync(bitmap);
        return imageSource;
    }
}
