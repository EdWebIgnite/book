# Movement and interaction: research and ideas

Written after looking at comparable apps and the published research on young children and touchscreens. Part 1 is what I found, with how far each finding can be trusted. Part 2 is the ideas list. Part 3 is the order I would do things in.

Effort tags: **[S]** a day or less, **[M]** a few days, **[L]** a week or more. **[art]** means it needs artwork delivered in separate layers, not one flat picture. **(have)** means the playground already does it in some form.

**Built in the playground so far** (item numbers from Part 2): 1, 2, 3, 4, 5, 6, 31, 33, 41, 48, 49, 50, 65 (synthesised whoosh), 68, 74, 75, 86, 87, 88, 90 (optional hold on the gear), 93, 94. Also from the "have" list: picture hints in place of text tips, and the guided story step winning over a nearby extra when a touch is close to both.

---

## Part 1: What the research says

### 1. Apps that already do something like this

| Product | What it does | What to take from it |
|---|---|---|
| ComiXology / Kindle "Guided View" | Tap or swipe moves one panel at a time with an animated pan and zoom. Panel order, and movement inside a panel, are authored into the book. Readers can choose to see the whole page at the start or end of each page, and can switch the animation off. | Our guided mode is the same idea. Their two reader-level switches (whole page first/last, animation off) are worth copying exactly. The standing complaint is that cutting a page into panels loses the page as a whole, which is why the whole-page view matters. |
| Marvel Infinite Comics, Thrillbent, Madefire "motion books" | Comics made for screens: each tap changes something (a balloon appears, one element of a panel changes, the view shifts). Madefire added motion and sound, and critics said the heavier versions felt like cheap animation. | The reader's tap controlling time is the strong idea. Movement the reader does not control is where it starts to feel like a bad cartoon. (I could not open the main write-up on these techniques, so this row leans on general knowledge more than the others.) |
| Webtoon vertical scroll | One long strip. The gap between panels is the pacing tool: wide gaps read as a pause, tight gaps as fast action. | Distance travelled can stand for time. That applies to our camera moves and to any scroll or drag journey. |
| Nosy Crow story apps | Tapping a character opens extra speech bubbles, separate from the main text. Some stories branch on the child's choices. Child actors narrate. | Interaction works when it tells you more about the story. Reviewers also warned that chasing every interactive bit in one sitting takes the momentum out of the story. |
| Hidden Folks | Big hand-drawn scenes, find things, everything makes a sound when tapped. Hints come in graded types, from a specific spot to a wide area. Timed challenges and multi-step puzzles were removed for being frustrating. | Three lessons for explore mode: tap anything with no penalty; grade the hints; never use a timer. Also their finding that once one door opened, players expected every door to open. |

### 2. What young children can do on a touchscreen

- **Tap, swipe and drag are the safe gestures under about age 9.** Nielsen Norman Group's testing with ages 3 to 12 says to build on these three.
- **Pinch, double-tap, tilt, shake and multi-touch are unreliable for preschoolers.** Sesame Workshop's guide (2012, based on more than 50 of their own studies) and several academic studies agree that pinch is among the hardest.
- **Precise or long drags are hard.** NN/g saw a five-year-old defeated by a drag task and recommends a tap alternative for anything that needs a drag.
- **Targets should be about 2 cm across for young children**, four times the adult guideline. A 5 mm close button failed for seven-year-olds.
- **Children rest their wrists on the bottom edge of a tablet and usually hold it sideways.** Buttons along the bottom edge get pressed by accident.
- **Children are slow to notice that a tap worked**, which leads to held touches and accidental double taps. They expect a response to every touch, and sound is a good way to confirm one.
- **Avoid anything needing two hands or quick reactions under age 5.**

How solid: the gesture-by-age numbers come from small studies and one widely repeated 2013 paper, so treat exact ages as rough. The direction (tap and drag easy, pinch hard, bigger targets) is consistent everywhere I looked. The Sesame guide is old but nothing I found contradicts it.

### 3. Does interactivity help or hurt the story?

The main source is a 2015 meta-analysis of 43 studies and 2,147 children aged 3 to 10 (Takacs, Swart and Bus).

- **Animation, music and sound effects that match the story helped comprehension** (a moderate effect), and helped disadvantaged children most.
- **Hotspots, games and dictionaries did not help.** In the pooled numbers their effect was slightly negative but not statistically significant, so "interactivity harms reading" is stronger than the data supports. For disadvantaged children the authors do call interactive features detrimental.
- The authors' explanation is that the child has to juggle two jobs at once: following the story and working the interactive bits.
- They suggest movement and zooming inside pictures may help when it happens at the moment the text mentions the thing. This is their reasoning, not something they measured. It is the closest the research comes to backing our core idea.
- Separately, an Oxford review found the best-supported feature for learning is **conversation prompts for the adult** built into the book. Parents talked more, and better, with prompts than without. A related warning: when the device does all the talking, parents tend to step back.

What this means for us: camera movement that points at what the words are about is on the right side of the evidence. Free-play hotspots are the risk. In a story they should be few, tied to the plot, and possible to switch off. Explore pages are a different activity from reading a story and should be treated as one.

### 4. Camera comfort

- Big zooms and slides are the type of animation most likely to make people feel unwell. Accessibility guidance (WCAG, and the "reduce motion" setting on phones) says to offer a way to swap them for fades.
- Material Design's guidance: make longer moves take longer, start quickly and slow down gently, and be wary of transitions much over 400 ms for ordinary interface changes. Our camera flights are closer to film moves than interface transitions, so they can be longer, but the principle of scaling time with distance holds.
- From game camera design (Itay Keren's survey of 2D game cameras): avoid camera movement that isn't needed, keep it predictable, never jump from still to full speed in one frame, and show more of what lies ahead than what is behind.

### 5. Principles I would hold the app to

1. The child's tap or drag drives time. Nothing important moves unless they caused it.
2. Movement should point at what the story is talking about, when it is talking about it.
3. Tap and single-finger drag must be enough to do everything. Pinch and double-tap are shortcuts, never the only way.
4. Every touch gets an immediate answer, even a touch on nothing.
5. No timers, no fail states, no penalty for tapping.
6. Play features sit beside the story, not in the middle of it, and an adult can turn them off.
7. Comfort first: predictable moves, a speed limit on zooming, and a reduce-motion mode.

---

## Part 2: The ideas

### A. Camera moves between stops (guided pages)

1. **Scale move time to distance.** Short hops quick, long flights slower, so speed feels even. Today every move takes the same time. **[S]**
2. **Speed limit on zooming.** Cap how fast the picture can grow or shrink per second so big zooms never lurch. **[S]**
3. **Default to "quick start, gentle stop"** and keep bounce and spring as opt-in styles per book, since overshoot is one of the comfort risks. **[S]** (have, as a setting)
4. **Tap guard.** Ignore a second tap that lands within about 300 ms, because children double-tap by accident and would skip a stop. **[S]**
5. **Impatient tap.** A tap during a move finishes that move at once, then the next tap goes on. Today it starts a new move from mid-air. **[S]**
6. **Gentle arc on long moves within a page**, pulling back slightly mid-flight so the child sees where they are going. We do this between pages already. **[S]** (have, between pages)
7. **Lead room.** Frame each stop slightly off-centre towards where the next stop is, so the eye is already pointing that way. **[M]**
8. **Per-stop move style.** Let the author choose for each stop: glide, cut, slow drift, fast whip. A comic fight wants cuts; a bedtime page wants drifts. **[M]**
9. **Per-stop speed and hold.** A "slow" flag for a dramatic reveal. **[S]**
10. **Two-box framing.** Each stop gets a "must show" box and a "nice to show" box, so the same stop frames well on an upright phone and a sideways tablet. This fixes the biggest weakness of one-box stops. **[M]**
11. **Shaped focus.** Let the dim or soft-edge focus follow a slanted panel or an oval, not only a rectangle. Our comic's slanted panels currently show slivers of their neighbours. **[M]**
12. **Slight tilt.** Rotate the camera a few degrees to square up a slanted panel or add energy. Keep it small and off in reduce-motion mode. **[M]**
13. **Curved camera paths.** Let the author draw the route between two stops, for example following a river. **[M]**
14. **Dim before move, or move before dim.** Sequence the focus effect and the camera rather than running them together; try both and see which reads better. **[S]**
15. **Lock or recommend an orientation per book.** Children mostly hold tablets sideways, and a book designed for one orientation frames poorly in the other. **[S]**

### B. Movement while resting on a stop

16. **Multi-point pans.** A stop that glides through three or more boxes, not just two. **[S]**
17. **Per-stop slow push or drift**, instead of one setting for the whole reader. **[S]** (have, as a global setting)
18. **Tap-to-reveal inside a stop.** One stop, several beats: a speech bubble appears, then the next, then the picture changes. This is the Infinite Comics idea and the one I would most like to try. **[M]**, or **[art]** when the picture itself changes
19. **Point at what the words say.** When the caption or narration mentions the bird, the bird gets a soft pulse or the camera leans towards it. Closest thing to a research-backed feature in this list. **[M]**
20. **Pan timed to the narration.** The glide lasts exactly as long as the sentence. **[S]** once audio exists
21. **Impact shake.** A short, small shake when arriving on a panel with a big sound effect. Off in reduce-motion mode. **[S]**
22. **Depth (parallax).** Foreground, middle and background slide at different speeds during a pan. Lovely when done lightly, and a known comfort risk when overdone. **[M] [art]**
23. **Small looping life.** Blinking eyes, swaying kelp, rising bubbles, twinkling stars. Story-matching animation is what the research favours. **[M] [art]**

### C. Page turns and pacing

24. **Page-turn style per book**: fly across, zoom out and across, slide, cut. **[S]** (have, as a reader setting)
25. **Scene-change transitions.** A fade through a colour or an iris close for "later that night", distinct from an ordinary page turn. **[S]**
26. **Auto-advance on narration end**, pausing the moment the child touches the screen. **[S]** once audio exists
27. **Beat stops.** A stop with no words that simply holds on a picture for a breath. Authors can already do this; the editor could make it a named option. **[S]**
28. **Child-friendly progress.** A row of dots or a little path with a marker, in place of "Page 2 of 8". **[S]**
29. **Calm ending.** A quiet closing move and "read again", since children re-read. A bedtime mode that is dimmer, slower and skips the celebration. **[S]**
30. **Whole page first or last as the reader's choice**, as ComiXology offers. **[S]** (have, as a setting)

### D. Explore pages: how dragging feels

31. **Zoom without pinching.** Big plus and minus buttons, or tap an empty spot to step in towards it. Pinch is unreliable under about five, and today it is the main way to zoom on a touchscreen. **[S]** High priority.
32. **Palm and wrist rejection.** Ignore a touch that sits still at the bottom edge while another finger moves. **[M]**
33. **Soft edges.** A rubber-band stretch and settle at the edge of the scene, so the child feels the edge. **[S]**
34. **Pan gain.** Let the picture travel further than the finger, so crossing a big scene doesn't need many long drags. **[S]**
35. **Rails journey.** The scene has a path through it and any drag moves the camera along that path. The child cannot get lost, and it makes a river or road trip feel like travelling. This is the "drag journey" you first described, and I would build it as a third page type. **[M]**
36. **Magnet stops.** When a drag ends near something worth seeing, the camera eases to frame it properly. **[M]**
37. **Follow a guide.** Tap Finn and the camera follows him as he swims to the next part of the story. **[M] [art]**
38. **Zoom that reveals.** Zooming far in on a window swaps in a picture of the room inside. **[M]**, needs extra art per reveal
39. **Auto tour.** After a long idle, the camera drifts slowly around the scene by itself until touched. **[S]**
40. **Lead in the drag direction.** Show slightly more of what lies ahead of the drag than behind. **[S]**

### E. Explore pages: guidance

41. **Hint ladder.** Step up the help the longer the child is stuck: a spoken clue, then a glow on the map, then the edge arrow, then Show me. We have the last three. **[S]**
42. **A companion in place of the arrow.** A small character that swims or flies towards the next story step. Sesame's guidance favours a character guide. **[M] [art]**
43. **Found tray.** Small outlines of the things to find that fill in as they are found. Optional per book, and kept off the bottom edge. **[M]**
44. **Map that fills in.** The corner map starts faint and colours in where the child has been. **[M]**
45. **Story trail.** A dotted line on the map joining the story steps found so far. **[S]**
46. **Riddle clues.** Each thing to find can carry a clue line ("I have eight arms and live on a ship"). **[S]**
47. **Corner map marks what's been found.** **[S]**

### F. Things to find, and how they react

48. **Answer every touch.** A soft ripple and a quiet sound wherever the child taps, even on nothing. **[S]** High priority.
49. **Minimum target size.** Grow small touch areas to about 2 cm on screen at the current zoom, and let the nearest one win. **[S]** High priority.
50. **Story spots and play spots.** Mark each thing to find as part of the story or just for fun. A parent switch turns the play spots off, and a "story first" option only unlocks them after the story is finished. **[S]**
51. **Consistency.** If one fish reacts, every fish should do something, even a small wiggle and a sound. A default reaction for "decoration" and an editor check for odd ones out. **[M]**
52. **More reactions**: wiggle, bounce, hide and peek, open and close, swim off and return. **[M] [art]**
53. **Varied replies.** Three different lines on repeat taps, so tapping the turtle again isn't dull. **[S]**
54. **Reactions that change the scene.** Find the octopus and the ship's lanterns come on. **[L] [art]**
55. **Album.** A page of everything found, to look at afterwards. **[M]**

### G. Picture effects that need little or no extra art

56. **Weather and air layers.** Bubbles, rain, snow, dust, fireflies or light rays drawn over a page by the app. Cheap, and makes flat art feel alive. **[M]**
57. **Torch mode.** A dark scene where the child drags a pool of light around to see. A natural fit for the deep sea and for night pages, and it is discovery by dragging. **[M]**
58. **Rub to reveal.** Wipe away fog, sand or snow with a finger. **[M]**, needs a second picture underneath
59. **Magic lens.** A draggable circle that shows a second version of the scene: inside the house, under the ground, the same place at night. **[M]**, needs a second picture
60. **Light changes between stops.** Tint the page warmer or darker as the story moves from afternoon to evening. **[S]**
61. **Layered pages.** A proper format for background, middle, foreground and characters as separate pieces. Unlocks parallax, loops and real reactions. **[L] [art]**

### H. Sound and voice

62. **Narration per stop**, with tap to hear it again. The single biggest gap between the prototype and a real product. **[M]**
63. **Read to me / Read by myself / Play for me** as the three reading modes. **[S]** once audio exists
64. **Words light up as they are spoken.** The evidence that this teaches reading is thin, but it is expected in the category and cheap once timings exist. **[M]**
65. **Sound on movement.** A soft whoosh scaled to the distance, a small settle sound on arrival. **[S]**
66. **Place sounds.** Background sound that changes with where the camera is: gulls at the surface, muffled quiet in the deep. Story-matching sound is on the helpful side of the research. **[M]**
67. **Music that ducks under the voice.** **[S]**
68. **Touch sounds.** Part of answering every touch. Hidden Folks did all of theirs by mouth, which is cheap and charming. **[S]**
69. **Haptics.** A light tick on a find and a soft bump at a scene edge, on phones that support it. **[S]**
70. **Record your own voice.** A parent or grandparent records the narration. **[M]**
71. **More than one language per book**, for captions and narration. **[M]**

### I. Words on screen

72. **Captions that sit near the speaker**, as a bubble anchored to a point in the picture, as well as the box at the bottom. **[M]**
73. **Captions one sentence at a time.** **[S]**
74. **Keep captions off the focus.** Place the caption above or below automatically so it never covers what the camera is showing. **[S]**
75. **Text size and a high-contrast option.** **[S]**
76. **Tap a word to hear it.** Dictionaries were among the features that did not help in the research, so off by default. **[M]**

### J. Comics

77. **Bubble-by-bubble reading inside a panel** on small screens, nudging the camera from one balloon to the next. **[M]**
78. **Sound-effect punch**: a quick pop of scale, a shake and a sound on arriving at a "CLANK!". **[S]**
79. **Cuts for action, glides for calm**, chosen per panel (see 8). **[M]**
80. **Vertical strip pages.** A third layout: one long strip scrolled by finger, where the gaps set the pace. **[M]**
81. **Double-page spreads.** **[S]**

### K. Story structure

82. **Choices.** A stop that offers two ways on. **[M]**
83. **Several stories in one scene.** Pick "Finn's light" today and "Crab's lost shell" tomorrow, in the same picture. Good value from one piece of art. **[M]**
84. **A different handful to find each time.** Five of the twenty extras chosen at random per visit. **[S]**
85. **Second-read extras.** New things unlock the second time through. **[S]**

### L. Comfort, access and settings

86. **Reduce-motion mode.** Fades and cuts in place of flights, no shake or parallax, and follow the device's own setting automatically. **[S]** High priority.
87. **Age presets in place of sliders.** "3 to 5", "6 to 8", "9 and up" set speed, target size, hint timing and whether pinch is expected. Parents should not see easing curves. **[S]**
88. **Move the centre-bottom buttons.** Whole page and Show me sit where wrists rest. Try them at the top, or higher up the sides. **[S]**
89. **Left-handed layout.** **[S]**
90. **Grown-up gate** on leaving a book and on settings, such as a three-second hold. **[S]**
91. **Hints that don't rely on colour.** Shape and movement as well, for colour-blind children. **[S]**
92. **Captions for everything spoken.** **[S]**

### M. For the adult reading along

93. **Conversation prompts.** A small, optional line for the adult at chosen stops: "Ask: why do you think the bird is sad?" The best-evidenced feature for learning in this whole document. **[S]**
94. **Parent controls**: play spots on or off, narration, auto-play, reading mode. **[S]**

### N. Editor

95. **Per-stop movement controls** (style, speed, hold) to match items 8 and 9. **[M]**
96. **Timeline preview** with a scrubber, to step through a page's moves without replaying the book. **[M]**
97. **Both orientations side by side** in the screen-size guide. **[S]**
98. **Target-size warning** when a thing to find would be under 2 cm on a phone. **[S]**
99. **Tile cutter**, so a big scene can be made from your own image inside the editor. **[M]**
100. **Suggest stops automatically**: detect comic panels, and offer them as a starting point. **[M]**
101. **Path drawing** for rails journeys and curved camera moves. **[M]**
102. **Attach audio to a stop** and set the pan length from it. **[M]**
103. **Import layered art** from a design file. **[L]**

### O. Testing with children

104. **Watch three to five children per age band** before investing in the bigger items. Things to note: missed taps, accidental exits, how long before the first drag on an explore page, whether they pinch, whether they find the story steps without Show me.
105. **Ask them to tell the story back.** This is the real test of whether the movement helps or distracts.
106. **Quiet logging in the playground**: taps, misses and time per stop, kept on the device only. **[S]**
107. **Try pairs of versions**: story-first play spots against free play spots; fast moves against slow.

---

## Part 3: What I would do, in order

First, the small things that the research says matter most. Together they are a few days of work and need no new art:

1. Answer every touch (48)
2. Zoom without pinching (31)
3. Minimum target size (49)
4. Tap guard and impatient tap (4, 5)
5. Move time scaled to distance, with a zoom speed limit (1, 2)
6. Reduce-motion mode and age presets (86, 87)
7. Story spots and play spots, with the parent switch (50)
8. Move the bottom-centre buttons (88)

Then the features that change what the product is:

9. Two-box framing and shaped focus (10, 11)
10. Narration and sound: per-stop voice, movement and place sounds (62, 65, 66)
11. Rails journey page type (35)
12. Tap-to-reveal inside a stop (18)
13. Torch mode and weather layers (57, 56)
14. Conversation prompts for the adult (93)

Last, and only once the art pipeline is settled:

15. Layered pages, and everything marked [art] (61 and onwards)

## Questions that change the order

- **What age is the core reader?** Three to five and six to eight want different defaults, and nearly every tuning decision depends on it.
- **Will the books have narration?** If yes, sound moves up to second place.
- **Can your illustrators deliver layered art?** If not, the [art] items are off the table and items 56 to 60 become the way to make pages feel alive.
- **Which devices?** If cheap Android tablets are common among your readers, heavy effects such as parallax and particles need testing on one early.

---

## Sources

Comparable apps
- [ComiXology Guided View on Kindle (Six Colors)](https://sixcolors.com/post/2017/05/comixology-guided-view-comes-to-kindle-apps/)
- [Guided View arrives on the Kindle web reader (Comics Beat)](https://www.comicsbeat.com/comixology-announces-that-guided-view-has-arrived-to-the-kindle-web-reader)
- [Current Approaches to Digital Comics (Pablo Defendini)](https://pablodefendini.github.io/digital-comics/part-1.html)
- [Madefire motion books (Batman News)](https://batman-news.com/2014/02/18/look-madefire-motion-books/)
- [Madefire: how an innovative gimmick couldn't keep up (Gutternaut)](https://gutternaut.substack.com/p/madefire-how-an-innovative-gimmick-couldnt-keep-up)
- [Webtoon 101, by a WEBTOON creator (Clip Studio)](https://tips.clip-studio.com/en-us/articles/4143)
- [Introduction to webtoon paneling (Contentcurve)](https://contentcurve.substack.com/p/introduction-to-webtoon-paneling)
- [What makes a good picture book app? (The Horn Book)](https://www.hbook.com/story/what-makes-a-good-picture-book-app)
- [Building thousands of tiny interactions into Hidden Folks (Game Developer)](https://gamedeveloper.com/design/building-thousands-of-tiny-interactions-into-i-hidden-folks-i-)

Children and touchscreens
- [Design for kids based on their stage of physical development (Nielsen Norman Group)](https://www.nngroup.com/articles/children-ux-physical-development/)
- [Best Practices: Designing Touch Tablet Experiences for Preschoolers (Sesame Workshop, via the Joan Ganz Cooney Center)](https://joanganzcooneycenter.org/?p=19607)
- [Touchscreen Interaction Design Recommendations for Children, TIDRC (University of Florida)](https://init.cise.ufl.edu/tidrc/)
- [Children's touch and gesture input studies (Anthony et al.)](https://lisa-anthony.com/wp-content/uploads/2013/04/anthony-et-al-jpuc2013.pdf)

Reading research
- [Benefits and pitfalls of multimedia and interactive features in technology-enhanced storybooks: a meta-analysis (Takacs, Swart and Bus, 2015)](https://pmc.ncbi.nlm.nih.gov/articles/PMC4647204)
- [Can the computer replace the adult for storybook reading? (Takacs, Swart and Bus, 2014)](https://www.frontiersin.org/articles/10.3389/fpsyg.2014.01366/pdf)
- [Supporting parent-child interactions with digital media (University of Oxford)](https://www.education.ox.ac.uk/oxford-education-deanery/digest/supporting-parent-child-interactions-with-digital-media-through-digital-design-can-it-be-done-and-if-so-how/supporting-parent-child-interactions-with-digital-media)

Movement and comfort
- [Scroll Back: the theory and practice of cameras in side-scrollers (Itay Keren)](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers)
- [Material Design: duration and easing](https://m1.material.io/motion/duration-easing.html)
- [prefers-reduced-motion: sometimes less movement is more (web.dev)](https://web.dev/prefers-reduced-motion)
- [Motion and animation accessibility guidance (GitHub Primer)](https://primer.style/accessibility/design-guidance/motion-and-animation)
