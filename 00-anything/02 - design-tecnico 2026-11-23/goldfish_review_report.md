# Goldfish Annotated Review Report

**Status:** resolved
**Target:** `00-anything/02 - design-tecnico 2026-11-23/tecnico_brasfort_design.md` (`full file: Lines 1-332`)

## Prioritized Findings & AI Suggestions

### [Issue 1] [`egm-design` / `egm-zoom-out`] - Severity: Blocker
*   **Affected Scope:** [Lines 19-20, 28, 58-60, 73-82, 107-110, 146, 187, 212, 224, 251, 269, 272, 273-275, 311](./tecnico_brasfort_design.md#L19-L20) (`app.manifest`, `EnvironmentContext`, `UncStager`)
*   **Discrepancy Description:** Com `<requestedExecutionLevel level="requireAdministrator" />` no `app.manifest`, unidades mapeadas (`Z:\`) da sessão do usuário não existem no token elevado antes de `Program.cs` iniciar, causando falha pré-execução. Além disso, o `UncStager` (auto-copy para `C:\ProgramData\...\bin\` + re-spawn) imita comportamento de *dropper* para EDRs e pode fechar a janela do console se não gerenciar o mesmo console.
*   **Rationale:** Como o binário `net48` puro (< 1 MB) utiliza apenas assemblies da BCL presentes no GAC local, carregar `config.json` para a memória no startup e pré-inicializar os tipos necessários elimina a dependência de acesso SMB contínuo durante o Reset de Rede sem precisar copiar e relançar o `.exe`.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Remove uma classe inteira (`UncStager.cs`), o parâmetro `--origin-path`, a flag não utilizada `IsWritableOrigin` e o risco de alerta em EDR/antivírus, mantendo a restrição documentada de abrir via caminho UNC (`\\servidor\...`) ou pendrive (nunca por letra de unidade mapeada `Z:\`).
*   ***Proposed Edit:***
    *   Location: `Goals` (Linha 28), `CUJ 2` (Linhas 58–60), `Technical Plan` (Linhas 73–82, 107–110), `Non-Functional Requirements` (Linha 187) e `Detailed Implementation Plan` (Itens 1, 5, 13, 19, 20, 21 e 32).
    *   Change: Remover `UncStager.cs`, `--origin-path` e `IsWritableOrigin`; especificar carregamento integral de `config.json` e inicialização em memória no startup, e documentar que execuções em rede devem usar caminho UNC direto (`\\servidor\...`).

---

### [Issue 2] [`egm-design` / `egm-zoom-out`] - Severity: Blocker
*   **Affected Scope:** [Lines 101-104, 121, 284](./tecnico_brasfort_design.md#L121) (`ProcessRunner` + `MaintenanceModule`)
*   **Discrepancy Description:** Executar `chkdsk.exe C: /f` com `RedirectStandardOutput = true` sem escrita em `StandardInput` causa deadlock indefinido aguardando a resposta `S`/`Y` no prompt de volume em uso.
*   **Rationale:** O comando nativo `chkntfs.exe /c C:` agenda a verificação de integridade do volume `C:` no próximo boot sem bloquear em prompt interativo dependente do idioma do Windows.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Elimina o risco de travamento em campo e evita automação frágil de `StandardInput` no `ProcessRunner`.
*   ***Proposed Edit:***
    *   Location: `MaintenanceModule` (Linha 121 e Linha 284).
    *   Change: Substituir `chkdsk C: /f` por `chkntfs.exe /c C:` mediante confirmação `[S/N]` do técnico no menu com aviso de agendamento para o próximo boot.

---

### [Issue 3] [`egm-implementation-readiness` / `egm-codebase-details`] - Severity: Blocker
*   **Affected Scope:** [Lines 237-245, 318-320](./tecnico_brasfort_design.md#L237-L245) (`scripts/build.ps1`, `TecnicoBrasfort.csproj`, `Verification Plan`)
*   **Discrepancy Description:** O compilador `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (único disponível no host atual sem `.NET SDK`) suporta exclusivamente **C# 5.0** e exige `/r:` explícito para `System.Management.dll`, `System.Runtime.Serialization.dll`, `System.Xml.dll` (necessário para `DataContractJsonSerializer`), `System.ServiceProcess.dll` e `System.Net.Http.dll`.
*   **Rationale:** Qualquer sintaxe C# 6+ (`$"..."`, `?.`, `nameof()`, expression-bodied `=>`) ou omissão de `System.Xml.dll` quebrará a compilação na máquina atual.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Garante compilação imediata e sem erros no ambiente real de desenvolvimento.
*   ***Proposed Edit:***
    *   Location: Itens 10 e 11 (`scripts/build.ps1` e `TecnicoBrasfort.csproj`) e `Verification Plan`.
    *   Change: Definir restrição estrita de **C# 5.0** (`<LangVersion>5</LangVersion>`), incluir `System.Xml.dll` e especificar o comando completo do `csc.exe` com `/warnaserror+` e `/win32manifest:src\TecnicoBrasfort\app.manifest`.

---

### [Issue 4] [`egm-implementation-readiness` / `egm-codebase-details` / `egm-zoom-out`] - Severity: Blocker
*   **Affected Scope:** [Lines 43, 97, 235-240, 306-314, 321-322](./tecnico_brasfort_design.md#L306-L322) (`tests/TecnicoBrasfort.Tests/*`, `Verification Plan`)
*   **Discrepancy Description:** O plano define testes unitários exclusivamente via `xUnit` (`dotnet test`), mas o host atual não possui `.NET SDK` para restaurar pacotes NuGet ou executar `dotnet test`, bloqueando o checklist obrigatório da Seção 13 do `AGENTS.md`.
*   **Rationale:** Um runner de testes leve em C# 5.0 / BCL pura (`TestRunner.cs` com asserções nativas) compilável pelo `csc.exe` via `scripts/build.ps1 -RunTests` permite executar 100% dos testes unitários localmente sem SDK externo.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Desbloqueia a verificação automatizada local na máquina atual sem impedir `dotnet test` onde o SDK estiver instalado.
*   ***Proposed Edit:***
    *   Location: Item 10 (`scripts/build.ps1`), Itens de `tests/TecnicoBrasfort.Tests/` e `Verification Plan`.
    *   Change: Adicionar `tests/TecnicoBrasfort.Tests/TestRunner.cs` em C# 5.0 (zero NuGet) e flag `-RunTests` no `scripts/build.ps1` para compilar e executar `dist/TecnicoBrasfort.Tests.exe` validando código de saída `0`.

---

### [Issue 5] [`egm-design`] - Severity: Major
*   **Affected Scope:** [Lines 78, 108-109, 271-272](./tecnico_brasfort_design.md#L108-L109) (`EnvironmentContext`)
*   **Discrepancy Description:** `Win32_ComputerSystem.UserName` consulta apenas a sessão de console física e retorna `null` ou usuário incorreto em sessões RDP / Fast User Switching; além disso, `NTAccount.Translate` pode falhar sem acesso ao DC.
*   **Rationale:** Buscar o proprietário (`GetOwnerSid()`) do processo `explorer.exe` na mesma `SessionId` do processo atual retorna diretamente o SID do usuário interativo em sessões físicas e RDP.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Corrige a detecção de usuário em atendimentos remotos via RDP sem depender de consulta online ao DC.
*   ***Proposed Edit:***
    *   Location: `EnvironmentContext` (Linhas 108–109 e Item correspondente no `Detailed Implementation Plan`).
    *   Change: Especificar resolução via `Win32_Process` (`explorer.exe` filtrado por `SessionId` atual com `GetOwnerSid()`), fallback para `Win32_ComputerSystem.UserName` e tratamento para sessão sem usuário interativo.

---

### [Issue 6] [`egm-design`] - Severity: Major
*   **Affected Scope:** [Lines 103-104, 124, 258-260, 286-287](./tecnico_brasfort_design.md#L124) (`ProcessRunner`, `NetworkModule`)
*   **Discrepancy Description:** No Reset de Rede, `netsh winsock reset` e `netsh int ip reset` rodam após `ipconfig /renew` (sujando o lease recém-obtido), e o `ProcessRunner` não define política de timeouts por categoria (60 s para rede vs. 45 min para `sfc`/`dism`).
*   **Rationale:** Inverter a ordem garante que a interface termine com lease DHCP ativo, e timeouts diferenciados evitam abortar comandos longos no meio.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Previne falhas operacionais diretas em `sfc`, `dism` e Reset de Rede.
*   ***Proposed Edit:***
    *   Location: `ProcessRunner` e `NetworkModule`.
    *   Change: Definir timeout padrão de 60 s para rede/AD e 2700 s (45 min) para manutenção/drivers; ordenar Reset de Rede como `netsh winsock reset` -> `netsh int ip reset` -> `ipconfig /release` -> `ipconfig /flushdns` -> `ipconfig /renew`.

---

### [Issue 7] [`egm-design`] - Severity: Major
*   **Affected Scope:** [Lines 139-140, 298-299](./tecnico_brasfort_design.md#L139-L140) (`ActiveDirectoryModule`)
*   **Discrepancy Description:** `w32tm /query /status` falha se o serviço `W32Time` estiver parado e depende do idioma do SO; `klist` em sessão elevada lista apenas tíquetes do técnico admin; testes HTTPS no `net48` exigem `SecurityProtocolType.Tls12` explícito.
*   **Rationale:** `w32tm /stripchart /computer:<DomainFqdn> /samples:1 /dataonly` mede o desvio real contra o DC independente de idioma/serviço local.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Torna o diagnóstico de Kerberos e Exchange confiável em campo.
*   ***Proposed Edit:***
    *   Location: `ActiveDirectoryModule` (Linhas 139–140 e Item correspondente no `Detailed Implementation Plan`).
    *   Change: Adotar `w32tm /stripchart /computer:<DomainFqdn> /samples:1 /dataonly`, habilitar `SecurityProtocolType.Tls12` distinguindo erro TCP/SSL/HTTP no `ExchangeCheckUrl` e explicitar o escopo de `klist`.

---

### [Issue 8] [`egm-design` / `egm-zoom-out`] - Severity: Major
*   **Affected Scope:** [Lines 136, 296](./tecnico_brasfort_design.md#L136) (`DriversUpdatesModule`)
*   **Discrepancy Description:** `UpdateSearcher.Search("IsInstalled=0")` via COM é síncrono e pode travar o console por minutos sem feedback quando offline ou com WSUS inacessível.
*   **Rationale:** Envolver a chamada em `Task.Run` com espera limitada, contador de tempo decorrido simples no console e tradução de `COMException` (`HRESULT`) evita congelamento aparente sem adicionar frameworks complexos.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Mantém simplicidade arquitetural e feedback visual ao técnico.
*   ***Proposed Edit:***
    *   Location: `DriversUpdatesModule` (Linha 136 e Item correspondente).
    *   Change: Especificar execução da busca COM em `Task.Run` com timeout controlado, indicador de tempo no console e tratamento de `COMException`.

---

### [Issue 9] [`egm-design` / `egm-zoom-out`] - Severity: Major
*   **Affected Scope:** [Lines 101-104, 120, 258-260](./tecnico_brasfort_design.md#L101-L104) (`ProcessRunner`, `MaintenanceModule`)
*   **Discrepancy Description:** Utilitários do Windows (`ipconfig`, `netsh`, `chkdsk`) emitem saída em OEM CodePage (`CP850`), enquanto `sfc.exe` emite em `UTF-16LE` via pipe, gerando caracteres nulos `\0` se lido com encoding fixo.
*   **Rationale:** Usar OEM CodePage por padrão e higienizar bytes nulos `\0` (ou encoding explícito para `sfc.exe`) resolve sem detectores heurísticos.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Evita texto ilegível na tela do técnico durante o `sfc /scannow`.
*   ***Proposed Edit:***
    *   Location: `ProcessRunner` (Linhas 101–104 e Item correspondente).
    *   Change: Configurar `StandardOutputEncoding`/`StandardErrorEncoding` para `CultureInfo.CurrentCulture.TextInfo.OEMCodePage` e remover `\0` / suportar `Encoding.Unicode` para `sfc.exe`.

---

### [Issue 10] [`egm-style`] - Severity: Major
*   **Affected Scope:** [Lines 30, 54, 60, 113](./tecnico_brasfort_design.md#L30) (`Goals`, `CUJ 1`, `CUJ 2`, `AuditLogger`)
*   **Discrepancy Description:** Contradição entre "fallback silencioso" (Linha 30), "cópia secundária em rede" (Linha 60) e `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` com aviso no console (Linhas 44 e 113).
*   **Rationale:** Alinhar o texto elimina ambiguidade sobre o comportamento observável do log secundário.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Uniformiza especificação de comportamento entre `Goals`, `CUJs` e `Core/`.
*   ***Proposed Edit:***
    *   Location: Linhas 30, 54 e 60.
    *   Change: Unificar para destino secundário padrão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`) com falha não bloqueante e aviso no console.

---

### [Issue 11] [`egm-implementation-readiness` / `egm-codebase-details` / `egm-style` / `egm-zoom-out`] - Severity: Major
*   **Affected Scope:** [Lines 89, 105-106, 252-279](./tecnico_brasfort_design.md#L252-L279) (Itens 14–22)
*   **Discrepancy Description:** Os itens 16–18 agrupam dois arquivos por item omitindo o caminho do segundo arquivo; faltam assinaturas de métodos em `Core/` e `Models.cs` (`EnvironmentInfo` vs. `EnvironmentContext`), além de `IRegistryReader` existir apenas para uma única chave.
*   **Rationale:** Separar 1 arquivo por item numerado, consolidar `IRegistryReader` dentro de `IFileSystem` (ou `IHostEnvironment`) e explicitar assinaturas exatas elimina decisões ad-hoc na implementação.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Garante relação 1:1 no plano de implementação e contratos exatos entre `Core/`, `Modules/` e `Tests/`.
*   ***Proposed Edit:***
    *   Location: Seção `Target Files to Create/Modify` (`src/TecnicoBrasfort/Core/*`).
    *   Change: Listar 1 arquivo por item com caminho relativo completo, consolidar a leitura de `HKU\<UserSID>\Printers\Connections` em `IHostEnvironment` (substituindo `IFileSystem` + `IRegistryReader` por um contrato coeso) e documentar assinaturas exatas de `Models.cs` e `IProcessRunner`.

---

### [Issue 12] [`egm-implementation-readiness`] - Severity: Major
*   **Affected Scope:** [Lines 279-302](./tecnico_brasfort_design.md#L279-L302) (`ConsoleMenu.cs` e `Modules/*`)
*   **Discrepancy Description:** Faltam o mapeamento exato das opções numeradas do menu (`1..23` + `0` Sair) e as assinaturas de construtor dos 7 módulos.
*   **Rationale:** Sem a tabela de opções, o implementador precisaria adivinhar a numeração do menu.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Torna o plano acionável diretamente para codificação.
*   ***Proposed Edit:***
    *   Location: Item de `ConsoleMenu.cs` e itens dos 7 módulos.
    *   Change: Incluir a tabela das opções `[1]` a `[23]` (adaptadas para AD/GPO/Exchange no lugar de Entra/Intune/Teams) + `[0] Sair` e os parâmetros de construtor dos módulos.

---

### [Issue 13] [`egm-codebase-details`] - Severity: Major
*   **Affected Scope:** [Lines 210-212](./tecnico_brasfort_design.md#L210-L212) (Item 1: `../../AGENTS.md`)
*   **Discrepancy Description:** O Item 1 prevê atualizar apenas Seções 5, 9 e 10 do `AGENTS.md`, deixando Seções 3, 7 e 13 desatualizadas em relação ao `scripts/build.ps1 -RunTests`, `EnvironmentInfo` e subpastas `00-anything/02 - ...` e `dist/`.
*   **Rationale:** Pela Regra 1 do `AGENTS.md`, todas as seções afetadas devem permanecer sincronizadas.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Mantém o `AGENTS.md` consistente com a arquitetura real.
*   ***Proposed Edit:***
    *   Location: Item 1 (Linhas 210–212).
    *   Change: Incluir atualização das Seções 2, 3, 5, 6, 7, 9, 10 e 13 do `AGENTS.md`.

---

### [Issue 14] [`egm-style` / `egm-implementation-readiness`] - Severity: Major
*   **Affected Scope:** [Lines 215, 218, 221](./tecnico_brasfort_design.md#L215-L221) (Itens 2, 3, 4)
*   **Discrepancy Description:** Referência a `"Regra 5.1"` inexistente no `AGENTS.md` (os ponteiros estão na Seção 5).
*   **Rationale:** Evita busca por seção inexistente durante a implementação.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Correção factual simples.
*   ***Proposed Edit:***
    *   Location: Linhas 215, 218 e 221.
    *   Change: Substituir `"conforme Regra 5.1"` por `"conforme Seção 5 do AGENTS.md"`.

---

### [Issue 15] [`egm-design`] - Severity: Nit
*   **Affected Scope:** [Lines 128-129, 289-290](./tecnico_brasfort_design.md#L128-L129) (`PrintingModule`)
*   **Discrepancy Description:** Subchaves em `HKU\<UserSID>\Printers\Connections` usam formato `,,servidor,impressora` e `ServiceController.Stop()` no `Spooler` pode ficar em `StopPending`.
*   **Rationale:** Explicitar a formatação `,,` -> `\\` e o kill de `spoolsv.exe` em `StopPending`.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Previne erro de runtime e exibição confusa de nome de impressora.
*   ***Proposed Edit:***
    *   Location: `PrintingModule`.
    *   Change: Documentar conversão de `,,servidor,impressora` para `\\servidor\impressora` e tratamento de `StopPending` antes de encerrar `spoolsv.exe`.

---

### [Issue 16] [`egm-design` / `egm-style`] - Severity: Nit
*   **Affected Scope:** [Lines 113, 278](./tecnico_brasfort_design.md#L278) (`AuditLogger`)
*   **Discrepancy Description:** Falta sanitização de `<TechAdmin>` (removendo `DOMINIO\` e caracteres inválidos) e padronização de `<HOSTNAME>_<YYYY-MM-DD>.log` na Linha 278.
*   **Rationale:** Evita `DirectoryNotFoundException` caso `DOMINIO\usuario` entre no nome do arquivo.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Corrige bug potencial de criação de arquivo.
*   ***Proposed Edit:***
    *   Location: Linha 113 e Item de `AuditLogger.cs`.
    *   Change: Padronizar máscaras `<HOSTNAME>_<YYYY-MM-DD>.log` e `<HOSTNAME>_<YYYY-MM-DD>_<HHmmss>_<TechAdmin>.log` com extração de `sAMAccountName` e sanitização via `Path.GetInvalidFileNameChars()`.

---

### [Issue 17] [`egm-style`] - Severity: Nit
*   **Affected Scope:** [Lines 126, 132, 287, 293](./tecnico_brasfort_design.md#L126) (`NetworkModule`, `DiagnosticsModule`)
*   **Discrepancy Description:** Termos vagos `"timeout curto"` (Linha 126) e `"principais processos"` (Linha 132).
*   **Rationale:** Quantificar parâmetros evita escolha arbitrária na implementação.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Define critérios objetivos e testáveis.
*   ***Proposed Edit:***
    *   Location: Linhas 126 e 132 (e respectivos itens no plano).
    *   Change: Especificar `timeout de 3000 ms` no `TcpClient` e `top 15 processos ordenados por WorkingSet64`.

---

### [Issue 18] [`egm-zoom-out` / `egm-style`] - Severity: Nit
*   **Affected Scope:** [Lines 40-47, 76-143, 102, 179](./tecnico_brasfort_design.md#L40-L47) (`FeatureRange`)
*   **Discrepancy Description:** Redundância entre `Open Questions` e `Technical Plan`, nota operacional sobre `AGENTS.md` dentro do contrato de `IProcessRunner` (Linha 102), `"único .exe puro sem dependências"` (Linha 179) e vazamento de detalhes de implementação (nomes de classes concretas e queries WQL) nas Linhas 76–143.
*   **Rationale:** Enxugar `FeatureRange` melhora legibilidade e mantém detalhes de código no `Detailed Implementation Plan`.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Melhora clareza e separação entre arquitetura e implementação.
*   ***Proposed Edit:***
    *   Location: Linhas 40–47, 76–143, 102 e 179.
    *   Change: Resumir `Open Questions` em 1 linha, remover nota do `AGENTS.md` da Linha 102, simplificar Linha 179 e manter detalhes concretos de WQL/classes apenas nas linhas `204+`.

---

### [Issue 19] [`egm-comprehension`] - Severity: Nit
*   **Affected Scope:** [Lines 13, 332](./tecnico_brasfort_design.md#L13) (`Overview`, Rodapé)
*   **Discrepancy Description:** Termo `graphify` e sigla `EGM` aparecem sem explicação breve inline.
*   **Rationale:** Melhora compreensão para novos membros do time sem exigir abrir o `AGENTS.md`.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Adição mínima que torna o texto autossuficiente.
*   ***Proposed Edit:***
    *   Location: Linha 13 e Linha 332.
    *   Change: Explicar `graphify` brevemente na Linha 13 (`mapeamento do grafo de dependências do código`) e expandir `EGM (Elephant-Goldfish Model)` na Linha 332.

---

### [Issue 20] [`egm-style` / `egm-codebase-details`] - Severity: Nit
*   **Affected Scope:** [Lines 316-329](./tecnico_brasfort_design.md#L316-L329) (`Verification Plan`)
*   **Discrepancy Description:** Ressalva sobre ausência do `.NET SDK` repetida 3 vezes em 6 linhas e falta de verificação explícita do checklist da Seção 13 / Git / `00-anything/`.
*   **Rationale:** Consolidar a nota de ambiente e incluir o checklist de governança torna o plano de verificação completo e direto.
*   ***AI Suggestion:*** `PROPOSE FIX`
*   ***Justification:*** Alinha o `Verification Plan` com as regras obrigatórias do `AGENTS.md`.
*   ***Proposed Edit:***
    *   Location: `Verification Plan` (Linhas 316–329).
    *   Change: Declarar a condição de ambiente sem `.NET SDK` uma única vez no topo e adicionar item de verificação do checklist da Seção 13 do `AGENTS.md` (branch Git, `00-anything/` e `graphify-out/`).

---

### [Issue 21] [`egm-structural`] - Severity: Nit
*   **Affected Scope:** [Lines 52-66, 189-196](./tecnico_brasfort_design.md#L52-L66) (`CUJs`, `Additional Resources`)
*   **Discrepancy Description:** Rótulos `**Given**:` têm dois-pontos fora do negrito ao contrário de `**Pros:**`; divisor na Linha 189 precede `H3`.
*   **Rationale:** O divisor da Linha 189 segue o template literal `design_doc.md` (não precisa mexer), enquanto padronizar `**Given:**`/`**When:**`/`**Then:**` melhora simetria.
*   ***AI Suggestion:*** `PROPOSE FIX` *(apenas para `**Given:**`/`**When:**`/`**Then:**`; `DECLINE` para o divisor da Linha 189)*
*   ***Justification:*** Simetria visual nos CUJs preservando conformidade com o template base.
*   ***Proposed Edit:***
    *   Location: Linhas 52–66.
    *   Change: Padronizar `**Given:**`, `**When:**`, `**Then:**`.
