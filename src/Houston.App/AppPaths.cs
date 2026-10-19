using System.IO;

namespace Houston.App;

internal static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Houston");

    public static string SettingsFile { get; } = Path.Combine(DataFolder, "settings.json");

    // Scratch folder required by WebView2; deleted on exit so only settings.json persists.
    public static string WebViewDataFolder { get; } = Path.Combine(DataFolder, "WebView2");
}
