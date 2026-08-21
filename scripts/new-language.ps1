<#
.SYNOPSIS
    Create a translation template for a new language.

.DESCRIPTION
    Emits lang\<code>.txt with every key in the catalogue, each preceded by the English
    source as a comment so a translator has the original and the target side by side.
    Values start as the English text: replace them, and delete any line you have not
    translated so it falls back cleanly rather than shipping untranslated text that looks
    translated.

    The mod reports at startup how many lines a language actually translates, and any
    key you leave out is counted as falling back.

.PARAMETER Code
    Two-letter language code matching the game's own setting, for example fr or de.

.PARAMETER Status
    Provenance recorded in the file and read back at startup. Use machine-draft for an
    untrusted first pass, human-reviewed once a native speaker has been through it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Code,
    [string] $Status = 'machine-draft'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'lang\en.txt'
$target = Join-Path $root "lang\$Code.txt"

if (-not (Test-Path $source)) {
    Write-Host "Missing $source; nothing to template from."
    exit 1
}

if (Test-Path $target) {
    Write-Host "$target already exists. Delete it first if you mean to start over."
    exit 1
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add("# CultAccess string catalogue - $Code")
$out.Add('#')
$out.Add('# Each entry shows the English source as a comment, then the line to translate.')
$out.Add('# Keep the {0}, {1} placeholders; reorder them freely to suit this language.')
$out.Add('# Delete any line you have not translated - it falls back to English, which is')
$out.Add('# better than shipping English that looks like a translation.')
$out.Add('#')
$out.Add('# Item, place and ability names are not here. They come from the game itself.')
$out.Add('')
$out.Add("_meta.status = $Status")
$out.Add("_meta.translator = ")
$out.Add('')

foreach ($line in Get-Content $source -Encoding UTF8) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }

    $split = $trimmed.IndexOf('=')
    if ($split -le 0) { continue }

    $key = $trimmed.Substring(0, $split).Trim()
    $value = $trimmed.Substring($split + 1).Trim()
    if ($key.StartsWith('_meta.')) { continue }

    $out.Add("# EN: $value")
    $out.Add("$key = $value")
    $out.Add('')
}

[System.IO.File]::WriteAllLines($target, $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $target with $((Get-Content $source | Where-Object { $_ -match '^[a-z].*=' }).Count) keys."
Write-Host 'Translate the values, then run scripts\deploy.ps1 to install it.'
