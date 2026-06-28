using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

static class Audio
{
    // Core toggle. Returns the device switched to, or null if either configured
    // device cannot be resolved.
    public static CoreAudioDevice? DoCycle(CoreAudioController controller, AppSettings settings)
    {
        var device1 = Resolve(controller, settings.Device1);
        var device2 = Resolve(controller, settings.Device2);
        if (device1 is null || device2 is null) return null;

        var current = controller.DefaultPlaybackDevice;
        var target = current != null && current.Id == device1.Id ? device2 : device1;
        MakeDefault(target);
        return target;
    }

    public static CoreAudioDevice? Resolve(CoreAudioController controller, string query) =>
        string.IsNullOrWhiteSpace(query)
            ? null
            : controller.GetPlaybackDevices(DeviceState.Active)
                .FirstOrDefault(device => device.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));

    public static void MakeDefault(CoreAudioDevice device)
    {
        device.SetAsDefault();
        device.SetAsDefaultCommunications();
    }
}
