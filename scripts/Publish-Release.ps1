[CmdletBinding()]
param(
    [string]$Version,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"),

    [switch]$SkipValidation,

    [switch]$SkipMacDmg,

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

function Get-NormalizedDirectoryPath {
    param([string]$Path)

    return ([System.IO.Path]::GetFullPath($Path)).TrimEnd('\', '/')
}

function Ensure-ChildPath {
    param(
        [string]$RootPath,
        [string]$ChildPath
    )

    $normalizedRoot = Get-NormalizedDirectoryPath -Path $RootPath
    $normalizedChild = Get-NormalizedDirectoryPath -Path $ChildPath

    if ($normalizedChild -ne $normalizedRoot -and -not $normalizedChild.StartsWith("$normalizedRoot$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the expected root path: $normalizedChild"
    }
}

function Remove-DirectoryIfExists {
    param(
        [string]$RootPath,
        [string]$TargetPath
    )

    if (Test-Path $TargetPath) {
        Ensure-ChildPath -RootPath $RootPath -ChildPath $TargetPath
        Remove-Item -LiteralPath $TargetPath -Recurse -Force
    }
}

function Test-IsMacOSHost {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)
}

function Test-CommandAvailable {
    param([string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Publish-SingleFile {
    param(
        [string]$ProjectPath,
        [string]$Configuration,
        [string]$RuntimeIdentifier,
        [string]$OutputDirectory
    )

    Invoke-External -FilePath "dotnet" -Arguments @(
        "publish",
        $ProjectPath,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "--force",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "--output", $OutputDirectory
    )
}

function New-ZipArchiveFromPath {
    param(
        [string]$SourcePath,
        [string]$ArchivePath
    )

    if (Test-Path $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }

    $resolvedSourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
    $shouldUseDitto = (Test-IsMacOSHost) `
        -and (Test-CommandAvailable -Name "ditto") `
        -and [System.IO.Path]::GetExtension($resolvedSourcePath).Equals(".app", [System.StringComparison]::OrdinalIgnoreCase)

    if ($shouldUseDitto) {
        Invoke-External -FilePath "ditto" -Arguments @(
            "-c",
            "-k",
            "--sequesterRsrc",
            "--keepParent",
            $resolvedSourcePath,
            $ArchivePath
        )
        return
    }

    if ((-not $IsWindows) -and (Test-CommandAvailable -Name "zip")) {
        $sourceParent = Split-Path -Parent $resolvedSourcePath
        $sourceName = Split-Path -Leaf $resolvedSourcePath
        Push-Location $sourceParent
        try {
            Invoke-External -FilePath "zip" -Arguments @("-q", "-r", $ArchivePath, $sourceName)
        }
        finally {
            Pop-Location
        }
        return
    }

    Compress-Archive -Path $resolvedSourcePath -DestinationPath $ArchivePath -CompressionLevel Optimal
}

function New-ZipArchiveFromDirectoryContents {
    param(
        [string]$DirectoryPath,
        [string]$ArchivePath
    )

    if (Test-Path $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }

    if ((-not $IsWindows) -and (Test-CommandAvailable -Name "zip")) {
        Push-Location $DirectoryPath
        try {
            Invoke-External -FilePath "zip" -Arguments @("-q", "-r", $ArchivePath, ".")
        }
        finally {
            Pop-Location
        }
        return
    }

    Compress-Archive -Path (Join-Path $DirectoryPath "*") -DestinationPath $ArchivePath -CompressionLevel Optimal
}

function New-MacIcon {
    param(
        [string]$IconsDirectory,
        [string]$ResourcesDirectory,
        [string]$TemporaryDirectory,
        [string]$AppName
    )

    if (-not (Test-IsMacOSHost) -or -not (Test-CommandAvailable -Name "iconutil")) {
        return $null
    }

    $requiredSourceFiles = @(
        "icon_16.png",
        "icon_32.png",
        "icon_64.png",
        "icon_128.png",
        "icon_256.png",
        "icon_512.png"
    )

    foreach ($file in $requiredSourceFiles) {
        if (-not (Test-Path (Join-Path $IconsDirectory $file))) {
            return $null
        }
    }

    $iconSetDirectory = Join-Path $TemporaryDirectory "$AppName.iconset"
    New-Item -ItemType Directory -Path $iconSetDirectory -Force | Out-Null

    $mapping = @{
        "icon_16x16.png"      = "icon_16.png"
        "icon_16x16@2x.png"   = "icon_32.png"
        "icon_32x32.png"      = "icon_32.png"
        "icon_32x32@2x.png"   = "icon_64.png"
        "icon_128x128.png"    = "icon_128.png"
        "icon_128x128@2x.png" = "icon_256.png"
        "icon_256x256.png"    = "icon_256.png"
        "icon_256x256@2x.png" = "icon_512.png"
        "icon_512x512.png"    = "icon_512.png"
        "icon_512x512@2x.png" = "icon_512.png"
    }

    foreach ($entry in $mapping.GetEnumerator()) {
        Copy-Item -LiteralPath (Join-Path $IconsDirectory $entry.Value) -Destination (Join-Path $iconSetDirectory $entry.Key) -Force
    }

    $iconPath = Join-Path $ResourcesDirectory "$AppName.icns"
    Invoke-External -FilePath "iconutil" -Arguments @(
        "-c", "icns",
        $iconSetDirectory,
        "-o", $iconPath
    )

    return $iconPath
}

function New-MacAppBundle {
    param(
        [string]$PublishDirectory,
        [string]$BundleDirectory,
        [string]$AppName,
        [string]$BundleIdentifier,
        [string]$DisplayVersion,
        [string]$IconsDirectory,
        [string]$TemporaryDirectory
    )

    $appBundlePath = Join-Path $BundleDirectory "$AppName.app"
    $contentsDirectory = Join-Path $appBundlePath "Contents"
    $macOsDirectory = Join-Path $contentsDirectory "MacOS"
    $resourcesDirectory = Join-Path $contentsDirectory "Resources"

    New-Item -ItemType Directory -Path $macOsDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesDirectory -Force | Out-Null

    Get-ChildItem -LiteralPath $PublishDirectory | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $macOsDirectory -Recurse -Force
    }

    $executablePath = Join-Path $macOsDirectory $AppName
    if (Test-IsMacOSHost -and (Test-Path $executablePath) -and (Test-CommandAvailable -Name "chmod")) {
        Invoke-External -FilePath "chmod" -Arguments @("+x", $executablePath)
    }

    $iconFile = New-MacIcon -IconsDirectory $IconsDirectory -ResourcesDirectory $resourcesDirectory -TemporaryDirectory $TemporaryDirectory -AppName $AppName
    $iconBlock = if ($null -ne $iconFile) {
@"
    <key>CFBundleIconFile</key>
    <string>$([System.IO.Path]::GetFileNameWithoutExtension($iconFile))</string>
"@
    }
    else {
        ""
    }

    $plistPath = Join-Path $contentsDirectory "Info.plist"
@"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>$AppName</string>
    <key>CFBundleExecutable</key>
    <string>$AppName</string>
    <key>CFBundleIdentifier</key>
    <string>$BundleIdentifier</string>
$iconBlock
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$AppName</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$DisplayVersion</string>
    <key>CFBundleVersion</key>
    <string>$DisplayVersion</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@ | Set-Content -LiteralPath $plistPath -Encoding utf8

    return $appBundlePath
}

function Set-MacAppBundleAdHocSignature {
    param([string]$AppBundlePath)

    if (-not (Test-IsMacOSHost)) {
        return $false
    }

    if (-not (Test-CommandAvailable -Name "codesign")) {
        Write-Warning "Skipping ad-hoc signing for $AppBundlePath because codesign is not available."
        return $false
    }

    Invoke-External -FilePath "codesign" -Arguments @(
        "--force",
        "--deep",
        "--sign", "-",
        $AppBundlePath
    )

    return $true
}

function New-MacDmg {
    param(
        [string]$AppBundlePath,
        [string]$DmgPath,
        [string]$VolumeName,
        [string]$TemporaryDirectory
    )

    if (-not (Test-IsMacOSHost)) {
        Write-Warning "Skipping DMG creation for $AppBundlePath because the host OS is not macOS."
        return $false
    }

    if (-not (Test-CommandAvailable -Name "hdiutil")) {
        Write-Warning "Skipping DMG creation because hdiutil is not available."
        return $false
    }

    $dmgStageDirectory = Join-Path $TemporaryDirectory "dmg"
    New-Item -ItemType Directory -Path $dmgStageDirectory -Force | Out-Null
    Copy-Item -LiteralPath $AppBundlePath -Destination $dmgStageDirectory -Recurse -Force

    try {
        New-Item -ItemType SymbolicLink -Path (Join-Path $dmgStageDirectory "Applications") -Target "/Applications" -ErrorAction Stop | Out-Null
    }
    catch {
        Write-Warning "Could not create /Applications shortcut inside DMG staging folder. Continuing without it."
    }

    if (Test-Path $DmgPath) {
        Remove-Item -LiteralPath $DmgPath -Force
    }

    Invoke-External -FilePath "hdiutil" -Arguments @(
        "create",
        "-volname", $VolumeName,
        "-srcfolder", $dmgStageDirectory,
        "-ov",
        "-format", "UDZO",
        $DmgPath
    )

    return $true
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
$iconsDirectory = Join-Path $repoRoot "src\QuasselGlow\Assets\Icons"
$releaseRoot = Join-Path $repoRoot ".artifacts\releases\$versionTag"
$stagingRoot = Join-Path $releaseRoot "_staging"
$appName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$displayVersion = $versionTag.TrimStart([char[]]@('v', 'V'))
$bundleIdentifier = "io.quasselglow.$($appName.ToLowerInvariant())"

if (Test-Path $releaseRoot) {
    if (-not $Force) {
        throw "Release directory already exists: $releaseRoot. Re-run with -Force to replace it."
    }

    Ensure-ChildPath -RootPath (Join-Path $repoRoot ".artifacts\releases") -ChildPath $releaseRoot
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

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

- Windows portable zip: single-file self-contained executable
- macOS zip: QuasselGlow.app bundle
- macOS dmg: created automatically when packaging on macOS

## Validation

- dotnet build Quassel.slnx -c $Configuration
- dotnet test tests/Quassel.Client.Application.Tests/Quassel.Client.Application.Tests.csproj -c $Configuration --no-build
- dotnet test tests/Quassel.Client.Protocol.Tests/Quassel.Client.Protocol.Tests.csproj -c $Configuration --no-build
"@ | Set-Content -Path $releaseNotesPath -Encoding utf8
}

$generatedArtifacts = New-Object System.Collections.Generic.List[string]

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $publishDirectory = Join-Path $stagingRoot "$runtimeIdentifier-publish"
    Remove-DirectoryIfExists -RootPath $stagingRoot -TargetPath $publishDirectory
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    Publish-SingleFile -ProjectPath $projectPath -Configuration $Configuration -RuntimeIdentifier $runtimeIdentifier -OutputDirectory $publishDirectory

    if ($runtimeIdentifier.StartsWith("osx-", [System.StringComparison]::OrdinalIgnoreCase)) {
        $bundleDirectory = Join-Path $stagingRoot "$runtimeIdentifier-bundle"
        Remove-DirectoryIfExists -RootPath $stagingRoot -TargetPath $bundleDirectory
        New-Item -ItemType Directory -Path $bundleDirectory -Force | Out-Null

        $appBundlePath = New-MacAppBundle `
            -PublishDirectory $publishDirectory `
            -BundleDirectory $bundleDirectory `
            -AppName $appName `
            -BundleIdentifier $bundleIdentifier `
            -DisplayVersion $displayVersion `
            -IconsDirectory $iconsDirectory `
            -TemporaryDirectory (Join-Path $stagingRoot "$runtimeIdentifier-mac")

        $appBundleSigned = Set-MacAppBundleAdHocSignature -AppBundlePath $appBundlePath
        if ($appBundleSigned) {
            Write-Host "Applied ad-hoc code signature to $([System.IO.Path]::GetFileName($appBundlePath))"
        }

        $appArchivePath = Join-Path $releaseRoot "$appName-$versionTag-$runtimeIdentifier-app.zip"
        New-ZipArchiveFromPath -SourcePath $appBundlePath -ArchivePath $appArchivePath
        $generatedArtifacts.Add($appArchivePath) | Out-Null

        if (-not $SkipMacDmg) {
            $dmgPath = Join-Path $releaseRoot "$appName-$versionTag-$runtimeIdentifier.dmg"
            $dmgCreated = New-MacDmg `
                -AppBundlePath $appBundlePath `
                -DmgPath $dmgPath `
                -VolumeName $appName `
                -TemporaryDirectory (Join-Path $stagingRoot "$runtimeIdentifier-dmg")

            if ($dmgCreated) {
                $generatedArtifacts.Add($dmgPath) | Out-Null
            }
        }
    }
    else {
        $archivePath = Join-Path $releaseRoot "$appName-$versionTag-$runtimeIdentifier.zip"
        New-ZipArchiveFromDirectoryContents -DirectoryPath $publishDirectory -ArchivePath $archivePath
        $generatedArtifacts.Add($archivePath) | Out-Null
    }
}

$hashFilePath = Join-Path $releaseRoot "SHA256SUMS.txt"
$hashLines = foreach ($artifactPath in $generatedArtifacts | Sort-Object) {
    $hash = Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256
    "{0} *{1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path $artifactPath -Leaf)
}
$hashLines | Set-Content -Path $hashFilePath -Encoding utf8

Remove-DirectoryIfExists -RootPath $releaseRoot -TargetPath $stagingRoot

Write-Host ""
Write-Host "Release artifacts created in $releaseRoot"
Write-Host "Generated artifacts:"
$generatedArtifacts | Sort-Object | ForEach-Object { Write-Host " - " (Split-Path $_ -Leaf) }
Write-Host " - " (Split-Path $hashFilePath -Leaf)
Write-Host " - " (Split-Path $releaseNotesPath -Leaf)
