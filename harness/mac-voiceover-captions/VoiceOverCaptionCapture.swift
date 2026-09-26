// Captures VoiceOver's own Caption Panel while a person drives VoiceOver by hand on a connected iPhone --
// the "person-driven capture" route described in Swipewalk.Core.Model.ScreenReaderCapture's
// VoiceOverCaptions source. VoiceOver is never scripted or turned on by this: the person turns it on
// themselves (Settings > Accessibility > VoiceOver) and its Caption Panel (VoiceOver > Caption Panel), then
// swipes through the screen. This only takes screenshots of the phone over the cable
// (`xcrun devicectl device capture screenshot`, confirmed at roughly 0.33s per shot on a physical iPhone with
// no XCUITest and no extra permission) and reads the on-screen caption text with Vision's on-device text
// recognition -- no network call, no XCUITest session, nothing sent anywhere.
//
// Two modes:
//
//   --analyze-image <path> [--languages en-US,es-ES,...]
//       Single-shot: analyzes one screenshot file and prints its own detections as JSON. Used by
//       IosVoiceOverCaptionWalkTests against synthetic fixtures (tests/Swipewalk.Core.Tests/Fixtures/voiceover-captions/); never
//       touches a device. See AnalyzeResult.
//
//   --device <udid> --duration-seconds <n> [--poll-interval-ms <ms>] [--languages en-US,es-ES,...]
//       Live: polls devicectl every poll-interval-ms, for up to duration-seconds, or until a line is read
//       from stdin (Swipewalk.Collectors.Ios.IosVoiceOverCaptionWalk writes one there when the person says
//       they're done), de-duplicates consecutive identical captions, and prints one final JSON line with
//       every utterance captured, in its own order. See CaptureResult.
//
// Coordinates in this file's own JSON output are TOP-LEFT origin pixel coordinates in the captured
// screenshot's own space (y=0 at the top, increasing downward) -- the same convention
// Swipewalk.Core.Model.Bounds uses -- with an explicit flip at Vision's boundary, since Vision's own
// regionOfInterest is normalized and BOTTOM-LEFT origin. That flip, and the NSBitmapImageRep pixel-access
// convention this relies on, were both confirmed empirically with a one-pixel test image (a known black band
// at one edge, read back at the expected coordinate) before being relied on here, not assumed.
//
// No private API: only public Vision, CoreGraphics, AppKit and Foundation.

import AppKit
import CoreGraphics
import Foundation
import Vision

struct Rect {
    var x: Double
    var y: Double
    var w: Double
    var h: Double
}

// MARK: - image loading and pixel access

func loadImage(_ path: String) -> CGImage? {
    guard let data = NSData(contentsOfFile: path),
          let src = CGImageSourceCreateWithData(data, nil)
    else { return nil }
    return CGImageSourceCreateImageAtIndex(src, 0, nil)
}

/// NSBitmapImageRep(cgImage:) exposes colorAt(x:y:) with (0,0) at the image's TOP-LEFT -- confirmed by a
/// one-pixel test image before this was relied on (a known black band at the top read back as black at
/// y=0 and white at the bottom), not assumed.
func loadRep(_ image: CGImage) -> NSBitmapImageRep { NSBitmapImageRep(cgImage: image) }

// MARK: - caption bar region

/// Finds the caption panel's own band adaptively: it's a dark, near-solid bar spanning most of the width,
/// always somewhere in the bottom portion of the screen (VoiceOver never puts it at the top). Returns a
/// TOP-LEFT-origin pixel rect, or nil when no such band is present right now (nothing being spoken, or the
/// Caption Panel setting is off).
func findCaptionBarRegion(_ rep: NSBitmapImageRep) -> Rect? {
    let h = rep.pixelsHigh, w = rep.pixelsWide
    guard w > 0, h > 0 else { return nil }
    let colStep = max(1, w / 150)
    var rows = [Double](repeating: 1, count: h)
    for y in 0..<h {
        var sum = 0.0
        var count = 0
        var x = 0
        while x < w {
            if let c = rep.colorAt(x: x, y: y) {
                sum += 0.299 * c.redComponent + 0.587 * c.greenComponent + 0.114 * c.blueComponent
                count += 1
            }
            x += colStep
        }
        rows[y] = count > 0 ? sum / Double(count) : 1
    }

    let darkThreshold = 0.35
    let searchFrom = Int(Double(h) * 0.55) // the caption panel never starts above the screen's own middle
    var bandTop: Int?
    var bandBottom: Int?
    var gap = 0
    var y = h - 1
    while y >= searchFrom {
        if rows[y] < darkThreshold {
            if bandBottom == nil { bandBottom = y }
            bandTop = y
            gap = 0
        } else if bandBottom != nil {
            gap += 1
            if gap > 2 { break } // a couple of light pixels (anti-aliasing, a stray glyph) are tolerated
        }
        y -= 1
    }
    guard let top = bandTop, let bottom = bandBottom, bottom - top > 20 else { return nil }
    let margin = Double(w) * 0.03
    return Rect(x: margin, y: Double(top), w: Double(w) - 2 * margin, h: Double(bottom - top + 1))
}

// MARK: - OCR

/// OCRs exactly the given TOP-LEFT-origin pixel region, converting to Vision's bottom-left-origin normalized
/// regionOfInterest, padded and clamped so floating-point rounding at the flip can never produce a
/// slightly-invalid rect (confirmed to otherwise make Vision quietly return zero results rather than throw).
func ocrRegion(_ image: CGImage, _ region: Rect, languages: [String]) -> String? {
    let iw = Double(image.width), ih = Double(image.height)
    guard iw > 0, ih > 0 else { return nil }
    let padFrac = 0.15
    let padded = Rect(
        x: region.x, y: max(0, region.y - region.h * padFrac), w: region.w, h: region.h * (1 + 2 * padFrac))
    let normX = min(max(padded.x / iw, 0), 1)
    let normW = min(max(padded.w / iw, 0), 1 - normX)
    let normH = min(max(padded.h / ih, 0), 1)
    let normYFromTop = padded.y / ih
    let normY = min(max(1.0 - normYFromTop - normH, 0), 1 - normH)
    let roi = CGRect(x: normX, y: normY, width: normW, height: normH)

    let request = VNRecognizeTextRequest()
    request.recognitionLevel = .accurate
    request.usesLanguageCorrection = false
    request.regionOfInterest = roi
    if !languages.isEmpty {
        request.recognitionLanguages = languages
    } else if #available(macOS 14.0, *) {
        request.automaticallyDetectsLanguage = true
    }
    let handler = VNImageRequestHandler(cgImage: image, options: [:])
    do {
        try handler.perform([request])
    } catch {
        return nil
    }
    guard let observations = request.results, !observations.isEmpty else { return nil }
    let lines = observations.compactMap { $0.topCandidates(1).first?.string }
    let text = lines.joined(separator: " ").trimmingCharacters(in: .whitespacesAndNewlines)
    return text.isEmpty ? nil : text
}

// MARK: - focus-rectangle detection (best-effort; see KnownLimitations "ios-voiceover-captions-capture")

/// Best-effort VoiceOver focus-rectangle detection: looks for a pair of strong horizontal lines (top/bottom)
/// and, within that row band, a pair of strong vertical lines (left/right) -- the cursor's own rounded-rect
/// border. Deliberately conservative: a candidate line must look like a real high-contrast border (VoiceOver's
/// cursor is close to the strongest possible edge on screen), not merely the strongest thing this particular
/// screen happens to have, or an ordinary UI element's own boundary (a card, a button background) would be
/// reported as if it were the cursor. Tested only against synthetic fixtures so far (a drawn background with
/// two ordinary, low-contrast card rectangles and no cursor correctly found nothing; a drawn cursor stroke was
/// found within a few pixels of where it was drawn) -- unverified against a real VoiceOver cursor on a real
/// device. See Swipewalk.Core.Limitations.KnownLimitations "ios-voiceover-captions-capture" for what real-device
/// testing still needs to confirm.
func findFocusRect(_ rep: NSBitmapImageRep, excluding captionRegion: Rect?) -> Rect? {
    let w = rep.pixelsWide, h = rep.pixelsHigh
    guard w > 0, h > 0 else { return nil }
    let step = max(1, min(w, h) / 300)
    var xs: [Int] = []
    var x = 0
    while x < w { xs.append(x); x += step }
    var ys: [Int] = []
    var y = 0
    while y < h { ys.append(y); y += step }

    var gray = [[Double]]()
    gray.reserveCapacity(ys.count)
    for gy in ys {
        var row = [Double](repeating: 1, count: xs.count)
        for (i, gx) in xs.enumerated() {
            if let c = rep.colorAt(x: gx, y: gy) {
                row[i] = 0.299 * c.redComponent + 0.587 * c.greenComponent + 0.114 * c.blueComponent
            }
        }
        gray.append(row)
    }
    let rowCount = gray.count, colCount = xs.count
    guard rowCount > 4, colCount > 4 else { return nil }

    // rowEdge[r]: how much color changes from row r-1 to row r, summed across every sampled column -- large
    // when row r is itself a horizontal border line. Its average (divided by colCount) is a fair per-sample
    // contrast estimate because a horizontal border typically spans nearly the full screen width. A vertical
    // border, by contrast, only spans the rectangle's own height, which can be a small fraction of the whole
    // screen, so a column's contrast is only judged WITHIN a specific candidate row band (localColEdge below),
    // never against the full image height -- otherwise a real vertical line looks artificially weak simply
    // because most of the screen's height has nothing to do with it (confirmed while building this: judging
    // columns against the full height missed every real cursor rect in testing).
    var rowEdge = [Double](repeating: 0, count: rowCount)
    for r in 1..<rowCount {
        for c in 0..<colCount {
            rowEdge[r] += abs(gray[r][c] - gray[r - 1][c])
        }
    }

    func topPeaks(_ values: [Double], validRange: Range<Int>, count: Int, minSeparation: Int) -> [Int] {
        var indexed = validRange.map { ($0, values[$0]) }
        indexed.sort { $0.1 > $1.1 }
        var picked: [Int] = []
        for (idx, _) in indexed {
            if picked.allSatisfy({ abs($0 - idx) >= minSeparation }) {
                picked.append(idx)
            }
            if picked.count >= count { break }
        }
        return picked
    }

    let marginRows = max(1, rowCount / 40)
    let marginCols = max(1, colCount / 40)
    let captionTopRow: Int = {
        guard let cap = captionRegion else { return rowCount }
        return Int(cap.y / Double(step))
    }()
    let rowRange = marginRows..<max(marginRows + 1, min(rowCount - marginRows, captionTopRow))
    guard rowRange.count > 4 else { return nil }
    let rowPeaks = topPeaks(rowEdge, validRange: rowRange, count: 6, minSeparation: max(2, rowCount / 30))
    let colRange = marginCols..<(colCount - marginCols)
    guard colRange.count > 4, rowPeaks.count >= 2 else { return nil }

    let minAvgContrast = 0.25 // see this function's header: ~0.6-0.9 for a real drawn border vs ~0.06-0.1 for an ordinary UI edge
    func avgRowContrast(_ r: Int) -> Double { rowEdge[r] / Double(colCount) }

    var best: (score: Double, rect: Rect)?
    for i in 0..<rowPeaks.count {
        for j in (i + 1)..<rowPeaks.count {
            let top = min(rowPeaks[i], rowPeaks[j]), bottom = max(rowPeaks[i], rowPeaks[j])
            let heightFrac = Double(bottom - top) / Double(rowCount)
            guard heightFrac > 0.03 && heightFrac < 0.9 else { continue }
            guard avgRowContrast(top) > minAvgContrast && avgRowContrast(bottom) > minAvgContrast else { continue }

            var localColEdge = [Double](repeating: 0, count: colCount)
            for r in (top + 1)...bottom {
                for c in 1..<colCount {
                    localColEdge[c] += abs(gray[r][c] - gray[r][c - 1])
                }
            }
            let bandHeight = Double(bottom - top)
            let localColPeaks = topPeaks(localColEdge, validRange: colRange, count: 6, minSeparation: max(2, colCount / 30))
            for a in 0..<localColPeaks.count {
                for b in (a + 1)..<localColPeaks.count {
                    let left = min(localColPeaks[a], localColPeaks[b]), right = max(localColPeaks[a], localColPeaks[b])
                    let widthFrac = Double(right - left) / Double(colCount)
                    guard widthFrac > 0.05 && widthFrac < 0.95 else { continue }
                    let leftContrast = localColEdge[left] / bandHeight, rightContrast = localColEdge[right] / bandHeight
                    guard leftContrast > minAvgContrast && rightContrast > minAvgContrast else { continue }
                    let score = rowEdge[top] + rowEdge[bottom] + localColEdge[left] + localColEdge[right]
                    if best == nil || score > best!.score {
                        best = (score, Rect(
                            x: Double(left * step), y: Double(top * step),
                            w: Double((right - left) * step), h: Double((bottom - top) * step)))
                    }
                }
            }
        }
    }
    return best?.rect
}

// MARK: - one-frame analysis (shared by both modes)

struct FrameAnalysis {
    var capturedText: String?
    var captionRegion: Rect?
    var focusRect: Rect?
    var imageWidth: Int
    var imageHeight: Int
}

func analyzeFrame(_ image: CGImage, languages: [String]) -> FrameAnalysis {
    let rep = loadRep(image)
    let captionRegion = findCaptionBarRegion(rep)
    let capturedText = captionRegion.flatMap { ocrRegion(image, $0, languages: languages) }
    let focusRect = findFocusRect(rep, excluding: captionRegion)
    return FrameAnalysis(
        capturedText: capturedText, captionRegion: captionRegion, focusRect: focusRect,
        imageWidth: image.width, imageHeight: image.height)
}

// MARK: - JSON result shapes (mirrored by Swipewalk.Collectors.Ios.IosVoiceOverCaptionWalk)

struct RectJson: Codable {
    let x: Double
    let y: Double
    let w: Double
    let h: Double
    init(_ r: Rect) { x = r.x; y = r.y; w = r.w; h = r.h }
}

struct AnalyzeResult: Codable {
    let ok: Bool
    let error: String?
    let capturedText: String?
    let captionRegion: RectJson?
    let focusRect: RectJson?
    let imageWidth: Int
    let imageHeight: Int
}

struct CaptionItemJson: Codable {
    let order: Int
    let spokenText: String
    let elapsedSeconds: Double
    let focusRect: RectJson?
}

struct CaptureResult: Codable {
    let ok: Bool
    let error: String?
    let items: [CaptionItemJson]
    let complete: Bool
    let notCompleteReason: String?
    let toolVersion: String
    let imageWidth: Int?
    let imageHeight: Int?
}

let toolVersion = "0.1-draft" // bumped when the analysis approach materially changes

func printJson<T: Encodable>(_ value: T) {
    let encoder = JSONEncoder()
    if let data = try? encoder.encode(value), let s = String(data: data, encoding: .utf8) {
        print(s)
    } else {
        print("{\"ok\":false,\"error\":\"could not encode result\"}")
    }
}

// MARK: - live devicectl polling

/// Runs `xcrun devicectl device capture screenshot`, bounded by timeoutSeconds. True on success (the file at
/// outPath was written).
func captureScreenshot(udid: String, outPath: String, timeoutSeconds: Double) -> Bool {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: "/usr/bin/xcrun")
    process.arguments = [
        "devicectl", "device", "capture", "screenshot", "--device", udid,
        "--destination", outPath, "--timeout", String(Int(timeoutSeconds)), "--quiet",
    ]
    let devNull = FileHandle.nullDevice
    process.standardOutput = devNull
    process.standardError = devNull
    do {
        try process.run()
    } catch {
        return false
    }
    process.waitUntilExit()
    return process.terminationStatus == 0 && FileManager.default.fileExists(atPath: outPath)
}

func runLive(udid: String, maxDuration: Double, pollIntervalMs: Int, languages: [String]) -> CaptureResult {
    let start = Date()
    var items: [CaptionItemJson] = []
    var lastText: String?
    var imageSize: (Int, Int)?
    var consecutiveFailures = 0

    // A background thread blocks on stdin so the poll loop can be told "the person is done" without waiting
    // for the full duration -- Swipewalk.Collectors.Ios.IosVoiceOverCaptionWalk writes one line and closes
    // stdin when the person presses Enter.
    var stopRequested = false
    let stopLock = NSLock()
    let stdinThread = Thread {
        _ = readLine() // returns nil at EOF too, which is also a stop signal
        stopLock.lock(); stopRequested = true; stopLock.unlock()
    }
    stdinThread.start()

    func shouldStop() -> Bool {
        stopLock.lock(); defer { stopLock.unlock() }
        return stopRequested
    }

    while Date().timeIntervalSince(start) < maxDuration && !shouldStop() {
        let tmpPath = NSTemporaryDirectory() + "swipewalk-voc-\(UUID().uuidString).png"
        guard captureScreenshot(udid: udid, outPath: tmpPath, timeoutSeconds: 5) else {
            consecutiveFailures += 1
            try? FileManager.default.removeItem(atPath: tmpPath)
            if consecutiveFailures >= 5 {
                return CaptureResult(
                    ok: false, error: "the device stopped responding to screenshot capture (5 attempts in a row) -- check the cable and that the phone is unlocked",
                    items: items, complete: false, notCompleteReason: "the device disconnected or stopped responding mid-capture",
                    toolVersion: toolVersion, imageWidth: imageSize?.0, imageHeight: imageSize?.1)
            }
            Thread.sleep(forTimeInterval: 0.5)
            continue
        }
        consecutiveFailures = 0
        defer { try? FileManager.default.removeItem(atPath: tmpPath) }

        if let image = loadImage(tmpPath) {
            imageSize = (image.width, image.height)
            let analysis = analyzeFrame(image, languages: languages)
            if let text = analysis.capturedText {
                if text != lastText {
                    items.append(CaptionItemJson(
                        order: items.count + 1, spokenText: text,
                        elapsedSeconds: Date().timeIntervalSince(start),
                        focusRect: analysis.focusRect.map(RectJson.init)))
                }
                lastText = text
            } else {
                // The caption cleared (nothing being spoken right now): the next utterance counts as new even
                // if it repeats an earlier one (e.g. the person swiped back).
                lastText = nil
            }
        }

        let elapsedThisFrame = Date().timeIntervalSince(start)
        let remaining = maxDuration - elapsedThisFrame
        guard remaining > 0 else { break }
        Thread.sleep(forTimeInterval: min(Double(pollIntervalMs) / 1000.0, remaining))
    }

    // Never claimed complete: unlike a scripted walk, nothing here can tell whether the person swiped through
    // every element on the screen or stopped partway -- see this file's header and
    // Swipewalk.Core.Model.ScreenReaderCapture's remarks on VoiceOverCaptions.
    return CaptureResult(
        ok: true, error: nil, items: items, complete: false,
        notCompleteReason: "a person drove this VoiceOver session by hand; Swipewalk cannot confirm every element on the screen was reached",
        toolVersion: toolVersion, imageWidth: imageSize?.0, imageHeight: imageSize?.1)
}

// MARK: - main

func argValue(_ args: [String], _ flag: String) -> String? {
    guard let i = args.firstIndex(of: flag), i + 1 < args.count else { return nil }
    return args[i + 1]
}

let args = CommandLine.arguments
let languages = argValue(args, "--languages")?.split(separator: ",").map(String.init) ?? []

if let imagePath = argValue(args, "--analyze-image") {
    guard let image = loadImage(imagePath) else {
        printJson(AnalyzeResult(ok: false, error: "could not load \(imagePath)", capturedText: nil, captionRegion: nil, focusRect: nil, imageWidth: 0, imageHeight: 0))
        exit(0)
    }
    let a = analyzeFrame(image, languages: languages)
    printJson(AnalyzeResult(
        ok: true, error: nil, capturedText: a.capturedText,
        captionRegion: a.captionRegion.map(RectJson.init), focusRect: a.focusRect.map(RectJson.init),
        imageWidth: a.imageWidth, imageHeight: a.imageHeight))
    exit(0)
}

guard let udid = argValue(args, "--device"), let durationString = argValue(args, "--duration-seconds"),
      let duration = Double(durationString)
else {
    print("usage: VoiceOverCaptionCapture.swift --analyze-image <path> [--languages en-US,es-ES]")
    print("   or: VoiceOverCaptionCapture.swift --device <udid> --duration-seconds <n> [--poll-interval-ms <ms>] [--languages en-US,es-ES]")
    exit(1)
}
let pollIntervalMs = Int(argValue(args, "--poll-interval-ms") ?? "") ?? 1200
let result = runLive(udid: udid, maxDuration: duration, pollIntervalMs: pollIntervalMs, languages: languages)
printJson(result)
