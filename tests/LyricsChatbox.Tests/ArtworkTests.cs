using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyricsChatbox.Tests;

public class ArtworkTests
{
    [Theory]
    [InlineData(1, 8192)]
    [InlineData(8192, 1)]
    public async Task ExtremeAspectRatioNeverUpscalesIntoLargeDecodedImage(int width, int height)
    {
        var source = BitmapSource.Create(width,height,96,96,PixelFormats.Gray8,null,new byte[width*height],width); source.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = new MemoryStream(); encoder.Save(file);
        var image = await Artwork.LoadAsync(_=>Task.FromResult<Stream>(new MemoryStream(file.ToArray())),default);
        Assert.NotNull(image); Assert.InRange(image.PixelWidth,1,256); Assert.InRange(image.PixelHeight,1,256);
    }
    [Fact]
    public async Task DecodedArtIsBoundedFrozenAndStaleCompletionCannotReplaceCurrent()
    {
        var source = BitmapSource.Create(10, 10, 96, 96, PixelFormats.Gray8, null, new byte[100], 10); source.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = new MemoryStream(); encoder.Save(file); var bytes = file.ToArray();
        var image = await Artwork.LoadAsync(_ => Task.FromResult<Stream>(new MemoryStream(bytes)), default);
        Assert.NotNull(image); Assert.True(image.IsFrozen); Assert.True(image.PixelWidth <= 256);
        var state = new ArtworkState(); var old = state.Reset(); var current = state.Reset();
        Assert.True(state.Complete(current, image)); Assert.False(state.Complete(old, null)); Assert.Same(image, state.Image);
        state.Reset(); Assert.Null(state.Image);
    }
    [Fact]
    public async Task MalformedOversizedAndCancelledStreamsFailSafely()
    {
        Assert.Null(await Artwork.LoadAsync(_ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])), default));
        Assert.Null(await Artwork.LoadAsync(_ => Task.FromResult<Stream>(new MemoryStream(new byte[Artwork.MaximumBytes + 1])), default));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Artwork.LoadAsync(_ => Task.FromResult<Stream>(new MemoryStream()), cancel.Token));
    }
}
