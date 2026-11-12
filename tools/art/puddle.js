/*
 * "Moss and the Biggest Puddle": a picture book for ages 4 to 5.
 *
 * Moss the bear cub has new red boots and no puddle. The whole book happens in one
 * garden, drawn once as a wide scene (6400 x 2000). Each story page is a window onto
 * that scene with its own weather and characters, so the place stays the same from
 * page to page. The last page is the whole garden to explore, with a short story
 * inside it (find Dot's three ducklings).
 *
 * Written for the age: short sentences, a repeating "too..." pattern, sound words,
 * camera stops that point at what the words are about, and a few questions for the
 * adult reading along.
 */
"use strict";
const k = require("./kit");
const g = require("./garden");

const W = 6400, H = 2000, OUT_W = 3200, OUT_H = 2000;
const A = k.at;
const f4 = v => +v.toFixed(4);

/** Turns scene coordinates into the fractions-of-the-page that book.json uses, for one window. */
const boxer = ([vx, vy, vw, vh]) => (x0, y0, x1, y1) =>
  ({ x: f4((x0 - vx) / vw), y: f4((y0 - vy) / vh), w: f4((x1 - x0) / vw), h: f4((y1 - y0) / vh) });
const WHOLE = { x: 0, y: 0, w: 1, h: 1 };

// Where things are in the garden.
const PUDDLE = { x: 4850, y: 1765, rx: 720, ry: 165 };
const CAN = [1900, 1700], BATH = [2850, 1620], BENCH = [3800, 1620], BOWL = [5650, 1670], DOG = [6040, 1640], KENNEL = [6060, 1400];

const SKY = {
  sunny: ["#58b7f0", "#cdeafb"],
  turning: ["#58b7f0", "#cdeafb"],
  storm: ["#3f4a5f", "#7b8698"],
  fresh: ["#4aaeee", "#d9f1ff"],
  evening: ["#f59a52", "#ffe6b0"],
};

/**
 * The garden. Returns three layers so characters can stand between them:
 * back (sky, house, lawn, props), front (grass in front of feet), over (rain, light, paper grain).
 */
function garden(weather, { bigPuddle = false, dog = false, canDrip = false } = {}) {
  const [top, bottom] = SKY[weather];
  const wet = weather === "fresh" || weather === "evening";
  const r = k.rng(7);
  let back = `<defs>
      <linearGradient id="gsky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient>
      <linearGradient id="glawn" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${wet ? "#8fd257" : "#a5d665"}"/><stop offset="1" stop-color="${wet ? "#5fa83a" : "#79b648"}"/></linearGradient>
      <linearGradient id="gturn" x1="0" y1="0" x2="1" y2="0"><stop offset=".12" stop-color="#8a95a6" stop-opacity="0"/><stop offset=".5" stop-color="#8a95a6" stop-opacity=".95"/><stop offset="1" stop-color="#6f7b8d"/></linearGradient>
      <filter id="gsoft" x="-5%" y="-20%" width="110%" height="140%"><feGaussianBlur stdDeviation="7"/></filter>
      <filter id="ggrain" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="4"/>
        <feColorMatrix values="0 0 0 0 0.35  0 0 0 0 0.3  0 0 0 0 0.25  0 0 0 0.16 0"/></filter>
    </defs>
    <rect width="${W}" height="1200" fill="url(#gsky)"/>`;

  // sky furniture
  if (weather === "sunny") back += k.sun(2300, 300, 150, { rays: 16 }) + A(900, 330, 1.1, g.cloud()) + A(3700, 240, 0.9, g.cloud()) + A(5200, 420, 1.2, g.cloud());
  if (weather === "turning") {
    back += k.sun(2300, 300, 150, { rays: 16 }) + A(700, 330, 1.0, g.cloud());
    back += `<rect width="${W}" height="1200" fill="url(#gturn)"/>`;
    for (const [x, y, s] of [[2700, 300, 1.5], [3300, 520, 1.2], [3900, 260, 1.7], [4600, 480, 1.4], [5300, 280, 1.6], [6000, 500, 1.4], [2050, 520, 1.0]]) back += A(x, y, s, g.cloud("#8793a3", "#6b7686"));
  }
  if (weather === "storm") {
    for (let i = 0; i < 12; i++) back += A(200 + i * 560, 240 + (i % 3) * 150, 1.5 + (i % 2) * 0.4, g.cloud("#5d6879", "#48525f"));
  }
  if (wet) {
    back += `<g transform="translate(${PUDDLE.x - 100} 1130)" opacity=".9">${g.rainbow(1000, 44)}</g>`;
    back += weather === "fresh" ? k.sun(5900, 300, 130, { rays: 14 }) + A(1400, 300, 1.1, g.cloud()) + A(3000, 220, 0.9, g.cloud())
                                : k.sun(5750, 760, 190, { color: "#ffd166", glow: "#ffe9a8" }) + A(1600, 330, 1.1, g.cloud("#ffe8cf", "#f6c79a")) + A(3400, 260, 0.9, g.cloud("#ffe8cf", "#f6c79a"));
  }

  // far hedge and trees beyond the fence, soft
  let hedge = "";
  for (let x = 1200; x < W + 200; x += 330) hedge += A(x, 1100, 1.5 + r() * 0.5, g.bush("#7fbf63", "#63a84d"));
  back += `<g filter="url(#gsoft)" opacity=".85">${hedge}</g>`;
  back += A(1300, 1110, 1, g.fence(W - 1300));

  // lawn, with mown stripes
  back += `<rect y="1090" width="${W}" height="${H - 1090}" fill="url(#glawn)"/>`;
  for (let x = -400; x < W; x += 520) back += `<path d="M${x},${H} L${x + 520},1090 L${x + 780},1090 L${x + 260},${H} Z" fill="#fff" opacity=".07"/>`;
  back += k.grass(1300, W, 1110, 1500, 90, wet ? ["#4f9a33", "#6cb845"] : ["#6aa83f", "#8cc452"], 11);
  for (let i = 0; i < 70; i++) {
    const x = 1350 + r() * (W - 1400), y = 1150 + r() * 800;
    back += `<circle cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" r="7" fill="#fff"/><circle cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" r="3" fill="#ffd23f"/>`;
  }

  // flowerbeds along the fence
  const colors = ["#f25a8a", "#ffd23f", "#ff7a3d", "#b388eb", "#ff6b6b"];
  for (let i = 0; i < 26; i++) {
    const x = 1380 + i * 190 + r() * 60;
    if (x > 3500 && x < 4350) continue; // the big tree stands here
    back += A(x, 1160 + r() * 40, 0.9 + r() * 0.4, i % 3 ? g.flower(colors[i % 5], "#ffd23f", 110 + r() * 50) : g.tulip(colors[(i + 2) % 5], 110 + r() * 40));
  }

  // trees, house, path
  back += A(2480, 1200, 0.85, g.tree()) + A(4020, 1230, 1.3, g.tree(1, "#4f9a44", "#387a30", "#7dbb58")) + A(5350, 1180, 0.7, g.tree());
  back += A(0, 1500, 1, g.house());
  back += `<rect x="0" y="1500" width="1320" height="${H - 1500}" fill="url(#glawn)"/>`;
  for (const [x, y, s] of [[700, 1640, 1.2], [980, 1760, 1.3], [1330, 1840, 1.25], [1680, 1890, 1.1]]) {
    back += `<ellipse cx="${x}" cy="${y}" rx="${120 * s}" ry="${38 * s}" fill="#b9b3a8"/><ellipse cx="${x}" cy="${y - 8}" rx="${116 * s}" ry="${34 * s}" fill="#d6d0c5"/>`;
  }
  back += A(240, 1560, 1.1, g.bush()) + A(1150, 1560, 0.8, g.bush("#69ae52", "#4a8a3c"));

  // garden things
  back += A(CAN[0], CAN[1], 0.9, g.wateringCan());
  if (canDrip) {
    back += `<g fill="#8fd6f0" stroke="#5fb4dc" stroke-width="3">${[1490, 1570, 1650].map(y => `<path d="M2132,${y} q-12,20 0,30 q12,-10 0,-30 Z"/>`).join("")}</g>`;
    back += A(2140, 1745, 1, g.puddle(105, 24));
  }
  back += A(BATH[0], BATH[1], 0.95, g.birdBath());
  back += A(KENNEL[0], KENNEL[1], 0.9, g.kennel());
  back += A(6230, 1900, 1.1, g.bigLeaf()) + A(6030, 1940, 0.9, g.bigLeaf("#5aa844")) + A(6330, 1960, 0.8, g.bigLeaf("#458f34"));
  back += A(BOWL[0], BOWL[1], 0.9, g.dogBowl());
  if (dog) back += A(DOG[0], DOG[1], 0.8, g.dog());

  if (bigPuddle) {
    back += A(PUDDLE.x, PUDDLE.y, 1, g.puddle(PUDDLE.rx, PUDDLE.ry, weather === "evening" ? { top: "#ffe2b0", bottom: "#f2a765" } : {}));
    // a ghost of the rainbow in the water
    back += `<g transform="translate(${PUDDLE.x} ${PUDDLE.y - 40}) scale(.62 -.18)" opacity=".35">${g.rainbow(1000, 44)}</g>`;
    for (const [x, y, rx] of [[3250, 1850, 150], [2350, 1900, 120], [5900, 1900, 110]]) back += A(x, y, 1, g.puddle(rx, rx * 0.2));
  }
  // (The bench is drawn by each page, so that Moss can hide under it.)

  let front = k.grass(0, W, 1900, 1995, 110, wet ? ["#4f9a33", "#3f8529"] : ["#6aa83f", "#5a9735"], 23);

  let over = "";
  if (weather === "storm") {
    over += `<rect width="${W}" height="${H}" fill="#1b2944" opacity=".36"/>`;
    const rr = k.rng(41);
    let rain = "";
    for (let i = 0; i < 1500; i++) {
      const x = rr() * (W + 300), y = rr() * H, len = 70 + rr() * 80;
      rain += `<path d="M${x.toFixed(0)},${y.toFixed(0)} l${(-len * 0.28).toFixed(0)},${len.toFixed(0)}"/>`;
    }
    over += `<g stroke="#e3f1ff" stroke-width="5" stroke-linecap="round" opacity=".5">${rain}</g>`;
    let rings = "";
    for (let i = 0; i < 90; i++) rings += `<ellipse cx="${(1300 + rr() * 5100).toFixed(0)}" cy="${(1200 + rr() * 780).toFixed(0)}" rx="${(18 + rr() * 26).toFixed(0)}" ry="7"/>`;
    over += `<g fill="none" stroke="#e3f1ff" stroke-width="4" opacity=".6">${rings}</g>`;
  }
  if (weather === "evening") over += `<rect width="${W}" height="${H}" fill="#ff8a2a" opacity=".13"/>`;
  if (wet) {
    const rr = k.rng(52);
    for (let i = 0; i < 60; i++) {
      const x = 1300 + rr() * 5000, y = 1150 + rr() * 800, s = 8 + rr() * 10;
      over += `<path d="M${x},${y - s} L${x + s * 0.22},${y - s * 0.22} L${x + s},${y} L${x + s * 0.22},${y + s * 0.22} L${x},${y + s} L${x - s * 0.22},${y + s * 0.22} L${x - s},${y} L${x - s * 0.22},${y - s * 0.22} Z" fill="#fff" opacity=".8"/>`;
    }
  }
  over += `<rect width="${W}" height="${H}" filter="url(#ggrain)"/>`;
  return { back, front, over };
}

const bench = () => A(BENCH[0], BENCH[1], 1.15, g.bench());

/** Put a page together: garden, then the page's own characters, then the layers that go in front. */
function page(weather, opts, view, people, stops) {
  const gdn = garden(weather, opts);
  return { svg: gdn.back + people + gdn.front + gdn.over, view, width: OUT_W, height: OUT_H, stops };
}

// ---------------------------------------------------------------- the story

function page1() {
  const view = [0, 480, 2400, 1500], b = boxer(view);
  const people = A(600, 1790, 1.0, g.box())
    + A(960, 1800, 1.15, g.boot()) + `<g transform="translate(1110 1812) scale(-1.15 1.15)">${g.boot()}</g>`
    + A(1560, 1760, 1.0, g.moss({ pose: "cheer", eyes: "happy", mouth: "laugh", boots: "none" }), true);
  return page("sunny", {}, view, bench() + people, [
    { box: b(300, 1360, 1300, 1900), caption: "Moss had new boots." },
    { box: b(850, 1580, 1250, 1880), to: b(1260, 1100, 1860, 1560), panSeconds: 3.5,
      caption: "Big, red, shiny boots. Moss loved them.", ask: "What do you love to wear?" },
  ]);
}

function page2() {
  const view = [300, 0, 3200, 2000], b = boxer(view);
  const people = A(1560, 1790, 1.0, g.moss({ pose: "proud", eyes: "happy" }));
  return page("sunny", {}, view, bench() + people, [
    { box: b(1180, 1180, 1940, 1900), caption: "Boots are for puddles. Everybody knows that." },
    { box: b(1940, 40, 2660, 560), to: b(2000, 1300, 3100, 1960), panSeconds: 4,
      caption: "But the sun was shining. There was not one puddle anywhere.", ask: "Where could Moss look for a puddle?" },
  ]);
}

function page3() {
  const view = [1300, 760, 1984, 1240], b = boxer(view);
  const people = A(2330, 1790, 0.95, g.moss({ pose: "toe", look: 1, mouth: "flat", eyes: "open" }), true);
  return page("sunny", { canDrip: true }, view, bench() + people, [
    { box: b(1980, 1380, 2300, 1700), caption: "Drip. Drip. Drip." },
    { box: b(1960, 1600, 2400, 1880), caption: "A puddle! Moss put in one toe." },
    { box: b(2080, 1180, 2580, 1580), caption: "Too small." },
  ]);
}

function page4() {
  const view = [1900, 480, 2400, 1500], b = boxer(view);
  const people = A(BATH[0] + 20, BATH[1] - 500, 1.2, g.bird({ splash: true }))
    + A(2500, 1740, 1.0, g.moss({ pose: "tiptoe", look: 0.8, mouth: "oh" }));
  return page("sunny", {}, view, bench() + people, [
    { box: b(2280, 1480, 2740, 1820), to: b(2440, 920, 3260, 1440), panSeconds: 4,
      caption: "The bird bath had water. Moss stretched up, up, up." },
    { box: b(2580, 900, 3140, 1260), caption: "Too high. And somebody was already in it." },
  ]);
}

function page5() {
  const view = [4300, 640, 2100, 1312], b = boxer(view);
  const people = A(4980, 1760, 1.0, g.moss({ pose: "whoa", look: 1, mouth: "wobble", eyes: "wide" }));
  return page("sunny", { dog: true }, view, bench() + people, [
    { box: b(5440, 1470, 5860, 1740), caption: "The dog’s bowl had water." },
    { box: b(5440, 1470, 5860, 1740), to: b(5660, 1360, 6100, 1690), panSeconds: 3,
      caption: "The dog looked at Moss. Moss looked at the dog." },
    { box: b(4660, 1150, 5300, 1800), caption: "Too... not mine.", ask: "Why didn’t Moss jump in the dog’s bowl?" },
  ]);
}

function page6() {
  const view = [0, 0, 3200, 2000], b = boxer(view);
  const people = A(590, 1560, 0.95, g.moss({ pose: "sit", eyes: "sad", mouth: "sad" }))
    + `<path d="M590,1040 q-14,24 0,36 q14,-12 0,-36 Z" fill="#bfe6fb" stroke="#6fb7e6" stroke-width="4"/>`;
  return page("turning", {}, view, bench() + people, [
    { box: b(240, 1020, 960, 1600), caption: "Moss sat down. Boots with no puddle are just shoes.", ask: "How is Moss feeling now?" },
    { box: b(1340, 30, 2300, 630), to: b(2240, 30, 3200, 630), panSeconds: 4.5, caption: "Then the sky went grey." },
    { box: b(400, 960, 780, 1300), caption: "Plip." },
  ]);
}

function page7() {
  const view = [2700, 480, 2400, 1500], b = boxer(view);
  const flash = `<ellipse cx="4670" cy="840" rx="640" ry="420" fill="#fff7c2" opacity=".2"/>
    <path d="M4700,520 L4580,820 L4680,820 L4560,1120 L4820,750 L4710,750 L4800,520 Z" fill="#fff7c2" stroke="#ffe066" stroke-width="8" stroke-linejoin="round"/>`;
  const people = flash
    + A(3650, 1640, 0.8, g.moss({ pose: "hide", eyes: "wide", mouth: "wobble", look: 0.6 }))
    + A(4060, 1650, 0.72, g.dot({ mood: "scared" }), true)
    + bench();
  return page("storm", {}, view, people, [
    { box: b(4150, 490, 5100, 1180), caption: "Plip, plop, plip, plop... BOOM!" },
    { box: b(3360, 1200, 3920, 1700), caption: "That was a LOT of rain. Moss hid under the bench." },
    { box: b(3860, 1300, 4300, 1700),
      caption: "Somebody else was hiding there too. “I’m Dot,” said the duck. “I don’t like the boom either.”",
      ask: "What do you do when it thunders?" },
  ]);
}

function page8() {
  const view = [3100, 0, 3200, 2000], b = boxer(view);
  const people = bench()
    + A(3560, 1700, 0.95, g.moss({ pose: "look", look: 1, eyes: "wide", mouth: "oh" }))
    + A(3930, 1730, 0.8, g.dot({}));
  return page("fresh", { bigPuddle: true }, view, people, [
    { box: b(3250, 1100, 4200, 1780), caption: "When the rain stopped, they peeped out." },
    { box: b(3250, 1100, 4200, 1780), to: b(4000, 1340, 5700, 1990), panSeconds: 4,
      caption: "And there it was. The biggest puddle in the whole world.", ask: "What do you think Moss will do?" },
  ]);
}

function page9() {
  const view = [3900, 500, 2400, 1500], b = boxer(view);
  const people = bench()
    + `<ellipse cx="4760" cy="${PUDDLE.y}" rx="170" ry="30" fill="#2b5f8f" opacity=".3"/>`
    + `<g stroke="#fff" stroke-width="12" stroke-linecap="round" opacity=".85"><path d="M4560,1430 v150 M4660,1480 v160 M4860,1480 v160 M4960,1430 v150"/></g>`
    + A(4760, 1300, 1.0, g.moss({ pose: "jump", eyes: "happy", mouth: "laugh" }))
    + A(5600, 1700, 0.85, g.dot({ pose: "flap" }), true);
  return page("fresh", { bigPuddle: true }, view, people, [
    { box: b(5300, 1280, 5900, 1740), caption: "One... two... three..." },
    { box: b(4380, 660, 5140, 1700), caption: "JUMP!" },
  ]);
}

function page10() {
  const view = [3900, 500, 2400, 1500], b = boxer(view);
  const people = bench()
    + A(4780, PUDDLE.y + 10, 1.0, g.splash())
    + A(4780, PUDDLE.y + 30, 1.0, g.moss({ pose: "splash", eyes: "happy", mouth: "laugh" }))
    + `<g transform="translate(5640 1420) rotate(-16)">${A(0, 0, 0.85, g.dot({ pose: "flap" }), true)}</g>`
    + `<g fill="#bfe6fb" stroke="#8fcbee" stroke-width="3">${[[5380, 1180], [5520, 1040], [5780, 1100], [5860, 1300], [5440, 1400]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="18"/>`).join("")}</g>`;
  return page("fresh", { bigPuddle: true }, view, people, [
    { box: b(4050, 900, 5550, 1960), caption: "SPLASH!" },
    { box: b(4380, 1180, 5180, 1840), caption: "It was a very, very good puddle." },
    { box: b(5300, 1020, 5960, 1500), caption: "“Again!” said Dot." },
  ]);
}

function page11() {
  const view = [3900, 500, 2400, 1500], b = boxer(view);
  // Dot afloat in the other boot.
  const boat = A(5080, 1620, 0.62, g.dot({ pose: "float" }))
    + A(5060, 1800, 2.1, g.boot())
    + `<ellipse cx="5090" cy="1806" rx="230" ry="36" fill="#f2a765"/><g fill="none" stroke="#fff" stroke-width="7" opacity=".8"><ellipse cx="5090" cy="1800" rx="250" ry="40"/><ellipse cx="5090" cy="1800" rx="330" ry="54" opacity=".5"/></g>`;
  const people = bench() + boat
    + A(4420, 1800, 1.0, g.moss({ pose: "sitUp", eyes: "happy", mouth: "laugh", boots: "left" }))
    + `<g fill="#bfe6fb" stroke="#8fcbee" stroke-width="3"><path d="M4290,1700 q-10,18 0,26 q10,-8 0,-26 Z"/><path d="M4560,1690 q-10,18 0,26 q10,-8 0,-26 Z"/></g>`;
  return page("evening", { bigPuddle: true }, view, people, [
    { box: b(4050, 1180, 4800, 1860), caption: "Moss’s boots were full of water." },
    { box: b(4050, 1180, 4800, 1860), to: b(4780, 1280, 5420, 1880), panSeconds: 3.5, caption: "And one boot made a very good boat." },
    { box: WHOLE, caption: "It was the best puddle day ever.", ask: "What was your favourite part?" },
  ]);
}

/** The whole garden after the rain, to explore. Dot has lost her three ducklings. */
function page12() {
  const gdn = garden("fresh", { bigPuddle: true, dog: true });
  const view = [0, 0, W, H], b = boxer(view);
  const around = (cx, cy, w, h) => b(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
  const hot = [];
  const find = (box, caption, extra = {}) => hot.push({ box, caption, ...extra });

  let v = gdn.back + bench();
  // Moss and Dot by the puddle
  v += A(4330, 1740, 0.95, g.moss({ pose: "stand", look: 1 })) + A(4700, 1700, 0.8, g.dot({}));
  // the three ducklings
  v += A(CAN[0] - 6, CAN[1] - 215, 0.85, g.duckling());                                   // 1: on the watering can
  find(around(1890, 1420, 300, 300), "One! Having a paddle in the watering can.", { story: 1, name: "the duckling at the watering can" });
  v += A(BOWL[0], BOWL[1] - 70, 0.75, g.duckling(), true);                                // 2: in the dog's bowl
  find(around(BOWL[0], BOWL[1] - 100, 320, 280), "Two! Splashing in the dog’s bowl. The dog does not mind. Much.", { story: 2, name: "the duckling by the dog" });
  v += A(6120, 1890, 0.8, g.duckling(), true) + A(6230, 1900, 1.1, g.bigLeaf());          // 3: under the big leaf
  find(around(6130, 1770, 380, 360), "Three! Hiding under the big leaf, nice and dry. “Thank you!” said Dot.", { story: 3, name: "the duckling under the big leaf" });

  // extras
  v += A(BATH[0] + 20, BATH[1] - 500, 1.2, g.bird({}));
  find(around(2870, 1080, 340, 260), "The little bird has her bath back.", { name: "the bird" });
  v += A(3150, 1880, 1.0, g.snail());
  find(around(3160, 1820, 300, 200), "A snail! Snails love the rain.", { name: "the snail" });
  v += A(5560, 1900, 1.1, g.frog());
  find(around(5560, 1850, 220, 180), "Ribbit! Frog thinks this is the best puddle too.", { name: "the frog" });
  v += A(2380, 1905, 1.0, g.worm());
  find(around(2390, 1880, 240, 130), "A wiggly worm has come up to see.", { name: "the worm" });
  v += A(5180, 1800, 1.0, g.paperBoat());
  find(around(5180, 1750, 260, 220), "A paper boat, sailing across the puddle.", { name: "the paper boat" });
  v += A(5200, 1262, 1.0, g.mushroom()) + A(5290, 1276, 0.7, g.mushroom());
  find(around(5235, 1225, 260, 170), "Mushrooms pop up after the rain.", { name: "the mushrooms" });
  v += A(1540, 1010, 1.0, g.butterfly()) + A(3400, 1260, 0.7, g.butterfly("#ffd23f"));
  find(around(1540, 1010, 260, 200), "A butterfly, drying her wings in the sun.", { name: "the butterfly" });
  v += A(2010, 1100, 0.9, g.ladybird());
  find(around(2010, 1080, 170, 140), "A ladybird! Can you count her spots?", { name: "the ladybird", ask: "How many spots can you see?" });
  find(around(DOG[0] - 20, DOG[1] - 110, 520, 280), "The dog is having a rest.", { name: "the dog" });
  find(b(PUDDLE.x - 1100, 150, PUDDLE.x + 900, 900), "A rainbow! The sun came back.", { name: "the rainbow", ask: "What colours can you see?" });

  v += gdn.front + gdn.over;
  const story = hot.filter(h => h.story).sort((x, y) => x.story - y.story);
  const extras = hot.filter(h => !h.story).sort((x, y) => x.box.x - y.box.x);
  return {
    svg: v, width: W, height: H,
    mode: "explore",
    guidance: { hints: true, showMe: true, map: true },
    stops: [
      { box: b(4000, 1150, 5200, 1900), style: "none",
        caption: "After the rain, the garden was full of surprises. But Dot has lost her three ducklings! Can you find them?" },
    ],
    hotspots: [...story, ...extras],
  };
}

function cover() {
  const CW = 1600, CH = 2400;
  let v = `<defs><linearGradient id="cvsky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4aaeee"/><stop offset="1" stop-color="#d9f1ff"/></linearGradient>
      <linearGradient id="cvlawn" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fd257"/><stop offset="1" stop-color="#5fa83a"/></linearGradient>
      <filter id="cvgrain" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="4"/>
        <feColorMatrix values="0 0 0 0 0.35  0 0 0 0 0.3  0 0 0 0 0.25  0 0 0 0.16 0"/></filter></defs>
    <rect width="${CW}" height="${CH}" fill="url(#cvsky)"/>`;
  v += `<g transform="translate(800 1560)" opacity=".9">${g.rainbow(760, 40)}</g>`;
  v += A(300, 820, 0.9, g.cloud()) + A(1350, 980, 0.8, g.cloud());
  v += `<rect y="1540" width="${CW}" height="${CH - 1540}" fill="url(#cvlawn)"/>` + k.grass(0, CW, 1560, 1760, 30, ["#4f9a33", "#6cb845"], 5);
  v += A(800, 2140, 1, g.puddle(700, 170)) + A(800, 2150, 0.9, g.splash());
  v += A(800, 2180, 1.45, g.moss({ pose: "splash", eyes: "happy", mouth: "laugh" }));
  v += `<g transform="translate(1330 1700) rotate(-14)">${A(0, 0, 0.8, g.dot({ pose: "flap" }), true)}</g>`;
  v += k.grass(0, CW, 2320, 2395, 26, ["#4f9a33", "#3f8529"], 8);
  const title = (y, t, size) => `<text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="${size}" fill="#23466e" stroke="#23466e" stroke-width="30" stroke-linejoin="round">${t}</text>
    <text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="${size}" fill="#fff8e1">${t}</text>`;
  v += title(300, "Moss", 300) + title(500, "and the", 150) + title(700, "Biggest Puddle", 196);
  v += `<rect width="${CW}" height="${CH}" filter="url(#cvgrain)"/>`;
  return { svg: v, width: CW, height: CH };
}

module.exports = {
  id: "biggest-puddle",
  title: "Moss and the Biggest Puddle",
  author: "Sample book, ages 4 to 5",
  focusStyle: "vignette",
  size: { width: OUT_W, height: OUT_H },
  pages: [page1, page2, page3, page4, page5, page6, page7, page8, page9, page10, page11, page12],
  cover,
};
