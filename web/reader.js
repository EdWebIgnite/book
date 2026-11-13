/*
 * Comic Reader engine, shared by the playground (index.html) and the editor preview (editor.html).
 *
 * Book format (book.json):
 * {
 *   "id": "zolas-kite", "title": "...", "author": "...", "cover": "cover.webp",
 *   "focusStyle": "dim" | "vignette" | "spotlight" | "none",   // book default, see below
 *   "pages": [
 *     { "image": "page1.webp",
 *       "mode": "guided" | "explore",                 // optional, guided by default
 *       "openWhole": false,                           // optional: go straight to the first stop instead of
 *                                                     // showing the whole page first (keeps a surprise hidden)
 *       "stops": [                                    // on explore pages these play first as a guided intro
 *         { "box": { "x": 0.1, "y": 0.2, "w": 0.4, "h": 0.3 },   // fractions of the page image
 *           "to":  { ... },          // optional: glide from box to this box while resting
 *           "panSeconds": 4,         // optional: how long that glide takes
 *           "moveSeconds": 2.5,      // optional: how long the camera takes to get to this stop (0 = cut)
 *           "auto": true,            // optional: go on to the next stop without a tap...
 *           "holdSeconds": 1.5,      // ...after resting here this long (also the rest time in watch mode)
 *           "style": "vignette",     // optional: overrides the book's focusStyle for this stop
 *           "caption": "Text...",    // optional: shown under the picture
 *           "sound": "page3.mp3",    // optional: a sound file in the book folder, played on arriving
 *           "ask": "Why is she sad?" }  // optional: a question for the adult reading along
 *       ],
 *       "hotspots": [                                 // explore pages: things to find and tap
 *         { "box": { ... }, "caption": "A sleepy lion!", "effect": "sparkle" | "none",
 *           "story": 2,              // optional: this is step 2 of the story hidden in the scene.
 *                                    // Story steps unlock in order and the next one shows a beacon.
 *           "name": "Old Turtle" }   // optional: used in "First find Old Turtle" when tapped too early
 *       ],
 *       "guidance": { "hints": true, "showMe": true, "map": false },  // idle hints, Show me button, corner map
 *       "effects": [                                  // optional: things the reader draws moving over the picture
 *         { "type": "rain" | "snow" | "leaves" | "sparkles",
 *           "amount": 0.6,                            // how much, 0 to 1
 *           "from": 2, "to": 3,                       // optional: runs from this stop to that one (1 = first)
 *           "ground": 0.62,                           // rain only: splash rings below this line (fraction of height)
 *           "dry": [ { box } ] },                     // optional: areas it misses (under a roof)
 *         { "type": "lightning", "stop": 1 },         // a soft flash and a rumble on arriving at this stop
 *         { "type": "shake", "stop": 2, "strength": 0.6 }   // the camera shakes on arriving at this stop
 *       ],
 *       "layers": [                                   // optional: pictures placed on top of the page
 *         { "image": "boat.png", "box": { ... },      // a picture with a see-through background, and where it sits
 *           "from": 2, "to": 3,                       // optional: only shown between these stops
 *           "moveTo": { ... }, "moveAt": 2,           // optional: moves to this box when stop 2 arrives...
 *           "moveSeconds": 1.2, "path": "hop",        // ...taking this long, straight or in a hop
 *           "idle": "bob" | "sway" }                  // optional: a small movement while it waits
 *       ],
 *       "tiles": { "width": 8192, "height": 6144, "size": 1024, "levels": 2,
 *                  "path": "page1_tiles/{z}/{x}_{y}.jpg" }   // optional, for very big scenes: "image" is then
 *                                    // a small overview, and sharp tiles load only where the child is looking.
 *                                    // Level 0 is full size, each level after is half the one before.
 *     }
 *   ]
 * }
 * Older files that list "panels" (plain boxes) instead of "stops" still load.
 */
"use strict";
(function () {

  const DEFAULT_SETTINGS = {
    moveMs: 1400,
    easing: "easeInOutSine",
    zoomPath: "smooth",
    pageTurn: "fly",
    arcAmount: 0.6,
    padding: 0.025,
    dim: 0.72,
    vignette: 0.75,
    spotlight: 0.97,        // how dark it gets outside the light on a "spotlight" stop
    wholePageFirst: true,
    wholePageLast: false,
    drift: "off",
    driftAmount: 0.06,
    driftSeconds: 6,
    autoPlay: false,
    autoSeconds: 4,
    hintSeconds: 4,
    glide: 0.5,
    // For parents
    age: "3-5",              // which preset was last applied
    reduceMotion: "auto",    // auto (follow the device) | on | off
    sound: true,
    extras: "afterStory",    // things to find that aren't part of the story: on | afterStory | off
    prompts: false,          // show "ask" questions for the adult
    captionSize: "medium",
    gate: false,             // gear needs a two-second hold
    // Fine tuning
    scaleTime: true,         // longer moves take longer
    minTargetCm: 2.4,        // smallest touch target for things to find
    doubleTapZoom: false,    // young children double-tap by accident
  };

  /** What each age band changes. Research basis: docs/movement-ideas.md. */
  const AGE_PRESETS = {
    "3-5": { moveMs: 1400, hintSeconds: 4, minTargetCm: 2.4, doubleTapZoom: false, easing: "easeInOutSine" },
    "6-8": { moveMs: 700, hintSeconds: 6, minTargetCm: 2.0, doubleTapZoom: false, easing: "easeInOutCubic" },
    "9+": { moveMs: 550, hintSeconds: 9, minTargetCm: 1.4, doubleTapZoom: true, easing: "easeInOutCubic" },
  };
  const CM = 37.8; // CSS pixels per centimetre
  const FALLING = new Set(["rain", "snow", "leaves", "sparkles"]);   // effects made of many small moving pieces

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
      openWhole: p.openWhole !== false,
      stops: (p.stops || (p.panels || []).map(b => ({ box: b }))).map(s => ({ ...s })),
      hotspots: (p.hotspots || []).map(h => ({ ...h })),
      guidance: { hints: true, showMe: true, map: false, ...(p.guidance || {}) },
      effects: (p.effects || []).map(e => ({ ...e, ...(e.dry ? { dry: e.dry.map(b => ({ ...b })) } : {}) })),
      layers: (p.layers || []).map(l => ({ ...l, box: { ...l.box }, ...(l.moveTo ? { moveTo: { ...l.moveTo } } : {}) })),
      tiles: p.tiles ? { ...p.tiles } : null,
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
            if (s.moveSeconds !== undefined && s.moveSeconds !== null && s.moveSeconds !== "") o.moveSeconds = +s.moveSeconds;
            if (s.auto) o.auto = true;
            if (s.holdSeconds) o.holdSeconds = +s.holdSeconds;
            if (s.caption) o.caption = s.caption;
            if (s.ask) o.ask = s.ask;
            if (s.sound) o.sound = s.sound;
            return o;
          }),
        };
        if (p.openWhole === false) page.openWhole = false;
        if (p.effects && p.effects.length) {
          page.effects = p.effects.map(e => {
            if (e.type === "lightning") return { type: "lightning", stop: e.stop || 1 };
            if (e.type === "shake") return { type: "shake", stop: e.stop || 1, strength: round4(e.strength === undefined ? 0.6 : e.strength) };
            const o = { type: e.type, amount: round4(e.amount === undefined ? 0.6 : e.amount) };
            if (e.from > 1) o.from = e.from;
            if (e.to >= 1) o.to = e.to;
            if (e.ground && e.type === "rain") o.ground = round4(e.ground);
            if (e.dry && e.dry.length) o.dry = e.dry.map(roundBox);
            return o;
          });
        }
        if (p.layers && p.layers.length) {
          page.layers = p.layers.map(l => {
            const o = { image: l.image, box: roundBox(l.box) };
            if (l.from > 1) o.from = l.from;
            if (l.to >= 1) o.to = l.to;
            if (l.moveTo) {
              o.moveTo = roundBox(l.moveTo);
              o.moveAt = l.moveAt || 1;
              o.moveSeconds = l.moveSeconds || 1.2;
              if (l.path === "hop") o.path = "hop";
            }
            if (l.idle === "bob" || l.idle === "sway") o.idle = l.idle;
            return o;
          });
        }
        if (p.tiles) page.tiles = { ...p.tiles };
        if (p.mode === "explore") {
          page.mode = "explore";
          page.hotspots = (p.hotspots || []).map(h => {
            const o = { box: roundBox(h.box) };
            if (h.name) o.name = h.name;
            if (h.story) o.story = h.story;
            if (h.caption) o.caption = h.caption;
            if (h.ask) o.ask = h.ask;
            if (h.sound) o.sound = h.sound;
            if (h.effect && h.effect !== "sparkle") o.effect = h.effect;
            return o;
          });
          const g = p.guidance || {};
          page.guidance = { hints: g.hints !== false, showMe: g.showMe !== false, map: !!g.map };
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

  // ---------------------------------------------------------------- sound

  /** Small synthesised sounds, so every touch can be answered without any audio files. */
  class Sounds {
    constructor() { this.ctx = null; }

    /** Browsers only allow sound after a touch, so this is called from the first one. */
    wake() {
      if (this.ctx) { if (this.ctx.state === "suspended") this.ctx.resume(); return; }
      const AC = window.AudioContext || window.webkitAudioContext;
      if (AC) { try { this.ctx = new AC(); } catch (_) { /* no audio available */ } }
    }

    tone(freq, start, len, gain = 0.06, type = "sine") {
      const c = this.ctx;
      if (!c) return;
      const t = c.currentTime + start, o = c.createOscillator(), g = c.createGain();
      o.type = type;
      o.frequency.value = freq;
      g.gain.setValueAtTime(0, t);
      g.gain.linearRampToValueAtTime(gain, t + 0.012);
      g.gain.exponentialRampToValueAtTime(0.0001, t + len);
      o.connect(g).connect(c.destination);
      o.start(t);
      o.stop(t + len + 0.02);
    }

    tap() { this.tone(720, 0, 0.09, 0.045); }
    zoom(up) { this.tone(up ? 620 : 520, 0, 0.08, 0.04); this.tone(up ? 820 : 400, 0.05, 0.1, 0.04); }
    find() { [1047, 1319, 1568].forEach((f, i) => this.tone(f, i * 0.07, 0.3, 0.055, "triangle")); }
    locked() { this.tone(330, 0, 0.12, 0.05, "triangle"); this.tone(262, 0.1, 0.18, 0.05, "triangle"); }
    party() { [784, 988, 1175, 1568, 1976].forEach((f, i) => this.tone(f, i * 0.09, 0.45, 0.06, "triangle")); }

    noise(seconds) {
      const c = this.ctx, buf = c.createBuffer(1, Math.ceil(c.sampleRate * seconds), c.sampleRate), d = buf.getChannelData(0);
      for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
      const src = c.createBufferSource();
      src.buffer = buf;
      return src;
    }

    /** Steady rain, as loud as `level` (0 to 1). Call it again to change or stop it. */
    rain(level) {
      const c = this.ctx;
      if (!c) return;
      if (!this._rain) {
        if (level < 0.01) return;
        const src = this.noise(2), hi = c.createBiquadFilter(), lo = c.createBiquadFilter(), g = c.createGain();
        src.loop = true;
        hi.type = "highpass"; hi.frequency.value = 1100;
        lo.type = "lowpass"; lo.frequency.value = 7000;
        g.gain.value = 0;
        src.connect(hi).connect(lo).connect(g).connect(c.destination);
        src.start();
        this._rain = g;
      }
      this._rain.gain.setTargetAtTime(level * 0.045, c.currentTime, 0.2);
    }

    /** A low rumble, not a crack: this is for small children. */
    thunder(delay = 0) {
      const c = this.ctx;
      if (!c) return;
      const t = c.currentTime + delay, src = this.noise(2.4), f = c.createBiquadFilter(), g = c.createGain();
      f.type = "lowpass";
      f.frequency.setValueAtTime(260, t);
      f.frequency.exponentialRampToValueAtTime(70, t + 2.2);
      g.gain.setValueAtTime(0, t);
      g.gain.linearRampToValueAtTime(0.5, t + 0.12);
      g.gain.exponentialRampToValueAtTime(0.001, t + 2.3);
      src.connect(f).connect(g).connect(c.destination);
      src.start(t);
    }

    /** Soft rush of air for a camera move, longer for longer moves. */
    whoosh(ms) {
      const c = this.ctx;
      if (!c || ms < 120) return;
      const len = Math.min(1.2, ms / 1000) * 0.9;
      const buf = c.createBuffer(1, Math.ceil(c.sampleRate * len), c.sampleRate);
      const d = buf.getChannelData(0);
      for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
      const src = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
      src.buffer = buf;
      f.type = "bandpass";
      f.Q.value = 0.8;
      f.frequency.setValueAtTime(300, c.currentTime);
      f.frequency.linearRampToValueAtTime(900, c.currentTime + len * 0.5);
      f.frequency.linearRampToValueAtTime(350, c.currentTime + len);
      g.gain.setValueAtTime(0, c.currentTime);
      g.gain.linearRampToValueAtTime(0.035, c.currentTime + len * 0.4);
      g.gain.linearRampToValueAtTime(0, c.currentTime + len);
      src.connect(f).connect(g).connect(c.destination);
      src.start();
    }
  }

  // ---------------------------------------------------------------- reader

  class Reader {
    constructor(canvas, opts = {}) {
      this.canvas = canvas;
      this.ctx = canvas.getContext("2d");
      this.settings = opts.settings || loadSettings();
      this.onChange = opts.onChange || (() => {});
      this.insets = opts.insets || { top: 64, bottom: 88 };
      this.insetGoal = { ...this.insets };   // the screen can ask for more room (for a caption); the picture eases to it
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
      this.party = 0;            // explore: when the story was finished, for the celebration
      this.tiles = new Map();    // big scenes: loaded tile images
      this.tileLoads = 0;
      this.baseUrl = "";         // folder the book's files are in, for loading tiles
      this.mapRect = null;       // where the corner map is on screen
      this.ripples = [];         // the little ring that answers every touch
      this.sounds = new Sounds();
      this.lastGo = 0;           // for ignoring accidental double taps
      this.moved = false;        // has the reader gone anywhere yet (for the first-tap hint)
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

    /** opts.baseUrl: the book's folder, needed for pages that load in tiles. */
    setBook(book, images, startStep = 0, opts = {}) {
      this.book = book;
      this.images = images;
      this.baseUrl = opts.baseUrl || "";
      this.assetUrls = opts.assets || {};   // file name -> URL, for files that aren't in the book's folder yet (the editor)
      this.assetCache = new Map();
      this.stopClip();
      this.tiles = new Map();
      this.fx = new Map();
      this.layerState = new Map();
      this.shake = null;
      this.flash = 0;
      this.arrivedForward = false;
      this.tileLoads = 0;
      this.party = 0;
      this.moved = false;
      this.lastGo = 0;
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
      this.arrivedForward = false;
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
          if (n && s.wholePageFirst && page.openWhole !== false) out.push({ page: p, stop: -1 });
          for (let i = 0; i < n; i++) out.push({ page: p, stop: i });
          out.push({ page: p, stop: -2, explore: true });
          return;
        }
        if (n === 0) { out.push({ page: p, stop: -1 }); return; }
        if (s.wholePageFirst && page.openWhole !== false) out.push({ page: p, stop: -1 });
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

    /** True when moves should be swapped for fades: the parent's setting, or the device's own. */
    get reduced() {
      const r = this.settings.reduceMotion;
      if (r === "on") return true;
      if (r === "off") return false;
      return !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
    }

    play(name, arg) { if (this.settings.sound && this.sounds[name]) this.sounds[name](arg); }

    /** Answer a touch: a small ring where the finger landed. */
    ripple(sx, sy) { this.ripples.push({ x: sx, y: sy, start: performance.now() }); }

    /**
     * How long a camera move should take. Longer and bigger moves take longer so the speed feels
     * even, and zooming is never faster than about double per quarter second.
     */
    moveTime(from, to, base) {
      const gw = Math.sqrt(from.w * to.w);
      const dc = Math.hypot(to.x + to.w / 2 - from.x - from.w / 2, to.y + to.h / 2 - from.y - from.h / 2) / gw;
      const dz = Math.abs(Math.log(to.w / from.w));
      const ms = this.settings.scaleTime ? base * clamp(0.55 + 0.45 * (dc + dz), 0.5, 2.4) : base;
      return { ms: Math.max(ms, dz * 330), far: dc > 3 };
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
        spot: style === "spotlight" ? s.spotlight * k : 0,
      });
      if (step.explore) return styled(this.exploreStart(), pr, "none", 0);
      const stop = this.stop;
      // The whole page never gets a spotlight: a book set to spotlight shows soft edges here instead.
      const wholeStyle = this.book.focusStyle === "spotlight" ? "vignette" : this.book.focusStyle;
      if (this.overview || !stop) return styled(inflate(pr, 0.03), pr, wholeStyle, 0.6);
      const box = this.boxRect(step.page, end && stop.to ? stop.to : stop.box);
      return styled(inflate(box, s.padding), box, stop.style || this.book.focusStyle, 1);
    }

    isFound(i) { return this.found.has(this.step.page + ":" + i); }

    /** Hotspot indexes that make up the page's story, in story order. */
    storyOrder() {
      return this.pageData.hotspots.map((h, i) => ({ h, i })).filter(x => x.h.story)
        .sort((a, b) => a.h.story - b.h.story).map(x => x.i);
    }

    /** The story step the child is looking for now, or -1. */
    nextStory() {
      if (!this.exploring) return -1;
      const next = this.storyOrder().find(i => !this.isFound(i));
      return next === undefined ? -1 : next;
    }

    /** What hints and Show me lead to: the next story step first, then anything else not found yet. */
    unfoundHotspots() {
      if (!this.exploring) return [];
      const hs = this.pageData.hotspots, next = this.nextStory();
      const extras = this.extrasOpen() ? hs.map((h, i) => ({ h, i })).filter(({ h, i }) => !h.story && !this.isFound(i)) : [];
      return next >= 0 ? [{ h: hs[next], i: next }, ...extras] : extras;
    }

    /** Are the just-for-fun things to find switched on right now? (A parent setting.) */
    extrasOpen() {
      const mode = this.settings.extras;
      if (mode === "off") return false;
      if (mode === "afterStory") return this.nextStory() < 0;   // no story on the page counts as finished
      return true;
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
        ask: !this.settings.prompts ? ""
           : exploring ? (this.note && this.note.until > now && this.note.ask ? this.note.ask : "")
           : (!this.overview && stop && stop.ask ? stop.ask : ""),
        firstStep: !this.moved && !exploring && this.stepIndex === 0,
        canZoomIn: exploring && this.visibleCam().h > 0.12 * 1.03,
        canZoomOut: exploring && this.visibleCam().w < this.maxCam().w * 0.98,
        canGoBack: this.stepIndex > 0 || this.overview,
        canShowMe: exploring && guidance.showMe !== false && this.unfoundHotspots().length > 0,
        found: hs.filter((_, i) => this.found.has(step.page + ":" + i)).length,
        hotspotCount: hs.length,
        storyTotal: hs.filter(h => h.story).length,
        storyFound: hs.filter((h, i) => h.story && this.found.has(step.page + ":" + i)).length,
        extraTotal: this.settings.extras === "off" ? 0 : hs.filter(h => !h.story).length,
        extraFound: hs.filter((h, i) => !h.story && this.found.has(step.page + ":" + i)).length,
      };
    }

    /** Move from wherever the camera is now (drift included) to the current step. */
    moveTo(previousPage, instant = false) {
      const s = this.settings, now = performance.now();
      this.party = 0;
      const pageChanged = previousPage !== undefined && previousPage !== this.step.page;
      const from = { ...this.shown, cam: this.displayedCam(now) }, to = this.target();
      const reduced = this.reduced;
      const timing = this.moveTime(from.cam, to.cam, s.moveMs);
      let ms = reduced ? 340 : timing.ms;
      // A stop can set its own travel time. It only applies when arriving from the stop before it.
      const own = this.stop && !this.overview && this.arrivedForward ? this.stop.moveSeconds : undefined;
      const hasOwn = own !== undefined && own !== null && own !== "";
      if (hasOwn && !reduced) ms = Math.max(0, +own * 1000);
      if (instant || (pageChanged && s.pageTurn === "cut" && !hasOwn)) ms = 0;
      // Pull back mid-flight on page changes (if chosen) and on any long journey, so the child sees where they are going.
      const arcK = reduced ? 0 : pageChanged && s.pageTurn === "zoomOut" ? s.arcAmount : timing.far ? 0.45 : 0;
      this.fling = null;
      this.note = null;
      this.stopClip();
      this.anim = {
        kind: "move", from, to,
        start: now, ms: ms + (arcK ? 220 : 0),
        easing: s.easing,
        smooth: s.zoomPath === "smooth",
        arcK, fade: reduced,
      };
      this.lastTouch = now;
      if (ms === 0) this._finish(); else this.play("whoosh", this.anim.ms);
      this.onChange(this.state);
    }

    /** Explore: animate the camera to a box, then optionally run something. */
    flyTo(cam, then, quick = false) {
      const now = performance.now();
      const from = { ...this.shown, cam: this.visibleCam() };
      const to = { ...this.shown, cam: this.clampCam(this.matchAspect(cam)) };
      const reduced = this.reduced;
      const timing = this.moveTime(from.cam, to.cam, Math.max(450, this.settings.moveMs));
      const arcK = !reduced && !quick && timing.far ? 0.45 : 0;
      this.fling = null;
      this.anim = {
        kind: "fly", from, to,
        start: now, ms: reduced ? 340 : quick ? 260 : timing.ms + (arcK ? 220 : 0),
        easing: "easeInOutCubic", smooth: true, arcK, fade: reduced && !quick, then,
      };
      if (!quick) this.play("whoosh", this.anim.ms);
    }

    _finish() {
      const done = this.anim;
      this.shown = { ...done.to };
      this.anim = null;
      this.restSince = performance.now();
      if (done.kind === "move") { this._arrive(); this._afterArrive(); }
      if (done.then) done.then();
    }

    /** A stop with an end box glides there once the camera has arrived. */
    _afterArrive() {
      const stop = this.stop;
      if (this.overview || !stop || !stop.to) return;
      // With reduced motion the glide becomes a short pause and a fade to where it would have ended.
      const reduced = this.reduced;
      this.anim = {
        kind: "pan",
        from: { ...this.shown },
        to: this.target(true),
        start: performance.now() + (reduced ? 1400 : 0),
        ms: reduced ? 340 : (stop.panSeconds || 4) * 1000,
        easing: "easeInOutSine",
        smooth: true,
        arcK: 0, fade: reduced,
      };
    }

    go(delta) {
      if (this.finished || !this.book) return;
      const now = performance.now();
      // Young children often tap twice without meaning to: ignore a second one that comes straight away.
      if (now - this.lastGo < 300) return;
      this.lastGo = now;
      // A tap while the camera is still travelling just finishes that move. The next tap goes on.
      if (delta > 0 && this.anim && this.anim.kind === "move" && now - this.anim.start < this.anim.ms) {
        this._finish();
        this.onChange(this.state);
        return;
      }
      this.moved = true;
      const before = this.step.page;
      const wasOverview = this.overview;
      this.overview = false;
      const next = this.stepIndex + delta;
      this.arrivedForward = delta > 0;
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
      this.lastGo = 0;
      this.go(1);
    }

    /** Explore: zoom in or out a step, for children who can't pinch yet. */
    zoomStep(f) {
      if (!this.exploring) return;
      const cam = this.takeCamera();
      const W = this.canvas.clientWidth, H = this.canvas.clientHeight;
      this.play("zoom", f > 1);
      this.flyTo(this.zoomAt(cam, f, W / 2, this.insets.top + (H - this.insets.top - this.insets.bottom) / 2), () => this.onChange(this.state), true);
    }

    /** Explore: fly to the next thing not found yet and make it react. */
    showMe() {
      const next = this.unfoundHotspots()[0];
      if (!next) return;
      const box = this.boxRect(this.step.page, next.h.box);
      this.lastTouch = performance.now();
      this.flyTo(inflate(box, Math.max(box.h, box.w) * 0.6), () => this.react(next.i));
    }

    /** Show a caption for a while (longer for longer text), then clear it. */
    say(text, extra = 0, ask = "") {
      const ms = Math.max(4500, 2500 + (text.length + ask.length) * 65) + extra;
      const note = this.note = { text, ask, until: performance.now() + ms };
      this.onChange(this.state);
      setTimeout(() => { if (this.note === note) { this.note = null; this.onChange(this.state); } }, ms + 50);
    }

    /** A hotspot was tapped: sparkle and show its caption. Story steps only open in order. */
    react(i) {
      const p = this.step.page, h = this.pageData.hotspots[i];
      const now = performance.now();
      if (h.story && !this.isFound(i) && i !== this.nextStory()) {
        const first = this.pageData.hotspots[this.nextStory()];
        this.effects.push({ box: this.boxRect(p, h.box), start: now, locked: true });
        this.play("locked");
        this.say("Not yet! First find " + (first.name || "the next part of the story") + ".");
        return;
      }
      const wasNew = !this.isFound(i);
      this.found.add(p + ":" + i);
      if (h.effect !== "none") this.effects.push({ box: this.boxRect(p, h.box), start: now });
      const storyDone = wasNew && h.story && this.nextStory() < 0;
      if (storyDone) this.party = now;
      this.play(storyDone ? "party" : "find");
      if (h.sound) this.playClip(h.sound);
      if (h.caption) this.say(h.caption, storyDone ? 3000 : 0, h.ask); else { this.note = null; this.onChange(this.state); }
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
      this.shown = { ...this.shown, cam, dim: 0, vig: 0, spot: 0 };
      this.lastTouch = performance.now();
      return cam;
    }

    /** soft: let a drag pull a little way past the edge, so the child feels where the scene ends. */
    setCam(cam, soft = false) {
      const c = this.clampCam(cam);
      if (soft && !this.reduced && Math.abs(c.w - cam.w) < 1e-6) {
        const give = (want, got, size) => got + clamp((want - got) * 0.35, -size * 0.12, size * 0.12);
        c.x = give(cam.x, c.x, c.w);
        c.y = give(cam.y, c.y, c.h);
      }
      this.shown = { ...this.shown, cam: c };
      this.lastTouch = performance.now();
    }

    /** After a drag past the edge, ease back inside. Returns true if it had to. */
    settle() {
      const cam = this.shown.cam, c = this.clampCam(cam);
      if (Math.abs(c.x - cam.x) < 1e-5 && Math.abs(c.y - cam.y) < 1e-5) return false;
      this.flyTo(c, null, true);
      return true;
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

    /** Which thing to find is under a touch, or -1. Small things get a touch area of at least minTargetCm. */
    hotspotAt(sx, sy) {
      const v = this.view;
      if (!this.exploring || !v) return -1;
      const p = this.step.page, min = this.settings.minTargetCm * CM, extras = this.extrasOpen();
      let inside = -1, insideArea = Infinity, near = -1, nearDist = Infinity;
      this.pageData.hotspots.forEach((h, i) => {
        if (!h.story && !extras) return;
        const b = this.boxRect(p, h.box);
        const r = rect(b.x * v.sc + v.ox, b.y * v.sc + v.oy, b.w * v.sc, b.h * v.sc);
        const cx = r.x + r.w / 2, cy = r.y + r.h / 2;
        if (contains(r, sx, sy)) {
          if (r.w * r.h < insideArea) { inside = i; insideArea = r.w * r.h; }   // the smallest box under the finger
          return;
        }
        const w = Math.max(r.w, min), hgt = Math.max(r.h, min);
        if (Math.abs(sx - cx) <= w / 2 && Math.abs(sy - cy) <= hgt / 2) {
          const d = Math.hypot(sx - cx, sy - cy);
          if (d < nearDist) { near = i; nearDist = d; }                         // otherwise the nearest small one
        }
      });
      if (inside >= 0) return inside;
      // The story step the child is being guided to wins over a nearby extra when the touch is within its area.
      const next = this.nextStory();
      if (next >= 0 && near >= 0 && near !== next) {
        const nb = this.boxRect(p, this.pageData.hotspots[next].box);
        const cx = (nb.x + nb.w / 2) * v.sc + v.ox, cy = (nb.y + nb.h / 2) * v.sc + v.oy;
        if (Math.abs(sx - cx) <= Math.max(nb.w * v.sc, min) / 2 && Math.abs(sy - cy) <= Math.max(nb.h * v.sc, min) / 2) return next;
      }
      return near;
    }

    tapAt(sx, sy) {
      if (!this.exploring) return;
      const hit = this.hotspotAt(sx, sy);
      this.lastTouch = performance.now();
      if (hit >= 0) this.react(hit); else this.play("tap");
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
      if (s.drift === "off" || this.overview || !stop || stop.to || this.reduced) return this.shown.cam;
      const p = EASINGS.easeInOutSine(Math.min(1, (now - this.restSince) / (s.driftSeconds * 1000)));
      if (s.drift === "zoomIn") return scaleAround(this.shown.cam, 1 - s.driftAmount * p);
      return scaleAround(this.shown.cam, 1, this.shown.cam.w * s.driftAmount * p);
    }

    _frame(now) {
      if (!this.running) return;
      const dt = Math.min(50, now - (this._last || now));
      this._last = now;
      for (const side of ["top", "bottom"]) {
        const gap = this.insetGoal[side] - this.insets[side];
        this.insets[side] = Math.abs(gap) < 0.5 ? this.insetGoal[side] : this.insets[side] + gap * Math.min(1, dt / 110);
      }
      const a = this.anim;
      if (a) {
        const raw = a.ms === 0 ? 1 : clamp((now - a.start) / a.ms, 0, 1);
        if (a.fade) {
          // Reduced motion: no travelling. Dip to dark, switch, and come back.
          const side = raw < 0.5 ? a.from : a.to;
          this.shown = { cam: side.cam, focus: side.focus, dim: side.dim, vig: side.vig, spot: side.spot, fade: 1 - Math.abs(1 - raw * 2) };
        } else {
          const e = (EASINGS[a.easing] || EASINGS.easeInOutCubic)(raw);
          let cam = mixRect(a.from.cam, a.to.cam, e, a.smooth);
          if (a.arcK) {
            // Swell the camera mid-move so both ends fit, then settle in.
            const u = inflate(union(a.from.cam, a.to.cam), 0.05);
            const mid = mixRect(a.from.cam, a.to.cam, 0.5, a.smooth);
            const need = Math.max(u.w / mid.w, u.h / mid.h) - 1;
            cam = scaleAround(cam, 1 + Math.max(0, need) * a.arcK * Math.sin(Math.PI * clamp(e, 0, 1)));
          }
          this.shown = {
            cam,
            focus: mixRect(a.from.focus, a.to.focus, Math.min(1, e), a.smooth),
            dim: lerp(a.from.dim, a.to.dim, raw),
            vig: lerp(a.from.vig, a.to.vig, raw),
            spot: lerp(a.from.spot || 0, a.to.spot || 0, raw),
          };
        }
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
      } else if (this.book && !this.finished && !this.exploring && this.canvas.clientWidth > 0) {
        // Moving on without a tap: a stop marked to go on by itself (only when we came to it going forwards,
        // so going back never gets pushed forward again), or watch mode.
        const stop = this.stop;
        if (this.clip && !this.clip.paused && !this.clip.ended) this.restSince = now;   // let narration finish first
        const rested = now - this.restSince;
        const own = stop && stop.auto && this.arrivedForward && !this.overview;
        const hold = stop && stop.holdSeconds ? +stop.holdSeconds : 0;
        if (own ? rested > (hold || 1.5) * 1000 : this.settings.autoPlay && rested > (hold || this.settings.autoSeconds) * 1000) {
          this.lastGo = 0;
          this.next();
        }
      }
      this.updateWeather(now);
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
      let ox = W / 2 - (cam.x + cam.w / 2) * sc;
      let oy = top + viewH / 2 - (cam.y + cam.h / 2) * sc;
      if (this.shake) {
        // A short, fading shake of the whole view.
        const t = now - this.shake.start;
        if (t > 620) this.shake = null;
        else if (t >= 0) {
          const d = Math.pow(1 - t / 620, 2) * this.shake.k * viewH * 0.022;
          ox += Math.sin(t * 0.085) * d;
          oy += Math.cos(t * 0.107) * d * 0.8;
        }
      }
      const toScreen = r => rect(r.x * sc + ox, r.y * sc + oy, r.w * sc, r.h * sc);
      this.view = { sc, ox, oy, W, H };

      ctx.imageSmoothingQuality = "high";
      this.pageRects.forEach((pr, i) => {
        const r = toScreen(pr);
        if (r.x + r.w < 0 || r.x > W) return;
        ctx.fillStyle = "#fff";
        ctx.fillRect(r.x, r.y, r.w, r.h);
        ctx.drawImage(this.images[i], r.x, r.y, r.w, r.h);
        if (this.book.pages[i].tiles && this.baseUrl) this.drawTiles(i, pr, sc, dpr, toScreen, W, H, now);
        this.drawLayers(i, r, now, W, H);
        this.drawWeather(i, pr, r, now, W, H);
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
      if (this.shown.spot > 0.01) {
        // Spotlight: an oval of light a quarter bigger than the focus box, so what's inside the box is lit
        // (all but its corners). Soft at the rim, dark everywhere else.
        const cx = f.x + f.w / 2, cy = f.y + f.h / 2;
        const rx = Math.max(f.w * 0.625, 1), ry = Math.max(f.h * 0.625, 1);
        ctx.save();
        ctx.translate(cx, cy);
        ctx.scale(rx, ry);
        const g = ctx.createRadialGradient(0, 0, 0.8, 0, 0, 1);
        g.addColorStop(0, "rgba(10,9,16,0)");
        g.addColorStop(1, `rgba(10,9,16,${this.shown.spot})`);
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
      if (this.shown.fade > 0.01) {
        ctx.fillStyle = `rgba(28,27,34,${this.shown.fade})`;
        ctx.fillRect(0, 0, W, H);
      }
      if (this.flash) {
        // Lightning: one soft flash and a fainter echo. Kept gentle and short on purpose.
        const t = now - this.flash;
        if (t > 800) this.flash = 0;
        else if (t >= 0) {
          const a = 0.5 * Math.exp(-t / 90) + (t > 170 ? 0.28 * Math.exp(-(t - 170) / 150) : 0);
          ctx.fillStyle = `rgba(255,255,238,${Math.min(0.6, a)})`;
          ctx.fillRect(0, 0, W, H);
        }
      }
      this.drawMap(now, W, top);
      if (!this.reduced) this.drawParty(now, W, H);
      this.drawRipples(now);
    }

    // ------------------------------------------------------------ moving effects, layers, sound clips

    /** Is this effect running at the step we're on? */
    effectOn(i, e) {
      const step = this.step;
      if (!step || step.page !== i || this.reduced) return 0;
      const at = step.stop;                                   // -1 is the whole page, -2 is exploring
      if (e.from > 1 && at >= -1 && at < e.from - 1) return 0;
      if (e.to >= 1 && (at === -2 || at > e.to - 1)) return 0;
      return 1;
    }

    /** Effects fade in and out over about half a second; layers ease towards where they should be. */
    updateWeather(now) {
      if (!this.book) return;
      const dt = Math.min(50, now - (this._weatherAt || now));
      this._weatherAt = now;
      let loud = 0;
      const showing = this.canvas.clientWidth > 0;
      if (!showing) this.stopClip();
      this.book.pages.forEach((page, i) => {
        (page.effects || []).forEach((e, k) => {
          if (!FALLING.has(e.type)) return;
          const key = i + ":" + k, want = showing ? this.effectOn(i, e) : 0, have = this.fx.get(key) || 0;
          const level = Math.abs(want - have) < 0.01 ? want : have + (want - have) * Math.min(1, dt / 260);
          this.fx.set(key, level);
          if (e.type === "rain") loud = Math.max(loud, level * (0.4 + 0.6 * (e.amount === undefined ? 0.6 : e.amount)));
        });
        (page.layers || []).forEach((l, k) => {
          const key = i + ":" + k, st = this.layerState.get(key) || { vis: -1, t: -1 };
          const step = this.step, here = step && step.page === i, at = here ? step.stop : 0;
          // Before its page is reached a layer waits at its start; once the page is behind us it stays at its end.
          let show, moved;
          if (!step || step.page < i) { show = !(l.from > 1); moved = 0; }
          else if (step.page > i) { show = !(l.to >= 1); moved = 1; }
          else {
            show = !(l.from > 1 && at >= -1 && at < l.from - 1) && !(l.to >= 1 && (at === -2 || at > l.to - 1));
            moved = l.moveTo && (at === -2 || at >= (l.moveAt || 1) - 1) ? 1 : 0;
          }
          const first = st.vis < 0, snap = first || this.reduced || !here;
          st.vis = snap ? +show : clamp(st.vis + (show ? 1 : -1) * dt / 240, 0, 1);
          st.t = snap ? moved : clamp(st.t + (moved ? 1 : -1) * dt / ((l.moveSeconds || 1.2) * 1000), 0, 1);
          this.layerState.set(key, st);
        });
      });
      if (!this.settings.sound) loud = 0;
      if (Math.abs(loud - (this._rainLoud || 0)) > 0.01 || (loud === 0 && this._rainLoud)) { this._rainLoud = loud; this.sounds.rain(loud); }
    }

    /** Arriving at a stop: its sound clip, and any lightning or shake set for it. */
    _arrive() {
      const step = this.step, stop = this.stop;
      if (!step || this.overview || !stop) return;
      if (stop.sound) this.playClip(stop.sound);
      for (const e of this.book.pages[step.page].effects || []) {
        if ((e.stop || 1) - 1 !== step.stop) continue;
        if (e.type === "lightning") {
          if (!this.reduced) this.flash = performance.now() + 700;
          this.play("thunder", 0.85);
        } else if (e.type === "shake") {
          // Silent on purpose: a stop that wants a sound with its shake sets its own sound file.
          if (!this.reduced) this.shake = { start: performance.now() + 120, k: e.strength === undefined ? 0.6 : e.strength };
        }
      }
    }

    /** A file from the book's folder (a layer picture). Loads the first time it's asked for. */
    asset(name) {
      if (!this.assetCache.has(name)) {
        this.assetCache.set(name, null);
        const src = this.assetUrls[name] || (this.baseUrl ? this.baseUrl + name : "");
        if (src) loadImage(src).then(img => this.assetCache.set(name, img)).catch(() => { /* missing file: the layer just doesn't show */ });
      }
      return this.assetCache.get(name);
    }

    /** Play a sound file from the book's folder: narration or a sound effect. One at a time. */
    playClip(name) {
      this.stopClip();
      if (!this.settings.sound || !name) return;
      const src = this.assetUrls[name] || (this.baseUrl ? this.baseUrl + name : "");
      if (!src || typeof Audio === "undefined") return;
      try {
        const a = new Audio(src);
        this.clip = a;
        const p = a.play();
        if (p && p.catch) p.catch(() => { if (this.clip === a) this.clip = null; });
      } catch (_) { this.clip = null; }
    }

    stopClip() {
      if (this.clip) { try { this.clip.pause(); } catch (_) { /* already gone */ } }
      this.clip = null;
    }

    /** Pictures placed on top of the page: shown between stops, moved when their stop arrives. */
    drawLayers(i, r, now, W, H) {
      const list = this.book.pages[i].layers;
      if (!list || !list.length || r.x > W || r.y > H || r.x + r.w < 0 || r.y + r.h < 0) return;
      const ctx = this.ctx;
      list.forEach((l, k) => {
        const st = this.layerState.get(i + ":" + k), img = this.asset(l.image);
        if (!st || !img || st.vis < 0.01) return;
        const e = EASINGS.easeInOutSine(clamp(st.t, 0, 1)), b = l.moveTo ? mixRect(l.box, l.moveTo, e, false) : l.box;
        let x = r.x + b.x * r.w, y = r.y + b.y * r.h;
        const w = b.w * r.w, h = b.h * r.h;
        if (l.moveTo && l.path === "hop") y -= Math.sin(Math.PI * e) * Math.max(h * 0.7, Math.abs(l.moveTo.x - l.box.x) * r.w * 0.3);
        ctx.save();
        ctx.globalAlpha = st.vis;
        const still = this.reduced || (st.t > 0.01 && st.t < 0.99);
        if (!still && l.idle === "bob") y += Math.sin(now / 520 + k * 2) * h * 0.035;
        if (!still && l.idle === "sway") {
          ctx.translate(x + w / 2, y + h);
          ctx.rotate(Math.sin(now / 700 + k * 2) * 0.05);
          ctx.translate(-(x + w / 2), -(y + h));
        }
        ctx.drawImage(img, x, y, w, h);
        ctx.restore();
      });
    }

    /**
     * Rain, snow, leaves and sparkles, drawn in the page's own space so they stay put as the camera
     * moves. Every piece's place comes from the clock and its number, so nothing is kept between frames.
     */
    drawWeather(i, pr, r, now, W, H) {
      const list = this.book.pages[i].effects;
      if (!list || !list.length || r.x > W || r.y > H || r.x + r.w < 0 || r.y + r.h < 0) return;
      const ctx = this.ctx, t = now / 1000, aspect = pr.w / pr.h;
      const rnd = n => { const s = Math.sin(n * 127.1 + 311.7) * 43758.5453; return s - Math.floor(s); };
      list.forEach((e, k) => {
        const level = this.fx.get(i + ":" + k) || 0;
        if (level < 0.01 || !FALLING.has(e.type)) return;
        const amount = e.amount === undefined ? 0.6 : e.amount, many = amount * aspect / 1.6;
        ctx.save();
        ctx.beginPath();
        ctx.rect(r.x, r.y, r.w, r.h);
        for (const b of e.dry || []) ctx.rect(r.x + b.x * r.w, r.y + b.y * r.h, b.w * r.w, b.h * r.h);
        ctx.clip("evenodd");
        ctx.lineCap = "round";
        if (e.type === "rain") {
          // Three depths: far rain is thin, slow and faint; near rain is thick, fast and bright.
          const slant = 0.2, perLayer = Math.round(110 * many);
          for (let layer = 0; layer < 3; layer++) {
            const depth = 0.5 + layer * 0.25, len = 0.035 + 0.045 * depth;
            ctx.beginPath();
            for (let n = 0; n < perLayer; n++) {
              const id = layer * 1000 + n;
              const fall = t * (1.1 + 0.5 * rnd(id + 0.5)) * depth * 1.25 + rnd(id) * 9;
              const y = (fall % 1.3) - 0.15;
              const x = rnd(id * 1.37 + Math.floor(fall / 1.3) * 7.9) * (aspect + slant) - slant * y;
              const sx = r.x + x * r.h, sy = r.y + y * r.h;
              ctx.moveTo(sx, sy);
              ctx.lineTo(sx + slant * len * r.h, sy - len * r.h);
            }
            ctx.strokeStyle = `rgba(236,244,255,${(0.2 + 0.4 * depth) * level})`;
            ctx.lineWidth = Math.max(0.8, r.h * 0.0024 * depth);
            ctx.stroke();
          }
          if (e.ground) {
            // Splash rings where drops land, bigger towards the bottom of the picture (nearer to us).
            const rings = Math.round(46 * many);
            ctx.lineWidth = Math.max(0.8, r.h * 0.002);
            for (let n = 0; n < rings; n++) {
              const beat = t / (0.7 + 0.6 * rnd(n + 40)) + rnd(n + 80) * 5, c = Math.floor(beat), u = beat - c;
              const gy = rnd(n * 3.1 + c * 2.9), y = e.ground + gy * (1 - e.ground), x = rnd(n * 7.3 + c * 1.7) * aspect;
              const size = (0.004 + 0.022 * u) * (0.45 + gy);
              ctx.beginPath();
              ctx.ellipse(r.x + x * r.h, r.y + y * r.h, size * r.h, size * r.h * 0.36, 0, 0, Math.PI * 2);
              ctx.strokeStyle = `rgba(240,247,255,${(1 - u) * 0.55 * level})`;
              ctx.stroke();
            }
          }
        } else if (e.type === "snow") {
          // Slow flakes that drift from side to side as they fall.
          const flakes = Math.round(170 * many);
          for (let n = 0; n < flakes; n++) {
            const depth = 0.4 + 0.6 * rnd(n + 0.3);
            const fall = t * (0.06 + 0.07 * rnd(n + 0.5)) * (0.6 + depth) + rnd(n) * 9, y = (fall % 1.2) - 0.1;
            const x = rnd(n * 1.37 + Math.floor(fall / 1.2) * 7.9) * aspect + 0.03 * depth * Math.sin(t * (0.5 + rnd(n + 0.7)) + n);
            ctx.beginPath();
            ctx.arc(r.x + x * r.h, r.y + y * r.h, Math.max(0.8, r.h * (0.0022 + 0.0042 * depth)), 0, Math.PI * 2);
            ctx.fillStyle = `rgba(255,255,255,${(0.4 + 0.55 * depth) * level})`;
            ctx.fill();
          }
        } else if (e.type === "leaves") {
          // A few leaves that tumble and flutter down.
          const tints = ["#d9822b", "#e3b23c", "#b5532a", "#c96a2b", "#93a13d"], leaves = Math.round(28 * many);
          for (let n = 0; n < leaves; n++) {
            const fall = t * (0.07 + 0.08 * rnd(n + 0.5)) + rnd(n) * 9, y = (fall % 1.2) - 0.1;
            const x = rnd(n * 1.37 + Math.floor(fall / 1.2) * 7.9) * aspect + 0.07 * Math.sin(t * (0.7 + rnd(n + 0.7)) + n);
            const size = r.h * (0.011 + 0.012 * rnd(n + 0.9));
            ctx.save();
            ctx.translate(r.x + x * r.h, r.y + y * r.h);
            ctx.rotate(t * (0.8 + rnd(n + 0.2)) + n);
            ctx.scale(1, 0.45 + 0.55 * Math.abs(Math.sin(t * 1.7 + n)));
            ctx.beginPath();
            ctx.moveTo(-size, 0);
            ctx.quadraticCurveTo(0, -size * 0.75, size, 0);
            ctx.quadraticCurveTo(0, size * 0.75, -size, 0);
            ctx.globalAlpha = 0.92 * level;
            ctx.fillStyle = tints[n % tints.length];
            ctx.fill();
            ctx.restore();
          }
        } else if (e.type === "sparkles") {
          // Little stars that glint and fade, each time somewhere new.
          const stars = Math.round(38 * many);
          for (let n = 0; n < stars; n++) {
            const beat = t / (1.5 + 2 * rnd(n + 40)) + rnd(n + 80) * 5, c = Math.floor(beat), u = beat - c;
            const glow = Math.pow(Math.sin(Math.PI * u), 2), x = rnd(n * 7.3 + c * 1.7) * aspect, y = rnd(n * 3.1 + c * 2.9);
            const size = r.h * (0.007 + 0.013 * rnd(n + 9)) * (0.5 + 0.5 * glow), cx = r.x + x * r.h, cy = r.y + y * r.h;
            ctx.beginPath();
            for (let a = 0; a < 8; a++) {
              const rad = a % 2 ? size * 0.28 : size, ang = a * Math.PI / 4;
              ctx[a ? "lineTo" : "moveTo"](cx + Math.cos(ang) * rad, cy + Math.sin(ang) * rad);
            }
            ctx.closePath();
            ctx.fillStyle = `rgba(255,251,224,${glow * 0.95 * level})`;
            ctx.fill();
          }
        }
        ctx.restore();
      });
    }

    /** The ring that answers every touch. */
    drawRipples(now) {
      const ctx = this.ctx;
      this.ripples = this.ripples.filter(r => now - r.start < 450);
      for (const r of this.ripples) {
        const t = (now - r.start) / 450;
        ctx.save();
        ctx.globalAlpha = (1 - t) * 0.85;
        ctx.strokeStyle = "#fff";
        ctx.lineWidth = 5 * (1 - t) + 1.5;
        ctx.shadowColor = "rgba(0,0,0,.45)";
        ctx.shadowBlur = 6;
        ctx.beginPath();
        ctx.arc(r.x, r.y, 10 + EASINGS.easeOutCubic(t) * 34, 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
      }
    }

    // ------------------------------------------------ big scenes: tiles

    /** A tile image if it has loaded. Starts loading it otherwise (a few at a time). */
    tile(page, z, x, y, now) {
      const key = page + "/" + z + "/" + x + "/" + y;
      let t = this.tiles.get(key);
      if (!t) {
        if (this.tileLoads >= 6) return null;
        const info = this.book.pages[page].tiles;
        t = { img: new Image(), ready: false, used: now };
        this.tileLoads++;
        t.img.onload = () => { t.ready = true; this.tileLoads--; };
        t.img.onerror = () => { this.tileLoads--; };
        t.img.src = this.baseUrl + info.path.replace("{z}", z).replace("{x}", x).replace("{y}", y);
        this.tiles.set(key, t);
        if (this.tiles.size > 160) {
          // Forget the tiles looked at longest ago.
          const old = [...this.tiles.entries()].filter(e => e[1].ready).sort((a, b) => a[1].used - b[1].used);
          for (const [k] of old.slice(0, 40)) this.tiles.delete(k);
        }
      }
      t.used = now;
      return t.ready ? t.img : null;
    }

    /** Draws sharp tiles over the overview image, only for the part of the page on screen. */
    drawTiles(i, pr, sc, dpr, toScreen, W, H, now) {
      const info = this.book.pages[i].tiles, ctx = this.ctx;
      const perSource = (sc * dpr * pr.h) / info.height;          // screen pixels per full-size pixel
      const z = perSource >= 1 ? 0 : Math.floor(Math.log2(1 / perSource));
      if (z >= info.levels) return;                                 // zoomed out: the overview is enough
      const f = Math.pow(2, z);
      const lw = Math.ceil(info.width / f), lh = Math.ceil(info.height / f);
      const tw = (info.size * f / info.width) * pr.w, th = (info.size * f / info.height) * pr.h;  // tile size in world units
      const v = this.view;
      const x0 = Math.max(0, Math.floor(((0 - v.ox) / sc - pr.x) / tw)), x1 = Math.min(Math.ceil(lw / info.size) - 1, Math.floor(((W - v.ox) / sc - pr.x) / tw));
      const y0 = Math.max(0, Math.floor(((0 - v.oy) / sc - pr.y) / th)), y1 = Math.min(Math.ceil(lh / info.size) - 1, Math.floor(((H - v.oy) / sc - pr.y) / th));
      for (let ty = y0; ty <= y1; ty++) {
        for (let tx = x0; tx <= x1; tx++) {
          const pw = Math.min(info.size, lw - tx * info.size), ph = Math.min(info.size, lh - ty * info.size);
          const r = toScreen(rect(pr.x + tx * tw, pr.y + ty * th, tw * pw / info.size, th * ph / info.size));
          const img = this.tile(i, z, tx, ty, now);
          if (img) { ctx.drawImage(img, r.x - 0.4, r.y - 0.4, r.w + 0.8, r.h + 0.8); continue; }
          if (z + 1 < info.levels) {
            // Not here yet: use the matching quarter of the next level down if we have it.
            const parent = this.tile(i, z + 1, tx >> 1, ty >> 1, now);
            if (parent) {
              const half = info.size / 2;
              ctx.drawImage(parent, (tx & 1) * half, (ty & 1) * half, pw / 2, ph / 2, r.x - 0.4, r.y - 0.4, r.w + 0.8, r.h + 0.8);
            }
          }
        }
      }
    }

    // ------------------------------------------------ explore: corner map and celebration

    /** Small map of the whole page with a frame showing where the child is looking. */
    drawMap(now, W, top) {
      this.mapRect = null;
      if (!this.exploring || !this.pageData.guidance.map) return;
      const ctx = this.ctx, page = this.step.page, pr = this.pageRects[page];
      const mw = Math.min(210, W * 0.3), mh = mw * pr.h / pr.w;
      const mx = W - mw - 12, my = top + 6;
      this.mapRect = { x: mx, y: my, w: mw, h: mh };
      ctx.save();
      ctx.shadowColor = "rgba(0,0,0,.45)";
      ctx.shadowBlur = 10;
      ctx.fillStyle = "#fff";
      ctx.fillRect(mx - 3, my - 3, mw + 6, mh + 6);
      ctx.shadowBlur = 0;
      ctx.drawImage(this.images[page], mx, my, mw, mh);
      const cam = this.visibleCam();
      const k = mw / pr.w;
      const vx = clamp(mx + (cam.x - pr.x) * k, mx, mx + mw), vy = clamp(my + (cam.y - pr.y) * k, my, my + mh);
      const vx1 = clamp(mx + (cam.x + cam.w - pr.x) * k, mx, mx + mw), vy1 = clamp(my + (cam.y + cam.h - pr.y) * k, my, my + mh);
      ctx.fillStyle = "rgba(0,0,0,.35)";
      ctx.beginPath();
      ctx.rect(mx, my, mw, mh);
      ctx.rect(vx, vy, vx1 - vx, vy1 - vy);
      ctx.fill("evenodd");
      ctx.strokeStyle = "#fff";
      ctx.lineWidth = 2.5;
      ctx.strokeRect(vx, vy, vx1 - vx, vy1 - vy);
      // The next story step blinks on the map.
      const next = this.nextStory();
      if (next >= 0) {
        const b = this.boxRect(page, this.pageData.hotspots[next].box);
        const pulse = 0.5 + 0.5 * Math.sin(now / 260);
        ctx.fillStyle = `rgba(255,236,120,${0.6 + 0.4 * pulse})`;
        ctx.beginPath();
        ctx.arc(mx + (b.x + b.w / 2 - pr.x) * k, my + (b.y + b.h / 2 - pr.y) * k, 4 + 2 * pulse, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }

    /** Centre the view on a point of the corner map. */
    mapGoTo(sx, sy) {
      const m = this.mapRect, pr = this.pageRects[this.step.page];
      const cam = this.takeCamera();
      const wx = pr.x + ((sx - m.x) / m.w) * pr.w, wy = pr.y + ((sy - m.y) / m.h) * pr.h;
      this.setCam(rect(wx - cam.w / 2, wy - cam.h / 2, cam.w, cam.h));
    }

    /** Stars raining down when the story is finished. */
    drawParty(now, W, H) {
      if (!this.party || now - this.party > 4200) return;
      const t = (now - this.party) / 4200, ctx = this.ctx;
      const colors = ["#ffd23f", "#ff6b6b", "#74c0fc", "#8ce99a", "#fff6c2", "#b197fc"];
      ctx.save();
      ctx.globalAlpha = t > 0.8 ? (1 - t) / 0.2 : 1;
      for (let k = 0; k < 70; k++) {
        const a = Math.sin(k * 91.7) * 43758.5, b = Math.sin(k * 17.3) * 12543.1;
        const fx = a - Math.floor(a), fy = b - Math.floor(b);
        const x = fx * W + Math.sin(t * 6 + k) * 24;
        const y = -40 + (t * (1.2 + fy * 0.9)) * (H + 80) - fy * H * 0.5;
        star(ctx, x, y, 8 + fy * 12, colors[k % colors.length]);
      }
      ctx.restore();
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
        if (e.locked) {
          // "Not yet": a grey ring that shakes its head.
          ctx.save();
          ctx.globalAlpha = 1 - t;
          ctx.strokeStyle = "#e9eef5";
          ctx.lineWidth = 6;
          ctx.setLineDash([14, 10]);
          ctx.beginPath();
          ctx.ellipse(cx + Math.sin(t * 28) * 10 * (1 - t), cy, r.w / 2 + 10, r.h / 2 + 10, 0, 0, Math.PI * 2);
          ctx.stroke();
          ctx.restore();
          continue;
        }
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
      if (!this.exploring) return;
      const ctx = this.ctx;
      const pulse = 0.5 + 0.5 * Math.sin(now / 260);
      // The next story step always carries a soft beacon, so the thread of the story is never lost.
      const story = this.nextStory();
      if (story >= 0) {
        const b = toScreen(this.boxRect(this.step.page, this.pageData.hotspots[story].box));
        const bx = b.x + b.w / 2, by = b.y + b.h / 2;
        if (bx > -b.w && bx < W + b.w && by > -b.h && by < H + b.h) {
          const grow = this.reduced ? 0.25 : (now / 1400) % 1;
          ctx.save();
          ctx.strokeStyle = `rgba(255,255,255,${0.75 * (1 - grow)})`;
          ctx.lineWidth = 5;
          ctx.beginPath();
          ctx.ellipse(bx, by, b.w / 2 + 6 + grow * 40, b.h / 2 + 6 + grow * 40, 0, 0, Math.PI * 2);
          ctx.stroke();
          ctx.strokeStyle = `rgba(255,236,120,${0.55 + 0.35 * pulse})`;
          ctx.lineWidth = 4;
          ctx.beginPath();
          ctx.ellipse(bx, by, b.w / 2 + 6, b.h / 2 + 6, 0, 0, Math.PI * 2);
          ctx.stroke();
          ctx.restore();
        }
      }
      if (this.anim || this.fling) return;
      if (this.pageData.guidance.hints === false) return;
      if (now - this.lastTouch < this.settings.hintSeconds * 1000) return;
      const next = this.unfoundHotspots()[0];
      if (!next) return;
      const r = toScreen(this.boxRect(this.step.page, next.h.box));
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
      reader.sounds.wake();
      const p = local(e);
      pointers.set(e.pointerId, p);
      if (pointers.size === 1) {
        gesture = { start: p, multi: false, startDist: 0, lastDist: 0, explore: reader.exploring, samples: [] };
        const m = reader.mapRect;
        if (gesture.explore && m && contains(m, p.x, p.y)) {
          // Touching the corner map moves the view there.
          gesture.map = true;
          reader.mapGoTo(p.x, p.y);
          return;
        }
        if (gesture.explore) gesture.cam = reader.takeCamera();
      }
      if (pointers.size === 2 && gesture && !gesture.map) {
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
      if (gesture.map) { if (reader.mapRect) reader.mapGoTo(p.x, p.y); return; }
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
        reader.setCam(rect(gesture.cam.x - dx / sc, gesture.cam.y - dy / sc, gesture.cam.w, gesture.cam.h), true);
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
      if (g.map) return;
      const dx = p.x - g.start.x, dy = p.y - g.start.y;
      const isTap = !g.multi && Math.hypot(dx, dy) < 10;

      if (g.explore) {
        if (g.multi) { reader.setCam(reader.shown.cam); return; }
        if (isTap) {
          const now = performance.now();
          reader.ripple(p.x, p.y);
          if (lastTap && now - lastTap.t < 320 && Math.hypot(p.x - lastTap.x, p.y - lastTap.y) < 30) {
            // A second tap in the same spot straight away: zoom if that's switched on, otherwise ignore it.
            lastTap = null;
            if (reader.settings.doubleTapZoom) reader.doubleTapAt(p.x, p.y);
          } else {
            lastTap = { x: p.x, y: p.y, t: now };
            reader.tapAt(p.x, p.y);
          }
          return;
        }
        // Pulled past the edge: ease back. Otherwise a flick keeps gliding the way it was going.
        if (reader.settle()) return;
        const s = g.samples;
        if (s.length >= 2 && !reader.reduced) {
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
      else if (isTap) { reader.ripple(p.x, p.y); reader.go(g.start.x < box.width * 0.3 ? -1 : 1); }
    }, opt);

    canvas.addEventListener("pointercancel", e => { pointers.delete(e.pointerId); gesture = null; }, opt);
  }

  window.ComicReader = {
    Reader, DEFAULT_SETTINGS, AGE_PRESETS, EASINGS,
    loadSettings, saveSettings, normalizeBook, serializeBook, loadImage, layoutPages,
    rect, inflate, fitToAspect,
  };
})();
