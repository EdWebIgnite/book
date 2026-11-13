# Book Reader for the desktop

This folder holds the reader as a C# program for Windows, Linux and macOS, built with [Avalonia](https://avaloniaui.net/). It opens the same books as the browser version in `web/` and is meant to behave the same way. The book format (`book.json`) is unchanged: both readers use the files in `content/`.

The browser version is still the reference. When the two disagree, `web/reader.js` is right and this is a bug.

## What is in here

| Folder | What it is |
| --- | --- |
| `Reader.Core/` | The reader with no screen attached. Geometry, settings, the book format, and the engine that decides where the camera is, what is in focus, what has been found and what should be heard. It uses no drawing or window code at all, so it can be tested on its own and reused by the editor and the phone apps. |
| `Reader.Avalonia/` | The reader on screen. `ReaderView` draws what the engine says and passes on touches, the wheel and the keyboard. `ReaderScreen` adds the buttons, the caption, the hints and The End box. It also loads the pictures, including the tiles of big scenes. |
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

Keys: Right or Space goes on, Left goes back, W shows the whole page, + and - zoom on explore pages, S opens the settings, Esc goes back to the books.

Settings are kept between runs in `settings.json` in the user's own app-data folder: `%APPDATA%\BookReader` on Windows, `~/.config/BookReader` on Linux. The file uses the same names as the browser playground, and "Copy settings" puts the same text on the clipboard.

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

Screenshots always start from the default settings and never touch the saved ones. Run with `--help` for the full list.

## Publish for Windows

This works from Linux too:

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

- **Reduced motion.** "Follow this device's setting" reads the Windows setting (Settings, Accessibility, Visual effects, Animation effects). Linux and macOS are not asked yet, so there the choice in the settings panel is the way to reduce motion.
- **The hint hands** are drawn rather than an emoji, so they look the same whatever fonts a computer has.
- **Soft glows** around the idle hint and the touch ripple are built from a few faint rings instead of a blurred shadow. They look much the same and cost less to draw.
- **The editor.** There is no way into a book editor from the shelf, because there is no desktop editor yet.
- **Still pages are not redrawn.** The browser redraws every frame. This reader stops when nothing on screen is changing.
- **All of a book's page pictures are loaded when it opens,** as in the browser. A long picture book takes a few hundred megabytes of memory while it is open. Tiles of big scenes do load only where the child is looking.
