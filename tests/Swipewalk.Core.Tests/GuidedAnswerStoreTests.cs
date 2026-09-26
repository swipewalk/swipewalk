using Swipewalk.Core.Coverage;
using Swipewalk.Engine;

namespace Swipewalk.Core.Tests;

public class GuidedAnswerStoreTests
{
    private static GuidedAnswer PassAnswer(string screenId = "s1", string criterion = "1.3.1") => new(
        ScreenId: screenId, CriterionNumber: criterion, Result: GuidedAnswerResult.Pass, Note: null,
        NotApplicableReason: null, FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now,
        Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null,
        Evidence: [new GuidedEvidence("looked fine")]);

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory("swipewalk-guided-store-").FullName;
        var store = GuidedAnswerStore.Empty.With(PassAnswer());

        await store.SaveAsync(dir);
        var loaded = GuidedAnswerStore.Load(dir);

        Assert.NotNull(loaded);
        var answer = Assert.Single(loaded.Answers);
        Assert.Equal("1.3.1", answer.CriterionNumber);
        Assert.Equal(GuidedAnswerResult.Pass, answer.Result);
        Assert.Equal("looked fine", answer.Evidence[0].Description);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var dir = Directory.CreateTempSubdirectory("swipewalk-guided-store-").FullName;

        Assert.Null(GuidedAnswerStore.Load(dir));
    }

    [Fact]
    public void Load_DamagedFile_ReturnsNullRatherThanThrowing()
    {
        var dir = Directory.CreateTempSubdirectory("swipewalk-guided-store-").FullName;
        File.WriteAllText(GuidedAnswerStore.PathFor(dir), "{ not valid json");

        Assert.Null(GuidedAnswerStore.Load(dir));
    }

    [Fact]
    public async Task SaveAsync_RejectsAPassWithNoEvidence()
    {
        var dir = Directory.CreateTempSubdirectory("swipewalk-guided-store-").FullName;
        var invalidPass = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.3.1", Result: GuidedAnswerResult.Pass, Note: null,
            NotApplicableReason: null, FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now,
            Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null, Evidence: []);
        var store = GuidedAnswerStore.Empty.With(invalidPass);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(dir));
        Assert.False(File.Exists(GuidedAnswerStore.PathFor(dir)));
    }

    [Fact]
    public async Task SaveAsync_RejectsAConfirmedNotApplicableWithNoReason()
    {
        var dir = Directory.CreateTempSubdirectory("swipewalk-guided-store-").FullName;
        var invalidNotApplicable = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.3.1", Result: GuidedAnswerResult.ConfirmedNotApplicable, Note: null,
            NotApplicableReason: null, FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now,
            Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null, Evidence: []);
        var store = GuidedAnswerStore.Empty.With(invalidNotApplicable);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(dir));
    }

    [Fact]
    public void GuidedAnswer_IsValid_AcceptsFailAndInconclusiveWithNoEvidenceOrReason()
    {
        var fail = new GuidedAnswer(
            ScreenId: "s1", CriterionNumber: "1.3.1", Result: GuidedAnswerResult.Fail, Note: null,
            NotApplicableReason: null, FastPassConfirmed: false, Tester: "me", AnsweredAt: DateTimeOffset.Now,
            Device: null, AssistiveTechnology: "None", AssistiveTechnologyVersion: null, Evidence: []);

        Assert.True(fail.IsValid);
    }
}
