# Disclaimer and terms of use

Swipewalk is free to use, including for commercial work, under the [Swipewalk License](LICENSE).
(Versions 0.1.0 to 0.4.1 were released under the Apache License 2.0 and stay under it.) This page
explains what the results mean and what you are responsible for. It does not add to or change the
licence.

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
  can, and review a report before sharing it. A shared `.swipewalk` file is not encrypted and holds all the text Swipewalk read on each screen, so review it too. Swipewalk blanks status bars, but not app content.
- **Keep the device safe.** Scans can change device settings temporarily (for example the text size
  during a large-text check) and restore them afterwards. Use test devices rather than personal ones
  where you can.

## No warranty

As the [licence](LICENSE) says (section 6), Swipewalk is provided "as is", without
warranties of any kind, and, as far as the law allows, the authors are not liable for damages arising
from its use, including decisions made on the basis of its results.

## Names and trademarks

WCAG is a W3C standard. Android is a trademark of Google LLC; iOS, iPhone, VoiceOver and Xcode are
trademarks of Apple Inc.; Windows and .NET MAUI are trademarks of Microsoft Corporation. They are
used here only to describe compatibility. Swipewalk is not affiliated with or endorsed by these
companies or by the W3C. The licence does not grant rights to the Swipewalk name beyond fairly
naming the software (Swipewalk License, section 10).

Swipewalk's sample app BuggyApp belongs to the fictional "City of Exampleville". Any
resemblance to a real government or its apps is unintended.
