# ============================================================================
# NillyTask Pro — Clean Uninstaller (Zero Admin · HKCU only)
# created by Nilly graphics (Sotiris Christos) — https://nillygraphics.com/
# ============================================================================
param([switch]$Silent)

$ErrorActionPreference = 'SilentlyContinue'
Get-Process NillyTaskPro -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$appKey = 'HKCU:\Software\Classes\Applications\NillyTaskPro.exe'
if (Test-Path $appKey) { Remove-Item -Path $appKey -Recurse -Force }
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'NillyTask Pro.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Nilly graphics\NillyTask Pro.lnk') -Force

if (-not $Silent) { Write-Host '  Το NillyTask Pro απεγκαταστάθηκε καθαρά.' -ForegroundColor Green }
