using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Laytivo.Core;

namespace Laytivo.App;

public sealed class GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private readonly HwndSource _source;
    private int? _registeredId;
    private int _modifiers, _key;
    private bool _disposed;
    public bool IsRegistered => _registeredId is not null;
    internal nint WindowHandle => _source.Handle;
    internal int? RegistrationId => _registeredId;
    public event EventHandler? Pressed;

    public GlobalHotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("Laytivo.RestoreHotkey")
        {
            ParentWindow = new nint(-3), // HWND_MESSAGE: independent of the visible settings window.
            WindowStyle = 0,
            Width = 0,
            Height = 0
        });
        _source.AddHook(OnMessage);
    }

    public void Configure(AppSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _source.Dispatcher.VerifyAccess();
        if (!RestoreHotkey.IsValid(settings.RestoreHotkeyModifiers, settings.RestoreHotkeyKey))
            throw new InvalidOperationException("Ungültiger Tastatur-Shortcut.");
        if (!settings.RestoreHotkeyEnabled)
        {
            if (_registeredId is { } previous && !UnregisterHotKey(_source.Handle, previous))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der bisherige Tastatur-Shortcut konnte nicht freigegeben werden.");
            _registeredId = null;
            return;
        }
        if (_registeredId is not null && _modifiers == settings.RestoreHotkeyModifiers && _key == settings.RestoreHotkeyKey) return;

        // Keep the previous combination until the new registration has succeeded.
        var nextId = _registeredId == 1 ? 2 : 1;
        if (!RegisterHotKey(_source.Handle, nextId, (uint)settings.RestoreHotkeyModifiers | 0x4000, (uint)settings.RestoreHotkeyKey))
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Shortcut {RestoreHotkey.Describe(settings)} konnte nicht aktiviert werden (möglicherweise bereits belegt). Bitte eine andere Kombination wählen.");
        if (_registeredId is { } oldId && !UnregisterHotKey(_source.Handle, oldId))
        {
            var error = Marshal.GetLastWin32Error();
            UnregisterHotKey(_source.Handle, nextId);
            throw new Win32Exception(error, "Der bisherige Tastatur-Shortcut konnte nicht freigegeben werden.");
        }
        _registeredId = nextId;
        _modifiers = settings.RestoreHotkeyModifiers;
        _key = settings.RestoreHotkeyKey;
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && _registeredId is { } id && wParam == id
            && ((long)lParam & 0x000f) == _modifiers && (((long)lParam >> 16) & 0xffff) == _key)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _source.Dispatcher.VerifyAccess();
        if (_registeredId is { } id) UnregisterHotKey(_source.Handle, id);
        _registeredId = null;
        _source.RemoveHook(OnMessage);
        _source.Dispose();
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
