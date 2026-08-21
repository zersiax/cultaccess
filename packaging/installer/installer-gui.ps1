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
$intro.Text = 'This installs the CultAccess screen reader mod for Cult of the Lamb. It will download BepInEx automatically if it is not already installed.'
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

$install.Add_Click({
    $install.Enabled = $false
    $browse.Enabled = $false
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
