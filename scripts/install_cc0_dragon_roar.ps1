[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Cs2Path = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# One openly licensed audio sample used as the first end-to-end AETHERION sound test.
# It is AI-generated/procedural according to the source publisher; disclose AI audio
# in the store content survey if required by the distribution platform.
$sourcePage = "https://sfxmint.com/sounds/dragon-roar-03"
$downloadUrl = "https://sfxmint.com/dl/dragon-roar-03.mp3"
$licenseUrl = "https://sfxmint.com/license"
$runtimePath = "sounds/race/dragon_roar.vsnd"
$raceId = "2033"

$contentRoot = Join-Path $Cs2Path "content\csgo_addons\aetherion"
$configPath = Join-Path $RepositoryRoot "configs\assets\sounds.json"
$incomingRoot = Join-Path $RepositoryRoot "assets\incoming\cc0\audio\sfxmint"
$incomingFile = Join-Path $incomingRoot "dragon-roar-03.mp3"
$sourceFile = Join-Path $contentRoot "sounds\race\dragon_roar.mp3"
$compiledFile = Join-Path $Cs2Path "game\csgo_addons\aetherion\sounds\race\dragon_roar.vsnd_c"
$resourceCompiler = Join-Path $Cs2Path "game\bin\win64\resourcecompiler.exe"

if (-not (Test-Path $resourceCompiler)) {
    throw "Workshop Tools resourcecompiler.exe not found: $resourceCompiler. Install CS2 Workshop Tools first."
}
if (-not (Test-Path $contentRoot)) {
    throw "AETHERION addon content root not found: $contentRoot"
}
if (-not (Test-Path $configPath)) {
    throw "Audio config not found: $configPath"
}
if ((Test-Path $incomingFile) -or (Test-Path $sourceFile) -or (Test-Path $compiledFile)) {
    throw "A dragon-roar file or compiled output already exists. Refusing to overwrite; inspect the existing asset first."
}
$backupPath = "$configPath.before-dragon-roar.bak"
if (Test-Path $backupPath) {
    throw "Config backup already exists: $backupPath. Refusing to overwrite."
}

New-Item -ItemType Directory -Force -Path $incomingRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $sourceFile) | Out-Null
Write-Host "Downloading CC0 dragon roar from SFXMint..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $incomingFile -UseBasicParsing

$bytes = [IO.File]::ReadAllBytes($incomingFile)
if ($bytes.Length -lt 16000 -or $bytes.Length -gt 25000000) {
    throw "Downloaded audio size is outside the expected safe range: $($bytes.Length) bytes"
}
$hasId3 = $bytes.Length -ge 3 -and [Text.Encoding]::ASCII.GetString($bytes, 0, 3) -eq "ID3"
$hasFrameSync = $bytes.Length -ge 2 -and $bytes[0] -eq 255 -and (($bytes[1] -band 224) -eq 224)
if (-not ($hasId3 -or $hasFrameSync)) {
    throw "Downloaded file does not look like an MP3 stream. Keeping the file in the incoming folder for inspection."
}

$sha256 = (Get-FileHash -Algorithm SHA256 -Path $incomingFile).Hash.ToLowerInvariant()
Copy-Item -LiteralPath $incomingFile -Destination $sourceFile

Write-Host "Compiling the MP3 into a Source 2 VSND resource..."
& $resourceCompiler -i $sourceFile -r
if ($LASTEXITCODE -ne 0) {
    throw "resourcecompiler.exe failed with exit code $LASTEXITCODE. The source MP3 remains staged; the config was not changed."
}
if (-not (Test-Path $compiledFile)) {
    throw "Expected compiled output was not created: $compiledFile. The config was not changed."
}

$config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
$entryProperty = $config.PSObject.Properties[$raceId]
if ($null -eq $entryProperty) {
    $entry = [PSCustomObject]@{
        race = "Драконорождённый"
        ult = ""
        sound = ""
        customSound = $runtimePath
        customSoundEnabled = $true
    }
    $config | Add-Member -MemberType NoteProperty -Name $raceId -Value $entry
}
else {
    $entry = $entryProperty.Value
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.customSound) -and $entry.customSound -ne $runtimePath) {
        throw "Race $raceId already has a different customSound mapping: $($entry.customSound). Compiled asset was kept, config was not changed."
    }
    $entry.customSound = $runtimePath
    $entry.customSoundEnabled = $true
}

Copy-Item -LiteralPath $configPath -Destination $backupPath
$config | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $configPath -Encoding UTF8

$receipt = @"
# SFXMint Dragon Roar 03 — source receipt

- Sound page: $sourcePage
- Direct download URL: $downloadUrl
- License: CC0 1.0 Universal
- License evidence: $licenseUrl
- Format: MP3
- SHA-256: $sha256
- Downloaded UTC: $([DateTime]::UtcNow.ToString("o"))
- AETHERION runtime path: $runtimePath
- Race mapping: $raceId (Dragonborn)
- Compiled output: game/csgo_addons/aetherion/$($runtimePath)_c

The publisher states that the public SFXMint library is AI-generated or procedurally synthesised and dedicated under CC0 1.0. Check the current Steam/itch AI-content disclosure rules before distributing a build that includes this audio.

The MP3 source is staged under assets/incoming/cc0/audio/sfxmint and copied into the addon content tree. Source 2 Workshop Tools compiled the game resource. The script does not prove that a connected CS2 client audibly plays the sound; verify it on a private server.
"@
Set-Content -LiteralPath (Join-Path $incomingRoot "SOURCE-RECEIPT.md") -Value $receipt -Encoding UTF8

Write-Host ""
Write-Host "Dragon roar compiled and mapped to race $raceId."
Write-Host "Source: $sourceFile"
Write-Host "Compiled: $compiledFile"
Write-Host "Runtime: play $runtimePath"
Write-Host "Config backup: $backupPath"
Write-Host "Receipt: $(Join-Path $incomingRoot 'SOURCE-RECEIPT.md')"
Write-Host "Next: run the private-server audio playback check. Do not assume client delivery is proven yet."
