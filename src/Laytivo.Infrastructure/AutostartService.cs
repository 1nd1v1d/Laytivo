using Microsoft.Win32;

namespace Laytivo.Infrastructure;

public sealed class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue("Laytivo") as string, Command(), StringComparison.Ordinal);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("Der Autostart-Schlüssel konnte nicht geöffnet werden.");
        if (enabled) key.SetValue("Laytivo", Command(), RegistryValueKind.String);
        else key.DeleteValue("Laytivo", false);
    }

    private static string Command()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Der Laytivo-Programmpfad fehlt.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "Laytivo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bitte Laytivo.exe direkt starten, um den Autostart zu konfigurieren.");
        return $"\"{executable}\" --startup";
    }
}
