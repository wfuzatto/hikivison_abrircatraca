$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\HikvisionAbrirCatraca\HikvisionAbrirCatraca.csproj"
$out = Join-Path $PSScriptRoot "dist"

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

dotnet restore $project
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out

Write-Host ""
Write-Host "Pronto:" -ForegroundColor Green
Write-Host (Join-Path $out "HikvisionAbrirCatraca.exe")
