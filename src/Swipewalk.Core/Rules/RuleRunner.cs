using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.ScreenReader;
using Swipewalk.Core.Standards;

namespace Swipewalk.Core.Rules;

public sealed class RuleRunner(IEnumerable<IRule> rules)
{
    private readonly IReadOnlyList<IRule> _rules = [.. rules];

    public ScreenResult Run(ScreenSnapshot snapshot)
    {
        var findings = new List<Finding>();
        foreach (var rule in _rules)
        {
            foreach (var finding in rule.Evaluate(snapshot))
            {
                Validate(rule, finding);
                findings.Add(finding);
            }
        }

        return new ScreenResult
        {
            Platform = snapshot.Platform,
            ScreenName = snapshot.ScreenName,
            Framework = snapshot.Framework,
            FrameworkVersion = snapshot.FrameworkVersion,
            Device = snapshot.Device,
            ScreenshotPath = snapshot.ScreenshotPath,
            Findings = [.. MergeEngineDuplicates(findings).Select(f => f with
            {
                RelevantStandards = KnownStandards.RelevantTo(f),
                Fix = FixGuidance.For(f, snapshot.Platform, snapshot.Framework, snapshot.FrameworkVersion),
            })],
            PixelScale = snapshot.PixelScale,
            LargeTextScreenshotPath = snapshot.LargeText?.ScreenshotPath,
            LargeTextPixelScale = snapshot.LargeText?.PixelScale ?? 1.0,
            LargeTextSetting = snapshot.LargeText is null ? null : snapshot.LargeTextSetting,
            LargeTextScale = snapshot.LargeText is null ? null : snapshot.LargeTextScale,
            LargeTextMethod = snapshot.LargeText is null ? null : snapshot.LargeTextMethod,
            LargeTextAppliedLive = snapshot.LargeText is null ? null : snapshot.LargeTextAppliedLive,
            LargeTextRestartCaptured = snapshot.LargeText is null ? null : snapshot.LargeTextRestartCaptured,
            PredictedTranscript = ScreenReaderPredictor.Predict(snapshot),
            ScreenReaderCapture = snapshot.ScreenReaderCapture,
            AppId = snapshot.AppId,
            AtfRan = snapshot.AtfRan,
            AtfSkippedReason = snapshot.AtfSkippedReason,
        };
    }

    /// <summary>
    /// When a platform engine reports the same issue on the same element as a Swipewalk rule, keep one
    /// finding and record the engine in <see cref="Finding.AlsoReportedBy"/>.
    /// </summary>
    private static List<Finding> MergeEngineDuplicates(List<Finding> findings)
    {
        var own = findings.Where(f => f.Source == Finding.DefaultSource).ToList();
        var result = new List<Finding>(own);
        foreach (var engine in findings.Where(f => f.Source != Finding.DefaultSource))
        {
            var index = result.FindIndex(o => o.Source == Finding.DefaultSource && SameElement(o, engine) && SameConcern(o, engine));
            if (index < 0)
            {
                result.Add(engine);
                continue;
            }
            var match = result[index];
            if (!match.AlsoReportedBy.Contains(engine.Source))
                result[index] = match with { AlsoReportedBy = [.. match.AlsoReportedBy, engine.Source] };
        }
        return result;
    }

    private static bool SameElement(Finding a, Finding b) =>
        a.NodePath == b.NodePath
        || (Math.Abs(a.Bounds.X - b.Bounds.X) < 1 && Math.Abs(a.Bounds.Y - b.Bounds.Y) < 1
            && Math.Abs(a.Bounds.Width - b.Bounds.Width) < 1 && Math.Abs(a.Bounds.Height - b.Bounds.Height) < 1);

    private static bool SameConcern(Finding a, Finding b) =>
        a.Criteria.Intersect(b.Criteria).Any()
        || (a.Kind == FindingKind.PlatformAdvisory && b.Kind == FindingKind.PlatformAdvisory);

    private static void Validate(IRule rule, Finding finding)
    {
        // A WcagIssue is a claim that a WCAG criterion is likely failed, so the hard rule ("every finding
        // maps to at least one WCAG 2.2 success criterion... if unsure, leave the mapping out and flag it")
        // requires one here. NeedsReview makes no such claim -- it can legitimately have none, for an engine
        // check (Apple's audit, ATF) whose purpose doesn't correspond to one specific criterion; see
        // EngineIssueRule's "textClipped" and AtfIssueRule's unmapped checks, both "flagged" this way.
        if (finding.Kind == FindingKind.WcagIssue && finding.Criteria.Count == 0)
            throw new InvalidOperationException(
                $"Rule '{rule.Id}' produced a WCAG finding with no success criterion.");

        if (finding.Kind == FindingKind.PlatformAdvisory
            && (finding.Criteria.Count > 0 || string.IsNullOrEmpty(finding.PlatformGuideline)))
            throw new InvalidOperationException(
                $"Rule '{rule.Id}' produced a platform advisory that cites WCAG criteria or no guideline.");
    }
}
