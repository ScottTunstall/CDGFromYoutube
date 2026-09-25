namespace CdgFromYoutube.Pipeline;

/// <summary>A folder for the downloaded video, removed when the conversion ends unless it is kept.</summary>
public sealed class TempWorkspace : IDisposable
{
    private readonly bool _keepFiles;
    private bool _disposed;

    /// <summary>Creates a fresh folder under the system temporary folder.</summary>
    /// <param name="keepFiles">Whether the folder survives the conversion.</param>
    public TempWorkspace(bool keepFiles)
    {
        _keepFiles = keepFiles;
        DirectoryPath = Path.Combine(Path.GetTempPath(), "cdgfromyoutube", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>The full path of the folder.</summary>
    public string DirectoryPath { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_keepFiles)
        {
            return;
        }

        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // A file is still open somewhere; the temporary folder can be cleaned up by hand.
        }
        catch (UnauthorizedAccessException)
        {
            // The same, for a file that cannot be deleted.
        }
    }
}
