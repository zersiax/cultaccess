<#
.SYNOPSIS
    Build CultAccess in Release, without deploying.

.DESCRIPTION
    The project's own MSBuild target copies the DLL into the game folder after a build, but
    that folder lives under Program Files and the copy is denied when the build runs
    sandboxed. Suppressing it here gives a conclusive pass or fail: a build that "failed"
    only on the deploy step is the single most misleading output in this project.

    Deploy separately with scripts\deploy.ps1.

    Output is deliberately terse. It gets read aloud.

.PARAMETER GameDir
    Override the game location if it is not at the default Steam path.

.PARAMETER SkipTests
    Skip the offline pure-test harness.
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CultAccess\CultAccess.csproj'
$tests = Join-Path $root 'tests\CultAccess.PureTests\CultAccess.PureTests.csproj'

if (-not $SkipTests) {
    Write-Host 'Running pure tests...'
    dotnet run --project $tests -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Pure tests FAILED. Stopping before build.'
        exit 1
    }
    Write-Host 'Pure tests passed.'
}

$buildArgs = @('build', $project, '-c', 'Release', '-p:DeployOnBuild=false', '--verbosity', 'quiet')
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }

Write-Host 'Building Release...'
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Build FAILED.'
    exit 1
}

$dll = Join-Path $root 'src\CultAccess\bin\Release\netstandard2.1\CultAccess.dll'
if (-not (Test-Path $dll)) {
    Write-Host "Build reported success but $dll is missing."
    exit 1
}

$info = Get-Item $dll
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash
Write-Host "Build OK. $($info.Length) bytes."
Write-Host "SHA-256 $hash"
Write-Host 'Next: scripts\deploy.ps1'
