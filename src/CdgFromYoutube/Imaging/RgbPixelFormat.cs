namespace CdgFromYoutube.Imaging;

/// <summary>
/// The packed eight bit red, green and blue pixel layout that frames are decoded into.
/// </summary>
public static class RgbPixelFormat
{
    /// <summary>The number of bytes used by one pixel.</summary>
    public const int BytesPerPixel = 3;

    /// <summary>The number of bits used by each channel.</summary>
    public const int BitsPerChannel = 8;

    /// <summary>Returns the number of bytes a frame of the given size occupies.</summary>
    public static int GetFrameSizeBytes(int width, int height) => width * height * BytesPerPixel;
}
