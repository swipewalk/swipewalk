using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

public class OrientationRestoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cf-orientation-restore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void NothingPending_UntilRemembered()
    {
        Assert.Null(OrientationRestore.Pending("emulator-5554", _dir));

        OrientationRestore.Remember("emulator-5554", "0,0", _dir);

        Assert.Equal("0,0", OrientationRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void SecondRemember_KeepsTheRealOriginal()
    {
        // A scan killed mid-check leaves the device rotated; the next check must not record the rotated
        // state as the original.
        OrientationRestore.Remember("emulator-5554", "0,0", _dir);
        OrientationRestore.Remember("emulator-5554", "0,1", _dir);

        Assert.Equal("0,0", OrientationRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void Forget_ClearsOnlyThatDevice()
    {
        OrientationRestore.Remember("emulator-5554", "0,0", _dir);
        OrientationRestore.Remember("SIM-1", "portrait", _dir);

        OrientationRestore.Forget("emulator-5554", _dir);

        Assert.Null(OrientationRestore.Pending("emulator-5554", _dir));
        Assert.Equal("portrait", OrientationRestore.Pending("SIM-1", _dir));
    }

    [Fact]
    public void DoesNotCollideWithAnAppearanceRestoreMarkerForTheSameDevice()
    {
        AppearanceRestore.Remember("emulator-5554", "light", _dir);
        OrientationRestore.Remember("emulator-5554", "0,0", _dir);

        Assert.Equal("light", AppearanceRestore.Pending("emulator-5554", _dir));
        Assert.Equal("0,0", OrientationRestore.Pending("emulator-5554", _dir));
    }
}
