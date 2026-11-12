/*
 * Comic-book drawing kit: inked figures built from poses, a robot, city backdrops,
 * and the usual comic effects (halftone dots, speed lines, bursts, lettering).
 */
"use strict";
const { rng, at } = require("./kit");

const INK = "#141414";
let n = 0;
const nid = p => `cm${p}${++n}`;
const esc = t => t.replace(/&/g, "&amp;").replace(/</g, "&lt;");
const path = pts => "M" + pts.map(p => p.join(",")).join(" L");
const lerp2 = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];

/** A limb: a thick inked line through joints, with an optional glove or boot at the end. */
function limb(pts, width, color, tip) {
  const d = path(pts);
  const common = `fill="none" stroke-linecap="round" stroke-linejoin="round"`;
  let out = `<path d="${d}" stroke="${INK}" stroke-width="${width + 12}" ${common}/><path d="${d}" stroke="${color}" stroke-width="${width}" ${common}/>`;
  if (tip) {
    const [a, b] = pts.slice(-2);
    out += `<path d="${path([lerp2(a, b, 0.5), b])}" stroke="${tip}" stroke-width="${width}" ${common}/>`;
  }
  return out;
}

// Joint positions for each pose. The figure faces right; the origin is on the ground under it.
const POSES = {
  stand: {
    torso: [[-40, -232], [40, -232], [26, -128], [-26, -128]], head: [0, -288], look: 0, bolt: 0,
    armB: [[-40, -224], [-84, -182], [-42, -146]], armF: [[40, -224], [84, -182], [42, -146]],
    legB: [[-18, -128], [-30, -66], [-42, -8]], legF: [[18, -128], [30, -66], [42, -8]],
    cape: "M-38,-236 C-120,-170 -140,-70 -104,-4 C-44,-30 34,-26 92,-8 C122,-80 100,-170 38,-236 Z",
  },
  point: {
    torso: [[-40, -232], [40, -232], [26, -128], [-26, -128]], head: [0, -288], look: 6, bolt: 0,
    armB: [[-40, -224], [-62, -176], [-56, -128]], armF: [[40, -224], [88, -258], [132, -304]],
    legB: [[-18, -128], [-26, -66], [-32, -8]], legF: [[18, -128], [26, -66], [34, -8]],
    cape: "",
  },
  cheer: {
    torso: [[-40, -232], [40, -232], [26, -128], [-26, -128]], head: [0, -288], look: 0, bolt: 0,
    armB: [[-40, -224], [-78, -272], [-66, -330]], armF: [[40, -224], [78, -272], [66, -330]],
    legB: [[-18, -128], [-30, -66], [-40, -8]], legF: [[18, -128], [30, -66], [40, -8]],
    cape: "",
  },
  run: {
    torso: [[14, -232], [78, -206], [30, -112], [-16, -128]], head: [94, -270], look: 8, bolt: 24,
    armB: [[22, -226], [-22, -208], [-58, -166]], armF: [[70, -206], [114, -176], [162, -202]],
    legB: [[-8, -124], [-58, -78], [-128, -92]], legF: [[18, -116], [86, -96], [78, -16]],
    cape: "M18,-234 C-70,-264 -150,-232 -256,-266 C-240,-228 -252,-200 -230,-170 C-150,-176 -60,-178 6,-150 Z",
  },
  leap: {
    torso: [[44, -226], [96, -174], [22, -88], [-22, -132]], head: [114, -248], look: 10, bolt: 42,
    armB: [[48, -222], [14, -182], [-12, -150]], armF: [[92, -180], [152, -228], [218, -272]],
    legB: [[-14, -126], [-78, -92], [-150, -70]], legF: [[14, -92], [-42, -44], [-108, -8]],
    cape: "M44,-228 C-30,-224 -110,-152 -206,-124 C-176,-98 -178,-60 -146,-32 C-84,-70 -22,-100 30,-96 Z",
  },
};

const SUIT = "#3b2d8f", GOLD = "#ffd23f", CAPE = "#ff7a1a";

/** Grown-up build: broader shoulders on the same pose. */
function adultPose(P) {
  const [a, b] = P.torso;
  const m = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
  const wide = p => [m[0] + (p[0] - m[0]) * 1.38, m[1] + (p[1] - m[1]) * 1.38];
  const a2 = wide(a), b2 = wide(b);
  const shift = (arm, from, to) => [[arm[0][0] + to[0] - from[0], arm[0][1] + to[1] - from[1]], ...arm.slice(1)];
  return { ...P, torso: [a2, b2, P.torso[2], P.torso[3]], armB: shift(P.armB, a, a2), armF: shift(P.armF, b, b2) };
}

/**
 * A person. pose: stand | point | cheer | run | leap.
 * adult gives grown-up proportions (use a larger scale too). costume draws the hero suit, mask and cape;
 * otherwise shirt/pants colours. tie and glasses are for the hero's everyday clothes.
 */
function figure(pose, { costume = false, adult = false, shirt = "#f77f00", pants = "#33539e", skin = "#8d5a3b",
                        hair = "puffs", hairColor = "#1d1310", mood = "smile", tie = false, glasses = false } = {}) {
  const P = adult ? adultPose(POSES[pose]) : POSES[pose];
  const suit = SUIT, gold = GOLD;
  if (adult) return adultFigure(P, { costume, shirt, pants, skin, hairColor, mood, tie, glasses });
  const top = costume ? suit : shirt, legs = costume ? suit : pants;
  const armColor = costume ? suit : skin;
  const [hx, hy] = P.head, lx = P.look;
  const cx = (P.torso[0][0] + P.torso[1][0] + P.torso[2][0] + P.torso[3][0]) / 4;
  const cy = (P.torso[0][1] + P.torso[1][1] + P.torso[2][1] + P.torso[3][1]) / 4;

  const mouths = {
    smile: `<path d="M${lx - 14},20 q14,13 28,0" stroke="${INK}" stroke-width="5" fill="none" stroke-linecap="round"/>`,
    grit: `<path d="M${lx - 13},16 h26 v10 h-26 Z" fill="#fff" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/><path d="M${lx},16 v10" stroke="${INK}" stroke-width="3"/>`,
    wow: `<ellipse cx="${lx}" cy="24" rx="10" ry="13" fill="#5a1d1d" stroke="${INK}" stroke-width="4"/>`,
    grin: `<path d="M${lx - 16},16 q16,22 32,0 Z" fill="#fff" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>`,
  };
  const eyes = costume
    ? `<path d="M-46,-14 Q0,-32 46,-14 L44,10 Q0,-4 -44,10 Z" fill="${suit}" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
       <ellipse cx="${lx - 18}" cy="-5" rx="11" ry="7" fill="#fff"/><ellipse cx="${lx + 18}" cy="-5" rx="11" ry="7" fill="#fff"/>
       <circle cx="${lx - 16}" cy="-5" r="4" fill="${INK}"/><circle cx="${lx + 20}" cy="-5" r="4" fill="${INK}"/>`
    : `<ellipse cx="${lx - 18}" cy="-4" rx="9" ry="10" fill="#fff" stroke="${INK}" stroke-width="3"/><ellipse cx="${lx + 18}" cy="-4" rx="9" ry="10" fill="#fff" stroke="${INK}" stroke-width="3"/>
       <circle cx="${lx - 16}" cy="-3" r="4.5" fill="${INK}"/><circle cx="${lx + 20}" cy="-3" r="4.5" fill="${INK}"/>`;
  const hairBack = hair === "puffs"
    ? `<circle cx="-50" cy="-38" r="31" fill="${hairColor}" stroke="${INK}" stroke-width="6"/><circle cx="50" cy="-38" r="31" fill="${hairColor}" stroke="${INK}" stroke-width="6"/>` : "";
  const hairTop = hair === "short"
    ? `<path d="M-46,-6 A46,46 0 0 1 46,-6 Q24,-26 0,-22 Q-24,-26 -46,-6 Z" fill="${hairColor}"/>`
    : `<path d="M-45,-12 A46,46 0 0 1 45,-12 Q0,-40 -45,-12 Z" fill="${hairColor}"/>`;
  const bolt = costume
    ? `<g transform="translate(${cx} ${cy - 6}) rotate(${P.bolt}) scale(1.5)"><path d="M6,-22 L-12,4 L0,4 L-6,24 L14,-4 L2,-4 Z" fill="${gold}" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/></g>` : "";
  const belt = costume
    ? `<path d="${path([P.torso[3], P.torso[2]])}" stroke="${INK}" stroke-width="20" stroke-linecap="round"/><path d="${path([P.torso[3], P.torso[2]])}" stroke="${gold}" stroke-width="10" stroke-linecap="round"/>` : "";

  return `<ellipse cx="${cx}" cy="4" rx="90" ry="12" fill="#000" opacity=".2"/>
    ${costume && P.cape ? `<path d="${P.cape}" fill="${CAPE}" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/>` : ""}
    ${limb(P.armB, 24, armColor, costume ? gold : null)}
    ${limb(P.legB, 28, legs, costume ? gold : "#fff")}
    ${limb(P.legF, 28, legs, costume ? gold : "#fff")}
    <path d="${path(P.torso)} Z" fill="${top}" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    ${bolt}${belt}
    ${limb(P.armF, 24, armColor, costume ? gold : null)}
    <g transform="translate(${hx} ${hy})">${hairBack}
      <circle r="46" fill="${skin}" stroke="${INK}" stroke-width="7"/>${hairTop}
      ${eyes}${mouths[mood]}
    </g>`;
}


/** The grown-up version: smaller head, square jaw, wide shoulders, thicker limbs. */
function adultFigure(P, { costume, shirt, pants, skin, hairColor, mood, tie, glasses }) {
  const top = costume ? SUIT : shirt, legs = costume ? SUIT : pants;
  const armColor = costume ? SUIT : shirt;
  const [hx, hy] = P.head, lx = P.look;
  const cx = (P.torso[0][0] + P.torso[1][0] + P.torso[2][0] + P.torso[3][0]) / 4;
  const cy = (P.torso[0][1] + P.torso[1][1] + P.torso[2][1] + P.torso[3][1]) / 4;
  const neck = [(P.torso[0][0] + P.torso[1][0]) / 2, (P.torso[0][1] + P.torso[1][1]) / 2];
  const mouths = {
    smile: `<path d="M${lx - 12},18 q12,10 24,0" stroke="${INK}" stroke-width="4.5" fill="none" stroke-linecap="round"/>`,
    grit: `<path d="M${lx - 13},14 h26 v9 h-26 Z" fill="#fff" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/><path d="M${lx - 4},14 v9 M${lx + 5},14 v9" stroke="${INK}" stroke-width="2.5"/>`,
    wow: `<ellipse cx="${lx}" cy="20" rx="9" ry="11" fill="#5a1d1d" stroke="${INK}" stroke-width="4"/>`,
    grin: `<path d="M${lx - 15},13 q15,20 30,0 Z" fill="#fff" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>`,
  };
  const eyes = costume
    ? `<path d="M-37,-16 Q0,-28 37,-16 L35,4 Q${lx},-8 -35,4 Z" fill="${SUIT}" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
       <path d="M${lx - 24},-8 l16,-3 l-2,8 l-12,1 Z M${lx + 24},-8 l-16,-3 l2,8 l12,1 Z" fill="#fff"/>`
    : `<circle cx="${lx - 14}" cy="-6" r="4" fill="${INK}"/><circle cx="${lx + 14}" cy="-6" r="4" fill="${INK}"/>
       <path d="M${lx - 22},-17 h14 M${lx + 8},-17 h14" stroke="${INK}" stroke-width="4" stroke-linecap="round"/>`
      + (glasses ? `<g fill="#dff3ff" fill-opacity=".5" stroke="${INK}" stroke-width="4"><rect x="${lx - 27}" y="-17" width="24" height="20" rx="5"/><rect x="${lx + 3}" y="-17" width="24" height="20" rx="5"/></g><path d="M${lx - 3},-9 h6" stroke="${INK}" stroke-width="4"/>` : "");
  const bolt = costume
    ? `<g transform="translate(${cx} ${cy - 14}) rotate(${P.bolt}) scale(2)"><path d="M6,-22 L-12,4 L0,4 L-6,24 L14,-4 L2,-4 Z" fill="${GOLD}" stroke="${INK}" stroke-width="2.5" stroke-linejoin="round"/></g>`
    : tie ? `<path d="M${neck[0] - 9},${neck[1] + 6} l9,10 l9,-10 Z M${neck[0] - 8},${neck[1] + 16} l8,62 l8,-62 Z" fill="#d7263d" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>` : "";
  const belt = `<path d="${path([P.torso[3], P.torso[2]])}" stroke="${INK}" stroke-width="22" stroke-linecap="round"/><path d="${path([P.torso[3], P.torso[2]])}" stroke="${costume ? GOLD : "#3a2a22"}" stroke-width="11" stroke-linecap="round"/>`;
  const skinTip = costume ? GOLD : skin;
  return `<ellipse cx="${cx}" cy="4" rx="100" ry="12" fill="#000" opacity=".2"/>
    ${costume && P.cape ? `<path d="${P.cape}" fill="${CAPE}" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/>` : ""}
    ${limb(P.armB, 28, armColor, skinTip)}
    ${limb(P.legB, 32, legs, costume ? GOLD : "#2a2a2a")}
    ${limb(P.legF, 32, legs, costume ? GOLD : "#2a2a2a")}
    <path d="${path([neck, [hx, hy + 30]])}" stroke="${INK}" stroke-width="34" stroke-linecap="round"/><path d="${path([neck, [hx, hy + 30]])}" stroke="${skin}" stroke-width="22" stroke-linecap="round"/>
    <path d="${path(P.torso)} Z" fill="${top}" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    ${bolt}${belt}
    ${limb(P.armF, 28, armColor, skinTip)}
    <g transform="translate(${hx} ${hy + 6})">
      <path d="M-35,-10 Q-37,-46 0,-46 Q37,-46 35,-10 L31,20 Q0,48 -31,20 Z" fill="${skin}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      <path d="M-38,-12 Q-44,-58 2,-58 Q44,-58 38,-12 Q30,-32 6,-34 Q-20,-36 -38,-12 Z" fill="${hairColor}" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
      ${eyes}${mouths[mood]}
      <path d="M${lx - 4},34 q4,4 8,0" stroke="${INK}" stroke-width="3" fill="none" stroke-linecap="round" opacity=".6"/>
    </g>`;
}

// ---------------------------------------------------------------- robot

const ROBOT_ARMS = {
  stomp: { L: [[-190, -390], [-268, -452], [-250, -556]], R: [[190, -390], [272, -330], [282, -232]] },
  reach: { L: [[-190, -390], [-262, -306], [-244, -214]], R: [[190, -390], [318, -366], [438, -292]] },
  down: { L: [[-190, -390], [-236, -296], [-222, -196]], R: [[190, -390], [236, -296], [222, -196]] },
};

function claw([x, y], [px, py]) {
  const ang = Math.atan2(y - py, x - px) * 180 / Math.PI;
  return `<g transform="translate(${x} ${y}) rotate(${ang})">
    <circle r="30" fill="#59636d" stroke="${INK}" stroke-width="8"/>
    <path d="M10,-22 q46,-18 62,6 q-22,-4 -40,10 Z M10,22 q46,18 62,-6 q-22,4 -40,-10 Z" fill="#c9d2d9" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/></g>`;
}

/** The runaway robot, seen from the front. arms: stomp | reach | down. eye: angry | dim | sleep. */
function robot({ arms = "down", eye = "angry", sit = false } = {}) {
  const B = "#6fa8a0", D = "#2f5f5a", L = "#a9d3cc", M = "#9aa5ad";
  const A = ROBOT_ARMS[arms];
  const eyes = {
    angry: `<path d="M-66,-552 L-18,-534 L-20,-512 L-64,-520 Z M66,-552 L18,-534 L20,-512 L64,-520 Z" fill="#ff3b3b"/>
            <path d="M-66,-552 L-18,-534 M66,-552 L18,-534" stroke="#ffb3b3" stroke-width="4"/>`,
    dim: `<rect x="-62" y="-536" width="44" height="10" rx="5" fill="#7d4a4a"/><rect x="18" y="-536" width="44" height="10" rx="5" fill="#7d4a4a"/>`,
    sleep: `<path d="M-62,-536 q22,16 44,0 M18,-536 q22,16 44,0" stroke="#7fd4ff" stroke-width="8" fill="none" stroke-linecap="round"/>`,
  };
  const stand = `
    <rect x="-126" y="-176" width="86" height="150" rx="14" fill="${M}" stroke="${INK}" stroke-width="9"/>
    <rect x="40" y="-176" width="86" height="150" rx="14" fill="${M}" stroke="${INK}" stroke-width="9"/>
    <rect x="-150" y="-46" width="132" height="46" rx="16" fill="${D}" stroke="${INK}" stroke-width="9"/>
    <rect x="18" y="-46" width="132" height="46" rx="16" fill="${D}" stroke="${INK}" stroke-width="9"/>`;
  const soles = `
    <rect x="-166" y="-96" width="122" height="96" rx="18" fill="${D}" stroke="${INK}" stroke-width="9"/>
    <rect x="44" y="-96" width="122" height="96" rx="18" fill="${D}" stroke="${INK}" stroke-width="9"/>
    <g stroke="${INK}" stroke-width="6"><path d="M-150,-70 h90 M-150,-48 h90 M-150,-26 h90 M60,-70 h90 M60,-48 h90 M60,-26 h90"/></g>`;
  const body = `
    ${limb(A.L, 48, M)}${limb(A.R, 48, M)}
    <circle cx="${A.L[1][0]}" cy="${A.L[1][1]}" r="24" fill="#59636d" stroke="${INK}" stroke-width="7"/>
    <circle cx="${A.R[1][0]}" cy="${A.R[1][1]}" r="24" fill="#59636d" stroke="${INK}" stroke-width="7"/>
    ${claw(A.L[2], A.L[1])}${claw(A.R[2], A.R[1])}
    <rect x="-32" y="-466" width="64" height="50" fill="#59636d" stroke="${INK}" stroke-width="8"/>
    <rect x="-172" y="-434" width="344" height="286" rx="38" fill="${B}" stroke="${INK}" stroke-width="10"/>
    <path d="M-172,-250 Q0,-210 172,-250 L172,-186 Q172,-148 134,-148 L-134,-148 Q-172,-148 -172,-186 Z" fill="#000" opacity=".12"/>
    <rect x="-112" y="-398" width="224" height="130" rx="16" fill="${D}" stroke="${INK}" stroke-width="8"/>
    <circle cx="-70" cy="-350" r="18" fill="#ff5d5d" stroke="${INK}" stroke-width="5"/><circle cx="-20" cy="-350" r="18" fill="#ffd23f" stroke="${INK}" stroke-width="5"/>
    <circle cx="30" cy="-350" r="18" fill="#7be07b" stroke="${INK}" stroke-width="5"/>
    <path d="M62,-316 A34,34 0 0 1 104,-352" stroke="#fff" stroke-width="7" fill="none"/><path d="M82,-322 L98,-346" stroke="#ff5d5d" stroke-width="6" stroke-linecap="round"/>
    <g stroke="${INK}" stroke-width="7" stroke-linecap="round"><path d="M-70,-222 h140 M-70,-198 h140 M-70,-174 h140"/></g>
    <g fill="#e8f1ef" stroke="${INK}" stroke-width="4"><circle cx="-146" cy="-408" r="8"/><circle cx="146" cy="-408" r="8"/><circle cx="-146" cy="-174" r="8"/><circle cx="146" cy="-174" r="8"/></g>
    <circle cx="-190" cy="-390" r="36" fill="#59636d" stroke="${INK}" stroke-width="8"/><circle cx="190" cy="-390" r="36" fill="#59636d" stroke="${INK}" stroke-width="8"/>
    <path d="M0,-596 V-654" stroke="${INK}" stroke-width="12"/><circle cx="0" cy="-668" r="18" fill="#ff5d5d" stroke="${INK}" stroke-width="7"/>
    <rect x="-136" y="-548" width="26" height="56" rx="8" fill="#59636d" stroke="${INK}" stroke-width="7"/><rect x="110" y="-548" width="26" height="56" rx="8" fill="#59636d" stroke="${INK}" stroke-width="7"/>
    <rect x="-112" y="-596" width="224" height="142" rx="32" fill="${L}" stroke="${INK}" stroke-width="10"/>
    <rect x="-86" y="-566" width="172" height="64" rx="18" fill="#1b2330" stroke="${INK}" stroke-width="7"/>${eyes[eye]}
    <rect x="-52" y="-490" width="104" height="20" rx="6" fill="#59636d" stroke="${INK}" stroke-width="5"/>
    <g stroke="${INK}" stroke-width="4"><path d="M-26,-490 v20 M0,-490 v20 M26,-490 v20"/></g>
    <path d="M-150,-420 q20,-10 60,-8" stroke="#fff" stroke-width="8" fill="none" stroke-linecap="round" opacity=".5"/>`;
  return `<ellipse cx="0" cy="6" rx="250" ry="26" fill="#000" opacity=".25"/>` +
    (sit ? `<g transform="translate(0 148)">${body}</g>${soles}` : stand + body);
}

/** The robot from behind, with the big OFF button on its back. */
function robotBack() {
  const B = "#6fa8a0", D = "#2f5f5a", L = "#a9d3cc", M = "#9aa5ad";
  const A = ROBOT_ARMS.down;
  let stripes = "";
  for (let i = 0; i < 12; i++) {
    const a0 = (i / 12) * Math.PI * 2, a1 = ((i + 0.5) / 12) * Math.PI * 2;
    stripes += `<path d="M${Math.cos(a0) * 92},${-296 + Math.sin(a0) * 92} A92,92 0 0 1 ${Math.cos(a1) * 92},${-296 + Math.sin(a1) * 92} L0,-296 Z" fill="${INK}"/>`;
  }
  return `<ellipse cx="0" cy="6" rx="250" ry="26" fill="#000" opacity=".25"/>
    <rect x="-126" y="-176" width="86" height="150" rx="14" fill="${M}" stroke="${INK}" stroke-width="9"/>
    <rect x="40" y="-176" width="86" height="150" rx="14" fill="${M}" stroke="${INK}" stroke-width="9"/>
    <rect x="-150" y="-46" width="132" height="46" rx="16" fill="${D}" stroke="${INK}" stroke-width="9"/>
    <rect x="18" y="-46" width="132" height="46" rx="16" fill="${D}" stroke="${INK}" stroke-width="9"/>
    ${limb(A.L, 48, M)}${limb(A.R, 48, M)}${claw(A.L[2], A.L[1])}${claw(A.R[2], A.R[1])}
    <rect x="-32" y="-466" width="64" height="50" fill="#59636d" stroke="${INK}" stroke-width="8"/>
    <rect x="-172" y="-434" width="344" height="286" rx="38" fill="${B}" stroke="${INK}" stroke-width="10"/>
    <g fill="#e8f1ef" stroke="${INK}" stroke-width="4"><circle cx="-146" cy="-408" r="8"/><circle cx="146" cy="-408" r="8"/><circle cx="-146" cy="-174" r="8"/><circle cx="146" cy="-174" r="8"/></g>
    <circle cx="0" cy="-296" r="92" fill="#ffd23f" stroke="${INK}" stroke-width="8"/>${stripes}
    <circle cx="0" cy="-296" r="92" fill="none" stroke="${INK}" stroke-width="8"/>
    <circle cx="0" cy="-296" r="62" fill="#e21d1d" stroke="${INK}" stroke-width="8"/>
    <ellipse cx="-18" cy="-318" rx="24" ry="12" fill="#fff" opacity=".5" transform="rotate(-30 -18 -318)"/>
    <text x="0" y="-280" text-anchor="middle" font-family="Bangers" font-size="44" fill="#fff" stroke="${INK}" stroke-width="2">OFF</text>
    <circle cx="-190" cy="-390" r="36" fill="#59636d" stroke="${INK}" stroke-width="8"/><circle cx="190" cy="-390" r="36" fill="#59636d" stroke="${INK}" stroke-width="8"/>
    <path d="M0,-596 V-654" stroke="${INK}" stroke-width="12"/><circle cx="0" cy="-668" r="18" fill="#ff5d5d" stroke="${INK}" stroke-width="7"/>
    <rect x="-112" y="-596" width="224" height="142" rx="32" fill="${L}" stroke="${INK}" stroke-width="10"/>
    <g stroke="${INK}" stroke-width="6" stroke-linecap="round"><path d="M-60,-552 h120 M-60,-526 h120 M-60,-500 h120"/></g>`;
}

// ---------------------------------------------------------------- backdrops and effects

function gradient(w, h, top, bottom) {
  const id = nid("g");
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient></defs>
    <rect width="${w}" height="${h}" fill="url(#${id})"/>`;
}

/** Ben-Day dots fading from one edge, the classic printed-comic texture. */
function halftone(w, h, { color = "#000", opacity = 0.25, gap = 22, r = 6, from = "bottom" } = {}) {
  const p = nid("ht"), g = nid("hg"), m = nid("hm");
  const dir = { bottom: [0, 0, 0, 1], top: [0, 1, 0, 0], left: [1, 0, 0, 0], right: [0, 0, 1, 0] }[from];
  return `<defs>
    <pattern id="${p}" width="${gap}" height="${gap}" patternUnits="userSpaceOnUse" patternTransform="rotate(30)"><circle cx="${gap / 2}" cy="${gap / 2}" r="${r}" fill="${color}"/></pattern>
    <linearGradient id="${g}" x1="${dir[0]}" y1="${dir[1]}" x2="${dir[2]}" y2="${dir[3]}"><stop offset=".15" stop-color="#000"/><stop offset="1" stop-color="#fff"/></linearGradient>
    <mask id="${m}"><rect width="${w}" height="${h}" fill="url(#${g})"/></mask></defs>
    <rect width="${w}" height="${h}" fill="url(#${p})" opacity="${opacity}" mask="url(#${m})"/>`;
}

/** Lines rushing in towards a point. */
function speedLines(w, h, cx, cy, count, seed, { color = "#fff", opacity = 0.8, inner = 0.3 } = {}) {
  const r = rng(seed), R = Math.hypot(w, h);
  let out = "";
  for (let i = 0; i < count; i++) {
    const a = (i / count) * Math.PI * 2 + r() * 0.1, d = 0.008 + r() * 0.02;
    const ri = R * (inner + r() * 0.25);
    out += `<path d="M${cx + Math.cos(a - d) * R},${cy + Math.sin(a - d) * R} L${cx + Math.cos(a + d) * R},${cy + Math.sin(a + d) * R} L${cx + Math.cos(a) * ri},${cy + Math.sin(a) * ri} Z"/>`;
  }
  return `<g fill="${color}" opacity="${opacity}">${out}</g>`;
}

/** Straight streaks for sideways movement. */
function streaks(x0, x1, y0, y1, count, seed, { color = "#fff", opacity = 0.85 } = {}) {
  const r = rng(seed);
  let out = "";
  for (let i = 0; i < count; i++) {
    const y = y0 + r() * (y1 - y0), a = x0 + r() * (x1 - x0) * 0.6, len = (x1 - x0) * (0.2 + r() * 0.5);
    out += `<path d="M${a},${y} h${len}" stroke-width="${4 + r() * 10}"/>`;
  }
  return `<g stroke="${color}" stroke-linecap="round" opacity="${opacity}">${out}</g>`;
}

function burstPath(cx, cy, rx, ry, spikes, seed, depth = 0.68) {
  const r = rng(seed);
  const pts = [];
  for (let i = 0; i < spikes * 2; i++) {
    const a = (i / (spikes * 2)) * Math.PI * 2 - Math.PI / 2;
    const k = i % 2 ? depth + r() * 0.08 : 1 + r() * 0.12;
    pts.push([cx + Math.cos(a) * rx * k, cy + Math.sin(a) * ry * k]);
  }
  return path(pts) + " Z";
}

function burst(cx, cy, rx, ry, spikes, seed, { fill = "#fff", stroke = INK, width = 10 } = {}) {
  return `<path d="${burstPath(cx, cy, rx, ry, spikes, seed)}" fill="${fill}" stroke="${stroke}" stroke-width="${width}" stroke-linejoin="round"/>`;
}

function lightning(x, y, s, rot = 0, fill = "#fff") {
  return `<g transform="translate(${x} ${y}) rotate(${rot}) scale(${s})"><path d="M20,-120 L-40,0 L-6,0 L-30,120 L46,-20 L8,-20 Z" fill="${fill}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/></g>`;
}

function dustCloud(x, y, s) {
  return at(x, y, s, `<g fill="#f3ead8" stroke="${INK}" stroke-width="6"><circle cx="-60" cy="-30" r="42"/><circle cx="0" cy="-52" r="58"/><circle cx="70" cy="-30" r="46"/><circle cx="20" cy="-12" r="44"/></g>
    <g fill="#f3ead8"><circle cx="-60" cy="-30" r="38"/><circle cx="0" cy="-52" r="54"/><circle cx="70" cy="-30" r="42"/><circle cx="20" cy="-12" r="40"/></g>`);
}

/** A row of buildings standing on baseY. */
function skyline(w, baseY, seed, { color, window: win, minH = 120, maxH = 420, tower = false } = {}) {
  const r = rng(seed);
  let out = "", x = -20;
  while (x < w) {
    const bw = 90 + r() * 150, bh = minH + r() * (maxH - minH);
    out += `<rect x="${x}" y="${baseY - bh}" width="${bw}" height="${bh + 4}" fill="${color}"/>`;
    if (win) {
      for (let wy = baseY - bh + 24; wy < baseY - 30; wy += 46) {
        for (let wx = x + 16; wx < x + bw - 26; wx += 38) {
          if (r() > 0.35) out += `<rect x="${wx}" y="${wy}" width="18" height="24" fill="${win}" opacity=".85"/>`;
        }
      }
    }
    x += bw + 6;
  }
  if (tower) {
    const tx = w * 0.62;
    out += `<rect x="${tx - 14}" y="${baseY - maxH * 1.5}" width="28" height="${maxH * 1.5}" fill="${color}"/>
      <ellipse cx="${tx}" cy="${baseY - maxH * 1.32}" rx="62" ry="34" fill="${color}"/><rect x="${tx - 3}" y="${baseY - maxH * 1.78}" width="6" height="${maxH * 0.3}" fill="${color}"/>`;
  }
  return out;
}

/** City street scene: sky, two rows of buildings, pavement and road. */
function city(w, h, groundY, { sky = ["#4db8ee", "#fff1b8"], far = "#8fb7d9", near = "#34507a", win = "#ffe27a", seed = 1, dots = "#1e63d6", behind = "" } = {}) {
  return gradient(w, h, sky[0], sky[1])
    + halftone(w, groundY, { color: dots, opacity: 0.22, from: "top" })
    + behind // e.g. a sun that sits behind the buildings
    + skyline(w, groundY, seed, { color: far, minH: 200, maxH: 480, tower: true })
    + skyline(w, groundY, seed + 9, { color: near, window: win, minH: 90, maxH: 300 })
    + `<rect y="${groundY}" width="${w}" height="${h - groundY}" fill="#565c68"/>
       <rect y="${groundY}" width="${w}" height="26" fill="#c9c3b5"/><rect y="${groundY + 26}" width="${w}" height="8" fill="${INK}" opacity=".5"/>`
    + (() => { let d = ""; for (let x = 40; x < w; x += 220) d += `<rect x="${x}" y="${groundY + (h - groundY) * 0.55}" width="120" height="14" fill="#f5e9a6"/>`; return d; })();
}

function ball(x, y, s) {
  return at(x, y, s, `<circle cy="-30" r="30" fill="#fff" stroke="${INK}" stroke-width="7"/><path d="M0,-44 l14,10 l-5,16 h-18 l-5,-16 Z" fill="${INK}"/>
    <path d="M0,-44 V-60 M14,-34 l14,-6 M9,-18 l10,12 M-9,-18 l-10,12 M-14,-34 l-14,-6" stroke="${INK}" stroke-width="5"/>`);
}

// ---------------------------------------------------------------- lettering

/** Sound effect in big slanted comic letters. */
function sfx(x, y, text, size, fill, rot = -8) {
  const t = `text-anchor="middle" font-family="Bangers" font-size="${size}" letter-spacing="${size * 0.04}"`;
  return `<g transform="translate(${x} ${y}) rotate(${rot})">
    <text ${t} x="${size * 0.05}" y="${size * 0.06}" fill="${INK}" stroke="${INK}" stroke-width="${size * 0.16}" stroke-linejoin="round">${esc(text)}</text>
    <text ${t} fill="${fill}" stroke="${INK}" stroke-width="${size * 0.16}" stroke-linejoin="round" paint-order="stroke">${esc(text)}</text>
    <text ${t} fill="${fill}">${esc(text)}</text></g>`;
}

function lines(cx, cy, list, size, color = INK) {
  const lh = size * 1.12;
  return list.map((l, i) => `<text x="${cx}" y="${cy - (list.length - 1) * lh / 2 + i * lh + size * 0.35}" text-anchor="middle" font-family="Comic Neue" font-weight="700" font-size="${size}" fill="${color}">${esc(l.toUpperCase())}</text>`).join("");
}

const textWidth = (list, size) => Math.max(...list.map(l => l.length)) * size * 0.63;

/** Speech bubble with a tail pointing at the speaker. */
function bubble(cx, cy, list, tail, size = 54) {
  const rx = textWidth(list, size) / 2 + 70, ry = list.length * size * 0.56 + 52;
  const a = Math.atan2(tail[1] - cy, tail[0] - cx);
  const bx = cx + Math.cos(a) * rx * 0.8, by = cy + Math.sin(a) * ry * 0.8;
  const nx = -Math.sin(a) * 30, ny = Math.cos(a) * 30;
  const tri = `M${bx + nx},${by + ny} L${tail[0]},${tail[1]} L${bx - nx},${by - ny} Z`;
  return `<path d="${tri}" fill="#fff" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    <ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="#fff" stroke="${INK}" stroke-width="9"/>
    <path d="${tri}" fill="#fff"/>${lines(cx, cy, list, size)}`;
}

/** Spiky bubble for shouting. */
function shout(cx, cy, list, tail, size = 60, fill = "#fff") {
  const rx = textWidth(list, size) / 2 + 110, ry = list.length * size * 0.62 + 84;
  const a = Math.atan2(tail[1] - cy, tail[0] - cx);
  const bx = cx + Math.cos(a) * rx * 0.6, by = cy + Math.sin(a) * ry * 0.6;
  const nx = -Math.sin(a) * 34, ny = Math.cos(a) * 34;
  const tri = `M${bx + nx},${by + ny} L${tail[0]},${tail[1]} L${bx - nx},${by - ny} Z`;
  const b = burstPath(cx, cy, rx, ry, 14, Math.round(cx + cy), 0.8);
  return `<path d="${tri}" fill="${fill}" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    <path d="${b}" fill="${fill}" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    <path d="${tri}" fill="${fill}"/>${lines(cx, cy, list, size)}`;
}

/** Square-cornered bubble for the robot's voice. */
function robotBubble(cx, cy, list, tail, size = 50) {
  const w = textWidth(list, size) + 90, h = list.length * size * 1.12 + 60;
  const bx = Math.max(cx - w / 2 + 50, Math.min(cx + w / 2 - 50, tail[0])), by = cy + h / 2 - 4;
  const tri = `M${bx - 30},${by} L${tail[0]},${tail[1]} L${bx + 30},${by} Z`;
  return `<path d="${tri}" fill="#d9f3ff" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
    <rect x="${cx - w / 2}" y="${cy - h / 2}" width="${w}" height="${h}" fill="#d9f3ff" stroke="${INK}" stroke-width="9"/>
    <path d="M${bx - 24},${by - 6} L${tail[0]},${tail[1]} L${bx + 24},${by - 6} Z" fill="#d9f3ff"/>${lines(cx, cy, list, size)}`;
}

/** Narration box. anchor "end" puts x at the right edge. */
function caption(x, y, text, size = 46, anchor = "start") {
  const w = text.length * size * 0.63 + 60, h = size + 40;
  const x0 = anchor === "end" ? x - w : x;
  return `<rect x="${x0 + 8}" y="${y + 8}" width="${w}" height="${h}" fill="${INK}"/>
    <rect x="${x0}" y="${y}" width="${w}" height="${h}" fill="#ffe66d" stroke="${INK}" stroke-width="7"/>${lines(x0 + w / 2, y + h / 2, [text], size)}`;
}

module.exports = {
  INK, SUIT, GOLD, CAPE, figure, POSES, robot, robotBack, gradient, halftone, speedLines, streaks, burst, lightning, dustCloud, skyline, city, ball,
  sfx, bubble, shout, robotBubble, caption, esc,
};
