# results.json reference

Every scan or recording saves a `results.json` in its run folder next to the HTML report. This page describes
what other tools can rely on. Swipewalk itself, the desktop app and the CLI export all read the same file.

Automated checks find only some accessibility issues; a finding is not a statement of conformance and manual
testing is still required. The file says so in its `notice` field.

## Versions and older files

`schemaVersion` is a string such as `"0.8"`. Since schema 0.4, changes have only added fields; earlier schema versions renamed or removed some summary fields, which a reader should ignore. A reader should:

- read `schemaVersion` first and stop with a plain message if it is newer than the version the tool was written for and the tool relies on a field whose meaning may have changed;
- treat any field it does not find as absent (files saved by earlier versions simply lack the newer fields);
- ignore fields it does not know.

Schema 0.8 added `sourceRootName` and, on every finding, `findingId`, `affects` and `reportAnchor`. A 0.7
file has none of them and still loads. Swipewalk recomputes `findingId`, `affects` and `reportAnchor` each
time it saves a run and ignores them when reading, so editing them by hand has no effect. `sourceRootName`
is recorded when the run is saved. Schema 0.8 also documents `myLaws`, which Swipewalk 0.4.1 had already started
writing under 0.7: a 0.7 file may or may not have it, and a 0.8 file has it whenever the person chose laws.

## Top level

| Field | Meaning |
|---|---|
| `schemaVersion`, `toolVersion`, `generatedAt` | Which format and Swipewalk version wrote the file, and when. |
| `appId` | Android package or iOS bundle id, when known. |
| `sourceRootName` | The name of the `--source` folder used for source mapping (added in schema 0.8). Only the folder's own name as typed, for example `"MyApp"`, never its full path, so a shared file carries no home directory. Absent when no source folder was given. |
| `myLaws[]` | The ids of the laws and standards the person chose to see first ("Laws that matter to me"). Reports and exports list these first; nothing is left out. Absent when none were chosen. A tool that shows laws can list these first too. Written by Swipewalk 0.4.1 and later; documented from schema 0.8. |
| `screens[]` | One entry per scanned screen (below). |
| `coverage`, `groups`, `limitations`, `standards` and others | Derived summaries; rely on `screens` for the facts. |

## Findings: `screens[].findings[]`

| Field | Meaning |
|---|---|
| `ruleId`, `kind`, `message` | The check, its kind (`wcagIssue`, `needsReview` or `platformAdvisory`) and the plain sentence. |
| `criteria[]` | WCAG 2.2 success criteria (`number`, `name`, `level`) the finding relates to. Empty for a platform advisory. |
| `platformGuideline` | For a platform advisory, the Apple or Android guideline it cites; not a WCAG requirement. |
| `role`, `label`, `nodePath`, `bounds`, `identifier` | The element on the screen. |
| `findingId` | An id such as `"SW-1a2b3c4d5e6f"` that stays the same when an unchanged screen is scanned again (it changes if the element's position in the tree changes); the id exported tickets use and triage marks are stored under (added in schema 0.8). |
| `affects` | Short text on who is affected, the same text the report shows under "Who's affected" (added in schema 0.8). |
| `reportAnchor` | The element id of this finding in the HTML report, such as `"s0-f2"`: screen number counted from 0, finding number counted from 1 (added in schema 0.8). Open `report.html#s0-f2` in the same run folder to land on it. |
| `sourceLocation` | Only with a source folder. `confidence` is `exact`, `likely`, `candidates` or `notFound`; `file` and `line` (exact and likely only) are relative to the folder named by `sourceRootName`; `reason` is a plain sentence; `candidates[]` lists `file` and `line` pairs. A line only: no column is saved. |
| `fix` | Suggested fix, example code and likely causes. |

## Shared run files (`.swipewalk`)

`swipewalk share` packs one run into a `.swipewalk` file: a standard zip (not encrypted) that holds `manifest.json`,
`signature.json` when signed, and a `run/` folder with the run's `run.json`, `results.json`, `triage.json` when
included, and the screenshots `results.json` refers to (`run/capture/screenshot.png`, for example). A reader ignores fields it
does not know. A file with any other kind of entry (for example a `report.html`) is refused as invalid.

`manifest.json` fields:

| Field | Meaning |
|---|---|
| `formatVersion` | `1`. A file with a higher number can't be read by this Swipewalk. |
| `minReaderVersion` | The oldest Swipewalk release that can safely read the file, for example `"0.5.0"`. Raised only when a change can't be ignored. A Swipewalk older than this refuses the file and names the version needed. |
| `app`, `appKey`, `appVersion`, `platform` | The app's name, its id, its own version and `Android` or `iOS`. |
| `deviceKind` | `phone`, `emulator` or `simulator`; never a device name or serial number. |
| `runId`, `startedAt`, `finishedAt`, `mode` | The run's identity (importing the same id again changes nothing), its times in UTC, and `scan`, `run`, `record` or `session`. |
| `swipewalkVersion`, `rulesetVersion`, `resultsSchemaVersion` | What wrote the run. A newer `swipewalkVersion` or `resultsSchemaVersion` than the reader's gives one calm note; it doesn't block. |
| `sharedAt`, `sharedBy` | When it was shared and the optional free text the sender typed (absent when blank). |
| `includesScreenshots`, `includesTriage` | What was left in. With screenshots off, every `…ScreenshotPath` property is removed from `results.json`. |
| `files[]` | Every file except `manifest.json` and `signature.json`: `path`, `size` in bytes and lowercase hex `sha256`. |

`signature.json` (`signatureVersion` 1) holds `algorithm` (`ECDSA-P256-SHA256`), `publicKey` (base64 DER
SubjectPublicKeyInfo), `signature` (base64, 64 bytes, IEEE P1363 `r||s`) and `signedAt`. The signed bytes are the
exact bytes of `manifest.json` as stored in the zip. The key's fingerprint is the SHA-256 of the DER public key
(64 lowercase hex digits); it is shown as its first 8 digits, `XXXX-XXXX`. A signature shows the file hasn't
changed since it was signed and which key signed it, not which person did.

How a reader reports the signature (the command line, the desktop app and the VS Code extension agree): no `signature.json` is "not signed". A
`signature.json` that isn't an object, has no whole-number `signatureVersion` of at least 1, or (with `signatureVersion` 1) has a missing or unknown
`algorithm`, a wrong key or a signature that doesn't match is "changed after signing". A `signatureVersion` above 1, or another algorithm in a file whose
manifest does not say a newer Swipewalk wrote it, is also "changed after signing"; only a `signatureVersion` above 1 in a file whose manifest says a newer
Swipewalk wrote it (its `swipewalkVersion` is newer than the reader's at major.minor, or its `resultsSchemaVersion` is newer than the one the reader knows) is "signed in a newer format", which a reader treats as not signed. A valid signature is
"trusted" when the key is on the reader's own list, "trusted through a team file" when it is only in an `accessibility/team-keys` file, and "not trusted yet"
otherwise. A reader asks the person before it opens a file that is not signed, changed after signing, or signed in a newer format.

Every `…ScreenshotPath` in a saved run is a relative path (with `/` separators) to a picture inside the run's own folder; a picture that was captured elsewhere (a saved capture replayed with `--from`) is copied into the run's `pictures` folder. A reader uses a picture only when it sits inside the run's folder, whoever wrote the file (with one exception, below). run.json's `picturesInFolder` says Swipewalk has already made sure of this. A run without it, saved by an earlier version, is brought inside the first time it is listed or opened from the person's own History folder, if the folder can be written: only a PNG named `screenshot.png` beside a capture's tree file (`tree.json` or `uiautomator.xml`) is copied in, and `results.json` is rewritten to list them relative to the folder; `picturesInFolder` is set once no listed picture outside the folder is left to look for (one that can't be found yet, or past the limit on how many are checked at a time, keeps the run unmarked so it is looked at again). Nothing is copied in, and no file is written, for an imported run, a folder whose run.json is missing, unreadable or marked as imported, a folder that isn't directly in the person's own History folder, or a History folder marked as shared or a different folder given with `--history`. If the folder can't be written, those pictures are used where they are, until Swipewalk is closed.

For a run opened from a shared file a reader keeps what is inside the run's own folder and nothing else: an entry name is refused if any part of it is a Windows device name
(`CON`, `NUL`, `COM1` and so on, with or without an extension). When a reader imports a run it rewrites `results.json` so that a screenshot
property stays only when it is a relative path inside the run's folder to a picture the file lists; the `ruleSources` list keeps only sources the reader knows, with its own links;
and the run's counts are worked out from the results, not taken from `run.json`.
