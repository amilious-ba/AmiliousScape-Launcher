#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$ROOT_DIR/Saradomin"
OUT_DIR="$ROOT_DIR/dist"

read -r -p "Version (e.g. 1.7.0): " VERSION
if [[ -z "${VERSION}" ]]; then
  echo "Version is required."
  exit 1
fi

echo "==> Building AmiliousScape Launcher v${VERSION}"
mkdir -p "$OUT_DIR"
cd "$PROJECT_DIR"

pkill -9 -f "[Ss]aradomin" 2>/dev/null || true

dotnet restore

COMMON_FLAGS=(
  -c Release
  --self-contained true
  -p:Version="${VERSION}"
  -p:PublishSingleFile=true
  -p:PublishReadyToRun=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:PublishTrimmed=false
)

echo "==> Publishing Linux x64"
rm -rf bin/Release/net6.0/linux-x64/publish
dotnet publish -r linux-x64 "${COMMON_FLAGS[@]}"
cp -f "bin/Release/net6.0/linux-x64/publish/Saradomin" \
  "$OUT_DIR/AmiliousScape-Launcher-linux-x64"
chmod +x "$OUT_DIR/AmiliousScape-Launcher-linux-x64"

echo "==> Publishing Windows x64"
rm -rf bin/Release/net6.0/win-x64/publish
dotnet publish -r win-x64 "${COMMON_FLAGS[@]}"
cp -f "bin/Release/net6.0/win-x64/publish/Saradomin.exe" \
  "$OUT_DIR/AmiliousScape-Launcher-win-x64.exe"

echo "==> Done v${VERSION}"
ls -lh "$OUT_DIR"