/*
 * Comic Reader engine, shared by the playground (index.html) and the editor preview (editor.html).
 *
 * Book format (book.json):
 * {
 *   "id": "zolas-kite", "title": "...", "author": "...", "cover": "cover.webp",
 *   "focusStyle": "dim" | "vignette" | "none",       // book default, see below
 *   "pages": [
 *     { "image": "page1.webp",
 *       "mode": "guided" | "explore",                 // optional, guided by default
 *       "stops": [                                    // on explore pages these play first as a guided intro
 *         { "box": { "x": 0.1, "y": 0.2, "w": 0.4, "h": 0.3 },   // fractions of the page image
 *           "to":  { ... },          // optional: glide from box to this box while resting
 *           "panSeconds": 4,         // optional: how long that glide takes
 *           "style": "vignette",     // optional: overrides the book's focusStyle for this stop
 *           "caption": "Text..." }   // optional: shown under the picture
 *       ],
 *       "hotspots": [                                 // explore pages: things to find and tap
 *         { "box": { ... }, "caption": "A sleepy lion!", "effect": "sparkle" | "none" }
 *       ],
 *       "guidance": { "hints": true, "showMe": true } // explore pages: idle hints and the Show me button
 *     }
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
    hintSeconds: 6,
    glide: 0.5,
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
  const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
  const rect = (x, y, w, h) => ({ x, y, w, h });
  const inflate = (r, d) => rect(r.x - d, r.y - d, r.w + 2 * d, r.h + 2 * d);
  const union = (a, b) => {
    const x = Math.min(a.x, b.x), y = Math.min(a.y, b.y);
    return rect(x, y, Math.max(a.x + a.w, b.x + b.w) - x, Math.max(a.y + a.h, b.y + b.h) - y);
  };
  const contains = (r, x, y) => x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h;

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
      mode: p.mode === "explore" ? "explore" : "guided",
      stops: (p.stops || (p.panels || []).map(b => ({ box: b }))).map(s => ({ ...s })),
      hotspots: (p.hotspots || []).map(h => ({ ...h })),
      guidance: { hints: true, showMe: true, ...(p.guidance || {}) },
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
      pages: book.pages.map(p => {
        const page = {
          image: p.image,
          stops: p.stops.map(s => {
            const o = { box: roundBox(s.box) };
            if (s.to) { o.to = roundBox(s.to); o.panSeconds = s.panSeconds || 4; }
            if (s.style) o.style = s.style;
            if (s.caption) o.caption = s.caption;
            return o;
          }),
        };
        if (p.mode === "explore") {
          page.mode = "explore";
          page.hotspots = (p.hotspots || []).map(h => {
            const o = { box: roundBox(h.box) };
            if (h.caption) o.caption = h.caption;
            if (h.effect && h.effect !== "sparkle") o.effect = h.effect;
            return o;
          });
          const g = p.guidance || {};
          page.guidance = { hints: g.hints !== false, showMe: g.showMe !== false };
        }
        return page;
      }),
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
      this.view = null;          // last screen mapping, for touch maths
      this.fling = null;         // explore: glide velocity after a flick
      this.found = new Set();    // explore: hotspots found, as "page:index"
      this.effects = [];         // explore: sparkles playing
      this.note = null;          // explore: caption from a tapped hotspot
      this.lastTouch = 0;
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
      this.fling = null;
      this.found = new Set();
      this.effects = [];
      this.note = null;
      this.shown = this.target();
      this.restSince = this.lastTouch = performance.now();
      this._afterArrive();
      this.onChange(this.state);
    }

    /** Index of the first step showing the given page and stop (-1 whole page, -2 explore). */
    findStep(page, stop) {
      let i = this.steps.findIndex(s => s.page === page && s.stop === stop);
      if (i < 0) i = this.steps.findIndex(s => s.page === page);
      return Math.max(0, i);
    }

    /** Show a page and stop straight away, then play its pan if it has one. */
    jumpTo(page, stop) {
      this.stepIndex = this.findStep(page, stop);
      this.overview = false;
      this.finished = false;
      this.anim = null;
      this.fling = null;
      this.shown = this.target();
      this.restSince = this.lastTouch = performance.now();
      this._afterArrive();
      this.onChange(this.state);
    }

    buildSteps() {
      const s = this.settings, out = [];
      this.book.pages.forEach((page, p) => {
        const n = page.stops.length;
        if (page.mode === "explore") {
          // Guided intro (if any), then the page is handed over for exploring.
          if (n && s.wholePageFirst) out.push({ page: p, stop: -1 });
          for (let i = 0; i < n; i++) out.push({ page: p, stop: i });
          out.push({ page: p, stop: -2, explore: true });
          return;
        }
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
      if (!this.exploring) this.moveTo(cur.page);
    }

    boxRect(page, b) {
      const pr = this.pageRects[page];
      return rect(pr.x + b.x * pr.w, pr.y + b.y * pr.h, b.w * pr.w, b.h * pr.h);
    }

    get step() { return this.steps[this.stepIndex]; }
    get stop() { const st = this.step; return st && st.stop >= 0 ? this.book.pages[st.page].stops[st.stop] : null; }
    get exploring() { const st = this.step; return !!(st && st.explore && !this.finished); }
    get pageData() { return this.book.pages[this.step.page]; }

    /** Where the camera starts when a page is handed over for exploring. */
    exploreStart() {
      const st = this.step, prev = this.steps[this.stepIndex - 1];
      if (prev && prev.page === st.page && prev.stop >= 0) {
        // Carry on from where the guided intro ended.
        const s = this.book.pages[prev.page].stops[prev.stop];
        return inflate(this.boxRect(st.page, s.to || s.box), this.settings.padding);
      }
      const pr = this.pageRects[st.page];
      const aspect = pr.w / pr.h;
      if (aspect > 2.5) return rect(pr.x, pr.y, pr.h * 1.4, pr.h);              // wide scene: start at the left
      if (aspect < 0.4) return rect(pr.x, pr.y, pr.w, pr.w * 1.4);              // tall scene: start at the top
      return inflate(pr, 0.03);
    }

    /** Camera, spotlight box and effect strengths for the current step (start box, or end box of a pan). */
    target(end = false) {
      const s = this.settings, step = this.step;
      const pr = this.pageRects[step.page];
      const styled = (cam, focus, style, k) => ({
        cam, focus,
        dim: style === "dim" ? s.dim * k : 0,
        vig: style === "vignette" ? s.vignette * k : 0,
      });
      if (step.explore) return styled(this.exploreStart(), pr, "none", 0);
      const stop = this.stop;
      if (this.overview || !stop) return styled(inflate(pr, 0.03), pr, this.book.focusStyle, 0.6);
      const box = this.boxRect(step.page, end && stop.to ? stop.to : stop.box);
      return styled(inflate(box, s.padding), box, stop.style || this.book.focusStyle, 1);
    }

    unfoundHotspots() {
      if (!this.exploring) return [];
      const p = this.step.page;
      return this.pageData.hotspots.map((h, i) => ({ h, i })).filter(({ i }) => !this.found.has(p + ":" + i));
    }

    get state() {
      const step = this.step, stop = this.stop;
      const exploring = this.exploring;
      const hs = exploring ? this.pageData.hotspots : [];
      const guidance = exploring ? this.pageData.guidance : {};
      const now = performance.now();
      return {
        stepIndex: this.stepIndex,
        stepCount: this.steps.length,
        page: step ? step.page : 0,
        pageCount: this.book ? this.book.pages.length : 0,
        overview: this.overview,
        wholePage: this.overview || (!stop && !exploring),
        finished: this.finished,
        exploring,
        caption: exploring ? (this.note && this.note.until > now ? this.note.text : "")
                           : (!this.overview && stop && stop.caption ? stop.caption : ""),
        canGoBack: this.stepIndex > 0 || this.overview,
        canShowMe: exploring && guidance.showMe !== false && this.unfoundHotspots().length > 0,
        found: hs.filter((_, i) => this.found.has(step.page + ":" + i)).length,
        hotspotCount: hs.length,
      };
    }

    /** Move from wherever the camera is now (drift included) to the current step. */
    moveTo(previousPage, instant = false) {
      const s = this.settings, now = performance.now();
      const pageChanged = previousPage !== undefined && previousPage !== this.step.page;
      let ms = s.moveMs;
      if (instant || (pageChanged && s.pageTurn === "cut")) ms = 0;
      this.fling = null;
      this.note = null;
      this.anim = {
        kind: "move",
        from: { ...this.shown, cam: this.displayedCam(now) },
        to: this.target(),
        start: now, ms,
        easing: s.easing,
        smooth: s.zoomPath === "smooth",
        arc: pageChanged && s.pageTurn === "zoomOut",
      };
      this.lastTouch = now;
      if (ms === 0) this._finish();
      this.onChange(this.state);
    }

    /** Explore: animate the camera to a box, then optionally run something. */
    flyTo(cam, then) {
      const now = performance.now();
      this.fling = null;
      this.anim = {
        kind: "fly",
        from: { ...this.shown, cam: this.visibleCam() },
        to: { ...this.shown, cam: this.clampCam(this.matchAspect(cam)) },
        start: now, ms: Math.max(450, this.settings.moveMs),
        easing: "easeInOutCubic", smooth: true, arc: false, then,
      };
    }

    _finish() {
      const done = this.anim;
      this.shown = { ...done.to };
      this.anim = null;
      this.restSince = performance.now();
      if (done.kind === "move") this._afterArrive();
      if (done.then) done.then();
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
      if (this.exploring) {
        // Zoom right out to the whole page, or back in to where exploring started.
        const whole = this.maxCam();
        const zoomedOut = this.visibleCam().w >= whole.w * 0.98;
        this.flyTo(zoomedOut ? this.exploreStart() : whole);
        return;
      }
      if (!this.overview && !this.stop) { this.next(); return; }
      this.overview = !this.overview;
      this.moveTo(this.step.page);
    }

    setOverview(on) {
      if (this.exploring) { if (on) this.flyTo(this.maxCam()); return; }
      if (this.overview !== on) this.toggleOverview();
    }

    restart() {
      this.finished = false;
      this.overview = false;
      this.found = new Set();
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

    /** Explore: fly to the next thing not found yet and make it react. */
    showMe() {
      const next = this.unfoundHotspots()[0];
      if (!next) return;
      const box = this.boxRect(this.step.page, next.h.box);
      this.lastTouch = performance.now();
      this.flyTo(inflate(box, Math.max(box.h, box.w) * 0.6), () => this.react(next.i));
    }

    /** A hotspot was found: sparkle and show its caption. */
    react(i) {
      const p = this.step.page, h = this.pageData.hotspots[i];
      this.found.add(p + ":" + i);
      const now = performance.now();
      if (h.effect !== "none") this.effects.push({ box: this.boxRect(p, h.box), start: now });
      this.note = h.caption ? { text: h.caption, until: now + 5000 } : null;
      this.onChange(this.state);
      if (this.note) setTimeout(() => this.onChange(this.state), 5050);
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

    // ------------------------------------------------ explore camera

    viewAspect() {
      const W = this.canvas.clientWidth, H = this.canvas.clientHeight;
      return W / Math.max(1, H - this.insets.top - this.insets.bottom);
    }

    matchAspect(cam) { return fitToAspect(cam, this.viewAspect()); }

    /** The area actually on screen right now, in world units, with the screen's shape. */
    visibleCam() { return this.matchAspect(this.displayedCam(performance.now())); }

    /** Fully zoomed out: the whole page. */
    maxCam() { return this.matchAspect(inflate(this.pageRects[this.step.page], 0.03)); }

    /** Keep the explore camera on the page and within sensible zoom limits. */
    clampCam(cam) {
      const pr = this.pageRects[this.step.page];
      const max = this.maxCam();
      const minH = 0.12; // closest zoom shows 12% of the page height
      let c = { ...cam };
      if (c.w > max.w) c = scaleAround(c, max.w / c.w);
      if (c.h < minH) c = scaleAround(c, minH / c.h);
      const m = 0.03;
      const fit = (pos, size, lo, len) => size >= len + 2 * m ? lo + len / 2 - size / 2 : clamp(pos, lo - m, lo + len + m - size);
      c.x = fit(c.x, c.w, pr.x, pr.w);
      c.y = fit(c.y, c.h, pr.y, pr.h);
      return c;
    }

    /** Explore: user takes the camera (stops any animation or glide). */
    takeCamera() {
      const cam = this.visibleCam();
      if (this.anim && this.anim.kind === "move") this._finish();
      this.anim = null;
      this.fling = null;
      this.shown = { ...this.shown, cam, dim: 0, vig: 0 };
      this.lastTouch = performance.now();
      return cam;
    }

    setCam(cam) {
      this.shown = { ...this.shown, cam: this.clampCam(cam) };
      this.lastTouch = performance.now();
    }

    screenScale(cam) { return this.canvas.clientWidth / cam.w; }

    /** Screen point (relative to the canvas) to world point, for a camera that matches the screen shape. */
    toWorld(cam, sx, sy) {
      const sc = this.screenScale(cam);
      return { x: cam.x + sx / sc, y: cam.y + (sy - this.insets.top) / sc };
    }

    /** Zoom by factor f keeping the world point under (sx, sy) fixed. */
    zoomAt(cam, f, sx, sy) {
      const a = this.toWorld(cam, sx, sy);
      const w = cam.w / f, h = cam.h / f;
      const sc = this.canvas.clientWidth / w;
      return rect(a.x - sx / sc, a.y - (sy - this.insets.top) / sc, w, h);
    }

    tapAt(sx, sy) {
      if (!this.exploring) return;
      const cam = this.visibleCam();
      const pt = this.toWorld(cam, sx, sy);
      const p = this.step.page;
      const hs = this.pageData.hotspots;
      // Smallest hotspot under the finger wins.
      let best = -1, area = Infinity;
      hs.forEach((h, i) => {
        const r = this.boxRect(p, h.box);
        const pad = 0.015; // a little forgiveness for small fingers
        if (contains(inflate(r, pad), pt.x, pt.y) && r.w * r.h < area) { best = i; area = r.w * r.h; }
      });
      this.lastTouch = performance.now();
      if (best >= 0) this.react(best);
    }

    doubleTapAt(sx, sy) {
      if (!this.exploring) return;
      const cam = this.takeCamera();
      const zoomedIn = cam.h < 0.45;
      this.flyTo(zoomedIn ? this.maxCam() : this.zoomAt(cam, 2.2, sx, sy));
    }

    // ------------------------------------------------ frame loop and drawing

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
      const dt = Math.min(50, now - (this._last || now));
      this._last = now;
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
      } else if (this.fling) {
        // Glide after a flick, slowing down.
        const f = this.fling;
        const cam = this.shown.cam;
        const moved = this.clampCam(rect(cam.x + f.vx * dt, cam.y + f.vy * dt, cam.w, cam.h));
        if (Math.abs(moved.x - (cam.x + f.vx * dt)) > 1e-6) f.vx = 0;
        if (Math.abs(moved.y - (cam.y + f.vy * dt)) > 1e-6) f.vy = 0;
        this.shown = { ...this.shown, cam: moved };
        const decay = Math.pow(0.9 + 0.08 * this.settings.glide, dt / 16);
        f.vx *= decay;
        f.vy *= decay;
        if (Math.hypot(f.vx, f.vy) < 1e-5) this.fling = null;
      } else if (this.book && this.settings.autoPlay && !this.finished && !this.exploring && this.canvas.clientWidth > 0
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
      this.view = { sc, ox, oy, W, H };

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

      this.drawEffects(now, toScreen);
      this.drawHint(now, toScreen, W, H, top, bottom);
    }

    /** Sparkle burst on found hotspots. */
    drawEffects(now, toScreen) {
      const ctx = this.ctx;
      this.effects = this.effects.filter(e => now - e.start < 1100);
      for (const e of this.effects) {
        const t = (now - e.start) / 1100;
        const r = toScreen(e.box);
        const cx = r.x + r.w / 2, cy = r.y + r.h / 2;
        const base = Math.max(r.w, r.h) / 2;
        ctx.save();
        ctx.globalAlpha = 1 - t;
        ctx.strokeStyle = "#ffd23f";
        ctx.lineWidth = 6 * (1 - t) + 1;
        ctx.beginPath();
        ctx.ellipse(cx, cy, r.w / 2 + base * 0.5 * t + 6, r.h / 2 + base * 0.5 * t + 6, 0, 0, Math.PI * 2);
        ctx.stroke();
        for (let k = 0; k < 10; k++) {
          const a = (k / 10) * Math.PI * 2 + t;
          const d = base * (0.9 + 0.9 * EASINGS.easeOutCubic(t));
          star(ctx, cx + Math.cos(a) * d, cy + Math.sin(a) * d, 9 * (1 - t) + 3, k % 2 ? "#fff6c2" : "#ffb703");
        }
        ctx.restore();
      }
    }

    /** When the child has been idle, point at something not found yet. */
    drawHint(now, toScreen, W, H, top, bottom) {
      if (!this.exploring || this.anim || this.fling) return;
      if (this.pageData.guidance.hints === false) return;
      if (now - this.lastTouch < this.settings.hintSeconds * 1000) return;
      const next = this.unfoundHotspots()[0];
      if (!next) return;
      const ctx = this.ctx;
      const r = toScreen(this.boxRect(this.step.page, next.h.box));
      const pulse = 0.5 + 0.5 * Math.sin(now / 260);
      const cx = r.x + r.w / 2, cy = r.y + r.h / 2;
      const onScreen = cx > 0 && cx < W && cy > top && cy < H - bottom;
      ctx.save();
      if (onScreen) {
        ctx.strokeStyle = `rgba(255,210,63,${0.5 + 0.5 * pulse})`;
        ctx.lineWidth = 4 + 4 * pulse;
        ctx.shadowColor = "#ffd23f";
        ctx.shadowBlur = 20;
        ctx.beginPath();
        ctx.ellipse(cx, cy, r.w / 2 + 10 + 6 * pulse, r.h / 2 + 10 + 6 * pulse, 0, 0, Math.PI * 2);
        ctx.stroke();
      } else {
        // Arrow at the screen edge, pointing the way.
        const mx = W / 2, my = top + (H - top - bottom) / 2;
        const ang = Math.atan2(cy - my, cx - mx);
        const m = 46;
        const ex = clamp(mx + Math.cos(ang) * W, m, W - m);
        const ey = clamp(my + Math.sin(ang) * H, top + m, H - bottom - m);
        ctx.translate(ex, ey);
        ctx.rotate(ang);
        ctx.fillStyle = `rgba(255,210,63,${0.75 + 0.25 * pulse})`;
        ctx.shadowColor = "rgba(0,0,0,.4)";
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.arc(0, 0, 28 + 3 * pulse, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "#3a2a00";
        ctx.beginPath();
        ctx.moveTo(16, 0);
        ctx.lineTo(-8, -13);
        ctx.lineTo(-8, 13);
        ctx.fill();
      }
      ctx.restore();
    }
  }

  function star(ctx, x, y, r, color) {
    ctx.fillStyle = color;
    ctx.beginPath();
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2;
      const d = i % 2 ? r * 0.4 : r;
      ctx.lineTo(x + Math.cos(a) * d, y + Math.sin(a) * d);
    }
    ctx.fill();
  }

  /**
   * Guided pages: tap left/right, swipe, two-finger pinch, mouse wheel.
   * Explore pages: drag to move, pinch or wheel to zoom, flick to glide, double-tap to zoom, tap to find things.
   */
  function attachGestures(reader) {
    const canvas = reader.canvas;
    const opt = { signal: reader.listeners.signal };
    const pointers = new Map();
    let gesture = null;
    let wheelAcc = 0, wheelLock = 0;
    let lastTap = null;
    const local = e => { const b = canvas.getBoundingClientRect(); return { x: e.clientX - b.left, y: e.clientY - b.top }; };

    canvas.addEventListener("wheel", e => {
      e.preventDefault();
      const now = performance.now();
      if (reader.exploring) {
        const p = local(e);
        const cam = reader.takeCamera();
        if (e.ctrlKey || (e.deltaMode !== 0 || Math.abs(e.deltaX) < 1)) {
          // Pinch on a trackpad, or a mouse wheel: zoom at the pointer.
          reader.setCam(reader.zoomAt(cam, Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0015)), p.x, p.y));
        } else {
          const sc = reader.screenScale(cam);
          reader.setCam(rect(cam.x + e.deltaX / sc, cam.y + e.deltaY / sc, cam.w, cam.h));
        }
        return;
      }
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
      const p = local(e);
      pointers.set(e.pointerId, p);
      if (pointers.size === 1) {
        gesture = { start: p, multi: false, startDist: 0, lastDist: 0, explore: reader.exploring, samples: [] };
        if (gesture.explore) gesture.cam = reader.takeCamera();
      }
      if (pointers.size === 2 && gesture) {
        const [a, b] = [...pointers.values()];
        gesture.multi = true;
        gesture.startDist = gesture.lastDist = Math.hypot(a.x - b.x, a.y - b.y);
        if (gesture.explore) {
          gesture.cam = reader.takeCamera();
          gesture.mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        }
      }
    }, opt);

    canvas.addEventListener("pointermove", e => {
      if (!pointers.has(e.pointerId) || !gesture) return;
      const p = local(e);
      pointers.set(e.pointerId, p);
      if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        gesture.lastDist = Math.hypot(a.x - b.x, a.y - b.y);
        if (gesture.explore) {
          // Zoom around the fingers' midpoint and follow it as it moves.
          const f = gesture.lastDist / (gesture.startDist || 1);
          const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
          const zoomed = reader.zoomAt(gesture.cam, f, gesture.mid.x, gesture.mid.y);
          const sc = reader.screenScale(zoomed);
          reader.setCam(rect(zoomed.x - (mid.x - gesture.mid.x) / sc, zoomed.y - (mid.y - gesture.mid.y) / sc, zoomed.w, zoomed.h));
        }
        return;
      }
      if (gesture.explore && !gesture.multi) {
        const sc = reader.screenScale(gesture.cam);
        const dx = p.x - gesture.start.x, dy = p.y - gesture.start.y;
        reader.setCam(rect(gesture.cam.x - dx / sc, gesture.cam.y - dy / sc, gesture.cam.w, gesture.cam.h));
        const now = performance.now();
        gesture.samples.push({ x: p.x, y: p.y, t: now });
        while (gesture.samples.length && now - gesture.samples[0].t > 90) gesture.samples.shift();
      }
    }, opt);

    canvas.addEventListener("pointerup", e => {
      if (!pointers.has(e.pointerId)) return;
      const p = local(e);
      pointers.delete(e.pointerId);
      if (pointers.size > 0 || !gesture) return;
      const g = gesture;
      gesture = null;
      const dx = p.x - g.start.x, dy = p.y - g.start.y;
      const isTap = !g.multi && Math.hypot(dx, dy) < 10;

      if (g.explore) {
        if (g.multi) { reader.setCam(reader.shown.cam); return; }
        if (isTap) {
          const now = performance.now();
          if (lastTap && now - lastTap.t < 320 && Math.hypot(p.x - lastTap.x, p.y - lastTap.y) < 30) {
            lastTap = null;
            reader.doubleTapAt(p.x, p.y);
          } else {
            lastTap = { x: p.x, y: p.y, t: now };
            reader.tapAt(p.x, p.y);
          }
          return;
        }
        // Flick: keep gliding in the direction of the last bit of the drag.
        const s = g.samples;
        if (s.length >= 2) {
          const a = s[0], b = s[s.length - 1];
          const dt = Math.max(1, b.t - a.t);
          const sc = reader.screenScale(reader.shown.cam);
          const vx = -(b.x - a.x) / dt / sc, vy = -(b.y - a.y) / dt / sc;
          if (Math.hypot(vx, vy) * sc > 0.25) reader.fling = { vx, vy };
        }
        return;
      }

      if (g.multi) {
        const zoom = g.lastDist / (g.startDist || 1);
        if (zoom < 0.85) reader.setOverview(true);
        else if (zoom > 1.15) reader.setOverview(false);
        return;
      }
      const box = canvas.getBoundingClientRect();
      if (Math.abs(dx) > 48 && Math.abs(dx) > Math.abs(dy)) reader.go(dx < 0 ? 1 : -1);
      else if (isTap) reader.go(g.start.x < box.width * 0.3 ? -1 : 1);
    }, opt);

    canvas.addEventListener("pointercancel", e => { pointers.delete(e.pointerId); gesture = null; }, opt);
  }

  window.ComicReader = {
    Reader, DEFAULT_SETTINGS, EASINGS,
    loadSettings, saveSettings, normalizeBook, serializeBook, loadImage, layoutPages,
    rect, inflate, fitToAspect,
  };
})();
