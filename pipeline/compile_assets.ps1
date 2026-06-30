# AETHERION — компиляция content -> game (.vmdl/.vmat/.vsnd)
# Запускать ПОСЛЕ установки Workshop Tools.
param([string]$Cs2 = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive")
$rc = Join-Path $Cs2 "game\bin\win64\resourcecompiler.exe"
if(-not (Test-Path $rc)){ Write-Host "❌ Workshop Tools не установлены — поставь DLC в Steam"; exit 1 }
$content = Join-Path $Cs2 "content\csgo_addons\aetherion"
Write-Host "Компилирую ассеты AETHERION..."
& $rc -i "$content\**\*" -r
Write-Host "✅ Готово. Скомпилированные ассеты в game/csgo_addons/aetherion"
