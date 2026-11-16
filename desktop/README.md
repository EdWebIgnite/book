# Book Reader for the desktop

This folder holds the reader as a C# program for Windows, Linux and macOS, and as an app for Android phones and tablets, built with [Avalonia](https://avaloniaui.net/). It opens the same books as the browser version in `web/` and is meant to behave the same way. The book format (`book.json`) is unchanged: both readers use the files in `content/`.

The browser version is still the reference. When the two disagree, `web/reader.js` is right and this is a bug.

## What is in here

| Folder | What it is |
| --- | --- |
| `Reader.Core/` | The reader with no screen attached. Geometry, settings, the book format, and the engine that decides where the camera is, what is in focus, what has been found and what should be heard. It uses no drawing or window code at all, so it can be tested on its own and reused by the editor and the phone apps. |
| `Reader.Avalonia/` | The reader on screen. `ReaderView` moves the engine on once a frame and passes on touches, the wheel and the keyboard; its drawing is in `ReaderPaint.cs`. `ReaderScreen` adds the buttons, the caption and The End box. `BookFiles.cs` loads the pictures, including the tiles of big scenes. |
| `Reader.Audio/` | Sound. The small interface sounds are made in code (`Synth.cs`); the SoundFlow library opens the sound device and reads sound files. |
| `Reader.App/` | The whole app as one view: the bookshelf, the reader screen and the settings panel, and how they hand over to each other. Shared by the Windows program and the phone app. |
| `Player/` | The program for computers: a window holding `Reader.App`, the command line, the screenshot hook and the bench. |
| `Phone/` | The app for Android phones and tablets: `Reader.App` on the phone's screen, with the books packed inside. See "Android" below. |
| `Editor/` | The book editor, `BookEditor.exe`: a program of its own for the people who make the books. See "The book editor" below. |
| `Editor.Core/` | The editor's logic with no window: opening and saving book folders, undo, the safety copy, joining and shrinking pictures, Check book and Export, lining up recognised words with a caption (`WordTiming.cs`) and downloading the speech recogniser's file (`SpeechModel.cs`). |
| `Reader.Tests/` | Tests for `Reader.Core`, the synthesised sounds, narration, book files, and the editor's logic (word timing included). |

Two things to know when reading the code:

- The code in `Reader.Avalonia/` uses the namespace `Reader.Ui`. A namespace ending in `.Avalonia` would hide the real `Avalonia` namespace from the code around it.
- The engine never looks at a clock. Whoever drives it passes the time in (`Update(nowMs)` once a frame, `SetTime(nowMs)` before a touch), which is what lets the tests and the screenshot hook use made-up time.

## What you need

The .NET 10 SDK. Everything else is fetched by the first build.

On this project's Linux build machine the SDK is in `/opt/dotnet`, so each command below needs this first:

```
export DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
```

## Build

```
cd desktop
dotnet build BookReader.sln
```

The solution includes the Android app, so building all of it needs the Android tools too (see "Android" below). Without them, build just the Windows program with `dotnet build Player`.

## Run

```
dotnet run --project Player
```

The Player looks for the books in this order: the folder given with `--content`, a `content` folder beside the program, then the nearest `content/library.json` above the program or the current folder. Run from the repo, it finds the sample books by itself. "Open a book folder" on the shelf (or Ctrl+O) opens any folder with a `book.json` in it.

```
dotnet run --project Player -- --content /path/to/content
dotnet run --project Player -- --book biggest-puddle --page 7 --stop 2
dotnet run --project Player -- --help
```

Keys: Right or Space goes on, Left goes back, W shows the whole page, + and - zoom on explore pages, S opens the settings, F shows the frame rate, Esc goes back to the books.

`--set key=value` uses a setting for one run, for example `--set autoPlay=true --set autoSeconds=1` to watch a book play by itself. `--fps` starts with the frame-rate readout showing.

Settings are kept between runs in `settings.json` in the user's own app-data folder: `%APPDATA%\BookReader` on Windows, `~/.config/BookReader` on Linux. The file uses the same names as the browser playground, and "Copy settings" puts the same text on the clipboard.

The file carries a `settingsVersion`. A file saved before version 2 (by the first desktop build) is brought up to date once when it is loaded: the whole page is shown again after its stops, the book look is switched on and the page change becomes the flip. Everything else in it is kept, and after that the file is left as the user sets it.

## The open book, the page turn and the cover

Two settings, both on to start with, make the reader look and turn like a real picture book. They are in the settings panel under Fine tuning, Between pages.

- **Show as an open book** (`bookLook`). Each landscape picture lies across both pages of an open book, as a double-page spread: the crease runs down the middle of the picture. Around the pages are a cover board, the edges of the pages underneath (more on the left the further into the book you are, more on the right at the start) and a soft shadow. The crease and the pages' curve into the binding are fully there when the book is seen whole and fade out as the camera goes in, so a close-up never has a dark line through a character. A portrait picture (the comic) is the right-hand page, and the left-hand page shows the picture before it, or plain paper. Pages being explored and tiled big scenes are drawn as before, with no book round them. With the setting off, pages lie side by side as they do in the browser.
- **Page change: Flip like a book page** (`pageTurn` = `flip`). Going from one whole spread to the next, the right-hand page lifts from its outer edge and rolls over to the left, bending as paper does, its back showing the left half of the next picture while the right half is uncovered underneath. Going back, the left-hand page rolls over to the right. It takes 900 ms at the standard speed: it lifts gently, and the last part of the landing slows right down rather than stopping short (`Easings.SoftLanding`). From a close-up, the camera first eases out to the whole book.
- **The cover.** A book whose `book.json` names a `cover` picture starts closed on it, in the middle of the screen. The first tap swings the cover open round the spine like a stiff board, its inside landing as the left-hand page of the first spread, while the camera eases to the open book. Going back from the first page closes it again. A book without a cover picture starts open. `--page` and `--stop` skip the cover.

The page turn is never used where it would show something a page is hiding. A page with `"openWhole": false` (page 6 of Moss, which hides a raindrop) is arrived at through a short dip to dark instead, and so is any page of a different shape from the one before it. A stop with `"moveSeconds": 0` still cuts. With reduced motion there is no turn and no swinging cover, only the fade. The turn needs the open book: without it, `flip` behaves like `fly`.

The last frame of a page turn is the open book exactly: the leaf is drawn right up to the moment it lies flat, the page edges move across while the leaf is in the air, and the crease, pictures-on-top and weather of the page the leaf carries fade out as it lifts and back in as it lands. The cover's swing ends the same way. Once the leaf is less than a twentieth of a pixel from rest, the picture it carries is drawn lying flat in its place, exactly as the open book draws it, so not even the outline of the pages changes in the last frame. Frame-by-frame checks with `record` show the change between frames falling smoothly to zero at the end of every turn and of the cover, at the crease too.

How it is made: the sizes of the book are in `BookLayout` and the bending is in `Curl` (both in `Reader.Core/BookLayout.cs`, with tests). The turning page rolls over a cylinder that travels from the outer edge to the spine. `ReaderBook.cs` cuts the page into upright strips, puts each where its bit of paper is on the roll (with the fold leaning a little, the top corner leading), draws strips that are lifted higher a little bigger, as nearer things look, and shades each by how it faces the light. The strips are cut evenly and also closely through the roll (`Curl.Cuts`), so even when the roll has shrunk to a sliver at the spine no strip reaches from the flat part over to the part lying back; such a strip used to be drawn as a dark flat piece across the crease. The close cuts also keep the steps along the top and bottom edges of the bend to a pixel or so.

## Words printed on the page

Besides the pop-up box at the bottom, a page can print its words on the picture, as in a printed book, over a calm part of the picture (sky, grass, a wall) that the illustrator left clear. In `book.json` it is an optional `text` on the page; a page without one prints nothing, so older books are unchanged:

```
"text": { "x": 0.025, "y": 0.02, "w": 0.45, "h": 0.19, "size": 0.032, "align": "left", "color": "#2b2118", "content": "..." }
```

- `x`, `y`, `w`, `h` are the words box in fractions of the page picture, as for stops (of the whole spread for a double-page picture). The words wrap to the box's width and start at its top; `h` is the room left for them, which Check book compares with what they need.
- `size` is the letter size as a fraction of the picture's height (0.042 when left out), so the words keep their size and place on every screen. `align` is `left` (left out), `center` or `right`. `color` is `#rrggbb` (a very dark brown when left out); dark letters get a soft light glow and light letters a dark one, so they read on watercolour.
- `content` is optional. Without it the page prints its stops' words, in order, as one paragraph. A line break in it starts a new line. The pop-up always shows the stop's own words.
- Fields inside `text` that this reader doesn't know are kept when the book is saved. The browser reader in `web/` ignores `text`.

How it is drawn (`Reader.Avalonia/PrintedWordsPicture.cs`): the words are drawn once into a small see-through picture covering just the words box and its glow, at half as sharp again as the full-size page picture (no side over 2048 pixels), with half and quarter size copies. It is then drawn with the page picture exactly as the page picture is: on the flat page, in the open book, on every strip of a curling page, on the inside of the cover and in the slide between pages, so the words zoom, move and curl with the paper. The letters are laid out as if the picture were 1000 units tall and scaled, so the lines break in the same places at every size, in the editor too. A page's words picture is about 5 MB for a block of 40 words on a 2400 x 1500 page; only the page being read and those either side keep theirs (15 MB at most), and the next and previous pages' are made while the reader is at rest, so a page turn never waits for them. They sit on the page under the pictures on top and the weather.

The grown-ups' settings have two switches, both on to start with: **Words on the page** (`printedWords`) and **Pop-up words** (`popUpWords`). With pop-ups off, the box still comes up where the printed words can't be read: zoomed in away from them (or outside a dim or spotlight stop's light), on pages that print nothing, and for things found on explore pages.

## The look

The reader has one skin for the shelf, the settings, the reader's buttons, the pop-up words and the boxes: warm cherry wood and cream, with fresh leaf green for the headings and main actions, and a sunny yellow here and there. Nothing is pure black, and the colours are soft enough to sit beside watercolour pictures.

- **Colours** are all in `Reader.Avalonia/Palette.cs`. Change one there and everything that uses it follows. `Reader.App/AppSkin.cs` hands them on to the standard controls (the settings' tick boxes, lists and buttons, with the Fluent theme's accent set to leaf green); the Windows program and the phone app both call `AppSkin.Install` as they start. A button gets the filled green look with the class `primary`, or the warm red with `danger`.
- **The font** is Nunito, a rounded font under the SIL Open Font License (`Reader.Avalonia/Assets/Fonts/OFL.txt`, also copied beside the program as `Licences/Nunito-OFL.txt`). Five weights, Regular to Black, trimmed to Latin letters, are built into the program: about 320 KB. It is used everywhere, the pop-up words and the words printed on the page included, so those break in the same places on every computer and phone and in the editor (`WordsLayout.Face`).
- **The shelf** (`Reader.App/ShelfView.cs`, `Bookcase.cs`, `ShelfArt.cs`) is a cherry wood bookcase with a top cap, a plinth and a soft cream back of tongue-and-groove boards, and the books stand face out on its shelves. Green is only an accent there: the potted plant in spare room and the little leaf mark by the title. Everything is drawn in code: the wood grain and a fine paper speckle are small pictures made once from seeded noise, and the plant and the books lying down are little vector shapes (the leaves are in `Reader.Avalonia/Ornaments.cs`, which The End box uses too). Covers are as big as fits: a shelf of books with a low shelf under it fits the window, or the first shelf and a peek of the next when there are more books.
- **Movement.** A book lifts, tilts a touch and its shadow grows under the pointer (or a finger), and dips when pressed. When the shelf appears the books drop gently into place one after another. Each is a short transition that stops where it lands, so a shelf that is just sitting there draws nothing at all. The dots on "Adding the book…" bob only while that note shows. The grown-ups' **Movement** setting (`reduceMotion`), set to Reduced or following a device that asks for less motion, turns all of it off: a book under the pointer is still shown lifted, at once and without the tilt.
- **Buttons** have a soft tactile look (`Buttons` in `Palette.cs`): a face a little lighter at the top, a light top edge, a darker lip along the bottom and a soft shadow. Under the pointer the face brightens; pressed, it darkens a touch, sinks a pixel or two and its shadow tucks in. Ordinary buttons have plain dark words and icons; the main actions (the Next arrow, "Read again", the `primary` class) keep a leaf green face with white, Show me a yellow one, and `danger` a warm red.
- **The reading screen** keeps its layout and behaviour; only the colours changed. The Next arrow is leaf green, the other buttons cream with dark icons, the surround a deep forest green, the pop-up box cream.

## Test

```
dotnet test Reader.Tests
```

The tests load all four sample books, so they need the repo's `content/` folder. `Reader.Tests/Fixtures/` holds what the browser reader writes for each sample book, and one test checks that this reader writes the same. After changing `web/reader.js` or a sample book, make those files again with `node make-fixtures.js` in that folder.

## Take a screenshot

The Player can draw one screen to a PNG and exit, with no window and no display. Time is made up, so the same command always gives the same picture.

```
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot shelf.png
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot rain.png --book biggest-puddle --page 7 --stop 2 --wait 2000
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot garden.png --book biggest-puddle --page 12 --stop -2 --size 1600x900
```

- `--page` and `--stop` count from 1. `--stop -1` is the whole page and `--stop -2` is exploring. With no `--stop`, the page opens the way it would in the book.
- `--wait` is how much reader time passes before the picture is taken (1000 ms if left out). A pan needs its full time to finish.
- `--size WxH` sets the window size, `--scale 2` draws for a high-density screen, `--settings` opens the settings panel.
- On explore pages, `--look 0.3,0.5` centres the view on a point of the page, `--zoom 3` zooms in and `--find 1` taps the first thing to find.
- `--set key=value` changes a setting for that picture, for example `--set reduceMotion=on` or `--set prompts=true`.
- `--do "tap 900,400; wait 2000; key Right"` acts out touches and key presses through the same path a real mouse and keyboard use. Steps: `tap x,y`, `hold x,y ms`, `drag x,y x,y`, `wheel x,y notches`, `key Name`, `wait ms`, `settle`.
- `--fps` shows the frame-rate readout in the picture.
- `--do "hover 400,350"` rests the mouse on a point without pressing, so a book on the shelf lifts, and `press 400,350` presses and keeps pressing, for a button held down.
- The shelf's little animations land at once in screenshots. `--animate` lets them run, for checking them with `record`.
- `--do "... ; record 1500 <folder>"` saves every frame for that long, sixty a second, as numbered pictures. Comparing each frame with the one before shows whether motion is smooth: the change should shrink steadily to nothing as a move or a page turn lands, with no spike.

Screenshots always start from the default settings and never touch the saved ones. Run with `--help` for the full list.

With no `--page` or `--stop` the picture is of the book as it opens: closed on its cover. To catch a page part of the way through its turn, get to the whole page at the end of a page and stop the clock during the turn (it lasts 900 ms), or catch the cover half open:

```
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot turn.png --book biggest-puddle --page 2 --stop 2 --wait 5000 \
  --do "key Right; wait 3500; key Right; wait 400"
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot cover.png --book biggest-puddle --do "wait 400; key Right; wait 550"
```

## Smooth movement, and how to measure it

How frames are driven: while anything on screen is changing, `ReaderView` asks the window for its next frame (`TopLevel.RequestAnimationFrame`, the same idea as a browser's `requestAnimationFrame`), moves the engine on to that frame's time, redraws, and asks again. There is no timer between the display and the motion. The frame times are evened out by `FrameClock` in `Reader.Core`, because the moment a frame is asked for wobbles by a few milliseconds even when the display is perfectly regular. When nothing is changing the view stops asking for frames, and a check ten times a second looks for anything that should start moving by itself.

What keeps each frame cheap:

- Page pictures are kept at half size, quarter size and so on (made when the book opens), and the smallest one that is big enough is drawn with the quickest filter. Shrinking a full-size page with a finer filter on every frame was the main cost before: over a tenth of a second a frame without a graphics card, and on a graphics card it meant big textures being thrown away and rebuilt.
- The finer filter is used only when a picture is being enlarged and everything has come to rest. That is the one extra redraw when the camera settles.
- Full-size pictures load in the background and only the last few are kept. They are fetched for the page being read, the next one and, where there is room for three, the one before, before the camera needs them.
- The program allows itself 512 MB of graphics memory for pictures (`Program.cs`). The usual allowance is less than two big pages.
- Brushes, pens, the book frame, the soft oval of the spotlight and the book's shadow are made once and kept.
- Only pages that are on screen are drawn.

**The readout.** Press F in the reader (or start with `--fps`) to see, top left, how many frames a second are being drawn and the longest wait between two frames in the last second. On a 60 Hz screen that should read about 60 and 17 ms while the camera moves. While the readout is on, every frame is drawn, moving or not.

**The bench.** This plays Moss and the Biggest Puddle from page 1 to the end of page 3 with no window, tapping whenever the camera has come to rest, and times every frame drawn:

```
dotnet Player/bin/Release/net10.0/BookReader.dll --bench
dotnet Player/bin/Release/net10.0/BookReader.dll --bench --pages 1 --set bookLook=false --set pageTurn=fly
```

Reader time is made up (sixty frames a second), so two builds draw the same frames and their numbers can be compared. It prints the number of frames, the average and worst time, and how many took over 20 ms, for all frames and separately for the frames while a page was turning and while the cover was swinging open. It also times a blank window, because the bench paints without a graphics card and reads every frame back, which costs about 20 ms a frame here by itself. The line that starts "so the reader's own drawing" has that taken off.

## Publish for Windows

`./publish-win.sh /some/folder/outside/the/repo` builds the Windows program and zips it with the sample books into `BookReader-windows.zip`, ready to unzip and run. It works from Linux too. By hand, it is:

```
dotnet publish Player -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o /some/folder/outside/the/repo
```

The result is one file, `BookReader.exe` (about 105 MB), which needs nothing installed. The `.pdb` files beside it are only for debugging and can be left behind. Put a copy of the repo's `content` folder next to the exe and the shelf will find it.

**Book files.** The Player opens a book file (`.picturebook`, made by the editor's Export): drop it on the window, press "Add a book file" on the shelf, or give it on the command line, which is what Windows does with "Open with" (or `--import <file>`). The book is checked and unpacked into the user's own app-data folder (`%APPDATA%\BookReader\books`, beside the settings), goes on the end of the shelf and opens. Books added this way are listed in the settings under Books, where they can be removed. There is no installer, so nothing registers the `.picturebook` ending with Windows; "Open with", "Choose another app", "Always" does that once by hand.

Keep build output out of the repo. `bin/`, `obj/` and `publish/` are ignored by `.gitignore`.

## Android

`Phone/` is the reader for Android phones and tablets. It is the same reader as on Windows (`Reader.Core`, `Reader.Avalonia` and `Reader.App`), with the books from `content/` packed inside, so it works with no internet. It asks for no permissions at all (not even internet), collects nothing, and has no links out of the app and no editor. More books can be added from book files (below).

- **Android 8.0 (API 26) or newer**, built against Android 16 (API 36). API 26 reaches nearly every phone still in use, cheap ones included, and is the oldest Android that .NET and Avalonia still build for.
- **64-bit and 32-bit Arm** (`arm64-v8a` and `armeabi-v7a`) in one APK of about 44 MB, 16 MB of it books. The 32-bit part is for the cheapest phones, many of which run a 32-bit Android. The 64-bit native libraries are aligned for phones with 16 KB memory pages (32-bit Android always uses 4 KB pages). From the Play Store (the `.aab`), each phone only downloads its own part.

### Build and install

You need the .NET 10 SDK with the Android workload (`dotnet workload install android`), the Android SDK with platform 36 and build-tools 36, and JDK 21. `ANDROID_HOME` and `JAVA_HOME` say where they are.

```
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64 ANDROID_HOME=/root/android-sdk
./publish-android.sh /some/folder/outside/the/repo
```

That makes `BookReader.apk` and `BookReader.aab`. To install the APK, copy it to the phone and open it (Android asks once to allow installing from that app), or with a cable and USB debugging on: `adb install BookReader.apk`.

Without a keystore the APK is signed with the .NET debug key, which is fine for your own phones. To sign with your own key, make one once and keep it outside the repo (never commit a keystore):

```
keytool -genkeypair -keystore ~/keys/bookreader.keystore -alias bookreader -keyalg RSA -keysize 2048 -validity 10000
BOOKREADER_STORE_PASS=... ./publish-android.sh /some/folder ~/keys/bookreader.keystore bookreader
```

An update must be signed with the same key as the copy already on the phone, or Android refuses it.

### How it reads on a phone

- **Held upright, one page at a time.** A picture that fills a double-page spread is read a page (half the picture) at a time when it is seen whole. As a page opens, the page shown is the one holding the first stop; at the end of a picture, the one holding the last stop. A picture with nothing to stop at shows its left page, then its right page. Close-ups still move over the whole picture: as the camera goes in, the other page fades back in (the same way the crease fades), so a close-up can sit across the middle. Going from one page of a picture to its other page, or on to the next picture, is a single page turning. Portrait books show one portrait page. Explore pages are explored the same both ways. These rules are in `Reader.Core` (`OnePage`, `Step.Half`) with tests in `OnePageTests.cs`.
- **Held sideways, the open book**, as on Windows, but bigger: the screen is so short that the whole book is framed by its pictures rather than by its cover board (`PictureFillsHeight` in the engine). The pictures take nearly the whole height (about 98%) and the board's edge runs off the top and bottom. The buttons float, see-through, over the corners.
- **Turning the phone keeps the place.** The same step stays on, and the camera glides to its new framing.
- **Touch.** A tap anywhere goes on; a swipe left goes on and a swipe right goes back; a quick second tap is ignored. Pinch and drag explore, and a tap finds things. Touch targets are at least the size set in the settings, measured with the screen's real density. Dragging the page corner is not there: a tap or a swipe turns the page.
- **Full screen while reading.** With a book open the status bar and the navigation bar are hidden; a swipe in from the edge brings them back for a moment, over the picture. They are hidden again when the app comes back to the front. On the shelf they show as usual.
- **The phone's edges.** Buttons and words keep clear of the camera's cutout, the rounded corners (Android 12 and newer say how round they are) and any bars that show; the picture fills the screen behind the bars.
- **Back** closes the settings, or leaves the book for the shelf. Only from the shelf does it leave the app, so a child is never thrown out of the middle of a story.
- **Into the background, or the screen locks:** the place is kept, sound stops and the clock stops, so the story carries on from the same moment. The screen stays on while a book is open.
- **Settings** are only the grown-ups' ones, behind the gear, and a fresh install starts with the two-second lock on the gear.
- **Cheap phones.** No picture is held bigger than 4096 pixels (most phone graphics chips can't draw bigger in one go; page 12 of the puddle book is 4800 wide), pictures are read at that size rather than shrunk afterwards, and only as many full-size pictures are kept as the phone's memory class allows (2 to 4). When Android says memory is short, the full-size pictures are let go and load again when wanted.
- **Sound** uses the same library as Windows, which plays through Android's own sound system. If it can't start, the app stays silent.

The books are unpacked from the app to its own storage the first time it runs, and again after each update.

### Adding a book from a file

The editor's Export makes one file per book, ending in `.picturebook` (a zip holding the book's folder). To put it on a phone, send it any way at all (WhatsApp, email, Google Drive, a cable) and open it there: Book Reader offers itself, and the book goes on the end of the shelf. A grown-up can also add one in the settings (behind the gear and its lock), under Books, with "Add a book from a file…", which opens the phone's own file picker, and can take books added this way off the phone again there.

- **How the app hears about the file.** The screen has intent filters for opening (`VIEW`) and sharing (`SEND`) a file of type `application/zip`, `application/octet-stream` (what phones call a file with an ending they don't know, which is how a `.picturebook` file usually arrives) and a few zip spellings, plus the `.picturebook` ending where the sending app gives a path. That means the app is also offered for zip files and unknown files; anything that isn't a book is turned down with a plain message. Files are read through Android's content system, which the sending app grants for that one file, so the app still asks for no permissions.
- **Checking.** `BookPackage` in `Reader.Core` does all of it, so it is the same on Windows and is tested: no name in the zip may lead out of the book's folder (`..`, a drive, a leading slash, an empty part) and links are refused; sizes are counted while unpacking, not taken from what the zip claims (1.5 GB in all, 400 MB for one file, 20,000 files); `book.json` must read and have pages; the cover, page pictures and pictures on top must be there and really be JPEG, PNG or WebP. Every problem has a friendly sentence for the parent, shown on the shelf or under Books. The book is unpacked beside the others and only swapped in once it has passed, so a bad file never leaves half a book.
- **Where the books go.** The app's own storage, in `imported/` (laid out like the content folder: `library.json` and `books/<id>/`), apart from the packed-in books, which are unpacked again after every update. So added books survive updates. The same book sent again (the same id) replaces the old copy.

The Player can show what the phone app would, with no phone: `--phone` behaves as the phone app (touch, one page at a time when upright, the phone's picture limits), `--units-per-cm` gives a screen density and `--safe left,top,right,bottom` the room kept for a notch and bars. `--size` is in Android's screen units and `--scale` is the density. For a Galaxy S22 Ultra held upright:

```
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot s22.png --phone --size 411x882 --scale 3.5 --safe 0,40,0,24   --units-per-cm 56 --book biggest-puddle --page 2 --stop -1
```

The bench takes `--phone` too, and reports how much memory a book took.

## Sound

Sound uses [SoundFlow](https://github.com/LSXPrime/SoundFlow) 1.4.2, which is MIT licensed and carries the public-domain miniaudio library for Windows, Linux, macOS, Android and iOS. It was chosen because it plays through each system's own sound API with nothing extra to install, reads wav and mp3 files, and lets the program supply its own samples.

Only the device and the file reading depend on it. A stop's recorded voice plays on a channel of its own beside any sound effect, and the reader asks the sound output how far the voice has got (from what has actually reached the speakers), so the words light up in step with what is heard. The taps, chimes, whoosh, rain and thunder are worked out in `Synth.cs`, following the browser version's `Sounds` class note for note, so they can be tested with no sound device.

If there is no sound device, or the sound library cannot load, the reader stays silent and carries on. Nothing in `Reader.Audio` throws.

## The book editor

`Editor/` is `BookEditor.exe`, the program the owner and other illustrators and writers use to make books. It opens any book folder (or a folder of pictures with no `book.json` yet), and everything in the browser editor (`web/editor.js`) is there, plus narration, previews on phone screens, Check book and Export. The preview is the real reader: `Reader.Avalonia`'s `ReaderScreen`, as the Player and the phone app use it.

```
dotnet run --project Editor                         # the editor
dotnet run --project Editor -- /path/to/book        # with a book open
./publish-editor.sh /some/folder/outside/the/repo   # BookEditor-windows.zip, ready to unzip and run on Windows
```

The zip holds `BookEditor.exe` (one file with everything else packed in) and a `runtimes` folder beside it with the speech recogniser's native code for Windows (with and without AVX), about 47 MB in all. The native code is kept as loose files on purpose: packed inside the exe it can't be found. The speech recogniser's 148 MB file of speech knowledge is never in the zip; it is downloaded the first time it is used (below).

The window: the pages down the left (drag one onto another to reorder), the page in the middle, settings for whatever is selected on the right, and the camera's journey through the page as cards along the bottom (drag a card onto another to reorder). F1 lists the keys. Ctrl+Z and Ctrl+Y undo and redo every change, typing in a box counting as one step.

**Book folders and saving.** A book is its folder. A picture or sound added in the editor is copied into the folder at once (recordings into `voice/`, sounds into `sounds/`), so saving only writes `book.json`, and nothing can go missing between adding and saving. Saving writes to a temporary file first and swaps it in, keeping the version before as `book.json.bak`. While there are unsaved changes a safety copy of the book is written every 15 seconds to the editor's own folder (`%APPDATA%\BookEditor\recovery`, never into the book's folder); opening the book again after a crash offers it back. Recently opened books are listed in `%APPDATA%\BookEditor\recent.json`. Fields of `book.json` the editor doesn't know are kept and written back unchanged, so a book edited by hand or by a newer version loses nothing.

**Pictures: whole spreads, or a left and a right page.** A page picture can be one picture covering the whole double-page spread, or two pictures, a left-hand and a right-hand page. Two pages are joined by the editor into one spread picture (side by side, the right page scaled to the left page's height) as soon as they are added, and the page uses that. This is done in the editor rather than in the format so the readers stay as simple as they are: they only ever see one picture per page. The editor notes which two pictures the spread came from in an editor-only `"spread": { "left": ..., "right": ... }` field on the page, so either half can be replaced later and joined again with the stops left where they are. The readers ignore that field and Export leaves it out. Pictures named as a pair (`page3-left.png` and `page3-right.png`, or `_L` and `_R`) are joined when added together; "+ Add left and right pages" picks the two by hand, and "Join with next page" makes two existing pages into one spread, keeping the stops, things to find, pictures on top and effects of both. A picture can be replaced keeping its stops. Joined spreads carry a note inside the file (a JPEG comment) saying the editor made them. Replacing a half makes a new joined picture (so undo can still go back to the old one), and when the book is saved, joined pictures that nothing uses any more (not the book, not any step undo or redo can reach, and not `book.json.bak`) are deleted. Only files with the editor's note are ever deleted, so the left and right pages themselves and any other picture brought in are never touched. A picture smaller than 1000 pixels on its short side, or of a different shape from the other pages, is pointed out when added and in Check book.

**Story and explore pages.** Drag on the picture to draw a stop (Shift draws over another box), drag a box to move it and a corner to resize it, and the arrow keys nudge it. A stop can pan to a second box, set its move time, go on by itself, have its own look, words, a question for the grown-up, a sound effect and a recording. Explore pages have things to find (story steps and extras), hints, Show me and the corner map. Moving effects (rain with splashes and dry areas, snow, leaves, sparkles, lightning with thunder, camera shake) and pictures on top (appear, go away, move straight or in a hop, bob or sway) work on both. Over the picture the editor can show the middle of a spread with its left and right pages, and around the selected stop a frame for each chosen screen showing what it will see (worked out the way the reader frames a stop, with the room the buttons and words take), with the other page dimmed for an upright phone.

**Narration and read-along.** Each stop can have a recording of its words: add a wav, mp3 or ogg file (or drop one on the window with the stop selected), or record from the microphone (through SoundFlow; if there is no microphone the button is greyed out with the reason, and adding a file still works). A fresh recording has the quiet at each end trimmed off. The shape of the recording is drawn with orange trim handles. To time the words, press Tap along: the recording plays from the start trim, and Space marks the start of each word (Backspace takes one back, Esc stops). Click a word to hear it and nudge it with `,` and `.`, or drag its line on the shape. "Space words evenly" gives a rough first guess.

**Time words automatically.** Beside Tap along, this has the computer listen to the recording and work out when each word starts; "Time all stops…" (also in the Book section) does every recording in the book in one go, as one step that can be undone, and asks whether to redo the ones already timed. It runs on the computer with no internet, using free, open-source speech recognition (the MIT-licensed `Whisper.net` NuGet packages, which carry the native code for Windows, Linux and macOS, plus a build for processors without AVX). The recogniser needs a file of speech knowledge, about 148 MB, which is not in the zip: the first time, the editor asks, then downloads it into `%APPDATA%\BookEditor\models` with a progress card and a Stop button. A stopped download carries on from where it got to; a finished one is checked against its SHA-256 before it is used. It knows many languages; the Book section has "Language of the recordings" (work it out, English or Afrikaans). How it works: the trimmed part of the recording is cut out, resampled to 16 kHz and given to the recogniser along with the caption as a hint. Its word times are taken halfway between where it puts each word and where its attention lines the word up with the sound, then each start is moved to where the sound really begins if there is a pause before the word (`Onsets` in `Editor.Core/WordTiming.cs`). The heard words are then lined up with the caption's words the way a spell checker lines up letters (`WordAligner`): words are compared made plain (lower case, no accents or punctuation, "three", "drie" and "3" all the same), a swap costs less the more alike two words sound, one word heard as two (or two as one) can be matched, and words that were missed or misheard badly are placed between their neighbours in proportion to their length, always in order and inside the trim. Anything that goes wrong (no download, no internet, the native code won't load, nothing recognisable heard, the words changed meanwhile) ends in a plain message and changes nothing; Tap along and nudging are always there to fix a word or two. Tried on clips made with espeak-ng where each word's start is known exactly, the starts came out within 0.02 s on average in English and within 0.08 s in Afrikaans (the worst word 0.2 to 0.4 s off). `BookEditor --time-words recording.wav --words "the words" [--language af] [--models folder]` does the same from the command line and prints the times.

In `book.json` this is a `voice` on the stop, with times in seconds from the start of the file:

```
"voice": { "file": "voice/p1-s1.wav", "start": 0.2, "end": 2.1, "words": [0.35, 0.75, 1.15, 1.55] }
```

`start` and `end` are the trim (left out when not trimmed), and `words` has one start time per word of the caption, a word being whatever lies between spaces, so punctuation stays with its word. The reader code is `ReadAlong` (splitting into words, which word is being spoken) and the voice handling in `ReaderEngine`.

**Things to find can be read aloud too.** A hotspot can have the same `voice` field, for its "when found" words, set up in the editor with the same panel as a stop (record or add, trim, time automatically or tap along). In the reader, finding it plays the voice (with Read to me on), the words light up in the found message, and the message stays up until the voice has finished. A found thing's voice is no cue to go on, so the Next arrow doesn't glow after it. Its own sound effect, if it has one, still plays alongside.

In the reader, a parents' setting **Read to me** (`readToMe`, on to start with, in the settings panel under For parents) plays a stop's recording when the camera arrives there and lights up each word of the caption as it is spoken. A tap goes on straight away and stops the voice. Without watch mode the reader waits for a tap, and once the voice has finished the Next arrow glows twice to say it's the child's turn. In watch mode it goes on 1.5 seconds after the voice ends (or after the stop's own rest time). A stop that goes on by itself waits for the voice first. A book that opens straight onto a stop (a first page that doesn't open whole, the whole page first switched off, or a jump to a stop) plays that stop's voice and sound as soon as the reader is showing, as arriving there would. Books with no recordings behave exactly as before, and the words are plain text unless a voice is reading them. `readToMe` is not in `web/reader.js`, so the settings test lists it as a desktop-only setting (`ReaderSettings.DesktopOnlyKeys`) rather than changing the browser version.

**Preview.** Play (P) opens the book as it is now in the real reader, from the selected stop (playing the move into it) or the page's start, sized as a Windows window, a Galaxy S22 Ultra upright or sideways, a cheap 720 x 1600 phone upright or sideways, or a 10 inch tablet. Phones behave as the phone app does (one page at a time when upright, the phone's picture limits and safe areas). Open book, Read to me, watch mode and the Ask lines can be switched on and off. Nothing needs saving first.

**Words on the page.** In the Page section, "Print the words on the page" adds a words box (at the top of the page that doesn't hold the first stop) and selects it. Drag it over a calm part of the picture and drag a corner to change its width; the words are drawn on the page exactly as the reader prints them, as they are typed. Its panel has the words to print (empty prints the stops' words), Left, Middle or Right, the letter size, the colour, and "Fit the box to the words". The box turns red, with a line where the words really end, when they don't fit. Delete takes the words off the page.

**Check book** (F7) lists, most serious first: missing pictures, sounds and recordings, stops or things to find outside the picture, tiny stops, stops with no words, captions too long for a phone held sideways or upright, pictures over 4096 pixels (Export shrinks them, or cuts explore scenes into tiles) or too small, pages of a different shape, stops with no recording in a book that has recordings, word timings that no longer match the words, printed words that don't fit their box, run off the picture, cross the middle of a spread or are too small, story steps numbered with gaps, effects and pictures on top set to stops that don't exist, and files in the folder the book doesn't use (which can be moved into an `_unused` folder; nothing is deleted). Click a line to go to it.

**Export** (Ctrl+E) makes a copy of the book for the apps in a folder of its own, named after the book's folder name, and a book file to send to a phone if wanted (`<id>.picturebook`, a zip holding that folder; see "Adding a book from a file" under Android). Only the files the book uses are copied. Page pictures are made no bigger than 4096 pixels (or 2560 for a smaller download, or left as they are), pictures with no see-through parts become JPEG, covers are made smaller, and explore scenes over the limit are cut into tiles (an overview picture plus 1024-pixel tiles at full and half size, the layout the readers already load) so a phone only loads what the child is looking at. Scenes already cut into tiles are copied as they are. Missing sounds, recordings (on stops and on things to find) and pictures on top are left out rather than left pointing at nothing. Ticking "Also add it to a reader's books" copies the result into a content folder's `books/` and adds it to the end of its `library.json`.

**Getting a book into the apps.** Send the `.picturebook` file to the phone and open it there; BookReader.exe takes the same file (dropped on its window, "Add a book file", or "Open with"). BookReader.exe can also open any exported folder with "Open a book folder", or shows it on the shelf once it is in the `content` folder beside the program (Export can put it there). To pack a book inside the phone app itself, put the exported book in `content/books/<id>`, add its id to `content/library.json` (Export does both when pointed at the repo's `content` folder), then build and install the app again with `publish-android.sh`.

**The walkthrough.** The first time the editor runs, the welcome screen offers a short walkthrough ("Show me how" or "No thanks"); it is always there again from the ? button (F1), which also offers to carry on from where it was stopped. Ten steps make a first book: start a book, add the pictures (with the left and right pages tip), draw a stop, type the words, add a voice, time the words, try it on a phone in Play, Check book, Export, and send the book file to the phone. Each step is a card with a sentence or two and an orange ring round the real control to use; it moves on by itself about a second after the step is done (or with Next or "Skip this step"), and "Stop for now" pauses it. How far it got is kept in `%APPDATA%\BookEditor\walkthrough.json`. The code is `Editor/Walkthrough.cs`; the ring and card sit in a layer over the window that lets clicks through to the controls underneath.

**Show the book file.** In the ? sheet and in the Book section, this shows the book's `book.json` as text, exactly as Save writes it (with any unsaved changes), read-only, with "Copy all", for sending to someone helping sort out a problem.

**The screenshot hook.** Like the Player, the editor can draw one screen to a PNG with no display, never touching the real settings, recent books or safety copies:

```
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot main.png --book ../content/books/biggest-puddle --page 1 --stop 1
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot phone.png --book ../content/books/biggest-puddle --page 1 --stop 1 --screen preview --device s22-upright --wait 2500
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot check.png --book ../content/books/waterhole --screen check
```

`--screen` is main, welcome, preview, check, export, help, newbook or bookfile, and `--do "drag 800,250 950,400; key ctrl+Z; save"` acts out mouse and keys first. `--do words` selects the page's printed words box. `--tour N` shows the walkthrough at step N. `--do autotime` really times the selected stop's (or `--find N` thing's) words, waiting for it, with `--models <folder>` pointing at a downloaded speech recogniser (screenshots otherwise start as a computer that hasn't downloaded it); `--do "download 4000"` starts the real download and takes the picture four seconds in. `--help` lists everything.

```
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot tour.png --book ../content/books/biggest-puddle --page 1 --stop 1 --tour 6
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot timed.png --book /a/copy/of/a/book --page 1 --stop 1 --models ~/.config/BookEditor/models --do autotime
```

## Different from the browser version

- **The open book, the page turn and the cover** are only in this reader (Windows and Android). In the browser, `flip` flies across and books start open.
- **Reduced motion.** "Follow this device's setting" reads the Windows setting (Settings, Accessibility, Visual effects, Animation effects). Linux and macOS are not asked yet, so there the choice in the settings panel is the way to reduce motion.
- **No hand hints.** At the very start the Next arrow pulses instead of a tapping hand, and there is no dragging hand on explore pages (the idle hints do that job).
- **Making room for a caption** starts gently. The browser moves the picture a fixed share of the way every frame, which starts with a jolt; here it eases like a damped spring and arrives at about the same time.
- **Soft glows** around the idle hint and the touch ripple are built from a few faint rings instead of a blurred shadow. They look much the same and cost less to draw.
- **The editor.** The desktop editor is a separate program (`BookEditor.exe`, below). There is no way into it from the shelf, the Player or the phone app, and they don't contain it.
- **Narration and read-along** (a recorded voice per stop or per thing to find, with the words lighting up) are only in this reader. The browser prototype in `web/` is retired and isn't changed any more: it ignores the `voice` field, and the browser editor would drop it if it saved the book.
- **Book files** (`.picturebook`) are only for these apps; the browser version doesn't open them.
- **Still pages are not redrawn.** The browser redraws every frame. This reader stops when nothing on screen is changing.
- **Page pictures** are kept at several sizes, and the full-size ones load when they are wanted, as described above. Tiles of big scenes load only where the child is looking, as in the browser.
