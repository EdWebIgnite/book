/*
 * "Who's at the Waterhole?" sample picture book.
 *
 * Seven guided story pages (focus areas, pans and captions) and one wide explore
 * scene with things to find. Each page here is the picture plus its stops, so the
 * boxes stay in step with the art.
 */
"use strict";
const k = require("./kit");

const W = 3200, H = 2000, HORIZON = 1150;

/** Pixel corners on a page to the fractions book.json uses. */
const boxOn = (w, h) => (x0, y0, x1, y1) => ({
  x: +(x0 / w).toFixed(4), y: +(y0 / h).toFixed(4), w: +((x1 - x0) / w).toFixed(4), h: +((y1 - y0) / h).toFixed(4),
});
const box = boxOn(W, H);

const LOOKS = {
  hot: { sky: "hot", far: "#e3cf96", near: "#d2b874", top: "#eecb72", bottom: "#c8973f", grass: ["#b8a14a", "#a48a38", "#cdb65a"] },
  day: { sky: "day", far: "#c3d6a0", near: "#a9c47e", top: "#e8c96c", bottom: "#c79b45", grass: ["#8aa64a", "#a3b556", "#c2b24e"] },
  golden: { sky: "golden", far: "#b9728a", near: "#9c5d74", top: "#e3a653", bottom: "#a96c30", grass: ["#b5852f", "#8f6a2a", "#c99a3a"] },
  dusk: { sky: "dusk", far: "#3f3576", near: "#2d2659", top: "#5d4d78", bottom: "#2e2648", grass: ["#3f5a4a", "#2f4a3c", "#4a6a50"] },
};

/** Sky, hills and ground for a savanna scene of any width. */
function land(w, look, seed) {
  const L = LOOKS[look];
  return k.sky(w, H, L.sky)
    + k.hills(w, HORIZON + 10, 150, L.far, seed, H)
    + k.hills(w, HORIZON + 40, 80, L.near, seed + 7, H)
    + k.ground(w, H, HORIZON + 30, L.top, L.bottom);
}

// The waterhole sits in the same place on every story page so the world feels consistent.
const POND = { cx: 1750, cy: 1560, rx: 1150, ry: 300 };
const inPond = (x, y) => ((x - POND.cx) / (POND.rx + 70)) ** 2 + ((y - POND.cy) / (POND.ry + 60)) ** 2 < 1;

function waterholePlace(look, seed) {
  const L = LOOKS[look];
  return land(W, look, seed)
    + k.at(2760, 1250, 2.0, k.acacia())
    + k.at(300, 1230, 1.0, k.acacia())
    + k.at(1250, 1215, 0.6, k.acacia())
    + k.at(2250, 1235, 0.9, k.bush())
    + k.grass(0, W, 1230, 1300, 26, L.grass, seed + 1, inPond)
    + k.waterhole(POND.cx, POND.cy, POND.rx, POND.ry)
    + k.at(2010, 1640, 1.6, k.lily()) + k.at(2380, 1470, 1.2, k.lily()) + k.at(1500, 1390, 1.0, k.lily())
    + k.at(2900, 1560, 1.5, k.reeds()) + k.at(1050, 1330, 1.0, k.reeds());
}

const frontGrass = (look, seed, n = 40) => k.grass(0, W, 1880, 1990, n, LOOKS[look].grass, seed);

// ---------------------------------------------------------------- pages

function page1() {
  const L = LOOKS.hot;
  let s = land(W, "hot", 4)
    + k.sun(560, 420, 190, { rays: 18 })
    + k.at(1500, 1215, 0.9, k.acacia({ leaf: "#8a9a48", leafLight: "#a9b45c", leafDark: "#6e7c38" }))
    + k.at(2900, 1260, 1.5, k.acacia({ leaf: "#8a9a48", leafLight: "#a9b45c", leafDark: "#6e7c38" }));
  // cracked, dry ground
  const r = k.rng(21);
  for (let i = 0; i < 26; i++) {
    const x = r() * W, y = 1320 + r() * 620, w = 60 + r() * 120;
    s += `<path d="M${x},${y} l${w * 0.4},${10 - r() * 20} l${w * 0.3},${14 - r() * 28} l${w * 0.3},${8 - r() * 16}" stroke="#a37a33" stroke-width="5" fill="none" opacity=".45" stroke-linecap="round"/>`;
  }
  s += k.grass(0, W, 1260, 1960, 60, L.grass, 5, (x, y) => x > 1850 && x < 2700 && y > 1500 && y < 1760);
  s += k.at(820, 1700, 1.6, k.rock("#b3a48f", "#948672"));
  // footprints behind Tembo
  for (let i = 0; i < 9; i++) {
    s += `<ellipse cx="${780 + i * 125}" cy="${1790 - (i % 2) * 34 - i * 6}" rx="34" ry="13" fill="#8a6a2e" opacity=".3"/>`;
  }
  s += k.at(2260, 1710, 1.75, k.elephant({ trunk: "droop", mood: "tired", sweat: true }));
  s += frontGrass("hot", 9, 24);
  return {
    svg: s,
    stops: [
      { box: box(220, 80, 960, 800), caption: "The sun was hot." },
      { box: box(300, 1250, 1500, 1980), caption: "The grass was dry and crackly." },
      { box: box(600, 1450, 1500, 1980), to: box(1780, 1020, 2880, 1820), panSeconds: 4,
        caption: "And little Tembo was very, very thirsty." },
    ],
  };
}

function page2() {
  let s = waterholePlace("day", 2) + k.sun(1900, 290, 110) + k.cloud(700, 380, 1.4) + k.cloud(1250, 230, 0.9);
  s += k.at(560, 1625, 1.9, k.elephant({ trunk: "drink" }));
  // ripples where his trunk meets the water
  s += `<g fill="none" stroke="#fff" stroke-width="8" opacity=".7"><ellipse cx="1172" cy="1612" rx="70" ry="16"/><ellipse cx="1172" cy="1612" rx="120" ry="28" opacity=".5"/></g>`;
  s += frontGrass("day", 12);
  return {
    svg: s,
    stops: [
      { box: box(450, 1050, 3150, 1960), caption: "Then he saw it. The waterhole!" },
      { box: box(150, 900, 1400, 1800), caption: "Slurp! The water was cool and sweet." },
    ],
  };
}

const dust = (x, y, s) => k.at(x, y, s, `<g fill="#f1dfb2" opacity=".75"><circle cx="0" cy="-30" r="46"/><circle cx="60" cy="-44" r="60"/><circle cx="130" cy="-26" r="40"/></g>`);

function page3() {
  let s = waterholePlace("day", 2) + k.sun(1900, 290, 110) + k.cloud(500, 300, 1.2) + k.cloud(1300, 420, 1.0);
  s += k.at(520, 1625, 1.7, k.elephant({ trunk: "relaxed" }));
  s += dust(3040, 1935, 1.3) + k.at(2800, 1940, 1.6, k.zebra(), true);
  s += dust(2270, 1950, 1.2) + k.at(2040, 1955, 1.5, k.zebra(), true);
  s += frontGrass("day", 14);
  return {
    svg: s,
    stops: [
      { box: box(1600, 1330, 3190, 1995), caption: "Clip, clop! Two zebras came trotting up." },
      { box: box(1600, 1330, 3190, 1995), to: box(120, 950, 1250, 1750), panSeconds: 3.5,
        caption: "“Is there room for us?” they asked. “Plenty of room,” said Tembo." },
    ],
  };
}

function page4() {
  let s = waterholePlace("day", 2) + k.cloud(600, 420, 1.3) + k.cloud(1400, 240, 0.9);
  s += k.at(1480, 1292, 1.25, k.zebra({ head: "down" }));
  s += k.at(1960, 1300, 1.25, k.zebra(), true);
  s += k.at(520, 1625, 1.7, k.elephant({ trunk: "relaxed" }));
  s += k.at(2620, 1800, 1.5, k.giraffe(), true);
  s += frontGrass("day", 16);
  return {
    svg: s,
    stops: [
      { box: box(2250, 1250, 3000, 1900), to: box(1950, 380, 2700, 1030), panSeconds: 4.5,
        caption: "Then came Giraffe. She was tall, tall, tall." },
      { box: box(1250, 950, 2300, 1500), caption: "“How will you reach the water?” asked the zebras." },
    ],
  };
}

function page5() {
  let s = waterholePlace("day", 2) + k.cloud(900, 330, 1.2) + k.cloud(2300, 220, 1.0);
  s += k.at(1380, 1292, 1.25, k.zebra({ head: "down" }));
  s += k.at(2080, 1300, 1.25, k.zebra(), true);
  s += k.at(520, 1625, 1.7, k.elephant({ trunk: "drink" }));
  s += k.at(2950, 1730, 1.3, k.giraffe({ pose: "drink" }), true);
  s += `<g fill="none" stroke="#fff" stroke-width="8" opacity=".7"><ellipse cx="2310" cy="1690" rx="70" ry="16"/><ellipse cx="2310" cy="1690" rx="120" ry="28" opacity=".5"/></g>`;
  s += frontGrass("day", 18);
  return {
    svg: s,
    stops: [
      { box: box(2600, 1150, 3190, 1800), to: box(2050, 1330, 2750, 1830), panSeconds: 4,
        caption: "She spread her long legs wide. Down, down, down went her head." },
      { box: box(100, 880, 3190, 1960), caption: "Now everyone was drinking together." },
    ],
  };
}

function page6() {
  // A different view: up in the thorn tree, looking down at the busy waterhole.
  const L = LOOKS.day;
  let s = land(W, "day", 6) + k.cloud(2300, 300, 1.3) + k.cloud(2950, 520, 0.8);
  s += k.grass(1500, W, 1250, 1500, 20, L.grass, 31);
  s += k.waterhole(2380, 1660, 720, 190, { seed: 8 });
  s += k.at(1810, 1700, 0.95, k.elephant({ trunk: "drink" }));
  s += k.at(2250, 1500, 0.75, k.zebra({ head: "down" })) + k.at(2560, 1505, 0.75, k.zebra(), true);
  s += k.at(3020, 1740, 0.75, k.giraffe({ pose: "drink" }), true);
  s += `<g fill="#fff" opacity=".8"><circle cx="2120" cy="1640" r="12"/><circle cx="2160" cy="1610" r="9"/><circle cx="2700" cy="1700" r="11"/><circle cx="2660" cy="1670" r="8"/></g>`;
  // big tree in the foreground with the bird's branch
  s += k.at(620, 2120, 4.3, k.acacia());
  s += `<path d="M640,1330 C900,1240 1200,1130 1640,1085" stroke="#6e4b2d" stroke-width="46" fill="none" stroke-linecap="round"/>
        <g fill="#446e2e"><ellipse cx="1730" cy="1072" rx="120" ry="36"/></g>
        <g fill="#5d8f3f"><ellipse cx="1730" cy="1062" rx="116" ry="30"/></g>
        <g fill="#7fb04f" opacity=".8"><ellipse cx="1715" cy="1052" rx="70" ry="12"/></g>`;
  s += k.at(1400, 1100, 3.0, k.bird({ mood: "sad" }));
  s += k.grass(1700, W, 1900, 1990, 16, L.grass, 33);
  return {
    svg: s,
    stops: [
      { box: box(1550, 1150, 3190, 1950), caption: "The waterhole was full of big, splashing friends." },
      { box: box(1050, 700, 1900, 1250), caption: "Up in the thorn tree sat a little bird." },
      { box: box(1050, 700, 1900, 1250), to: box(1650, 1200, 3100, 1900), panSeconds: 4,
        caption: "“There’s no room for someone as small as me,” she said." },
    ],
  };
}

function page7() {
  let s = waterholePlace("day", 2) + k.sun(420, 330, 120, { color: "#ffd45a" }) + k.cloud(1500, 260, 1.0);
  // rainbow in the spray
  const colors = ["#ff6b6b", "#ffa94d", "#ffe066", "#8ce99a", "#74c0fc", "#b197fc"];
  colors.forEach((c, i) => {
    const r = 640 - i * 26;
    s += `<path d="M${1850 - r},1180 A${r},${r} 0 0 1 ${1850 + r},1180" stroke="${c}" stroke-width="26" fill="none" opacity=".55"/>`;
  });
  s += k.at(2640, 1300, 1.0, k.giraffe(), true);
  s += k.at(1560, 1292, 1.2, k.zebra());
  s += k.at(2960, 1740, 1.6, k.zebra(), true);
  s += k.at(640, 1650, 2.0, k.elephant({ trunk: "up" }));
  // the shower: an arc of drops from his trunk to the bird
  const r = k.rng(77);
  for (let i = 0; i < 70; i++) {
    const t = r(), x = 1290 + t * 900, y = 860 - Math.sin(t * Math.PI) * 300 + t * 160 + (r() - 0.5) * 150;
    const sz = 10 + r() * 14;
    s += `<path d="M${x},${y} q-${sz * 0.7},${sz * 1.2} 0,${sz * 1.7} q${sz * 0.7},-${sz * 0.5} 0,-${sz * 1.7}Z" fill="#8fd3f4" opacity="${0.6 + r() * 0.4}"/>`;
  }
  s += k.at(2280, 1120, 2.8, k.bird({ mood: "happy" }), true);
  s += `<g fill="#bfe8fb"><circle cx="2100" cy="1160" r="14"/><circle cx="2420" cy="1180" r="12"/><circle cx="2190" cy="1210" r="9"/><circle cx="2360" cy="1230" r="10"/></g>`;
  s += frontGrass("day", 20);
  return {
    svg: s,
    stops: [
      { box: box(180, 660, 1480, 1760), caption: "Tembo filled his trunk right to the top." },
      { box: box(980, 500, 1730, 1100), to: box(1880, 740, 2630, 1340), panSeconds: 3.5,
        caption: "WHOOSH! A shower, just for her." },
      { box: { x: 0, y: 0, w: 1, h: 1 }, caption: "“There is always room at the waterhole,” said Tembo." },
    ],
  };
}

/** The explore scene: a wide evening view with friends to find. */
function page8() {
  const PW = 6400, L = LOOKS.dusk, b = boxOn(PW, H);
  const pond = { cx: 3200, cy: 1570, rx: 2200, ry: 290 };
  const off = (x, y) => ((x - pond.cx) / (pond.rx + 70)) ** 2 + ((y - pond.cy) / (pond.ry + 60)) ** 2 < 1;
  const tree = { leaf: "#3f6b46", leafLight: "#5a8a58", leafDark: "#2c4f38", trunk: "#4a3526" };
  let s = k.sky(PW, H, "dusk") + k.stars(PW, 900, 150, 5) + k.moon(5300, 360, 130)
    + k.hills(PW, HORIZON + 10, 150, L.far, 3, H) + k.hills(PW, HORIZON + 40, 80, L.near, 12, H)
    + k.ground(PW, H, HORIZON + 30, L.top, L.bottom);
  s += k.at(1560, 1330, 2.1, k.acacia(tree)) + k.at(4700, 1250, 1.2, k.acacia(tree)) + k.at(6000, 1300, 1.7, k.acacia(tree))
    + k.at(3300, 1220, 0.7, k.acacia(tree));
  s += k.grass(0, PW, 1230, 1300, 50, L.grass, 41, off);
  s += k.waterhole(pond.cx, pond.cy, pond.rx, pond.ry, { top: "#5d7fc4", bottom: "#2c3f86", mud: "#4a3a52", seed: 9 });
  // moon path on the water
  s += `<ellipse cx="5000" cy="1600" rx="260" ry="34" fill="#fff6d6" opacity=".25"/><ellipse cx="4960" cy="1680" rx="160" ry="20" fill="#fff6d6" opacity=".18"/>`;
  s += k.at(5360, 1570, 1.6, k.reeds("#3c6648", "#5a3f2a")) + k.at(1040, 1560, 1.5, k.reeds("#3c6648", "#5a3f2a")) + k.at(3700, 1320, 1.0, k.reeds("#3c6648", "#5a3f2a"));

  // friends, left to right
  s += k.at(430, 1540, 2.0, k.mound("#8a6a58", "#6a4e44"));
  s += k.at(700, 1600, 1.15, k.meerkat({ look: 4 })) + k.at(830, 1640, 1.35, k.meerkat()) + k.at(960, 1610, 0.95, k.meerkat({ look: -4 }));
  s += k.at(1250, 1890, 1.4, k.beetle());
  s += k.at(1905, 640, 1.25, k.owl());
  s += k.at(2330, 1870, 1.6, k.elephant({ trunk: "relaxed" }));
  s += k.at(2330, 1388, 1.5, k.bird({ mood: "happy" }));
  s += k.at(2950, 1740, 2.2, k.lily()) + k.at(2940, 1725, 1.3, k.frog());
  s += k.at(3500, 1570, 1.5, k.hippoEyes());
  s += k.at(4000, 1300, 1.25, k.zebra({ head: "down" })) + k.at(4420, 1306, 1.25, k.zebra(), true);
  s += k.at(4620, 1860, 1.5, k.tortoise());
  s += k.at(5560, 1800, 1.4, k.giraffe(), true);
  s += k.at(6060, 1760, 1.5, k.lionPeek());
  for (let i = 0; i < 9; i++) s += k.tuft(5900 + i * 40, 1775 + (i % 3) * 8, 1.7, L.grass[i % 3]);
  s += k.at(5050, 1820, 1.2, k.rock("#6f6880", "#544e66"));
  s += k.grass(0, PW, 1880, 1990, 70, L.grass, 43);

  // evening light over everything, then the things that glow
  s += `<rect width="${PW}" height="${H}" fill="#1b1658" opacity=".3"/>`;
  const r = k.rng(99);
  for (let i = 0; i < 26; i++) s += k.at(200 + r() * (PW - 400), 900 + r() * 900, 0.6 + r() * 0.7, k.firefly());

  return {
    svg: s, width: PW,
    mode: "explore",
    guidance: { hints: true, showMe: true },
    stops: [
      { box: b(1700, 1000, 3100, 1960), style: "none",
        caption: "The sun went down, and more friends came to drink. Who can you find?" },
    ],
    hotspots: [
      { box: b(2200, 1240, 2470, 1420), caption: "The little bird found the best seat of all." },
      { box: b(2800, 1620, 3100, 1790), caption: "Ribbit! A frog on a lily pad." },
      { box: b(3310, 1470, 3690, 1610), caption: "Hippo! Only her eyes and ears peep out." },
      { box: b(1800, 440, 2010, 680), caption: "Hoo-hoo! Owl has just woken up." },
      { box: b(620, 1350, 1030, 1660), caption: "The meerkat family. Always on the lookout!" },
      { box: b(1180, 1790, 1420, 1910), caption: "A dung beetle, rolling his ball home." },
      { box: b(3850, 1050, 4650, 1320), caption: "The zebras are still sipping." },
      { box: b(4450, 1720, 4830, 1880), caption: "Tortoise is nearly there. Slow and steady!" },
      { box: b(5150, 560, 5800, 1820), caption: "Giraffe can see everyone from up there." },
      { box: b(5920, 1590, 6200, 1780), caption: "A lion cub, playing hide-and-seek in the grass." },
    ],
  };
}

function cover() {
  const CW = 1600, CH = 2400;
  let s = k.sky(CW, CH, "day") + k.sun(300, 900, 120)
    + k.hills(CW, 1480, 150, "#c3d6a0", 4, CH) + k.hills(CW, 1520, 80, "#a9c47e", 9, CH)
    + k.ground(CW, CH, 1510, "#e8c96c", "#c79b45")
    + k.at(1250, 1640, 1.5, k.acacia())
    + k.waterhole(900, 2260, 1000, 190)
    + k.at(540, 2130, 2.4, k.elephant({ trunk: "up" }))
    + k.at(1330, 1195, 2.2, k.bird({ mood: "happy" }), true)
    + k.at(1420, 2330, 2.0, k.reeds()) + k.at(240, 2360, 1.8, k.lily())
    + k.grass(0, CW, 2330, 2395, 14, LOOKS.day.grass, 3);
  const title = (y, text) => `<text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="210" fill="#5a3413" stroke="#5a3413" stroke-width="26" stroke-linejoin="round">${text}</text>
    <text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="210" fill="#fff8e1">${text}</text>`;
  s += title(330, "Who’s at the") + title(560, "Waterhole?");
  return { svg: s, width: CW, height: CH };
}

module.exports = {
  id: "waterhole",
  title: "Who’s at the Waterhole?",
  author: "Sample book",
  focusStyle: "vignette",
  size: { width: W, height: H },
  pages: [page1, page2, page3, page4, page5, page6, page7, page8],
  cover,
};
