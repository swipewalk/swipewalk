namespace Swipewalk.Collectors;

/// <summary>
/// Waits for an app to return to the foreground after a system setting change (e.g. a text-size change)
/// that can restart it or briefly show another window, so a capture that follows isn't lost to that
/// transient state. Platform-neutral: the caller supplies the actual foreground check, which keeps this
/// testable without a device or simulator.
/// </summary>
public static class ForegroundWait
{
    /// <summary>How long to give the app to come back to front before giving up.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(6);

    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Calls <paramref name="isInFront"/> until it returns true or <paramref name="timeout"/> elapses, waiting
    /// <paramref name="pollInterval"/> between attempts. Returns whether the app was confirmed in front.
    /// </summary>
    public static async Task<bool> UntilInFrontAsync(
        Func<Task<bool>> isInFront, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (await isInFront())
                return true;
            if (cancellationToken.IsCancellationRequested || DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(pollInterval, cancellationToken);
        }
    }
}
