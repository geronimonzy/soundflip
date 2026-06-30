using Windows.ApplicationModel;

sealed record AutoStartStatus(bool Enabled, bool CanToggle, string Detail)
{
    public static readonly AutoStartStatus Loading = new(false, false, "Checking startup support...");

    public static AutoStartStatus Unsupported(string detail) => new(false, false, detail);

    public static AutoStartStatus Disabled(string detail = "Enable startup from the tray menu or Windows Startup Apps.") =>
        new(false, true, detail);

    public static AutoStartStatus EnabledStatus(string detail = "audsw will start when you sign in.") =>
        new(true, true, detail);

    public static AutoStartStatus DisabledByUser(string detail = "Startup was disabled by the user. Re-enable it from Windows Startup Apps.") =>
        new(false, false, detail);

    public static AutoStartStatus DisabledByPolicy(string detail = "Startup is disabled by system policy on this device.") =>
        new(false, false, detail);

    public static AutoStartStatus Error(string detail) => new(false, false, detail);
}

static class AutoStart
{
    public const string TaskId = "audswStartup";

    public static async Task<AutoStartStatus> GetStatusAsync()
    {
        if (!PackageIdentity.HasIdentity)
            return AutoStartStatus.Unsupported("Start with Windows is available only in packaged Store/MSIX builds.");

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
            return AutoStartStatus.Unsupported("Start with Windows is available only in packaged Store/MSIX builds.");

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
