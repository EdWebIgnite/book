/* Book editor: draw focus stops on page images, set their order and options, preview, save book.json. */
"use strict";
(function () {
  const CR = window.ComicReader;
  const $ = id => document.getElementById(id);
  const COLORS = ["#ff5a36", "#2f9bff", "#21c07a", "#ffb800", "#c45bff", "#ff4fa3", "#00c2c7", "#9acd32"];
  const MIN = 0.01;          // smallest box, as a fraction of the page
  const HANDLE = 9;          // corner handle grab size, in screen pixels
  const STYLE_NAMES = { dim: "Dim the rest", vignette: "Soft dark edges", none: "Nothing, just frame it" };

  // The open book. Pages also carry _img (the loaded image) and _file (a File added from disk, until saved).
  let book = null;
  let pageIndex = 0;
  let selected = -1;         // stop index on the current page, -1 for none
  let dirHandle = null;      // folder from "Open folder" or the first "Save", when the browser allows it
  let dirty = false;
  let settings = CR.loadSettings();
  const undoStack = [];

  const page = () => (book && book.pages[pageIndex]) || null;
  const stopColor = i => COLORS[i % COLORS.length];
  const natural = (a, b) => a.localeCompare(b, undefined, { numeric: true });
  const slugify = s => (s || "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
  const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

  // ---------------------------------------------------------------- messages and state

  let msgTimer;
  function say(text, ms = 4000) {
    const m = $("msg");
    m.textContent = text;
    m.classList.remove("hidden");
    clearTimeout(msgTimer);
    msgTimer = setTimeout(() => m.classList.add("hidden"), ms);
  }

  function setDirty(v = true, label) {
    dirty = v;
    $("status").textContent = !book ? "" : label || (dirty ? "Unsaved changes" : "Saved");
  }

  // Browser confirm() is blocked in some places, so unsaved work is protected with "click again".
  let discardArmed = false;
  function okToDiscard() {
    if (!dirty || discardArmed) { discardArmed = false; return true; }
    discardArmed = true;
    say("You have unsaved changes. Do that again to throw them away.");
    setTimeout(() => discardArmed = false, 5000);
    return false;
  }

  function snapshot() {
    return {
      meta: { title: book.title, id: book.id, author: book.author, focusStyle: book.focusStyle, cover: book.cover },
      pages: book.pages.map(p => ({ ...p, stops: p.stops.map(s => JSON.parse(JSON.stringify(s))) })),
      pageIndex, selected,
    };
  }

  function pushUndo() {
    if (!book) return;
    undoStack.push(snapshot());
    if (undoStack.length > 200) undoStack.shift();
  }

  function undo() {
    const s = undoStack.pop();
    if (!s) { say("Nothing to undo"); return; }
    Object.assign(book, s.meta);
    book.pages = s.pages;
    pageIndex = clamp(s.pageIndex, 0, Math.max(0, book.pages.length - 1));
    selected = s.selected;
    setDirty();
    renderAll();
  }

  /** Every change goes through here: remember for undo, apply, redraw. */
  function change(fn) {
    pushUndo();
    fn();
    setDirty();
    renderAll();
  }

  // ---------------------------------------------------------------- opening books

  function setBook(b) {
    book = b;
    pageIndex = 0;
    selected = -1;
    undoStack.length = 0;
    setDirty(false, b.pages.length ? "Opened" : "New book");
    renderAll();
  }

  async function openFromLibrary(id) {
    try {
      const { book: b, images, dir } = await Content.loadBook(id);
      b.pages.forEach((p, i) => p._img = images[i]);
      b._dir = dir;
      dirHandle = null;
      setBook(b);
    } catch (e) {
      say("Couldn't open that book: " + e.message);
    }
  }

  function newBook() {
    dirHandle = null;
    setBook(CR.normalizeBook({ id: "", title: "New book", author: "", focusStyle: "vignette", pages: [] }));
    say("Add your page images with the button on the left, or drag them onto the page area.");
  }

  async function addFiles(fileList) {
    const files = [...fileList].filter(f => f.type.startsWith("image/")).sort((a, b) => natural(a.name, b.name));
    if (!files.length) { say("Those weren't image files."); return; }
    if (!book) newBook();
    pushUndo();
    const used = new Set(book.pages.map(p => p.image));
    for (const file of files) {
      let name = file.name.replace(/\s+/g, "-");
      if (used.has(name)) {
        const dot = name.lastIndexOf(".");
        let n = 2;
        while (used.has(`${name.slice(0, dot)}-${n}${name.slice(dot)}`)) n++;
        name = `${name.slice(0, dot)}-${n}${name.slice(dot)}`;
      }
      used.add(name);
      const img = await CR.loadImage(URL.createObjectURL(file));
      book.pages.push({ image: name, stops: [], _img: img, _file: file });
    }
    pageIndex = book.pages.length - files.length;
    selected = -1;
    setDirty();
    renderAll();
    say(`Added ${files.length} page${files.length > 1 ? "s" : ""}. Drag on a page to add stops.`);
  }

  async function openFolder() {
    if (!window.showDirectoryPicker) {
      say("This browser can't open folders. Use Chrome or Edge on a computer, or use New book and add images.", 6000);
      return;
    }
    let handle;
    try { handle = await window.showDirectoryPicker({ mode: "readwrite" }); }
    catch (e) { if (e.name !== "AbortError") say("Couldn't open a folder here: " + e.message, 6000); return; }
    try {
      const files = {};
      for await (const [name, h] of handle.entries()) if (h.kind === "file") files[name] = h;
      let raw;
      if (files["book.json"]) {
        raw = JSON.parse(await (await files["book.json"].getFile()).text());
      } else {
        const images = Object.keys(files).filter(n => /\.(png|jpe?g|webp|gif)$/i.test(n) && !/^cover\./i.test(n)).sort(natural);
        raw = { id: slugify(handle.name), title: handle.name, focusStyle: "vignette", pages: images.map(image => ({ image, stops: [] })) };
      }
      const b = CR.normalizeBook(raw);
      for (const p of b.pages) {
        if (!files[p.image]) throw new Error("book.json lists " + p.image + " but it isn't in the folder");
        p._img = await CR.loadImage(URL.createObjectURL(await files[p.image].getFile()));
      }
      dirHandle = handle;
      setBook(b);
      say(files["book.json"] ? `Opened ${handle.name}. Save writes book.json back into it.`
                             : `Made a new book from ${b.pages.length} images in ${handle.name}.`);
    } catch (e) {
      say("Couldn't read that folder: " + e.message, 6000);
    }
  }

  // ---------------------------------------------------------------- saving

  async function writeFile(dir, name, data) {
    const fh = await dir.getFileHandle(name, { create: true });
    const w = await fh.createWritable();
    await w.write(data);
    await w.close();
  }

  async function exists(dir, name) {
    try { await dir.getFileHandle(name); return true; } catch (_) { return false; }
  }

  async function save() {
    if (!book) return;
    if (!book.id) book.id = slugify(book.title) || "my-book";
    if (!book.cover && book.pages[0]) book.cover = book.pages[0].image;
    const json = JSON.stringify(CR.serializeBook(book), null, 2);

    // First save of a book that didn't come from a folder: offer to pick one.
    if (!dirHandle && window.showDirectoryPicker) {
      try {
        say("Pick the folder to save this book in (for example content/books/" + book.id + ").", 6000);
        dirHandle = await window.showDirectoryPicker({ mode: "readwrite" });
      } catch (e) {
        if (e.name === "AbortError") return;
        dirHandle = null; // not allowed here: fall through to export
      }
    }

    if (dirHandle) {
      try {
        const names = [...book.pages.map(p => p.image), book.cover];
        for (const name of new Set(names)) {
          if (!name || await exists(dirHandle, name)) continue;
          const p = book.pages.find(pg => pg.image === name);
          let data = p && p._file;
          if (!data) {
            const src = p ? p._img.src : book._dir ? book._dir + name : null;
            if (src) data = await (await fetch(src)).blob();
          }
          if (data) await writeFile(dirHandle, name, data);
        }
        await writeFile(dirHandle, "book.json", json);
        book.pages.forEach(p => delete p._file);
        setDirty(false);
        say(`Saved in the folder "${dirHandle.name}". If it's a new book, add "${book.id}" to content/library.json.`, 6000);
        return;
      } catch (e) {
        say("Couldn't save to the folder (" + e.message + "). Showing book.json instead.", 6000);
      }
    }

    // No folder access: try a download, and always show the text so it can be copied.
    try {
      const a = document.createElement("a");
      a.href = URL.createObjectURL(new Blob([json], { type: "application/json" }));
      a.download = "book.json";
      document.body.append(a);
      a.click();
      a.remove();
    } catch (_) { /* downloads blocked */ }
    showJson();
    const newImages = book.pages.filter(p => p._file).map(p => p.image);
    if (newImages.length) say("Also copy these images into the book folder: " + newImages.join(", "), 8000);
    setDirty(false, "Exported book.json");
  }

  function showJson() {
    if (!book) return;
    if (!book.id) book.id = slugify(book.title) || "my-book";
    $("jsonText").value = JSON.stringify(CR.serializeBook(book), null, 2);
    $("jsonId").textContent = book.id;
    $("jsonBox").classList.remove("hidden");
  }

  // ---------------------------------------------------------------- page view and drawing

  const canvas = $("editCanvas");
  const ctx = canvas.getContext("2d");
  let view = null;
  let drag = null;

  function computeView() {
    const p = page();
    if (!p) return null;
    const W = canvas.clientWidth, H = canvas.clientHeight - 36; // room for the bottom bar
    const m = 28, img = p._img;
    const s = Math.min((W - 2 * m) / img.naturalWidth, (H - 2 * m) / img.naturalHeight);
    const pw = img.naturalWidth * s, ph = img.naturalHeight * s;
    return { ox: (W - pw) / 2, oy: (H - ph) / 2, pw, ph };
  }

  const toScreen = b => ({ x: view.ox + b.x * view.pw, y: view.oy + b.y * view.ph, w: b.w * view.pw, h: b.h * view.ph });
  const toPage = (px, py) => ({ x: (px - view.ox) / view.pw, y: (py - view.oy) / view.ph });

  /** What a screen of the chosen shape would show around a box, in page fractions. */
  function screenArea(b) {
    const aspect = parseFloat($("device").value);
    if (!aspect) return null;
    const img = page()._img, A = img.naturalWidth / img.naturalHeight;
    const cam = CR.inflate(CR.rect(b.x * A, b.y, b.w * A, b.h), settings.padding);
    const v = CR.fitToAspect(cam, aspect);
    return { x: v.x / A, y: v.y, w: v.w / A, h: v.h };
  }

  function label(x, y, text, color) {
    ctx.font = "bold 13px system-ui, sans-serif";
    const w = Math.max(22, ctx.measureText(text).width + 12);
    ctx.fillStyle = color;
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, 22, 11); else ctx.rect(x, y, w, 22);
    ctx.fill();
    ctx.fillStyle = "#fff";
    ctx.fillText(text, x + (w - ctx.measureText(text).width) / 2, y + 15);
  }

  function arrow(x0, y0, x1, y1, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(x0, y0);
    ctx.lineTo(x1, y1);
    ctx.stroke();
    const a = Math.atan2(y1 - y0, x1 - x0);
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x1 - 14 * Math.cos(a - 0.4), y1 - 14 * Math.sin(a - 0.4));
    ctx.lineTo(x1 - 14 * Math.cos(a + 0.4), y1 - 14 * Math.sin(a + 0.4));
    ctx.fill();
  }

  function draw() {
    const dpr = window.devicePixelRatio || 1;
    const W = canvas.clientWidth, H = canvas.clientHeight;
    if (canvas.width !== Math.round(W * dpr) || canvas.height !== Math.round(H * dpr)) {
      canvas.width = Math.round(W * dpr);
      canvas.height = Math.round(H * dpr);
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, W, H);
    const p = page();
    view = computeView();
    if (!p || !view) return;

    ctx.drawImage(p._img, view.ox, view.oy, view.pw, view.ph);

    p.stops.forEach((s, i) => {
      const color = stopColor(i), sel = i === selected;
      const r = toScreen(s.box);
      if (sel) { ctx.fillStyle = color + "26"; ctx.fillRect(r.x, r.y, r.w, r.h); }
      ctx.strokeStyle = color;
      ctx.lineWidth = sel ? 4 : 2.5;
      ctx.setLineDash([]);
      ctx.strokeRect(r.x, r.y, r.w, r.h);
      label(r.x + 4, r.y + 4, String(i + 1), color);
      if (s.to) {
        const t = toScreen(s.to);
        ctx.setLineDash([10, 6]);
        ctx.strokeRect(t.x, t.y, t.w, t.h);
        ctx.setLineDash([]);
        arrow(r.x + r.w / 2, r.y + r.h / 2, t.x + t.w / 2, t.y + t.h / 2, color);
        label(t.x + 4, t.y + 4, (i + 1) + " end", color);
      }
      if (sel) {
        for (const part of s.to ? ["box", "to"] : ["box"]) {
          const q = toScreen(s[part]);
          for (const [hx, hy] of [[q.x, q.y], [q.x + q.w, q.y], [q.x, q.y + q.h], [q.x + q.w, q.y + q.h]]) {
            ctx.fillStyle = "#fff";
            ctx.fillRect(hx - 6, hy - 6, 12, 12);
            ctx.strokeStyle = color;
            ctx.lineWidth = 2;
            ctx.strokeRect(hx - 6, hy - 6, 12, 12);
          }
        }
      }
    });

    // Screen-size guide around the selected stop.
    const s = selected >= 0 ? p.stops[selected] : null;
    if (s) {
      for (const part of s.to ? ["box", "to"] : ["box"]) {
        const area = screenArea(s[part]);
        if (!area) continue;
        const v = toScreen(area);
        ctx.setLineDash([4, 5]);
        ctx.lineWidth = 2;
        ctx.strokeStyle = "rgba(255,255,255,.9)";
        ctx.strokeRect(v.x, v.y, v.w, v.h);
        ctx.setLineDash([]);
        ctx.font = "12px system-ui, sans-serif";
        ctx.fillStyle = "rgba(255,255,255,.9)";
        ctx.fillText("screen shows this", v.x + 4, v.y + v.h - 6);
      }
    }

    if (drag && drag.mode === "new" && drag.cur) {
      const r = toScreen(normRect(drag.start, drag.cur));
      ctx.setLineDash([6, 4]);
      ctx.strokeStyle = "#fff";
      ctx.lineWidth = 2;
      ctx.strokeRect(r.x, r.y, r.w, r.h);
      ctx.setLineDash([]);
    }
  }

  function normRect(a, b) {
    const x0 = clamp(Math.min(a.x, b.x), 0, 1), y0 = clamp(Math.min(a.y, b.y), 0, 1);
    const x1 = clamp(Math.max(a.x, b.x), 0, 1), y1 = clamp(Math.max(a.y, b.y), 0, 1);
    return { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
  }

  // ---------------------------------------------------------------- mouse editing

  /** What's under the pointer: a corner handle of the selected stop, else the smallest box containing it. */
  function hitTest(px, py) {
    const p = page();
    if (!p || !view) return null;
    if (selected >= 0 && p.stops[selected]) {
      const s = p.stops[selected];
      for (const part of s.to ? ["to", "box"] : ["box"]) {
        const r = toScreen(s[part]);
        const corners = [["nw", r.x, r.y], ["ne", r.x + r.w, r.y], ["sw", r.x, r.y + r.h], ["se", r.x + r.w, r.y + r.h]];
        for (const [h, hx, hy] of corners) {
          if (Math.abs(px - hx) <= HANDLE && Math.abs(py - hy) <= HANDLE) return { stop: selected, part, handle: h };
        }
      }
    }
    let best = null;
    p.stops.forEach((s, i) => {
      for (const part of ["box", "to"]) {
        const b = s[part];
        if (!b || (b.w > 0.95 && b.h > 0.95)) continue; // whole-page boxes are picked from the list instead
        const r = toScreen(b);
        if (px >= r.x && px <= r.x + r.w && py >= r.y && py <= r.y + r.h) {
          const area = r.w * r.h;
          if (!best || area < best.area) best = { stop: i, part, handle: null, area };
        }
      }
    });
    return best;
  }

  function localPoint(e) {
    const r = canvas.getBoundingClientRect();
    return [e.clientX - r.left, e.clientY - r.top];
  }

  canvas.addEventListener("pointerdown", e => {
    if (!page() || e.button !== 0) return;
    canvas.setPointerCapture(e.pointerId);
    const [px, py] = localPoint(e);
    const pt = toPage(px, py);
    const hit = e.shiftKey ? null : hitTest(px, py);
    if (hit) {
      selected = hit.stop;
      pushUndo();
      drag = { mode: hit.handle ? "resize" : "move", ...hit, start: pt, orig: { ...page().stops[hit.stop][hit.part] }, moved: false };
      renderSide();
    } else {
      drag = { mode: "new", start: pt, cur: null };
    }
    draw();
  });

  canvas.addEventListener("pointermove", e => {
    const [px, py] = localPoint(e);
    if (!drag) {
      const hit = page() ? hitTest(px, py) : null;
      canvas.style.cursor = !page() ? "default"
        : !hit ? "crosshair"
        : hit.handle ? (hit.handle === "nw" || hit.handle === "se" ? "nwse-resize" : "nesw-resize")
        : "move";
      return;
    }
    const pt = toPage(px, py);
    if (drag.mode === "new") { drag.cur = pt; draw(); return; }
    const dx = pt.x - drag.start.x, dy = pt.y - drag.start.y;
    if (Math.abs(dx) + Math.abs(dy) > 0.002) drag.moved = true;
    const o = drag.orig, s = page().stops[drag.stop];
    if (drag.mode === "move") {
      s[drag.part] = { x: clamp(o.x + dx, 0, 1 - o.w), y: clamp(o.y + dy, 0, 1 - o.h), w: o.w, h: o.h };
    } else {
      let x0 = o.x, y0 = o.y, x1 = o.x + o.w, y1 = o.y + o.h;
      if (drag.handle.includes("w")) x0 += dx; else x1 += dx;
      if (drag.handle.includes("n")) y0 += dy; else y1 += dy;
      const r = normRect({ x: x0, y: y0 }, { x: x1, y: y1 });
      if (r.w >= MIN && r.h >= MIN) s[drag.part] = r;
    }
    draw();
  });

  canvas.addEventListener("pointerup", () => {
    if (!drag) return;
    const d = drag;
    drag = null;
    if (d.mode === "new") {
      const r = d.cur ? normRect(d.start, d.cur) : null;
      if (r && r.w > MIN && r.h > MIN) {
        change(() => {
          const at = selected >= 0 ? selected + 1 : page().stops.length;
          page().stops.splice(at, 0, { box: r });
          selected = at;
        });
      } else {
        selected = -1;
        renderAll();
      }
      return;
    }
    if (!d.moved) undoStack.pop(); else setDirty();
    renderAll();
  });

  // ---------------------------------------------------------------- side panels

  function renderBookFields() {
    const on = !!book;
    for (const id of ["fTitle", "fId", "fAuthor", "fStyle"]) $(id).disabled = !on;
    if (!on) return;
    $("fTitle").value = book.title || "";
    $("fId").value = book.id || "";
    $("fId").placeholder = slugify(book.title) || "my-book";
    $("fAuthor").value = book.author || "";
    $("fStyle").value = book.focusStyle || "dim";
  }

  function renderPages() {
    const root = $("pages");
    root.innerHTML = "";
    if (!book) return;
    book.pages.forEach((p, i) => {
      const item = document.createElement("div");
      item.className = "page-item" + (i === pageIndex ? " active" : "");
      const img = document.createElement("img");
      img.src = p._img.src;
      img.alt = "";
      item.append(img);
      if (book.cover === p.image) {
        const tag = document.createElement("span");
        tag.className = "cover-tag";
        tag.textContent = "Cover";
        item.append(tag);
      }
      const meta = document.createElement("div");
      meta.className = "meta";
      const name = document.createElement("span");
      name.textContent = `Page ${i + 1} · ${p.stops.length} stop${p.stops.length === 1 ? "" : "s"}`;
      meta.append(name);
      const btn = (text, title, fn) => {
        const b = document.createElement("button");
        b.textContent = text;
        b.title = title;
        b.onclick = ev => { ev.stopPropagation(); fn(); };
        meta.append(b);
      };
      btn("↑", "Move page earlier", () => i > 0 && change(() => { swap(book.pages, i, i - 1); pageIndex = i - 1; }));
      btn("↓", "Move page later", () => i < book.pages.length - 1 && change(() => { swap(book.pages, i, i + 1); pageIndex = i + 1; }));
      btn("★", "Use as cover", () => change(() => { book.cover = p.image; }));
      btn("✕", "Remove page", () => change(() => {
        book.pages.splice(i, 1);
        pageIndex = clamp(pageIndex, 0, Math.max(0, book.pages.length - 1));
        selected = -1;
      }));
      item.append(meta);
      item.onclick = () => { pageIndex = i; selected = -1; renderAll(); };
      root.append(item);
    });
  }

  const swap = (arr, a, b) => { [arr[a], arr[b]] = [arr[b], arr[a]]; };

  function stopSummary(s) {
    const bits = [];
    if (s.box.w > 0.95 && s.box.h > 0.95) bits.push("Whole page");
    if (s.to) bits.push("Pan " + (s.panSeconds || 4) + "s");
    if (s.style) bits.push(STYLE_NAMES[s.style]);
    if (s.caption) bits.push("“" + s.caption + "”");
    return bits.join(" · ") || "Stop";
  }

  function renderStopList() {
    const root = $("stopList");
    root.innerHTML = "";
    const p = page();
    $("stopsTitle").textContent = p ? `Stops on page ${pageIndex + 1}` : "Stops";
    if (!p) return;
    if (!p.stops.length) {
      const h = document.createElement("p");
      h.className = "help";
      h.textContent = "No stops yet. Drag on the page to draw one. Stops play in the order listed here.";
      root.append(h);
    }
    p.stops.forEach((s, i) => {
      const row = document.createElement("div");
      row.className = "stop-row" + (i === selected ? " active" : "");
      const num = document.createElement("span");
      num.className = "num";
      num.style.background = stopColor(i);
      num.textContent = i + 1;
      const txt = document.createElement("span");
      txt.className = "txt";
      txt.textContent = stopSummary(s);
      row.append(num, txt);
      row.onclick = () => { selected = i; renderAll(); };
      root.append(row);
    });
    const add = document.createElement("div");
    add.className = "inline";
    const whole = document.createElement("button");
    whole.className = "btn small";
    whole.textContent = "+ Whole page stop";
    whole.title = "A stop that shows the whole page, e.g. to end on the full picture";
    whole.onclick = () => change(() => {
      const at = selected >= 0 ? selected + 1 : p.stops.length;
      p.stops.splice(at, 0, { box: { x: 0, y: 0, w: 1, h: 1 } });
      selected = at;
    });
    add.append(whole);
    root.append(add);
  }

  function renderInspector() {
    const root = $("inspector");
    root.innerHTML = "";
    const p = page();
    if (!book) return;
    if (!p) {
      root.innerHTML = '<p class="help">Add page images to start.</p>';
      return;
    }
    const s = selected >= 0 ? p.stops[selected] : null;
    if (!s) {
      root.innerHTML = `<h2>How to</h2><p class="help">
        Drag on the page to draw a stop. Click a stop to select it, drag it to move, drag a white corner to resize.
        Hold Shift to draw a new stop on top of an existing one.<br><br>
        Keys: Delete removes a stop, arrow keys nudge it (Shift for bigger steps), P previews, Ctrl+Z undoes,
        Page Up/Down change page.</p>`;
      return;
    }
    const color = stopColor(selected);
    const el = (html) => { const t = document.createElement("template"); t.innerHTML = html.trim(); return t.content.firstChild; };

    root.append(el(`<h2 style="color:${color}">Stop ${selected + 1}</h2>`));

    const actions = el('<div class="inline"></div>');
    const act = (text, fn, title) => { const b = el(`<button class="btn small">${text}</button>`); if (title) b.title = title; b.onclick = fn; actions.append(b); };
    act("Earlier", () => selected > 0 && change(() => { swap(p.stops, selected, selected - 1); selected--; }));
    act("Later", () => selected < p.stops.length - 1 && change(() => { swap(p.stops, selected, selected + 1); selected++; }));
    act("Delete", () => change(() => { p.stops.splice(selected, 1); selected = -1; }));
    act("▶ Preview", () => openPreview(selected), "Play the move into this stop");
    root.append(actions);

    // Focus style
    root.append(el('<label class="field">What happens around it</label>'));
    const style = document.createElement("select");
    style.add(new Option(`Book default (${STYLE_NAMES[book.focusStyle] || book.focusStyle})`, ""));
    for (const [v, t] of Object.entries(STYLE_NAMES)) style.add(new Option(t, v));
    style.value = s.style || "";
    style.onchange = () => change(() => { if (style.value) s.style = style.value; else delete s.style; });
    root.append(style);

    // Pan
    const panRow = el('<label class="check"><input type="checkbox"> Pan to a second box</label>');
    const panBox = panRow.querySelector("input");
    panBox.checked = !!s.to;
    panBox.onchange = () => change(() => {
      if (panBox.checked) {
        // Start the end box next to the start box so it's easy to grab.
        const b = s.box;
        const nx = b.x + b.w + 0.02 <= 1 - b.w ? b.x + b.w * 0.6 : Math.max(0, b.x - b.w * 0.6);
        s.to = { x: clamp(nx, 0, 1 - b.w), y: b.y, w: b.w, h: b.h };
        s.panSeconds = s.panSeconds || 4;
      } else {
        delete s.to;
        delete s.panSeconds;
      }
    });
    root.append(panRow);
    if (s.to) {
      root.append(el('<p class="help">The camera arrives at the solid box, then glides to the dashed box. Drag the dashed box to where it should end.</p>'));
      const lab = el(`<label class="field">Glide time <output>${s.panSeconds || 4} s</output></label>`);
      const range = el('<input type="range" min="1" max="15" step="0.5" style="width:100%">');
      range.value = s.panSeconds || 4;
      range.onpointerdown = () => pushUndo();
      range.oninput = () => { s.panSeconds = +range.value; lab.querySelector("output").textContent = range.value + " s"; setDirty(); renderStopList(); };
      root.append(lab, range);
    }

    // Caption
    root.append(el('<label class="field">Caption (leave empty if the text is in the picture)</label>'));
    const cap = document.createElement("textarea");
    cap.rows = 3;
    cap.value = s.caption || "";
    cap.onfocus = () => pushUndo();
    cap.oninput = () => { if (cap.value) s.caption = cap.value; else delete s.caption; setDirty(); renderStopList(); };
    root.append(cap);

    const sizeRow = el('<div class="inline"></div>');
    const full = el('<button class="btn small">Make it the whole page</button>');
    full.onclick = () => change(() => { s.box = { x: 0, y: 0, w: 1, h: 1 }; });
    sizeRow.append(full);
    root.append(sizeRow);
  }

  function renderSide() {
    renderStopList();
    renderInspector();
  }

  function renderAll() {
    renderBookFields();
    renderPages();
    renderSide();
    const hasPage = !!page();
    $("empty").classList.toggle("hidden", hasPage);
    $("viewbar").classList.toggle("hidden", !hasPage);
    draw();
  }

  // Book fields
  for (const [id, key] of [["fTitle", "title"], ["fId", "id"], ["fAuthor", "author"]]) {
    $(id).addEventListener("focus", () => pushUndo());
    $(id).addEventListener("input", () => {
      book[key] = id === "fId" ? slugify($(id).value) : $(id).value;
      if (id === "fTitle") $("fId").placeholder = slugify(book.title) || "my-book";
      setDirty();
    });
  }
  $("fId").addEventListener("blur", () => { if (book) $("fId").value = book.id || ""; });
  $("fStyle").addEventListener("change", () => change(() => { book.focusStyle = $("fStyle").value; }));

  // ---------------------------------------------------------------- preview

  let preview = null;

  function updatePreviewUi(st) {
    $("pvPage").textContent = `Page ${st.page + 1} of ${st.pageCount}`;
    $("pvWhole").textContent = st.wholePage ? "Stops" : "Whole page";
    $("pvPrev").disabled = !st.canGoBack;
    $("pvEnd").classList.toggle("hidden", !st.finished);
    $("pvCaption").textContent = st.caption;
    $("pvCaption").classList.toggle("hidden", !st.caption);
  }

  /** Opens the reader on the book as it is now. With a stop, plays the move into that stop. */
  function openPreview(stop) {
    if (!book || !book.pages.length) return;
    settings = CR.loadSettings();
    $("msg").classList.add("hidden");
    if (!preview) preview = new CR.Reader($("previewCanvas"), { settings, onChange: updatePreviewUi });
    preview.settings = settings;
    $("preview").classList.remove("hidden");
    preview.setBook(CR.normalizeBook(CR.serializeBook(book)), book.pages.map(p => p._img));
    if (stop === undefined || stop < 0) { preview.jumpTo(pageIndex, -1); return; }
    preview.jumpTo(pageIndex, stop);
    if (preview.stepIndex > 0) {
      const before = preview.steps[preview.stepIndex - 1];
      preview.jumpTo(before.page, before.stop);
      preview.next();
    }
  }

  function closePreview() {
    $("preview").classList.add("hidden");
    if (preview) preview.settings.autoPlay = false;
  }

  document.querySelectorAll("#preview button").forEach(b => b.addEventListener("mousedown", e => e.preventDefault()));
  $("pvClose").onclick = closePreview;
  $("pvBack").onclick = closePreview;
  $("pvPrev").onclick = () => preview.prev();
  $("pvNext").onclick = () => preview.next();
  $("pvWhole").onclick = () => preview.toggleOverview();
  $("pvAgain").onclick = () => preview.restart();

  // ---------------------------------------------------------------- header, keys, drag and drop

  $("newBook").onclick = () => okToDiscard() && newBook();
  $("openFolder").onclick = () => okToDiscard() && openFolder();
  $("openSelect").onchange = () => {
    const id = $("openSelect").value;
    $("openSelect").value = "";
    if (id && okToDiscard()) openFromLibrary(id);
  };
  $("addPages").onclick = () => $("fileInput").click();
  $("fileInput").onchange = () => { addFiles($("fileInput").files); $("fileInput").value = ""; };
  $("save").onclick = save;
  $("undo").onclick = undo;
  $("previewBtn").onclick = () => openPreview(selected >= 0 ? selected : undefined);
  $("showJson").onclick = showJson;
  $("closeJson").onclick = () => $("jsonBox").classList.add("hidden");
  $("copyJson").onclick = async () => {
    try { await navigator.clipboard.writeText($("jsonText").value); say("Copied book.json"); }
    catch (_) { $("jsonText").select(); say("Copy was blocked. The text is selected: press Ctrl+C."); }
  };
  $("device").onchange = () => { $("device").blur(); draw(); };

  document.addEventListener("keydown", e => {
    const typing = ["INPUT", "TEXTAREA", "SELECT"].includes(e.target.tagName);
    const mod = e.ctrlKey || e.metaKey;
    if (mod && (e.key === "s" || e.key === "S")) { e.preventDefault(); save(); return; }

    if (!$("preview").classList.contains("hidden")) {
      if (e.key === "Escape") closePreview(); else preview.handleKey(e);
      return;
    }
    if (typing) return;
    if (mod && (e.key === "z" || e.key === "Z")) { e.preventDefault(); undo(); return; }
    if (!book) return;
    const p = page();
    const s = p && selected >= 0 ? p.stops[selected] : null;

    if (e.key === "Escape") { $("jsonBox").classList.add("hidden"); selected = -1; renderAll(); }
    else if ((e.key === "Delete" || e.key === "Backspace") && s) { e.preventDefault(); change(() => { p.stops.splice(selected, 1); selected = -1; }); }
    else if (e.key === "p" || e.key === "P") openPreview(selected >= 0 ? selected : undefined);
    else if (e.key === "PageDown" && pageIndex < book.pages.length - 1) { pageIndex++; selected = -1; renderAll(); }
    else if (e.key === "PageUp" && pageIndex > 0) { pageIndex--; selected = -1; renderAll(); }
    else if (s && e.key.startsWith("Arrow")) {
      e.preventDefault();
      const step = e.shiftKey ? 0.01 : 0.002;
      const dx = e.key === "ArrowLeft" ? -step : e.key === "ArrowRight" ? step : 0;
      const dy = e.key === "ArrowUp" ? -step : e.key === "ArrowDown" ? step : 0;
      change(() => { s.box = { ...s.box, x: clamp(s.box.x + dx, 0, 1 - s.box.w), y: clamp(s.box.y + dy, 0, 1 - s.box.h) }; });
    }
  });

  let dragDepth = 0;
  window.addEventListener("dragenter", e => { if ([...e.dataTransfer.types].includes("Files")) { dragDepth++; $("dropHint").classList.remove("hidden"); } });
  window.addEventListener("dragleave", () => { if (--dragDepth <= 0) { dragDepth = 0; $("dropHint").classList.add("hidden"); } });
  window.addEventListener("dragover", e => e.preventDefault());
  window.addEventListener("drop", e => {
    e.preventDefault();
    dragDepth = 0;
    $("dropHint").classList.add("hidden");
    if (e.dataTransfer.files.length) addFiles(e.dataTransfer.files);
  });

  window.addEventListener("beforeunload", e => { if (dirty) e.preventDefault(); });
  new ResizeObserver(draw).observe($("center"));

  // Books available from content/ (when served from the repo or hosted with the playground).
  Content.listBooks().then(list => {
    for (const b of list) $("openSelect").add(new Option(b.title, b.id));
  }).catch(() => {
    $("openSelect").options[0].text = "No library found";
  });

  renderAll();
  // For automated tests.
  window.__editor = { get book() { return book; }, get selected() { return selected; }, get pageIndex() { return pageIndex; } };
})();
