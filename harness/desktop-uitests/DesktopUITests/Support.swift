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

    static func launch(page: String? = nil, history: URL? = nil, extraArguments: [String] = []) -> XCUIApplication {
        let app = XCUIApplication(url: appURL)
        let historyDir = history ?? FileManager.default.temporaryDirectory
            .appendingPathComponent("cf-uitest-history-\(UUID().uuidString)")
        // --text-scale: start at 100% and don't remember text size changes made by tests.
        // -ApplePersistenceIgnoreState: never show macOS's "reopen windows after a crash" dialog in tests.
        app.launchArguments = ["-ApplePersistenceIgnoreState", "YES", "--history", historyDir.path, "--text-scale", "1"]
            + (page.map { ["--page", $0] } ?? []) + extraArguments
        app.launch()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 30), "Swipewalk did not start")
        return app
    }
}

extension XCUIElementQuery {
    /// First element whose label or value contains the text.
    func containing(_ text: String) -> XCUIElement {
        matching(NSPredicate(format: "label CONTAINS %@ OR value CONTAINS %@", text, text)).firstMatch
    }
}
