// Walks Xcode's Accessibility Inspector over the macOS Accessibility (AX) API and prints what it reports
// for each element, as JSON on stdout. Run with `xcrun swift InspectorWalk.swift [--max-steps N]
// [--timeout-seconds T]`.
//
// This never drives VoiceOver itself -- VoiceOver is never turned on. It reads the Inspector's own
// Inspection panel (label, value, traits, identifier, hint, class), which the Inspector fills in from the
// same accessibility properties VoiceOver would read, without VoiceOver running. Swipewalk.Collectors.Ios
// labels this "AccessibilityInspector" evidence, distinct from real recorded speech.
//
// Requires: the calling process (or its "responsible" parent -- e.g. the Terminal running the Swipewalk
// CLI, or Swipewalk's desktop app) has been granted Accessibility permission in System Settings > Privacy
// & Security > Accessibility, and a person has already, once per Inspector session: chosen the target
// device in the Inspector's toolbar, and clicked the first element on the app's screen (so this walk
// starts from the top). Neither picking the target nor clicking an element has any AX surface at all --
// the toolbar's device picker exposes no children, value or press action over the AX API, and no menu item
// selects a target -- so this script only walks Next/Previous once that one-time setup is done by hand.
//
// No private API: only the public ApplicationServices (AXUIElement) API and AppKit's NSWorkspace.

import ApplicationServices
import AppKit
import Foundation

// MARK: - AX helpers

func axAttr(_ el: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    let err = AXUIElementCopyAttributeValue(el, name as CFString, &value)
    return err == .success ? value : nil
}
func axString(_ el: AXUIElement, _ name: String) -> String? { axAttr(el, name) as? String }
func axChildren(_ el: AXUIElement) -> [AXUIElement] { (axAttr(el, kAXChildrenAttribute) as? [AXUIElement]) ?? [] }
func axPress(_ el: AXUIElement) -> Bool { AXUIElementPerformAction(el, kAXPressAction as CFString) == .success }

struct FieldRow {
    let id: String?
    let desc: String?
    let help: String?
    let role: String
    let element: AXUIElement
}

func flatFields(_ root: AXUIElement) -> [FieldRow] {
    var rows: [FieldRow] = []
    func walk(_ e: AXUIElement, depth: Int) {
        guard depth < 14 else { return }
        rows.append(FieldRow(
            id: axString(e, kAXIdentifierAttribute), desc: axString(e, kAXDescriptionAttribute),
            help: axString(e, kAXHelpAttribute), role: axString(e, kAXRoleAttribute) ?? "?", element: e))
        for c in axChildren(e) { walk(c, depth: depth + 1) }
    }
    walk(root, depth: 0)
    return rows
}

/// The Traits (and User Input Labels) disclosure collapses every time the selected element changes, so it
/// must be expanded again before every read. Its expand/collapse state lives in AXHelp ("Expand, 1 item" /
/// "Collapse, 1 item"), not AXDescription (which is just the item count).
func ensureExpanded(_ scrollArea: AXUIElement, idPrefix: String) {
    for row in flatFields(scrollArea) where (row.id ?? "").hasPrefix(idPrefix) {
        if let help = row.help, help.hasPrefix("Expand") {
            _ = axPress(row.element)
            Thread.sleep(forTimeInterval: 0.08)
        }
    }
}

struct ElementCapture {
    var label: String
    var value: String
    var traits: [String]
    var identifier: String
    var hint: String
    var className: String
    var address: String // the Inspector's own element-identity field; used to detect a wrapped walk
}

func captureCurrent(scrollArea: AXUIElement) -> ElementCapture {
    ensureExpanded(scrollArea, idPrefix: "AX_TraitsHumanReadable_TEXT")
    var label = "", value = "", identifier = "", hint = "", className = "", address = ""
    var traits: [String] = []
    var inTraits = false
    for row in flatFields(scrollArea) {
        switch row.id {
        case "AX_Label_TEXT": label = row.desc ?? ""
        case "AX_Value_TEXT": value = row.desc ?? ""
        case "AX_Identifier_TEXT": identifier = row.desc ?? ""; inTraits = false
        case "AX_Hint_TEXT": hint = row.desc ?? ""
        case "AX_ElementClassName_TEXT": className = row.desc ?? ""
        case "AX_ElementMemoryAddress_TEXT": address = row.desc ?? ""
        default:
            if let rid = row.id, rid.hasPrefix("AX_TraitsHumanReadable_TEXT") { inTraits = true; continue }
            if inTraits, row.role == "AXUnknown", let d = row.desc, !d.isEmpty { traits.append(d) }
        }
    }
    return ElementCapture(label: label, value: value, traits: traits, identifier: identifier, hint: hint, className: className, address: address)
}

func findMenuItem(_ appEl: AXUIElement, menu menuTitle: String, item itemTitle: String) -> AXUIElement? {
    guard let menuBar = axAttr(appEl, kAXMenuBarAttribute) as! AXUIElement? else { return nil }
    for bar in axChildren(menuBar) where axString(bar, kAXTitleAttribute) == menuTitle {
        for menu in axChildren(bar) {
            for item in axChildren(menu) where axString(item, kAXTitleAttribute) == itemTitle { return item }
        }
    }
    return nil
}

func resolvePath(_ windows: [AXUIElement], _ path: String) -> AXUIElement? {
    let parts = path.split(separator: ".").map(String.init)
    guard let first = parts.first, first.hasPrefix("w"), let windowIndex = Int(first.dropFirst()), windowIndex < windows.count else { return nil }
    var current = windows[windowIndex]
    for part in parts.dropFirst() {
        guard let childIndex = Int(part) else { return nil }
        let kids = axChildren(current)
        guard childIndex < kids.count else { return nil }
        current = kids[childIndex]
    }
    return current
}

/// Physical-device latency is higher and more variable than the Simulator's: a fixed post-press sleep is
/// not reliable. Poll the element-identity address field until it changes and then holds steady for two
/// consecutive reads, instead of guessing a constant.
func waitForSettle(_ scrollArea: AXUIElement, before: String, timeout: TimeInterval = 3.0) {
    let start = Date()
    var seenChange = false
    var last = before
    while Date().timeIntervalSince(start) < timeout {
        Thread.sleep(forTimeInterval: 0.08)
        let current = addressNow(scrollArea)
        if !seenChange {
            if current != before { seenChange = true; last = current }
            continue
        }
        if current == last { return }
        last = current
    }
}
func addressNow(_ scrollArea: AXUIElement) -> String {
    flatFields(scrollArea).first(where: { $0.id == "AX_ElementMemoryAddress_TEXT" })?.desc ?? ""
}

// MARK: - JSON output shape (kept in sync with Swipewalk.Collectors.Ios.InspectorWalkResult/InspectorWalkItem)

struct WalkItemOut: Codable {
    let label: String?
    let value: String?
    let traits: [String]?
    let identifier: String?
    let hint: String?
    let className: String?
}

struct WalkOutput: Codable {
    let ok: Bool
    let error: String?
    let items: [WalkItemOut]
    let complete: Bool
    let notCompleteReason: String?
    let toolVersion: String?
}

func printResult(_ result: WalkOutput) {
    let encoder = JSONEncoder()
    if let data = try? encoder.encode(result), let json = String(data: data, encoding: .utf8) {
        print(json)
    } else {
        print("{\"ok\":false,\"error\":\"could not encode the result as JSON\",\"items\":[],\"complete\":false,\"notCompleteReason\":\"internal error\",\"toolVersion\":null}")
    }
}

func fail(_ message: String) -> Never {
    printResult(WalkOutput(ok: false, error: message, items: [], complete: false, notCompleteReason: message, toolVersion: nil))
    exit(0) // an expected failure (Inspector not running, layout not found, ...) -- not a script crash
}

// MARK: - Argument parsing

var maxSteps = 500
var timeoutSeconds: TimeInterval = 180
var args = CommandLine.arguments.dropFirst()
var iterator = args.makeIterator()
while let arg = iterator.next() {
    switch arg {
    case "--max-steps": if let v = iterator.next(), let n = Int(v) { maxSteps = n }
    case "--timeout-seconds": if let v = iterator.next(), let n = Double(v) { timeoutSeconds = n }
    default: break
    }
}

// MARK: - Setup: find the Inspector, its Inspection menu items, and its panel fields

let apps = NSWorkspace.shared.runningApplications
guard let inspector = apps.first(where: { $0.bundleIdentifier == "com.apple.AccessibilityInspector" }) else {
    fail("Xcode's Accessibility Inspector is not running. Open it (Xcode > Open Developer Tool > Accessibility Inspector), " +
         "choose your device from the target menu, click the first element on the app's screen, then try again.")
}
let appEl = AXUIElementCreateApplication(inspector.processIdentifier)
let windows = (axAttr(appEl, kAXWindowsAttribute) as? [AXUIElement]) ?? []
guard let scrollArea = resolvePath(windows, "w0.0.7"), let _ = resolvePath(windows, "w0.0.2") else {
    fail("Could not find the Accessibility Inspector's inspection panel. Its window layout may not match what this was " +
         "built against; make sure the Inspector's main window is open and an element is selected.")
}
guard let nextItem = findMenuItem(appEl, menu: "Inspection", item: "Move to Next Item"),
      let prevItem = findMenuItem(appEl, menu: "Inspection", item: "Move to Previous Item") else {
    fail("Could not find the Accessibility Inspector's \"Move to Next/Previous Item\" commands. Choose your device " +
         "from the target menu and click the first element on the app's screen first.")
}

let seed = captureCurrent(scrollArea: scrollArea)
if seed.className.isEmpty && seed.label.isEmpty && seed.value.isEmpty {
    fail("No element is selected in the Accessibility Inspector yet. Click the first element on the app's screen, then try again.")
}

// MARK: - Rewind to the start of the screen (Previous until the walk wraps or stops moving)

let overallDeadline = Date().addingTimeInterval(timeoutSeconds)
var seenGoingBack = [seed.address]
var stepsBack = 0
while stepsBack < maxSteps && Date() < overallDeadline {
    let before = addressNow(scrollArea)
    guard axPress(prevItem) else { break }
    waitForSettle(scrollArea, before: before)
    let cap = captureCurrent(scrollArea: scrollArea)
    if seenGoingBack.contains(cap.address) { break } // wrapped back to somewhere already seen
    seenGoingBack.append(cap.address)
    stepsBack += 1
}

// MARK: - Walk forward, capturing each element, until the walk wraps, the timeout is hit, or maxSteps is reached

var items: [WalkItemOut] = []
var seenForward: [String] = []
var complete = false
var notCompleteReason: String? = nil
for _ in 0..<maxSteps {
    if Date() >= overallDeadline {
        notCompleteReason = "the walk timed out before it finished this screen"
        break
    }
    let cap = captureCurrent(scrollArea: scrollArea)
    if seenForward.contains(cap.address) {
        complete = true // wrapped back to the first element: the whole screen was walked
        break
    }
    seenForward.append(cap.address)
    items.append(WalkItemOut(
        label: cap.label.isEmpty ? nil : cap.label,
        value: cap.value.isEmpty ? nil : cap.value,
        traits: cap.traits.isEmpty ? [] : cap.traits, // empty (not nil): "no traits reported" is itself evidence
        identifier: cap.identifier.isEmpty ? nil : cap.identifier,
        hint: cap.hint.isEmpty ? nil : cap.hint,
        className: cap.className.isEmpty ? nil : cap.className))
    let before = addressNow(scrollArea)
    if !axPress(nextItem) {
        notCompleteReason = "the Accessibility Inspector stopped responding to \"Move to Next Item\""
        break
    }
    waitForSettle(scrollArea, before: before)
}
if !complete && notCompleteReason == nil {
    notCompleteReason = "stopped after \(maxSteps) elements without the walk order repeating an already-seen element"
}

let toolVersion = Bundle(url: inspector.bundleURL ?? URL(fileURLWithPath: "/")).flatMap {
    $0.infoDictionary?["CFBundleShortVersionString"] as? String
}
printResult(WalkOutput(ok: true, error: nil, items: items, complete: complete, notCompleteReason: notCompleteReason, toolVersion: toolVersion))
