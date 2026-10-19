using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Houston.App;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    /// <summary>Synchronous on purpose: a tiny file read before the window is created.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), JsonOptions)
                       ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or corrupt file just means defaults; it is rewritten on the next save.
            Debug.WriteLine($"Settings load failed: {ex.Message}");
        }

        return new AppSettings();
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        await WriteLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            // Write-then-rename so a crash mid-write never leaves a truncated settings.json behind.
            var temporary = AppPaths.SettingsFile + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, AppPaths.SettingsFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Settings save failed: {ex.Message}");
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
