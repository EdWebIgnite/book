/*
 * Comic Reader engine, shared by the playground (index.html) and the editor preview (editor.html).
 *
 * Book format (book.json):
 * {
 *   "id": "zolas-kite", "title": "...", "author": "...", "cover": "cover.webp",
 *   "focusStyle": "dim" | "vignette" | "none",       // book default, see below
 *   "pages": [
 *     { "image": "page1.webp",
 *       "stops": [
 *         { "box": { "x": 0.1, "y": 0.2, "w": 0.4, "h": 0.3 },   // fractions of the page image
 *           "to":  { ... },          // optional: glide from box to this box while resting
 *           "panSeconds": 4,         // optional: how long that glide takes
 *           "style": "vignette",     // optional: overrides the book's focusStyle for this stop
 *           "caption": "Text..." }   // optional: shown under the picture
 *       ] }
 *   ]
 * }
 * Older files that list "panels" (plain boxes) instead of "stops" still load.
 */
"use strict";
(function () {

  const DEFAULT_SETTINGS = {
    moveMs: 650,
    easing: "easeInOutCubic",
    zoomPath: "smooth",
    pageTurn: "fly",
    arcAmount: 0.6,
    padding: 0.025,
    dim: 0.72,
    vignette: 0.75,
    wholePageFirst: true,
    wholePageLast: false,
    drift: "off",
    driftAmount: 0.06,
    driftSeconds: 6,
    autoPlay: false,
    autoSeconds: 4,
  };

  const STORE_KEY = "comic-reader-movement";

  function loadSettings() {
    const s = { ...DEFAULT_SETTINGS };
    try {
      const saved = JSON.parse(localStorage.getItem(STORE_KEY) || "{}");
      if (saved.panelPadding !== undefined && saved.padding === undefined) saved.padding = saved.panelPadding;
      for (const k of Object.keys(DEFAULT_SETTINGS)) if (saved[k] !== undefined) s[k] = saved[k];
    } catch (_) { /* storage blocked: use defaults */ }
    return s;
  }

  function saveSettings(s) {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(s)); } catch (_) { /* ignore */ }
  }

  // ---------------------------------------------------------------- maths

  const EASINGS = {
    linear: t => t,
    easeInOutCubic: t => t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2,
    easeOutCubic: t => 1 - Math.pow(1 - t, 3),
    easeInOutSine: t => -(Math.cos(Math.PI * t) - 1) / 2,
    easeOutBack: t => { const c1 = 1.4, c3 = c1 + 1; return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2); },
    spring: t => t >= 1 ? 1 : 1 - Math.exp(-6 * t) * Math.cos(11 * t),
  };

  const lerp = (a, b, t) => a + (b - a) * t;
  const rect = (x, y, w, h) => ({ x, y, w, h });
  const inflate = (r, d) => rect(r.x - d, r.y - d, r.w + 2 * d, r.h + 2 * d);
  const union = (a, b) => {
    const x = Math.min(a.x, b.x), y = Math.min(a.y, b.y);
    return rect(x, y, Math.max(a.x + a.w, b.x + b.w) - x, Math.max(a.y + a.h, b.y + b.h) - y);
  };

  /** Blend two rectangles. "Smooth" blends sizes on a log scale so big zooms feel even. */
  function mixRect(a, b, t, smooth) {
    if (!smooth) return rect(lerp(a.x, b.x, t), lerp(a.y, b.y, t), lerp(a.w, b.w, t), lerp(a.h, b.h, t));
    const cx = lerp(a.x + a.w / 2, b.x + b.w / 2, t);
    const cy = lerp(a.y + a.h / 2, b.y + b.h / 2, t);
    const w = Math.exp(lerp(Math.log(a.w), Math.log(b.w), t));
    const h = Math.exp(lerp(Math.log(a.h), Math.log(b.h), t));
    return rect(cx - w / 2, cy - h / 2, w, h);
  }

  function scaleAround(r, f, dx = 0) {
    const cx = r.x + r.w / 2 + dx, cy = r.y + r.h / 2;
    return rect(cx - r.w * f / 2, cy - r.h * f / 2, r.w * f, r.h * f);
  }

  /** What part of the world a screen of the given aspect ratio shows when fitted around `cam`. */
  function fitToAspect(cam, aspect) {
    let w = cam.w, h = cam.h;
    if (w / h < aspect) w = h * aspect; else h = w / aspect;
    return rect(cam.x + cam.w / 2 - w / 2, cam.y + cam.h / 2 - h / 2, w, h);
  }

  // ---------------------------------------------------------------- books

  function normalizeBook(raw) {
    const book = JSON.parse(JSON.stringify(raw));
    book.focusStyle = book.focusStyle || "dim";
    book.pages = (book.pages || []).map(p => ({
      image: p.image,
      stops: (p.stops || (p.panels || []).map(b => ({ box: b }))).map(s => ({ ...s })),
    }));
    return book;
  }

  const round4 = v => Math.round(v * 10000) / 10000;
  const roundBox = b => ({ x: round4(b.x), y: round4(b.y), w: round4(b.w), h: round4(b.h) });

  /** Clean JSON for saving: only known fields, rounded numbers. */
  function serializeBook(book) {
    return {
      id: book.id,
      title: book.title,
      author: book.author || "",
      cover: book.cover || (book.pages[0] && book.pages[0].image) || "",
      focusStyle: book.focusStyle || "dim",
      pages: book.pages.map(p => ({
        image: p.image,
        stops: p.stops.map(s => {
          const o = { box: roundBox(s.box) };
          if (s.to) { o.to = roundBox(s.to); o.panSeconds = s.panSeconds || 4; }
          if (s.style) o.style = s.style;
          if (s.caption) o.caption = s.caption;
          return o;
        }),
      })),
    };
  }

  function loadImage(src) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error("Image failed: " + src));
      img.src = src;
    });
  }

  /** Pages side by side in "world" space: each page is 1.0 tall and as wide as its aspect ratio. */
  function layoutPages(images) {
    const GAP = 0.12;
    let x = 0;
    return images.map(img => {
      const r = rect(x, 0, img.naturalWidth / img.naturalHeight, 1);
      x += r.w + GAP;
      return r;
    });
  }

  // ---------------------------------------------------------------- reader

  class Reader {
    constructor(canvas, opts = {}) {
      this.canvas = canvas;
      this.ctx = canvas.getContext("2d");
      this.settings = opts.settings || loadSettings();
      this.onChange = opts.onChange || (() => {});
      this.insets = opts.insets || { top: 64, bottom: 88 };
      this.book = null;
      this.steps = [];
      this.stepIndex = 0;
      this.overview = false;
      this.finished = false;
      this.anim = null;
      this.restSince = 0;
      this.running = true;
      this.listeners = new AbortController();
      attachGestures(this);
      this._frame = this._frame.bind(this);
      requestAnimationFrame(this._frame);
    }

    destroy() {
      this.running = false;
      this.listeners.abort();
    }

    setBook(book, images, startStep = 0) {
      this.book = book;
      this.images = images;
      this.pageRects = layoutPages(images);
      this.steps = this.buildSteps();
      this.stepIndex = Math.max(0, Math.min(startStep, this.steps.length - 1));
      this.overview = false;
      this.finished = false;
      this.anim = null;
      this.shown = this.target();
      this.restSince = performance.now();
      this._afterArrive();
      this.onChange(this.state);
    }

    /** Index of the first step showing the given page and stop (-1 = whole page). */
    findStep(page, stop) {
      let i = this.steps.findIndex(s => s.page === page && s.stop === stop);
      if (i < 0) i = this.steps.findIndex(s => s.page === page);
      return Math.max(0, i);
    }

    /** Show a page and stop straight away (stop -1 = whole page), then play its pan if it has one. */
    jumpTo(page, stop) {
      this.stepIndex = this.findStep(page, stop);
      this.overview = false;
      this.finished = false;
      this.anim = null;
      this.shown = this.target();
      this.restSince = performance.now();
      this._afterArrive();
      this.onChange(this.state);
    }

    buildSteps() {
      const s = this.settings, out = [];
      this.book.pages.forEach((page, p) => {
        const n = page.stops.length;
        if (n === 0) { out.push({ page: p, stop: -1 }); return; }
        if (s.wholePageFirst) out.push({ page: p, stop: -1 });
        for (let i = 0; i < n; i++) out.push({ page: p, stop: i });
        if (s.wholePageLast) out.push({ page: p, stop: -1 });
      });
      return out;
    }

    /** Call after settings change. Keeps the reader on the same page and stop. */
    refresh() {
      if (!this.book) return;
      const cur = this.steps[this.stepIndex];
      this.steps = this.buildSteps();
      this.stepIndex = this.findStep(cur.page, cur.stop);
      this.moveTo(cur.page);
    }

    boxRect(page, b) {
      const pr = this.pageRects[page];
      return rect(pr.x + b.x * pr.w, pr.y + b.y * pr.h, b.w * pr.w, b.h * pr.h);
    }

    get step() { return this.steps[this.stepIndex]; }
    get stop() { const st = this.step; return st && st.stop >= 0 ? this.book.pages[st.page].stops[st.stop] : null; }

    /** Camera, spotlight box and effect strengths for the current step (start box, or end box of a pan). */
    target(end = false) {
      const s = this.settings, step = this.step;
      const pr = this.pageRects[step.page];
      const styled = (cam, focus, style, k) => ({
        cam, focus,
        dim: style === "dim" ? s.dim * k : 0,
        vig: style === "vignette" ? s.vignette * k : 0,
      });
      const stop = this.stop;
      if (this.overview || !stop) return styled(inflate(pr, 0.03), pr, this.book.focusStyle, 0.6);
      const box = this.boxRect(step.page, end && stop.to ? stop.to : stop.box);
      return styled(inflate(box, s.padding), box, stop.style || this.book.focusStyle, 1);
    }

    get state() {
      const step = this.step, stop = this.stop;
      return {
        stepIndex: this.stepIndex,
        stepCount: this.steps.length,
        page: step ? step.page : 0,
        pageCount: this.book ? this.book.pages.length : 0,
        overview: this.overview,
        wholePage: this.overview || !stop,
        finished: this.finished,
        caption: !this.overview && stop && stop.caption ? stop.caption : "",
        canGoBack: this.stepIndex > 0 || this.overview,
      };
    }

    /** Move from wherever the camera is now (drift included) to the current step. */
    moveTo(previousPage, instant = false) {
      const s = this.settings, now = performance.now();
      const pageChanged = previousPage !== undefined && previousPage !== this.step.page;
      let ms = s.moveMs;
      if (instant || (pageChanged && s.pageTurn === "cut")) ms = 0;
      this.anim = {
        kind: "move",
        from: { ...this.shown, cam: this.displayedCam(now) },
        to: this.target(),
        start: now, ms,
        easing: s.easing,
        smooth: s.zoomPath === "smooth",
        arc: pageChanged && s.pageTurn === "zoomOut",
      };
      if (ms === 0) this._finish();
      this.onChange(this.state);
    }

    _finish() {
      const done = this.anim;
      this.shown = { ...done.to };
      this.anim = null;
      this.restSince = performance.now();
      if (done.kind === "move") this._afterArrive();
    }

    /** A stop with an end box glides there once the camera has arrived. */
    _afterArrive() {
      const stop = this.stop;
      if (this.overview || !stop || !stop.to) return;
      this.anim = {
        kind: "pan",
        from: { ...this.shown },
        to: this.target(true),
        start: performance.now(),
        ms: (stop.panSeconds || 4) * 1000,
        easing: "easeInOutSine",
        smooth: true,
        arc: false,
      };
    }

    go(delta) {
      if (this.finished || !this.book) return;
      const before = this.step.page;
      const wasOverview = this.overview;
      this.overview = false;
      const next = this.stepIndex + delta;
      if (next >= this.steps.length) { this.finished = true; this.onChange(this.state); return; }
      if (next < 0) { if (wasOverview) this.moveTo(before); return; }
      this.stepIndex = next;
      this.moveTo(before);
    }

    next() { this.go(1); }
    prev() { this.go(-1); }

    toggleOverview() {
      if (!this.overview && !this.stop) { this.next(); return; }
      this.overview = !this.overview;
      this.moveTo(this.step.page);
    }

    setOverview(on) { if (this.overview !== on) this.toggleOverview(); }

    restart() {
      this.finished = false;
      this.overview = false;
      const before = this.step.page;
      this.stepIndex = 0;
      this.moveTo(before);
    }

    /** Replays the move into the current step from the one before it. */
    replay() {
      if (this.stepIndex === 0) return;
      this.stepIndex--;
      this.overview = false;
      this.anim = null;
      this.shown = this.target();
      this.go(1);
    }

    /** Keyboard shortcuts. Returns true when the key was used. */
    handleKey(e) {
      if (!this.book) return false;
      const k = e.key;
      if (k === "ArrowRight" || k === " " || k === "PageDown") this.next();
      else if (k === "ArrowLeft" || k === "PageUp") this.prev();
      else if (k === "w" || k === "W" || k === "ArrowUp") this.toggleOverview();
      else if (k === "ArrowDown") this.setOverview(false);
      else return false;
      e.preventDefault();
      return true;
    }

    displayedCam(now) {
      if (this.anim || !this.shown) return this.shown ? this.shown.cam : rect(0, 0, 1, 1);
      const s = this.settings, stop = this.stop;
      if (s.drift === "off" || this.overview || !stop || stop.to) return this.shown.cam;
      const p = EASINGS.easeInOutSine(Math.min(1, (now - this.restSince) / (s.driftSeconds * 1000)));
      if (s.drift === "zoomIn") return scaleAround(this.shown.cam, 1 - s.driftAmount * p);
      return scaleAround(this.shown.cam, 1, this.shown.cam.w * s.driftAmount * p);
    }

    _frame(now) {
      if (!this.running) return;
      const a = this.anim;
      if (a) {
        const raw = a.ms === 0 ? 1 : Math.min(1, (now - a.start) / a.ms);
        const e = (EASINGS[a.easing] || EASINGS.easeInOutCubic)(raw);
        let cam = mixRect(a.from.cam, a.to.cam, e, a.smooth);
        if (a.arc) {
          // Swell the camera mid-move so both pages fit, then settle in.
          const u = inflate(union(a.from.cam, a.to.cam), 0.05);
          const mid = mixRect(a.from.cam, a.to.cam, 0.5, a.smooth);
          const need = Math.max(u.w / mid.w, u.h / mid.h) - 1;
          cam = scaleAround(cam, 1 + Math.max(0, need) * this.settings.arcAmount * Math.sin(Math.PI * Math.min(1, Math.max(0, e))));
        }
        this.shown = {
          cam,
          focus: mixRect(a.from.focus, a.to.focus, Math.min(1, e), a.smooth),
          dim: lerp(a.from.dim, a.to.dim, raw),
          vig: lerp(a.from.vig, a.to.vig, raw),
        };
        if (raw >= 1) { this._finish(); this.onChange(this.state); }
      } else if (this.book && this.settings.autoPlay && !this.finished && this.canvas.clientWidth > 0
                 && now - this.restSince > this.settings.autoSeconds * 1000) {
        this.next();
      }
      this.draw(now);
      requestAnimationFrame(this._frame);
    }

    draw(now) {
      const { canvas, ctx } = this;
      const dpr = window.devicePixelRatio || 1;
      const W = canvas.clientWidth, H = canvas.clientHeight;
      if (W === 0 || H === 0) return;
      if (canvas.width !== Math.round(W * dpr) || canvas.height !== Math.round(H * dpr)) {
        canvas.width = Math.round(W * dpr);
        canvas.height = Math.round(H * dpr);
      }
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.fillStyle = "#1c1b22";
      ctx.fillRect(0, 0, W, H);
      if (!this.book || !this.shown) return;

      const { top, bottom } = this.insets;
      const viewH = Math.max(1, H - top - bottom);
      const cam = this.displayedCam(now);
      const sc = Math.min(W / cam.w, viewH / cam.h);
      const ox = W / 2 - (cam.x + cam.w / 2) * sc;
      const oy = top + viewH / 2 - (cam.y + cam.h / 2) * sc;
      const toScreen = r => rect(r.x * sc + ox, r.y * sc + oy, r.w * sc, r.h * sc);

      ctx.imageSmoothingQuality = "high";
      this.pageRects.forEach((pr, i) => {
        const r = toScreen(pr);
        if (r.x + r.w < 0 || r.x > W) return;
        ctx.fillStyle = "#fff";
        ctx.fillRect(r.x, r.y, r.w, r.h);
        ctx.drawImage(this.images[i], r.x, r.y, r.w, r.h);
      });

      const f = toScreen(this.shown.focus);
      if (this.shown.vig > 0.01) {
        // Soft oval darkening that leaves the focus box clear.
        const cx = f.x + f.w / 2, cy = f.y + f.h / 2;
        const rx = Math.max(f.w / 2, 1), ry = Math.max(f.h / 2, 1);
        ctx.save();
        ctx.translate(cx, cy);
        ctx.scale(rx, ry);
        const g = ctx.createRadialGradient(0, 0, 1.25, 0, 0, 2.6);
        g.addColorStop(0, "rgba(0,0,0,0)");
        g.addColorStop(1, `rgba(0,0,0,${this.shown.vig})`);
        ctx.fillStyle = g;
        ctx.fillRect(-cx / rx - 1, -cy / ry - 1, W / rx + 2, H / ry + 2);
        ctx.restore();
      }
      if (this.shown.dim > 0.01) {
        ctx.beginPath();
        ctx.rect(0, 0, W, H);
        if (ctx.roundRect) ctx.roundRect(f.x, f.y, f.w, f.h, 6); else ctx.rect(f.x, f.y, f.w, f.h);
        ctx.fillStyle = `rgba(0,0,0,${this.shown.dim})`;
        ctx.fill("evenodd");
      }
    }
  }

  /** Tap left/right, swipe, two-finger pinch, mouse wheel and trackpad pinch. */
  function attachGestures(reader) {
    const canvas = reader.canvas;
    const opt = { signal: reader.listeners.signal };
    const pointers = new Map();
    let gesture = null;
    let wheelAcc = 0, wheelLock = 0;

    canvas.addEventListener("wheel", e => {
      e.preventDefault();
      const now = performance.now();
      if (now < wheelLock) return;
      if (e.ctrlKey) {
        if (e.deltaY > 4) { reader.setOverview(true); wheelLock = now + 400; }
        else if (e.deltaY < -4) { reader.setOverview(false); wheelLock = now + 400; }
        return;
      }
      wheelAcc += e.deltaY + e.deltaX;
      if (Math.abs(wheelAcc) > 60) { reader.go(wheelAcc > 0 ? 1 : -1); wheelAcc = 0; wheelLock = now + 350; }
    }, { ...opt, passive: false });

    canvas.addEventListener("pointerdown", e => {
      canvas.setPointerCapture(e.pointerId);
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 1) gesture = { x: e.clientX, y: e.clientY, multi: false, startDist: 0, lastDist: 0 };
      if (pointers.size === 2 && gesture) {
        const [a, b] = [...pointers.values()];
        gesture.multi = true;
        gesture.startDist = gesture.lastDist = Math.hypot(a.x - b.x, a.y - b.y);
      }
    }, opt);

    canvas.addEventListener("pointermove", e => {
      if (!pointers.has(e.pointerId)) return;
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 2 && gesture) {
        const [a, b] = [...pointers.values()];
        gesture.lastDist = Math.hypot(a.x - b.x, a.y - b.y);
      }
    }, opt);

    canvas.addEventListener("pointerup", e => {
      if (!pointers.has(e.pointerId)) return;
      const p = pointers.get(e.pointerId);
      pointers.delete(e.pointerId);
      if (pointers.size > 0 || !gesture) return;
      const g = gesture;
      gesture = null;
      if (g.multi) {
        const zoom = g.lastDist / (g.startDist || 1);
        if (zoom < 0.85) reader.setOverview(true);
        else if (zoom > 1.15) reader.setOverview(false);
        return;
      }
      const dx = p.x - g.x, dy = p.y - g.y;
      const box = canvas.getBoundingClientRect();
      if (Math.abs(dx) > 48 && Math.abs(dx) > Math.abs(dy)) reader.go(dx < 0 ? 1 : -1);
      else if (Math.hypot(dx, dy) < 10) reader.go(g.x - box.left < box.width * 0.3 ? -1 : 1);
    }, opt);

    canvas.addEventListener("pointercancel", e => { pointers.delete(e.pointerId); gesture = null; }, opt);
  }

  window.ComicReader = {
    Reader, DEFAULT_SETTINGS, EASINGS,
    loadSettings, saveSettings, normalizeBook, serializeBook, loadImage, layoutPages,
    rect, inflate, fitToAspect,
  };
})();
