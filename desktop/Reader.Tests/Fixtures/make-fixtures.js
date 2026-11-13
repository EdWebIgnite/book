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
