using Laytivo.Core;
using Laytivo.Infrastructure;

namespace Laytivo.Infrastructure.Tests;

public sealed class ConfigurationStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Laytivo.Tests-" + Guid.NewGuid().ToString("N"));
    private ConfigurationStore Store => new(_directory);

    [Fact]
    public void ShortcutSurvivesRestartAndLegacyConfigurationGetsDefault()
    {
        var settings = new AppSettings(RestoreHotkeyEnabled: false, RestoreHotkeyModifiers: 6, RestoreHotkeyKey: 0x78);
        Store.Save(new(1, settings, null));
        Assert.Equal(settings, Store.Load().Settings);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Store.FilePath))!;
        var saved = json["settings"]!.AsObject();
        saved.Remove("restoreHotkeyEnabled"); saved.Remove("restoreHotkeyModifiers"); saved.Remove("restoreHotkeyKey");
        File.WriteAllText(Store.FilePath, json.ToJsonString());
        var legacy = Store.Load().Settings;
        Assert.True(legacy.RestoreHotkeyEnabled);
        Assert.Equal("Strg + Alt + R", RestoreHotkey.Describe(legacy));
    }

    [Fact]
    public void SnapStateAndVisibleFrameSurviveRoundTripAndOlderProfilesStillLoad()
    {
        var config = Configuration();
        var entry = config.Workspace!.Windows[0] with
        {
            State = SavedWindowState.Normal,
            IsSnapped = true,
            NormalBounds = new(-1928, 0, 976, 1048),
            VisibleBounds = new(-1920, 0, 960, 1040)
        };
        Store.Save(config with { Workspace = config.Workspace with { Windows = [entry] } });
        Assert.Equal(entry, Store.Load().Workspace!.Windows[0]);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Store.FilePath))!;
        var saved = json["workspace"]!["windows"]![0]!.AsObject();
        saved.Remove("isSnapped"); saved.Remove("visibleBounds");
        File.WriteAllText(Store.FilePath, json.ToJsonString());
        var legacy = Store.Load().Workspace!.Windows[0];
        Assert.False(legacy.IsSnapped);
        Assert.Null(legacy.VisibleBounds);
    }

    [Fact]
    public void MissingFileReturnsDefaultsWithoutWriting()
    {
        Assert.Equal(AppConfiguration.Empty, Store.Load());
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void RoundTripPreservesProfileSettingsAndNegativeCoordinates()
    {
        var config = Configuration();
        Store.Save(config);
        var loaded = Store.Load();
        Assert.Equal(config.Settings, loaded.Settings);
        Assert.Equal(config.Workspace!.SavedAtUtc, loaded.Workspace!.SavedAtUtc);
        Assert.Equal(config.Workspace.Monitors, loaded.Workspace.Monitors);
        Assert.Equal(config.Workspace.Windows, loaded.Workspace.Windows);
    }

    [Fact]
    public void ReplacingProfileCreatesPreviousBackupAndNoTemporaryFiles()
    {
        Store.Save(Configuration());
        var original = File.ReadAllText(Store.FilePath);
        Store.Save(AppConfiguration.Empty);
        Assert.Equal(original, File.ReadAllText(Store.FilePath + ".bak"));
        Assert.Equal(AppConfiguration.Empty, Store.Load());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":99,\"settings\":{}}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":null}")]
    public void InvalidConfigurationIsReportedAndPreserved(string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, contents);
        Assert.ThrowsAny<Exception>(() => Store.Load());
        Assert.Equal(contents, File.ReadAllText(Store.FilePath));
    }

    [Fact]
    public void InvalidSaveDoesNotReplaceGoodConfiguration()
    {
        Store.Save(AppConfiguration.Empty);
        var original = File.ReadAllText(Store.FilePath);
        Assert.Throws<InvalidDataException>(() => Store.Save(AppConfiguration.Empty with { SchemaVersion = 100 }));
        Assert.Equal(original, File.ReadAllText(Store.FilePath));
    }

    [Fact]
    public void ExplicitRecoveryArchivesOriginal()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, "invalid");
        var archive = Store.ArchiveInvalidConfiguration();
        Assert.Equal("invalid", File.ReadAllText(archive));
        Assert.False(File.Exists(Store.FilePath));
        Store.Save(AppConfiguration.Empty);
        Assert.Equal(AppConfiguration.Empty, Store.Load());
    }

    [Fact]
    public void OversizedConfigurationIsRejected()
    {
        Directory.CreateDirectory(_directory);
        using (var file = File.Create(Store.FilePath)) file.SetLength(9 * 1024 * 1024);
        Assert.Throws<InvalidDataException>(() => Store.Load());
    }

    [Fact]
    public void LogRetentionIsBoundedAndRecordsAreJson()
    {
        var log = new LocalLog(_directory);
        for (var i = 0; i < 7; i++) log.Write("test", new { text = new string('x', 1024 * 1024) });
        Assert.Equal(5, Directory.GetFiles(_directory).Length);
        foreach (var line in File.ReadLines(Path.Combine(_directory, "laytivo.jsonl")))
        {
            using var json = System.Text.Json.JsonDocument.Parse(line);
            Assert.Equal("test", json.RootElement.GetProperty("action").GetString());
        }
    }

    [Fact]
    public void LauncherRejectsMissingExeAndNonExecutablePaths()
    {
        var entry = Configuration().Workspace!.Windows[0];
        var launcher = new ApplicationLauncher();
        Assert.Throws<FileNotFoundException>(() => launcher.Launch(entry with { ProcessPath = Path.Combine(_directory, "missing.exe") }));
        Assert.Throws<InvalidOperationException>(() => launcher.Launch(entry with { ProcessPath = "relative.exe" }));
        Assert.Throws<InvalidOperationException>(() => launcher.Launch(entry with { ProcessPath = @"C:\test.cmd" }));
    }

    private static AppConfiguration Configuration() => new(1, new(false, true, false, 15),
        new("default", "Standard-Desktop", DateTimeOffset.UtcNow,
            [new("left", "DISPLAY2", new(-1920, 0, 1920, 1080), new(-1920, 0, 1920, 1040), 1.25, false)],
            [new("one", @"C:\Apps\Editor.exe", "Editor", "document.txt", @"C:\Apps", "Document", "EditorClass", "left",
                new(-1800, 100, 900, 600), SavedWindowState.Maximized, true)]));

    public void Dispose()
    {
        // Only the unique directory created by this test instance is removed.
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
