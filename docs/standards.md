# Standards and laws

<!-- Generated from src/Swipewalk.Core/Standards/KnownStandards.cs.
     Regenerate: dotnet run --project src/Swipewalk.Cli -- standards > docs/standards.md -->

"Relevant to" means a finding's WCAG criterion is within the WCAG version and level that the standard references. It is not legal advice or a legal conclusion. Which requirements apply to you depends on your jurisdiction, contracts and the standard's own exceptions and additional requirements.

| Standard | Jurisdiction | Based on | Applies to | Checked |
|---|---|---|---|---|
| [ADA Title II (DOJ 2024 rule)](https://www.ada.gov/resources/2024-03-08-web-rule/) (`ada-title-ii`) | United States | WCAG 2.1 AA | Web content and mobile apps that state and local governments provide or make available, directly or through contracts, licenses or other arrangements. | 2026-09 |
| [Section 508 (Revised 508 Standards)](https://www.access-board.gov/ict/) (`section-508`) | United States (federal) | WCAG 2.0 AA | Information and communication technology that federal agencies develop, procure, maintain or use, including software and mobile apps. | 2026-09 |
| [EN 301 549 v3.2.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf) (`en-301-549`) | European Union (and EEA) | WCAG 2.1 AA | Version cited for the Web Accessibility Directive (public sector websites and mobile apps). Clause 11 applies WCAG 2.1 to non-web software, including mobile apps. ETSI published v4.1.1 (aligned with WCAG 2.2) in September 2026; it confers a presumption of conformity only once cited in the EU Official Journal. Check which version your contract or regulator references. | 2026-09 |
| [EN 301 549 v4.1.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/04.01.01_60/en_301549v040101p.pdf) (`en-301-549-v4`) | European Union (and EEA) | WCAG 2.2 AA | Published by ETSI in September 2026, with clauses 9 to 11 aligned to WCAG 2.2. It confers a presumption of conformity only once cited in the EU Official Journal; that citation was not verified when this entry was checked. Check which version your contract or regulator references. | 2026-09 |
| [Public Sector Bodies (Websites and Mobile Applications) (No. 2) Accessibility Regulations 2018](https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps) (`uk-public-sector`) | United Kingdom | WCAG 2.2 AA | Public sector websites, and mobile apps developed for use by the public (apps for specific groups such as employees or students are not covered). The regulations do not name a WCAG version; GOV.UK guidance tells public sector bodies to meet WCAG 2.2 AA. | 2026-09 |

## Rule sources (ruleset 2026.09.22)

Reports record these versions. Later changes to WCAG, laws or platform guidelines are not reflected until Swipewalk is updated; reports warn when a mapping was last reviewed more than a year earlier.

| Source | Version | Mapping reviewed |
|---|---|---|
| [WCAG](https://www.w3.org/TR/WCAG22/) | 2.2 (W3C Recommendation) | 2026-09 |
| [WCAG2ICT: Guidance on Applying WCAG 2 to Non-Web Information and Communications Technologies](https://www.w3.org/TR/wcag2ict-22/) | W3C Group Note, 11 December 2025 (WCAG 2.2) | 2026-09 |
| [Apple Human Interface Guidelines: accessibility (44×44 pt hit targets)](https://developer.apple.com/design/human-interface-guidelines/accessibility) | current web edition | 2026-09 |
| [Android accessibility guidance (48×48 dp touch targets)](https://support.google.com/accessibility/android/answer/7101858) | current web edition | 2026-09 |
| [Apple Human Interface Guidelines: typography (support Dynamic Type, including accessibility sizes)](https://developer.apple.com/design/human-interface-guidelines/typography) | current web edition | 2026-09 |
| [Android developer documentation: activity element configChanges (including fontScale)](https://developer.android.com/guide/topics/manifest/activity-element#config) | current web edition | 2026-09 |
| [Android developer documentation: handle configuration changes](https://developer.android.com/guide/topics/resources/runtime-changes) | current web edition | 2026-09 |
| [dotnet/maui PR #34445: iOS fix for font autoscaling not updating in realtime](https://github.com/dotnet/maui/pull/34445) | merged 2026-06-23; released in Microsoft.Maui.Controls 10.0.100 (.NET 10 SR10) | 2026-09 |
| [ADA Title II (DOJ 2024 rule)](https://www.ada.gov/resources/2024-03-08-web-rule/) | WCAG 2.1 AA | 2026-09 |
| [Section 508 (Revised 508 Standards)](https://www.access-board.gov/ict/) | WCAG 2.0 AA | 2026-09 |
| [EN 301 549 v3.2.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf) | WCAG 2.1 AA | 2026-09 |
| [EN 301 549 v4.1.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/04.01.01_60/en_301549v040101p.pdf) | WCAG 2.2 AA | 2026-09 |
| [Public Sector Bodies (Websites and Mobile Applications) (No. 2) Accessibility Regulations 2018](https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps) | WCAG 2.2 AA | 2026-09 |

## Exceptions and requirements beyond WCAG (not checked)

- **ADA Title II (DOJ 2024 rule):** The rule has exceptions (archived web content, preexisting conventional electronic documents, some third-party content, individualized password-protected documents, preexisting social media posts) that automated checks cannot evaluate.
- **Section 508 (Revised 508 Standards):** Applies WCAG 2.0 to non-web software with exceptions (E207.2: 2.4.1, 2.4.5, 3.2.3, 3.2.4 and complete processes do not apply). Chapter 5 adds software requirements beyond WCAG (for example 502 interoperability with assistive technology and 503.2 user preferences), which are not checked.
- **EN 301 549 v3.2.1:** Clause 11 applies WCAG to non-web software; 2.4.1, 2.4.2, 2.4.5, 3.1.2, 3.2.3 and 3.2.4 are void there, and closed functionality has separate requirements. Clauses 5 and 11 add requirements beyond WCAG (for example user preferences, assistive technology interoperability and biometrics), which are not checked.
- **EN 301 549 v4.1.1:** Clause 11 applies WCAG 2.2 to non-web software; 2.4.1, 2.4.5, 3.1.2, 3.2.3 and 3.2.6 are void there, 2.4.2 applies as "non-web software titled" and 3.2.4 applies to the software as a whole. Clauses 5 and 11 add requirements beyond WCAG, which are not checked.
- **Public Sector Bodies (Websites and Mobile Applications) (No. 2) Accessibility Regulations 2018:** The regulations also require an accessibility statement and list exemptions (for example some organisations and content types), which are not evaluated.

## Criteria Swipewalk maps findings to

| Criterion | Since | ada-title-ii | section-508 | en-301-549 | en-301-549-v4 | uk-public-sector |
|---|---|---|---|---|---|---|
| 1.1.1 Non-text Content (A) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 1.4.3 Contrast (Minimum) (AA) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 1.4.4 Resize Text (AA) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 1.4.11 Non-text Contrast (AA) | WCAG 2.1 | yes | — | yes | yes | yes |
| 2.4.3 Focus Order (A) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 2.4.6 Headings and Labels (AA) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 2.5.3 Label in Name (A) | WCAG 2.1 | yes | — | yes | yes | yes |
| 2.5.8 Target Size (Minimum) (AA) | WCAG 2.2 | — | — | — | yes | yes |
| 4.1.2 Name, Role, Value (A) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 1.3.2 Meaningful Sequence (A) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 2.4.2 Page Titled (A) | WCAG 2.0 | yes | yes | — | yes | yes |
| 2.4.4 Link Purpose (In Context) (A) | WCAG 2.0 | yes | yes | yes | yes | yes |
| 1.3.5 Identify Input Purpose (AA) | WCAG 2.1 | yes | — | yes | yes | yes |
| 1.4.10 Reflow (AA) | WCAG 2.1 | yes | — | yes | yes | yes |
