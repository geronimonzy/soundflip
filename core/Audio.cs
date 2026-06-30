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

    // --- IO pair: switch output and/or input together ---

    public readonly record struct PairResult(CoreAudioDevice? Output, CoreAudioDevice? Input)
    {
        public bool Any => Output is not null || Input is not null;
    }

    public static PairResult SetPair(CoreAudioController controller, string output, string input)
    {
        CoreAudioDevice? outputDevice = string.IsNullOrWhiteSpace(output) ? null : SetTo(controller, AudioKind.Output, output);
        CoreAudioDevice? inputDevice = string.IsNullOrWhiteSpace(input) ? null : SetTo(controller, AudioKind.Input, input);
        return new PairResult(outputDevice, inputDevice);
    }

    // Index of the pair whose output+input both already match the current defaults,
    // or -1 if none does (so cycling starts from the first pair).
    public static int CurrentPairIndex(CoreAudioController controller, IReadOnlyList<PairEntry> pairs)
    {
        var output = CurrentDefault(controller, AudioKind.Output);
        var input = CurrentDefault(controller, AudioKind.Input);

        for (int i = 0; i < pairs.Count; i++)
            if (PairSideMatches(output, pairs[i].Output) && PairSideMatches(input, pairs[i].Input))
                return i;
        return -1;
    }

    // The pair to switch to when cycling, or null if there are none.
    public static PairEntry? NextPair(CoreAudioController controller, IReadOnlyList<PairEntry> pairs)
    {
        if (pairs.Count == 0) return null;
        int index = CurrentPairIndex(controller, pairs);
        return pairs[NextIndex(index, pairs.Count)];
    }

    static bool PairSideMatches(CoreAudioDevice? device, string match) =>
        string.IsNullOrWhiteSpace(match)
            ? device is null
            : device != null && device.FullName.Contains(match, StringComparison.OrdinalIgnoreCase);
}
