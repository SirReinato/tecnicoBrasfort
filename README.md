# Tecnico Brasfort

Painel desktop WinForms portátil (`.exe` único em C# 5.0 / `.NET Framework 4.8`) destinado exclusivamente a técnicos e analistas de TI para execução de rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

## Pré-requisitos

*   **Execução em estações Windows:** Windows 10 / 11 ou Windows Server 2016+ (`.NET Framework 4.8` nativo).
*   **Compilação local:** Windows com `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (ou `.NET SDK` opcional).

## Recursos da v2 (Painel GUI)

*   **Autenticação fail-closed e elevação assistida:** Solicita senha na abertura do painel validada contra hash PBKDF2-SHA256 com salt configurado no `config.json` (com opção `"SkipAccessGateWhenElevated": true` para dispensar dupla senha após elevação administrativa válida). Em estações com UAC desativado (`EnableLUA = 0`), detecta falta de privilégio e solicita credenciais de TI inline antes de abrir o painel.
*   **Seleção livre + 5 Combos:** Permite marcar qualquer conjunto de ações por checkboxes ou aplicar um dos 5 combos pré-montados (incluindo *Limpeza Lógica / Travamentos* na ordem `Temp -> DISM /RestoreHealth -> SFC -> WinSxS`).
*   **Botão `[?]` em todas as ações:** Explica o que a rotina faz, seu impacto e exibe o comando CMD/PowerShell equivalente com botão para copiar.
*   **Modo segundo plano visível:** Minimiza a janela de progresso para um banner compacto no canto da tela (*"Suporte TI Brasfort em andamento — Não desligue o computador"*) + ícone na bandeja.
*   **Modo Técnico Ausente:** Botão na tela de execução em andamento que fecha permanentemente o painel principal (`MainForm`), mantendo apenas o progresso/resumo visível até o fechamento final da janela (quando todo o aplicativo é encerrado).
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
2.  Copie [config.example.json](./config.example.json) para `config.json` ao lado do `.exe`, preenchendo o domínio/Exchange da rede e colando os campos `RequireAccessPassword`, `AccessSaltBase64`, `AccessHashBase64` e `AccessIterations` gerados pelo script. Em produção, defina `"SkipAccessGateWhenElevated": true` se desejar evitar digitação dupla de senha após autenticar com uma conta administrativa.
3.  Restrinja as permissões **NTFS / Share** da pasta compartilhada na rede ao grupo de técnicos de TI.

## Uso em Rede, UAC Desativado (`EnableLUA = 0`) e Pendrive

*   O executável solicita elevação UAC (`requireAdministrator`). Em estações onde o UAC está desativado (`EnableLUA = 0`), o aplicativo detecta automaticamente que abriu sem privilégio de Administrador, solicita uma credencial pertencente ao grupo local `Administrators` (ex.: `Workstation Administrators` via GPO) e se relança elevado — exigindo que o serviço *Secondary Logon* (`seclogon`) esteja habilitado na máquina.
*   Ao executar pela rede, abra sempre pelo caminho **UNC** (`\\servidor\pasta\TecnicoBrasfort.exe`) ou via **pendrive**. Em estações com `EnableLUA = 1`, o Windows bloqueia letras de unidade mapeadas (`Z:\`) antes da inicialização do processo elevado; em estações com `EnableLUA = 0`, o relançamento resolve o mapeamento para UNC via `WNetGetConnectionW` sempre que possível.
*   Os logs e resumos de fechamento são gravados localmente em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` com cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`).
