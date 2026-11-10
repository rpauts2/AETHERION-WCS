[CmdletBinding()]
param(
    [string]$Cs2Path = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
    [string]$Source2ViewerCli = "C:\Tools\Source2Viewer-CLI.exe",
    [string]$OutputDirectory = (Join-Path $env:TEMP "AETHERION-CS2-stock-assets")
)

$ErrorActionPreference = "Stop"
$pak = Join-Path $Cs2Path "game\csgo\pak01_dir.vpk"
if (-not (Test-Path -LiteralPath $pak)) {
    throw "CS2 base VPK not found: $pak. Check -Cs2Path."
}
if (-not (Test-Path -LiteralPath $Source2ViewerCli)) {
    throw "Source2Viewer-CLI.exe not found: $Source2ViewerCli. Download the official CLI release and pass -Source2ViewerCli."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$rawPath = Join-Path $OutputDirectory "vpk-list-raw.txt"
$allPath = Join-Path $OutputDirectory "all-matched-resources.txt"
$particlePath = Join-Path $OutputDirectory "stock-particles.txt"
$soundEventPath = Join-Path $OutputDirectory "stock-soundevents.txt"
$soundPath = Join-Path $OutputDirectory "stock-sounds.txt"
$summaryPath = Join-Path $OutputDirectory "summary.json"

Write-Host "Listing files in the CS2 base VPK. This is read-only..."
$lines = @(& $Source2ViewerCli -i $pak --vpk_list 2>&1)
$exitCode = $LASTEXITCODE
$rawText = ($lines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
Set-Content -LiteralPath $rawPath -Value $rawText -Encoding UTF8

if ($exitCode -ne 0) {
    throw "Source2Viewer-CLI failed with exit code $exitCode. Raw output: $rawPath"
}

$pattern = '(?i)(?:particles|soundevents|sounds)/[A-Za-z0-9_./-]+\.(?:vpcf_c|vsndevts_c|vsnd_c)'
$matches = [regex]::Matches($rawText, $pattern)
$resources = @(
    $matches |
    ForEach-Object { $_.Value.Replace("\", "/") } |
    Sort-Object -Unique
)

if ($resources.Count -eq 0) {
    throw "No Source 2 particle/audio paths were recognized in CLI output. Raw VPK list saved to $rawPath for manual inspection."
}

$particles = @($resources | Where-Object { $_ -match '(?i)^particles/.+\.vpcf_c$' })
$soundEvents = @($resources | Where-Object { $_ -match '(?i)^soundevents/.+\.vsndevts_c$' })
$sounds = @($resources | Where-Object { $_ -match '(?i)^sounds/.+\.vsnd_c$' })

Set-Content -LiteralPath $allPath -Value $resources -Encoding UTF8
Set-Content -LiteralPath $particlePath -Value $particles -Encoding UTF8
Set-Content -LiteralPath $soundEventPath -Value $soundEvents -Encoding UTF8
Set-Content -LiteralPath $soundPath -Value $sounds -Encoding UTF8

$summary = [ordered]@{
    scanned_vpk = $pak
    source2viewer_cli = $Source2ViewerCli
    scanned_utc = [DateTime]::UtcNow.ToString("o")
    vpk_list_exit_code = $exitCode
    matched_resource_paths = $resources.Count
    particles_vpcf_c = $particles.Count
    soundevents_vsndevts_c = $soundEvents.Count
    sounds_vsnd_c = $sounds.Count
    output_files = [ordered]@{
        all = $allPath
        particles = $particlePath
        soundevents = $soundEventPath
        sounds = $soundPath
        raw_vpk_list = $rawPath
    }
    warning = "This inventories stock CS2 depot paths only. It does not extract, copy, or license assets for redistribution."
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "CS2 stock resource inventory complete:"
Write-Host "  Particle systems: $($particles.Count)"
Write-Host "  Sound event scripts: $($soundEvents.Count)"
Write-Host "  Direct sound resources: $($sounds.Count)"
Write-Host "  Output: $OutputDirectory"
Write-Host ""
Write-Host "Send stock-particles.txt and stock-soundevents.txt back for mapping AETHERION's missing custom effects to verified game resources."
Write-Host "No VPK resources were extracted, copied or modified."
