param(
    [switch]$RunTests,
    [string]$CertThumbprint = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $RepoRoot "src\TecnicoBrasfort"
$TestsDir = Join-Path $RepoRoot "tests\TecnicoBrasfort.Tests"
$DistDir = Join-Path $RepoRoot "dist"
$ManifestPath = Join-Path $SrcDir "app.manifest"
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

$SrcFiles = Get-ChildItem -Path $SrcDir -Filter "*.cs" -Recurse
if (-not $SrcFiles -or $SrcFiles.Count -eq 0) {
    Write-Host "Nenhum arquivo .cs encontrado em $SrcDir. Estrutura de build validada."
    exit 0
}

if (-not (Test-Path $CscPath)) {
    throw "Compilador csc.exe nao encontrado em: $CscPath"
}

$BclRefs = @(
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Management.dll",
    "/r:System.Runtime.Serialization.dll",
    "/r:System.Xml.dll",
    "/r:System.ServiceProcess.dll",
    "/r:System.Net.Http.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll"
)

$ExeOut = Join-Path $DistDir "TecnicoBrasfort.exe"
$CscArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/warnaserror+",
    "/win32manifest:$ManifestPath",
    "/out:$ExeOut"
) + $BclRefs + @("/recurse:$SrcDir\*.cs")

Write-Host "Compilando TecnicoBrasfort.exe via csc.exe (C# 5.0 / net48)..."
& $CscPath @CscArgs
if ($LASTEXITCODE -ne 0) {
    throw "Falha na compilacao do TecnicoBrasfort.exe (exit code $LASTEXITCODE)"
}

if ($CertThumbprint -ne "") {
    $SignTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $SignTool) {
        throw "signtool.exe nao encontrado no PATH para assinatura Authenticode."
    }
    Write-Host "Assinando $ExeOut com certificado $CertThumbprint..."
    & $SignTool.Source sign /fd SHA256 /sha1 $CertThumbprint /tr http://timestamp.digicert.com /td SHA256 $ExeOut
    if ($LASTEXITCODE -ne 0) {
        throw "Falha na assinatura digital (exit code $LASTEXITCODE)"
    }
}

$Hash = (Get-FileHash -Path $ExeOut -Algorithm SHA256).Hash.ToLowerInvariant()
$SumFile = Join-Path $DistDir "SHA256SUMS.txt"
"$Hash  TecnicoBrasfort.exe" | Out-File -FilePath $SumFile -Encoding ascii
Write-Host "Build concluido: $ExeOut (SHA256: $Hash)"

if ($RunTests) {
    $TestFiles = Get-ChildItem -Path $TestsDir -Filter "*.cs" -Recurse
    if (-not $TestFiles -or $TestFiles.Count -eq 0) {
        throw "Nenhum arquivo de teste encontrado em $TestsDir"
    }

    $AppSourcesWithoutProgram = Get-ChildItem -Path $SrcDir -Filter "*.cs" -Recurse |
        Where-Object { $_.Name -ne "Program.cs" -and $_.Directory.Name -ne "UI" } |
        ForEach-Object { $_.FullName }

    $TestSourcePaths = $TestFiles | ForEach-Object { $_.FullName }
    $TestsExeOut = Join-Path $DistDir "TecnicoBrasfort.Tests.exe"

    $TestCscArgs = @(
        "/nologo",
        "/target:exe",
        "/warnaserror+",
        "/main:TecnicoBrasfort.Tests.TestRunner",
        "/out:$TestsExeOut"
    ) + $BclRefs + $AppSourcesWithoutProgram + $TestSourcePaths

    Write-Host "Compilando suite de testes TecnicoBrasfort.Tests.exe..."
    & $CscPath @TestCscArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Falha na compilacao dos testes (exit code $LASTEXITCODE)"
    }

    Write-Host "Executando TecnicoBrasfort.Tests.exe..."
    & $TestsExeOut
    if ($LASTEXITCODE -ne 0) {
        throw "Testes falharam com exit code $LASTEXITCODE"
    }
}
