# Painel CLI de Suporte TI — Tecnico Brasfort

*   **Author:** Renato França
*   **Status:** Approved
*   **Date:** 2026-11-23

--------------------------------------------------------------------------------

## Overview

O **Tecnico Brasfort** é uma ferramenta portátil de linha de comando (CLI), compilada em `.exe` (C# / .NET Framework 4.8), destinada exclusivamente a técnicos e analistas de TI da Brasfort para executar rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

A ferramenta é distribuída via pendrive ou pasta de rede compartilhada (por caminho UNC direto) e executada sobre a sessão ativa do usuário final mediante elevação UAC com credenciais administrativas do técnico. O projeto segue integralmente as regras de governança adaptadas em `AGENTS.md` (isolamento de artefatos em `00-anything/`, fluxo Git por branch, decomposição em issues, configuração genérica sem dados internos hardcoded, `graphify` — ferramenta de mapeamento do grafo de dependências do código — e testes unitários com mocks).

## The Problem

Em atendimentos presenciais e remotos, os técnicos de TI executam manualmente comandos dispersos do Windows e do Active Directory, o que gera inconsistência entre atendimentos, falta de histórico do que foi feito em cada máquina e falhas operacionais específicas do ambiente corporativo:

1.  **Quebra de contexto na elevação UAC ("Over-the-Shoulder"):** Quando o usuário final está logado e o técnico executa uma ferramenta como Administrador usando uma conta administrativa separada, o Windows cria outra sessão de logon. Unidades mapeadas (`Z:\`) desaparecem antes mesmo da inicialização do processo elevado, o diretório atual muda para `C:\Windows\System32` e variáveis como `%TEMP%`, `%USERPROFILE%` e `HKCU` passam a apontar para o perfil do técnico admin em vez do usuário logado.
2.  **Queda de execução via compartilhamento UNC:** Ao rodar um utilitário diretamente de uma pasta de rede (`\\servidor\pasta\...`) e executar um reset de rede (`netsh int ip reset`, `ipconfig /release`), a conexão SMB cai no meio do processo, podendo falhar leituras tardias de configuração ou gravação de logs na origem.
3.  **Bloqueios de Defender/EDR em wrappers:** Empacotar scripts PowerShell com `ps2exe` gera alta taxa de falso positivo em antivírus/EDR e quebra referências de caminho relativo após elevação.
4.  **Ausência de trilha de auditoria portátil:** Não há registro padronizado por estação das ações executadas e dos códigos de saída retornados, e gravar apenas em pasta de rede ou pendrive falha quando a mídia está somente leitura ou quando dois técnicos atuam simultaneamente.

## Goals

*   Entregar um executável `.exe` portátil em C# 5.0 / .NET Framework 4.8 (`net48`) com tamanho abaixo de 1 MB, capaz de rodar em Windows 10/11 e Windows Server 2016+ sem exigir instalação prévia de runtime.
*   Tratar elevação UAC com conta administrativa separada, identificando explicitamente o usuário interativo logado na sessão (física ou RDP) para operações de perfil (temporários de usuários e impressoras mapeadas por SID).
*   Carregar integralmente a configuração e pré-inicializar os módulos em memória no startup para que execuções via caminho UNC (`\\servidor\...`) não dependam da conexão SMB durante rotinas de Reset de Rede, sem copiar nem relançar o `.exe` em disco.
*   Cobrir as 7 categorias operacionais do painel (23 ações numeradas: Manutenção do Sistema, Rede e Internet, Impressão, Diagnóstico, Drivers e Atualizações, AD/GPO/Exchange e Auditoria/Logs) com streaming de saída em tempo real e confirmação explícita `[S/N]` para ações destrutivas ou que agendam reinicialização.
*   Garantir rastreabilidade por meio de escrita dupla de logs: arquivo primário local em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` e cópia secundária por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`), com falha não bloqueante e aviso no console caso o destino secundário esteja indisponível.
*   Cumprir integralmente o `AGENTS.md` do projeto: zero dados internos hardcoded no código (usando `config.example.json`), testes unitários isolados via mocks (`IProcessRunner`, `IHostEnvironment`) executáveis também sem `.NET SDK`, atualização contínua de `graphify` e organização de rascunhos em `00-anything/`.

## Non-Goals

*   Uso autônomo ou self-service por usuários finais.
*   Integrações com Microsoft Entra ID, Microsoft Intune ou limpeza de cache do Microsoft Teams (removidos do escopo por definição do autor).
*   Execução via letra de unidade mapeada (`Z:\`) na sessão do usuário quando houver elevação UAC com conta separada (restrição pré-execução do Windows com `requireAdministrator`; deve-se abrir via caminho UNC `\\servidor\...` ou pendrive).
*   Armazenamento de senhas administrativas, tokens, IPs internos ou nomes reais de servidores da Brasfort no código-fonte ou no binário `.exe`.
*   Instalação de runtimes externos (.NET 8 Desktop Runtime) ou download de módulos PowerShell externos (`PSWindowsUpdate`) nas máquinas atendidas.

## Open Questions

Nenhuma questão em aberto; todas as decisões técnicas levantadas no brainstorm ([concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md)) foram resolvidas e incorporadas ao plano técnico abaixo.

## Critical User Journeys (CUJs)

### CUJ 1: Atendimento presencial via pendrive com elevação UAC sobre a sessão do usuário

*   **Given:** O usuário final está logado em sua estação Windows 10/11 e o técnico conecta um pendrive contendo `TecnicoBrasfort.exe`.
*   **When:** O técnico executa `TecnicoBrasfort.exe`, fornece suas credenciais de administrador no prompt UAC, visualiza no cabeçalho o usuário interativo detectado, executa a limpeza de arquivos temporários e depois roda o SFC (`/scannow`).
*   **Then:** O painel limpa `C:\Windows\Temp` e `C:\Users\*\AppData\Local\Temp` (ignorando arquivos bloqueados sem travar), exibe a saída do `sfc /scannow` em tempo real sem caracteres corrompidos e com resumo do código de saída em português, grava a entrada no log local em `ProgramData` e salva a cópia secundária em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\`.

### CUJ 2: Atendimento via pasta de rede (UNC) com Reset de Rede

*   **Given:** O técnico acessa o compartilhamento de rede corporativo por caminho UNC (`\\servidor\ti\TecnicoBrasfort.exe`) na máquina de um usuário com falha de conectividade.
*   **When:** O técnico inicia o executável (que carrega a configuração em memória no startup), seleciona a opção de Reset de Rede e confirma o prompt `[S/N]`.
*   **Then:** O programa executa os comandos de reset de rede e renovação DHCP sem travar quando a conexão SMB cai, registra o resultado no log local em `ProgramData` e tenta gravar a cópia secundária em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou em `LogDirectoryOverride`, emitindo apenas aviso no console se um destino secundário de rede estiver inacessível).

### CUJ 3: Diagnóstico de AD/GPO/Exchange e consulta ao histórico de ações

*   **Given:** Uma estação apresenta falha de aplicação de GPO ou erro de conexão com o Exchange corporativo.
*   **When:** O técnico acessa a categoria AD/GPO/Exchange para rodar atualização de GPO de computador, verificação de canal seguro, checagem de desvio de relógio contra o controlador de domínio e teste HTTPS no endpoint configurado no `config.json`, e em seguida abre a opção "Ver registro de ações deste PC".
*   **Then:** O painel exibe os diagnósticos de domínio, alerta se o desvio de relógio ultrapassar 5 minutos (limite do Kerberos), diferencia falhas TCP/SSL/HTTP do Exchange e lista o histórico de ações executadas naquela estação a partir dos logs em `ProgramData`.

## Technical Plan (Architecture)

### Arquitetura e Fluxo de Dados

```text
TecnicoBrasfort.exe (app.manifest: requireAdministrator | .NET Framework 4.8)
  │
  ▼
Program.cs
  ├─► ConfigLoader (carrega config.json para memória no startup ou aplica defaults genéricos)
  ├─► EnvironmentContext (Admin elevado, Usuário interativo físico/RDP + SID, Origem UNC/USB)
  ├─► AuditLogger (Primário: C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\<HOSTNAME>_<YYYY-MM-DD>.log
  │                Secundário: C:\Users\Public\Desktop\TecnicoBrasfort_Logs\<HOSTNAME>_<YYYY-MM-DD>_<HHmmss>_<TechAdmin>.log)
  └─► ConsoleMenu (UI pura: cabeçalho de contexto, menu 1..23 + 0 Sair, prompts [S/N])
        │
        └─► Modules/* ──► IProcessRunner / IHostEnvironment
                              └─► ProcessRunner + ArgumentEscaper (streaming stdout/stderr + timeouts)
```

### Decisão de Zero NuGet em Runtime (`net48` BCL-only)

*   O projeto principal utiliza exclusivamente bibliotecas nativas da BCL do `.NET Framework 4.8`, garantindo distribuição como um único `.exe` abaixo de 1 MB sem DLLs auxiliares ao lado e sem loaders de empacotamento que elevam falsos positivos em antivírus/EDR.
*   Pacotes externos de teste ficam isolados no projeto de testes (com runner nativo em BCL pura para execução local via `csc.exe` quando não houver `.NET SDK`).

### Contratos da Camada `Core/`

1.  **Executor de Processos (`IProcessRunner`, `ProcessRunner`, `ArgumentEscaper`):**
    *   Toda execução externa valida e escapa argumentos estritamente antes de invocar o processo, redireciona `stdout`/`stderr` com streaming em tempo real e aplica política de timeout por categoria (60 segundos para comandos rápidos de rede/AD; 45 minutos para manutenção pesada como SFC, DISM, `chkdsk` somente leitura e backup de drivers).
    *   Trata o encoding OEM do console por padrão e higieniza bytes nulos da saída `UTF-16LE` emitida pelo `sfc.exe` via pipe.
2.  **Abstração de Ambiente (`IHostEnvironment`):**
    *   Consolida operações de sistema de arquivos, verificação de espaço livre em disco e leitura de conexões de impressora do usuário no Registro (`HKU\<UserSID>\Printers\Connections`), permitindo testes unitários isolados sem side-effects no host.
3.  **Detector de Contexto (`EnvironmentContext`):**
    *   Identifica hostname, versão do Windows, conta administrativa elevada, origem (UNC ou local/pendrive) e o SID do usuário interativo da sessão atual (física ou RDP) buscando o proprietário do `explorer.exe` na mesma sessão, com fallback secundário para consulta de console físico e tratamento gracioso para sessões sem usuário interativo.
4.  **Logger de Auditoria (`AuditLogger`):**
    *   Escrita dupla com sanitização do nome do técnico (removendo prefixo de domínio e caracteres inválidos de arquivo). Grava sempre no log primário em `ProgramData` e tenta gravar a cópia por sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`); falhas na cópia secundária emitem apenas aviso no console sem interromper o atendimento.

### Escopo Funcional dos 7 Módulos (`Modules/`)

1.  **Manutenção do Sistema (`MaintenanceModule`):** Limpeza de temporários (`C:\Windows\Temp` e perfis em `C:\Users\*\AppData\Local\Temp` ignorando arquivos em uso), limpeza de componentes WinSxS, SFC (`/scannow`), DISM (`/RestoreHealth`), verificação de disco `C:` (somente leitura por padrão, com sub-opção de agendamento no próximo boot via `chkntfs.exe /c C:` após confirmação `[S/N]`) e saúde do disco (SMART nativo via WMI, alertando quando controladoras RAID/USB ocultarem telemetria).
2.  **Rede e Internet (`NetworkModule`):** Reset de rede com confirmação `[S/N]` e aviso de queda de acesso remoto (executando reset Winsock/IP antes de `release`, `flushdns` e `renew`), informações de rede (`ipconfig /all`), testes de conectividade (Gateway, DNS, portas AD `53/88/389/445`, porta `443` do Exchange com timeout de `3000 ms` e alvos configurados), flush DNS e abertura de Conexões de Rede (`ncpa.cpl`).
3.  **Impressão (`PrintingModule`):** Limpeza de fila travada (parando o serviço `Spooler`, encerrando `spoolsv.exe` em caso de `StopPending`, limpando `spool\PRINTERS\*` e reiniciando após `[S/N]`) e listagem combinada de impressoras da máquina e conexões do usuário interativo.
4.  **Diagnóstico (`DiagnosticsModule`):** Informações do sistema (SO, hardware, BIOS/serial e discos) e monitor de recursos/processos (top 15 processos por consumo de memória `WorkingSet64` e atalho para `resmon.exe`).
5.  **Drivers e Atualizações (`DriversUpdatesModule`):** Dispositivos com erro, atualização/busca de atualizações do Windows via interface COM nativa executada de forma assíncrona com timeout e tradução de erros, e backup de drivers com checagem prévia de espaço livre mínimo de 5 GB (padrão em `C:\Temp\DriverBackup_<PC>`).
6.  **AD / GPO / Exchange (`ActiveDirectoryModule`):** `gpupdate /target:computer /force` (explicando limitação de `/target:user` sob UAC), `gpresult` de computador, verificação de canal seguro e DC, checagem de desvio de relógio contra o DC via `w32tm /stripchart` (alertando acima de 5 minutos), inspeção `klist` da sessão atual, DNS `autodiscover` e verificação TLS 1.2 / HTTPS do Exchange distinguindo erro de rede, certificado SSL e código HTTP.
7.  **Auditoria TI (`AuditModule`):** Visualização paginada do registro de ações deste PC a partir de `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\`.

### Manifesto UAC e Build

*   Manifesto UAC configurado como `requireAdministrator`.
*   Fluxo de build em `Release` preparado para assinatura opcional via `signtool.exe` quando houver certificado interno (AD CS / GPO).

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
*   **Reason for Rejection:** Rejeitada pelo autor: opção pela BCL nativa do `.NET Framework 4.8` para manter um único `.exe` sem dependências externas em runtime.

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **Segurança e Privacidade:** Zero credenciais, tokens, IPs internos ou nomes reais de servidores da Brasfort no repositório ou compilados no `.exe`. Elevação executada exclusivamente pelo prompt UAC nativo do Windows. Prevenção contra command injection via `ArgumentEscaper` estrito em toda chamada externa. Suporte opcional a assinatura Authenticode (`signtool.exe`) via certificado interno AD CS/GPO para reduzir alertas de SmartScreen/EDR.
*   **Desempenho e Portabilidade:** Executável único abaixo de 1 MB em `.NET Framework 4.8`, inicialização em menos de 1 segundo e streaming assíncrono de `stdout`/`stderr` para evitar congelamento da interface durante comandos longos (`sfc`, `dism`, `pnputil`).
*   **Distribuição e Rollback:** Atualização feita pela substituição direta do arquivo `.exe` no pendrive ou pasta de rede; rollback imediato restaurando a versão anterior do `.exe`.

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

### Restrição de Compilação (C# 5.0 Estrito)

Todo o código-fonte em `src/` e `tests/` deve respeitar estritamente a sintaxe **C# 5.0** (`<LangVersion>5</LangVersion>`): proibido uso de interpolação `$"..."`, operador `?.`, `nameof()`, inicializadores de auto-propriedades, expression-bodied members (`=>`), `out var` ou `using var`. Utilizar `string.Format(...)`, inicialização em construtores e checagens `!= null`.

### Target Files to Create/Modify

#### Governança, Configuração e Build

1.  **`../../AGENTS.md`** (`Modify`)
    *   **Type / Scope:** Governança e Documentação de Arquitetura
    *   **Proposed Changes:** Atualizar Seção 2 (compatibilidade C# 5.0 / `csc.exe` + `TestRunner.cs`), Seção 3 (adicionar `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests`), Seção 5 (refletir `02 - design-tecnico 2026-11-23/`, `dist/`, `scripts/build.ps1`, `ArgumentEscaper.cs`, `IHostEnvironment.cs`, `HostEnvironmentAdapter.cs`, `TestRunner.cs` e remover `UncStager.cs`/`IRegistryReader`), Seção 6 e 7 (alinhar fluxo sem `UncStager` e relacionar `EnvironmentContext` com o DTO `EnvironmentInfo`), Seção 9 (substituir `ProcessStartInfo.ArgumentList` por `ProcessStartInfo.Arguments` + `ArgumentEscaper` estrito no `net48` / C# 5.0), Seção 10 (registrar detecção da máquina atual: ausência de `.NET SDK` e disponibilidade do `csc.exe` v4.0.30319), Seção 13 (permitir validação via `scripts/build.ps1 -RunTests` sem `.NET SDK`) e Histórico de alterações.
2.  **`../../CLAUDE.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `./AGENTS.md` conforme Seção 5 do `AGENTS.md`.
3.  **`../../GEMINI.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `./AGENTS.md` conforme Seção 5 do `AGENTS.md`.
4.  **`../../.github/copilot-instructions.md`** (`Create`)
    *   **Type / Scope:** Arquivo Ponteiro Multi-IA
    *   **Proposed Changes:** Redirecionar explicitamente para `../AGENTS.md` conforme Seção 5 do `AGENTS.md`.
5.  **`../../README.md`** (`Create`)
    *   **Type / Scope:** Documentação Humana
    *   **Proposed Changes:** Documentar pré-requisitos, uso do `scripts/build.ps1` (incluindo `-RunTests` e `-CertThumbprint`), estrutura do `config.json`, restrição de abertura por caminho UNC (`\\servidor\...`) em vez de unidade mapeada (`Z:\`), e assinatura opcional via `signtool.exe`.
6.  **`../../.gitignore`** (`Create`)
    *   **Type / Scope:** Configuração Git
    *   **Proposed Changes:** Ignorar `bin/`, `obj/`, `dist/`, `graphify-out/`, `config.json`, `*.log`, `.vs/` e `.vscode/`.
7.  **`../../.editorconfig`** (`Create`)
    *   **Type / Scope:** Qualidade e Formatação C#
    *   **Proposed Changes:** Definir indentação (4 espaços), convenções de nomenclatura (`PascalCase`, `camelCase`, `_camelCase`) e regras compatíveis com C# 5.0.
8.  **`../../config.example.json`** (`Create`)
    *   **Type / Scope:** Template de Configuração
    *   **Proposed Changes:** Definir chaves genéricas (`DomainFqdn`, `DnsTestTargets`, `ExchangeCheckUrl`, `LogDirectoryOverride`) sem dados internos ou credenciais da Brasfort.
9.  **`../../TecnicoBrasfort.sln`** (`Create`)
    *   **Type / Scope:** Solution .NET
    *   **Proposed Changes:** Vincular `src/TecnicoBrasfort/TecnicoBrasfort.csproj` e `tests/TecnicoBrasfort.Tests/TecnicoBrasfort.Tests.csproj`.
10. **`../../scripts/build.ps1`** (`Create`)
    *   **Type / Scope:** Script de Build, Testes e Empacotamento
    *   **Proposed Changes:** Aceitar parâmetros `-RunTests` e `-CertThumbprint`. Se houver `.NET SDK`, suportar `dotnet build -c Release`; na máquina atual (ou por padrão via `csc.exe`), compilar o executável principal usando `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /optimize+ /warnaserror+ /win32manifest:src\TecnicoBrasfort\app.manifest /out:dist\TecnicoBrasfort.exe /r:System.dll /r:System.Core.dll /r:System.Management.dll /r:System.Runtime.Serialization.dll /r:System.Xml.dll /r:System.ServiceProcess.dll /r:System.Net.Http.dll /recurse:src\TecnicoBrasfort\*.cs`. Quando `-RunTests` for informado, compilar `tests\TecnicoBrasfort.Tests\*.cs` junto aos arquivos `Core/`, `Modules/` e `UI/` de `src/` (excluindo `Program.cs` via `/main:TecnicoBrasfort.Tests.TestRunner`) gerando e executando `dist\TecnicoBrasfort.Tests.exe` (falhando o script se exit code != 0). Gerar `dist\SHA256SUMS.txt` e invocar `signtool.exe` se `-CertThumbprint` for passado.

#### Projeto Principal (`src/TecnicoBrasfort/`)

11. **`../../src/TecnicoBrasfort/TecnicoBrasfort.csproj`** (`Create`)
    *   **Type / Scope:** Projeto C# (`net48`, `Exe`)
    *   **Proposed Changes:** Configurar `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>`, `<LangVersion>5</LangVersion>`, `<ApplicationManifest>app.manifest</ApplicationManifest>` e referências exclusivas à BCL (`System`, `System.Core`, `System.Management`, `System.Runtime.Serialization`, `System.Xml`, `System.ServiceProcess`, `System.Net.Http`), sem pacotes NuGet.
12. **`../../src/TecnicoBrasfort/app.manifest`** (`Create`)
    *   **Type / Scope:** Manifesto Windows UAC
    *   **Proposed Changes:** Declarar `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` e compatibilidade Windows 10/11.
13. **`../../src/TecnicoBrasfort/Program.cs`** (`Create`)
    *   **Type / Scope:** Ponto de Entrada / Bootstrap
    *   **Proposed Changes:** Configurar `Console.OutputEncoding = Encoding.UTF8`, carregar `AppConfig` em memória via `ConfigLoader` no startup, coletar `EnvironmentInfo` via `EnvironmentContext`, instanciar `ProcessRunner`, `HostEnvironmentAdapter`, `AuditLogger` e os 7 módulos, e iniciar `ConsoleMenu.Run()` com tratamento global de exceções.
14. **`../../src/TecnicoBrasfort/Core/Models.cs`** (`Create`)
    *   **Type / Scope:** Contratos e DTOs (C# 5.0)
    *   **Proposed Changes:** Definir classes com inicialização nos construtores:
        *   `AppConfig` (`[DataContract]`): `string DomainFqdn`, `string[] DnsTestTargets`, `string ExchangeCheckUrl`, `string LogDirectoryOverride`.
        *   `ProcessResult`: `int ExitCode`, `TimeSpan Duration`, `bool TimedOut`, `string StandardOutput`, `string StandardError`, `string Summary`.
        *   `ActionLogEntry`: `DateTime Timestamp`, `string Hostname`, `string TechAdmin`, `string InteractiveUser`, `string ModuleName`, `string ActionName`, `int ExitCode`, `string Summary`.
        *   `EnvironmentInfo`: `string Hostname`, `string OsVersion`, `string ElevatedAdmin`, `string InteractiveUser`, `string InteractiveUserSid`, `string OriginDirectory`, `bool IsUncOrigin`.
15. **`../../src/TecnicoBrasfort/Core/ArgumentEscaper.cs`** (`Create`)
    *   **Type / Scope:** Segurança de Execução (`net48`)
    *   **Proposed Changes:** Expor `public static string JoinArguments(string[] args)` e `public static string Escape(string arg)` para `ProcessStartInfo.Arguments`, lançando `ArgumentException` caso contenham caracteres de controle (`\r`, `\n`, `\0`) e aplicando aspas/escapamento padrão Win32.
16. **`../../src/TecnicoBrasfort/Core/IProcessRunner.cs`** (`Create`)
    *   **Type / Scope:** Interface de Execução de Processos
    *   **Proposed Changes:** Definir `ProcessResult Run(string fileName, string[] args, int timeoutMs, Action<string> onOutput, Action<string> onError, bool useUnicodeOutput);`.
17. **`../../src/TecnicoBrasfort/Core/ProcessRunner.cs`** (`Create`)
    *   **Type / Scope:** Implementação de Execução de Processos
    *   **Proposed Changes:** Implementar `IProcessRunner` com `UseShellExecute = false`, `RedirectStandardOutput = true`, `RedirectStandardError = true`, `StandardOutputEncoding` / `StandardErrorEncoding` usando `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)` por padrão (ou `Encoding.Unicode` quando `useUnicodeOutput == true`), remoção de `\0` residuais nas linhas recebidas, streaming assíncrono via `OutputDataReceived`/`ErrorDataReceived`, timeout por categoria (`60000` ms padrão; `2700000` ms para comandos longos) e encerramento seguro em timeout.
18. **`../../src/TecnicoBrasfort/Core/IHostEnvironment.cs`** (`Create`)
    *   **Type / Scope:** Interface Consolidada de Sistema de Arquivos e Registro
    *   **Proposed Changes:** Definir `bool FileExists(string path)`, `bool DirectoryExists(string path)`, `void CreateDirectory(string path)`, `string ReadAllText(string path)`, `void AppendAllText(string path, string content)`, `IEnumerable<string> GetFiles(string path, string searchPattern)`, `IEnumerable<string> GetDirectories(string path)`, `bool TryDeleteFile(string path)`, `long GetAvailableFreeSpaceBytes(string path)` e `IList<string> GetUserPrinterConnections(string userSid)`.
19. **`../../src/TecnicoBrasfort/Core/HostEnvironmentAdapter.cs`** (`Create`)
    *   **Type / Scope:** Implementação Real de `IHostEnvironment`
    *   **Proposed Changes:** Implementar operações via `System.IO` (`File`, `Directory`, `DriveInfo`) e leitura de `Registry.Users` em `<userSid>\Printers\Connections`, convertendo nomes como `,,servidor,impressora` para `\\servidor\impressora`.
20. **`../../src/TecnicoBrasfort/Core/ConfigLoader.cs`** (`Create`)
    *   **Type / Scope:** Carregador de Configuração
    *   **Proposed Changes:** Ler `config.json` ao lado de `AppDomain.CurrentDomain.BaseDirectory` para memória no startup usando `DataContractJsonSerializer` (`System.Runtime.Serialization.Json`), aplicando defaults genéricos (`corp.example.local`, `["1.1.1.1", "8.8.8.8"]`, `https://mail.example.local/owa`) caso ausente ou inválido.
21. **`../../src/TecnicoBrasfort/Core/EnvironmentContext.cs`** (`Create`)
    *   **Type / Scope:** Detector de Contexto
    *   **Proposed Changes:** Retornar `EnvironmentInfo` coletando hostname, versão do Windows, `WindowsIdentity.GetCurrent().Name`, `AppDomain.CurrentDomain.BaseDirectory` (`IsUncOrigin` via `Uri.IsUnc`), e resolvendo o usuário interativo e SID consultando WMI `Win32_Process WHERE Name = 'explorer.exe' AND SessionId = <CurrentSessionId>` com `GetOwnerSid()`, usando `Win32_ComputerSystem.UserName` como fallback secundário e tratando ausência de usuário interativo.
22. **`../../src/TecnicoBrasfort/Core/AuditLogger.cs`** (`Create`)
    *   **Type / Scope:** Logger Dual de Auditoria
    *   **Proposed Changes:** Expor `void Log(ActionLogEntry entry)` e `IList<string> ReadLogsForCurrentHost()`. Sanitizar `<HOSTNAME>` e `<TechAdmin>` (extraindo `sAMAccountName` após `\` e removendo `Path.GetInvalidFileNameChars()`). Gravar sempre em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\<HOSTNAME>_<YYYY-MM-DD>.log` e tentar gravar a cópia da sessão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\<HOSTNAME>_<YYYY-MM-DD>_<HHmmss>_<TechAdmin>.log` (ou `LogDirectoryOverride`); em erro no destino secundário, emitir apenas aviso no console.
23. **`../../src/TecnicoBrasfort/UI/ConsoleMenu.cs`** (`Create`)
    *   **Type / Scope:** Interface CLI e Roteamento do Menu
    *   **Proposed Changes:** Renderizar cabeçalho com `EnvironmentInfo`, solicitar confirmação `[S/N]` para ações destrutivas e rotear exatamente as opções `[1]`..`[23]` + `[0]` Sair:
        *   `[1]` Temp (`MaintenanceModule.CleanTempFiles`)
        *   `[2]` Limpeza WinSxS (`MaintenanceModule.CleanComponentStore`)
        *   `[3]` SFC (`MaintenanceModule.RunSfc`)
        *   `[4]` DISM (`MaintenanceModule.RunDismRestoreHealth`)
        *   `[5]` Verificar disco C: (`MaintenanceModule.CheckDiskC`, com sub-opção `[S/N]` para agendar boot via `chkntfs.exe /c C:`)
        *   `[6]` Saúde do disco SMART (`MaintenanceModule.CheckDiskHealth`)
        *   `[7]` Reset de rede (`NetworkModule.ResetNetwork` — `[S/N]`)
        *   `[8]` Info de rede (`NetworkModule.ShowNetworkInfo`)
        *   `[9]` Testes de conectividade (`NetworkModule.RunConnectivityTests`)
        *   `[10]` Limpar cache DNS (`NetworkModule.FlushDns`)
        *   `[11]` Conexões de Rede (`NetworkModule.OpenNetworkConnections`)
        *   `[12]` Limpar fila de impressão (`PrintingModule.ClearPrintQueue` — `[S/N]`)
        *   `[13]` Listar impressoras (`PrintingModule.ListPrinters`)
        *   `[14]` Informações do sistema (`DiagnosticsModule.ShowSystemInfo`)
        *   `[15]` Monitor de recursos/processos (`DiagnosticsModule.ShowProcessMonitor`)
        *   `[16]` Dispositivos com erro (`DriversUpdatesModule.ListErrorDevices`)
        *   `[17]` Buscar atualizações e drivers (`DriversUpdatesModule.SearchPendingUpdates`)
        *   `[18]` Backup de drivers (`DriversUpdatesModule.BackupDrivers`)
        *   `[19]` Abrir Windows Update (`DriversUpdatesModule.OpenWindowsUpdateSettings`)
        *   `[20]` Atualizar GPO computador (`ActiveDirectoryModule.ForceComputerGpUpdate`)
        *   `[21]` Relatório GPO computador (`ActiveDirectoryModule.ShowComputerGpResult`)
        *   `[22]` Diagnóstico AD, Canal Seguro, Relógio e Exchange (`ActiveDirectoryModule.RunDomainAndExchangeDiagnostics`)
        *   `[23]` Ver registro de ações deste PC (`AuditModule.ShowHostLogs`)
        *   `[0]` Sair
24. **`../../src/TecnicoBrasfort/Modules/MaintenanceModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Manutenção (construtor: `IProcessRunner`, `IHostEnvironment`, `AuditLogger`, `EnvironmentInfo`)
    *   **Proposed Changes:** Limpeza de `C:\Windows\Temp` e `C:\Users\*\AppData\Local\Temp`; `Dism.exe /Online /Cleanup-Image /StartComponentCleanup`; `sfc /scannow` (`useUnicodeOutput: true`, timeout 45 min); `DISM.exe /Online /Cleanup-Image /RestoreHealth`; `chkdsk C:` leitura + sub-opção `chkntfs.exe /c C:`; SMART via WMI (`MSStorageDriver_FailurePredictStatus` e `Win32_DiskDrive`).
25. **`../../src/TecnicoBrasfort/Modules/NetworkModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Rede (construtor: `IProcessRunner`, `AuditLogger`, `EnvironmentInfo`, `AppConfig`)
    *   **Proposed Changes:** Reset de rede na ordem `netsh winsock reset` -> `netsh int ip reset` -> `ipconfig /release` -> `ipconfig /flushdns` -> `ipconfig /renew` (timeout 60 s cada); `ipconfig /all`; testes `TcpClient` com timeout de `3000 ms` (Gateway, DNS, portas AD `53/88/389/445` contra `DomainFqdn`, porta `443` de `ExchangeCheckUrl` e `DnsTestTargets`); `ipconfig /flushdns`; abertura de `ncpa.cpl`.
26. **`../../src/TecnicoBrasfort/Modules/PrintingModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Impressão (construtor: `IProcessRunner`, `IHostEnvironment`, `AuditLogger`, `EnvironmentInfo`)
    *   **Proposed Changes:** Parar `Spooler` via `ServiceController` (tratando `StopPending` e encerrando `spoolsv.exe` se necessário), limpar `C:\Windows\System32\spool\PRINTERS\*`, reiniciar `Spooler`; listar `Win32_Printer` + `IHostEnvironment.GetUserPrinterConnections(info.InteractiveUserSid)`.
27. **`../../src/TecnicoBrasfort/Modules/DiagnosticsModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Diagnóstico (construtor: `IProcessRunner`, `AuditLogger`, `EnvironmentInfo`)
    *   **Proposed Changes:** Coletar `Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_BIOS` e `Win32_LogicalDisk`; listar top 15 processos ordenados por `WorkingSet64` via `Process.GetProcesses()` e oferecer abertura de `resmon.exe`.
28. **`../../src/TecnicoBrasfort/Modules/DriversUpdatesModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Drivers e Atualizações (construtor: `IProcessRunner`, `IHostEnvironment`, `AuditLogger`, `EnvironmentInfo`)
    *   **Proposed Changes:** Consultar `Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0`; validar >= 5 GB livres via `IHostEnvironment.GetAvailableFreeSpaceBytes` antes de `pnputil.exe /export-driver * <destino>` (padrão `C:\Temp\DriverBackup_<HOSTNAME>`, timeout 45 min); executar busca COM `Microsoft.Update.Session` (`IsInstalled=0`) dentro de `Task.Run` com espera limitada, indicador de tempo no console e tradução de `COMException`; abrir `ms-settings:windowsupdate`.
29. **`../../src/TecnicoBrasfort/Modules/ActiveDirectoryModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo AD / GPO / Exchange (construtor: `IProcessRunner`, `AuditLogger`, `EnvironmentInfo`, `AppConfig`)
    *   **Proposed Changes:** Executar `gpupdate.exe /target:computer /force`, `gpresult.exe /r /scope:computer`, `nltest.exe /sc_verify:<DomainFqdn>` e `/dsgetdc:<DomainFqdn>`, `w32tm.exe /stripchart /computer:<DomainFqdn> /samples:1 /dataonly` (parseando o offset numérico de segundos independente de idioma e alertando se `Math.Abs(offset) > 300`), `klist.exe` (explicitando escopo da sessão admin elevada), DNS `autodiscover.<DomainFqdn>`, e teste HTTPS em `ExchangeCheckUrl` habilitando `SecurityProtocolType.Tls12` e diferenciando erro TCP, erro SSL/certificado e código HTTP.
30. **`../../src/TecnicoBrasfort/Modules/AuditModule.cs`** (`Create`)
    *   **Type / Scope:** Módulo de Auditoria (construtor: `AuditLogger`)
    *   **Proposed Changes:** Exibir de forma paginada as linhas retornadas por `AuditLogger.ReadLogsForCurrentHost()`.

#### Testes Unitários (`tests/TecnicoBrasfort.Tests/`)

31. **`../../tests/TecnicoBrasfort.Tests/TecnicoBrasfort.Tests.csproj`** (`Create`)
    *   **Type / Scope:** Projeto de Testes (`net48`, compatível com C# 5.0 e runner local)
    *   **Proposed Changes:** Referenciar `TecnicoBrasfort.csproj` para uso opcional em ambientes com `.NET SDK` (`xUnit`), mantendo o código de testes compatível com C# 5.0 sem depender de NuGet quando compilado pelo `scripts/build.ps1 -RunTests`.
32. **`../../tests/TecnicoBrasfort.Tests/TestRunner.cs`** (`Create`)
    *   **Type / Scope:** Runner Nativo de Testes (C# 5.0 / BCL pura)
    *   **Proposed Changes:** Implementar classe estática `Assert` (`Equal`, `True`, `False`, `Throws`) e método `Main()` que executa todos os testes de `CoreTests` e `ModulesTests`, imprimindo resumo e retornando exit code `0` (sucesso) ou `1` (falha).
33. **`../../tests/TecnicoBrasfort.Tests/CoreTests.cs`** (`Create`)
    *   **Type / Scope:** Testes Unitários de Infraestrutura
    *   **Proposed Changes:** Validar `ArgumentEscaper` (aspas, espaços, rejeição de `\n`/`\r`/`\0`), `ConfigLoader` (defaults genéricos vs. JSON), `AuditLogger` (sanitização de `DOMINIO\TechAdmin`, criação do log primário e comportamento não bloqueante quando destino secundário falha).
34. **`../../tests/TecnicoBrasfort.Tests/ModulesTests.cs`** (`Create`)
    *   **Type / Scope:** Testes Unitários dos Módulos
    *   **Proposed Changes:** Validar ordem dos comandos do Reset de Rede, uso de `chkntfs.exe /c C:` no agendamento, validação >= 5 GB no backup de drivers, listagem de impressoras por SID e parsing do offset de `w32tm /stripchart` usando implementações fake/mock em memória de `IProcessRunner` e `IHostEnvironment`, sem efeitos colaterais no host.

### Verification Plan

> **Condição do Ambiente Atual:** Verificado via `dotnet --info` que o host atual possui runtimes .NET 6/8 sem `.NET SDK` instalado, porém conta com `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5.0). Todas as etapas abaixo são executáveis localmente sem SDK externo, com suporte adicional a `dotnet` quando disponível em outro host/CI.

*   **Compilação, Análise Estática e Testes Unitários (Host Atual):**
    *   Executar `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests`.
    *   Confirmar compilação limpa com `/warnaserror+` em C# 5.0, geração de `dist\TecnicoBrasfort.exe` (< 1 MB), `dist\SHA256SUMS.txt` e execução com exit code `0` de `dist\TecnicoBrasfort.Tests.exe`.
*   **Verificações Adicionais (Quando `.NET SDK` Estiver Disponível):**
    *   Executar `dotnet build -c Release`, `dotnet test`, `dotnet format --verify-no-changes` e `dotnet list package --vulnerable`.
*   **Segurança, Governança e Grafo (`AGENTS.md` Seção 13):**
    *   Confirmar ausência de segredos, IPs ou nomes reais da infraestrutura Brasfort no código e em `config.example.json`.
    *   Confirmar zero arquivos temporários/relatórios na raiz (todos isolados em `00-anything/`).
    *   Executar `npx graphify` para atualizar `graphify-out/` em paridade com o `AGENTS.md`.
    *   Validar fluxo Git conforme Regra 2 do `AGENTS.md`.
*   **Validação Manual em Estação/VM:**
    *   Testar execução via pendrive e via caminho UNC (`\\servidor\...`) como Admin sobre sessão de usuário padrão (e sessão RDP), validando detecção do usuário interativo, Reset de Rede sem travamento, agendamento com `chkntfs /c C:` e escrita dupla de logs (`ProgramData` + `Public\Desktop`).

--------------------------------------------------------------------------------

*Drafted with EGM (Elephant-Goldfish Model)*
