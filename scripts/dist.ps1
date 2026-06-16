# File: scripts/dist.ps1

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SkipChecks,
    [switch]$SkipSmokeCheck,
    [switch]$NoArchive
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifacts = Join-Path $root "artifacts"
$dist = Join-Path $artifacts "dist"
$desktopOutput = Join-Path $artifacts "PostgresCopy-desktop-$Runtime"
$cliOutput = Join-Path $artifacts "PostgresCopy-cli-$Runtime"

function Assert-SafeName {
    param(
        [string]$Name,
        [string]$Value
    )

    if ($Value -notmatch '^[A-Za-z0-9._-]+$') {
        throw "$Name contains unsupported characters: $Value"
    }
}

function Test-IsUnderRoot {
    param([string]$Path)

    $resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    return $resolvedPath.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Remove-DirectoryIfExists {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    if (-not (Test-IsUnderRoot $Path)) {
        throw "Refusing to remove a path outside the repository: $Path"
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Invoke-RepoScript {
    param(
        [string]$RelativePath,
        [hashtable]$Arguments = @{}
    )

    $scriptPath = Join-Path $root $RelativePath
    & $scriptPath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$RelativePath failed with exit code $LASTEXITCODE."
    }
}

function New-Archive {
    param(
        [string]$SourceDirectory,
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory)) {
        throw "Cannot archive missing directory: $SourceDirectory"
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    Compress-Archive -Path $SourceDirectory -DestinationPath $DestinationPath -Force
}

function Get-RelativePath {
    param([string]$Path)

    $resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    return $resolvedPath.Substring($resolvedRoot.Length + 1)
}

Assert-SafeName "Runtime" $Runtime
Assert-SafeName "Configuration" $Configuration

Push-Location $root
try {
    if (-not $SkipChecks) {
        Write-Host "Running standard checks before distribution..."
        Invoke-RepoScript "scripts/check.ps1"
    }

    Write-Host "Preparing distribution output..."
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    Remove-DirectoryIfExists $desktopOutput
    Remove-DirectoryIfExists $cliOutput

    $desktopArgs = @{
        Runtime = $Runtime
        Configuration = $Configuration
    }

    if (-not $SkipSmokeCheck) {
        $desktopArgs["SmokeCheck"] = $true
    }

    Write-Host "Publishing desktop distribution..."
    Invoke-RepoScript "scripts/publish-desktop.ps1" $desktopArgs

    Write-Host "Publishing CLI distribution..."
    Invoke-RepoScript "scripts/publish-cli.ps1" @{
        Runtime = $Runtime
        Configuration = $Configuration
    }

    $checksumTargets = @(
        (Join-Path $desktopOutput "PostgresCopy.Desktop.exe"),
        (Join-Path $cliOutput "PostgresCopy.exe")
    )

    if (-not $NoArchive) {
        $desktopArchive = Join-Path $dist "PostgresCopy-desktop-$Runtime.zip"
        $cliArchive = Join-Path $dist "PostgresCopy-cli-$Runtime.zip"

        Write-Host "Creating distribution archives..."
        New-Archive $desktopOutput $desktopArchive
        New-Archive $cliOutput $cliArchive

        $checksumTargets += $desktopArchive
        $checksumTargets += $cliArchive
    }

    $checksumPath = Join-Path $dist "SHA256SUMS.txt"
    $checksumLines = foreach ($path in $checksumTargets) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Expected distribution file was not found: $path"
        }

        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $path
        "$($hash.Hash.ToLowerInvariant())  $(Get-RelativePath $path)"
    }

    $checksumLines | Set-Content -LiteralPath $checksumPath -Encoding ascii

    Write-Host "Distribution complete."
    Write-Host "Desktop:   $desktopOutput"
    Write-Host "CLI:       $cliOutput"
    if (-not $NoArchive) {
        Write-Host "Archives:  $dist"
    }
    Write-Host "Checksums: $checksumPath"
}
finally {
    Pop-Location
}
