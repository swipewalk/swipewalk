namespace Swipewalk.Collectors.Ios;

/// <summary>
/// How the harness is signed for a physical device:
/// 1. an installed, manually managed development profile that fits (team, harness bundle ids, this device, not
///    expired): manual signing, no Apple ID or network needed; covers certificates and profiles installed by hand
///    from the Apple Developer portal. Xcode-managed profiles can't be used for manual signing;
/// 2. otherwise Xcode automatic signing, which needs an Apple ID signed in to Xcode to create or reuse profiles;
/// 3. otherwise a failure that says why each installed profile didn't fit.
/// </summary>
public sealed record SigningPlan(SigningTeam Team, ProvisioningProfile? Profile, string BundlePrefix, string Description)
{
    public const string DefaultBundlePrefix = "org.swipewalk.harness";

    public bool IsManual => Profile is not null;

    /// <summary>Bundle ids signed for a UI test: the host app and the XCTest runner app.</summary>
    public static string[] HarnessBundleIds(string prefix) => [$"{prefix}.runner", $"{prefix}.uitests.xctrunner"];

    public static (SigningPlan? Plan, string? Problem) Create(
        string? explicitTeam, string? explicitProfile, string? bundlePrefix, string udid,
        IReadOnlyList<ProvisioningProfile>? installed = null, bool? xcodeHasAccount = null, DateTime? now = null)
    {
        var teamChoice = SigningTeams.Resolve(explicitTeam);
        if (teamChoice.Team is not { } team)
            return (null, teamChoice.Problem);

        var prefix = bundlePrefix ?? DefaultBundlePrefix;
        installed ??= ProvisioningProfiles.FindInstalled();
        var at = now ?? DateTime.UtcNow;
        var bundleIds = HarnessBundleIds(prefix);

        var candidates = explicitProfile is null
            ? installed
            : installed.Where(p => p.Uuid.Equals(explicitProfile, StringComparison.OrdinalIgnoreCase) || p.Name == explicitProfile).ToList();
        if (explicitProfile is not null && candidates.Count == 0)
            return (null, $"Provisioning profile '{explicitProfile}' is not installed. Double-click the .mobileprovision file to install it.");

        var rejected = new List<string>();
        foreach (var profile in candidates.Where(p => p.TeamId == team.TeamId).OrderByDescending(p => p.Expires))
        {
            var reason =
                profile.IsXcodeManaged ? "is managed by Xcode (usable only through Xcode automatic signing)" :
                !profile.IsDevelopment ? "is not a development profile (UI tests need one)" :
                profile.Expires <= at ? $"expired on {profile.Expires:yyyy-MM-dd}" :
                !profile.AllDevices && !profile.Devices.Contains(udid) ? "does not include this device (add it in the Apple Developer portal and reinstall the profile)" :
                bundleIds.FirstOrDefault(id => !profile.Covers(id)) is { } uncovered
                    ? $"does not cover {uncovered} (use a wildcard profile, or --harness-bundle-prefix to match its app id)" :
                null;
            if (reason is null)
                return (new SigningPlan(team, profile, prefix, $"{team}, installed profile {profile}"), null);
            rejected.Add($"{profile} {reason}");
        }

        if (explicitProfile is null && (xcodeHasAccount ?? XcodeHasAccount()))
            return (new SigningPlan(team, null, prefix, $"{team}, Xcode automatic signing (profile created with your Apple ID)"), null);

        return (null,
            "No way to sign the scanning harness for this device. " +
            (rejected.Count > 0 ? $"Installed profiles for {team.TeamId}: {string.Join("; ", rejected)}. " : $"No development profile for {team.TeamId} is installed. ") +
            "Install a development provisioning profile that includes this device (a wildcard app id works), or sign in to Xcode > Settings > Accounts.");
    }

    /// <summary>Whether an Apple ID is signed in to Xcode, which automatic signing needs.</summary>
    public static bool XcodeHasAccount()
    {
        var plist = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Preferences/com.apple.dt.Xcode.plist");
        if (!File.Exists(plist))
            return false;
        var content = File.ReadAllBytes(plist);
        var text = System.Text.Encoding.Latin1.GetString(content);
        return text.Contains("IDEProvisioningTeamByIdentifier", StringComparison.Ordinal)
               || text.Contains("DVTDeveloperAccountManagerAppleIDLists", StringComparison.Ordinal);
    }
}
