namespace Swipewalk.Core.Coverage;

/// <summary>
/// One criterion's guided-check script: what to record, and numbered steps for each assistive technology a
/// tester might be using. Built from <see cref="CoverageCatalog"/>'s existing "how to check by hand" notes
/// but broken into an actual step sequence a non-expert can follow one at a time. Covers the first 10 criteria
/// only (1.3.1, 1.3.2, 1.3.4, 1.4.1, 2.4.3, 2.5.1, 2.5.7, 3.3.1, 3.3.2, 4.1.3); the remaining ~45 stay
/// <see cref="Coverage.ScreenCriterionStatus.NotTested"/>/manual with only the catalog's one-line note until a
/// later batch adds their steps -- the model/UI here needs no schema change to hold them.
/// </summary>
/// <param name="RecordPrompt">The one-line question the tester is recording an answer to.</param>
/// <param name="TalkBackSteps">Numbered steps for a tester using TalkBack (Android). For a criterion whose
/// check doesn't differ by platform/AT (e.g. 1.3.4, 2.5.1, 2.5.7), the same steps as <see cref="VoiceOverSteps"/>.</param>
/// <param name="VoiceOverSteps">Numbered steps for a tester using VoiceOver (iOS).</param>
/// <param name="HardwareKeyboardSteps">Optional variant for a tester using a hardware keyboard instead of a
/// screen reader (only written for criteria the review specifically calls out for it).</param>
public sealed record GuidedStep(
    string CriterionNumber,
    string RecordPrompt,
    IReadOnlyList<string> TalkBackSteps,
    IReadOnlyList<string> VoiceOverSteps,
    IReadOnlyList<string>? HardwareKeyboardSteps = null);

public static class GuidedStepsCatalog
{
    /// <summary>
    /// Shown once per guided-checks session (desktop page and CLI), before the first criterion card, not
    /// repeated per criterion.
    /// </summary>
    public static IReadOnlyList<string> BeforeYouStart { get; } =
    [
        "Gestures: some steps below ask you to try a gesture (pinch, drag, swipe-path). Try it the way a real " +
        "user would -- a light touch is enough; you don't need to reproduce it perfectly to tell whether an " +
        "alternative exists.",

        "How to turn the screen reader off quickly, since several steps ask you to check something without " +
        "it: VoiceOver -- triple-click the side button (or the Home button on an older iPhone) if that " +
        "shortcut is set up under Settings > Accessibility > Accessibility Shortcut; otherwise Settings > " +
        "Accessibility > VoiceOver. TalkBack -- hold both volume keys for about 3 seconds (the standard " +
        "TalkBack shortcut, if enabled), or Settings > Accessibility > TalkBack.",

        "Use a test account and test data. Don't sign in with a real account or enter real personal information.",

        "Never complete a real payment or a real irreversible action (a real purchase, a real account " +
        "deletion, a real message send) just to test a flow -- stop short of the final confirming tap once " +
        "you've seen what you need, or use a sandbox/test mode if the app has one.",
    ];

    public static IReadOnlyList<GuidedStep> All { get; } =
    [
        new("1.3.1",
            "Does the structure (headings, groups, fields) come through, or only plain text?",
            TalkBackSteps:
            [
                "Turn on TalkBack (Settings > Accessibility > TalkBack).",
                "Touch the first element at the top of the screen, then swipe right through every element.",
                "Listen for \"heading\", \"list, N items\", or a field's label -- not just bare text.",
                "Note anything that reads as plain text but looks (visually) like a heading, group or list.",
                "Turn TalkBack off when done.",
            ],
            VoiceOverSteps:
            [
                "Turn on VoiceOver (Settings > Accessibility > VoiceOver, or triple-click the side button if set up).",
                "Swipe right through every element.",
                "Listen for \"heading\", grouped announcements, or a field's label.",
                "Note anything that visually looks structured but is announced as plain text.",
                "Turn VoiceOver off.",
            ]),

        new("1.3.2",
            "Does the reading order keep the meaning (labels before values, steps in order)?",
            TalkBackSteps:
            [
                "Turn on TalkBack.",
                "Swipe right through the whole screen once, without looking -- just listen.",
                "Compare what you heard, in order, against the screen's visual layout (labels before their " +
                "values, steps in order).",
                "Note the first place the order breaks the meaning, if any.",
                "Turn TalkBack off.",
            ],
            VoiceOverSteps:
            [
                "Turn on VoiceOver.",
                "Swipe right through the whole screen once.",
                "Compare the spoken order against the visual layout.",
                "Note the first place it breaks meaning.",
                "Turn VoiceOver off.",
            ]),

        new("1.3.4",
            "Does the screen work in both portrait and landscape, or is a specific orientation locked without a good reason?",
            TalkBackSteps:
            [
                "Turn off rotation lock (Quick Settings on Android, Control Center on iOS).",
                "Rotate the device to the orientation the app is NOT currently showing.",
                "If the app doesn't rotate with the device at all, or shows a message asking you to rotate " +
                "back to the other orientation: that's a 1.3.4 failure, unless this specific screen has a " +
                "genuine reason a single orientation is essential (e.g. a piano-keyboard app, a check-deposit " +
                "camera screen) -- if you're not sure it's essential, record Fail rather than guess not-applicable.",
                "If it does rotate and nothing is blocked, record Pass for this screen. If some content in the " +
                "new orientation is cut off and can't be reached, write that in your note -- it isn't a 1.3.4 " +
                "failure by itself (it may belong under a different criterion, such as 1.4.10 Reflow).",
                "Rotate back to the original orientation before moving on.",
            ],
            VoiceOverSteps:
            [
                "Turn off rotation lock (Quick Settings on Android, Control Center on iOS).",
                "Rotate the device to the orientation the app is NOT currently showing.",
                "If the app doesn't rotate with the device at all, or shows a message asking you to rotate " +
                "back to the other orientation: that's a 1.3.4 failure, unless this specific screen has a " +
                "genuine reason a single orientation is essential (e.g. a piano-keyboard app, a check-deposit " +
                "camera screen) -- if you're not sure it's essential, record Fail rather than guess not-applicable.",
                "If it does rotate and nothing is blocked, record Pass for this screen. If some content in the " +
                "new orientation is cut off and can't be reached, write that in your note -- it isn't a 1.3.4 " +
                "failure by itself (it may belong under a different criterion, such as 1.4.10 Reflow).",
                "Rotate back to the original orientation before moving on.",
            ]),

        new("1.4.1",
            "Is color ever the only way something is conveyed?",
            TalkBackSteps:
            [
                "Turn on grayscale: Settings > Accessibility > Color and motion > Color correction > " +
                "Grayscale (the exact path varies by device/Android version). Don't use color inversion or " +
                "the other correction modes -- they keep hues distinct from each other, so they won't show " +
                "whether color alone is carrying meaning.",
                "Look at the screen with your eyes (screen reader off) -- find anything that was conveying " +
                "meaning only through color (an error shown only as a red border, a status shown only as a " +
                "colored dot).",
                "Check each one still makes sense without color (is there also an icon, a label, an underline?).",
                "Note anything that doesn't.",
                "Turn grayscale back off.",
            ],
            VoiceOverSteps:
            [
                "Turn on Settings > Accessibility > Display & Text Size > Color Filters > Grayscale.",
                "Look at the screen with your eyes (VoiceOver off) -- find anything conveying meaning only through color.",
                "Check each one still makes sense without color.",
                "Note anything that doesn't.",
                "Turn Color Filters back off.",
            ]),

        new("2.4.3",
            "Does the order preserve meaning and let you complete tasks?",
            TalkBackSteps:
            [
                "Turn on TalkBack.",
                "Swipe right through every focusable element, one at a time, paying attention to whether the " +
                "order still makes sense to follow (not whether it's strictly top-to-bottom).",
                "If the screen has a dialog, menu or popover, open it and check focus moves into it, and " +
                "check it moves back to where you were (not to the top of the screen, not lost) when you close it.",
                "Note any jump that changes the meaning of what you're hearing as a 2.4.3 problem. If focus " +
                "gets stuck somewhere and you can't move away with TalkBack, note that separately as a " +
                "possible 2.1.2 No Keyboard Trap issue instead.",
                "Turn TalkBack off.",
            ],
            VoiceOverSteps:
            [
                "Turn on VoiceOver.",
                "Swipe right through every element, checking the order makes sense rather than checking it's strictly top-to-bottom.",
                "Open any dialog/popover and check focus lands inside it, then returns to where you were on close.",
                "Note any meaning-breaking jump as a 2.4.3 problem. If focus gets stuck and you can't move " +
                "away, note that separately as a possible 2.1.2 No Keyboard Trap issue instead.",
                "Turn VoiceOver off.",
            ],
            HardwareKeyboardSteps:
            [
                "Connect a hardware keyboard (on iOS, also turn on Settings > Accessibility > Keyboards > Full Keyboard Access).",
                "Press Tab repeatedly through every focusable element instead of swiping, paying attention to " +
                "whether the order still makes sense to follow (not whether it's strictly top-to-bottom).",
                "If the screen has a dialog, menu or popover, open it and check focus moves into it with Tab, " +
                "and back to where you were (not lost) when you close it.",
                "Note any jump that changes the meaning of what you're hearing as a 2.4.3 problem, and any " +
                "place Tab gets stuck as a possible 2.1.2 No Keyboard Trap issue instead -- a keyboard-only " +
                "user can hit different focus-order problems than a screen-reader user on the same screen, so " +
                "note any difference from what TalkBack/VoiceOver showed too.",
            ]),

        new("2.5.1",
            "Does every multi-point or path-based gesture have a one-finger alternative?",
            TalkBackSteps:
            [
                "With the screen reader OFF, try any pinch, two-finger, or swipe-path gesture the screen " +
                "supports (pinch-to-zoom on a map, a swipe-to-dismiss card, a drawing/signature area).",
                "For each one, try a plain single-finger tap, double-tap, or long-press in the same area and " +
                "see whether it achieves the same result (or look for a separate button/menu item that does).",
                "If no single-finger alternative exists, ask: is the gesture itself essential to the task " +
                "(freehand drawing, a signature, a piano-style multi-touch instrument)? If so, this is the " +
                "WCAG-recognized exception -- record Pass with that reasoning as your evidence, not Fail.",
                "If there's no alternative and the gesture isn't essential, record Fail.",
                "This check is about the app's touch design, not what a screen reader announces -- test with " +
                "the screen reader off either way.",
            ],
            VoiceOverSteps:
            [
                "With the screen reader OFF, try any pinch, two-finger, or swipe-path gesture the screen " +
                "supports (pinch-to-zoom on a map, a swipe-to-dismiss card, a drawing/signature area).",
                "For each one, try a plain single-finger tap, double-tap, or long-press in the same area and " +
                "see whether it achieves the same result (or look for a separate button/menu item that does).",
                "If no single-finger alternative exists, ask: is the gesture itself essential to the task " +
                "(freehand drawing, a signature, a piano-style multi-touch instrument)? If so, this is the " +
                "WCAG-recognized exception -- record Pass with that reasoning as your evidence, not Fail.",
                "If there's no alternative and the gesture isn't essential, record Fail.",
                "This check is about the app's touch design, not what a screen reader announces -- test with " +
                "the screen reader off either way.",
            ]),

        new("2.5.7",
            "Does every drag interaction have a non-drag alternative that works WITHOUT turning a screen reader on?",
            TalkBackSteps:
            [
                "With the screen reader off, find anything you can drag (reorder a list, a custom slider, swipe-to-delete).",
                "For each, look for a non-drag way to do the same thing that an ordinary sighted, " +
                "non-screen-reader user could find and use -- a visible \"Move up/down\" menu action, a " +
                "Delete button revealed by a long-press, direct value entry for a slider. A workaround that " +
                "only exists as a TalkBack/VoiceOver-specific gesture does NOT satisfy this criterion -- 2.5.7 " +
                "needs an alternative any user has, not one gated behind turning on assistive technology. The " +
                "alternative must also not itself be a swipe or other path-based gesture (that would just move " +
                "the problem to 2.5.1 Pointer Gestures).",
                "For a standard (not custom-drawn) slider specifically: if it supports tapping directly on " +
                "the track to jump to a value, or has visible +/- buttons, that's a sufficient alternative -- " +
                "record Pass. If it only responds to dragging the thumb, with no tap-on-track and no +/- " +
                "buttons, don't fail it outright -- record it as Inconclusive and note \"standard slider, " +
                "drag-only, needs review\", so a person with more platform-slider expertise looks at it specifically.",
                "If there's no adequate alternative, ask: is dragging itself essential to the task (e.g. a " +
                "freehand drawing/sketching tool), or is it a control the platform itself provides and the app " +
                "hasn't modified (e.g. an unmodified system slider)? If so, record Pass with that reasoning as " +
                "your evidence.",
                "Otherwise, note any drag interaction with no adequate alternative as Fail.",
            ],
            VoiceOverSteps:
            [
                "With the screen reader off, find anything you can drag (reorder a list, a custom slider, swipe-to-delete).",
                "For each, look for a non-drag way to do the same thing that an ordinary sighted, " +
                "non-screen-reader user could find and use -- a visible \"Move up/down\" menu action, a " +
                "Delete button revealed by a long-press, direct value entry for a slider. A workaround that " +
                "only exists as a TalkBack/VoiceOver-specific gesture does NOT satisfy this criterion -- 2.5.7 " +
                "needs an alternative any user has, not one gated behind turning on assistive technology. The " +
                "alternative must also not itself be a swipe or other path-based gesture (that would just move " +
                "the problem to 2.5.1 Pointer Gestures).",
                "For a standard (not custom-drawn) slider specifically: if it supports tapping directly on " +
                "the track to jump to a value, or has visible +/- buttons, that's a sufficient alternative -- " +
                "record Pass. If it only responds to dragging the thumb, with no tap-on-track and no +/- " +
                "buttons, don't fail it outright -- record it as Inconclusive and note \"standard slider, " +
                "drag-only, needs review\", so a person with more platform-slider expertise looks at it specifically.",
                "If there's no adequate alternative, ask: is dragging itself essential to the task (e.g. a " +
                "freehand drawing/sketching tool), or is it a control the platform itself provides and the app " +
                "hasn't modified (e.g. an unmodified system slider)? If so, record Pass with that reasoning as " +
                "your evidence.",
                "Otherwise, note any drag interaction with no adequate alternative as Fail.",
            ]),

        new("3.3.1",
            "Is the error described in visible text, and can the screen reader read that text if you navigate to it?",
            TalkBackSteps:
            [
                "Turn on TalkBack.",
                "Find a form field and submit it empty or with an invalid value -- if this screen never shows " +
                "or flags an error at all (nothing to submit, no validation exists here), this criterion does " +
                "not apply to this screen; record \"confirm not applicable\" with that reason.",
                "Look at the screen: is the error described in visible text (not just a red border or a beep), " +
                "and does that text say WHICH field is wrong (not only that something, somewhere, is)?",
                "Move TalkBack's focus to that error text directly (swipe to it) and check TalkBack reads it out loud when you land on it.",
                "Note separately: (a) whether visible text describes the error and identifies the field, and " +
                "(b) whether TalkBack can reach and read it -- these are two different things; whether it's " +
                "announced automatically without you navigating to it is a separate, 4.1.3 Status Messages " +
                "question, not this one.",
            ],
            VoiceOverSteps:
            [
                "Turn on VoiceOver.",
                "Trigger a validation error the same way -- if this screen never shows or flags an error at " +
                "all (nothing to submit, no validation exists here), this criterion does not apply to this " +
                "screen; record \"confirm not applicable\" with that reason.",
                "Look at the screen: is the error described in visible text, and does it say WHICH field is wrong?",
                "Move VoiceOver focus to that text and check it reads out loud.",
                "Note the same two things separately (visible text identifies the error and the field; " +
                "reachable by VoiceOver) -- automatic announcement is 4.1.3, not this criterion.",
            ]),

        new("3.3.2",
            "Does every field have a visible, persistent label or instructions -- not just a hint that disappears?",
            TalkBackSteps:
            [
                "Turn on TalkBack (used here only to help you find each field in order, not for what it " +
                "announces -- that's 4.1.2).",
                "For each field, look BEFORE typing: is there a label or instructions visible outside the " +
                "field itself (above/beside it), not only placeholder text sitting inside the field?",
                "Start typing a character and check: does the label/instructions stay visible, or was it only " +
                "placeholder text that just disappeared? A label that disappears once you start typing is " +
                "widely treated as not enough on its own -- record Fail or Inconclusive and describe it in your note.",
                "For a field that's required, or expects an unusual or specific format (e.g. \"MM/DD/YYYY\", " +
                "\"10-digit number\"), check that's stated somewhere visible -- not only implied by a " +
                "validation error you'd see after getting it wrong. An ordinary free-text field with no " +
                "particular format doesn't need this.",
                "Note any field that fails either check above.",
            ],
            VoiceOverSteps:
            [
                "Turn on VoiceOver (used here only to help you find each field in order, not for what it " +
                "announces -- the criterion itself is about what's visible, not what's spoken).",
                "For each field, look BEFORE typing: is there a label or instructions visible outside the " +
                "field itself (above/beside it), not only placeholder text sitting inside the field?",
                "Start typing a character and check: does the label/instructions stay visible, or was it only " +
                "placeholder text that just disappeared? A label that disappears once you start typing is " +
                "widely treated as not enough on its own -- record Fail or Inconclusive and describe it in your note.",
                "For a field that's required, or expects an unusual or specific format, check that's stated " +
                "somewhere visible -- not only implied by a validation error you'd see after getting it wrong. " +
                "An ordinary free-text field with no particular format doesn't need this.",
                "Note any field that fails either check above.",
            ]),

        new("4.1.3",
            "Are status changes that don't move focus still announced?",
            TalkBackSteps:
            [
                "If this screen never shows any status message (a \"Saved\" confirmation, a loading spinner " +
                "finishing, a validation summary appearing) at all, this criterion does not apply to this " +
                "screen; record \"confirm not applicable\" with that reason.",
                "Turn on TalkBack.",
                "Trigger a status change.",
                "First check whether the message takes keyboard/screen-reader focus itself -- if it does " +
                "(TalkBack's focus visibly jumps to it), leave this particular message out of your check: a " +
                "message that takes focus is expected to be heard simply because focus moved there, so it " +
                "isn't the kind of gap 4.1.3 is about.",
                "For a message that does NOT take focus: without touching anything else, listen -- does " +
                "TalkBack announce it on its own anyway? Note whether it was announced, shown only visually " +
                "with no announcement, or neither. If EVERY status message on this screen takes focus (so none " +
                "was left to check this way), record \"confirm not applicable\" with that reason instead.",
            ],
            VoiceOverSteps:
            [
                "If this screen never shows any status message at all, this criterion does not apply to this " +
                "screen; record \"confirm not applicable\" with that reason.",
                "Turn on VoiceOver.",
                "Trigger a status change.",
                "Check whether VoiceOver focus moves to the message -- if so, leave this particular message " +
                "out of your check (see the TalkBack steps' reasoning).",
                "For a message that doesn't take focus, listen for an automatic announcement, and note the " +
                "result. If every status message on this screen takes focus, record \"confirm not applicable\" instead.",
            ]),
    ];

    public static GuidedStep? For(string criterionNumber) => All.FirstOrDefault(s => s.CriterionNumber == criterionNumber);
}
