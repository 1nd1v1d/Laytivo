using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Laytivo.Core;
using Laytivo.Infrastructure;

namespace Laytivo.DesktopSmoke;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--fixture")
        {
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var form = new Form
            {
                Text = $"Laytivo-Test {args[1]} {args[2]}",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(80, 90, 640, 460)
            };
            form.Controls.Add(new Label { Text = "Temporäres Laytivo-Testfenster", AutoSize = true, Location = new(30, 35) });
            if (args[2] == "Maximized") form.Shown += (_, _) => form.WindowState = FormWindowState.Maximized;
            System.Windows.Forms.Application.Run(form);
            return 0;
        }
        if (args.Length != 2 || args[0] != "--run")
        {
            if (args.Length == 2 && args[0] == "--verify-snap-capture")
                return VerifySnapCapture(Path.GetFullPath(args[1]));
            Console.WriteLine("Nutzung: Laytivo.DesktopSmoke.exe --run <Ergebnisverzeichnis>. Öffnet und schließt ausschließlich eigene Testfenster.");
            return 2;
        }
        return RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();
    }

    private static int VerifySnapCapture(string output)
    {
        var system = new WindowsWindowSystem();
        var checks = new List<string>();
        Directory.CreateDirectory(output);
        try
        {
            var monitors = system.GetMonitors();
            var candidates = system.GetWindows();
            // Read an already snapped window; never move or start user apps in this regression check.
            var window = candidates.FirstOrDefault(w =>
                {
                    var position = new Placement { Length = Marshal.SizeOf<Placement>() };
                    if (!GetWindowPlacement(w.Handle, ref position) || position.ShowCommand != 1
                        || !GetWindowRect(w.Handle, out var bounds)) return false;
                    var display = monitors.Single(m => m.Id == w.Window.MonitorId);
                    return !Near(ToBounds(bounds), new(position.Normal.Left + display.WorkArea.X - display.Bounds.X,
                        position.Normal.Top + display.WorkArea.Y - display.Bounds.Y,
                        position.Normal.Right - position.Normal.Left, position.Normal.Bottom - position.Normal.Top));
                }) ?? throw new InvalidOperationException("Testvoraussetzung fehlt: Bitte ein Fenster mit Windows-Snap andocken und den Test erneut ausführen.");
            Check(window.Window.State == SavedWindowState.Normal, "Angedocktes Fenster ist nicht maximiert", checks);
            Check(GetWindowRect(window.Handle, out var actual), "Aktuelle native Fenstergrenzen gelesen", checks);
            var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
            Check(GetWindowPlacement(window.Handle, ref placement), "Native Wiederherstellungsposition gelesen", checks);
            var monitor = system.GetMonitors().Single(m => m.Id == window.Window.MonitorId);
            var oldRestoreBounds = new DesktopRect(placement.Normal.Left + monitor.WorkArea.X - monitor.Bounds.X,
                placement.Normal.Top + monitor.WorkArea.Y - monitor.Bounds.Y,
                placement.Normal.Right - placement.Normal.Left, placement.Normal.Bottom - placement.Normal.Top);
            var actualBounds = ToBounds(actual);
            Check(!Near(oldRestoreBounds, actualBounds), "Regression reproduziert: Snap-Position unterscheidet sich von alter Wiederherstellungsposition", checks);
            var workspace = new WorkspaceCaptureService(system).Capture([new(window, "", AppContext.BaseDirectory)]);
            var store = new ConfigurationStore(Path.Combine(output, "data"));
            store.Save(new(1, new(LaunchMissingApps: false), workspace));
            var configuration = store.Load();
            var saved = configuration.Workspace!.Windows.Single();
            Check(Near(saved.NormalBounds, actualBounds), "Speichern übernimmt aktuelle Snap-Geometrie statt Ausgangsposition", checks);
            Check(saved.IsSnapped, "Windows-Andockzustand wird gespeichert", checks);
            Check(saved.VisibleBounds is not null, "Sichtbare Rahmengeometrie wird gespeichert", checks);
            Check(Near(Geometry.AdaptWindow(saved, monitor, monitor), actualBounds),
                "Snap-Restore plant die identische Geometrie ohne Rahmenlücken", checks);
            // With an identical target, restoration must be a no-op and preserve the existing shell arrangement.
            var applied = system.PlaceWindow(window, actualBounds, SavedWindowState.Normal);
            Check(applied.IsSnapped && IsWindowArranged(window.Handle), "Vorhandene native Andockung bleibt bei Wiederherstellung erhalten", checks);
            Check(GetWindowRect(window.Handle, out var after) && Near(ToBounds(after), actualBounds),
                "Vorhandenes Snap-Fenster bleibt an identischer Position", checks);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            { passed = true, timeUtc = DateTimeOffset.UtcNow, oldRestoreBounds, actualBounds, saved.NormalBounds, checks },
                new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = false, checks, error = ex.ToString() }));
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Placement
    {
        public int Length, Flags, ShowCommand;
        public NativePoint Min, Max;
        public NativeRect Normal;
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowArranged(nint window);
    private static DesktopRect ToBounds(NativeRect bounds) => new(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);

    private static async Task<int> RunAsync(string output)
    {
        var system = new WindowsWindowSystem();
        var token = Guid.NewGuid().ToString("N");
        var processes = new List<Process>();
        Directory.CreateDirectory(output);
        var log = new LocalLog(Path.Combine(output, "logs"));
        var checks = new List<string>();
        try
        {
            var monitors = system.GetMonitors();
            Check(monitors.Count > 0, "Aktive Monitore erkannt", checks);
            Console.WriteLine(JsonSerializer.Serialize(monitors));
            foreach (var role in new[] { "Normal", "Maximized" })
            {
                processes.Add(Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--fixture {token} {role}")
                { UseShellExecute = false, CreateNoWindow = true })!);
            }
            var live = await WaitAsync(system, token, 2);
            Check(live.Count == 2, "Zwei eigene Fenster erkannt", checks);
            Check(live.Any(w => w.Window.State == SavedWindowState.Maximized), "Maximierten Zustand erfasst", checks);
            var selection = live.Select(w => new CaptureSelection(w,
                $"--fixture {token} {(w.Window.Title.EndsWith("Maximized", StringComparison.Ordinal) ? "Maximized" : "Normal")}",
                AppContext.BaseDirectory)).ToArray();
            var workspace = new WorkspaceCaptureService(system).Capture(selection);
            var configuration = new AppConfiguration(1, new(), workspace);
            var store = new ConfigurationStore(Path.Combine(output, "data"));
            store.Save(configuration);
            configuration = store.Load();
            Check(configuration.Workspace!.Windows.Count == 2, "Profil dauerhaft gespeichert und geladen", checks);
            var normal = live.Single(w => w.Window.State == SavedWindowState.Normal);
            var monitor = monitors.First(m => m.Id == normal.Window.MonitorId);
            system.PlaceWindow(normal, new(monitor.WorkArea.X + 20, monitor.WorkArea.Y + 30, 500, 350), SavedWindowState.Normal);
            var restore = new WorkspaceRestoreService(system, new ApplicationLauncher(), log,
                new(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400)));
            var report = await restore.RestoreAsync(configuration);
            Check(report.Results.All(r => r.Outcome == RestoreOutcome.Restored), "Bestehende Fenster wiederhergestellt", checks);
            await Task.Delay(600);
            var restored = system.GetWindows().Where(w => w.Window.Title.Contains(token, StringComparison.Ordinal)).ToArray();
            foreach (var entry in configuration.Workspace.Windows)
            {
                var current = restored.Single(w => w.Window.Title == entry.Title);
                Check(current.Window.State == entry.State, $"Zustand geprüft: {entry.State}", checks);
                Check(Near(current.Window.NormalBounds, entry.NormalBounds), $"Normalgeometrie geprüft: {entry.State}", checks);
            }
            // Exercise physical placement on every currently connected display, including negative origins.
            foreach (var target in monitors)
            {
                var desired = Geometry.Adapt(normal.Window.NormalBounds, monitor, target);
                system.PlaceWindow(normal, desired, SavedWindowState.Normal);
                await Task.Delay(400);
                var actual = system.GetWindows().Single(w => w.Handle == normal.Handle).Window;
                Check(actual.MonitorId == target.Id && Near(actual.NormalBounds, desired), $"Positionierung auf {target.DeviceName} bei {target.DpiScale:P0}", checks);
            }
            var closed = processes.Single(p => p.Id == normal.ProcessId);
            closed.Kill();
            await closed.WaitForExitAsync();
            report = await restore.RestoreAsync(configuration);
            Check(report.Results.All(r => r.Outcome == RestoreOutcome.Restored), "Fehlende Testanwendung automatisch gestartet", checks);
            var afterLaunch = await WaitAsync(system, token, 2);
            Check(afterLaunch.Count == 2, "Keine doppelten Testfenster nach Programmstart", checks);
            report = await restore.RestoreAsync(configuration);
            Check(report.Results.All(r => r.Outcome == RestoreOutcome.Restored)
                && system.GetWindows().Count(w => w.Window.Title.Contains(token, StringComparison.Ordinal)) == 2,
                "Erneute Wiederherstellung ohne Doppelstart", checks);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            { passed = true, timeUtc = DateTimeOffset.UtcNow, monitors, checks }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Desktop-Smoke erfolgreich: {checks.Count} Prüfungen auf {monitors.Count} Monitoren.");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = false, checks, error = ex.ToString() }));
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            foreach (var window in system.GetWindows().Where(w => w.Window.Title.Contains(token, StringComparison.Ordinal)))
            {
                try { using var process = Process.GetProcessById(window.ProcessId); process.Kill(); } catch (ArgumentException) { }
            }
            foreach (var process in processes)
            {
                if (!process.HasExited) process.Kill();
                process.Dispose();
            }
        }
    }

    private static async Task<IReadOnlyList<LiveWindow>> WaitAsync(WindowsWindowSystem system, string token, int count)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            var live = system.GetWindows().Where(w => w.Window.Title.Contains(token, StringComparison.Ordinal)).ToArray();
            if (live.Length == count) return live;
            await Task.Delay(200);
        }
        throw new TimeoutException("Testfenster wurden nicht rechtzeitig verfügbar.");
    }
    private static bool Near(DesktopRect actual, DesktopRect expected) => Math.Abs(actual.X - expected.X) <= 3
        && Math.Abs(actual.Y - expected.Y) <= 3 && Math.Abs(actual.Width - expected.Width) <= 3 && Math.Abs(actual.Height - expected.Height) <= 3;
    private static void Check(bool condition, string description, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description);
        Console.WriteLine("OK: " + description);
    }
}
