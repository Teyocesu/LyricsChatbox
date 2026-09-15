internal static class Artwork
{
    private const int MaxBytes = 1_048_576;
    public static async Task<ArtworkRow> Check(Windows.Storage.Streams.IRandomAccessStreamReference thumbnail)
    {
        try
        {
            using var stream = await thumbnail.OpenReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            if (stream.Size > MaxBytes) return new(true, false, stream.ContentType, null, "Exceeds 1 MiB bound");
            using var input = stream.AsStreamForRead();
            var buffer = new byte[8192];
            long read = 0;
            int count;
            while ((count = await input.ReadAsync(buffer)) > 0)
            {
                read += count;
                if (read > MaxBytes) return new(true, false, stream.ContentType, null, "Exceeds 1 MiB bound");
            }
            stream.Seek(0);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            return new(true, read > 0 && decoder.PixelWidth is > 0 and <= 4096 &&
                decoder.PixelHeight is > 0 and <= 4096, stream.ContentType, read, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { return new(true, false, null, null, ex.GetType().Name); }
    }
}
