import XCTest

/// Drives the Swipewalk desktop app like a user (and like assistive technology: elements are found by
/// their accessible names). Each test starts the app with an empty, temporary run history.
final class DesktopTests: XCTestCase {
    override func setUp() {
        continueAfterFailure = false
    }

    /// Picks a non-physical device (an emulator or the iOS Simulator) in the Device picker and waits for the
    /// choice to register. New scan no longer preselects one (NewScanPage.OnPlatformChanged), so tests that
    /// only need *some* device chosen -- not a specific one, like the app-picker tests below -- use this
    /// instead of relying on a default. Deliberately not just "the first item": that could be a physical
    /// phone, which would pop the physical-phone notice (Services/PhysicalDeviceNotice) and block whatever the test
    /// does next, which has nothing to do with that notice.
    private func selectAnyDevice(_ devicePicker: XCUIElement, in app: XCUIApplication) throws {
        devicePicker.click()
        // Excludes "choose_a_device" too: while nothing is chosen, the menu also has that inert leading entry
        // (SelectButtonHandler.Rebuild's placeholder item, present so the button's own displayed value is
        // never blank) which would otherwise match "not physical" and do nothing when clicked.
        let nonPhysical = app.menuItems.matching(
            NSPredicate(format: "NOT (identifier CONTAINS %@) AND NOT (identifier CONTAINS %@)", "physical_device", "choose_a_device")
        ).firstMatch
        guard nonPhysical.waitForExistence(timeout: 10) else { throw XCTSkip("No emulator or Simulator connected") }
        nonPhysical.click()
        let chosen = NSPredicate(format: "value != %@ AND value != %@", "Choose a device", "No device found (see Devices)")
        expectation(for: chosen, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 10)
    }

    func testSidebarReachesEveryPage() {
        let app = Desktop.launch()
        let pages: [(String, String)] = [
            ("New scan", "An installed app, identified by its Android package or iOS bundle id:"),
            ("Devices", "Android phones and emulators (over adb), iOS Simulators and iPhones connected to this Mac."),
            ("History", "Runs are saved in"),
            ("Dashboard", "Automated checks find only some accessibility issues"),
        ]
        for (item, expected) in pages {
            app.buttons[item].firstMatch.click()
            XCTAssertTrue(app.staticTexts.containing(expected).waitForExistence(timeout: 10), "\(item) page did not show '\(expected)'")
        }
    }

    /// Every page is reachable from the keyboard (WCAG 2.1.1): Go menu shortcuts.
    func testKeyboardShortcutsReachEveryPage() {
        let app = Desktop.launch()
        let pages: [(String, String)] = [
            ("2", "An installed app, identified by its Android package or iOS bundle id:"),
            ("3", "Android phones and emulators (over adb)"),
            ("4", "Runs are saved in"),
            ("1", "Automated checks find only some accessibility issues"),
        ]
        for (key, expected) in pages {
            app.typeKey(key, modifierFlags: .command)
            XCTAssertTrue(app.staticTexts.containing(expected).waitForExistence(timeout: 10), "⌘\(key) did not open the page showing '\(expected)'")
        }
    }

    /// Text can be enlarged to 200% within the app (WCAG 1.4.4): View > Bigger Text (⌘=) four times.
    func testTextCanBeEnlargedTo200Percent() {
        let app = Desktop.launch(page: "scan")
        let label = app.staticTexts["Law or standard"].firstMatch
        XCTAssertTrue(label.waitForExistence(timeout: 10))
        let before = label.frame.height
        for _ in 0..<4 { app.typeKey("=", modifierFlags: .command) }
        let enlarged = NSPredicate { _, _ in label.frame.height >= before * 1.8 }
        expectation(for: enlarged, evaluatedWith: label)
        waitForExpectations(timeout: 10)
        app.typeKey("0", modifierFlags: .command)
    }

    /// Dashboard and Compare page on a history with two runs of the sample app (seeded by the test script).
    func testDashboardComparesWithThePreviousRun() throws {
        guard let seeded = ProcessInfo.processInfo.environment["CF_SEEDED_HISTORY"], !seeded.isEmpty else {
            throw XCTSkip("Run through scripts/desktop-uitests.sh, which seeds the history")
        }
        let app = Desktop.launch(page: "dashboard", history: URL(fileURLWithPath: seeded))
        XCTAssertTrue(app.staticTexts.containing("Since the previous run: 1 new, 1 no longer found").waitForExistence(timeout: 10))
        // The chart is one element whose name carries the numbers (not only bar heights).
        XCTAssertTrue(app.descendants(matching: .any).containing("Chart of WCAG issues in the last 2 run(s)").exists,
                      "The trend chart has no text alternative")

        app.buttons.containing("Compare with previous run").click()
        XCTAssertTrue(app.staticTexts["Changes since the previous run"].waitForExistence(timeout: 10))
        for section in ["New (1)", "No longer found (1)", "Not checked again (1)"] {
            XCTAssertTrue(app.staticTexts[section].exists, "Compare page is missing '\(section)'")
        }
        XCTAssertTrue(app.staticTexts.containing("img_email_receipt").exists, "The label that was fixed is not listed")
    }

    /// A History row must have its own accessible name (app, platform, mode, date and counts), not just the
    /// non-accessible app-name Label nested inside it -- the same gap fixed earlier for the app picker's rows,
    /// which have no other interactive control to preserve. History rows also contain a Delete button, so the row's
    /// name comes from grouping only the informational Labels (RunAccessibleNameConverter, HistoryPage.xaml);
    /// Delete must stay its own separately reachable, named control outside that group.
    func testHistoryRowsHaveAccessibleNamesAndDeleteStaysReachable() throws {
        guard let seeded = ProcessInfo.processInfo.environment["CF_SEEDED_HISTORY"], !seeded.isEmpty else {
            throw XCTSkip("Run through scripts/desktop-uitests.sh, which seeds the history")
        }
        let app = Desktop.launch(page: "history", history: URL(fileURLWithPath: seeded))

        // The row's grouped summary names the app, platform, mode, date and counts -- not just "org.swipewalk.buggyapp".
        let row = app.descendants(matching: .any).containing("org.swipewalk.buggyapp, ")
        XCTAssertTrue(row.waitForExistence(timeout: 10), "History row has no accessible summary name")
        XCTAssertTrue(row.label.contains("Android"), "Row name should include the platform: \(row.label)")
        XCTAssertTrue(row.label.contains("WCAG issues"), "Row name should include the WCAG issue count: \(row.label)")

        // Delete stays its own separately reachable, named control for each seeded run -- not hidden by the
        // row's Description (never put on a container: on Apple platforms it makes the container one element
        // and hides its children from screen readers).
        let deleteButtons = app.buttons.matching(
            NSPredicate(format: "label BEGINSWITH %@", "Delete the run of org.swipewalk.buggyapp from "))
        XCTAssertEqual(deleteButtons.count, 2, "Both seeded runs should each have their own reachable, named Delete button")
    }

    /// Continue (RunRecord.CanContinue) is shown only for a recording that ended early --
    /// never for a scan, and never for a record that finished normally. The script seeds two extra runs of a
    /// fictional app ("org.swipewalk.recordtest") for this: one "record" run with an endedEarlyReason and one
    /// without, alongside the two "scan" runs seeded above.
    func testContinueButtonShownOnlyForEndedEarlyRecordingRuns() throws {
        guard let seeded = ProcessInfo.processInfo.environment["CF_SEEDED_HISTORY"], !seeded.isEmpty else {
            throw XCTSkip("Run through scripts/desktop-uitests.sh, which seeds the history")
        }
        let app = Desktop.launch(page: "history", history: URL(fileURLWithPath: seeded))
        // Not app.staticTexts: the row's App/date/platform/etc. Labels are grouped under one Description (see
        // RunAccessibleNameConverter/HistoryPage.xaml), which removes their own StaticText elements from the
        // accessibility tree in favor of that single combined name -- same as testHistoryRowsHave... below.
        let row = app.descendants(matching: .any).containing("org.swipewalk.recordtest, ")
        XCTAssertTrue(row.waitForExistence(timeout: 10), "History row for org.swipewalk.recordtest has no accessible summary name")

        // Exactly one Continue button in the whole list: the ended-early "record" run of org.swipewalk.recordtest.
        // The other three seeded runs (two scans of org.swipewalk.buggyapp, one normally-finished "record" of
        // org.swipewalk.recordtest) must not show it.
        let continueButtons = app.buttons.matching(
            NSPredicate(format: "label BEGINSWITH %@", "Continue the run of org.swipewalk.recordtest from "))
        XCTAssertEqual(continueButtons.count, 1, "Only the ended-early recording should have a reachable Continue button")
        XCTAssertTrue(continueButtons.firstMatch.label.hasSuffix("which ended early"),
                      "Continue button's name should say the run ended early: \(continueButtons.firstMatch.label)")
    }

    /// The Guided checks page (walks a run's screens with step-by-step WCAG questions), reached from a History
    /// row's own "Guided checks" button, on the seeded org.swipewalk.buggyapp scan (real findings citing 1.1.1,
    /// none citing 1.3.1 or 3.3.1 -- see the fixture's own results.json). Covers: the page opens with reachable,
    /// named controls; a Pass with evidence saves and shows the exact tester-facing wording; a confirmed
    /// not-applicable with a reason saves; a Pass that disagrees with a real automated finding shows the
    /// contradiction banner immediately; and answers persist (reopening the page keeps what was recorded).
    func testGuidedChecksPageRecordsAnswersAndShowsAContradiction() throws {
        guard let seeded = ProcessInfo.processInfo.environment["CF_SEEDED_HISTORY"], !seeded.isEmpty else {
            throw XCTSkip("Run through scripts/desktop-uitests.sh, which seeds the history")
        }
        let app = Desktop.launch(page: "history", history: URL(fileURLWithPath: seeded))
        let guidedButton = app.buttons.containing("Guided checks for the run of org.swipewalk.buggyapp")
        XCTAssertTrue(guidedButton.waitForExistence(timeout: 10), "History row has no reachable Guided checks button")
        guidedButton.click()
        XCTAssertTrue(app.staticTexts.containing("Guided checks").waitForExistence(timeout: 15), "Guided checks page did not open")

        // The screen picker is reachable and named, like the other choice controls (SelectButton).
        let screenPicker = app.buttons["Screen"].firstMatch
        XCTAssertTrue(screenPicker.waitForExistence(timeout: 15), "Screen picker is not a named button")
        XCTAssertFalse((screenPicker.value as? String ?? "").isEmpty, "Screen picker does not expose its selected value")

        func recordPass(_ criterion: String, evidence: String) {
            let resultPicker = app.buttons["Result for \(criterion)"].firstMatch
            XCTAssertTrue(resultPicker.waitForExistence(timeout: 15), "No result picker for \(criterion)")
            resultPicker.click()
            app.menuItems["Pass"].firstMatch.click()
            let evidenceField = app.textFields["Evidence for \(criterion), required for pass: what you saw or heard"].firstMatch
            XCTAssertTrue(evidenceField.waitForExistence(timeout: 10), "No evidence field for \(criterion)")
            evidenceField.click()
            evidenceField.typeText(evidence)
            let save = app.buttons["Save the answer for \(criterion)"].firstMatch
            XCTAssertTrue(save.waitForExistence(timeout: 5))
            XCTAssertTrue(save.isEnabled, "Save should enable once evidence is entered for \(criterion)")
            save.click()
        }

        // 1.3.1 has no automated finding on this fixture: a Pass here must save cleanly, no contradiction.
        recordPass("1.3.1", evidence: "Headings and groups read correctly with VoiceOver.")
        XCTAssertTrue(app.staticTexts.containing("The tester recorded a pass for 1.3.1").waitForExistence(timeout: 10),
                      "Recorded-pass wording did not appear for 1.3.1")

        // Confirm not applicable, with a reason, for a different criterion this fixture has no finding for.
        let resultPicker331 = app.buttons["Result for 3.3.1"].firstMatch
        XCTAssertTrue(resultPicker331.waitForExistence(timeout: 10), "No result picker for 3.3.1")
        resultPicker331.click()
        app.menuItems["Confirm not applicable"].firstMatch.click()
        let reason331 = app.textFields["Reason 3.3.1 does not apply here, required"].firstMatch
        XCTAssertTrue(reason331.waitForExistence(timeout: 10))
        reason331.click()
        reason331.typeText("This screen never shows a validation error.")
        let save331 = app.buttons["Save the answer for 3.3.1"].firstMatch
        XCTAssertTrue(save331.waitForExistence(timeout: 5))
        save331.click()
        XCTAssertTrue(app.staticTexts.containing("You confirmed 3.3.1 does not apply on this screen").waitForExistence(timeout: 10),
                      "Confirmed-not-applicable confirmation did not appear for 3.3.1")

        // 1.1.1 has real automated findings on this fixture (missing accessible names) -- a Pass here must be
        // flagged immediately, not silently accepted.
        recordPass("1.1.1", evidence: "All images looked labeled to me.")
        XCTAssertTrue(app.staticTexts.containing("Automated checks on this screen found").waitForExistence(timeout: 10),
                      "Contradiction banner did not appear for a pass next to a real 1.1.1 finding")
        let dismiss = app.windows.buttons["OK"].firstMatch
        XCTAssertTrue(dismiss.waitForExistence(timeout: 10), "The contradiction alert's OK button did not appear")
        dismiss.click()
        XCTAssertFalse(dismiss.waitForExistence(timeout: 5), "The contradiction alert did not close")

        // Answers persist to guided-answers.json in the run's own folder (checked directly, rather than via a
        // further UI round trip, which raced with the app's own navigation/render timing in practice).
        let guidedAnswersFile = try findGuidedAnswersFile(under: seeded)
        XCTAssertNotNil(guidedAnswersFile, "No guided-answers.json was written under the seeded history")
        if let file = guidedAnswersFile {
            let contents = try String(contentsOfFile: file, encoding: .utf8)
            XCTAssertTrue(contents.contains("\"criterionNumber\": \"1.3.1\""), "1.3.1's answer was not saved")
            XCTAssertTrue(contents.contains("\"criterionNumber\": \"3.3.1\""), "3.3.1's answer was not saved")
            XCTAssertTrue(contents.contains("\"criterionNumber\": \"1.1.1\""), "1.1.1's answer was not saved")
            XCTAssertTrue(contents.contains("confirmedNotApplicable"), "3.3.1's confirmed-not-applicable result was not saved")
        }
    }

    private func findGuidedAnswersFile(under root: String) throws -> String? {
        let enumerator = FileManager.default.enumerator(atPath: root)
        while let path = enumerator?.nextObject() as? String {
            if path.hasSuffix("guided-answers.json") {
                return root + "/" + path
            }
        }
        return nil
    }

    func testDevicesPageChecksReadiness() throws {
        let app = Desktop.launch(page: "devices")
        let check = app.buttons.containing("Check whether")
        guard check.waitForExistence(timeout: 20) else { throw XCTSkip("No devices connected") }
        check.click()
        let result = app.staticTexts.matching(NSPredicate(format: "label CONTAINS 'Ready' OR label CONTAINS 'Fix'")).firstMatch
        XCTAssertTrue(result.waitForExistence(timeout: 60), "No readiness results after Check")
    }

    /// Choice controls must expose their name, role and value (WCAG 4.1.2). MAUI's defaults on Mac Catalyst
    /// exposed choices as text fields and checkboxes as unnamed switches; the app uses native controls instead.
    func testChoiceControlsHaveCorrectRolesAndNames() {
        let app = Desktop.launch(page: "scan")
        // Mac Catalyst exposes a menu button as a button whose value is the selected item.
        for name in ["Platform", "Device", "Law or standard", "What to scan"] {
            let choice = app.buttons[name].firstMatch
            XCTAssertTrue(choice.waitForExistence(timeout: 10), "\(name) is not a button with that name")
            XCTAssertFalse((choice.value as? String ?? "").isEmpty, "\(name) does not expose its selected value")
        }
        // Native macOS checkboxes (UISwitch, checkbox style) carry their own label and on/off state.
        for name in ["Also check with large system text (Android 200%, iOS AX3)", "App built with .NET MAUI (fix examples in XAML)"] {
            XCTAssertTrue(app.switches[name].exists || app.checkBoxes[name].exists, "'\(name)' is not a named checkbox")
        }
        XCTAssertEqual(app.textFields.matching(NSPredicate(format: "label IN %@", ["Platform", "Device"])).count, 0,
                       "Choices must not be exposed as text fields")
    }

    /// The large-text-restart picker (Controls/SelectButton, not MAUI Picker -- WCAG 4.1.2) only appears once
    /// a recording is under way, next to "Scan this screen now", and exposes its name, role and current
    /// value like the other choice controls. Needs CF_E2E=1 and the Android emulator (BuggyApp): this only
    /// checks the control is there and accessible, not the per-screen prompt itself, which needs a screen
    /// that actually needs a restart to apply the larger size (e.g. WeatherTwentyOne on iOS) -- not exercised
    /// by this automated suite.
    func testLargeTextRestartPickerAppearsOnceRecordingAndIsAccessible() throws {
        guard ProcessInfo.processInfo.environment["CF_E2E"] == "1" else { throw XCTSkip("CF_E2E not set") }
        let device = ProcessInfo.processInfo.environment["CF_E2E_DEVICE_NAME"] ?? "sdk_gphone"
        let app = Desktop.launch(page: "devices")
        let scanOnDevice = app.buttons.containing("New scan on Google \(device)")
        guard scanOnDevice.waitForExistence(timeout: 20) else { throw XCTSkip("Device '\(device)' not connected") }
        scanOnDevice.click()
        let picker = app.buttons["Device"].firstMatch
        XCTAssertTrue(picker.waitForExistence(timeout: 10))
        let selected = NSPredicate(format: "value CONTAINS %@", device)
        expectation(for: selected, evaluatedWith: picker)
        waitForExpectations(timeout: 15)

        let modePicker = app.buttons["What to scan"].firstMatch
        XCTAssertTrue(modePicker.waitForExistence(timeout: 10))
        modePicker.click()
        app.menuItems["Record: each screen as I use the app"].firstMatch.click()

        let appId = app.textFields["App package or bundle id"].firstMatch
        XCTAssertTrue(appId.waitForExistence(timeout: 10))
        appId.click()
        appId.typeText("org.swipewalk.buggyapp")
        app.buttons["Start"].firstMatch.click()

        let restartPicker = app.buttons["When checking larger text needs the app restarted"].firstMatch
        guard restartPicker.waitForExistence(timeout: 60) else {
            XCTFail("Recording did not start (large-text-restart picker never appeared)")
            return
        }
        XCTAssertEqual(restartPicker.value as? String, "Ask each time", "The picker should default to asking")

        app.buttons["Finish"].firstMatch.click()
        let reportPage = app.buttons["Open in browser"].firstMatch
        _ = reportPage.waitForExistence(timeout: 60) // best effort: this test cares about the picker, not the report
    }

    func testStartWithoutAppExplainsWhatIsMissing() throws {
        let app = Desktop.launch(page: "scan")
        // Start stays disabled until a device is chosen (UpdateDeviceRequiredControls) -- choose one so this
        // test can reach the "which app?" validation it actually cares about.
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        try selectAnyDevice(devicePicker, in: app)
        app.buttons["Start"].firstMatch.click()
        XCTAssertTrue(app.staticTexts.containing("Enter the app's package or bundle id").waitForExistence(timeout: 10),
                      "Starting without an app should explain what is missing")
        app.windows.buttons["OK"].firstMatch.click()
    }

    /// The App field stays a normal, always-usable text field (WCAG 2.5.8 target size, 4.1.2 role/name):
    /// typing an id directly must keep working whether or not the app picker is ever opened.
    func testAppFieldAcceptsATypedIdDirectly() {
        let app = Desktop.launch(page: "scan")
        let appId = app.textFields["App package or bundle id"].firstMatch
        XCTAssertTrue(appId.waitForExistence(timeout: 10))
        appId.click()
        appId.typeText("org.example.city.permits")
        XCTAssertEqual(appId.value as? String, "org.example.city.permits")
    }

    /// No device is chosen when New scan opens (NewScanPage.OnPlatformChanged never preselects one any more --
    /// see testPhysicalDeviceNoticeShownOnceForAPhysicalDeviceOnly for why), so Start and Choose app… start
    /// disabled with a visible reason rather than opening an empty/broken app picker or a scan with no device.
    /// Reproducible on every launch, device or no device connected -- no XCTSkip needed here.
    func testChooseAppAndStartStayDisabledUntilADeviceIsChosen() {
        let app = Desktop.launch(page: "scan")
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        XCTAssertFalse(app.buttons["Choose app…"].firstMatch.isEnabled,
                       "Choose app… should stay disabled until a device is chosen")
        XCTAssertFalse(app.buttons["Start"].firstMatch.isEnabled,
                       "Start should stay disabled until a device is chosen")
        XCTAssertTrue(app.staticTexts.containing("Choose a device above").exists,
                      "A visible reason should explain why they're disabled")
    }

    /// The app picker (Pages/AppPickerPage) is reachable and operable entirely by keyboard: Tab from the
    /// search field moves into the list, arrow keys move through it, and Space chooses the focused app and
    /// fills the App field. (Space, not Return, is the native "activate the focused item" key for this
    /// Mac Catalyst CollectionView -- confirmed by driving it with the keyboard; Return in the search field
    /// itself is handled separately, see testAppPickerEnterAcceptsATypedIdWithNoMatch.)
    func testAppPickerIsOperableByKeyboard() throws {
        let app = Desktop.launch(page: "scan")
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        // "No device found (see Devices)" is still EmptyText for zero devices, regardless of the new "Choose a
        // device" placeholder shown once at least one device exists but none is chosen yet -- so this still
        // correctly tells "no device at all" apart from "a device is available".
        guard (devicePicker.value as? String) != "No device found (see Devices)" else {
            throw XCTSkip("No device connected")
        }
        let ready = NSPredicate(format: "value != %@", "No device found (see Devices)")
        expectation(for: ready, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)
        // No device is chosen by default any more -- Choose app… needs one picked first.
        try selectAnyDevice(devicePicker, in: app)

        app.buttons["Choose app…"].firstMatch.click()
        let search = app.textFields["Filter apps by id or name"].firstMatch
        guard search.waitForExistence(timeout: 15) else { XCTFail("App picker did not open"); return }
        // "Show system apps" is a named, native checkbox (see testChoiceControlsHaveCorrectRolesAndNames).
        XCTAssertTrue(app.switches["Show system apps"].exists || app.checkBoxes["Show system apps"].exists,
                      "'Show system apps' is not a named checkbox")
        guard app.cells.firstMatch.waitForExistence(timeout: 15) else { throw XCTSkip("No apps listed on this device") }

        search.click()
        app.typeKey(XCUIKeyboardKey.tab, modifierFlags: [])
        app.typeKey(XCUIKeyboardKey.downArrow, modifierFlags: [])
        app.typeKey(XCUIKeyboardKey.space, modifierFlags: [])

        let appId = app.textFields["App package or bundle id"].firstMatch
        let filled = NSPredicate(format: "value != ''")
        expectation(for: filled, evaluatedWith: appId)
        waitForExpectations(timeout: 10)
        XCTAssertFalse(app.textFields["Filter apps by id or name"].exists, "Choosing an app should close the picker")
    }

    /// Escape cancels the app picker without choosing anything and without trapping focus: the New scan
    /// page's own controls are usable again straight after.
    func testAppPickerEscapeCancelsWithoutASelection() throws {
        let app = Desktop.launch(page: "scan")
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        guard (devicePicker.value as? String) != "No device found (see Devices)" else {
            throw XCTSkip("No device connected")
        }
        let ready = NSPredicate(format: "value != %@", "No device found (see Devices)")
        expectation(for: ready, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)
        try selectAnyDevice(devicePicker, in: app)

        app.buttons["Choose app…"].firstMatch.click()
        let search = app.textFields["Filter apps by id or name"].firstMatch
        guard search.waitForExistence(timeout: 15) else { XCTFail("App picker did not open"); return }
        app.typeKey(XCUIKeyboardKey.escape, modifierFlags: [])
        let dismissed = NSPredicate(format: "exists == 0")
        expectation(for: dismissed, evaluatedWith: search)
        waitForExpectations(timeout: 10)
        XCTAssertEqual(app.textFields["App package or bundle id"].firstMatch.value as? String, "",
                       "Cancelling should not fill the App field")
        XCTAssertTrue(app.buttons["Choose app…"].firstMatch.exists, "Focus should return to a usable New scan page, not be trapped")
    }

    /// Typing an id that matches nothing installed, then pressing Enter in the search field, accepts it as
    /// typed (WCAG 2.5.8, and requirement 1: an id not yet installed must still be usable).
    func testAppPickerEnterAcceptsATypedIdWithNoMatch() throws {
        let app = Desktop.launch(page: "scan")
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        guard (devicePicker.value as? String) != "No device found (see Devices)" else {
            throw XCTSkip("No device connected")
        }
        let ready = NSPredicate(format: "value != %@", "No device found (see Devices)")
        expectation(for: ready, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)
        try selectAnyDevice(devicePicker, in: app)

        app.buttons["Choose app…"].firstMatch.click()
        let search = app.textFields["Filter apps by id or name"].firstMatch
        guard search.waitForExistence(timeout: 15) else { XCTFail("App picker did not open"); return }
        search.click()
        search.typeText("org.example.city.zzz.not.installed")
        app.typeKey(XCUIKeyboardKey.return, modifierFlags: [])
        let appId = app.textFields["App package or bundle id"].firstMatch
        let filled = NSPredicate(format: "value == 'org.example.city.zzz.not.installed'")
        expectation(for: filled, evaluatedWith: appId)
        waitForExpectations(timeout: 10)
    }

    /// End to end: scan the sample app on the Android emulator, then find the run in History.
    /// Needs CF_E2E=1, the Android emulator connected (CF_E2E_DEVICE_NAME, default sdk_gphone) and BuggyApp in front.
    func testScanShowsReportAndSavesToHistory() throws {
        guard ProcessInfo.processInfo.environment["CF_E2E"] == "1" else { throw XCTSkip("CF_E2E not set") }
        let device = ProcessInfo.processInfo.environment["CF_E2E_DEVICE_NAME"] ?? "sdk_gphone"
        let historyDir = Desktop.freshHistoryDir()
        let app = Desktop.launch(page: "devices", history: historyDir)
        let scanOnDevice = app.buttons.containing("New scan on Google \(device)")
        guard scanOnDevice.waitForExistence(timeout: 20) else { throw XCTSkip("Device '\(device)' not connected") }
        scanOnDevice.click()
        let picker = app.buttons["Device"].firstMatch
        XCTAssertTrue(picker.waitForExistence(timeout: 10))
        let selected = NSPredicate(format: "value CONTAINS %@", device)
        expectation(for: selected, evaluatedWith: picker)
        waitForExpectations(timeout: 15)

        let appId = app.textFields["App package or bundle id"].firstMatch
        XCTAssertTrue(appId.waitForExistence(timeout: 10))
        appId.click()
        appId.typeText("org.swipewalk.buggyapp")
        app.buttons["Start"].firstMatch.click()

        // Poll the run's own report.html on disk instead of querying the UI while the scan runs and the
        // Report page's WebView loads and renders: any query rooted at `app` -- even for a plain native
        // button on the same page -- has to snapshot the WHOLE accessibility tree, including the WebView's
        // own (large, screenshot-embedded) one, and XCUITest can time out doing that while it's still
        // rendering ("Failed to get matching snapshots", found flaky about 1 in 3 runs even querying only a
        // native "Open in browser" button). Leaving the Report page with a keyboard shortcut below (rather
        // than clicking a button on it) sidesteps the same problem for the next step.
        guard let reportFile = Desktop.waitForReportFile(in: historyDir, timeout: 300),
              let reportText = try? String(contentsOf: reportFile, encoding: .utf8),
              reportText.contains("Automated checks found") else {
            // Keep what the app showed (including the progress messages) to diagnose the failure.
            let dump = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("e2e-failure.txt")
            try? app.debugDescription.write(to: dump, atomically: true, encoding: .utf8)
            XCTFail("The report did not open after the scan; screen saved to \(dump.path)")
            return
        }

        app.typeKey("4", modifierFlags: .command) // Go > History -- avoids querying a button on the Report page
        // The row's app name is no longer its own StaticText: it is part of the row's grouped accessible name
        // (see testHistoryRowsHaveAccessibleNamesAndDeleteStaysReachable), so search all elements, not just
        // staticTexts.
        if !app.descendants(matching: .any).containing("org.swipewalk.buggyapp").waitForExistence(timeout: 10) {
            let dump = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("e2e-history.txt")
            try? app.debugDescription.write(to: dump, atomically: true, encoding: .utf8)
            XCTFail("The run is not in History; screen saved to \(dump.path)")
        }
    }

    /// Selecting a physical phone in New scan's Device picker shows the physical-phone notice
    /// (Services/PhysicalDeviceNotice, a native system alert, so VoiceOver should read it in full -- a custom
    /// modal page's own announcement of it was found unreliable on a real Mac) that the large-text check
    /// changes that phone's own system text size; selecting an emulator never does, and neither shows the
    /// persistent reminder next to the large-text option (NewScanPage.PhysicalDeviceTextSizeLabel). No device
    /// is chosen by default (NewScanPage.OnPlatformChanged): Start and Choose app… start disabled with a
    /// reason shown, and the Device picker itself shows a placeholder, until a device is actually picked --
    /// this used to matter here specifically, because a physical phone that happened to be the default
    /// selection never showed the notice at all, and picking that same already-selected phone again in the
    /// menu doesn't raise a change either. "Don't Show Again" is remembered across app launches via the app's
    /// own macOS preferences (Services/PhysicalDeviceNoticePreference), keyed on its bundle id -- the same one
    /// a normal install would use -- so this test resets that preference first and again at the end
    /// (--reset-physical-device-notice, App.xaml.cs), and never leaves it dismissed for whoever runs the app
    /// normally afterwards. OK and Return must behave exactly like each other, never like Don't Show Again --
    /// otherwise an accidental key press before deliberately choosing could suppress the notice for good.
    /// (Escape is covered separately: see testPhysicalDeviceNoticeEscapeNotReachableByXCUITest -- a real
    /// Escape key press closes this notice too, confirmed on the Mac, but XCUITest's synthesized one doesn't
    /// reach it.)
    func testPhysicalDeviceNoticeShownOnceForAPhysicalDeviceOnly() throws {
        var app = Desktop.launch(page: "scan", extraArguments: ["--reset-physical-device-notice"])
        defer { app.terminate() }
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))

        // No device chosen yet: Start and Choose app… stay disabled, with a visible reason, and the picker
        // itself shows its placeholder as both its displayed title and its accessible value.
        XCTAssertFalse(app.buttons["Start"].firstMatch.isEnabled, "Start should stay disabled until a device is chosen")
        XCTAssertFalse(app.buttons["Choose app…"].firstMatch.isEnabled, "Choose app… should stay disabled until a device is chosen")
        XCTAssertEqual(devicePicker.value as? String, "Choose a device")
        XCTAssertTrue(app.staticTexts.containing("Choose a device above").exists,
                      "A visible reason should explain why Start is disabled")

        // The device list loads asynchronously (adb devices, then one getprop round trip per device); opening
        // the native pop-up menu before it's populated would show a stale, empty menu that doesn't refresh once
        // open -- wait for the picker's own value to move off the placeholder first, same as
        // testAppPickerIsOperableByKeyboard below.
        let loaded = NSPredicate(format: "value != %@", "No device found (see Devices)")
        expectation(for: loaded, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)

        // DeviceInfo.ToString() (Core/Model/DeviceInfo.cs) ends "... · emulator" / "... · physical device" /
        // "... · simulator". UIKit derives each menu item's own accessibility identifier from that same text
        // (lowercased, spaces to underscores); matching on "identifier" here rather than "label" is what makes
        // this reliable -- a label-based predicate on this async-populated native menu was seen to miss items
        // that were demonstrably already there (visible in an app.debugDescription dump taken right after).
        devicePicker.click()
        let emulatorItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "emulator")).firstMatch
        guard emulatorItem.waitForExistence(timeout: 10) else { throw XCTSkip("No Android emulator connected") }
        emulatorItem.click()
        XCTAssertFalse(app.staticTexts["Physical phone selected"].waitForExistence(timeout: 3),
                       "Selecting the emulator must not show the physical-phone notice")
        XCTAssertFalse(app.staticTexts.containing("system text size").exists,
                       "Selecting the emulator must not show the persistent large-text-on-a-phone reminder")
        XCTAssertTrue(app.buttons["Start"].firstMatch.isEnabled, "Start should be enabled once a device is chosen")

        devicePicker.click()
        let physicalItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "physical_device")).firstMatch
        guard physicalItem.waitForExistence(timeout: 10) else { throw XCTSkip("No physical device connected") }
        physicalItem.click()

        let heading = app.staticTexts["Physical phone selected"]
        guard heading.waitForExistence(timeout: 10) else { XCTFail("Physical-phone notice did not appear"); return }
        XCTAssertTrue(app.staticTexts.containing("test device").exists, "The notice should say to use a test device")
        // Matching a distinctive phrase from near the end of the body (not just the opening line already
        // checked above) confirms the whole message is exposed to accessibility, not truncated -- this is a
        // genuine system alert, not a Label in a custom page, so VoiceOver reads its title and message in
        // full (confirmed by listening with VoiceOver on the Mac).
        XCTAssertTrue(app.staticTexts.containing("relies on every day").exists,
                      "The notice body should be shown in full, not truncated")
        XCTAssertTrue(app.windows.buttons["OK"].exists, "The notice should offer OK")
        XCTAssertTrue(app.windows.buttons["Don't Show Again"].exists, "The notice should offer Don't Show Again")

        // OK closes the notice without remembering anything (Escape is covered separately, in
        // testPhysicalDeviceNoticeEscapeNotReachableByXCUITest, kept isolated so that test's XCTSkip
        // doesn't run inside this one's own coverage).
        app.windows.buttons["OK"].firstMatch.click()
        XCTAssertFalse(heading.waitForExistence(timeout: 3), "OK should close the notice")
        // Large-text is checked by default, so the persistent reminder should appear once the notice is closed.
        XCTAssertTrue(app.staticTexts.containing("system text size").waitForExistence(timeout: 10),
                      "The persistent reminder should show for a physical device with large-text checked")

        // Unchecking large-text hides the persistent reminder without touching the device selection.
        app.switches["Also check with large system text (Android 200%, iOS AX3)"].firstMatch.click()
        XCTAssertFalse(app.staticTexts.containing("system text size").exists,
                       "The persistent reminder should hide once large-text is unchecked")

        // A fresh launch with the same physical device chosen again must show the notice again: OK must not
        // have suppressed it for good.
        app.terminate()
        app = Desktop.launch(page: "scan")
        var currentPicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(currentPicker.waitForExistence(timeout: 10))
        expectation(for: loaded, evaluatedWith: currentPicker)
        waitForExpectations(timeout: 15)
        currentPicker.click()
        var currentPhysicalItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "physical_device")).firstMatch
        guard currentPhysicalItem.waitForExistence(timeout: 10) else { XCTFail("Physical device disappeared between launches"); return }
        currentPhysicalItem.click()
        var currentHeading = app.staticTexts["Physical phone selected"]
        guard currentHeading.waitForExistence(timeout: 10) else {
            XCTFail("The notice should reappear: OK must not have suppressed it for good"); return
        }

        // Return must also behave exactly like OK -- never Don't Show Again -- so a stray Return press before
        // a deliberate choice can't silently suppress the notice either.
        app.typeKey(XCUIKeyboardKey.return, modifierFlags: [])
        XCTAssertFalse(currentHeading.waitForExistence(timeout: 3), "Return should close the notice")
        app.terminate()
        app = Desktop.launch(page: "scan")
        currentPicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(currentPicker.waitForExistence(timeout: 10))
        expectation(for: loaded, evaluatedWith: currentPicker)
        waitForExpectations(timeout: 15)
        currentPicker.click()
        currentPhysicalItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "physical_device")).firstMatch
        guard currentPhysicalItem.waitForExistence(timeout: 10) else { XCTFail("Physical device disappeared between launches"); return }
        currentPhysicalItem.click()
        currentHeading = app.staticTexts["Physical phone selected"]
        guard currentHeading.waitForExistence(timeout: 10) else {
            XCTFail("The notice should reappear: Return must not have suppressed it for good"); return
        }

        // Clicking "Don't Show Again" is the only thing that persists.
        app.windows.buttons["Don't Show Again"].firstMatch.click()
        XCTAssertFalse(currentHeading.waitForExistence(timeout: 3), "Don't Show Again should close the notice")

        // A fresh launch (not just this page instance) with the same physical device chosen must not show the
        // notice again, now that "Don't Show Again" was clicked -- the preference is remembered across
        // launches, not only for the lifetime of this page.
        app.terminate()
        app = Desktop.launch(page: "scan")
        currentPicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(currentPicker.waitForExistence(timeout: 10))
        expectation(for: loaded, evaluatedWith: currentPicker)
        waitForExpectations(timeout: 15)
        currentPicker.click()
        currentPhysicalItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "physical_device")).firstMatch
        guard currentPhysicalItem.waitForExistence(timeout: 10) else { XCTFail("Physical device disappeared between launches"); return }
        currentPhysicalItem.click()
        XCTAssertFalse(app.staticTexts["Physical phone selected"].waitForExistence(timeout: 3),
                       "The notice must not reappear once Don't Show Again was chosen")

        // Clean up: restore the shared preference so a normal run of the app shows the notice again, as if
        // this test had never run.
        app.terminate()
        app = Desktop.launch(page: "scan", extraArguments: ["--reset-physical-device-notice"])
    }

    /// Escape does close the physical-phone notice (Services/PhysicalDeviceNotice.ShowMacAlertAsync) with a
    /// real key press, confirmed on the Mac -- but not automatable here: XCUITest's synthesized Escape doesn't
    /// reach this native, AppKit-bridged alert, unlike a real key press or the custom modal pages
    /// testAppPickerEscapeCancelsWithoutASelection covers. Skipped rather than asserted or removed, so this
    /// automation gap stays visible without asserting a false failure.
    func testPhysicalDeviceNoticeEscapeNotReachableByXCUITest() throws {
        throw XCTSkip("XCUITest's synthesized Escape doesn't reach this native, AppKit-bridged alert; a real Escape key press was verified on 2026-09-23 and closes it.")
    }

    /// Same coverage as testPhysicalDeviceNoticeShownOnceForAPhysicalDeviceOnly, but pins the selection to the
    /// iOS Simulator and a physical iPhone specifically. The original test matches "emulator" (Android-only
    /// Kind, so already unambiguous) and "physical_device" (ambiguous: a physical Android phone would also
    /// match if one were connected) -- this exercises the iPhone case, which that test cannot reach on a Mac
    /// with only an iPhone attached (NewScanPage's Device picker loads one platform's devices at a time; the
    /// page starts on Android).
    func testPhysicalDeviceNoticeShownOnceForPhysicalIPhoneOnly() throws {
        var app = Desktop.launch(page: "scan", extraArguments: ["--reset-physical-device-notice"])
        defer { app.terminate() }
        let devicePicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(devicePicker.waitForExistence(timeout: 10))
        XCTAssertEqual(devicePicker.value as? String, "Choose a device", "No device should be chosen by default")
        let loaded = NSPredicate(format: "value != %@", "No device found (see Devices)")

        // The device picker only ever lists the currently selected platform's devices (NewScanPage.xaml.cs
        // OnPlatformChanged loads Devices.AndroidAsync or Devices.IosAsync, not a combined list); the page
        // starts on Android, so switch to iOS first or "simulator"/the iPhone will never appear at all.
        app.buttons["Platform"].firstMatch.click()
        let iosItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "ios")).firstMatch
        XCTAssertTrue(iosItem.waitForExistence(timeout: 10), "iOS platform option did not appear")
        iosItem.click()
        expectation(for: loaded, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)

        // "simulator" is iOS-only (Android's non-physical Kind is "emulator"), so this already uniquely
        // targets the iOS Simulator, not an Android emulator.
        devicePicker.click()
        let simulatorItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "simulator")).firstMatch
        guard simulatorItem.waitForExistence(timeout: 10) else { throw XCTSkip("No iOS Simulator connected") }
        simulatorItem.click()
        XCTAssertFalse(app.staticTexts["Physical phone selected"].waitForExistence(timeout: 3),
                       "Selecting the iOS Simulator must not show the physical-phone notice")
        XCTAssertFalse(app.staticTexts.containing("system text size").exists,
                       "Selecting the iOS Simulator must not show the persistent large-text-on-a-phone reminder")
        XCTAssertTrue(app.buttons["Start"].firstMatch.isEnabled, "Start should be enabled once a device is chosen")

        // "physical_device" alone would also match a physical Android phone if one were connected; requiring
        // "ios" as well pins this to the physical iPhone specifically. When no iPhone is attached, this (and
        // the whole test) skips rather than fails.
        devicePicker.click()
        let physicalItem = app.menuItems.matching(
            NSPredicate(format: "identifier CONTAINS %@ AND identifier CONTAINS %@", "ios", "physical_device")
        ).firstMatch
        guard physicalItem.waitForExistence(timeout: 10) else { throw XCTSkip("No physical iPhone connected") }
        physicalItem.click()

        let heading = app.staticTexts["Physical phone selected"]
        guard heading.waitForExistence(timeout: 10) else { XCTFail("Physical-phone notice did not appear for the iPhone"); return }
        XCTAssertTrue(app.staticTexts.containing("test device").exists, "The notice should say to use a test device")
        XCTAssertTrue(app.staticTexts.containing("relies on every day").exists,
                      "The notice body should be shown in full, not truncated")
        XCTAssertTrue(app.windows.buttons["OK"].exists, "The notice should offer OK")
        XCTAssertTrue(app.windows.buttons["Don't Show Again"].exists, "The notice should offer Don't Show Again")

        app.windows.buttons["OK"].firstMatch.click()
        XCTAssertFalse(heading.waitForExistence(timeout: 3), "OK should close the notice")
        XCTAssertTrue(app.staticTexts.containing("system text size").waitForExistence(timeout: 10),
                      "The persistent reminder should show for the physical iPhone with large-text checked")

        app.switches["Also check with large system text (Android 200%, iOS AX3)"].firstMatch.click()
        XCTAssertFalse(app.staticTexts.containing("system text size").exists,
                       "The persistent reminder should hide once large-text is unchecked")

        // A fresh launch with the same physical iPhone chosen again must show the notice again: OK must not
        // have suppressed it for good.
        app.terminate()
        app = Desktop.launch(page: "scan")
        var currentPicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(currentPicker.waitForExistence(timeout: 10))
        app.buttons["Platform"].firstMatch.click()
        var currentIosItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "ios")).firstMatch
        XCTAssertTrue(currentIosItem.waitForExistence(timeout: 10), "iOS platform option did not appear")
        currentIosItem.click()
        expectation(for: loaded, evaluatedWith: currentPicker)
        waitForExpectations(timeout: 15)
        currentPicker.click()
        var currentPhysicalItem = app.menuItems.matching(
            NSPredicate(format: "identifier CONTAINS %@ AND identifier CONTAINS %@", "ios", "physical_device")
        ).firstMatch
        guard currentPhysicalItem.waitForExistence(timeout: 10) else { XCTFail("Physical iPhone disappeared between launches"); return }
        currentPhysicalItem.click()
        let currentHeading = app.staticTexts["Physical phone selected"]
        guard currentHeading.waitForExistence(timeout: 10) else {
            XCTFail("The notice should reappear: OK must not have suppressed it for good"); return
        }

        // Clicking "Don't Show Again" is the only thing that persists.
        app.windows.buttons["Don't Show Again"].firstMatch.click()
        XCTAssertFalse(currentHeading.waitForExistence(timeout: 3), "Don't Show Again should close the notice")

        app.terminate()
        app = Desktop.launch(page: "scan")
        currentPicker = app.buttons["Device"].firstMatch
        XCTAssertTrue(currentPicker.waitForExistence(timeout: 10))
        app.buttons["Platform"].firstMatch.click()
        currentIosItem = app.menuItems.matching(NSPredicate(format: "identifier CONTAINS %@", "ios")).firstMatch
        XCTAssertTrue(currentIosItem.waitForExistence(timeout: 10), "iOS platform option did not appear")
        currentIosItem.click()
        expectation(for: loaded, evaluatedWith: currentPicker)
        waitForExpectations(timeout: 15)
        currentPicker.click()
        currentPhysicalItem = app.menuItems.matching(
            NSPredicate(format: "identifier CONTAINS %@ AND identifier CONTAINS %@", "ios", "physical_device")
        ).firstMatch
        guard currentPhysicalItem.waitForExistence(timeout: 10) else { XCTFail("Physical iPhone disappeared between launches"); return }
        currentPhysicalItem.click()
        XCTAssertFalse(app.staticTexts["Physical phone selected"].waitForExistence(timeout: 3),
                       "The notice must not reappear once Don't Show Again was chosen")

        app.terminate()
        app = Desktop.launch(page: "scan", extraArguments: ["--reset-physical-device-notice"])
    }
}
