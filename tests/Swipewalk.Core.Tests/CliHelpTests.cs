namespace Swipewalk.Core.Tests;

/// <summary>
/// `swipewalk --help` is usually the first command anyone runs, and shells, scripts and CI read a
/// non-zero exit code as a failure. Asking for help must therefore succeed, while a mistyped command
/// must not. Program.cs is a top-level-statements executable rather than a library, so this reads it
/// as text, the same way <see cref="UserGuideTests"/> does.
/// </summary>
public class CliHelpTests
{
    private static string ProgramSource()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Swipewalk.Cli", "Program.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new InvalidOperationException("Could not find src/Swipewalk.Cli/Program.cs above AppContext.BaseDirectory.");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void AskingForHelp_IsTreatedAsSuccess(string argument)
    {
        var source = ProgramSource();
        var start = source.IndexOf("askedForHelp", StringComparison.Ordinal);
        Assert.True(start >= 0, "Program.cs no longer decides whether help was asked for; see askedForHelp.");
        var decision = source[start..source.IndexOf(';', start)];
        Assert.Contains($"\"{argument}\"", decision);
    }

    [Fact]
    public void UsageText_IsPrintedBeforeTheExitCodeIsChosen()
    {
        var source = ProgramSource();
        Assert.Contains("Console.WriteLine(Usage);\n    return askedForHelp ? 0 : 1;", source.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// `compare`'s "Not checked again" heading must cover both reasons a finding lands there (see
    /// ReportComparison.ScreenNotScannedAgainReason and .CheckDidNotRunReason) rather than naming only one, as it
    /// did before this test was added -- the heading used to say only "the check didn't run this time", which
    /// undersold findings whose screen was never scanned again.
    /// </summary>
    [Fact]
    public void CompareCommand_NotCheckedAgainHeading_MentionsBothReasons()
    {
        var source = ProgramSource();
        var start = source.IndexOf("Not checked again (", StringComparison.Ordinal);
        Assert.True(start >= 0, "Program.cs no longer has a \"Not checked again\" compare heading.");
        var heading = source[start..source.IndexOf(')', start)];
        Assert.Contains("rescan", heading, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("didn't run", heading, StringComparison.OrdinalIgnoreCase);
    }
}
