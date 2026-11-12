/*
 * Sea-life kit for the big underwater scene: creatures, corals, kelp, rocks and wrecks.
 * Creatures face right with the origin at their centre, unless a comment says "ground":
 * those stand on the origin like the animals in kit.js.
 */
"use strict";
const { rng, at } = require("./kit");

let n = 0;
const nid = p => `sea${p}${++n}`;
const eye = (x, y, r = 10) => `<circle cx="${x}" cy="${y}" r="${r}" fill="#fff"/><circle cx="${x + r * 0.3}" cy="${y}" r="${r * 0.55}" fill="#1b1b1b"/><circle cx="${x + r * 0.5}" cy="${y - r * 0.25}" r="${r * 0.2}" fill="#fff"/>`;
const smile = (x, y, w = 14, c = "#1b1b1b") => `<path d="M${x},${y} q${w / 2},${w * 0.45} ${w},0" stroke="${c}" stroke-width="3.5" fill="none" stroke-linecap="round"/>`;

/** Place with a rotation as well. */
const spin = (x, y, deg, s, inner, flip = false) =>
  `<g transform="translate(${x} ${y}) rotate(${deg}) scale(${flip ? -s : s} ${s})">${inner}</g>`;

// ---------------------------------------------------------------- fish

function fish({ body = "#ffb703", fin = "#fb8500", belly = "#fff3c4" } = {}) {
  return `<path d="M-50,0 L-98,-38 Q-84,0 -98,38 Z" fill="${fin}"/>
    <path d="M-12,-32 Q8,-66 44,-38 Z" fill="${fin}"/><path d="M-8,30 Q4,54 28,34 Z" fill="${fin}"/>
    <ellipse rx="62" ry="36" fill="${body}"/>
    <path d="M-54,12 Q0,46 56,10 Q0,28 -54,12 Z" fill="${belly}" opacity=".8"/>
    <path d="M6,4 Q-12,20 4,30 Q20,20 6,4 Z" fill="${fin}"/>
    ${eye(36, -8)}${smile(46, 10)}`;
}

/** Finn: orange with white bands. */
function clownfish() {
  const c = nid("cf");
  return `<defs><clipPath id="${c}"><ellipse rx="62" ry="36"/></clipPath></defs>
    <path d="M-50,0 L-98,-38 Q-84,0 -98,38 Z" fill="#f77f00" stroke="#2b1b17" stroke-width="5" stroke-linejoin="round"/>
    <path d="M-12,-32 Q8,-66 44,-38 Z" fill="#f77f00" stroke="#2b1b17" stroke-width="4"/><path d="M-8,30 Q4,54 28,34 Z" fill="#f77f00" stroke="#2b1b17" stroke-width="4"/>
    <ellipse rx="62" ry="36" fill="#ff8c1a"/>
    <g clip-path="url(#${c})"><path d="M-34,-40 Q-22,0 -34,40 L-10,40 Q0,0 -10,-40 Z" fill="#fff" stroke="#2b1b17" stroke-width="4"/>
      <path d="M12,-40 Q22,0 12,40 L30,40 Q40,0 30,-40 Z" fill="#fff" stroke="#2b1b17" stroke-width="4"/></g>
    <path d="M2,6 Q-14,22 2,32 Q18,20 2,6 Z" fill="#f77f00" stroke="#2b1b17" stroke-width="3"/>
    ${eye(44, -8, 11)}${smile(48, 12, 12)}`;
}

function tinyFish(color) {
  return `<path d="M-22,0 L-44,-16 L-38,0 L-44,16 Z" fill="${color}"/><ellipse rx="28" ry="15" fill="${color}"/><circle cx="14" cy="-3" r="3.5" fill="#1b1b1b"/>`;
}

/** A loose group of small fish all heading the same way. */
function school(x, y, count, seed, { colors = ["#ffd166"], spreadX = 500, spreadY = 220, s = 1, flip = false } = {}) {
  const r = rng(seed);
  let out = "";
  for (let i = 0; i < count; i++) {
    const fx = x + (r() - 0.5) * spreadX, fy = y + (r() - 0.5) * spreadY * (1 - Math.abs((fx - x) / spreadX));
    out += at(fx, fy, s * (0.8 + r() * 0.5), tinyFish(colors[Math.floor(r() * colors.length)]), flip);
  }
  return out;
}

function puffer() {
  let spikes = "";
  for (let i = 0; i < 18; i++) {
    const a = (i / 18) * Math.PI * 2;
    spikes += `<path d="M${Math.cos(a - 0.08) * 58},${Math.sin(a - 0.08) * 58} L${Math.cos(a) * 80},${Math.sin(a) * 80} L${Math.cos(a + 0.08) * 58},${Math.sin(a + 0.08) * 58} Z" fill="#e0a528"/>`;
  }
  return `${spikes}<path d="M-56,-10 L-86,-26 L-80,6 Z" fill="#e0a528"/>
    <circle r="60" fill="#ffd166"/><path d="M-56,18 A60,60 0 0 0 56,18 Q0,34 -56,18 Z" fill="#fff8e1"/>
    <g fill="#c98f1e"><circle cx="-24" cy="-30" r="5"/><circle cx="6" cy="-42" r="4"/><circle cx="-38" cy="-8" r="4"/></g>
    ${eye(18, -12, 14)}${eye(46, -8, 11)}<ellipse cx="42" cy="18" rx="7" ry="9" fill="#7a3e1d"/>`;
}

// ---------------------------------------------------------------- bigger creatures

function turtle() {
  const S = "#5d9150", SD = "#3f6f3a", K = "#a3cf7a";
  return `<path d="M-110,30 Q-180,60 -190,110 Q-130,100 -90,56 Z" fill="${K}"/>
    <path d="M50,-36 Q150,-96 204,-146 Q210,-90 120,-40 Z" fill="${K}" opacity=".85"/>
    <path d="M-128,-6 l-46,-10 l10,26 Z" fill="${K}"/>
    <ellipse cx="170" cy="-6" rx="50" ry="36" fill="${K}"/><path d="M112,-10 L140,-30 L140,16 Z" fill="${K}"/>
    <ellipse rx="134" ry="88" fill="${SD}"/><ellipse cy="-6" rx="122" ry="76" fill="${S}"/>
    <g fill="none" stroke="${SD}" stroke-width="7" stroke-linejoin="round"><path d="M-40,-44 L0,-58 L40,-44 L40,10 L0,26 L-40,10 Z"/>
      <path d="M-40,-44 L-92,-34 M40,-44 L92,-34 M-40,10 L-96,24 M40,10 L96,24 M0,-58 L0,-82 M0,26 L0,68"/></g>
    <path d="M40,30 Q150,50 198,134 Q110,126 30,62 Z" fill="${K}"/>
    ${eye(188, -14, 10)}${smile(190, 8, 16, "#2f4f2a")}
    <g fill="${SD}" opacity=".5"><circle cx="150" cy="-22" r="5"/><circle cx="160" cy="8" r="4"/></g>`;
}

function seahorse(color = "#ffb703", dark = "#e08a00") {
  return `<path d="M-6,-14 q-34,-8 -40,14 q24,10 42,2 Z" fill="${dark}"/>
    <path d="M2,-58 C34,-30 34,22 8,52 C-16,80 -6,108 18,102 C34,96 30,76 16,78" stroke="${color}" stroke-width="30" fill="none" stroke-linecap="round"/>
    <g stroke="${dark}" stroke-width="4" stroke-linecap="round"><path d="M8,-30 h20 M12,-10 h20 M10,10 h18 M2,30 h16"/></g>
    <path d="M14,-76 L58,-68" stroke="${color}" stroke-width="13" stroke-linecap="round"/>
    <circle cx="0" cy="-80" r="25" fill="${color}"/>
    <path d="M-16,-98 l6,-18 l8,14 l8,-16 l6,18 Z" fill="${dark}"/>
    ${eye(6, -84, 8)}`;
}

function octopus(color = "#b565d9", dark = "#8e44ad") {
  const arms = [
    "M-70,20 C-150,30 -190,90 -250,70 C-290,56 -280,10 -250,14",
    "M-50,40 C-110,90 -120,160 -180,170 C-220,176 -230,140 -206,130",
    "M-22,52 C-50,120 -30,180 -70,220 C-96,244 -124,224 -110,200",
    "M0,56 C0,130 30,170 10,230 C0,258 -32,254 -30,228",
    "M24,52 C60,120 40,180 86,214 C112,232 134,208 118,190",
    "M50,40 C120,80 130,150 188,160 C226,166 234,128 208,122",
    "M70,20 C150,26 196,84 256,62 C292,48 284,4 254,10",
    "M60,0 C130,-20 170,-70 226,-76 C258,-78 262,-44 240,-40",
  ];
  return arms.map(d => `<path d="${d}" stroke="${color}" stroke-width="26" fill="none" stroke-linecap="round"/>`).join("")
    + arms.slice(1, 6).map(d => `<path d="${d}" stroke="#e9c9f7" stroke-width="5" fill="none" stroke-linecap="round" stroke-dasharray="1 16" opacity=".9"/>`).join("")
    + `<ellipse cy="-70" rx="104" ry="116" fill="${color}"/>
    <g fill="${dark}" opacity=".45"><circle cx="-50" cy="-140" r="14"/><circle cx="30" cy="-160" r="10"/><circle cx="62" cy="-120" r="8"/></g>
    <ellipse cx="-20" cy="-136" rx="40" ry="18" fill="#fff" opacity=".18"/>
    ${eye(-36, -38, 20)}${eye(36, -38, 20)}
    <path d="M-20,-4 q20,18 40,0" stroke="#4a235a" stroke-width="5" fill="none" stroke-linecap="round"/>
    <ellipse cx="-62" cy="-14" rx="14" ry="8" fill="#f0a9d0" opacity=".7"/><ellipse cx="62" cy="-14" rx="14" ry="8" fill="#f0a9d0" opacity=".7"/>`;
}

function whale() {
  const B = "#4d7fae", D = "#3a6690", L = "#d6e8f2";
  let grooves = "";
  for (let i = 0; i < 7; i++) grooves += `<path d="M${330 - i * 44},${78 + i * 3} Q${240 - i * 46},${118 + i * 2} ${130 - i * 50},${112 + i}" stroke="#b6cfdf" stroke-width="4" fill="none"/>`;
  return `<path d="M-360,-24 L-470,-124 Q-432,-40 -476,54 Z" fill="${D}"/>
    <path d="M-384,-18 C-250,-156 150,-172 334,-62 C406,-12 404,58 334,92 C150,160 -220,112 -384,-18 Z" fill="${B}"/>
    <path d="M394,44 C330,120 60,150 -150,96 C40,112 250,96 394,44 Z" fill="${L}"/>${grooves}
    <path d="M120,96 Q84,196 -14,214 Q40,138 56,84 Z" fill="${D}"/>
    <path d="M400,34 Q320,62 236,56" stroke="#2c4f70" stroke-width="5" fill="none" stroke-linecap="round"/>
    <circle cx="262" cy="14" r="13" fill="#fff"/><circle cx="266" cy="14" r="8" fill="#1b1b1b"/><circle cx="269" cy="11" r="3" fill="#fff"/>
    <g fill="#fff" opacity=".35"><circle cx="60" cy="-110" r="7"/><circle cx="100" cy="-96" r="5"/><circle cx="-80" cy="-100" r="6"/><circle cx="-200" cy="-70" r="5"/></g>
    <path d="M-250,-110 Q0,-176 250,-98" stroke="#fff" stroke-width="10" fill="none" opacity=".12" stroke-linecap="round"/>`;
}

function shark() {
  return `<path d="M-236,0 L-330,-112 L-290,-4 L-322,72 Z" fill="#7d93a6"/>
    <path d="M-20,-66 L44,-172 L84,-60 Z" fill="#7d93a6"/>
    <path d="M-250,0 C-150,-82 120,-92 264,-10 C150,64 -120,62 -250,0 Z" fill="#93a8ba"/>
    <path d="M-236,6 C-120,56 140,58 262,-8 C150,30 -100,36 -236,6 Z" fill="#f1f5f8"/>
    <path d="M60,30 L22,132 L134,40 Z" fill="#7d93a6"/>
    <g stroke="#6b8194" stroke-width="5" stroke-linecap="round"><path d="M96,-26 v30 M114,-28 v30 M132,-28 v28"/></g>
    ${eye(196, -22, 11)}
    <path d="M176,14 Q214,30 250,6" stroke="#44586a" stroke-width="5" fill="none" stroke-linecap="round"/>
    <path d="M190,19 l6,9 l6,-8 l6,8 l6,-9 l6,7 l6,-9" stroke="#fff" stroke-width="3" fill="none"/>`;
}

function manta() {
  return `<path d="M0,84 Q10,170 -10,250" stroke="#1d3557" stroke-width="7" fill="none" stroke-linecap="round"/>
    <path d="M0,-62 C120,-84 262,-22 306,44 C204,30 100,72 0,92 C-100,72 -204,30 -306,44 C-262,-22 -120,-84 0,-62 Z" fill="#274c77"/>
    <path d="M-40,-60 Q-56,-100 -30,-112 Q-22,-84 -18,-62 Z M40,-60 Q56,-100 30,-112 Q22,-84 18,-62 Z" fill="#274c77"/>
    <path d="M-150,10 Q0,50 150,10 Q0,76 -150,10 Z" fill="#e7f0f7" opacity=".85"/>
    <g fill="#e7f0f7" opacity=".7"><circle cx="-190" cy="6" r="9"/><circle cx="190" cy="6" r="9"/><circle cx="-120" cy="-24" r="6"/><circle cx="120" cy="-24" r="6"/></g>
    <circle cx="-34" cy="-44" r="6" fill="#fff"/><circle cx="34" cy="-44" r="6" fill="#fff"/>`;
}

/** Origin at the top of the bell. */
function jellyfish(color = "#ff8fab", glow = false) {
  const g = nid("jg");
  const tent = [-40, -20, 0, 20, 40].map((x, i) =>
    `<path d="M${x},6 q${i % 2 ? 16 : -16},40 0,80 q${i % 2 ? -16 : 16},40 0,${70 + i * 8}" stroke="${color}" stroke-width="7" fill="none" stroke-linecap="round" opacity=".75"/>`).join("");
  return (glow ? `<defs><radialGradient id="${g}"><stop offset="0" stop-color="${color}" stop-opacity=".7"/><stop offset="1" stop-color="${color}" stop-opacity="0"/></radialGradient></defs><circle cy="-10" r="190" fill="url(#${g})"/>` : "")
    + `${tent}<path d="M-64,4 C-64,-86 64,-86 64,4 Q44,18 22,6 Q0,18 -22,6 Q-44,18 -64,4 Z" fill="${color}" opacity=".9"/>
    <path d="M-40,-30 Q-20,-64 16,-62" stroke="#fff" stroke-width="7" fill="none" stroke-linecap="round" opacity=".6"/>
    <circle cx="-16" cy="-22" r="5" fill="#1b1b1b"/><circle cx="16" cy="-22" r="5" fill="#1b1b1b"/>${smile(-7, -12, 14)}`;
}

/** Ground. */
function crab(color = "#e5383b", dark = "#b02a2c") {
  return `<g stroke="${dark}" stroke-width="10" stroke-linecap="round" fill="none"><path d="M-50,-30 l-40,10 l-10,24 M-56,-40 l-44,-6 l-18,20 M50,-30 l40,10 l10,24 M56,-40 l44,-6 l18,20"/></g>
    <path d="M-52,-56 Q-96,-80 -90,-116" stroke="${color}" stroke-width="14" fill="none" stroke-linecap="round"/>
    <path d="M52,-56 Q96,-80 90,-116" stroke="${color}" stroke-width="14" fill="none" stroke-linecap="round"/>
    <path d="M-90,-116 m-26,0 a26,26 0 1 1 40,-20 l-22,16 l28,4 a26,26 0 0 1 -46,0 Z" fill="${color}"/>
    <path d="M90,-116 m26,0 a26,26 0 1 0 -40,-20 l22,16 l-28,4 a26,26 0 0 0 46,0 Z" fill="${color}"/>
    <ellipse cy="-42" rx="66" ry="42" fill="${color}"/>
    <path d="M-20,-78 v-24 M20,-78 v-24" stroke="${dark}" stroke-width="7"/>
    ${eye(-20, -106, 10)}${eye(20, -106, 10)}${smile(-12, -36, 24)}`;
}

function starfish(color = "#ff7b54", dot = "#ffd3b6") {
  const pts = [];
  for (let i = 0; i < 10; i++) {
    const a = (i / 10) * Math.PI * 2 - Math.PI / 2, r = i % 2 ? 26 : 66;
    pts.push(`${Math.cos(a) * r},${Math.sin(a) * r}`);
  }
  return `<polygon points="${pts.join(" ")}" fill="${color}" stroke="${color}" stroke-width="16" stroke-linejoin="round"/>
    <g fill="${dot}"><circle r="6"/><circle cy="-34" r="4"/><circle cx="32" cy="-10" r="4"/><circle cx="-32" cy="-10" r="4"/><circle cx="20" cy="28" r="4"/><circle cx="-20" cy="28" r="4"/></g>
    <circle cx="-9" cy="-6" r="3.5" fill="#1b1b1b"/><circle cx="9" cy="-6" r="3.5" fill="#1b1b1b"/>${smile(-7, 4, 14)}`;
}

/** An eel looking out of a hole in the rock. Origin at the hole. */
function eelPeek() {
  return `<ellipse rx="64" ry="52" fill="#141222"/>
    <path d="M-10,14 C40,6 70,-28 116,-36" stroke="#5f8f4e" stroke-width="46" fill="none" stroke-linecap="round"/>
    <path d="M-6,24 C44,18 76,-14 118,-24" stroke="#cfe3a4" stroke-width="12" fill="none" stroke-linecap="round" opacity=".7"/>
    <g fill="#3f6b37"><circle cx="40" cy="-4" r="6"/><circle cx="70" cy="-28" r="5"/><circle cx="20" cy="2" r="4"/></g>
    ${eye(122, -48, 10)}<path d="M112,-28 Q132,-20 148,-32" stroke="#1b1b1b" stroke-width="4" fill="none" stroke-linecap="round"/>`;
}

function dolphin() {
  return `<path d="M-196,50 L-272,18 L-250,56 L-268,98 Z" fill="#6a8fb5"/>
    <path d="M-10,-78 L12,-142 L52,-74 Z" fill="#6a8fb5"/>
    <path d="M-204,52 C-140,-70 80,-112 190,-32 Q216,-12 232,6 Q200,12 180,8 C80,14 -80,22 -204,52 Z" fill="#7fa3c7"/>
    <path d="M-190,50 C-80,24 80,16 196,6 C90,34 -80,40 -190,50 Z" fill="#e3eef6"/>
    <path d="M60,-2 L38,62 L104,8 Z" fill="#6a8fb5"/>
    ${eye(158, -24, 8)}<path d="M180,2 Q204,8 224,2" stroke="#3f5f80" stroke-width="4" fill="none" stroke-linecap="round"/>`;
}

/** The anglerfish, without her light (draw anglerLight on top of any darkness). */
function angler() {
  let teeth = "";
  for (let i = 0; i < 7; i++) teeth += `<path d="M${78 + i * 14},${24 + Math.sin(i / 6 * Math.PI) * 22} l7,-20 l7,20 Z" fill="#f5f5f0"/>`;
  return `<path d="M-160,0 L-244,-64 L-222,0 L-244,64 Z" fill="#2c2347"/>
    <path d="M-60,-104 l14,-44 l22,36 l18,-40 l20,44 Z" fill="#2c2347"/>
    <path d="M-170,0 C-150,-112 60,-132 160,-42 C192,-12 192,32 160,62 C60,132 -150,112 -170,0 Z" fill="#43366b"/>
    <path d="M-150,30 C-60,104 60,112 150,60 C60,90 -60,84 -150,30 Z" fill="#5a4a8a" opacity=".7"/>
    <path d="M70,18 Q130,62 178,8 L178,34 Q130,100 58,50 Z" fill="#1a1330"/>${teeth}
    <path d="M-20,30 Q-50,60 -30,84 Q0,66 -20,30 Z" fill="#2c2347"/>
    <circle cx="92" cy="-42" r="20" fill="#fff"/><circle cx="97" cy="-42" r="11" fill="#1b1b1b"/><circle cx="101" cy="-46" r="4" fill="#fff"/>
    <ellipse cx="56" cy="-12" rx="14" ry="8" fill="#f0a9d0" opacity=".5"/>
    <path d="M56,-112 C80,-180 150,-210 212,-190" stroke="#43366b" stroke-width="10" fill="none" stroke-linecap="round"/>`;
}

function anglerLight() {
  const g = nid("al");
  return `<defs><radialGradient id="${g}"><stop offset="0" stop-color="#fff7b0" stop-opacity=".95"/><stop offset=".3" stop-color="#ffe680" stop-opacity=".45"/>
    <stop offset="1" stop-color="#ffe680" stop-opacity="0"/></radialGradient></defs>
    <circle cx="216" cy="-192" r="330" fill="url(#${g})"/><circle cx="216" cy="-192" r="24" fill="#fffde6"/>`;
}

// ---------------------------------------------------------------- plants and corals (ground)

function kelp(height, seed, { stem = "#5c7a29", leaf = "#86a832", leaf2 = "#a3c244" } = {}) {
  const r = rng(seed);
  const sway = 26 + r() * 20, phase = r() * 6;
  let d = "M0,0", leaves = "";
  for (let y = 60; y <= height; y += 60) {
    const x = Math.sin(y / 150 + phase) * sway;
    d += ` L${x.toFixed(1)},${-y}`;
    const side = (y / 60) % 2 ? 1 : -1;
    leaves += `<ellipse cx="${x + side * 46}" cy="${-y + 8}" rx="54" ry="14" transform="rotate(${side * -28} ${x + side * 46} ${-y + 8})" fill="${r() > 0.5 ? leaf : leaf2}"/>
      <circle cx="${x + side * 6}" cy="${-y + 2}" r="8" fill="${leaf2}"/>`;
  }
  return `${leaves}<path d="${d}" stroke="${stem}" stroke-width="13" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`;
}

function seagrass(color = "#4f9d69") {
  return `<g stroke="${color}" stroke-width="12" stroke-linecap="round" fill="none"><path d="M-40,0 q-20,-60 -4,-110"/><path d="M-14,0 q14,-80 -6,-150"/>
    <path d="M10,0 q24,-60 10,-126"/><path d="M36,0 q30,-50 26,-96"/><path d="M-60,0 q-24,-40 -30,-74"/></g>`;
}

function branchCoral(color = "#ff6b9d", seed = 1) {
  const r = rng(seed);
  let out = "";
  const grow = (x, y, ang, len, depth) => {
    const x2 = x + Math.sin(ang) * len, y2 = y - Math.cos(ang) * len;
    out += `<path d="M${x.toFixed(1)},${y.toFixed(1)} L${x2.toFixed(1)},${y2.toFixed(1)}" stroke-width="${8 + depth * 7}"/>`;
    if (depth > 0) {
      grow(x2, y2, ang - 0.35 - r() * 0.3, len * 0.74, depth - 1);
      grow(x2, y2, ang + 0.35 + r() * 0.3, len * 0.74, depth - 1);
    } else out += `<circle cx="${x2.toFixed(1)}" cy="${y2.toFixed(1)}" r="9" fill="#fff" opacity=".5" stroke="none"/>`;
  };
  grow(0, 0, 0, 90, 3);
  return `<g stroke="${color}" stroke-linecap="round" fill="none">${out}</g>`;
}

function fanCoral(color = "#9d4edd", vein = "#c77dff") {
  let veins = "";
  for (let i = -5; i <= 5; i++) veins += `<path d="M0,-30 L${i * 26},${-206 + Math.abs(i) * 7}"/>`;
  return `<path d="M-10,0 h20 v-40 h-20 Z" fill="${color}"/>
    <path d="M0,-30 L-140,-170 A210,210 0 0 1 140,-170 Z" fill="${color}" opacity=".92"/>
    <g stroke="${vein}" stroke-width="5" fill="none" opacity=".9">${veins}<path d="M-96,-126 A150,150 0 0 1 96,-126 M-60,-90 A100,100 0 0 1 60,-90"/></g>`;
}

function brainCoral(color = "#ffd166", line = "#e0a528") {
  return `<path d="M-116,0 A116,96 0 0 1 116,0 Z" fill="${color}"/>
    <g stroke="${line}" stroke-width="7" fill="none" stroke-linecap="round"><path d="M-84,-20 q20,-30 40,-6 q20,24 40,-4 q20,-28 44,-2"/>
      <path d="M-60,-54 q18,-22 36,-4 q18,18 36,-4 q14,-16 30,-2"/><path d="M-30,-80 q16,-12 30,0 q14,10 30,0"/></g>`;
}

function tubeCoral(color = "#48cae4", dark = "#0096c7") {
  return [[-70, 96], [-28, 170], [16, 130], [58, 196], [100, 84]].map(([x, h]) =>
    `<rect x="${x - 22}" y="${-h}" width="44" height="${h}" rx="18" fill="${color}"/><ellipse cx="${x}" cy="${-h + 8}" rx="16" ry="9" fill="${dark}"/>`).join("");
}

/** Finn's home. */
function anemone(color = "#ff8fab", tip = "#ffe5ec", base = "#c9516a") {
  let t = "";
  for (let i = 0; i < 15; i++) {
    const x = -70 + i * 10, lean = (i - 7) * 7, h = 130 + (i % 3) * 22;
    t += `<path d="M${x},-66 q${lean * 0.3 + (i % 2 ? 14 : -14)},${-h / 2} ${lean},${-h}" stroke="${color}" stroke-width="15" fill="none" stroke-linecap="round"/>
      <circle cx="${x + lean}" cy="${-66 - h}" r="9" fill="${tip}"/>`;
  }
  return `${t}<path d="M-84,0 Q-74,-64 -62,-76 L62,-76 Q74,-64 84,0 Z" fill="${base}"/><ellipse cy="-74" rx="64" ry="14" fill="#a93a54"/>`;
}

// ---------------------------------------------------------------- things people left behind

/** Ground. Open chest of gold. */
function chest() {
  const g = nid("cg");
  return `<defs><radialGradient id="${g}"><stop offset="0" stop-color="#ffe680" stop-opacity=".8"/><stop offset="1" stop-color="#ffe680" stop-opacity="0"/></radialGradient></defs>
    <circle cy="-120" r="210" fill="url(#${g})"/>
    <path d="M-112,-110 L-124,-208 Q0,-250 124,-208 L112,-110 Z" fill="#7a4a21"/><path d="M-118,-160 Q0,-200 118,-160" stroke="#e0b030" stroke-width="12" fill="none"/>
    <g fill="#ffd23f" stroke="#c9981a" stroke-width="3"><circle cx="-60" cy="-116" r="20"/><circle cx="-20" cy="-128" r="22"/><circle cx="24" cy="-122" r="21"/><circle cx="64" cy="-114" r="19"/><circle cx="0" cy="-108" r="20"/></g>
    <rect x="-112" y="-110" width="224" height="110" rx="10" fill="#8f5a2b"/>
    <rect x="-112" y="-110" width="224" height="20" fill="#e0b030"/><rect x="-18" y="-110" width="36" height="110" fill="#e0b030"/>
    <rect x="-112" y="-20" width="224" height="20" rx="6" fill="#6b4320"/><circle cy="-58" r="12" fill="#6b4320"/>`;
}

function bottle() {
  return `<g transform="rotate(28)"><rect x="-70" y="-28" width="110" height="56" rx="24" fill="#b7e4c7" opacity=".85" stroke="#74c69d" stroke-width="5"/>
    <rect x="34" y="-12" width="44" height="24" fill="#b7e4c7" opacity=".85" stroke="#74c69d" stroke-width="5"/><rect x="74" y="-10" width="18" height="20" rx="4" fill="#b08968"/>
    <rect x="-50" y="-14" width="62" height="28" rx="4" fill="#fff3c4"/><path d="M-40,-4 h40 M-40,6 h30" stroke="#b08968" stroke-width="3"/></g>`;
}

/** Ground. */
function anchor() {
  return `<g transform="rotate(-18)" fill="none" stroke="#6c757d" stroke-linecap="round"><circle cy="-250" r="24" stroke-width="14"/>
    <path d="M0,-226 V-20" stroke-width="20"/><path d="M-70,-190 H70" stroke-width="18"/>
    <path d="M-110,-110 Q-110,-10 0,-10 Q110,-10 110,-110" stroke-width="20"/>
    <path d="M-110,-110 l-26,36 M-110,-110 l34,22 M110,-110 l26,36 M110,-110 l-34,22" stroke-width="14"/></g>`;
}

/** Ground. The old sunken ship, about 900 wide. */
function shipwreck() {
  const H = "#6f4e37", D = "#573b29", M = "#4a3323";
  let planks = "";
  for (let i = 1; i < 5; i++) planks += `<path d="M${-410 + i * 6},${-i * 38} Q0,${-i * 38 - 16} ${444 - i * 8},${-i * 36}" stroke="${D}" stroke-width="5" fill="none"/>`;
  return `<g transform="rotate(-5)">
    <path d="M430,-196 L580,-262" stroke="${M}" stroke-width="16" stroke-linecap="round"/>
    <path d="M0,-200 L24,-640" stroke="${M}" stroke-width="24" stroke-linecap="round"/>
    <path d="M-150,-470 L190,-496" stroke="${M}" stroke-width="14" stroke-linecap="round"/>
    <path d="M-140,-466 L176,-490 L150,-300 L96,-340 L60,-290 L10,-330 L-40,-286 L-100,-330 Z" fill="#e9dcc0" opacity=".85"/>
    <path d="M-60,-420 l30,40 M60,-440 l-20,50" stroke="#cbbd9c" stroke-width="5"/>
    <rect x="-8" y="-590" width="68" height="32" rx="6" fill="${H}"/>
    <path d="M24,-640 L24,-700 M24,-700 l70,18 l-22,16 l22,18 l-70,4" stroke="${M}" stroke-width="8" fill="#b23a48" stroke-linejoin="round"/>
    <path d="M226,-206 L236,-380 l-16,10 l8,-30 l-20,6" stroke="${M}" stroke-width="20" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <rect x="-404" y="-300" width="176" height="112" rx="8" fill="${H}"/><rect x="-412" y="-312" width="192" height="20" rx="6" fill="${D}"/>
    <rect x="-376" y="-270" width="46" height="46" rx="6" fill="#1b1410"/><rect x="-300" y="-270" width="46" height="46" rx="6" fill="#1b1410"/>
    <path d="M-430,0 L-402,-152 Q-384,-192 -332,-192 L332,-212 Q432,-202 472,-122 L420,0 Z" fill="${H}"/>${planks}
    <path d="M-430,0 L-420,-50 Q0,-74 444,-48 L420,0 Z" fill="${D}"/>
    <path d="M60,-96 l30,-34 l40,14 l36,-20 l34,30 l-10,44 l-50,22 l-56,-10 Z" fill="#1b1410"/>
    <g fill="#1b2a33" stroke="#c9a227" stroke-width="9"><circle cx="-300" cy="-112" r="24"/><circle cx="-190" cy="-116" r="24"/><circle cx="-80" cy="-120" r="24"/><circle cx="300" cy="-130" r="24"/></g>
    <g fill="#5f8f4e" opacity=".85"><ellipse cx="-340" cy="-194" rx="60" ry="14"/><ellipse cx="180" cy="-208" rx="80" ry="14"/><ellipse cx="-120" cy="-470" rx="40" ry="10"/><ellipse cx="380" cy="-190" rx="46" ry="12"/></g>
    <g fill="#d9d2c0"><circle cx="-380" cy="-60" r="7"/><circle cx="-356" cy="-40" r="5"/><circle cx="250" cy="-60" r="7"/><circle cx="230" cy="-36" r="5"/><circle cx="380" cy="-70" r="6"/></g>
  </g>`;
}

/** The little exploring submarine. */
function submarine() {
  return `<path d="M-200,0 l-56,-46 v92 Z" fill="#e07a00"/><rect x="-262" y="-12" width="16" height="24" fill="#6c757d"/>
    <path d="M-250,-60 q-30,60 0,120 q30,-60 0,-120" fill="#adb5bd"/>
    <rect x="-44" y="-128" width="96" height="64" rx="14" fill="#ff9f1c"/><path d="M10,-128 V-176 H44" stroke="#6c757d" stroke-width="12" fill="none" stroke-linecap="round"/>
    <rect x="-204" y="-74" width="408" height="148" rx="74" fill="#ffb347"/><rect x="-204" y="-10" width="408" height="84" rx="42" fill="#ff9f1c"/>
    <rect x="-190" y="-6" width="380" height="12" fill="#fff" opacity=".7"/>
    <g fill="#bde0fe" stroke="#e07a00" stroke-width="10"><circle cx="-110" cy="-22" r="30"/><circle cx="-20" cy="-22" r="30"/><circle cx="70" cy="-22" r="30"/></g>
    <g fill="#fff" opacity=".7"><circle cx="-120" cy="-32" r="8"/><circle cx="-30" cy="-32" r="8"/><circle cx="60" cy="-32" r="8"/></g>
    <circle cx="176" cy="24" r="18" fill="#fff7c2" stroke="#e07a00" stroke-width="6"/>`;
}

/** The submarine's headlamp beam (draw after any darkness so it glows). */
function subBeam() {
  const g = nid("sb");
  return `<defs><linearGradient id="${g}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#fff7c2" stop-opacity=".6"/><stop offset="1" stop-color="#fff7c2" stop-opacity="0"/></linearGradient></defs>
    <path d="M176,24 L620,260 L420,460 Z" fill="url(#${g})"/>`;
}

/** Ground. A deep-sea vent with tube worms. */
function vent() {
  const worms = [[-150, 90], [-120, 130], [-92, 70], [120, 110], [150, 76]].map(([x, h]) =>
    `<rect x="${x - 9}" y="${-h}" width="18" height="${h}" rx="8" fill="#f1f1e6"/><ellipse cx="${x}" cy="${-h - 12}" rx="13" ry="22" fill="#e5383b"/>`).join("");
  return `<g fill="#c9c2d6" opacity=".3"><circle cx="-10" cy="-330" r="50"/><circle cx="30" cy="-420" r="66"/><circle cx="-20" cy="-530" r="80"/><circle cx="40" cy="-660" r="94"/></g>
    <path d="M-80,0 L-46,-230 Q0,-262 46,-230 L80,0 Z" fill="#3b3348"/><path d="M40,0 L66,-150 Q92,-170 112,-150 L130,0 Z" fill="#332c40"/>
    <path d="M-40,-220 Q0,-200 40,-222" stroke="#ff8c42" stroke-width="10" fill="none" stroke-linecap="round" opacity=".8"/>${worms}`;
}

// ---------------------------------------------------------------- above the water

/** Sits on the waterline. */
function sailboat() {
  return `<path d="M0,-40 V-360" stroke="#6b4a2f" stroke-width="10"/>
    <path d="M10,-350 L10,-70 L170,-70 Z" fill="#fff"/><path d="M-10,-320 L-10,-70 L-130,-70 Z" fill="#ffe8b5"/>
    <path d="M0,-360 l50,14 l-50,14 Z" fill="#e63946"/>
    <path d="M-170,-50 L-130,14 L130,14 L180,-50 Z" fill="#d7263d"/><path d="M-170,-50 L180,-50 L172,-36 L-162,-36 Z" fill="#fff"/>`;
}

function seagull() {
  return `<path d="M-60,0 Q-30,-40 0,-8 Q30,-40 60,0" stroke="#fff" stroke-width="12" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M-60,0 Q-30,-40 0,-8 Q30,-40 60,0" stroke="#5c6f82" stroke-width="3" fill="none" stroke-linecap="round" opacity=".5"/>`;
}

/** Ground. */
function palm() {
  const fr = [-150, -100, -40, 30, 100, 150].map((a, i) =>
    `<path d="M40,-400 Q${40 + Math.sin(a / 57) * 150},${-470 + Math.abs(a) * 0.5} ${40 + Math.sin(a / 57) * 250},${-380 + Math.abs(a) * 0.9}" stroke="${i % 2 ? "#3f9d4f" : "#57b868"}" stroke-width="34" fill="none" stroke-linecap="round"/>`).join("");
  return `<path d="M-6,0 Q30,-200 40,-400" stroke="#8a5a2b" stroke-width="34" fill="none" stroke-linecap="round"/>
    <g stroke="#6b4320" stroke-width="5"><path d="M-6,-60 h34 M2,-140 h34 M12,-220 h34 M22,-300 h32"/></g>${fr}
    <circle cx="26" cy="-384" r="18" fill="#6b4320"/><circle cx="56" cy="-378" r="16" fill="#6b4320"/>`;
}


// ---------------------------------------------------------------- hidden in the rock

/** Spiral shell fossil. */
function ammonite(color = "#b9adb8") {
  let d = "M0,0";
  for (let t = 0; t < 26; t++) {
    const a = t * 0.55, r = 6 + t * 4.6;
    d += ` L${(Math.cos(a) * r).toFixed(1)},${(Math.sin(a) * r).toFixed(1)}`;
  }
  let ribs = "";
  for (let t = 14; t < 26; t++) {
    const a = t * 0.55, r1 = 6 + t * 4.6 - 16, r2 = 6 + t * 4.6 + 16;
    ribs += `<path d="M${Math.cos(a) * r1},${Math.sin(a) * r1} L${Math.cos(a) * r2},${Math.sin(a) * r2}"/>`;
  }
  return `<path d="${d}" stroke="${color}" stroke-width="30" fill="none" stroke-linecap="round" stroke-linejoin="round" opacity=".85"/>
    <g stroke="#7a6f80" stroke-width="5" opacity=".6">${ribs}</g>`;
}

/** Fish skeleton fossil, about 300 long. */
function fishBones(color = "#c4b9c2") {
  let ribs = "";
  for (let i = 0; i < 8; i++) { const x = -90 + i * 26, h = 46 - Math.abs(i - 3) * 7; ribs += `<path d="M${x},0 l-10,-${h} M${x},0 l-10,${h}"/>`; }
  return `<g stroke="${color}" stroke-width="9" stroke-linecap="round" fill="none" opacity=".85"><path d="M-130,0 H110"/>${ribs}
    <path d="M-130,0 l-46,-40 M-130,0 l-46,40"/></g>
    <path d="M100,-36 Q170,-30 176,0 Q170,30 100,36 Z" fill="${color}" opacity=".85"/><circle cx="134" cy="-8" r="9" fill="#6f6677"/>`;
}

/** A long fossil of an ancient sea reptile, about 900 long. */
function seaLizardFossil(color = "#c9bec6") {
  let ribs = "", spine = "M-380,20";
  for (let i = 0; i <= 20; i++) {
    const x = -380 + i * 34, y = Math.sin(i / 20 * Math.PI) * -40 + 20;
    spine += ` L${x},${y.toFixed(1)}`;
    if (i > 5 && i < 16) ribs += `<path d="M${x},${y.toFixed(1)} q-14,50 -4,${90 - Math.abs(i - 10) * 8}"/>`;
  }
  return `<g stroke="${color}" stroke-linecap="round" stroke-linejoin="round" fill="none" opacity=".9">
    <path d="${spine}" stroke-width="16"/><g stroke-width="9">${ribs}</g>
    <path d="M-180,10 l-50,90 l-40,10 M-150,0 l-30,100 M150,-10 l40,96 l44,6 M170,-10 l60,84" stroke-width="11"/>
    <path d="M-380,20 q-60,-10 -90,-60 M-380,20 q-60,20 -84,70" stroke-width="10"/></g>
    <path d="M300,-30 Q430,-50 520,-8 Q430,22 300,10 Z" fill="${color}" opacity=".9"/><circle cx="350" cy="-14" r="13" fill="#6f6677"/>
    <path d="M400,-2 l10,14 l10,-14 l10,14 l10,-14 l10,14 l10,-14 l10,12" stroke="#6f6677" stroke-width="4" fill="none"/>`;
}

/** Ground. A cluster of glowing crystals. */
function crystals(seed = 1, colors = ["#7df9ff", "#c77dff", "#ff8fab"]) {
  const r = rng(seed), g = nid("cr");
  let out = `<defs><radialGradient id="${g}"><stop offset="0" stop-color="${colors[0]}" stop-opacity=".55"/><stop offset="1" stop-color="${colors[0]}" stop-opacity="0"/></radialGradient></defs>
    <ellipse cy="-70" rx="230" ry="170" fill="url(#${g})"/>`;
  for (let i = 0; i < 6; i++) {
    const x = -110 + i * 44 + r() * 14, h = 80 + r() * 130, w = 22 + r() * 16, lean = (i - 2.5) * 14, c = colors[i % colors.length];
    out += `<path d="M${x - w},0 L${x - w + lean * 0.6},${-h * 0.8} L${x + lean},${-h} L${x + w + lean * 0.6},${-h * 0.8} L${x + w},0 Z" fill="${c}" opacity=".92"/>
      <path d="M${x},0 L${x + lean},${-h}" stroke="#fff" stroke-width="5" opacity=".6"/>`;
  }
  return out;
}

/** Ground. Giant clam holding a pearl. */
function clam() {
  const g = nid("pl");
  let scallop = "M-150,-10";
  for (let i = 0; i < 6; i++) scallop += ` q25,-34 50,0`;
  return `<defs><radialGradient id="${g}"><stop offset="0" stop-color="#fff" stop-opacity=".9"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient></defs>
    <path d="M-150,-10 Q-170,-170 0,-230 Q170,-170 150,-10 Z" fill="#c77dff"/>
    <g stroke="#e0aaff" stroke-width="8" fill="none"><path d="M-90,-30 Q-90,-150 0,-210 M-40,-30 Q-40,-140 0,-210 M40,-30 Q40,-140 0,-210 M90,-30 Q90,-150 0,-210"/></g>
    <ellipse cy="-14" rx="150" ry="30" fill="#5a189a"/><ellipse cy="-20" rx="120" ry="20" fill="#ffc8dd"/>
    <circle cy="-52" r="120" fill="url(#${g})"/><circle cy="-52" r="34" fill="#fffdf5"/><circle cx="-10" cy="-62" r="10" fill="#fff"/>
    <path d="${scallop} L150,30 Q0,64 -150,30 Z" fill="#9d4edd"/><path d="${scallop}" stroke="#e0aaff" stroke-width="8" fill="none"/>`;
}

// ---------------------------------------------------------------- water and rock

function bubbles(x, y, count, seed, height = 500) {
  const r = rng(seed);
  let out = "";
  for (let i = 0; i < count; i++) {
    const t = i / count, s = 8 + r() * 20;
    out += `<circle cx="${x + Math.sin(t * 9 + seed) * 40 + (r() - 0.5) * 30}" cy="${y - t * height}" r="${s}" fill="#fff" fill-opacity=".14" stroke="#fff" stroke-opacity=".6" stroke-width="3"/>`;
  }
  return out;
}

/**
 * A rock mass from a rough outline. Points are joined with small random bumps so it
 * doesn't look drawn with a ruler.
 */
function rock(points, seed, { fill = "#6f6677", shade = "#554d5e", light = "#8d8296" } = {}) {
  const r = rng(seed);
  const out = [];
  for (let i = 0; i < points.length; i++) {
    const a = points[i], b = points[(i + 1) % points.length];
    const len = Math.hypot(b[0] - a[0], b[1] - a[1]), steps = Math.max(1, Math.round(len / 150));
    for (let k = 0; k < steps; k++) {
      const t = k / steps, bump = k === 0 ? 0 : (r() - 0.5) * 46;
      const nx = -(b[1] - a[1]) / len, ny = (b[0] - a[0]) / len;
      out.push([a[0] + (b[0] - a[0]) * t + nx * bump, a[1] + (b[1] - a[1]) * t + ny * bump]);
    }
  }
  const d = "M" + out.map(p => `${p[0].toFixed(0)},${p[1].toFixed(0)}`).join(" L") + " Z";
  const id = nid("rk");
  let specks = "";
  const xs = out.map(p => p[0]), ys = out.map(p => p[1]);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  // layers of rock, like the stripes in a cliff
  for (let y = y0 + 120; y < y1; y += 210 + r() * 120) {
    let line = `M${x0 - 20},${y}`;
    for (let x = x0; x < x1; x += 400) line += ` q200,${-60 + r() * 120} 400,${-20 + r() * 40}`;
    specks += `<path d="${line}" stroke="${r() > 0.5 ? light : shade}" stroke-width="${26 + r() * 50}" fill="none" opacity=".28"/>`;
  }
  for (let i = 0; i < Math.round((x1 - x0) * (y1 - y0) / 90000); i++) {
    const w = 30 + r() * 70;
    specks += `<ellipse cx="${x0 + r() * (x1 - x0)}" cy="${y0 + r() * (y1 - y0)}" rx="${w}" ry="${w * 0.45}" fill="${shade}" opacity=".5"/>`;
  }
  return `<defs><clipPath id="${id}"><path d="${d}"/></clipPath></defs><path d="${d}" fill="${fill}"/><g clip-path="url(#${id})">${specks}</g>`;
}

/** A sandy or mossy cap along the top of a ledge. pts: left to right along the top. */
function ledgeTop(pts, color, width = 44) {
  const d = "M" + pts.map(p => p.join(",")).join(" L");
  return `<path d="${d}" stroke="${color}" stroke-width="${width}" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`;
}

module.exports = {
  spin, fish, clownfish, tinyFish, school, puffer, turtle, seahorse, octopus, whale, shark, manta, jellyfish, crab, starfish, eelPeek,
  dolphin, angler, anglerLight, kelp, seagrass, branchCoral, fanCoral, brainCoral, tubeCoral, anemone, chest, bottle, anchor,
  shipwreck, submarine, subBeam, vent, sailboat, seagull, palm, bubbles, rock, ledgeTop,
  ammonite, fishBones, seaLizardFossil, crystals, clam,
};
