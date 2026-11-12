/*
 * Illustration kit for the sample books: scenery and characters as SVG snippets.
 *
 * Every character is drawn facing right in its own small coordinate space with the
 * origin on the ground under it (y is negative going up), then placed with at().
 */
"use strict";

let uid = 0;
const nid = p => `${p}${++uid}`;

/** Small seeded random generator so the art comes out the same every run. */
function rng(seed) {
  let s = seed >>> 0;
  return () => (s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296;
}

/** Place a snippet: feet at (x, y), scaled, optionally facing left. */
const at = (x, y, s, inner, flip = false) =>
  `<g transform="translate(${x} ${y}) scale(${flip ? -s : s} ${s})">${inner}</g>`;

// ---------------------------------------------------------------- scenery

const SKIES = {
  hot: ["#7cc4ee", "#c9e6f2", "#fbe9b3"],
  day: ["#4fb0ea", "#a5daf7", "#e9f6ee"],
  golden: ["#6d5aa8", "#f08b62", "#ffd98a"],
  dusk: ["#141a44", "#3b3486", "#d9778a"],
};

function sky(W, H, kind) {
  const id = nid("sky"), c = SKIES[kind];
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="${c[0]}"/><stop offset=".6" stop-color="${c[1]}"/><stop offset="1" stop-color="${c[2]}"/>
  </linearGradient></defs><rect width="${W}" height="${H}" fill="url(#${id})"/>`;
}

function sun(cx, cy, r, { color = "#ffd94a", glow = "#fff3b0", rays = 0 } = {}) {
  const id = nid("sun");
  let out = `<defs><radialGradient id="${id}"><stop offset="0" stop-color="${glow}" stop-opacity=".95"/>
    <stop offset="1" stop-color="${glow}" stop-opacity="0"/></radialGradient></defs>
    <circle cx="${cx}" cy="${cy}" r="${r * 2.6}" fill="url(#${id})"/>`;
  for (let i = 0; i < rays; i++) {
    const a = (i / rays) * Math.PI * 2, r1 = r * 1.25, r2 = r * (i % 2 ? 1.55 : 1.8);
    out += `<line x1="${cx + Math.cos(a) * r1}" y1="${cy + Math.sin(a) * r1}" x2="${cx + Math.cos(a) * r2}" y2="${cy + Math.sin(a) * r2}"
      stroke="${color}" stroke-width="${r * 0.09}" stroke-linecap="round" opacity=".9"/>`;
  }
  return out + `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${color}"/>
    <circle cx="${cx - r * 0.25}" cy="${cy - r * 0.25}" r="${r * 0.55}" fill="#fff" opacity=".18"/>`;
}

function moon(cx, cy, r) {
  const id = nid("moon");
  return `<defs><radialGradient id="${id}"><stop offset="0" stop-color="#fff8d8" stop-opacity=".7"/>
    <stop offset="1" stop-color="#fff8d8" stop-opacity="0"/></radialGradient></defs>
    <circle cx="${cx}" cy="${cy}" r="${r * 2.8}" fill="url(#${id})"/>
    <circle cx="${cx}" cy="${cy}" r="${r}" fill="#fff6d6"/>
    <circle cx="${cx - r * 0.3}" cy="${cy - r * 0.2}" r="${r * 0.18}" fill="#eadfb4"/>
    <circle cx="${cx + r * 0.25}" cy="${cy + r * 0.3}" r="${r * 0.12}" fill="#eadfb4"/>`;
}

function stars(W, H, n, seed) {
  const r = rng(seed);
  let out = "";
  for (let i = 0; i < n; i++) {
    out += `<circle cx="${r() * W}" cy="${r() * H}" r="${2 + r() * 4}" fill="#fff" opacity="${0.4 + r() * 0.6}"/>`;
  }
  return out;
}

function cloud(x, y, s, opacity = 0.92) {
  return at(x, y, s, `<g fill="#fff" opacity="${opacity}">
    <ellipse cx="0" cy="0" rx="150" ry="46"/><ellipse cx="-70" cy="-30" rx="80" ry="52"/>
    <ellipse cx="30" cy="-52" rx="96" ry="66"/><ellipse cx="110" cy="-22" rx="70" ry="44"/></g>`);
}

/** A rolling band of hills across the picture. */
function hills(W, yBase, amp, color, seed, H) {
  const r = rng(seed), n = Math.max(6, Math.round(W / 400));
  const pts = [];
  for (let i = 0; i <= n; i++) pts.push([(i / n) * W, yBase - r() * amp]);
  let d = `M0,${H} L0,${pts[0][1]}`;
  for (let i = 1; i < pts.length; i++) {
    const [px, py] = pts[i - 1], [x, y] = pts[i];
    d += ` Q${px + (x - px) * 0.5},${py - amp * 0.25 * (i % 2 ? 1 : -0.4)} ${x},${y}`;
  }
  return `<path d="${d} L${W},${H} Z" fill="${color}"/>`;
}

function ground(W, H, horizon, top, bottom) {
  const id = nid("gr");
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient></defs>
    <rect y="${horizon}" width="${W}" height="${H - horizon}" fill="url(#${id})"/>`;
}

function tuft(x, y, s, color) {
  return at(x, y, s, `<g stroke="${color}" stroke-width="6" stroke-linecap="round" fill="none">
    <path d="M0,0 q-10,-34 -24,-46"/><path d="M0,0 q-2,-40 -4,-62"/><path d="M0,0 q8,-36 20,-50"/>
    <path d="M-8,0 q-16,-20 -34,-24"/><path d="M8,0 q14,-22 32,-28"/></g>`);
}

/** Grass tufts scattered over a band of ground, smaller towards the horizon. */
function grass(x0, x1, y0, y1, n, colors, seed, skip) {
  const r = rng(seed);
  const items = [];
  for (let i = 0; i < n; i++) {
    const x = x0 + r() * (x1 - x0), t = r(), y = y0 + t * (y1 - y0);
    const c = colors[Math.floor(r() * colors.length)];
    if (skip && skip(x, y)) continue;
    items.push([y, tuft(x, y, 0.5 + t * 1.1, c)]);
  }
  return items.sort((a, b) => a[0] - b[0]).map(i => i[1]).join("");
}

function acacia({ leaf = "#5d8f3f", leafLight = "#7fb04f", leafDark = "#446e2e", trunk = "#6e4b2d" } = {}) {
  return `<ellipse cx="0" cy="4" rx="150" ry="16" fill="#000" opacity=".12"/>
    <path d="M-22,0 C-16,-90 -34,-170 -8,-250 L10,-250 C24,-170 14,-90 26,0 Z" fill="${trunk}"/>
    <g stroke="${trunk}" stroke-linecap="round" fill="none">
      <path d="M0,-215 C-40,-270 -110,-290 -175,-330" stroke-width="18"/>
      <path d="M2,-225 C40,-280 110,-300 185,-345" stroke-width="18"/>
      <path d="M0,-240 C0,-290 10,-330 20,-365" stroke-width="15"/>
      <path d="M-95,-292 C-110,-320 -100,-340 -95,-352" stroke-width="10"/>
    </g>
    <ellipse cx="-150" cy="-338" rx="180" ry="46" fill="${leafDark}"/>
    <ellipse cx="170" cy="-352" rx="200" ry="50" fill="${leafDark}"/>
    <ellipse cx="10" cy="-392" rx="230" ry="56" fill="${leafDark}"/>
    <ellipse cx="-150" cy="-352" rx="176" ry="40" fill="${leaf}"/>
    <ellipse cx="170" cy="-367" rx="196" ry="44" fill="${leaf}"/>
    <ellipse cx="10" cy="-408" rx="226" ry="50" fill="${leaf}"/>
    <ellipse cx="-30" cy="-424" rx="150" ry="24" fill="${leafLight}" opacity=".8"/>
    <ellipse cx="140" cy="-380" rx="110" ry="18" fill="${leafLight}" opacity=".7"/>
    <ellipse cx="-190" cy="-364" rx="90" ry="16" fill="${leafLight}" opacity=".7"/>`;
}

function rock(color = "#9b8f86", shade = "#7d726b") {
  return `<path d="M-70,0 C-80,-40 -40,-70 0,-66 C46,-70 78,-38 70,0 Z" fill="${color}"/>
    <path d="M-70,0 C-60,-14 40,-18 70,0 Z" fill="${shade}"/>
    <path d="M-30,-50 q20,-12 44,-4" stroke="#fff" stroke-width="5" opacity=".25" fill="none" stroke-linecap="round"/>`;
}

function mound(color = "#b98652", shade = "#946538") {
  return `<path d="M-120,0 C-100,-90 -60,-190 -20,-250 C0,-280 30,-270 40,-230 C60,-150 100,-70 130,0 Z" fill="${color}"/>
    <path d="M10,-265 C30,-200 60,-90 130,0 L60,0 C40,-90 20,-190 10,-265 Z" fill="${shade}" opacity=".6"/>
    <ellipse cx="-20" cy="-110" rx="14" ry="20" fill="#5a3a1c"/>`;
}

function bush(color = "#6b9644", dark = "#4e7632") {
  return `<ellipse cx="0" cy="-34" rx="110" ry="50" fill="${dark}"/>
    <ellipse cx="-50" cy="-52" rx="62" ry="46" fill="${color}"/><ellipse cx="40" cy="-60" rx="74" ry="52" fill="${color}"/>
    <ellipse cx="0" cy="-40" rx="90" ry="34" fill="${color}"/>`;
}

function reeds(color = "#4f7f3a", head = "#7a4e2a") {
  let out = "";
  for (const [x, h, lean] of [[-40, 150, -14], [-14, 210, -4], [10, 180, 8], [36, 230, 4], [60, 140, 18]]) {
    out += `<path d="M${x},0 Q${x + lean},-${h / 2} ${x + lean * 1.4},-${h}" stroke="${color}" stroke-width="9" fill="none" stroke-linecap="round"/>`;
    if (h > 170) out += `<ellipse cx="${x + lean * 1.3}" cy="-${h - 10}" rx="10" ry="34" fill="${head}"/>`;
  }
  return out;
}

function waterhole(cx, cy, rx, ry, { top = "#63c3e6", bottom = "#2f86c0", mud = "#8b6a45", seed = 3 } = {}) {
  const id = nid("w"), r = rng(seed);
  let ripples = "";
  for (let i = 0; i < 9; i++) {
    const x = cx + (r() - 0.5) * rx * 1.5, y = cy + (r() - 0.5) * ry * 1.1, w = 60 + r() * 140;
    ripples += `<path d="M${x - w},${y} q${w},-14 ${w * 2},0" stroke="#fff" stroke-width="7" fill="none" opacity=".35" stroke-linecap="round"/>`;
  }
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient></defs>
    <ellipse cx="${cx}" cy="${cy + 6}" rx="${rx + 46}" ry="${ry + 30}" fill="${mud}"/>
    <ellipse cx="${cx}" cy="${cy}" rx="${rx + 18}" ry="${ry + 12}" fill="#a98758"/>
    <ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="url(#${id})"/>${ripples}`;
}

function lily() {
  return `<path d="M-44,0 A44,18 0 1 1 30,-12 L0,0 L36,6 A44,18 0 0 1 -44,0 Z" fill="#5fa24a"/>
    <path d="M-12,-8 q-6,-22 8,-24 q16,2 6,24 Z" fill="#f7b6d2"/><circle cx="-2" cy="-10" r="5" fill="#ffe27a"/>`;
}

// ---------------------------------------------------------------- characters

const TRUNKS = {
  relaxed: "M232,-205 C292,-196 300,-120 268,-62",
  droop: "M228,-198 C266,-176 268,-96 252,-34",
  drink: "M232,-205 C300,-190 320,-100 322,-6",
  up: "M232,-216 C302,-232 352,-300 322,-392",
};

/** Tembo the little elephant. trunk: relaxed | droop | drink | up. mood: happy | tired. */
function elephant({ trunk = "relaxed", mood = "happy", sweat = false } = {}) {
  const B = "#93a0b8", D = "#7b88a1", L = "#b3bdd0", P = "#f0a9b4", N = "#dfe5ef";
  const eye = mood === "tired"
    ? `<path d="M188,-243 q12,8 24,0" stroke="#2b2230" stroke-width="6" fill="none" stroke-linecap="round"/>`
    : `<circle cx="200" cy="-246" r="11" fill="#2b2230"/><circle cx="204" cy="-250" r="4" fill="#fff"/>`;
  const mouth = mood === "tired"
    ? `<path d="M196,-178 q12,-8 24,0" stroke="#4a4458" stroke-width="5" fill="none" stroke-linecap="round"/>`
    : `<path d="M194,-184 q14,14 28,0" stroke="#4a4458" stroke-width="5" fill="none" stroke-linecap="round"/>`;
  const drops = sweat ? `<g fill="#8fd3f4"><path d="M150,-330 q-12,20 0,28 q12,-8 0,-28Z"/><path d="M96,-318 q-10,16 0,23 q10,-7 0,-23Z"/>
      <path d="M205,-322 q-9,15 0,21 q9,-6 0,-21Z"/></g>` : "";
  const nails = x => `<g fill="${N}"><ellipse cx="${x + 14}" cy="-4" rx="8" ry="6"/><ellipse cx="${x + 30}" cy="-3" rx="8" ry="6"/><ellipse cx="${x + 46}" cy="-4" rx="8" ry="6"/></g>`;
  return `<ellipse cx="30" cy="4" rx="200" ry="18" fill="#000" opacity=".14"/>
    <rect x="-108" y="-140" width="58" height="140" rx="24" fill="${D}"/>
    <rect x="62" y="-140" width="58" height="140" rx="24" fill="${D}"/>
    <path d="M-128,-222 C-168,-206 -172,-152 -160,-120" stroke="${D}" stroke-width="10" fill="none" stroke-linecap="round"/>
    <path d="M-166,-124 l-10,22 l14,-8 l6,14 l6,-22 Z" fill="#5d6880"/>
    <ellipse cx="10" cy="-196" rx="152" ry="108" fill="${B}"/>
    <ellipse cx="0" cy="-150" rx="120" ry="52" fill="#fff" opacity=".07"/>
    <rect x="-70" y="-136" width="60" height="136" rx="24" fill="${B}"/>
    <rect x="100" y="-136" width="60" height="136" rx="24" fill="${B}"/>
    ${nails(-70)}${nails(100)}
    <circle cx="166" cy="-226" r="90" fill="${B}"/>
    <path d="${TRUNKS[trunk]}" stroke="${B}" stroke-width="48" fill="none" stroke-linecap="round"/>
    <path d="M126,-312 C36,-346 4,-240 52,-170 C82,-132 136,-156 142,-204 C146,-240 142,-286 126,-312 Z" fill="${L}"/>
    <path d="M116,-290 C56,-306 38,-236 68,-190 C88,-164 122,-178 126,-210 C128,-238 126,-270 116,-290 Z" fill="${P}" opacity=".75"/>
    ${eye}
    <path d="M184,-268 q14,-10 30,-4" stroke="#6b7690" stroke-width="5" fill="none" stroke-linecap="round"/>
    <ellipse cx="204" cy="-214" rx="17" ry="11" fill="${P}" opacity=".7"/>
    ${mouth}${drops}`;
}

/** Zebra, facing right. */
function zebra({ head = "up" } = {}) {
  const clip = nid("zc"), legClip = nid("zl");
  const neck = head === "down"
    ? { poly: "M62,-214 L196,-150 L214,-104 L96,-126 Z", hx: 226, hy: -112, rot: 62, mane: "M72,-222 L196,-160" }
    : { poly: "M62,-214 L150,-312 L196,-270 L112,-140 Z", hx: 188, hy: -306, rot: 30, mane: "M58,-222 L146,-318" };
  const legs = [[-104, 0.86], [-62, 1], [58, 0.86], [100, 1]];
  let stripes = "";
  for (let i = -9; i < 12; i++) stripes += `<rect x="${i * 34}" y="-460" width="15" height="480" transform="skewX(-14)"/>`;
  let bands = "";
  for (let y = -120; y < -20; y += 24) bands += `<rect x="-140" y="${y}" width="300" height="10"/>`;
  const { hx, hy, rot } = neck;
  return `<ellipse cx="20" cy="4" rx="170" ry="15" fill="#000" opacity=".14"/>
    <defs>
      <clipPath id="${clip}"><ellipse cx="0" cy="-176" rx="128" ry="68"/><path d="${neck.poly}"/>
        <ellipse cx="${hx}" cy="${hy}" rx="56" ry="34" transform="rotate(${rot} ${hx} ${hy})"/></clipPath>
      <clipPath id="${legClip}">${legs.map(([x]) => `<rect x="${x}" y="-140" width="28" height="126" rx="10"/>`).join("")}</clipPath>
    </defs>
    <path d="M-124,-196 C-150,-180 -152,-140 -146,-112" stroke="#333" stroke-width="8" fill="none" stroke-linecap="round"/>
    <path d="M-150,-116 l-8,34 l12,-10 l8,12 l4,-34 Z" fill="#222"/>
    <g clip-path="url(#${legClip})"><rect x="-140" y="-140" width="300" height="140" fill="#f3f1ec"/><g fill="#222">${bands}</g></g>
    ${legs.map(([x]) => `<rect x="${x}" y="-18" width="28" height="18" rx="5" fill="#2a2a2a"/>`).join("")}
    <path d="${neck.mane}" stroke="#1e1e1e" stroke-width="13" stroke-linecap="round" stroke-dasharray="4 13"/>
    <g clip-path="url(#${clip})"><rect x="-200" y="-420" width="520" height="440" fill="#fbfaf6"/><g fill="#222">${stripes}</g>
      <ellipse cx="0" cy="-118" rx="120" ry="26" fill="#000" opacity=".08"/></g>
    <g transform="rotate(${rot} ${hx} ${hy})">
      <ellipse cx="${hx + 38}" cy="${hy + 2}" rx="26" ry="24" fill="#3a3a3a"/>
      <ellipse cx="${hx + 48}" cy="${hy - 4}" rx="4" ry="3" fill="#111"/>
      <path d="M${hx - 34},${hy - 26} q-4,-34 16,-40 q12,18 4,40 Z" fill="#fbfaf6" stroke="#222" stroke-width="5"/>
      <circle cx="${hx + 2}" cy="${hy - 8}" r="8" fill="#1d1d1d"/><circle cx="${hx + 5}" cy="${hy - 11}" r="3" fill="#fff"/>
    </g>`;
}

/** Giraffe, facing right. pose: stand | drink (legs spread, head down to the water). */
function giraffe({ pose = "stand" } = {}) {
  const C = "#f4cd72", P = "#b9682a", D = "#dcae52", H = "#5a3a22";
  const drink = pose === "drink";
  const drop = drink ? 86 : 0;
  const hipY = -340 + drop;
  const bodyClip = nid("gb"), neckClip = nid("gn");
  const r = rng(11);
  const leg = (hx, fx, color) => `<path d="M${hx},${hipY + 20} L${fx},-14" stroke="${color}" stroke-width="28" stroke-linecap="round"/>
    <path d="M${fx},-16 L${fx},-4" stroke="${H}" stroke-width="30" stroke-linecap="round"/>`;
  let bodySpots = "", neckSpots = "";
  for (let i = 0; i < 16; i++) {
    bodySpots += `<ellipse cx="${-110 + r() * 230}" cy="${hipY - 60 + r() * 120}" rx="${18 + r() * 16}" ry="${14 + r() * 12}" transform="rotate(${r() * 60})" fill="${P}"/>`;
  }
  for (let i = 0; i < 12; i++) {
    const y = -20 - i * 36;
    neckSpots += `<rect x="${-30 + (i % 2) * 26 + r() * 6}" y="${y}" width="${22 + r() * 8}" height="${24 + r() * 6}" rx="9" fill="${P}"/>`;
  }
  const px = 84, py = hipY - 36, ang = drink ? 124 : 13;
  return `<ellipse cx="20" cy="4" rx="${drink ? 240 : 150}" ry="14" fill="#000" opacity=".14"/>
    <defs>
      <clipPath id="${bodyClip}"><ellipse cx="0" cy="${hipY}" rx="122" ry="70" transform="rotate(-8 0 ${hipY})"/></clipPath>
      <clipPath id="${neckClip}"><path d="M-36,14 L-12,-440 L26,-440 L40,14 Z"/></clipPath>
    </defs>
    ${leg(-52, drink ? -30 : -44, D)}${leg(58, drink ? -40 : 46, D)}
    <path d="M-116,${hipY - 10} C-140,${hipY + 10} -140,${hipY + 60} -134,${hipY + 96}" stroke="${D}" stroke-width="8" fill="none" stroke-linecap="round"/>
    <path d="M-138,${hipY + 92} l-6,34 l12,-8 l8,10 l2,-34 Z" fill="${H}"/>
    <ellipse cx="0" cy="${hipY}" rx="122" ry="70" transform="rotate(-8 0 ${hipY})" fill="${C}"/>
    <g clip-path="url(#${bodyClip})">${bodySpots}</g>
    ${leg(-84, drink ? -150 : -86, C)}${leg(84, drink ? 190 : 88, C)}
    <g transform="translate(${px} ${py}) rotate(${ang})">
      <path d="M-40,8 L-16,-430" stroke="${P}" stroke-width="16" stroke-linecap="round"/>
      <path d="M-36,14 L-12,-440 L26,-440 L40,14 Z" fill="${C}"/>
      <g clip-path="url(#${neckClip})">${neckSpots}</g>
      <g transform="translate(8 -452) rotate(${drink ? -34 : -24})">
        <path d="M-10,-26 l-6,-46" stroke="${H}" stroke-width="9" stroke-linecap="round"/><circle cx="-17" cy="-76" r="10" fill="${H}"/>
        <path d="M14,-28 l2,-46" stroke="${H}" stroke-width="9" stroke-linecap="round"/><circle cx="16" cy="-78" r="10" fill="${H}"/>
        <path d="M-34,-10 q-34,-18 -44,-2 q14,18 44,12 Z" fill="${C}" stroke="${D}" stroke-width="4"/>
        <ellipse cx="22" cy="0" rx="66" ry="34" fill="${C}"/>
        <ellipse cx="66" cy="8" rx="34" ry="26" fill="#f8e2a6"/>
        <ellipse cx="82" cy="2" rx="5" ry="4" fill="${H}"/>
        <path d="M58,22 q14,8 28,0" stroke="${H}" stroke-width="5" fill="none" stroke-linecap="round"/>
        <circle cx="16" cy="-8" r="10" fill="#2b1d12"/><circle cx="19" cy="-11" r="3.5" fill="#fff"/>
        <path d="M4,-22 l-6,-10 M14,-24 l-2,-12 M24,-22 l4,-10" stroke="#2b1d12" stroke-width="4" stroke-linecap="round"/>
        <ellipse cx="34" cy="14" rx="12" ry="8" fill="#f0a9b4" opacity=".6"/>
      </g>
    </g>`;
}

/** Little bird, facing right. mood: sad | happy (wings up). */
function bird({ mood = "happy", color = "#2a9dbb", belly = "#ffe9a8" } = {}) {
  const dark = "#1c7690";
  const wing = mood === "happy"
    ? `<path d="M-10,-42 C-44,-66 -58,-88 -42,-102 C-16,-92 0,-70 6,-46 Z" fill="${dark}"/>
       <path d="M-2,-48 C-2,-80 12,-100 32,-98 C34,-78 22,-58 10,-44 Z" fill="${dark}"/>`
    : `<path d="M-30,-44 C-34,-20 -14,-6 12,-14 C8,-34 -8,-46 -30,-44 Z" fill="${dark}"/>`;
  const eye = mood === "happy"
    ? `<path d="M28,-66 q7,-9 14,0" stroke="#1b1b1b" stroke-width="5" fill="none" stroke-linecap="round"/>`
    : `<circle cx="34" cy="-60" r="7" fill="#1b1b1b"/><circle cx="36" cy="-58" r="2.2" fill="#fff"/>
       <path d="M25,-63 q9,-7 18,-1 l0,-8 l-18,0 Z" fill="${color}"/>
       <path d="M24,-72 q8,-6 17,-9" stroke="#0f5f75" stroke-width="3" fill="none" stroke-linecap="round"/>
       <path d="M29,-49 q-6,10 0,14 q6,-4 0,-14Z" fill="#bfe8fb"/>`;
  return `<path d="M-6,-6 l-4,8 M10,-6 l4,8" stroke="#e08a1e" stroke-width="5" stroke-linecap="round"/>
    <path d="M-34,-30 l-40,-14 l8,22 l-16,6 l48,2 Z" fill="${dark}"/>
    <ellipse cx="0" cy="-34" rx="40" ry="32" fill="${color}"/>
    <ellipse cx="6" cy="-24" rx="26" ry="20" fill="${belly}"/>
    <circle cx="28" cy="${mood === "happy" ? -62 : -56}" r="24" fill="${color}"/>
    ${wing}
    <g transform="translate(0 ${mood === "happy" ? 0 : 6})">${eye}
      <path d="M50,-62 l24,6 l-24,8 Z" fill="#f29f1f"/>
      <ellipse cx="36" cy="-50" rx="7" ry="5" fill="#f0a9b4" opacity=".7"/></g>`;
}

function meerkat({ look = 0 } = {}) {
  const C = "#d2a676", D = "#a67c52";
  return `<ellipse cx="0" cy="2" rx="46" ry="8" fill="#000" opacity=".14"/>
    <path d="M18,-10 C60,-6 76,-30 70,-56" stroke="${D}" stroke-width="12" fill="none" stroke-linecap="round"/>
    <ellipse cx="0" cy="-78" rx="38" ry="80" fill="${C}"/>
    <ellipse cx="0" cy="-66" rx="24" ry="58" fill="#f0dcc0"/>
    <path d="M-30,-100 q-10,30 6,44 M30,-100 q10,30 -6,44" stroke="${D}" stroke-width="11" fill="none" stroke-linecap="round"/>
    <g transform="translate(${look} 0)">
      <ellipse cx="0" cy="-172" rx="34" ry="30" fill="${C}"/>
      <circle cx="-26" cy="-192" r="9" fill="${D}"/><circle cx="26" cy="-192" r="9" fill="${D}"/>
      <ellipse cx="-13" cy="-176" rx="10" ry="12" fill="#3a2a1c"/><ellipse cx="13" cy="-176" rx="10" ry="12" fill="#3a2a1c"/>
      <circle cx="-11" cy="-179" r="3" fill="#fff"/><circle cx="15" cy="-179" r="3" fill="#fff"/>
      <ellipse cx="0" cy="-158" rx="9" ry="7" fill="#f0dcc0"/><circle cx="0" cy="-162" r="4" fill="#3a2a1c"/>
    </g>`;
}

function tortoise() {
  return `<ellipse cx="0" cy="2" rx="96" ry="10" fill="#000" opacity=".14"/>
    <rect x="-66" y="-22" width="24" height="24" rx="9" fill="#8a9a56"/><rect x="34" y="-22" width="24" height="24" rx="9" fill="#8a9a56"/>
    <path d="M70,-40 q40,-22 50,6 q-8,22 -44,16 Z" fill="#9aab62"/>
    <circle cx="104" cy="-34" r="5" fill="#2b2b1e"/><path d="M98,-20 q8,6 16,0" stroke="#2b2b1e" stroke-width="3" fill="none"/>
    <path d="M-86,-16 C-86,-96 76,-96 80,-16 Z" fill="#7a5c36"/>
    <g fill="#a9824e"><path d="M-40,-70 l36,-10 l34,10 l-8,30 l-52,0 Z"/><path d="M-72,-28 l22,-34 l10,36 Z"/><path d="M40,-32 l8,-30 l22,34 Z"/></g>`;
}

function hippoEyes() {
  return `<ellipse cx="0" cy="6" rx="150" ry="20" fill="#fff" opacity=".25"/>
    <path d="M-110,6 C-100,-40 -40,-52 0,-40 C40,-52 100,-40 110,6 Z" fill="#8f7d93"/>
    <ellipse cx="-58" cy="-44" rx="18" ry="22" fill="#8f7d93"/><ellipse cx="58" cy="-44" rx="18" ry="22" fill="#8f7d93"/>
    <ellipse cx="-58" cy="-44" rx="8" ry="11" fill="#d7a3b4"/><ellipse cx="58" cy="-44" rx="8" ry="11" fill="#d7a3b4"/>
    <circle cx="-28" cy="-22" r="10" fill="#fff"/><circle cx="28" cy="-22" r="10" fill="#fff"/>
    <circle cx="-26" cy="-22" r="5" fill="#231a26"/><circle cx="30" cy="-22" r="5" fill="#231a26"/>
    <ellipse cx="-16" cy="2" rx="6" ry="4" fill="#5d4c61"/><ellipse cx="16" cy="2" rx="6" ry="4" fill="#5d4c61"/>`;
}

function owl() {
  return `<ellipse cx="0" cy="-64" rx="52" ry="66" fill="#8a6238"/>
    <ellipse cx="0" cy="-48" rx="32" ry="42" fill="#d9b98a"/>
    <g fill="#8a6238"><path d="M-10,-40 l10,10 l10,-10 M-14,-22 l14,12 l14,-12 M-10,-4 l10,10 l10,-10" stroke="#8a6238" stroke-width="5" fill="none"/></g>
    <path d="M-44,-110 l8,-34 l22,26 Z M44,-110 l-8,-34 l-22,26 Z" fill="#8a6238"/>
    <circle cx="-20" cy="-92" r="22" fill="#fff3c4"/><circle cx="20" cy="-92" r="22" fill="#fff3c4"/>
    <circle cx="-20" cy="-92" r="10" fill="#22180e"/><circle cx="20" cy="-92" r="10" fill="#22180e"/>
    <circle cx="-17" cy="-95" r="3.5" fill="#fff"/><circle cx="23" cy="-95" r="3.5" fill="#fff"/>
    <path d="M-8,-78 l8,16 l8,-16 Z" fill="#f2a22a"/>
    <path d="M-16,0 l0,10 M16,0 l0,10" stroke="#f2a22a" stroke-width="7" stroke-linecap="round"/>`;
}

/** Lion cub peeking over grass: only the top of the head shows. */
function lionPeek() {
  return `<circle cx="-56" cy="-70" r="24" fill="#d99a3c"/><circle cx="56" cy="-70" r="24" fill="#d99a3c"/>
    <circle cx="-56" cy="-70" r="12" fill="#7a4a1e"/><circle cx="56" cy="-70" r="12" fill="#7a4a1e"/>
    <path d="M-80,0 C-84,-76 84,-76 80,0 Z" fill="#e9b04e"/>
    <ellipse cx="-28" cy="-26" rx="11" ry="13" fill="#2d1d0e"/><ellipse cx="28" cy="-26" rx="11" ry="13" fill="#2d1d0e"/>
    <circle cx="-25" cy="-30" r="4" fill="#fff"/><circle cx="31" cy="-30" r="4" fill="#fff"/>
    <path d="M-12,-6 l12,10 l12,-10 Z" fill="#7a4a1e"/>`;
}

function frog() {
  return `<ellipse cx="0" cy="-18" rx="44" ry="26" fill="#6cbf4b"/>
    <circle cx="-22" cy="-44" r="15" fill="#6cbf4b"/><circle cx="22" cy="-44" r="15" fill="#6cbf4b"/>
    <circle cx="-22" cy="-46" r="8" fill="#fff"/><circle cx="22" cy="-46" r="8" fill="#fff"/>
    <circle cx="-21" cy="-46" r="4" fill="#1d2b12"/><circle cx="23" cy="-46" r="4" fill="#1d2b12"/>
    <path d="M-20,-14 q20,14 40,0" stroke="#2f6a1c" stroke-width="4" fill="none" stroke-linecap="round"/>
    <ellipse cx="-38" cy="-4" rx="16" ry="8" fill="#58a83a"/><ellipse cx="38" cy="-4" rx="16" ry="8" fill="#58a83a"/>`;
}

function beetle() {
  return `<circle cx="70" cy="-44" r="44" fill="#7a5a36"/><circle cx="56" cy="-58" r="12" fill="#8f6c44" opacity=".8"/>
    <ellipse cx="0" cy="-22" rx="34" ry="22" fill="#23253a"/><circle cx="-34" cy="-22" r="13" fill="#23253a"/>
    <path d="M0,-44 L0,0" stroke="#3d4060" stroke-width="3"/>
    <path d="M-16,-6 l-10,10 M0,-2 l0,10 M16,-6 l12,-20 M24,-30 l10,-10" stroke="#23253a" stroke-width="5" stroke-linecap="round"/>
    <circle cx="-40" cy="-26" r="3" fill="#fff"/>`;
}

function firefly() {
  const id = nid("ff");
  return `<defs><radialGradient id="${id}"><stop offset="0" stop-color="#fffbb0"/><stop offset="1" stop-color="#fffbb0" stop-opacity="0"/></radialGradient></defs>
    <circle r="34" fill="url(#${id})"/><circle r="6" fill="#fffde0"/>`;
}


module.exports = {
  rng, at, sky, sun, moon, stars, cloud, hills, ground, grass, tuft, acacia, rock, mound, bush, reeds, waterhole, lily,
  elephant, zebra, giraffe, bird, meerkat, tortoise, hippoEyes, owl, lionPeek, frog, beetle, firefly,
};
