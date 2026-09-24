using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

public class TextSizeRestoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cf-restore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void NothingPending_UntilRemembered()
    {
        Assert.Null(TextSizeRestore.Pending("emulator-5554", _dir));

        TextSizeRestore.Remember("emulator-5554", "1.0", _dir);

        Assert.Equal("1.0", TextSizeRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void SecondRemember_KeepsTheRealOriginal()
    {
        // A scan killed mid-check leaves the device at 2.0; the next check must not record 2.0 as the original.
        TextSizeRestore.Remember("emulator-5554", "1.0", _dir);
        TextSizeRestore.Remember("emulator-5554", "2.0", _dir);

        Assert.Equal("1.0", TextSizeRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void Forget_ClearsOnlyThatDevice()
    {
        TextSizeRestore.Remember("emulator-5554", "1.0", _dir);
        TextSizeRestore.Remember("SIM-1", "large", _dir);

        TextSizeRestore.Forget("emulator-5554", _dir);

        Assert.Null(TextSizeRestore.Pending("emulator-5554", _dir));
        Assert.Equal("large", TextSizeRestore.Pending("SIM-1", _dir));
    }

    [Fact]
    public void NetworkSerials_AreSafeFileNames()
    {
        TextSizeRestore.Remember("192.168.1.5:5555", "1.15", _dir);

        Assert.Equal("1.15", TextSizeRestore.Pending("192.168.1.5:5555", _dir));
        Assert.DoesNotContain(':', Path.GetFileName(Assert.Single(Directory.GetFiles(_dir))));
    }
}
