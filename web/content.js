/* Finds and loads the books in content/. Shared by the playground and the editor. */
"use strict";
(function () {
  // Works from the repo (web/ next to ../content) and when hosted with content/ alongside the page.
  const BASES = ["content/", "../content/"];
  let base = null;

  async function fetchJson(path) {
    const res = await fetch(path, { cache: "no-store" });
    if (!res.ok) throw new Error(res.status + " " + path);
    return res.json();
  }

  async function library() {
    for (const b of BASES) {
      try { const lib = await fetchJson(b + "library.json"); base = b; return lib; } catch (_) { /* try next */ }
    }
    throw new Error("couldn't find content/library.json");
  }

  /** [{ id, title, coverUrl }] for every book listed in content/library.json. */
  async function listBooks() {
    const lib = await library();
    const out = [];
    for (const id of lib.books) {
      const raw = await fetchJson(base + "books/" + id + "/book.json");
      out.push({ id, title: raw.title, coverUrl: base + "books/" + id + "/" + (raw.cover || raw.pages[0].image) });
    }
    return out;
  }

  /** Loads book.json and every page image. */
  async function loadBook(id) {
    if (!base) await library();
    const dir = base + "books/" + id + "/";
    const book = ComicReader.normalizeBook(await fetchJson(dir + "book.json"));
    const images = await Promise.all(book.pages.map(p => ComicReader.loadImage(dir + p.image)));
    return { book, images, dir };
  }

  window.Content = { listBooks, loadBook };
})();
