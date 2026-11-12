/*
 * "Captain Megawatt and the Runaway Robot": the sample comic.
 * Three pages of inked action panels with the lettering in the art. Every stop is
 * one panel, and the book uses the "dim" focus style.
 */
"use strict";
const { at } = require("./kit");
const c = require("./comic");

const W = 2000, H = 3000;
let clipN = 0;

/**
 * A panel with any four-sided shape (slanted edges give the page some energy).
 * pts are page coordinates; draw(w, h) paints in the panel's own space.
 */
function panel(pts, draw) {
  const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
  const x = Math.min(...xs), y = Math.min(...ys), w = Math.max(...xs) - x, h = Math.max(...ys) - y;
  const id = `lp${++clipN}`, poly = pts.map(p => p.join(",")).join(" ");
  const f = v => +v.toFixed(4);
  return {
    svg: `<defs><clipPath id="${id}"><polygon points="${poly}"/></clipPath></defs>
      <g clip-path="url(#${id})"><g transform="translate(${x} ${y})">${draw(w, h)}</g></g>
      <polygon points="${poly}" fill="none" stroke="${c.INK}" stroke-width="14" stroke-linejoin="miter"/>`,
    box: { x: f(x / W), y: f(y / H), w: f(w / W), h: f(h / H) },
  };
}

function compose(panels) {
  return {
    svg: `<rect width="${W}" height="${H}" fill="#fdfaf2"/>` + panels.map(p => p.svg).join(""),
    stops: panels.map(p => ({ box: p.box })),
  };
}

const SKIN = "#e9b990", HAIR = "#4a2f1b";
const HERO = { costume: true, adult: true, skin: SKIN, hairColor: HAIR };
// The Captain in everyday clothes.
const OFFICE = { adult: true, skin: SKIN, hairColor: HAIR, shirt: "#f4f4f4", pants: "#4b5563", tie: true, glasses: true };
const kid = (shirt, pants, skin = "#8d5a3b", hairColor = "#1d1310") => ({ shirt, pants, skin, hairColor, hair: "short" });
const fig = (x, y, s, pose, opts, flip) => at(x, y, s, c.figure(pose, opts), flip);

// ---------------------------------------------------------------- page 1

function page1() {
  return compose([
    // Establishing shot
    panel([[60, 60], [1940, 60], [1940, 880], [60, 960]], (w, h) =>
      c.city(w, h, 640, { seed: 3, behind: `<circle cx="1480" cy="330" r="150" fill="#ffe066" opacity=".9"/>` })
      + fig(430, 850, 0.8, "run", kid("#2a9d8f", "#444b6e", "#f1c7a0", "#c9872f"))
      + c.ball(640, 866, 0.9)
      + fig(900, 846, 0.75, "cheer", kid("#e63946", "#33539e"))
      + fig(1320, 864, 1.1, "stand", OFFICE)
      + c.caption(36, 36, "Sparkton City. Saturday morning.", 50)
      + c.caption(w - 36, 700, "All was quiet. Until...", 50, "end")),

    // The robot arrives
    panel([[60, 996], [980, 957], [980, 2000], [60, 2000]], (w, h) =>
      c.gradient(w, h, "#ff7b39", "#ffd56b")
      + c.halftone(w, h, { color: "#c2261a", opacity: 0.3, from: "top" })
      + c.skyline(w, 900, 14, { color: "#7a2e2e", window: "#ffd56b", minH: 60, maxH: 220 })
      + `<rect y="900" width="${w}" height="${h - 900}" fill="#4a3f4f"/>`
      + c.speedLines(w, h, 460, 520, 26, 5, { color: "#fff", opacity: 0.35, inner: 0.36 })
      + `<g stroke="${c.INK}" stroke-width="8" fill="none" stroke-linecap="round"><path d="M300,1010 l-70,22 l-40,-18 M350,1030 l-20,34 M680,1012 l64,26 l50,-12 M640,1034 l26,30"/></g>`
      + at(500, 1012, 1.12, c.robot({ arms: "stomp", eye: "angry" }))
      + c.dustCloud(200, 1020, 1.0) + c.dustCloud(800, 1024, 1.1)
      + c.sfx(330, 190, "CLANK!", 160, "#ffd23f", -8)
      + c.sfx(730, 250, "CLANK!", 100, "#fff", 9)),

    // Someone in the crowd spots it
    panel([[1016, 955], [1940, 916], [1940, 2000], [1016, 2000]], (w, h) =>
      c.city(w, h, 780, { seed: 21, sky: ["#ffb347", "#ffe9a8"], far: "#d98f5c", near: "#7a3b3b", dots: "#c2261a" })
      + `<path d="M0,820 L${w},790 L${w},1010 L0,1084 Z" fill="#000" opacity=".28"/>`
      + c.ball(770, 1040, 1.3)
      + fig(300, 1120, 2.5, "point", { ...OFFICE, mood: "wow" })
      + c.shout(590, 200, ["A runaway", "robot!"], [400, 440], 58, "#fff")),

    // Suiting up
    panel([[60, 2036], [1940, 2036], [1940, 2940], [60, 2940]], (w, h) =>
      `<rect width="${w}" height="${h}" fill="#ffd23f"/>`
      + c.speedLines(w, h, 940, 470, 40, 9, { color: "#ff8c1a", opacity: 0.9, inner: 0.16 })
      + c.halftone(w, h, { color: "#e63946", opacity: 0.35, from: "bottom" })
      + c.burst(940, 500, 520, 420, 18, 3, { fill: "#fff8d6", width: 10 })
      + c.lightning(250, 600, 1.7, -14) + c.lightning(1650, 640, 1.6, 16) + c.lightning(1420, 240, 0.9, 30, "#ffd23f") + c.lightning(480, 250, 0.8, -30, "#ffd23f")
      + fig(940, 884, 2.2, "stand", { ...HERO, mood: "grin" })
      + c.caption(36, 36, "This looks like a job for...", 50)
      + c.sfx(400, 800, "CAPTAIN", 150, "#fff", -7)
      + c.sfx(1470, 810, "MEGAWATT!", 125, "#e63946", 6)),
  ]);
}

// ---------------------------------------------------------------- page 2

function page2() {
  return compose([
    // The dash
    panel([[60, 60], [1940, 60], [1940, 760], [60, 700]], (w, h) =>
      c.city(w, h, 470, { seed: 8 })
      + c.streaks(0, w, 40, 440, 30, 3, { color: "#fff", opacity: 0.55 })
      + `<path d="M0,410 L1150,400 L1150,520 L0,570 Z" fill="#ffd23f" opacity=".9"/><path d="M0,446 L1150,432 L1150,486 L0,530 Z" fill="${c.SUIT}" opacity=".9"/>`
      + c.streaks(0, 1000, 600, 690, 14, 6, { color: "#fff", opacity: 0.8 })
      + c.dustCloud(330, 650, 0.9) + c.dustCloud(620, 660, 0.7)
      + fig(1220, 650, 1.7, "run", { ...HERO, mood: "grit" })
      + c.sfx(520, 270, "ZOOOM!", 210, "#ffd23f", -6)),

    // The robot grabs, and misses
    panel([[60, 736], [1150, 771], [1150, 1900], [60, 1900]], (w, h) =>
      c.gradient(w, h, "#ff7b39", "#ffd56b")
      + c.halftone(w, h, { color: "#c2261a", opacity: 0.3, from: "top" })
      + c.skyline(w, 1040, 31, { color: "#7a2e2e", window: "#ffd56b", minH: 80, maxH: 260 })
      + `<rect y="1040" width="${w}" height="${h - 1040}" fill="#4a3f4f"/>`
      + at(400, 1180, 1.5, c.robot({ arms: "reach", eye: "angry" }))
      + `<g stroke="#fff" stroke-width="12" fill="none" stroke-linecap="round" opacity=".9"><path d="M700,560 q120,30 200,150"/><path d="M660,640 q110,30 180,130"/></g>`
      + fig(900, 650, 1.2, "leap", { ...HERO, mood: "grin" })
      + c.sfx(800, 930, "SWOOSH!", 120, "#fff", -10)
      + c.robotBubble(330, 150, ["Beep! Catch...", "you..."], [380, 330], 50)
      + c.bubble(860, 250, ["Too slow!"], [960, 400], 54)),

    // The plan
    panel([[1186, 772], [1940, 796], [1940, 1900], [1186, 1900]], (w, h) =>
      `<rect width="${w}" height="${h}" fill="#35b7c9"/>`
      + c.halftone(w, h, { color: "#0d5f73", opacity: 0.35, from: "bottom" })
      + c.speedLines(w, h, 470, 590, 24, 12, { color: "#fff", opacity: 0.45, inner: 0.2 })
      + at(470, 940, 1.15, c.robotBack())
      + c.burst(470, 600, 150, 150, 14, 7, { fill: "none", stroke: "#ffd23f", width: 12 })
      + fig(150, 2060, 3.6, "stand", { ...HERO, mood: "grin" })
      + c.bubble(410, 170, ["Aha! The", "off switch!"], [240, 800], 56)),

    // The leap
    panel([[60, 1936], [1940, 1936], [1940, 2940], [60, 2940]], (w, h) =>
      c.gradient(w, h, "#e63946", "#ff9f43")
      + c.speedLines(w, h, 1560, 420, 44, 15, { color: "#fff", opacity: 0.35, inner: 0.12 })
      + c.halftone(w, h, { color: "#7a1020", opacity: 0.4, from: "bottom" })
      + at(1560, 1300, 1.6, c.robotBack())
      + `<path d="M0,1004 L260,1004 L940,560 L820,470 Z" fill="#ffd23f" opacity=".85"/><path d="M0,1004 L150,1004 L900,540 L850,500 Z" fill="#fff" opacity=".7"/>`
      + fig(780, 820, 2.5, "leap", { ...HERO, mood: "grit" })
      + c.sfx(430, 300, "HYAH!", 260, "#ffd23f", -12)),
  ]);
}

// ---------------------------------------------------------------- page 3

function page3() {
  return compose([
    // The button
    panel([[60, 60], [1940, 60], [1940, 1300], [60, 1380]], (w, h) => {
      let rivets = "";
      for (let y = 90; y < h; y += 190) rivets += `<circle cx="900" cy="${y}" r="20" fill="#e8f1ef" stroke="${c.INK}" stroke-width="7"/><circle cx="1790" cy="${y}" r="20" fill="#e8f1ef" stroke="${c.INK}" stroke-width="7"/>`;
      let stripes = "";
      for (let i = 0; i < 14; i++) {
        const a0 = (i / 14) * Math.PI * 2, a1 = ((i + 0.5) / 14) * Math.PI * 2, R = 330;
        stripes += `<path d="M${1340 + Math.cos(a0) * R},${650 + Math.sin(a0) * R} A${R},${R} 0 0 1 ${1340 + Math.cos(a1) * R},${650 + Math.sin(a1) * R} L1340,650 Z" fill="${c.INK}"/>`;
      }
      return `<rect width="${w}" height="${h}" fill="#e63946"/>`
        + c.speedLines(w, h, 1180, 650, 44, 4, { color: "#ffd23f", opacity: 0.85, inner: 0.14 })
        + `<rect x="840" y="-20" width="1100" height="${h + 40}" fill="#6fa8a0" stroke="${c.INK}" stroke-width="14"/>
           <rect x="840" y="-20" width="1100" height="${h + 40}" fill="#000" opacity=".08"/>${rivets}
           <circle cx="1340" cy="650" r="330" fill="#ffd23f"/>${stripes}<circle cx="1340" cy="650" r="330" fill="none" stroke="${c.INK}" stroke-width="14"/>
           <circle cx="1340" cy="650" r="232" fill="#8f1010" stroke="${c.INK}" stroke-width="14"/>
           <circle cx="1322" cy="650" r="220" fill="#e21d1d" stroke="${c.INK}" stroke-width="12"/>
           <ellipse cx="1400" cy="520" rx="80" ry="34" fill="#fff" opacity=".45" transform="rotate(28 1400 520)"/>
`
        + c.burst(1130, 650, 300, 270, 16, 11, { fill: "#fff", width: 12 })
        + c.burst(1130, 650, 190, 170, 12, 13, { fill: "#ffd23f", width: 8 })
        + `<path d="M-20,900 L760,720" stroke="${c.INK}" stroke-width="212" stroke-linecap="round"/><path d="M-20,900 L760,720" stroke="${c.SUIT}" stroke-width="188" stroke-linecap="round"/>
           <path d="M620,752 L800,712" stroke="#ffd23f" stroke-width="188"/><path d="M620,650 L640,846" stroke="${c.INK}" stroke-width="12"/>
           <ellipse cx="960" cy="668" rx="170" ry="140" fill="#ffd23f" stroke="${c.INK}" stroke-width="14" transform="rotate(-12 960 668)"/>
           <g stroke="${c.INK}" stroke-width="10" stroke-linecap="round" fill="none"><path d="M1040,580 q26,40 12,84"/><path d="M1076,640 q20,34 4,74"/><path d="M990,548 q30,30 30,60"/></g>`
        + c.sfx(500, 330, "CLICK!", 270, "#fff", -8);
    }),

    // Powering down
    panel([[60, 1416], [900, 1380], [900, 2100], [60, 2100]], (w, h) =>
      c.gradient(w, h, "#1d2a55", "#324a8a")
      + c.halftone(w, h, { color: "#0b1230", opacity: 0.45, from: "bottom" })
      + at(420, 2090, 3.2, c.robot({ arms: "down", eye: "dim" }))
      + c.sfx(420, 640, "bzzz... zz...", 100, "#9bd1ff", -3)),

    // Sitting down
    panel([[936, 1379], [1940, 1336], [1940, 2100], [936, 2100]], (w, h) =>
      c.city(w, h, 560, { seed: 40, sky: ["#ffb347", "#ffe9a8"], far: "#d98f5c", near: "#7a3b3b", dots: "#c2261a" })
      + `<g stroke="${c.INK}" stroke-width="10" stroke-linecap="round"><path d="M180,560 l-60,-70 M250,520 l-30,-90 M780,520 l30,-90 M850,560 l60,-70"/></g>`
      + at(500, 730, 0.92, c.robot({ arms: "down", eye: "sleep", sit: true }))
      + c.dustCloud(150, 740, 1.1) + c.dustCloud(850, 744, 1.2)
      + c.sfx(700, 200, "THUD!", 160, "#ffd23f", 6)),

    // The end
    panel([[60, 2136], [1940, 2136], [1940, 2940], [60, 2940]], (w, h) =>
      c.city(w, h, 580, { seed: 3, sky: ["#ff8f5a", "#ffe08a"], far: "#c9788a", near: "#5a3a6a", dots: "#b0305a" })
      + at(1440, 790, 1.02, c.robot({ arms: "down", eye: "sleep", sit: true }))
      + `<g font-family="Bangers" fill="#fff" stroke="${c.INK}" stroke-width="5"><text x="1600" y="250" font-size="64">Z</text><text x="1660" y="190" font-size="86">Z</text><text x="1740" y="120" font-size="110">Z</text></g>`
      + fig(250, 790, 0.9, "cheer", kid("#2a9d8f", "#444b6e", "#f1c7a0", "#c9872f"))
      + fig(1060, 794, 0.85, "cheer", kid("#e63946", "#33539e"), true)
      + fig(680, 794, 1.75, "stand", { ...HERO, mood: "grin" })
      + c.bubble(1020, 150, ["Even robots", "need a nap."], [790, 300], 52)
      + c.caption(w - 36, h - 130, "The end", 60, "end")),
  ]);
}

function cover() {
  const CW = 1600, CH = 2400;
  let s = `<rect width="${CW}" height="${CH}" fill="#ffd23f"/>`
    + c.speedLines(CW, CH, 800, 1300, 44, 2, { color: "#ff8c1a", opacity: 0.9, inner: 0.14 })
    + c.halftone(CW, CH, { color: "#e63946", opacity: 0.35, from: "bottom" })
    + c.skyline(CW, 2400, 5, { color: "#34507a", window: "#ffe27a", minH: 160, maxH: 520 })
    + at(1120, 2420, 1.9, c.robot({ arms: "reach", eye: "angry" }), true)
    + c.burst(640, 1500, 520, 520, 18, 4, { fill: "#fff8d6", width: 12 })
    + at(560, 1900, 3.3, c.figure("leap", { ...HERO, mood: "grin" }))
    + c.sfx(800, 290, "CAPTAIN", 220, "#fff", -5)
    + c.sfx(805, 570, "MEGAWATT", 235, "#e63946", -5)
    + `<g transform="rotate(-5 800 720)"><rect x="330" y="668" width="940" height="100" fill="${c.INK}"/>
        <text x="800" y="742" text-anchor="middle" font-family="Bangers" font-size="70" fill="#ffd23f" letter-spacing="3">AND THE RUNAWAY ROBOT</text></g>`
    + c.burst(1390, 990, 150, 130, 12, 8, { fill: "#e63946", width: 10 })
    + `<text x="1390" y="1016" text-anchor="middle" font-family="Bangers" font-size="84" fill="#fff" transform="rotate(10 1390 990)">No.1</text>`
    + `<rect x="14" y="14" width="${CW - 28}" height="${CH - 28}" fill="none" stroke="${c.INK}" stroke-width="28"/>`;
  return { svg: s, width: CW, height: CH };
}

module.exports = {
  id: "captain-megawatt",
  title: "Captain Megawatt and the Runaway Robot",
  author: "Sample comic",
  focusStyle: "dim",
  size: { width: W, height: H },
  pages: [page1, page2, page3],
  cover,
};
