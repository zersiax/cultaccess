<#
.SYNOPSIS
    Build the tester package.

.DESCRIPTION
    Produces dist\CultAccess-tester-<version>.zip containing everything we are permitted to
    redistribute, plus an installer that handles the rest.

    Deliberately NOT included: BepInEx. The governing ruleset forbids shipping a mod loader,
    and BepInEx is LGPL 2.1 with its own distribution. install.ps1 installs it from a zip the
    tester downloads, or from one already present on the machine.

    Included and deliberate: nvdaControllerClient64.dll with its LGPL 2.1 licence text. NVDA
    is the target screen reader and requiring a separate hunt for a DLL would be a barrier for
    exactly the audience this mod serves. Recorded in THIRD_PARTY_NOTICES.md.
#>
[CmdletBinding()]
param(
    [string] $Version = '0.1.0',

    # Documented below as the offline escape, but never declared, so passing it was an error
    # and the check ran unconditionally. Harmless in effect and wrong as an interface.
    [switch] $SkipLinkCheck
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root 'src\CultAccess\bin\Release\netstandard2.1\CultAccess.dll'

# A dead link in tester instructions is how the wrong BepInEx got shipped. Gate the package
# on it rather than trusting anyone to remember. -SkipLinkCheck exists for working offline.
if (-not $SkipLinkCheck) {
    Push-Location $root
    try {
        python (Join-Path $root 'scripts/validate-links.py')
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Link check failed. Fix the URLs above, or re-run with -SkipLinkCheck if offline.'
            exit 1
        }
    }
    finally { Pop-Location }
}

if (-not (Test-Path $dll)) {
    Write-Host 'No Release build found. Run scripts\build.ps1 first.'
    exit 1
}

$stage = Join-Path $root "dist\stage-$Version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

$pluginDir = Join-Path $stage 'plugins\CultAccess'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $pluginDir 'lang') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $pluginDir 'sounds') -Force | Out-Null

Copy-Item $dll -Destination $pluginDir -Force

# Not in the repository: it is third-party LGPL 2.1 binary and rides in the release assets
# instead, so a fresh clone will not have it. Say so plainly rather than failing on a missing
# path, because the file is deliberately absent and that is not obvious from the error.
$nvda = Join-Path $root 'lib\x64\nvdaControllerClient64.dll'
if (-not (Test-Path $nvda)) {
    Write-Host "Missing $nvda."
    Write-Host 'It is deliberately not committed. Take it from a previous release asset or'
    Write-Host 'from the Tolk distribution and place it at lib\x64\. See THIRD_PARTY_NOTICES.md.'
    exit 1
}
Copy-Item $nvda -Destination $pluginDir -Force
Copy-Item (Join-Path $root 'lib\LICENSE-NVDA.txt') -Destination $pluginDir -Force
Copy-Item (Join-Path $root 'lang\*.txt') -Destination (Join-Path $pluginDir 'lang') -Force

# The sound folder ships with its instructions and no audio: every cue is generated in code,
# and a file dropped here overrides one. No third-party audio, so nothing to licence.
Copy-Item (Join-Path $root 'sounds\*.txt') -Destination (Join-Path $pluginDir 'sounds') -Force

# LICENSE and THIRD_PARTY_NOTICES.md are not optional: the notice is the licence
# obligation that travels with the bundled NVDA client. Both are tracked, so this should
# never fire — but the old Test-Path guard silently skipped whatever was missing, which
# meant any working copy lacking the notice could build a zip that shipped an LGPL binary
# without it and say nothing. A licence obligation should fail loudly, not quietly.
foreach ($doc in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    $path = Join-Path $root $doc
    if (-not (Test-Path $path)) {
        Write-Host "Missing $doc, which must ship with the package."
        Write-Host 'It is not tracked in the repository. Carry it over from a working copy'
        Write-Host 'before packaging; the NVDA client cannot be redistributed without it.'
        exit 1
    }
    Copy-Item $path -Destination $stage -Force
}

foreach ($doc in @('CHANGELOG.md', 'README.md')) {
    $path = Join-Path $root $doc
    if (Test-Path $path) { Copy-Item $path -Destination $stage -Force }
    else { Write-Host "Note: $doc not found, packaging without it." }
}

# Only the launcher and the two text files sit at the top level, so there is exactly one
# thing a tester can click. The supporting scripts go one level down.
Copy-Item (Join-Path $root 'packaging\Install CultAccess.cmd') -Destination $stage -Force

$installerDir = Join-Path $stage 'installer'
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
Copy-Item (Join-Path $root 'packaging\installer\*.ps1') -Destination $installerDir -Force
Copy-Item (Join-Path $root 'packaging\INSTALL.txt') -Destination $stage -Force
Copy-Item (Join-Path $root 'packaging\TESTING.txt') -Destination $stage -Force

$zip = Join-Path $root "dist\CultAccess-tester-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1KB)
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Built $zip"
Write-Host "  $size KB, SHA-256 $hash"
Write-Host "  plugin DLL SHA-256 $((Get-FileHash $dll -Algorithm SHA256).Hash)"
