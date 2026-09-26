namespace Swipewalk.Collectors.Android;

/// <summary>
/// Reads and sets Android's screen-rotation settings via <c>adb shell settings get/put system
/// accelerometer_rotation|user_rotation</c>, for the orientation rescan (<c>scan --orientation both</c>; see
/// <see cref="Collectors.OrientationRestore"/>). Setting <c>accelerometer_rotation</c> to 0 turns off
/// auto-rotate so <c>user_rotation</c> sticks regardless of how the device is physically held or sitting on a
/// desk -- exactly what a scan needs on an emulator or a real device.
/// </summary>
public static class AndroidOrientation
{
    /// <summary><c>user_rotation</c> values, as Android's <c>Surface</c> constants: 0 = ROTATION_0, 1 =
    /// ROTATION_90, 2 = ROTATION_180, 3 = ROTATION_270.</summary>
    public const int Rotation0 = 0;
    public const int Rotation90 = 1;
    public const int Rotation180 = 2;
    public const int Rotation270 = 3;

    public static async Task<(int AccelerometerRotation, int UserRotation)> ReadAsync(string? serial)
    {
        var adb = new Adb(serial);
        var accelerometer = ParseInt(await adb.RunAsync("shell", "settings", "get", "system", "accelerometer_rotation"));
        var user = ParseInt(await adb.RunAsync("shell", "settings", "get", "system", "user_rotation"));
        return (accelerometer, user);
    }

    public static async Task SetAsync(string? serial, int accelerometerRotation, int userRotation)
    {
        var adb = new Adb(serial);
        await adb.RunAsync("shell", "settings", "put", "system", "accelerometer_rotation", accelerometerRotation.ToString());
        await adb.RunAsync("shell", "settings", "put", "system", "user_rotation", userRotation.ToString());
    }

    private static int ParseInt(string output) => int.TryParse(output.Trim(), out var value) ? value : Rotation0;
}
