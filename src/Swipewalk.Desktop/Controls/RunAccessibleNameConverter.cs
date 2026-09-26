using System.Globalization;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Controls;

/// <summary>
/// Builds the accessible name for a History row from its <see cref="RunRecord"/>. HistoryPage groups the row's
/// informational Labels (app, date, platform, mode, counts, standard) into one element with this as its
/// <c>SemanticProperties.Description</c>, so VoiceOver/keyboard focus on the row announces the full summary
/// instead of only the app name Label nested inside it -- but the row's Delete button stays outside that group
/// (see HistoryPage.xaml) so it remains its own separately reachable, named control: a
/// Description on a container hides any separately reachable children underneath it.
/// </summary>
public sealed class RunAccessibleNameConverter : IValueConverter
{
    /// <summary>ConverterParameter "Delete" gives the Delete button's own name instead, matching the wording of
    /// the delete confirmation in HistoryPage.xaml.cs.</summary>
    public const string DeleteParameter = "Delete";

    /// <summary>ConverterParameter "Continue" gives the Continue button (only shown for a recording that
    /// ended early -- see <see cref="RunRecord.CanContinue"/>) its own name.</summary>
    public const string ContinueParameter = "Continue";

    /// <summary>ConverterParameter "GuidedChecks" gives the Guided checks button its own name.</summary>
    public const string GuidedChecksParameter = "GuidedChecks";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RunRecord run)
            return string.Empty;

        if (string.Equals(parameter as string, DeleteParameter, StringComparison.Ordinal))
            return $"Delete the run of {run.App} from {run.StartedAt:yyyy-MM-dd HH:mm}";

        if (string.Equals(parameter as string, ContinueParameter, StringComparison.Ordinal))
            return $"Continue the run of {run.App} from {run.StartedAt:yyyy-MM-dd HH:mm}, which ended early";

        if (string.Equals(parameter as string, GuidedChecksParameter, StringComparison.Ordinal))
            return $"Guided checks for the run of {run.App} from {run.StartedAt:yyyy-MM-dd HH:mm}";

        // RecordingInProgress here means a live recording -- here or in another window; see RunHistory.List,
        // which only leaves it true when the owning process is confirmed still running, and otherwise reads a
        // run whose process is gone (e.g. Swipewalk was force-quit or crashed) as CanContinue instead, the
        // same as any other ended-early recording.
        var status = run.RecordingInProgress ? ", recording in progress" : run.CanContinue ? ", ended early" : "";
        return $"{run.App}, {run.StartedAt:yyyy-MM-dd HH:mm}, {run.Platform}, {run.Mode}{status}, " +
               $"{run.Counts.Screens} screen(s), {run.Counts.WcagIssues} WCAG issues, {run.Counts.NeedsReview} to review, {run.StandardLabel}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
