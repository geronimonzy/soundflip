namespace audsw.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Parse_NoArgs_LaunchesTray()
    {
        var command = CommandLine.Parse(Array.Empty<string>());

        Assert.Equal(Command.Tray, command.Kind);
        Assert.Null(command.DeviceQuery);
    }

    [Fact]
    public void Parse_Set_JoinsRemainingArgsIntoDeviceQuery()
    {
        var command = CommandLine.Parse(["set", "USB", "DAC"]);

        Assert.Equal(Command.Set, command.Kind);
        Assert.Equal("USB DAC", command.DeviceQuery);
        Assert.Equal(AudioKind.Output, command.DeviceKind);
    }

    [Fact]
    public void Parse_Set_InputKind_ConsumesKindWord()
    {
        var command = CommandLine.Parse(["set", "input", "Yeti", "Mic"]);

        Assert.Equal(Command.Set, command.Kind);
        Assert.Equal(AudioKind.Input, command.DeviceKind);
        Assert.Equal("Yeti Mic", command.DeviceQuery);
    }

    [Fact]
    public void Parse_Set_OutputKind_ConsumesKindWord()
    {
        var command = CommandLine.Parse(["set", "output", "Speakers"]);

        Assert.Equal(AudioKind.Output, command.DeviceKind);
        Assert.Equal("Speakers", command.DeviceQuery);
    }

    [Fact]
    public void Parse_List_DefaultsToOutputs()
    {
        var command = CommandLine.Parse(["list"]);

        Assert.Equal(Command.List, command.Kind);
        Assert.Equal(AudioKind.Output, command.DeviceKind);
    }

    [Fact]
    public void Parse_List_Inputs()
    {
        var command = CommandLine.Parse(["list", "inputs"]);

        Assert.Equal(Command.List, command.Kind);
        Assert.Equal(AudioKind.Input, command.DeviceKind);
    }

    [Fact]
    public void Parse_Cycle_DefaultsToOutputs()
    {
        var command = CommandLine.Parse(["cycle"]);

        Assert.Equal(Command.Cycle, command.Kind);
        Assert.Equal(CycleScope.Outputs, command.Scope);
    }

    [Fact]
    public void Parse_Cycle_Inputs()
    {
        Assert.Equal(CycleScope.Inputs, CommandLine.Parse(["cycle", "inputs"]).Scope);
    }

    [Fact]
    public void Parse_Cycle_Pairs()
    {
        Assert.Equal(CycleScope.Pairs, CommandLine.Parse(["cycle", "pairs"]).Scope);
    }

    [Fact]
    public void Parse_HelpAlias_ReturnsHelp()
    {
        var command = CommandLine.Parse(["--help"]);

        Assert.Equal(Command.Help, command.Kind);
    }

    [Fact]
    public void Parse_UnknownCommand_PreservesOriginalToken()
    {
        var command = CommandLine.Parse(["switchit"]);

        Assert.Equal(Command.Unknown, command.Kind);
        Assert.Equal("switchit", command.BadCommand);
    }

    [Fact]
    public void HelpText_IncludesSettingsPath()
    {
        string help = CommandLine.HelpText(@"C:\Users\me\AppData\Local\audsw\audsw.json");

        Assert.Contains("audsw help", help);
        Assert.Contains(@"C:\Users\me\AppData\Local\audsw\audsw.json", help);
    }
}
