import SwiftUI

/// Which planted-screen the person picked from the root menu.
enum Screen: Hashable {
    case uikit
    case swiftui
}

/// App entry point: a plain menu that leads to the two "Pay a parking ticket"
/// screens, one built in UIKit and one in SwiftUI, each planted with the same
/// bug classes (see ../README.md).
struct RootView: View {
    @State private var path: [Screen]

    /// `initialScreen` lets a launch argument (see NativeiOSApp.swift) open a screen directly, the
    /// same convenience the Android sample gets from exported activities -- so a scan can reach
    /// either screen without scripting a tap through the picker first.
    init(initialScreen: Screen? = nil) {
        _path = State(initialValue: initialScreen.map { [$0] } ?? [])
    }

    var body: some View {
        NavigationStack(path: $path) {
            VStack(spacing: 16) {
                Text("City of Exampleville")
                    .font(.title2)
                Text("Pick a screen to open the same \u{201c}Pay a parking ticket\u{201d} flow, built two ways.")
                    .font(.body)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal)

                Button("Views screen") {
                    path.append(.uikit)
                }
                .buttonStyle(.borderedProminent)

                Button("SwiftUI screen") {
                    path.append(.swiftui)
                }
                .buttonStyle(.bordered)
            }
            .padding()
            .navigationTitle("Native iOS Sample")
            .navigationDestination(for: Screen.self) { screen in
                switch screen {
                case .uikit:
                    ViewsScreenRepresentable()
                case .swiftui:
                    SwiftUIScreenView()
                }
            }
        }
    }
}

/// Hosts the UIKit screen inside the SwiftUI navigation stack.
private struct ViewsScreenRepresentable: UIViewControllerRepresentable {
    func makeUIViewController(context: Context) -> ViewsScreenViewController {
        ViewsScreenViewController()
    }

    func updateUIViewController(_ uiViewController: ViewsScreenViewController, context: Context) {
        // Nothing to update; the screen has no external state.
    }
}
