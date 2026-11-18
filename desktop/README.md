# Dassie: the reader for the desktop and Android

This folder holds the reader, **Dassie**, as a C# program for Windows, Linux and macOS, and as an app for Android phones and tablets, built with [Avalonia](https://avaloniaui.net/).

The name people see is one constant, `Brand.Name` in `Reader.Core/Brand.cs` (the window title, the welcome, the wordmark, the About page and the Android label all read it; `Phone/Phone.csproj`'s `ApplicationTitle` repeats it for the build tools). The program files (`BookReader.exe`), the folders, the settings folder (`BookReader`) and the Android application id (`app.bookreader`) keep their old names on purpose: changing them would stop installed copies from updating and would lose testers' settings and books. The name has had no trademark check yet. It opens the same books as the browser version in `web/` and is meant to behave the same way. The book format (`book.json`) is unchanged: both readers use the files in `content/`.

The browser version is still the reference. When the two disagree, `web/reader.js` is right and this is a bug.

## What is in here

| Folder | What it is |
| --- | --- |
| `Reader.Core/` | The reader with no screen attached. Geometry, settings, the book format, and the engine that decides where the camera is, what is in focus, what has been found and what should be heard. It uses no drawing or window code at all, so it can be tested on its own and reused by the editor and the phone apps. |
| `Reader.Avalonia/` | The reader on screen. `ReaderView` moves the engine on once a frame and passes on touches, the wheel and the keyboard; its drawing is in `ReaderPaint.cs`. `ReaderScreen` adds the buttons, the caption and The End box. `BookFiles.cs` loads the pictures, including the tiles of big scenes. |
| `Reader.Audio/` | Sound. The small interface sounds are made in code (`Synth.cs`); the SoundFlow library opens the sound device and reads sound files. |
| `Reader.App/` | The whole app as one view (`AppView.cs`): the first-run welcome, "Who is reading?", the bookshelf, the reader screen, the grown-ups' gate and the grown-ups' area, and how they hand over to each other. Shared by the Windows program and the phone app. Its small set of controls (buttons, switches, choices, rows, the children's pictures) is in `Kit.cs`. |
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

The Player looks for the books in this order: the folder given with `--content`, a `content` folder beside the program, then the nearest `content/library.json` above the program or the current folder. Run from the repo, it finds the sample books by itself. "Open a book folder" under Books in the grown-ups' area (or Ctrl+O, which asks the gate first) opens any folder with a `book.json` in it.

```
dotnet run --project Player -- --content /path/to/content
dotnet run --project Player -- --book biggest-puddle --page 7 --stop 2
dotnet run --project Player -- --help
```

Keys: Right or Space goes on, Left goes back, W shows the whole page, + and - zoom on explore pages, S opens the grown-ups' gate (the number keys answer it), F shows the frame rate, Esc goes back (a page of the grown-ups' area, the gate, or from the book to the shelf). On the shelf and in the grown-ups' area Tab moves between things, with a clear ring round the one that has the focus, and Enter or Space presses it. The window can be made as small as 640 by 400.

`--set key=value` uses a setting for one run, for example `--set autoPlay=true --set autoSeconds=1` to watch a book play by itself. `--fps` starts with the frame-rate readout showing.

Settings are kept between runs in `settings.json` in the user's own app-data folder: `%APPDATA%\BookReader` on Windows, `~/.config/BookReader` on Linux, the app's own storage on Android. The file uses the same names as the browser playground, and "Copy settings" (under Fine tuning for testers) puts the same text on the clipboard. The children, their progress, the app's language and bedtime colours are kept beside it in `profiles.json` (see "Children, progress and where they are kept" below). Everything stays on the device.

The file carries a `settingsVersion`. A file saved before version 2 (by the first desktop build) is brought up to date once when it is loaded: the whole page is shown again after its stops, the book look is switched on and the page change becomes the flip. Everything else in it is kept, and after that the file is left as the user sets it.

## The app around the reader

The design work behind this is in `docs/design/` (`research.md`, with its numbered rules, and `proposal.md`). The look is that proposal's direction A, "Bookcase, evolved".

### First run

The first time the app starts, a short welcome for the parent: three pages, each with Skip (what it is; how a child uses it; the grown-ups' area and privacy: no internet, no adverts, no sign-up, nothing collected), with the language at the top of every page, then "A new reader": a name or nickname (it can be left empty, 12 letters at most), one of eight drawn pictures and an age band. "Start reading" goes to the shelf. A tester's install from before there were children (it has `settings.json` but no `profiles.json`) gets the welcome once too, and so ends up with one child; their settings are kept. The welcome can be shown again from About in the grown-ups' area (`WelcomeView.cs`).

### Children

Up to five children, each a nickname and a picture: a white sun, leaf, star, drop, flower, moon, kite or heart on a colour (told apart by shape as well as colour), never a photo, a birthday or an account. Each has their own age band, which sets how fast the pictures move, how soon hints come and how big touch areas are (the settings' `age` presets). With more than one child, "Who is reading?" comes first (`ProfilesView.cs`); the child's picture at the top of the shelf goes back to it. Adding, changing and removing a child is in the grown-ups' area, behind the gate; "Add a child" on "Who is reading?" goes through the gate too.

### The shelf

`ShelfView.cs` and `Bookcase.cs`. Along the top: the wordmark, who is reading (picture and name), and a smaller, quieter "Grown-ups" button with a lock. Below, the cherry bookcase with the child's name on the brass plate ("Lia's books", "Lia se boeke"; "My books" with no name) and a pale painted back, so the covers are the most colourful thing on screen. A book the child is part of the way through has a ribbon bookmark hanging from its top edge; a book they have read to the end has a round tick over its lower corner; a finished book started again shows both. The book they were last part of the way through is offered on a framed "Carry on reading" card with a picture of the page they were on, the title and "Page 7 of 12": on a wide screen it stands on the top shelf two books wide, on a phone or tablet held upright it is a strip above the case. A cover or the card opens the book at the child's place. The books keep the library's order (children find a book by where it stands), not an order by progress.

On a computer or a tablet held sideways the case has a low shelf with a pile of clothbound books and the dassie sitting at the right; on phones and upright screens there is no low shelf, and the dassie lies on top of the cornice. Covers are as big as fits: one shelf of books and the card when they fit at a good size, otherwise rows with a peek of the next. When the shelf holds books in more than one language, each cover carries a small EN or AF tag.

With no books at all, the shelf has a sign with the dassie, "No books yet", and "Add a book" with a lock, which goes through the gate to the grown-ups' Books page. Opening a big book says "Opening the book..." after a short wait; a book that won't open says so on the shelf; adding a book file says what happened.

### Reading

The reading screen is as it was, with three changes: the top left button shows books (back to my books), the top right is a small lock that leads to the gate (it replaced the gear and its two-second hold), and The End is new (below).

**Tap a word to hear it.** In the pop-up words, when the stop's recording has a time for every word, a tap on a word plays just that word's part of the recording (from its start to the next word's start, at most 1.6 seconds) and lights it up while it plays. A voice reading the whole caption stops first. Without timings the words can't be tapped and a tap there goes to the picture as before. It works whether Read to me is on or off: it is the child asking. The engine side is `ReaderEngine.SayWord` and `ReadAlong.WordSlice`, with tests.

**The End** (`ReaderScreen.BuildEnd`): the book's cover with its new tick, "The End", "You read the whole book, Lia!", "Read it again" as the main button, and "My books" and "Read next" (with the next book's small cover) below it. "Read next" offers the next book along the shelf that this child hasn't finished; it is left out when there is none. Nothing plays on by itself. The dassie looks over the top of the card. Reaching The End marks the book finished for this child.

### The grown-ups' gate and the grown-ups' area

The old gate was a two-second hold on the gear. Small children hold things down by accident, and it could be switched off, so it is replaced (research rule 13) by `GateView.cs`: three numbers written as words in the app's language ("seven, two, nine", "sewe, twee, nege") to tap on a number pad (the keyboard's number keys work too). New numbers every time, new numbers after a wrong try, and after three wrong tries the pad rests for 30 seconds. It cannot be switched off. It is asked for the grown-ups' area, adding a child and adding a book, and not again for anything done inside the area. The logic is `GateChallenge` in `Reader.Core/Gate.cs`, with tests. The old `gate` setting is still read and written so settings files stay the same, but nothing uses it.

The grown-ups' area (`ParentsView.cs`) is a full screen on a phone and a sheet down the right on a tablet or a computer. Its first page has the children (each with facts, never a score: "2 books finished · read on 3 days this week") and "Add a child", then Reading, Books, Language, Bedtime colours (a switch) and About and privacy. Each child's page has their name, picture and age band, the books they have read with dates and places, and Remove (asked once more). Reading has Read to me, Words on the page, Pop-up words and their size, Questions for the grown-up, Things to find, Show as an open book (the flat look switch), Watch mode, Movement and Sounds. Books has "Add a book file", "Open a book folder" on a computer, and every book on the shelf with its size and language; books added from files can be removed (asked once more). About and privacy says plainly that the app uses no internet, asks for no permissions, collects nothing, has no accounts, tracking, adverts or anything to buy, and keeps children's names, pictures and progress only in its own storage. The playground's fine tuning is under "Fine tuning for testers" at the bottom of About: always on a computer, and on a phone after seven taps on the version line.

### Bedtime colours

A switch in the grown-ups' area gives every screen the night palette: deep night blue with lamp-gold accents and cream words, the bookcase's wood in lamplight, a few still stars on the wall, dark glass buttons and a gold Next arrow while reading, and a dimmer cream for the pop-up words. Only the colours change, never the layout. It is described as "darker and quieter", with no health claim. The two palettes are `Theme.Day` and `Theme.Night` in `Reader.Avalonia/Palette.cs`; switching rebuilds the screens around the reader and restyles the reader screen in place, so a book being read carries on.

### Languages

All the words the app shows are in one table, `Reader.Core/Strings.cs`: a key, then the words in each language. The app speaks English and Afrikaans; it follows the device's language (Windows' own setting, `LANG` on Linux, the phone's language on Android) and falls back to English. The grown-ups' area can choose one, and the welcome has the choice at the top. Changing it rebuilds every screen at once. Books bring their own words; the reader's buttons and The End follow the app's language.

To add a language: add its code and its own name to `Strings.Languages`, then one more column to every row of the table (the tests check that every row has every language and the same `{0}` blanks), and the numbers 0 to 9 for the gate. `Strings.Resolve` then picks it for devices in that language.

A book can say its language in `book.json` with an optional `"language": "af"` (a short code) after `author`. The shelf shows it as a small tag on covers only when it holds books in more than one language, and Books lists it. Books without it are unchanged; the browser reader ignores it; the editor keeps it (Book section, "Language of the book") and Export writes it. The four sample books say `"en"`.

### Children, progress and where they are kept

`profiles.json`, beside `settings.json` (`%APPDATA%\BookReader\profiles.json` on Windows, `~/.config/BookReader/profiles.json` on Linux, the app's own files on Android, which Android removes with the app and doesn't back up). It holds whether the welcome has been seen, the language choice (`auto` or a code), bedtime colours, who is reading, and for each child: id, name, picture, age band, the days they read (the last few months, for "read on 3 days this week") and, for each book id, the page and stop they were on (when part of the way through), finished, times finished, the page count and when last read. The place is noted at every step and written when the page changes, when the book is closed, at The End and when the app goes to the background.

Writing is safe against a crash or a flat battery: the new file is written and flushed beside the old one, the old one (if it still reads) is copied to `profiles.json.bak`, and the new one is moved into place. Reading puts up with a damaged or hand-edited file: anything missing or of the wrong kind is left at its starting value, and a file that doesn't read at all falls back to the `.bak`, then to a fresh start. The code is `AppState` and `AppStateStore` in `Reader.Core/Profiles.cs`, with tests.

### Offline and private

Nothing in the app uses the network. The Android app asks for no permissions at all (the built `AndroidManifest.xml` has no `uses-permission`), and book files arrive through Android's own content system for that one file. Nothing is collected or sent anywhere; there are no accounts, analytics, adverts or links out. Not in the app on purpose: stars, badges, streaks, notifications and playing on into the next book (research section 3).

## The open book, the page turn and the cover

Two settings, both on to start with, make the reader look and turn like a real picture book. "Show as an open book" is under Reading in the grown-ups' area; the page change is under Fine tuning for testers, Between pages.

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

The reader has one skin for every screen. It is the cherry bookcase of the first version, tidied (direction A of `docs/design/proposal.md`): a calm cream wall with faint broad stripes, a pale painted back to the case so the covers carry the colour, bottle green for the one main action of each screen, deep green headings, warm cream cards and polished brass for small accents. Nothing is pure black. Words children read are dark ink on cream (about 14:1); white on the bottle green is about 6:1; brass is decoration only, never the only edge of something to press. State is shown by shape as well as colour (the ribbon, the tick, the switch's tick, the chosen choice's ring and underline).

- **Colours** are all in `Reader.Avalonia/Palette.cs`, in two sets, `Theme.Day` and `Theme.Night` (bedtime colours). Some names are from the first, leafier version: `Leaf` is the main-action colour (bottle green by day, lamp gold at night), `Sun` the brass. `Reader.App/AppSkin.cs` hands them on to the standard controls; the Windows program and the phone app both call `AppSkin.Install` as they start, and `AppSkin.Apply` runs again when bedtime colours are switched.
- **The name and the mascot** (`Reader.Avalonia/BrandArt.cs`, all drawn in code). The wordmark is the name in lowercase, round and heavy, with a small dassie lying along the top of the double s and the i's dot in cherry, after `docs/design/mockups/wordmarks.png`. The mascot is a small dassie (a rock hyrax): round, brown with a pale belly, small ears low on a wide head. It appears only on the welcome, in a corner of the shelf and at The End, in three poses (sitting, lying, looking over an edge). It never speaks and never asks for anything; when it first appears it may blink once, then it is still (no blink with reduced movement).
- **Touch sizes.** Children's buttons are 56 or more across, the main action of each screen 64 to 74 and covers far bigger; grown-up controls are 48 and quieter. Nothing a child presses is in the bottom 16 of a phone or the bottom 48 of a tablet.
- **Fonts.** Nunito, a rounded font, is used for everything children read, the pop-up words and the words printed on the page included, so those break in the same places on every computer and phone and in the editor (`WordsLayout.Face`). Fraunces Bold, a soft old-style serif, is used only for the big headings ("My Books", "Settings", "The End"). Both are under the SIL Open Font License (`Reader.Avalonia/Assets/Fonts/OFL.txt` and `OFL-Fraunces.txt`, also copied beside the program under `Licences/`) and built into the program, trimmed to Latin letters: about 320 KB for five weights of Nunito and 31 KB for Fraunces.
- **The shelf** (`Reader.App/ShelfView.cs`, `Bookcase.cs`, `ShelfArt.cs`) is a fitted bookcase in cherry: a moulded cornice with the brass name plate, plain sides, a pale painted back, a plinth on turned feet, and the books standing face out on its shelves (see "The shelf" above). Everything is drawn in code: the wood grain and a fine paper speckle are small pictures made once from seeded noise, and the rest are little vector shapes. The wall and the case are drawn only when the window changes size.
- **Buttons** have a soft tactile look (`Buttons` in `Palette.cs`): a face a little lighter at the top, a light top edge, a darker lip along the bottom and a soft shadow. Under the pointer the face brightens; pressed, it darkens a touch, sinks a pixel or two and its shadow tucks in. Ordinary buttons are warm cream with plain dark words and icons; the main actions (the Next arrow, "Read it again", "Carry on", the `primary` class) have a bottle green face with white words (lamp gold with dark words at bedtime), Show me a brass one, and `danger` an oxblood one. The screens around the reader use the same faces through `Kit.cs` (`Pill`, `RoundTap`, `Switch`, `Choices`, `Row`).
- **Movement.** A book lifts, tilts a touch and its shadow grows under the pointer (or a finger), and dips when pressed. When the shelf appears the books drop gently into place one after another (300 ms each). Each is a short transition that stops where it lands, so a shelf that is just sitting there draws nothing at all. The dots on "Adding the book…" bob only while that note shows. The grown-ups' **Movement** setting (`reduceMotion`), set to Reduced or following a device that asks for less motion, turns all of it off: a book under the pointer is still shown lifted, at once and without the tilt.
- **The reading screen** keeps its layout and behaviour. The surround is a deep bottle green (night blue at bedtime), the pop-up box cream, the "Ask" box green with a brass badge, and the word being read (or tapped) sits on a filled brass box.

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
- `--size WxH` sets the window size, `--scale 2` draws for a high-density screen, `--settings` opens the grown-ups' area (without the gate).
- On explore pages, `--look 0.3,0.5` centres the view on a point of the page, `--zoom 3` zooms in and `--find 1` taps the first thing to find.
- `--set key=value` changes a setting for that picture, for example `--set reduceMotion=on` or `--set prompts=true`.
- Who is reading: one child with no name to start with; `--welcome` is a first run (the welcome pages); `--demo` has three children, Lia, Thabo and Mila, with Lia part of the way through Moss and two books finished; `--child 2` makes the second one the reader; `--state file.json` starts from a saved `profiles.json` (never written to).
- `--lang af` is a device in that language; `--bedtime` switches bedtime colours on.
- `--screen name` opens a screen: `shelf` (closes "Who is reading?"), `welcome` or `welcome:3`, `profiles`, `gate`, `parents`, `parents/reading`, `parents/books`, `parents/language`, `parents/about`, `parents/child`, `parents/new`, `parents/tuning`. `--focus` shows the keyboard's focus ring on the first thing on the shelf.
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

Reader time is made up (sixty frames a second), so two builds draw the same frames and their numbers can be compared. It prints the number of frames, the average, median, 95th and 99th percentile and worst time, and how many took over 8.3 ms (a 120 Hz screen), 16.7 ms and 20 ms, for all frames and separately for the frames while a page was turning and while the cover was swinging open, with the part of each frame spent before painting on its own. It also prints what the screen's thread allocated each frame and how many collections there were, and times a blank window, because the bench paints without a graphics card, which costs about 20 ms a frame here by itself. `BENCH_SLOW=1` lists the frames that took over 8 ms before painting. The line that starts "so the reader's own drawing" has that taken off.

## Performance

Everything below runs with no window and paints without a graphics card, so painting times are only for comparing two builds. The time spent on the screen's own thread before painting, the allocations, the collections and the memory are what a phone sees too. Build with `-c Release` first.

```
dotnet Player/bin/Release/net10.0/BookReader.dll --bench                         # reading: frame times (median, 95th, 99th, worst, over 8.3 and 16.7 ms), allocations, collections
dotnet Player/bin/Release/net10.0/BookReader.dll --bench --pages 2 --phone --size 411x882 --scale 2.625
dotnet Player/bin/Release/net10.0/BookReader.dll --perf open                     # each book: tap to first frame
dotnet Player/bin/Release/net10.0/BookReader.dll --perf explore                  # panning and zooming the big scenes
dotnet Player/bin/Release/net10.0/BookReader.dll --perf shelf                    # books settling in, covers lifting
dotnet Player/bin/Release/net10.0/BookReader.dll --perf memory --book waterhole  # read to the end: peak memory, collections, leaks after closing
dotnet Player/bin/Release/net10.0/BookReader.dll --perf idle                     # a page at rest: redraws (should be 0), the idle check
dotnet Player/bin/Release/net10.0/BookReader.dll --perf readalong --book <folder> --page 2 --stop 1   # a timed recording read aloud
dotnet Player/bin/Release/net10.0/BookReader.dll --perf audio --book <sound file>
dotnet Player/bin/Release/net10.0/BookReader.dll --perf covers                   # a check: covers made while loading match those made on the screen's thread
```

**Start-up, in the real program.** With `BOOKREADER_PERF=1` the Player writes milestones to the console with the time since it started (`main`, `skin installed`, `window made`, `shelf listed`, `first frame`, `covers shown`, and `opening …`, `book read`, `book's first frame` for each book opened). On Linux it can run under `xvfb-run`. A Debug build of the phone app writes the same to `adb logcat` (search for `perf:`).

**At rest.** Nothing should be drawn while nothing moves. To check the real program, start it on a screen (`--screen parents`, `--screen welcome`, `--book <id> --finish` for The End, `--book <id> --page 2 --stop 1`) and watch its CPU for ten seconds: it should stay near zero.

**The editor.** Its screenshot hook times steps: `BookEditor --shot x.png --book <folder> --page 2 --stop 1 --do "live; timed key PageDown; timed drag 700,300 760,340; caption; typeeach Some more words; timed key ctrl+Z"` prints how long the book took to open and how long each step took, laid out and drawn (`live` loads pictures in the background from then on, as the real editor does; `caption` puts the keyboard in the stop's words; `typeeach` types one key at a time). In the editor the printed words on the page are made on another thread, and the stop cards under the page are built again once typing pauses rather than for every key.

What keeps things quick, besides what is listed under Smooth movement:

- A book's pictures are read side by side, a few at a time, and the cover, the board's colour, the first page's printed words and both faces of the cover are made alongside, off the screen's thread. The screen's thread only puts the finished book on screen.
- The printed words of the pages either side are made on another thread while nothing moves (they took a tenth of a second, at the end of every page turn, on the screen's thread). A page turned to before they are ready waits for them rather than making them twice.
- While a voice reads, only the caption's words change: the picture is drawn once as reading starts and once when it ends, not sixty times a second.
- Sound files are kept once read (about a minute of sound in all), so a word tapped after its stop was read aloud plays at once.
- Paths of tiles and pictures on top, the words key, the buffers of a turning page and the backdrop's brush are worked out once, not every frame.
- The reader screen doesn't fill the screen under the picture with the same colour the app already has there: everything under a moving picture is painted again every frame.
- The Windows program is published ReadyToRun (compiled ahead of time), which brings its first frame forward by about 40%.
- The phone app uses .NET's profiled AOT (the default). Full AOT (`-p:AndroidEnableProfiledAot=false`) makes the APK about 13 MB bigger; whether its quicker start is worth that is best judged on a cheap phone.

## Publish for Windows

`./publish-win.sh /some/folder/outside/the/repo` builds the Windows program and zips it with the sample books into `BookReader-windows.zip`, ready to unzip and run. It works from Linux too. By hand, it is:

```
dotnet publish Player -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o /some/folder/outside/the/repo
```

The result is one file, `BookReader.exe` (about 70 MB, compiled ahead of time so it starts sooner), which needs nothing installed. The `.pdb` files beside it are only for debugging and can be left behind. Put a copy of the repo's `content` folder next to the exe and the shelf will find it.

**Book files.** The Player opens a book file (`.picturebook`, made by the editor's Export): drop it on the window, press "Add a book file" under Books in the grown-ups' area, or give it on the command line, which is what Windows does with "Open with" (or `--import <file>`). The book is checked and unpacked into the user's own app-data folder (`%APPDATA%\BookReader\books`, beside the settings), goes on the end of the shelf and opens. Books added this way are listed in the grown-ups' area under Books, where they can be removed. There is no installer, so nothing registers the `.picturebook` ending with Windows; "Open with", "Choose another app", "Always" does that once by hand.

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
- **Back** closes the gate, goes back a page of the grown-ups' area (or closes it), goes back a page of the welcome, closes "Who is reading?" when it was opened from the shelf, or leaves the book for the shelf. Only from the shelf does it leave the app, so a child is never thrown out of the middle of a story.
- **Into the background, or the screen locks:** the place is kept, sound stops and the clock stops, so the story carries on from the same moment. The screen stays on while a book is open.
- **Settings** are only the grown-ups' ones, in the grown-ups' area behind the gate (the lock on the shelf and in the reader).
- **Cheap phones.** No picture is held bigger than 4096 pixels (most phone graphics chips can't draw bigger in one go; page 12 of the puddle book is 4800 wide), pictures are read at that size rather than shrunk afterwards, and only as many full-size pictures are kept as the phone's memory class allows (2 to 4). When Android says memory is short, the full-size pictures are let go and load again when wanted.
- **Sound** uses the same library as Windows, which plays through Android's own sound system. If it can't start, the app stays silent.

The books are unpacked from the app to its own storage the first time it runs, and again after each update.

### Adding a book from a file

The editor's Export makes one file per book, ending in `.picturebook` (a zip holding the book's folder). To put it on a phone, send it any way at all (WhatsApp, email, Google Drive, a cable) and open it there: Dassie offers itself, and the book goes on the end of the shelf. A grown-up can also add one in the grown-ups' area (behind the gate), under Books, with "Add a book file", which opens the phone's own file picker, and can take books added this way off the phone again there.

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

`Editor/` is `BookEditor.exe`, the **Dassie Book Editor**, the program the owner and other illustrators and writers use to make books. Its name is one constant, `EditorBrand.Name` in `Editor/EditorSkin.cs`, made from the reader's `Brand.Name`. It opens any book folder (or a folder of pictures with no `book.json` yet), and everything in the browser editor (`web/editor.js`) is there, plus narration, previews on phone screens, Check book and Export. The preview is the real reader: `Reader.Avalonia`'s `ReaderScreen`, as the Player and the phone app use it.

```
dotnet run --project Editor                         # the editor
dotnet run --project Editor -- /path/to/book        # with a book open
./publish-editor.sh /some/folder/outside/the/repo   # BookEditor-windows.zip, ready to unzip and run on Windows
```

The zip holds `BookEditor.exe` (one file with everything else packed in) and a `runtimes` folder beside it with the speech recogniser's native code for Windows (with and without AVX), about 47 MB in all. The native code is kept as loose files on purpose: packed inside the exe it can't be found. The speech recogniser's 148 MB file of speech knowledge is never in the zip; it is downloaded the first time it is used (below).

**The look.** The editor shares the reader's brand but is calmer and denser, as a desk tool for careful colour work should be: the wordmark small at the top left with "Book Editor" beside it, cream and paper panels, bottle green for the main action (Save turns green while there is something to save), brass only to mark what is selected, the reader's raised buttons made smaller (32 units high, corners of 7), Nunito for everything and Fraunces only for the big headings. Spacing keeps to a 4 unit grid. The page itself sits on a neutral mid grey, so the artwork's own colours can be judged; View, Round the page offers a light grey or paper instead. The look is in `Editor/Ui.cs` (colours, type, small builders) and `Editor/EditorSkin.cs` (the standard controls).

**The window.** Along the top: the menus (File, Edit, View, Book, Help), New, Open and Recent, Undo and Redo, the book's title, then Play, Check, Export, Save and Help. On a narrow window (a 1366 x 768 laptop at 125%) the words beside the tools go and their icons and tool tips stay. Below that, the pages down the left (drag one onto another to reorder; the page being edited is kept in view), the page in the middle, settings for whatever is selected on the right, and the camera's journey through the page as cards along the bottom (drag a card onto another to reorder). Above the page: what dragging draws, "Middle of the spread" and "Screens", which chooses the screens whose frames are drawn round the selected stop. Along the bottom, a status bar: whether everything is saved, the page and what is selected, and the zoom (out, the percentage, in, fit), where 100% is one pixel of the picture to one of the screen. A box under the mouse lights up, so it is clear what a click will take. Ctrl+Z and Ctrl+Y undo and redo every change, typing in a box counting as one step. The smallest the window goes is 960 x 560, which fits a 1366 x 768 laptop at 125% with its taskbar.

**The start screen.** With no book open: start a new book, open a book folder, or open a book file (`.picturebook`, made by Export, perhaps on another computer: it is unpacked into a new folder of its own in Documents\Picture books, named after the book, and that folder is opened; the file itself is left as it was). Beside them, the recent books as cards with their covers and when they last changed, and the books in the reader's content folder when there is one. A book folder or a `.picturebook` dropped on the window opens too. The first time, the walkthrough is offered here.

**Keys.** F1 (Help, Keyboard shortcuts and tips) shows every shortcut, grouped, from one list (`Shortcuts` in `Editor/MainWindow.Chrome.cs`) that the menus and tool tips also use: Ctrl+N, Ctrl+O, Ctrl+Shift+O (open a book file), Ctrl+S, Ctrl+W (close the book), P or F5 (play), F7 (check), Ctrl+E (export), Ctrl+Z, Ctrl+Y, Ctrl+C, Ctrl+V, Ctrl+D, Delete, Esc, the arrow keys (Shift further, Alt resizes), `[` and `]` (stop before or after), Page Up and Page Down, Home and End (first and last page), Ctrl+Page Up and Down (move the page), Ctrl+= and Ctrl+- (zoom), Ctrl+0 (fit), Ctrl+1 (100%), and the read-aloud keys below.

**What the editor remembers.** In `%APPDATA%\BookEditor\settings.json`: the window's place and size (and whether it was maximised; a window left on a screen that has since been unplugged comes back onto one that is there), the folder a file or folder picker last came back from (the next one starts there), the colour round the page, the screens shown round a stop and "Middle of the spread". Never anything about a book. A damaged file just means the defaults.

**Messages.** A short note at the bottom says what just happened and goes away by itself; when something went wrong it is red, stays longer and says what to do. Questions (save changes, get back unsaved changes) and the new book form are cards in the window, with the main answer in green and Esc for Cancel; a problem in the new book form is shown in the form.

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

In the reader, a parents' setting **Read to me** (`readToMe`, on to start with, under Reading in the grown-ups' area) plays a stop's recording when the camera arrives there and lights up each word of the caption as it is spoken. A tap goes on straight away and stops the voice. Without watch mode the reader waits for a tap, and once the voice has finished the Next arrow glows twice to say it's the child's turn. In watch mode it goes on 1.5 seconds after the voice ends (or after the stop's own rest time). A stop that goes on by itself waits for the voice first. A book that opens straight onto a stop (a first page that doesn't open whole, the whole page first switched off, or a jump to a stop) plays that stop's voice and sound as soon as the reader is showing, as arriving there would. Books with no recordings behave exactly as before, and the words are plain text unless a voice is reading them. `readToMe` is not in `web/reader.js`, so the settings test lists it as a desktop-only setting (`ReaderSettings.DesktopOnlyKeys`) rather than changing the browser version.

**Preview.** Play (P) opens the book as it is now in the real reader (the Dassie reader with its own look, exactly as the apps draw it), from the selected stop (playing the move into it) or the page's start, sized as a Windows window, a Galaxy S22 Ultra upright or sideways, a cheap 720 x 1600 phone upright or sideways, or a 10 inch tablet, in a dark frame on the same neutral grey as the page. Phones behave as the phone app does (one page at a time when upright, the phone's picture limits and safe areas). Open book, Read to me, watch mode, the Ask lines, the reader's bedtime colours and its language (English or Afrikaans, for its own buttons and messages) can be switched, so illustrators see what families will. The editor round it keeps its own colours. Nothing needs saving first.

**Words on the page.** In the Page section, "Print the words on the page" adds a words box (at the top of the page that doesn't hold the first stop) and selects it. Drag it over a calm part of the picture and drag a corner to change its width; the words are drawn on the page exactly as the reader prints them, as they are typed. Its panel has the words to print (empty prints the stops' words), Left, Middle or Right, the letter size, the colour, and "Fit the box to the words". The box turns red, with a line where the words really end, when they don't fit. Delete takes the words off the page.

**Check book** (F7) lists, most serious first: missing pictures, sounds and recordings, stops or things to find outside the picture, tiny stops, stops with no words, captions too long for a phone held sideways or upright, pictures over 4096 pixels (Export shrinks them, or cuts explore scenes into tiles) or too small, pages of a different shape, stops with no recording in a book that has recordings, word timings that no longer match the words, printed words that don't fit their box, run off the picture, cross the middle of a spread or are too small, story steps numbered with gaps, effects and pictures on top set to stops that don't exist, and files in the folder the book doesn't use (which can be moved into an `_unused` folder; nothing is deleted). Click a line to go to it.

**Export** (Ctrl+E) makes a copy of the book for the apps in a folder of its own, named after the book's folder name, and a book file to send to a phone if wanted (`<id>.picturebook`, a zip holding that folder; see "Adding a book from a file" under Android). Only the files the book uses are copied. Page pictures are made no bigger than 4096 pixels (or 2560 for a smaller download, or left as they are), pictures with no see-through parts become JPEG, covers are made smaller, and explore scenes over the limit are cut into tiles (an overview picture plus 1024-pixel tiles at full and half size, the layout the readers already load) so a phone only loads what the child is looking at. Scenes already cut into tiles are copied as they are. Missing sounds, recordings (on stops and on things to find) and pictures on top are left out rather than left pointing at nothing. Ticking "Also add it to a reader's books" copies the result into a content folder's `books/` and adds it to the end of its `library.json`.

**Getting a book into the apps.** Send the `.picturebook` file to the phone and open it there; BookReader.exe takes the same file (dropped on its window, "Add a book file", or "Open with"). BookReader.exe can also open any exported folder with "Open a book folder", or shows it on the shelf once it is in the `content` folder beside the program (Export can put it there). To pack a book inside the phone app itself, put the exported book in `content/books/<id>`, add its id to `content/library.json` (Export does both when pointed at the repo's `content` folder), then build and install the app again with `publish-android.sh`.

**The walkthrough.** The first time the editor runs, the start screen offers a short walkthrough ("Show me how" or "No thanks"); it is always there again from Help (F1, or Help, Step-by-step walkthrough), which also offers to carry on from where it was stopped. Ten steps make a first book: start a book, add the pictures (with the left and right pages tip), draw a stop, type the words, add a voice, time the words, try it on a phone in Play, Check book, Export, and send the book file to the phone. Each step is a card with a sentence or two and a brass ring round the real control to use; it moves on by itself about a second after the step is done (or with Next or "Skip this step"), and "Stop for now" pauses it. How far it got is kept in `%APPDATA%\BookEditor\walkthrough.json`. The code is `Editor/Walkthrough.cs`; the ring and card sit in a layer over the window that lets clicks through to the controls underneath.

**Show the book file.** In File, in the Help sheet and in the Book section, this shows the book's `book.json` as text, exactly as Save writes it (with any unsaved changes), read-only, with "Copy all", for sending to someone helping sort out a problem.

**The screenshot hook.** Like the Player, the editor can draw one screen to a PNG with no display, never touching the real settings, recent books or safety copies:

```
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot main.png --book ../content/books/biggest-puddle --page 1 --stop 1
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot phone.png --book ../content/books/biggest-puddle --page 1 --stop 1 --screen preview --device s22-upright --wait 2500
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot check.png --book ../content/books/waterhole --screen check
```

`--screen` is main, welcome, welcome-offer, preview, check, export, help, newbook, bookfile, about or close (closing the book, which asks first when there are changes), and `--do "drag 800,250 950,400; key ctrl+Z; save"` acts out mouse and keys first (`move x,y` only moves the mouse, to see what lights up; `say text` and `problem text` show a message; `surround light` changes the colour round the page). `--scale 1.25` draws it as Windows does at 125% display scaling: the picture keeps its size in pixels and everything in it is bigger, so `--size 1366x768 --scale 1.25` is a common laptop. `--do words` selects the page's printed words box. `--tour N` shows the walkthrough at step N. `--do autotime` really times the selected stop's (or `--find N` thing's) words, waiting for it, with `--models <folder>` pointing at a downloaded speech recogniser (screenshots otherwise start as a computer that hasn't downloaded it); `--do "download 4000"` starts the real download and takes the picture four seconds in. `--help` lists everything.

```
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot tour.png --book ../content/books/biggest-puddle --page 1 --stop 1 --tour 6
dotnet Editor/bin/Release/net10.0/BookEditor.dll --shot timed.png --book /a/copy/of/a/book --page 1 --stop 1 --models ~/.config/BookEditor/models --do autotime
```

## The app icons

Both apps have an icon drawn in code from the mascot (`AppIcon` in `Reader.Avalonia/BrandArt.cs`): the dassie peeking over an open book in front of a brass sun. The reader's is on bottle green; the Book Editor's is on cream with a pencil across the corner, so the two belong together but can't be confused on a taskbar. At 24 pixels and under only the face (and the pencil) is drawn, so it still reads. The windows draw their icon from the same code as they open.

The files the projects use are made from that drawing, and kept in the repo:

```
dotnet Editor/bin/Release/net10.0/BookEditor.dll --make-icons /some/folder
```

writes `dassie.ico` (copy to `Player/Assets/`) and `dassie-editor.ico` (to `Editor/Assets/`), each with 16, 24, 32, 48, 64 and 256 pixel pictures, the Android launcher icons in `android/` (copy its folders into `Phone/Resources/`: square and round icons for each screen density, the adaptive icon's foreground with its picture in the middle two thirds, and its bottle green background colour), and `preview.png` showing every size on light and dark. The Windows icons are set with `ApplicationIcon` in `Player.csproj` and `Editor.csproj`; the Android one with `Icon` and `RoundIcon` in `Phone/MainActivity.cs`. The Player and the phone app only use the pictures: they never reference the editor.

## Different from the browser version

- **The open book, the page turn and the cover** are only in this reader (Windows and Android). In the browser, `flip` flies across and books start open.
- **Reduced motion.** "Follow this device's setting" reads the Windows setting (Settings, Accessibility, Visual effects, Animation effects). Linux and macOS are not asked yet, so there the Movement choice under Reading in the grown-ups' area is the way to reduce motion.
- **No hand hints.** At the very start the Next arrow pulses instead of a tapping hand, and there is no dragging hand on explore pages (the idle hints do that job).
- **Making room for a caption** starts gently. The browser moves the picture a fixed share of the way every frame, which starts with a jolt; here it eases like a damped spring and arrives at about the same time.
- **Soft glows** around the idle hint and the touch ripple are built from a few faint rings instead of a blurred shadow. They look much the same and cost less to draw.
- **The editor.** The desktop editor is a separate program (`BookEditor.exe`, below). There is no way into it from the shelf, the Player or the phone app, and they don't contain it.
- **Narration and read-along** (a recorded voice per stop or per thing to find, with the words lighting up) are only in this reader. The browser prototype in `web/` is retired and isn't changed any more: it ignores the `voice` field, and the browser editor would drop it if it saved the book.
- **Book files** (`.picturebook`) are only for these apps; the browser version doesn't open them.
- **Still pages are not redrawn.** The browser redraws every frame. This reader stops when nothing on screen is changing.
- **Page pictures** are kept at several sizes, and the full-size ones load when they are wanted, as described above. Tiles of big scenes load only where the child is looking, as in the browser.
