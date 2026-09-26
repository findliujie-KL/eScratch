param([switch]$Publish, [switch]$SelfContained)
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
if ($Publish) {
    $flavor = if ($SelfContained) { 'standalone' } else { 'compact' }
    $output = Join-Path $PSScriptRoot "artifacts/$flavor"
    & $dotnet publish $project -c Release -r win-x64 --self-contained $SelfContained.ToString().ToLowerInvariant() -p:PublishSingleFile=false -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Compress-Archive -Path "$output/*" -DestinationPath (Join-Path $PSScriptRoot "artifacts/eScratch-windows-x64-$flavor.zip") -Force
} else {
    & $dotnet build $project -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
