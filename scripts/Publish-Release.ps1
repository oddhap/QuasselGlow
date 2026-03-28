[CmdletBinding()]
param(
    [string]$Version,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64", "osx-x64", "osx-arm64"),

    [switch]$SkipValidation,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-VersionTag {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Version cannot be empty."
    }

    if ($Value.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "v" + $Value.TrimStart([char[]]@('v', 'V'))
    }

    return "v$Value"
}

function Get-CentralizedVersionTag {
    param([string]$RepoRoot)

    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        return $null
    }

    [xml]$props = Get-Content -LiteralPath $propsPath
    $versionPrefix = $props.Project.PropertyGroup.VersionPrefix | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionPrefix)) {
        return $null
    }

    return Normalize-VersionTag -Value $versionPrefix
}

function Invoke-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host ">" $FilePath ($Arguments -join " ")
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Ensure-ChildPath {
    param(
        [string]$RootPath,
        [string]$ChildPath
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + '\'
    $normalizedChild = [System.IO.Path]::GetFullPath($ChildPath)

    if (-not $normalizedChild.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the expected root path: $normalizedChild"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$centralizedVersionTag = Get-CentralizedVersionTag -RepoRoot $repoRoot
$versionTag = if ([string]::IsNullOrWhiteSpace($Version)) {
    if ($null -eq $centralizedVersionTag) {
        throw "Version was not provided and no VersionPrefix was found in Directory.Build.props."
    }

    $centralizedVersionTag
}
else {
    Normalize-VersionTag -Value $Version
}

if ($null -ne $centralizedVersionTag -and $versionTag -ne $centralizedVersionTag) {
    Write-Warning "Publish version $versionTag does not match centralized VersionPrefix $centralizedVersionTag from Directory.Build.props."
}

$solutionPath = Join-Path $repoRoot "Quassel.slnx"
$projectPath = Join-Path $repoRoot "src\QuasselGlow\QuasselGlow.csproj"
$releaseRoot = Join-Path $repoRoot ".artifacts\releases\$versionTag"

if (Test-Path $releaseRoot) {
    if (-not $Force) {
        throw "Release directory already exists: $releaseRoot. Re-run with -Force to replace it."
    }

    Ensure-ChildPath -RootPath (Join-Path $repoRoot ".artifacts\releases") -ChildPath $releaseRoot
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

if (-not $SkipValidation) {
    Invoke-External -FilePath "dotnet" -Arguments @("build", $solutionPath, "-c", $Configuration)
    Invoke-External -FilePath "dotnet" -Arguments @("test", (Join-Path $repoRoot "tests\Quassel.Client.Application.Tests\Quassel.Client.Application.Tests.csproj"), "-c", $Configuration, "--no-build")
    Invoke-External -FilePath "dotnet" -Arguments @("test", (Join-Path $repoRoot "tests\Quassel.Client.Protocol.Tests\Quassel.Client.Protocol.Tests.csproj"), "-c", $Configuration, "--no-build")
}

$releaseNotesPath = Join-Path $releaseRoot "RELEASE_NOTES.md"
if (-not (Test-Path $releaseNotesPath)) {
    @"
# QuasselGlow $versionTag

Release summary goes here.

## Highlights

- Add user-facing highlights here

## Downloads

- List generated archives here before publishing

## Validation

- dotnet build Quassel.slnx -c $Configuration
- dotnet test tests/Quassel.Client.Application.Tests/Quassel.Client.Application.Tests.csproj -c $Configuration --no-build
- dotnet test tests/Quassel.Client.Protocol.Tests/Quassel.Client.Protocol.Tests.csproj -c $Configuration --no-build
"@ | Set-Content -Path $releaseNotesPath -Encoding utf8
}

$generatedArchives = New-Object System.Collections.Generic.List[string]

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $publishDirectory = Join-Path $releaseRoot $runtimeIdentifier
    if (Test-Path $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    Invoke-External -FilePath "dotnet" -Arguments @(
        "publish",
        $projectPath,
        "-c", $Configuration,
        "-r", $runtimeIdentifier,
        "--self-contained", "true",
        "--output", $publishDirectory
    )

    $archivePath = Join-Path $releaseRoot "QuasselGlow-$versionTag-$runtimeIdentifier.zip"
    if (Test-Path $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal
    $generatedArchives.Add($archivePath) | Out-Null
}

$hashFilePath = Join-Path $releaseRoot "SHA256SUMS.txt"
$hashLines = foreach ($archivePath in $generatedArchives | Sort-Object) {
    $hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    "{0} *{1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path $archivePath -Leaf)
}
$hashLines | Set-Content -Path $hashFilePath -Encoding utf8

Write-Host ""
Write-Host "Release artifacts created in $releaseRoot"
Write-Host "Generated archives:"
$generatedArchives | Sort-Object | ForEach-Object { Write-Host " - " (Split-Path $_ -Leaf) }
Write-Host " - " (Split-Path $hashFilePath -Leaf)
Write-Host " - " (Split-Path $releaseNotesPath -Leaf)
