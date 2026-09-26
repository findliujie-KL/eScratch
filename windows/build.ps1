param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $root '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path $localSdk) { $localSdk } else { 'dotnet' }
$model = Join-Path $PSScriptRoot 'eScratch/Assets/eng.traineddata'
if (!(Test-Path $model)) {
    New-Item -ItemType Directory -Force (Split-Path $model) | Out-Null
    $temporary = "$model.download"
    try {
        Invoke-WebRequest 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata' -OutFile $temporary
        if ((Get-Item $temporary).Length -lt 1024) { throw 'English OCR model download was incomplete.' }
        Move-Item -LiteralPath $temporary -Destination $model
    } finally { if (Test-Path $temporary) { Remove-Item -LiteralPath $temporary } }
}
$project = Join-Path $PSScriptRoot 'eScratch/eScratch.csproj'
$configuration = if ($Publish) { 'Release' } else { 'Debug' }
& $dotnet build $project -c $configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Publish) {
    $source = Join-Path $PSScriptRoot 'eScratch/bin/Release/net48'
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts/net48'))
    $output = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'portable'))
    # Only clean this branch's generated output; never mix in old .NET 10 files.
    if (!$output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package output path.' }
    if (Test-Path -LiteralPath $output) {
        if ((Get-Item -LiteralPath $output).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Package output must not be a link.' }
        Remove-Item -LiteralPath $output -Recurse -Force
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.exe', '.config', '.dll', '.txt' } | Copy-Item -Destination $output
    Copy-Item -LiteralPath (Join-Path $source 'Assets') -Destination $output -Recurse
    Copy-Item -LiteralPath (Join-Path $source 'x64') -Destination $output -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PORTABLE-README.txt') -Destination (Join-Path $output 'README.txt')
    if (!(Test-Path (Join-Path $output 'eScratch.exe.config'))) { throw 'Framework configuration is missing.' }
    if (Test-Path (Join-Path $output 'coreclr.dll')) { throw 'Unexpected modern .NET runtime in Framework package.' }
    [xml]$manifest = Get-Content $project
    $version = $manifest.Project.PropertyGroup.Version
    $zip = Join-Path $artifactRoot "eScratch-$version-net48-win-x64-portable.zip"
    Compress-Archive -Path "$output/*" -DestinationPath $zip -Force
    Write-Output "Portable package: $zip"
}
