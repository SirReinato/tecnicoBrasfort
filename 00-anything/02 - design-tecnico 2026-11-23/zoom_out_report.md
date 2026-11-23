# Relatório de Revisão Arquitetural — Stage 5 (Initial Zoom Out Review)

*   **Documento Auditado:** [tecnico_brasfort_design.md](./tecnico_brasfort_design.md) (`Linhas 1..332`)
*   **Contexto Base:** [AGENTS.md](../../AGENTS.md) e [concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md)
*   **Revisor:** `egm-zoom-out` (Skeptical Architect)
*   **Data:** 2026-11-23

---

## Avaliação Geral de Altitude e Aderência

O documento foca nos problemas operacionais reais de campo (perda de contexto do usuário interativo em elevação UAC com conta administrativa separada, queda de sessão SMB ao resetar rede via UNC, falsos positivos de EDR em wrappers e trilha de auditoria resiliente). A escolha do `.NET Framework 4.8` puro (zero NuGet em runtime) evita dependências externas nas estações atendidas.

Sob a revisão de **Simplicidade, YAGNI e Altitude (`Zoom Out`)**, foram identificados 2 achados **Major** e 3 achados **Nit** onde o plano introduz dependências incompatíveis com a máquina de desenvolvimento atual, reimplementa mecanismos nativos do Windows/PE e antecipa detalhes de baixo nível na seção arquitetural (`Linhas 1..203`).

---

## Findings (Achados da Auditoria)

### 1. [Major] Suíte `xUnit` dependente de `.NET SDK` em um host que só possui `csc.exe`

*   **Severity:** Major
*   **Affected Scope:** `Open Questions` (Linha 43), `Decisão de Zero NuGet em Runtime` (Linha 97) e `Detailed Implementation Plan / Verification Plan` (Linhas 235–240, 306–324).
*   **Simplicity Rationale:**
    O próprio documento registra na Linha 319 que a máquina de desenvolvimento atual **não possui `.NET SDK` instalado**, dispondo apenas do compilador nativo `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Apesar disso, o design projeta um `.sln`, dois `.csproj` e uma suíte `xUnit` (`TecnicoBrasfort.Tests.csproj`) que **não pode ser restaurada nem executada na máquina atual** (`dotnet test`, `dotnet format` e `dotnet list package` falham sem SDK).
*   **Recomendação:** Permitir que os testes unitários também sejam compiláveis e executáveis diretamente pelo `csc.exe` dentro do `scripts/build.ps1` (ex.: um executável de testes leve `TecnicoBrasfort.Tests.exe` usando asserções puras da BCL sem NuGet), eliminando o bloqueio de verificação local na máquina atual.

### 2. [Major] `UncStager` copiando e relançando o `.exe` em `ProgramData` (YAGNI e risco de alerta em EDR)

*   **Severity:** Major
*   **Affected Scope:** `Goals` (Linha 28), `CUJ 2` (Linhas 58–60), `Technical Plan` (Linhas 80–82, 107–110), `Non-Functional Requirements` (Linha 187) e `Detailed Implementation Plan` (Linhas 273–275).
*   **Simplicity Rationale:**
    Para evitar travamento ao rodar de um caminho UNC (`\\servidor\pasta\TecnicoBrasfort.exe`) durante um Reset de Rede, o design propõe um módulo `UncStager` que calcula hash SHA-256, cria pastas em `C:\ProgramData\Brasfort\TecnicoBrasfort\bin\`, copia `.exe` e `config.json`, passa `--origin-path` via CLI e faz *re-spawn* de um novo processo elevado.
    1. Copiar um `.exe` de um compartilhamento de rede para `C:\ProgramData\...` e executá-lo imediatamente a partir de outro processo reproduz o comportamento de *dropper/self-staging* monitorado por EDRs e antivírus, aumentando o risco que o projeto busca evitar.
    2. Um binário C# `.NET Framework 4.8` puro (< 100 KB de IL) sem DLLs externas pode carregar `config.json` para a memória no startup e pré-carregar seus tipos antes do reset de rede (ou usar `/swaprun:net` no PE), já que os assemblies da BCL são carregados do GAC local (`C:\Windows\Microsoft.NET\...`) e o log primário já reside localmente em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\`.
*   **Recomendação:** Substituir o `UncStager` (auto-copy + re-spawn) pelo carregamento integral de `config.json` e pré-inicialização dos módulos em memória no startup.

### 3. [Nit] Vazamento de detalhes de codificação na seção arquitetural (`Linhas 1..203`)

*   **Severity:** Nit
*   **Affected Scope:** `Technical Plan (Architecture)` (`Linhas 76–143`).
*   **Simplicity Rationale:**
    A seção arquitetural (`Linhas 1..203`) deve focar em fronteiras de componentes, fluxo de dados e contratos de alto nível, reservando nomes de classes da BCL (`DataContractJsonSerializer`, `NTAccount.Translate(typeof(SecurityIdentifier))`), propriedades booleanas (`UseShellExecute = false`) e queries WQL (`MSStorageDriver_FailurePredictStatus`, `Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0`) para o `Detailed Implementation Plan` (`Linhas 204+`).
*   **Recomendação:** Enxugar as referências de classes concretas e queries WQL nas linhas `76–143`, mantendo-as apenas nas linhas `204+`.

### 4. [Nit] Excesso de interfaces wrapper (`IRegistryReader`, `IFileSystem`) para uso pontual

*   **Severity:** Nit
*   **Affected Scope:** `Technical Plan` (Linhas 89, 105–106) e `Detailed Implementation Plan` (Linhas 261–266).
*   **Simplicity Rationale:**
    Criar `IRegistryReader` + `RegistryReaderAdapter` apenas para ler uma única chave (`HKU\<UserSID>\Printers\Connections`) no `PrintingModule` fragmenta o projeto sem cobrir outras chamadas de SO (`ServiceController`, `TcpClient`, WMI, COM).
*   **Recomendação:** Consolidar as leituras/operações de ambiente do host em uma única interface coesa (`ISystemProbe` ou `IHostEnvironment`) ao lado de `IProcessRunner`, ou separar métodos puros de parsing/validação dos métodos de coleta do SO.

### 5. [Nit] Assimetria entre operações mockadas e chamadas diretas a WMI/COM/`ServiceController`

*   **Severity:** Nit
*   **Affected Scope:** `Especificação dos Módulos` (Linhas 122, 126, 128, 131, 134, 136) vs. `Testes Unitários` (Linhas 312–314).
*   **Simplicity Rationale:**
    Enquanto arquivos e registro possuem interfaces para mock, WMI, `ServiceController`, `TcpClient` e COM são invocados diretamente nos módulos, o que impediria testar esses fluxos em `ModulesTests.cs` sem tocar no sistema operacional.
*   **Recomendação:** Centralizar interações de SO no contrato único de ambiente/processo ou isolar a lógica de decisão/formatação em métodos puros testáveis.
