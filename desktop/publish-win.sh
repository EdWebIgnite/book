#!/usr/bin/env bash
# Builds the Windows program and packs it with the sample books into one zip.
#   desktop/publish-win.sh [output folder]      (default: desktop/out)
# The result is BookReader-windows.zip: unzip it anywhere and run BookReader.exe.
# Nothing else needs to be installed on the Windows machine.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/out}"
stage="$out/BookReader"
rm -rf "$stage" "$out/BookReader-windows.zip"
mkdir -p "$stage"
dotnet publish "$here/Player" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "$stage"
rm -f "$stage"/*.pdb
# The program looks for a "content" folder beside itself.
cp -r "$here/../content" "$stage/content"
cat > "$stage/READ ME.txt" <<'TXT'
Book Reader

Double-click BookReader.exe to start. Nothing needs installing.

The first time, Windows may say it protected your PC, because the program is
not signed. Click "More info", then "Run anyway".

The books are in the "content" folder next to the program. They are the same
files the browser version uses.

Keys: Right or Space goes on. Left goes back. W shows the whole page.
+ and - zoom on explore pages. S opens the settings. Esc goes back to the books.
F shows how many frames a second the program is drawing (top left).
TXT
(cd "$out" && zip -qr BookReader-windows.zip BookReader)
echo "Made $out/BookReader-windows.zip"
