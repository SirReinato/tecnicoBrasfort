# Painel CLI de Suporte TI — Tecnico Brasfort

*   **Author:** Renato França
*   **Status:** Draft
*   **Date:** 2026-11-23

--------------------------------------------------------------------------------

## Overview

O **Tecnico Brasfort** é uma ferramenta portátil de linha de comando (CLI), compilada em `.exe` (C# / .NET Framework 4.8), destinada exclusivamente a técnicos e analistas de TI da Brasfort para executar rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

A ferramenta é distribuída via pendrive ou pasta de rede compartilhada e executada sobre a sessão ativa do usuário final mediante elevação UAC com credenciais administrativas do técnico. O projeto segue integralmente as regras de governança adaptadas em `AGENTS.md` (isolamento de artefatos em `00-anything/`, fluxo Git por branch, decomposição em issues, configuração genérica sem dados internos hardcoded, `graphify` e testes unitários com mocks).

## The Problem

Em atendimentos presenciais e remotos, os técnicos de TI executam manualmente comandos dispersos do Windows e do Active Directory, o que gera inconsistência entre atendimentos, falta de histórico do que foi feito em cada máquina e falhas operacionais específicas do ambiente corporativo:

1.  **Quebra de contexto na elevação UAC ("Over-the-Shoulder"):** Quando o usuário final está logado e o técnico executa uma ferramenta como Administrador usando uma conta administrativa separada, o Windows cria outra sessão de logon. Unidades mapeadas (`Z:\`) desaparecem, o diretório atual muda para `C:\Windows\System32` e variáveis como `%TEMP%`, `%USERPROFILE%` e `HKCU` passam a apontar para o perfil do técnico admin em vez do usuário logado.
2.  **Queda de execução via compartilhamento UNC:** Ao rodar um utilitário diretamente de uma pasta de rede (`\\servidor\pasta\...`) e executar um reset de rede (`netsh int ip reset`, `ipconfig /release`), a conexão SMB cai no meio do processo, podendo travar o executável e impedir o registro de logs.
3.  **Bloqueios de Defender/EDR em wrappers:** Empacotar scripts PowerShell com `ps2exe` gera alta taxa de falso positivo em antivírus/EDR e quebra referências de caminho relativo após elevação.
4.  **Ausência de trilha de auditoria portátil:** Não há registro padronizado por estação das ações executadas e dos códigos de saída retornados, e gravar apenas em pasta de rede ou pendrive falha quando a mídia está somente leitura ou quando dois técnicos atuam simultaneamente.

## Goals

*   Entregar um executável `.exe` portátil em C# / .NET Framework 4.8 (`net48`) com tamanho abaixo de 1 MB, capaz de rodar em Windows 10/11 e Windows Server 2016+ sem exigir instalação prévia de runtime.
*   Tratar elevação UAC com conta administrativa separada, identificando explicitamente o usuário interativo logado na sessão para operações de perfil (temporários de usuários e impressoras mapeadas em `HKU\<SID>`).
*   Proteger execuções originadas de pasta UNC realizando staging local automático em `C:\ProgramData\Brasfort\TecnicoBrasfort\bin` antes de executar rotinas que reiniciam interfaces de rede.
*   Cobrir as 7 categorias operacionais do painel (Manutenção do Sistema, Rede e Internet, Impressão, Diagnóstico, Drivers e Atualizações, AD/GPO/Exchange e Auditoria/Logs) com streaming de saída em tempo real e confirmação explícita `[S/N]` para ações destrutivas ou que agendam reinicialização.
*   Garantir rastreabilidade por meio de escrita dupla de logs: arquivo primário local em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` e cópia de melhor esforço na Área de Trabalho (com fallback silencioso caso haja bloqueio de permissão ou redirecionamento OneDrive).
*   Cumprir integralmente o `AGENTS.md` do projeto: zero dados internos hardcoded no código (usando `config.example.json`), testes unitários isolados via mocks (`IProcessRunner`, `IFileSystem`), atualização contínua de `graphify` e organização de rascunhos em `00-anything/`.

## Non-Goals

*   Uso autônomo ou self-service por usuários finais.
*   Integrações com Microsoft Entra ID, Microsoft Intune ou limpeza de cache do Microsoft Teams (removidos do escopo por definição do autor).
*   Armazenamento de senhas administrativas, tokens, IPs internos ou nomes reais de servidores da Brasfort no código-fonte ou no binário `.exe`.
*   Instalação de runtimes externos (.NET 8 Desktop Runtime) ou download de módulos PowerShell externos (`PSWindowsUpdate`) nas máquinas atendidas.

## Open Questions

Nenhuma questão arquitetural em aberto no momento. Decisões fechadas para o `Technical Plan`:
*   **Dependências em runtime:** Somente BCL nativa do `.NET Framework 4.8` (zero NuGet no executável principal; `xUnit` e mocks restritos ao projeto de testes).
*   **Destino da cópia de log na Área de Trabalho:** `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (evita conflito de perfil UAC e redirecionamento OneDrive), mantendo `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` como fonte primária.
*   **Saúde de disco (SMART):** WMI/CIM nativo na v1, informando claramente quando a controladora RAID/USB ocultar telemetria.
*   **Assinatura Authenticode:** Build preparado para `signtool.exe` opcional quando houver certificado interno via AD CS/GPO.

## Critical User Journeys (CUJs)

### CUJ 1: Atendimento presencial via pendrive com elevação UAC sobre a sessão do usuário

*   **Given**: O usuário final está logado em sua estação Windows 10/11 e o técnico conecta um pendrive contendo `TecnicoBrasfort.exe`.
*   **When**: O técnico executa `TecnicoBrasfort.exe`, fornece suas credenciais de administrador no prompt UAC, visualiza no cabeçalho o usuário interativo detectado, executa a limpeza de arquivos temporários e depois roda o SFC (`/scannow`).
*   **Then**: O painel limpa `C:\Windows\Temp` e `C:\Users\*\AppData\Local\Temp` (ignorando arquivos bloqueados sem travar), exibe a saída do `sfc /scannow` em tempo real com resumo do código de saída em português, grava a entrada no log local em `ProgramData` e salva a cópia na Área de Trabalho configurada.

### CUJ 2: Atendimento via pasta de rede (UNC) com Reset de Rede

*   **Given**: O técnico acessa o compartilhamento de rede corporativo (`\\servidor\ti\TecnicoBrasfort.exe`) na máquina de um usuário com falha de conectividade.
*   **When**: O técnico inicia o executável (que detecta a origem UNC), seleciona a opção de Reset de Rede (`IP`, `Winsock` e `DNS`) e confirma o prompt `[S/N]`.
*   **Then**: O programa já está rodando a partir da cópia local em `C:\ProgramData\Brasfort\TecnicoBrasfort\bin`, executa os comandos `ipconfig` e `netsh` sem travar quando a conexão SMB cai, registra o resultado no log local em `ProgramData` e avisa de forma controlada caso a cópia secundária em rede não esteja acessível.

### CUJ 3: Diagnóstico de AD/GPO/Exchange e consulta ao histórico de ações

*   **Given**: Uma estação apresenta falha de aplicação de GPO ou erro de conexão com o Exchange corporativo.
*   **When**: O técnico acessa a categoria AD/GPO/Exchange para rodar `gpupdate /target:computer /force`, verificação de canal seguro (`nltest`), checagem de relógio (`w32tm`) e teste HTTPS no endpoint configurado no `config.json`, e em seguida abre a opção "Ver registro de ações deste PC".
*   **Then**: O painel exibe os diagnósticos de domínio, alerta se o desvio de relógio comprometer Kerberos, reporta o status HTTP/TCP do Exchange e lista o histórico de ações executadas naquela estação a partir dos logs em `ProgramData`.

## Technical Plan (Architecture)

### Arquitetura e Fluxo de Dados

```text
TecnicoBrasfort.exe (app.manifest: requireAdministrator)
  │
  ▼
Program.cs
  ├─► ConfigLoader (DataContractJsonSerializer nativo -> config.json ou defaults genéricos)
  ├─► EnvironmentContext (Admin elevado, Usuário interativo via Win32_ComputerSystem + SID, Origem)
  │     │
  │     └─[Se origem == UNC]──► UncStager: copia .exe + config.json para
  │                             C:\ProgramData\Brasfort\TecnicoBrasfort\bin\
  │                             e relança com --origin-path "<UNC>"
  │
  ├─► AuditLogger (Primário: C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\<PC>_<Data>.log
  │                Secundário: C:\Users\Public\Desktop\TecnicoBrasfort_Logs\<PC>_<Data>_<Hora>_<Tech>.log)
  │
  └─► ConsoleMenu (UI pura: cabeçalho de contexto, menu numerado, prompts [S/N])
        │
        └─► Modules/* ──► IProcessRunner / IFileSystem / IRegistryReader
                              └─► ProcessRunner + ArgumentEscaper (streaming stdout/stderr + timeout)
```

### Decisão de Zero NuGet em Runtime (`net48` BCL-only)

*   O projeto principal `TecnicoBrasfort.csproj` (`TargetFramework: net48`) utiliza exclusivamente assemblies nativos do `.NET Framework 4.8` (`System`, `System.Core`, `System.Management`, `System.Runtime.Serialization`, `System.ServiceProcess`, `System.Net.Http`).
*   Isso garante distribuição como um único `.exe` abaixo de 1 MB sem DLLs auxiliares ao lado e sem loaders de empacotamento que elevam falsos positivos em antivírus/EDR.
*   Pacotes NuGet (`xUnit`, `xunit.runner.visualstudio`, biblioteca de mocks) ficam isolados exclusivamente em `tests/TecnicoBrasfort.Tests`.

### Contratos da Camada `Core/`

1.  **`IProcessRunner`, `ProcessRunner` e `ArgumentEscaper`:**
    *   O `.NET Framework 4.8` não possui `ProcessStartInfo.ArgumentList` (disponível apenas a partir do .NET Core/.NET 5+). Por esse motivo, toda execução externa usa `ProcessStartInfo.Arguments` montado por um `ArgumentEscaper` estrito com validação de parâmetros, `UseShellExecute = false`, `RedirectStandardOutput = true` e `RedirectStandardError = true`. *(Nota: o `AGENTS.md` será atualizado durante a implementação para refletir o uso de `ArgumentEscaper` no lugar de `ArgumentList`.)*
    *   A saída é transmitida em tempo real via eventos `OutputDataReceived` e `ErrorDataReceived` para que comandos longos (`sfc`, `dism`) exibam progresso e tempo decorrido sem travar o console.
    *   Retorna `ProcessResult` (`ExitCode`, `Duration`, `TimedOut`, `StandardOutput`, `StandardError`, `Summary`).
2.  **`IFileSystem` e `IRegistryReader`:**
    *   Abstraem operações de leitura/exclusão de arquivos, checagem de espaço em disco (`DriveInfo`) e leitura de chaves `HKU\<UserSID>\Printers\Connections`, permitindo testes unitários completos sem alterar o sistema do desenvolvedor.
3.  **`EnvironmentContext` e `UncStager`:**
    *   Captura `Assembly.GetExecutingAssembly().Location` e o argumento opcional `--origin-path`.
    *   Resolve o usuário interativo logado na sessão física/RDP consultando `Win32_ComputerSystem.UserName` e convertendo para `SecurityIdentifier` (SID) via `NTAccount.Translate(typeof(SecurityIdentifier))`.
    *   Quando `IsUncOrigin` é verdadeiro e o processo ainda não está no diretório de staging, `UncStager` copia o binário (e `config.json`, se existir) para `C:\ProgramData\Brasfort\TecnicoBrasfort\bin\` e inicia a cópia local passando `--origin-path`. Isso evita queda do processo caso o técnico execute Reset de Rede.
4.  **`AuditLogger`:**
    *   Escrita dupla: sempre grava no log local primário em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\<HOSTNAME>_<YYYY-MM-DD>.log`.
    *   Em seguida, tenta gravar uma cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\<HOSTNAME>_<YYYY-MM-DD>_<HHmmss>_<TechAdmin>.log` (ou em `LogDirectoryOverride`, se configurado). Falhas na cópia secundária (permissão, disco cheio, mídia somente leitura) emitem apenas um aviso no console e não interrompem o atendimento.

### Especificação dos Módulos (`Modules/`)

1.  **`MaintenanceModule`:**
    *   **Temporários:** Limpa `C:\Windows\Temp` e `C:\Users\*\AppData\Local\Temp`, ignorando arquivos travados por processos em uso e contabilizando espaço liberado.
    *   **Limpeza de disco avançada:** Executa `Dism.exe /Online /Cleanup-Image /StartComponentCleanup`.
    *   **SFC e DISM:** Executa `sfc /scannow` e `DISM.exe /Online /Cleanup-Image /RestoreHealth`, traduzindo os códigos de retorno em resumo legível em português.
    *   **Verificar disco C:** Executa `chkdsk C:` em modo somente leitura por padrão; oferece sub-opção explícita para `chkdsk C: /f` mediante confirmação `[S/N]` com aviso de agendamento para o próximo reboot.
    *   **Saúde do disco (SMART):** Consulta WMI (`MSStorageDriver_FailurePredictStatus` em `root\wmi` e `Win32_DiskDrive.Status` em `root\cimv2`); informa claramente quando controladoras RAID ou adaptadores USB não expuserem atributos SMART.
2.  **`NetworkModule`:**
    *   **Reset de rede:** Mediante confirmação `[S/N]` com aviso de queda de acesso remoto (RDP/AnyDesk/TeamViewer), executa `ipconfig /release`, `ipconfig /renew`, `ipconfig /flushdns`, `netsh winsock reset` e `netsh int ip reset`.
    *   **Informações e DNS:** Executa `ipconfig /all`, `ipconfig /flushdns` e abertura de `ncpa.cpl`.
    *   **Testes de conectividade:** Testa Gateway padrão, servidores DNS configurados, portas AD (`53`, `88`, `389`, `445`) contra `DomainFqdn` e porta `443` do `ExchangeCheckUrl` usando `TcpClient` com timeout curto, além de alvos externos definidos em `DnsTestTargets` no `config.json`.
3.  **`PrintingModule`:**
    *   **Limpar fila travada:** Mediante confirmação `[S/N]`, para o serviço `Spooler` via `ServiceController`, encerra `spoolsv.exe` se permanecer travado, remove arquivos de `C:\Windows\System32\spool\PRINTERS\*` e reinicia o serviço `Spooler`.
    *   **Listar impressoras:** Combina `Win32_Printer` (nível máquina) com as conexões de rede do usuário interativo em `HKU\<UserSID>\Printers\Connections`.
4.  **`DiagnosticsModule`:**
    *   **Informações do sistema:** Coleta via WMI `Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_BIOS` (serial/modelo) e `Win32_LogicalDisk` (espaço livre/total).
    *   **Monitor de recursos e processos:** Lista os principais processos ordenados por memória/CPU via `Process.GetProcesses()` e oferece atalho para abrir `resmon.exe`.
5.  **`DriversUpdatesModule`:**
    *   **Dispositivos com erro:** Consulta `Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0` exibindo nome, DeviceID e código de erro.
    *   **Backup de drivers:** Verifica espaço livre no destino (exigindo mínimo de 5 GB livres antes de iniciar), sugere `C:\Temp\DriverBackup_<PC>` como padrão local para não lotar pendrives e executa `pnputil.exe /export-driver * <destino>`.
    *   **Windows Update e drivers:** Utiliza o objeto COM nativo `Microsoft.Update.Session` (`Type.GetTypeFromProgID`) para buscar atualizações pendentes (`IsInstalled=0`) sem depender de módulos externos da internet.
6.  **`ActiveDirectoryModule`:**
    *   **GPO:** Executa `gpupdate.exe /target:computer /force` (exibindo nota de que `/target:user` em sessão elevada afetaria a conta admin) e `gpresult.exe /r /scope:computer`.
    *   **Domínio e relógio:** Executa `nltest.exe /sc_verify:<DomainFqdn>`, `nltest.exe /dsgetdc:<DomainFqdn>` e `w32tm.exe /query /status`, alertando se a diferença de relógio ultrapassar 5 minutos (limite do Kerberos).
    *   **Exchange e Kerberos:** Executa `klist.exe`, resolve DNS de `autodiscover.<DomainFqdn>` e testa conexão HTTPS/TCP 443 em `ExchangeCheckUrl`.
7.  **`AuditModule`:**
    *   Lê e exibe de forma paginada os arquivos de log desta estação armazenados em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\`.

### Manifesto UAC e Build

*   `app.manifest` configura `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />`.
*   O fluxo de build em `Release` suporta assinatura opcional via `signtool.exe` quando um certificado interno (AD CS / GPO) estiver disponível na máquina de compilação.

## Alternatives Considered

### Alternative A: Script Batch puro (`.bat` / `.cmd`)

*   **Pros:** Nenhuma etapa de compilação; abre em qualquer versão do Windows sem dependências externas.
*   **Cons:** Encoding frágil para caracteres em português (UTF-8 vs. CP850), tratamento precário de erros/códigos de saída e ausência de APIs nativas para WMI, registro de usuário por SID (`HKU\<SID>`) e Active Directory.
*   **Reason for Rejection:** Rejeitada pelo autor e pela análise técnica: limitações de encoding, fragilidade no tratamento de erros e falta de APIs modernas de sistema/AD tornam a manutenção arriscada em campo.

### Alternative B: Empacotamento PowerShell via `ps2exe`

*   **Pros:** Permite escrever a lógica em PowerShell e gerar um arquivo `.exe` com manifesto UAC.
*   **Cons:** Wrappers `ps2exe` possuem alta incidência de falsos positivos no Windows Defender e EDRs por semelhança heurística com droppers, além de comportamento instável de `$PSScriptRoot` e caminhos relativos após elevação UAC via rede.
*   **Reason for Rejection:** Rejeitada na análise técnica: risco elevado de quarentena por antivírus/EDR nas estações atendidas e fragilidade na resolução de caminhos em execuções UNC elevadas.

### Alternative C: Launcher `.cmd` + Módulos `.ps1` (sem `.exe`)

*   **Pros:** Permite edição direta em campo sem recompilar, facilita assinatura Authenticode por script (`Set-AuthenticodeSignature`) e captura `%~dp0` antes da elevação UAC.
*   **Cons:** Distribuição composta por múltiplos arquivos visíveis e aparência menos padronizada para uso pelos técnicos.
*   **Reason for Rejection:** Rejeitada pelo autor: preferência explícita por um executável `.exe` único para apresentação profissional em campo.

### Alternative D: `.NET 8` Single-File (Self-Contained)

*   **Pros:** Acesso a APIs mais recentes do .NET (como `ProcessStartInfo.ArgumentList` e `System.Text.Json`).
*   **Cons:** Empacotar o runtime em arquivo único gera um binário de ~60–70 MB para um menu CLI de suporte, aumentando tempo de cópia em rede lenta ou pendrive sem trazer ganho funcional equivalente ao custo de tamanho.
*   **Reason for Rejection:** Rejeitada na análise técnica: o `.NET Framework 4.8` já é nativo no Windows 10/11 e Windows Server 2016+, entregando o mesmo conjunto de funcionalidades de sistema em um `.exe` abaixo de 1 MB.

### Alternative E: NuGet em Runtime no `net48` (`ILRepack`/`Costura.Fody` ou pasta com DLLs soltas)

*   **Pros:** Reduz código manual de serialização JSON e formatação de console usando bibliotecas populares de terceiros.
*   **Cons:** Mesclar ou embutir DLLs (`ILRepack`/`Costura.Fody`) aumenta o tamanho do binário e aciona heurísticas de antivírus/EDR; distribuir DLLs soltas ao lado do `.exe` quebra o modelo de arquivo único caso o técnico copie apenas o executável.
*   **Reason for Rejection:** Rejeitada pelo autor: opção pela BCL nativa do `.NET Framework 4.8` para manter um único `.exe` puro sem dependências externas em runtime.

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **Segurança e Privacidade:** Zero credenciais, tokens, IPs internos ou nomes reais de servidores da Brasfort no repositório ou compilados no `.exe`. Elevação executada exclusivamente pelo prompt UAC nativo do Windows. Prevenção contra command injection via `ArgumentEscaper` estrito em toda chamada externa. Suporte opcional a assinatura Authenticode (`signtool.exe`) via certificado interno AD CS/GPO para reduzir alertas de SmartScreen/EDR.
*   **Desempenho e Portabilidade:** Executável único abaixo de 1 MB em `.NET Framework 4.8`, inicialização em menos de 1 segundo e streaming assíncrono de `stdout`/`stderr` para evitar congelamento da interface durante comandos longos (`sfc`, `dism`, `pnputil`).
*   **Distribuição e Rollback:** Atualização feita pela substituição direta do arquivo `.exe` no pendrive ou pasta de rede; rollback imediato restaurando a versão anterior do `.exe`. O `UncStager` em `C:\ProgramData\Brasfort\TecnicoBrasfort\bin\` sobrescreve a cópia local quando detecta diferença de tamanho/hash em relação ao binário de origem.

--------------------------------------------------------------------------------

### Additional Resources

*   [AGENTS.md](../../AGENTS.md) — Governança, regras de Git, isolamento em `00-anything/`, `graphify` e estratégia de testes.
*   [concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md) — Síntese inicial do brainstorm e revisão multi-perspectiva (Goldfish).

### Future Improvements

*   **Suporte opcional a `smartctl.exe`:** Permitir leitura SMART avançada em discos atrás de controladoras RAID ou adaptadores USB caso o binário esteja presente em subpasta `tools\`.
*   **Automação de assinatura digital:** Integrar `signtool.exe` ao script/pipeline de release assim que o certificado interno via AD CS/GPO estiver disponível.
*   **Sincronização central de logs:** Adicionar opção configurável para enviar cópia dos logs para um compartilhamento central da equipe de TI após restabelecimento da rede.

--------------------------------------------------------------------------------

## Detailed Implementation Plan

### Target Files to Create/Modify

#### Governança, Configuração e Build

1.  **`../../AGENTS.md`** (`Modify`)
    *   **Type / Scope:** Governança e Documentação de Arquitetura
    *   **Proposed Changes:** Atualizar Seção 5 (incluir `UncStager`, `ArgumentEscaper`, `IFileSystem`, `IRegistryReader` e `scripts/build.ps1`), Seção 9 (substituir `ProcessStartInfo.ArgumentList` por `ProcessStartInfo.Arguments` + `ArgumentEscaper` estrito no `net48`), Seção 10 (registrar a detecção da máquina atual: ausência de `.NET SDK` e disponibilidade do `csc.exe` v4.0.30319) e adicionar entrada no Histórico de alterações.
2.  **`../../CLAUDE.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `./AGENTS.md` conforme Regra 5.1.
3.  **`../../GEMINI.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `./AGENTS.md` conforme Regra 5.1.
4.  **`../../.github/copilot-instructions.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `../AGENTS.md` conforme Regra 5.1.
5.  **`../../README.md`** (`Create`)
    *   **Type / Scope:** Documentação Humana
    *   **Proposed Changes:** Documentar pré-requisitos, uso do `scripts/build.ps1` (via `csc.exe` ou `dotnet build`), estrutura do `config.json`, comportamento de staging UNC e assinatura opcional via `signtool.exe`.
6.  **`../../.gitignore`** (`Create`)
    *   **Type / Scope:** Configuração Git
    *   **Proposed Changes:** Ignorar `bin/`, `obj/`, `dist/`, `graphify-out/`, `config.json`, `*.log`, `.vs/` e `.vscode/`.
7.  **`../../.editorconfig`** (`Create`)
    *   **Type / Scope:** Qualidade e Formatação C#
    *   **Proposed Changes:** Definir indentação, convenções de nomenclatura (`PascalCase`, `camelCase`, `_camelCase`) e regras de analisadores para `dotnet format`.
8.  **`../../config.example.json`** (`Create`)
    *   **Type / Scope:** Template de Configuração
    *   **Proposed Changes:** Definir chaves genéricas (`DomainFqdn`, `DnsTestTargets`, `ExchangeCheckUrl`, `LogDirectoryOverride`) sem dados internos ou credenciais da Brasfort.
9.  **`../../TecnicoBrasfort.sln`** (`Create`)
    *   **Type / Scope:** Solution .NET
    *   **Proposed Changes:** Vincular `src/TecnicoBrasfort/TecnicoBrasfort.csproj` e `tests/TecnicoBrasfort.Tests/TecnicoBrasfort.Tests.csproj`.
10. **`../../scripts/build.ps1`** (`Create`)
    *   **Type / Scope:** Script de Build e Empacotamento
    *   **Proposed Changes:** Detectar se há `.NET SDK` (`dotnet build -c Release`); caso contrário, compilar diretamente com `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` embutindo `app.manifest`, gerando `dist/TecnicoBrasfort.exe`, `dist/SHA256SUMS.txt` e suportando parâmetro opcional `-CertThumbprint` para `signtool.exe`.

#### Projeto Principal (`src/TecnicoBrasfort/`)

11. **`../../src/TecnicoBrasfort/TecnicoBrasfort.csproj`** (`Create`)
    *   **Type / Scope:** Projeto C# (`net48`, `Exe`)
    *   **Proposed Changes:** Configurar `TargetFrameworkVersion` v4.8, `ApplicationManifest` apontando para `app.manifest` e referências exclusivas à BCL (`System`, `System.Core`, `System.Management`, `System.Runtime.Serialization`, `System.ServiceProcess`, `System.Net.Http`), sem pacotes NuGet.
12. **`../../src/TecnicoBrasfort/app.manifest`** (`Create`)
    *   **Type / Scope:** Manifesto Windows UAC
    *   **Proposed Changes:** Declarar `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` e compatibilidade Windows 10/11.
13. **`../../src/TecnicoBrasfort/Program.cs`** (`Create`)
    *   **Type / Scope:** Ponto de Entrada / Bootstrap
    *   **Proposed Changes:** Interpretar `--origin-path`, carregar configuração via `ConfigLoader`, inicializar `EnvironmentContext` e `UncStager`, instanciar `AuditLogger`, injetar dependências nos 7 módulos e iniciar `ConsoleMenu` com tratamento global de exceções.
14. **`../../src/TecnicoBrasfort/Core/Models.cs`** (`Create`)
    *   **Type / Scope:** Contratos e DTOs
    *   **Proposed Changes:** Definir `AppConfig` (com `[DataContract]` / `[DataMember]`), `ProcessResult`, `ActionLogEntry` e `EnvironmentInfo`.
15. **`../../src/TecnicoBrasfort/Core/ArgumentEscaper.cs`** (`Create`)
    *   **Type / Scope:** Segurança de Execução (`net48`)
    *   **Proposed Changes:** Fornecer método puro de escaping e validação de argumentos para `ProcessStartInfo.Arguments`, bloqueando caracteres de controle e quebras de linha.
16. **`../../src/TecnicoBrasfort/Core/IProcessRunner.cs` e `ProcessRunner.cs`** (`Create`)
    *   **Type / Scope:** Executor de Processos
    *   **Proposed Changes:** Interface `IProcessRunner` e implementação `ProcessRunner` com `UseShellExecute = false`, redirecionamento e streaming em tempo real de `stdout`/`stderr`, timeout configurável e retorno de `ProcessResult`.
17. **`../../src/TecnicoBrasfort/Core/IFileSystem.cs` e `FileSystemAdapter.cs`** (`Create`)
    *   **Type / Scope:** Abstração de Sistema de Arquivos
    *   **Proposed Changes:** Encapsular verificação, leitura, gravação, exclusão segura de arquivos e consulta de espaço livre (`DriveInfo`) para viabilizar mocks em testes.
18. **`../../src/TecnicoBrasfort/Core/IRegistryReader.cs` e `RegistryReaderAdapter.cs`** (`Create`)
    *   **Type / Scope:** Abstração de Registro Windows
    *   **Proposed Changes:** Encapsular leitura de subchaves em `Registry.Users` (`HKU\<UserSID>\Printers\Connections`) para listar impressoras mapeadas do usuário logado.
19. **`../../src/TecnicoBrasfort/Core/ConfigLoader.cs`** (`Create`)
    *   **Type / Scope:** Carregador de Configuração
    *   **Proposed Changes:** Ler `config.json` ao lado do executável (ou no `--origin-path`) usando `DataContractJsonSerializer` nativo, aplicando defaults genéricos caso o arquivo não exista ou seja inválido.
20. **`../../src/TecnicoBrasfort/Core/EnvironmentContext.cs`** (`Create`)
    *   **Type / Scope:** Detector de Contexto
    *   **Proposed Changes:** Coletar hostname, versão do Windows, conta administrativa elevada, usuário interativo via WMI (`Win32_ComputerSystem.UserName` -> SID via `NTAccount`), caminho de origem e flags `IsUncOrigin` / `IsWritableOrigin`.
21. **`../../src/TecnicoBrasfort/Core/UncStager.cs`** (`Create`)
    *   **Type / Scope:** Staging Local para UNC
    *   **Proposed Changes:** Quando `IsUncOrigin` for verdadeiro e a execução ainda não estiver em `C:\ProgramData\Brasfort\TecnicoBrasfort\bin\`, copiar o `.exe` (verificando hash SHA-256) e `config.json` para o staging local e relançar passando `--origin-path`.
22. **`../../src/TecnicoBrasfort/Core/AuditLogger.cs`** (`Create`)
    *   **Type / Scope:** Logger Dual de Auditoria
    *   **Proposed Changes:** Gravar entrada formatada em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\<PC>_<Data>.log` e tentar cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\<PC>_<Data>_<Hora>_<Tech>.log` (ou `LogDirectoryOverride`), tratando falhas na cópia secundária apenas com aviso no console.
23. **`../../src/TecnicoBrasfort/UI/ConsoleMenu.cs`** (`Create`)
    *   **Type / Scope:** Interface CLI
    *   **Proposed Changes:** Renderizar cabeçalho de contexto, exibir as 7 categorias numeradas, solicitar confirmação explícita `[S/N]` antes de ações destrutivas/reboot e formatar resumos de saída.
24. **`../../src/TecnicoBrasfort/Modules/MaintenanceModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Manutenção
    *   **Proposed Changes:** Implementar limpeza de temporários (`C:\Windows\Temp` + `C:\Users\*\AppData\Local\Temp`), limpeza WinSxS (`Dism.exe /StartComponentCleanup`), `sfc /scannow`, `DISM /RestoreHealth`, `chkdsk C:` (leitura padrão + `/f` com `[S/N]`) e consulta SMART via WMI.
25. **`../../src/TecnicoBrasfort/Modules/NetworkModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Rede
    *   **Proposed Changes:** Implementar reset de rede (`ipconfig` + `netsh winsock/ip reset` com `[S/N]`), `ipconfig /all`, testes de conectividade (Gateway, DNS, portas AD `53/88/389/445`, Exchange `443` via `TcpClient` e `DnsTestTargets`), flush DNS e abertura de `ncpa.cpl`.
26. **`../../src/TecnicoBrasfort/Modules/PrintingModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Impressão
    *   **Proposed Changes:** Implementar limpeza de fila (`ServiceController` no `Spooler`, encerramento de `spoolsv` se travado, limpeza de `spool\PRINTERS\*` e reinício com `[S/N]`) e listagem combinada de `Win32_Printer` + `HKU\<UserSID>\Printers\Connections`.
27. **`../../src/TecnicoBrasfort/Modules/DiagnosticsModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Diagnóstico
    *   **Proposed Changes:** Coletar resumo do sistema via WMI (`Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_BIOS`, `Win32_LogicalDisk`), listar top processos por CPU/RAM e oferecer atalho para `resmon.exe`.
28. **`../../src/TecnicoBrasfort/Modules/DriversUpdatesModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Drivers e Atualizações
    *   **Proposed Changes:** Consultar dispositivos com erro (`Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0`), exportar drivers via `pnputil /export-driver` com validação de >= 5 GB livres (padrão em `C:\Temp\DriverBackup_<PC>`) e buscar atualizações pendentes via COM `Microsoft.Update.Session`.
29. **`../../src/TecnicoBrasfort/Modules/ActiveDirectoryModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo AD / GPO / Exchange
    *   **Proposed Changes:** Executar `gpupdate /target:computer /force`, `gpresult /r /scope:computer`, `nltest /sc_verify` e `/dsgetdc`, `w32tm /query /status` (alertando desvio > 5 min), `klist`, resolução DNS de `autodiscover` e checagem TCP/HTTPS 443 em `ExchangeCheckUrl`.
30. **`../../src/TecnicoBrasfort/Modules/AuditModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Auditoria
    *   **Proposed Changes:** Ler e exibir de forma paginada os registros de ações desta estação gravados em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\`.

#### Testes Unitários (`tests/TecnicoBrasfort.Tests/`)

31. **`../../tests/TecnicoBrasfort.Tests/TecnicoBrasfort.Tests.csproj`** (`Create`)
    *   **Type / Scope:** Projeto de Testes (`xUnit`)
    *   **Proposed Changes:** Referenciar `TecnicoBrasfort.csproj` e pacotes `xunit` / `xunit.runner.visualstudio` / `Microsoft.NET.Test.Sdk` (requer `.NET SDK` instalado na máquina de dev/CI).
32. **`../../tests/TecnicoBrasfort.Tests/CoreTests.cs`** (`Create`)
    *   **Type / Scope:** Testes Unitários de Infraestrutura
    *   **Proposed Changes:** Validar `ArgumentEscaper` (aspas, espaços, rejeição de caracteres de controle), `ConfigLoader` (defaults vs. JSON), `AuditLogger` (fallback quando destino secundário falha) e `UncStager` com mocks de `IFileSystem`.
33. **`../../tests/TecnicoBrasfort.Tests/ModulesTests.cs`** (`Create`)
    *   **Type / Scope:** Testes Unitários dos Módulos
    *   **Proposed Changes:** Validar construção de comandos, interpretação de códigos de saída, validação de espaço >= 5 GB no backup de drivers e leitura de impressoras via `HKU\<SID>` usando mocks de `IProcessRunner`, `IFileSystem` e `IRegistryReader`, sem efeitos colaterais no host.

### Verification Plan

*   **Compilation / Build:**
    *   Na máquina atual (verificado via `dotnet --info`: runtimes .NET 6/8 presentes, **sem `.NET SDK`** instalado, porém `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` disponível): executar `powershell -ExecutionPolicy Bypass -File scripts/build.ps1` e confirmar geração de `dist/TecnicoBrasfort.exe` (< 1 MB) e `dist/SHA256SUMS.txt`.
    *   Em ambiente com `.NET SDK` instalado: executar `dotnet build -c Release`.
*   **Unit Tests:**
    *   Executar `dotnet test` (requer instalação do `.NET SDK` na máquina de desenvolvimento ou pipeline CI, pois apenas runtimes foram detectados no host atual).
*   **Static Analysis, Security & Graph:**
    *   Executar `dotnet format --verify-no-changes` e `dotnet list package --vulnerable` (com `.NET SDK` disponível).
    *   Verificar ausência de segredos, IPs ou nomes reais da infraestrutura Brasfort no código e em `config.example.json`.
    *   Executar `npx graphify` para atualizar `graphify-out/` em paridade com o `AGENTS.md`.
*   **Manual Field Validation (Estação/VM):**
    *   Testar execução via pendrive e via compartilhamento UNC como Admin sobre sessão de usuário padrão, validando detecção do usuário interativo, staging local no Reset de Rede e escrita dupla de logs (`ProgramData` + `Public\Desktop`).

--------------------------------------------------------------------------------

*Drafted with EGM*
