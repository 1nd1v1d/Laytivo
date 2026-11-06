using System.Runtime.InteropServices;
using Laytivo.Core;
using Microsoft.Win32;

namespace Laytivo.Infrastructure;

public sealed class AutostartService : IAutostartService
{
    private readonly string _runKey, _approvedRunKey, _approvedFolderKey, _shortcutPath;
    private readonly string? _executable;
    public AutostartService() : this(@"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved",
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), null)
    { }

    internal AutostartService(string runKey, string approvedRoot, string startupDirectory, string? executable)
    {
        _runKey = runKey;
        _approvedRunKey = approvedRoot + @"\Run";
        _approvedFolderKey = approvedRoot + @"\StartupFolder";
        _shortcutPath = Path.Combine(startupDirectory, "Laytivo.lnk");
        _executable = executable;
    }

    public bool IsEnabled() => File.Exists(_shortcutPath)
        ? IsOwnShortcut() && !IsDisabled(_approvedFolderKey, "Laytivo.lnk")
        : HasOwnLegacyEntry() && !IsDisabled(_approvedRunKey, "Laytivo");

    public bool MigrateLegacyEntry()
    {
        if (File.Exists(_shortcutPath) || !HasOwnLegacyEntry() || IsDisabled(_approvedRunKey, "Laytivo")) return false;
        SetEnabled(true);
        return true;
    }

    public void SetEnabled(bool enabled)
    {
        var executable = Executable();
        if (enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_shortcutPath)!);
            WithShortcut(_shortcutPath, link =>
            {
                link.TargetPath = executable;
                link.Arguments = "--startup";
                link.WorkingDirectory = Path.GetDirectoryName(executable)!;
                link.Description = "Laytivo nach der Windows-Anmeldung starten";
                link.IconLocation = executable + ",0";
                link.Save();
                return true;
            });
            if (!IsOwnShortcut()) throw new IOException("Die Windows-Autostart-Verknüpfung konnte nicht geprüft werden.");
            using var approved = Registry.CurrentUser.OpenSubKey(_approvedFolderKey, true);
            approved?.DeleteValue("Laytivo.lnk", false);
        }
        else if (File.Exists(_shortcutPath) && IsOwnShortcut()) File.Delete(_shortcutPath);

        // Remove only this installation's old Run entry, after the shortcut is saved.
        if (HasOwnLegacyEntry())
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKey, true);
            key?.DeleteValue("Laytivo", false);
        }
    }

    private bool HasOwnLegacyEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKey);
        return string.Equals(key?.GetValue("Laytivo") as string, $"\"{Executable()}\" --startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDisabled(string keyPath, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(name) is byte[] { Length: > 0 } status && (status[0] & 1) != 0;
    }

    private bool IsOwnShortcut() => WithShortcut(_shortcutPath, link =>
        string.Equals((string)link.TargetPath, Executable(), StringComparison.OrdinalIgnoreCase)
        && string.Equals((string)link.Arguments, "--startup", StringComparison.Ordinal)
        && string.Equals((string)link.WorkingDirectory, Path.GetDirectoryName(Executable()), StringComparison.OrdinalIgnoreCase));

    private static T WithShortcut<T>(string path, Func<dynamic, T> action)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows-Verknüpfungen sind nicht verfügbar.");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic? link = null;
        try { link = shell.CreateShortcut(path); return action(link); }
        finally
        {
            if (link is not null) Marshal.FinalReleaseComObject((object)link);
            Marshal.FinalReleaseComObject((object)shell);
        }
    }

    private string Executable()
    {
        var executable = _executable ?? Environment.ProcessPath ?? throw new InvalidOperationException("Der Laytivo-Programmpfad fehlt.");
        if (!Path.IsPathFullyQualified(executable)
            || !string.Equals(Path.GetFileName(executable), "Laytivo.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bitte Laytivo.exe direkt starten, um den Autostart zu konfigurieren.");
        return executable;
    }
}
