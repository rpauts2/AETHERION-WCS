 [CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$incoming = Join-Path $RepositoryRoot "assets\incoming\cc0"
$downloads = Join-Path $incoming ".downloads"
New-Item -ItemType Directory -Force -Path $downloads | Out-Null

$packs = @(
    [pscustomobject]@{ Id = "cethiel-dragon-3d"; Name = "Cethiel's Dragon 3D"; Url = "https://opengameart.org/sites/default/files/dragon_oga.zip"; Source = "https://opengameart.org/content/cethiels-dragon-3d"; License = "CC0-1.0"; Destination = "cethiels_dragon" },
    [pscustomobject]@{ Id = "gobkit-animal-pack"; Name = "Gobkit Free Animal Pack"; Url = "https://gobkit.com/freebies/animal/gobkit-animal-pack.zip"; Source = "https://gobkit.com/freebies"; License = "CC0-1.0"; Destination = "gobkit_animal_pack" },
    [pscustomobject]@{ Id = "gobkit-minion-pack"; Name = "Gobkit Free Minion Pack"; Url = "https://gobkit.com/freebies/gobkit-free-pack.zip"; Source = "https://gobkit.com/freebies"; License = "CC0-1.0"; Destination = "gobkit_minion_pack" },
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
            $target = [IO.Path]::GetFullPath((Join-Path $DestinationPath $entry.FullName))
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe ZIP entry path rejected: $($entry.FullName)"
            }
            if ($entry.FullName.EndsWith("/")) {
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

$receipt = @()
foreach ($pack in $packs) {
    $destination = Join-Path $incoming $pack.Destination
    if (Test-Path $destination) {
        throw "Destination already exists; refusing to overwrite: $destination"
    }

    $archive = Join-Path $downloads ($pack.Id + ".zip")
    Write-Host "Downloading $($pack.Name)..."
    Invoke-WebRequest -Uri $pack.Url -OutFile $archive -UseBasicParsing
    if (-not (Test-Path $archive) -or (Get-Item $archive).Length -lt 1000) {
        throw "Download looks incomplete: $archive"
    }

    $hash = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()
    New-Item -ItemType Directory -Path $destination | Out-Null
    Expand-SafeZip -ArchivePath $archive -DestinationPath $destination

    $readme = @"
# $($pack.Name)

- Source page: $($pack.Source)
- Download URL: $($pack.Url)
- Declared license: $($pack.License)
- Original archive: $($pack.Id).zip
- Archive SHA-256: $hash
- Downloaded UTC: $([DateTime]::UtcNow.ToString("o"))

This is an acquired source package, not a compiled Counter-Strike 2 resource. Inspect the archive contents and license evidence, then port and compile through Counter-Strike 2 Workshop Tools before referencing it from game configs.
"@
    Set-Content -LiteralPath (Join-Path $destination "SOURCE-RECEIPT.md") -Value $readme -Encoding UTF8
    $receipt += [pscustomobject]@{
        id = $pack.Id; name = $pack.Name; source = $pack.Source; download_url = $pack.Url
        license = $pack.License; archive = ".downloads/$($pack.Id).zip"; sha256 = $hash
        extracted_to = "assets/incoming/cc0/$($pack.Destination)"; status = "downloaded_not_engine_verified"
    }
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $incoming "download-receipt.json") -Encoding UTF8
}

Write-Host ""
Write-Host "Downloaded and safely extracted $($receipt.Count) CC0 source packages."
Write-Host "Receipt: $(Join-Path $incoming 'download-receipt.json')"
Write-Host "No files were copied into the CS2 content or game directory. Source2 conversion and visual testing are still required."
