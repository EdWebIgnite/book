#!/usr/bin/env bash
# Builds the Android app (phones and tablets) with the sample books packed inside it.
#   desktop/publish-android.sh <output folder> [keystore] [key alias]
# Makes BookReader.apk (copy it to a phone and open it to install) and BookReader.aab (for Google Play).
#
# Signing: give a keystore and its alias, and the passwords in the environment as
# BOOKREADER_STORE_PASS and BOOKREADER_KEY_PASS. Without a keystore the build is signed with the
# .NET debug key, which is fine for trying the app on your own phone. Never put a keystore in the repo.
#
# Needs the .NET 10 SDK with the android workload, the Android SDK (platform 36) and a JDK. Where they
# are can be set with ANDROID_HOME and JAVA_HOME.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?Give the folder to put BookReader.apk in}"
keystore="${2:-}"
alias="${3:-bookreader}"
mkdir -p "$out"
out="$(cd "$out" && pwd)"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

args=(-c Release -o "$stage")
[ -n "${ANDROID_HOME:-}" ] && args+=("-p:AndroidSdkDirectory=$ANDROID_HOME")
[ -n "${JAVA_HOME:-}" ] && args+=("-p:JavaSdkDirectory=$JAVA_HOME")
if [ -n "$keystore" ]; then
  : "${BOOKREADER_STORE_PASS:?Set BOOKREADER_STORE_PASS to the keystore password}"
  args+=(-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$(cd "$(dirname "$keystore")" && pwd)/$(basename "$keystore")"
         "-p:AndroidSigningKeyAlias=$alias"
         -p:AndroidSigningStorePass=env:BOOKREADER_STORE_PASS
         "-p:AndroidSigningKeyPass=env:${BOOKREADER_KEY_PASS:+BOOKREADER_KEY_PASS}${BOOKREADER_KEY_PASS:-BOOKREADER_STORE_PASS}")
fi
dotnet publish "$here/Phone/Phone.csproj" "${args[@]}"

cp "$(ls "$stage"/*-Signed.apk | head -n 1)" "$out/BookReader.apk"
cp "$(ls "$stage"/*-Signed.aab "$stage"/*.aab 2>/dev/null | head -n 1)" "$out/BookReader.aab"
echo "Made $out/BookReader.apk ($(du -h "$out/BookReader.apk" | cut -f1)) and $out/BookReader.aab"
