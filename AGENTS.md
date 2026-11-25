# AGENTS.md — Guia de Arquitetura, Governança e Regras do Projeto

> **🤖 LEIA ISTO PRIMEIRO.** Este arquivo é a fonte da verdade e o ponto de entrada obrigatório para qualquer modelo de IA ou agente autônomo (Cursor, Claude, Gemini, Copilot, Codex, ChatGPT etc.) que for analisar, criar ou modificar código neste projeto.
> Ele documenta a arquitetura, convenções, stack técnica, estrutura de pastas, regras operacionais, estratégia de testes, segurança e fluxo de tarefas. Leia-o **integralmente** antes de inspecionar ou alterar qualquer arquivo.

---

## ⚠️ REGRAS OBRIGATÓRIAS (ARQUIVO, GIT, GESTÃO & ESTRUTURAÇÃO)

### 1. Toda alteração estrutural DEVE atualizar este `AGENTS.md` no mesmo commit/tarefa

Considera-se "alteração estrutural" qualquer uma das seguintes ações (lista não exaustiva):

* Criar, renomear, mover ou remover arquivos e pastas em `src/`, `tests/`, `scripts/` ou na raiz.
* Criar, modificar ou remover **módulos de manutenção, serviços em `Core/`, formulários em `UI/` ou testes unitários**.
* Adicionar, atualizar (major) ou remover **pacotes NuGet** ou configurações no `.csproj` / `.sln`.
* Adicionar ou alterar **chaves de configuração** (e espelhar no `config.example.json`).
* Implementar ou alterar **regras de negócio**, modelos de log/fechamento, contratos de execução de processos ou validações de segurança.
* Adotar novas convenções de arquitetura, padrões de código, regras de segurança ou fluxos de build/assinatura.

**Como atualizar este arquivo após uma alteração estrutural:**
1. Atualize a seção correspondente neste documento (árvore de pastas, stack, configuração, regras de negócio, etc.).
2. Registre uma nova entrada na tabela de [Histórico de alterações deste arquivo](#-histórico-de-alterações-deste-arquivo) contendo data (`YYYY-MM-DD`) e resumo conciso do que mudou.
3. Se a alteração afetar o uso prático pelos técnicos ou o processo de build, atualize também o `README.md`.

> ⛔ **Atenção:** Se uma IA finalizar uma tarefa estrutural sem atualizar este arquivo e seu histórico, **a tarefa é considerada incompleta e inválida**.

---

### 2. Fluxo obrigatório de Git: Nova Feature/Fix Branch + Push ao GitHub

Para garantir rastreabilidade, isolamento de escopo e facilidade em code reviews e auditorias:

* **NUNCA desenvolva ou commite diretamente na branch `main` (ou `master`) no dia a dia**.
* Toda nova implementação, melhoria, refatoração ou correção DEVE ser desenvolvida em uma branch dedicada:
  ```bash
  git checkout -b feature/nome-da-funcionalidade   # Para novas funcionalidades
  git checkout -b fix/nome-da-correcao            # Para correção de bugs
  git checkout -b refactor/nome-do-ajuste         # Para refatorações estruturais
  ```
* **Commits atômicos, granulares e semânticos (Conventional Commits)**:
  * Nunca crie commits genéricos ou vagos (como `"wip"`, `"update"`, `"ajustes"`, `"fix"`).
  * Use o padrão semântico: `feat:`, `fix:`, `refactor:`, `style:`, `docs:`, `chore:`, `test:`, `sec:`.
  * Descreva com clareza o escopo da alteração no título e, sempre que aplicável, inclua no corpo o *o quê* e o *porquê* da mudança.
* **Push obrigatório ao repositório remoto ao concluir** (quando houver remote `origin` configurado):
  ```bash
  git push -u origin <nome-da-branch>
  ```

> 🚀 **Exceção Única de Inicialização (`Initial Commit` na `main`):**
> Na inicialização do projeto do zero, a criação da estrutura base (`.sln`, `.csproj`, arquivos ponteiro, `.gitignore`, `scripts/build.ps1` e este `AGENTS.md`) PODE e DEVE ser commitada diretamente na branch `main` como o commit inaugural (`chore: initial project setup`). A partir desse primeiro commit na `main`, a proibição de commits diretos torna-se 100% rígida e irrevogável para qualquer tarefa subsequente.

---

### 3. Gestão e Decomposição de Demandas em Issues (Linear / MCP & Modo Copiável)

Sempre que uma nova atualização, funcionalidade ou refatoração for solicitada, o agente DEVE organizar o plano de execução em etapas antes ou durante o desenvolvimento:

1. **Decomposição em Etapas Lógicas e Funcionais**:
   * Divida a solicitação em issues granulares, sequenciais e independentes.
   * Cada issue deve ser autossuficiente e conter título semântico, objetivo funcional, passo a passo técnico e critérios de aceitação (DoD).
2. **Criação de Issues com Linear MCP Ativo**:
   * Se o ambiente possuir a integração do **Linear via MCP** habilitada, o agente deve **criar e registrar automaticamente essas issues** no projeto/time correspondente do Linear.
3. **Formato Copiável para Agentes Sem Linear MCP**:
   * Em todas as respostas ou documentações de planejamento, forneça também um bloco claro com o conteúdo exato da **atividade do momento** e das etapas seguintes.
4. **Replanejamento e Adaptação Dinâmica de Planos Futuros**:
   * Se durante o desenvolvimento surgirem novos requisitos, bloqueios técnicos ou mudanças arquiteturais, o agente DEVE **atualizar imediatamente as issues e etapas futuras**.

---

### 4. Proibição de Arquivos Markdown, Relatórios EGM e Testes na Raiz (`00-anything/`)

* **PROIBIDO** gerar, salvar ou descarregar arquivos de rascunho, drafts, specs temporárias, testes avulsos, relatórios de auditoria/crítica/síntese de agentes EGM (`critic_report.md`, `goldfish_review_report.md`, `implementation_readiness_report.md`, `concepts_brief.md`, `design_draft.md` etc.) ou anotações na raiz do repositório.
* A raiz do repositório deve conter **exclusivamente** os arquivos de governança e configuração estruturais (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `README.md`, `TecnicoBrasfort.sln`, `.editorconfig`, `.gitignore`, `config.example.json`).
* **Diretório Obrigatório (`00-anything/`)**: Todos os arquivos de ideação, design, reviews de EGM, relatórios de subagentes ou documentações exploratórias DEVEM ser alocados dentro de subpastas datadas em `00-anything/`:
  ```text
  00-anything/{sequencial_2_digitos} - {nome-da-implementacao} {data-yyyy-mm-dd}/
  ```

---

### 5. Regra de Inicialização Estrita: Criação Genérica e Literal (Anti-Alucinação / Zero Pré-Concepção)

* **PROIBIÇÃO DE INFERIR DADOS INTERNOS OU REGRAS NÃO SOLICITADAS**: O agente **NUNCA** deve inventar nomes de servidores, IPs internos, URLs do Exchange ou senhas reais no código-fonte.
* **CONFIGURAÇÃO GENÉRICA POR PADRÃO**: Valores específicos de ambiente e hashes de senha DEVEM ficar exclusivamente em `config.json` (gitignored), mantendo placeholders genéricos em `config.example.json`.

---

### 6. Dependência Obrigatória: `graphify` (Sincronização Contínua com `AGENTS.md`)

* **Presença e Execução Obrigatória**: O projeto DEVE manter o mapeamento estrutural via **`graphify`** (`graphify update .` ou `npx graphify`).
* **Regra de Atualização Simultânea**: Toda alteração estrutural no projeto DEVE acionar a atualização do mapa do `graphify` no mesmo ciclo/commit em que o `AGENTS.md` for atualizado.
* **Configuração no Git**: A pasta `graphify-out/` deve constar no `.gitignore`.

---

## 1. Visão geral

O **Tecnico Brasfort** é uma aplicação desktop WinForms portátil (`.exe` único em **C# 5.0 / .NET Framework 4.8**, `/target:winexe`) destinada exclusivamente a técnicos e analistas de TI para execução de rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

* **Objetivo do projeto:** Centralizar rotinas recorrentes de suporte presencial e remoto em um executável gráfico único (pendrive ou pasta de rede por caminho UNC), com autenticação fail-closed, seleção múltipla e 5 combos, ajuda `[?]`, modo em segundo plano visível ao usuário final, aviso de reinicialização necessária e resumo de fechamento de chamado.
* **Público-alvo:** Técnicos e analistas de TI da Brasfort (uso não destinado a usuários finais).
* **Status atual:** `v1` CLI concluída; iniciando implantação da `v2` GUI WinForms conforme `00-anything/04 - design-tecnico-v2 2026-11-23/tecnico_brasfort_v2_design.md` (`SIR-146`..`SIR-151`).

---

## 2. Stack técnica

| Camada | Tecnologia | Observações |
| :--- | :--- | :--- |
| **Linguagem & Runtime** | **C# 5.0 / .NET Framework 4.8 (`net48`)** | Nativo no Windows 10/11 e Server 2016+; gera `.exe` `/target:winexe` único < 1 MB sem instalar runtime. Restrição estrita de C# 5.0 (`<LangVersion>5</LangVersion>`) para `csc.exe` v4.0.30319. |
| **Dependências Runtime** | **Zero NuGet em runtime (BCL-only)** | Assemblies nativos: `System`, `System.Core`, `System.Management`, `System.Runtime.Serialization`, `System.Xml`, `System.ServiceProcess`, `System.Net.Http`, `System.Windows.Forms`, `System.Drawing`. |
| **Interface Desktop** | **Windows Forms (código C# 5.0 puro, sem `.resx`)** | 6 formulários construídos via código com `[STAThread]`, `dpiAware`, elevação assistida, marshal assíncrono `BeginInvoke` e banner `TopMost` + `NotifyIcon`. |
| **APIs de Sistema** | **Win32 / WMI / CIM / Registry (`RegistryView.Registry64`)** | Usuário interativo (`explorer.exe` + `SessionId`), discos, impressoras (`HKU\<SID>`), serviços, Windows Update COM e chaves de reboot pendente. |
| **Manifesto & Elevação** | **`app.manifest` (`requireAdministrator` + `dpiAware`)** | Elevação UAC explícita e suporte a escala DPI corporativa. Execução via rede deve usar caminho UNC (`\\servidor\...`). |
| **Controle de Acesso** | **PBKDF2-SHA256 (`Rfc2898DeriveBytes`) + `FixedTimeEquals` XOR** | Validação contra hash com salt no `config.json` (política fail-closed). |
| **Testes Unitários** | **`TestRunner.cs` (BCL pura, 100% headless)** | Compilado via `csc.exe` (`scripts/build.ps1 -RunTests`) excluindo `UI\*.cs` e `Program.cs`. |
| **Assinatura Digital** | **`signtool.exe` (Authenticode)** *(opcional/recomendado)* | Suporte via `-CertThumbprint` no `scripts/build.ps1`. |
| **Mapeamento de Arquitetura** | **graphify** | Mapeamento estrutural contínuo atualizado em paridade com este `AGENTS.md`. |

---

## 3. Comandos principais

| Comando | Descrição |
| :--- | :--- |
| `powershell -ExecutionPolicy Bypass -File scripts/build.ps1` | Compila `dist/TecnicoBrasfort.exe` (`/target:winexe`) via `csc.exe` v4.0.30319 e gera `dist/SHA256SUMS.txt` |
| `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests` | Compila o executável principal e executa `dist/TecnicoBrasfort.Tests.exe` de forma headless (obrigatório antes de concluir tarefas) |
| `powershell -ExecutionPolicy Bypass -File scripts/New-AccessPasswordHash.ps1` | Gera o bloco JSON de salt + hash PBKDF2-SHA256 invocando `AccessGate.GenerateHash` do binário compilado |
| `graphify update .` | Atualiza o grafo estrutural do projeto em `graphify-out/` |

---

## 4. Configuração e variáveis de ambiente

Nenhuma informação sensível ou específica de infraestrutura interna (IPs, nomes de DCs, URLs internas ou senhas em texto claro) deve ser commitada no código-fonte.

* **`config.example.json`**: Template versionado no Git com valores genéricos de exemplo.
* **`config.json`**: Arquivo obrigatório em produção colocado ao lado do `.exe` (ignorado no `.gitignore`), carregado integralmente em memória no startup pelo `ConfigLoader.LoadWithStatus`.

| Chave (`config.json`) | Utilidade | Exemplo Genérico |
| :--- | :--- | :--- |
| `DomainFqdn` | Domínio AD alvo para diagnósticos (`nltest`, `w32tm`, `autodiscover`) | `corp.example.local` |
| `DnsTestTargets` | Alvos internos/externos para testes de conectividade | `["1.1.1.1", "8.8.8.8"]` |
| `ExchangeCheckUrl` | URL HTTPS para checagem de alcançabilidade do Exchange/OWA | `https://mail.example.local/owa` |
| `LogDirectoryOverride` | Sobrescrita opcional do diretório secundário de logs | `""` (usa `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\`) |
| `RequireAccessPassword` | Ativa/desativa tela de senha (`true` por padrão em memória) | `false` *(apenas no template de exemplo)* |
| `AccessSaltBase64` | Salt aleatório Base64 (>= 16 bytes) para PBKDF2-SHA256 | `""` |
| `AccessHashBase64` | Hash derivado Base64 (32 bytes) para PBKDF2-SHA256 | `""` |
| `AccessIterations` | Iterações PBKDF2 (mínimo `100000`) | `100000` |
| `SkipAccessGateWhenElevated` | Dispensa `AccessGateDialog` quando o token real já estiver elevado (`false` por padrão) | `false` *(use `true` em produção para evitar dupla senha)* |

> ⚠️ **Princípio de Zero Segredos:** Nunca armazene senhas em texto claro no código-fonte, no `.exe`, no `config.json` ou no Git.

---

## 5. Estrutura de pastas e arquivos

```text
Tecnico Brasfort/
├── 00-anything/                         # Diretório OBRIGATÓRIO para drafts, specs, relatórios EGM e rascunhos
│   ├── 01 - brainstorm-inicial 2026-11-19/
│   ├── 02 - design-tecnico 2026-11-23/
│   ├── 03 - painel-gui-combos-chamado 2026-11-23/
│   ├── 04 - design-tecnico-v2 2026-11-23/
│   └── 05 - elevacao-adm-assistida 2026-11-24/
├── AGENTS.md                            # Fonte da verdade para IAs e agentes (este arquivo)
├── CLAUDE.md, GEMINI.md                 # Arquivos ponteiro apontando para AGENTS.md
├── .github/copilot-instructions.md      # Arquivo ponteiro apontando para AGENTS.md
├── README.md                            # Documentação humana e guia de build/uso para técnicos
├── .editorconfig                        # Regras de estilo e formatação C#
├── .gitignore                           # Ignora bin/, obj/, dist/, graphify-out/, config.json, *.log
├── TecnicoBrasfort.sln                  # Solution .NET
├── config.example.json                  # Template público de configuração
├── scripts/
│   ├── build.ps1                        # Build WinForms (/target:winexe + /win32icon), testes headless e SHA-256
│   ├── New-AccessPasswordHash.ps1       # Gerador local de salt + hash PBKDF2-SHA256 via AccessGate
│   └── New-AppIcon.ps1                  # Gerador do icone padrao multi-resolucao (16/32/48/256) em Assets/app.ico
├── dist/                                # Saída compilada local (gitignored)
├── src/
│   └── TecnicoBrasfort/
│       ├── TecnicoBrasfort.csproj       # Projeto net48 WinExe (LangVersion 5) com app.manifest e ApplicationIcon
│       ├── Assets/
│       │   └── app.ico                  # Icone padrao multi-resolucao (#303841, #76ABAE, #FF5722)
│       ├── app.manifest                 # Manifesto UAC (requireAdministrator + dpiAware)
│       ├── Program.cs                   # [STAThread], SessionApplicationContext, handlers globais, AccessGate e MainForm
│       ├── Core/                        # Infraestrutura compartilhada
│       │   ├── Models.cs                # DTOs (AppConfig fail-closed, ActionDefinition, ComboDefinition, etc.)
│       │   ├── IExecutionObserver.cs    # Observador de saída/progresso (NullExecutionObserver)
│       │   ├── UnattendedSessionState.cs# Máquina de estados headless do Modo Técnico Ausente
│       │   ├── AccessGate.cs            # Validação fail-closed PBKDF2-SHA256 + FixedTimeEquals XOR
│       │   ├── ElevationPolicy.cs       # Decisão de elevação (headless), parsing de credenciais e erros Win32
│       │   ├── ElevationService.cs      # Detecção UAC 64-bit, LogonUserW, WNetGetConnectionW e relançamento
│       │   ├── ActionCatalog.cs         # Catálogo das 21 ações em lote, 5 Ferramentas Rápidas, 5 Combos e [?]
│       │   ├── TicketSummaryBuilder.cs  # Gerador de texto de fechamento de chamado
│       │   ├── BatchExecutionEngine.cs  # Orquestrador de fila tolerante a falhas e detecção de reboot
│       │   ├── ArgumentEscaper.cs       # Escaping e validação estrita de argumentos no net48
│       │   ├── IProcessRunner.cs        # Contrato de execução de processos externos com CancellationToken
│       │   ├── ProcessRunner.cs         # Streaming stdout/stderr, cancelamento via taskkill /F /T /PID
│       │   ├── IHostEnvironment.cs      # Contrato consolidado de arquivos, discos, registro e reboot
│       │   ├── HostEnvironmentAdapter.cs# Implementação real (System.IO, RegistryView.Registry64)
│       │   ├── EnvironmentContext.cs    # Detecta admin elevado, usuário interativo (SessionId) e origem UNC
│       │   ├── AuditLogger.cs           # Logger thread-safe (lock) + SaveTicketSummary
│       │   └── ConfigLoader.cs          # LoadWithStatus em memória via DataContractJsonSerializer
│       ├── UI/                          # Formulários WinForms em código C# 5.0 puro (sem .resx)
│       │   ├── AppIconProvider.cs       # Cache único do ícone padrão via ExtractAssociatedIcon (fallback SystemIcons.Shield)
│       │   ├── ElevationCredentialDialog.cs # Diálogo de credencial TI (EnableLUA = 0) com relançamento inline
│       │   ├── AccessGateDialog.cs      # Diálogo de senha (até 3 tentativas)
│       │   ├── ActionHelpDialog.cs      # Modal [?] com explicação e cópia resiliente de comando
│       │   ├── BackgroundStatusBannerForm.cs # Banner TopMost sem foco + NotifyIcon
│       │   ├── ExecutionProgressForm.cs # Progresso modeless, log ao vivo, segundo plano e fechamento
│       │   └── MainForm.cs              # Cabeçalho, 5 combos, checkboxes + [?], Ferramentas Rápidas
│       └── Modules/                     # Módulos independentes (com sobrecargas v1 + v2)
│           ├── MaintenanceModule.cs     # Temp, DISM, SFC, chkdsk + chkntfs /c C:, SMART
│           ├── NetworkModule.cs         # Reset de rede, IP/DNS, conectividade (3000 ms), ncpa.cpl
│           ├── PrintingModule.cs        # Limpar spooler/fila, listar impressoras (máquina + usuário)
│           ├── DiagnosticsModule.cs     # Info do sistema WMI, top 15 WorkingSet64 e resmon.exe
│           ├── DriversUpdatesModule.cs  # Dispositivos com erro, backup >= 5 GB, Windows Update COM e reparo WU/Store/ProfileList/WSearch
│           ├── ActiveDirectoryModule.cs # gpupdate, gpresult, nltest, w32tm /stripchart, Exchange TLS 1.2
│           └── AuditModule.cs           # Leitura de logs do PC para GUI
└── tests/
    └── TecnicoBrasfort.Tests/           # Testes unitários C# 5.0 headless com fakes em memória
        ├── TecnicoBrasfort.Tests.csproj
        ├── TestRunner.cs                # Runner nativo BCL (Assert + Main)
        ├── CoreTests.cs                 # ArgumentEscaper, ConfigLoader, AuditLogger
        ├── ModulesTests.cs              # Testes dos módulos sem side-effects no host
        └── V2CoreTests.cs               # AccessGate, ActionCatalog, BatchExecutionEngine, TicketSummaryBuilder
```

---

## 6. Arquitetura e fluxo de dados

```text
Program.cs ([STAThread] + handlers globais)
  ├─ ElevationService / ElevationPolicy (checa Admin + EnableLUA 64-bit; relança via runas ou ElevationCredentialDialog)
  ├─ ConfigLoader.LoadWithStatus (carrega config.json em memória no startup)
  ├─ AccessGate + AccessGateDialog (fail-closed por padrão; pula se SkipAccessGateWhenElevated && Admin real)
  ├─ EnvironmentContext (gera EnvironmentInfo: origem UNC/USB, usuário interativo via SessionId + SID)
  ├─ AuditLogger (thread-safe com lock; grava logs e resumos de fechamento)
  └─ MainForm (3 zonas)
       ├─ [?] -> ActionHelpDialog
       ├─ [Ferramentas Rápidas] -> execução direta (Backup de Drivers em worker thread)
       └─ [Executar Selecionadas] -> Confirmação pré-voo -> ExecutionProgressForm (Show modeless)
            ├─ [Minimizar] -> BackgroundStatusBannerForm (TopMost sem foco) + NotifyIcon
            └─ BatchExecutionEngine -> Modules/* via IProcessRunner + IHostEnvironment -> TicketSummaryBuilder
```

---

## 7. Modelo de dados e estado

| Entidade | Descrição | Onde é persistida / consumida |
| :--- | :--- | :--- |
| `EnvironmentInfo` | Hostname, OsVersion, ElevatedAdmin, InteractiveUser, InteractiveUserSid, OriginDirectory e IsUncOrigin. | Memória (em runtime) / Cabeçalho da UI, Modules e Logs |
| `ActionLogEntry` | Timestamp, Hostname, TechAdmin, InteractiveUser, ModuleName, ActionName, ExitCode e Summary. | Log primário (`ProgramData`) e cópia secundária (`Public\Desktop`) |
| `AppConfig` | DomainFqdn, DnsTestTargets, ExchangeCheckUrl, LogDirectoryOverride, RequireAccessPassword (`true` default), AccessSaltBase64, AccessHashBase64, AccessIterations, SkipAccessGateWhenElevated (`false` default). | `config.json` carregado em memória no startup |
| `ActionDefinition` / `ComboDefinition` | Metadados das 20 ações em lote, 5 Ferramentas Rápidas, 5 Combos e ajuda `[?]`. | `ActionCatalog` (memória) / `MainForm`, `BatchExecutionEngine` |
| `ActionExecutionResult` / `BatchExecutionReport` | Resultado estruturado de cada etapa, flag `RebootRequired`, `TicketSummaryText` e `SavedSummaryPath`. | `BatchExecutionEngine` / `ExecutionProgressForm` / `Fechamento_*.txt` |

---

## 8. Regras de negócio

1. **Autenticação Fail-Closed e Bypass Seguro por Token:** `AppConfig.RequireAccessPassword` é `true` por padrão em memória. Se `config.json` estiver ausente, corrompido ou sem hash válido, a abertura do painel é bloqueada (exceto se um `config.json` íntegro definir explicitamente `"RequireAccessPassword": false`). Quando `"SkipAccessGateWhenElevated": true` em um `config.json` íntegro e o token real do Windows possuir `WindowsBuiltInRole.Administrator`, o `AccessGateDialog` é dispensado sem depender de flags de linha de comando.
2. **Ordem do Combo 1 (Limpeza Lógica / Travamentos):** Executar sempre `Temp -> DISM /RestoreHealth -> SFC /scannow -> WinSxS`.
3. **Execução Tolerante a Falhas e Sem Bloqueios:** Em erro de uma etapa da fila, o `BatchExecutionEngine` registra o resultado e avança para a próxima; confirmações destrutivas/remotas ocorrem uma única vez no pré-voo antes de iniciar a fila.
4. **Modo Segundo Plano Protegido:** O banner visual flutuante informa o usuário final sem roubar foco e bloqueia fechamento pelo usuário (`Alt+F4`) enquanto a fila estiver ativa.
5. **Persistência Garantida de Fechamento e Logs:** O `AuditLogger` usa `lock` para escrita concorrente e salva o resumo de fechamento em disco antes mesmo da interação do técnico na tela final.
6. **Elevação, Contexto de Usuário e UNC:** Mantidas todas as regras da `v1` (`explorer.exe` + `SessionId`, abertura pela rede apenas por caminho UNC `\\servidor\...`, `chkntfs /c C:`, `pnputil` >= 5 GB livres).

---

## 9. Convenções de código e boas práticas

* **Restrição C# 5.0 Estrita + WinForms sem `.resx`:** Todo o código em `src/` e `tests/` deve compilar com `csc.exe` v4.0.30319 (`/warnaserror+`). Proibido uso de `$"..."`, `?.`, `nameof()`, inicializadores de auto-propriedades, `=>`, `out var`, `using var` ou arquivos `.resx`.
* **Nomenclatura C#:** `PascalCase` para tipos/métodos/propriedades, `camelCase` para parâmetros/locais, `_camelCase` para campos privados.
* **Comandos Externos Seguros:** Sempre usar `ProcessStartInfo.Arguments` via `ArgumentEscaper.JoinArguments(args)`.
* **Git e Commits:** Padrão **Conventional Commits** em branches dedicadas (`feature/...`, `fix/...`, `refactor/...`).

---

## 10. Estratégia de Testes

O projeto adota **testes unitários isolados e 100% headless com fakes em memória (`TestRunner.cs` em C# 5.0 / BCL pura)**, excluindo `UI\*.cs` e `Program.cs` do binário de testes.

| Parâmetro | Detecção da Máquina Atual (Atualizado Dinamicamente) |
| :--- | :--- |
| **Sistema Operacional Detectado** | Windows 10/11 (PowerShell) |
| **Host .NET (`dotnet --info`)** | Runtimes .NET 6.0.9 e 8.0.27 instalados; **sem `.NET SDK`** |
| **Compilador Nativo Ativo** | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5.0 / .NET Framework 4.8) |

---

## 11. Estratégia de Segurança

1. **Zero Segredos no Repositório:** Nenhuma senha em texto claro, token ou IP interno real da Brasfort deve ser commitado. Senha de acesso armazenada apenas como hash PBKDF2-SHA256 com salt (>= 16 bytes, >= 100.000 iterações) em `config.json`.
2. **Comparação em Tempo Constante:** `AccessGate.FixedTimeEquals` implementado via acumulador XOR sem early-return.
3. **Prevenção de Command Injection:** `ArgumentEscaper` em toda chamada externa.
4. **Zero NuGet em Runtime + Assinatura Authenticode:** Suporte via `scripts/build.ps1 -CertThumbprint`.

---

## 12. Cuidados e armadilhas comuns

1. **Arquivos soltos na raiz:** Use sempre `00-anything/{sequencial} - {nome} {data}/`.
2. **`CryptographicOperations.FixedTimeEquals` não existe no `.NET Framework 4.8`:** Usar `AccessGate.FixedTimeEquals` manual via XOR.
3. **WinForms `ShowDialog()` vs. `Hide()`:** Usar `Show(owner)` modeless em `ExecutionProgressForm` para permitir minimizar para o banner sem encerrar o formulário.
4. **Cross-thread UI e `Clipboard`:** Sempre usar `BeginInvoke` protegido por `IsDisposed || !IsHandleCreated`, `[STAThread]` no `Main` e `Clipboard.SetDataObject(text, true, 5, 100)` com `try/catch (ExternalException)`.
5. **Esquecer de atualizar o `AGENTS.md` e o `graphify`:** Toda mudança estrutural exige atualização simultânea deste arquivo e do grafo.

---

## 13. Checklist para agentes ao finalizar uma tarefa

* [ ] **Isolamento de artefatos na raiz:** Nenhum rascunho, spec ou relatório EGM solto na raiz (todos dentro de `00-anything/{sequencial} - {nome} {data}/`).
* [ ] **Zero hardcoding interno / Zero senhas:** Nenhuma senha em texto claro, IP ou servidor real da Brasfort commitado.
* [ ] **Sincronização com `graphify`:** O mapa de grafo em `graphify-out/` foi atualizado em paridade com as alterações estruturais.
* [ ] **Build e Testes Unitários:** `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests` executado com sucesso.
* [ ] **Formatação e Segurança:** Código em C# 5.0 estrito com `/warnaserror+` e zero `.resx`.
* [ ] **Documentação atualizada:** Árvore de pastas, regras e **Histórico de alterações** deste `AGENTS.md` atualizados.
* [ ] **Git:** Branch dedicada criada, commits semânticos realizados e enviados via `git push -u origin <branch>` quando houver remote.

---

## 📜 Histórico de alterações deste arquivo

| Data | Responsável | Alteração Realizada |
| :--- | :--- | :--- |
| 2026-11-19 | Agente IA | Criação inicial do `AGENTS.md` do projeto **Tecnico Brasfort** (`v1` CLI em C# / .NET Framework 4.8). |
| 2026-11-23 | Agente IA | Alinhamento com `tecnico_brasfort_design.md` (`SIR-139`): C# 5.0 estrito, `IHostEnvironment`, `ArgumentEscaper` e `TestRunner.cs`. |
| 2026-11-23 | Agente IA | Evolução para `v2` GUI WinForms (`SIR-146` / `tecnico_brasfort_v2_design.md`): `/target:winexe`, `System.Windows.Forms`/`System.Drawing`, `dpiAware`, remoção de `ConsoleMenu.cs`, novos arquivos `Core/` e `UI/`, autenticação fail-closed PBKDF2-SHA256 e `scripts/New-AccessPasswordHash.ps1`. |
| 2026-11-24 | Agente IA | Evolução `v2.1` (`SIR-152` / `elevacao_adm_assistida_design.md`): detecção automática de privilégio/UAC, `Core/ElevationPolicy.cs`, `Core/ElevationService.cs`, `UI/ElevationCredentialDialog.cs` (6 formulários) e `SkipAccessGateWhenElevated`. |
| 2026-11-24 | Agente IA | Evolução `v2.2` (`SIR-155` / `tecnico_ausente_e_icone_design.md`): ícone padrão multi-resolução (`scripts/New-AppIcon.ps1`, `Assets/app.ico`, `/win32icon:`) e `UI/AppIconProvider.cs` aplicado às 6 janelas visíveis e `NotifyIcon`. |
| `2026-11-24` | Agente IA | Evolução `v2.2` (`SIR-156`): `Core/UnattendedSessionState.cs` headless, `SessionApplicationContext` em `Program.cs`, desinscrição de eventos/guarda cross-thread em `MainForm.cs` e cobertura em `V2CoreTests.cs`. |
| 2026-11-24 | Agente IA | Evolução `v2.2` (`SIR-157`): Modo Técnico Ausente na `ExecutionProgressForm` (`_btnUnattended` -> `_lblUnattendedActive`, encerramento total ao fechar após conclusão) e `BackgroundStatusBannerForm.ForceClose()`. |
| 2026-11-25 | Agente IA | Evolução `v2.3` (`SIR-160`): verificação e reparo de Windows Update/Store (`drv_wu_repair`), restauração de `ProfileList` (`S-1-5-18/19/20`), backup/limpeza de caches `DoSvc`/`SoftwareDistribution` e recuperação condicional do `WSearch` (`Windows.db`). |
