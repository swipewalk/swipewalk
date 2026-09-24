using Swipewalk.Core.Model;

namespace Swipewalk.Core.Limitations;

public enum LimitationKind
{
    /// <summary>Something Swipewalk cannot check or may get wrong.</summary>
    Limitation,

    /// <summary>A behavior of a platform or framework that affects accessibility and that developers should know.</summary>
    FrameworkNote,
}

public enum LimitationArea
{
    Coverage,
    ScreenReader,
    Detection,
    Rules,
    Devices,
    FixExamples,
}

/// <summary>
/// A documented limitation or framework note. Empty <see cref="Platforms"/> or <see cref="Frameworks"/>
/// means it applies to all of them.
/// </summary>
public sealed record Limitation
{
    public required string Id { get; init; }
    public LimitationKind Kind { get; init; } = LimitationKind.Limitation;
    public required LimitationArea Area { get; init; }
    public IReadOnlyList<Platform> Platforms { get; init; } = [];
    public IReadOnlyList<AppFramework> Frameworks { get; init; } = [];
    public required string Title { get; init; }

    /// <summary>What the limitation is, in plain language.</summary>
    public required string Description { get; init; }

    /// <summary>What it means for results: missed issues, false positives, or approximations.</summary>
    public required string Impact { get; init; }

    /// <summary>What a person should check by hand to cover the gap.</summary>
    public required string ManualCheck { get; init; }

    /// <summary>Rule ids affected, if any.</summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    /// <summary>Roadmap item that would address it, if planned.</summary>
    public string? Planned { get; init; }

    public bool AppliesTo(Platform platform, AppFramework framework) =>
        (Platforms.Count == 0 || Platforms.Contains(platform))
        && (Frameworks.Count == 0 || Frameworks.Contains(framework));

    /// <summary>"All platforms · MAUI" style label for grouping in docs and reports.</summary>
    public string ScopeLabel =>
        $"{(Platforms.Count == 0 ? "All platforms" : string.Join(", ", Platforms))} · " +
        $"{(Frameworks.Count == 0 ? "All frameworks" : string.Join(", ", Frameworks.Select(FrameworkName)))}";

    private static string FrameworkName(AppFramework f) => f switch
    {
        AppFramework.Maui => ".NET MAUI",
        AppFramework.Unknown => "Other / native",
        _ => f.ToString(),
    };
}
