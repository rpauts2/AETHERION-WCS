[CmdletBinding()]
param(
    [string]$Cs2Path = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
    [string]$AddonName = "aetherion",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$resourceCompiler = Join-Path $Cs2Path "game\bin\win64\resourcecompiler.exe"
$contentRoot = Join-Path $Cs2Path "content\csgo_addons\$AddonName"
$sourceRoot = Join-Path $contentRoot "sounds"
$gameRoot = Join-Path $Cs2Path "game\csgo_addons\$AddonName\sounds"

if (-not (Test-Path -LiteralPath $resourceCompiler)) {
    throw "Workshop Tools resourcecompiler.exe not found: $resourceCompiler"
}
if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Addon audio source directory not found: $sourceRoot"
}

$audioFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
    $_.Extension -in @(".wav", ".mp3")
})
if ($audioFiles.Count -eq 0) {
    throw "No WAV/MP3 files found under $sourceRoot"
}

$compiled = 0
$skipped = 0
$failed = @()
foreach ($file in $audioFiles) {
    $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
    $relativeVsnd = [IO.Path]::ChangeExtension($relative, ".vsnd") + "_c"
    $expected = Join-Path $gameRoot $relativeVsnd

    if ((Test-Path -LiteralPath $expected) -and -not $Force) {
        Write-Host "SKIP existing compiled resource: $relativeVsnd"
        $skipped++
        continue
    }

    Write-Host "Compile: $relative"
    & $resourceCompiler -i $file.FullName -r
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $failed += "$relative (resourcecompiler exit $exitCode)"
        continue
    }
    if (-not (Test-Path -LiteralPath $expected)) {
        $failed += "$relative (expected output missing: $expected)"
        continue
    }
    $compiled++
}

Write-Host ""
Write-Host "Audio compile summary: compiled=$compiled skipped=$skipped failed=$($failed.Count)"
if ($failed.Count -gt 0) {
    $failed | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host "All source audio files are compiled. Run scripts/audit_audio_assets.py to check active customSound mappings."
