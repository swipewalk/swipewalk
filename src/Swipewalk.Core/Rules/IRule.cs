using Swipewalk.Core.Model;

namespace Swipewalk.Core.Rules;

/// <summary>An automated check over a platform-neutral screen snapshot.</summary>
public interface IRule
{
    /// <summary>Stable identifier used in JSON output, e.g. "missing-label".</summary>
    string Id { get; }

    IEnumerable<Finding> Evaluate(ScreenSnapshot snapshot);
}
