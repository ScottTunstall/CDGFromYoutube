namespace CdgFromYoutube.Imaging;

/// <summary>
/// A four by four ordered dithering matrix used to mix two colors inside a CD+G tile.
/// </summary>
/// <remarks>
/// The matrix is the standard Bayer threshold table, which is why the thresholds are not in numeric order.
/// </remarks>
public static class OrderedDither
{
    /// <summary>The width and height of the threshold matrix.</summary>
    public const int MatrixSize = 4;

    /// <summary>The number of distinct thresholds, one per matrix cell.</summary>
    public const int ThresholdCount = MatrixSize * MatrixSize;

    private static readonly byte[] Thresholds =
    [
         0,  8,  2, 10,
        12,  4, 14,  6,
         3, 11,  1,  9,
        15,  7, 13,  5,
    ];

    /// <summary>Returns the threshold for a pixel position, in the range zero to <see cref="ThresholdCount"/>.</summary>
    public static int GetThreshold(int x, int y) =>
        Thresholds[((y % MatrixSize) * MatrixSize) + (x % MatrixSize)];
}
