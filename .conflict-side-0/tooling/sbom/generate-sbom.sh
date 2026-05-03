#!/usr/bin/env bash
# Generate a CycloneDX SBOM for the Sunfish solution.
# Wave 4.5 of the paper-alignment plan; paper §16.1.
#
# Mirrors tooling/sbom/generate-sbom.ps1 for Linux / macOS CI.
#
# Usage:
#   ./tooling/sbom/generate-sbom.sh
#
# Env overrides:
#   REPO_ROOT            — defaults to the script's grandparent directory.
#   OUTPUT_DIR           — defaults to $REPO_ROOT/sbom.
#   CYCLONEDX_VERSION    — pinned version of the CycloneDX .NET tool.
#
# NOTE: This script is a scaffolding deliverable. A production release pass
# must add SBOM signing (cosign / in-toto attestation), tag signing, and
# ingestion into a dependency-scanning platform. Out of scope for Wave 4.5.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/sbom}"
CYCLONEDX_VERSION="${CYCLONEDX_VERSION:-4.0.2}"

log() { printf '[sbom] %s\n' "$*"; }

solution="$REPO_ROOT/Sunfish.slnx"
if [[ ! -f "$solution" ]]; then
    echo "Sunfish.slnx not found at $solution" >&2
    exit 1
fi

# Detect whether the CycloneDX global tool is already installed.
if dotnet tool list --global 2>/dev/null | grep -qi '^cyclonedx'; then
    log "CycloneDX tool already installed — skipping install."
else
    log "Installing CycloneDX .NET tool v$CYCLONEDX_VERSION (global)…"
    dotnet tool install --global CycloneDX --version "$CYCLONEDX_VERSION"
fi

# Ensure the dotnet global tools path is on $PATH for the remainder of the
# script (this is commonly an oversight on fresh CI images).
if ! command -v dotnet-CycloneDX >/dev/null 2>&1; then
    export PATH="$PATH:$HOME/.dotnet/tools"
fi
if ! command -v dotnet-CycloneDX >/dev/null 2>&1; then
    echo "dotnet-CycloneDX not on PATH after install. Is \$HOME/.dotnet/tools on PATH?" >&2
    exit 2
fi

mkdir -p "$OUTPUT_DIR"

log "Running dotnet-CycloneDX against $solution …"
(
    cd "$REPO_ROOT"
    dotnet-CycloneDX \
        --output "$OUTPUT_DIR" \
        --json \
        --set-name 'Sunfish' \
        "$solution"
)

# Canonicalize output filename.
default="$OUTPUT_DIR/bom.json"
target="$OUTPUT_DIR/Sunfish.cdx.json"
if [[ -f "$default" ]]; then
    mv -f "$default" "$target"
fi

if [[ ! -f "$target" ]]; then
    echo "Expected SBOM at $target but it was not produced." >&2
    exit 3
fi

# Portable SHA-256 — prefer shasum (ships on macOS), fall back to sha256sum.
if command -v shasum >/dev/null 2>&1; then
    sha="$(shasum -a 256 "$target" | awk '{print $1}')"
elif command -v sha256sum >/dev/null 2>&1; then
    sha="$(sha256sum "$target" | awk '{print $1}')"
else
    echo "Neither shasum nor sha256sum on PATH." >&2
    exit 4
fi

log "SBOM written to $target"
log "SHA-256: $sha"

printf '%s  Sunfish.cdx.json' "$sha" > "$OUTPUT_DIR/Sunfish.cdx.sha256"
log "Attestation stub written to $OUTPUT_DIR/Sunfish.cdx.sha256"
