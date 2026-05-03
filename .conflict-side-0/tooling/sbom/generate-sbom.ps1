<#
.SYNOPSIS
    Generate a CycloneDX Software Bill of Materials (SBOM) for the Sunfish
    solution. Wave 4.5 of the paper-alignment plan; paper §16.1 requires an
    SBOM published with each release for security review.

.DESCRIPTION
    Installs the CycloneDX .NET tool (global tool, pinned version) if it is
    not already present, runs it against Sunfish.slnx, writes the SBOM as
    JSON to <repo-root>/sbom/Sunfish.cdx.json, and prints its SHA-256 for
    release-note attestation.

    This script is explicitly a *scaffolding* deliverable: a production release
    pass must add signing of the SBOM (cosign / in-toto attestation), GPG
    signing of the release tag, and ingestion into a dependency-scanning
    platform (Dependency-Track, Snyk, etc.). Those concerns are deliberately
    out of scope for Wave 4.5. See docs/specifications/air-gap-deployment.md
    for how the SBOM plugs into the internal-update-server design.

.PARAMETER RepoRoot
    Repository root. Defaults to the parent of this script's containing
    directory (i.e. tooling/sbom/.. = repo root).

.PARAMETER OutputDirectory
    Directory to emit the SBOM into. Defaults to <RepoRoot>/sbom.

.PARAMETER CycloneDxVersion
    Version of the CycloneDX .NET tool to install if absent. Pinned for
    reproducibility. Bump this in a dedicated commit with the bump rationale.

.EXAMPLE
    pwsh -File tooling/sbom/generate-sbom.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [string]$OutputDirectory,
    [string]$CycloneDxVersion = '4.0.2'
)

$ErrorActionPreference = 'Stop'

function Write-Step($message) {
    Write-Host "[sbom] $message" -ForegroundColor Cyan
}

function Test-CycloneDxInstalled {
    try {
        $null = & dotnet tool list --global 2>$null | Select-String -SimpleMatch 'cyclonedx'
        return $LASTEXITCODE -eq 0 -and ($null -ne (dotnet tool list --global | Select-String -SimpleMatch 'cyclonedx'))
    } catch {
        return $false
    }
}

function Install-CycloneDxTool {
    param([string]$Version)
    Write-Step "Installing CycloneDX .NET tool v$Version (global)..."
    & dotnet tool install --global CycloneDX --version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install CycloneDX tool (exit $LASTEXITCODE)."
    }
}

function Get-FileSha256 {
    param([string]$Path)
    $hash = Get-FileHash -Path $Path -Algorithm SHA256
    return $hash.Hash.ToLowerInvariant()
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $RepoRoot 'sbom'
}

$solution = Join-Path $RepoRoot 'Sunfish.slnx'
if (-not (Test-Path $solution)) {
    throw "Sunfish.slnx not found at $solution"
}

if (-not (Test-CycloneDxInstalled)) {
    Install-CycloneDxTool -Version $CycloneDxVersion
} else {
    Write-Step "CycloneDX tool already installed -- skipping install."
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

Write-Step "Running dotnet-CycloneDX against $solution ..."
Push-Location $RepoRoot
try {
    # -o sets output directory, -j emits JSON, -sv sets SBOM version to the
    # current timestamp so downstream consumers can order releases.
    & dotnet-CycloneDX `
        --output $OutputDirectory `
        --json `
        --set-name 'Sunfish' `
        $solution
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-CycloneDX failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

# CycloneDX emits bom.json by default; rename to the canonical name.
$default = Join-Path $OutputDirectory 'bom.json'
$target  = Join-Path $OutputDirectory 'Sunfish.cdx.json'
if (Test-Path $default) {
    Move-Item -Path $default -Destination $target -Force
}

if (-not (Test-Path $target)) {
    throw "Expected SBOM at $target but it was not produced."
}

$sha = Get-FileSha256 -Path $target
Write-Step "SBOM written to $target"
Write-Step "SHA-256: $sha"

# Emit an attestation stub next to the SBOM so release tooling can pick it up.
$attestationPath = Join-Path $OutputDirectory 'Sunfish.cdx.sha256'
"$sha  Sunfish.cdx.json" | Out-File -FilePath $attestationPath -Encoding ascii -NoNewline
Write-Step "Attestation stub written to $attestationPath"
