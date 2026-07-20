<#
.SYNOPSIS
    Packs and publishes the SsalKit.DependencyInjection NuGet package.

.DESCRIPTION
    Invokes scripts\pack.ps1 to produce a .nupkg for the requested version, then pushes it
    to a NuGet feed (nuget.org by default) using `dotnet nuget push --skip-duplicate`.

    The script stops immediately on the first failing step and propagates a non-zero exit
    code.

.PARAMETER Version
    The package version to build and publish. Required.

.PARAMETER ApiKey
    The NuGet API key used to authenticate the push. If omitted, falls back to the
    NUGET_API_KEY environment variable. If neither is provided, the script errors out
    before attempting any work.

.PARAMETER Source
    The NuGet feed to push to. Defaults to the official nuget.org v3 feed.

.EXAMPLE
    .\scripts\publish.ps1 -Version 1.2.3 -ApiKey $env:NUGET_API_KEY

.EXAMPLE
    $env:NUGET_API_KEY = 'xxxxxxxx'
    .\scripts\publish.ps1 -Version 1.2.3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ApiKey,

    [string]$Source = 'https://api.nuget.org/v3/index.json'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = $env:NUGET_API_KEY
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Error 'No API key provided. Pass -ApiKey or set the NUGET_API_KEY environment variable.'
    exit 1
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$PackScript = Join-Path $PSScriptRoot 'pack.ps1'
$ArtifactsPath = Join-Path $RepoRoot 'artifacts'
$PackagePath = Join-Path $ArtifactsPath "SsalKit.DependencyInjection.$Version.nupkg"

Write-Host "SsalKit.DependencyInjection publish" -ForegroundColor Green
Write-Host "  Repo root : $RepoRoot"
Write-Host "  Version   : $Version"
Write-Host "  Source    : $Source"
Write-Host ""

& $PackScript -Version $Version -Configuration 'Release'

if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack step failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

if (-not (Test-Path $PackagePath)) {
    Write-Error "Expected package not found at '$PackagePath'."
    exit 1
}

Write-Host "==> Push" -ForegroundColor Cyan
Write-Host "    dotnet nuget push $PackagePath --source $Source --skip-duplicate" -ForegroundColor DarkGray

& dotnet nuget push $PackagePath --api-key $ApiKey --source $Source --skip-duplicate

if ($LASTEXITCODE -ne 0) {
    Write-Error "Push step failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Publish succeeded: $PackagePath -> $Source" -ForegroundColor Green
