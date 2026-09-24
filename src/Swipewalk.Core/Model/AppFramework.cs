namespace Swipewalk.Core.Model;

/// <summary>
/// The UI framework the scanned app was built with. Scanning does not depend on it; it only selects
/// fix examples. Unknown falls back to the platform's native API.
/// </summary>
public enum AppFramework
{
    Unknown,
    Maui,
}
