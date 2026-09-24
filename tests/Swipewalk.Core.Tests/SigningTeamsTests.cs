using Swipewalk.Collectors;
using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

public class SigningTeamsTests
{
    private static readonly SigningTeam Personal = new("A1B2C3D4E5", "Example Developer");
    private static readonly SigningTeam Other = new("ABCDE12345", "Other Team");

    [Fact]
    public void SingleTeam_IsChosenAutomatically()
    {
        var choice = SigningTeams.Resolve(null, new UserSettings(), [Personal]);

        Assert.Equal(Personal, choice.Team);
        Assert.Contains("only development team", choice.Source);
    }

    [Fact]
    public void SeveralTeams_AskOnce_UnlessRemembered()
    {
        Assert.Null(SigningTeams.Resolve(null, new UserSettings(), [Personal, Other]).Team);
        Assert.Equal(Other, SigningTeams.Resolve(null, new UserSettings { AppleTeamId = "ABCDE12345" }, [Personal, Other]).Team);
    }

    [Fact]
    public void ExplicitTeam_WinsOverRemembered()
    {
        var choice = SigningTeams.Resolve("A1B2C3D4E5", new UserSettings { AppleTeamId = "ABCDE12345" }, [Personal, Other]);

        Assert.Equal(Personal, choice.Team);
    }

    [Fact]
    public void NoTeam_ExplainsFreeAppleId()
    {
        var choice = SigningTeams.Resolve(null, new UserSettings(), []);

        Assert.Null(choice.Team);
        Assert.Contains("free Apple ID", choice.Problem);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cf-settings-{Guid.NewGuid():N}.json");
        new UserSettings { AppleTeamId = "A1B2C3D4E5" }.Save(path);

        Assert.Equal("A1B2C3D4E5", UserSettings.Load(path).AppleTeamId);
        File.Delete(path);
    }
}
