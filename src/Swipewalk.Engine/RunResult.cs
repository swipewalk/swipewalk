using Swipewalk.Core.Reports;

namespace Swipewalk.Engine;

/// <summary>A finished scan or recording: the report and where it was written.</summary>
public sealed record RunResult(ScanReport Report, string HtmlPath, string JsonPath);
