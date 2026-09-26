using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using Swipewalk.Collectors;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Detecting .NET MAUI from an installed app bundle: Microsoft.Maui.Controls.dll sits flat in the bundle root
/// on both the Simulator and a device .ipa/.app (confirmed 2026-09-22 against the TipCalc/DeveloperBalance/
/// Calculator MAUI samples and BuggyApp on a booted Simulator, all .NET MAUI 10.0.60), so a real .NET assembly
/// is used as the fixture for "is this file detected as .NET MAUI at all" tests: this test project's own
/// compiled .dll. The one test that specifically checks the "+commit" suffix is stripped
/// (<see cref="ReadInformationalVersion_StripsCommitSuffix"/>) instead builds its own fixture with a known
/// informational version (<see cref="CreateAssemblyWithInformationalVersion"/>), since whether this test
/// assembly's own build stamped one on isn't something a test run can rely on (e.g. it doesn't in a git
/// worktree checkout) -- "10.0.60+bf6156897c887d33dcd40592db3bcb6471916e03" is the shape .NET MAUI's own build
/// ships (read from the real TipCalc sample).
/// </summary>
public class IosFrameworkDetectionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"swipewalk-frameworktest-{Guid.NewGuid():N}");

    /// <summary>A real, readable .NET assembly for tests that only need "this file is a genuine .NET
    /// assembly named Microsoft.Maui.Controls.dll" -- not for anything about its informational version.</summary>
    private static readonly string RealAssemblyPath = typeof(IosFrameworkDetectionTests).Assembly.Location;

    public IosFrameworkDetectionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void DetectFrameworkInDirectory_WithMauiControlsDll_ReportsMauiAndStripsCommitSuffix()
    {
        File.Copy(RealAssemblyPath, Path.Combine(_dir, "Microsoft.Maui.Controls.dll"));

        var detected = IosCollector.DetectFrameworkInDirectory(_dir);

        Assert.Equal(AppFramework.Maui, detected.Framework);
        Assert.NotNull(detected.Version);
        Assert.DoesNotContain('+', detected.Version);
    }

    [Fact]
    public void DetectFrameworkInDirectory_WithoutMauiControlsDll_ReportsUnknown()
    {
        // A native app bundle: nothing that looks like MAUI.
        File.WriteAllText(Path.Combine(_dir, "SomeOtherFile.txt"), "not an assembly");

        var detected = IosCollector.DetectFrameworkInDirectory(_dir);

        Assert.Equal(AppFramework.Unknown, detected.Framework);
        Assert.Null(detected.Version);
    }

    [Fact]
    public void DetectFrameworkInDirectory_EmptyDirectory_ReportsUnknown()
    {
        var detected = IosCollector.DetectFrameworkInDirectory(_dir);

        Assert.Equal(AppFramework.Unknown, detected.Framework);
        Assert.Null(detected.Version);
    }

    /// <summary>
    /// This used to copy this test project's own compiled dll and rely on the SDK having stamped it with a
    /// genuine "+commit" suffix (a deterministic build reading real git info) -- which a git worktree checkout
    /// doesn't produce (its ".git" is a gitdir pointer, not a full checkout the SDK's source-revision-id step
    /// recognizes), so the test failed here while still passing in CI. Building the fixture assembly directly,
    /// with a known "+commit" informational version we control, tests the exact same code path
    /// (<see cref="IosCollector.ReadInformationalVersion"/> reading a real AssemblyInformationalVersionAttribute
    /// via PE metadata and stripping everything from "+") without depending on how -- or whether -- the
    /// ambient build environment adds one.
    /// </summary>
    [Fact]
    public void ReadInformationalVersion_StripsCommitSuffix()
    {
        var dll = Path.Combine(_dir, "Microsoft.Maui.Controls.dll");
        const string raw = "10.0.60+bf6156897c887d33dcd40592db3bcb6471916e03";
        CreateAssemblyWithInformationalVersion(dll, raw);

        var version = IosCollector.ReadInformationalVersion(dll);

        Assert.NotNull(version);
        Assert.DoesNotContain('+', version);
        // The raw AssemblyInformationalVersion this fixture was built with really does have a "+commit"
        // suffix, so this proves a genuine strip, not a version string that happened not to have one.
        Assert.Contains('+', raw);
        Assert.StartsWith(version, raw, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadInformationalVersion_NotADotNetAssembly_ReturnsNullWithoutThrowing()
    {
        var dll = Path.Combine(_dir, "Microsoft.Maui.Controls.dll");
        File.WriteAllBytes(dll, [0x00, 0x01, 0x02, 0x03]);

        var version = IosCollector.ReadInformationalVersion(dll);

        Assert.Null(version);
    }

    [Fact]
    public void DetectFrameworkInDirectory_UnreadableMauiControlsDll_StillReportsMaui_ButVersionUnknown()
    {
        // The marker is presence of the file, not that it parses: a corrupt or oddly-built dll still means
        // the app bundles Microsoft.Maui.Controls.dll, so the framework itself is still known.
        File.WriteAllBytes(Path.Combine(_dir, "Microsoft.Maui.Controls.dll"), [0x4D, 0x5A, 0x00, 0x00]);

        var detected = IosCollector.DetectFrameworkInDirectory(_dir);

        Assert.Equal(AppFramework.Maui, detected.Framework);
        Assert.Null(detected.Version);
    }

    [Fact]
    public void AppInstaller_DetectFrameworkFromInstallFile_RawAppDirectory_DelegatesToBundleDetection()
    {
        File.Copy(RealAssemblyPath, Path.Combine(_dir, "Microsoft.Maui.Controls.dll"));

        var (framework, version) = AppInstaller.DetectFrameworkFromInstallFile(_dir, isIpa: false);

        Assert.Equal(AppFramework.Maui, framework);
        Assert.NotNull(version);
    }

    [Fact]
    public void AppInstaller_DetectFrameworkFromInstallFile_Ipa_ReadsThePayloadAppWithoutFullExtraction()
    {
        var ipaPath = Path.Combine(_dir, "app.ipa");
        using (var zip = ZipFile.Open(ipaPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(RealAssemblyPath, "Payload/Test.app/Microsoft.Maui.Controls.dll");
            zip.CreateEntry("Payload/Test.app/Info.plist");
        }

        var (framework, version) = AppInstaller.DetectFrameworkFromInstallFile(ipaPath, isIpa: true);

        Assert.Equal(AppFramework.Maui, framework);
        Assert.NotNull(version);
    }

    [Fact]
    public void AppInstaller_DetectFrameworkFromInstallFile_IpaWithoutMauiControlsDll_ReportsUnknown()
    {
        var ipaPath = Path.Combine(_dir, "app.ipa");
        using (var zip = ZipFile.Open(ipaPath, ZipArchiveMode.Create))
            zip.CreateEntry("Payload/Test.app/Info.plist");

        var (framework, version) = AppInstaller.DetectFrameworkFromInstallFile(ipaPath, isIpa: true);

        Assert.Equal(AppFramework.Unknown, framework);
        Assert.Null(version);
    }

    /// <summary>Builds a minimal, real .NET assembly on disk carrying exactly
    /// <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/> = <paramref name="informationalVersion"/>
    /// and nothing else -- a fixture <see cref="IosCollector.ReadInformationalVersion"/> can read as a genuine PE
    /// file, with a version string this test controls rather than whatever the ambient build happened to stamp
    /// on this test assembly (see <see cref="ReadInformationalVersion_StripsCommitSuffix"/>'s remarks).</summary>
    private static void CreateAssemblyWithInformationalVersion(string path, string informationalVersion)
    {
        var builder = new PersistedAssemblyBuilder(new AssemblyName("SwipewalkTestFixture"), typeof(object).Assembly);
        var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informationalVersion]));
        var module = builder.DefineDynamicModule("SwipewalkTestFixture.dll");
        module.DefineType("Fixture", TypeAttributes.Public | TypeAttributes.Class).CreateType();
        using var stream = new FileStream(path, FileMode.Create);
        builder.Save(stream);
    }
}
