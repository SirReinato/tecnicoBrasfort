param(
    [int]$Iterations = 100000
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ExePath = Join-Path $RepoRoot "dist\TecnicoBrasfort.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "Executavel nao encontrado em $ExePath. Compilando projeto..." -ForegroundColor Yellow
    $BuildScript = Join-Path $PSScriptRoot "build.ps1"
    & $BuildScript
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao compilar TecnicoBrasfort.exe."
    }
}

[void][System.Reflection.Assembly]::LoadFrom($ExePath)

$AccessGateType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType("TecnicoBrasfort.Core.AccessGate", $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $AccessGateType) {
    throw "Classe TecnicoBrasfort.Core.AccessGate ainda nao existe no executavel (implementada na SIR-147)."
}

$SecurePwd = Read-Host "Digite a senha de acesso do Tecnico Brasfort" -AsSecureString
$Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePwd)
$PlainPwd = $null

try {
    $PlainPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto($Bstr)
    if ([string]::IsNullOrWhiteSpace($PlainPwd)) {
        throw "A senha nao pode ser vazia."
    }

    $SaltBase64 = ""
    $HashBase64 = ""
    [TecnicoBrasfort.Core.AccessGate]::GenerateHash($PlainPwd, $Iterations, [ref]$SaltBase64, [ref]$HashBase64)

    Write-Host ""
    Write-Host "Copie os campos abaixo para o arquivo config.json ao lado do executavel:" -ForegroundColor Green
    $Snippet = [ordered]@{
        RequireAccessPassword = $true
        AccessSaltBase64      = $SaltBase64
        AccessHashBase64      = $HashBase64
        AccessIterations      = $Iterations
    }
    $Snippet | ConvertTo-Json
}
finally {
    if ($Bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr)
    }
    $PlainPwd = $null
}
