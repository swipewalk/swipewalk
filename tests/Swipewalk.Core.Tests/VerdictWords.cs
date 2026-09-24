namespace Swipewalk.Core.Tests;

/// <summary>
/// Words banned as a compliance verdict anywhere in output (reports, JSON, CLI text). "accessible"
/// is deliberately not included: it is legitimate accessibility terminology (e.g. "accessible name",
/// "Accessible Authentication (Minimum)" is a WCAG 2.2 criterion name), and only banned specifically as a
/// verdict ("the app is accessible"), which none of Swipewalk's fixed report text asserts. Shared so every
/// test that scans generated text for verdict wording checks the same list.
/// </summary>
internal static class VerdictWords
{
    public static readonly string[] All =
        ["compliant", "certified", "passes", "passed", "compliance", "fully accessible", "meets wcag", "satisf"];
}
