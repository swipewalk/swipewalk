namespace Swipewalk.Collectors.Ios;

/// <summary>
/// Wraps an <see cref="IosCollector.IosInspectorGuide"/> so it is asked at most once, caching whatever it
/// answers the first time and returning that cached answer on every later call without asking again. This is
/// what makes record mode's Accessibility Inspector route (see
/// <see cref="Collectors.IScreenSource.CaptureScreenReaderAsync"/> and <c>IosScreenSource</c>'s override) ask
/// its one-time permission-and-setup step once for the whole recording, not once per screen: one instance is
/// created per recording and its <see cref="AskAsync"/> method is passed to
/// <see cref="IosCollector.RunInspectorCaptureAsync"/> for every screen. Pure and device-independent (no AX API,
/// process, or I/O of its own), so it can be unit-tested without a Mac's Accessibility permission or a real
/// Accessibility Inspector session -- <see cref="IosCollector.RunInspectorCaptureAsync"/> and
/// <see cref="Ios.IosInspectorWalk"/> are what actually touch either of those, and only when the wrapped guide's
/// first answer is true.
/// </summary>
internal sealed class CachedInspectorGuide(IosCollector.IosInspectorGuide? guide, Action<bool> onFirstAnswer)
{
    /// <summary>The cached answer once asked; null before the first call to <see cref="AskAsync"/>.</summary>
    public bool? Answer { get; private set; }

    /// <summary>
    /// Returns the cached answer if this has already been asked once; otherwise asks <paramref name="guide"/>
    /// (a null guide, e.g. no prompt wired up, answers false without asking), caches whatever it answers, calls
    /// <paramref name="onFirstAnswer"/> with that answer (exactly once, only from this first call -- a caller
    /// uses it to log the one-time outcome), and returns it.
    /// </summary>
    public async Task<bool> AskAsync(CancellationToken cancellationToken)
    {
        if (Answer is { } cached)
            return cached;
        var answer = guide is not null && await guide(cancellationToken);
        Answer = answer;
        onFirstAnswer(answer);
        return answer;
    }
}
