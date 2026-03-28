# yt-dlp-gui ビルドスクリプト
# Usage: .\build.ps1 [-Configuration Debug|Release] [-CreateZip]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "yt-dlp-gui\yt-dlp-gui.csproj"
$OutputDir = Join-Path $ProjectRoot "yt-dlp-gui\bin\$Configuration\net6.0-windows10.0.17763.0"

# 必要な外部ファイル
$RequiredFiles = @(
    "yt-dlp.exe",
    "ffmpeg.exe",
    "ffprobe.exe"
)

Write-Host "=== yt-dlp-gui Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host ""

# 1. ビルド実行
Write-Host "[1/3] Building project..." -ForegroundColor Yellow
dotnet build $ProjectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host ""

# 2. 必要なファイルをコピー
Write-Host "[2/3] Copying required files..." -ForegroundColor Yellow
foreach ($file in $RequiredFiles) {
    $src = Join-Path $ProjectRoot $file
    $dst = Join-Path $OutputDir $file
    if (Test-Path $src) {
        Copy-Item $src $dst -Force
        Write-Host "  Copied: $file" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: $file not found in project root" -ForegroundColor Yellow
    }
}
Write-Host "Files copied!" -ForegroundColor Green
Write-Host ""

# 3. ZIPファイル作成 (オプション)
if ($CreateZip) {
    Write-Host "[3/3] Creating ZIP file..." -ForegroundColor Yellow
    $ZipName = "yt-dlp-gui.zip"
    $ZipPath = Join-Path $ProjectRoot $ZipName

    # 既存のZIPを削除
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    # ZIP作成
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipPath -Force

    $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "ZIP created: $ZipName ($ZipSize MB)" -ForegroundColor Green
} else {
    Write-Host "[3/3] Skipping ZIP creation (use -CreateZip to enable)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "Output: $OutputDir"
