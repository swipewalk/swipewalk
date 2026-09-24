using System.Text.RegularExpressions;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Keeps docs/user-guide.md honest: every "swipewalk &lt;command&gt;" and every "--option" shown in
/// its code blocks must be a real command/option of the CLI. The guide has no reference to the CLI
/// project (Program.cs is a top-level-statements executable, not a library), so this reads
/// Program.cs as text, the same way <see cref="StandardsTests.DocsPage_IsUpToDate"/> reads
/// docs/standards.md: by walking up from AppContext.BaseDirectory until the repo root is found.
/// </summary>
public class UserGuideTests
{
    /// <summary>Options that belong to `dotnet run`/the shell rather than the swipewalk CLI, so they
    /// are legitimately absent from Program.cs's usage text even if they show up in an example.</summary>
    private static readonly HashSet<string> NotCliOptions = ["--project"];

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "docs", "user-guide.md")))
                return dir.FullName;
        throw new InvalidOperationException("Could not find docs/user-guide.md above AppContext.BaseDirectory.");
    }

    /// <summary>Concatenates the contents of every fenced (```) code block in a Markdown document.</summary>
    private static string CodeBlocks(string markdown)
    {
        var block = new List<string>();
        var inBlock = false;
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inBlock = !inBlock;
                continue;
            }
            if (inBlock)
                block.Add(line);
        }
        return string.Join('\n', block);
    }

    [Fact]
    public void EveryCommandAndOptionInTheGuide_IsARealCliCommandOrOption()
    {
        var root = RepoRoot();
        var guide = File.ReadAllText(Path.Combine(root, "docs", "user-guide.md"));
        var cliSource = File.ReadAllText(Path.Combine(root, "src", "Swipewalk.Cli", "Program.cs"));
        var code = CodeBlocks(guide);

        var commands = Regex.Matches(code, @"\bswipewalk\s+([a-z]+)\b")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Contains($"swipewalk {command}", cliSource));

        var options = Regex.Matches(code, @"--[a-zA-Z][a-zA-Z-]*")
            .Select(m => m.Value)
            .Distinct()
            .Where(option => !NotCliOptions.Contains(option))
            .ToList();
        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Contains(option, cliSource));
    }

    /// <summary>
    /// The other direction: every command and option the CLI actually accepts must be documented in the
    /// guide, not just the reverse. The source used here is Program.cs's <c>Usage</c> constant -- what
    /// <c>swipewalk --help</c> prints -- rather than re-deriving the option set from the option-parsing
    /// code (the dictionary keys read via <c>o.GetValueOrDefault</c>/<c>o.ContainsKey</c> throughout
    /// Program.cs). Usage is the more reliable single source for this direction because it's already
    /// required to be a complete, user-facing reference (it's what people see when the command is wrong
    /// or unknown), so keeping the guide in sync with it also keeps the guide in sync with `--help`
    /// itself. It isn't automatically complete, though: writing this test found that Usage itself was
    /// missing --result-bundle, an option ToScanOptions has always parsed (<see
    /// cref="EveryCommandAndOptionInTheGuide_IsARealCliCommandOrOption"/> already passed with it missing,
    /// because that test only checks the guide is a subset of all of Program.cs, not of Usage
    /// specifically) -- fixed by adding it to Usage in the same change that added this test.
    /// </summary>
    [Fact]
    public void EveryCommandAndOptionInUsage_IsInTheGuide()
    {
        var root = RepoRoot();
        var guide = File.ReadAllText(Path.Combine(root, "docs", "user-guide.md"));
        var usage = UsageText(File.ReadAllText(Path.Combine(root, "src", "Swipewalk.Cli", "Program.cs")));

        var commands = Regex.Matches(usage, @"\bswipewalk\s+([a-z]+)\b")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Contains($"swipewalk {command}", guide));

        var options = Regex.Matches(usage, @"--[a-zA-Z][a-zA-Z-]*")
            .Select(m => m.Value)
            .Distinct()
            .Where(option => !NotCliOptions.Contains(option))
            .ToList();
        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Contains(option, guide));
    }

    /// <summary>Extracts the body of Program.cs's <c>const string Usage = """ ... """;</c> raw string literal.</summary>
    private static string UsageText(string cliSource)
    {
        const string marker = "Usage = \"\"\"";
        var markerIndex = cliSource.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Program.cs no longer has a `const string Usage = \"\"\"` literal.");
        var bodyStart = cliSource.IndexOf('\n', markerIndex) + 1;
        var bodyEnd = cliSource.IndexOf("\"\"\";", bodyStart, StringComparison.Ordinal);
        Assert.True(bodyEnd >= 0, "Could not find the closing \"\"\"; for Program.cs's Usage literal.");
        return cliSource[bodyStart..bodyEnd];
    }
}
