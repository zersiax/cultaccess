<#
.SYNOPSIS
    Install CultAccess from the command line.

.DESCRIPTION
    The same installation as the double-click installer, for anyone who prefers a shell or
    needs to script it. Most testers should use "Install CultAccess.cmd" instead.

    BepInEx is downloaded automatically from its own distribution if it is not present.

.PARAMETER GameDir
    Only needed if the game is not found automatically.

.PARAMETER BepInExZip
    Install BepInEx from a zip you already have instead of downloading it.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -GameDir "D:\Steam\steamapps\common\Cult of the Lamb"
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [string] $BepInExZip
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
. (Join-Path $here 'install-core.ps1')

try {
    if (-not $GameDir) { $GameDir = Find-CultOfTheLamb }

    if (-not (Test-GameFolder $GameDir)) {
        Write-Host 'Could not find Cult of the Lamb.'
        Write-Host 'Run again with the folder, for example:'
        Write-Host '  .\install.ps1 -GameDir "D:\Steam\steamapps\common\Cult of the Lamb"'
        exit 1
    }

    Write-Host "Game folder: $GameDir"

    if (Test-GameRunning) {
        Write-Host 'Cult of the Lamb is running. Close it completely, then run this again.'
        exit 1
    }

    if (Test-BepInExCorrect $GameDir) {
        Write-Host 'BepInEx is already installed and correct.'
    }
    elseif (Test-BepInExInstalled $GameDir) {
        Write-Host 'BepInEx is present but is the wrong or an incomplete build; replacing it.'
        Install-BepInEx -GameDir $GameDir -FromZip $BepInExZip
    }
    else { Install-BepInEx -GameDir $GameDir -FromZip $BepInExZip }

    $packageRoot = Resolve-PackageRoot $here
    if (-not $packageRoot) {
        throw 'Could not find the plugins folder. Extract the whole zip and keep its folders together.'
    }

    $target = Install-CultAccess -GameDir $GameDir -PackageDir $packageRoot

    Write-Host ''
    Write-Host "CultAccess installed at $target"
    Write-Host 'Start the game; you should hear "Cult Access loaded" within a few seconds.'
}
catch {
    Write-Host ''
    Write-Host ('Install failed: ' + (Get-FriendlyInstallError $_.Exception))
    exit 1
}
