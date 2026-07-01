// Records a fatal startup error to a log file and shows it in a message box, so a
// crash during launch is visible instead of the process silently vanishing.
static class StartupLog
{
    public static void Fail(Exception? ex)
    {
        string message = ex?.ToString() ?? "Unknown startup error.";

        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "audsw");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup.log"), $"[{DateTime.Now:u}] {message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort.
        }

        try
        {
            string shown = message.Length > 1600 ? message[..1600] + "\n..." : message;
            Win32.MessageBoxW(IntPtr.Zero, shown, "audsw failed to start", Win32.MB_ICONERROR);
        }
        catch
        {
            // Never let error reporting throw over the original failure.
        }
    }
}
