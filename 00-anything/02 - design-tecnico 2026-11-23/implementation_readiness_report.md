# Relatório Consolidado — Stage 9 (Implementation Readiness Test)

*   **Documento Auditado:** [tecnico_brasfort_design.md](./tecnico_brasfort_design.md) (`DetailedRange: Linhas 204..332`)
*   **Contexto Base:** [AGENTS.md](../../AGENTS.md) e [concepts_brief.md](../01%20-%20brainstorm-inicial%202026-11-19/concepts_brief.md)
*   **Revisores:** `egm-implementation-readiness` (Pragmatic Tech Lead), `egm-codebase-details` (Pedantic Technical Quality Auditor), `egm-style` (Technical Copy Editor)
*   **Data:** 2026-11-23

---

## Resumo Executivo

Foram consolidados **9 achados** na auditoria de prontidão de implementação (`DetailedRange: Linhas 204..332`):
*   **🔴 Blockers:** 2
*   **🟠 Majors:** 4
*   **🟡 Nits:** 3

---

## 🔴 Blockers (Impeditivos de Compilação e Verificação no Host Atual)

### 1. [Blocker | `egm-implementation-readiness` & `egm-codebase-details`] Compatibilidade sintática C# 5.0 do `csc.exe` v4.0.30319 e referências obrigatórias (incluindo `System.Xml.dll`)
*   **Escopo Afetado:** Item 10 (`scripts/build.ps1`, Linhas 237–239), Item 11 (`TecnicoBrasfort.csproj`, Linhas 243–245) e `Verification Plan` (Linhas 318–320).
*   **Descrição e Racional Técnico:**
    1. O compilador nativo `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` suporta **exclusivamente C# 5.0**. Qualquer sintaxe C# 6+ (interpolação de strings `$"..."`, operador `?.`, `nameof()`, inicializadores de auto-propriedade, expression-bodied `=>`, `out var`) falha com erro de compilação no host atual sem `.NET SDK`.
    2. O `csc.exe` não referencia automaticamente assemblies fora de `mscorlib`/`System`, e `DataContractJsonSerializer` depende tanto de `System.Runtime.Serialization.dll` quanto de `System.Xml.dll`.
*   **Correção Proposta:**
    *   Especificar `<LangVersion>5</LangVersion>` e sintaxe **C# 5.0 estrita** em todo o código-fonte (`src/` e `tests/`).
    *   Incluir `System.Xml.dll` nas referências e definir a linha de comando exata do `csc.exe` em `scripts/build.ps1`:
        `csc.exe /nologo /target:exe /optimize+ /warnaserror+ /win32manifest:src\TecnicoBrasfort\app.manifest /out:dist\TecnicoBrasfort.exe /r:System.dll /r:System.Core.dll /r:System.Management.dll /r:System.Runtime.Serialization.dll /r:System.Xml.dll /r:System.ServiceProcess.dll /r:System.Net.Http.dll /recurse:src\TecnicoBrasfort\*.cs`

### 2. [Blocker | `egm-implementation-readiness` & `egm-codebase-details`] Impossibilidade de executar `dotnet test` / NuGet `xUnit` na máquina atual sem `.NET SDK`
*   **Escopo Afetado:** Itens 31–33 (`tests/TecnicoBrasfort.Tests/*`, Linhas 306–314) e `Verification Plan` (Linhas 321–322).
*   **Descrição e Racional Técnico:**
    *   Como o host atual não possui `.NET SDK`, `dotnet test` não pode restaurar pacotes NuGet (`xUnit`) nem compilar o projeto de testes, o que bloquearia o item obrigatório de Testes Unitários na Seção 13 do `AGENTS.md`.
*   **Correção Proposta:**
    *   Incluir um runner de testes auto-contido em C# 5.0 / BCL pura (`tests/TecnicoBrasfort.Tests/TestRunner.cs` com asserções nativas `Assert.Equal`/`Assert.True`), compilado e executado diretamente pelo `scripts/build.ps1 -RunTests` via `csc.exe` (gerando e executando `dist/TecnicoBrasfort.Tests.exe` com código de saída `0` em caso de sucesso).

---

## 🟠 Majors (Contratos, Estrutura de Arquivos e Sincronização com `AGENTS.md`)

### 3. [Major | Todos os 3 revisores] Agrupamento de múltiplos arquivos nos Itens 16, 17 e 18 e ausência de assinaturas exatas em `Core/`
*   **Escopo Afetado:** Itens 14 a 22 (Linhas 252–279).
*   **Descrição e Racional Técnico:**
    *   Os itens 16, 17 e 18 agrupam dois arquivos na mesma entrada e omitem o caminho relativo do segundo arquivo (`e ProcessRunner.cs`). Além disso, faltam as assinaturas exatas de métodos das interfaces (`IProcessRunner`, `IFileSystem`/`ISystemEnvironment`) e propriedades dos DTOs em `Models.cs` (bem como a distinção entre o serviço `EnvironmentContext` e o DTO `EnvironmentInfo`).
*   **Correção Proposta:**
    *   Listar **1 arquivo por item numerado** com caminho relativo completo (`../../src/...`).
    *   Explicitar as assinaturas exatas de `Models.cs`, `IProcessRunner` e `IFileSystem` (consolidando a leitura de `HKU\<UserSID>\Printers\Connections` para eliminar `IRegistryReader` avulso, além de remover `UncStager.cs` conforme convergência dos Stages 5, 7 e 8).

### 4. [Major | `egm-implementation-readiness`] Mapeamento explícito das opções numeradas do menu (`1..23` + `0` Sair) para os métodos dos 7 módulos
*   **Escopo Afetado:** Item 23 (`UI/ConsoleMenu.cs`, Linhas 279–281) e Itens 24–30 (`Modules/*`, Linhas 282–302).
*   **Descrição e Racional Técnico:**
    *   O plano cita "exibir as 7 categorias numeradas", mas não lista a tabela de correspondência entre cada número digitado pelo técnico (`1` a `23` e `0`) e o método público correspondente em cada módulo, nem as dependências injetadas no construtor dos módulos.
*   **Correção Proposta:**
    *   Incluir a tabela exata de opções do menu (`[1]` a `[23]` distribuídas nas 7 categorias + `[0] Sair`) e a assinatura padrão de construtor dos módulos.

### 5. [Major | `egm-codebase-details`] Atualização incompleta das Seções 3, 5, 7 e 13 do `AGENTS.md` no Item 1
*   **Escopo Afetado:** Item 1 (`../../AGENTS.md`, Linhas 210–212).
*   **Descrição e Racional Técnico:**
    *   O Item 1 prevê atualizar apenas as Seções 5, 9 e 10 do `AGENTS.md`, mas as Seções 3 (Comandos principais) e 13 (Checklist final) ainda exigem `dotnet build`/`dotnet test` sem mencionar `scripts/build.ps1 -RunTests`, a Seção 5 não lista `02 - design-tecnico 2026-11-23/` e `dist/`, e a Seção 7 precisa alinhar `EnvironmentContext` / `EnvironmentInfo`.
*   **Correção Proposta:**
    *   Expandir o Item 1 para cobrir a atualização das Seções 2, 3, 5, 6, 7, 8, 9, 10, 12 e 13 do `AGENTS.md`.

### 6. [Major | `egm-style` & `egm-implementation-readiness`] Referência a "Regra 5.1" inexistente no `AGENTS.md`
*   **Escopo Afetado:** Itens 2, 3 e 4 (Linhas 215, 218 e 221).
*   **Correção Proposta:**
    *   Substituir `"conforme Regra 5.1"` por `"conforme Seção 5 do AGENTS.md"`.

---

## 🟡 Nits (Padronização e Plano de Verificação)

### 7. [Nit | `egm-style`] Padronização das máscaras de nome de arquivo no `AuditLogger`
*   **Escopo Afetado:** Item 22 (Linha 278).
*   **Correção Proposta:** Usar `<HOSTNAME>_<YYYY-MM-DD>.log` e `<HOSTNAME>_<YYYY-MM-DD>_<HHmmss>_<TechAdmin>.log` (com `<TechAdmin>` sanitizado sem `DOMINIO\`).

### 8. [Nit | `egm-style`] Desduplicação do aviso sobre ausência do `.NET SDK` no `Verification Plan`
*   **Escopo Afetado:** Linhas 318–325.
*   **Correção Proposta:** Declarar a condição do host (sem `.NET SDK`, com `csc.exe` v4.0.30319) uma única vez no início do `Verification Plan`.

### 9. [Nit | `egm-codebase-details`] Verificação explícita do checklist da Seção 13 e fluxo Git no `Verification Plan`
*   **Escopo Afetado:** Linhas 316–329.
*   **Correção Proposta:** Incluir no `Verification Plan` a verificação de zero arquivos temporários na raiz (`00-anything/`), atualização de `graphify-out/` e conformidade de branch/commits Git.
