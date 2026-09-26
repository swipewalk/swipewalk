// Regenerates this folder's fixture PNGs. Not run by any build or test -- a dev-only tool, run by hand with
// `xcrun swift generate_fixtures.swift <output-dir>` after a deliberate change to what these fixtures need to
// cover. Deliberately synthetic (a plain background, a couple of card-like rectangles, a rendered caption bar,
// a rounded-rect stroke standing in for the VoiceOver focus indicator) -- never a real device screenshot, so
// there's nothing here that could show a real notification, app content or personal information.
//
// See IosVoiceOverCaptionWalkTests for what each fixture is expected to produce.

import AppKit
import CoreGraphics
import Foundation

func makeFixture(width: Int, height: Int, captionText: String?, focusRect: CGRect?, out: String) {
    let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8, samplesPerPixel: 4,
        hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    let ctx = NSGraphicsContext.current!.cgContext

    // Background: light gray "app" content with a couple of slightly darker card rectangles -- deliberately
    // low contrast (~0.1), so a test can check these don't get mistaken for the (much higher contrast) cursor.
    ctx.setFillColor(CGColor(red: 0.95, green: 0.95, blue: 0.97, alpha: 1))
    ctx.fill(CGRect(x: 0, y: 0, width: width, height: height))
    ctx.setFillColor(CGColor(red: 0.85, green: 0.87, blue: 0.9, alpha: 1))
    ctx.fill(CGRect(x: 40, y: height - 300, width: width - 80, height: 120))
    ctx.fill(CGRect(x: 40, y: height - 460, width: width - 80, height: 120))

    // Focus indicator stand-in: a black rounded-rect stroke. This context uses CoreGraphics' normal
    // bottom-left origin (y=0 at the bottom, increasing upward) -- focusRect is given in that space.
    if let r = focusRect {
        let path = NSBezierPath(roundedRect: NSRect(x: r.minX, y: r.minY, width: r.width, height: r.height), xRadius: 10, yRadius: 10)
        ctx.setStrokeColor(CGColor(red: 0, green: 0, blue: 0, alpha: 1))
        path.lineWidth = 6
        path.stroke()
    }

    // Caption bar: a solid near-black band across most of the bottom, with white text -- standing in for
    // VoiceOver's own Caption Panel.
    if let text = captionText {
        let barHeight = 90.0
        ctx.setFillColor(CGColor(red: 0.05, green: 0.05, blue: 0.05, alpha: 0.92))
        ctx.fill(CGRect(x: 0, y: 0, width: Double(width), height: barHeight))
        let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: 34), .foregroundColor: NSColor.white]
        let attrStr = NSAttributedString(string: text, attributes: attrs)
        let size = attrStr.size()
        attrStr.draw(at: NSPoint(x: (Double(width) - size.width) / 2, y: (barHeight - size.height) / 2))
    }

    NSGraphicsContext.restoreGraphicsState()
    let pngData = rep.representation(using: .png, properties: [:])!
    try! pngData.write(to: URL(fileURLWithPath: out))
    print("wrote \(out) (\(width)x\(height))")
}

let outDir = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "."
let w = 1170, h = 2532 // iPhone-like portrait dimensions

// English caption, with a focus rect matching the card it's naming.
makeFixture(width: w, height: h, captionText: "Submit, Button",
    focusRect: CGRect(x: 40, y: h - 300, width: w - 80, height: 120), out: outDir + "/en_submit_button.png")

// Spanish caption, no focus rect drawn -- exercises the "OCR text but no cursor rect found" (text-only match) path.
makeFixture(width: w, height: h, captionText: "Enviar, Boton", focusRect: nil, out: outDir + "/es_enviar_boton.png")

// Japanese caption (non-Latin script), focus rect around a different card -- checks the region-finder and
// focus-rect detector aren't tuned to one fixed position.
makeFixture(width: w, height: h, captionText: "\u{9001}\u{4fe1}, \u{30dc}\u{30bf}\u{30f3}",
    focusRect: CGRect(x: 40, y: h - 460, width: w - 80, height: 120), out: outDir + "/ja_button.png")

// No caption bar and no focus rect at all -- an idle frame between utterances; must not produce a false
// positive from the plain background/card edges alone.
makeFixture(width: w, height: h, captionText: nil, focusRect: nil, out: outDir + "/idle_no_caption.png")
