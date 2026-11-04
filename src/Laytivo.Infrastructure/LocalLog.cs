using System.Text.Json;
using Laytivo.Core;

namespace Laytivo.Infrastructure;

public sealed class LocalLog(string directory) : ILocalLog
{
    private readonly object _sync = new();
    public void Write(string action, object details)
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var file = Path.Combine(directory, "laytivo.jsonl");
                if (File.Exists(file) && new FileInfo(file).Length >= 1024 * 1024)
                {
                    var oldest = file + ".4";
                    if (File.Exists(oldest)) File.Delete(oldest);
                    for (var i = 3; i >= 1; i--)
                        if (File.Exists(file + "." + i)) File.Move(file + "." + i, file + "." + (i + 1));
                    File.Move(file, file + ".1");
                }
                File.AppendAllText(file, JsonSerializer.Serialize(new { timeUtc = DateTimeOffset.UtcNow, action, details }) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Laytivo-Log: {ex.Message}");
            }
        }
    }
}
