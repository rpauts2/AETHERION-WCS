[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$SkipGobkitCatalog
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$incoming = Join-Path $RepositoryRoot "assets\incoming\cc0"
$downloads = Join-Path $incoming ".downloads"
New-Item -ItemType Directory -Force -Path $downloads | Out-Null

# Fixed source packages with verified CC0 terms and direct archive URLs.
# Gobkit packs are discovered dynamically from its read-only public CC0 manifest below.
$packs = @(
    [pscustomobject]@{ Id = "cethiel-dragon-3d"; Name = "Cethiel's Dragon 3D"; Url = "https://opengameart.org/sites/default/files/dragon_oga.zip"; Source = "https://opengameart.org/content/cethiels-dragon-3d"; License = "CC0-1.0"; Destination = "cethiels_dragon" },
    [pscustomobject]@{ Id = "kenney-particle-pack"; Name = "Kenney Particle Pack"; Url = "https://opengameart.org/sites/default/files/kenney_particlePack.zip"; Source = "https://kenney.nl/assets/particle-pack"; License = "CC0-1.0"; Destination = "kenney_particle_pack" },
    [pscustomobject]@{ Id = "kenney-smoke-particles"; Name = "Kenney Smoke Particles"; Url = "https://opengameart.org/sites/default/files/smokeParticleAssets.zip"; Source = "https://opengameart.org/content/smoke-particle-assets"; License = "CC0-1.0"; Destination = "kenney_smoke_particles" }
)

function Expand-SafeZip {
    param([string]$ArchivePath, [string]$DestinationPath)
    $root = [IO.Path]::GetFullPath($DestinationPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
            if ($entry.FullName -match '(^|[/\\])\.\.([/\\]|$)|^[/\\]|^[A-Za-z]:') {
                throw "Unsafe ZIP entry path rejected: $($entry.FullName)"
            }
            $target = [IO.Path]::GetFullPath((Join-Path $DestinationPath $entry.FullName))
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe ZIP entry path rejected: $($entry.FullName)"
            }
            if ($entry.FullName.EndsWith("/") -or $entry.FullName.EndsWith("\")) {
                New-Item -ItemType Directory -Force -Path $target | Out-Null
                continue
            }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            $inputStream = $entry.Open()
            try {
                $outputStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try { $inputStream.CopyTo($outputStream) }
                finally { $outputStream.Dispose() }
            }
            finally { $inputStream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}

function Save-SourcePack {
    param(
        [string]$Id,
        [string]$Name,
        [string]$Url,
        [string]$Source,
        [string]$License,
        [string]$Destination,
        [object]$Metadata = $null
    )

    if ($Id -notmatch '^[A-Za-z0-9_-]+$' -or $Destination -notmatch '^[A-Za-z0-9_-]+$') {
        throw "Unsafe package identifier or destination: $Id"
    }
    if (-not $Url.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Only HTTPS archive URLs are allowed: $Url"
    }

    $destinationPath = Join-Path $incoming $Destination
    if (Test-Path $destinationPath) {
        Write-Host "SKIP existing package (no overwrite): $Name"
        return $null
    }

    $archive = Join-Path $downloads ($Id + ".zip")
    Write-Host "Downloading $Name..."
    Invoke-WebRequest -Uri $Url -OutFile $archive -UseBasicParsing
    if (-not (Test-Path $archive) -or (Get-Item $archive).Length -lt 1000) {
        throw "Download looks incomplete: $archive"
    }

    $hash = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()
    New-Item -ItemType Directory -Path $destinationPath | Out-Null
    Expand-SafeZip -ArchivePath $archive -DestinationPath $destinationPath

    $metadataJson = ""
    if ($null -ne $Metadata) {
        $metadataJson = ($Metadata | ConvertTo-Json -Depth 12)
    }
    $readme = @"
# $Name

- Source page: $Source
- Download URL: $Url
- Declared license: $License
- Original archive: $Id.zip
- Archive SHA-256: $hash
- Downloaded UTC: $([DateTime]::UtcNow.ToString("o"))

## Source metadata

$metadataJson

This is an acquired source package, not a compiled Counter-Strike 2 resource. Inspect the archive contents and license evidence, then port and compile through Counter-Strike 2 Workshop Tools before referencing it from game configs.
"@
    Set-Content -LiteralPath (Join-Path $destinationPath "SOURCE-RECEIPT.md") -Value $readme -Encoding UTF8

    return [pscustomobject]@{
        id = $Id
        name = $Name
        source = $Source
        download_url = $Url
        license = $License
        archive = ".downloads/$Id.zip"
        sha256 = $hash
        extracted_to = "assets/incoming/cc0/$Destination"
        metadata = $Metadata
        status = "downloaded_not_engine_verified"
    }
}

$receipt = @()
foreach ($pack in $packs) {
    $result = Save-SourcePack -Id $pack.Id -Name $pack.Name -Url $pack.Url -Source $pack.Source -License $pack.License -Destination $pack.Destination
    if ($null -ne $result) { $receipt += $result }
}

if (-not $SkipGobkitCatalog) {
    $manifestUrl = "https://gobkit.com/api/free"
    $manifestPath = Join-Path $downloads "gobkit-free-manifest.json"
    Write-Host "Reading Gobkit public CC0 manifest..."
    Invoke-WebRequest -Uri $manifestUrl -OutFile $manifestPath -UseBasicParsing
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $manifest.packs) { throw "Gobkit manifest contains no packs; refusing to guess archive URLs." }

    # Preserve the exact manifest that was used for this run.
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $incoming "gobkit-free-manifest.json") -Force
    $sourcePage = "https://gobkit.com/freebies"
    $gobkitCount = 0
    foreach ($pack in $manifest.packs) {
        if ([string]$pack.license -notmatch 'CC0\s*1\.0') {
            Write-Host "SKIP non-CC0 Gobkit pack: $($pack.name)"
            continue
        }
        if (-not $pack.zip) {
            Write-Host "SKIP Gobkit pack without a ZIP URL: $($pack.name)"
            continue
        }
        $uri = [Uri]$pack.zip
        if ($uri.Scheme -ne "https" -or $uri.Host -ne "gobkit.com") {
            throw "Unexpected Gobkit archive host; refusing URL: $($pack.zip)"
        }
        $safeId = ([string]$pack.id) -replace '[^A-Za-z0-9_-]', '_'
        $metadata = [pscustomobject]@{
            api_version = $manifest.version
            kind = $pack.kind
            format = $pack.format
            count = $pack.count
            rigged = $pack.rigged
            fps = $pack.fps
            animations = $pack.animations
            model_names = @($pack.models | ForEach-Object { $_.name })
        }
        $result = Save-SourcePack -Id ("gobkit-" + $safeId) -Name ([string]$pack.name) -Url ([string]$pack.zip) -Source $sourcePage -License ([string]$pack.license) -Destination ("gobkit-" + $safeId) -Metadata $metadata
        if ($null -ne $result) {
            $receipt += $result
            $gobkitCount++
        }
    }
    Write-Host "Gobkit CC0 packs downloaded this run: $gobkitCount"
}

$receiptPath = Join-Path $incoming "download-receipt.json"
$receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding UTF8

Write-Host ""
Write-Host "Downloaded and safely extracted $($receipt.Count) new CC0 source packages."
Write-Host "Receipt: $receiptPath"
Write-Host "Gobkit manifest snapshot: $(Join-Path $incoming 'gobkit-free-manifest.json')"
Write-Host "No files were copied into the CS2 content or game directory. Source2 conversion and visual testing are still required."
