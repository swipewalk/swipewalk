namespace Swipewalk.Core.Reports;

/// <summary>
/// One period of recording activity within a single run: <see cref="StartedAt"/> when that session began
/// (fresh, or resumed with <c>record --continue</c>/the desktop app's Continue action), <see cref="EndedAt"/>
/// the last time it's known to have still been running -- updated after every screen (see
/// Swipewalk.Engine.Recorder), so a session that ended uncleanly (the process killed, the device
/// disconnecting) still shows an honest, if slightly early, end time rather than none at all. See
/// <see cref="ScanReport.Sessions"/>.
/// </summary>
public sealed record RecordingSession(DateTimeOffset StartedAt, DateTimeOffset EndedAt);
