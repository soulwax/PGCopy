#!/usr/bin/env bash
set -euo pipefail

# File: scripts/dist-linux.sh
#
# Builds Linux production artifacts into ./dist:
#   - Executable bundle (published app files)
#   - .deb package
#   - .rpm package
#   - .AppImage
#
# Prerequisites:
#   - dotnet
#   - fpm
#   - appimagetool
#
# Usage:
#   chmod +x scripts/dist-linux.sh
#   ./scripts/dist-linux.sh
#
# Optional env overrides:
#   VERSION=0.3.0
#   CONFIGURATION=Release
#   RUNTIME_ID=linux-x64
#   ARCH=x86_64
#   APP_NAME=postgrescopy-desktop

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist"
STAGING_DIR="$ROOT_DIR/.artifacts/linux-dist"
PUBLISH_DIR="$DIST_DIR/publish"

APP_NAME="${APP_NAME:-postgrescopy}"
DISPLAY_NAME="${DISPLAY_NAME:-PostgresCopy}"
DESCRIPTION="${DESCRIPTION:-Dead-simple PostgreSQL table data copy tool.}"
MAINTAINER="${MAINTAINER:-PostgresCopy contributors}"
URL="${URL:-https://github.com/}"
LICENSE="${LICENSE:-MIT}"

CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_ID="${RUNTIME_ID:-linux-x64}"
ARCH="${ARCH:-x86_64}"

# Linux packaging is OS/SDK-aware:
# - The WindowsForms GUI targets net*-windows, so publishing it for linux-x64 fails.
# - We still keep the GUI project referenced for future Windows packaging, but for Linux we
#   publish the OS-agnostic CLI project and package that.
PROJECT_FILE="$ROOT_DIR/src/PostgresCopy/PostgresCopy.csproj"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command not found: $1" >&2
    exit 1
  fi
}

require_cmd dotnet

HAVE_FPM=0
if command -v fpm >/dev/null 2>&1; then
  HAVE_FPM=1
fi

HAVE_APPIMAGETOOL=0
if command -v appimagetool >/dev/null 2>&1; then
  HAVE_APPIMAGETOOL=1
fi

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Error: project file not found: $PROJECT_FILE" >&2
  exit 1
fi

VERSION="${VERSION:-$(grep -oPm1 '(?<=<Version>)[^<]+' "$PROJECT_FILE" || true)}"
if [[ -z "${VERSION:-}" ]]; then
  echo "Error: VERSION is empty. Set VERSION env var or add <Version> in csproj." >&2
  exit 1
fi

echo "==> Preparing folders"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR" "$DIST_DIR" "$PUBLISH_DIR"

echo "==> Publishing to $PUBLISH_DIR"
dotnet publish "$PROJECT_FILE" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME_ID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$PUBLISH_DIR"

MAIN_BINARY="$PUBLISH_DIR/PostgresCopy"
if [[ ! -f "$MAIN_BINARY" ]]; then
  # fallback in case name differs
  MAIN_BINARY="$(find "$PUBLISH_DIR" -maxdepth 1 -type f -executable | head -n 1 || true)"
fi

if [[ -z "${MAIN_BINARY:-}" || ! -f "$MAIN_BINARY" ]]; then
  echo "Error: could not determine published desktop executable in $PUBLISH_DIR" >&2
  exit 1
fi

echo "==> Building DEB/RPM payload layout"
PKG_ROOT="$STAGING_DIR/pkgroot"
mkdir -p "$PKG_ROOT/opt/$APP_NAME" "$PKG_ROOT/usr/bin"

cp -a "$PUBLISH_DIR/." "$PKG_ROOT/opt/$APP_NAME/"
cat > "$PKG_ROOT/usr/bin/$APP_NAME" <<EOF
#!/usr/bin/env bash
exec /opt/$APP_NAME/$(basename "$MAIN_BINARY") "\$@"
EOF
chmod +x "$PKG_ROOT/usr/bin/$APP_NAME"

if [[ "$HAVE_FPM" -eq 1 ]]; then
  echo "==> Creating .deb"
  fpm -s dir -t deb \
    -n "$APP_NAME" \
    -v "$VERSION" \
    --architecture "$ARCH" \
    --description "$DESCRIPTION" \
    --license "$LICENSE" \
    --maintainer "$MAINTAINER" \
    --url "$URL" \
    --package "$DIST_DIR/${APP_NAME}_${VERSION}_${ARCH}.deb" \
    -C "$PKG_ROOT" \
    .

  echo "==> Creating .rpm"
  fpm -s dir -t rpm \
    -n "$APP_NAME" \
    -v "$VERSION" \
    --architecture "$ARCH" \
    --description "$DESCRIPTION" \
    --license "$LICENSE" \
    --maintainer "$MAINTAINER" \
    --url "$URL" \
    --package "$DIST_DIR/${APP_NAME}-${VERSION}.${ARCH}.rpm" \
    -C "$PKG_ROOT" \
    .
else
  echo "==> Skipping .deb/.rpm creation: fpm not found"
fi

echo "==> Preparing AppImage payload layout"
APPDIR="$STAGING_DIR/AppDir"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/icons/hicolor/256x256/apps"

cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"

# Optional icon: keep script resilient when GUI assets are not available for this packaging mode.
ICON_SRC="$ROOT_DIR/src/PostgresCopy.Desktop/Assets/kitsunedb.png"
if [[ -f "$ICON_SRC" ]]; then
  cp "$ICON_SRC" "$APPDIR/usr/share/icons/hicolor/256x256/apps/${APP_NAME}.png" || true
  cp "$ICON_SRC" "$APPDIR/${APP_NAME}.png" || true
fi

cat > "$APPDIR/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=$DISPLAY_NAME
Exec=$APP_NAME
Icon=$APP_NAME
Type=Application
Categories=Utility;
Terminal=false
EOF

cp "$APPDIR/${APP_NAME}.desktop" "$APPDIR/AppRun.desktop" 2>/dev/null || true

cat > "$APPDIR/AppRun" <<EOF
#!/usr/bin/env bash
HERE="\$(dirname "\$(readlink -f "\$0")")"
exec "\$HERE/usr/bin/$(basename "$MAIN_BINARY")" "\$@"
EOF
chmod +x "$APPDIR/AppRun"

if [[ "$HAVE_APPIMAGETOOL" -eq 1 ]]; then
  echo "==> Creating AppImage"
  APPIMAGE_PATH="$DIST_DIR/${APP_NAME}-${VERSION}-${ARCH}.AppImage"
  ARCH_FOR_APPIMAGE="$ARCH"
  if [[ "$ARCH_FOR_APPIMAGE" == "x86_64" ]]; then
    ARCH_FOR_APPIMAGE="x86_64"
  fi

  ARCH="$ARCH_FOR_APPIMAGE" appimagetool "$APPDIR" "$APPIMAGE_PATH"
else
  echo "==> Skipping AppImage creation: appimagetool not found"
fi

echo "==> Done. Artifacts in $DIST_DIR:"
ls -1 "$DIST_DIR"
