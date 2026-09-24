using System.Diagnostics;

namespace Swipewalk.Collectors.Android;

/// <summary>Runs adb commands against one device (or the only connected one).</summary>
internal sealed class Adb(string? serial)
{
    /// <summary>Serials of devices in the "device" state from <c>adb devices</c> output.</summary>
    public static IReadOnlyList<string> ParseSerials(string output) =>
        [.. ParseStates(output).Where(d => d.State == "device").Select(d => d.Serial)];

    /// <summary>Every listed device with its state ("device", "unauthorized", "offline", ...).</summary>
    public static IReadOnlyList<(string Serial, string State)> ParseStates(string output) =>
        [.. output.Split('\n').Skip(1)
            .Select(l => l.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .Select(parts => (parts[0], parts[1]))];

    /// <summary>
    /// The device to use: the given serial, or the only connected device. With several connected and none
    /// chosen, adb would fail with a vague error, so fail early with the list.
    /// </summary>
    public async Task<string> ResolveSerialAsync()
    {
        if (serial is not null)
            return serial;
        var serials = ParseSerials(await RunAsync("devices"));
        return serials.Count switch
        {
            1 => serials[0],
            0 => throw new InvalidOperationException("No Android device or emulator is connected (adb devices is empty)."),
            _ => throw new InvalidOperationException(
                $"More than one Android device is connected ({string.Join(", ", serials)}). Pass --device <serial>."),
        };
    }

    public async Task<string> RunAsync(params string[] args)
        => System.Text.Encoding.UTF8.GetString(await RunBinaryAsync(args));

    public async Task<byte[]> RunBinaryAsync(params string[] args)
    {
        var info = new ProcessStartInfo(AdbPath())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (serial is not null)
        {
            info.ArgumentList.Add("-s");
            info.ArgumentList.Add(serial);
        }
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start adb.");
        using var output = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(copy, error, process.WaitForExitAsync());

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"adb {string.Join(' ', args)} failed: {error.Result.Trim()}");
        return output.ToArray();
    }

    /// <summary>Android SDK folders to look in: ANDROID_HOME, ANDROID_SDK_ROOT, then the default macOS location.</summary>
    internal static IEnumerable<string> SdkRoots() =>
        new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Android/sdk"),
        }.OfType<string>().Where(Directory.Exists);

    private static string AdbPath()
    {
        foreach (var sdk in new[] { Environment.GetEnvironmentVariable("ANDROID_HOME"), Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Android/sdk") })
        {
            var candidate = sdk is null ? null : Path.Combine(sdk, "platform-tools", OperatingSystem.IsWindows() ? "adb.exe" : "adb");
            if (candidate is not null && File.Exists(candidate))
                return candidate;
        }
        return "adb";
    }
}
