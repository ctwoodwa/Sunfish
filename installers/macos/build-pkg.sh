#!/usr/bin/env bash
# Build an unsigned Sunfish .pkg using productbuild.
# Wave 4.5 scaffolding — see installers/macos/README.md for context.
#
# Usage:
#   ./installers/macos/build-pkg.sh --version 0.1.0-preview

set -euo pipefail

VERSION="0.0.0-dev"
RUNTIME="osx-arm64"
CONFIGURATION="Release"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --runtime) RUNTIME="$2"; shift 2 ;;  # osx-x64 or osx-arm64
        *) echo "Unknown arg: $1" >&2; exit 1 ;;
    esac
done

log() { printf '[pkg] %s\n' "$*"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

STAGING="$SCRIPT_DIR/staging"
OUTPUT_DIR="$SCRIPT_DIR/output"
SCRIPTS_DIR="$SCRIPT_DIR/scripts"
COMPONENT_PKG="$SCRIPT_DIR/component.pkg"
FINAL_PKG="$OUTPUT_DIR/Sunfish-${VERSION}.pkg"

APP_PROJECT="$REPO_ROOT/apps/local-node-host/Sunfish.LocalNodeHost.csproj"

rm -rf "$STAGING" "$SCRIPTS_DIR" "$COMPONENT_PKG"
mkdir -p "$STAGING/Library/Application Support/Sunfish/LocalNodeHost"
mkdir -p "$STAGING/Library/LaunchDaemons"
mkdir -p "$OUTPUT_DIR"
mkdir -p "$SCRIPTS_DIR"

log "Publishing $APP_PROJECT ($RUNTIME, $CONFIGURATION)…"
dotnet publish "$APP_PROJECT" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$STAGING/Library/Application Support/Sunfish/LocalNodeHost" \
    -p:Version="$VERSION"

cp "$SCRIPT_DIR/launchd/com.sunfish.local-node-host.plist" \
   "$STAGING/Library/LaunchDaemons/com.sunfish.local-node-host.plist"

# postinstall — launchctl bootstrap the daemon.
cat > "$SCRIPTS_DIR/postinstall" <<'EOF'
#!/bin/bash
set -e
mkdir -p /var/log/sunfish
chmod 0755 /var/log/sunfish

PLIST="/Library/LaunchDaemons/com.sunfish.local-node-host.plist"
# Ensure root ownership + 0644 perms so launchd accepts it.
chown root:wheel "$PLIST"
chmod 0644 "$PLIST"

# Use the modern bootstrap/bootout verbs (macOS 10.11+).
launchctl bootout system/com.sunfish.local-node-host 2>/dev/null || true
launchctl bootstrap system "$PLIST"
launchctl enable system/com.sunfish.local-node-host

exit 0
EOF
chmod 0755 "$SCRIPTS_DIR/postinstall"

log "Building component pkg…"
pkgbuild \
    --root "$STAGING" \
    --identifier "com.sunfish.local-node-host" \
    --version "$VERSION" \
    --scripts "$SCRIPTS_DIR" \
    --install-location "/" \
    "$COMPONENT_PKG"

log "Wrapping with productbuild…"
productbuild \
    --package "$COMPONENT_PKG" \
    --identifier "com.sunfish.installer" \
    --version "$VERSION" \
    "$FINAL_PKG"

log "Built: $FINAL_PKG"
log "Signing + notarization skipped (Wave 4.5 scaffolding). See README for production guidance."
