import SwiftUI

// Native (no MAUI) ground-truth sample app for Swipewalk. See ../README.md.
@main
struct NativeiOSApp: App {
    var body: some Scene {
        WindowGroup {
            RootView(initialScreen: Self.screenFromLaunchArguments())
        }
    }

    /// `xcrun simctl launch <device> org.swipewalk.nativeios -views` (or `-swiftui`) opens that
    /// screen directly, without scripting a tap through the picker -- a launch-argument switch, the
    /// same style as this repo's iOS large-text fallback (`-UIPreferredContentSizeCategoryName`).
    private static func screenFromLaunchArguments() -> Screen? {
        let args = ProcessInfo.processInfo.arguments
        if args.contains("-views") { return .uikit }
        if args.contains("-swiftui") { return .swiftui }
        return nil
    }
}
