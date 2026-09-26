using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Swipewalk.Collectors.Ios;

/// <summary>A rectangle in a screenshot's own TOP-LEFT-origin pixel space, as
/// harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift reports it (a caption panel region, or a
/// best-effort VoiceOver focus-cursor detection -- see <see cref="VoiceOverCaptionItem.FocusRect"/>'s
/// remarks). Not yet in <see cref="Swipewalk.Core.Model.Bounds"/>'s tree-unit space -- see
/// <see cref="IosVoiceOverCaptionCapture"/> for that conversion.</summary>
public sealed record VoiceOverRect(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("w")] double W,
    [property: JsonPropertyName("h")] double H);

/// <summary>One decoded frame of harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift's
/// <c>--analyze-image</c> single-shot mode, used by tests against synthetic fixtures
/// (tests/Swipewalk.Core.Tests/Fixtures/voiceover-captions/) -- never touches a device. Mirrors the script's own
/// <c>AnalyzeResult</c>.</summary>
public sealed record VoiceOverFrameAnalysis(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("capturedText")] string? CapturedText,
    [property: JsonPropertyName("captionRegion")] VoiceOverRect? CaptionRegion,
    [property: JsonPropertyName("focusRect")] VoiceOverRect? FocusRect,
    [property: JsonPropertyName("imageWidth")] int ImageWidth,
    [property: JsonPropertyName("imageHeight")] int ImageHeight)
{
    public static VoiceOverFrameAnalysis Failed(string error) => new(false, error, null, null, null, 0, 0);
}

/// <summary>One de-duplicated VoiceOver caption, in the order it was captured. <see cref="FocusRect"/> is a
/// best-effort, unverified-on-a-real-device detection of the VoiceOver cursor's own rounded-rect border in the
/// same frame the caption came from (see <see cref="IosVoiceOverCaptionCapture"/> for how, and how confidently,
/// this is matched to a scanned tree node); null when nothing that looked like a cursor was found in that frame.</summary>
public sealed record VoiceOverCaptionItem(
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("spokenText")] string SpokenText,
    [property: JsonPropertyName("elapsedSeconds")] double ElapsedSeconds,
    [property: JsonPropertyName("focusRect")] VoiceOverRect? FocusRect);

/// <summary>The whole live session's raw result, as harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift
/// prints it. <see cref="Complete"/> is always false for this source (see the script's own remarks): unlike a
/// scripted walk, nothing here can tell whether the person swiped through every element on the screen or
/// stopped partway.</summary>
public sealed record VoiceOverCaptionCaptureRawResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("items")] IReadOnlyList<VoiceOverCaptionItem> Items,
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("notCompleteReason")] string? NotCompleteReason,
    [property: JsonPropertyName("toolVersion")] string? ToolVersion,
    [property: JsonPropertyName("imageWidth")] int? ImageWidth,
    [property: JsonPropertyName("imageHeight")] int? ImageHeight)
{
    /// <summary>A result for when the script itself could not be run or its output could not be read at all
    /// (as opposed to the script running and reporting its own <see cref="Ok"/> false).</summary>
    public static VoiceOverCaptionCaptureRawResult Failed(string error) => new(false, error, [], false, error, null, null, null);
}

/// <summary>
/// Runs harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift, which captures VoiceOver's own Caption
/// Panel while a person drives VoiceOver by hand on a connected iPhone -- see that script's header and
/// <see cref="Swipewalk.Core.Model.ScreenReaderCapture"/>'s <c>VoiceOverCaptions</c> source for the design.
/// VoiceOver is never scripted here: this only polls <c>xcrun devicectl device capture screenshot</c> and reads
/// the on-screen caption text with Vision's on-device text recognition, entirely on this Mac.
/// </summary>
public static class IosVoiceOverCaptionWalk
{
    public const string ScriptRelativePath = "harness/mac-voiceover-captions/VoiceOverCaptionCapture.swift";

    private static readonly TimeSpan AnalyzeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Polls the connected device for up to <paramref name="maxDuration"/>, or until
    /// <paramref name="stopRequested"/> fires (the person says they're done -- a line is written to the
    /// script's stdin and it finishes on its own next poll), de-duplicating consecutive identical captions.
    /// Never throws: a script that can't be found, can't start, or doesn't finish in time comes back as a
    /// normal <see cref="VoiceOverCaptionCaptureRawResult"/> with <see cref="VoiceOverCaptionCaptureRawResult.Ok"/>
    /// false and why.
    /// </summary>
    /// <param name="pollIntervalMs">How often a new screenshot is taken; the TalkBack capture's real-device
    /// finding (captions lag about 0.6s behind the actual announcement) is the closest available guide for iOS
    /// too, so this defaults higher than that to leave OCR room -- unverified against a real Caption Panel. A
    /// real-device timing measurement is needed to tighten this; see KnownLimitations
    /// "ios-voiceover-captions-capture".</param>
    public static async Task<VoiceOverCaptionCaptureRawResult> RunLiveAsync(
        string udid, TimeSpan maxDuration, CancellationToken stopRequested, string? scriptPath = null,
        int pollIntervalMs = 1200, IReadOnlyList<string>? languages = null, CancellationToken cancellationToken = default)
    {
        var script = scriptPath ?? FindScript();
        if (script is null)
            return VoiceOverCaptionCaptureRawResult.Failed($"Could not find {ScriptRelativePath}.");

        var info = new ProcessStartInfo("xcrun")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
        };
        foreach (var arg in new[]
                 {
                     "swift", script, "--device", udid, "--duration-seconds", ((int)maxDuration.TotalSeconds).ToString(),
                     "--poll-interval-ms", pollIntervalMs.ToString(),
                 })
            info.ArgumentList.Add(arg);
        if (languages is { Count: > 0 })
        {
            info.ArgumentList.Add("--languages");
            info.ArgumentList.Add(string.Join(',', languages));
        }

        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return VoiceOverCaptionCaptureRawResult.Failed($"Could not start the VoiceOver-captions capture: {ex.Message}");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var stopRegistration = stopRequested.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("stop");
                    process.StandardInput.Close();
                }
            }
            catch (InvalidOperationException)
            {
                // The process had already exited between HasExited and the write -- nothing to signal.
            }
        });

        // The script itself is given maxDuration to run its poll loop; this bound adds room for it to notice
        // the stop signal, finish its current screenshot/OCR pass and print its result, without waiting forever
        // if it hangs some other way.
        var bound = maxDuration + TimeSpan.FromSeconds(30);
        using var timeoutCts = new CancellationTokenSource(bound);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return VoiceOverCaptionCaptureRawResult.Failed(
                cancellationToken.IsCancellationRequested
                    ? "The VoiceOver-captions capture was cancelled."
                    : "The VoiceOver-captions capture did not finish in time.");
        }

        var output = stdout.ToString();
        var jsonLine = output.Split('\n').LastOrDefault(l => l.TrimStart().StartsWith('{'));
        if (process.ExitCode != 0 || jsonLine is null)
            return VoiceOverCaptionCaptureRawResult.Failed(
                $"The VoiceOver-captions capture script failed (exit {process.ExitCode}): {FirstLine(output.Length > 0 ? output : stderr.ToString())}");
        try
        {
            return JsonSerializer.Deserialize<VoiceOverCaptionCaptureRawResult>(jsonLine)
                ?? VoiceOverCaptionCaptureRawResult.Failed("The VoiceOver-captions capture script produced no result.");
        }
        catch (JsonException ex)
        {
            return VoiceOverCaptionCaptureRawResult.Failed($"The VoiceOver-captions capture script's output could not be read: {ex.Message}");
        }
    }

    /// <summary>Single-shot analysis of one screenshot file (the script's <c>--analyze-image</c> mode), used
    /// by tests against synthetic fixtures (tests/Swipewalk.Core.Tests/Fixtures/voiceover-captions/) -- never touches a device.</summary>
    public static async Task<VoiceOverFrameAnalysis> AnalyzeImageAsync(
        string imagePath, string? scriptPath = null, IReadOnlyList<string>? languages = null)
    {
        var script = scriptPath ?? FindScript();
        if (script is null)
            return VoiceOverFrameAnalysis.Failed($"Could not find {ScriptRelativePath}.");

        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "swift", script, "--analyze-image", imagePath })
            info.ArgumentList.Add(arg);
        if (languages is { Count: > 0 })
        {
            info.ArgumentList.Add("--languages");
            info.ArgumentList.Add(string.Join(',', languages));
        }

        string output;
        int exitCode;
        try
        {
            (exitCode, output) = await IosCollector.RunAsync(info, AnalyzeTimeout);
        }
        catch (InvalidOperationException ex)
        {
            return VoiceOverFrameAnalysis.Failed($"The VoiceOver-captions analyzer did not finish: {ex.Message}");
        }

        var jsonLine = output.Split('\n').LastOrDefault(l => l.TrimStart().StartsWith('{'));
        if (exitCode != 0 || jsonLine is null)
            return VoiceOverFrameAnalysis.Failed($"The VoiceOver-captions analyzer failed (exit {exitCode}): {FirstLine(output)}");
        try
        {
            return JsonSerializer.Deserialize<VoiceOverFrameAnalysis>(jsonLine)
                ?? VoiceOverFrameAnalysis.Failed("The VoiceOver-captions analyzer produced no result.");
        }
        catch (JsonException ex)
        {
            return VoiceOverFrameAnalysis.Failed($"The VoiceOver-captions analyzer's output could not be read: {ex.Message}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill.
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
        // bundle's Resources folder -- same layout convention as harness/ios and harness/mac-inspector-walk.
        return new[] { baseDirectory, Path.Combine(baseDirectory, "..", "Resources") }
            .Select(root => Path.GetFullPath(Path.Combine(root, ScriptRelativePath)))
            .FirstOrDefault(File.Exists);
    }
}
