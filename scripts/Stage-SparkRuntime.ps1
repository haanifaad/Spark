<#
.SYNOPSIS
    Stages the self-contained OpenClaw Gateway runtime and bundled Node.js executable
    into a Spark publish directory.

.DESCRIPTION
    Assembles a completely standalone, junction-free production OpenClaw Gateway
    runtime under `<DestinationDir>\runtimes\gateway` and copies the signed Node.js
    runtime executable into `<DestinationDir>\runtimes\node\node.exe`.

    The resulting Spark application does not depend on the openclaw source repository
    or a globally installed Node.js runtime.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationDir,

    [string]$OpenClawSourceDir,

    [string]$NodeExePath,

    [string]$CacheDir,

    [switch]$ForceRestage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Resolve-OpenClawSource {
    if ($OpenClawSourceDir -and (Test-Path -LiteralPath (Join-Path $OpenClawSourceDir "openclaw.mjs"))) {
        return (Resolve-Path -LiteralPath $OpenClawSourceDir).Path
    }
    if ($env:OPENCLAW_SOURCE_DIR -and (Test-Path -LiteralPath (Join-Path $env:OPENCLAW_SOURCE_DIR "openclaw.mjs"))) {
        return (Resolve-Path -LiteralPath $env:OPENCLAW_SOURCE_DIR).Path
    }

    $parent = Split-Path -Parent $repoRoot
    foreach ($name in @("openclaw-main", "openclaw")) {
        $candidate = Join-Path $parent $name
        if (Test-Path -LiteralPath (Join-Path $candidate "openclaw.mjs")) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Could not locate OpenClaw Core repository (openclaw.mjs). Pass -OpenClawSourceDir or set OPENCLAW_SOURCE_DIR."
}

function Resolve-NodeExe {
    $candidates = @(
        $NodeExePath,
        $env:SPARK_NODE_PATH,
        $env:OPENCLAW_NODE_PATH,
        (Join-Path $env:ProgramFiles "nodejs\node.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\node\node.exe")
    )

    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }

    $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "Could not locate Node.js executable (node.exe). Pass -NodeExePath or install Node.js 22+."
}

function Resolve-PnpmCmd {
    foreach ($name in @("pnpm.cmd", "pnpm.exe")) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) {
            return $cmd.Source
        }
    }
    $roaming = Join-Path $env:APPDATA "npm\pnpm.cmd"
    if (Test-Path -LiteralPath $roaming) {
        return $roaming
    }
    throw "Could not locate pnpm.cmd."
}

$resolvedDest = if (Test-Path -LiteralPath $DestinationDir) {
    (Resolve-Path -LiteralPath $DestinationDir).Path
} else {
    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    (Resolve-Path -LiteralPath $DestinationDir).Path
}

$resolvedCache = if ($CacheDir) {
    $CacheDir
} else {
    Join-Path $repoRoot "artifacts\gateway-stage"
}

$resolvedNode = Resolve-NodeExe

# Stage production gateway via pnpm deploy (flat hoisted node_modules, zero junctions)
$cacheEntry = Join-Path $resolvedCache "openclaw.mjs"
$cacheNodeModules = Join-Path $resolvedCache "node_modules"
if ($ForceRestage -or -not (Test-Path -LiteralPath $cacheEntry) -or -not (Test-Path -LiteralPath $cacheNodeModules)) {
    $sourceDir = Resolve-OpenClawSource
    $pnpm = Resolve-PnpmCmd
    Write-Host "Staging production OpenClaw Gateway from '$sourceDir' into '$resolvedCache'..." -ForegroundColor Cyan

    if (Test-Path -LiteralPath $resolvedCache) {
        Remove-Item -LiteralPath $resolvedCache -Recurse -Force
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedCache) -Force | Out-Null

    & $pnpm --dir $sourceDir --filter openclaw deploy --prod --prefer-offline --ignore-scripts --config.node-linker=hoisted $resolvedCache
    if ($LASTEXITCODE -ne 0) {
        throw "pnpm deploy failed with exit code $LASTEXITCODE."
    }
} else {
    Write-Host "Reusing cached OpenClaw Gateway stage at '$resolvedCache' (use -ForceRestage to rebuild)." -ForegroundColor Cyan
}

# Copy bundled Node.js runtime
$nodeDestDir = Join-Path $resolvedDest "runtimes\node"
New-Item -ItemType Directory -Path $nodeDestDir -Force | Out-Null
$nodeDestExe = Join-Path $nodeDestDir "node.exe"
Write-Host "Bundling Node.js runtime: $resolvedNode -> $nodeDestExe" -ForegroundColor Cyan
Copy-Item -LiteralPath $resolvedNode -Destination $nodeDestExe -Force

# Mirror staged gateway into runtimes\gateway, excluding non-runtime docs/src/patches
$gatewayDestDir = Join-Path $resolvedDest "runtimes\gateway"
New-Item -ItemType Directory -Path $gatewayDestDir -Force | Out-Null
Write-Host "Bundling OpenClaw Gateway runtime into '$gatewayDestDir'..." -ForegroundColor Cyan

$robocopyArgs = @(
    $resolvedCache,
    $gatewayDestDir,
    "/MIR",
    "/XD", (Join-Path $resolvedCache "docs"), (Join-Path $resolvedCache "src"), (Join-Path $resolvedCache "patches"),
    "/NFL", "/NDL", "/NJH", "/NJS", "/NC", "/NS", "/NP",
    "/R:2", "/W:1"
)
& robocopy @robocopyArgs | Out-Null
# Robocopy exit codes 0..7 indicate success; >= 8 indicate failure
if ($LASTEXITCODE -ge 8) {
    throw "Robocopy failed while staging gateway runtime with exit code $LASTEXITCODE."
}

# Verify staged runtime integrity
$bundledGatewayEntry = Join-Path $gatewayDestDir "openclaw.mjs"
if (-not (Test-Path -LiteralPath $bundledGatewayEntry)) {
    throw "Staged gateway entrypoint missing at $bundledGatewayEntry."
}

$versionOutput = & $nodeDestExe $bundledGatewayEntry --version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Bundled runtime smoke check failed (exit $LASTEXITCODE): $versionOutput"
}

Write-Host "Verified bundled Spark runtime: $($versionOutput -join ' ')" -ForegroundColor Green
