using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

/// <summary>
/// <see cref="CachedInspectorGuide"/> is what makes record mode's Accessibility Inspector route ask its
/// one-time permission-and-setup guide once for the whole recording, not once per screen -- see
/// <c>IosScreenSource.CaptureScreenReaderAsync</c>. Pure and device-independent, so unlike
/// <see cref="IosInspectorWalkTests"/>/<see cref="IosInspectorGuideTests"/> this never touches a real guide
/// prompt, the macOS Accessibility permission or Accessibility Inspector.
/// </summary>
public class CachedInspectorGuideTests
{
    [Fact]
    public async Task AskAsync_AsksOnlyOnce_AndCachesTheAnswer()
    {
        var calls = 0;
        var cached = new CachedInspectorGuide(_ => { calls++; return Task.FromResult(true); }, _ => { });

        var first = await cached.AskAsync(CancellationToken.None);
        var second = await cached.AskAsync(CancellationToken.None);
        var third = await cached.AskAsync(CancellationToken.None);

        Assert.True(first);
        Assert.True(second);
        Assert.True(third);
        Assert.Equal(1, calls);
        Assert.Equal(true, cached.Answer);
    }

    [Fact]
    public async Task AskAsync_DeclinedTheFirstTime_NeverAsksAgain()
    {
        var calls = 0;
        var cached = new CachedInspectorGuide(_ => { calls++; return Task.FromResult(false); }, _ => { });

        Assert.False(await cached.AskAsync(CancellationToken.None));
        Assert.False(await cached.AskAsync(CancellationToken.None));

        Assert.Equal(1, calls);
        Assert.Equal(false, cached.Answer);
    }

    [Fact]
    public async Task AskAsync_NoGuideWiredUp_DeclinesWithoutThrowing()
    {
        var cached = new CachedInspectorGuide(guide: null, _ => { });

        Assert.False(await cached.AskAsync(CancellationToken.None));
        Assert.False(await cached.AskAsync(CancellationToken.None));
        Assert.Equal(false, cached.Answer);
    }

    [Fact]
    public async Task AskAsync_CallsOnFirstAnswerExactlyOnce_WithTheRealAnswer()
    {
        var reported = new List<bool>();
        var cached = new CachedInspectorGuide(_ => Task.FromResult(true), answer => reported.Add(answer));

        await cached.AskAsync(CancellationToken.None);
        await cached.AskAsync(CancellationToken.None);
        await cached.AskAsync(CancellationToken.None);

        Assert.Equal([true], reported);
    }

    [Fact]
    public void Answer_BeforeAnyCall_IsNull()
    {
        var cached = new CachedInspectorGuide(_ => Task.FromResult(true), _ => { });

        Assert.Null(cached.Answer);
    }
}
