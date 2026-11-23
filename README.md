# Tecnico Brasfort

Painel desktop WinForms portátil (`.exe` único em C# 5.0 / `.NET Framework 4.8`) destinado exclusivamente a técnicos e analistas de TI para execução de rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

## Pré-requisitos

*   **Execução em estações Windows:** Windows 10 / 11 ou Windows Server 2016+ (`.NET Framework 4.8` nativo).
*   **Compilação local:** Windows com `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (ou `.NET SDK` opcional).

## Recursos da v2 (Painel GUI)

*   **Autenticação fail-closed:** Solicita senha na abertura do painel validada contra hash PBKDF2-SHA256 com salt configurado no `config.json`.
*   **Seleção livre + 5 Combos:** Permite marcar qualquer conjunto de ações por checkboxes ou aplicar um dos 5 combos pré-montados (incluindo *Limpeza Lógica / Travamentos* na ordem `Temp -> DISM /RestoreHealth -> SFC -> WinSxS`).
*   **Botão `[?]` em todas as ações:** Explica o que a rotina faz, seu impacto e exibe o comando CMD/PowerShell equivalente com botão para copiar.
*   **Modo segundo plano visível:** Minimiza a janela de progresso para um banner compacto no canto da tela (*"Suporte TI Brasfort em andamento — Não desligue o computador"*) + ícone na bandeja.
*   **Fechamento de chamado e aviso de reinício:** Grava automaticamente o resumo de fechamento junto aos logs e destaca ao final se a máquina precisa ser reiniciada.

## Build e Testes

```powershell
# Compilar o executável WinForms em dist\TecnicoBrasfort.exe
powershell -ExecutionPolicy Bypass -File scripts/build.ps1

# Compilar e executar a suíte de testes unitários headless em dist\TecnicoBrasfort.Tests.exe
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests

# Compilar e assinar com certificado interno (AD CS / GPO)
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -CertThumbprint "<THUMBPRINT>"
```

## Configuração (`config.json`) e Senha de Acesso

Por segurança (política **fail-closed**), o executável exige um `config.json` válido ao lado de `TecnicoBrasfort.exe` com o hash PBKDF2-SHA256 da senha de acesso.

1.  Gere o bloco JSON da senha localmente (sem expor a senha em texto claro):
    ```powershell
    powershell -ExecutionPolicy Bypass -File scripts/New-AccessPasswordHash.ps1
    ```
2.  Copie [config.example.json](./config.example.json) para `config.json` ao lado do `.exe`, preenchendo o domínio/Exchange da rede e colando os campos `RequireAccessPassword`, `AccessSaltBase64`, `AccessHashBase64` e `AccessIterations` gerados pelo script.
3.  Restrinja as permissões **NTFS / Share** da pasta compartilhada na rede ao grupo de técnicos de TI.

## Uso em Rede vs. Pendrive

*   O executável solicita elevação UAC (`requireAdministrator`).
*   Ao executar pela rede, abra sempre pelo caminho **UNC** (`\\servidor\pasta\TecnicoBrasfort.exe`) ou via **pendrive**. Não utilize letras de unidade mapeadas (`Z:\`) da sessão do usuário, pois o Windows bloqueia o acesso antes da inicialização do processo elevado.
*   Os logs e resumos de fechamento são gravados localmente em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` com cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`).
