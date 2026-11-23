# Relatório Consolidado — Stage 7 (Critic Review)

*   **Documento Auditado:** [tecnico_brasfort_design.md](./tecnico_brasfort_design.md) (`FeatureRange: Linhas 1..203`)
*   **Contexto Base:** [AGENTS.md](../../AGENTS.md) e [concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md)
*   **Revisores:** `egm-design` (Hostile Design Critic), `egm-structural` (Structural Layout Validator), `egm-style` (Technical Copy Editor)
*   **Data:** 2026-11-23

---

## Resumo Executivo

Foram consolidados **15 achados** na revisão crítica do `FeatureRange` (`Linhas 1..203`):
*   **🔴 Blockers:** 2
*   **🟠 Majors:** 6
*   **🟡 Nits:** 7

---

## 🔴 Blockers (Impeditivos Arquiteturais / Falhas Críticas em Runtime)

### 1. [Blocker | `egm-design`] Barreira pré-execução do `app.manifest` (`requireAdministrator`) em unidades mapeadas (`Z:\`) e ciclo de vida de console do `UncStager`
*   **Escopo Afetado:** Linhas 19–20, 58–60, 73–82, 107–110, 146 (`EnvironmentContext`, `UncStager`, `app.manifest`).
*   **Descrição e Racional Técnico:**
    1. Com `<requestedExecutionLevel level="requireAdministrator" />` no `app.manifest`, se o técnico tentar abrir `Z:\TI\TecnicoBrasfort.exe` a partir de uma letra de unidade mapeada na sessão do usuário, o serviço AppInfo do Windows tenta resolver `Z:\...` dentro do novo token elevado **antes** de `Program.cs` iniciar e aborta com erro do sistema (*"O Windows não pode acessar o dispositivo, caminho ou arquivo especificado"*).
    2. Ao rodar via caminho UNC direto (`\\servidor\pasta\TecnicoBrasfort.exe`), se o `UncStager` fizer re-spawn de um segundo processo sem aguardar (`WaitForExit`) ou se auto-copiar para `ProgramData\...\bin` (já criticado no Stage 5 Zoom Out por imitar comportamento de *dropper* para EDR), haverá fechamento abrupto da janela de console ou alerta de segurança.
*   **Correção Proposta:**
    *   Documentar explicitamente que execuções via rede devem ser abertas por caminho UNC (`\\servidor\pasta\...`) ou pendrive (e não via letra de unidade mapeada da sessão do usuário).
    *   Substituir o auto-copy + re-spawn do `UncStager` pelo carregamento integral de `config.json` em memória no startup + flag PE `/swaprun:net` (ou pré-inicialização dos tipos em memória), eliminando o re-spawn de processo e o risco de alerta em EDR.

### 2. [Blocker | `egm-design`] Deadlock em `chkdsk C: /f` por prompt interativo `S/N` no `stdin`
*   **Escopo Afetado:** Linhas 101–104, 121 (`ProcessRunner` + `MaintenanceModule`).
*   **Descrição e Racional Técnico:**
    *   Como `C:` é o volume do sistema operacional em uso, executar `chkdsk.exe C: /f` com `RedirectStandardOutput = true` faz o `chkdsk.exe` pausar indefinidamente aguardando `S` (PT-BR) ou `Y` (EN-US) no `StandardInput` para agendar a verificação no próximo boot. Como o `ProcessRunner` não escreve em `stdin`, o comando ficará congelado até estourar timeout.
*   **Correção Proposta:**
    *   Para agendar a verificação do volume `C:` no próximo boot após a confirmação `[S/N]` do técnico no menu, utilizar o utilitário nativo **`chkntfs.exe /c C:`** (ou escrita em `StandardInput`), que agenda a verificação de boot nativamente sem prompt interativo dependente do idioma do Windows.

---

## 🟠 Majors (Riscos Operacionais, Bugs Windows/AD e Consistência)

### 3. [Major | `egm-design`] Falha de detecção do usuário interativo via `Win32_ComputerSystem.UserName` em sessões RDP / Fast User Switching
*   **Escopo Afetado:** Linhas 78, 108–109 (`EnvironmentContext`).
*   **Descrição e Racional Técnico:**
    *   `Win32_ComputerSystem.UserName` consulta apenas a sessão física de console (`Session 1`). Em atendimentos via RDP ou máquinas com múltiplas sessões, retorna `null` ou o usuário errado. Além disso, `NTAccount.Translate` pode falhar se o DC estiver inacessível.
*   **Correção Proposta:**
    *   Resolver o usuário interativo obtendo o proprietário do processo `explorer.exe` na mesma `SessionId` (`Process.GetCurrentProcess().SessionId`) via WMI (`Win32_Process.GetOwnerSid()`), usando `Win32_ComputerSystem.UserName` como fallback secundário e tratando graciosamente a ausência de sessão interativa.

### 4. [Major | `egm-design`] Ordem dos comandos no Reset de Rede e política de timeouts no `ProcessRunner`
*   **Escopo Afetado:** Linhas 103–104, 124 (`ProcessRunner` + `NetworkModule`).
*   **Descrição e Racional Técnico:**
    *   Na linha 124, rodar `netsh winsock reset` e `netsh int ip reset` *depois* de `ipconfig /renew` redefine a pilha IP após a renovação do lease DHCP. Além disso, o documento não distingue o timeout de comandos rápidos (30–60 s) do timeout de comandos longos (`sfc`, `dism`, `chkdsk`, `pnputil`, que levam de 15 a 45 minutos).
*   **Correção Proposta:**
    *   Definir timeouts por categoria no `ProcessRunner` (60 s padrão para rede/AD; 45 minutos para manutenção pesada/backup de drivers).
    *   Ordenar o Reset de Rede como: `netsh winsock reset` -> `netsh int ip reset` -> `ipconfig /release` -> `ipconfig /flushdns` -> `ipconfig /renew`.

### 5. [Major | `egm-design`] `ActiveDirectoryModule`: falha de `w32tm /query /status` quando serviço está parado, escopo de `klist` e TLS 1.2 no Exchange
*   **Escopo Afetado:** Linhas 139–140 (`ActiveDirectoryModule`).
*   **Descrição e Racional Técnico:**
    *   `w32tm /query /status` falha com `0x80070426` se o serviço `W32Time` estiver parado e depende de parsing no idioma do SO.
    *   `klist.exe` em sessão elevada inspeciona apenas os tíquetes do técnico admin.
    *   No `.NET Framework 4.8`, requisições HTTPS para `ExchangeCheckUrl` exigem `SecurityProtocolType.Tls12` explícito e tratamento diferenciado entre erro TCP 443, erro de certificado SSL/CA interna e código HTTP.
*   **Correção Proposta:**
    *   Usar `w32tm.exe /stripchart /computer:<DomainFqdn> /samples:1 /dataonly` para medir o desvio real de relógio contra o DC sem depender do serviço local nem do idioma.
    *   Explicitar o escopo de `klist.exe` e detalhar a verificação TLS 1.2 / certificado / status HTTP para `ExchangeCheckUrl`.

### 6. [Major | `egm-design`] Bloqueio síncrono na busca COM `Microsoft.Update.Session`
*   **Escopo Afetado:** Linha 136 (`DriversUpdatesModule`).
*   **Descrição e Racional Técnico:**
    *   `UpdateSearcher.Search("IsInstalled=0")` é uma chamada COM síncrona que pode bloquear o console por vários minutos sem saída de texto e lançar `COMException` quando offline/WSUS indisponível.
*   **Correção Proposta:**
    *   Executar a busca COM em `Task` com indicador visual de tempo decorrido no console, timeout controlado e tradução de `COMException` (`HRESULT`).

### 7. [Major | `egm-design`] Encoding de saída no `ProcessRunner` (`CP850`/OEM vs. `UTF-16LE` do `sfc.exe`)
*   **Escopo Afetado:** Linhas 101–104, 120 (`ProcessRunner` + `MaintenanceModule`).
*   **Descrição e Racional Técnico:**
    *   Comandos nativos (`ipconfig`, `netsh`, `chkdsk`, `nltest`) usam OEM CodePage (`CP850`), enquanto `sfc.exe` emite saída em `UTF-16LE` quando redirecionado via pipe, gerando caracteres nulos `\0` ou acentuação corrompida se lido com encoding fixo.
*   **Correção Proposta:**
    *   Configurar `ProcessRunner` para usar o OEM CodePage do sistema por padrão e higienizar/decodificar `UTF-16LE` para `sfc.exe`.

### 8. [Major | `egm-style`] Inconsistência sobre o comportamento de falha e destino da cópia secundária de log
*   **Escopo Afetado:** Linhas 30, 54, 60 e 113 (`Goals`, `CUJ 1`, `CUJ 2`, `AuditLogger`).
*   **Descrição e Racional Técnico:**
    *   A Linha 30 fala em *"fallback silencioso caso haja bloqueio de permissão ou redirecionamento OneDrive"*, a Linha 60 (`CUJ 2`) menciona *"cópia secundária em rede"*, enquanto as Linhas 44 e 113 definem `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` com *"aviso no console sem interromper o atendimento"*.
*   **Correção Proposta:**
    *   Unificar todas as menções: destino secundário padrão em `C:\Users\Public\Desktop\TecnicoBrasfort_Logs\` (ou `LogDirectoryOverride`) com **falha não bloqueante e aviso no console**.

---

## 🟡 Nits (Refinamentos Técnicos, Estilo e Estrutura)

### 9. [Nit | `egm-design`] `PrintingModule`: formatação de `,,servidor,impressora` e estado `StopPending` do Spooler
*   **Escopo Afetado:** Linhas 128–129.
*   **Correção Proposta:** Converter nomes de subchaves `,,servidor,impressora` em `\\servidor\impressora` e aguardar/forçar término de `spoolsv.exe` caso o serviço `Spooler` fique em `StopPending`.

### 10. [Nit | `egm-design`] Sanitização de `<TechAdmin>` no nome do arquivo de log
*   **Escopo Afetado:** Linha 113 (`AuditLogger`).
*   **Correção Proposta:** Extrair apenas o nome de usuário sem prefixo `DOMINIO\` e remover `Path.GetInvalidFileNameChars()` ao montar o nome do arquivo de log.

### 11. [Nit | `egm-style`] Quantificação de "timeout curto" e "principais processos"
*   **Escopo Afetado:** Linhas 126 e 132.
*   **Correção Proposta:** Especificar `timeout de 3000 ms` no `TcpClient` (Linha 126) e `top 15 processos ordenados por WorkingSet64` (Linha 132).

### 12. [Nit | `egm-style`] Nota de atualização do `AGENTS.md` dentro do contrato de `IProcessRunner`
*   **Escopo Afetado:** Linha 102.
*   **Correção Proposta:** Manter a tarefa de atualização do `AGENTS.md` apenas na seção `Detailed Implementation Plan` (Item 1), removendo o parêntese operacional da Linha 102.

### 13. [Nit | `egm-style`] Redundância na seção `Open Questions` e na Linha 179
*   **Escopo Afetado:** Linhas 40–47 e 179.
*   **Correção Proposta:** Resumir `Open Questions` em uma única frase e simplificar *"um único `.exe` puro sem dependências externas"* na Linha 179.

### 14. [Nit | `egm-structural`] Divisor horizontal antes de `### Additional Resources`
*   **Escopo Afetado:** Linhas 189–196.
*   **Correção Proposta:** Remover o divisor horizontal da linha 189 (ou manter a hierarquia limpa dentro da seção final antes do `DetailedRange`).

### 15. [Nit | `egm-structural`] Simetria de dois-pontos nos rótulos `Given`/`When`/`Then`
*   **Escopo Afetado:** Linhas 52–66.
*   **Correção Proposta:** Padronizar `*   **Given:**`, `*   **When:**`, `*   **Then:**` com dois-pontos dentro do negrito, igual ao restante do documento.
