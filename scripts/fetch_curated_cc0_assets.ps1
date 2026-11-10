[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$SkipExtraction
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$incoming = Join-Path $RepositoryRoot "assets\incoming\cc0-curated"
$downloads = Join-Path $incoming ".downloads"
New-Item -ItemType Directory -Force -Path $downloads | Out-Null

$packs = @(
    [pscustomobject]@{ Id="kenney-modular-dungeon-kit"; Name="Kenney Modular Dungeon Kit"; Method="direct"; Source="https://kenney.nl/assets/modular-dungeon-kit"; License="https://creativecommons.org/publicdomain/zero/1.0/"; Url="https://kenney.nl/media/pages/assets/modular-dungeon-kit/7bed87605b-1771926065/kenney_modular-dungeon-kit_1.0.zip"; Bytes=6886434; Sha256="dd0aa6776db8912283cdca60161dee6a8839bbda3558eba2ea501419eb5b4623"; Destination="kenney_modular_dungeon_kit" },
    [pscustomobject]@{ Id="kenney-particle-pack"; Name="Kenney Particle Pack"; Method="direct"; Source="https://kenney.nl/assets/particle-pack"; License="https://creativecommons.org/publicdomain/zero/1.0/"; Url="https://kenney.nl/media/pages/assets/particle-pack/f8fe0f8cb8-1677578741/kenney_particle-pack.zip"; Bytes=15001764; Sha256="b631d4b07f7002549fdcf155f01141ad482f79f3440e4e301eed49ce5f1d8958"; Destination="kenney_particle_pack" },
    [pscustomobject]@{ Id="kenney-smoke-particles"; Name="Kenney Smoke Particles"; Method="direct"; Source="https://kenney.nl/assets/smoke-particles"; License="https://creativecommons.org/publicdomain/zero/1.0/"; Url="https://kenney.nl/media/pages/assets/smoke-particles/23249a0d35-1677695171/kenney_smoke-particles.zip"; Bytes=6019666; Sha256="97a1d09c66e4fd6c247c8ea87f84c0cc59caaeceae19414c995afb1616a1e1c9"; Destination="kenney_smoke_particles" },
    [pscustomobject]@{ Id="kaykit-adventures"; Name="KayKit Character Pack Adventures 1.0"; Method="direct"; Source="https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0"; License="https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/main/README.md"; Url="https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/archive/refs/heads/main.zip"; Bytes=6141833; Sha256="e19176d799036452bc783c00dd1036e158c66c5a43b7285ccbfea3466d6f57bf"; Destination="kaykit_adventures" },
    [pscustomobject]@{ Id="kaykit-skeletons"; Name="KayKit Character Pack Skeletons 1.0"; Method="direct"; Source="https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0"; License="https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0/main/README.md"; Url="https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0/archive/refs/heads/main.zip"; Bytes=6658096; Sha256="bb666ec30ff243f54cffdd936fcceeb5c68cdb424d230fe637b7bfd756c0c8e6"; Destination="kaykit_skeletons" },
    [pscustomobject]@{ Id="kaykit-dungeon-remastered"; Name="KayKit Dungeon Remastered 1.0"; Method="direct"; Source="https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0"; License="https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0/main/README.md"; Url="https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0/archive/refs/heads/main.zip"; Bytes=6312130; Sha256="8e92aa460f5be4252dff0cf030f92846ab74b875ce4756076ecce75b5d0c9bee"; Destination="kaykit_dungeon_remastered" },
    [pscustomobject]@{ Id="kaykit-medieval-hexagon"; Name="KayKit Medieval Hexagon Pack 1.0"; Method="direct"; Source="https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0"; License="https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0/main/README.md"; Url="https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0/archive/refs/heads/main.zip"; Bytes=8354303; Sha256="abe5b0c00456ab66a82883a6b16e860c419d822db5597596efccb09e57820c42"; Destination="kaykit_medieval_hexagon" },
    [pscustomobject]@{ Id="kaykit-character-animations-free-1-1"; Name="KayKit Character Animations Free 1.1"; Method="itch"; Source="https://kaylousberg.itch.io/kaykit-character-animations"; License="https://kaylousberg.itch.io/kaykit-character-animations"; Bytes=14858957; Sha256="65882f31f905ad2e953819648a59287cdeab8f623908d5ef701971d3758be20f"; UploadId="15799903"; Destination="kaykit_character_animations" },
    [pscustomobject]@{ Id="quaternius-ual1-standard"; Name="Quaternius Universal Animation Library Standard"; Method="itch"; Source="https://quaternius.itch.io/universal-animation-library"; License="https://quaternius.itch.io/universal-animation-library"; Bytes=15904933; Sha256="cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724"; UploadId="17958403"; Destination="quaternius_ual1_standard" },
    [pscustomobject]@{ Id="quaternius-ual2-standard"; Name="Quaternius Universal Animation Library 2 Standard"; Method="itch"; Source="https://quaternius.itch.io/universal-animation-library-2"; License="https://quaternius.itch.io/universal-animation-library-2"; Bytes=18735003; Sha256="4008ea208a604773a2b2177d965f0f5d3195498b5bf838c3f5785d68e95f2a68"; UploadId="17958478"; Destination="quaternius_ual2_standard" }
)

function Expand-SafeZip {
    param([string]$ArchivePath,[string]$DestinationPath)
    $root = [IO.Path]::GetFullPath($DestinationPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($zip.Entries.Count -eq 0) { throw "ZIP archive has no entries: $ArchivePath" }
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $DestinationPath $entry.FullName))
            if (-not $target.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe ZIP entry path rejected: $($entry.FullName)"
            }
            if ($entry.FullName.EndsWith("/")) {
                New-Item -ItemType Directory -Force -Path $target | Out-Null
                continue
            }
            if (Test-Path -LiteralPath $target) { throw "Extraction would overwrite existing file: $target" }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            $inputStream=$entry.Open()
            try {
                $outputStream=[IO.File]::Open($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                try { $inputStream.CopyTo($outputStream) }
                finally { $outputStream.Dispose() }
            } finally { $inputStream.Dispose() }
        }
    } finally { $zip.Dispose() }
}

function Get-ItchFileUrl {
    param([string]$Source,[string]$UploadId)
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $sourcePage=Invoke-WebRequest -Uri $Source -WebSession $session -UseBasicParsing
    if ($sourcePage.Content -notmatch "(?i)CC0|Creative Commons Zero") {
        throw "The current itch.io source page does not explicitly declare CC0: $Source"
    }
    $purchase=Invoke-WebRequest -Uri "$Source/purchase" -WebSession $session -UseBasicParsing
    $csrf=[regex]::Match($purchase.Content,'<meta name="csrf_token" value="([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($csrf)) { throw "itch.io CSRF token missing: $Source" }
    $uri="$Source/file/$UploadId?source=game_download&as_props=1"
    $response=Invoke-WebRequest -Uri $uri -Method Post -Body @{csrf_token=$csrf} -WebSession $session -UseBasicParsing
    $payload=$response.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($payload.url)) { throw "itch.io did not return a signed download URL for upload $UploadId" }
    return [pscustomobject]@{Url=$payload.url;Session=$session}
}

$receipt=@()
foreach ($pack in $packs) {
    $archive=Join-Path $downloads ($pack.Id+".zip")
    $destination=Join-Path $incoming $pack.Destination
    $alreadyExtracted = $false
    if (-not $SkipExtraction -and (Test-Path -LiteralPath $destination)) {
        $existingReceipt = Join-Path $destination "SOURCE-RECEIPT.md"
        if (-not (Test-Path -LiteralPath $existingReceipt)) {
            throw "Destination exists without a source receipt; refusing to overwrite or trust it: $destination"
        }
        $receiptText = Get-Content -Raw -LiteralPath $existingReceipt
        if ($receiptText -notmatch [regex]::Escape($pack.Sha256)) {
            throw "Existing extraction receipt does not match the pinned SHA-256; inspect manually: $destination"
        }
        $alreadyExtracted = $true
    }

    if (Test-Path -LiteralPath $archive) {
        $existingHash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($existingHash -ne $pack.Sha256) { throw "Existing archive hash mismatch; refusing to overwrite: $archive" }
        if ((Get-Item -LiteralPath $archive).Length -ne $pack.Bytes) { throw "Existing archive byte size mismatch: $archive" }
    }
    else {
        $temporary="$archive.download"
        if ($pack.Method -eq "itch") {
            $download=Get-ItchFileUrl -Source $pack.Source -UploadId $pack.UploadId
            Invoke-WebRequest -Uri $download.Url -OutFile $temporary -WebSession $download.Session -UseBasicParsing
        }
        else {
            if ($pack.License -like "https://raw.githubusercontent.com/*") {
                $licenseText=(Invoke-WebRequest -Uri $pack.License -UseBasicParsing).Content
                if ($licenseText -notmatch "(?i)CC0|Creative Commons Zero") {
                    throw "The current official KayKit README no longer explicitly declares CC0: $($pack.License)"
                }
            }
            elseif ($pack.Source -like "https://kenney.nl/*") {
                $sourceText=(Invoke-WebRequest -Uri $pack.Source -UseBasicParsing).Content
                if ($sourceText -notmatch "(?i)Creative Commons CC0|CC0 1\.0") {
                    throw "The current official Kenney asset page no longer explicitly declares CC0: $($pack.Source)"
                }
            }
            Invoke-WebRequest -Uri $pack.Url -OutFile $temporary -UseBasicParsing
        }

        if (-not (Test-Path -LiteralPath $temporary)) { throw "Download output missing: $temporary" }
        $actualBytes=(Get-Item -LiteralPath $temporary).Length
        if ($actualBytes -ne $pack.Bytes) {
            Remove-Item -LiteralPath $temporary -Force
            throw "Archive byte count changed for $($pack.Id): expected $($pack.Bytes), got $actualBytes. Check upstream before proceeding."
        }
        $actualHash=(Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $pack.Sha256) {
            Remove-Item -LiteralPath $temporary -Force
            throw "Archive SHA-256 changed for $($pack.Id): expected $($pack.Sha256), got $actualHash. Check upstream before proceeding."
        }
        Move-Item -LiteralPath $temporary -Destination $archive
    }

    $actualHash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $actualBytes=(Get-Item -LiteralPath $archive).Length
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try { if ($zip.Entries.Count -eq 0) { throw "Empty ZIP: $archive" } }
    finally { $zip.Dispose() }

    if (-not $SkipExtraction -and -not $alreadyExtracted) {
        New-Item -ItemType Directory -Path $destination | Out-Null
        Expand-SafeZip -ArchivePath $archive -DestinationPath $destination
        $readme=@"
# $($pack.Name)

- Original source: $($pack.Source)
- Licence evidence: $($pack.License)
- Archive: $($pack.Id).zip
- Expected SHA-256 from curated 2026-08 snapshot: $($pack.Sha256)
- Verified SHA-256: $actualHash
- Byte count: $actualBytes
- Downloaded UTC: $([DateTime]::UtcNow.ToString("o"))

This is an original CC0 source package. It has not been imported into CS2, compiled or tested in-game. Retain this receipt with the pack. Verify model rig, materials, animation clips and/or texture contents before building Source 2 resources.
"@
        Set-Content -LiteralPath (Join-Path $destination "SOURCE-RECEIPT.md") -Value $readme -Encoding UTF8
    }

    $extractedPath = $null
    if ($SkipExtraction) {
        $itemStatus = "downloaded_source_only"
    }
    elseif ($alreadyExtracted) {
        $extractedPath = $destination
        $itemStatus = "existing_extraction_verified"
    }
    else {
        $extractedPath = $destination
        $itemStatus = "downloaded_and_safely_extracted_source_only"
    }

    $receipt += [pscustomobject]@{
        id=$pack.Id; name=$pack.Name; source=$pack.Source; license_evidence=$pack.License
        archive=$archive; bytes=$actualBytes; sha256=$actualHash
        extracted_to=$extractedPath; status=$itemStatus
    }
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $incoming "download-receipt.json") -Encoding UTF8
    Write-Host "Verified: $($pack.Name) ($actualBytes bytes)"
}

Write-Host ""
Write-Host "CC0 source intake completed: $($receipt.Count) archives."
Write-Host "Incoming directory: $incoming"
Write-Host "Receipt: $(Join-Path $incoming 'download-receipt.json')"
Write-Host "No files were copied into CS2 content/game folders. Model/VFX/audio compilation and private-server testing are separate steps."
