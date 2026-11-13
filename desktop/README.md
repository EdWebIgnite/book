# Book Reader for the desktop

This folder holds the reader as a C# program for Windows, Linux and macOS, built with [Avalonia](https://avaloniaui.net/). It opens the same books as the browser version in `web/` and is meant to behave the same way. The book format (`book.json`) is unchanged: both readers use the files in `content/`.

The browser version is still the reference. When the two disagree, `web/reader.js` is right and this is a bug.

## What is in here

| Folder | What it is |
| --- | --- |
| `Reader.Core/` | The reader with no screen attached. Geometry, settings, the book format, and the engine that decides where the camera is, what is in focus, what has been found and what should be heard. It uses no drawing or window code at all, so it can be tested on its own and reused by the editor and the phone apps. |
| `Reader.Avalonia/` | The reader on screen. `ReaderView` moves the engine on once a frame and passes on touches, the wheel and the keyboard; its drawing is in `ReaderPaint.cs`. `ReaderScreen` adds the buttons, the caption and The End box. `BookFiles.cs` loads the pictures, including the tiles of big scenes. |
| `Reader.Audio/` | Sound. The small interface sounds are made in code (`Synth.cs`); the SoundFlow library opens the sound device and reads sound files. |
| `Player/` | The program itself: the bookshelf, the reader screen and the settings panel. |
| `Reader.Tests/` | Tests for `Reader.Core` and for the synthesised sounds. |

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

## The book look and the page flip

Two settings, both on to start with, make the reader look and turn like a real book. They are in the settings panel under Fine tuning, Between pages.

- **Show pages as a book** (`bookLook`). Guided pages are shown one at a time as the top page of a book lying open: a cover board behind the page (its colour comes from the book's cover picture), a spine on the left, the edges of the pages underneath on the right and bottom, and a soft shadow. Pages being explored and tiled big scenes are drawn as before, with nothing around them. With the setting off, pages lie side by side as they do in the browser.
- **Page change: Flip like a book page** (`pageTurn` = `flip`). Going from one whole page to the next turns the page over from its right edge to the spine, in 900 ms at the standard speed. Going back plays it the other way. From a close-up, the camera first eases out to the whole page.

The flip is never used where it would show something a page is hiding. A page with `"openWhole": false` (page 6 of Moss, which hides a raindrop) is arrived at through a short dip to dark instead, and so is any page of a different shape from the one before it. A stop with `"moveSeconds": 0` still cuts. With reduced motion there is no flip, only the fade. The flip needs the book look: without it, `flip` behaves like `fly`.

On a machine with no screen, `xvfb-run -a dotnet Player/bin/Debug/net10.0/BookReader.dll` runs it on a pretend display.

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

Screenshots always start from the default settings and never touch the saved ones. Run with `--help` for the full list.

To catch a page half way through its flip, get to the whole page at the end of page 1 and stop the clock part of the way through the turn:

```
dotnet Player/bin/Debug/net10.0/BookReader.dll --shot flip.png --book biggest-puddle --page 1 --stop 2 --wait 4000 \
  --do "tap 900,400; wait 3500; key Right; wait 432"
```

## Smooth movement, and how to measure it

How frames are driven: while anything on screen is changing, `ReaderView` asks the window for its next frame (`TopLevel.RequestAnimationFrame`, the same idea as a browser's `requestAnimationFrame`), moves the engine on to that frame's time, redraws, and asks again. There is no timer between the display and the motion. The frame times are evened out by `FrameClock` in `Reader.Core`, because the moment a frame is asked for wobbles by a few milliseconds even when the display is perfectly regular. When nothing is changing the view stops asking for frames, and a check ten times a second looks for anything that should start moving by itself.

What keeps each frame cheap:

- Page pictures are kept at half size, quarter size and so on (made when the book opens), and the smallest one that is big enough is drawn with the quickest filter. Shrinking a full-size page with a finer filter on every frame was the main cost before: over a tenth of a second a frame without a graphics card, and on a graphics card it meant big textures being thrown away and rebuilt.
- The finer filter is used only when a picture is being enlarged and everything has come to rest. That is the one extra redraw when the camera settles.
- Full-size pictures load in the background and only the last few are kept. They are fetched for the page being read and the next one before the camera needs them.
- The program allows itself 512 MB of graphics memory for pictures (`Program.cs`). The usual allowance is less than two big pages.
- Brushes, pens, the book frame, the soft oval of the spotlight and the book's shadow are made once and kept.
- Only pages that are on screen are drawn.

**The readout.** Press F in the reader (or start with `--fps`) to see, top left, how many frames a second are being drawn and the longest wait between two frames in the last second. On a 60 Hz screen that should read about 60 and 17 ms while the camera moves. While the readout is on, every frame is drawn, moving or not.

**The bench.** This plays Moss and the Biggest Puddle from page 1 to the end of page 3 with no window, tapping whenever the camera has come to rest, and times every frame drawn:

```
dotnet Player/bin/Release/net10.0/BookReader.dll --bench
dotnet Player/bin/Release/net10.0/BookReader.dll --bench --pages 1 --set bookLook=false --set pageTurn=fly
```

Reader time is made up (sixty frames a second), so two builds draw the same frames and their numbers can be compared. It prints the number of frames, the average and worst time, and how many took over 20 ms. It also times a blank window, because the bench paints without a graphics card and reads every frame back, which costs about 20 ms a frame here by itself. The line that starts "so the reader's own drawing" has that taken off.

## Publish for Windows

`./publish-win.sh /some/folder/outside/the/repo` builds the Windows program and zips it with the sample books into `BookReader-windows.zip`, ready to unzip and run. It works from Linux too. By hand, it is:

```
dotnet publish Player -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o /some/folder/outside/the/repo
```

The result is one file, `BookReader.exe` (about 105 MB), which needs nothing installed. The `.pdb` files beside it are only for debugging and can be left behind. Put a copy of the repo's `content` folder next to the exe and the shelf will find it.

Keep build output out of the repo. `bin/`, `obj/` and `publish/` are ignored by `.gitignore`.

## Sound

Sound uses [SoundFlow](https://github.com/LSXPrime/SoundFlow) 1.4.2, which is MIT licensed and carries the public-domain miniaudio library for Windows, Linux, macOS, Android and iOS. It was chosen because it plays through each system's own sound API with nothing extra to install, reads wav and mp3 files, and lets the program supply its own samples.

Only the device and the file reading depend on it. The taps, chimes, whoosh, rain and thunder are worked out in `Synth.cs`, following the browser version's `Sounds` class note for note, so they can be tested with no sound device.

If there is no sound device, or the sound library cannot load, the reader stays silent and carries on. Nothing in `Reader.Audio` throws.

## Different from the browser version

- **The book look and the flip** are only on the desktop. In the browser, `flip` flies across.
- **Reduced motion.** "Follow this device's setting" reads the Windows setting (Settings, Accessibility, Visual effects, Animation effects). Linux and macOS are not asked yet, so there the choice in the settings panel is the way to reduce motion.
- **No hand hints.** At the very start the Next arrow pulses instead of a tapping hand, and there is no dragging hand on explore pages (the idle hints do that job).
- **Making room for a caption** starts gently. The browser moves the picture a fixed share of the way every frame, which starts with a jolt; here it eases like a damped spring and arrives at about the same time.
- **Soft glows** around the idle hint and the touch ripple are built from a few faint rings instead of a blurred shadow. They look much the same and cost less to draw.
- **The editor.** There is no way into a book editor from the shelf, because there is no desktop editor yet.
- **Still pages are not redrawn.** The browser redraws every frame. This reader stops when nothing on screen is changing.
- **Page pictures** are kept at several sizes, and the full-size ones load when they are wanted, as described above. Tiles of big scenes load only where the child is looking, as in the browser.
