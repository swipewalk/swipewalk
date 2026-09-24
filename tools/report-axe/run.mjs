import puppeteer from 'puppeteer-core';
import fs from 'fs';
// Checks a Swipewalk report.html with axe-core in light and dark mode; exits 1 on any violation.
// Usage: node run.mjs <report.html>  (uses the installed Google Chrome, or CHROME_PATH)
const [,, file] = process.argv;
let failed = false;
const axe = fs.readFileSync(new URL('./node_modules/axe-core/axe.min.js', import.meta.url), 'utf8');
const browser = await puppeteer.launch({ executablePath: process.env.CHROME_PATH ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: 'new' });
for (const scheme of ['light', 'dark']) {
  const page = await browser.newPage();
  await page.emulateMediaFeatures([{ name: 'prefers-color-scheme', value: scheme }]);
  await page.setViewport({ width: 1280, height: 900 });
  await page.goto('file://' + file, { waitUntil: 'load' });
  await page.addScriptTag({ content: axe });
  const result = await page.evaluate(async () => await axe.run(document, { runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] }));
  console.log(`== ${scheme}: ${result.violations.length} violation type(s), ${result.passes.length} passed rules`);
  failed ||= result.violations.length > 0;
  for (const v of result.violations)
    console.log(`  [${v.impact}] ${v.id}: ${v.help} (${v.nodes.length}) e.g. ${v.nodes[0].target.join(' ')}`);
  await page.close();
}
await browser.close();
process.exit(failed ? 1 : 0);

