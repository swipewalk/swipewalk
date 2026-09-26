import XCTest

/// Launches Swipewalk.app (path from CF_APP_PATH) with an isolated, empty run history.
enum Desktop {
    static var appURL: URL {
        let env = ProcessInfo.processInfo.environment
        if let path = env["CF_APP_PATH"] { return URL(fileURLWithPath: path) }
        // Default: the Debug build next to this repo's harness folder.
        let repo = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().deletingLastPathComponent()
        return repo.appendingPathComponent("src/Swipewalk.Desktop/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Swipewalk.app")
    }

    /// A fresh, empty run history folder. Broken out from `launch` so a test that wants to inspect what gets
    /// written into it (report.html, results.json) can hand it in up front and keep the URL -- see
    /// `waitForReportFile`.
    static func freshHistoryDir() -> URL {
        FileManager.default.temporaryDirectory.appendingPathComponent("cf-uitest-history-\(UUID().uuidString)")
    }

    static func launch(page: String? = nil, history: URL? = nil, extraArguments: [String] = []) -> XCUIApplication {
        let app = XCUIApplication(url: appURL)
        let historyDir = history ?? freshHistoryDir()
        // --text-scale: start at 100% and don't remember text size changes made by tests.
        // -ApplePersistenceIgnoreState: never show macOS's "reopen windows after a crash" dialog in tests.
        app.launchArguments = ["-ApplePersistenceIgnoreState", "YES", "--history", historyDir.path, "--text-scale", "1"]
            + (page.map { ["--page", $0] } ?? []) + extraArguments
        app.launch()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 30), "Swipewalk did not start")
        return app
    }

    /// Polls the filesystem for a report.html written into a run folder under `historyDir`, instead of querying
    /// into ReportPage's WebView (found flaky, about 1 in 3 runs: "Failed to get matching snapshots" -- XCUITest
    /// timing out trying to snapshot the WebView's own large accessibility tree while the report is still
    /// rendering). RunHistory.SaveAsync writes report.html directly into the run's own folder, one level under
    /// `historyDir`, so this only has to look one level deep. Returns the path found, or nil after `timeout`.
    static func waitForReportFile(in historyDir: URL, timeout: TimeInterval) -> URL? {
        let deadline = Date().addingTimeInterval(timeout)
        let fm = FileManager.default
        repeat {
            let runFolders = (try? fm.contentsOfDirectory(at: historyDir, includingPropertiesForKeys: nil)) ?? []
            if let report = runFolders.map({ $0.appendingPathComponent("report.html") }).first(where: { fm.fileExists(atPath: $0.path) }) {
                return report
            }
            Thread.sleep(forTimeInterval: 0.5)
        } while Date() < deadline
        return nil
    }
}

extension XCUIElementQuery {
    /// First element whose label or value contains the text.
    func containing(_ text: String) -> XCUIElement {
        matching(NSPredicate(format: "label CONTAINS %@ OR value CONTAINS %@", text, text)).firstMatch
    }
}
