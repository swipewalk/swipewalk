namespace Swipewalk.Collectors;

/// <summary>
/// The app under test was not in front, so nothing was captured. Protects privacy on real devices: other
/// apps, notification shades and home screens are never scanned or screenshotted.
/// </summary>
public sealed class TargetNotInFrontException(string message) : InvalidOperationException(message);
