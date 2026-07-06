using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

enum AudioKind { Output, Input }

static class Audio
{
    // --- Device enumeration / resolution, generalized over output vs input ---

    public static IReadOnlyList<CoreAudioDevice> Devices(CoreAudioController controller, AudioKind kind) =>
        (kind == AudioKind.Output
            ? controller.GetPlaybackDevices(DeviceState.Active)
            : controller.GetCaptureDevices(DeviceState.Active))
        .ToList();

    public static CoreAudioDevice? CurrentDefault(CoreAudioController controller, AudioKind kind) =>
        kind == AudioKind.Output ? controller.DefaultPlaybackDevice : controller.DefaultCaptureDevice;

    public static CoreAudioDevice? Resolve(CoreAudioController controller, AudioKind kind, string query) =>
        string.IsNullOrWhiteSpace(query)
            ? null
            : Devices(controller, kind)
                .FirstOrDefault(device => device.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));

    // Sets both the default and default-communications roles so call/voice apps
    // (Teams, Discord, Zoom, ...) follow the switch, for outputs and inputs alike.
    public static void MakeDefault(CoreAudioDevice device)
    {
        device.SetAsDefault();
        device.SetAsDefaultCommunications();
    }

    // --- Direct jump to a single device ---

    public static CoreAudioDevice? SetTo(CoreAudioController controller, AudioKind kind, string query)
    {
        var device = Resolve(controller, kind, query);
        if (device is null) return null;
        MakeDefault(device);
        return device;
    }

    // --- Ring cycling over N configured devices of one kind ---

    // Advance to the next resolvable device after the current default. Returns the
    // device switched to, or null if fewer than one ring entry resolves.
    public static CoreAudioDevice? CycleRing(CoreAudioController controller, AudioKind kind, IReadOnlyList<string> ring)
    {
        var resolved = ring
            .Select(match => Resolve(controller, kind, match))
            .Where(device => device is not null)
            .Select(device => device!)
            // Two ring entries can resolve to the same device; collapse them (first
            // occurrence wins) so the cycle never degenerates into a no-op.
            .DistinctBy(device => device.Id)
            .ToList();

        if (resolved.Count == 0) return null;

        var current = CurrentDefault(controller, kind);
        int currentIndex = current is null ? -1 : resolved.FindIndex(device => device.Id == current.Id);

        var target = resolved[NextIndex(currentIndex, resolved.Count)];
        MakeDefault(target);
        return target;
    }

    // Pure ring-advance math: next index after `current` in a ring of `count`.
    // A current of -1 (not in the ring) starts the ring at 0.
    public static int NextIndex(int current, int count)
    {
        if (count <= 0) return 0;
        if (current < 0) return 0;
        return (current + 1) % count;
    }
}
