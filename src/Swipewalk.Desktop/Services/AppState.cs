using System.Collections.ObjectModel;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Services;

/// <summary>Shared state for the pages: the run history and the log of the current run.</summary>
public static class AppState
{
    /// <summary>Run history; --history &lt;folder&gt; points it elsewhere (UI tests use this to stay isolated).</summary>
    public static RunHistory History { get; } = new(ArgumentValue("--history"));

    /// <summary>The value after <paramref name="name"/> on the command line, if present.</summary>
    public static string? ArgumentValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Whether <paramref name="name"/> is present on the command line at all, for a bare boolean flag
    /// (e.g. --reset-physical-device-notice) that takes no value -- unlike <see cref="ArgumentValue"/>, this
    /// doesn't require (and ignores) anything after it.</summary>
    public static bool HasFlag(string name) => Environment.GetCommandLineArgs().Contains(name);

    /// <summary>Raised after a run is saved, so History and Dashboard refresh.</summary>
    public static event Action? RunsChanged;

    /// <summary>A palette color for the current theme: "Issue", "Review" or "Advisory" (dark variants meet contrast on dark backgrounds).</summary>
    public static Color ThemeColor(string name) =>
        (Color)Application.Current!.Resources[Application.Current.RequestedTheme == AppTheme.Dark ? name + "Dark" : name];

    public static void NotifyRunsChanged() => MainThread.BeginInvokeOnMainThread(() => RunsChanged?.Invoke());
}

/// <summary>
/// Progress messages from the Engine, appended on the UI thread. Important ones (install results, failures,
/// each recorded screen, the final result) are also announced to screen readers (WCAG 4.1.3 Status Messages).
/// </summary>
public sealed class UiLog(ObservableCollection<string> lines) : IProgress<string>
{
    private static readonly string[] Announced = ["Installed", "Failed", "Not ready", "FAIL", "Screen ", "Automated checks found", "Recording ", "Stopped"];

    public void Report(string value) => MainThread.BeginInvokeOnMainThread(() =>
    {
        lines.Add(value);
        if (Announced.Any(prefix => value.TrimStart().StartsWith(prefix, StringComparison.Ordinal)))
            SemanticScreenReader.Announce(value.Trim());
    });
}
