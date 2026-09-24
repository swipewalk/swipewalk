using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Core.Standards;

namespace Swipewalk.Engine;

public static class ReportWriter
{
    /// <summary>Writes report.html and results.json (with screenshot paths relative to the output directory).</summary>
    public static async Task<(string Html, string Json)> WriteAsync(ScanReport report, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var htmlPath = Path.Combine(outDir, "report.html");
        await File.WriteAllTextAsync(htmlPath, HtmlReport.Render(report));

        string? Relative(string? path) => path is null ? null : Path.GetRelativePath(outDir, path);
        var portable = report with
        {
            Screens = [.. report.Screens.Select(s => s with
            {
                ScreenshotPath = Relative(s.ScreenshotPath),
                LargeTextScreenshotPath = Relative(s.LargeTextScreenshotPath),
            })],
        };
        var jsonPath = Path.Combine(outDir, "results.json");
        await File.WriteAllTextAsync(jsonPath, JsonReport.Serialize(portable));
        return (htmlPath, jsonPath);
    }

    public static string Summary(ScanReport report) =>
        $"Automated checks found {report.Count(FindingKind.WcagIssue)} WCAG issue(s)" +
        (report.FocusStandard is { } id && KnownStandards.Find(id) is { } s ? $" relevant to {s.ShortName} ({s.Basis})" : "") +
        $", {report.Count(FindingKind.NeedsReview)} " +
        $"item(s) needing review and {report.Count(FindingKind.PlatformAdvisory)} platform advisory(ies) " +
        $"on {report.Screens.Count} screen(s). Manual testing is still required.";
}
