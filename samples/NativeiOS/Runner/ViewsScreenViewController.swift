import UIKit

/// Ground-truth screen for Swipewalk, built the idiomatic UIKit way (programmatic
/// views, no storyboard). Bugs are deliberate; each is tagged with an ID (N1..)
/// matching ground-truth.uikit.json. Elements tagged OK are negative controls.
final class ViewsScreenViewController: UIViewController {
    override func viewDidLoad() {
        super.viewDidLoad()
        title = "Pay a parking ticket"
        view.backgroundColor = .white
        buildLayout()
    }

    private func buildLayout() {
        let scrollView = UIScrollView()
        scrollView.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(scrollView)

        let stack = UIStackView()
        stack.axis = .vertical
        stack.spacing = 16
        stack.translatesAutoresizingMaskIntoConstraints = false
        scrollView.addSubview(stack)

        NSLayoutConstraint.activate([
            scrollView.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor),
            scrollView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            scrollView.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            scrollView.bottomAnchor.constraint(equalTo: view.bottomAnchor),

            stack.topAnchor.constraint(equalTo: scrollView.topAnchor, constant: 20),
            stack.leadingAnchor.constraint(equalTo: scrollView.leadingAnchor, constant: 20),
            stack.trailingAnchor.constraint(equalTo: scrollView.trailingAnchor, constant: -20),
            stack.bottomAnchor.constraint(equalTo: scrollView.bottomAnchor, constant: -20),
            stack.widthAnchor.constraint(equalTo: scrollView.widthAnchor, constant: -40),
        ])

        stack.addArrangedSubview(makeHeaderRow())
        stack.addArrangedSubview(makeHelperTextLabel())
        stack.addArrangedSubview(makeTicketNumberField())
        stack.addArrangedSubview(makePlateNumberField())
        stack.addArrangedSubview(makeHelpRow())
        stack.addArrangedSubview(makePayButton())
        stack.addArrangedSubview(makeActionRow())
        stack.addArrangedSubview(makeIconRow())
        stack.addArrangedSubview(makeLateFeesLabel())
        stack.addArrangedSubview(makeHistoryButton())
    }

    // MARK: Header

    private func makeHeaderRow() -> UIView {
        // OK: heading, real text.
        let cityLabel = UILabel()
        cityLabel.text = "City of Exampleville"
        cityLabel.font = UIFontMetrics(forTextStyle: .title2).scaledFont(for: .systemFont(ofSize: 22, weight: .semibold))
        cityLabel.adjustsFontForContentSizeCategory = true
        cityLabel.accessibilityTraits.insert(.header)
        cityLabel.setContentHuggingPriority(.defaultLow, for: .horizontal)

        // N1: icon-only search button with no accessible name. A plain UIButton
        // built from an SF Symbol image doesn't get a name for free; forgetting
        // accessibilityLabel here is the same slip as MAUI's B2 icon button.
        let searchButton = UIButton(type: .system)
        searchButton.setImage(UIImage(systemName: "magnifyingglass"), for: .normal)
        searchButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            searchButton.widthAnchor.constraint(equalToConstant: 44),
            searchButton.heightAnchor.constraint(equalToConstant: 44),
        ])

        let row = UIStackView(arrangedSubviews: [cityLabel, searchButton])
        row.axis = .horizontal
        row.alignment = .center
        row.spacing = 12
        return row
    }

    // MARK: Helper text

    private func makeHelperTextLabel() -> UIView {
        // N2: low-contrast helper text (light gray on white, about 2.3:1).
        let label = UILabel()
        label.text = "Payments post within 2 business days."
        label.font = .preferredFont(forTextStyle: .footnote)
        label.adjustsFontForContentSizeCategory = true
        label.textColor = UIColor(white: 0.67, alpha: 1)
        label.numberOfLines = 0
        return label
    }

    // MARK: Text fields

    private func makeTicketNumberField() -> UIView {
        // OK: entry with a real, correctly associated accessible name.
        let caption = UILabel()
        caption.text = "Ticket number"
        caption.font = .preferredFont(forTextStyle: .subheadline)
        caption.adjustsFontForContentSizeCategory = true

        let field = UITextField()
        field.borderStyle = .roundedRect
        field.placeholder = "e.g. 12345678"
        field.accessibilityLabel = "Ticket number"

        let stack = UIStackView(arrangedSubviews: [caption, field])
        stack.axis = .vertical
        stack.spacing = 4
        return stack
    }

    private func makePlateNumberField() -> UIView {
        // N6: a caption label sits above the field, but nothing wires it up
        // (no placeholder, no accessibilityLabel, not in an accessibilityElements
        // list) so VoiceOver announces the field with no name at all.
        let caption = UILabel()
        caption.text = "Plate number"
        caption.font = .preferredFont(forTextStyle: .subheadline)
        caption.adjustsFontForContentSizeCategory = true

        let field = UITextField()
        field.borderStyle = .roundedRect

        let stack = UIStackView(arrangedSubviews: [caption, field])
        stack.axis = .vertical
        stack.spacing = 4
        return stack
    }

    // MARK: Help row

    private func makeHelpRow() -> UIView {
        let helpText = UILabel()
        helpText.text = "Need help?"
        helpText.font = .preferredFont(forTextStyle: .subheadline)
        helpText.adjustsFontForContentSizeCategory = true
        helpText.setContentHuggingPriority(.defaultLow, for: .horizontal)

        // N3: icon button drawn at 20x20 -- well under Apple's 44 pt guideline,
        // and under WCAG 2.5.8's 24x24 CSS px minimum too. Unlike a MAUI
        // ImageButton, a plain UIButton gets no invisible hit-area padding, so
        // the drawn frame is the whole tappable area.
        let clearButton = UIButton(type: .system)
        clearButton.setImage(UIImage(systemName: "xmark.circle.fill"), for: .normal)
        clearButton.accessibilityLabel = "Clear form"
        clearButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            clearButton.widthAnchor.constraint(equalToConstant: 20),
            clearButton.heightAnchor.constraint(equalToConstant: 20),
        ])

        let row = UIStackView(arrangedSubviews: [helpText, clearButton])
        row.axis = .horizontal
        row.alignment = .center
        row.spacing = 8
        return row
    }

    // MARK: Pay button

    private func makePayButton() -> UIView {
        // N5: visible text "Pay" but the accessible name is "Submit" (label in
        // name, WCAG 2.5.3) -- a common slip when a button's accessibilityLabel
        // is copied from an older spec instead of matching the shipped title.
        var config = UIButton.Configuration.filled()
        config.title = "Pay"
        config.baseBackgroundColor = UIColor(red: 0x1F / 255, green: 0x4E / 255, blue: 0x79 / 255, alpha: 1)
        let button = UIButton(configuration: config)
        button.accessibilityLabel = "Submit"
        button.translatesAutoresizingMaskIntoConstraints = false
        button.heightAnchor.constraint(equalToConstant: 50).isActive = true
        return button
    }

    // MARK: Action row

    private func makeActionRow() -> UIView {
        // N4: accessible name copied from a developer identifier instead of a
        // real label.
        let emailButton = UIButton(type: .system)
        emailButton.setImage(UIImage(systemName: "envelope"), for: .normal)
        emailButton.accessibilityLabel = "img_btn_email_receipt"
        emailButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            emailButton.widthAnchor.constraint(equalToConstant: 44),
            emailButton.heightAnchor.constraint(equalToConstant: 44),
        ])

        // OK: text button whose visible title and accessible name match (UIKit
        // derives the name from the title automatically; nothing overrides it).
        let cancelButton = UIButton(type: .system)
        cancelButton.setTitle("Cancel", for: .normal)

        let row = UIStackView(arrangedSubviews: [emailButton, cancelButton])
        row.axis = .horizontal
        row.spacing = 16
        row.alignment = .center
        return row
    }

    // MARK: Icon row

    private func makeIconRow() -> UIView {
        // OK: icon-only button with a correct accessibilityLabel, a normal
        // 44x44 target and the default system tint (good contrast on white).
        let infoButton = UIButton(type: .system)
        infoButton.setImage(UIImage(systemName: "info.circle"), for: .normal)
        infoButton.accessibilityLabel = "Payment information"
        infoButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            infoButton.widthAnchor.constraint(equalToConstant: 44),
            infoButton.heightAnchor.constraint(equalToConstant: 44),
        ])

        // N8: icon-only control WITH a correct accessibilityLabel, but the icon
        // itself is tinted too light to read against the white background
        // (distinct from N1/N4: this is a contrast bug, not a naming bug).
        let helpButton = UIButton(type: .system)
        helpButton.setImage(UIImage(systemName: "questionmark.circle"), for: .normal)
        helpButton.tintColor = UIColor(white: 0.87, alpha: 1)
        helpButton.accessibilityLabel = "Help"
        helpButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            helpButton.widthAnchor.constraint(equalToConstant: 44),
            helpButton.heightAnchor.constraint(equalToConstant: 44),
        ])

        let row = UIStackView(arrangedSubviews: [infoButton, helpButton])
        row.axis = .horizontal
        row.spacing = 16
        row.alignment = .center
        return row
    }

    // MARK: Late fees text

    private func makeLateFeesLabel() -> UIView {
        // N7: fixed 14pt font with adjustsFontForContentSizeCategory left at
        // its default (false), so the text ignores the system Dynamic Type
        // setting (WCAG 1.4.4; only visible on a large-text rescan). The fix
        // is UIFontMetrics + adjustsFontForContentSizeCategory = true, as used
        // on the other labels in this screen.
        let label = UILabel()
        label.text = "Late fees apply after 30 days."
        label.font = UIFont.systemFont(ofSize: 14)
        label.textColor = UIColor(white: 0.12, alpha: 1)
        label.numberOfLines = 0
        return label
    }

    // MARK: History button

    private func makeHistoryButton() -> UIView {
        // OK: normal button, visible title matches its accessible name.
        var config = UIButton.Configuration.gray()
        config.title = "View payment history"
        let button = UIButton(configuration: config)
        button.translatesAutoresizingMaskIntoConstraints = false
        button.heightAnchor.constraint(equalToConstant: 50).isActive = true
        return button
    }
}
