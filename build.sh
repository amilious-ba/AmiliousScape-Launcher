#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$ROOT_DIR/Saradomin"
OUT_DIR="$ROOT_DIR/dist"

echo "==> Building AmiliousScape Launcher"
echo "Root: $ROOT_DIR"

mkdir -p "$OUT_DIR"
cd "$PROJECT_DIR"

# Stop any running launcher that might lock the output
pkill -9 -f "[Ss]aradomin" 2>/dev/null || true

echo "==> Restoring packages"
dotnet restore

COMMON_FLAGS=(
  -c Release
  --self-contained true
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

echo
echo "==> Done"
echo "Linux:   $OUT_DIR/AmiliousScape-Launcher-linux-x64"
echo "Windows: $OUT_DIR/AmiliousScape-Launcher-win-x64.exe"
ls -lh "$OUT_DIR"
