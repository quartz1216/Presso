#requires -Version 5.1
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Step($msg) { Write-Host "==>" $msg -ForegroundColor Cyan }

$Project   = Join-Path $Root "src\Presso\Presso.csproj"
$Installer = Join-Path $Root "installer\Presso.iss"
$OutputDir = Join-Path $Root "installer\output"

# 1) Publish self-contained single-file exe
Step "dotnet publish (Release, win-x64, self-contained, single-file)"
& dotnet publish $Project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$PublishDir = Join-Path $Root "src\Presso\bin\Release\net8.0-windows\win-x64\publish"
$PublishExe = Join-Path $PublishDir "Presso.exe"
if (-not (Test-Path -LiteralPath $PublishExe)) {
    throw "Publish output not found: $PublishExe"
}
Step "Published: $PublishExe"

# 2) Verify ffmpeg exists — installer will pick it up
$FfmpegPath = Join-Path $Root "ffmpeg\bin\ffmpeg.exe"
if (-not (Test-Path -LiteralPath $FfmpegPath)) {
    throw "ffmpeg.exe が見つかりません: $FfmpegPath`nffmpeg/bin/ffmpeg.exe と ffprobe.exe を配置してください。"
}
Step "ffmpeg found: $FfmpegPath"

# 3) Build installer
$Iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path -LiteralPath $Iscc)) {
    throw "Inno Setup not found: $Iscc"
}

Step "Building installer with ISCC"
& $Iscc $Installer
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Step "Installer output:"
Get-ChildItem -Path $OutputDir -Filter "*.exe" -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host "  " $_.FullName }

Step "Done."
