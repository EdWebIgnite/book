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
// Moss and the Biggest Puddle is not here any more: it uses painted artwork (tools/make_puddle_pages.py)
// and its book.json is edited by hand in the editor. tools/art/puddle.js is the old drawn version,
// kept because the art brief's layout roughs came from it.
const BOOKS = { waterhole: "./art/waterhole", "captain-megawatt": "./art/megawatt", "deep-sea": "./art/deep" };
const TILE = 1024;        // tile size for big scenes
const TILE_LEVELS = 2;    // full size, and half size; the overview image covers anything smaller
const OVERVIEW = 2048;    // width of a big scene's overview image

function overlay(page, w, h) {
  // A page can be a window onto a bigger drawing; boxes are fractions of that window.
  const [vx, vy, vw, vh] = page.view || [0, 0, w, h];
  const px = b => `x="${vx + b.x * vw}" y="${vy + b.y * vh}" width="${b.w * vw}" height="${b.h * vh}"`;
  w = vw; h = vh;
  const ox = vx, oy = vy;
  let s = "";
  (page.stops || []).forEach((st, i) => {
    s += `<rect ${px(st.box)} fill="none" stroke="#ff2d2d" stroke-width="10"/><text x="${ox + st.box.x * w + 16}" y="${oy + st.box.y * h + 80}" font-size="80" font-weight="800" fill="#ff2d2d">${i + 1}</text>`;
    if (st.to) s += `<rect ${px(st.to)} fill="none" stroke="#ff2dd0" stroke-width="10" stroke-dasharray="30 18"/><text x="${ox + st.to.x * w + 16}" y="${oy + st.to.y * h + 80}" font-size="80" font-weight="800" fill="#ff2dd0">${i + 1} end</text>`;
  });
  (page.hotspots || []).forEach((hs, i) => {
    s += `<rect ${px(hs.box)} fill="none" stroke="#ffe600" stroke-width="10" stroke-dasharray="12 12"/><text x="${ox + hs.box.x * w + 10}" y="${oy + hs.box.y * h - 14}" font-size="64" font-weight="800" fill="#ffe600">★${i + 1}</text>`;
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
      const out = { image, stops: p.stops || [] };
      if (p.tiled) {
        // A big scene: a small overview image, plus tiles at full and half size.
        const full = [0, 0, width, height];
        jobs.push({ svg: p.svg, width: OVERVIEW, height: Math.round(OVERVIEW * height / width), view: full, file: path.join(dir, image) });
        let count = 0;
        for (let z = 0; z < TILE_LEVELS; z++) {
          const f = 2 ** z, lw = Math.ceil(width / f), lh = Math.ceil(height / f);
          for (let ty = 0; ty * TILE < lh; ty++) {
            for (let tx = 0; tx * TILE < lw; tx++) {
              const w = Math.min(TILE, lw - tx * TILE), h = Math.min(TILE, lh - ty * TILE);
              jobs.push({ svg: p.svg, width: w, height: h, view: [tx * TILE * f, ty * TILE * f, w * f, h * f], quality: 84, quiet: true,
                          file: path.join(dir, `page${i + 1}_tiles`, String(z), `${tx}_${ty}.jpg`) });
              count++;
            }
          }
        }
        console.log(`${id} page ${i + 1}: ${count} tiles`);
        out.tiles = { width, height, size: TILE, levels: TILE_LEVELS, path: `page${i + 1}_tiles/{z}/{x}_{y}.jpg` };
        if (process.env.CHECK_DIR) {
          jobs.push({ svg: p.svg + overlay(p, width, height), width: 4096, height: Math.round(4096 * height / width), view: full, file: path.join(process.env.CHECK_DIR, `${id}-${image}`) });
        }
      } else {
        // p.view lets a page be a window onto a larger drawing (several pages can share one scene).
        jobs.push({ svg: p.svg, width, height, view: p.view, file: path.join(dir, image) });
        if (process.env.CHECK_DIR) {
          jobs.push({ svg: p.svg + overlay(p, width, height), width, height, view: p.view, file: path.join(process.env.CHECK_DIR, `${id}-${image}`) });
        }
      }
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
