using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPress.Core.Settings;

/// <summary>Loads and saves the user's advanced-panel preferences.</summary>
public interface ISettingsStore
{
    /// <summary>Returns the saved settings, or <see cref="AppSettings.Default"/>
    /// if none have been saved yet or the saved file is unreadable.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>JSON-on-disk implementation. See ADR-0005 for why JSON in the
/// OS app-data folder was chosen over alternatives.</summary>
public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? AppSettings.Default;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A missing or corrupt settings file must never block startup;
            // it just means the user starts from defaults again.
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
    }
}
