/*
 * Draws the sample books and writes their book.json files into content/books/.
 *
 *   node tools/make_books.js            all books
 *   node tools/make_books.js waterhole  one book
 *
 * Needs Node and Playwright (Chromium) to turn the vector drawings into images.
 * Set CHECK_DIR=some/folder to also write copies with the stop boxes drawn on, for checking.
 */
"use strict";
const fs = require("fs");
const path = require("path");
const { renderAll } = require("./art/render");

const CONTENT = path.join(__dirname, "..", "content");
const BOOKS = { waterhole: "./art/waterhole", "captain-megawatt": "./art/megawatt" };

function overlay(page, w, h) {
  const px = b => `x="${b.x * w}" y="${b.y * h}" width="${b.w * w}" height="${b.h * h}"`;
  let s = "";
  (page.stops || []).forEach((st, i) => {
    s += `<rect ${px(st.box)} fill="none" stroke="#ff2d2d" stroke-width="10"/><text x="${st.box.x * w + 16}" y="${st.box.y * h + 80}" font-size="80" font-weight="800" fill="#ff2d2d">${i + 1}</text>`;
    if (st.to) s += `<rect ${px(st.to)} fill="none" stroke="#ff2dd0" stroke-width="10" stroke-dasharray="30 18"/><text x="${st.to.x * w + 16}" y="${st.to.y * h + 80}" font-size="80" font-weight="800" fill="#ff2dd0">${i + 1} end</text>`;
  });
  (page.hotspots || []).forEach((hs, i) => {
    s += `<rect ${px(hs.box)} fill="none" stroke="#ffe600" stroke-width="10" stroke-dasharray="12 12"/><text x="${hs.box.x * w + 10}" y="${hs.box.y * h - 14}" font-size="64" font-weight="800" fill="#ffe600">★${i + 1}</text>`;
  });
  return s;
}

async function main() {
  const only = process.argv[2];
  const jobs = [];
  for (const [id, mod] of Object.entries(BOOKS)) {
    if (only && only !== id) continue;
    if (!fs.existsSync(path.join(__dirname, mod + ".js"))) continue;
    const book = require(mod);
    const dir = path.join(CONTENT, "books", id);
    fs.rmSync(dir, { recursive: true, force: true });
    const pages = book.pages.map((make, i) => {
      const p = make();
      const width = p.width || book.size.width, height = p.height || book.size.height;
      const image = `page${i + 1}.jpg`;
      jobs.push({ svg: p.svg, width, height, file: path.join(dir, image) });
      if (process.env.CHECK_DIR) {
        jobs.push({ svg: p.svg + overlay(p, width, height), width, height, file: path.join(process.env.CHECK_DIR, `${id}-${image}`) });
      }
      const out = { image, stops: p.stops || [] };
      if (p.mode === "explore") Object.assign(out, { mode: "explore", hotspots: p.hotspots || [], guidance: p.guidance || {} });
      return out;
    });
    const c = book.cover();
    jobs.push({ svg: c.svg, width: c.width, height: c.height, file: path.join(dir, "cover.jpg") });
    if (process.env.CHECK_DIR) jobs.push({ svg: c.svg, width: c.width, height: c.height, file: path.join(process.env.CHECK_DIR, `${id}-cover.jpg`) });
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, "book.json"), JSON.stringify({
      id, title: book.title, author: book.author, cover: "cover.jpg", focusStyle: book.focusStyle, pages,
    }, null, 2) + "\n");
  }
  await renderAll(jobs);
  const ids = fs.readdirSync(path.join(CONTENT, "books")).filter(d => fs.existsSync(path.join(CONTENT, "books", d, "book.json"))).sort();
  fs.writeFileSync(path.join(CONTENT, "library.json"), JSON.stringify({ books: ids }, null, 2) + "\n");
}

main().catch(e => { console.error(e); process.exit(1); });
