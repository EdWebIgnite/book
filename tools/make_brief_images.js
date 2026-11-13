/*
 * Reference images for the illustrator's brief (docs/art-brief): a character sheet drawn
 * from the kit. The page roughs come from make_books.js with CHECK_DIR set.
 *   node tools/make_brief_images.js
 */
"use strict";
const path = require("path");
const k = require("./art/kit");
const g = require("./art/garden");
const { renderAll } = require("./art/render");

const OUT = path.join(__dirname, "..", "docs", "art-brief", "img");
const A = k.at;
const label = (x, y, t, size = 44) => `<text x="${x}" y="${y}" text-anchor="middle" font-weight="600" font-size="${size}" fill="#23324a">${t}</text>`;

function mossSheet() {
  const W = 3600, H = 2500;
  let v = `<rect width="${W}" height="${H}" fill="#f6f8fb"/>`;
  const poses = [
    ["cheer", { eyes: "happy", mouth: "laugh", boots: "none" }, "Delighted (no boots yet)"], ["proud", { eyes: "happy" }, "Proud"],
    ["toe", { look: 1, mouth: "flat" }, "One toe in. Not impressed"], ["tiptoe", { look: 0.6, mouth: "oh" }, "On tiptoe, reaching"],
    ["whoa", { mouth: "wobble", eyes: "wide" }, "Backing off"], ["sit", { eyes: "sad", mouth: "sad" }, "Sad, sitting"],
    ["hide", { eyes: "wide", mouth: "wobble" }, "Hiding, scared"], ["look", { look: 1, eyes: "wide", mouth: "oh" }, "Amazed"],
    ["jump", { eyes: "happy", mouth: "laugh" }, "Jumping"], ["splash", { eyes: "happy", mouth: "laugh" }, "Splash!"],
    ["sitUp", { eyes: "happy", mouth: "laugh", boots: "left" }, "Laughing, one boot off"], ["stand", {}, "Neutral"],
  ];
  poses.forEach(([p, o, text], i) => {
    const x = 310 + (i % 6) * 596, y = 760 + Math.floor(i / 6) * 760;
    v += A(x, y, 1.0, g.moss({ pose: p, ...o })) + label(x, y + 90, text);
  });
  v += A(520, 2300, 1.1, g.dot({})) + label(520, 2420, "Dot")
    + A(1080, 2300, 1.1, g.dot({ pose: "flap", mood: "scared" })) + label(1080, 2420, "Dot, startled")
    + A(1560, 2300, 1.3, g.duckling()) + label(1560, 2420, "Duckling (three)")
    + A(2300, 2300, 0.9, g.dog({})) + label(2300, 2420, "The dog")
    + A(3050, 2300, 1.3, g.bird({ splash: true })) + label(3050, 2420, "The bird")
    + A(3400, 2300, 1.2, g.boot()) + label(3400, 2420, "Boot");
  return { svg: v, width: W, height: H, file: path.join(OUT, "characters.jpg") };
}

renderAll([mossSheet()]);
