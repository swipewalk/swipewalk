namespace Swipewalk.Core.Model;

/// <summary>One predicted screen-reader stop: what is announced when the user swipes to it.</summary>
public sealed record Announcement(int Order, string NodePath, string Text, Bounds Bounds, bool HasName);
