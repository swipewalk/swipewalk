using Swipewalk.Collectors;
using Swipewalk.Collectors.Android;

namespace Swipewalk.Core.Tests;

/// <summary>
/// Installing an Android App Bundle (.aab) via bundletool -- see AppInstaller.InstallAndroidAsync's .aab branch
/// and Bundletool.Locate. The device-driving parts (build-apks, install-apks against a real adb device) are
/// verified on hardware, not here (see docs/user-guide.md's Android section and CLAUDE-adjacent PR notes); these
/// tests cover the pure, deterministic pieces: locating a configured bundletool path, detecting the fast-
/// deployment build that also applies to .aab entries (they're a zip, same as an .apk), and the keystore
/// password contract.
/// </summary>
public class AndroidBundleInstallTests
{
    [Fact]
    public void Locate_WithConfiguredJarPath_ReturnsItAsJar()
    {
        var jar = Path.Combine(Path.GetTempPath(), $"swipewalk-test-{Guid.NewGuid():N}.jar");
        File.WriteAllText(jar, "");
        try
        {
            var location = Bundletool.Locate(jar);
            Assert.NotNull(location);
            Assert.Equal(jar, location.Value.Path);
            Assert.True(location.Value.IsJar);
        }
        finally
        {
            File.Delete(jar);
        }
    }

    [Fact]
    public void Locate_WithConfiguredExecutablePath_ReturnsItAsNotJar()
    {
        var exe = Path.Combine(Path.GetTempPath(), $"swipewalk-test-{Guid.NewGuid():N}");
        File.WriteAllText(exe, "");
        try
        {
            var location = Bundletool.Locate(exe);
            Assert.NotNull(location);
            Assert.False(location.Value.IsJar);
        }
        finally
        {
            File.Delete(exe);
        }
    }

    [Fact]
    public void Locate_WithConfiguredPathThatDoesNotExist_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Bundletool.Locate("/does/not/exist/bundletool.jar"));
        Assert.Contains("does not exist", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The .NET MAUI fast-deployment check (IsFastDeploymentApk) reads raw zip entry names, so it applies
    /// unchanged to a .aab -- a bundle is a zip too, just with module-prefixed paths (base/lib/... instead of
    /// lib/...) -- rather than needing its own aab-specific detection.
    /// </summary>
    [Fact]
    public void FastDeploymentApk_IsDetectedInsideAabModulePaths()
    {
        Assert.True(AppInstaller.IsFastDeploymentApk(
            ["base/manifest/AndroidManifest.xml", "base/lib/arm64-v8a/libmonodroid.so", "base/dex/classes.dex"]));
        Assert.False(AppInstaller.IsFastDeploymentApk(
            ["base/lib/arm64-v8a/libmonodroid.so", "base/lib/arm64-v8a/libassemblies.arm64-v8a.blob.so"]));
    }

    [Fact]
    public void AndroidBundleSigning_WithoutStorePasswordEnvVar_ThrowsAClearMessage()
    {
        var originalStore = Environment.GetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD");
        var originalKey = Environment.GetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD");
        Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", null);
        try
        {
            var signing = new AppInstaller.AndroidBundleSigning("/some/keystore.jks", "release");
            var ex = Assert.Throws<InvalidOperationException>(() => signing.ReadPasswords());
            Assert.Contains("SWIPEWALK_KEYSTORE_PASSWORD", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", originalStore);
            Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", originalKey);
        }
    }

    /// <summary>
    /// With no SWIPEWALK_KEY_PASSWORD, ReadPasswords leaves the key password null rather than assuming it's the
    /// same as the store password: AppInstaller then leaves bundletool's --key-pass out entirely, and
    /// bundletool itself tries the keystore password for the key (its own --help says so) -- the right
    /// behaviour for the common case of a self-managed keystore with one password for both.
    /// </summary>
    [Fact]
    public void AndroidBundleSigning_WithOnlyStorePassword_LeavesKeyPasswordNull()
    {
        var originalStore = Environment.GetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD");
        var originalKey = Environment.GetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD");
        Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", "store-secret");
        Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", null);
        try
        {
            var signing = new AppInstaller.AndroidBundleSigning("/some/keystore.jks", "release");
            var (store, key) = signing.ReadPasswords();
            Assert.Equal("store-secret", store);
            Assert.Null(key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", originalStore);
            Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", originalKey);
        }
    }

    [Fact]
    public void AndroidBundleSigning_WithBothPasswords_KeepsThemDistinct()
    {
        var originalStore = Environment.GetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD");
        var originalKey = Environment.GetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD");
        Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", "store-secret");
        Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", "key-secret");
        try
        {
            var signing = new AppInstaller.AndroidBundleSigning("/some/keystore.jks", "release");
            var (store, key) = signing.ReadPasswords();
            Assert.Equal("store-secret", store);
            Assert.Equal("key-secret", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWIPEWALK_KEYSTORE_PASSWORD", originalStore);
            Environment.SetEnvironmentVariable("SWIPEWALK_KEY_PASSWORD", originalKey);
        }
    }
}
