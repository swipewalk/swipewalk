namespace Swipewalk.Collectors;

/// <summary>
/// A large-text check's very first step -- reading the device's current text size, before anything is
/// remembered or changed -- failed. Distinct from a plain <see cref="InvalidOperationException"/> so a
/// caller (the Engine's Recorder) can report accurately that nothing was changed: every other failure
/// inside <see cref="IScreenSource.BeginLargeTextAsync"/> happens after the original size was already
/// read and remembered (see <see cref="TextSizeRestore"/>), so the device may already be partway changed
/// and needs restoring rather than just skipping.
/// </summary>
public sealed class TextSizeReadFailedException(string message) : InvalidOperationException(message);
