<#
.SYNOPSIS
    Packs and publishes every shippable SsalKit NuGet package.

.DESCRIPTION
    Local equivalent of the release CI pipeline (.github\workflows\release.yml). Invokes
    scripts\pack.ps1 to produce a .nupkg per package for the requested version, then pushes
    each one to a NuGet feed (nuget.org by default) using `dotnet nuget push --skip-duplicate`.

    The package list is owned by pack.ps1; this script pushes whatever pack.ps1 declares.

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

Write-Host "SsalKit publish" -ForegroundColor Green
Write-Host "  Repo root : $RepoRoot"
Write-Host "  Version   : $Version"
Write-Host "  Source    : $Source"
Write-Host ""

& $PackScript -Version $Version -Configuration 'Release'

if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack step failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Push everything pack.ps1 just produced for this version, the way release.yml pushes
# artifacts/*.nupkg -- filtering on the version keeps stale artifacts out of the push.
$PackagePaths = @(Get-ChildItem -Path $ArtifactsPath -Filter "*.$Version.nupkg" -File | Sort-Object Name)

if ($PackagePaths.Count -eq 0) {
    Write-Error "No packages for version '$Version' found under '$ArtifactsPath'."
    exit 1
}

foreach ($Package in $PackagePaths) {
    Write-Host "==> Push $($Package.Name)" -ForegroundColor Cyan
    Write-Host "    dotnet nuget push $($Package.FullName) --source $Source --skip-duplicate" -ForegroundColor DarkGray

    & dotnet nuget push $Package.FullName --api-key $ApiKey --source $Source --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Push step failed for '$($Package.Name)' with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "Publish succeeded: $($PackagePaths.Count) package(s) -> $Source" -ForegroundColor Green
foreach ($Package in $PackagePaths) {
    Write-Host "  $($Package.Name)" -ForegroundColor Green
}
