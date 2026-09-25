using System.Text;

namespace CdgFromYoutube.Media;

/// <summary>
/// Turns a video title into the base name of the output files, which a player pairs up by name.
/// </summary>
public static class OutputNaming
{
    /// <summary>The name used when a title holds nothing that can be part of a file name.</summary>
    public const string FallbackBaseName = "karaoke";

    /// <summary>The longest base name produced, which leaves room for the extension and the folder.</summary>
    public const int MaximumLength = 120;

    private static readonly HashSet<char> InvalidFileNameCharacters = [.. Path.GetInvalidFileNameChars()];

    /// <summary>Returns a file name that carries as much of the title as a file name can.</summary>
    public static string ResolveBaseName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return FallbackBaseName;
        }

        StringBuilder builder = new(title.Length);
        foreach (char character in title)
        {
            builder.Append(InvalidFileNameCharacters.Contains(character) || char.IsControl(character) ? ' ' : character);
        }

        string collapsed = CollapseRunsOfSpaces(builder.ToString()).Trim().TrimEnd('.');
        if (collapsed.Length > MaximumLength)
        {
            collapsed = collapsed[..MaximumLength].TrimEnd();
        }

        return collapsed.Length == 0 ? FallbackBaseName : collapsed;
    }

    private static string CollapseRunsOfSpaces(string text)
    {
        StringBuilder builder = new(text.Length);
        foreach (char character in text)
        {
            if (character == ' ' && builder.Length > 0 && builder[^1] == ' ')
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
