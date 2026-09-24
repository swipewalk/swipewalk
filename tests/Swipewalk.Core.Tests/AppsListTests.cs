using Swipewalk.Collectors;

namespace Swipewalk.Core.Tests;

public class AppsListTests
{
    [Fact]
    public void ParsePmPackages_StripsThePackagePrefix()
    {
        const string output = "package:org.example.city.permits\npackage:org.example.city.transit\n\n";

        Assert.Equal(["org.example.city.permits", "org.example.city.transit"], AppsList.ParsePmPackages(output));
    }

    [Fact]
    public void ParsePmPackages_IgnoresBlankAndMalformedLines()
    {
        const string output = "\npackage:org.example.city.permits\nsome other line\n";

        Assert.Equal(["org.example.city.permits"], AppsList.ParsePmPackages(output));
    }

    [Fact]
    public async Task ParseSimctlListApps_DefaultsToUserAppsWithDisplayNames()
    {
        // A made-up, trimmed-down version of what `xcrun simctl listapps <udid>` prints: an old-style
        // ("OpenStep") plist, one entry per installed app, keyed by bundle id.
        const string plist = """
            {
              "com.apple.MobileSMS" =     {
                ApplicationType = System;
                CFBundleIdentifier = "com.apple.MobileSMS";
                CFBundleDisplayName = Messages;
                CFBundleName = Messages;
              };
              "org.example.exampleville.permits" =     {
                ApplicationType = User;
                CFBundleIdentifier = "org.example.exampleville.permits";
                CFBundleDisplayName = "Permit Center";
                CFBundleName = PermitCenter;
              };
            }
            """;

        var apps = await AppsList.ParseSimctlListApps(plist, includeSystem: false);

        var app = Assert.Single(apps);
        Assert.Equal("org.example.exampleville.permits", app.Id);
        Assert.Equal("Permit Center", app.Label);
    }

    [Fact]
    public async Task ParseSimctlListApps_IncludeSystemAddsTheSystemApps()
    {
        const string plist = """
            {
              "com.apple.MobileSMS" =     {
                ApplicationType = System;
                CFBundleIdentifier = "com.apple.MobileSMS";
                CFBundleDisplayName = Messages;
              };
              "org.example.exampleville.permits" =     {
                ApplicationType = User;
                CFBundleIdentifier = "org.example.exampleville.permits";
                CFBundleDisplayName = "Permit Center";
              };
            }
            """;

        var apps = await AppsList.ParseSimctlListApps(plist, includeSystem: true);

        Assert.Equal(2, apps.Count);
        Assert.Contains(apps, a => a.Id == "com.apple.MobileSMS" && a.Label == "Messages");
        Assert.Contains(apps, a => a.Id == "org.example.exampleville.permits" && a.Label == "Permit Center");
    }

    [Fact]
    public async Task ParseSimctlListApps_FallsBackToCFBundleNameWithoutADisplayName()
    {
        const string plist = """
            {
              "org.example.exampleville.transit" =     {
                ApplicationType = User;
                CFBundleIdentifier = "org.example.exampleville.transit";
                CFBundleName = TransitTracker;
              };
            }
            """;

        var apps = await AppsList.ParseSimctlListApps(plist, includeSystem: false);

        Assert.Equal("TransitTracker", Assert.Single(apps).Label);
    }

    [Fact]
    public void ParseDevicectlApps_ReadsIdAndName()
    {
        // A made-up, trimmed-down version of `xcrun devicectl device info apps --json-output` (result.apps[]).
        const string json = """
            {"result":{"apps":[
              {"bundleIdentifier":"org.example.exampleville.permits","name":"Permit Center","removable":true},
              {"bundleIdentifier":"org.example.exampleville.transit","name":"Transit Tracker","removable":true}
            ]}}
            """;

        var apps = AppsList.ParseDevicectlApps(json);

        Assert.Equal(["org.example.exampleville.permits", "org.example.exampleville.transit"], apps.Select(a => a.Id));
        Assert.Equal(["Permit Center", "Transit Tracker"], apps.Select(a => a.Label));
    }

    [Fact]
    public void ParseDevicectlApps_SkipsEntriesWithoutABundleId()
    {
        const string json = """{"result":{"apps":[{"name":"No id"},{"bundleIdentifier":"org.example.exampleville.permits","name":"Permit Center"}]}}""";

        var app = Assert.Single(AppsList.ParseDevicectlApps(json));
        Assert.Equal("org.example.exampleville.permits", app.Id);
    }

    [Fact]
    public void ParseDevicectlApps_EmptyWithoutAnAppsList()
    {
        Assert.Empty(AppsList.ParseDevicectlApps("""{"result":{}}"""));
    }
}
