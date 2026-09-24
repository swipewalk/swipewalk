using System.IO.Compression;
using Swipewalk.Collectors;
using Swipewalk.Collectors.Ios;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Detecting .NET MAUI from an installed app bundle: Microsoft.Maui.Controls.dll sits flat in the bundle root
/// on both the Simulator and a device .ipa/.app (confirmed 2026-09-22 against the TipCalc/DeveloperBalance/
/// Calculator MAUI samples and BuggyApp on a booted Simulator, all .NET MAUI 10.0.60), so a real .NET assembly
/// is used as the fixture: this test project's own compiled .dll, which the SDK stamps with a genuine
/// AssemblyInformationalVersionAttribute including a "+commit" suffix (deterministic build), the same shape
/// .NET MAUI's own build ships ("10.0.60+bf6156897c887d33dcd40592db3bcb6471916e03" was read from the real
/// TipCalc sample).
/// </summary>
public class IosFrameworkDetectionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"swipewalk-frameworktest-{Guid.NewGuid():N}");

    /// <summary>A real, readable .NET assembly with a genuine "+commit" suffix on its informational version.</summary>
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

    [Fact]
    public void ReadInformationalVersion_StripsCommitSuffix()
    {
        var dll = Path.Combine(_dir, "Microsoft.Maui.Controls.dll");
        File.Copy(RealAssemblyPath, dll);

        var version = IosCollector.ReadInformationalVersion(dll);

        Assert.NotNull(version);
        Assert.DoesNotContain('+', version);
        // The raw AssemblyInformationalVersion (read independently here) really does have a "+commit" suffix,
        // so this is a genuine strip, not a version string that happened not to have one.
        Assert.Contains('+', ReadRawInformationalVersion(dll));
        Assert.StartsWith(version, ReadRawInformationalVersion(dll), StringComparison.Ordinal);
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

    /// <summary>Reads AssemblyInformationalVersion independently of <see cref="IosCollector"/>, from the
    /// assembly's own metadata, so the "genuinely has a +commit suffix" assertion doesn't rely on the method
    /// under test.</summary>
    private static string ReadRawInformationalVersion(string path)
    {
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
        return info.ProductVersion ?? "";
    }
}
