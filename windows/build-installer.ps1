param([string]$Compiler)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1" -Publish
if (!$Compiler) { $Compiler = Join-Path (Split-Path $PSScriptRoot -Parent) '.tools/inno/ISCC.exe' }
if (!(Test-Path $Compiler)) { throw 'Install Inno Setup 7 and pass -Compiler with the path to ISCC.exe.' }
$manifest = Get-Content "$PSScriptRoot/installer/prerequisites.json" -Raw | ConvertFrom-Json
[xml]$project = Get-Content "$PSScriptRoot/eScratch/eScratch.csproj"
$version = $project.Project.PropertyGroup.Version
& $Compiler "/DAppVersion=$version" "/DDesktopUrl=$($manifest.desktop.url)" "/DDesktopHash=$($manifest.desktop.sha256)" "/DVcUrl=$($manifest.vc.url)" "/DVcHash=$($manifest.vc.sha256)" "$PSScriptRoot/installer/eScratch.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
