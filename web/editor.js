/* Book editor: draw focus stops on page images, set their order and options, preview, save book.json. */
"use strict";
(function () {
  const CR = window.ComicReader;
  const $ = id => document.getElementById(id);
  const COLORS = ["#ff5a36", "#2f9bff", "#21c07a", "#ffb800", "#c45bff", "#ff4fa3", "#00c2c7", "#9acd32"];
  const MIN = 0.01;          // smallest box, as a fraction of the page
  const HANDLE = 9;          // corner handle grab size, in screen pixels
  const STYLE_NAMES = { dim: "Dim the rest", vignette: "Soft dark edges", spotlight: "Spotlight (everything else dark)", none: "Nothing, just frame it" };

  // The open book. Pages also carry _img (the loaded image) and _file (a File added from disk, until saved).
  let book = null;
  let pageIndex = 0;
  let selected = -1;         // stop index on the current page, -1 for none
  let selHot = -1;           // hotspot index on the current page, -1 for none (never both)
  let drawKind = "stop";     // what dragging on the page creates: "stop", "hot", or "dry" (an area rain misses)
  let dirHandle = null;      // folder from "Open folder" or the first "Save", when the browser allows it
  let dirty = false;
  let settings = CR.loadSettings();

  // "Everything" shows every tool at once. "Step by step" takes one job at a time.
  const UI_KEY = "book-editor-ui";
  const STEPS = {
    pages: ["Pages", "Add your page pictures and put them in order. For each page, choose whether the camera guides it or the child explores it."],
    camera: ["Camera", "Drag on the page to draw where the camera stops. Stops play left to right along the bottom. Click one to set its timing and its look."],
    words: ["Words", "Type what is read at each stop. Leave a stop empty if it needs no words."],
    effects: ["Effects", "Add weather that moves over the picture. Skip this if the page doesn't need any."],
    play: ["Play", "Watch the page the way a child will see it, then save."],
  };
  let uiMode = "free", step = "pages";
  try {
    const u = JSON.parse(localStorage.getItem(UI_KEY) || "{}");
    if (u.mode === "guided") uiMode = "guided";
    if (STEPS[u.step]) step = u.step;
  } catch (_) { /* storage blocked: start with everything */ }
  const undoStack = [];

  const page = () => (book && book.pages[pageIndex]) || null;
  const stopColor = i => COLORS[i % COLORS.length];
  const HOT = "#ffd23f";
  const DRY = "#7fd3f2";
  const LAYER = "#b08cff";
  const newPage = (image, extra = {}) => ({ image, mode: "guided", stops: [], hotspots: [], guidance: { hints: true, showMe: true }, ...extra });
  const isExplore = () => !!page() && page().mode === "explore";
  let selLayer = -1;         // picture-on-top index on the current page, -1 for none
  let dryTarget = null;      // the effect a newly drawn dry area belongs to
  let clipItem = null;       // a copied stop or thing to find: { kind, data }
  const assets = new Map();  // files added this session that aren't in the book folder yet: name -> { file, url }
  const layerImgs = new Map(); // layer pictures for drawing on the page: name -> Image (or null while loading)
  const FX_NAMES = { rain: "Rain", snow: "Snow", leaves: "Falling leaves", sparkles: "Sparkles", lightning: "Lightning", shake: "Camera shake" };
  const FALLING = ["rain", "snow", "leaves", "sparkles"];
  function select(kind, i) { selected = kind === "stop" ? i : -1; selHot = kind === "hot" ? i : -1; selLayer = kind === "layer" ? i : -1; }
  const clone = o => JSON.parse(JSON.stringify(o));
  const assetUrl = name => (assets.get(name) && assets.get(name).url) || (book && book._dir ? book._dir + name : "");
  /** A layer's picture, loaded the first time it's needed; the page redraws when it arrives. */
  function layerImg(name) {
    if (!layerImgs.has(name)) {
      layerImgs.set(name, null);
      const src = assetUrl(name);
      if (src) CR.loadImage(src).then(img => { layerImgs.set(name, img); draw(); }).catch(() => {});
    }
    return layerImgs.get(name);
  }
  /** Keep a newly chosen file under a name no other file in the book uses. */
  function addAsset(file) {
    let name = file.name.replace(/[^\w.\-]+/g, "_"), n = 1;
    const dot = name.lastIndexOf("."), stem = dot > 0 ? name.slice(0, dot) : name, ext = dot > 0 ? name.slice(dot) : "";
    const taken = nm => assets.has(nm) || (book && book.pages.some(pg => pg.image === nm));
    while (taken(name)) name = `${stem}-${++n}${ext}`;
    assets.set(name, { file, url: URL.createObjectURL(file) });
    return name;
  }
  function pickFile(accept, then) {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = accept;
    input.onchange = () => { if (input.files[0]) then(input.files[0]); };
    input.click();
  }
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
      pages: book.pages.map(p => ({
        ...p,
        stops: JSON.parse(JSON.stringify(p.stops)),
        hotspots: JSON.parse(JSON.stringify(p.hotspots || [])),
        guidance: { ...(p.guidance || {}) },
        effects: JSON.parse(JSON.stringify(p.effects || [])),
        layers: JSON.parse(JSON.stringify(p.layers || [])),
      })),
      pageIndex, selected, selHot, selLayer,
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
    selHot = s.selHot;
    selLayer = s.selLayer === undefined ? -1 : s.selLayer;
    dryTarget = null;
    if (drawKind === "dry") drawKind = "stop";
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
    layerImgs.clear();
    pageIndex = 0;
    select(null);
    drawKind = "stop";
    undoStack.length = 0;
    setDirty(false, b.pages.length ? "Opened" : "New book");
    renderAll();
  }

  async function openFromLibrary(id) {
    try {
      const { book: b, images, dir } = await Content.loadBook(id);
      assets.clear();
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
    assets.clear();
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
      book.pages.push(newPage(name, { _img: img, _file: file }));
    }
    pageIndex = book.pages.length - files.length;
    select(null);
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
      // Pictures on top and sounds the book already uses, so they show and play here.
      assets.clear();
      for (const pg of b.pages) {
        const used = [...(pg.layers || []).map(l => l.image), ...pg.stops.map(st => st.sound), ...pg.hotspots.map(h => h.sound)];
        for (const name of used) if (name && files[name] && !assets.has(name)) assets.set(name, { url: URL.createObjectURL(await files[name].getFile()) });
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

  /** Every picture-on-top and sound file the book uses. */
  function extraFiles() {
    const names = new Set();
    for (const pg of book.pages) {
      for (const l of pg.layers || []) names.add(l.image);
      for (const st of pg.stops) if (st.sound) names.add(st.sound);
      for (const h of pg.hotspots || []) if (h.sound) names.add(h.sound);
    }
    return [...names];
  }

  /**
   * Cut a big page into tiles: a small overview picture, then the picture at full and at half size in
   * 1024-pixel squares. The reader loads only the squares under the camera.
   */
  async function cutTiles(dir, p) {
    const img = p._img, W = img.naturalWidth, H = img.naturalHeight, SIZE = 1024, LEVELS = 2;
    const base = (p._source || p.image).replace(/\.[^.]+$/, "").replace(/_overview$/, "");
    const cv = document.createElement("canvas"), c = cv.getContext("2d");
    const shot = (w, h, paint) => { cv.width = w; cv.height = h; paint(); return new Promise(done => cv.toBlob(done, "image/jpeg", 0.85)); };
    const tilesDir = await dir.getDirectoryHandle(base + "_tiles", { create: true });
    let count = 0;
    for (let z = 0; z < LEVELS; z++) {
      const f = 2 ** z, lw = Math.ceil(W / f), lh = Math.ceil(H / f);
      const zDir = await tilesDir.getDirectoryHandle(String(z), { create: true });
      for (let ty = 0; ty * SIZE < lh; ty++) {
        for (let tx = 0; tx * SIZE < lw; tx++) {
          const w = Math.min(SIZE, lw - tx * SIZE), h = Math.min(SIZE, lh - ty * SIZE);
          await writeFile(zDir, `${tx}_${ty}.jpg`, await shot(w, h, () => c.drawImage(img, tx * SIZE * f, ty * SIZE * f, w * f, h * f, 0, 0, w, h)));
          count++;
          if (count % 12 === 0) say(`Cutting tiles for ${base}: ${count} done...`, 60000);
        }
      }
    }
    const ow = 2048, oh = Math.round(ow * H / W), overview = base + "_overview.jpg";
    await writeFile(dir, overview, await shot(ow, oh, () => c.drawImage(img, 0, 0, ow, oh)));
    if (book.cover === p.image) book.cover = overview;
    p._source = p._source || p.image;
    p.image = overview;
    p.tiles = { width: W, height: H, size: SIZE, levels: LEVELS, path: `${base}_tiles/{z}/{x}_{y}.jpg` };
    delete p._tile;
    return count;
  }

  async function save() {
    if (!book) return;
    if (!book.id) book.id = slugify(book.title) || "my-book";
    if (!book.cover && book.pages[0]) book.cover = book.pages[0].image;
    let json = JSON.stringify(CR.serializeBook(book), null, 2);

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
        let tiles = 0;
        for (const pg of book.pages) if (pg._tile) tiles += await cutTiles(dirHandle, pg);
        json = JSON.stringify(CR.serializeBook(book), null, 2);
        const names = [...book.pages.map(p => p.image), book.cover, ...extraFiles()];
        for (const name of new Set(names)) {
          if (!name || await exists(dirHandle, name)) continue;
          const p = book.pages.find(pg => pg.image === name);
          let data = (p && p._file) || (assets.get(name) && assets.get(name).file);
          if (!data) {
            const src = p ? p._img.src : book._dir ? book._dir + name : null;
            if (src) data = await (await fetch(src)).blob();
          }
          if (data) await writeFile(dirHandle, name, data);
        }
        await writeFile(dirHandle, "book.json", json);
        book.pages.forEach(p => delete p._file);
        setDirty(false);
        say(`Saved in the folder "${dirHandle.name}"${tiles ? `, with ${tiles} tiles cut` : ""}. If it's a new book, add "${book.id}" to content/library.json.`, 6000);
        renderAll();
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
    const newImages = [...book.pages.filter(p => p._file).map(p => p.image), ...extraFiles().filter(n => assets.get(n) && assets.get(n).file)];
    if (newImages.length) say("Also copy these files into the book folder: " + newImages.join(", "), 8000);
    if (book.pages.some(pg => pg._tile)) say("Tiles can only be cut when saving to a folder, in Chrome or Edge on a computer.", 8000);
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

    // Pictures on top of the page. The selected one shows where it moves to as a ghost.
    (p.layers || []).forEach((l, i) => {
      const img = layerImg(l.image), r = toScreen(l.box), sel = i === selLayer;
      if (img) ctx.drawImage(img, r.x, r.y, r.w, r.h);
      if (sel || !img) {
        ctx.strokeStyle = LAYER;
        ctx.lineWidth = sel ? 3 : 1.5;
        ctx.setLineDash([]);
        ctx.strokeRect(r.x, r.y, r.w, r.h);
      }
      if (sel) {
        label(r.x + 4, r.y + 4, "picture " + (i + 1), "#7a4fd6");
        for (const [hx, hy] of [[r.x, r.y], [r.x + r.w, r.y], [r.x, r.y + r.h], [r.x + r.w, r.y + r.h]]) {
          ctx.fillStyle = "#fff"; ctx.fillRect(hx - 6, hy - 6, 12, 12);
          ctx.strokeStyle = LAYER; ctx.lineWidth = 2; ctx.strokeRect(hx - 6, hy - 6, 12, 12);
        }
        if (l.moveTo) {
          const t = toScreen(l.moveTo);
          if (img) { ctx.globalAlpha = 0.45; ctx.drawImage(img, t.x, t.y, t.w, t.h); ctx.globalAlpha = 1; }
          ctx.strokeStyle = LAYER; ctx.lineWidth = 2.5; ctx.setLineDash([7, 5]);
          ctx.strokeRect(t.x, t.y, t.w, t.h);
          ctx.setLineDash([]);
          label(t.x + 4, t.y + 4, "moves to", "#7a4fd6");
          arrow(r.x + r.w / 2, r.y + r.h / 2, t.x + t.w / 2, t.y + t.h / 2, LAYER);
        }
      }
    });

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

    // Areas the rain misses.
    for (const e of p.effects || []) {
      for (const b of e.dry || []) {
        const r = toScreen(b);
        ctx.strokeStyle = DRY;
        ctx.lineWidth = 2;
        ctx.setLineDash([2, 5]);
        ctx.strokeRect(r.x, r.y, r.w, r.h);
        ctx.setLineDash([]);
        label(r.x + 4, r.y + 4, "dry", "#2a7f9e");
      }
    }

    // Hotspots: things to find on explore pages.
    if (p.mode === "explore") {
      p.hotspots.forEach((h, i) => {
        const r = toScreen(h.box), sel = i === selHot;
        if (sel) { ctx.fillStyle = HOT + "33"; ctx.fillRect(r.x, r.y, r.w, r.h); }
        ctx.strokeStyle = HOT;
        ctx.lineWidth = sel ? 4 : 2.5;
        ctx.setLineDash([3, 4]);
        ctx.strokeRect(r.x, r.y, r.w, r.h);
        ctx.setLineDash([]);
        label(r.x + 4, r.y + 4, "★" + (i + 1), "#b8860b");
        if (sel) {
          for (const [hx, hy] of [[r.x, r.y], [r.x + r.w, r.y], [r.x, r.y + r.h], [r.x + r.w, r.y + r.h]]) {
            ctx.fillStyle = "#fff";
            ctx.fillRect(hx - 6, hy - 6, 12, 12);
            ctx.strokeStyle = "#b8860b";
            ctx.lineWidth = 2;
            ctx.strokeRect(hx - 6, hy - 6, 12, 12);
          }
        }
      });
    }

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
      ctx.strokeStyle = drawKind === "hot" ? HOT : drawKind === "dry" ? DRY : "#fff";
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
    const cornerHit = (r) => {
      const corners = [["nw", r.x, r.y], ["ne", r.x + r.w, r.y], ["sw", r.x, r.y + r.h], ["se", r.x + r.w, r.y + r.h]];
      for (const [h, hx, hy] of corners) if (Math.abs(px - hx) <= HANDLE && Math.abs(py - hy) <= HANDLE) return h;
      return null;
    };
    // A selected picture is on top of everything, so it gets the first say.
    if (selLayer >= 0 && p.layers && p.layers[selLayer]) {
      const l = p.layers[selLayer];
      const h = cornerHit(toScreen(l.box));
      if (h) return { kind: "layer", stop: selLayer, part: "box", handle: h };
      for (const part of l.moveTo ? ["moveTo", "box"] : ["box"]) {
        const r = toScreen(l[part]);
        if (px >= r.x && px <= r.x + r.w && py >= r.y && py <= r.y + r.h) return { kind: "layer", stop: selLayer, part, handle: null };
      }
    }
    if (selected >= 0 && p.stops[selected]) {
      const s = p.stops[selected];
      for (const part of s.to ? ["to", "box"] : ["box"]) {
        const h = cornerHit(toScreen(s[part]));
        if (h) return { kind: "stop", stop: selected, part, handle: h };
      }
    }
    if (selHot >= 0 && p.hotspots[selHot]) {
      const h = cornerHit(toScreen(p.hotspots[selHot].box));
      if (h) return { kind: "hot", stop: selHot, part: "box", handle: h };
    }
    let best = null;
    if (p.mode === "explore") {
      p.hotspots.forEach((h, i) => {
        const r = toScreen(h.box);
        if (px >= r.x && px <= r.x + r.w && py >= r.y && py <= r.y + r.h) {
          const area = r.w * r.h;
          if (!best || area < best.area) best = { kind: "hot", stop: i, part: "box", handle: null, area };
        }
      });
    }
    p.stops.forEach((s, i) => {
      for (const part of ["box", "to"]) {
        const b = s[part];
        if (!b || (b.w > 0.95 && b.h > 0.95)) continue; // whole-page boxes are picked from the list instead
        const r = toScreen(b);
        if (px >= r.x && px <= r.x + r.w && py >= r.y && py <= r.y + r.h) {
          const area = r.w * r.h;
          if (!best || area < best.area) best = { kind: "stop", stop: i, part, handle: null, area };
        }
      }
    });
    return best;
  }

  /** The stop or hotspot a hit or drag refers to. */
  const dragItem = d => d.kind === "hot" ? page().hotspots[d.stop] : d.kind === "layer" ? page().layers[d.stop] : page().stops[d.stop];

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
      select(hit.kind, hit.stop);
      pushUndo();
      drag = { mode: hit.handle ? "resize" : "move", ...hit, start: pt, orig: { ...dragItem(hit)[hit.part] }, moved: false };
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
    const o = drag.orig, s = dragItem(drag);
    if (drag.mode === "move") {
      // A picture may sit partly, or wholly, off the page: that's how something comes in from the side.
      const off = drag.kind === "layer";
      s[drag.part] = { x: clamp(o.x + dx, off ? -o.w : 0, off ? 1 : 1 - o.w), y: clamp(o.y + dy, off ? -o.h : 0, off ? 1 : 1 - o.h), w: o.w, h: o.h };
    } else if (drag.kind === "layer") {
      // Pictures keep their shape: the width follows the mouse and the height follows the width.
      const img = layerImg(s.image), pg = page()._img;
      const shape = img ? (img.naturalHeight / img.naturalWidth) * (pg.naturalWidth / pg.naturalHeight) : o.h / o.w;
      const w = Math.max(MIN, drag.handle.includes("w") ? o.w - dx : o.w + dx), h = w * shape;
      s.box = { x: drag.handle.includes("w") ? o.x + o.w - w : o.x, y: drag.handle.includes("n") ? o.y + o.h - h : o.y, w, h };
      if (s.moveTo) s.moveTo = { ...s.moveTo, w, h };
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
          if (drawKind === "dry" && dryTarget && (page().effects || []).includes(dryTarget)) {
            (dryTarget.dry = dryTarget.dry || []).push(r);
            drawKind = "stop";
            dryTarget = null;
            select(null);
          } else if (drawKind === "hot" && isExplore()) {
            page().hotspots.push({ box: r });
            select("hot", page().hotspots.length - 1);
          } else {
            const at = selected >= 0 ? selected + 1 : page().stops.length;
            page().stops.splice(at, 0, { box: r });
            select("stop", at);
          }
        });
      } else {
        select(null);
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
      item.draggable = true;
      item.ondragstart = ev => { ev.dataTransfer.setData("text/plain", "page:" + i); ev.dataTransfer.effectAllowed = "move"; };
      item.ondragover = ev => { ev.preventDefault(); item.classList.add("dragover"); };
      item.ondragleave = () => item.classList.remove("dragover");
      item.ondrop = ev => {
        ev.preventDefault();
        item.classList.remove("dragover");
        const data = ev.dataTransfer.getData("text/plain"), from = data.startsWith("page:") ? parseInt(data.slice(5), 10) : -1;
        if (!(from >= 0) || from === i || from >= book.pages.length) return;
        ev.stopPropagation();
        change(() => { const [moved] = book.pages.splice(from, 1); book.pages.splice(i, 0, moved); pageIndex = i; select(null); });
      };
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
      name.textContent = `Page ${i + 1} · ` + (p.mode === "explore"
        ? `explore, ${p.hotspots.length} to find`
        : `${p.stops.length} stop${p.stops.length === 1 ? "" : "s"}`);
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
        select(null);
      }));
      item.append(meta);
      item.onclick = () => { pageIndex = i; select(null); drawKind = "stop"; renderAll(); };
      root.append(item);
    });
  }

  const swap = (arr, a, b) => { [arr[a], arr[b]] = [arr[b], arr[a]]; };

  function stopSummary(s) {
    const bits = [];
    if (s.box.w > 0.95 && s.box.h > 0.95) bits.push("Whole page");
    if (s.to) bits.push("Pan " + (s.panSeconds || 4) + "s");
    if (s.style) bits.push(STYLE_NAMES[s.style]);
    if (s.moveSeconds !== undefined) bits.push(+s.moveSeconds === 0 ? "Cut" : `${s.moveSeconds}s to get here`);
    if (s.auto) bits.push(`Goes on after ${s.holdSeconds || 1.5}s`);
    if (s.caption) bits.push("“" + s.caption + "”");
    return bits.join(" · ") || "Stop";
  }

  function renderStopList() {
    const root = $("stopList");
    root.innerHTML = "";
    const p = page();
    $("stopsTitle").textContent = p ? `Page ${pageIndex + 1}` : "Page";
    if (!p) { $("fxPanel").innerHTML = $("layerPanel").innerHTML = ""; $("fxTag").textContent = $("layerTag").textContent = ""; renderTimeline(); return; }
    const el = html => { const t = document.createElement("template"); t.innerHTML = html.trim(); return t.content.firstChild; };

    // Page type and guidance
    root.append(el('<label class="field">Page type</label>'));
    const type = document.createElement("select");
    type.add(new Option("Guided: the camera moves stop to stop", "guided"));
    type.add(new Option("Explore: the child drags around freely", "explore"));
    type.value = p.mode || "guided";
    type.onchange = () => change(() => { p.mode = type.value; select(null); drawKind = "stop"; });
    root.append(type);
    {
      const row = el('<label class="check"><input type="checkbox"> Show the whole page first</label>');
      const box = row.querySelector("input");
      box.checked = p.openWhole !== false;
      box.onchange = () => change(() => { p.openWhole = box.checked; });
      root.append(row);
      if (p.openWhole === false) root.append(el('<p class="help">This page opens on its first stop, so nothing on it is given away early. Use Spotlight stops to keep the rest dark.</p>'));
    }
    // A very big picture can be cut into tiles so a phone only loads the part being looked at.
    const big = Math.max(p._img.naturalWidth, p._img.naturalHeight);
    if (p.tiles && big < p.tiles.width) {
      root.append(el(`<p class="help">This is a big scene, already cut into tiles (${p.tiles.width} x ${p.tiles.height}).</p>`));
    } else if (big > 4096) {
      const row = el('<label class="check"><input type="checkbox"> Cut this big picture into tiles when saving</label>'), tick = row.querySelector("input");
      tick.checked = !!p._tile || !!p.tiles;
      tick.onchange = () => { p._tile = tick.checked; if (!tick.checked) delete p.tiles; setDirty(); renderSide(); };
      root.append(row);
      root.append(el(`<p class="help">It is ${p._img.naturalWidth} x ${p._img.naturalHeight}. With tiles, only the part being looked at loads sharp, so it stays quick on a phone. Tiles are written when you save to a folder.</p>`));
    }
    if (p.mode === "explore") {
      root.append(el('<p class="help">Any stops play first as a guided intro. Then the page is handed to the child to drag, pinch and discover.</p>'));
      for (const [key, text] of [["hints", "Hint at things not found when the child is idle"], ["showMe", "Show a \"Show me\" button"], ["map", "Show a small map in the corner (for big scenes)"]]) {
        const row = el(`<label class="check"><input type="checkbox"> ${text}</label>`);
        const box = row.querySelector("input");
        box.checked = key === "map" ? !!p.guidance.map : p.guidance[key] !== false;
        box.onchange = () => change(() => { p.guidance[key] = box.checked; });
        root.append(row);
      }
      root.append(el('<label class="field">Dragging on the page draws</label>'));
      const kind = document.createElement("select");
      kind.add(new Option("Intro stops (guided camera)", "stop"));
      kind.add(new Option("Things to find (tap to react)", "hot"));
      kind.value = drawKind;
      kind.onchange = () => { drawKind = kind.value; kind.blur(); renderAll(); };
      root.append(kind);
    }

    const fx = $("fxPanel");
    fx.innerHTML = "";
    renderEffects(fx, p, el);
    const kinds = (p.effects || []).map(e => FX_NAMES[e.type] || e.type);
    $("fxTag").textContent = kinds.length ? kinds.join(", ") : "None";
    const lay = $("layerPanel");
    lay.innerHTML = "";
    renderLayers(lay, p, el);
    $("layerTag").textContent = (p.layers || []).length ? String(p.layers.length) : "None";
    if (selLayer >= 0) $("layerGroup").open = true;
    renderTimeline();
  }


  /**
   * The camera's journey for this page, left to right: how the page opens, each stop as a card
   * (drag one to change the order), what joins them, and on explore pages the things to find.
   */
  function renderTimeline() {
    const root = $("timeline");
    root.innerHTML = "";
    const p = page();
    if (!p) return;
    const el = html => { const t = document.createElement("template"); t.innerHTML = html.trim(); return t.content.firstChild; };
    const explore = p.mode === "explore";

    const head = el(`<div class="tl-head"><b>Page ${pageIndex + 1}</b><span class="sub"></span><span class="spacer"></span></div>`);
    const n = p.stops.length;
    head.querySelector(".sub").textContent = (n ? `${n} stop${n === 1 ? "" : "s"}` : "No stops yet: drag on the page to draw one")
      + (explore ? ` · ${p.hotspots.length} to find` : "");
    for (const e of p.effects || []) head.append(el(`<span class="tl-fx">${(FX_NAMES[e.type] || e.type) + (e.stop ? " on stop " + e.stop : e.from > 1 ? " from stop " + e.from : "")}</span>`));
    if ((p.layers || []).length) head.append(el(`<span class="tl-fx" style="background:#2c2444;color:#cdb8ff">${p.layers.length} picture${p.layers.length === 1 ? "" : "s"} on top</span>`));
    if (clipItem) {
      const paste = el(`<button class="btn small" title="Ctrl+V">Paste ${clipItem.kind === "hot" ? "thing to find" : "stop"}</button>`);
      paste.onclick = pasteClip;
      head.append(paste);
    }
    const whole = el('<button class="btn small" title="A stop that shows the whole page, for example to end on the full picture">+ Whole page stop</button>');
    whole.onclick = () => change(() => {
      const at = selected >= 0 ? selected + 1 : p.stops.length;
      p.stops.splice(at, 0, { box: { x: 0, y: 0, w: 1, h: 1 } });
      select("stop", at);
    });
    const play = el(`<button class="btn small">▶ Play ${explore ? "exploring" : "this page"}</button>`);
    play.onclick = () => openPreview(explore ? -2 : undefined);
    head.append(whole, play);
    root.append(head);

    const track = el('<div class="tl-track"></div>');
    const link = (auto, text) => el(`<div class="tl-link${auto ? " auto" : ""}">${text}</div>`);
    if (p.openWhole !== false && (n || !explore)) {
      track.append(el('<div class="tl-ghost">Opens on the whole page</div>'));
      if (n) track.append(link(false, "tap"));
    }
    p.stops.forEach((s, i) => {
      const card = el(`<div class="tl-card${i === selected ? " active" : ""}" draggable="true" style="--c:${stopColor(i)}">
        <canvas width="268" height="124"></canvas><span class="dot" style="background:${stopColor(i)}">${i + 1}</span>
        <span class="tl-badges"></span><span class="cap"></span></div>`);
      const cap = card.querySelector(".cap");
      cap.textContent = s.caption || "No words";
      if (!s.caption) cap.classList.add("none");
      const badges = card.querySelector(".tl-badges");
      const badge = (text, title) => { const b = el("<span></span>"); b.textContent = text; b.title = title; badges.append(b); };
      if (s.moveSeconds !== undefined) badge(+s.moveSeconds === 0 ? "cut" : s.moveSeconds + "s", "Time the camera takes to get here");
      if (s.to) badge("glide " + (s.panSeconds || 4) + "s", "Glides to a second box");
      if (s.style === "spotlight") badge("spot", "Spotlight");
      if (s.sound) badge("♪", "Plays " + s.sound);
      drawThumb(card.querySelector("canvas"), p._img, s.box);
      card.title = stopSummary(s);
      card.onclick = () => { select("stop", i); renderAll(); };
      card.ondragstart = ev => { ev.dataTransfer.setData("text/plain", String(i)); ev.dataTransfer.effectAllowed = "move"; };
      card.ondragover = ev => { ev.preventDefault(); card.classList.add("dragover"); };
      card.ondragleave = () => card.classList.remove("dragover");
      card.ondrop = ev => {
        ev.preventDefault();
        const from = parseInt(ev.dataTransfer.getData("text/plain"), 10);
        if (!(from >= 0) || from === i || from >= p.stops.length) { card.classList.remove("dragover"); return; }
        change(() => { const [moved] = p.stops.splice(from, 1); p.stops.splice(i, 0, moved); select("stop", i); });
      };
      track.append(card);
      if (i < n - 1) track.append(link(s.auto, s.auto ? `after ${s.holdSeconds || 1.5}s` : "tap"));
    });
    if (explore) {
      if (n) track.append(link(false, "tap"));
      track.append(el('<div class="tl-ghost" style="margin-right:10px">The child explores</div>'));
      p.hotspots.forEach((h, i) => {
        const chip = el(`<div class="tl-chip${i === selHot ? " active" : ""}"><span class="dot">${h.story ? h.story : "★"}</span><span></span></div>`);
        chip.lastChild.textContent = h.name || h.caption || "Thing " + (i + 1);
        chip.title = (h.story ? "Story step " + h.story + ". " : "Extra to find. ") + (h.caption || "");
        chip.onclick = () => { select("hot", i); renderAll(); };
        track.append(chip);
      });
      if (!p.hotspots.length) track.append(el('<span class="help" style="align-self:center">Nothing to find yet. In the Page settings, set dragging to draw things to find.</span>'));
    }
    root.append(track);
    const active = track.querySelector(".active");
    if (active && active.scrollIntoView) active.scrollIntoView({ block: "nearest", inline: "nearest" });
  }

  /** A small picture of what a stop frames, for its card: the box, widened to fill the card. */
  function drawThumb(canvas, img, box) {
    const ctx = canvas.getContext("2d"), W = canvas.width, H = canvas.height;
    ctx.fillStyle = "#0e0f13";
    ctx.fillRect(0, 0, W, H);
    if (!img || !img.naturalWidth) return;
    const iw = img.naturalWidth, ih = img.naturalHeight;
    let sw = box.w * iw, sh = box.h * ih;
    if (sw / sh < W / H) sw = sh * W / H; else sh = sw * H / W;
    const k = Math.min(1, iw / sw, ih / sh);            // never ask for more than the picture has
    sw *= k; sh *= k;
    const sx = clamp((box.x + box.w / 2) * iw - sw / 2, 0, iw - sw), sy = clamp((box.y + box.h / 2) * ih - sh / 2, 0, ih - sh);
    const fit = Math.min(W / sw, H / sh), dw = sw * fit, dh = sh * fit;
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(img, sx, sy, sw, sh, (W - dw) / 2, (H - dh) / 2, dw, dh);
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
    const el = (html) => { const t = document.createElement("template"); t.innerHTML = html.trim(); return t.content.firstChild; };
    $("right").classList.toggle("has-sel", !!(s || (selHot >= 0 && p.hotspots[selHot])));
    if (uiMode === "guided" && step === "words") { renderWords(root, p, el); return; }
    if (uiMode === "guided" && step === "play") { renderPlay(root, el); return; }
    if (uiMode === "guided" && step !== "camera") return;
    if (selHot >= 0 && p.hotspots[selHot]) { renderHotspotInspector(root, p, p.hotspots[selHot]); return; }
    if (!s) {
      root.append(el(`<div class="card" data-g="camera"><h2>Nothing selected</h2><p class="help">
        Drag on the page to draw a stop. Click a stop, on the page or in the strip along the bottom, to change it.
        Drag it to move it, drag a white corner to resize it. Hold Shift to draw a new stop on top of another.</p>
        <p class="help">Delete removes it. Arrow keys nudge it. P plays. Ctrl+Z undoes. Page Up and Page Down change page.</p></div>`));
      return;
    }
    const color = stopColor(selected);
    const card = el('<div class="card"></div>');
    root.append(card);
    card.append(el(`<h2><span class="dot" style="background:${color}">${selected + 1}</span>Stop ${selected + 1}</h2>`));

    const actions = el('<div class="inline"></div>');
    const act = (text, fn, title) => { const b = el(`<button class="btn small">${text}</button>`); if (title) b.title = title; b.onclick = fn; actions.append(b); };
    act("Earlier", () => selected > 0 && change(() => { swap(p.stops, selected, selected - 1); selected--; }));
    act("Later", () => selected < p.stops.length - 1 && change(() => { swap(p.stops, selected, selected + 1); selected++; }));
    act("Duplicate", duplicateSelected, "Make a copy of this stop (Ctrl+D)");
    act("Copy", copySelected, "Copy it, to paste on another page (Ctrl+C, then Ctrl+V)");
    act("Delete", () => change(() => { p.stops.splice(selected, 1); select(null); }));
    act("▶ Play", () => openPreview(selected), "Play the move into this stop");
    card.append(actions);
    const wordsSec = el('<div data-g="words"><h3>Words</h3></div>'), camSec = el('<div data-g="camera"></div>');
    card.append(wordsSec, camSec);
    let tgt = camSec;

    // Focus style
    tgt.append(el('<h3>Look</h3>'));
    tgt.append(el('<label class="field">What happens around it</label>'));
    const style = document.createElement("select");
    style.add(new Option(`Book default (${STYLE_NAMES[book.focusStyle] || book.focusStyle})`, ""));
    for (const [v, t] of Object.entries(STYLE_NAMES)) style.add(new Option(t, v));
    style.value = s.style || "";
    style.onchange = () => change(() => { if (style.value) s.style = style.value; else delete s.style; });
    tgt.append(style);

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
    tgt.append(panRow);
    if (s.to) {
      tgt.append(el('<p class="help">The camera arrives at the solid box, then glides to the dashed box. Drag the dashed box to where it should end.</p>'));
      const lab = el(`<label class="field">Glide time <output>${s.panSeconds || 4} s</output></label>`);
      const range = el('<input type="range" min="1" max="15" step="0.5" style="width:100%">');
      range.value = s.panSeconds || 4;
      range.onpointerdown = () => pushUndo();
      range.oninput = () => { s.panSeconds = +range.value; lab.querySelector("output").textContent = range.value + " s"; setDirty(); renderStopList(); };
      tgt.append(lab, range);
    }

    // Timing
    tgt.append(el('<h3>Timing</h3>'));
    tgt.append(el('<label class="field">Time the camera takes to get here</label>'));
    const timeRow = el('<div class="inline"><input type="number" min="0" max="20" step="0.1" placeholder="automatic" style="width:110px"> <span class="help" style="margin:0">seconds. Empty: the reader decides. 0: cut straight to it.</span></div>');
    const time = timeRow.querySelector("input");
    time.value = s.moveSeconds === undefined ? "" : s.moveSeconds;
    time.onfocus = () => pushUndo();
    time.oninput = () => { if (time.value === "") delete s.moveSeconds; else s.moveSeconds = clamp(+time.value, 0, 20); setDirty(); renderStopList(); };
    tgt.append(timeRow);
    const autoRow = el('<label class="check"><input type="checkbox"> Go on to the next stop by itself (no tap)</label>');
    const autoBox = autoRow.querySelector("input");
    autoBox.checked = !!s.auto;
    autoBox.onchange = () => change(() => { if (autoBox.checked) { s.auto = true; s.holdSeconds = s.holdSeconds || 1.5; } else { delete s.auto; delete s.holdSeconds; } });
    tgt.append(autoRow);
    if (s.auto) {
      const lab = el(`<label class="field">It rests here for <output>${s.holdSeconds || 1.5} s</output> first</label>`);
      const range = el('<input type="range" min="0.5" max="10" step="0.5" style="width:100%">');
      range.value = s.holdSeconds || 1.5;
      range.onpointerdown = () => pushUndo();
      range.oninput = () => { s.holdSeconds = +range.value; lab.querySelector("output").textContent = range.value + " s"; setDirty(); renderStopList(); };
      tgt.append(lab, range);
      tgt.append(el('<p class="help">Use this to chain several camera moves into one tap. Keep the words short: a child cannot stop it to read.</p>'));
    }

    // Caption
    tgt = wordsSec;
    tgt.append(el('<label class="field">What is read here (leave empty if the words are in the picture)</label>'));
    const cap = document.createElement("textarea");
    cap.rows = 3;
    cap.value = s.caption || "";
    cap.onfocus = () => pushUndo();
    cap.oninput = () => { if (cap.value) s.caption = cap.value; else delete s.caption; setDirty(); renderStopList(); };
    tgt.append(cap);

    tgt.append(el('<label class="field">Question for the grown-up (optional)</label>'));
    const ask = document.createElement("input");
    ask.type = "text";
    ask.placeholder = "e.g. How do you think she feels?";
    ask.value = s.ask || "";
    ask.onfocus = () => pushUndo();
    ask.oninput = () => { if (ask.value) s.ask = ask.value; else delete s.ask; setDirty(); };
    tgt.append(ask);

    tgt.append(soundRow(el, s, "Sound when the camera arrives (narration or an effect)"));
    tgt = camSec;
    const sizeRow = el('<div class="inline"></div>');
    const full = el('<button class="btn small">Make it the whole page</button>');
    full.onclick = () => change(() => { s.box = { x: 0, y: 0, w: 1, h: 1 }; });
    sizeRow.append(full);
    tgt.append(sizeRow);
  }

  /** Things the reader draws moving over the page. Add as many as the page needs. */
  function renderEffects(root, p, el) {
    p.effects = p.effects || [];
    const stopSelect = (first, value, fn) => {
      const sel = document.createElement("select");
      if (first) sel.add(new Option(first, "0"));
      p.stops.forEach((s, i) => sel.add(new Option(`Stop ${i + 1}`, String(i + 1))));
      sel.value = String(value || (first ? 0 : 1));
      if (sel.selectedIndex < 0) sel.selectedIndex = 0;
      sel.onchange = () => change(() => fn(+sel.value));
      return sel;
    };
    const slider = (box, text, min, max, step, value, fn) => {
      box.append(el(`<label class="field">${text}</label>`));
      const input = el(`<input type="range" min="${min}" max="${max}" step="${step}">`);
      input.value = value;
      input.onpointerdown = () => pushUndo();
      input.oninput = () => { fn(+input.value); setDirty(); draw(); };
      box.append(input);
    };

    p.effects.forEach((e, idx) => {
      const box = el(`<div class="fx-item"><div class="fx-head"><b>${FX_NAMES[e.type] || e.type}</b><button class="btn small">Remove</button></div></div>`);
      box.querySelector("button").onclick = () => change(() => {
        p.effects.splice(idx, 1);
        if (dryTarget === e) { dryTarget = null; drawKind = "stop"; }
      });
      if (e.type === "lightning" || e.type === "shake") {
        if (!p.stops.length) box.append(el('<p class="help">Draw a stop first: this happens when the camera arrives at one.</p>'));
        else {
          box.append(el('<label class="field">It happens on arriving at</label>'));
          box.append(stopSelect(null, e.stop || 1, v => { e.stop = v; }));
        }
        if (e.type === "shake") slider(box, "How hard", 0.2, 1.5, 0.05, e.strength === undefined ? 0.6 : e.strength, v => { e.strength = v; });
      } else {
        slider(box, "How much", 0.2, 1, 0.05, e.amount === undefined ? 0.6 : e.amount, v => { e.amount = v; });
        if (p.stops.length > 1) {
          box.append(el('<label class="field">It starts</label>'));
          box.append(stopSelect("As the page opens", e.from > 1 ? e.from : 0, v => { if (v > 1) e.from = v; else delete e.from; }));
          box.append(el('<label class="field">It stops</label>'));
          box.append(stopSelect("Never, it stays for the whole page", e.to >= 1 ? e.to : 0, v => { if (v >= 1) e.to = v; else delete e.to; }));
        }
        if (e.type === "rain") {
          const row = el('<label class="check"><input type="checkbox"> Splashes where it lands</label>'), tick = row.querySelector("input");
          tick.checked = !!e.ground;
          tick.onchange = () => change(() => { e.ground = tick.checked ? 0.6 : 0; });
          box.append(row);
          if (e.ground) slider(box, "The ground starts this far down the page", 0.2, 0.95, 0.01, e.ground, v => { e.ground = v; });
        }
        const n = (e.dry || []).length, drawing = drawKind === "dry" && dryTarget === e;
        const row = el(`<div class="inline"><button class="btn small">${drawing ? "Now drag on the page..." : "Draw an area it misses"}</button>${n ? `<button class="btn small">Clear areas (${n})</button>` : ""}</div>`);
        row.firstChild.onclick = () => {
          if (drawing) { drawKind = "stop"; dryTarget = null; } else { drawKind = "dry"; dryTarget = e; }
          select(null);
          renderAll();
        };
        if (n) row.lastChild.onclick = () => change(() => { e.dry = []; });
        box.append(row);
      }
      root.append(box);
    });

    const add = document.createElement("select");
    add.add(new Option("Add an effect...", ""));
    for (const [type, name] of Object.entries(FX_NAMES)) add.add(new Option(name, type));
    add.onchange = () => change(() => {
      const type = add.value;
      if (FALLING.includes(type)) p.effects.push({ type, amount: 0.6 });
      else if (type === "shake") p.effects.push({ type, stop: Math.max(1, selected + 1), strength: 0.6 });
      else if (type) p.effects.push({ type, stop: Math.max(1, selected + 1) });
    });
    root.append(add);
    if (p.effects.some(e => FALLING.includes(e.type))) {
      root.append(el('<p class="help">An area it misses is somewhere it should not fall, like under a bench or a roof. Paint the picture without rain or snow in it, or you will see both.</p>'));
    }
  }

  /** Pictures placed on top of the page: a character or a prop with a see-through background. */
  function renderLayers(root, p, el) {
    p.layers = p.layers || [];
    const stopSelect = (first, value, fn) => {
      const sel = document.createElement("select");
      sel.add(new Option(first, "0"));
      p.stops.forEach((s, i) => sel.add(new Option(`Stop ${i + 1}`, String(i + 1))));
      sel.value = String(value || 0);
      if (sel.selectedIndex < 0) sel.selectedIndex = 0;
      sel.onchange = () => change(() => fn(+sel.value));
      return sel;
    };
    p.layers.forEach((l, i) => {
      const row = el(`<div class="stop-row${i === selLayer ? " active" : ""}"><span class="dot" style="background:#7a4fd6">${i + 1}</span><span class="txt"></span></div>`);
      row.querySelector(".txt").textContent = l.image + (l.moveTo ? " · moves" : "") + (l.idle ? " · " + l.idle + "s" : "");
      row.onclick = () => { select(i === selLayer ? null : "layer", i); renderAll(); };
      root.append(row);
    });
    const add = el('<div class="inline"><button class="btn small">+ Add a picture</button></div>');
    add.firstChild.onclick = () => pickFile("image/png,image/webp,image/gif,image/*", async file => {
      const name = addAsset(file);
      const img = await CR.loadImage(assets.get(name).url);
      layerImgs.set(name, img);
      const pg = p._img, w = clamp(img.naturalWidth / pg.naturalWidth, 0.06, 0.35), h = w * (img.naturalHeight / img.naturalWidth) * (pg.naturalWidth / pg.naturalHeight);
      change(() => { p.layers.push({ image: name, box: { x: 0.5 - w / 2, y: 0.5 - h / 2, w, h } }); select("layer", p.layers.length - 1); });
    });
    root.append(add);
    const l = selLayer >= 0 ? p.layers[selLayer] : null;
    if (!l) {
      root.append(el('<p class="help">Use a PNG with a see-through background. Click a picture in this list to place it, size it, and make it move.</p>'));
      return;
    }
    const box = el(`<div class="fx-item"><div class="fx-head"><b>Picture ${selLayer + 1}</b><button class="btn small">Remove</button></div></div>`);
    box.querySelector("button").onclick = () => change(() => { p.layers.splice(selLayer, 1); select(null); });
    box.append(el('<p class="help">Drag it on the page to place it. Drag a corner to size it.</p>'));
    if (p.stops.length) {
      box.append(el('<label class="field">It appears</label>'));
      box.append(stopSelect("As the page opens", l.from > 1 ? l.from : 0, v => { if (v > 1) l.from = v; else delete l.from; }));
      box.append(el('<label class="field">It goes away</label>'));
      box.append(stopSelect("Never, it stays", l.to >= 1 ? l.to : 0, v => { if (v >= 1) l.to = v; else delete l.to; }));
    }
    const moveRow = el('<label class="check"><input type="checkbox"> It moves somewhere</label>'), moves = moveRow.querySelector("input");
    moves.checked = !!l.moveTo;
    moves.onchange = () => change(() => {
      if (moves.checked) {
        l.moveTo = { ...l.box, x: clamp(l.box.x + l.box.w * 1.2, -l.box.w, 1) };
        l.moveAt = l.moveAt || Math.max(1, selected + 1);
        l.moveSeconds = l.moveSeconds || 1.2;
      } else { delete l.moveTo; delete l.moveAt; delete l.moveSeconds; delete l.path; }
    });
    box.append(moveRow);
    if (l.moveTo) {
      box.append(el('<p class="help">Drag the faded copy to where it should end up.</p>'));
      if (p.stops.length) {
        box.append(el('<label class="field">It sets off when the camera reaches</label>'));
        const at = document.createElement("select");
        p.stops.forEach((s, i) => at.add(new Option(`Stop ${i + 1}`, String(i + 1))));
        at.value = String(l.moveAt || 1);
        at.onchange = () => change(() => { l.moveAt = +at.value; });
        box.append(at);
      }
      const lab = el(`<label class="field">The move takes <output>${l.moveSeconds || 1.2} s</output></label>`);
      const range = el('<input type="range" min="0.3" max="8" step="0.1">');
      range.value = l.moveSeconds || 1.2;
      range.onpointerdown = () => pushUndo();
      range.oninput = () => { l.moveSeconds = +range.value; lab.querySelector("output").textContent = range.value + " s"; setDirty(); };
      box.append(lab, range);
      box.append(el('<label class="field">The way it travels</label>'));
      const path = document.createElement("select");
      path.add(new Option("Straight there", "straight"));
      path.add(new Option("In a hop, up and over", "hop"));
      path.value = l.path === "hop" ? "hop" : "straight";
      path.onchange = () => change(() => { if (path.value === "hop") l.path = "hop"; else delete l.path; });
      box.append(path);
    }
    box.append(el('<label class="field">While it waits</label>'));
    const idle = document.createElement("select");
    idle.add(new Option("It keeps still", ""));
    idle.add(new Option("It bobs up and down", "bob"));
    idle.add(new Option("It sways", "sway"));
    idle.value = l.idle || "";
    idle.onchange = () => change(() => { if (idle.value) l.idle = idle.value; else delete l.idle; });
    box.append(idle);
    root.append(box);
  }

  /** A row for choosing a sound file for a stop or a thing to find. */
  function soundRow(el, item, text) {
    const wrap = el(`<div><label class="field">${text}</label><div class="inline" style="margin-top:0"></div></div>`), row = wrap.lastChild;
    if (item.sound) {
      const name = el('<span class="help" style="margin:0;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"></span>');
      name.textContent = item.sound;
      const play = el('<button class="btn small" title="Listen">▶</button>'), remove = el('<button class="btn small">Remove</button>');
      play.onclick = () => { const src = assetUrl(item.sound); if (src) new Audio(src).play().catch(() => say("That sound couldn't be played here.")); };
      remove.onclick = () => change(() => { delete item.sound; });
      row.append(name, play, remove);
    } else {
      const pick = el('<button class="btn small">Choose a sound file</button>');
      pick.onclick = () => pickFile("audio/*", file => change(() => { item.sound = addAsset(file); }));
      row.append(pick);
    }
    return wrap;
  }

  // ---------------------------------------------------------------- duplicate, copy, paste

  function duplicateSelected() {
    const p = page();
    if (!p) return;
    if (selHot >= 0 && p.hotspots[selHot]) {
      change(() => { const h = clone(p.hotspots[selHot]); delete h.story; nudge(h.box); p.hotspots.splice(selHot + 1, 0, h); select("hot", selHot + 1); });
    } else if (selected >= 0 && p.stops[selected]) {
      change(() => { const st = clone(p.stops[selected]); nudge(st.box); if (st.to) nudge(st.to); p.stops.splice(selected + 1, 0, st); select("stop", selected + 1); });
    }
  }
  function nudge(b) { b.x = clamp(b.x + 0.03, 0, 1 - b.w); b.y = clamp(b.y + 0.03, 0, 1 - b.h); }

  function copySelected() {
    const p = page();
    if (!p) return false;
    if (selHot >= 0 && p.hotspots[selHot]) clipItem = { kind: "hot", data: clone(p.hotspots[selHot]) };
    else if (selected >= 0 && p.stops[selected]) clipItem = { kind: "stop", data: clone(p.stops[selected]) };
    else return false;
    say(`Copied. Go to any page and press Ctrl+V to paste it.`, 2500);
    renderTimeline();
    return true;
  }

  function pasteClip() {
    const p = page();
    if (!p || !clipItem) return;
    if (clipItem.kind === "hot") {
      if (p.mode !== "explore") { say("Things to find can only go on an explore page."); return; }
      change(() => { const h = clone(clipItem.data); delete h.story; p.hotspots.push(h); select("hot", p.hotspots.length - 1); });
    } else {
      change(() => { const at = selected >= 0 ? selected + 1 : p.stops.length; p.stops.splice(at, 0, clone(clipItem.data)); select("stop", at); });
    }
  }

  function renderHotspotInspector(root, p, h) {
    const el = html => { const t = document.createElement("template"); t.innerHTML = html.trim(); return t.content.firstChild; };
    const card = el('<div class="card"></div>');
    root.append(card);
    card.append(el(`<h2><span class="dot" style="background:#a37a00">★</span>Thing to find ${selHot + 1}</h2>`));
    const actions = el('<div class="inline"><button class="btn small">Duplicate</button><button class="btn small">Copy</button><button class="btn small">Delete</button></div>');
    actions.children[0].onclick = duplicateSelected;
    actions.children[1].onclick = copySelected;
    actions.children[2].onclick = () => change(() => { p.hotspots.splice(selHot, 1); select(null); });
    card.append(actions);
    const wordsSec = el('<div data-g="words"><h3>Words</h3></div>'), camSec = el('<div data-g="camera"><h3>How it behaves</h3></div>');
    card.append(wordsSec, camSec);
    let tgt = wordsSec;
    tgt.append(el('<label class="field">Name (e.g. Old Turtle)</label>'));
    const name = document.createElement("input");
    name.type = "text";
    name.value = h.name || "";
    name.onfocus = () => pushUndo();
    name.oninput = () => { if (name.value) h.name = name.value; else delete h.name; setDirty(); renderStopList(); };
    tgt.append(name);
    tgt.append(el('<label class="field">What it says when found</label>'));
    const cap = document.createElement("textarea");
    cap.rows = 4;
    cap.value = h.caption || "";
    cap.onfocus = () => pushUndo();
    cap.oninput = () => { if (cap.value) h.caption = cap.value; else delete h.caption; setDirty(); renderStopList(); };
    tgt.append(cap);
    tgt.append(el('<label class="field">Question for the grown-up (optional)</label>'));
    const ask = document.createElement("input");
    ask.type = "text";
    ask.placeholder = "e.g. What else lives in the sea?";
    ask.value = h.ask || "";
    ask.onfocus = () => pushUndo();
    ask.oninput = () => { if (ask.value) h.ask = ask.value; else delete h.ask; setDirty(); };
    tgt.append(ask);
    tgt.append(soundRow(el, h, "Sound when it is found"));
    tgt = camSec;
    tgt.append(el('<label class="field">Story step (leave empty for an extra thing to find)</label>'));
    const story = document.createElement("input");
    story.type = "number";
    story.min = "1";
    story.value = h.story || "";
    story.onchange = () => change(() => { const n = parseInt(story.value, 10); if (n > 0) h.story = n; else delete h.story; });
    tgt.append(story);
    tgt.append(el('<p class="help">Story steps open in order: 1, then 2, and so on. The next one always shows a beacon, and tapping a later one too early says "First find" and the name of the next one.</p>'));
    tgt.append(el('<label class="field">When found</label>'));
    const fx = document.createElement("select");
    fx.add(new Option("Sparkle", "sparkle"));
    fx.add(new Option("No effect, just the words", "none"));
    fx.value = h.effect === "none" ? "none" : "sparkle";
    fx.onchange = () => change(() => { if (fx.value === "none") h.effect = "none"; else delete h.effect; });
    tgt.append(fx);
    tgt.append(el('<p class="help">Hints and Show me lead to the next story step first, then to the extras in the order listed.</p>'));
  }


  /** Step by step, Words: every stop on the page with its words, so they can be written in one go. */
  function renderWords(root, p, el) {
    const card = el(`<div class="card"><h2>Words on page ${pageIndex + 1}</h2></div>`);
    root.append(card);
    if (!p.stops.length && !p.hotspots.length) card.append(el('<p class="help">This page has no stops yet. Go back to Camera and draw some.</p>'));
    const focusOn = (kind, i) => { select(kind, i); draw(); renderTimeline(); };
    const grow = t => { t.style.height = "auto"; t.style.height = t.scrollHeight + 2 + "px"; };
    p.stops.forEach((s, i) => {
      const row = el(`<div class="word-row"><span class="dot" style="background:${stopColor(i)}">${i + 1}</span><div><textarea rows="2" placeholder="What is read at this stop"></textarea><input type="text" placeholder="Question for the grown-up (optional)"></div></div>`);
      const cap = row.querySelector("textarea"), ask = row.querySelector("input");
      cap.value = s.caption || "";
      ask.value = s.ask || "";
      cap.onfocus = ask.onfocus = () => { pushUndo(); focusOn("stop", i); };
      cap.oninput = () => { if (cap.value) s.caption = cap.value; else delete s.caption; setDirty(); renderTimeline(); grow(cap); };
      ask.oninput = () => { if (ask.value) s.ask = ask.value; else delete s.ask; setDirty(); };
      card.append(row);
      grow(cap);
    });
    if (p.mode === "explore" && p.hotspots.length) {
      card.append(el("<h3>Things to find</h3>"));
      p.hotspots.forEach((h, i) => {
        const row = el(`<div class="word-row"><span class="dot" style="background:#a37a00">${h.story || "★"}</span><div><input type="text" placeholder="Its name, e.g. Old Turtle" style="margin:0 0 5px"><textarea rows="2" placeholder="What it says when found"></textarea></div></div>`);
        const name = row.querySelector("input"), cap = row.querySelector("textarea");
        name.value = h.name || "";
        cap.value = h.caption || "";
        name.onfocus = cap.onfocus = () => { pushUndo(); focusOn("hot", i); };
        name.oninput = () => { if (name.value) h.name = name.value; else delete h.name; setDirty(); renderTimeline(); };
        cap.oninput = () => { if (cap.value) h.caption = cap.value; else delete h.caption; setDirty(); renderTimeline(); grow(cap); };
        card.append(row);
        grow(cap);
      });
    }
  }

  /** Step by step, Play. */
  function renderPlay(root, el) {
    const card = el(`<div class="card"><h2>Watch it</h2><p class="help">It plays with the playground's settings, the way a child will see it. Press Esc to come back.</p>
      <div class="inline"><button class="btn primary">▶ Play this page</button><button class="btn">▶ Play the whole book</button></div>
      <p class="help">Happy with it? Save writes the book file. If something is off, go back to the step it belongs to.</p>
      <div class="inline"><button class="btn">Save</button></div></div>`);
    const [one, all, saveBtn] = card.querySelectorAll("button");
    one.onclick = () => openPreview(page() && page().mode === "explore" && !page().stops.length ? -2 : undefined);
    all.onclick = () => { const keep = pageIndex; pageIndex = 0; openPreview(); pageIndex = keep; };
    saveBtn.onclick = save;
    root.append(card);
  }

  /** The mode switch, the step bar and the note that says what this step is for. */
  function renderChrome() {
    document.body.dataset.mode = uiMode;
    document.body.dataset.step = step;
    for (const b of $("modeSwitch").children) b.classList.toggle("on", b.dataset.mode === uiMode);
    for (const b of $("steps").querySelectorAll("button")) b.classList.toggle("on", b.dataset.step === step);
    const intro = $("stepIntro");
    intro.innerHTML = "";
    if (uiMode !== "guided") return;
    const names = Object.keys(STEPS), at = names.indexOf(step), next = names[at + 1];
    const title = document.createElement("b"), text = document.createElement("p");
    title.textContent = `${at + 1}. ${STEPS[step][0]}`;
    text.textContent = STEPS[step][1];
    intro.append(title, text);
    if (next) {
      const go = document.createElement("button");
      go.className = "btn small primary";
      go.textContent = `Next: ${STEPS[next][0]}`;
      go.onclick = () => setUi("guided", next);
      intro.append(go);
    }
  }

  function setUi(mode, st) {
    uiMode = mode;
    if (st) step = st;
    try { localStorage.setItem(UI_KEY, JSON.stringify({ mode: uiMode, step })); } catch (_) { /* storage blocked */ }
    if (uiMode === "guided") {
      // Open the group this step is about.
      if (step === "pages") $("pageGroup").open = $("bookGroup").open = true;
      if (step === "camera") $("pageGroup").open = true;
      if (step === "effects") $("fxGroup").open = true;
    }
    renderAll();
  }

  function renderSide() {
    renderStopList();
    renderInspector();
  }

  function renderAll() {
    renderChrome();
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

  let ui = null, preview = null;

  /** Opens the reader on the book as it is now. With a stop, plays the move into that stop. */
  function openPreview(stop) {
    if (!book || !book.pages.length) return;
    $("msg").classList.add("hidden");
    if (!ui) {
      ui = ComicReaderUI.mount($("preview"), { onClose: closePreview, endAgain: "Play again", endBack: "Back to editing" });
      preview = ui.reader;
      ui.setTitle("Preview");
    }
    // Pick up whatever the playground's settings are now.
    settings = CR.loadSettings();
    Object.assign(ui.settings, settings);
    $("preview").classList.remove("hidden");
    const copy = CR.normalizeBook(CR.serializeBook(book)), urls = {};
    // A big picture that's still in memory at full size plays from that; its tiles may not exist yet.
    copy.pages.forEach((pg, i) => { const im = book.pages[i]._img; if (pg.tiles && Math.max(im.naturalWidth, im.naturalHeight) >= pg.tiles.width) pg.tiles = null; });
    for (const [name, a] of assets) urls[name] = a.url;
    preview.setBook(copy, book.pages.map(p => p._img), 0, { baseUrl: book._dir || "", assets: urls });
    if (stop === -2) { preview.jumpTo(pageIndex, -2); return; }
    if (stop === undefined || stop < 0) { preview.jumpTo(pageIndex, -1); return; }
    preview.jumpTo(pageIndex, stop);
    if (preview.stepIndex > 0) {
      const before = preview.steps[preview.stepIndex - 1];
      preview.jumpTo(before.page, before.stop);
      preview.lastGo = 0;
      preview.next();
    }
  }

  function closePreview() {
    $("preview").classList.add("hidden");
    if (preview) preview.settings.autoPlay = false;
  }

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
  // Leaving with unsaved changes asks once: the first click warns, the second goes.
  $("exit").onclick = e => { if (!okToDiscard()) e.preventDefault(); };
  for (const b of $("modeSwitch").children) b.onclick = () => setUi(b.dataset.mode);
  for (const b of $("steps").querySelectorAll("button")) b.onclick = () => setUi("guided", b.dataset.step);
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
    const hot = p && selHot >= 0 ? p.hotspots[selHot] : null;
    const layer = p && selLayer >= 0 && p.layers ? p.layers[selLayer] : null;
    const s = hot || layer || (p && selected >= 0 ? p.stops[selected] : null);
    if (mod && (e.key === "d" || e.key === "D")) { e.preventDefault(); duplicateSelected(); return; }
    if (mod && (e.key === "c" || e.key === "C")) { if (copySelected()) e.preventDefault(); return; }
    if (mod && (e.key === "v" || e.key === "V")) { if (clipItem) { e.preventDefault(); pasteClip(); } return; }

    if (e.key === "Escape") { $("jsonBox").classList.add("hidden"); select(null); renderAll(); }
    else if ((e.key === "Delete" || e.key === "Backspace") && s) {
      e.preventDefault();
      change(() => { if (hot) p.hotspots.splice(selHot, 1); else if (layer) p.layers.splice(selLayer, 1); else p.stops.splice(selected, 1); select(null); });
    }
    else if (e.key === "p" || e.key === "P") openPreview(hot ? -2 : selected >= 0 ? selected : undefined);
    else if (e.key === "PageDown" && pageIndex < book.pages.length - 1) { pageIndex++; select(null); drawKind = "stop"; renderAll(); }
    else if (e.key === "PageUp" && pageIndex > 0) { pageIndex--; select(null); drawKind = "stop"; renderAll(); }
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
  window.__editor = { get book() { return book; }, get selected() { return selected; }, get selHot() { return selHot; }, get pageIndex() { return pageIndex; }, get view() { return view; }, get preview() { return preview; } };
})();
