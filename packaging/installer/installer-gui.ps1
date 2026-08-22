<#
    Double-click installer for CultAccess.

    Built for screen reader users first. That drives every choice here: plain WinForms
    controls with no custom drawing, an explicit AccessibleName on anything whose visible
    text is not self-explanatory, a sensible tab order, Enter bound to the Install button,
    and — most importantly — progress reported into a read-only multi-line text box the
    reader can arrow through afterwards rather than a bare progress bar that says nothing.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
. (Join-Path $here 'install-core.ps1')

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Install CultAccess'
$form.Size = New-Object System.Drawing.Size(620, 460)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false

$intro = New-Object System.Windows.Forms.Label
$intro.Text = 'This installs the CultAccess screen reader mod for Cult of the Lamb. It will download BepInEx automatically if it is not already installed. If that download is blocked, use the second button to fetch it yourself.'
$intro.Location = New-Object System.Drawing.Point(12, 12)
$intro.Size = New-Object System.Drawing.Size(580, 40)
$form.Controls.Add($intro)

$pathLabel = New-Object System.Windows.Forms.Label
$pathLabel.Text = '&Game folder:'
$pathLabel.Location = New-Object System.Drawing.Point(12, 62)
$pathLabel.Size = New-Object System.Drawing.Size(100, 20)
$form.Controls.Add($pathLabel)

$pathBox = New-Object System.Windows.Forms.TextBox
$pathBox.Location = New-Object System.Drawing.Point(12, 84)
$pathBox.Size = New-Object System.Drawing.Size(470, 22)
$pathBox.AccessibleName = 'Game folder'
$pathBox.AccessibleDescription = 'Folder containing Cult Of The Lamb dot exe'
$form.Controls.Add($pathBox)

$browse = New-Object System.Windows.Forms.Button
$browse.Text = '&Browse...'
$browse.Location = New-Object System.Drawing.Point(492, 83)
$browse.Size = New-Object System.Drawing.Size(100, 25)
$browse.AccessibleName = 'Browse for the game folder'
$form.Controls.Add($browse)

$install = New-Object System.Windows.Forms.Button
$install.Text = '&Install'
$install.Location = New-Object System.Drawing.Point(12, 118)
$install.Size = New-Object System.Drawing.Size(140, 32)
$install.AccessibleName = 'Install CultAccess'
$form.Controls.Add($install)
$form.AcceptButton = $install

# Separate from Install rather than a fallback inside it. A tester whose antivirus blocks the
# download needs to know the manual route exists *before* they hit the failure, not after, and
# a button they can tab to is discoverable in a way a log line is not.
$manual = New-Object System.Windows.Forms.Button
$manual.Text = 'Use a BepInEx zip I &downloaded myself...'
$manual.Location = New-Object System.Drawing.Point(162, 118)
$manual.Size = New-Object System.Drawing.Size(300, 32)
$manual.AccessibleName = 'Use a BepInEx zip I downloaded myself'
$manual.AccessibleDescription = 'Shows the download link, then lets you pick the zip you saved'
$form.Controls.Add($manual)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = 'Pro&gress:'
$statusLabel.Location = New-Object System.Drawing.Point(12, 160)
$statusLabel.Size = New-Object System.Drawing.Size(200, 20)
$form.Controls.Add($statusLabel)

# Read-only rather than disabled: a disabled box is skipped by screen readers, and the whole
# point of this box is that the user can review what happened after the fact.
$status = New-Object System.Windows.Forms.TextBox
$status.Multiline = $true
$status.ReadOnly = $true
$status.ScrollBars = 'Vertical'
$status.Location = New-Object System.Drawing.Point(12, 182)
$status.Size = New-Object System.Drawing.Size(580, 200)
$status.AccessibleName = 'Progress messages'
$form.Controls.Add($status)

$close = New-Object System.Windows.Forms.Button
$close.Text = '&Close'
$close.Location = New-Object System.Drawing.Point(492, 390)
$close.Size = New-Object System.Drawing.Size(100, 28)
$close.AccessibleName = 'Close the installer'
$form.Controls.Add($close)
$form.CancelButton = $close

function Add-Status([string] $message) {
    $status.AppendText($message + [Environment]::NewLine)
    [System.Windows.Forms.Application]::DoEvents()
}

Set-InstallReporter { param($m) Add-Status $m }

$browse.Add_Click({
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select the Cult of the Lamb folder'
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $pathBox.Text = $dialog.SelectedPath
    }
})

$close.Add_Click({ $form.Close() })

<#
    Guides someone whose automatic download was blocked - by antivirus, a proxy, or a
    corporate network - through fetching the zip by hand.

    The link is resolved live rather than written into this file. A hardcoded link is how the
    wrong one reached testers once already, and it would be stale by the next BepInEx release
    regardless. If the lookup fails, the package page is offered instead, which is a page a
    human can navigate rather than a guess at a file name.

    The URL is put on the clipboard and read into the progress box as well as opened in a
    browser, because a browser that opens behind the installer is invisible to a screen reader
    user, and because pasting it into a different browser is often the actual fix.
#>
$manual.Add_Click({
    $game = $pathBox.Text.Trim()
    if (-not (Test-GameFolder $game)) {
        Add-Status 'Set the game folder first, with Browse, then try this again.'
        $status.Focus() | Out-Null
        return
    }

    $manual.Enabled = $false
    $install.Enabled = $false
    try {
        Add-Status ''
        Add-Status 'Looking up the current BepInEx download link...'
        $download = Get-BepInExDownload

        if ($download) {
            $link = $download.Url
            $what = 'BepInEx ' + $download.Version
        }
        else {
            $link = 'https://thunderstore.io/c/cult-of-the-lamb/p/BepInEx/BepInExPack_CultOfTheLamb/'
            $what = 'the BepInEx pack page'
        }

        try { Set-Clipboard -Value $link } catch { }
        Add-Status ('Link to ' + $what + ', also copied to your clipboard:')
        Add-Status $link
        Add-Status ''
        Add-Status 'Opening it in your browser. If your antivirus blocks the download, allow it'
        Add-Status 'or try another browser, then come back here.'
        Add-Status ''
        Add-Status 'If the browser reports a certificate or SSL error on this link, that is your'
        Add-Status 'security software inspecting encrypted traffic rather than a problem with the'
        Add-Status 'file. Turning off its web or HTTPS scanning for a moment, or using a browser'
        Add-Status 'it does not hook, will get you the download.'
        try { Start-Process $link } catch { Add-Status 'Could not open a browser. Paste the link above instead.' }

        $prompt = [System.Windows.Forms.MessageBox]::Show(
            'The download link is on your clipboard and open in your browser.' + [Environment]::NewLine + [Environment]::NewLine +
            'Download the zip, then choose Yes to pick the file you saved.' + [Environment]::NewLine +
            'Choose No to stop here.',
            'Download BepInEx',
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Information)

        if ($prompt -ne [System.Windows.Forms.DialogResult]::Yes) {
            Add-Status 'Stopped. Press this button again when the download has finished.'
            $status.Focus() | Out-Null
            return
        }

        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title = 'Select the BepInEx zip you downloaded'
        $dialog.Filter = 'Zip archives (*.zip)|*.zip|All files (*.*)|*.*'
        $downloads = Join-Path $env:USERPROFILE 'Downloads'
        if (Test-Path $downloads) { $dialog.InitialDirectory = $downloads }

        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            Add-Status 'No file chosen. Press this button again when you are ready.'
            $status.Focus() | Out-Null
            return
        }

        if (Test-GameRunning) {
            Add-Status 'Cult of the Lamb is running. Close the game completely, then try again.'
            $status.Focus() | Out-Null
            return
        }

        Add-Status ''
        Install-BepInEx -GameDir $game -FromZip $dialog.FileName

        $packageRoot = Resolve-PackageRoot $here
        if (-not $packageRoot) {
            Add-Status 'BepInEx is in. Could not find the plugins folder, though: extract the whole'
            Add-Status 'zip and keep its folders together, then press Install.'
            $status.Focus() | Out-Null
            return
        }

        Install-CultAccess -GameDir $game -PackageDir $packageRoot | Out-Null

        Add-Status ''
        Add-Status 'Done. CultAccess is installed.'
        Add-Status 'Start the game; you should hear "Cult Access loaded" within a few seconds.'
        Add-Status 'Press F1 in game for the full key list, and F7 to mark the log when something is not announced.'
        $status.Focus() | Out-Null
    }
    catch {
        Add-Status ''
        Add-Status ('Install failed: ' + (Get-FriendlyInstallError $_.Exception))
        $status.Focus() | Out-Null
    }
    finally {
        $manual.Enabled = $true
        $install.Enabled = $true
    }
})

$install.Add_Click({
    $install.Enabled = $false
    $browse.Enabled = $false
    $manual.Enabled = $false
    try {
        $game = $pathBox.Text.Trim()
        if (-not (Test-GameFolder $game)) {
            Add-Status 'That folder does not contain Cult Of The Lamb.exe. Use Browse to pick the game folder.'
            return
        }

        Add-Status "Game folder: $game"

        if (Test-GameRunning) {
            Add-Status 'Cult of the Lamb is running. Close the game completely, then press Install again.'
            $status.Focus() | Out-Null
            return
        }

        if (Test-BepInExCorrect $game) {
            Add-Status 'BepInEx is already installed and correct.'
        }
        elseif (Test-BepInExInstalled $game) {
            Add-Status 'BepInEx is present but is the wrong or an incomplete build; replacing it.'
            Install-BepInEx -GameDir $game
        }
        else {
            Install-BepInEx -GameDir $game
        }

        $packageRoot = Resolve-PackageRoot $here
        if (-not $packageRoot) {
            Add-Status 'Could not find the plugins folder. Extract the whole zip and keep its folders together, then run this again.'
            return
        }

        $target = Install-CultAccess -GameDir $game -PackageDir $packageRoot

        Add-Status ''
        Add-Status 'Done. CultAccess is installed.'
        Add-Status 'Start the game; you should hear "Cult Access loaded" within a few seconds.'
        Add-Status 'Press F1 in game for the full key list, and F7 to mark the log when something is not announced.'

        # Move focus so the reader lands on the messages rather than a dead button.
        $status.Focus() | Out-Null
    }
    catch {
        Add-Status ''
        Add-Status ('Install failed: ' + (Get-FriendlyInstallError $_.Exception))
        $status.Focus() | Out-Null
    }
    finally {
        $install.Enabled = $true
        $browse.Enabled = $true
        $manual.Enabled = $true
    }
})

$form.Add_Shown({
    $detected = Find-CultOfTheLamb
    if ($detected) {
        $pathBox.Text = $detected
        Add-Status "Found the game at: $detected"
        Add-Status 'Press Install to continue.'
    }
    else {
        Add-Status 'Could not find Cult of the Lamb automatically. Use Browse to pick the game folder.'
    }
    $install.Focus() | Out-Null
})

[void]$form.ShowDialog()
