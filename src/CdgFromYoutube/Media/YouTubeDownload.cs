namespace CdgFromYoutube.Media;

/// <summary>A video that yt-dlp downloaded, together with the title it reported.</summary>
/// <param name="Title">The title of the video.</param>
/// <param name="FilePath">The full path of the downloaded file.</param>
public sealed record YouTubeDownload(string Title, string FilePath);
