using System.Text.Json;
using System.Text.Json.Serialization;
using Laytivo.Core;

namespace Laytivo.Infrastructure;

public sealed class ConfigurationStore(string directory) : IConfigurationStore
{
    public string FilePath => Path.Combine(directory, "configuration.json");
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppConfiguration Load()
    {
        if (!File.Exists(FilePath)) return AppConfiguration.Empty;
        if (new FileInfo(FilePath).Length > 8 * 1024 * 1024)
            throw new InvalidDataException("Die Konfigurationsdatei ist zu groß.");
        using var stream = File.OpenRead(FilePath);
        var config = JsonSerializer.Deserialize<AppConfiguration>(stream, Options)
            ?? throw new InvalidDataException("Die Konfigurationsdatei ist leer.");
        ConfigurationValidator.Validate(config);
        return config;
    }

    public void Save(AppConfiguration configuration)
    {
        ConfigurationValidator.Validate(configuration);
        Directory.CreateDirectory(directory);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, configuration, Options);
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public string ArchiveInvalidConfiguration()
    {
        var destination = FilePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
        if (File.Exists(FilePath)) File.Move(FilePath, destination);
        return destination;
    }
}
