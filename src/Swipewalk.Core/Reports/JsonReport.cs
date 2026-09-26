using System.Text.Json;
using System.Text.Json.Serialization;
using Swipewalk.Core.Model;

namespace Swipewalk.Core.Reports;

public static class JsonReport
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(ScanReport report) => JsonSerializer.Serialize(report, Options);

    /// <summary>
    /// Deserializes a results.json. A file saved before <see cref="Model.ScreenResult.ScreenId"/> existed has
    /// no "screenId" property at all, so each affected screen here gets a deterministic id derived from its
    /// position (<c>"legacy-{index}"</c>) instead of that property's own default (a fresh random GUID every
    /// time) -- a random default would give the SAME old file a DIFFERENT id on every separate load (a new
    /// `swipewalk guide` process, a later report open), silently orphaning any guided-check answer saved
    /// against an earlier load. Deterministic-by-position is stable across loads of the same file (record mode
    /// only ever appends screens, never reorders them) even though it can't be a real per-screen identity.
    /// Also forces <see cref="Model.ScreenReaderCapture.Scope"/> to
    /// <see cref="Model.ScreenReaderCaptureScope.FocusableElementsOnly"/> for any TalkBack capture -- a
    /// results.json saved before that field existed has no "scope" property at all, and would otherwise
    /// silently deserialize to the field's default (<see cref="Model.ScreenReaderCaptureScope.AllElements"/>),
    /// which is wrong for TalkBack: Scope is a fixed fact about the source, not something a real capture ever
    /// varies, so this is always correct to force, not just a best-effort default. And clears
    /// <see cref="Model.ScreenReaderCapture.NotCompleteReason"/> on an old TalkBack capture that carries the
    /// exact reason the harness used to write on every ordinary, fully-successful capture before
    /// RulesetVersion 2026.09.30 (see <see cref="LegacyTalkBackFocusableElementsOnlyReason"/>) -- reading that
    /// file back today, unchanged, would show a self-contradictory report caveat ("did not cover every
    /// focusable/interactive element ... only focusable/interactive elements were captured"). The capture
    /// itself stays not <see cref="Model.ScreenReaderCapture.Complete"/>, deliberately not upgraded: the old
    /// harness allowed up to half its elements to stay silent and still called this the normal case, so there
    /// is no way to tell from an old file alone whether this particular run genuinely reached everything.
    /// </summary>
    public static ScanReport? Deserialize(string json)
    {
        var report = JsonSerializer.Deserialize<ScanReport>(json, Options);
        if (report is null)
            return report;
        return report with
        {
            Screens = [.. report.Screens.Select((s, i) => FixLegacyFields(s, i))],
        };
    }

    /// <summary>The exact reason TalkBack's harness (<c>harness/android/.../TalkBackCollector.kt</c>) used to
    /// write on every ordinary, fully-successful capture, before RulesetVersion 2026.09.30 fixed
    /// <see cref="Model.ScreenReaderCapture.Complete"/>'s meaning for TalkBack -- kept here only to recognize
    /// and clear it from an old saved file (see <see cref="Deserialize"/>'s remarks); no longer written by
    /// the harness itself, and no automated cross-check against the Kotlin source exists (it runs outside
    /// dotnet test's reach).</summary>
    private const string LegacyTalkBackFocusableElementsOnlyReason =
        "only focusable/interactive elements were captured, not plain informational text (to keep the capture fast)";

    private static ScreenResult FixLegacyFields(ScreenResult screen, int index)
    {
        if (string.IsNullOrEmpty(screen.ScreenId))
            screen = screen with { ScreenId = $"legacy-{index}" };
        if (screen.ScreenReaderCapture is { Source: ScreenReaderSource.TalkBack } capture)
        {
            if (capture.Scope != ScreenReaderCaptureScope.FocusableElementsOnly)
                capture = capture with { Scope = ScreenReaderCaptureScope.FocusableElementsOnly };
            if (capture.NotCompleteReason == LegacyTalkBackFocusableElementsOnlyReason)
                capture = capture with { NotCompleteReason = null };
            screen = screen with { ScreenReaderCapture = capture };
        }
        return screen;
    }
}
