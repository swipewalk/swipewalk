using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Swipewalk.Collectors.Ios;

/// <summary>One element as Xcode's Accessibility Inspector reported it (see <see cref="IosInspectorWalk"/>);
/// the raw shape harness/mac-inspector-walk/InspectorWalk.swift writes as JSON, before it is matched to a
/// scanned tree node (see <see cref="IosInspectorCapture"/>).</summary>
public sealed record InspectorWalkItem(
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("traits")] IReadOnlyList<string>? Traits,
    [property: JsonPropertyName("identifier")] string? Identifier,
    [property: JsonPropertyName("hint")] string? Hint,
    [property: JsonPropertyName("className")] string? ClassName);

/// <summary>The whole walk's raw result, as harness/mac-inspector-walk/InspectorWalk.swift prints it. Never
/// represents a script crash (see <see cref="IosInspectorWalk.RunAsync"/>): every expected failure --
/// the Inspector not running, its window layout not found, no element selected -- comes back with
/// <see cref="Ok"/> false and <see cref="Error"/> explaining why, exit code 0.</summary>
public sealed record InspectorWalkResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("items")] IReadOnlyList<InspectorWalkItem> Items,
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("notCompleteReason")] string? NotCompleteReason,
    [property: JsonPropertyName("toolVersion")] string? ToolVersion)
{
    /// <summary>A result for when the script itself could not be run or its output could not be read at all
    /// (as opposed to the script running and reporting its own <see cref="Ok"/> false) -- a missing script,
    /// a non-zero exit, or output that isn't the JSON this expects.</summary>
    public static InspectorWalkResult Failed(string error) => new(false, error, [], false, error, null);
}

/// <summary>
/// Runs harness/mac-inspector-walk/InspectorWalk.swift, which walks Xcode's Accessibility Inspector's
/// Inspection menu ("Move to Next/Previous Item") over the macOS Accessibility (AX) API and reports what its
/// panel shows for each element -- the Inspector's own navigation order and the accessibility properties
/// VoiceOver would read, without VoiceOver itself running (see <see cref="ScreenReaderCapture"/>'s
/// <c>AccessibilityInspector</c> source). Requires a person to have already, once per Inspector session,
/// opened the Inspector, chosen the target device in its toolbar, and clicked the first element on the
/// app's screen -- there is no AX surface for any of those three steps (the toolbar's device picker exposes
/// no children, value or press action over the AX API, and no menu item selects a target -- see
/// <see cref="IosCollector.RunInspectorCaptureAsync"/> for the guided prompt that asks for it) -- and the
/// macOS Accessibility permission (see <see cref="IosAccessibilityPermission"/>) for whichever app is
/// responsible for this process.
/// </summary>
public static class IosInspectorWalk
{
    public const string ScriptRelativePath = "harness/mac-inspector-walk/InspectorWalk.swift";

    /// <summary>How long the script itself is given to finish its walk (see <c>--timeout-seconds</c>); this
    /// method's own process-wide bound is a little longer, to give the script room to report a clean timeout
    /// itself rather than being killed mid-write.</summary>
    private static readonly TimeSpan DefaultWalkTimeout = TimeSpan.FromSeconds(120);

    /// <param name="scriptPath">Path to InspectorWalk.swift; null finds it (see <see cref="FindScript"/>).</param>
    /// <param name="maxSteps">Upper bound on elements walked in each direction (rewind, then forward); 500 is
    /// a safety bound well above a typical screen's element count, so it should rarely matter in practice --
    /// it only guards against walking forever if the Inspector's own wrap detection somehow never
    /// triggers.</param>
    /// <param name="timeout">How long the whole walk may take before it's stopped -- see <see cref="DefaultWalkTimeout"/>.</param>
    public static async Task<InspectorWalkResult> RunAsync(
        string? scriptPath = null, int maxSteps = 500, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var script = scriptPath ?? FindScript();
        if (script is null)
            return InspectorWalkResult.Failed($"Could not find {ScriptRelativePath}.");

        var bound = timeout ?? DefaultWalkTimeout;
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "swift", script, "--max-steps", maxSteps.ToString(), "--timeout-seconds", ((int)bound.TotalSeconds).ToString() })
            info.ArgumentList.Add(arg);

        string output;
        int exitCode;
        try
        {
            (exitCode, output) = await IosCollector.RunAsync(info, bound + TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException ex)
        {
            return InspectorWalkResult.Failed($"The Accessibility Inspector walk did not finish: {ex.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return InspectorWalkResult.Failed("The Accessibility Inspector walk was cancelled.");
        }

        // The script prints exactly one JSON line on success (see InspectorWalk.swift's printResult); a
        // non-zero exit or unparsable output means it crashed before writing that line at all, distinct from
        // the script running fine and reporting its own ok:false (a normal, expected outcome -- see
        // InspectorWalkResult's remarks).
        var jsonLine = output.Split('\n').LastOrDefault(l => l.TrimStart().StartsWith('{'));
        if (exitCode != 0 || jsonLine is null)
            return InspectorWalkResult.Failed(
                $"The Accessibility Inspector walk script failed (exit {exitCode}): {FirstLine(output)}");

        try
        {
            return JsonSerializer.Deserialize<InspectorWalkResult>(jsonLine)
                ?? InspectorWalkResult.Failed("The Accessibility Inspector walk script produced no result.");
        }
        catch (JsonException ex)
        {
            return InspectorWalkResult.Failed($"The Accessibility Inspector walk script's output could not be read: {ex.Message}");
        }
    }

    private static string FirstLine(string text) => text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "(no output)";

    private static string? FindScript() => FindScript(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);

    internal static string? FindScript(string currentDirectory, string baseDirectory)
    {
        // Working in a Swipewalk checkout: run the script in place.
        for (var dir = new DirectoryInfo(currentDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ScriptRelativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        // Installed copies (dotnet tool, desktop app) carry the script next to the assemblies, or in the app
        // bundle's Resources folder -- same layout convention as harness/ios (see IosCollector.FindHarness).
        return new[] { baseDirectory, Path.Combine(baseDirectory, "..", "Resources") }
            .Select(root => Path.GetFullPath(Path.Combine(root, ScriptRelativePath)))
            .FirstOrDefault(File.Exists);
    }
}
