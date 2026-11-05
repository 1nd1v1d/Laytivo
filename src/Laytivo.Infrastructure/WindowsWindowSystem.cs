using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Laytivo.Core;
using static Laytivo.Infrastructure.NativeMethods;

namespace Laytivo.Infrastructure;

public sealed class WindowsWindowSystem : IWindowSystem
{
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "#32770", "tooltips_class32",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
    };

    public IReadOnlyList<MonitorSnapshot> GetMonitors() => ReadMonitors().Values.ToArray();

    private static Dictionary<nint, MonitorSnapshot> ReadMonitors()
    {
        var result = new Dictionary<nint, MonitorSnapshot>();
        EnumDisplayMonitors(0, 0, (nint handle, nint _, ref Rect bounds, nint parameter) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), DeviceName = "" };
            if (!GetMonitorInfo(handle, ref info)) return true;
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>(), DeviceName = "", DeviceString = "", DeviceId = "", DeviceKey = "" };
            var id = EnumDisplayDevices(info.DeviceName, 0, ref device, 1) && !string.IsNullOrWhiteSpace(device.DeviceId)
                ? device.DeviceId : info.DeviceName;
            // Duplicate hardware IDs (e.g. identical panels) retain a per-display fallback.
            if (result.Values.Any(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))) id += "|" + info.DeviceName;
            var scale = GetScaleFactorForMonitor(handle, out var percent) == 0 ? percent / 100.0 : 1.0;
            result[handle] = new(id, info.DeviceName, Convert(info.Monitor), Convert(info.Work), scale, (info.Flags & 1) != 0);
            return true;
        }, 0);
        return result;
    }

    public IReadOnlyList<LiveWindow> GetWindows()
    {
        var monitors = ReadMonitors();
        var result = new List<LiveWindow>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || GetWindow(handle, 4) != 0) return true;
            var style = (long)GetWindowLongPtr(handle, -20);
            if ((style & 0x80) != 0 || (style & 0x08000000) != 0) return true; // tool/no-activate windows
            if (DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            GetWindowThreadProcessId(handle, out var pid);
            if (pid == Environment.ProcessId || pid == 0) return true;
            var title = new StringBuilder(1024);
            var className = new StringBuilder(256);
            GetWindowText(handle, title, title.Capacity);
            GetClassName(handle, className, className.Capacity);
            if (title.Length == 0 || ExcludedClasses.Contains(className.ToString())) return true;
            var placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
            if (!GetWindowPlacement(handle, ref placement)) return true;
            var monitor = monitors.GetValueOrDefault(MonitorFromWindow(handle, 2));
            if (monitor is null) return true;
            var path = "";
            var processName = "";
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
                path = process.MainModule?.FileName ?? "";
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { }
            if (processName is "ShellExperienceHost" or "StartMenuExperienceHost" or "SearchHost" or "LockApp") return true;
            var normal = placement.NormalPosition;
            // WINDOWPLACEMENT uses workspace coordinates. Convert only the taskbar offset, not the monitor origin.
            var rect = new DesktopRect(normal.Left + monitor.WorkArea.X - monitor.Bounds.X,
                normal.Top + monitor.WorkArea.Y - monitor.Bounds.Y, normal.Right - normal.Left, normal.Bottom - normal.Top);
            // Snap keeps the pre-snap restore rectangle in WINDOWPLACEMENT. A normal window must
            // be captured at its current screen rectangle, which already includes the workspace offset.
            // Minimized/maximized windows still need their normal restore bounds rather than their live bounds.
            if (placement.ShowCommand == 1)
            {
                if (!GetWindowRect(handle, out var current)) return true;
                rect = Convert(current);
            }
            if (!rect.IsValid) return true;
            var state = placement.ShowCommand == 3 || (placement.ShowCommand == 2 && (placement.Flags & 2) != 0)
                ? SavedWindowState.Maximized : SavedWindowState.Normal;
            var snapped = placement.ShowCommand == 1 && IsWindowArranged(handle);
            DesktopRect? visible = null;
            if (placement.ShowCommand == 1 && DwmGetFrameBounds(handle, 9, out var frame, Marshal.SizeOf<Rect>()) == 0)
            {
                var candidate = Convert(frame);
                if (candidate.IsValid && candidate.X >= rect.X && candidate.Y >= rect.Y
                    && candidate.X + candidate.Width <= rect.X + rect.Width && candidate.Y + candidate.Height <= rect.Y + rect.Height
                    && rect.Width - candidate.Width <= 400 && rect.Height - candidate.Height <= 400) visible = candidate;
            }
            var launchSupported = !string.IsNullOrEmpty(path) && File.Exists(path)
                && !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
                && !new[] { "ApplicationFrameHost", "steamwebhelper", "msedgewebview2", "rundll32", "dllhost", "RuntimeBroker" }
                    .Contains(processName, StringComparer.OrdinalIgnoreCase);
            result.Add(new(handle, (int)pid, new(Guid.NewGuid().ToString("N"), path, processName, "",
                Path.GetDirectoryName(path) ?? "", title.ToString(), className.ToString(), monitor.Id, rect, state, launchSupported, snapped, visible)));
            return true;
        }, 0);
        return result;
    }

    public WindowPlacementResult PlaceWindow(LiveWindow window, DesktopRect bounds, SavedWindowState state, DesktopRect? visibleBounds = null)
    {
        GetWindowThreadProcessId(window.Handle, out var pid);
        if (!IsWindow(window.Handle) || pid != window.ProcessId)
            throw new InvalidOperationException("Das Fenster wurde inzwischen geschlossen oder ersetzt.");
        var monitors = ReadMonitors();
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        var target = monitors.Values.FirstOrDefault(m => centerX >= m.Bounds.X && centerY >= m.Bounds.Y
            && centerX < m.Bounds.X + m.Bounds.Width && centerY < m.Bounds.Y + m.Bounds.Height)
            ?? throw new InvalidOperationException("Der Zielmonitor ist nicht mehr verbunden.");
        var placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
        if (!GetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (state == SavedWindowState.Normal && placement.ShowCommand == 1
            && (visibleBounds is { } targetFrame
                ? DwmGetFrameBounds(window.Handle, 9, out var existingFrame, Marshal.SizeOf<Rect>()) == 0 && Near(Convert(existingFrame), targetFrame)
                : GetWindowRect(window.Handle, out var existing) && Near(Convert(existing), bounds)))
            return new(IsWindowArranged(window.Handle));
        var preserveSnap = state == SavedWindowState.Normal && IsWindowArranged(window.Handle);
        placement.Flags = 4; // WPF_ASYNCWINDOWPLACEMENT; never wait for a hung target UI thread.
        placement.ShowCommand = 1;
        var x = bounds.X - (target.WorkArea.X - target.Bounds.X);
        var y = bounds.Y - (target.WorkArea.Y - target.Bounds.Y);
        placement.NormalPosition = new Rect { Left = x, Top = y, Right = x + bounds.Width, Bottom = y + bounds.Height };
        // SW_SHOWNORMAL via SetWindowPlacement clears an existing shell arrangement, even when
        // the saved rectangle is identical. Moving an arranged normal window only needs SetWindowPos.
        if (!preserveSnap && !SetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!SetWindowPos(window.Handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0004 | 0x0010 | 0x4000))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (state == SavedWindowState.Maximized)
        {
            placement.ShowCommand = 3;
            if (!SetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        // Native async calls only acknowledge queuing. Confirm the resulting placement within a bounded interval.
        var watch = Stopwatch.StartNew();
        DesktopRect? lastApplied = null;
        var lastCorrection = TimeSpan.FromMilliseconds(-100);
        var requested = bounds;
        var lastRequest = TimeSpan.Zero;
        do
        {
            var actual = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
            if (GetWindowPlacement(window.Handle, ref actual)
                && actual.ShowCommand == (state == SavedWindowState.Maximized ? 3 : 1))
            {
                var applied = state == SavedWindowState.Normal && GetWindowRect(window.Handle, out var current)
                    ? Convert(current) : new DesktopRect(actual.NormalPosition.Left + target.WorkArea.X - target.Bounds.X,
                        actual.NormalPosition.Top + target.WorkArea.Y - target.Bounds.Y,
                        actual.NormalPosition.Right - actual.NormalPosition.Left, actual.NormalPosition.Bottom - actual.NormalPosition.Top);
                lastApplied = applied;
                if (state == SavedWindowState.Normal && visibleBounds is { } wanted
                    && GetWindowRect(window.Handle, out var outer)
                    && DwmGetFrameBounds(window.Handle, 9, out var frame, Marshal.SizeOf<Rect>()) == 0)
                {
                    // Do not accept an old frame while an asynchronous move is still queued.
                    if (!Near(Convert(outer), requested) || watch.Elapsed - lastRequest < TimeSpan.FromMilliseconds(100))
                    { Thread.Sleep(25); continue; }
                    var actualVisible = Convert(frame);
                    if (Near(actualVisible, wanted)) return new(IsWindowArranged(window.Handle));
                    // Snap and normal windows may have different invisible borders. Measure the
                    // current frame after the move (and DPI transition), then correct in physical pixels.
                    if (watch.Elapsed - lastCorrection >= TimeSpan.FromMilliseconds(100))
                    {
                        var corrected = new DesktopRect(outer.Left + wanted.X - frame.Left, outer.Top + wanted.Y - frame.Top,
                            outer.Right - outer.Left + wanted.Width - actualVisible.Width,
                            outer.Bottom - outer.Top + wanted.Height - actualVisible.Height);
                        if (corrected.IsValid && !SetWindowPos(window.Handle, 0, corrected.X, corrected.Y,
                            corrected.Width, corrected.Height, 0x0004 | 0x0010 | 0x4000))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        requested = corrected;
                        lastRequest = watch.Elapsed;
                        lastCorrection = watch.Elapsed;
                    }
                }
                else if (Near(applied, bounds) && watch.Elapsed >= TimeSpan.FromMilliseconds(100)) return new(IsWindowArranged(window.Handle));
            }
            Thread.Sleep(25);
        } while (watch.Elapsed < TimeSpan.FromMilliseconds(1000));
        throw new InvalidOperationException($"Die Anwendung hat die Zielposition/-größe oder den Fensterzustand nicht übernommen. Ziel: {bounds}; Ist: {lastApplied}.");
    }

    private static DesktopRect Convert(Rect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    private static bool Near(DesktopRect actual, DesktopRect expected) => Math.Abs(actual.X - expected.X) <= 1
        && Math.Abs(actual.Y - expected.Y) <= 1 && Math.Abs(actual.Width - expected.Width) <= 1 && Math.Abs(actual.Height - expected.Height) <= 1;
}
