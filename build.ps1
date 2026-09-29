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

# 配布ZIPから除外するもの
# ビルド出力フォルダはそのまま実行環境になるため、動かすと個人データが溜まる。
# これらを同梱したまま公開するとダウンロード履歴やローカルパスが流出する。
$ZipExclude = @(
    "logs",                 # ダウンロードしたURL・タイトルが記録される
    "temp",                 # ダウンロード中の一時ファイル
    "download-queue.json",  # キューの中身
    "yt-dlp-gui.yaml"       # ローカルパスを含む個人の設定
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

    # 除外対象を抜いた状態を一旦別フォルダに用意してから圧縮する
    # （Compress-Archive には除外指定が無いため）
    $StageDir = Join-Path ([System.IO.Path]::GetTempPath()) ("yt-dlp-gui-pkg-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $StageDir -Force | Out-Null
    try {
        Get-ChildItem $OutputDir | Where-Object {
            $ZipExclude -notcontains $_.Name -and $_.Extension -ne ".pdb"
        } | ForEach-Object {
            Copy-Item $_.FullName -Destination $StageDir -Recurse -Force
        }

        foreach ($name in $ZipExclude) {
            if (Test-Path (Join-Path $OutputDir $name)) {
                Write-Host "  Excluded: $name" -ForegroundColor Gray
            }
        }

        Compress-Archive -Path "$StageDir\*" -DestinationPath $ZipPath -Force
    } finally {
        Remove-Item $StageDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 除外漏れがないか検証する。漏れたZIPは配布事故になるので消して失敗させる。
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    $leaked = @($archive.Entries | ForEach-Object { $_.FullName.Replace([char]92, [char]47) } | Where-Object {
        $entry = $_
        ($ZipExclude | Where-Object { $entry -eq $_ -or $entry.StartsWith("$_/") }).Count -gt 0
    })
    $archive.Dispose()
    if ($leaked.Count -gt 0) {
        Remove-Item $ZipPath -Force
        Write-Host "Excluded files leaked into the ZIP, archive deleted:" -ForegroundColor Red
        $leaked | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 1
    }

    $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "ZIP created: $ZipName ($ZipSize MB)" -ForegroundColor Green
} else {
    Write-Host "[3/3] Skipping ZIP creation (use -CreateZip to enable)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "Output: $OutputDir"
