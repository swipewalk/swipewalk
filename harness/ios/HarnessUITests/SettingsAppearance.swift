import XCTest

/// Drives Settings > Appearance on a physical iPhone: reads whether the device currently follows Light,
/// Dark, or Automatic (day/night scheduling), and switches explicitly to Light or Dark. This is the only
/// file that knows Settings' Appearance navigation path -- callers (ScanTests' one-shot test methods) never
/// touch its elements directly, mirroring SettingsTextSize.swift for Larger Text.
///
/// "Appearance" is its own top-level Settings row (identifier `com.apple.settings.appearance`) on iOS 27,
/// separate from Display & Brightness -- confirmed on a physical iPhone (iOS 27.0, 2026-09-26) by dumping the
/// Settings root's accessibility tree; an earlier version of this file assumed the Light/Dark picker lived
/// under Display & Brightness (true on older iOS releases) and failed with "element not found" there. On the
/// Appearance screen the Light/Dark picker is two buttons whose accessible label includes the screen's own
/// mock preview time, e.g. "9:41, Light" / "9:41, Dark" (not the plain "Light"/"Dark" text visible under
/// each), so they're matched by a CONTAINS predicate rather than an exact label; the selected one carries the
/// standard "Selected" trait, read via `isSelected`. Re-check this file whenever an iOS release moves things
/// again, the same maintenance note SettingsTextSize.swift carries for Larger Text.
///
/// Automatic is never applied by this file, only read and restored: the appearance rescan (see
/// Swipewalk.Engine.ScanService's iOS appearance branch) needs a fixed Light or Dark starting point to know
/// what "the other appearance" means, so a device found on Automatic is left untouched and the check is
/// skipped with a reason instead. Restoring an explicit Light/Dark choice back to Automatic only means
/// turning the Automatic switch on again -- iOS keeps its schedule configuration while Automatic is off, so
/// this does not need to remember or reapply that schedule itself.
enum SettingsAppearance {
    static let bundleId = "com.apple.Preferences"

    // MARK: - Public entry points

    /// Returns Settings to the Appearance screen from a known root: terminate + relaunch, tap the nav bar's
    /// leftmost button until none is left, then navigate to Appearance -- the same returnToRoot/locateAndTap
    /// shape as SettingsTextSize.openLargerText.
    static func openAppearance(_ settings: XCUIApplication) throws {
        returnToRoot(settings)
        try locateAndTap(settings, label: "Appearance", identifier: "com.apple.settings.appearance")
    }

    /// Reads which appearance is currently active on the shown Appearance screen, without changing anything:
    /// "automatic" when the Automatic switch is on (Light/Dark still show one of them selected underneath,
    /// but that selection isn't meaningful while Automatic decides it), otherwise "light" or "dark" from
    /// whichever of the two picker buttons carries the "Selected" trait.
    static func readState(_ settings: XCUIApplication) throws -> String {
        let light = lightButton(settings)
        let dark = darkButton(settings)
        guard light.waitForExistence(timeout: 8) else { throw SettingsAppearanceError.elementNotFound("Light button") }
        guard dark.waitForExistence(timeout: 8) else { throw SettingsAppearanceError.elementNotFound("Dark button") }
        let automatic = automaticSwitch(settings)
        if automatic.exists, (automatic.value as? String) == "1" {
            return "automatic"
        }
        if dark.isSelected { return "dark" }
        return "light"
    }

    /// Switches to an explicit "light" or "dark" appearance (turning Automatic off if it was on), verifying
    /// by read-back (retrying once on mismatch) the same way SettingsTextSize.applyState does.
    static func applyAppearance(_ settings: XCUIApplication, _ appearance: String) throws {
        try applyOnce(settings, appearance)
        if try readState(settings) == appearance {
            return
        }
        try applyOnce(settings, appearance)
        let after = try readState(settings)
        guard after == appearance else {
            throw SettingsAppearanceError.verificationFailed("wanted \(appearance) but Settings reports \(after) after two attempts")
        }
    }

    private static func applyOnce(_ settings: XCUIApplication, _ appearance: String) throws {
        let button = appearance == "dark" ? darkButton(settings) : lightButton(settings)
        guard button.waitForExistence(timeout: 8) else { throw SettingsAppearanceError.elementNotFound("\(appearance) button") }
        if !button.isSelected {
            button.tap()
            Thread.sleep(forTimeInterval: 0.6)
        }
    }

    private static func lightButton(_ settings: XCUIApplication) -> XCUIElement {
        settings.buttons.matching(NSPredicate(format: "label CONTAINS[c] %@", "Light")).firstMatch
    }

    private static func darkButton(_ settings: XCUIApplication) -> XCUIElement {
        settings.buttons.matching(NSPredicate(format: "label CONTAINS[c] %@", "Dark")).firstMatch
    }

    private static func automaticSwitch(_ settings: XCUIApplication) -> XCUIElement {
        settings.switches.matching(NSPredicate(format: "label == %@", "Automatic")).firstMatch
    }

    // MARK: - Navigation (duplicated from SettingsTextSize.swift on purpose: each Settings driver stays
    // self-contained, so a future fix to one screen's navigation can't disturb the other -- the thin-harness
    // convention this project follows for driving system Settings screens).

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

    private static func locateAndTap(_ app: XCUIApplication, label: String, identifier: String) throws {
        Thread.sleep(forTimeInterval: 0.4)
        func locate() -> XCUIElement {
            // The redesigned Settings root list (iOS 26+) uses Button rows with a real accessibility
            // identifier (e.g. "com.apple.settings.appearance"); older screens use Cell rows instead --
            // try both, then fall back to the row's own label text (a cell's or button's child).
            var target = app.buttons[identifier]
            if !target.exists {
                target = app.cells[identifier]
            }
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
        guard target.exists else { throw SettingsAppearanceError.elementNotFound(label) }

        let backButtonCountBefore = app.navigationBars.buttons.count
        var tapAttempt = 0
        var navigated = false
        while !navigated && tapAttempt < 3 {
            let fresh = locate()
            if fresh.exists { fresh.tap() }
            tapAttempt += 1
            Thread.sleep(forTimeInterval: 0.8)
            navigated = app.navigationBars[label].exists || app.navigationBars.buttons.count > backButtonCountBefore
        }
        guard navigated else { throw SettingsAppearanceError.navigationFailed(label) }
    }

    private static func nudgeUp(_ app: XCUIApplication) {
        let start = app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.7))
        let end = app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.55))
        start.press(forDuration: 0.05, thenDragTo: end)
    }
}

/// Machine-readable errors from driving Settings -- never lets a navigation/verification miss crash the test
/// run; callers catch these and write them into appearance.json's "error" field instead.
enum SettingsAppearanceError: Error, CustomStringConvertible {
    case elementNotFound(String)
    case navigationFailed(String)
    case verificationFailed(String)

    var description: String {
        switch self {
        case .elementNotFound(let step): return "settings-appearance: element not found: \(step)"
        case .navigationFailed(let step): return "settings-appearance: did not navigate: \(step)"
        case .verificationFailed(let detail): return "settings-appearance: verification failed: \(detail)"
        }
    }
}
