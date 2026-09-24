using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

public class AppearanceRestoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cf-appearance-restore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void NothingPending_UntilRemembered()
    {
        Assert.Null(AppearanceRestore.Pending("emulator-5554", _dir));

        AppearanceRestore.Remember("emulator-5554", "light", _dir);

        Assert.Equal("light", AppearanceRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void SecondRemember_KeepsTheRealOriginal()
    {
        // A scan killed mid-check leaves the device switched; the next check must not record the switched
        // appearance as the original.
        AppearanceRestore.Remember("emulator-5554", "light", _dir);
        AppearanceRestore.Remember("emulator-5554", "dark", _dir);

        Assert.Equal("light", AppearanceRestore.Pending("emulator-5554", _dir));
    }

    [Fact]
    public void Forget_ClearsOnlyThatDevice()
    {
        AppearanceRestore.Remember("emulator-5554", "light", _dir);
        AppearanceRestore.Remember("SIM-1", "dark", _dir);

        AppearanceRestore.Forget("emulator-5554", _dir);

        Assert.Null(AppearanceRestore.Pending("emulator-5554", _dir));
        Assert.Equal("dark", AppearanceRestore.Pending("SIM-1", _dir));
    }

    [Fact]
    public void DoesNotCollideWithATextSizeRestoreMarkerForTheSameDevice()
    {
        TextSizeRestore.Remember("emulator-5554", "1.0", _dir);
        AppearanceRestore.Remember("emulator-5554", "light", _dir);

        Assert.Equal("1.0", TextSizeRestore.Pending("emulator-5554", _dir));
        Assert.Equal("light", AppearanceRestore.Pending("emulator-5554", _dir));
    }
}
