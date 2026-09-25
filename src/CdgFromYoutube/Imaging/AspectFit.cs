namespace CdgFromYoutube.Imaging;

/// <summary>The width and height of a frame in pixels.</summary>
public readonly record struct FrameSize(int Width, int Height)
{
    /// <summary>Describes the size as "1920x1080".</summary>
    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>Scales frames into a fixed raster without changing their shape.</summary>
public static class AspectFit
{
    /// <summary>
    /// Returns the largest whole pixel size that shows all of <paramref name="source"/> inside
    /// <paramref name="bounds"/>.
    /// </summary>
    public static FrameSize FitInside(FrameSize source, FrameSize bounds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Height, 1);

        double scale = Math.Min((double)bounds.Width / source.Width, (double)bounds.Height / source.Height);
        int width = Math.Max(1, (int)Math.Floor(source.Width * scale));
        int height = Math.Max(1, (int)Math.Floor(source.Height * scale));
        return new FrameSize(width, height);
    }

    /// <summary>Returns whether fitting the source inside the bounds means throwing pixels away.</summary>
    public static bool IsDownScale(FrameSize source, FrameSize bounds) =>
        FitInside(source, bounds).Width < source.Width;
}
