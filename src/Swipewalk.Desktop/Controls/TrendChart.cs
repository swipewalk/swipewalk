using Swipewalk.Desktop.Services;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Controls;

/// <summary>
/// Bar chart of WCAG issues per run, oldest to newest, with each bar's number shown above it so the chart
/// doesn't rely on color or bar height alone. Built from layout views rather than a GraphicsView: text drawn on
/// a canvas doesn't render on Mac Catalyst, and labels scale with the app's text size.
/// Screen readers get one element whose description lists the same numbers (here, unlike on other containers,
/// hiding the children is intended: the bars carry no information of their own).
/// </summary>
public sealed class TrendChart : HorizontalStackLayout
{
    public const int MaxRuns = 10;

    public TrendChart(string app, IReadOnlyList<RunRecord> newestFirst)
    {
        var values = newestFirst.Take(MaxRuns).Reverse().Select(r => r.Counts.WcagIssues).ToList();
        var max = Math.Max(1, values.DefaultIfEmpty().Max());
        var barArea = 80 * Fonts.Scale;
        Spacing = 12;
        HorizontalOptions = LayoutOptions.Start;
        foreach (var value in values)
        {
            Children.Add(new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                Spacing = 4,
                Children =
                {
                    new Label { Text = value.ToString(), FontSize = Fonts.Size("FontCaption"), HorizontalOptions = LayoutOptions.Center },
                    new BoxView
                    {
                        Color = AppState.ThemeColor("Issue"),
                        WidthRequest = 28 * Fonts.Scale,
                        // A zero count still shows a thin bar, so the run is visibly there.
                        HeightRequest = Math.Max(2, barArea * value / max),
                    },
                },
            });
        }
        SemanticProperties.SetDescription(this,
            $"Chart of WCAG issues in the last {values.Count} run(s) of {app}, oldest to newest: {string.Join(", ", values)}");
    }
}
