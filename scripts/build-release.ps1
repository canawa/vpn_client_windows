param(
    [ValidateSet("installer", "portable", "all")]
    [string]$Target = "all"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\CoffeeManiaVPN\CoffeeManiaVPN.csproj"
$Dist = Join-Path $Root "dist"
$AppDir = Join-Path $Dist "app"
$PortableDir = Join-Path $Dist "portable"

function Publish-App {
    param(
        [string]$Output,
        [hashtable]$Properties
    )

    if (Test-Path $Output) {
        Remove-Item $Output -Recurse -Force
    }

    $propArgs = $Properties.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" }
    Write-Host "Publishing -> $Output" -ForegroundColor Cyan
    dotnet publish $Project -c Release -o $Output -r win-x64 --self-contained true @propArgs

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function New-PortableZip {
    param([string]$SourceDir)

    $zipPath = Join-Path $Dist "CoffeeManiaVPN-portable.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host "Creating ZIP: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $SourceDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

function Sign-Artifact {
    param(
        [string]$FilePath,
        [string]$Description = "КОФЕМАНИЯ ВПН"
    )

    $signScript = Join-Path $Root "scripts\sign-artifact.ps1"
    if (-not (Test-Path $signScript)) {
        return
    }

    & $signScript -FilePath $FilePath -Description $Description
}

function Build-InnoInstaller {
    $iss = Join-Path $Root "installer\CoffeeManiaVPN.iss"
    $isccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        Write-Host "Inno Setup not found. Install from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host "Or build manually: ISCC.exe `"$iss`"" -ForegroundColor Yellow
        return $false
    }

    Write-Host "Building Inno Setup installer..." -ForegroundColor Cyan
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE"
    }

    return $true
}

Push-Location $Root
try {
    New-Item -ItemType Directory -Force -Path $Dist | Out-Null

    if ($Target -in @("installer", "all")) {
        Publish-App -Output $AppDir -Properties @{
            PublishSingleFile = "false"
            PublishReadyToRun = "true"
        }

        $appExe = Join-Path $AppDir "CoffeeManiaVPN.exe"
        if (Test-Path $appExe) {
            Sign-Artifact -FilePath $appExe -Description "КОФЕМАНИЯ ВПН"
        }

        $built = Build-InnoInstaller
        if ($built) {
            Start-Sleep -Seconds 2
            Get-ChildItem $Dist -Filter "CoffeeManiaVPN-Setup-*.exe" | ForEach-Object {
                Sign-Artifact -FilePath $_.FullName -Description "Установщик КОФЕМАНИЯ ВПН"
                Write-Host "Done: $($_.FullName)" -ForegroundColor Green
            }
        }
    }

    if ($Target -in @("portable", "all")) {
        Publish-App -Output $PortableDir -Properties @{
            PublishSingleFile = "true"
            IncludeAllContentForSelfExtract = "true"
            IncludeNativeLibrariesForSelfExtract = "true"
            EnableCompressionInSingleFile = "true"
            PublishReadyToRun = "true"
        }
        $portableExe = Join-Path $PortableDir "CoffeeManiaVPN.exe"
        if (Test-Path $portableExe) {
            Sign-Artifact -FilePath $portableExe -Description "КОФЕМАНИЯ ВПН"
        }
        New-PortableZip -SourceDir $PortableDir
        $zipPath = Join-Path $Dist "CoffeeManiaVPN-portable.zip"
        Write-Host "Done: $zipPath" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Build finished. Output is in the dist folder." -ForegroundColor Green
}
finally {
    Pop-Location
}
