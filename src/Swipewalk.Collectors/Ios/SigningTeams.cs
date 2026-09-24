using System.Security.Cryptography.X509Certificates;

namespace Swipewalk.Collectors.Ios;

/// <summary>An Apple developer team that has a valid development certificate in the keychain.</summary>
public sealed record SigningTeam(string TeamId, string TeamName)
{
    public override string ToString() => $"{TeamName} ({TeamId})";
}

/// <summary>
/// Chooses the team that signs the iOS harness for physical devices, so users rarely need --team:
/// an explicit --team (remembered for next time), else the remembered team, else the only team with a
/// valid Apple Development certificate in the keychain. Apple requires signing for anything that runs on a
/// physical device; the Simulator needs none, and the app being scanned is never re-signed.
/// </summary>
public static class SigningTeams
{
    public sealed record Choice(SigningTeam? Team, string Source, string? Problem);

    public static Choice Resolve(string? explicitTeamId, UserSettings? settings = null, IReadOnlyList<SigningTeam>? available = null)
    {
        settings ??= UserSettings.Load();
        available ??= FromKeychain();

        if (explicitTeamId is not null)
        {
            var team = available.FirstOrDefault(t => t.TeamId == explicitTeamId) ?? new SigningTeam(explicitTeamId, "team");
            return new(team, "--team (remembered for next time)", null);
        }
        if (settings.AppleTeamId is { } saved)
            return new(available.FirstOrDefault(t => t.TeamId == saved) ?? new SigningTeam(saved, "team"), "remembered from an earlier run", null);

        return available.Count switch
        {
            1 => new(available[0], "the only development team in the keychain", null),
            0 => new(null, "", "No Apple Development certificate found. Open Xcode > Settings > Accounts, sign in with an Apple ID " +
                               "(a free Apple ID works for your own devices) and select \"Manage Certificates\" > \"+\" > Apple Development."),
            _ => new(null, "", $"Several development teams found: {string.Join(", ", available)}. " +
                               "Pass --team <team id> once; it is remembered."),
        };
    }

    /// <summary>Teams (certificate OU) with a currently valid Apple Development certificate and private key.</summary>
    public static IReadOnlyList<SigningTeam> FromKeychain()
    {
        if (!OperatingSystem.IsMacOS())
            return [];
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var now = DateTime.Now;
        return [.. store.Certificates
            .Where(c => c.HasPrivateKey && c.NotBefore <= now && c.NotAfter > now
                        && (c.Subject.Contains("CN=Apple Development:", StringComparison.Ordinal)
                            || c.Subject.Contains("CN=iPhone Developer:", StringComparison.Ordinal)))
            .Select(c => new SigningTeam(Field(c.SubjectName, "OU") ?? "", Field(c.SubjectName, "O") ?? "team"))
            .Where(t => t.TeamId.Length > 0)
            .DistinctBy(t => t.TeamId)];
    }

    /// <summary>Remembers a team given with --team.</summary>
    public static void Remember(string teamId)
    {
        var settings = UserSettings.Load();
        if (settings.AppleTeamId != teamId)
            (settings with { AppleTeamId = teamId }).Save();
    }

    private static string? Field(X500DistinguishedName name, string key) =>
        name.EnumerateRelativeDistinguishedNames()
            .FirstOrDefault(r => r.GetSingleElementType().FriendlyName == key || r.GetSingleElementType().Value == Oid(key))
            ?.GetSingleElementValue();

    private static string Oid(string key) => key switch { "OU" => "2.5.4.11", "O" => "2.5.4.10", _ => "" };
}
