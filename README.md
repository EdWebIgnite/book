# Comic Reader for kids

A reading app for children's books where the camera does the storytelling: it moves between areas of a page, pans across a picture, or hands the page to the child to explore by touch.

**Where things stand:** the movement was worked out in the browser (`web/`), which stays as the quick test bed. The reader is now also a desktop program in C# and Avalonia (`desktop/`), which plays the same book files; the book editor moves there next, and the phone apps will reuse its reader library. The Android prototype (`android/`) is parked.

## How reading works

There are two kinds of page, and a book can mix them.

**Guided pages.** The page is read as a series of **stops**. A stop is a box on the page: a comic panel, or any area of a picture (a face, an animal, a detail). The camera moves from stop to stop.

- By default each page opens whole first, then goes through its stops, then moves across to the next page.
- What happens around a stop is set per book and can be changed per stop: **dim** the rest (good for comics), **soft dark edges**, a **spotlight** (an oval of light with everything else dark), or **nothing** (the camera just frames it).
- A page can skip the whole-page opening and start on its first stop. Together with spotlight stops this keeps a surprise hidden until its moment, such as the first raindrop in the Moss book.
- A stop can **pan**: the camera arrives at one box, then glides to a second box (for example from a giraffe's legs up to her head).
- A stop can have a **caption** for books where the words aren't in the artwork.
- A stop can set **how long the camera takes to reach it** (or cut straight to it), and can **go on by itself** after a rest, so several moves play from one tap.
- Tap the right side of the screen or swipe left to go on. Tap the left side or swipe right to go back. Pinch in, or press **Whole page**, to zoom out.

**Moving effects.** The reader can draw things moving over a page: **rain** (with splash rings and a rain sound), **snow**, **falling leaves** and **sparkles**. Each can run for the whole page or only between two stops, and can have areas it misses, such as under a bench. A stop can also have a soft **lightning** flash with a rumble, or a **camera shake**. Reduced motion switches the movement off. The picture should be painted without the rain or snow, or both will show.

**Pictures on top.** A page can carry extra pictures with see-through backgrounds, such as a character or a prop. Each can appear and go away at chosen stops, move to a second place when a stop arrives (straight, or in a hop), and bob or sway while it waits.

**Sound.** A stop can play a sound file when the camera arrives: a line of narration or a sound effect. A stop that goes on by itself waits for its sound to finish. Things to find can have a sound too.

**Explore pages.** The child is in control: drag to move around, pinch (or double-tap) to zoom, flick to glide. Good for big scenes such as a wide panorama.

- Any stops on an explore page play first as a short **guided intro**, then the page is handed over.
- A page can have **things to find**: spots that sparkle and show a caption when tapped. A page with none is free looking only.
- Guidance is switched on per page: a **hint** (a glow, or an arrow at the screen edge) when the child has been idle, and a **Show me** button that flies to the next thing not found yet.
- The next arrow moves on to the following page.

**Big scenes.** An explore page can be a huge picture, many screens across and down. The picture is cut into tiles so only the part being looked at loads sharp, which keeps it quick on a phone.

- A scene can carry a **story**: some of the things to find are numbered story steps. They open in order, the next one always shows a soft beacon, and tapping a later one too early says "Not yet! First find..." and names the one to look for. When the last step is found, stars rain down.
- Everything else in the scene is an extra to find, in any order.
- A **corner map** shows the whole scene and where the child is. Touching the map moves the view there.

## Built for under-nines

The reader follows what the research says about young children and touchscreens (the findings, sources and the full ideas list are in `docs/movement-ideas.md`):

- Everything works with a tap or a one-finger drag. Zoom has + and − buttons, so nobody has to pinch.
- Every touch gets an answer: a ripple and a soft sound, even on empty space.
- Things to find have a touch area of at least 2 cm, and accidental double taps are ignored.
- Longer camera moves take longer and zooming has a speed limit. A reduced-motion mode swaps flights for fades and follows the device's own setting.
- Controls are picture buttons, kept off the bottom edge where wrists rest. Hints are pictures too (a tapping or dragging hand), since most of these children can't read yet.
- Captions never cover what the camera is showing: the picture moves up to make room.
- Stops and things to find can carry a short question for the adult ("Ask: how is Moss feeling now?").
- Parents get age presets (3 to 5, 6 to 8, 9 and up), a switch for the just-for-fun things to find (always, after the story, or never), sound, word size and an optional hold-to-open lock on the settings.

## Sample books

Original books, there to exercise every feature. Moss and the Biggest Puddle has painted artwork (made with an image tool from the brief in `docs/art-brief/`); the other three are placeholders drawn in code.

- **Moss and the Biggest Puddle.** A picture book written for ages 4 to 5. Moss the bear cub has new red boots and no puddle. Eleven guided pages with short sentences, a repeating "too..." pattern, pans, weather that changes the whole garden, and questions for the adult. It ends on the whole garden to explore, where Dot the duck has lost her three ducklings. The paintings are in `art/biggest-puddle/`; `python3 tools/make_puddle_pages.py` turns them into the page images, and the camera stops are in the book's `book.json`.
- **Who's at the Waterhole?** A picture book. Seven guided pages using focus areas, pans and captions, then a wide evening scene to explore with ten things to find.
- **Captain Megawatt and the Runaway Robot.** A three-page comic with the words lettered into the art. Every stop is one panel, using the dim style.
- **The Light in the Deep.** One big underwater scene (8192 x 6144), from the sunny surface down to the dark sea floor. A six-step story leads from Finn the clownfish to the light at the bottom, with twenty extras to find along the way.

## Movement playground (computer)

`web/index.html` is a browser version of the reader with a settings panel (the ⚙ button, or press `S`) for trying different camera moves: speed, feel, how pages change, slow drift on a stop, focus strength, how far the picture glides after a flick on explore pages, how soon hints appear, and a watch mode that moves on by itself. Close the panel with the Close button, `S` or `Esc`. Settings are remembered in the browser. **Copy settings** copies them as text so the app can be set to match.

## Book editor (computer)

`web/editor.html` is where books are put together. It is for the people making the books, so the playground has no link to it: open it directly, hold the "My Books" title for three seconds, or press Shift+E on the shelf.

**Layout.** Pages run down the left, the page you're working on is in the middle, and the settings for whatever is selected are on the right. Along the bottom is the page's timeline: how it opens, then each stop as a card with a small picture, its words and its timing, joined by "tap" or "after 2s". Drag a card to change the order. On explore pages the things to find follow as chips.

**Two ways to work.** *Everything* shows every tool at once. *Step by step* takes one job at a time: Pages, Camera, Words (every stop's words in one list), Effects, Play. Switch between them at the top whenever you like.

- **Open a book** from the library, **Open folder** (a folder of page images, with or without a `book.json`), or **New book** and add page images (button, or drag the files onto the page area).
- **Drag on a page** to draw a stop. Click to select, drag to move, drag a white corner to resize. Hold Shift to draw a new stop on top of an existing one.
- Stops play in the order shown along the bottom. Drag a card there to move it, or use Earlier and Later.
- **Show the whole page first** can be switched off per page.
- For the selected stop: the time the camera takes to get there, whether it goes on by itself, the focus style (including Spotlight), **Pan to a second box** (then drag the dashed box to where the camera should end), glide time, and caption.
- **Page type** switches a page between guided and explore. On an explore page you choose whether dragging draws intro stops or **things to find**, give each thing its name, caption and (if it's part of the story) its story step number, and switch hints, the Show me button and the corner map on or off.
- **Moving effects** for the page: add rain, snow, leaves, sparkles, lightning or a camera shake, and set how much, which stops they run between, and areas they miss (drawn on the page).
- **Pictures on top**: add a PNG, drag it into place, size it by a corner, choose when it appears, and drag its faded copy to where it should move to.
- **Sound**: choose a sound file for a stop or a thing to find.
- **Quicker editing**: Duplicate (Ctrl+D), Copy and Paste between pages (Ctrl+C, Ctrl+V), and drag pages in the left strip to reorder them.
- **Big pictures**: a page wider or taller than 4096 pixels gets a tick box to cut it into tiles when saving to a folder.
- **Show screen size** draws what a phone, tablet or computer screen will actually show around the selected stop, since the screen shape rarely matches the box.
- **Play** (or `P`) plays the book from the selected stop using the playground's movement settings.
- **Save** writes `book.json` (and any new images) into a folder in Chrome or Edge. In other browsers it shows the `book.json` text to copy.
- Undo with Ctrl+Z. Delete removes a stop, arrow keys nudge it.

## Running the playground and editor

Start a small web server in the repo folder and open the pages:

```
python3 -m http.server 8000
# then open http://localhost:8000/web/          (playground)
#      and http://localhost:8000/web/editor.html (editor)
```

Opening the files straight from disk won't work, because browsers block them from reading the book files.

## Project layout

```
content/books/                   Books, one folder per book (page images + book.json)
content/library.json             List of book folders, for the playground and editor
web/index.html                   Movement playground
web/editor.html, editor.js       Book editor
web/reader.js                    Reader engine shared by the playground and editor preview
web/reader-ui.js, reader.css     The reader screen (buttons, captions, hints), also shared
docs/movement-ideas.md           Research on comparable apps and on children and touchscreens, and the ideas list
docs/art-brief/                  Brief for the illustrator of Moss and the Biggest Puddle (open index.html)
art/biggest-puddle/              Painted artwork for Moss and the Biggest Puddle, with notes on how it was exported
tools/make_books.js              Draws the three placeholder books (node tools/make_books.js)
tools/make_puddle_pages.py       Turns the Moss paintings into page images and letters the cover
tools/make_puddle_extras.js      Makes the Moss book's demo butterfly picture and splash sound
tools/make_brief_images.js       Draws the character sheet used in the art brief
tools/art/                       The drawings: garden.js and puddle.js (the old drawn Moss book, now only layout roughs), kit.js and waterhole.js
                                 (waterhole book), comic.js and megawatt.js (comic), sea.js and deep.js (underwater scene)
tools/art/fonts/                 Open-licence fonts used in the art (Baloo 2, Bangers, Comic Neue), with their licences
desktop/                         The reader as a desktop program (C#, Avalonia). See desktop/README.md
android/                         Android app, parked for now
```

## Adding a book

The easiest way is the editor. By hand: make a folder in `content/books/` with the page images and a `book.json`, and add the folder name to `content/library.json`.

```json
{
  "id": "my-book",
  "title": "My Book",
  "author": "Your name",
  "cover": "cover.jpg",
  "focusStyle": "vignette",
  "pages": [
    {
      "image": "page1.jpg",
      "stops": [
        { "box": { "x": 0.38, "y": 0.43, "w": 0.31, "h": 0.43 }, "caption": "The elephant was very thirsty." },
        { "box": { "x": 0.71, "y": 0.55, "w": 0.23, "h": 0.27 },
          "to":  { "x": 0.75, "y": 0.11, "w": 0.19, "h": 0.27 }, "panSeconds": 4 },
        { "box": { "x": 0.48, "y": 0.2, "w": 0.13, "h": 0.13 }, "style": "none" }
      ]
    },
    {
      "image": "page2.jpg",
      "mode": "explore",
      "stops": [
        { "box": { "x": 0.27, "y": 0.5, "w": 0.22, "h": 0.48 }, "caption": "Who can you find?" }
      ],
      "hotspots": [
        { "box": { "x": 0.44, "y": 0.81, "w": 0.05, "h": 0.09 }, "caption": "Ribbit! A frog on a lily pad." }
      ],
      "guidance": { "hints": true, "showMe": true }
    }
  ]
}
```

- Boxes are fractions of the page image: `x` and `y` are the top-left corner, `w` and `h` the size. `0.5` means halfway across or down.
- `focusStyle` (book) and `style` (one stop) can be `dim`, `vignette` (soft dark edges), `spotlight` or `none`.
- `"openWhole": false` on a page makes it open on its first stop instead of showing the whole page first.
- `moveSeconds` is how long the camera takes to reach a stop (`0` cuts to it). `"auto": true` with `holdSeconds` makes the stop go on by itself after resting that long.
- `to` and `panSeconds` make a pan stop. `caption` is optional. `ask` is an optional question for the adult reading along.
- A page with no stops is shown whole (good for splash pages).
- `"mode": "explore"` makes an explore page. Its `stops` are the guided intro, `hotspots` are the things to find (add `"effect": "none"` for words without the sparkle), and `guidance` switches hints, the Show me button and the corner map (`"map": true`).
- `"effects"` on a page adds moving effects. `{ "type": "rain", "amount": 0.7, "from": 2, "to": 3, "ground": 0.62, "dry": [ { box } ] }`: the type can be `rain`, `snow`, `leaves` or `sparkles`; `from` and `to` are the stops it runs between; `ground` (rain only) is how far down the page the ground begins, for splashes; `dry` lists boxes it misses. `{ "type": "lightning", "stop": 1 }` and `{ "type": "shake", "stop": 1, "strength": 0.6 }` happen on arriving at a stop.
- `"layers"` on a page adds pictures on top: `{ "image": "boat.png", "box": {...}, "from": 2, "to": 3, "moveTo": {...}, "moveAt": 2, "moveSeconds": 1.2, "path": "hop", "idle": "bob" }`. Only `image` and `box` are needed.
- `"sound": "file.mp3"` on a stop or a hotspot plays that file from the book's folder.
- A hotspot with `"story": 1` (then 2, 3 and so on) is a story step; give story steps a `"name"` so the "First find..." message can name them.
- A big scene adds `"tiles": { "width": 8192, "height": 6144, "size": 1024, "levels": 2, "path": "page1_tiles/{z}/{x}_{y}.jpg" }` to the page. `image` is then a small overview of the whole scene, level 0 holds the full-size tiles and each level after is half the size. `tools/make_books.js` shows how the sample's tiles are cut. The editor cuts tiles itself when saving to a folder.
- Older files with `"panels": [ {x, y, w, h}, ... ]` still load; each panel becomes a stop.
- JPEG, PNG or WebP all work. Around 2000 to 3200 pixels on the long side is plenty for a normal page; wide explore scenes can be bigger.

**Android app status:** parked while the movement is worked out in the browser. It opens these books but only uses each stop's box with the dim style: no pans, soft edges, captions or explore pages yet.

## Building

Needs JDK 17+ and the Android SDK (Android Studio installs both).

```
cd android
./gradlew assembleDebug          # APK at app/build/outputs/apk/debug/app-debug.apk
./gradlew testDebugUnitTest      # tests
./gradlew testDebugUnitTest -Proborazzi.test.record=true   # re-record screenshots
```

To try it on a phone: open `android/` in Android Studio, plug in an Android phone with USB debugging on, and press Run. Or copy the APK to the phone and install it (allow installs from unknown sources).
