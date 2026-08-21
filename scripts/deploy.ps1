<#
.SYNOPSIS
    Copy the built plugin and its native speech libraries into the game, then verify.

.DESCRIPTION
    The game lives under Program Files, so this copy usually needs an elevated shell. Run it
    from an administrator prompt if it reports access denied.

    Verification is not optional here. Debugging a fix that was never actually deployed has
    cost this project real sessions, so the script compares the SHA-256 of the source and
    the deployed file and fails loudly if they differ.

.PARAMETER GameDir
    Override the game location if it is not at the default Steam path.
#>
[CmdletBinding()]
param(
    [string] $GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cult of the Lamb'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root 'src\CultAccess\bin\Release\netstandard2.1\CultAccess.dll'
$nativeDir = Join-Path $root 'lib'
$target = Join-Path $GameDir 'BepInEx\plugins\CultAccess'

if (-not (Test-Path $dll)) {
    Write-Host "No build found at $dll. Run scripts\build.ps1 first."
    exit 1
}

if (-not (Test-Path (Join-Path $GameDir 'BepInEx'))) {
    Write-Host "BepInEx not found under $GameDir."
    Write-Host 'A Steam update or file verification can remove it. Reinstall the loader before blaming the mod.'
    exit 1
}

if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

try {
    Copy-Item $dll -Destination $target -Force -ErrorAction Stop
}
catch [System.IO.IOException] {
    # Windows maps a loaded assembly into the process, so the file cannot be replaced while
    # the game holds it. The raw exception text does not say that, and guessing wrong here
    # wastes a whole test cycle on a build that was never actually deployed.
    Write-Host 'DEPLOY FAILED: the plugin file is in use.'
    Write-Host 'Cult of the Lamb is almost certainly still running. Close it and run this again.'
    exit 1
}
catch [System.UnauthorizedAccessException] {
    Write-Host 'DEPLOY FAILED: access denied writing into the game folder.'
    Write-Host 'Run this script from an administrator prompt.'
    exit 1
}

foreach ($native in @('x64\*.dll', '*.txt')) {
    $source = Join-Path $nativeDir $native
    if (Test-Path $source) { Copy-Item $source -Destination $target -Force }
}

# The editable string catalogue. A user may add their own lang\<code>.txt beside the
# shipped files, so copy in without clearing the folder.
$langSource = Join-Path $root 'lang'
if (Test-Path $langSource) {
    $langTarget = Join-Path $target 'lang'
    if (-not (Test-Path $langTarget)) { New-Item -ItemType Directory -Path $langTarget -Force | Out-Null }
    Copy-Item (Join-Path $langSource '*.txt') -Destination $langTarget -Force
}

# The sound folder, with its instructions. No audio ships; every cue is generated in code
# and a file dropped here overrides one. Copied in without clearing, so a player's own
# replacements survive an update.
$soundSource = Join-Path $root 'sounds'
if (Test-Path $soundSource) {
    $soundTarget = Join-Path $target 'sounds'
    if (-not (Test-Path $soundTarget)) { New-Item -ItemType Directory -Path $soundTarget -Force | Out-Null }
    Copy-Item (Join-Path $soundSource '*.txt') -Destination $soundTarget -Force
}

$sourceHash = (Get-FileHash $dll -Algorithm SHA256).Hash
$deployed = Join-Path $target 'CultAccess.dll'
$deployedHash = (Get-FileHash $deployed -Algorithm SHA256).Hash

if ($sourceHash -ne $deployedHash) {
    Write-Host 'DEPLOY VERIFICATION FAILED. The deployed file does not match the build.'
    Write-Host "  built    $sourceHash"
    Write-Host "  deployed $deployedHash"
    exit 1
}

$size = (Get-Item $deployed).Length
Write-Host "Deployed OK. $size bytes, hashes match."
Write-Host "SHA-256 $deployedHash"
Write-Host "Log: $GameDir\BepInEx\LogOutput.log"
