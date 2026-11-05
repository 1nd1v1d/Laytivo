using Laytivo.Core;
using Microsoft.Win32;

namespace Laytivo.Infrastructure;

public sealed class AutostartService : IAutostartService
{
    private readonly string _runKey;
    private readonly string _approvedKey;
    private readonly string? _executable;
    public AutostartService() : this(@"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", null)
    { }

    internal AutostartService(string runKey, string approvedKey, string? executable)
    { _runKey = runKey; _approvedKey = approvedKey; _executable = executable; }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKey);
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey);
        var disabled = approved?.GetValue("Laytivo") is byte[] { Length: > 0 } status && (status[0] & 1) != 0;
        return !disabled && string.Equals(key?.GetValue("Laytivo") as string, Command(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKey, true)
            ?? throw new InvalidOperationException("Der Autostart-Schlüssel konnte nicht geöffnet werden.");
        if (enabled)
        {
            var command = Command();
            if (command.Length > 260) throw new InvalidOperationException("Der EXE-Pfad ist für Windows-Autostart zu lang. Bitte Laytivo in einen kürzeren Pfad installieren.");
            key.SetValue("Laytivo", command, RegistryValueKind.String);
            // Explicitly enabling in Laytivo also repairs a Task Manager disabled marker.
            using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey, true);
            approved?.DeleteValue("Laytivo", false);
        }
        else key.DeleteValue("Laytivo", false);
    }

    private string Command()
    {
        var executable = _executable ?? Environment.ProcessPath ?? throw new InvalidOperationException("Der Laytivo-Programmpfad fehlt.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "Laytivo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bitte Laytivo.exe direkt starten, um den Autostart zu konfigurieren.");
        return $"\"{executable}\" --startup";
    }
}
