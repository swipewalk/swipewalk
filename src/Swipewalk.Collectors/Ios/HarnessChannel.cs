using System.Diagnostics;

namespace Swipewalk.Collectors.Ios;

/// <summary>
/// How the host exchanges files with the harness in serve mode. Simulators share the Mac's file system;
/// physical devices are reached through devicectl, limited to the harness runner's own app container.
/// </summary>
internal interface IHarnessChannel
{
    /// <summary>The session directory as the harness sees it (absolute on a simulator, container-relative on a device).</summary>
    string Root { get; }

    Task WriteAsync(string relativePath, string content);

    Task<bool> ExistsAsync(string relativePath);

    Task<string> ReadAsync(string relativePath);

    /// <summary>Copies files from a directory of the session to a local directory; missing files are skipped.</summary>
    Task FetchAsync(string relativeDir, string localDir, params string[] files);

    Task CleanUpAsync();
}

internal sealed class LocalChannel : IHarnessChannel
{
    public LocalChannel() => Directory.CreateDirectory(Root);

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "swipewalk-session-" + Guid.NewGuid().ToString("N")[..8]);

    public async Task WriteAsync(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path + ".tmp", content);
        File.Move(path + ".tmp", path, overwrite: true); // atomic, so the harness never reads a partial command
    }

    public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(File.Exists(Path.Combine(Root, relativePath)));

    public Task<string> ReadAsync(string relativePath) => File.ReadAllTextAsync(Path.Combine(Root, relativePath));

    public Task FetchAsync(string relativeDir, string localDir, params string[] files)
    {
        Directory.CreateDirectory(localDir);
        foreach (var file in files)
        {
            var source = Path.Combine(Root, relativeDir, file);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(localDir, file), overwrite: true);
        }
        return Task.CompletedTask;
    }

    public Task CleanUpAsync()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
        return Task.CompletedTask;
    }
}

/// <summary>Files in the harness runner's app data container on a physical device, via devicectl (about a second per call).</summary>
internal sealed class DeviceChannel(string udid, string runnerBundleId) : IHarnessChannel
{
    /// <summary>Well past the "about a second" a devicectl round trip normally takes, so a call that's
    /// actually stuck is killed and treated as a failed round trip instead of hanging <see cref="IosCollector.RunAsync"/>
    /// -- and this class's own callers -- forever with nothing bounding it. Added 2026-09-23 after a record
    /// run froze on a physical iPhone during the large-text step (Settings visibly moving, the text never
    /// changing size); a devicectl call stalling here -- e.g. because the device is busy with that step's own
    /// Settings/app-switching automation -- is a suspected, unreproduced explanation, and this bound closes off
    /// the one unbounded wait on that path regardless of the exact cause.</summary>
    private static readonly TimeSpan DevicectlTimeout = TimeSpan.FromSeconds(20);

    private readonly string _staging = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "swipewalk-device-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    public string Root { get; } = "tmp/swipewalk-session-" + Guid.NewGuid().ToString("N")[..8];

    public async Task WriteAsync(string relativePath, string content)
    {
        var local = Path.Combine(_staging, Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(local, content);
        await Devicectl("copy", "to", "--source", local, "--destination", $"{Root}/{relativePath}");
        File.Delete(local);
    }

    public async Task<bool> ExistsAsync(string relativePath)
    {
        var local = Path.Combine(_staging, Guid.NewGuid().ToString("N"));
        var ok = await TryDevicectl("copy", "from", "--source", $"{Root}/{relativePath}", "--destination", local);
        if (File.Exists(local))
            File.Delete(local);
        return ok;
    }

    public async Task<string> ReadAsync(string relativePath)
    {
        var local = Path.Combine(_staging, Guid.NewGuid().ToString("N"));
        await Devicectl("copy", "from", "--source", $"{Root}/{relativePath}", "--destination", local);
        var text = await File.ReadAllTextAsync(local);
        File.Delete(local);
        return text;
    }

    public async Task FetchAsync(string relativeDir, string localDir, params string[] files)
    {
        Directory.CreateDirectory(localDir);
        foreach (var file in files)
            await TryDevicectl("copy", "from", "--source", $"{Root}/{relativeDir}/{file}", "--destination", Path.Combine(localDir, file));
    }

    public Task CleanUpAsync()
    {
        try { Directory.Delete(_staging, recursive: true); } catch (IOException) { }
        return Task.CompletedTask; // files in the runner's tmp directory are cleared by iOS
    }

    private async Task Devicectl(params string[] args)
    {
        if (!await TryDevicectl(args))
            throw new InvalidOperationException($"devicectl {args[0]} {args[1]} failed; is the device still connected and unlocked?");
    }

    private async Task<bool> TryDevicectl(params string[] args)
    {
        var info = new ProcessStartInfo("xcrun") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "devicectl", "device" }.Concat(args).Concat(
                     ["--device", udid, "--domain-type", "appDataContainer", "--domain-identifier", runnerBundleId]))
            info.ArgumentList.Add(arg);
        try
        {
            var (exitCode, _) = await IosCollector.RunAsync(info, DevicectlTimeout);
            return exitCode == 0;
        }
        catch (InvalidOperationException)
        {
            // devicectl itself stalled past DevicectlTimeout: treat it the same as any other failed round
            // trip. Callers already retry (ExistsAsync is polled in a loop bounded by
            // IosHarnessSession.SendCommandAsync's own 90-second deadline) or surface a clear error
            // (Devicectl below), so one stuck call no longer hangs the caller with nothing bounding it.
            return false;
        }
    }
}
