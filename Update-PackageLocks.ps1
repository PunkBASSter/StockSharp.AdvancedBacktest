#Requires -Version 5.1
<#
.SYNOPSIS
    Updates NuGet package lock files with the latest resolved versions.

.DESCRIPTION
    This script disables locked mode, forces package re-evaluation to fetch
    the latest versions matching version constraints, restores packages,
    and builds the solution to verify compatibility.

.PARAMETER Solution
    Path to the solution file. Defaults to StockSharp.AdvancedBacktest.slnx

.PARAMETER SkipBuild
    Skip the build step after restoring packages.

.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Debug.

.EXAMPLE
    .\Update-PackageLocks.ps1
    Updates lock files and builds the solution.

.EXAMPLE
    .\Update-PackageLocks.ps1 -SkipBuild
    Updates lock files without building.
#>

[CmdletBinding()]
param(
    [string]$Solution = "StockSharp.AdvancedBacktest.slnx",
    [switch]$SkipBuild,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir

try {
    Write-Host "=== NuGet Package Lock Update ===" -ForegroundColor Cyan
    Write-Host ""

    # Verify solution exists
    if (-not (Test-Path $Solution)) {
        throw "Solution file not found: $Solution"
    }

    # Step 1: Restore with force-evaluate to update lock files
    Write-Host "[1/3] Restoring packages with latest versions..." -ForegroundColor Yellow
    Write-Host "      (RestoreLockedMode=false, force-evaluate)" -ForegroundColor DarkGray

    $restoreResult = dotnet restore $Solution `
        -p:RestoreLockedMode=false `
        --force-evaluate `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Restore failed:" -ForegroundColor Red
        Write-Host $restoreResult
        exit 1
    }
    Write-Host "      Restore completed successfully." -ForegroundColor Green
    Write-Host ""

    # Step 2: Build to verify compatibility
    if (-not $SkipBuild) {
        Write-Host "[2/3] Building solution to verify compatibility..." -ForegroundColor Yellow

        $buildResult = dotnet build $Solution `
            --no-restore `
            -c $Configuration `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed with updated packages:" -ForegroundColor Red
            Write-Host $buildResult
            Write-Host ""
            Write-Host "Lock files have been updated but build failed." -ForegroundColor Yellow
            Write-Host "You may want to revert changes: git checkout -- **/packages.lock.json" -ForegroundColor Yellow
            exit 1
        }
        Write-Host "      Build completed successfully." -ForegroundColor Green
    }
    else {
        Write-Host "[2/3] Skipping build (--SkipBuild specified)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Step 3: Show updated lock files
    Write-Host "[3/3] Updated lock files:" -ForegroundColor Yellow
    $lockFiles = Get-ChildItem -Path . -Filter "packages.lock.json" -Recurse |
        Where-Object { $_.FullName -notlike "*\StockSharp\*" }

    foreach ($file in $lockFiles) {
        $relativePath = $file.FullName.Replace($scriptDir, "").TrimStart("\")
        $lastWrite = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        Write-Host "      $relativePath (modified: $lastWrite)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Show git status for lock files
    Write-Host "=== Git Status ===" -ForegroundColor Cyan
    $gitStatus = git status --porcelain -- "**/packages.lock.json" 2>&1
    if ($gitStatus) {
        Write-Host $gitStatus
        Write-Host ""
        Write-Host "To commit changes:" -ForegroundColor Yellow
        Write-Host "  git add **/packages.lock.json" -ForegroundColor DarkGray
        Write-Host "  git commit -m 'Update NuGet package lock files'" -ForegroundColor DarkGray
    }
    else {
        Write-Host "No changes to lock files." -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
