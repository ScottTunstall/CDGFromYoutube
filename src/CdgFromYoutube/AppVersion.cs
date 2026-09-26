using System.Reflection;

namespace CdgFromYoutube;

/// <summary>The program's name and version, shown by <c>--version</c> and at the start of every run.</summary>
/// <remarks>
/// The version is read from the assembly rather than duplicated as a constant, so the one place that
/// sets it is <c>CdgFromYoutube.csproj</c>'s <c>&lt;Version&gt;</c> property.
/// </remarks>
public static class AppVersion
{
    /// <summary>The program's name, as installed and as it appears in "Installed apps".</summary>
    public const string Name = "CDGFromYoutube";

    /// <summary>The current version, such as "1.1.0", or "unknown" if the assembly was built without one.</summary>
    public static string Current { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    /// <summary>The name and version together, such as "CDGFromYoutube (1.1.0)".</summary>
    public static string Banner { get; } = $"{Name} ({Current})";
}
