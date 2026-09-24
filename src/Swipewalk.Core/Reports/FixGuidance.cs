using Swipewalk.Core.Model;

namespace Swipewalk.Core.Reports;

/// <summary>
/// A suggested fix: what to change, an example in the app's framework or native API, and the settings
/// that commonly cause this kind of finding. Causes are likely, not certain: the scanner sees the running
/// app, not its source.
/// </summary>
public sealed record Fix(string Summary, string? Code = null, string? CodeLanguage = null)
{
    /// <summary>Framework name the causes apply to, e.g. ".NET MAUI" or "Android (Views and Compose)".</summary>
    public string? CausesFor { get; init; }

    public IReadOnlyList<string> LikelyCauses { get; init; } = [];
}

/// <summary>
/// Fix suggestions and likely causes per rule. Examples and causes use the detected framework (MAUI today)
/// and fall back to the platform's native APIs. To support a framework, add its column to each entry.
/// </summary>
public static class FixGuidance
{
    private sealed record Guidance(string Summary, Variant? Maui = null, Variant? Android = null, Variant? Ios = null);

    private sealed record Variant(string? Code, params string[] Causes);

    /// <summary>
    /// The verified keep-place pattern for a .NET MAUI Android app whose MainActivity leaves ConfigChanges.FontScale
    /// out of its ConfigurationChanges (the shape of a fix, not a drop-in snippet -- see
    /// <see cref="TextResizeLiveUpdateRule"/> and <see cref="TextResizeNavigationRule"/>): the activity restarts
    /// and the app rebuilds its window on a text-size change, and the re-created activity does read the new
    /// font scale, but the app returns to its first page unless it restores where the person was. Verified 2026-09-23 on the emulator
    /// with BuggyApp, .NET MAUI 10.0.110 (a NavigationPage app): remembering the current page (e.g. set in each
    /// page's OnAppearing) and pushing it again in App.CreateWindow after the root page is built keeps the same
    /// screen, with a brief flash of the first page; typed text, scroll position, page parameters, pages below
    /// it and modal pages are not restored by this alone; Shell apps were not tested. Not OnSaveInstanceState,
    /// which a MAUI app does not use this way.
    /// </summary>
    private const string MauiAndroidKeepPlaceCode =
        "// MainActivity.cs -- the shape of a fix, not a drop-in snippet.\n" +
        "// 1. Leave ConfigChanges.FontScale out of ConfigurationChanges (the .NET MAUI template's default). With it\n" +
        "//    listed, .NET MAUI keeps the old text size until the app is relaunched.\n" +
        "[Activity(..., ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |\n" +
        "    ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]\n" +
        "// 2. Android then restarts the activity on a text-size change and the app starts again on its first page.\n" +
        "//    To keep the person's place: remember which page is showing (e.g. in each page's OnAppearing) and, in\n" +
        "//    App.CreateWindow, push that page again after building the root page. Tested with a NavigationPage app\n" +
        "//    (the first page flashes briefly); not tested with Shell. This restores only the page -- not typed text,\n" +
        "//    scroll position, page parameters, the pages below it or modal pages; restore those yourself if needed.";

    /// <summary>
    /// Android code for the "text-resize" screen-level finding (text that did not reach the larger size at
    /// all): unlike <see cref="MauiAndroidKeepPlaceCode"/>, this is about whether text grows, not about where
    /// the app lands, so it names both the ConfigChanges.FontScale cause (text stuck at the old size) and
    /// FontAutoScalingEnabled -- and only points to the keep-place fix for the case where FontScale is absent
    /// and the app returns to its first page as a side effect.
    /// </summary>
    private const string MauiTextResizeScreenAndroidCode =
        "// MainActivity.cs: if ConfigurationChanges includes ConfigChanges.FontScale, remove it. With it listed,\n" +
        "// .NET MAUI keeps the old text size until the app is relaunched (it does not re-apply font sizes on the change).\n" +
        "// Without it, Android restarts the activity and text uses the new size (the app may then return to its\n" +
        "// first page; the user guide describes a way to keep the person's place, and its limits).\n" +
        "// Also check for FontAutoScalingEnabled=\"False\" on the text or in a Style.";

    public static Fix? For(Finding finding, Platform platform, AppFramework framework, string? frameworkVersion = null) =>
        Lookup(finding, platform, frameworkVersion) is { } guidance ? Select(guidance, platform, framework) : null;

    private static Guidance? Lookup(Finding finding, Platform platform, string? frameworkVersion)
    {
        string Detail(string key, string fallback) => finding.Details.GetValueOrDefault(key) ?? fallback;
        var visible = Detail("visibleText", "Pay");
        var suggested = Detail("suggestedForeground", "#767676");
        // Set by TextResizeRule only on iOS + MAUI screen-level findings; see MauiTextResizeScreenCauses.
        var largeTextRestartCaptured = finding.Details.GetValueOrDefault("largeTextRestartCaptured");

        return finding.RuleId switch
        {
            "missing-name" when finding.Role == "image" => new(
                "If the image conveys information, give it a short description. If it is decorative, hide it from assistive technology.",
                Maui: new("<Image Source=\"seal.png\" SemanticProperties.Description=\"City seal\" />\n<!-- decorative: -->\n<Image Source=\"divider.png\" AutomationProperties.IsInAccessibleTree=\"False\" />",
                    "No SemanticProperties.Description on an informative Image",
                    "Decorative image not marked AutomationProperties.IsInAccessibleTree=\"False\""),
                Android: new("android:contentDescription=\"@string/city_seal\"\n<!-- decorative: -->\nandroid:importantForAccessibility=\"no\"",
                    "ImageView without android:contentDescription",
                    "Compose Image with contentDescription = null for an informative image"),
                Ios: new("imageView.isAccessibilityElement = true\nimageView.accessibilityLabel = \"City seal\"",
                    "UIImageView is not an accessibility element by default",
                    "SwiftUI Image with no usable name (for example some Image(systemName:) symbols) and no .accessibilityLabel, or a decorative image not using Image(decorative:)")),

            "missing-name" when finding.Role == "textfield" => new(
                "Give the field an accessible name that matches its visible label.",
                Maui: new("<Entry SemanticProperties.Description=\"Plate number\" />",
                    "The visible Label above the Entry is not linked to it (AutomationProperties.LabeledBy is legacy and not reliable across platforms; set SemanticProperties.Description)",
                    "No SemanticProperties.Description and no Placeholder"),
                Android: new("<TextView android:labelFor=\"@id/plate\" android:text=\"Plate number\" />",
                    "No android:labelFor on the visible label, and no hint",
                    "Compose TextField without a label or Modifier.semantics { contentDescription }"),
                Ios: new("textField.accessibilityLabel = \"Plate number\"",
                    "UITextField without accessibilityLabel (the nearby UILabel is not associated)",
                    "SwiftUI TextField with an empty title")),

            "missing-name" => new(
                "Give the control a name that says what it does (for example \"Search\"), not what it looks like.",
                Maui: new("<ImageButton Source=\"search.png\" SemanticProperties.Description=\"Search\" />",
                    "ImageButton or icon Button without SemanticProperties.Description",
                    "Only AutomationId is set; it is never announced",
                    "Custom control built from a layout with a TapGestureRecognizer (no name or button role)"),
                Android: new("android:contentDescription=\"@string/search\"",
                    "ImageButton or icon-only view without android:contentDescription",
                    "Compose Icon/IconButton with contentDescription = null"),
                Ios: new("button.accessibilityLabel = \"Search\"",
                    "Image-only UIButton without accessibilityLabel",
                    "SwiftUI Button(action:) { Image(...) } without .accessibilityLabel")),

            "identifier-name" => new(
                "Replace the identifier with words users understand. Keep test identifiers in AutomationId / resource ids, which screen readers do not announce.",
                Maui: new($"<ImageButton SemanticProperties.Description=\"Email receipt\" AutomationId=\"{finding.Label}\" />",
                    "AutomationId value copied into SemanticProperties.Description",
                    "A localization key used as the description, or a missing translation showing the key (for example lbl_title)",
                    "Description bound to a property that holds an id rather than text"),
                Android: new("android:contentDescription=\"Email receipt\"\n<!-- keep the identifier in android:id or a test tag -->",
                    "contentDescription set to a resource or test id",
                    "Missing string resource translation, or a key shown instead of text"),
                Ios: new($"button.accessibilityLabel = \"Email receipt\"\nbutton.accessibilityIdentifier = \"{finding.Label}\"",
                    "accessibilityLabel set where accessibilityIdentifier was meant",
                    "NSLocalizedString key with no translation, so the key is shown",
                    "SwiftUI Image(\"asset_name\") read aloud by its asset name")),

            "label-in-name" => new(
                $"Make the accessible name start with the visible text \"{visible}\", or remove the override so the visible text is used.",
                Maui: new($"<Button Text=\"{visible}\" />\n<!-- or: SemanticProperties.Description=\"{visible} ticket\" -->",
                    "SemanticProperties.Description overrides Text with different wording",
                    "Text was changed or localized but Description was not"),
                Android: new($"android:contentDescription=\"{visible} ticket\"",
                    "contentDescription set on a view that already shows text",
                    "Compose Modifier.semantics { contentDescription } replacing the visible text"),
                Ios: new($"button.accessibilityLabel = \"{visible} ticket\"",
                    "accessibilityLabel overrides the button title with different wording",
                    "SwiftUI .accessibilityLabel with text that doesn't include the visible label")),

            "text-contrast" => new(
                $"Use a text color with at least 4.5:1 contrast against the background, for example {suggested} instead of {Detail("foreground", "the current color")}.",
                Maui: new($"<Label TextColor=\"{suggested}\" />",
                    "Hard-coded TextColor, or a style in Resources/Styles (the MAUI template sets Entry/Editor PlaceholderColor to Gray200 in the light theme)",
                    "AppThemeBinding with a light-theme value that is too pale",
                    "Opacity below 1 on the text or a parent"),
                Android: new($"android:textColor=\"{suggested}\"",
                    "Theme textColorSecondary/hint colors that are too light",
                    "alpha on the view or color resource"),
                Ios: new($"label.textColor = UIColor(named: \"TextPrimary\") // asset color set to {suggested}",
                    ".secondaryLabel/.tertiaryLabel or custom gray on a light background",
                    "alpha below 1 on the label or a superview")),

            "target-size" => new(
                finding.Kind == FindingKind.PlatformAdvisory
                    ? $"Enlarge the touch area to the platform minimum ({(platform == Platform.iOS ? "44×44 pt" : "48×48 dp")}); padding counts toward it."
                    : $"Enlarge the touch area to at least 24×24 CSS px (WCAG 2.5.8), or move neighbouring targets further apart. The examples use the platform guideline ({(platform == Platform.iOS ? "44×44 pt" : "48×48 dp")}), which is larger than WCAG requires.",
                Maui: new(platform == Platform.iOS
                        ? "<ImageButton MinimumWidthRequest=\"44\" MinimumHeightRequest=\"44\" Padding=\"12\" />"
                        : "<ImageButton MinimumWidthRequest=\"48\" MinimumHeightRequest=\"48\" Padding=\"12\" />",
                    "Small WidthRequest/HeightRequest with no MinimumWidthRequest/MinimumHeightRequest",
                    "Padding=\"0\" on a Button or ImageButton",
                    "A Label with a TapGestureRecognizer, whose target is only as big as its text"),
                Android: new("android:minWidth=\"48dp\"\nandroid:minHeight=\"48dp\"",
                    "Fixed layout_width/layout_height below 48dp",
                    "Compose Modifier.size below 48.dp without minimumInteractiveComponentSize()"),
                Ios: new("button.frame.size = CGSize(width: 44, height: 44) // or enlarge with content insets",
                    "Fixed width/height constraints below 44 pt",
                    "SwiftUI .frame smaller than 44 pt with no .padding (plus .contentShape) to enlarge the tappable area")),

            "text-resize" when finding.Role == "screen" => new(
                "Make text follow the system text size (at launch and while running), and check each screen at 200% text size.",
                Maui: MauiTextResizeScreenVariant(platform, largeTextRestartCaptured, frameworkVersion),
                Android: new(null,
                    "Text sizes in dp instead of sp",
                    "Activity handles fontScale in configChanges without re-laying out"),
                Ios: new("label.adjustsFontForContentSizeCategory = true",
                    "Labels without adjustsFontForContentSizeCategory = true",
                    "Fonts created with a fixed size instead of UIFont.preferredFont or UIFontMetrics")),

            "text-resize" => new(
                "Let the text grow: remove fixed heights on the text and its containers, and allow wrapping.",
                Maui: new("<Label Text=\"...\" LineBreakMode=\"WordWrap\" />  <!-- no HeightRequest -->",
                    "Fixed HeightRequest on the Label or a parent (Border, Frame, Grid row with an absolute height)",
                    "FontAutoScalingEnabled=\"False\"",
                    "MaxLines or LineBreakMode=\"TailTruncation\"/\"NoWrap\" cutting the text"),
                Android: new("android:layout_height=\"wrap_content\"",
                    "Fixed layout_height on the TextView or its parent",
                    "maxLines/ellipsize, or textSize in dp instead of sp",
                    "Compose Modifier.height fixed, maxLines = 1, or fontSize from dp"),
                Ios: new("label.numberOfLines = 0\nlabel.adjustsFontForContentSizeCategory = true",
                    "Fixed height constraint on the label or its container",
                    "numberOfLines = 1, or a font not created with UIFont.preferredFont/UIFontMetrics",
                    "SwiftUI .frame(height:), .lineLimit(1) or .font(.system(size:))")),

            "text-resize-live" => new(
                "Apply the system text-size change while the app is running, not only at the next launch.",
                Maui: new(platform == Platform.iOS
                        // Pure XML so the language label below (MAUI XAML) is accurate; the handler-mapping
                        // workaround for apps that can't update yet is listed as a cause instead, since Fix
                        // only carries one code snippet.
                        ? "<!-- Pin the version explicitly: new projects can resolve an older default -->\n<PropertyGroup>\n  <MauiVersion>10.0.110</MauiVersion>\n</PropertyGroup>"
                        : MauiAndroidKeepPlaceCode,
                    [
                        .. MauiTextResizeLiveIosCauses(frameworkVersion),
                        "iOS: if you can't update yet, a handler-mapping workaround (observed to fix live updates in our tests with MAUI 10.0.60; verify on your app): Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping(\"AdjustsFontForContentSizeCategory\", (handler, view) => handler.PlatformView.AdjustsFontForContentSizeCategory = true); repeat for ButtonHandler (TitleLabel), EntryHandler, EditorHandler platform views; or observe content-size-category changes (UIApplication.Notifications.ObserveContentSizeCategoryChanged) and re-apply FontSize on controls with FontAutoScalingEnabled",
                        "Android: MainActivity lists ConfigChanges.FontScale but does not update text sizes when the configuration change arrives, so text keeps its old size until relaunch",
                    ]),
                Ios: new("label.adjustsFontForContentSizeCategory = true\nlabel.font = UIFont.preferredFont(forTextStyle: .body)\n// or UIFontMetrics(forTextStyle: .body).scaledFont(for: baseFont)",
                    "UIKit labels not using adjustsFontForContentSizeCategory with preferredFont/UIFontMetrics, so they only read the content size category once",
                    "SwiftUI Text using a fixed .font(size:) instead of a Dynamic Type text style (for example .font(.body))"),
                Android: new("<!-- AndroidManifest.xml -->\n<activity android:configChanges=\"orientation|screenSize\" ... />\n<!-- remove fontScale, or handle onConfigurationChanged() and re-apply text sizes -->",
                    "The activity's android:configChanges includes fontScale but onConfigurationChanged() does not update text sizes")),

            "text-resize-navigation" => new(
                "Keep the person's place when the app restarts after a text-size change.",
                Maui: new(MauiAndroidKeepPlaceCode,
                    "MainActivity's ConfigurationChanges list omits ConfigChanges.FontScale, so the activity restarts and the app rebuilds its first page (App.CreateWindow) without remembering which page was showing"),
                Android: new(null,
                    "Restore the destination that was showing after a configuration-triggered restart -- for example NavController.saveState()/restoreState() with the Navigation component, a ViewModel (its state survives the restart) or, in Compose, rememberSaveable",
                    "The activity restarts on a font-scale change (android:configChanges does not list fontScale) and the app does not restore which screen was showing")),

            "page-titled" => new(
                "Set an Android accessibility pane title on the screen's root view so TalkBack has a title to announce for it.",
                Maui: new("#if ANDROID\n// In the page's HandlerChanged or OnAppearing -- MAUI's Page.Title shows in the toolbar\n// but does not set an Android pane title (seen on samples/BuggyApp's MainPage, which sets\n// Page.Title=\"Parking tickets\" yet still has none): set it explicitly.\nif (Handler?.PlatformView is Android.Views.View platformView)\n    AndroidX.Core.View.ViewCompat.SetAccessibilityPaneTitle(platformView, Title);\n#endif\n// Not tried with TalkBack yet -- confirm the announcement after applying this.",
                    "Page.Title (or Shell's title) is set, but nothing sets the Android accessibility pane title from it -- MAUI does not do this automatically"),
                Android: new("ViewCompat.setAccessibilityPaneTitle(rootView, \"Parking tickets\")\n// Setting the Activity's own title instead sets the window title (TalkBack also announces\n// that), but this check reads only the pane title, so the finding would still fire.",
                    "No AccessibilityPaneTitle set on the screen's (fragment/pane) root view")),

            "input-purpose" => new(
                "Declare the field's autofill/content-type hint so autofill and personalization tools can identify it.",
                Maui: new("<!-- Keyboard picks the on-screen keyboard layout only; it does not declare the field's purpose. -->\n<Entry Keyboard=\"Email\" />\n<!-- Swipewalk found no cross-platform MAUI property for this; set it per platform through a handler: -->\n#if ANDROID\nhandler.PlatformView.SetAutofillHints(Android.Views.View.AutofillHintEmailAddress);\n#elif IOS\nhandler.PlatformView.TextContentType = UIKit.UITextContentType.EmailAddress;\n#endif",
                    "No autofill hint set for a field that collects the user's own information"),
                Android: new("editText.setAutofillHints(View.AUTOFILL_HINT_EMAIL_ADDRESS)",
                    "No autofillHints set on an EditText/TextInputEditText that collects personal information"),
                Ios: new("textField.textContentType = .emailAddress",
                    "No textContentType set on a UITextField/SwiftUI TextField that collects personal information")),

            "icon-contrast" => new(
                "Use an icon color with at least 3:1 contrast against its background.",
                Ios: new("button.tintColor = UIColor(named: \"IconPrimary\") // pick a color that reaches 3:1",
                    "Icon tint color too close to its background",
                    "A semi-transparent icon or an icon over a photo/gradient background")),

            "large-text-lost-content" => new(
                "Make the screen scrollable so content that no longer fits at a larger text size can still be reached, instead of being cut off with no way back to it.",
                Maui: new("<ScrollView>\n    <VerticalStackLayout Padding=\"20,12\" Spacing=\"12\">\n        <!-- page content -->\n    </VerticalStackLayout>\n</ScrollView>",
                    "The page's content is not inside a ScrollView (a plain VerticalStackLayout/Grid directly under ContentPage.Content) -- MAUI does not add scrolling on its own, so content pushed past the screen edge at a larger text size has no way back",
                    "A ScrollView is present but its parent doesn't constrain its height (for example a VerticalStackLayout, or a Grid row set to Auto) -- the ScrollView then grows to fit its content instead of scrolling it, so it never reports itself scrollable"),
                Android: new("<ScrollView android:layout_width=\"match_parent\" android:layout_height=\"match_parent\">\n    <!-- content that can overflow -->\n</ScrollView>",
                    "Content laid out directly in a FrameLayout/ConstraintLayout with no ScrollView/NestedScrollView ancestor",
                    "A ScrollView ancestor exists but its own height isn't limited by its parent (for example wrap_content inside an unbounded parent) -- it grows with its content instead of scrolling it, so it never reports scrollable=true"),
                Ios: new("// Embed the content in a UIScrollView (or SwiftUI ScrollView) whose content size follows its content\nlet scroll = UIScrollView()\nscroll.translatesAutoresizingMaskIntoConstraints = false\nscroll.addSubview(contentView)\n// pin contentView's edges to scroll.contentLayoutGuide (lets it scroll vertically)\n// and its width to scroll.frameLayoutGuide (so only height, not width, can overflow)",
                    "Content pinned to a fixed-size container (no UIScrollView/SwiftUI ScrollView) so content past the screen edge at Dynamic Type's larger accessibility sizes cannot be scrolled to")),

            "engine:dynamicType" => new(
                "Check the screen with the largest text size (Settings > Accessibility > Display & Text Size). Text should grow and must not be cut off.",
                Maui: new("<!-- FontAutoScalingEnabled is True by default; avoid fixed HeightRequest on text containers -->\n<Label FontAutoScalingEnabled=\"True\" />",
                    "FontAutoScalingEnabled=\"False\"",
                    "MAUI scales fonts itself when views are created, which Apple's audit may report as unsupported Dynamic Type; confirm by relaunching at a large size"),
                Ios: new("label.font = UIFont.preferredFont(forTextStyle: .body)\nlabel.adjustsFontForContentSizeCategory = true",
                    "Fonts created with a fixed size",
                    "adjustsFontForContentSizeCategory not set")),

            _ => null,
        };
    }

    /// <summary>
    /// iOS + MAUI causes for the "text-resize-live" finding, selected by whether the app's own MAUI version is
    /// known and whether it is before or at/after the version that fixed dotnet/maui#34445 (see
    /// <see cref="FrameworkVersions"/>). Unknown keeps the original wording (can't rule out either cause).
    /// </summary>
    private static string[] MauiTextResizeLiveIosCauses(string? frameworkVersion)
    {
        var version = FrameworkVersions.Parse(frameworkVersion);
        if (version is null)
            return
            [
                "iOS: before Microsoft.Maui.Controls 10.0.100, a known issue, fixed in 10.0.100 (dotnet/maui#34445)",
                "iOS: new projects can resolve an older Microsoft.Maui.Controls than the latest release unless <MauiVersion> is set explicitly in the project file",
                "iOS: Swipewalk can't read the app's MAUI version -- check Microsoft.Maui.Controls in the project file",
                "iOS: if the app already uses 10.0.100 or later, a custom handler or code that sets FontSize directly",
            ];
        return version >= FrameworkVersions.MauiLiveTextSizeFix
            ?
            [
                $"iOS: this app uses .NET MAUI {frameworkVersion}, which includes the fix for live text-size changes (dotnet/maui#34445); a custom handler or code that sets FontSize directly is the likely cause",
            ]
            :
            [
                $"iOS: this app uses .NET MAUI {frameworkVersion}, an older version affected by a known issue, fixed in 10.0.100 (dotnet/maui#34445); updating Microsoft.Maui.Controls to 10.0.100 or later should make standard controls apply the new size while the app runs; rescan to confirm",
            ];
    }

    /// <summary>
    /// Maui variant for the "text-resize" screen-level finding (mirrors <c>TextResizeRule</c>'s message):
    /// the Android causes are the same either way, but the iOS causes depend on whether the scan confirmed
    /// a restart doesn't help (<paramref name="largeTextRestartCaptured"/> == "true", set in
    /// <c>Finding.Details["largeTextRestartCaptured"]</c> by <c>TextResizeRule</c> only on iOS + MAUI) or
    /// couldn't tell, and (when it couldn't tell) on whether the app's own MAUI version is known -- see
    /// <see cref="FrameworkVersions"/>. Case 2 (<paramref name="largeTextRestartCaptured"/> == "true") does not
    /// depend on the version: it already only lists causes that would not grow even after a restart, whichever
    /// MAUI version is in use.
    /// </summary>
    private static Variant MauiTextResizeScreenVariant(Platform platform, string? largeTextRestartCaptured, string? frameworkVersion)
    {
        var iosCauses = largeTextRestartCaptured == "true"
            ? new[]
              {
                  "iOS: FontAutoScalingEnabled=\"False\" on this text, so it never scales",
                  "iOS: a control .NET MAUI does not scale automatically, for example a Shell tab bar title (unscaled in every version we tested)",
              }
            : MauiTextResizeScreenCouldNotConfirmIosCauses(frameworkVersion);
        var causes = new[]
        {
            "Android: FontAutoScalingEnabled=\"False\" on this text or in a Style",
            "Android: ConfigChanges.FontScale listed but font sizes not re-applied, so text keeps its old size",
        }.Concat(iosCauses).ToArray();
        var code = platform == Platform.Android
            ? MauiTextResizeScreenAndroidCode
            : "// Re-apply font sizes in UIApplication.Notifications.ObserveContentSizeCategoryChanged(...)";
        return new(code, causes);
    }

    /// <summary>
    /// iOS + MAUI causes for the "text-resize" screen-level finding when a restart wasn't confirmed to help or
    /// not, selected the same way as <see cref="MauiTextResizeLiveIosCauses"/>: unknown version keeps the
    /// original wording (can't rule out the pre-10.0.100 restart issue); a known version at or after the fix
    /// rules it out; a known version before it keeps both possible causes, since the restart outcome here is
    /// unconfirmed either way.
    /// </summary>
    private static string[] MauiTextResizeScreenCouldNotConfirmIosCauses(string? frameworkVersion)
    {
        var version = FrameworkVersions.Parse(frameworkVersion);
        if (version is null)
            return
            [
                "iOS: before Microsoft.Maui.Controls 10.0.100, a known issue where text only grows after a restart, fixed in " +
                "10.0.100 (dotnet/maui#34445) -- the scan could not confirm whether a restart would help here",
                "iOS: FontAutoScalingEnabled=\"False\" on this text, or a control MAUI does not scale automatically (for example " +
                "a Shell tab bar title), which would not grow even after a restart",
            ];
        return version >= FrameworkVersions.MauiLiveTextSizeFix
            ?
            [
                $"iOS: this app uses .NET MAUI {frameworkVersion}, which includes the fix for live text-size changes (dotnet/maui#34445); " +
                "the scan could not confirm whether a restart would help here, so FontAutoScalingEnabled=\"False\" on this text, or a " +
                "control MAUI does not scale automatically (for example a Shell tab bar title), is the likely cause",
            ]
            :
            [
                $"iOS: this app uses .NET MAUI {frameworkVersion}, an older version affected by a known issue where text only grows " +
                "after a restart, fixed in 10.0.100 (dotnet/maui#34445) -- the scan could not confirm whether a restart would help here",
                "iOS: FontAutoScalingEnabled=\"False\" on this text, or a control MAUI does not scale automatically (for example " +
                "a Shell tab bar title), which would not grow even after a restart",
            ];
    }

    private static Fix Select(Guidance g, Platform platform, AppFramework framework)
    {
        var (variant, language, causesFor) = framework == AppFramework.Maui && g.Maui is not null
            ? (g.Maui, "MAUI XAML", ".NET MAUI")
            : platform switch
            {
                Platform.Android => (g.Android, "Android XML", "Android (Views and Compose)"),
                Platform.iOS => (g.Ios, "Swift (UIKit)", "iOS (UIKit and SwiftUI)"),
                _ => (null, null, null),
            };

        // Causes prefixed with a platform ("Android: ...") only apply there.
        var other = platform == Platform.Android ? "iOS:" : "Android:";
        var causes = (variant?.Causes ?? []).Where(c => !c.StartsWith(other, StringComparison.Ordinal)).ToList();
        if (language == "MAUI XAML" && variant?.Code is { } code && !code.TrimStart().StartsWith('<'))
            language = "MAUI C#";

        return new Fix(g.Summary, variant?.Code, variant?.Code is null ? null : language)
        {
            CausesFor = causes.Count > 0 ? causesFor : null,
            LikelyCauses = causes,
        };
    }
}
