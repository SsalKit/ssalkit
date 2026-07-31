<#
.SYNOPSIS
    Cleans, builds, tests, and packs every shippable SsalKit NuGet package.

.DESCRIPTION
    Local equivalent of the release CI pipeline (.github\workflows\release.yml). Runs
    `dotnet clean`, `dotnet build`, `dotnet test`, and finally `dotnet pack` for each
    shippable project, producing one .nupkg per package under <repo root>\artifacts.

    Keep the project list below in sync with the "Pack" step of release.yml.

    The script stops immediately on the first failing step (build error, test failure, etc.)
    and propagates a non-zero exit code.

.PARAMETER Version
    The package version to stamp onto the generated .nupkg. Defaults to "0.1.0".

.PARAMETER Configuration
    The build configuration to use. Defaults to "Release".

.EXAMPLE
    .\scripts\pack.ps1 -Version 1.2.3

.EXAMPLE
    .\scripts\pack.ps1 -Version 1.2.3 -Configuration Debug
#>
[CmdletBinding()]
param(
    [string]$Version = '0.1.0',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# Repo root is one level up from this script's directory.
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$SolutionPath = Join-Path $RepoRoot 'SsalKit.sln'
$ArtifactsPath = Join-Path $RepoRoot 'artifacts'

# Shippable packages, mirroring the "Pack" step of .github\workflows\release.yml.
$PackageNames = @(
    'SsalKit.DependencyInjection',
    'SsalKit.Randomness',
    'SsalKit.Generators.Toolkit',
    'SsalKit.Generators.Toolkit.Testing',
    'SsalKit.Guard',
    'SsalKit.Timekeeping',
    'SsalKit.StableHashing'
)

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    Write-Host "    dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Step '$Name' failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

Write-Host "SsalKit pack" -ForegroundColor Green
Write-Host "  Repo root     : $RepoRoot"
Write-Host "  Version       : $Version"
Write-Host "  Configuration : $Configuration"
Write-Host "  Packages      : $($PackageNames -join ', ')"
Write-Host ""

Invoke-Step -Name 'Clean' -Arguments @('clean', $SolutionPath, '--configuration', $Configuration)

Invoke-Step -Name 'Restore' -Arguments @('restore', $SolutionPath)

Invoke-Step -Name 'Build' -Arguments @('build', $SolutionPath, '--configuration', $Configuration, '--no-restore')

Invoke-Step -Name 'Test' -Arguments @('test', $SolutionPath, '--configuration', $Configuration, '--no-build')

foreach ($PackageName in $PackageNames) {
    $ProjectPath = Join-Path $RepoRoot "src\$PackageName"

    Invoke-Step -Name "Pack $PackageName" -Arguments @(
        'pack', $ProjectPath,
        '--configuration', $Configuration,
        "-p:Version=$Version",
        '-o', $ArtifactsPath
    )
}

Write-Host ""
Write-Host "Pack succeeded. Output:" -ForegroundColor Green
foreach ($PackageName in $PackageNames) {
    Write-Host "  $ArtifactsPath\$PackageName.$Version.nupkg" -ForegroundColor Green
}
