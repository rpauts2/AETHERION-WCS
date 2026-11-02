$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Host "[AETHERION] Validating visual configuration..."
python scripts/validate_visual_assets.py
if ($LASTEXITCODE -ne 0) {
    throw "Visual configuration validation failed."
}

Write-Host "[AETHERION] Restoring .NET dependencies..."
dotnet restore AetherionWcs.csproj
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

Write-Host "[AETHERION] Building Release..."
dotnet build AetherionWcs.csproj --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

$Dll = Join-Path $RepoRoot "bin\Release\net8.0\AetherionWcs.dll"
if (-not (Test-Path $Dll)) {
    throw "Build command succeeded but expected output was not found: $Dll"
}

Write-Host "[AETHERION] Build succeeded: $Dll"
