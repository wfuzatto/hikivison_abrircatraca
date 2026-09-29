$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\HikvisionAbrirCatraca\HikvisionAbrirCatraca.csproj"
$out = Join-Path $PSScriptRoot "dist"
$exe = Join-Path $out "HikvisionAbrirCatraca.exe"
$signScript = Join-Path $PSScriptRoot "scripts\sign.ps1"

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

Write-Host "Restaurando dependências..." -ForegroundColor Cyan
dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore falhou com código $LASTEXITCODE."
}

Write-Host ""
Write-Host "Publicando Windows x64 self-contained single-file..." -ForegroundColor Cyan
$publishArgs = @(
    "publish",
    $project,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $out
)
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falhou com código $LASTEXITCODE."
}

if (-not (Test-Path $exe)) {
    throw "O publish terminou, mas o executável não foi encontrado: $exe"
}

if (-not (Test-Path $signScript)) {
    throw "Script de assinatura não encontrado: $signScript"
}

& $signScript -FilePath $exe

Write-Host ""
$signature = Get-AuthenticodeSignature -FilePath $exe
if ($signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid) {
    Write-Host "Assinatura: VÁLIDA" -ForegroundColor Green
    Write-Host "Produtor:   $($signature.SignerCertificate.Subject)"
}
else {
    Write-Host "Assinatura: AUSENTE / NÃO VALIDADA ($($signature.Status))" -ForegroundColor Yellow
}

$file = Get-Item $exe

Write-Host ""
Write-Host "Pronto:" -ForegroundColor Green
Write-Host $exe
Write-Host ("Tamanho: {0:N2} MB" -f ($file.Length / 1MB))
Write-Host ("Data:    {0}" -f $file.LastWriteTime)
