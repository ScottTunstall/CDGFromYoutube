using System.Reflection;

namespace CdgFromYoutube;

/// <summary>The program's version, shown by <c>--version</c>.</summary>
/// <remarks>
/// Read from the assembly rather than duplicated as a constant, so the one place that sets it is
/// <c>CdgFromYoutube.csproj</c>'s <c>&lt;Version&gt;</c> property.
/// </remarks>
public static class AppVersion
{
    /// <summary>The current version, such as "1.1.0", or "unknown" if the assembly was built without one.</summary>
    public static string Current { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
