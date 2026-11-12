# Comic Reader for kids

A reading app for children's books drawn in comic style. Instead of showing a whole page, the reader moves a camera from panel to panel, dims the rest of the page, and zooms out when you want to see the whole page.

Android first (Kotlin + Jetpack Compose). iPhone (Swift) comes later and will read the same book format.

## How reading works

- A page is read as a series of **stops**. A stop is a box on the page: a comic panel, or any area of a picture (a face, an animal, a detail). The camera moves from stop to stop.
- By default each page opens whole first, then goes through its stops, then moves across to the next page.
- What happens around a stop is set per book and can be changed per stop: **dim** the rest (good for comics), **soft dark edges**, or **nothing** (the camera just frames it).
- A stop can **pan**: the camera arrives at one box, then glides to a second box (for example from a giraffe's legs up to her head).
- A stop can have a **caption** for books where the words aren't in the artwork.
- Tap the right side of the screen or swipe left to go on. Tap the left side or swipe right to go back. Pinch in, or press **Whole page**, to zoom out.

## Movement playground (computer)

`web/index.html` is a browser version of the reader with a settings panel (the ⚙ button, or press `S`) for trying different camera moves: speed, feel, how pages change, slow drift on a stop, focus strength, and a watch mode that moves on by itself. Settings are remembered in the browser. **Copy settings** copies them as text so the app can be set to match.

## Book editor (computer)

`web/editor.html` is where books are put together:

- **Open a book** from the library, **Open folder** (a folder of page images, with or without a `book.json`), or **New book** and add page images (button, or drag the files onto the page area).
- **Drag on a page** to draw a stop. Click to select, drag to move, drag a white corner to resize. Hold Shift to draw a new stop on top of an existing one.
- Stops play in the order listed on the right. Change the order with Earlier and Later.
- For the selected stop: the focus style, **Pan to a second box** (then drag the dashed box to where the camera should end), glide time, and caption.
- **Show screen size** draws what a phone, tablet or computer screen will actually show around the selected stop, since the screen shape rarely matches the box.
- **Preview** (or `P`) plays the book from the selected stop using the playground's movement settings.
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
content/books/                   Books, one folder per book (shared by the web playground and the app)
content/library.json             List of book folders, for the web playground
web/index.html                   Movement playground
web/editor.html, editor.js       Book editor
web/reader.js                    Reader engine shared by the playground and editor preview
android/                         Android app (open this folder in Android Studio)
  app/src/main/java/.../data/    Book format and loading
  app/src/main/java/.../ui/      Library screen, reader, camera layout
  app/src/test/                  Logic tests and screenshot tests
  app/screenshots/               Screenshots recorded by the tests
tools/make_sample_book.py        Draws the placeholder comic "Zola's Kite" (stops = panels)
tools/make_picture_book.py       Draws the placeholder picture book "Who's at the Waterhole?" (focus areas)
```

## Adding a book

The easiest way is the editor. By hand: make a folder in `content/books/` with the page images and a `book.json`, and add the folder name to `content/library.json`.

```json
{
  "id": "my-book",
  "title": "My Book",
  "author": "Your name",
  "cover": "cover.webp",
  "focusStyle": "vignette",
  "pages": [
    {
      "image": "page1.webp",
      "stops": [
        { "box": { "x": 0.38, "y": 0.43, "w": 0.31, "h": 0.43 }, "caption": "The elephant was very thirsty." },
        { "box": { "x": 0.71, "y": 0.55, "w": 0.23, "h": 0.27 },
          "to":  { "x": 0.75, "y": 0.11, "w": 0.19, "h": 0.27 }, "panSeconds": 4 },
        { "box": { "x": 0.48, "y": 0.2, "w": 0.13, "h": 0.13 }, "style": "none" }
      ]
    }
  ]
}
```

- Boxes are fractions of the page image: `x` and `y` are the top-left corner, `w` and `h` the size. `0.5` means halfway across or down.
- `focusStyle` (book) and `style` (one stop) can be `dim`, `vignette` (soft dark edges) or `none`.
- `to` and `panSeconds` make a pan stop. `caption` is optional.
- A page with no stops is shown whole (good for splash pages).
- Older files with `"panels": [ {x, y, w, h}, ... ]` still load; each panel becomes a stop.
- WebP or PNG both work. Around 1200 to 2400 pixels on the long side is plenty.

**Android app status:** the app reads this format but so far only uses each stop's box with the dim style. Pans, soft edges, "none" and captions will be added to the app once the movement is settled in the playground.

## Building

Needs JDK 17+ and the Android SDK (Android Studio installs both).

```
cd android
./gradlew assembleDebug          # APK at app/build/outputs/apk/debug/app-debug.apk
./gradlew testDebugUnitTest      # tests
./gradlew testDebugUnitTest -Proborazzi.test.record=true   # re-record screenshots
```

To try it on a phone: open `android/` in Android Studio, plug in an Android phone with USB debugging on, and press Run. Or copy the APK to the phone and install it (allow installs from unknown sources).
