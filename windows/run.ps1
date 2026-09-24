$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1"
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $root '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path $localSdk) { $localSdk } else { 'dotnet' }
& $dotnet run --project "$PSScriptRoot/eScratch/eScratch.csproj" --no-build
