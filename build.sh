#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$ROOT_DIR/Saradomin"
OUT_DIR="$ROOT_DIR/dist"

# Latest GitHub release tag (e.g. v2.0.4 → 2.0.4)
LATEST_TAG="$(
  curl -fsSL -H "User-Agent: AmiliousScape-Launcher-Build" \
    "https://api.github.com/repos/amilious-ba/AmiliousScape-Launcher/releases/latest" \
    | sed -n 's/.*"tag_name":[[:space:]]*"\([^"]*\)".*/\1/p' \
    | head -n1
)" || true

LATEST_VERSION="${LATEST_TAG#v}"
LATEST_VERSION="${LATEST_VERSION#V}"

if [[ -n "${LATEST_VERSION}" ]]; then
  read -r -p "Version (latest on GitHub: ${LATEST_VERSION}): " VERSION
else
  read -r -p "Version (e.g. 1.7.0) [could not fetch GitHub latest]: " VERSION
fi

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