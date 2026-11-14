#!/usr/bin/env bash
# Builds the book editor for Windows and packs it into one zip.
#   desktop/publish-editor.sh [output folder]      (default: desktop/out)
# The result is BookEditor-windows.zip: unzip it anywhere and run BookEditor.exe.
# Nothing else needs to be installed on the Windows machine. The editor is its own program:
# it is never part of BookReader.exe or the phone app.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/out}"
stage="$out/BookEditor"
rm -rf "$stage" "$out/BookEditor-windows.zip"
mkdir -p "$stage"
dotnet publish "$here/Editor" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "$stage"
rm -f "$stage"/*.pdb
cat > "$stage/READ ME.txt" <<'TXT'
Book Editor

Double-click BookEditor.exe to start. Nothing needs installing.

The first time, Windows may say it protected your PC, because the program is
not signed. Click "More info", then "Run anyway".

Each book is a folder with its pictures, sounds and a file called book.json.
"Start a new book" makes the folder for you (in Documents, Picture books,
unless you choose somewhere else). "Open a book folder" opens one you have,
or makes a new book from a folder of pictures.

Making a book:
1. Add the page pictures. A picture can be a whole double-page spread, or
   add a left and a right page and they are joined into one spread.
2. Drag on a picture to draw where the camera stops, in reading order, and
   type the words for each stop.
3. Add a recording of each stop's words (or record with the microphone),
   then press Tap along and press Space as each word starts, so the words
   light up as they are read.
4. Press Play to see it in the real reader, on a computer or a phone.
5. Check book lists anything that will go wrong. Export makes a copy ready
   for the apps.

Save with Ctrl+S. While there are unsaved changes, a safety copy is kept
every few seconds, so nothing is lost if the computer stops. Ctrl+Z undoes
anything. F1 shows every key.

To put a book in BookReader.exe: export it with "Also add it to a reader's
books" ticked and choose the "content" folder next to BookReader.exe. Or open
the exported folder in BookReader with "Open a book folder".
TXT
(cd "$out" && zip -qr BookEditor-windows.zip BookEditor)
echo "Made $out/BookEditor-windows.zip"
