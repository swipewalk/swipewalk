# Disclaimer and terms of use

Swipewalk is free, open-source software. You may use, copy, modify and distribute it, including
for commercial work, under the [Apache License 2.0](LICENSE). This page explains what the results
mean and what you are responsible for. It does not add to or change the license.

## What the results are, and are not

- **Automated checks find only some accessibility issues.** Many WCAG success criteria cannot be
  tested automatically, and the checks that exist test only parts of the criteria they map to.
- **A report is not a statement of conformance.** No result from Swipewalk, including a report
  with no findings, shows that an app conforms to WCAG, meets Section 508, EN 301 549, the ADA or any
  other law, standard or contract. Manual testing by people using assistive technology is still
  required.
- **Screen-reader output is predicted** from the accessibility tree. It is not a recording of
  TalkBack, VoiceOver or Narrator.
- **"Relevant to" a law or standard is not legal advice.** Swipewalk labels a finding with the
  laws and standards whose WCAG version and level include its success criterion. Which requirements
  apply to you depends on your jurisdiction, contracts, exceptions and dates. Ask a qualified
  lawyer.
- **Findings can be wrong.** False positives and missed issues happen; see
  [known limitations](docs/limitations.md). Please [report them](https://github.com/swipewalk/swipewalk/issues/new/choose).

## Your responsibilities

- **Scan only apps you are allowed to test.** Scanning reads another app's on-screen content and
  accessibility tree and takes screenshots. Make sure you have permission from the app's owner, and
  follow the app's and the app store's terms.
- **Handle reports as sensitive.** Screenshots and accessibility trees can contain personal data
  that was on screen (names, account details, messages). Use test accounts and test data where you
  can, and review a report before sharing it. Swipewalk blanks status bars, but not app content.
- **Keep the device safe.** Scans can change device settings temporarily (for example the text size
  during a large-text check) and restore them afterwards. Use test devices rather than personal ones
  where you can.

## No warranty

As the [license](LICENSE) says (sections 7 and 8), Swipewalk is provided "as is", without
warranties or conditions of any kind, and the authors are not liable for damages arising from its
use, including decisions made on the basis of its results.

## Names and trademarks

WCAG is a W3C standard. Android is a trademark of Google LLC; iOS, iPhone, VoiceOver and Xcode are
trademarks of Apple Inc.; Windows and .NET MAUI are trademarks of Microsoft Corporation. They are
used here only to describe compatibility. Swipewalk is not affiliated with or endorsed by these
companies or by the W3C. The license does not grant rights to the Swipewalk name (Apache License
2.0, section 6).

The sample app in `samples/BuggyApp` belongs to the fictional "City of Exampleville". Any
resemblance to a real government or its apps is unintended.
