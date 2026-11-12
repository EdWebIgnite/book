/*
 * Drawing kit for "Moss and the Biggest Puddle": Moss the bear cub (posable), Dot the
 * duck, the other garden animals, and the garden itself.
 *
 * Characters stand on their origin (y is negative going up) and face the viewer.
 * Softer and more shaded than kit.js: gradients for form, a darker rim for definition.
 */
"use strict";
const { rng, at } = require("./kit");

let n = 0;
const nid = p => `gd${p}${++n}`;
const P = pts => pts.map(p => p.join(",")).join(" L");

const FUR = "#b9793f", FUR_D = "#8c5526", FUR_L = "#d39a5c", CREAM = "#f7e4c4", INK = "#3a2420";
const BOOT = "#e02b2b", BOOT_D = "#a81919";

/** A soft limb: a thick rounded line with a darker rim. */
function limb(pts, width, color = FUR, rim = FUR_D) {
  const d = "M" + P(pts);
  const c = `fill="none" stroke-linecap="round" stroke-linejoin="round"`;
  return `<path d="${d}" stroke="${rim}" stroke-width="${width + 7}" ${c}/><path d="${d}" stroke="${color}" stroke-width="${width}" ${c}/>`;
}

/** A red wellington boot. Origin at the heel on the ground; the toe points right. */
function boot() {
  return `<path d="M-36,-104 L-36,-6 Q-36,14 -16,14 L54,14 Q78,14 78,-10 Q78,-34 50,-38 L36,-40 L36,-104 Z" fill="${BOOT}" stroke="${BOOT_D}" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-36,2 L76,2" stroke="${BOOT_D}" stroke-width="10" stroke-linecap="round"/>
    <rect x="-41" y="-112" width="82" height="20" rx="9" fill="#f04848" stroke="${BOOT_D}" stroke-width="5"/>
    <path d="M-20,-84 Q-24,-50 -18,-22" stroke="#fff" stroke-width="9" fill="none" stroke-linecap="round" opacity=".55"/>
    <path d="M40,-22 Q56,-24 62,-14" stroke="#fff" stroke-width="7" fill="none" stroke-linecap="round" opacity=".5"/>`;
}

// Joints for each pose: [shoulder side x is fixed] elbow and paw for each arm, knee and foot for each leg.
// lift raises the whole bear (jumping), squash flattens him (hiding), sit draws him seated.
const MOSS = {
  stand:  { aL: [[-96, -214], [-104, -150]], aR: [[96, -214], [104, -150]], lL: [[-44, -80], [-46, -8]], lR: [[44, -80], [46, -8]] },
  proud:  { aL: [[-118, -230], [-74, -180]], aR: [[118, -230], [74, -180]], lL: [[-50, -80], [-56, -8]], lR: [[50, -80], [56, -8]] },
  cheer:  { aL: [[-110, -316], [-124, -396]], aR: [[110, -316], [124, -396]], lL: [[-44, -80], [-48, -8]], lR: [[44, -80], [48, -8]] },
  look:   { aL: [[-100, -214], [-106, -150]], aR: [[112, -330], [70, -404]], lL: [[-44, -80], [-46, -8]], lR: [[44, -80], [46, -8]], tilt: -6 },
  toe:    { aL: [[-126, -262], [-176, -246]], aR: [[122, -250], [168, -220]], lL: [[-40, -80], [-36, -8]], lR: [[78, -92], [136, -30]], lean: -5 },
  tiptoe: { aL: [[-86, -344], [-70, -430]], aR: [[90, -350], [112, -436]], lL: [[-50, -80], [-56, -30]], lR: [[50, -80], [56, -30]], toes: true, tilt: -8 },
  whoa:   { aL: [[-124, -290], [-150, -350]], aR: [[124, -290], [150, -350]], lL: [[-50, -80], [-60, -8]], lR: [[40, -80], [38, -8]], lean: 8 },
  hide:   { aL: [[-124, -300], [-60, -372]], aR: [[124, -300], [60, -372]], lL: [[-62, -54], [-56, -8]], lR: [[62, -54], [56, -8]], squash: 0.78 },
  jump:   { aL: [[-118, -330], [-150, -410]], aR: [[118, -330], [150, -410]], lL: [[-84, -110], [-64, -44]], lR: [[84, -110], [64, -44]], lift: 0 },
  splash: { aL: [[-134, -270], [-204, -300]], aR: [[134, -270], [204, -300]], lL: [[-66, -74], [-96, -8]], lR: [[66, -74], [96, -8]] },
  sit:    { sit: true, aL: [[-100, -150], [-40, -196]], aR: [[100, -150], [40, -196]] },
  sitUp:  { sit: true, aL: [[-112, -160], [-150, -110]], aR: [[112, -160], [150, -110]] },
};

/**
 * Moss the bear cub.
 * pose: a key of MOSS. look: -1..1, where his face points. eyes: open | happy | wide | sad | flat.
 * mouth: smile | laugh | flat | oh | sad | wobble. boots: both | left | none.
 */
function moss({ pose = "stand", look = 0, eyes = "open", mouth = "smile", boots = "both", brow = 0 } = {}) {
  const J = MOSS[pose];
  const g = nid("m");
  const dx = look * 16;
  const defs = `<defs>
    <radialGradient id="${g}b" cx=".4" cy=".3" r=".8"><stop offset="0" stop-color="${FUR_L}"/><stop offset=".65" stop-color="${FUR}"/><stop offset="1" stop-color="${FUR_D}"/></radialGradient>
    <radialGradient id="${g}h" cx=".42" cy=".32" r=".75"><stop offset="0" stop-color="${FUR_L}"/><stop offset=".7" stop-color="${FUR}"/><stop offset="1" stop-color="#a06830"/></radialGradient>
  </defs>`;

  const eyeAt = x => {
    const cx = x + dx, cy = -18;
    if (eyes === "happy") return `<path d="M${cx - 17},${cy + 4} q17,-22 34,0" stroke="${INK}" stroke-width="8" fill="none" stroke-linecap="round"/>`;
    if (eyes === "flat") return `<path d="M${cx - 16},${cy} h32" stroke="${INK}" stroke-width="8" stroke-linecap="round"/>`;
    const r = eyes === "wide" ? 21 : 17;
    return `${eyes === "wide" ? `<circle cx="${cx}" cy="${cy}" r="${r + 7}" fill="#fff"/>` : ""}
      <circle cx="${cx}" cy="${cy}" r="${r}" fill="${INK}"/>
      <circle cx="${cx + 6}" cy="${cy - 7}" r="${r * 0.36}" fill="#fff"/><circle cx="${cx - 6}" cy="${cy + 6}" r="${r * 0.18}" fill="#fff" opacity=".8"/>
      ${eyes === "sad" ? `<path d="M${cx - 20},${cy - 15} q20,-4 40,10 l0,-22 l-40,0 Z" fill="${FUR}"/>` : ""}`;
  };
  const browAt = (x, side) => {
    const tiltUp = eyes === "sad" ? -side * 9 : eyes === "wide" ? 0 : side * brow * 8;
    const y = eyes === "wide" ? -62 : -52;
    return `<path d="M${x + dx - 18},${y + tiltUp} q18,-10 36,${-tiltUp * 2}" stroke="${FUR_D}" stroke-width="8" fill="none" stroke-linecap="round"/>`;
  };
  const mouths = {
    smile: `<path d="M${dx - 26},50 q26,24 52,0" stroke="${INK}" stroke-width="7" fill="none" stroke-linecap="round"/>`,
    laugh: `<path d="M${dx - 34},44 q34,6 68,0 q-4,50 -34,50 q-30,0 -34,-50 Z" fill="#6e2424" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/><path d="M${dx - 18},80 q18,-16 36,0 q-18,16 -36,0 Z" fill="#f08a8a"/>`,
    flat: `<path d="M${dx - 22},56 h44" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>`,
    oh: `<ellipse cx="${dx}" cy="62" rx="15" ry="19" fill="#6e2424" stroke="${INK}" stroke-width="6"/>`,
    sad: `<path d="M${dx - 22},62 q22,-18 44,0" stroke="${INK}" stroke-width="7" fill="none" stroke-linecap="round"/>`,
    wobble: `<path d="M${dx - 26},56 q9,-10 17,0 q9,10 18,0 q9,-10 17,0" stroke="${INK}" stroke-width="7" fill="none" stroke-linecap="round"/>`,
  };
  const head = (hx, hy, tilt) => `<g transform="translate(${hx} ${hy}) rotate(${tilt})">
    <circle cx="-92" cy="-80" r="44" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/><circle cx="92" cy="-80" r="44" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/>
    <circle cx="-92" cy="-78" r="23" fill="#e9a9a0"/><circle cx="92" cy="-78" r="23" fill="#e9a9a0"/>
    <ellipse rx="124" ry="108" fill="url(#${g}h)" stroke="${FUR_D}" stroke-width="6"/>
    <path d="M-16,-106 q6,-22 16,-4 q8,-24 18,0 q8,-14 12,4" fill="${FUR}" stroke="${FUR_D}" stroke-width="5" stroke-linejoin="round"/>
    <ellipse cx="${dx}" cy="36" rx="62" ry="48" fill="${CREAM}"/>
    <ellipse cx="${-84 + dx * 0.5}" cy="30" rx="22" ry="14" fill="#f29a9a" opacity=".55"/><ellipse cx="${84 + dx * 0.5}" cy="30" rx="22" ry="14" fill="#f29a9a" opacity=".55"/>
    ${eyeAt(-48)}${eyeAt(48)}${browAt(-48, -1)}${browAt(48, 1)}
    <ellipse cx="${dx}" cy="12" rx="23" ry="16" fill="${INK}"/><ellipse cx="${dx - 7}" cy="7" rx="8" ry="4" fill="#fff" opacity=".7"/>
    <path d="M${dx},28 v${mouth === "laugh" ? 14 : 22}" stroke="${INK}" stroke-width="6" stroke-linecap="round"/>
    ${mouths[mouth]}
  </g>`;

  if (J.sit) {
    // Sitting on the ground, legs out towards the viewer: we see the soles of his boots.
    const sole = (x, on) => on
      ? `<g transform="translate(${x} 0)"><ellipse cy="-52" rx="52" ry="62" fill="${BOOT_D}"/><ellipse cy="-56" rx="44" ry="54" fill="#7a1010"/>
          <g stroke="${BOOT_D}" stroke-width="8" stroke-linecap="round"><path d="M-26,-80 h52 M-30,-56 h60 M-26,-32 h52"/></g></g>`
      : `<g transform="translate(${x} 0)"><ellipse cy="-46" rx="44" ry="52" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/>
          <ellipse cy="-36" rx="22" ry="18" fill="#e9a9a0"/><circle cx="-20" cy="-70" r="9" fill="#e9a9a0"/><circle cy="-78" r="9" fill="#e9a9a0"/><circle cx="20" cy="-70" r="9" fill="#e9a9a0"/></g>`;
    return `${defs}<ellipse cy="6" rx="190" ry="22" fill="#000" opacity=".16"/>
      <path d="M-104,-10 C-130,-110 -96,-236 0,-244 C96,-236 130,-110 104,-10 Q0,22 -104,-10 Z" fill="url(#${g}b)" stroke="${FUR_D}" stroke-width="6"/>
      <ellipse cy="-112" rx="62" ry="70" fill="${CREAM}"/>
      ${sole(-84, boots === "both" || boots === "left")}${sole(84, boots === "both")}
      ${limb([[-82, -214], ...J.aL], 48)}${limb([[82, -214], ...J.aR], 48)}
      ${head(0, -316, 0)}`;
  }

  const sq = J.squash || 1;
  const toes = J.toes ? 13 : 0;   // up on tiptoe: heels lift, toes point down
  const lifted = pt => pt[1] < -20 && !J.toes;   // a foot that's off the ground keeps its own height
  const bootAt = (pt, flip) => boots === "none" || (boots === "left" && !flip)
    ? `<ellipse cx="${pt[0] + (flip ? -10 : 10)}" cy="${lifted(pt) ? pt[1] + 14 : -12}" rx="44" ry="22" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/>`
    : `<g transform="translate(${pt[0]} ${lifted(pt) ? pt[1] + 26 : toes ? -30 : 0}) rotate(${flip ? -toes : toes}) scale(${flip ? -1 : 1} 1)">${boot()}</g>`;
  const paw = pt => `<circle cx="${pt[0]}" cy="${pt[1]}" r="27" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/>`;
  const body = `<g transform="rotate(${J.lean || 0})">
      ${limb([[-40, -150], ...J.lL], 60)}${limb([[40, -150], ...J.lR], 60)}
      ${bootAt(J.lL[1], true)}${bootAt(J.lR[1], false)}
      ${limb([[-78, -278], ...J.aL], 48)}
      <path d="M-84,-296 C-124,-236 -126,-164 -96,-126 Q0,-88 96,-126 C126,-164 124,-236 84,-296 Q0,-328 -84,-296 Z" fill="url(#${g}b)" stroke="${FUR_D}" stroke-width="6"/>
      <ellipse cy="-198" rx="62" ry="68" fill="${CREAM}"/>
      ${limb([[78, -278], ...J.aR], 48)}${paw(J.aL[1])}${paw(J.aR[1])}
      ${head(0, -400, J.tilt || 0)}
    </g>`;
  return `${defs}<ellipse cy="6" rx="${150 / sq}" ry="20" fill="#000" opacity=".16"/><g transform="scale(${1 / Math.sqrt(sq)} ${sq})">${body}</g>`;
}

/** Just Moss's head and paws, for peeping over something. Origin where the paws rest. */
function mossPeek({ look = 0, eyes = "wide", mouth = "oh" } = {}) {
  const whole = moss({ pose: "stand", look, eyes, mouth });
  const c = nid("pk");
  return `<defs><clipPath id="${c}"><rect x="-200" y="-560" width="400" height="300"/></clipPath></defs>
    <g transform="translate(0 290)" clip-path="url(#${c})">${whole}</g>
    <circle cx="-96" cy="6" r="28" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/><circle cx="96" cy="6" r="28" fill="${FUR}" stroke="${FUR_D}" stroke-width="6"/>`;
}

/** Dot the duck. pose: stand | flap | float. Faces right. */
function dot({ pose = "stand", mood = "happy" } = {}) {
  const g = nid("d");
  const W = "#fffdf6", S = "#d9dfe6", O = "#f59a23", OD = "#d97706";
  const wing = pose === "flap"
    ? `<path d="M-30,-150 C-110,-230 -150,-250 -190,-236 C-170,-200 -120,-150 -40,-110 Z" fill="${W}" stroke="${S}" stroke-width="6" stroke-linejoin="round"/>
       <path d="M20,-156 C40,-250 70,-290 120,-296 C116,-246 90,-180 50,-120 Z" fill="${W}" stroke="${S}" stroke-width="6" stroke-linejoin="round"/>`
    : `<path d="M-70,-150 C-20,-176 44,-160 60,-112 C20,-86 -40,-90 -70,-150 Z" fill="${W}" stroke="${S}" stroke-width="6" stroke-linejoin="round"/>
       <path d="M-30,-126 q30,16 60,2 M-20,-108 q26,12 50,2" stroke="${S}" stroke-width="5" fill="none" stroke-linecap="round"/>`;
  const legs = pose === "float" ? "" :
    `<path d="M-24,-40 v34 M30,-40 v34" stroke="${OD}" stroke-width="12" stroke-linecap="round"/>
     <path d="M-54,0 L-24,-14 L6,0 Z M0,0 L30,-14 L60,0 Z" fill="${O}" stroke="${OD}" stroke-width="5" stroke-linejoin="round"/>`;
  const eye = mood === "scared"
    ? `<circle cx="96" cy="-250" r="20" fill="#fff" stroke="${S}" stroke-width="3"/><circle cx="100" cy="-250" r="9" fill="${INK}"/>`
    : `<circle cx="96" cy="-250" r="13" fill="${INK}"/><circle cx="100" cy="-255" r="5" fill="#fff"/><path d="M82,-268 q10,-8 22,-4" stroke="${INK}" stroke-width="4" fill="none" stroke-linecap="round"/>`;
  const bill = mood === "scared" || pose === "flap"
    ? `<path d="M128,-246 L192,-250 Q200,-238 190,-232 L130,-226 Z" fill="${O}" stroke="${OD}" stroke-width="5" stroke-linejoin="round"/><path d="M130,-222 L182,-212 Q170,-196 132,-204 Z" fill="${OD}"/>`
    : `<path d="M128,-246 L196,-240 Q204,-226 190,-220 L130,-218 Z" fill="${O}" stroke="${OD}" stroke-width="5" stroke-linejoin="round"/><path d="M134,-230 h56" stroke="${OD}" stroke-width="4"/>`;
  return `<defs><radialGradient id="${g}" cx=".4" cy=".3" r=".8"><stop offset="0" stop-color="#fff"/><stop offset=".7" stop-color="${W}"/><stop offset="1" stop-color="${S}"/></radialGradient></defs>
    ${pose === "float" ? "" : `<ellipse cy="6" rx="110" ry="14" fill="#000" opacity=".15"/>`}${legs}
    <path d="M-150,-150 l-56,-34 l26,54 Z" fill="${W}" stroke="${S}" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-150,-120 C-150,-200 -60,-214 20,-200 C96,-190 126,-150 110,-96 C96,-40 -20,-24 -90,-44 C-130,-56 -150,-84 -150,-120 Z" fill="url(#${g})" stroke="${S}" stroke-width="6"/>
    <path d="M40,-180 C40,-226 52,-256 78,-270 L120,-236 C104,-210 104,-190 108,-150 Z" fill="${W}"/>
    <circle cx="92" cy="-250" r="56" fill="url(#${g})" stroke="${S}" stroke-width="6"/>
    <path d="M70,-302 q-6,-26 12,-20 q4,-22 16,-2" fill="${W}" stroke="${S}" stroke-width="5" stroke-linejoin="round"/>
    ${wing}${bill}${eye}<ellipse cx="74" cy="-226" rx="13" ry="8" fill="#f8a5a5" opacity=".6"/>`;
}

/** A fluffy duckling, facing right. */
function duckling() {
  return `<ellipse cy="4" rx="46" ry="8" fill="#000" opacity=".15"/>
    <path d="M-14,-10 v10 M12,-10 v10" stroke="#d97706" stroke-width="7" stroke-linecap="round"/>
    <path d="M-50,-50 l-22,-14 l10,24 Z" fill="#ffd84a"/>
    <ellipse cy="-42" rx="50" ry="38" fill="#ffd84a" stroke="#e6b422" stroke-width="5"/>
    <circle cx="34" cy="-86" r="34" fill="#ffe066" stroke="#e6b422" stroke-width="5"/>
    <path d="M24,-118 q-2,-14 8,-10 q4,-12 10,2" fill="#ffe066" stroke="#e6b422" stroke-width="4"/>
    <path d="M62,-88 l30,4 l-28,12 Z" fill="#f59a23" stroke="#d97706" stroke-width="4" stroke-linejoin="round"/>
    <circle cx="44" cy="-92" r="7" fill="${INK}"/><circle cx="46" cy="-95" r="2.5" fill="#fff"/>
    <path d="M-24,-46 q20,14 40,-2" stroke="#e6b422" stroke-width="5" fill="none" stroke-linecap="round"/>`;
}

/** The dog, lying down and not impressed. Faces left, about 620 long. */
function dog({ brow = true } = {}) {
  const B = "#c98d55", BD = "#9a6534", W = "#fbf3e4";
  return `<ellipse cx="20" cy="6" rx="330" ry="24" fill="#000" opacity=".16"/>
    <path d="M300,-110 q70,-30 60,-96" stroke="${B}" stroke-width="26" fill="none" stroke-linecap="round"/>
    <path d="M-70,-30 C-70,-190 90,-220 220,-190 C320,-166 340,-70 300,-24 Q110,10 -70,-30 Z" fill="${B}" stroke="${BD}" stroke-width="6"/>
    <path d="M40,-28 C40,-110 150,-130 220,-100 C250,-60 240,-30 220,-20 Z" fill="${W}" opacity=".9"/>
    <path d="M150,-60 q90,-10 130,40 l-150,6 Z" fill="${B}" stroke="${BD}" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-150,-14 h180 q24,0 24,-22 q0,-22 -24,-22 h-150 Z" fill="${W}" stroke="${BD}" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-130,-14 v-18 M-104,-14 v-18" stroke="${BD}" stroke-width="5" stroke-linecap="round"/>
    <path d="M-250,-150 C-250,-250 -110,-270 -50,-200 C-20,-160 -40,-80 -100,-70 C-200,-60 -250,-90 -250,-150 Z" fill="${B}" stroke="${BD}" stroke-width="6"/>
    <path d="M-256,-140 C-300,-140 -310,-96 -280,-80 C-240,-64 -190,-74 -176,-104 Z" fill="${W}" stroke="${BD}" stroke-width="6" stroke-linejoin="round"/>
    <ellipse cx="-292" cy="-112" rx="20" ry="15" fill="${INK}"/><ellipse cx="-298" cy="-117" rx="6" ry="3" fill="#fff" opacity=".7"/>
    <path d="M-262,-84 q24,10 50,-6" stroke="${INK}" stroke-width="5" fill="none" stroke-linecap="round"/>
    <path d="M-80,-232 C-20,-250 20,-190 0,-110 C-20,-70 -60,-90 -70,-150 Z" fill="${BD}" stroke="#7a4d24" stroke-width="6" stroke-linejoin="round"/>
    <circle cx="-170" cy="-176" r="15" fill="${INK}"/><circle cx="-175" cy="-181" r="5" fill="#fff"/>
    <path d="M-200,-190 q30,-6 44,12 l0,-24 l-44,0 Z" fill="${B}"/>
    <path d="M-204,${brow ? -222 : -204} q28,${brow ? -16 : -6} 56,${brow ? 6 : 0}" stroke="#7a4d24" stroke-width="8" fill="none" stroke-linecap="round"/>`;
}

/** A round little bird, facing left. splash adds raised wings and drops. */
function bird({ splash = false } = {}) {
  return `${splash ? `<path d="M10,-60 C40,-120 70,-130 96,-120 C84,-90 60,-60 30,-40 Z" fill="#5c7a99" stroke="#415a73" stroke-width="5" stroke-linejoin="round"/>
      <g fill="#cdeeff"><circle cx="-80" cy="-110" r="9"/><circle cx="-40" cy="-140" r="7"/><circle cx="60" cy="-150" r="8"/><circle cx="100" cy="-90" r="6"/><circle cx="-110" cy="-70" r="6"/></g>` : ""}
    <path d="M60,-40 l50,-16 l-20,34 Z" fill="#5c7a99" stroke="#415a73" stroke-width="5" stroke-linejoin="round"/>
    <ellipse cy="-44" rx="64" ry="50" fill="#7393b3" stroke="#415a73" stroke-width="5"/>
    <path d="M-52,-44 C-52,-10 -10,6 30,-6 C20,-50 -20,-70 -52,-44 Z" fill="#f2784b"/>
    <circle cx="-40" cy="-84" r="38" fill="#7393b3" stroke="#415a73" stroke-width="5"/>
    <path d="M-74,-86 l-34,8 l32,12 Z" fill="#f2b84b" stroke="#c98a1e" stroke-width="4" stroke-linejoin="round"/>
    <circle cx="-52" cy="-92" r="9" fill="${INK}"/><circle cx="-55" cy="-95" r="3" fill="#fff"/>
    <path d="M-10,0 v12 M16,0 v12" stroke="#c98a1e" stroke-width="6" stroke-linecap="round"/>`;
}

function snail() {
  return `<path d="M-90,0 C-90,-30 -40,-30 60,-20 C100,-16 120,-40 116,-70" stroke="#e6c79a" stroke-width="30" fill="none" stroke-linecap="round"/>
    <path d="M108,-96 l-12,-40 M124,-96 l10,-40" stroke="#e6c79a" stroke-width="8" stroke-linecap="round"/><circle cx="96" cy="-138" r="8" fill="${INK}"/><circle cx="134" cy="-138" r="8" fill="${INK}"/>
    <circle cx="-10" cy="-64" r="62" fill="#c2703d" stroke="#8f4d24" stroke-width="6"/>
    <path d="M-10,-64 m0,-40 a40,40 0 1 1 -40,40 a26,26 0 1 1 26,26 a12,12 0 1 1 12,-12" stroke="#8f4d24" stroke-width="7" fill="none" stroke-linecap="round"/>
    <path d="M100,-46 q12,8 22,0" stroke="${INK}" stroke-width="4" fill="none" stroke-linecap="round"/>`;
}

function frog() {
  return `<ellipse cy="4" rx="80" ry="10" fill="#000" opacity=".15"/>
    <ellipse cx="-58" cy="-10" rx="30" ry="14" fill="#4f9e3a"/><ellipse cx="58" cy="-10" rx="30" ry="14" fill="#4f9e3a"/>
    <ellipse cy="-40" rx="70" ry="46" fill="#6cbf4b" stroke="#3f7f2d" stroke-width="6"/>
    <ellipse cy="-26" rx="44" ry="26" fill="#d6f0b6"/>
    <circle cx="-34" cy="-86" r="26" fill="#6cbf4b" stroke="#3f7f2d" stroke-width="6"/><circle cx="34" cy="-86" r="26" fill="#6cbf4b" stroke="#3f7f2d" stroke-width="6"/>
    <circle cx="-34" cy="-88" r="14" fill="#fff"/><circle cx="34" cy="-88" r="14" fill="#fff"/><circle cx="-32" cy="-88" r="7" fill="${INK}"/><circle cx="36" cy="-88" r="7" fill="${INK}"/>
    <path d="M-34,-44 q34,26 68,0" stroke="#2f5f20" stroke-width="6" fill="none" stroke-linecap="round"/>`;
}

function ladybird() {
  return `<ellipse cy="-22" rx="34" ry="26" fill="#e02b2b" stroke="#8f1515" stroke-width="5"/><path d="M0,-48 v52" stroke="${INK}" stroke-width="4"/>
    <circle cx="-14" cy="-28" r="6" fill="${INK}"/><circle cx="14" cy="-18" r="6" fill="${INK}"/><circle cx="-10" cy="-10" r="4" fill="${INK}"/><circle cx="34" cy="-22" r="14" fill="${INK}"/>`;
}

function worm() {
  return `<path d="M-70,-6 q20,-44 46,-10 q20,26 44,-8 q14,-20 34,-10" stroke="#e8908a" stroke-width="22" fill="none" stroke-linecap="round"/>
    <circle cx="52" cy="-34" r="4" fill="${INK}"/><path d="M48,-20 q8,6 14,-2" stroke="${INK}" stroke-width="3" fill="none" stroke-linecap="round"/>`;
}

function butterfly(color = "#f08ad0") {
  return `<path d="M0,0 C-70,-70 -110,-20 -60,20 C-90,60 -30,70 0,10 Z M0,0 C70,-70 110,-20 60,20 C90,60 30,70 0,10 Z" fill="${color}" stroke="#fff" stroke-width="5" stroke-linejoin="round"/>
    <ellipse rx="8" ry="30" fill="${INK}"/><path d="M-4,-28 q-12,-22 -22,-22 M4,-28 q12,-22 22,-22" stroke="${INK}" stroke-width="4" fill="none" stroke-linecap="round"/>`;
}

function paperBoat() {
  return `<path d="M-90,-30 L90,-30 L50,10 L-50,10 Z" fill="#fff" stroke="#b8c4d0" stroke-width="5" stroke-linejoin="round"/>
    <path d="M0,-130 L60,-30 L-60,-30 Z" fill="#f4f7fb" stroke="#b8c4d0" stroke-width="5" stroke-linejoin="round"/><path d="M0,-130 V-30" stroke="#b8c4d0" stroke-width="4"/>`;
}

// ---------------------------------------------------------------- garden things

function tree(s = 1, leaf = "#5aa046", dark = "#3f7d34", light = "#84c25c") {
  return `<ellipse cy="10" rx="260" ry="30" fill="#000" opacity=".14"/>
    <path d="M-50,0 C-36,-160 -60,-300 -30,-470 L40,-470 C60,-300 40,-160 60,0 Z" fill="#8a5a33"/>
    <path d="M10,-470 L40,-470 C60,-300 40,-160 60,0 L20,0 C10,-160 24,-300 10,-470 Z" fill="#6f4526" opacity=".7"/>
    <g fill="${dark}"><circle cx="-190" cy="-560" r="180"/><circle cx="170" cy="-540" r="200"/><circle cx="-20" cy="-720" r="220"/><circle cx="-10" cy="-520" r="190"/></g>
    <g fill="${leaf}"><circle cx="-200" cy="-590" r="160"/><circle cx="170" cy="-580" r="180"/><circle cx="-20" cy="-750" r="200"/><circle cx="-10" cy="-560" r="170"/></g>
    <g fill="${light}" opacity=".85"><circle cx="-250" cy="-650" r="80"/><circle cx="80" cy="-830" r="90"/><circle cx="210" cy="-660" r="70"/><circle cx="-80" cy="-700" r="60"/></g>`;
}

function flower(color = "#f25a8a", centre = "#ffd23f", h = 120) {
  let petals = "";
  for (let i = 0; i < 6; i++) petals += `<ellipse cx="0" cy="-18" rx="13" ry="20" transform="rotate(${i * 60})" fill="${color}"/>`;
  return `<path d="M0,0 q-8,-${h / 2} 0,-${h}" stroke="#3f8f3a" stroke-width="8" fill="none" stroke-linecap="round"/>
    <path d="M0,-${h * 0.4} q-30,-10 -36,-34 q30,2 36,34 Z M0,-${h * 0.55} q30,-10 36,-34 q-30,2 -36,34 Z" fill="#4fa346"/>
    <g transform="translate(0 -${h})">${petals}<circle r="12" fill="${centre}"/></g>`;
}

function tulip(color = "#ff7a3d", h = 130) {
  return `<path d="M0,0 q6,-${h / 2} 0,-${h}" stroke="#3f8f3a" stroke-width="8" fill="none" stroke-linecap="round"/>
    <path d="M0,-20 q-40,-30 -30,-90 q30,30 30,90 Z M0,-20 q40,-30 30,-90 q-30,30 -30,90 Z" fill="#4fa346"/>
    <path d="M-24,-${h + 40} q0,50 24,50 q24,0 24,-50 l-12,18 l-12,-22 l-12,22 Z" fill="${color}" stroke="${color}" stroke-width="4" stroke-linejoin="round"/>`;
}

function bush(color = "#5aa046", dark = "#3f7d34") {
  return `<g fill="${dark}"><circle cx="-90" cy="-70" r="90"/><circle cx="60" cy="-90" r="110"/><circle cx="170" cy="-60" r="80"/></g>
    <g fill="${color}"><circle cx="-90" cy="-90" r="80"/><circle cx="60" cy="-112" r="98"/><circle cx="170" cy="-78" r="70"/></g>`;
}

/** Big floppy leaves from the vegetable patch. */
function bigLeaf(color = "#4f9a3a") {
  return `<path d="M0,0 q-10,-60 -4,-110" stroke="#c9566a" stroke-width="12" fill="none" stroke-linecap="round"/>
    <path d="M-4,-110 C-140,-120 -190,-220 -150,-300 C-60,-330 60,-300 120,-220 C140,-160 80,-110 -4,-110 Z" fill="${color}" stroke="#35722a" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-4,-110 C-40,-180 -70,-240 -120,-290 M-30,-160 l-60,-10 M-56,-210 l-50,-30 M-30,-160 l50,-40 M-56,-210 l80,-40" stroke="#35722a" stroke-width="5" fill="none" stroke-linecap="round"/>`;
}

function mushroom() {
  return `<rect x="-14" y="-50" width="28" height="50" rx="10" fill="#f6ecd9"/><path d="M-56,-46 C-56,-110 56,-110 56,-46 Z" fill="#e0403a" stroke="#a82822" stroke-width="5"/>
    <g fill="#fff"><circle cx="-24" cy="-74" r="9"/><circle cx="14" cy="-84" r="11"/><circle cx="34" cy="-60" r="6"/></g>`;
}

function wateringCan() {
  return `<ellipse cx="0" cy="6" rx="150" ry="16" fill="#000" opacity=".15"/>
    <path d="M90,-150 C150,-180 190,-240 250,-280" stroke="#2a8f98" stroke-width="26" fill="none" stroke-linecap="round"/>
    <ellipse cx="258" cy="-286" rx="34" ry="18" transform="rotate(-38 258 -286)" fill="#1f7078"/>
    <path d="M-110,-200 C-200,-200 -200,-40 -110,-60" stroke="#2a8f98" stroke-width="22" fill="none" stroke-linecap="round"/>
    <path d="M-110,-230 L110,-230 L96,0 L-96,0 Z" fill="#35a8b3" stroke="#1f7078" stroke-width="6" stroke-linejoin="round"/>
    <ellipse cy="-230" rx="110" ry="24" fill="#1f7078"/><ellipse cy="-234" rx="92" ry="16" fill="#8fd6f0"/>
    <path d="M-70,-190 q-6,90 0,160" stroke="#fff" stroke-width="12" fill="none" stroke-linecap="round" opacity=".4"/>
    <path d="M40,-140 l14,28 l30,4 l-22,20 l6,30 l-28,-14 l-28,14 l6,-30 l-22,-20 l30,-4 Z" fill="#ffd23f" opacity=".9"/>`;
}

function birdBath() {
  return `<ellipse cy="8" rx="170" ry="20" fill="#000" opacity=".15"/>
    <path d="M-110,0 L-70,-40 L70,-40 L110,0 Z" fill="#b9b4ad" stroke="#8a857e" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-36,-40 C-20,-160 -46,-300 -30,-430 L30,-430 C46,-300 20,-160 36,-40 Z" fill="#cbc6bf" stroke="#8a857e" stroke-width="6"/>
    <path d="M0,-430 L30,-430 C46,-300 20,-160 36,-40 L10,-40 C4,-160 12,-300 0,-430 Z" fill="#a39e97" opacity=".6"/>
    <path d="M-250,-500 Q-240,-420 0,-420 Q240,-420 250,-500 Z" fill="#cbc6bf" stroke="#8a857e" stroke-width="6" stroke-linejoin="round"/>
    <ellipse cy="-500" rx="250" ry="42" fill="#b9b4ad" stroke="#8a857e" stroke-width="6"/><ellipse cy="-500" rx="218" ry="30" fill="#8fd6f0"/>
    <path d="M-150,-504 q50,-12 110,0 M40,-494 q50,-10 100,0" stroke="#fff" stroke-width="6" fill="none" stroke-linecap="round" opacity=".7"/>`;
}

function bench() {
  const W = "#a8703f", WD = "#7a4d24";
  return `<ellipse cy="10" rx="430" ry="26" fill="#000" opacity=".16"/>
    <path d="M-360,0 V-470 M360,0 V-470" stroke="#4a4f57" stroke-width="30" stroke-linecap="round"/>
    <path d="M-300,0 V-330 M300,0 V-330" stroke="#3a3f47" stroke-width="26" stroke-linecap="round"/>
    <g fill="${W}" stroke="${WD}" stroke-width="6"><rect x="-400" y="-640" width="800" height="62" rx="16"/><rect x="-400" y="-560" width="800" height="62" rx="16"/><rect x="-400" y="-480" width="800" height="62" rx="16"/>
      <rect x="-430" y="-360" width="860" height="56" rx="18"/></g>
    <rect x="-430" y="-318" width="860" height="22" rx="10" fill="${WD}"/>
    <path d="M-390,-626 h780 M-390,-546 h780" stroke="#c98d55" stroke-width="6" stroke-linecap="round" opacity=".6"/>`;
}

function dogBowl() {
  return `<ellipse cy="6" rx="130" ry="14" fill="#000" opacity=".15"/>
    <path d="M-120,0 L-90,-74 L90,-74 L120,0 Z" fill="#3d7fd9" stroke="#2a5aa0" stroke-width="6" stroke-linejoin="round"/>
    <ellipse cy="-74" rx="92" ry="20" fill="#2a5aa0"/><ellipse cy="-76" rx="78" ry="13" fill="#8fd6f0"/>
    <path d="M-34,-34 a11,11 0 1 1 10,-10 h48 a11,11 0 1 1 10,10 a11,11 0 1 1 -10,10 h-48 a11,11 0 1 1 -10,-10 Z" fill="#fff" opacity=".9"/>`;
}

function kennel() {
  return `<ellipse cy="10" rx="300" ry="26" fill="#000" opacity=".15"/>
    <rect x="-240" y="-330" width="480" height="330" fill="#d9a066" stroke="#9a6534" stroke-width="6"/>
    <g stroke="#b8804a" stroke-width="5"><path d="M-240,-250 h480 M-240,-170 h480 M-240,-90 h480"/></g>
    <path d="M-100,0 V-150 Q-100,-240 0,-240 Q100,-240 100,-150 V0 Z" fill="#3a2a22"/>
    <path d="M-300,-320 L0,-540 L300,-320 Z" fill="#c0492f" stroke="#8a2f1c" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-150,-430 h300 M-220,-376 h440" stroke="#8a2f1c" stroke-width="5" opacity=".6"/>`;
}

function box() {
  return `<ellipse cy="8" rx="230" ry="18" fill="#000" opacity=".15"/>
    <path d="M-190,-170 L-250,-250 L-30,-270 L10,-190 Z" fill="#c99a62" stroke="#8f6a3c" stroke-width="6" stroke-linejoin="round"/>
    <path d="M190,-170 L250,-240 L40,-270 L-6,-190 Z" fill="#d9ab72" stroke="#8f6a3c" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-190,-170 h380 V0 h-380 Z" fill="#d9ab72" stroke="#8f6a3c" stroke-width="6" stroke-linejoin="round"/>
    <path d="M-190,-170 h380 l-20,26 h-340 Z" fill="#b98a52"/>
    <path d="M-150,-170 q30,-50 70,-30 q30,-50 70,-20 q40,-40 80,-10 q30,-20 60,20 l10,40 h-300 Z" fill="#fff" opacity=".95"/>
    <rect x="-60" y="-110" width="120" height="60" rx="8" fill="#fff" opacity=".8"/><path d="M-40,-90 h80 M-40,-70 h60" stroke="#b98a52" stroke-width="6" stroke-linecap="round"/>`;
}

/** The front of Moss's house: wall, door, window and step. About 1300 wide, 1500 tall; origin bottom left. */
function house() {
  const g = nid("h");
  return `<defs><linearGradient id="${g}" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#f7dba6"/><stop offset="1" stop-color="#f0c98a"/></linearGradient></defs>
    <rect x="0" y="-1500" width="1300" height="1500" fill="url(#${g})"/>
    <g stroke="#e3b877" stroke-width="5" opacity=".7"><path d="M0,-1300 h1300 M0,-1100 h1300 M0,-900 h1300 M0,-700 h1300 M0,-500 h1300 M0,-300 h1300 M0,-100 h1300"/></g>
    <rect x="1240" y="-1500" width="60" height="1500" fill="#d9a865"/>
    <rect x="0" y="-70" width="1300" height="70" fill="#b98a6a"/>
    <path d="M400,-60 V-640 Q400,-820 580,-820 Q760,-820 760,-640 V-60 Z" fill="#2f8f83" stroke="#1d6a60" stroke-width="10"/>
    <path d="M440,-60 V-630 Q440,-780 580,-780 Q720,-780 720,-630 V-60 Z" fill="#3aa698"/>
    <rect x="470" y="-600" width="220" height="230" rx="16" fill="#2f8f83"/><rect x="470" y="-330" width="220" height="230" rx="16" fill="#2f8f83"/>
    <circle cx="700" cy="-350" r="18" fill="#ffd23f" stroke="#c9981a" stroke-width="5"/>
    <path d="M500,-700 a80,80 0 0 1 160,0 Z" fill="#cdeeff" stroke="#1d6a60" stroke-width="8"/>
    <rect x="900" y="-760" width="300" height="320" rx="14" fill="#cdeeff" stroke="#fff" stroke-width="22"/>
    <path d="M1050,-760 V-440 M900,-600 H1200" stroke="#fff" stroke-width="14"/>
    <path d="M920,-740 l80,0 l-80,110 Z" fill="#fff" opacity=".5"/>
    <rect x="880" y="-440" width="340" height="70" rx="10" fill="#c0492f"/>
    <g>${[910, 980, 1050, 1120, 1190].map((x, i) => at(x, -440, 0.5, i % 2 ? tulip("#ffd23f", 90) : flower("#f25a8a", "#ffd23f", 90))).join("")}</g>
    <rect x="330" y="-60" width="500" height="60" rx="8" fill="#c9c2b8" stroke="#9a938a" stroke-width="6"/>
    <rect x="420" y="-74" width="320" height="22" rx="8" fill="#7a4d24"/>`;
}

/** A length of picket fence, origin bottom left. */
function fence(width) {
  let out = `<rect x="0" y="-150" width="${width}" height="22" fill="#e9e2d6"/><rect x="0" y="-70" width="${width}" height="22" fill="#e9e2d6"/>`;
  for (let x = 20; x < width; x += 74) {
    out += `<path d="M${x},0 V-190 L${x + 26},-222 L${x + 52},-190 V0 Z" fill="#fbf7ef" stroke="#d6cdbd" stroke-width="5" stroke-linejoin="round"/>`;
  }
  return out;
}

function cloud(color = "#fff", shade = "#e3ecf5") {
  return `<g fill="${shade}"><ellipse cx="0" cy="16" rx="230" ry="60"/></g>
    <g fill="${color}"><ellipse cx="0" cy="0" rx="220" ry="64"/><ellipse cx="-100" cy="-44" rx="110" ry="74"/><ellipse cx="40" cy="-80" rx="140" ry="96"/><ellipse cx="160" cy="-30" rx="100" ry="64"/></g>`;
}

function puddle(rx, ry, { top = "#bfe6fb", bottom = "#6fb7e6" } = {}) {
  const g = nid("pd");
  return `<defs><linearGradient id="${g}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient></defs>
    <ellipse cy="6" rx="${rx + 26}" ry="${ry + 16}" fill="#6b8f3a" opacity=".55"/>
    <ellipse rx="${rx}" ry="${ry}" fill="url(#${g})"/>
    <g stroke="#fff" stroke-width="${Math.max(4, rx / 90)}" fill="none" stroke-linecap="round" opacity=".7">
      <path d="M${-rx * 0.6},${-ry * 0.3} q${rx * 0.15},${-ry * 0.2} ${rx * 0.32},0"/><path d="M${rx * 0.1},${ry * 0.35} q${rx * 0.2},${-ry * 0.2} ${rx * 0.4},0"/>
      <path d="M${-rx * 0.2},${ry * 0.1} q${rx * 0.1},${-ry * 0.14} ${rx * 0.2},0"/></g>`;
}

function rainbow(r, width = 40) {
  return ["#ff6b6b", "#ffa94d", "#ffe066", "#8ce99a", "#74c0fc", "#9775fa"].map((c, i) =>
    `<path d="M${-(r - i * width)},0 A${r - i * width},${r - i * width} 0 0 1 ${r - i * width},0" stroke="${c}" stroke-width="${width + 1}" fill="none" opacity=".8"/>`).join("");
}

/** One big splash of water, centred where the feet land. */
function splash(s = 1) {
  const drops = [];
  const r = rng(5);
  for (let i = 0; i < 26; i++) {
    const a = -Math.PI * (0.08 + 0.84 * r()), d = 260 + r() * 420, sz = 14 + r() * 26;
    drops.push(`<path d="M${Math.cos(a) * d},${Math.sin(a) * d} q-${sz * 0.7},${sz * 1.1} 0,${sz * 1.7} q${sz * 0.7},-${sz * 0.6} 0,-${sz * 1.7} Z" fill="#bfe6fb" stroke="#8fcbee" stroke-width="3"/>`);
  }
  const jet = (x, lean, h, w) => `<path d="M${x - w},0 C${x - w + lean * 0.3},${-h * 0.6} ${x + lean - w * 0.4},${-h} ${x + lean},${-h} C${x + lean + w * 0.5},${-h * 0.9} ${x + w + lean * 0.3},${-h * 0.5} ${x + w},0 Z" fill="#d9f1fd" stroke="#8fcbee" stroke-width="5" stroke-linejoin="round"/>`;
  return at(0, 0, s, `${jet(-330, -210, 330, 70)}${jet(-170, -120, 470, 80)}${jet(170, 120, 470, 80)}${jet(330, 210, 330, 70)}${jet(0, 0, 250, 110)}
    ${drops.join("")}
    <g fill="none" stroke="#fff" stroke-width="10" opacity=".8"><ellipse rx="430" ry="70"/><ellipse rx="600" ry="100" opacity=".5"/></g>`);
}

module.exports = {
  moss, mossPeek, dot, duckling, dog, bird, snail, frog, ladybird, worm, butterfly, paperBoat, boot,
  tree, flower, tulip, bush, bigLeaf, mushroom, wateringCan, birdBath, bench, dogBowl, kennel, box, house, fence, cloud, puddle, rainbow, splash,
};
