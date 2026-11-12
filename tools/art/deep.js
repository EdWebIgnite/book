/*
 * "The Light in the Deep": one very big underwater scene to explore, with a story inside it.
 *
 * The picture is 8192 x 6144 and is cut into tiles. It runs from the sunny surface down
 * through a coral reef, a kelp forest and a shipwreck to the dark sea floor. Six story
 * steps lead the child from Finn the clownfish down to the light at the bottom, and
 * there are extra things to find off that path.
 */
"use strict";
const k = require("./kit");
const s = require("./sea");

const W = 8192, H = 6144, SURF = 640, FLOOR = 5760;
const A = k.at;
const f4 = v => +v.toFixed(4);
const corners = (x0, y0, x1, y1) => ({ x: f4(x0 / W), y: f4(y0 / H), w: f4((x1 - x0) / W), h: f4((y1 - y0) / H) });
/** A box from its centre and size, in pixels. */
const around = (cx, cy, w, h) => corners(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);

function water() {
  let out = `<defs>
    <linearGradient id="dsky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#9fdcff"/><stop offset="1" stop-color="#e9f8ff"/></linearGradient>
    <linearGradient id="dsea" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#63d3e8"/><stop offset=".16" stop-color="#2cabd8"/>
      <stop offset=".42" stop-color="#1779ba"/><stop offset=".72" stop-color="#0c4288"/><stop offset="1" stop-color="#072452"/></linearGradient>
    <linearGradient id="dray" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".2"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>
  </defs>
  <rect width="${W}" height="${SURF + 20}" fill="url(#dsky)"/><rect y="${SURF}" width="${W}" height="${H - SURF}" fill="url(#dsea)"/>`;
  // sunbeams slanting down from the surface
  const r = k.rng(8);
  for (let i = 0; i < 13; i++) {
    const x = 1700 + i * 520 + r() * 200, w = 120 + r() * 220, len = 1500 + r() * 1400, lean = -500 - r() * 200;
    out += `<path d="M${x},${SURF} L${x + w},${SURF} L${x + w * 2.2 + lean},${SURF + len} L${x + lean - w},${SURF + len} Z" fill="url(#dray)"/>`;
  }
  return out;
}

function surface() {
  let wave = `M0,${SURF}`;
  for (let x = 0; x < W; x += 160) wave += ` q40,-22 80,0 q40,22 80,0`;
  return `<path d="${wave}" stroke="#fff" stroke-width="12" fill="none" opacity=".85" stroke-linecap="round"/>
    <path d="${wave} L${W},${SURF + 70} L0,${SURF + 70} Z" fill="#fff" opacity=".18"/>`;
}

/** Soft darkness that grows with depth. Anything drawn after this looks lit. */
function depth() {
  return `<defs><linearGradient id="ddepth" gradientUnits="userSpaceOnUse" x1="0" y1="1500" x2="0" y2="${H}">
    <stop offset="0" stop-color="#030a25" stop-opacity="0"/><stop offset=".5" stop-color="#030a25" stop-opacity=".3"/><stop offset="1" stop-color="#030a25" stop-opacity=".62"/></linearGradient></defs>
    <rect y="1500" width="${W}" height="${H - 1500}" fill="url(#ddepth)"/>`;
}

function scene() {
  const hot = [];
  /** Remember a thing to find. */
  const find = (box, caption, extra = {}) => hot.push({ box, caption, ...extra });
  let v = water();

  // ---- above the water
  v += k.sun(6900, 250, 130, { rays: 14 }) + k.cloud(2600, 230, 1.7) + k.cloud(5200, 300, 1.2) + k.cloud(7750, 420, 1.0) + k.cloud(380, 300, 1.1);
  v += A(3300, 330, 1.2, s.seagull()) + A(3560, 250, 0.9, s.seagull()) + A(6000, 400, 1.0, s.seagull());

  // ---- the rock that shapes the world
  const ROCK = { fill: "#7a6f80", shade: "#5f5567" };
  const left = [[0, SURF], [1650, SURF], [1750, 900], [1900, 1500], [2100, 2110], [3550, 2160], [3640, 2260], [3520, 2700], [3050, 3180],
    [1500, 3480], [760, 3800], [640, 4300], [700, 4700], [3650, 4770], [3740, 4860], [3640, 5250], [3300, FLOOR + 30], [0, FLOOR + 30]];
  const right = [[W, 1650], [7300, 1740], [6000, 1860], [5740, 1960], [5820, 2450], [6500, 2850], [7350, 3000], [7700, 3300], [7780, 3880],
    [4750, 3930], [4640, 4040], [4760, 4420], [5600, 4560], [7000, 4520], [7700, 4700], [7800, 5500], [7700, FLOOR + 30], [W, FLOOR + 30]];
  v += s.rock(left, 5, ROCK) + s.rock(right, 9, ROCK);
  v += s.ledgeTop([[1650, 652], [1750, 900], [1900, 1500], [2100, 2110]], "#cdbb93", 34);
  v += s.ledgeTop([[2100, 2112], [2600, 2130], [3100, 2128], [3550, 2162]], "#ead9a8", 54);
  v += s.ledgeTop([[W, 1652], [7300, 1742], [6000, 1862], [5760, 1960]], "#ead9a8", 54);
  v += s.ledgeTop([[7770, 3882], [4770, 3932]], "#5f7f52", 48);
  v += s.ledgeTop([[720, 4702], [3640, 4772]], "#8f8aa3", 44);
  // fossils pressed into the rock
  v += s.spin(1050, 1450, 20, 1.5, s.ammonite()) + s.spin(520, 2250, -30, 1.1, s.ammonite()) + s.spin(330, 4350, 10, 1.4, s.ammonite())
    + s.spin(2750, 5420, 40, 1.2, s.ammonite()) + s.spin(7450, 2450, -15, 1.3, s.ammonite()) + s.spin(6200, 4250, 25, 0.9, s.ammonite())
    + s.spin(8000, 5100, 0, 1.2, s.ammonite())
    + s.spin(700, 3300, -12, 1.4, s.fishBones()) + s.spin(6800, 2280, 8, 1.2, s.fishBones(), true) + s.spin(350, 5350, 14, 1.1, s.fishBones());
  v += s.spin(1650, 5300, -4, 1.5, s.seaLizardFossil());
  find(around(1700, 5290, 1500, 420), "A fossil! This sea creature swam here millions of years ago.", { name: "the big fossil" });
  // a secret cave in the left rock, with a tunnel out to the open water
  v += `<path d="M2450,2880 C2900,2900 3200,2800 3600,2700" stroke="#135d99" stroke-width="170" fill="none" stroke-linecap="round"/>
    <ellipse cx="1650" cy="2840" rx="900" ry="340" fill="#104f88"/><ellipse cx="1650" cy="2900" rx="820" ry="270" fill="#0d4479"/>`;
  v += A(1000, 2680, 1.1, s.jellyfish("#ffc8dd")) + A(2100, 2700, 0.9, s.fish({ body: "#b388eb", fin: "#7b2cbf", belly: "#e0aaff" }), true);
  v += A(1500, 3150, 1.0, s.crab("#b388eb", "#7b2cbf"));
  // a snug hollow in the right rock for the clam
  v += `<ellipse cx="7250" cy="2440" rx="520" ry="230" fill="#104f88"/><path d="M6780,2500 C6500,2560 6200,2420 5800,2400" stroke="#135d99" stroke-width="130" fill="none" stroke-linecap="round"/>`;
  v += A(7300, 2640, 1.2, s.clam());
  find(around(7300, 2500, 420, 360), "A giant clam, keeping its pearl safe.", { name: "the giant clam" });
  // the little island on top of the left rock
  v += `<ellipse cx="960" cy="${SURF + 14}" rx="640" ry="170" fill="#f0d9a0"/><ellipse cx="900" cy="${SURF - 60}" rx="380" ry="60" fill="#f7e6b8"/>`;
  v += A(880, 560, 0.9, s.palm());
  find(around(930, 400, 620, 600), "A tiny island with one palm tree. Even the sea has a beach.", { name: "the island" });
  v += surface();
  v += A(4300, SURF + 8, 1.0, s.sailboat());
  find(around(4300, 460, 420, 420), "A little sailboat, way up on top of the water.", { name: "the sailboat" });
  v += s.spin(5650, 520, -24, 1.0, s.dolphin());
  v += `<g fill="none" stroke="#fff" stroke-width="10" opacity=".8"><ellipse cx="5440" cy="${SURF}" rx="110" ry="20"/><ellipse cx="5440" cy="${SURF}" rx="180" ry="32" opacity=".5"/></g>`;
  find(around(5650, 510, 540, 330), "Whee! A dolphin jumps for joy.", { name: "the dolphin" });

  // ---- the sea floor
  v += s.rock([[0, FLOOR], [1400, FLOOR + 20], [3300, FLOOR - 10], [5200, FLOOR + 14], [7000, FLOOR - 6], [W, FLOOR + 10], [W, H + 40], [0, H + 40]], 13, { fill: "#4b4663", shade: "#383350" });
  v += s.ledgeTop([[3320, FLOOR], [5200, FLOOR + 14], [7000, FLOOR - 4], [7700, FLOOR + 6]], "#625d80", 40);
  const r = k.rng(31);
  for (let i = 0; i < 26; i++) v += A(3400 + r() * 4200, FLOOR + 6 + r() * 30, 0.4 + r() * 0.8, k.rock("#3d3856", "#2e2a44"));

  // ---- sunny water
  v += s.school(3000, 1250, 18, 3, { colors: ["#ffd166", "#ffb703"], spreadX: 760, spreadY: 300, s: 1.5 });
  find(around(3000, 1250, 820, 340), "A whole school of fish, all turning together.", { name: "the school of fish" });
  v += s.school(7000, 1050, 12, 7, { colors: ["#90e0ef", "#caf0f8"], spreadX: 640, spreadY: 240, s: 1.3, flip: true });
  v += s.school(5050, 2380, 12, 11, { colors: ["#e9ecef", "#ced4da"], spreadX: 600, spreadY: 220, s: 1.3 });
  v += A(2350, 1680, 1.3, s.fish()) + A(3750, 1050, 1.2, s.fish({ body: "#48cae4", fin: "#0096c7", belly: "#caf0f8" }), true)
    + A(3320, 1830, 1.4, s.fish({ body: "#2ec4b6", fin: "#e76f51", belly: "#cbf3f0" }), true)
    + A(6050, 1320, 1.1, s.fish({ body: "#f72585", fin: "#b5179e", belly: "#ffd6ea" }));
  v += A(5100, 1350, 1.25, s.puffer());
  find(around(5100, 1350, 240, 240), "Puff! The pufferfish is as round as a ball.", { name: "the pufferfish" });
  v += s.bubbles(3900, 2700, 12, 4, 900) + s.bubbles(5350, 1800, 9, 6, 700) + s.bubbles(2820, 1750, 7, 9, 500);
  v += A(4350, 1800, 1.1, s.jellyfish("#ff8fab")) + A(4540, 1960, 0.9, s.jellyfish("#c8b6ff")) + A(4200, 2040, 0.8, s.jellyfish("#ffc8dd"));
  find(around(4370, 2010, 520, 600), "Jellyfish drift along like little umbrellas.", { name: "the jellyfish" });

  // ---- Finn's coral reef (on the left shelf)
  v += A(2250, 2120, 1.5, s.branchCoral("#ff6b9d", 2)) + A(2480, 2130, 1.0, s.brainCoral())
    + A(2630, 2132, 1.0, s.seagrass()) + A(2960, 2132, 0.9, s.seagrass("#52b788"))
    + A(3080, 2134, 1.3, s.fanCoral()) + A(3340, 2144, 1.2, s.tubeCoral()) + A(3480, 2156, 1.0, s.branchCoral("#ffa94d", 4));
  v += A(2780, 2134, 1.5, s.anemone());
  v += A(2800, 1900, 1.35, s.clownfish());
  find(around(2790, 1950, 430, 480),
    "“Psst! I’m Finn. Something is glowing far down in the dark. Will you go and see what it is? Ask Old Turtle the way.”",
    { story: 1, name: "Finn the clownfish" });
  v += A(2610, 2086, 0.95, s.starfish());
  find(around(2610, 2086, 170, 170), "A starfish, taking it easy on the sand.", { name: "the starfish" });
  v += A(3205, 2136, 0.9, s.crab());
  find(around(3205, 2070, 250, 190), "Click, clack. Crab waves his claws hello.", { name: "the crab" });

  // ---- Old Turtle's hill (the right seamount)
  v += A(6150, 1846, 1.2, s.fanCoral("#f77f00", "#ffb703")) + A(6480, 1816, 1.0, s.brainCoral("#ff8fab", "#e05780"))
    + A(6900, 1778, 1.4, s.branchCoral("#b388eb", 7)) + A(7350, 1736, 1.1, s.tubeCoral("#ffd166", "#e0a528"))
    + A(7720, 1698, 1.0, s.anemone("#95d5b2", "#d8f3dc", "#40916c")) + A(6700, 1798, 0.8, s.seagrass()) + A(7560, 1716, 0.9, s.seagrass("#52b788"))
    + A(7080, 1740, 0.7, s.starfish("#f72585", "#ffd6ea"));
  v += A(6650, 1470, 1.25, s.turtle(), true);
  find(around(6650, 1470, 580, 380),
    "Old Turtle blinks slowly. “A glow in the deep? Swim down through the kelp forest and ask the seahorses.”",
    { story: 2, name: "Old Turtle" });

  // ---- open water in the middle
  v += A(4500, 2860, 1.6, s.manta());
  find(around(4500, 2930, 1000, 600), "A manta ray glides by like a kite.", { name: "the manta ray" });
  v += A(4150, 3480, 1.5, s.shark(), true);
  find(around(4130, 3460, 860, 440), "A shark! Don’t worry, this one only smiles.", { name: "the shark" });

  // ---- the kelp forest
  const kelpAt = [4900, 5140, 5380, 5640, 6160, 6400, 6660, 6920, 7180, 7430, 7640];
  const groundAt = x => 3930 - (x - 4750) * 0.0165;
  // The rock roof over the forest, so no kelp grows up into it.
  const roofAt = x => x < 5820 ? 2300 : x < 6500 ? 2450 + (x - 5820) / 680 * 400 : x < 7350 ? 2850 + (x - 6500) / 850 * 150 : 3000 + (x - 7350) / 350 * 300;
  const kelpH = (x, want) => Math.max(180, Math.min(want, (groundAt(x) - roofAt(x) - 90) / 1.25));
  kelpAt.forEach((x, i) => { if (i % 2 === 0) v += A(x, groundAt(x), 1.25, s.kelp(kelpH(x, 620 + (i * 53) % 240), i + 2)); });
  v += A(5900, 3400, 1.5, s.seahorse()) + A(6120, 3520, 1.3, s.seahorse("#ff8fab", "#e05780"), true) + A(5760, 3640, 1.0, s.seahorse("#ffa94d", "#e8590c"));
  find(around(5940, 3500, 600, 560),
    "“We saw it!” squeak the seahorses. “The glow is down past the sunken ship. Octopus lives there.”",
    { story: 3, name: "the seahorses" });
  v += A(7330, 3888, 1.1, s.chest());
  find(around(7330, 3760, 320, 320), "Treasure! A chest full of shiny gold coins.", { name: "the treasure chest" });
  kelpAt.forEach((x, i) => { if (i % 2 === 1) v += A(x, groundAt(x), 1.25, s.kelp(kelpH(x, 580 + (i * 71) % 260), i + 20)); });
  v += s.school(6800, 3350, 8, 15, { colors: ["#ffd166"], spreadX: 500, spreadY: 200, s: 1.1, flip: true });
  v += A(4725, 4215, 1.2, s.eelPeek(), true);
  find(around(4650, 4195, 340, 240), "Peekaboo! An eel hides in the rocks.", { name: "the eel" });

  // ---- the sunken ship
  v += A(1900, 4734, 1.6, s.shipwreck());
  v += A(2340, 4330, 1.15, s.octopus());
  find(around(2340, 4370, 640, 580),
    "Octopus points with four arms at once. “Down there, where it gets dark! Listen for the whale’s song.”",
    { story: 4, name: "Octopus" });
  v += A(3300, 4764, 1.0, s.anchor());
  find(around(3290, 4610, 300, 340), "A lost anchor from the old ship.", { name: "the anchor" });
  v += A(980, 4690, 1.0, s.bottle());
  find(around(985, 4690, 240, 190), "A message in a bottle. Who sent it, do you think?", { name: "the bottle" });
  v += A(1250, 4706, 0.8, s.seagrass("#40916c")) + A(3050, 4756, 0.7, s.seagrass("#40916c")) + A(2900, 4750, 0.8, s.crab("#f4a261", "#e76f51"));
  v += s.school(1500, 4250, 9, 21, { colors: ["#90e0ef"], spreadX: 520, spreadY: 200, s: 1.1 });
  v += s.bubbles(2050, 4300, 8, 12, 600);

  // ---- the deep
  v += A(4220, FLOOR + 8, 1.3, s.vent()) + A(4570, FLOOR + 12, 0.9, s.vent());
  find(around(4340, 5480, 680, 660), "Warm water bubbles up here from deep inside the Earth. Tube worms love it.", { name: "the warm vents" });
  v += A(5750, 5080, 2.0, s.whale());
  find(around(5750, 5090, 1720, 720),
    "The whale sings, low and slow. “Keep going, little diver. You are nearly at the bottom.”",
    { story: 5, name: "the whale" });
  v += A(5320, FLOOR + 2, 0.8, s.crab("#e9ecef", "#adb5bd"));
  v += A(7200, 5500, 1.4, s.angler(), true);

  v += depth();

  // ---- things that make their own light
  v += A(1050, 3120, 1.5, s.crystals(3)) + A(2000, 3140, 1.8, s.crystals(5, ["#c77dff", "#7df9ff", "#b9fbc0"])) + A(1550, 2540, 1.2, `<g transform="scale(1 -1)">${s.crystals(8, ["#ff8fab", "#7df9ff", "#c77dff"])}</g>`)
    + A(2450, 3060, 1.1, s.crystals(11));
  find(around(1650, 2850, 1750, 700), "A secret cave, sparkling with crystals!", { name: "the crystal cave" });
  for (let i = 0; i < 150; i++) {
    const x = 3400 + r() * 4500, y = 4500 + r() * 1500;
    v += `<circle cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" r="${(3 + r() * 6).toFixed(1)}" fill="#bff3ff" opacity="${(0.25 + r() * 0.5).toFixed(2)}"/>`;
  }
  v += A(7200, 5500, 1.4, s.anglerLight(), true);
  find(around(7090, 5390, 820, 640),
    "You found it! It is Angler’s lantern. “I keep it shining,” she says, “so that nobody down here ever feels lost.”",
    { story: 6, name: "the glowing light" });
  v += A(3420, 3950, 1.2, s.jellyfish("#9bf6ff", true)) + A(3900, 5150, 1.0, s.jellyfish("#ffadf0", true)) + A(6700, 4760, 0.8, s.jellyfish("#b9fbc0", true));
  find(around(3420, 4050, 320, 460), "Some jellyfish make their own light.", { name: "the glowing jellyfish" });
  v += A(4250, 4480, 1.1, s.subBeam()) + A(4250, 4480, 1.1, s.submarine());
  find(around(4250, 4450, 560, 320), "A little submarine has come exploring too.", { name: "the submarine" });

  // Story steps first (in order), then extras from the top of the sea to the bottom.
  const story = hot.filter(h => h.story).sort((a, b) => a.story - b.story);
  const extras = hot.filter(h => !h.story).sort((a, b) => a.box.y - b.box.y);
  return { svg: v, hotspots: [...story, ...extras] };
}

function page1() {
  const { svg, hotspots } = scene();
  return {
    svg, width: W, height: H, tiled: true,
    mode: "explore",
    guidance: { hints: true, showMe: true, map: true },
    stops: [
      { box: corners(2900, 120, 5900, 1700), style: "none",
        caption: "Far out at sea, the sun sparkles on the water. But what is down below?" },
      { box: corners(2150, 1450, 3550, 2450), style: "none",
        caption: "Splash! Down we go, to the coral reef. Somebody is waving." },
    ],
    hotspots,
  };
}

function cover() {
  const CW = 1600, CH = 2400;
  let v = `<defs><linearGradient id="cvsea" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#63d3e8"/><stop offset=".45" stop-color="#1779ba"/><stop offset="1" stop-color="#06204a"/></linearGradient>
    <linearGradient id="cvray" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".22"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient></defs>
    <rect width="${CW}" height="${CH}" fill="url(#cvsea)"/>`;
  for (const [x, w] of [[200, 180], [620, 120], [980, 220], [1340, 140]]) v += `<path d="M${x},0 L${x + w},0 L${x + w - 260},1500 L${x - 420},1500 Z" fill="url(#cvray)"/>`;
  v += s.school(1150, 1050, 9, 4, { colors: ["#ffd166"], spreadX: 520, spreadY: 240, s: 1.6, flip: true });
  v += s.rock([[0, 2150], [520, 2080], [1100, 2200], [1600, 2120], [1600, 2440], [0, 2440]], 3, { fill: "#3d3856", shade: "#2e2a44" });
  v += A(330, 2120, 2.2, s.anemone()) + A(820, 2190, 1.6, s.branchCoral("#ff6b9d", 2)) + A(1180, 2190, 1.5, s.fanCoral());
  v += A(1330, 1900, 1.0, s.anglerLight(), true);
  v += s.bubbles(560, 1500, 8, 5, 600);
  v += A(700, 1280, 4.2, s.clownfish());
  const title = (y, t, size) => `<text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="${size}" fill="#06204a" stroke="#06204a" stroke-width="26" stroke-linejoin="round">${t}</text>
    <text x="${CW / 2}" y="${y}" text-anchor="middle" font-weight="800" font-size="${size}" fill="#fff8e1">${t}</text>`;
  v += title(330, "The Light", 250) + title(560, "in the Deep", 210);
  return { svg: v, width: CW, height: CH };
}

module.exports = {
  id: "deep-sea",
  title: "The Light in the Deep",
  author: "Sample book",
  focusStyle: "none",
  size: { width: W, height: H },
  pages: [page1],
  cover,
};
