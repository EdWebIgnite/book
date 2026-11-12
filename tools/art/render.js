/* Renders SVG strings to image files with headless Chromium (Playwright). */
"use strict";
const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");

const font = name => fs.readFileSync(path.join(__dirname, "fonts", name + ".woff2")).toString("base64");
const face = (family, weight, file) =>
  `@font-face { font-family: "${family}"; font-weight: ${weight}; src: url(data:font/woff2;base64,${font(file)}) format("woff2"); }`;
// Baloo: picture-book titles. Bangers: comic titles and sound effects. Comic Neue: comic speech.
const FONT_CSS = `
  ${face("Baloo", 600, "baloo-2-latin-600-normal")}
  ${face("Baloo", 800, "baloo-2-latin-800-normal")}
  ${face("Bangers", 400, "bangers-latin-400-normal")}
  ${face("Comic Neue", 700, "comic-neue-latin-700-normal")}
  html, body { margin: 0; background: #fff; }
  svg { display: block; }
  text { font-family: "Baloo", sans-serif; }`;

/** jobs: [{ svg, width, height, file }]. JPEG for .jpg files, PNG otherwise. */
async function renderAll(jobs) {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  for (const job of jobs) {
    await page.setViewportSize({ width: job.width, height: job.height });
    await page.setContent(`<style>${FONT_CSS}</style><svg xmlns="http://www.w3.org/2000/svg" width="${job.width}" height="${job.height}"
      viewBox="0 0 ${job.width} ${job.height}">${job.svg}</svg>`);
    await page.evaluate(() => document.fonts.ready);
    fs.mkdirSync(path.dirname(job.file), { recursive: true });
    const jpeg = /\.jpe?g$/.test(job.file);
    await page.screenshot({ path: job.file, type: jpeg ? "jpeg" : "png", ...(jpeg ? { quality: 88 } : {}) });
    console.log("wrote", path.relative(process.cwd(), job.file));
  }
  await browser.close();
}

module.exports = { renderAll };
