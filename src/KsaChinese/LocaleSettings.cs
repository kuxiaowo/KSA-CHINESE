using System.Text.Json;

namespace KsaChinese;

/// <summary>Per-profile preferences; never write into a manager's shared mod files.</summary>
public static class LocaleSettings
{
    public static string Read(string path, string fallback, IEnumerable<string> available, Action<string> log)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            using var settings = JsonDocument.Parse(File.ReadAllText(path));
            if (settings.RootElement.ValueKind == JsonValueKind.Object
                && settings.RootElement.TryGetProperty("locale", out var value)
                && value.ValueKind == JsonValueKind.String
                && available.Contains(value.GetString(), StringComparer.OrdinalIgnoreCase))
                return value.GetString()!;
            log("Ignoring unsupported profile language setting; using packaged default.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            log("Cannot read profile language setting; using packaged default: " + error.Message);
        }
        return fallback;
    }

    public static void Write(string path, string locale, Action<string> log)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { locale }) + "\n");
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log("Language changed for this session, but could not save the profile preference: " + error.Message);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
