param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

$ErrorActionPreference = "Stop"

function Get-EnvValue([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    return $value.Trim()
}

function Get-SignTool {
    $configured = Get-EnvValue "SIGNTOOL_PATH"
    if ($configured) {
        if (-not (Test-Path $configured)) {
            throw "SIGNTOOL_PATH foi configurado, mas o arquivo não existe: $configured"
        }
        return (Resolve-Path $configured).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $kitsRoot = Join-Path $programFilesX86 "Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $candidate = Get-ChildItem -Path $kitsRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe não foi encontrado. Instale o Windows SDK ou defina SIGNTOOL_PATH."
}

if (-not (Test-Path $FilePath)) {
    throw "Arquivo para assinatura não encontrado: $FilePath"
}

$FilePath = (Resolve-Path $FilePath).Path

$thumbprint = Get-EnvValue "CODE_SIGN_CERT_THUMBPRINT"
$pfxPath = Get-EnvValue "CODE_SIGN_PFX_PATH"
$pfxBase64 = Get-EnvValue "CODE_SIGN_PFX_BASE64"
$pfxPassword = [Environment]::GetEnvironmentVariable("CODE_SIGN_PFX_PASSWORD")
$timestampUrl = Get-EnvValue "CODE_SIGN_TIMESTAMP_URL"
$certStore = Get-EnvValue "CODE_SIGN_CERT_STORE"
$required = (Get-EnvValue "CODE_SIGN_REQUIRED") -match '^(1|true|yes|sim)$'

if (-not $timestampUrl) {
    $timestampUrl = "http://timestamp.digicert.com"
}

if (-not $certStore) {
    $certStore = "CurrentUser"
}

$hasSigningConfig = $thumbprint -or $pfxPath -or $pfxBase64

if (-not $hasSigningConfig) {
    if ($required) {
        throw "Assinatura digital é obrigatória (CODE_SIGN_REQUIRED), mas nenhum certificado foi configurado."
    }

    Write-Host ""
    Write-Host "Assinatura digital: NÃO CONFIGURADA." -ForegroundColor Yellow
    Write-Host "O EXE será mantido sem assinatura até que um certificado de Code Signing seja configurado."
    Write-Host "Configure CODE_SIGN_CERT_THUMBPRINT, CODE_SIGN_PFX_PATH ou CODE_SIGN_PFX_BASE64."
    return
}

$signtool = Get-SignTool
$tempPfx = $null
$importedCertificates = @()
$importedFromPfx = $false

try {
    if (-not $thumbprint) {
        if ($pfxBase64) {
            if ([string]::IsNullOrEmpty($pfxPassword)) {
                throw "CODE_SIGN_PFX_BASE64 foi definido, mas CODE_SIGN_PFX_PASSWORD está vazio."
            }

            try {
                $bytes = [Convert]::FromBase64String($pfxBase64)
            }
            catch {
                throw "CODE_SIGN_PFX_BASE64 não contém Base64 válido."
            }

            $tempPfx = Join-Path $env:TEMP ("hikvision_codesign_{0}.pfx" -f [Guid]::NewGuid().ToString("N"))
            [IO.File]::WriteAllBytes($tempPfx, $bytes)
            $pfxPath = $tempPfx
        }

        if (-not $pfxPath -or -not (Test-Path $pfxPath)) {
            throw "O arquivo PFX configurado não foi encontrado."
        }

        if ([string]::IsNullOrEmpty($pfxPassword)) {
            throw "CODE_SIGN_PFX_PASSWORD é obrigatório para assinatura usando PFX."
        }

        $securePassword = ConvertTo-SecureString $pfxPassword -AsPlainText -Force
        $importedCertificates = @(Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation "Cert:\CurrentUser\My" -Password $securePassword -Exportable:$false)

        $signingCert = $importedCertificates |
            Where-Object { $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1

        if (-not $signingCert) {
            throw "O PFX foi importado, mas nenhum certificado com chave privada foi encontrado."
        }

        $thumbprint = $signingCert.Thumbprint
        $certStore = "CurrentUser"
        $importedFromPfx = $true
    }

    $thumbprint = ($thumbprint -replace '\s', '').ToUpperInvariant()

    Write-Host ""
    Write-Host "Assinando executável..." -ForegroundColor Cyan
    Write-Host "Arquivo: $FilePath"
    Write-Host "Thumbprint: $thumbprint"
    Write-Host "Timestamp: $timestampUrl"

    $signArgs = @(
        "sign",
        "/fd", "SHA256",
        "/td", "SHA256",
        "/tr", $timestampUrl,
        "/sha1", $thumbprint
    )

    if ($certStore -ieq "LocalMachine") {
        $signArgs += "/sm"
    }
    elseif ($certStore -ine "CurrentUser") {
        throw "CODE_SIGN_CERT_STORE deve ser CurrentUser ou LocalMachine."
    }

    $signArgs += $FilePath

    & $signtool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign falhou com código $LASTEXITCODE."
    }

    Write-Host ""
    Write-Host "Verificando assinatura Authenticode..." -ForegroundColor Cyan

    & $signtool verify /pa /all /v $FilePath
    if ($LASTEXITCODE -ne 0) {
        throw "A assinatura foi aplicada, mas a validação Authenticode falhou com código $LASTEXITCODE."
    }

    $authenticode = Get-AuthenticodeSignature -FilePath $FilePath
    if ($authenticode.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Get-AuthenticodeSignature retornou status '$($authenticode.Status)': $($authenticode.StatusMessage)"
    }

    Write-Host ""
    Write-Host "Assinatura digital válida." -ForegroundColor Green
    Write-Host "Produtor: $($authenticode.SignerCertificate.Subject)"
    Write-Host "Emissor:   $($authenticode.SignerCertificate.Issuer)"
    Write-Host "Validade:  $($authenticode.SignerCertificate.NotBefore) até $($authenticode.SignerCertificate.NotAfter)"
}
finally {
    if ($importedFromPfx) {
        foreach ($certificate in $importedCertificates) {
            try {
                $certPath = "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
                if (Test-Path $certPath) {
                    Remove-Item $certPath -Force
                }
            }
            catch {
                Write-Warning "Não foi possível remover um certificado temporário do repositório CurrentUser."
            }
        }
    }

    if ($tempPfx -and (Test-Path $tempPfx)) {
        Remove-Item $tempPfx -Force
    }
}
