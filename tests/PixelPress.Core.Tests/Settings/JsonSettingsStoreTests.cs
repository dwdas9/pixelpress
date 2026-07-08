using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Presets;
using PixelPress.Core.Settings;
using Xunit;

namespace PixelPress.Core.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"pixelpress-settings-tests-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_tempDirectory, "settings.json");

    [Fact]
    public void Load_returns_defaults_when_no_file_exists_yet()
    {
        var store = new JsonSettingsStore(FilePath);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
    }

    [Fact]
    public void Save_then_load_round_trips_every_field()
    {
        var store = new JsonSettingsStore(FilePath);
        var saved = new AppSettings
        {
            Preset = PresetId.SmallestSize,
            TargetFormat = ImageFormatId.WebP,
            MetadataPolicy = MetadataPolicy.Strip,
            OutputPolicy = OutputPolicy.OverwriteOriginals,
            ResizeEnabled = true,
            ResizeMaxDimensionPixels = 1600,
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void Save_creates_the_parent_directory_if_missing()
    {
        var store = new JsonSettingsStore(FilePath);

        store.Save(AppSettings.Default);

        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Load_falls_back_to_defaults_when_the_file_is_corrupt()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(FilePath, "{ not valid json ");
        var store = new JsonSettingsStore(FilePath);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
