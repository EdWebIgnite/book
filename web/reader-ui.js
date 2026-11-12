/*
 * The reader screen: the picture, its buttons, captions and hints.
 * Shared by the playground and the editor's preview so they always behave the same.
 *
 * Layout follows the research in docs/movement-ideas.md: picture icons instead of words
 * (most of these children can't read yet), controls on the side and in the corners rather
 * than along the bottom edge where wrists rest, and zoom buttons so nobody has to pinch.
 */
"use strict";
(function () {
  const CR = window.ComicReader;

  const svg = (d, extra = "") => `<svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" ${extra}>${d}</svg>`;
  const ICON = {
    back: svg('<path d="M15 5l-7 7 7 7"/>'),
    gear: svg('<circle cx="12" cy="12" r="3.2"/><path d="M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M18.7 5.3l-2.1 2.1M7.4 16.6l-2.1 2.1"/>'),
    plus: svg('<path d="M12 5v14M5 12h14"/>'),
    minus: svg('<path d="M5 12h14"/>'),
    // four corners pointing out: see the whole page
    whole: svg('<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>'),
    // four corners pointing in: back to the close-up
    close: svg('<path d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5"/>'),
    star: svg('<path d="M12 3l2.6 5.6 6.1.7-4.5 4.2 1.2 6-5.4-3-5.4 3 1.2-6L3.3 9.3l6.1-.7z" fill="currentColor" stroke="none"/>'),
    prev: svg('<path d="M15 5l-7 7 7 7"/>', 'style="width:38px;height:38px"'),
    next: svg('<path d="M9 5l7 7-7 7"/>', 'style="width:38px;height:38px"'),
  };

  /**
   * Builds the reader screen inside `root`.
   * opts: settings, onClose, onGear (omit to hide the gear), title, endAgain, endBack.
   */
  function mount(root, opts = {}) {
    root.classList.add("reader");
    root.innerHTML = `
      <canvas></canvas>
      <div class="bar top">
        <button class="round" data-a="back" aria-label="Back">${ICON.back}</button>
        <span class="title"></span>
        ${opts.onGear ? `<button class="round" data-a="gear" aria-label="Settings for grown-ups">${ICON.gear}</button>` : ""}
      </div>
      <div class="side">
        <button class="round" data-a="zin" aria-label="Zoom in">${ICON.plus}</button>
        <button class="round" data-a="zout" aria-label="Zoom out">${ICON.minus}</button>
        <button class="round" data-a="whole" aria-label="Whole page">${ICON.whole}</button>
        <button class="round showme" data-a="showme" aria-label="Show me">${ICON.star}</button>
      </div>
      <div class="hand tap hidden" aria-hidden="true">\u{1F446}</div>
      <div class="hand drag hidden" aria-hidden="true">\u{1F446}</div>
      <div class="words">
        <div class="ask hidden"><span class="who">Ask</span><span class="q"></span></div>
        <div class="caption hidden"></div>
      </div>
      <div class="bar bottom">
        <button class="arrow" data-a="prev" aria-label="Back">${ICON.prev}</button>
        <div class="pageno"></div>
        <button class="arrow" data-a="next" aria-label="Next">${ICON.next}</button>
      </div>
      <div class="gate-tip hidden">Hold for 2 seconds</div>
      <div class="end hidden">
        <div class="box">
          <h2>The End</h2>
          <button class="primary" data-a="again">${opts.endAgain || "Read again"}</button>
          <button data-a="endback">${opts.endBack || "Back to books"}</button>
        </div>
      </div>`;

    const q = sel => root.querySelector(sel);
    const btn = a => root.querySelector(`[data-a="${a}"]`);
    const settings = opts.settings || CR.loadSettings();
    const reader = new CR.Reader(q("canvas"), { settings, onChange: update, insets: { top: 64, bottom: 104 } });
    let wasExploring = false, dragTimer = 0;

    function update(st) {
      root.dataset.cap = settings.captionSize || "medium";
      let progress = "";
      if (st.exploring && st.storyTotal) progress += ` · story ${st.storyFound} of ${st.storyTotal}`;
      if (st.exploring && st.extraTotal) progress += ` · found ${st.extraFound} of ${st.extraTotal}`;
      q(".pageno").textContent = `Page ${st.page + 1} of ${st.pageCount}` + progress;

      // Side buttons: zoom only makes sense while exploring.
      btn("zin").classList.toggle("hidden", !st.exploring);
      btn("zout").classList.toggle("hidden", !st.exploring);
      btn("zin").disabled = !st.canZoomIn;
      btn("zout").disabled = !st.canZoomOut;
      btn("showme").classList.toggle("hidden", !st.canShowMe);
      const closeUp = st.wholePage && !st.exploring;
      btn("whole").innerHTML = closeUp ? ICON.close : ICON.whole;
      btn("whole").setAttribute("aria-label", closeUp ? "Back to the close-up" : "Whole page");
      btn("prev").disabled = !st.canGoBack;

      const cap = q(".caption"), ask = q(".ask");
      if (st.caption) cap.textContent = st.caption;
      cap.classList.toggle("hidden", !st.caption);
      if (st.ask) q(".ask .q").textContent = st.ask;
      ask.classList.toggle("hidden", !st.ask);

      // Picture hints: tap to go on at the very start, drag to look around when exploring begins.
      q(".hand.tap").classList.toggle("hidden", !st.firstStep || st.finished);
      if (st.exploring && !wasExploring) {
        q(".hand.drag").classList.remove("hidden");
        clearTimeout(dragTimer);
        dragTimer = setTimeout(() => q(".hand.drag").classList.add("hidden"), 5000);
      } else if (!st.exploring) q(".hand.drag").classList.add("hidden");
      wasExploring = st.exploring;

      q(".end").classList.toggle("hidden", !st.finished);
      if (opts.onChange) opts.onChange(st);
    }

    // The drag hint goes away as soon as the child touches the picture.
    q("canvas").addEventListener("pointerdown", () => q(".hand.drag").classList.add("hidden"));

    // Buttons shouldn't keep keyboard focus, or Space would press them and advance as well.
    root.querySelectorAll("button").forEach(b => b.addEventListener("mousedown", e => e.preventDefault()));
    const sound = () => reader.sounds.wake();
    const on = (a, fn) => { const b = btn(a); if (b) b.addEventListener("click", () => { sound(); fn(); }); };
    on("back", () => opts.onClose && opts.onClose());
    on("endback", () => opts.onClose && opts.onClose());
    on("again", () => reader.restart());
    on("prev", () => reader.prev());
    on("next", () => reader.next());
    on("whole", () => reader.toggleOverview());
    on("showme", () => reader.showMe());
    on("zin", () => reader.zoomStep(1.7));
    on("zout", () => reader.zoomStep(1 / 1.7));

    // The gear is for grown-ups. With the lock on it needs a two-second hold.
    const gear = btn("gear");
    if (gear) {
      let timer = 0, opened = false;
      const tip = q(".gate-tip");
      gear.addEventListener("pointerdown", () => {
        opened = false;
        if (!settings.gate) return;
        gear.classList.add("holding");
        timer = setTimeout(() => { opened = true; gear.classList.remove("holding"); opts.onGear(); }, 2000);
      });
      const cancel = () => { clearTimeout(timer); gear.classList.remove("holding"); };
      gear.addEventListener("pointerup", cancel);
      gear.addEventListener("pointerleave", cancel);
      gear.addEventListener("click", () => {
        if (!settings.gate) { opts.onGear(); return; }
        if (!opened) { tip.classList.remove("hidden"); setTimeout(() => tip.classList.add("hidden"), 1800); }
      });
    }

    return {
      reader, settings,
      setTitle(text) { q(".title").textContent = text; },
      refresh() { if (reader.book) update(reader.state); },
    };
  }

  window.ComicReaderUI = { mount };
})();
