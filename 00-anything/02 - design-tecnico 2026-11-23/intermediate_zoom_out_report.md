# Relatório de Revisão Arquitetural — Stage 8 (Intermediate Zoom Out Review)

*   **Documento Auditado:** [tecnico_brasfort_design.md](./tecnico_brasfort_design.md) (`Linhas 1..332`)
*   **Contexto e Relatórios Base:**
    *   [AGENTS.md](../../AGENTS.md)
    *   [concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md)
    *   [zoom_out_report.md](./zoom_out_report.md) (Stage 5)
    *   [comprehension_report.md](./comprehension_report.md) (Stage 6)
    *   [critic_report.md](./critic_report.md) (Stage 7)
*   **Revisor:** `egm-zoom-out` (Skeptical Architect — Intermediate Zoom Out)
*   **Data:** 2026-11-23

---

## 1. Avaliação das Mitigações dos Stages 6 e 7 vs. Baseline de Simplicidade (Stage 5)

A revisão intermediária de altitude (`Zoom Out`) avalia se as correções propostas nos Stages 6 (Comprehension) e 7 (Critic) fortalecem a arquitetura sem introduzir *bloat*, abstrações especulativas (YAGNI) ou queda de altitude no `FeatureRange` (`Linhas 1..203`).

### 1.1. Mitigações que Simplificam e Endurecem o Design (Aprovadas integralmente)

1.  **Eliminação do `UncStager` (auto-copy + re-spawn) em favor de carregamento integral em memória no startup (Stage 5 #2 + Stage 7 Blocker #1):**
    *   **Veredito Zoom-Out:** Remover a cópia de `.exe` para `C:\ProgramData\Brasfort\TecnicoBrasfort\bin\`, o cálculo de hash SHA-256, o argumento `--origin-path` e o *re-spawn* de processo elimina um arquivo/classe inteiro (`UncStager.cs`), evita o fechamento abrupto da janela de console e remove o comportamento típico de *dropper* que acionaria heurísticas de EDR/antivírus.
    *   Como o binário `.NET Framework 4.8` (< 100 KB) usa apenas assemblies da BCL presentes no GAC local (`C:\Windows\Microsoft.NET\...`), basta ler `config.json` para a memória no startup e pré-inicializar os tipos necessários para sobreviver à queda de SMB durante o Reset de Rede, além de documentar que a execução via rede deve ocorrer por caminho UNC (`\\servidor\pasta\...`) ou pendrive, e não por unidade mapeada (`Z:\`).
2.  **Uso de `chkntfs.exe /c C:` no lugar de `chkdsk.exe C: /f` via pipe (Stage 7 Blocker #2):**
    *   **Veredito Zoom-Out:** Evita construir lógica de automação de `StandardInput` dependente de idioma (`S` em PT-BR vs. `Y` em EN-US) dentro do `ProcessRunner`. O comando nativo `chkntfs.exe /c C:` agenda a verificação de boot de forma determinística e não interativa após a confirmação `[S/N]` do técnico na UI.
3.  **Resolução do usuário interativo via `explorer.exe` na mesma `SessionId` (Stage 7 Major #3):**
    *   **Veredito Zoom-Out:** Consultar o proprietário (`GetOwnerSid`) do processo `explorer.exe` filtrado pelo `SessionId` atual (com fallback para `Win32_ComputerSystem.UserName`) resolve sessões RDP e Fast User Switching na mesma classe `EnvironmentContext`, já entregando o SID diretamente sem depender de `NTAccount.Translate` contra o Domain Controller.
4.  **Ordem do Reset de Rede e comando `w32tm /stripchart` (Stage 7 Majors #4 e #5):**
    *   **Veredito Zoom-Out:** Inverter a ordem dos comandos (`netsh` antes de `ipconfig /release` e `/renew`) e substituir `w32tm /query /status` por `w32tm /stripchart /computer:<DomainFqdn> /samples:1 /dataonly` troca comandos frágeis/dependentes de idioma por chamadas nativas mais confiáveis, sem adicionar classes novas.
5.  **Remoção da flag morta `IsWritableOrigin` e alinhamento do log secundário (Stage 6 #2 + Stage 7 Major #8):**
    *   **Veredito Zoom-Out:** Como o destino secundário padrão foi fixado em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`), a flag `IsWritableOrigin` em `EnvironmentContext` tornou-se código morto (YAGNI) e deve ser removida, unificando o texto sobre falha não bloqueante com aviso no console.

---

## 2. Findings (Achados de Contenção de Complexidade e Altitude para Consolidação)

### 1. [Major] Persistência do bloqueio de verificação local: testes `xUnit` dependentes de `.NET SDK` ausente na máquina atual

*   **Severity:** Major
*   **Affected Scope:** `Open Questions` (Linha 43), `Decisão de Zero NuGet em Runtime` (Linha 97), `Detailed Implementation Plan` (Linhas 235–240, 306–314) e `Verification Plan` (Linhas 319–325).
*   **Simplicity Rationale:**
    O achado **Major #1 do Stage 5** permanece aberto no documento: o host atual **não possui `.NET SDK` instalado** (apenas runtimes e o compilador `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`), mas o plano continua projetando `TecnicoBrasfort.Tests.csproj` exclusivamente sobre pacotes NuGet (`xUnit`) que exigem `dotnet test`.
*   **Recomendação:** Manter a compatibilidade do projeto de testes com `csc.exe` dentro de `scripts/build.ps1` (compilando e executando um runner leve de testes em C# puro/BCL com asserções nativas via `scripts/build.ps1 -RunTests`), garantindo que 100% dos testes unitários rodem localmente sem SDK externo.

### 2. [Major] Contenção de complexidade nas mitigações de `ProcessRunner` (Encoding `sfc.exe`) e `DriversUpdatesModule` (COM Windows Update)

*   **Severity:** Major
*   **Affected Scope:** `Contratos da Camada Core/` (Linhas 101–104), `MaintenanceModule` (Linha 120) e `DriversUpdatesModule` (Linha 136).
*   **Simplicity Rationale:**
    Os achados **Major #6** e **Major #7** do Stage 7 corrigem problemas reais do Windows, mas devem ser implementados de forma mínima:
    1.  **No `ProcessRunner`:** Usar `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)` por padrão e remover bytes nulos (`\0`) ao processar linhas (ou aceitar encoding explícito ao invocar `sfc.exe`), resolvendo a saída `UTF-16LE` do `sfc.exe` sem criar detectores heurísticos de stream.
    2.  **No COM `Microsoft.Update.Session`:** Envolver `UpdateSearcher.Search("IsInstalled=0")` em `Task.Run` com espera limitada (`Task.Wait(timeout)`), exibindo tempo decorrido simples no console e capturando `COMException` com tradução amigável.

### 3. [Nit] Remoção completa dos artefatos residuais do `UncStager` no `Detailed Implementation Plan` (`Linhas 204..332`)

*   **Severity:** Nit
*   **Affected Scope:** `Goals` (Linha 28), `CUJ 2` (Linhas 58–60), `Technical Plan` (Linhas 80–82, 107–110), `Non-Functional Requirements` (Linha 187) e `Detailed Implementation Plan` (Itens 1, 5, 13, 19, 20, 21 e 32).
*   **Simplicity Rationale:**
    Ao substituir o `UncStager` pelo carregamento em memória no startup, remover `src/TecnicoBrasfort/Core/UncStager.cs` (Item 21), o argumento `--origin-path` e a flag `IsWritableOrigin` da lista de arquivos do `Detailed Implementation Plan`.

### 4. [Nit] Consolidação das abstrações de SO (`IFileSystem` + `IRegistryReader`) e disciplina de altitude no `FeatureRange` (`Linhas 1..203`)

*   **Severity:** Nit
*   **Affected Scope:** `Technical Plan (Architecture)` (Linhas 76–143) e `Detailed Implementation Plan` (Itens 17 e 18, Linhas 261–266).
*   **Simplicity Rationale:**
    Consolidar `IFileSystem` e `IRegistryReader` (ou incluir a leitura de subchaves de impressora e sondagens básicas de ambiente em um único contrato `ISystemEnvironment`/`IFileSystem`) para reduzir arquivos boilerplate, e posicionar detalhes de código do Stage 7 (`SecurityProtocolType.Tls12`, `Path.GetInvalidFileNameChars()`, `3000 ms`, `top 15 processos por WorkingSet64`) no `Detailed Implementation Plan` (`Linhas 204+`).
