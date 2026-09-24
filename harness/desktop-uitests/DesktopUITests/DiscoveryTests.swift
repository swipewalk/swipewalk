import XCTest

/// Development aid: writes the accessibility hierarchy of each page to the (sandboxed) runner's temporary
/// directory, printed as "CF_DUMP_DIR=...". Runs only with CF_DUMP=1.
final class DiscoveryTests: XCTestCase {
    func testDumpPages() throws {
        guard ProcessInfo.processInfo.environment["CF_DUMP"] == "1" else { throw XCTSkip("CF_DUMP not set") }
        let dir = NSTemporaryDirectory()
        print("CF_DUMP_DIR=\(dir)")
        for page in ["dashboard", "scan", "devices", "history"] {
            let app = Desktop.launch(page: page)
            sleep(4)
            try app.debugDescription.write(to: URL(fileURLWithPath: dir).appendingPathComponent("\(page).txt"), atomically: true, encoding: .utf8)
            app.terminate()
        }
    }

    /// Dumps the New scan page's app picker (opened via "Choose app…"), which needs a device connected to open
    /// without the "choose a device first" alert. Also runs only with CF_DUMP=1.
    func testDumpAppPicker() throws {
        guard ProcessInfo.processInfo.environment["CF_DUMP"] == "1" else { throw XCTSkip("CF_DUMP not set") }
        let dir = NSTemporaryDirectory()
        print("CF_DUMP_DIR=\(dir)")
        let app = Desktop.launch(page: "scan")
        let devicePicker = app.buttons["Device"].firstMatch
        guard devicePicker.waitForExistence(timeout: 10) else { throw XCTSkip("Device picker did not appear") }
        let ready = NSPredicate(format: "value != %@", "No device found (see Devices)")
        expectation(for: ready, evaluatedWith: devicePicker)
        waitForExpectations(timeout: 15)
        app.buttons["Choose app…"].firstMatch.click()
        sleep(2)
        try app.debugDescription.write(to: URL(fileURLWithPath: dir).appendingPathComponent("app-picker.txt"), atomically: true, encoding: .utf8)
        app.terminate()
    }
}
