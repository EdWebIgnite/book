// Writes what the browser reader's serializeBook produces for every sample book, so the tests can
// check that the desktop reader writes the same thing. Run it again whenever web/reader.js or a
// sample book changes:
//
//     node make-fixtures.js
//
"use strict";
const fs = require("fs");
const path = require("path");

const repo = path.resolve(__dirname, "../../..");
global.window = {};                                   // reader.js hangs its exports on window
require(path.join(repo, "web/reader.js"));
const CR = global.window.ComicReader;

const library = JSON.parse(fs.readFileSync(path.join(repo, "content/library.json"), "utf8"));
for (const id of library.books) {
  const raw = JSON.parse(fs.readFileSync(path.join(repo, "content/books", id, "book.json"), "utf8"));
  const out = CR.serializeBook(CR.normalizeBook(raw));
  fs.writeFileSync(path.join(__dirname, id + ".browser.json"), JSON.stringify(out, null, 2) + "\n");
  console.log("wrote", id + ".browser.json");
}

// How long the browser reader takes over a set of camera moves at the standard speed, so the desktop
// can be checked against it move for move.
{
  const r = (x, y, w, h) => ({ x, y, w, h });
  const moves = [
    [r(0, 0, 1.66, 1.06), r(0.135, 0.535, 0.81, 0.4233)],        // whole page in to a stop
    [r(0.135, 0.535, 0.81, 0.4233), r(0.535, 0.6417, 0.4233, 0.3033)],   // one stop to the next
    [r(0.2, 0.2, 0.5, 0.3), r(0.2, 0.2, 0.5, 0.3)],               // nowhere to go
    [r(0, 0, 1, 1), r(1.72, 0, 1, 1)],                            // one page across
    [r(0, 0, 1, 1), r(12, 0, 1, 1)],                              // a long way
    [r(0, 0, 1.6, 1), r(0.7, 0.4, 0.02, 0.0125)],                 // a very deep zoom
    [r(0.7, 0.4, 0.02, 0.0125), r(0, 0, 1.6, 1)],
    [r(-0.03, -0.03, 1.66, 1.06), r(1.69, -0.03, 1.66, 1.06)],    // whole page to the next whole page
  ];
  const out = [];
  for (const scaleTime of [true, false]) {
    for (const base of [1400, 700]) {
      for (const [from, to] of moves) {
        const t = CR.Reader.prototype.moveTime.call({ settings: { scaleTime } }, from, to, base);
        out.push({ scaleTime, base, from, to, ms: t.ms, far: t.far });
      }
    }
  }
  fs.writeFileSync(path.join(__dirname, "movetime.browser.json"), JSON.stringify(out, null, 2) + "\n");
  console.log("wrote movetime.browser.json");
}
