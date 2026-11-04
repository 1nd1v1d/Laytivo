namespace Laytivo.Core;

public static class Geometry
{
    public static MonitorSnapshot SelectMonitor(MonitorSnapshot source, IReadOnlyList<MonitorSnapshot> current)
    {
        if (current.Count == 0) throw new InvalidOperationException("Kein aktiver Monitor gefunden.");
        return current.FirstOrDefault(m => string.Equals(m.Id, source.Id, StringComparison.OrdinalIgnoreCase))
            ?? current.FirstOrDefault(m => string.Equals(m.DeviceName, source.DeviceName, StringComparison.OrdinalIgnoreCase))
            ?? current.FirstOrDefault(m => m.IsPrimary) ?? current[0];
    }

    // All coordinates are physical desktop pixels under PerMonitorV2 awareness.
    public static DesktopRect AdaptWindow(WindowSnapshot window, MonitorSnapshot source, MonitorSnapshot target)
    {
        if (!window.IsSnapped || window.VisibleBounds is not { } visible) return Adapt(window.NormalBounds, source, target);
        // Clamp the visible frame, then add back the invisible resize borders. Clamping the outer
        // rectangle itself would move snapped windows inwards and introduce gaps between neighbours.
        var adapted = Adapt(visible, source, target);
        var ratio = target.DpiScale / source.DpiScale;
        var outer = window.NormalBounds;
        var left = (int)Math.Round((visible.X - outer.X) * ratio);
        var top = (int)Math.Round((visible.Y - outer.Y) * ratio);
        var right = (int)Math.Round((outer.X + outer.Width - visible.X - visible.Width) * ratio);
        var bottom = (int)Math.Round((outer.Y + outer.Height - visible.Y - visible.Height) * ratio);
        return new(adapted.X - left, adapted.Y - top, adapted.Width + left + right, adapted.Height + top + bottom);
    }

    public static DesktopRect Adapt(DesktopRect saved, MonitorSnapshot source, MonitorSnapshot target)
    {
        var from = source.WorkArea;
        var to = target.WorkArea;
        var dpiRatio = target.DpiScale / source.DpiScale;
        var width = Math.Clamp((int)Math.Round(saved.Width * dpiRatio), Math.Min(160, to.Width), to.Width);
        var height = Math.Clamp((int)Math.Round(saved.Height * dpiRatio), Math.Min(100, to.Height), to.Height);
        var x = to.X + (int)Math.Round((saved.X - (double)from.X) * to.Width / from.Width);
        var y = to.Y + (int)Math.Round((saved.Y - (double)from.Y) * to.Height / from.Height);
        return new(Math.Clamp(x, to.X, to.X + to.Width - width),
            Math.Clamp(y, to.Y, to.Y + to.Height - height), width, height);
    }
}
