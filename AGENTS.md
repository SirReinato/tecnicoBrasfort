# AGENTS.md — Guia de Arquitetura, Governança e Regras do Projeto

> **🤖 LEIA ISTO PRIMEIRO.** Este arquivo é a fonte da verdade e o ponto de entrada obrigatório para qualquer modelo de IA ou agente autônomo (Cursor, Claude, Gemini, Copilot, Codex, ChatGPT etc.) que for analisar, criar ou modificar código neste projeto.
> Ele documenta a arquitetura, convenções, stack técnica, estrutura de pastas, regras operacionais, estratégia de testes, segurança e fluxo de tarefas. Leia-o **integralmente** antes de inspecionar ou alterar qualquer arquivo.

---

## ⚠️ REGRAS OBRIGATÓRIAS (ARQUIVO, GIT, GESTÃO & ESTRUTURAÇÃO)

### 1. Toda alteração estrutural DEVE atualizar este `AGENTS.md` no mesmo commit/tarefa

Considera-se "alteração estrutural" qualquer uma das seguintes ações (lista não exaustiva):

* Criar, renomear, mover ou remover arquivos e pastas em `src/`, `tests/`, `scripts/` ou na raiz.
* Criar, modificar ou remover **módulos de manutenção, serviços em `Core/`, camadas de UI ou testes unitários**.
* Adicionar, atualizar (major) ou remover **pacotes NuGet** ou configurações no `.csproj` / `.sln`.
* Adicionar ou alterar **chaves de configuração** (e espelhar no `config.example.json`).
* Implementar ou alterar **regras de negócio**, modelos de log, contratos de execução de processos ou validações de segurança.
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
   * Divida a solicitação em issues granulares, sequenciais e independentes (ex: *Fase 1: Setup e Build*, *Fase 2: Core e Abstrações*, *Fase 3: Módulos*, *Fase 4: UI e Testes*).
   * Cada issue deve ser autossuficiente e conter:
     - **Título claro e semântico** (ex: `feat(core): implementar EnvironmentContext para detecção de usuário interativo`).
     - **Objetivo funcional**: o que essa etapa entrega e por que é necessária.
     - **Passo a passo técnico**: arquivos exatos a criar/alterar e APIs envolvidas.
     - **Critérios de aceitação (DoD)**: como validar que a etapa foi concluída com sucesso.

2. **Criação de Issues com Linear MCP Ativo**:
   * Se o ambiente possuir a integração do **Linear via MCP** habilitada, o agente deve **criar e registrar automaticamente essas issues** no projeto/time correspondente do Linear, associando labels, prioridades e estimativas adequadas.

3. **Formato Copiável para Agentes Sem Linear MCP**:
   * Em todas as respostas ou documentações de planejamento, forneça também um bloco claro com o conteúdo exato da **atividade do momento** e das etapas seguintes, para que outro agente sem Linear MCP entenda o contexto, as restrições e o código esperado sem instruções adicionais.

4. **Replanejamento e Adaptação Dinâmica de Planos Futuros**:
   * Se durante o desenvolvimento surgirem novos requisitos, bloqueios técnicos ou mudanças arquiteturais, o agente DEVE **atualizar imediatamente as issues e etapas futuras**. Nunca prossiga com um plano defasado.

---

### 4. Proibição de Arquivos Markdown, Relatórios EGM e Testes na Raiz (`00-anything/`)

* **PROIBIDO** gerar, salvar ou descarregar arquivos de rascunho, drafts, specs temporárias, testes avulsos, relatórios de auditoria/crítica/síntese de agentes EGM (`critic_report.md`, `goldfish_review_report.md`, `implementation_readiness_report.md`, `concepts_brief.md`, `design_draft.md` etc.) ou anotações na raiz do repositório.
* A raiz do repositório deve conter **exclusivamente** os arquivos de governança e configuração estruturais (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `README.md`, `TecnicoBrasfort.sln`, `.editorconfig`, `.gitignore`, `config.example.json`).
* **Diretório Obrigatório (`00-anything/`)**: Todos os arquivos de ideação, design, reviews de EGM, relatórios de subagentes, artefatos de testes exploratórios ou documentações temporárias DEVEM ser alocados dentro de `00-anything/` na raiz do projeto.
* **Padrão Obrigatório de Subpastas por Implementação**:
  ```text
  00-anything/{sequencial_2_digitos} - {nome-da-implementacao} {data-yyyy-mm-dd}/
  ```
* **Isolamento Total do Escopo**: Todos os arquivos que essa estruturação, teste ou execução produzir devem ficar exclusivamente dentro dessa subpasta.
* **Numeração Sequencial Contínua**: Verifique as subpastas existentes em `00-anything/` e incremente o numeral de 2 dígitos (`01`, `02`, `03`...) em ordem cronológica crescente.

---

### 5. Regra de Inicialização Estrita: Criação Genérica e Literal (Anti-Alucinação / Zero Pré-Concepção)

Ao iniciar ou expandir o projeto a partir deste `AGENTS.md`, o agente DEVE seguir o princípio de **Construção Literal e Genérica**:

* **PROIBIÇÃO DE INFERIR DADOS INTERNOS OU REGRAS NÃO SOLICITADAS**: O agente **NUNCA** deve inventar nomes de servidores, IPs internos, URLs do Exchange ou comandos destrutivos que não foram explicitamente especificados pelo usuário.
* **CONFIGURAÇÃO GENÉRICA POR PADRÃO**: Valores específicos de ambiente (domínio AD, servidores DNS de teste, URL de OWA/Autodiscover) DEVEM ficar em `config.example.json` com placeholders genéricos (ex.: `corp.example.local`).
* Nenhuma funcionalidade fora do escopo aprovado deve ser criada sem solicitação explícita.

---

### 6. Dependência Obrigatória: `graphify` (Sincronização Contínua com `AGENTS.md`)

* **Presença e Execução Obrigatória**: O projeto DEVE manter o mapeamento estrutural via **`graphify`** (`npx graphify` ou CLI global equivalente).
* **Simbiose e Complementaridade**: O `AGENTS.md` fornece o contexto semântico, arquitetural e as regras operacionais para as IAs, enquanto o `graphify` mapeia visualmente as relações estruturais e o grafo de dependências do código C#/.NET.
* **Regra de Atualização Simultânea**: Toda alteração estrutural no projeto (criação de módulos, serviços em `Core/`, interfaces ou projetos de teste) DEVE acionar a atualização do mapa do `graphify` no mesmo ciclo/commit em que o `AGENTS.md` for atualizado.
* **Configuração no Git**: A pasta gerada pelo graphify (`graphify-out/`) deve constar obrigatoriamente no `.gitignore` para evitar poluição do repositório, mas ser mantida atualizada localmente para consumo das IAs.

---

## 1. Visão geral

O **Tecnico Brasfort** é uma ferramenta CLI portátil (`.exe` nativo em **C# 5.0 / .NET Framework 4.8**) destinada exclusivamente a técnicos e analistas de TI para execução de rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO/Exchange e auditoria em estações Windows.

* **Objetivo do projeto:** Centralizar rotinas recorrentes de suporte presencial e remoto em um único executável portátil (pendrive ou pasta de rede por caminho UNC), com tratamento seguro de elevação UAC sobre a sessão do usuário final e registro auditável de ações.
* **Público-alvo:** Técnicos e analistas de TI da Brasfort (uso não destinado a usuários finais).
* **Status atual:** Design técnico aprovado em `00-anything/02 - design-tecnico 2026-11-23/tecnico_brasfort_design.md`; estrutura inicial e build em implantação (`SIR-139`).

---

## 2. Stack técnica

| Camada | Tecnologia | Observações |
| :--- | :--- | :--- |
| **Linguagem & Runtime** | **C# 5.0 / .NET Framework 4.8 (`net48`)** | Nativo no Windows 10/11 e Server 2016+; gera `.exe` único < 1 MB sem instalar runtime. Restrição estrita de C# 5.0 (`<LangVersion>5</LangVersion>`) para compatibilidade total com `csc.exe` v4.0.30319. |
| **Dependências Runtime** | **Zero NuGet em runtime (BCL-only)** | Uso exclusivo de assemblies nativos (`System`, `System.Core`, `System.Management`, `System.Runtime.Serialization`, `System.Xml`, `System.ServiceProcess`, `System.Net.Http`). |
| **Interface CLI** | **System.Console** | Streaming em tempo real de `stdout`/`stderr`, suporte a UTF-8 e cores legíveis. |
| **APIs de Sistema** | **Win32 / WMI / CIM / Registry (`Microsoft.Win32`)** | Acesso a usuário interativo (`explorer.exe` + `SessionId`), discos, impressoras (`HKU\<SID>`), serviços e Windows Update COM. |
| **Manifesto & Elevação** | **`app.manifest` (`requireAdministrator`)** | Garante elevação UAC explícita. Execução via rede deve usar caminho UNC (`\\servidor\...`) ou pendrive (nunca letra mapeada `Z:\`). |
| **Testes Unitários** | **`TestRunner.cs` (BCL pura) + `xUnit` (opcional com SDK)** | Runner nativo compilado via `csc.exe` (`scripts/build.ps1 -RunTests`) com mocks em memória de `IProcessRunner` e `IHostEnvironment`. |
| **Qualidade & Formatação** | **`.editorconfig` + `csc.exe /warnaserror+`** | Com `.NET SDK` disponível, também `dotnet format --verify-no-changes`. |
| **Segurança & SAST** | **Análise de zero segredos + `ArgumentEscaper`** | Com `.NET SDK` disponível, também `dotnet list package --vulnerable`. |
| **Assinatura Digital** | **`signtool.exe` (Authenticode)** *(opcional/recomendado)* | Suporte via `-CertThumbprint` no `scripts/build.ps1` para assinatura interna via AD CS / GPO (*Trusted Publishers*). |
| **Mapeamento de Arquitetura** | **graphify** | Mapeamento estrutural contínuo atualizado em paridade com este `AGENTS.md`. |

---

## 3. Comandos principais

| Comando | Descrição |
| :--- | :--- |
| `powershell -ExecutionPolicy Bypass -File scripts/build.ps1` | Compila `dist/TecnicoBrasfort.exe` via `csc.exe` v4.0.30319 (ou `dotnet build`) e gera `dist/SHA256SUMS.txt` |
| `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests` | Compila o executável principal e executa `dist/TecnicoBrasfort.Tests.exe` (obrigatório antes de concluir tarefas) |
| `dotnet build -c Release` | Compila via `.NET SDK` (quando disponível no ambiente) |
| `dotnet test` | Executa testes via `.NET SDK` (quando disponível no ambiente) |
| `dotnet format --verify-no-changes` | Verifica formatação e estilo de código (quando `.NET SDK` estiver disponível) |
| `npx graphify` | Atualiza o grafo estrutural do projeto em `graphify-out/` |

---

## 4. Configuração e variáveis de ambiente

Nenhuma informação sensível ou específica de infraestrutura interna (IPs, nomes de DCs, URLs internas ou credenciais) deve ser commitada no código-fonte.

* **`config.example.json`**: Template versionado no Git com valores genéricos de exemplo.
* **`config.json`**: Arquivo opcional colocado ao lado do `.exe` em produção (ignorado no `.gitignore` caso contenha dados internos), carregado integralmente em memória no startup pelo `ConfigLoader`.

| Chave (`config.json`) | Utilidade | Exemplo Genérico |
| :--- | :--- | :--- |
| `DomainFqdn` | Domínio AD alvo para diagnósticos (`nltest`, `w32tm`, `autodiscover`) | `corp.example.local` |
| `DnsTestTargets` | Alvos internos/externos para testes de conectividade | `["1.1.1.1", "8.8.8.8"]` |
| `ExchangeCheckUrl` | URL HTTPS para checagem de alcançabilidade do Exchange/OWA | `https://mail.example.local/owa` |
| `LogDirectoryOverride` | Sobrescrita opcional do diretório secundário de logs | `""` (usa `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\`) |

> ⚠️ **Princípio de Zero Segredos:** Nunca armazene senhas administrativas, tokens ou chaves privadas no `config.json` nem no binário `.exe`. A elevação ocorre exclusivamente via prompt UAC nativo do Windows.

---

## 5. Estrutura de pastas e arquivos

```text
Tecnico Brasfort/
├── 00-anything/                         # Diretório OBRIGATÓRIO para drafts, specs, relatórios EGM e rascunhos
│   ├── 01 - brainstorm-inicial 2026-11-19/
│   └── 02 - design-tecnico 2026-11-23/
├── AGENTS.md                            # Fonte da verdade para IAs e agentes (este arquivo)
├── CLAUDE.md, GEMINI.md                 # Arquivos ponteiro apontando para AGENTS.md
├── .github/copilot-instructions.md      # Arquivo ponteiro apontando para AGENTS.md
├── README.md                            # Documentação humana e guia de build/uso para técnicos
├── .editorconfig                        # Regras de estilo e formatação C#
├── .gitignore                           # Ignora bin/, obj/, dist/, graphify-out/, config.json, *.log
├── TecnicoBrasfort.sln                  # Solution .NET
├── config.example.json                  # Template público de configuração
├── scripts/
│   └── build.ps1                        # Build via csc.exe (ou dotnet), execução de testes e SHA-256
├── dist/                                # Saída compilada local (gitignored)
├── src/
│   └── TecnicoBrasfort/
│       ├── TecnicoBrasfort.csproj       # Projeto net48 (LangVersion 5) com app.manifest
│       ├── app.manifest                 # Manifesto UAC (requireAdministrator)
│       ├── Program.cs                   # Bootstrap, carga de config em memória e loop do menu
│       ├── Core/                        # Infraestrutura compartilhada
│       │   ├── Models.cs                # DTOs: AppConfig, ProcessResult, ActionLogEntry, EnvironmentInfo
│       │   ├── ArgumentEscaper.cs       # Escaping e validação estrita de argumentos no net48
│       │   ├── IProcessRunner.cs        # Contrato de execução de processos externos
│       │   ├── ProcessRunner.cs         # Streaming stdout/stderr, OEM/Unicode e timeouts
│       │   ├── IHostEnvironment.cs      # Contrato consolidado de arquivos, discos e registro
│       │   ├── HostEnvironmentAdapter.cs# Implementação real (System.IO, DriveInfo, Registry.Users)
│       │   ├── EnvironmentContext.cs    # Detecta admin elevado, usuário interativo (SessionId) e origem UNC
│       │   ├── AuditLogger.cs           # Escrita dupla de logs (ProgramData + Public\Desktop)
│       │   └── ConfigLoader.cs          # Carregamento em memória via DataContractJsonSerializer
│       ├── UI/                          # Renderização do painel CLI e prompts de confirmação
│       │   └── ConsoleMenu.cs           # Menu 1..23 + 0 Sair e confirmações [S/N]
│       └── Modules/                     # Módulos independentes por categoria do painel
│           ├── MaintenanceModule.cs     # Temp, WinSxS, SFC, DISM, chkdsk + chkntfs /c C:, SMART
│           ├── NetworkModule.cs         # Reset de rede, IP/DNS, conectividade (3000 ms), ncpa.cpl
│           ├── PrintingModule.cs        # Limpar spooler/fila, listar impressoras (máquina + usuário)
│           ├── DiagnosticsModule.cs     # Info do sistema WMI, top 15 WorkingSet64 e resmon.exe
│           ├── DriversUpdatesModule.cs  # Dispositivos com erro, backup >= 5 GB, Windows Update COM
│           ├── ActiveDirectoryModule.cs # gpupdate, gpresult, nltest, w32tm /stripchart, Exchange TLS 1.2
│           └── AuditModule.cs           # Visualização paginada de registro de ações do PC
└── tests/
    └── TecnicoBrasfort.Tests/           # Testes unitários C# 5.0 com fakes em memória
        ├── TecnicoBrasfort.Tests.csproj
        ├── TestRunner.cs                # Runner nativo BCL (Assert + Main)
        ├── CoreTests.cs                 # ArgumentEscaper, ConfigLoader, AuditLogger
        └── ModulesTests.cs              # Testes dos módulos sem side-effects no host
```

---

## 6. Arquitetura e fluxo de dados

```text
Program.cs
  ├─ ConfigLoader (carrega config.json em memória no startup ou aplica defaults genéricos)
  ├─ EnvironmentContext (gera EnvironmentInfo: origem UNC/USB, usuário interativo via SessionId + SID)
  ├─ AuditLogger (inicializa log primário em ProgramData + cópia secundária em Public\Desktop)
  └─ ConsoleMenu
       └─ Roteia opções [1]..[23] para Modules/* via IProcessRunner + IHostEnvironment
```

### Princípios de Responsabilidade:
1. **UI (`ConsoleMenu`)**: Exibe cabeçalho de contexto, menu `1..23` + `0` Sair e solicita confirmações `[S/N]`. Não executa comandos de sistema diretamente.
2. **Core (`ProcessRunner`, `HostEnvironmentAdapter`, `EnvironmentContext`, `AuditLogger`)**: Centraliza execução de processos com streaming, detecção de contexto (Admin vs. Usuário logado) e gravação segura de logs.
3. **Modules**: Cada categoria do menu reside em sua própria classe, dependendo apenas de contratos (`IProcessRunner`, `IHostEnvironment`) para permitir testes unitários isolados.

---

## 7. Modelo de dados e estado

| Entidade | Descrição | Onde é persistida / consumida |
| :--- | :--- | :--- |
| `EnvironmentInfo` | DTO gerado por `EnvironmentContext`: Hostname, OsVersion, ElevatedAdmin, InteractiveUser, InteractiveUserSid, OriginDirectory e IsUncOrigin. | Memória (em runtime) / Cabeçalho da UI, Modules e Logs |
| `ActionLogEntry` | Timestamp, Hostname, TechAdmin, InteractiveUser, ModuleName, ActionName, ExitCode e Summary. | Log primário (`ProgramData`) e cópia secundária (`Public\Desktop`) |
| `AppConfig` | DomainFqdn, DnsTestTargets, ExchangeCheckUrl e LogDirectoryOverride. | `config.json` (opcional) carregado em memória no startup |

---

## 8. Regras de negócio

1. **Elevação e Contexto de Usuário:** Toda limpeza de temporários de usuário (`C:\Users\*\AppData\Local\Temp`) ou listagem de impressoras de rede (`HKU\<UserSID>\Printers\Connections`) resolve explicitamente os perfis e o SID do usuário interativo (via `explorer.exe` na mesma `SessionId`), nunca usando `%TEMP%` ou `HKCU` do administrador elevado.
2. **Execução via Rede (UNC):** Como o manifesto `requireAdministrator` bloqueia letras mapeadas (`Z:\`) antes do startup, execuções em rede devem usar caminho UNC (`\\servidor\...`). O `ConfigLoader` carrega a configuração para memória no startup para não depender do SMB durante Reset de Rede.
3. **Confirmação para Ações Destrutivas/Agendamentos:** Ações que derrubam rede, param serviços críticos ou agendam verificação no boot (`netsh winsock reset`, `chkntfs /c C:`, limpeza de spooler) exigem confirmação explícita `[S/N]`.
4. **Verificação de Espaço em Backup de Drivers:** `pnputil /export-driver` valida >= 5 GB livres antes de iniciar, adotando `C:\Temp\DriverBackup_<HOSTNAME>` como padrão local.
5. **Escrita Segura de Logs:** O log primário em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` é sempre gravado; falhas na cópia secundária (`C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` ou `LogDirectoryOverride`) emitem apenas aviso no console sem interromper o atendimento.

---

## 9. Convenções de código e boas práticas

* **Restrição C# 5.0 Estrita:** Todo o código em `src/` e `tests/` deve compilar com `csc.exe` v4.0.30319 (`/warnaserror+`). Proibido uso de `$"..."`, `?.`, `nameof()`, inicializadores de auto-propriedades, `=>`, `out var` ou `using var`.
* **Nomenclatura C#:**
  * Classes, Interfaces (`I` prefixo), Métodos e Propriedades: **PascalCase**.
  * Parâmetros e variáveis locais: **camelCase**.
  * Campos privados: `_camelCase`.
* **Comandos Externos Seguros:** No `.NET Framework 4.8` não existe `ProcessStartInfo.ArgumentList`; portanto, toda execução usa `ProcessStartInfo.Arguments` montado via `ArgumentEscaper.JoinArguments(args)` (com rejeição de `\r`, `\n`, `\0`), nunca concatenando input não validado em `cmd.exe /c`.
* **Git e Commits:**
  * Padrão **Conventional Commits**: `feat(escopo): ...`, `fix(escopo): ...`, `test(escopo): ...`, `sec(escopo): ...`.
  * Push compulsório da branch ao finalizar a tarefa (quando houver remote configurado).

---

## 10. Estratégia de Testes

O projeto adota **testes unitários isolados com fakes em memória (`TestRunner.cs` em C# 5.0 / BCL pura)** para que nenhum teste execute comandos destrutivos (`sfc`, `dism`, `chkdsk`, `netsh`) ou altere configurações reais da máquina do desenvolvedor.

| Parâmetro | Detecção da Máquina Atual (Atualizado Dinamicamente) |
| :--- | :--- |
| **Sistema Operacional Detectado** | Windows 10/11 (PowerShell) |
| **Host .NET (`dotnet --info`)** | Runtimes .NET 6.0.9 e 8.0.27 instalados; **sem `.NET SDK`** |
| **Compilador Nativo Ativo** | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5.0 / .NET Framework 4.8) |

---

## 11. Estratégia de Segurança

1. **Zero Segredos no Repositório:** Nenhuma senha, token ou IP interno real da Brasfort deve ser commitado.
2. **Prevenção de Command Injection:** Toda chamada a processos externos (`sfc`, `dism`, `netsh`, `nltest`, `pnputil`, `chkntfs`, `w32tm`) usa `ArgumentEscaper` com argumentos explícitos e validados.
3. **Zero NuGet em Runtime:** O executável principal não carrega DLLs externas de terceiros.
4. **Assinatura Authenticode:** Recomenda-se assinar o `.exe` de release com certificado interno (AD CS / GPO) via `scripts/build.ps1 -CertThumbprint` para evitar falsos positivos em EDR/SmartScreen.

---

## 12. Cuidados e armadilhas comuns

1. **Arquivos soltos na raiz:** Nunca salvar drafts, specs ou relatórios EGM na raiz. Use sempre `00-anything/{sequencial} - {nome} {data}/`.
2. **Assumir perfil do usuário em sessão elevada:** Em UAC "Executar como outro usuário", `%TEMP%`, `%USERPROFILE%` e `HKCU` apontam para o técnico admin, não para o usuário logado.
3. **Unidades mapeadas (`Z:\`) após UAC:** O Windows bloqueia `Z:\` antes do `Main()` com `requireAdministrator`; usar sempre caminho UNC (`\\servidor\...`) ou pendrive.
4. **Deadlock no `chkdsk C: /f`:** Redirecionar `stdout` sem `chkntfs /c C:` trava no prompt `S/N` do sistema.
5. **Sintaxe C# 6+ no `csc.exe` v4.0.30319:** Quebra o build local imediatamente.
6. **Esquecer de atualizar o `AGENTS.md` e o `graphify`:** Toda mudança estrutural exige atualização simultânea deste arquivo e do grafo.

---

## 13. Checklist para agentes ao finalizar uma tarefa

* [ ] **Isolamento de artefatos na raiz:** Nenhum rascunho, spec ou relatório EGM solto na raiz (todos dentro de `00-anything/{sequencial} - {nome} {data}/`).
* [ ] **Zero hardcoding interno:** Nenhum IP, servidor real ou credencial da Brasfort commitado no código ou em `config.example.json`.
* [ ] **Sincronização com `graphify`:** O mapa de grafo em `graphify-out/` foi atualizado em paridade com as alterações estruturais.
* [ ] **Build e Testes Unitários:** `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -RunTests` executado com sucesso (ou `dotnet build`/`dotnet test` quando houver `.NET SDK`).
* [ ] **Formatação e Segurança:** Código em C# 5.0 estrito com `/warnaserror+` (e `dotnet format` / `dotnet list package --vulnerable` quando houver SDK).
* [ ] **Documentação atualizada:** Árvore de pastas, regras e **Histórico de alterações** deste `AGENTS.md` atualizados.
* [ ] **Git:** Branch dedicada (`feature/...`, `fix/...`) criada (respeitada a exceção do commit inaugural na `main`), commits semânticos realizados e enviados via `git push -u origin <branch>` quando houver remote.

---

## 📜 Histórico de alterações deste arquivo

| Data | Responsável | Alteração Realizada |
| :--- | :--- | :--- |
| 2026-11-19 | Agente IA | Criação inicial do `AGENTS.md` do projeto **Tecnico Brasfort**, adaptando todas as regras de governança do modelo base para uma aplicação CLI em C# / .NET Framework 4.8 voltada a suporte de TI em ambiente Windows/AD/GPO. |
| 2026-11-23 | Agente IA | Alinhamento com `tecnico_brasfort_design.md` (SIR-139): definição de C# 5.0 estrito (`csc.exe` v4.0.30319), remoção de `UncStager`/`IRegistryReader`, consolidação de `IHostEnvironment`, `ArgumentEscaper` no lugar de `ArgumentList`, `TestRunner.cs` nativo em BCL pura, atualização das Seções 2, 3, 5, 6, 7, 8, 9, 10, 12 e 13. |
