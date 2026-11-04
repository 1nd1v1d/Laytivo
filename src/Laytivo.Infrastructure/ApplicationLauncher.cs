using System.Diagnostics;
using Laytivo.Core;

namespace Laytivo.Infrastructure;

public sealed class ApplicationLauncher : IApplicationLauncher
{
    public void Launch(WindowSnapshot window)
    {
        if (!window.AutoLaunchSupported || !Path.IsPathFullyQualified(window.ProcessPath)
            || !string.Equals(Path.GetExtension(window.ProcessPath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kein unterstützter absoluter EXE-Pfad vorhanden.");
        if (!File.Exists(window.ProcessPath)) throw new FileNotFoundException("Die gespeicherte EXE wurde nicht gefunden.", window.ProcessPath);
        var start = new ProcessStartInfo(window.ProcessPath, window.LaunchArguments)
        {
            UseShellExecute = false,
            WorkingDirectory = Directory.Exists(window.WorkingDirectory)
                ? window.WorkingDirectory : Path.GetDirectoryName(window.ProcessPath)!
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows hat keinen Prozess gestartet.");
    }
}
