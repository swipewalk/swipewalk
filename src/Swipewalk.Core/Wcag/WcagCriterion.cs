namespace Swipewalk.Core.Wcag;

public enum WcagLevel
{
    A,
    AA,
    AAA,
}

/// <summary>WCAG versions. Each version includes every criterion of the earlier ones (except 4.1.1, removed in 2.2).</summary>
public enum WcagVersion
{
    V2_0,
    V2_1,
    V2_2,
}

/// <summary>A WCAG 2.2 success criterion, e.g. 4.1.2 Name, Role, Value (A), and the version that introduced it.</summary>
public sealed record WcagCriterion(string Number, string Name, WcagLevel Level, WcagVersion Since)
{
    public override string ToString() => $"{Number} {Name} ({Level})";
}

public static class WcagVersionExtensions
{
    public static string Display(this WcagVersion version) => version switch
    {
        WcagVersion.V2_0 => "2.0",
        WcagVersion.V2_1 => "2.1",
        _ => "2.2",
    };
}
