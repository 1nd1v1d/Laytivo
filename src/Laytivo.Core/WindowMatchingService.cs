using System.Text.RegularExpressions;

namespace Laytivo.Core;

public static class WindowMatchingService
{
    public static Dictionary<string, LiveWindow> Match(IReadOnlyList<WindowSnapshot> saved, IReadOnlyList<LiveWindow> live)
    {
        var candidates = from entry in saved
                         from window in live
                         let score = Score(entry, window.Window)
                         where score > 0
                         orderby score descending, entry.Id, window.Handle
                         select (entry, window);
        var result = new Dictionary<string, LiveWindow>();
        var used = new HashSet<nint>();
        foreach (var (entry, window) in candidates)
        {
            if (result.ContainsKey(entry.Id) || !used.Add(window.Handle)) continue;
            result.Add(entry.Id, window);
        }
        return result;
    }

    private static int Score(WindowSnapshot saved, WindowSnapshot live)
    {
        var pathMatches = !string.IsNullOrWhiteSpace(saved.ProcessPath)
            && (string.Equals(saved.ProcessPath, live.ProcessPath, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(saved.ProcessName, live.ProcessName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(StablePath(saved.ProcessPath), StablePath(live.ProcessPath), StringComparison.OrdinalIgnoreCase)));
        var titleMatches = !string.IsNullOrWhiteSpace(saved.Title)
            && string.Equals(saved.Title, live.Title, StringComparison.OrdinalIgnoreCase);
        var classMatches = !string.IsNullOrWhiteSpace(saved.WindowClass)
            && string.Equals(saved.WindowClass, live.WindowClass, StringComparison.Ordinal);
        if (!pathMatches)
        {
            // An inaccessible executable requires all remaining identity clues. Never match a different known path.
            if ((!string.IsNullOrEmpty(saved.ProcessPath) && !string.IsNullOrEmpty(live.ProcessPath))
                || !titleMatches || !classMatches || string.IsNullOrWhiteSpace(saved.ProcessName)
                || !string.Equals(saved.ProcessName, live.ProcessName, StringComparison.OrdinalIgnoreCase)) return 0;
            return 60;
        }
        if (!classMatches) return 0;
        return 100 + (titleMatches ? 100 : 0);
    }

    // Only known version-directory layouts may change. Keep the installation root,
    // package publisher, architecture and executable path as identity boundaries.
    private static string StablePath(string path)
    {
        path = Regex.Replace(path, @"(?<=\\)app-\d+(?:\.\d+){1,3}(?=\\)", "app-{version}", RegexOptions.IgnoreCase);
        return Regex.Replace(path, @"(\\WindowsApps\\[^\\_]+)_\d+(?:\.\d+){3}(_[^\\]+\\)", "$1_{version}$2", RegexOptions.IgnoreCase);
    }
}
