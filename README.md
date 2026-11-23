# Tecnico Brasfort

Painel CLI portátil (`.exe` em C# 5.0 / `.NET Framework 4.8`) destinado exclusivamente a técnicos e analistas de TI para execução de rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

## Pré-requisitos

*   **Execução em estações Windows:** Windows 10 / 11 ou Windows Server 2016+ (`.NET Framework 4.8` nativo).
*   **Compilação local:** Windows com `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (ou `.NET SDK` opcional).

## Build e Testes

```powershell
# Compilar apenas o executável principal em dist\TecnicoBrasfort.exe
powershell -ExecutionPolicy Bypass -File scripts/build.ps1

# Compilar e executar a suíte de testes unitários em dist\TecnicoBrasfort.Tests.exe
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests

# Compilar e assinar com certificado interno (AD CS / GPO)
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -CertThumbprint "<THUMBPRINT>"
```

## Configuração (`config.json`)

Copie `config.example.json` para `config.json` ao lado de `TecnicoBrasfort.exe` para informar o domínio e endpoints reais do ambiente sem expor dados internos no repositório:

```json
{
  "DomainFqdn": "corp.example.local",
  "DnsTestTargets": ["1.1.1.1", "8.8.8.8"],
  "ExchangeCheckUrl": "https://mail.example.local/owa",
  "LogDirectoryOverride": ""
}
```

## Uso em Rede vs. Pendrive

*   O executável solicita elevação UAC (`requireAdministrator`).
*   Ao executar pela rede, abra sempre pelo caminho **UNC** (`\\servidor\pasta\TecnicoBrasfort.exe`) ou via **pendrive**. Não utilize letras de unidade mapeadas (`Z:\`) da sessão do usuário, pois o Windows bloqueia o acesso antes da inicialização do processo elevado.
*   Os logs são gravados localmente em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` com cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`).
