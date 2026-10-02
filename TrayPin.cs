using System.Runtime.InteropServices;
using Microsoft.Win32;

// Windows remembers per tray icon whether the user pinned it to the visible part
// of the taskbar (HKCU\Control Panel\NotifyIconSettings\<id>, IsPromoted), keyed by
// the executable path. A Store (MSIX) update installs into a new versioned folder
// (...\WindowsApps\<name>_1.4.1.0_x64__<hash>\soundflip.exe), so after every update
// the icon gets a fresh, unpinned entry and falls back into the ^ overflow.
//
// SoundFlip doesn't restore the pin itself: a packaged app can only write that key
// with the restricted unvirtualizedResources capability, and the key is
// undocumented. It only reads it, to tell the user once that the pin was lost
// (PinNoticeDialog). Reads need no capability.
static class TrayPin
{
    const string SettingsKey = @"Control Panel\NotifyIconSettings";
    const string ExeName = "soundflip.exe";

    internal sealed record Entry(string Id, string Path, int? Promoted);

    // True when the previous installed version was pinned and this version's entry
    // is still undecided. False on any doubt: no entry yet, not a Store install,
    // or the shell keeps this somewhere else in a future Windows.
    public static bool PinLostInUpdate()
    {
        string? current = Environment.ProcessPath;
        if (current is null) return false;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
            if (root is null) return false;

            var entries = new List<Entry>();
            foreach (string id in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(id);
                if (key?.GetValue("ExecutablePath") is not string path) continue;
                entries.Add(new Entry(id, ResolveKnownFolder(path), key.GetValue("IsPromoted") as int?));
            }

            return PinLost(entries, current);
        }
        catch
        {
            return false;
        }
    }

    // Pure so it can be unit-tested. Compares against the newest earlier version of
    // the same package only: a user who saw the notice and chose not to re-pin
    // leaves that version undecided, so the next update stays quiet.
    internal static bool PinLost(IReadOnlyList<Entry> entries, string currentPath)
    {
        var mine = entries.FirstOrDefault(e => string.Equals(e.Path, currentPath, StringComparison.OrdinalIgnoreCase));
        if (mine is null || mine.Promoted is not null) return false;
        if (PackageOf(currentPath) is not { } current) return false;

        var previous = entries
            .Select(e => (Entry: e, Package: PackageOf(e.Path)))
            .Where(x => x.Package is { } p
                && string.Equals(p.Name, current.Name, StringComparison.OrdinalIgnoreCase)
                && p.Version < current.Version)
            .OrderByDescending(x => x.Package!.Value.Version)
            .Select(x => x.Entry)
            .FirstOrDefault();

        return previous?.Promoted == 1;
    }

    // Package name and version from an MSIX install path, e.g.
    // ...\WindowsApps\KirillFedorov.SoundFlip_1.4.1.0_x64__hash\soundflip.exe.
    // Null for anything else (unpackaged builds, other apps).
    internal static (string Name, Version Version)? PackageOf(string exePath)
    {
        if (!string.Equals(System.IO.Path.GetFileName(exePath), ExeName, StringComparison.OrdinalIgnoreCase)) return null;

        string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(exePath) ?? "");
        string[] parts = folder.Split('_');
        if (parts.Length != 5 || !Version.TryParse(parts[1], out var version)) return null;
        return (parts[0], version);
    }

    // The shell stores paths with a leading known-folder GUID, e.g.
    // "{6D809377-6AF0-444B-8957-A3773F02200E}\WindowsApps\..." for Program Files.
    static string ResolveKnownFolder(string path)
    {
        if (path.Length < 38 || path[0] != '{' || path[37] != '}') return path;
        if (!Guid.TryParse(path[..38], out var folder)) return path;
        if (SHGetKnownFolderPath(ref folder, 0, IntPtr.Zero, out var buffer) != 0) return path;
        try
        {
            return Marshal.PtrToStringUni(buffer) + path[38..];
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
