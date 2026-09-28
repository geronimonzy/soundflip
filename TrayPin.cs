using System.Runtime.InteropServices;
using Microsoft.Win32;

// Windows remembers per tray icon whether the user pinned it to the visible part
// of the taskbar (HKCU\Control Panel\NotifyIconSettings\<id>, IsPromoted), keyed by
// the executable path. A Store (MSIX) update installs into a new versioned folder
// (...\WindowsApps\<name>_1.4.1.0_x64__<hash>\soundflip.exe), so after every update
// the icon gets a fresh, unpinned entry and falls back into the ^ overflow.
//
// CarryForward restores the user's own choice: if an older SoundFlip entry is
// pinned and the current one has never been decided, mark the current one pinned.
// It never pins on its own and never overrides an explicit unpin (IsPromoted = 0).
static class TrayPin
{
    const string SettingsKey = @"Control Panel\NotifyIconSettings";
    const string ExeName = "soundflip.exe";

    internal sealed record Entry(string Id, string Path, int? Promoted);

    public static void CarryForward()
    {
        string? current = Environment.ProcessPath;
        if (current is null) return;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
            if (root is null) return;

            var entries = new List<Entry>();
            foreach (string id in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(id);
                if (key?.GetValue("ExecutablePath") is not string path) continue;
                entries.Add(new Entry(id, ResolveKnownFolder(path), key.GetValue("IsPromoted") as int?));
            }

            string? target = EntryToPromote(entries, current);
            if (target is null) return;

            using var writable = Registry.CurrentUser.OpenSubKey($@"{SettingsKey}\{target}", writable: true);
            writable?.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
        }
        catch
        {
            // Undocumented shell state: if it isn't there or looks different, the
            // icon simply stays where Windows put it.
        }
    }

    // The entry for this exe to pin, or null. Pure so it can be unit-tested.
    internal static string? EntryToPromote(IReadOnlyList<Entry> entries, string currentPath)
    {
        var mine = entries.FirstOrDefault(e => string.Equals(e.Path, currentPath, StringComparison.OrdinalIgnoreCase));
        if (mine is null || mine.Promoted is not null) return null;

        bool pinnedBefore = entries.Any(e =>
            !ReferenceEquals(e, mine)
            && e.Promoted == 1
            && string.Equals(System.IO.Path.GetFileName(e.Path), ExeName, StringComparison.OrdinalIgnoreCase));

        return pinnedBefore ? mine.Id : null;
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
