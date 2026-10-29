# Builds artifacts\FlipFace.exe: one self-contained file that runs without installing .NET.
# Usage: .\publish.ps1 [-Output <folder>]
param([string]$Output = (Join-Path $PSScriptRoot "artifacts"))

$ErrorActionPreference = "Stop"

dotnet publish (Join-Path $PSScriptRoot "src\FlipFace.App\FlipFace.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    --output $Output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Get-Item (Join-Path $Output "FlipFace.exe")
"Built {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)
