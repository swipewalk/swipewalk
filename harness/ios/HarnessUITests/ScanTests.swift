import XCTest
import UIKit

/// Thin capture harness driven by Swipewalk. Environment (pass to xcodebuild with a TEST_RUNNER_ prefix):
///   CF_BUNDLE_ID   app to inspect
///   CF_OUTPUT      directory: capture target (testCaptureScreen) or command directory (testServe)
///   CF_MODE        "serve" runs testServe; otherwise testCaptureScreen runs
///   CF_RELAUNCH    "1": testCaptureScreen terminates + relaunches the app instead of just activating it
///   CF_LAUNCH_ARG  space-separated launch arguments (implies CF_RELAUNCH); e.g. the per-app large-text
///                  fallback "-UIPreferredContentSizeCategoryName UICTContentSizeCategoryAccessibilityExtraLarge"
///   CF_TEXTSIZE_TARGET  testTextSizeRestore only: the exact state to move Settings to, as
///                       SettingsTextSize.swift's TextSizeState.description ("on:5/7") -- used both to move
///                       to AX3 and to put an earlier state back; there is no separate "set" step
///   CF_ORIENTATION  testSetOrientation only: "portrait" or "landscapeLeft" -- see that test's own comment
/// Settings text-size driving (see SettingsTextSize.swift) is exposed both as one-shot test methods below
/// (testTextSizeRead/Restore, for `scan`) and as serve-mode commands (textsize-read/restore, relaunch, for
/// `record`) in testServe. Callers always read first and remember the original before applying anything (see
/// Swipewalk.Collectors.Ios.IosCollector/IosScreenSource's CapturePhysicalLargeTextAsync): an earlier combined
/// read+apply step (testTextSizeSetAX3/textsize-set) was removed because a failure partway through it could
/// leave the phone changed with no record of the original size to restore.
final class ScanTests: XCTestCase {
    private var env: [String: String] { ProcessInfo.processInfo.environment }

    /// One capture of the foreground screen into CF_OUTPUT: tree.json (snapshot + Apple audit) and screenshot.png.
    /// With CF_ATTACH=1 (physical devices, whose files the Mac can't read), the files are written to the
    /// runner's temporary directory and attached to the test result for the host to export.
    func testCaptureScreen() throws {
        try XCTSkipIf(env["CF_MODE"] == "serve", "serve mode")
        guard let bundleId = env["CF_BUNDLE_ID"] else {
            XCTFail("CF_BUNDLE_ID must be set")
            return
        }
        let attach = env["CF_ATTACH"] == "1"
        let output = attach
            ? URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("swipewalk-capture", isDirectory: true)
            : URL(fileURLWithPath: env["CF_OUTPUT"] ?? NSTemporaryDirectory(), isDirectory: true)
        let (target, launched, broughtForward) = app(bundleId)
        try capture(target, into: output, full: true, launched: launched, broughtForward: broughtForward)
        if attach {
            for name in ["tree.json", "screenshot.png"] {
                let attachment = XCTAttachment(contentsOfFile: output.appendingPathComponent(name))
                attachment.name = name
                attachment.lifetime = .keepAlways
                add(attachment)
            }
        }
    }

    /// One session of the record-mode command protocol: stays running and executes command files written by
    /// the host until it gets "stop" (or times out). Commands are CF_OUTPUT/commands/<n>.json:
    /// {"action": "peek"|"capture"|"stop"|..., "dir": "<path>"}. After each, the harness writes <dir>/done (or
    /// <dir>/error). Holds no logic. By default (manual capture) the host opens one of these sessions per
    /// action -- a capture, an ensure-front check, a large-text step -- and sends "stop" right after, so the
    /// harness is built once (the first run) but relaunched for each action; auto-scan instead keeps one
    /// session open for the whole recording, the original design, so the harness is built and launched only
    /// once (see Swipewalk.Collectors.Ios.IosScreenSource's liveSession parameter and IosHarnessSession).
    func testServe() throws {
        try XCTSkipUnless(env["CF_MODE"] == "serve", "not serve mode")
        guard let bundleId = env["CF_BUNDLE_ID"], let output = env["CF_OUTPUT"] else {
            XCTFail("CF_BUNDLE_ID and CF_OUTPUT must be set")
            return
        }
        // On a physical device the host can only reach this app's own container, so paths may be relative to it.
        let root = Self.resolve(output)
        let commands = root.appendingPathComponent("commands", isDirectory: true)
        let fm = FileManager.default
        try fm.createDirectory(at: commands, withIntermediateDirectories: true)
        let target = XCUIApplication(bundleIdentifier: bundleId)
        try Data().write(to: root.appendingPathComponent("ready"))

        var next = 1
        let deadline = Date().addingTimeInterval(4 * 60 * 60)
        // While this test method runs, iOS shows its own "Automation Running" banner for as long as the
        // XCTest session is live -- that is a system behavior tied to the session, not anything this harness
        // draws (confirmed on a physical iPhone: it isn't visible in a devicectl/XCUIScreen screenshot, but
        // is well documented for on-device XCTest UI testing, e.g. WebDriverAgent's identical overlay, which
        // is itself just an XCTest UI test target). By default this session covers only one action and the
        // host sends "stop" for it right after (see IosScreenSource.RunAsync), so the banner is visible only
        // for that action; with auto-scan, the host keeps this session for the whole recording instead and
        // ends it the same way once recording finishes. Either way, a host that dies without sending "stop"
        // (crash, force-quit, `kill -9`, the desktop app quit mid-action or mid-recording) would otherwise
        // leave this session -- and the banner -- running for the full 4-hour deadline above. staleTimeout is
        // a fallback: a per-action session normally finishes in seconds and an auto-scan session sends a peek
        // roughly every 1.5-2s (see Swipewalk.Engine.Recorder.PollInterval), so going this long with no
        // command at all means the host is gone, not just idle; end the session so iOS clears its automation
        // state on its own.
        let staleTimeout: TimeInterval = 60
        var lastActivity = Date()
        while Date() < deadline {
            let file = commands.appendingPathComponent("\(next).json")
            guard let data = try? Data(contentsOf: file),
                  let command = try? JSONSerialization.jsonObject(with: data) as? [String: String],
                  let action = command["action"] else {
                if Date().timeIntervalSince(lastActivity) > staleTimeout { return }
                Thread.sleep(forTimeInterval: 0.1)
                continue
            }
            next += 1
            lastActivity = Date()
            if action == "stop" { return }
            let dir = Self.resolve(command["dir"] ?? root.path)

            // Text-size and relaunch commands drive Settings or the app under test directly; unlike
            // peek/capture they are allowed to bring the app to front themselves, since the host only sends
            // them as part of its own large-text sequence (never while the user might have switched away on
            // purpose mid-recording).
            if action == "textsize-read" || action == "textsize-restore" {
                do {
                    try fm.createDirectory(at: dir, withIntermediateDirectories: true)
                    let settings = XCUIApplication(bundleIdentifier: SettingsTextSize.bundleId)
                    // Always leave Settings closed and the app under test back in front, whether this step
                    // succeeds or fails: a read/apply failure must not leave the phone showing Settings (and
                    // the app under test not in front) for whatever command comes next.
                    defer {
                        settings.terminate()
                        if target.state != .runningForeground {
                            target.activate()
                            _ = target.wait(for: .runningForeground, timeout: 8)
                        }
                    }
                    try SettingsTextSize.openLargerText(settings)
                    var result: [String: String] = [:]
                    switch action {
                    case "textsize-read":
                        result["state"] = try SettingsTextSize.readState(settings).description
                    default: // textsize-restore: also used to move to AX3 -- see ScanTests.swift's header comment
                        guard let targetState = (command["target"]).flatMap(TextSizeState.parse) else {
                            throw SettingsTextSizeError.verificationFailed("missing/invalid \"target\" for textsize-restore")
                        }
                        try SettingsTextSize.applyState(settings, targetState)
                        result["after"] = try SettingsTextSize.readState(settings).description
                    }
                    let data = try JSONSerialization.data(withJSONObject: result, options: [.sortedKeys])
                    try data.write(to: dir.appendingPathComponent("textsize.json"))
                    try Data().write(to: dir.appendingPathComponent("done"))
                } catch {
                    try? "\(error)".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
                }
                continue
            }
            // Called once, before the recording loop starts (see Swipewalk.Collectors.IScreenSource.EnsureInFrontAsync),
            // so a recording begins from a known point: allowed to bring the app forward (or start it) itself,
            // the same exception peek/capture below don't get, since the host only sends this once, up front --
            // never once the user might have switched away on purpose mid-recording.
            if action == "ensure-front" {
                do {
                    try fm.createDirectory(at: dir, withIntermediateDirectories: true)
                    let alreadyForeground = target.state == .runningForeground
                    let wasNotRunning = target.state == .notRunning
                    if !alreadyForeground {
                        target.activate()
                        guard target.wait(for: .runningForeground, timeout: 10) else {
                            try? "not-in-front".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
                            continue
                        }
                    }
                    let result: [String: Bool] = ["launched": wasNotRunning, "broughtForward": !alreadyForeground && !wasNotRunning]
                    let data = try JSONSerialization.data(withJSONObject: result, options: [.sortedKeys])
                    try data.write(to: dir.appendingPathComponent("launched.json"))
                    try Data().write(to: dir.appendingPathComponent("done"))
                } catch {
                    try? "\(error)".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
                }
                continue
            }
            if action == "relaunch" {
                do {
                    try fm.createDirectory(at: dir, withIntermediateDirectories: true)
                    if let launchArg = command["launchArg"], !launchArg.isEmpty {
                        target.launchArguments = launchArg.split(separator: " ").map(String.init)
                    } else {
                        target.launchArguments = []
                    }
                    target.terminate()
                    target.launch()
                    _ = target.wait(for: .runningForeground, timeout: 10)
                    try Data().write(to: dir.appendingPathComponent("done"))
                } catch {
                    try? "\(error)".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
                }
                continue
            }

            // Never bring the app back or capture another app: the user may have switched away on purpose.
            if target.state != .runningForeground {
                try? fm.createDirectory(at: dir, withIntermediateDirectories: true)
                try? "not-in-front".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
                continue
            }
            do {
                try capture(target, into: dir, full: action == "capture")
                try Data().write(to: dir.appendingPathComponent("done"))
            } catch {
                try? fm.createDirectory(at: dir, withIntermediateDirectories: true)
                try? "\(error)".data(using: .utf8)?.write(to: dir.appendingPathComponent("error"))
            }
        }
    }

    private static func resolve(_ path: String) -> URL {
        path.hasPrefix("/")
            ? URL(fileURLWithPath: path, isDirectory: true)
            : URL(fileURLWithPath: NSHomeDirectory(), isDirectory: true).appendingPathComponent(path, isDirectory: true)
    }

    /// Brings the app under test to front. CF_LAUNCH_ARG sets launch arguments and always terminates +
    /// relaunches (launch arguments only take effect on a fresh launch); CF_RELAUNCH alone terminates +
    /// relaunches with no arguments (MAUI apps were found to apply a system text-size change only at launch,
    /// not on activate()); otherwise a plain activate() if the app isn't already in front -- activate()
    /// launches it if it isn't running at all, the same as tapping its icon.
    ///
    /// Returns whether the app had to be started from not running (`launched`, checked before any of the
    /// above runs anything) and whether it was merely brought forward from the background (`broughtForward`):
    /// the two matter differently to the caller -- a launch shows the app's own first screen, not wherever it
    /// was left, and must be reported (see Swipewalk.Collectors.BringToFront.LaunchedNote); a background app's
    /// state is normally preserved and is just logged.
    private func app(_ bundleId: String) -> (app: XCUIApplication, launched: Bool, broughtForward: Bool) {
        let app = XCUIApplication(bundleIdentifier: bundleId)
        let alreadyForeground = app.state == .runningForeground
        let wasNotRunning = app.state == .notRunning
        if let launchArg = env["CF_LAUNCH_ARG"], !launchArg.isEmpty {
            app.launchArguments = launchArg.split(separator: " ").map(String.init)
            app.terminate()
            app.launch()
            settleAfterForegroundChange()
        } else if env["CF_RELAUNCH"] == "1" {
            app.terminate()
            app.launch()
            settleAfterForegroundChange()
        } else if !alreadyForeground {
            app.activate()
            settleAfterForegroundChange()
        }
        return (app, wasNotRunning, !alreadyForeground && !wasNotRunning)
    }

    /// XCUIApplication reports `.runningForeground` as soon as the app-switch begins, before the transition
    /// animation visually settles. `app.snapshot()` (queried by bundle id) always waits for that specific
    /// app's own hierarchy and is unaffected, but `XCUIScreen.main.screenshot()` in `capture(_:into:full:)`
    /// just grabs whatever is on screen at that instant -- if taken too soon after activate()/launch(), it can
    /// still show the outgoing app's frame while tree.json (from the snapshot) already describes the new app.
    /// Found 2026-09-22: a study's screenshot.png for one app showed the previous app on screen in exactly
    /// this way, alongside a correctly-labelled tree.json for the new app, in the same capture.
    private func settleAfterForegroundChange() {
        Thread.sleep(forTimeInterval: 0.6)
    }

    /// One-shot: reads the current Larger Text state (switch + slider) without changing anything.
    func testTextSizeRead() throws {
        try runTextSizeStep { settings in
            try SettingsTextSize.openLargerText(settings)
            return ["state": try SettingsTextSize.readState(settings).description]
        }
    }

    /// One-shot: moves Larger Text to CF_TEXTSIZE_TARGET exactly -- used both to move to AX3 (the host reads
    /// and remembers the original state first, with testTextSizeRead, before calling this) and to put an
    /// earlier state back.
    func testTextSizeRestore() throws {
        guard let target = env["CF_TEXTSIZE_TARGET"].flatMap(TextSizeState.parse) else {
            XCTFail("CF_TEXTSIZE_TARGET must be set to a valid state (e.g. off:3/7)")
            return
        }
        try runTextSizeStep { settings in
            try SettingsTextSize.openLargerText(settings)
            try SettingsTextSize.applyState(settings, target)
            return ["after": try SettingsTextSize.readState(settings).description]
        }
    }

    /// One-shot: rotates the Simulator (or device) to CF_ORIENTATION ("portrait" or "landscapeLeft") for the
    /// orientation rescan (`scan --orientation both`; see Swipewalk.Collectors.Ios.IosCollector
    /// .SetOrientationAsync, whose remarks explain why this exists -- there is no `simctl` equivalent).
    /// XCUIDevice.shared.orientation is the same API UI-testing teams commonly use to rotate a Simulator; it
    /// has only been verified here against a Simulator (Swipewalk does not attempt this on a physical
    /// iPhone). No output file: success is exit code 0, a crash or a non-zero exit is the failure signal, the
    /// same shape as the relaunch action in testServe below.
    func testSetOrientation() throws {
        guard let target = env["CF_ORIENTATION"] else {
            XCTFail("CF_ORIENTATION must be set to \"portrait\" or \"landscapeLeft\"")
            return
        }
        XCUIDevice.shared.orientation = target == "landscapeLeft" ? .landscapeLeft : .portrait
        // Lets the rotation animate and the app's layout settle before the next capture reads it.
        Thread.sleep(forTimeInterval: 1.0)
    }

    /// Runs a Settings text-size step and writes its result (or a machine-readable error, never a crash) as
    /// JSON to CF_OUTPUT/textsize.json -- attached to the test result with CF_ATTACH=1 (physical devices,
    /// whose files the Mac can't read directly), written straight into CF_OUTPUT otherwise. Settings is left
    /// running; scan brings the app under test back to front itself in the next step (testCaptureScreen).
    private func runTextSizeStep(_ step: (XCUIApplication) throws -> [String: String]) throws {
        let attach = env["CF_ATTACH"] == "1"
        let output = attach
            ? URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("swipewalk-textsize", isDirectory: true)
            : URL(fileURLWithPath: env["CF_OUTPUT"] ?? NSTemporaryDirectory(), isDirectory: true)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        let settings = XCUIApplication(bundleIdentifier: SettingsTextSize.bundleId)
        var result: [String: String]
        do {
            result = try step(settings)
            result["ok"] = "true"
        } catch {
            result = ["ok": "false", "error": "\(error)"]
        }
        let data = try JSONSerialization.data(withJSONObject: result, options: [.sortedKeys])
        try data.write(to: output.appendingPathComponent("textsize.json"))
        if attach {
            let attachment = XCTAttachment(contentsOfFile: output.appendingPathComponent("textsize.json"))
            attachment.name = "textsize.json"
            attachment.lifetime = .keepAlways
            add(attachment)
        }
    }

    /// Writes tree.json; with full, also screenshot.png and Apple audit issues. `launched`/`broughtForward`
    /// (scan mode only, from `app(_:)`) are carried as plain keys in tree.json rather than a separate sidecar,
    /// so they travel with it on a physical device too, where files come back as result-bundle attachments
    /// (see Swipewalk.Collectors.Ios.IosCollector.CaptureAsync, which reads them back).
    private func capture(_ app: XCUIApplication, into outDir: URL, full: Bool, launched: Bool = false, broughtForward: Bool = false) throws {
        try FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)
        let tree = try encode(app.snapshot())
        var scale = 1.0
        var issues: [[String: Any]] = []
        guard full else {
            try write(["scale": scale, "tree": tree, "auditIssues": issues, "launched": launched, "broughtForward": broughtForward], to: outDir)
            return
        }

        let screenshot = XCUIScreen.main.screenshot()
        scale = screenshot.image.scale
        // XCUIScreenshot.pngRepresentation encodes the screen's native (portrait) pixel buffer and
        // ignores image.imageOrientation, so a screenshot taken while the interface is rotated (the
        // orientation rescan, `scan --orientation both`) comes out sideways at the wrong pixel dimensions
        // (confirmed 2026-09: image.size correctly reports the rotated 874x402pt logical size and
        // .imageOrientation .left, but pngRepresentation still writes 1206x2622 raw portrait pixels).
        // UIImage.draw(in:) applies imageOrientation when rendering, so re-rendering through it (only when
        // there's a rotation to apply) bakes the correct orientation into the pixels before encoding --
        // this applies to any capture where the interface happens not to be in its "up" orientation,
        // not only the orientation rescan.
        let orientedImage = screenshot.image.imageOrientation == .up ? screenshot.image : screenshot.image.orientedForEncoding()
        guard let pngData = orientedImage.pngData() else {
            throw NSError(domain: "Harness", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not encode the screenshot as PNG."])
        }
        try pngData.write(to: outDir.appendingPathComponent("screenshot.png"))
        // The status bar is drawn by SpringBoard; its frame lets the host blank it (time, carrier, notifications).
        let statusBar = XCUIApplication(bundleIdentifier: "com.apple.springboard").statusBars.firstMatch
        let statusBarFrame = statusBar.exists ? Self.frame(statusBar.frame) : nil

        try app.performAccessibilityAudit(for: .all) { issue in
            var entry: [String: Any] = [
                "type": Self.name(of: issue.auditType),
                "compactDescription": issue.compactDescription,
                "detailedDescription": issue.detailedDescription,
            ]
            if let element = issue.element, element.exists {
                entry["frame"] = Self.frame(element.frame)
                entry["label"] = element.label
                entry["identifier"] = element.identifier
            }
            issues.append(entry)
            return true // record, don't fail the test
        }

        var document: [String: Any] = ["scale": scale, "tree": tree, "auditIssues": issues, "launched": launched, "broughtForward": broughtForward]
        if let statusBarFrame { document["statusBar"] = statusBarFrame }
        try write(document, to: outDir)
    }

    private func write(_ document: [String: Any], to outDir: URL) throws {
        let data = try JSONSerialization.data(withJSONObject: document, options: [.prettyPrinted, .sortedKeys])
        try data.write(to: outDir.appendingPathComponent("tree.json"))
    }

    private func encode(_ snapshot: XCUIElementSnapshot) throws -> [String: Any] {
        var node: [String: Any] = [
            "type": Self.name(of: snapshot.elementType),
            "identifier": snapshot.identifier,
            "label": snapshot.label,
            "title": snapshot.title,
            "enabled": snapshot.isEnabled,
            "frame": Self.frame(snapshot.frame),
        ]
        if let value = snapshot.value { node["value"] = "\(value)" }
        if let placeholder = snapshot.placeholderValue { node["placeholder"] = placeholder }
        node["children"] = try snapshot.children.map { try encode($0) }
        return node
    }

    private static func frame(_ r: CGRect) -> [Double] {
        [Double(r.origin.x), Double(r.origin.y), Double(r.size.width), Double(r.size.height)]
    }

    private static func name(of type: XCUIElement.ElementType) -> String {
        switch type {
        case .application: return "application"
        case .window: return "window"
        case .button: return "button"
        case .staticText: return "staticText"
        case .image: return "image"
        case .textField: return "textField"
        case .secureTextField: return "secureTextField"
        case .searchField: return "searchField"
        case .textView: return "textView"
        case .switch: return "switch"
        case .toggle: return "toggle"
        case .slider: return "slider"
        case .link: return "link"
        case .cell: return "cell"
        case .navigationBar: return "navigationBar"
        case .tabBar: return "tabBar"
        case .scrollView: return "scrollView"
        case .scrollBar: return "scrollBar"
        case .toolbar: return "toolbar"
        case .table: return "table"
        case .collectionView: return "collectionView"
        case .picker: return "picker"
        case .segmentedControl: return "segmentedControl"
        case .progressIndicator: return "progressIndicator"
        case .activityIndicator: return "activityIndicator"
        case .webView: return "webView"
        case .alert: return "alert"
        case .sheet: return "sheet"
        case .keyboard: return "keyboard"
        case .key: return "key"
        case .menuItem: return "menuItem"
        case .tab: return "tab"
        case .radioButton: return "radioButton"
        case .checkBox: return "checkBox"
        case .stepper: return "stepper"
        case .other: return "other"
        default: return "other:\(type.rawValue)"
        }
    }

    private static func name(of type: XCUIAccessibilityAuditType) -> String {
        switch type {
        case .contrast: return "contrast"
        case .elementDetection: return "elementDetection"
        case .hitRegion: return "hitRegion"
        case .sufficientElementDescription: return "sufficientElementDescription"
        case .dynamicType: return "dynamicType"
        case .textClipped: return "textClipped"
        case .trait: return "trait"
        default: return "other:\(type.rawValue)"
        }
    }
}

extension UIImage {
    /// Re-renders this image with `imageOrientation` baked into the pixel data (`UIImage.draw(in:)` applies
    /// the orientation transform; a raw `pngData()`/`cgImage` does not). Used whenever a captured
    /// screenshot isn't `.up`-oriented -- most often the orientation rescan, but any capture taken while
    /// the interface is rotated -- since `XCUIScreenshot.pngRepresentation` writes the screen's native,
    /// un-rotated buffer regardless; see the call site in `capture(_:into:full:launched:broughtForward:)`.
    func orientedForEncoding() -> UIImage {
        let format = UIGraphicsImageRendererFormat.preferred()
        format.scale = scale
        format.opaque = true
        return UIGraphicsImageRenderer(size: size, format: format).image { _ in
            draw(in: CGRect(origin: .zero, size: size))
        }
    }
}
