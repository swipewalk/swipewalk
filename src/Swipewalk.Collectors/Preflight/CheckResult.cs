namespace Swipewalk.Collectors.Preflight;

public enum CheckStatus
{
    Pass,

    /// <summary>Scanning can continue, but something may affect results or need attention.</summary>
    Warn,

    /// <summary>Scanning cannot work until this is fixed.</summary>
    Fail,
}

/// <summary>One readiness check before a scan, with what to do when it doesn't pass.</summary>
public sealed record CheckResult(string Name, CheckStatus Status, string Detail, string? Fix = null)
{
    public override string ToString() =>
        $"{Status switch { CheckStatus.Pass => "ok  ", CheckStatus.Warn => "warn", _ => "FAIL" }}  {Name}: {Detail}" +
        (Fix is null ? "" : $"\n        → {Fix}");
}
