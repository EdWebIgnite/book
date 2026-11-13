/*
 * Small extra files for Moss and the Biggest Puddle, used to show layers and stop sounds:
 * a butterfly with a see-through background, and a short splash sound.
 *   node tools/make_puddle_extras.js
 * Both are placeholders made in code. Real books would use painted pictures and recorded sound.
 */
"use strict";
const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const g = require("./art/garden");

const OUT = path.join(__dirname, "..", "content", "books", "biggest-puddle");

function splashWav() {
  // A burst of hiss that drops in pitch and fades: close enough to a splash for a demo.
  const rate = 22050, n = Math.round(rate * 0.9), data = Buffer.alloc(44 + n * 2);
  data.write("RIFF", 0); data.writeUInt32LE(36 + n * 2, 4); data.write("WAVEfmt ", 8);
  data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22);
  data.writeUInt32LE(rate, 24); data.writeUInt32LE(rate * 2, 28); data.writeUInt16LE(2, 32); data.writeUInt16LE(16, 34);
  data.write("data", 36); data.writeUInt32LE(n * 2, 40);
  let low = 0, seed = 7;
  const noise = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 2147483648 - 1; };
  for (let i = 0; i < n; i++) {
    const t = i / rate, k = 0.55 * Math.exp(-t * 5) + 0.04;           // the filter closes as it fades
    low += k * (noise() - low);
    const env = Math.min(1, t / 0.012) * Math.exp(-t * 4.2);
    data.writeInt16LE(Math.max(-1, Math.min(1, low * env * 2.4)) * 32767 | 0, 44 + i * 2);
  }
  fs.writeFileSync(path.join(OUT, "splash.wav"), data);
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 360, height: 300 }, deviceScaleFactor: 1 });
  await page.setContent(`<body style="margin:0;background:transparent"><svg xmlns="http://www.w3.org/2000/svg" width="360" height="300" viewBox="-90 -80 180 150" style="filter:blur(.5px)">${g.butterfly()}</svg></body>`);
  await page.screenshot({ path: path.join(OUT, "butterfly.png"), omitBackground: true });
  await browser.close();
  splashWav();
  console.log("wrote butterfly.png and splash.wav");
})();
