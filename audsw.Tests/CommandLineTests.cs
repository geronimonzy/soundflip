namespace audsw.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Parse_NoArgs_LaunchesTray()
    {
        var command = CommandLine.Parse(Array.Empty<string>(), @"C:\work");

        Assert.Equal(Command.Tray, command.Kind);
        Assert.Null(command.DeviceQuery);
        Assert.Null(command.TargetDirectory);
    }

    [Fact]
    public void Parse_Set_JoinsRemainingArgsIntoDeviceQuery()
    {
        var command = CommandLine.Parse(["set", "USB", "DAC"], @"C:\work");

        Assert.Equal(Command.Set, command.Kind);
        Assert.Equal("USB DAC", command.DeviceQuery);
    }

    [Fact]
    public void Parse_ExportAssets_UsesDefaultStoreAssetsDirectory()
    {
        var command = CommandLine.Parse(["export-assets"], @"C:\repo");

        Assert.Equal(Command.ExportAssets, command.Kind);
        Assert.Equal(System.IO.Path.Combine(@"C:\repo", "Store", "Assets"), command.TargetDirectory);
    }

    [Fact]
    public void Parse_HelpAlias_ReturnsHelp()
    {
        var command = CommandLine.Parse(["--help"], @"C:\work");

        Assert.Equal(Command.Help, command.Kind);
    }

    [Fact]
    public void Parse_UnknownCommand_PreservesOriginalToken()
    {
        var command = CommandLine.Parse(["switchit"], @"C:\work");

        Assert.Equal(Command.Unknown, command.Kind);
        Assert.Equal("switchit", command.BadCommand);
    }

    [Fact]
    public void HelpText_IncludesSettingsPath()
    {
        string help = CommandLine.HelpText(@"C:\Users\me\AppData\Local\audsw\audsw.cfg");

        Assert.Contains("audsw help", help);
        Assert.Contains(@"C:\Users\me\AppData\Local\audsw\audsw.cfg", help);
    }
}
