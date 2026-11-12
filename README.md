# Comic Reader for kids

A reading app for children's books drawn in comic style. Instead of showing a whole page, the reader moves a camera from panel to panel, dims the rest of the page, and zooms out when you want to see the whole page.

Android first (Kotlin + Jetpack Compose). iPhone (Swift) comes later and will read the same book format.

## How reading works

- Each page opens as a whole page, then the camera zooms to panel 1, panel 2 and so on.
- After the last panel it pans across to the next page.
- Tap the right side of the screen or swipe left to go on. Tap the left side or swipe right to go back.
- Pinch in, or press **Whole page**, to zoom out. Pinch out, or press **Panels**, to go back in.

## Project layout

```
android/                         Android app (open this folder in Android Studio)
  app/src/main/assets/books/     Bundled books, one folder per book
  app/src/main/java/.../data/    Book format and loading
  app/src/main/java/.../ui/      Library screen, reader, camera layout
  app/src/test/                  Logic tests and screenshot tests
  app/screenshots/               Screenshots recorded by the tests
tools/make_sample_book.py        Draws the placeholder book "Zola's Kite"
```

## Adding a book

Make a folder in `android/app/src/main/assets/books/` with the page images and a `book.json`:

```json
{
  "id": "my-book",
  "title": "My Book",
  "author": "Your name",
  "cover": "cover.webp",
  "pages": [
    {
      "image": "page1.webp",
      "panels": [
        { "x": 0.04, "y": 0.03, "w": 0.92, "h": 0.36 },
        { "x": 0.04, "y": 0.41, "w": 0.45, "h": 0.27 }
      ]
    }
  ]
}
```

- Panel boxes are fractions of the page image: `x` and `y` are the top-left corner, `w` and `h` the size. `0.5` means halfway across or down.
- List panels in reading order. The camera follows that order.
- A page with no `panels` is shown whole (good for splash pages).
- WebP or PNG both work. Around 1200 to 2000 pixels wide per page is plenty.

Working out panel boxes by hand is slow, so a simple tool to draw boxes on a page and export `book.json` is a sensible next step.

## Building

Needs JDK 17+ and the Android SDK (Android Studio installs both).

```
cd android
./gradlew assembleDebug          # APK at app/build/outputs/apk/debug/app-debug.apk
./gradlew testDebugUnitTest      # tests
./gradlew testDebugUnitTest -Proborazzi.test.record=true   # re-record screenshots
```

To try it on a phone: open `android/` in Android Studio, plug in an Android phone with USB debugging on, and press Run. Or copy the APK to the phone and install it (allow installs from unknown sources).
