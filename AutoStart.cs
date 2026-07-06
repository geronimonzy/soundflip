using Windows.ApplicationModel;

sealed record AutoStartStatus(bool Enabled, bool CanToggle, string Detail)
{
    public static readonly AutoStartStatus Loading = new(false, false, "Checking startup support...");

    public static AutoStartStatus Unsupported(string detail) => new(false, false, detail);

    public static AutoStartStatus Disabled(string detail = "Enable startup from the tray menu or Windows Startup Apps.") =>
        new(false, true, detail);

    public static AutoStartStatus EnabledStatus(string detail = "SoundFlip will start when you sign in.") =>
        new(true, true, detail);

    public static AutoStartStatus DisabledByUser(string detail = "Startup was disabled by the user. Re-enable it from Windows Startup Apps.") =>
        new(false, false, detail);

    public static AutoStartStatus DisabledByPolicy(string detail = "Startup is disabled by system policy on this device.") =>
        new(false, false, detail);

    public static AutoStartStatus Error(string detail) => new(false, false, detail);
}

// Packaged (Store/MSIX) builds go through the Windows StartupTask model so the
// user keeps control in Settings > Apps > Startup. Unpackaged builds fall back to
// the classic HKCU Run key, so the toggle works regardless of how the app was
// installed. The exe is a WinExe, so a login launch opens no window either way.
static class AutoStart
{
    public const string TaskId = "SoundFlipStartup";

    public static async Task<AutoStartStatus> GetStatusAsync()
    {
        if (!PackageIdentity.HasIdentity)
            return RunKey.GetStatus();

        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            return Describe(task.State);
        }
        catch (Exception ex)
        {
            return AutoStartStatus.Error("Startup task is unavailable: " + ex.Message);
        }
    }

    public static async Task<AutoStartStatus> SetEnabledAsync(bool enabled)
    {
        if (!PackageIdentity.HasIdentity)
            return RunKey.SetEnabled(enabled);

        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                StartupTaskState newState = task.State == StartupTaskState.Disabled
                    ? await task.RequestEnableAsync()
                    : task.State;
                return Describe(newState);
            }

            if (task.State == StartupTaskState.Enabled)
                task.Disable();

            StartupTask refreshed = await StartupTask.GetAsync(TaskId);
            return Describe(refreshed.State);
        }
        catch (Exception ex)
        {
            return AutoStartStatus.Error("Startup task is unavailable: " + ex.Message);
        }
    }

    static AutoStartStatus Describe(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => AutoStartStatus.EnabledStatus(),
        StartupTaskState.Disabled => AutoStartStatus.Disabled(),
        StartupTaskState.DisabledByUser => AutoStartStatus.DisabledByUser(),
        StartupTaskState.DisabledByPolicy => AutoStartStatus.DisabledByPolicy(),
        _ => AutoStartStatus.Error("Startup state could not be determined."),
    };
}

// Unpackaged autostart: a value under HKCU\...\Run pointing at this exe.
static class RunKey
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "SoundFlip";
    const string LegacyValueName = "audsw";

    public static AutoStartStatus GetStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KeyPath);
            MigrateLegacyValue(key);

            if (key.GetValue(ValueName) is not string existing)
                return AutoStartStatus.Disabled("Enable startup from the tray menu.");

            // Portable installs move the exe between versions/locations, so a Run value
            // that no longer matches the current exe would silently stop autostarting;
            // self-heal it in place instead of reporting a stale "enabled" status.
            if (Environment.ProcessPath is string exe &&
                !string.Equals(existing, QuotedPath(exe), StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, QuotedPath(exe));

            return AutoStartStatus.EnabledStatus();
        }
        catch (Exception ex)
        {
            return AutoStartStatus.Error("Startup registry entry is unavailable: " + ex.Message);
        }
    }

    // Pre-rename installs wrote an "audsw" value that points at an exe which no
    // longer exists after updating. Carry the enabled intent over to the new
    // value name (with the current exe path) and drop the stale entry.
    static void MigrateLegacyValue(Microsoft.Win32.RegistryKey key)
    {
        if (key.GetValue(LegacyValueName) is not string) return;

        if (key.GetValue(ValueName) is not string && Environment.ProcessPath is string exe)
            key.SetValue(ValueName, QuotedPath(exe));
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }

    static string QuotedPath(string exe) => $"\"{exe}\"";

    public static AutoStartStatus SetEnabled(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KeyPath);
            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe))
                    return AutoStartStatus.Error("Could not determine the path of the running executable.");
                key.SetValue(ValueName, QuotedPath(exe));
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return GetStatus();
        }
        catch (Exception ex)
        {
            return AutoStartStatus.Error("Startup registry entry is unavailable: " + ex.Message);
        }
    }
}

static class PackageIdentity
{
    public static bool HasIdentity
    {
        get
        {
            int length = 0;
            int result = NativeMethods.GetCurrentPackageFullName(ref length, null);
            return result != NativeMethods.APPMODEL_ERROR_NO_PACKAGE;
        }
    }
}
