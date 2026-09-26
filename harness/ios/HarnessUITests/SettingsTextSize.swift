import XCTest

/// Drives Settings > Accessibility > Display & Text Size > Larger Text on a physical iPhone: reads the
/// current switch/slider state, sets an exact state (AX3, or a state to restore), and always starts from a
/// known root. This is the only file that knows Settings' navigation path -- callers (ScanTests' one-shot
/// test methods and serve-mode commands) never touch Settings elements directly.
///
/// Reliability notes carried over from an earlier spike (iOS 27.0):
/// - Settings (com.apple.Preferences) stays resident across separate `xcodebuild test` invocations, so a
///   plain `.activate()` can inherit whatever page an earlier run left it on. `returnToRoot` always
///   terminates + relaunches, then taps the navigation bar's leftmost button until none remains.
/// - Only narrow element types (.cells/.staticTexts/.switches/.sliders) are queried. A broad `.otherElements`
///   or `.any` query that matches nothing at all was observed to hard-fail the whole test ("Failed to get
///   matching snapshot") instead of just reporting `exists == false`.
/// - The "Larger Accessibility Sizes" switch's accessible frame spans the whole row, so a center tap lands on
///   the label text and never flips it; the tap targets the row's right edge instead.
/// - The slider has no identifier and no increment()/decrement() (that is a stepper-only API); its value is a
///   rounded percentage that can land on a neighboring step, so setting it binary-searches the normalized
///   position passed to `adjust(toNormalizedSliderPosition:)`, reading the step back after each try (see
///   `setSliderStep`).
enum SettingsTextSize {
    static let bundleId = "com.apple.Preferences"

    /// Larger Text step count: 7 standard Dynamic Type sizes (xSmall...xxxLarge) with the switch off, or 12
    /// once "Larger Accessibility Sizes" is on (adds AX1...AX5).
    static func stepCount(toggleOn: Bool) -> Int { toggleOn ? 12 : 7 }

    // MARK: - Public entry points

    /// Returns Settings to the Larger Text screen from a known root: terminate + relaunch, tap the nav bar's
    /// leftmost button until none is left, then navigate Accessibility > Display & Text Size > Larger Text.
    static func openLargerText(_ settings: XCUIApplication) throws {
        returnToRoot(settings)
        try locateAndTap(settings, label: "Accessibility", identifier: "Accessibility")
        try locateAndTap(settings, label: "Display & Text Size", identifier: "Display & Text Size")
        try locateAndTap(settings, label: "Larger Text", identifier: "Larger Text")
    }

    /// Reads the switch + slider state on the currently shown Larger Text screen. Does not change anything.
    static func readState(_ settings: XCUIApplication) throws -> TextSizeState {
        let toggle = settings.switches.firstMatch
        let slider = settings.sliders.firstMatch
        guard toggle.waitForExistence(timeout: 8) else { throw SettingsTextSizeError.elementNotFound("Larger Accessibility Sizes switch") }
        guard slider.waitForExistence(timeout: 8) else { throw SettingsTextSizeError.elementNotFound("Larger Text slider") }
        let toggleOn = (toggle.value as? String) == "1"
        let steps = stepCount(toggleOn: toggleOn)
        guard let fraction = parseSliderPercent((slider.value as? String) ?? "") else {
            throw SettingsTextSizeError.verificationFailed("could not parse slider value '\((slider.value as? String) ?? "")'")
        }
        let index = Int((fraction * CGFloat(steps - 1)).rounded())
        return TextSizeState(toggleOn: toggleOn, sliderIndex: index, steps: steps)
    }

    /// Sets the switch + slider to an exact state on the currently shown Larger Text screen, verifying by
    /// read-back (retrying once on mismatch) and throwing a machine-readable error rather than crashing if
    /// the final state still doesn't match. Kept at two attempts (considered raising to three on 2026-09-26,
    /// alongside the `setSliderStep` rewrite below, but backed out: `setSliderStep`'s own binary search
    /// already retries internally, so a third full pass here wasn't measured to help, and it would push the
    /// worst case for `textsize-restore` close to `IosHarnessSession.CommandTimeout` (90s) once combined with
    /// `locateAndTap`'s own five attempts).
    static func applyState(_ settings: XCUIApplication, _ target: TextSizeState) throws {
        let attempts = 2
        for attempt in 1...attempts {
            try applyOnce(settings, target)
            let after = try readState(settings)
            if after == target {
                return
            }
            if attempt == attempts {
                throw SettingsTextSizeError.verificationFailed("wanted \(target) but Settings reports \(after) after \(attempts) attempts")
            }
        }
    }

    private static func applyOnce(_ settings: XCUIApplication, _ target: TextSizeState) throws {
        let toggle = settings.switches.firstMatch
        let slider = settings.sliders.firstMatch
        guard toggle.waitForExistence(timeout: 8) else { throw SettingsTextSizeError.elementNotFound("Larger Accessibility Sizes switch") }
        guard slider.waitForExistence(timeout: 8) else { throw SettingsTextSizeError.elementNotFound("Larger Text slider") }
        try ensureToggle(toggle, on: target.toggleOn)
        setSliderStep(slider, toIndex: target.sliderIndex, totalSteps: target.steps)
        // Moving the slider can reflow the row and disturb the switch; re-verify and recover once.
        if ((settings.switches.firstMatch.value as? String) == "1") != target.toggleOn {
            try ensureToggle(settings.switches.firstMatch, on: target.toggleOn)
            setSliderStep(settings.sliders.firstMatch, toIndex: target.sliderIndex, totalSteps: target.steps)
        }
    }

    // MARK: - Navigation

    /// Terminate + relaunch (relaunch alone does not reset the nav stack), then tap the nav bar's leftmost
    /// button until none exists, i.e. the stack is at its root. "No back button" is the reliable,
    /// scroll-independent "at the top" signal: no row is guaranteed visible without scrolling first.
    private static func returnToRoot(_ settings: XCUIApplication) {
        settings.terminate()
        settings.launch()
        _ = settings.wait(for: .runningForeground, timeout: 8)
        Thread.sleep(forTimeInterval: 0.6)

        func atTop() -> Bool { !settings.navigationBars.buttons.element(boundBy: 0).exists }
        var backTaps = 0
        while !atTop() && backTaps < 8 {
            let backButton = settings.navigationBars.buttons.element(boundBy: 0)
            guard backButton.exists, backButton.isHittable else { break }
            backButton.tap()
            backTaps += 1
            Thread.sleep(forTimeInterval: 0.4)
        }
    }

    /// Finds a Settings row by identifier first, falling back to a label predicate, scrolling gently to
    /// reveal it if needed, and taps it -- verifying the tap actually navigated (a back button now exists)
    /// before returning.
    private static func locateAndTap(_ app: XCUIApplication, label: String, identifier: String) throws {
        Thread.sleep(forTimeInterval: 0.4)
        func locate() -> XCUIElement {
            var target = app.cells[identifier]
            if !target.exists {
                target = app.cells.containing(NSPredicate(format: "label CONTAINS[c] %@", label)).firstMatch
            }
            if !target.exists {
                target = app.staticTexts.matching(NSPredicate(format: "label == %@", label)).firstMatch
            }
            return target
        }
        let screenHeight = app.frame.height
        func comfortablyPlaced(_ el: XCUIElement) -> Bool {
            guard el.exists, el.isHittable else { return false }
            let midY = el.frame.midY
            return midY > screenHeight * 0.15 && midY < screenHeight * 0.85
        }
        var target = locate()
        var attempts = 0
        var stuckCount = 0
        var lastCellCount = app.cells.count
        while !comfortablyPlaced(target) && attempts < 15 {
            if stuckCount >= 3 {
                app.swipeUp()
                stuckCount = 0
            } else {
                nudgeUp(app)
            }
            attempts += 1
            Thread.sleep(forTimeInterval: 0.3)
            target = locate()
            let cellCountNow = app.cells.count
            stuckCount = cellCountNow == lastCellCount ? stuckCount + 1 : 0
            lastCellCount = cellCountNow
        }
        guard target.exists else { throw SettingsTextSizeError.elementNotFound(label) }

        let backButtonCountBefore = app.navigationBars.buttons.count
        var tapAttempt = 0
        var navigated = false
        // Five attempts (was three until 2026-09-26) with a longer settle wait (1.2s, was 0.8s -- up to
        // about 6s of extra waiting per row in the worst case): found 2026-09-26, on the Simulator only, that
        // this navigation becomes markedly less reliable once Settings itself is rendering at the enlarged
        // accessibility text size -- which this is in whenever it restores from an accessibility size, since
        // applying AX3 makes every app, Settings included, launch at that size from then on. Reproduced with a
        // screenshot showing genuinely enlarged Settings rows (2-line wrapped labels, only ~5 rows fitting on
        // screen) while the tap was repeatedly failing to navigate. One possible cause, not confirmed: a stale
        // accessibility frame from a self-sizing table view cell not yet settled. NOT reproduced on a physical
        // iPhone (also 2026-09-26): several `scan --large-text` and `record` runs there, including the
        // restore-from-AX3 step this comment describes, all navigated normally -- so this extra patience is a
        // low-risk, Simulator-motivated safety margin, not a confirmed fix for a hardware problem.
        while !navigated && tapAttempt < 5 {
            let fresh = locate()
            if fresh.exists { fresh.tap() }
            tapAttempt += 1
            Thread.sleep(forTimeInterval: 1.2)
            // A Settings row's destination screen is usually titled the same as the row itself (tapping
            // "Display & Text Size" opens a screen titled "Display & Text Size"), which turned out to be a
            // more reliable navigation signal on a physical iPhone (iOS 27.0) than the nav-bar button count
            // alone: some destinations have the same number of buttons as their source screen (or fewer),
            // which made a plain `>` comparison miss a real navigation and retry the tap pointlessly.
            navigated = app.navigationBars[label].exists || app.navigationBars.buttons.count > backButtonCountBefore
        }
        guard navigated else { throw SettingsTextSizeError.navigationFailed(label) }
    }

    /// A gentle, short scroll (about 15% of the screen height) instead of `swipeUp()`'s default flick, which
    /// was observed to overshoot past a target row in a single gesture.
    private static func nudgeUp(_ app: XCUIApplication) {
        let start = app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.7))
        let end = app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.55))
        start.press(forDuration: 0.05, thenDragTo: end)
    }

    // MARK: - Switch and slider

    /// Taps the switch until it reports the wanted state (or gives up), verifying after each tap. The
    /// switch's accessible frame spans the whole row, so the tap targets the row's right edge (about a real
    /// UISwitch's width in from the edge) rather than the center, which lands on the label instead.
    private static func ensureToggle(_ toggle: XCUIElement, on: Bool, attempts: Int = 4) throws {
        for _ in 0..<attempts {
            if ((toggle.value as? String) == "1") == on { return }
            toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.92, dy: 0.5)).tap()
            Thread.sleep(forTimeInterval: 0.7)
        }
        guard ((toggle.value as? String) == "1") == on else {
            throw SettingsTextSizeError.verificationFailed("Larger Accessibility Sizes switch would not go \(on ? "on" : "off")")
        }
    }

    /// Moves the slider to an exact discrete index out of `totalSteps` (0-based) by binary-searching the
    /// normalized position `adjust(toNormalizedSliderPosition:)` takes, reading the resulting labeled step back
    /// after each try and narrowing the search range until it lands exactly on the one requested, or giving up
    /// (leaving `applyState`'s own read-back verification to fail loudly) once the attempt budget runs out.
    /// This assumes the reported percentage moves the same direction as the position fed to `adjust` (true for
    /// any slider) -- it does not need the percentage to be evenly spaced by step, since it only narrows toward
    /// whatever position makes the read-back match; confirmed by running it to AX3 repeatedly and seeing the
    /// read-back state come back exactly "on:9/12" each time, matching the target.
    ///
    /// Replaces an earlier version (used until 2026-09-26) that jumped by one precomputed "step width" --
    /// `frame.width / (totalSteps - 1)` -- assuming the 12 labeled sizes sit at even intervals along the
    /// track. Found 2026-09-26 by running the harness against the iOS Simulator's Settings app: a fixed step
    /// width badly overshoots once the five accessibility sizes (AX1...AX5) are involved, since they don't
    /// occupy the same share of the track as the seven standard Dynamic Type sizes -- observed swinging
    /// between two far-apart reported positions, never converging. Binary search over `adjust`'s own
    /// normalized-position input needs no assumption about how steps are spaced along the track, and never
    /// depends on the slider's on-screen frame or touch coordinates at all. Verified on a physical iPhone too,
    /// separately from the Simulator finding above: several `scan --large-text` and `record` runs (including
    /// the `record` "check anyway" + restart flow) all reached AX3 exactly and restored cleanly with this
    /// version, on both BuggyApp (MAUI) and a native SwiftUI sample.
    private static func setSliderStep(_ slider: XCUIElement, toIndex index: Int, totalSteps: Int) {
        var low: CGFloat = 0
        var high: CGFloat = 1
        var fraction = totalSteps > 1 ? CGFloat(index) / CGFloat(totalSteps - 1) : 0
        var attempts = 0
        while attempts < 8 {
            slider.adjust(toNormalizedSliderPosition: fraction)
            Thread.sleep(forTimeInterval: 0.4)
            guard let currentFraction = parseSliderPercent((slider.value as? String) ?? "") else { return }
            let currentIndex = Int((currentFraction * CGFloat(totalSteps - 1)).rounded())
            if currentIndex == index { return }
            if currentIndex < index {
                low = fraction
            } else {
                high = fraction
            }
            fraction = (low + high) / 2
            attempts += 1
        }
    }

    private static func parseSliderPercent(_ value: String) -> CGFloat? {
        let digits = value.filter { $0.isNumber || $0 == "." }
        guard let d = Double(digits), d >= 0 else { return nil }
        return CGFloat(d / 100.0)
    }
}

/// One state of Settings > Accessibility > Display & Text Size > Larger Text: the "Larger Accessibility
/// Sizes" switch and the slider's discrete step index out of the resulting step count. Round-trips through
/// `description`/`parse` as "on:9/12" -- the format written to textsize.json and stored by the host's
/// TextSizeRestore marker (device key = UDID) so an interrupted run can restore the exact original state.
struct TextSizeState: Equatable, CustomStringConvertible {
    var toggleOn: Bool
    var sliderIndex: Int
    var steps: Int

    var description: String { "\(toggleOn ? "on" : "off"):\(sliderIndex)/\(steps)" }

    /// AX3 (accessibilityExtraLarge, about 235%): switch on, index 9 of 0...11. Comfortably past WCAG 1.4.4's
    /// 200% floor and matches the Simulator's `simctl ui content_size accessibility-extra-large`.
    static let ax3 = TextSizeState(toggleOn: true, sliderIndex: 9, steps: 12)

    static func parse(_ s: String) -> TextSizeState? {
        let parts = s.split(separator: ":")
        guard parts.count == 2 else { return nil }
        let toggleOn: Bool
        switch parts[0] {
        case "on": toggleOn = true
        case "off": toggleOn = false
        default: return nil
        }
        let idxSteps = parts[1].split(separator: "/")
        guard idxSteps.count == 2, let index = Int(idxSteps[0]), let steps = Int(idxSteps[1]) else { return nil }
        return TextSizeState(toggleOn: toggleOn, sliderIndex: index, steps: steps)
    }
}

/// Machine-readable errors from driving Settings -- never lets a navigation/verification miss crash the test
/// run; callers catch these and write them into textsize.json's "error" field (or the serve command's error
/// file) instead.
enum SettingsTextSizeError: Error, CustomStringConvertible {
    case elementNotFound(String)
    case navigationFailed(String)
    case verificationFailed(String)

    var description: String {
        switch self {
        case .elementNotFound(let step): return "settings-text-size: element not found: \(step)"
        case .navigationFailed(let step): return "settings-text-size: did not navigate: \(step)"
        case .verificationFailed(let detail): return "settings-text-size: verification failed: \(detail)"
        }
    }
}
