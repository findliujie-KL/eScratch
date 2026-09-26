$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1"
# Framework builds run directly, without a dotnet.exe host.
& "$PSScriptRoot/eScratch/bin/Debug/net48/eScratch.exe"
