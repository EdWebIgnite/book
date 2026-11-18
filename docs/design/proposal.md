# Design proposal for the reader and the editor

Read with `research.md` (rule numbers below, such as R12, point to its section 12) and the pictures in `mockups/`. Start with `mockups/overview.png`.

The mockups use the working name "kiekie" and a small yellow weaver bird as mascot. Both are placeholders for the look; the name is discussed in part 5. The mockups are HTML pictures, not the app, and show the four sample books because those are the books that exist.

## 1. Principles

1. **The picture is the hero.** Covers and pages carry the colour; the app around them is light and quiet. Why: busy surroundings pull young children off task, extras that are not the story cost understanding, and a coloured frame changes how artwork looks (research 2, 3, 4; R20, R25).
2. **One big thing to press.** Each screen has one obvious main action, at least 72 dp, and every child control is at least 56 dp and stays in the same place. Why: under-5s need large targets, miss far more often than adults, and learn by position (research 1; R1, R3, R7).
3. **Pictures first, words second.** A child who cannot read can do everything on the child's side. Words are there for the parent and the early reader. Why: 3 to 5-year-olds think in pictures and mostly cannot read (research 1; R11).
4. **Calm, with moments of delight.** Movement and sound only when they answer a touch or tell the story. No points, streaks or pleading. Why: story-matched motion helps, fast pace and rewards hurt, and nearly every app young children use tries to manipulate them (research 2, 3, 8; R14 to R19).
5. **Nothing can go wrong.** No wrong taps, no lost place, no way out of the app, no way into settings without a grown-up. Why: accidental taps are constant at this age and children cannot tell a promotion from content (research 1; R8, R9, R12, R13).
6. **A parent can trust it in ten seconds.** No adverts, no sign-up, nothing collected, works offline, said plainly on first run and kept true. Why: parents worry about screens and data, POPIA is strict about children, and data is expensive (research 7; R31 to R34).
7. **Two languages, equal.** English and Afrikaans are peers everywhere: interface, fonts, the gate, the store page. Why: the first market is both, and the gap found in the market is narrated picture books in Afrikaans (research 6, 7; R30).

## 2. Three directions

### A. Bookcase, evolved

Mockups: `a-shelf-1280.png`, `a-shelf-phone.png`, `a-reading-sideways.png`.

**Concept.** The current cherry bookcase, tidied: a pale painted back so covers stand out, the brass plate carries the child's name, a framed "Carry on reading" card sits on the shelf.

- **Palette.** Wallpaper cream `#F4EEDC`, card cream `#FBF5E6`, cherry `#A7553A` to `#8A3F2A`, bottle green `#2F6B50` (main action), deep green `#234B3B` (headings), brass `#D9B45A`, ink `#2B2118`, muted `#6B5E50`.
  Contrast: ink on cream 14.5:1; deep green on cream 9.0:1; white on bottle green 6.3:1; muted on cream 5.8:1; cherry label on cream 6.8:1; bottle green against wallpaper 5.4:1. Brass on cream is 1.8:1, so brass is decoration only, never the only edge of a control.
- **Type.** Fraunces Bold for headings and the wordmark, Nunito for the rest. As now.
- **Shapes.** Rounded rectangles (14 to 16 dp), bevelled lips, brass hairlines.
- **Shelf.** A fitted bookcase. One tall bay for books, a low bay of props. Phone: two bays of two.
- **Reading controls.** Cream round buttons, bottle-green Next, words box with a brass edge, dark green frame.
- **Parents' area.** As now: cream panel, serif heading, green switches.
- **Motion and sound.** Books settle onto the shelf, the brass plate glints once. Wooden knocks, a page rustle, a small bell.
- **Mascot.** None; the leaf sprig is the emblem.
- **Covers.** Face out on a wooden shelf with a cast shadow, against the pale back.

Against the research: keeps a familiar real-world idea (good for young children, R10) and is distinctive. But the case, mouldings, wallpaper, props and floor are decoration in the sense of the classroom study (R20), the wood takes about a quarter of a phone screen that covers could have, cherry and brass sit in the same warm range as most of the watercolour art, and a late-1800s London nursery is one culture's and one era's idea of childhood (principle 7; no direct evidence, a judgement). It also costs the most to draw on cheap phones.

### B. Sunny ledge (recommended)

Mockups: all files starting `b-`.

**Concept.** A sunlit wall with one plain picture ledge: warm paper, big covers, one sun-yellow button per screen, round friendly shapes, a small bird who turns up at the start and the end.

- **Palette.**

  | Name | Hex | Used for |
  | --- | --- | --- |
  | Paper | `#FFF8EA` | background |
  | Paper shade | `#FFEFD2` | the soft hill and sun shapes behind |
  | Card | `#FFFFFF` | cards, chips, quiet buttons |
  | Ink | `#1F2A44` | all text and icons |
  | Muted | `#5A6178` | grown-up small print |
  | Sun | `#FFC53D` (lip `#E09E12`) | the main action, the word being read (`#FFE08A`) |
  | Teal | `#0B7A75` | grown-up accents, progress, switches |
  | Coral | `#E4572E` | the bookmark ribbon, one wordmark dot |
  | Leaf | `#1F7A47` | the finished tick |
  | Ledge | `#F3E2BF` (lip `#DDC595`) | the shelf |

  Contrast: ink on paper 13.5:1; ink on white 14.3:1; ink on sun 9.0:1; ink on the highlighted word 11.1:1; muted on paper 5.8:1; teal on paper 4.9:1; white on teal 5.2:1; white on leaf 5.3:1; leaf on paper 5.1:1; coral on paper 3.5:1 (shape only, never text).
  Known weak spot: sun on paper is 1.5:1. The sun button is therefore always identified by its dark ink icon or label (9:1), its darker lip and its shadow, never by the yellow alone.
- **Type.** Fredoka SemiBold for headings and the wordmark (rounded, sturdy, Open Font License, covers Afrikaans). Nunito 700 to 800 for everything else, story words included, so existing books keep their line breaks.
- **Shapes.** Circles and pills. Cards 24 to 30 dp corners. Buttons have a 3 to 4 dp darker lip and a soft shadow, so they still look pressable (the "3D button" the current skin has, flattened a little).
- **Shelf.** Top bar; one "Carry on reading" card; "My books" on a single pale ledge. No case, no wallpaper.
- **Reading controls.** Cream glass circles; one sun Next; cream words box; nothing else coloured.
- **Parents' area.** White cards on warm paper, teal icons and switches, plain language. Visibly a different, quieter place.
- **Motion and sound.** Soft and bouncy but short: covers lift 4 dp and tilt 1.5 degrees on touch; the Carry on card's picture drifts slowly once; the bird blinks. Sounds: a soft wooden "tok" for taps, a paper swish for pages, a two-note marimba for finishing. No voice from the mascot.
- **Mascot.** Yes, small: at welcome, the picker, empty and error states, and The End. Never during a story, never asking for anything (R16).
- **Covers.** As large as fits, face out, real book corners and a spine highlight, on paper with a soft shadow. A coral ribbon bookmark means "partway"; a leaf tick means "finished".

Against the research: best match. The surround is the quietest of the three so covers are the most colourful thing on screen (R20); one accent on the thing to press (R21); states by shape (R23); the cheapest to draw; no cultural or period reference. Risks: yellow needs the care noted above; "clean and bright" is common among children's apps, so the wordmark, the ribbon and the bird must carry the character. The bookcase's charm is lost, though the ledge keeps the idea of a shelf.

### C. Lantern

Mockups: `c-shelf-1280.png`, `c-shelf-phone.png`, `c-reading-sideways.png`.

**Concept.** Story time by lamplight: a deep night-blue sky, covers lit like windows, one lamp-gold button.

- **Palette.** Night `#1B1F4B` (to `#14173A`), card `#2B3070`, cream text `#FFF4DC`, muted `#A9ACD6`, lamp gold `#FFCB5C`, lilac `#C9C5FF`, mint tick `#7FD6A4`, coral `#FF8A6B`.
  Contrast: cream on night 14.3:1; cream on card 10.9:1; night ink on gold 10.4:1; gold on night 10.4:1; lilac on night 9.6:1; muted on night 7.1:1; muted on card 5.4:1; mint on night 9.0:1.
- **Type.** Fraunces for headings (a storybook serif suits the mood), Nunito for the rest, Fredoka wordmark.
- **Shapes.** Same as B, with a thin light edge and a warm glow in place of drop shadows.
- **Shelf.** Same layout as B under a sky of still stars; the bird is asleep.
- **Reading controls.** Dark glass circles, gold Next, cream words box.
- **Parents' area.** Dark cards, lilac accents.
- **Motion and sound.** Slower and fewer: no bounce, fades only, a star twinkles now and then. Sounds a third quieter and lower.
- **Mascot.** Same bird, asleep or yawning.
- **Covers.** Glow against the dark; they look their best here.

Against the research: covers stand out most, every text pair passes easily, and a dark screen puts out less light at bedtime (research 8, though no study shows a dark theme protects sleep). Against: dark screens are hard to see in daylight on cheap LCD phones, which is when and where most reading will happen; it reads as "bedtime only" to a parent browsing the store at noon; and watercolour pages are light, so a dark frame makes them look brighter and washed out by comparison.

### Recommendation

**Build B, and ship C's palette as "Bedtime colours" inside it** (a parent switch, later an automatic evening option). They share every layout, so C costs a second set of colours and not a second design.

Reasons, in order: B follows the calm-surround findings most closely; it gives covers the most room on a small phone; it is the lightest to draw on low-end Android; it carries no era or culture; and its one-accent rule makes the main action unmistakable to a 3-year-old. A is the most characterful but spends attention and screen on furniture. If the owner wants to keep the bookcase's warmth, the cheapest carry-over into B is the wooden ledge (make it pale oak with grain) and the brass name plate under "My books".

What is lost from the current build: the cornice, wallpaper, fern and book pile; Fraunces in the day theme; bottle green as the action colour.

## 3. Screens for direction B

Sizes are dp. "Phone upright" is 360 to 430 wide; "phone sideways" 640 to 930 wide and 360 to 430 high; "tablet" 600 and up on the short side; "Windows" a window of 1024 by 640 or more.

**Common rules**

- Screen edge margin: 16 phone, 24 tablet, 40 to 64 Windows. Spacing steps: 4, 8, 12, 16, 24, 32, 48.
- Top bar: 64 high phone, 88 tablet and Windows. Wordmark left (34 phone, 46 wide). Right: the child's chip (56 high: avatar 44 plus name), then the grown-ups lock (48; with the word "Grown-ups" on wide screens).
- Main button: pill or circle, 56 to 64 high on phone uprights, 72 to 84 elsewhere, label 21 to 27 sp, always with an icon.
- Quiet button: white, same shape, 56 or more high.
- Type scale (sp): display 34 to 46, heading 22 to 28, child label 19 to 24, grown-up label 16 to 17, small print 13.5 to 14.5.
- Every screen works rotated. Books open sideways-first; on an upright phone the reading screen shows a one-time picture hint to turn the phone, and still works upright.
- Safe areas: keep 16 clear of the bottom edge and the system gesture bar; keep clear of camera cut-outs.

**3.1 First-run welcome** (`b-welcome-phone.png`). For the parent, shown once.

1. Page one: language switch at the top (English, Afrikaans; starts on the phone's language), three covers, the wordmark, one line saying what it is, three promises with icons (no adverts or sign-up or collection; works offline; read together or read aloud), main button "Set up for my child", text link "Skip and start reading".
2. Page two: child's first name (optional, 12 letters at most), pick one of 8 pictures. "Add another child" link. Says "Kept on this phone only."
3. Page three: two choices with pictures and defaults on: "Read to me" and "Words on the page". Then "Start reading".
- Three pages at most, each skippable. No account, no email, no age or birthday (R32).
- Tablet and Windows: the same pages as a centred card 520 wide on the paper background.

**3.2 Who is reading?** (`b-profiles-phone.png`). Shown at start only when there are two or more children; otherwise skipped.

- Avatars are a shape on a colour (sun, leaf, star, drop, flower, moon, boot, kite), 124 across on phones and 160 on wider screens, name below at 24 sp. Two columns upright; one row sideways and on wide screens.
- Up to 5 children. "Add a child" is the dashed circle with a small lock: it goes through the gate.
- Tapping a child goes straight to their shelf. The chip in the top bar returns here.

**3.3 Shelf** (`b-shelf-phone.png`, `b-shelf-1280.png`).

- **Carry on reading** card: shown only when a book is partway. Picture of the page the child was on (16:10), title, "Page 7 of 12" with a bar or dots, and the sun play button. The whole card is one target. Phone upright: 134 high, full width. Wide: 236 high with a "Carry on" pill 84 high.
- **My books**: covers 2:3, as large as fits. Phone upright 2 columns (cover about 166 by 249, gap 31). Phone sideways one row that scrolls sideways, cover height about 60% of the screen. Tablet 3 or 4 columns. Windows 4 to 6, cover 212 by 318 at 1280. A ledge under each row, 16 to 20 high, reaching 44 past the first and last cover.
- Order: partway books first, then unread, then finished; new books added by file go to the front once.
- **Marks**: coral ribbon from the top edge, right side, 18 to 22 wide, for partway. Leaf circle with a white tick, 40 to 46, overlapping the lower right corner, for finished. A finished book that is started again shows both.
- More than fits: scroll down; the top bar stays; no pages or tabs.
- A tap on a cover: the cover lifts and grows from where it stands into the closed book in the middle of the reading screen (300 ms), then the existing cover-opening takes over. Reduced motion: a fade.
- The bird sits at the bottom right on phones and on the card's edge on wide screens. It is not a button.

**3.4 Reading: story stops** (`b-reading-sideways.png`).

- The picture fills the screen as now. A soft dark fade (10 to 22%) at the top and bottom edges keeps controls readable on pale skies.
- Top left: back to books, 56 circle, books icon. Top right: read-aloud on or off, 56, with a small leaf dot when on. Top middle: page dots in a 24 high pill (the current page is a longer dot); a number for books over 16 pages.
- Lower left: Back, 60 circle. Lower right: Next, 80 circle in sun (96 on tablets). Both 16 to 22 from the edges. On tablets move both up to the vertical middle of the side edges, out of the wrist zone (R6).
- A tap anywhere on the picture also goes on, as now.
- Pop-up words box: bottom centre between the two arrows, cream, 22 corner, text 25 sp on phones and 30 on tablets and Windows, weight 800, centred, at most 3 lines; longer text is split across stops by the editor's check. Three small pips show which stop of the page this is.
- Read-along: the word being spoken sits on a filled sun-tint box with 5 dp padding and 8 corner. Words already read stay full ink. No bouncing ball, no colour change of the letters (R23).
- Words printed on the page keep their current look; the same highlight shape is drawn behind the printed word.
- Controls fade to 60% after 4 seconds without a touch and return on any touch. They never disappear.
- "Ask" prompts for the grown-up (existing setting): a small teal chip with a speech icon above the words box, grown-up type size.

**3.5 Reading: find-it pages** (`b-findit-sideways.png`).

- Top middle: the task in 2 to 4 words plus one slot per thing to find (36 circles; dashed when empty, leaf tick when found). The task is spoken when the page opens and again when the pill is tapped.
- Found: a ring (white 9 with sun 5 inside, about 1.4 times the thing's size) draws round it in 250 ms with a soft chime, and the caption appears in a words box near it for as long as its narration lasts.
- Right edge: zoom in, zoom out, whole page, 56 each, 12 apart. Drag to look around. Pinch works but is never needed (R4).
- "Show me" (bulb icon, 56 pill, lower right beside Next) appears after 20 seconds without a find, or straight away for the youngest age setting. It moves the camera to the next thing and pulses the ring twice. No penalty, no count of hints.
- A tap on nothing: a small ripple and nothing else. No error sound (R8).
- Next is always there; finding everything is not required.

**3.6 The End** (`b-end-sideways.png`).

- The last picture, blurred and dimmed 46%. A paper card (640 by 292 sideways; 340 wide and stacked upright): the cover with its new tick, "The End", one line of plain praise for the act ("You read the whole book, Lia!"), then "Read it again" as the main button, and below it "My books" and "Read next" with the next unread book's cover.
- The tick draws itself once (400 ms) with the two-note sound. The bird waves once. That is the whole celebration: no stars, no score, no count (R15).
- "Read next" picks the next unread book in shelf order; if none, the least recently read.
- Nothing plays on by itself. A repeat mode for parents is in part 4.

**3.7 Grown-ups' gate** (`b-gate-phone.png`).

- Lock icon, "Grown-ups only", "Tap these numbers to go on:", three numbers as words ("seven, two, nine" or "sewe, twee, nege"), three boxes, a number pad with 66 high keys.
- New numbers each time. Three misses: the pad greys for 30 seconds. Cannot be switched off (R13).
- Asked for: the parents' area, adding a child, adding or removing a book. Not asked again for 5 minutes while the parents' area stays open.
- Wide screens: a centred card 420 wide.

**3.8 Parents' area** (`b-parents-phone.png`). A full screen on phones, a 480 wide side sheet on tablets and Windows.

- **Children**: one row each with facts ("3 books finished · read on 4 days this week"). Tap for name, picture, the books read with dates, per-child reading choices, remove. "Add a child".
- **Reading**: Read to me; Words on the page; Pop-up words; Questions for the grown-up; Things to find (story first, all, off); Child's age (sets speed, hint timing and target sizes, as now); Bedtime colours.
- **App**: Language (English, Afrikaans; changes at once); Sounds; Movement (follow the phone, reduced); Books (list with sizes, add a book file, remove); Privacy (one short page: what is stored and where, nothing leaves the phone); About and licences.
- The current "fine tuning" playground settings move to a developer page reached by tapping the version number seven times, out of parents' sight.
- Rows are 60 high or more, switches 54 by 32, one idea per row with one line of explanation at most.
- Windows only: "Open a book folder" for illustrators stays, under Books.

**3.9 Empty, loading and error states.** Each is the bird, one sentence, and at most one button.

- No books: bird looking at an empty ledge. "No books yet." Button for the grown-up: lock icon, "Add a book".
- Adding a book: the new cover's outline on the ledge fills from bottom to top; "Adding the book…".
- Opening a book: the cover shows at once from the shelf picture; a thin sun bar under it if loading passes 400 ms. No spinner.
- A book that will not open: "This book would not open." Buttons "Try again" and "My books". Details for the grown-up behind the lock.
- A book file that is not valid or too new: plain reason in the parents' area ("This file is not a book" or "This book needs a newer version of the app").
- Storage full: "There is not enough room on this phone for this book (needs 12 MB)."
- No narration in this book: the read-aloud button shows a line through it and says so once when tapped.

**3.10 What changes by screen**

| | Phone upright | Phone sideways | Tablet | Windows |
| --- | --- | --- | --- | --- |
| Shelf columns | 2 | 1 row, scrolls sideways | 3 to 4 | 4 to 6 |
| Carry on card | full width, 134 high | left third, full height | full width, 200 high | full width, 236 high |
| Reading | works; hint to turn | the design size | Next and Back at mid-height, 96 | arrow keys and space also work; pointer shows hover lift |
| Words box text | 22 | 25 | 30 | 30 |
| Parents' area | full screen | full screen | side sheet 480 | side sheet 480 |
| Child targets | 56 min, main 64+ | 56 min, main 80 | 64 min, main 96 | 56 min, main 84 |

Windows extras: keyboard works throughout (Tab order follows reading order, Enter presses, Esc goes back to books); a visible 3 dp ink focus ring; the window can be any size down to 640 by 400.

## 4. Features

**v1: the must-haves**

| Feature | Notes |
| --- | --- |
| Child profiles | Up to 5; nickname and picture only; on the device only (R32). |
| Reading progress | Per child, per book: last page and stop, finished yes or no, times finished, date last read. Shown as Carry on, ribbon and tick. |
| First-run welcome | 3 skippable pages (3.1). |
| English and Afrikaans app text | All strings in both; follows the phone's language; switch in the parents' area and on the welcome. Fonts checked for ê ë ô û ʼn (R30). |
| Grown-ups' gate and parents' area | 3.7 and 3.8. Needed for Google Play's families rules and for trust. |

**v1: small additions worth the cost**

| Feature | Why | Cost |
| --- | --- | --- |
| "Read it again" and "Read next" at The End | Re-reading is how young children learn a book; one suggestion saves a trip (research 1). | Small |
| Tap a word to hear it (pop-up and printed words, when read-aloud is paused or finished) | Uses word timings the books already have; links print to sound without adding a game. Evidence modest. | Small to medium |
| Bedtime colours (direction C palette, softer sounds, no bounce) | Bedtime is the main use; parents ask for it; second palette only. | Small |
| Book language shown on the cover (a small "EN" or "AF" tag) when the shelf holds both | A parent must be able to tell before opening. | Small |
| Offline and privacy statement on the welcome and in the parents' area | The strongest trust signal, already true. | Tiny |
| Empty, loading and error states with plain words | 3.9. | Small |

**Later**

| Feature | Why later |
| --- | --- |
| Book details sheet for parents (ages, length in minutes, language, illustrator, a line about the story, size) | Useful once there are more than about 8 books; needs new fields in book files. |
| "Stop after this book" and a quiet reading timer for parents | Good for bedtime and screen-time worries. Must end at a book's end, never mid-story, with no countdown shown to the child. |
| Evening auto-switch to bedtime colours | After the manual switch has proved itself. |
| Favourites (a heart the child sets) | Only matters with a bigger shelf; adds a control to every cover. |
| Record your own voice (parent or grandparent reads a book) | Strong pull for families, and for languages the books lack. Needs microphone permission, which ends "asks for no permissions"; it must be opt-in and stored on the device only. |
| "Easier letters" option (Andika) per child | Evidence says the best font differs by child; needs books re-flowed and checked. |
| Reading tips for parents (one line at The End now and then, for example an open question to ask) | Prompts raise parent talk (research 2). Write and translate carefully; parent switch. |
| More languages (isiZulu, isiXhosa and others) for app text and books | Where the need is greatest (research 7). Structure the strings for it from v1. |
| Text size choice for pop-up words (normal, large) | Simple, but must not break the editor's fit checks. |
| A book catalogue to download more books | A business decision first; brings the internet permission, a privacy policy with more in it, and data-cost care (sizes shown, Wi-Fi only by default). |
| Carry progress to a new phone (export and import a file) | Without accounts. Rarely needed at first. |

**Not recommended**

| Feature | Reason |
| --- | --- |
| Stars, badges, streaks, levels, reading goals for the child | Rewards for reading lower later interest; badge-chasing is the known failure of the big apps (R15). |
| Notifications and reminders | A manipulation pattern at this age; also needs a permission. |
| Autoplay into the next book | Removes the natural stopping point parents rely on. |
| Mini-games, stickers, colouring unrelated to the story | Extras that are not the story reduce understanding (research 2). |
| Accounts, cloud sync, analytics, adverts, links out | Each one breaks a promise on the welcome page and brings POPIA and store duties (research 7). |
| Speaking mascot or a mascot that reacts to leaving | Parasocial pressure (R16). |
| Leaderboards or comparing children | Parents see facts per child; children see nothing about each other. |
| Dyslexia-specific font as a headline feature | No good evidence for such fonts; the "easier letters" option covers the need honestly. |

## 5. Names

A quick web search was run on each for apps, books and children's brands with the same or a near name. This is a sanity check only, not a trademark search or legal clearance. Before choosing, check the CIPC trade mark register (classes 9, 16 and 41), Google Play, the App Store and the .co.za and .com domains.

| Name | Meaning and sound | Clash found |
| --- | --- | --- |
| **Toktokkie** | The tapping beetle, and the children's knock-and-run game. "Tok" is the sound of a tap, which is how the books move. Said the same in English and Afrikaans: tok-tok-kee. Known to most South Africans; unknown abroad. | None found for apps, books or children's brands. A walking-trail business in Namibia uses "Tok Tokkie" (from memory, different field). |
| **Kiekie** | Afrikaans for a snapshot. The books are pictures a camera moves across. Short; a child says "kee-kee". Means nothing in English, which is fine for a brand. | No app of that name found, but "Keiki" is an established toddler learning app in the same store category, and "Kiki" names are many. Real risk of confusion. |
| **Dassie** | The rock hyrax that suns itself on rocks and peeks out. Same word in both languages; easy to say; a ready mascot. | No brand or app found. Dassies appear in several children's book titles, which is harmless. |
| Peekaboek | Peekaboo plus "boek". Clever in both languages. | "Peekabook" is an existing children's interactive book app, and "Peekaboo" book apps are many. Drop. |
| Koekeloer | Afrikaans: to peer about curiously. Lovely meaning. Hard for English speakers to spell and say. | "Koekeloere" is used for Dutch children's services. Drop. |
| Mossie | The Cape sparrow; fond and familiar in Afrikaans. English ears abroad hear "mozzie" (mosquito). Echoes Moss the bear, which would tie the app to one book. | None found. |
| Bokkie | Little buck; also "darling". Warm in both languages. | No app found. Some adults will think of an old fire-safety mascot (from memory, not checked). |
| Storiekas | Afrikaans: story cupboard. Fits a bookcase. Opaque to English speakers. | None found. |
| Puddle Books | From the first book. English only; "Plas" does not carry over. Ties the name to one title. | None found in a brief search. |
| Loer | Afrikaans: to peek. Four letters. English readers say "lower" or "lure". | Not searched. "Loerie" is a well-known South African awards name. |

**Top three**

1. **Toktokkie.** The only one that names what a child does (tap, tap) and that turned up no neighbour at all. It gives a mascot (a small round beetle) and a sound for the interface (the wooden "tok"). Weak points: nine letters, so the icon label is long, and it is South African only. Tagline: "tap, tap, read" and "tok, tok, lees".
   Wordmark: lowercase Fredoka SemiBold in ink; the two o's are solid dots, one sun yellow and one coral, like two taps. App icon: a "t" on sun yellow with a coral dot.
2. **Kiekie.** The best meaning and the shortest, and the one used in the mockups. Held back only by "Keiki". If a trade mark attorney is comfortable, it is the nicer name.
   Wordmark: lowercase Fredoka SemiBold in ink; the dots of the two i's are sun yellow and coral. Icon: a "k" on sun yellow with a coral dot.
3. **Dassie.** Safe, friendly, bilingual, mascot built in. Says nothing about books or pictures, so it leans on a tagline: "picture books that move" and "prenteboeke wat beweeg".
   Wordmark: lowercase Fredoka SemiBold in ink with a small tan dassie lying along the top of the double s. Icon: the dassie's head over a "d".

All three are drawn in `mockups/wordmarks.png`. Each works in one colour, on light and on dark, and at icon size. If Toktokkie is chosen, the mascot in the mockups becomes the beetle; nothing else in the design changes.

## 6. The Book Editor

The editor is a tool for adults doing careful colour work, so it should be calmer and denser than the reader and share only the brand's marks. In rough order of value:

1. **Neutral workspace.** Make the area around the page a mid grey (about `#3A3D42` to `#4A4D52`), not near-black, and keep panels neutral grey with no blue tint. A very dark or coloured surround changes how illustrators judge their own colours (research 10). Offer a light grey option.
2. **One accent, used sparingly.** Replace the orange with the brand's sun yellow for selection and the primary action only (Save when there are changes). Stop colours stay as they are but should be checked for colour-blind safety and always carry their number.
3. **A real toolbar.** Group the top row into File (New, Open, Recent, Save), History (Undo, Redo), and Output (Play, Check book, Export), with icons and labels, dividers between groups, and tooltips that show the shortcut. Show the save state in words beside the title ("Saved", "Changes not saved").
4. **Tidy the device previews.** Turn the row of tick boxes (phone upright, sideways, small phone, tablet, computer) into one "Show screens" dropdown or a segmented strip, so the canvas header is one line.
5. **Inspector in collapsible sections.** Stop, Words, Read aloud, Movement, Effects, each with a header that folds, remembering what was open. Put the most used field first and move rare options behind "More".
6. **Consistent type and spacing.** Nunito throughout at two sizes (13 and 15) and two weights; an 8 dp grid; one corner radius (6); one button height (32) with a larger 36 for primary. Section labels in small capitals as now, but one style.
7. **Stop strip as a timeline.** Keep the thumbnails; add drag to reorder, a clear insertion line, and the same number badges as the canvas. Show voice and timing state with icons plus words.
8. **Check book as a list you can act on.** Each problem with a plain sentence, a severity icon and a "Go to" that selects the page and stop. A count on the toolbar button.
9. **First-use help.** The welcome screen offers "Open the sample book" and a five-step picture guide; empty panels say what to do ("Drag on the picture to draw a stop").
10. **Brand, lightly.** The wordmark small at top left with "Book Editor" beside it, the same Fredoka for the title only, the same icon set as the reader. Language of the editor follows the same English and Afrikaans strings where they overlap.
11. **Keyboard and precision.** Arrow keys nudge a selected box by 1 unit, Shift by 10; number fields for x, y, w, h; zoom to fit, 100% and to selection; Ctrl+Z everywhere, including text.
12. **Preview that matches.** The preview window uses the reader's new skin and offers Bedtime colours and both languages, so illustrators see what families will.

## 7. Files

- `research.md`: findings with sources and the numbered rules.
- `proposal.md`: this file.
- `mockups/overview.png`: the three directions side by side.
- `mockups/wordmarks.png`: the three wordmarks.
- Direction A: `a-shelf-1280.png`, `a-shelf-phone.png`, `a-reading-sideways.png`.
- Direction B: `b-shelf-1280.png`, `b-shelf-phone.png`, `b-reading-sideways.png`, `b-findit-sideways.png`, `b-end-sideways.png`, `b-welcome-phone.png`, `b-profiles-phone.png`, `b-gate-phone.png`, `b-parents-phone.png`.
- Direction C: `c-shelf-1280.png`, `c-shelf-phone.png`, `c-reading-sideways.png`.
- `src/`: the HTML, CSS and scripts that draw them (`node src/build.js`, then `node src/sheets.js`). Fonts are Fredoka, Nunito, Fraunces (all SIL Open Font License, fetched from Google Fonts).

Not drawn: tablet upright, phone-sideways shelf, the Afrikaans versions of the screens, bedtime colours for the screens beyond the three in direction C, and the editor. The rules in part 3 cover them.
