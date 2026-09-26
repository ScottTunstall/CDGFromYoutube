namespace CdgFromYoutube.Cdg;

/// <summary>
/// The sixteen colors that a screen can show, with the distance lookups that tile encoding needs.
/// </summary>
/// <remarks>
/// A CD+G screen only ever uses the entries of the loaded color table, so every pixel of a frame has to be
/// expressed as one of these sixteen colors.
/// </remarks>
public sealed class CdgPalette
{
    private readonly CdgColor[] _colors;
    private readonly int[] _distances;
    private readonly byte[]? _colorMap;

    /// <summary>Creates a palette from exactly <see cref="CdgFormat.ColorCount"/> colors.</summary>
    public CdgPalette(IReadOnlyList<CdgColor> colors)
        : this(colors, [])
    {
    }

    /// <summary>
    /// Creates a palette from exactly <see cref="CdgFormat.ColorCount"/> colors, some of which form the
    /// shades of antialiased lettering.
    /// </summary>
    public CdgPalette(IReadOnlyList<CdgColor> colors, IReadOnlyList<CdgRamp> ramps)
        : this(colors, ramps, null)
    {
    }

    private CdgPalette(IReadOnlyList<CdgColor> colors, IReadOnlyList<CdgRamp> ramps, byte[]? colorMap)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(ramps);
        Ramps = [.. ramps];
        if (colors.Count != CdgFormat.ColorCount)
        {
            throw new ArgumentException(
                $"A CD+G palette holds exactly {CdgFormat.ColorCount} colours, but {colors.Count} were given.",
                nameof(colors));
        }

        _colorMap = colorMap;

        _colors = [.. colors];
        _distances = new int[CdgFormat.ColorCount * CdgFormat.ColorCount];
        for (int first = 0; first < CdgFormat.ColorCount; first++)
        {
            for (int second = first + 1; second < CdgFormat.ColorCount; second++)
            {
                int distance = _colors[first].DistanceSquared(_colors[second]);
                SetDistance(first, second, distance);
                SetDistance(second, first, distance);
            }
        }
    }

    /// <summary>
    /// Creates a palette in which every color is drawn as a fixed entry, decided in advance, rather than
    /// as the nearest one.
    /// </summary>
    /// <param name="colors">Exactly <see cref="CdgFormat.ColorCount"/> colors.</param>
    /// <param name="colorMap">
    /// The entry for every four bit color, indexed by <see cref="CdgColor.Pack"/>.
    /// </param>
    public static CdgPalette WithColorMap(IReadOnlyList<CdgColor> colors, IReadOnlyList<byte> colorMap)
    {
        ArgumentNullException.ThrowIfNull(colorMap);
        if (colorMap.Count != CdgColorHistogram.BucketCount)
        {
            throw new ArgumentException(
                $"A colour map holds an entry for each of the {CdgColorHistogram.BucketCount} colours, but {colorMap.Count} were given.",
                nameof(colorMap));
        }

        if (colorMap.Any(index => index >= CdgFormat.ColorCount))
        {
            throw new ArgumentException(
                $"A colour map can only point at the {CdgFormat.ColorCount} entries of the palette.",
                nameof(colorMap));
        }

        return new CdgPalette(colors, [], [.. colorMap]);
    }

    /// <summary>The shades of antialiased lettering, or none when the palette was built from the picture.</summary>
    public IReadOnlyList<CdgRamp> Ramps { get; }

    /// <summary>
    /// Whether every color has a fixed entry, which <see cref="MapColor"/> returns, rather than being drawn
    /// as the nearest entry.
    /// </summary>
    public bool HasColorMap => _colorMap is not null;

    /// <summary>
    /// Returns the entry a color is drawn as: its fixed entry when the palette has a color map, and the
    /// nearest entry otherwise.
    /// </summary>
    public byte MapColor(CdgColor color) => _colorMap is null ? FindNearestIndex(color) : _colorMap[color.Pack()];

    /// <summary>The palette entries in table order.</summary>
    public ReadOnlySpan<CdgColor> Colors => _colors;

    /// <summary>Returns the palette entry at the given index.</summary>
    public CdgColor this[int index] => _colors[index];

    /// <summary>Returns the index of the palette entry that is closest to the given color.</summary>
    public byte FindNearestIndex(CdgColor color) => FindTwoNearest(color).NearestIndex;

    /// <summary>Returns the two palette entries that are closest to the given color.</summary>
    public NearestColors FindTwoNearest(CdgColor color)
    {
        byte nearest = 0;
        byte second = 0;
        int nearestDistance = int.MaxValue;
        int secondDistance = int.MaxValue;
        for (int index = 0; index < _colors.Length; index++)
        {
            int distance = color.DistanceSquared(_colors[index]);
            if (distance < nearestDistance)
            {
                second = nearest;
                secondDistance = nearestDistance;
                nearest = (byte)index;
                nearestDistance = distance;
            }
            else if (distance < secondDistance)
            {
                second = (byte)index;
                secondDistance = distance;
            }
        }

        return new NearestColors(nearest, nearestDistance, second, secondDistance);
    }

    /// <summary>Returns the squared distance between two palette entries.</summary>
    public int GetDistanceSquared(byte firstIndex, byte secondIndex) =>
        _distances[(firstIndex * CdgFormat.ColorCount) + secondIndex];

    /// <summary>The two palette entries closest to a color, and how far away each of them is.</summary>
    /// <param name="NearestIndex">The index of the closest entry.</param>
    /// <param name="NearestDistance">The distance to the closest entry.</param>
    /// <param name="SecondIndex">The index of the next closest entry.</param>
    /// <param name="SecondDistance">The distance to the next closest entry.</param>
    public readonly record struct NearestColors(
        byte NearestIndex,
        int NearestDistance,
        byte SecondIndex,
        int SecondDistance);

    private void SetDistance(int firstIndex, int secondIndex, int distance) =>
        _distances[(firstIndex * CdgFormat.ColorCount) + secondIndex] = distance;
}
