namespace Swipewalk.Collectors.Ios;

/// <summary>
/// One state of Settings &gt; Accessibility &gt; Display &amp; Text Size &gt; Larger Text on a physical iPhone: the
/// "Larger Accessibility Sizes" switch and the slider's discrete step index out of the resulting step count
/// (7 with the switch off: xSmall...xxxLarge; 12 with it on: xSmall...xxxLarge + AX1...AX5). Mirrors the
/// harness's Swift <c>TextSizeState</c> (harness/ios/HarnessUITests/SettingsTextSize.swift) and its
/// "on:9/12" wire format, which is also what <see cref="TextSizeRestore"/> stores for a physical iPhone
/// (device key = UDID; value = this state's <see cref="ToString"/>).
/// </summary>
public readonly record struct TextSizeState(bool ToggleOn, int SliderIndex, int Steps)
{
    /// <summary>AX3 (accessibilityExtraLarge, about 235%): switch on, index 9 of 0...11.</summary>
    public static readonly TextSizeState Ax3 = new(true, 9, 12);

    public override string ToString() => $"{(ToggleOn ? "on" : "off")}:{SliderIndex}/{Steps}";

    public static TextSizeState? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var parts = value.Split(':');
        if (parts.Length != 2)
            return null;
        bool toggleOn;
        switch (parts[0])
        {
            case "on": toggleOn = true; break;
            case "off": toggleOn = false; break;
            default: return null;
        }
        var idxSteps = parts[1].Split('/');
        if (idxSteps.Length != 2 || !int.TryParse(idxSteps[0], out var index) || !int.TryParse(idxSteps[1], out var steps))
            return null;
        return new TextSizeState(toggleOn, index, steps);
    }
}
