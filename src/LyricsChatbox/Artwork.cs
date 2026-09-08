using System.IO;
using System.Windows.Media.Imaging;

namespace LyricsChatbox;

public static class Artwork
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    public static async Task<BitmapSource?> LoadAsync(Func<CancellationToken, Task<Stream>> open, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var source = await open(timeout.Token);
            using var buffer = new MemoryStream(); var bytes = new byte[8192]; int count;
            while ((count = await source.ReadAsync(bytes, timeout.Token)) > 0)
            {
                if (buffer.Length + count > MaximumBytes) return null;
                buffer.Write(bytes, 0, count);
            }
            timeout.Token.ThrowIfCancellationRequested();
            buffer.Position = 0;
            var decoder = BitmapDecoder.Create(buffer, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if (frame.PixelWidth > 8192 || frame.PixelHeight > 8192 || (long)frame.PixelWidth * frame.PixelHeight > 40_000_000) return null;
            buffer.Position = 0;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            if (frame.PixelWidth >= frame.PixelHeight) image.DecodePixelWidth = Math.Min(256, frame.PixelWidth);
            else image.DecodePixelHeight = Math.Min(256, frame.PixelHeight);
            image.StreamSource = buffer; image.EndInit(); image.Freeze();
            timeout.Token.ThrowIfCancellationRequested();
            return image;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidOperationException or
            System.Runtime.InteropServices.COMException or OperationCanceledException or OverflowException)
        { return null; }
    }
}

// One current image; invalidating playback immediately clears it, independent of asynchronous decoding.
public sealed class ArtworkState
{
    private long generation;
    public BitmapSource? Image { get; private set; }
    public long Reset() { Image = null; return ++generation; }
    public bool Complete(long request, BitmapSource? image)
    {
        if (request != generation) return false;
        Image = image; return true;
    }
}
