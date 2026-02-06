namespace CloverBridge;

/// <summary>
/// Application version management
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Current application version - Update this to change the version displayed in the UI
    /// </summary>
    public const string Version = "1.4.0";

    /// <summary>
    /// Gets the complete version string
    /// </summary>
    public static string GetVersion() => $"v{Version}";

    /// <summary>
    /// Gets the version from assembly if available
    /// </summary>
    public static string GetAssemblyVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName()
                .Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : GetVersion();
        }
        catch
        {
            return GetVersion();
        }
    }
}
