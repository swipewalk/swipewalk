using System.Diagnostics;
using System.Text.Json;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// One live XCUITest session against the harness's serve mode (<c>ScanTests.swift</c>'s <c>testServe</c>):
/// while it exists, iOS shows its own "Automation Running" banner for the session's whole life (see
/// <c>testServe</c>'s comment; confirmed on a physical iPhone). <see cref="IosScreenSource"/> opens one of
/// these per action (a capture, an ensure-front check, a large-text step) by default, disposing it immediately
/// after so the banner is visible only while that action runs -- and keeps a single instance for the whole
/// recording only when built for auto-scan (see <see cref="IosScreenSource.StartAsync"/>'s <c>liveSession</c>
/// parameter), where the banner staying up is an accepted cost of polling every ~1.5-2s.
/// </summary>
internal sealed class IosHarnessSession : IAsyncDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(5);

    private readonly IHarnessChannel _channel;
    private readonly Process _harness;
    private readonly bool _physical;
    private readonly string _bundleId;
    private int _command;

    private IosHarnessSession(IHarnessChannel channel, Process harness, bool physical, string bundleId)
    {
        _channel = channel;
        _harness = harness;
        _physical = physical;
        _bundleId = bundleId;
    }

    public string Root => _channel.Root;

    /// <summary>Builds (first run only -- the derived-data path is reused, so later builds are incremental)
    /// and starts the harness in serve mode, and waits until it is ready to take commands. Kills the process
    /// and cleans up its channel before rethrowing if starting is cancelled or fails partway through, so a
    /// Ctrl-C (or any other cancellation) while a session is starting never leaves an orphaned xcodebuild/XCUITest
    /// runner process behind.</summary>
    public static async Task<IosHarnessSession> StartAsync(
        string bundleId, string udid, bool physical, string? harnessProject, SigningPlan? signing, CancellationToken cancellationToken)
    {
        IHarnessChannel channel = physical
            ? new DeviceChannel(udid, SigningPlan.HarnessBundleIds(signing!.BundlePrefix)[1])
            : new LocalChannel();
        Process? harness = null;
        try
        {
            var start = IosCollector.HarnessProcess(harnessProject, udid, bundleId, channel.Root, "ScanTests/testServe", serve: true, signing: signing);
            harness = Process.Start(start) ?? throw new InvalidOperationException("Could not start xcodebuild.");
            var output = harness.StandardOutput.ReadToEndAsync();
            var errors = harness.StandardError.ReadToEndAsync();

            var deadline = DateTime.UtcNow + ReadyTimeout;
            while (!await channel.ExistsAsync("ready"))
            {
                if (harness.HasExited)
                {
                    var log = await output + await errors;
                    throw new InvalidOperationException(log.Contains("Failed to initialize for UI testing", StringComparison.Ordinal)
                        ? "The device did not allow UI automation. Keep it unlocked and approve the Face ID / passcode prompt when recording starts."
                        : $"iOS harness exited before it was ready (is {bundleId} installed?). {(await errors).Trim()}");
                }
                if (DateTime.UtcNow > deadline)
                {
                    harness.Kill(entireProcessTree: true);
                    throw new InvalidOperationException("iOS harness did not start within 5 minutes.");
                }
                await Task.Delay(physical ? 1000 : 250, cancellationToken);
            }
            return new IosHarnessSession(channel, harness, physical, bundleId);
        }
        catch
        {
            if (harness is { HasExited: false }) harness.Kill(entireProcessTree: true);
            harness?.Dispose();
            await channel.CleanUpAsync();
            throw;
        }
    }

    /// <summary>Sends a command (peek/capture/textsize-*/relaunch/ensure-front) into a fresh result directory
    /// and waits for the harness; returns that directory. <paramref name="target"/> and <paramref name="launchArg"/>
    /// are the extra fields textsize-restore and relaunch take.</summary>
    public async Task<string> SendCommandAsync(string action, CancellationToken cancellationToken, string? target = null, string? launchArg = null)
    {
        var dir = $"{action}-{_command + 1}";
        await WriteCommandAsync(action, $"{Root}/{dir}", target, launchArg);
        var deadline = DateTime.UtcNow + CommandTimeout;
        while (!await _channel.ExistsAsync($"{dir}/done"))
        {
            if (await _channel.ExistsAsync($"{dir}/error"))
            {
                var error = await _channel.ReadAsync($"{dir}/error");
                throw error.StartsWith("not-in-front", StringComparison.Ordinal)
                    ? new TargetNotInFrontException($"{_bundleId} is not in front; nothing was captured.")
                    : new InvalidOperationException($"iOS harness {action} failed: {error}");
            }
            if (_harness.HasExited)
                throw new InvalidOperationException($"iOS harness stopped unexpectedly (is {_bundleId} still running?).");
            if (DateTime.UtcNow > deadline)
                throw new InvalidOperationException($"iOS harness {action} timed out.");
            await Task.Delay(_physical ? 300 : 100, cancellationToken);
        }
        return dir;
    }

    public Task<string> ReadAsync(string relativePath) => _channel.ReadAsync(relativePath);

    public Task FetchAsync(string relativeDir, string localDir, params string[] files) => _channel.FetchAsync(relativeDir, localDir, files);

    private async Task WriteCommandAsync(string action, string dir, string? target = null, string? launchArg = null)
    {
        var number = Interlocked.Increment(ref _command);
        var command = new Dictionary<string, string> { ["action"] = action, ["dir"] = dir };
        if (target is not null)
            command["target"] = target;
        if (launchArg is not null)
            command["launchArg"] = launchArg;
        await _channel.WriteAsync($"commands/{number}.json", JsonSerializer.Serialize(command));
    }

    /// <summary>
    /// Ends the session: sends "stop" so the harness's serve loop returns and the XCTest session (and the
    /// "Automation Running" state iOS shows for its whole lifetime on a physical device) is torn down, killing
    /// the process if it doesn't exit in time. Runs whenever the caller's <c>await using</c> block ends --
    /// normally right after one action for a per-action session, or when the whole recording finishes/is
    /// cancelled/errors for a long-lived auto-scan session. If the host process itself dies first (crash,
    /// force-quit, `kill -9`) and this never runs, the harness's own staleness watchdog (see ScanTests.swift's
    /// testServe) ends the session on its own once it stops receiving commands.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await WriteCommandAsync("stop", Root);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _harness.WaitForExitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
        {
            _harness.Kill(entireProcessTree: true);
        }
        _harness.Dispose();
        await _channel.CleanUpAsync();
    }
}
