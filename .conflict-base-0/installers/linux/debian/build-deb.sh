#!/usr/bin/env bash
# Build an unsigned Sunfish .deb package.
# Wave 4.5 scaffolding — see installers/linux/README.md for context.
#
# Usage:
#   ./installers/linux/debian/build-deb.sh --version 0.1.0-preview [--arch amd64]

set -euo pipefail

VERSION="0.0.0-dev"
ARCH="amd64"
RUNTIME="linux-x64"
CONFIGURATION="Release"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --arch)    ARCH="$2"; shift 2 ;;
        --runtime) RUNTIME="$2"; shift 2 ;;
        *) echo "Unknown arg: $1" >&2; exit 1 ;;
    esac
done

log() { printf '[deb] %s\n' "$*"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
STAGING="$SCRIPT_DIR/staging"
OUTPUT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)/output"

APP_PROJECT="$REPO_ROOT/apps/local-node-host/Sunfish.LocalNodeHost.csproj"

rm -rf "$STAGING"
mkdir -p "$STAGING/opt/sunfish/local-node-host"
mkdir -p "$STAGING/lib/systemd/system"
mkdir -p "$STAGING/etc/sunfish"
mkdir -p "$STAGING/DEBIAN"
mkdir -p "$OUTPUT_DIR"

log "Publishing $APP_PROJECT ($RUNTIME, $CONFIGURATION)…"
dotnet publish "$APP_PROJECT" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$STAGING/opt/sunfish/local-node-host" \
    -p:Version="$VERSION"

cp "$SCRIPT_DIR/sunfish-local-node.service" \
   "$STAGING/lib/systemd/system/sunfish-local-node.service"

# DEBIAN/control
cat > "$STAGING/DEBIAN/control" <<EOF
Package: sunfish
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: Sunfish <noreply@sunfish.example>
Description: Sunfish local-node kernel host
 Headless background service that hosts the Sunfish local-node kernel.
 See /opt/sunfish/local-node-host/README for paper-alignment details.
EOF

# DEBIAN/postinst — creates the sunfish user + enables/starts the service.
cat > "$STAGING/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if ! getent passwd sunfish >/dev/null; then
    adduser --system --group --home /var/lib/sunfish --no-create-home sunfish
fi
mkdir -p /var/lib/sunfish
chown -R sunfish:sunfish /var/lib/sunfish
# Per paper §16.3, /etc/sunfish is the pre-seeded-config directory only; it
# is intentionally world-readable and NOT chowned to sunfish.
chmod 0755 /etc/sunfish

# systemd enable + start. Tolerant of non-systemd-backed chroots.
if [ -d /run/systemd/system ]; then
    systemctl daemon-reload
    systemctl enable sunfish-local-node.service >/dev/null 2>&1 || true
    systemctl start  sunfish-local-node.service >/dev/null 2>&1 || true
fi
EOF
chmod 0755 "$STAGING/DEBIAN/postinst"

# DEBIAN/prerm — stop + disable the service before file removal.
cat > "$STAGING/DEBIAN/prerm" <<'EOF'
#!/bin/sh
set -e
if [ -d /run/systemd/system ]; then
    systemctl stop    sunfish-local-node.service >/dev/null 2>&1 || true
    systemctl disable sunfish-local-node.service >/dev/null 2>&1 || true
fi
EOF
chmod 0755 "$STAGING/DEBIAN/prerm"

# DEBIAN/postrm — on `purge` only, drop the user. User-data under
# $XDG_DATA_HOME is preserved per paper §16.3 (enterprise wipe is a separate
# operation, not a side-effect of uninstall).
cat > "$STAGING/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
case "$1" in
  purge)
    if getent passwd sunfish >/dev/null; then
        deluser --system sunfish >/dev/null 2>&1 || true
    fi
    # Deliberately do NOT remove /var/lib/sunfish here — operators must make
    # the wipe decision explicitly.
    ;;
esac
EOF
chmod 0755 "$STAGING/DEBIAN/postrm"

PKG_NAME="sunfish_${VERSION}_${ARCH}.deb"
log "Building package $PKG_NAME…"
fakeroot dpkg-deb --build "$STAGING" "$OUTPUT_DIR/$PKG_NAME"

log "Built: $OUTPUT_DIR/$PKG_NAME"
log "Signing skipped (Wave 4.5 scaffolding). See README for production guidance."
