<#
    Shared installation logic for CultAccess.

    Dot-sourced by both the double-click GUI and install.ps1, so the two front ends cannot
    drift apart. Every function reports progress through a script-scoped callback rather than
    writing to the host, which is what lets the GUI show the same messages in a text box a
    screen reader can review.
#>

$script:ReportProgress = { param($message) Write-Host $message }

function Set-InstallReporter([scriptblock] $Reporter) {
    if ($Reporter) { $script:ReportProgress = $Reporter }
}

function Report([string] $message) { & $script:ReportProgress $message }

<#
    Find the package root: the folder containing plugins\CultAccess.

    Walks upward from wherever this script happens to live, so moving the scripts into a
    subfolder, or back out of one, does not break the install. Anchored on the plugin payload
    itself rather than on a fixed number of parent hops.
#>
function Resolve-PackageRoot([string] $StartDirectory) {
    $dir = $StartDirectory
    for ($i = 0; $i -lt 4 -and $dir; $i++) {
        if (Test-Path (Join-Path $dir 'plugins\CultAccess\CultAccess.dll')) { return $dir }
        $dir = Split-Path -Parent $dir
    }
    return $null
}

<#
    Locate the game. Steam libraries can live on any drive, so read Steam's own index rather
    than guessing at a couple of paths.
#>
function Find-CultOfTheLamb {
    $candidates = @(
        'C:\Program Files (x86)\Steam\steamapps\common\Cult of the Lamb',
        'C:\Program Files\Steam\steamapps\common\Cult of the Lamb'
    )

    foreach ($vdf in @(
        'C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf',
        'C:\Program Files\Steam\steamapps\libraryfolders.vdf')) {

        if (-not (Test-Path $vdf)) { continue }
        foreach ($line in Get-Content $vdf) {
            if ($line -match '"path"\s+"(.+?)"') {
                $candidates += (Join-Path ($matches[1] -replace '\\', '\') 'steamapps\common\Cult of the Lamb')
            }
        }
    }

    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c 'Cult Of The Lamb.exe')) { return $c }
    }
    return $null
}

function Test-GameFolder([string] $GameDir) {
    return $GameDir -and (Test-Path (Join-Path $GameDir 'Cult Of The Lamb.exe'))
}

<#
    Is a *correct and complete* BepInEx present?

    Mere presence is not enough. There are two packs on Thunderstore with nearly the same
    name, and installing the wrong one produces a mod that loads, announces itself, and is
    then completely mute:

      - BepInEx/BepInExPack               hooks UnityEngine.CoreModule Application..cctor,
                                          which runs before Unity's player loop exists. A
                                          plugin loaded there gets Awake but is never
                                          registered for Update.
      - BepInEx/BepInExPack_CultOfTheLamb hooks Assembly-CSharp TwitchManager..cctor, which
                                          runs once everything is initialised. This is the
                                          one this game needs.

    A half-installed loader is just as bad: the doorstop files live in the game root while
    the runtime lives in BepInEx\core, and replacing one without the other leaves a version
    mismatch that silently loads nothing. So check all three parts.
#>
function Test-BepInExCorrect([string] $GameDir) {
    if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.Preloader.dll'))) { return $false }
    if (-not (Test-Path (Join-Path $GameDir 'winhttp.dll'))) { return $false }
    if (-not (Test-Path (Join-Path $GameDir 'doorstop_config.ini'))) { return $false }

    $config = Join-Path $GameDir 'BepInEx\config\BepInEx.cfg'
    if (-not (Test-Path $config)) { return $false }

    # The decisive marker: the game-specific pack targets Assembly-CSharp.
    foreach ($line in Get-Content $config) {
        if ($line -match '^\s*Assembly\s*=\s*(.+?)\s*$') {
            return $matches[1] -ieq 'Assembly-CSharp.dll'
        }
    }
    return $false
}

<#
    Is the game running? Windows keeps winhttp.dll and the BepInEx core mapped while it is,
    so replacing them fails partway through. Check before touching anything rather than
    discovering it halfway through an install.
#>
function Test-GameRunning {
    try {
        return @(Get-Process -Name 'Cult Of The Lamb' -ErrorAction SilentlyContinue).Count -gt 0
    }
    catch { return $false }
}

function Test-BepInExInstalled([string] $GameDir) {
    return Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll')
}

<#
    Fetch the current BepInEx pack from its own distribution and install it.

    Deliberately downloaded rather than bundled: BepInEx is a separate LGPL project and this
    package does not redistribute a mod loader. Downloading at install time gives the tester a
    one-step experience without us mirroring somebody else's software.

    The version is resolved from the Thunderstore API rather than hardcoded, so this keeps
    working when BepInEx updates.
#>
<#
.SYNOPSIS
    The current direct download URL for the BepInEx pack, and its version.

.DESCRIPTION
    Resolved live from Thunderstore rather than written down. A hardcoded link is how the
    wrong one reached testers once already, and a link that is correct today is only correct
    until the next release.

    Returns $null when the lookup fails, so a caller can offer the package page instead of a
    guess. Never throws: this exists to help someone whose download was blocked, and failing
    loudly at them a second time helps nobody.
#>
function Get-BepInExDownload {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $api = 'https://thunderstore.io/api/experimental/package/BepInEx/BepInExPack_CultOfTheLamb/'
        $info = Invoke-RestMethod -Uri $api -TimeoutSec 30
        if (-not $info.latest.download_url) { return $null }

        return [pscustomobject]@{
            Url     = $info.latest.download_url
            Version = $info.latest.version_number
        }
    }
    catch {
        return $null
    }
}

function Install-BepInEx {
    param([Parameter(Mandatory = $true)] [string] $GameDir, [string] $FromZip)

    $zip = $FromZip
    $temp = Join-Path $env:TEMP ('cultaccess-bepinex-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp -Force | Out-Null

    try {
        if (-not $zip) {
            Report 'Looking up the current BepInEx version...'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

            # The Cult of the Lamb pack, NOT the generic BepInExPack. They differ in one
            # decisive way: the generic pack hooks UnityEngine.CoreModule's
            # Application..cctor, which runs the chainloader before Unity's player loop
            # exists. A plugin loaded there gets its Awake called but is never registered for
            # Update, so the mod announces itself and is then completely mute. The game
            # specific pack hooks Assembly-CSharp's TwitchManager..cctor, which runs at a
            # point where everything is properly initialised.
            $api = 'https://thunderstore.io/api/experimental/package/BepInEx/BepInExPack_CultOfTheLamb/'
            $info = Invoke-RestMethod -Uri $api -TimeoutSec 30
            $url = $info.latest.download_url
            Report ("Downloading BepInEx " + $info.latest.version_number + "...")

            $zip = Join-Path $temp 'bepinex.zip'
            Invoke-WebRequest -Uri $url -OutFile $zip -TimeoutSec 180 -UseBasicParsing
        }
        else {
            Report 'Using the BepInEx zip you supplied...'
        }

        if (-not (Test-Path $zip)) { throw 'The BepInEx download did not produce a file.' }

        Report 'Extracting BepInEx...'
        $extract = Join-Path $temp 'extract'
        New-Item -ItemType Directory -Path $extract -Force | Out-Null
        Expand-Archive -Path $zip -DestinationPath $extract -Force

        # The pack nests everything under BepInExPack_CultOfTheLamb\. Copy from whichever
        # level actually holds winhttp.dll, so both that layout and a flat one work.
        $marker = Get-ChildItem $extract -Recurse -Filter 'winhttp.dll' -ErrorAction SilentlyContinue |
                  Select-Object -First 1
        if (-not $marker) { throw 'That zip does not look like a BepInEx pack: no winhttp.dll inside.' }
        $source = $marker.Directory.FullName

        if (Test-GameRunning) {
            throw 'Cult of the Lamb is running. Close it completely before installing, so the loader files can be replaced.'
        }

        # Move the old runtime aside rather than deleting it, so a failure part way through
        # can put things back. Deleting first and copying second leaves no loader at all if
        # the copy fails - which is exactly what happened when this was written.
        $backup = Join-Path $temp 'previous'
        New-Item -ItemType Directory -Path $backup -Force | Out-Null

        $oldCore = Join-Path $GameDir 'BepInEx\core'
        $movedCore = $false
        if (Test-Path $oldCore) {
            Move-Item $oldCore (Join-Path $backup 'core') -Force
            $movedCore = $true
        }

        $rootFiles = @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')
        $movedRoot = @()
        foreach ($stale in $rootFiles) {
            $path = Join-Path $GameDir $stale
            if (Test-Path $path) {
                Move-Item $path (Join-Path $backup $stale) -Force
                $movedRoot += $stale
            }
        }

        try {
            # BepInEx\config and BepInEx\plugins are deliberately left in place: the config
            # is overwritten by the pack's own, and mods already installed are preserved.
            Copy-Item (Join-Path $source '*') -Destination $GameDir -Recurse -Force

            if (-not (Test-BepInExCorrect $GameDir)) {
                throw 'BepInEx installed but does not look correct afterwards. The pack may have changed layout.'
            }
        }
        catch {
            Report 'Install failed; putting the previous BepInEx back...'
            if ($movedCore -and -not (Test-Path $oldCore)) {
                Move-Item (Join-Path $backup 'core') $oldCore -Force -ErrorAction SilentlyContinue
            }
            foreach ($stale in $movedRoot) {
                $path = Join-Path $GameDir $stale
                if (-not (Test-Path $path)) {
                    Move-Item (Join-Path $backup $stale) $path -Force -ErrorAction SilentlyContinue
                }
            }
            throw
        }

        Report 'BepInEx installed and verified.'
    }
    finally {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

<#
    Copy the mod in and prove the copy matches. Existing settings and any language files the
    user added are left alone.
#>
function Install-CultAccess {
    param([Parameter(Mandatory = $true)] [string] $GameDir,
          [Parameter(Mandatory = $true)] [string] $PackageDir)

    $source = Join-Path $PackageDir 'plugins\CultAccess'
    if (-not (Test-Path (Join-Path $source 'CultAccess.dll'))) {
        throw "This package looks incomplete: no CultAccess.dll under $source"
    }

    $target = Join-Path $GameDir 'BepInEx\plugins\CultAccess'
    New-Item -ItemType Directory -Path (Join-Path $target 'lang') -Force | Out-Null

    Report 'Copying CultAccess...'
    Copy-Item (Join-Path $source '*.dll') -Destination $target -Force -ErrorAction Stop
    Copy-Item (Join-Path $source '*.txt') -Destination $target -Force
    Copy-Item (Join-Path $source 'lang\*.txt') -Destination (Join-Path $target 'lang') -Force

    $expected = (Get-FileHash (Join-Path $source 'CultAccess.dll') -Algorithm SHA256).Hash
    $actual = (Get-FileHash (Join-Path $target 'CultAccess.dll') -Algorithm SHA256).Hash
    if ($expected -ne $actual) {
        throw 'Verification failed: the copied file does not match the package.'
    }

    Report 'Installed and verified.'
    return $target
}

<#
    Turn the two failures that actually happen into plain language. The raw exceptions say
    nothing useful, and a tester who cannot see the screen should not have to interpret them.
#>
function Get-FriendlyInstallError([System.Exception] $Exception) {
    if ($Exception -is [System.IO.IOException] -and $Exception.Message -match 'being used|user-mapped') {
        return 'Cult of the Lamb appears to be running. Close the game completely, then try again.'
    }
    if ($Exception -is [System.UnauthorizedAccessException]) {
        return 'Windows refused permission to write into the game folder. Close this and run the installer again, choosing Yes when Windows asks for permission.'
    }
    if ($Exception.Message -match 'Unable to connect|remote name|timed out|SSL|TLS') {
        return 'Could not reach the BepInEx download. Check your internet connection and try again.'
    }
    return $Exception.Message
}
