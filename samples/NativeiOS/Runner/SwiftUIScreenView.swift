import SwiftUI

/// Ground-truth screen for Swipewalk, built the idiomatic SwiftUI way. Bugs are
/// deliberate; each is tagged with an ID (N1..) matching
/// ground-truth.swiftui.json. Elements tagged OK are negative controls. This is
/// the same "Pay a parking ticket" flow as ViewsScreenViewController, planting
/// the same 8 bug classes the way a SwiftUI developer would actually make them.
struct SwiftUIScreenView: View {
    @State private var ticketNumber = ""
    @State private var plateNumber = ""

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                headerRow
                helperText
                ticketNumberField
                plateNumberField
                helpRow
                payButton
                actionRow
                iconRow
                lateFeesText
                historyButton
            }
            .padding(20)
        }
        .navigationTitle("Pay a parking ticket")
    }

    // MARK: Header

    private var headerRow: some View {
        HStack {
            // OK: heading, real text.
            Text("City of Exampleville")
                .font(.title2)
                .accessibilityAddTraits(.isHeader)

            Spacer()

            // N1: icon-only button with no accessible name. SwiftUI doesn't
            // derive a name from a plain SF Symbol image inside a button, so
            // forgetting .accessibilityLabel here is a realistic, common miss.
            Button {
                // Search action.
            } label: {
                Image(systemName: "magnifyingglass")
                    .frame(width: 44, height: 44)
            }
        }
    }

    // MARK: Helper text

    private var helperText: some View {
        // N2: low-contrast helper text (light gray on white, about 2.3:1).
        Text("Payments post within 2 business days.")
            .font(.footnote)
            .foregroundColor(Color(white: 0.67))
    }

    // MARK: Text fields

    private var ticketNumberField: some View {
        // OK: entry with a real, correctly associated accessible name.
        VStack(alignment: .leading, spacing: 4) {
            Text("Ticket number")
                .font(.subheadline)
            TextField("e.g. 12345678", text: $ticketNumber)
                .textFieldStyle(.roundedBorder)
                .accessibilityLabel("Ticket number")
        }
    }

    private var plateNumberField: some View {
        // N6: a caption sits above the field, but the field's own placeholder
        // is empty and nothing associates the caption with it (no
        // .accessibilityLabel, no accessibilityElement(children: .combine)),
        // so VoiceOver announces the field with no name at all.
        VStack(alignment: .leading, spacing: 4) {
            Text("Plate number")
                .font(.subheadline)
            TextField("", text: $plateNumber)
                .textFieldStyle(.roundedBorder)
        }
    }

    // MARK: Help row

    private var helpRow: some View {
        HStack(spacing: 8) {
            Text("Need help?")
                .font(.subheadline)

            Spacer()

            // N3: icon button drawn at 20x20 -- well under Apple's 44 pt
            // guideline, and under WCAG 2.5.8's 24x24 CSS px minimum too.
            // SwiftUI's default hit-testing follows the frame you give it, so
            // there is no invisible padding to save this one.
            Button {
                // Clear action.
            } label: {
                Image(systemName: "xmark.circle.fill")
            }
            .frame(width: 20, height: 20)
            .accessibilityLabel("Clear form")
        }
    }

    // MARK: Pay button

    private var payButton: some View {
        // N5: visible text "Pay" but the accessible name is "Submit" (label in
        // name, WCAG 2.5.3).
        Button("Pay") {
            // Pay action.
        }
        .buttonStyle(.borderedProminent)
        .tint(Color(red: 0x1F / 255, green: 0x4E / 255, blue: 0x79 / 255))
        .frame(maxWidth: .infinity, minHeight: 50)
        .accessibilityLabel("Submit")
    }

    // MARK: Action row

    private var actionRow: some View {
        HStack(spacing: 16) {
            // N4: accessible name copied from a developer identifier instead
            // of a real label.
            Button {
                // Email receipt action.
            } label: {
                Image(systemName: "envelope")
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel("img_btn_email_receipt")

            // OK: text button whose visible title and accessible name match
            // (SwiftUI derives the name from a plain text label automatically).
            Button("Cancel") {
                // Cancel action.
            }
        }
    }

    // MARK: Icon row

    private var iconRow: some View {
        HStack(spacing: 16) {
            // OK: icon-only button with a correct accessibilityLabel, a normal
            // 44x44 target and the default tint (good contrast on white).
            Button {
                // Info action.
            } label: {
                Image(systemName: "info.circle")
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel("Payment information")

            // N8: icon-only control WITH a correct accessibilityLabel, but the
            // icon color has low contrast against its background (distinct
            // from N1/N4: this is a contrast bug, not a naming bug).
            Button {
                // Help action.
            } label: {
                Image(systemName: "questionmark.circle")
                    .foregroundColor(Color(white: 0.87))
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel("Help")
        }
    }

    // MARK: Late fees text

    private var lateFeesText: some View {
        // N7: a fixed point size instead of a Dynamic-Type-aware text style
        // (e.g. .body), so the text ignores the system text-size setting
        // (WCAG 1.4.4; only visible on a large-text rescan).
        Text("Late fees apply after 30 days.")
            .font(.system(size: 14))
            .foregroundColor(Color(white: 0.12))
    }

    // MARK: History button

    private var historyButton: some View {
        // OK: normal button, visible title matches its accessible name.
        Button("View payment history") {
            // Navigate to history.
        }
        .buttonStyle(.bordered)
        .frame(maxWidth: .infinity, minHeight: 50)
    }
}
