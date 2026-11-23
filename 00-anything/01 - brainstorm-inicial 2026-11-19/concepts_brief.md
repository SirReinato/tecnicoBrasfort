# Painel CLI de Suporte TI — Tecnico Brasfort

* **Author:** Renato França
* **Date:** 2026-11-19

## Overview

O **Tecnico Brasfort** é uma ferramenta portátil de linha de comando (CLI) para técnicos e analistas de TI executarem rotinas de manutenção, diagnóstico, rede, impressão, drivers, Active Directory/GPO e auditoria em estações Windows. A ferramenta é transportada em pendrive ou pasta de rede compartilhada e executada sobre a sessão do usuário final mediante elevação UAC com credenciais administrativas do técnico.

O projeto também estabelece a adaptação completa da governança do `AGENTS.md` de referência (originalmente voltado para React/Vite) para a realidade de scripts e binários Windows (`.exe`/PowerShell/C#), mantendo isolamento de artefatos em `00-anything/`, fluxo Git por branch, decomposição em issues, mapeamento de dependências e verificações automatizadas compatíveis com Windows.

---

## Analysis

A revisão multi-perspectiva (6 lentes) identificou riscos arquiteturais e operacionais que precisam ser tratados antes da implementação:

### 1. Elevação UAC com Credenciais Separadas ("Over-the-Shoulder")
Quando o usuário final está logado e o técnico executa a ferramenta como Administrador usando outra conta:
* **Perda de unidades mapeadas:** Mapeamentos de rede (`Z:\`) pertencem à sessão de logon do usuário e desaparecem no processo elevado.
* **Diretório de trabalho e caminho UNC:** O Windows redefine o diretório atual para `C:\Windows\System32` após o UAC, e caminhos UNC podem falhar se o token elevado não tiver acesso SMB ou se o código depender de caminhos relativos.
* **Desvio de perfil:** Variáveis como `%TEMP%`, `%USERPROFILE%`, `HKCU`, impressoras mapeadas por usuário e `gpupdate /target:user` passam a apontar para o perfil do **técnico admin**, e não para o **usuário logado**.
* **Mitigação necessária:** Capturar o caminho de origem antes ou no momento da elevação, identificar o usuário interativo real (via `Win32_ComputerSystem` / `explorer.exe` / `HKU\<SID>`) e distinguir explicitamente ações de máquina (`HKLM`, `C:\Windows\Temp`) de ações no perfil do usuário logado.

### 2. Queda de Rede ao Executar de Pasta Compartilhada
Se a ferramenta rodar diretamente de um compartilhamento UNC (`\\servidor\pasta\...`) e o técnico acionar **Reset de rede** (`netsh int ip reset`, `ipconfig /release`), a conexão SMB cai no meio da execução, podendo travar o binário ou impedir a gravação de logs.
* **Mitigação necessária:** Ao iniciar a partir de um caminho UNC, copiar/estagiar o executável para um diretório local temporário (ex.: `C:\ProgramData\Brasfort\TecnicoBrasfort\bin`) mantendo o caminho de origem como referência para sincronização de logs.

### 3. Formato de Entrega (`.exe` vs `.ps1` + `.cmd` vs `ps2exe`)
* **`.exe` gerado por `ps2exe`:** Alto risco de falso positivo no Windows Defender e em EDRs, pois wrappers `ps2exe` são frequentemente bloqueados por heurística, além de comportamento frágil de `$PSScriptRoot` e caminhos relativos.
* **`.exe` nativo em C# (.NET Framework 4.8 ou .NET 8 single-file):** Produz binário PE limpo, permite manifesto UAC embutido (`requireAdministrator`), streaming em tempo real de `stdout`/`stderr`, melhor controle de caminhos UNC e menor taxa de falso positivo (especialmente com .NET 4.8 já presente no Windows 10/11, gerando executável abaixo de 1 MB).
* **Assinatura digital:** Um `.exe` não assinado vindo de pendrive ou rede chamando `sfc`, `dism`, `netsh`, `nltest` e apagando arquivos em `spool\PRINTERS` pode disparar alertas comportamentais. O ideal é assinar internamente via AD CS (certificado de Code Signing distribuído por GPO em *Trusted Publishers*).

### 4. Armadilhas nos Módulos Windows e AD/GPO
* **Limpeza de Temp:** Deve varrer `C:\Users\*\AppData\Local\Temp` e `C:\Windows\Temp` ignorando arquivos em uso, em vez de usar `$env:TEMP` do admin.
* **Limpeza de Disco (`cleanmgr`) e `chkdsk`:** `cleanmgr /sagerun` depende de chaves prévias no registro; `Dism.exe /Online /Cleanup-Image /StartComponentCleanup` é mais previsível. O `chkdsk C:` deve rodar primeiro em modo somente leitura, exigindo confirmação explícita antes de agendar `/f` no reboot.
* **Saúde de Disco (SMART):** `Get-PhysicalDisk` e `Get-StorageReliabilityCounter` funcionam em NVMe/AHCI, mas falham atrás de controladoras RAID ou pontes USB.
* **Impressão:** `Get-Printer` elevado lista impressoras da máquina; impressoras de rede mapeadas pelo usuário ficam em `HKU\<UserSID>\Printers\Connections`.
* **Backup de Drivers:** `Export-WindowsDriver` consome de 1 GB a 5 GB; salvar direto no pendrive pode lotar a mídia. Deve validar espaço livre antes e priorizar destino local (ex.: `C:\Temp\DriverBackup_<PC>`).
* **AD / GPO / Exchange (substituindo Entra/Intune/Teams):** Incluir `gpupdate /target:computer /force`, `gpresult /r /scope:computer`, verificação de canal seguro (`Test-ComputerSecureChannel` / `nltest /sc_verify`), checagem de relógio (`w32tm /query /status`, crítico para Kerberos) e testes de conectividade para DC, DNS, portas AD (`53`, `88`, `389`, `445`) e Exchange (`443` / `autodiscover`).
* **Registro de Ações (Opção 23):** Gravar direto apenas na pasta de origem falha se o pendrive estiver protegido contra escrita, se a pasta de rede for somente leitura ou se dois técnicos gravarem no mesmo arquivo ao mesmo tempo. A estratégia segura é **escrita dupla**: log primário garantido localmente (`C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\`) + cópia de melhor esforço na pasta de execução em arquivos únicos por sessão (`<OriginDir>\Logs\<PC>\<Data>_<Hora>_<Tech>.log`).

### 5. Adaptação do `AGENTS.md` para Stack Windows
* **Mantido integralmente:** Atualização obrigatória do `AGENTS.md` a cada mudança estrutural, fluxo Git em branches (`feature/`, `fix/`), commits semânticos, decomposição em issues (Linear MCP + formato copiável), isolamento em `00-anything/`, inicialização literal/genérica (sem IPs, domínios reais ou credenciais hardcoded no código — usar `config.example.json`) e checklist final.
* **Substituído na stack técnica:** Trocar Vite/React/`styled-components`/Playwright/Semgrep por equivalentes reais da stack escolhida:
  * **Linting / Formatação:** `dotnet format` + analisadores Roslyn / `.editorconfig` (C#) ou `PSScriptAnalyzer` (PowerShell).
  * **Testes Automatizados:** `xUnit` + mocks de interface (`IProcessRunner`, `IFileSystem`) em C# ou `Pester v5` com `Mock` em PowerShell, permitindo validar lógica sem executar comandos destrutivos na máquina do desenvolvedor.
  * **Segurança / SAST:** `dotnet list package --vulnerable` + analisadores de segurança (C#) ou regras de segurança do `PSScriptAnalyzer` (PowerShell) + verificação contra vazamento de segredos/dados internos.
  * **Mapeamento de Dependências (`graphify`):** Uso de `graphify` integrado ao projeto ou geração determinística de grafo de dependências dos módulos/projetos.

---

## Goals vs. Non-Goals

* **In-Scope Goals:**
  * Ferramenta CLI executável (`.exe`) para técnicos de TI com menu organizado por categorias (Manutenção do Sistema, Rede e Internet, Impressão, Diagnóstico, Drivers e Atualizações, AD/GPO/Exchange e Auditoria/Logs).
  * Tratamento seguro de elevação UAC, caminhos UNC/pendrive, detecção do usuário logado na sessão e confirmações explícitas para ações destrutivas ou que exigem reboot.
  * Registro de ações auditável com fallback local (`ProgramData`) e sincronização na pasta de execução.
  * Repositório inicializado em `C:\Users\renato.lima\Documents\10 - scripts\Tecnico Brasfort` seguindo todas as regras adaptadas do `AGENTS.md`.
* **Out-of-Scope Non-Goals:**
  * Uso autônomo por usuários finais (self-service).
  * Integração com Entra ID, Intune ou limpeza de cache do Microsoft Teams (removidos do escopo por solicitação do autor).
  * Hardcoding de IPs internos, nomes de servidores ou credenciais administrativas no código-fonte.

---

## Alternatives Weighed

* **Batch puro (`.bat` / `.cmd`):** Rejeitado pelo autor e pela análise técnica por limitações de encoding (UTF-8 vs CP850), tratamento frágil de erros e ausência de APIs modernas de sistema/AD.
* **`ps2exe` (empacotamento de `.ps1` em `.exe`):** Rejeitado na análise técnica por gerar alta taxa de falsos positivos em Windows Defender/EDR e quebrar referências de diretório (`$PSScriptRoot`) em execuções elevadas via rede.
* **`.cmd` + `.ps1` modular (sem `.exe`):** Mantido como alternativa de contingência caso a equipe priorize edição rápida em campo sem compilação, embora o autor tenha preferência explícita por `.exe` pela apresentação profissional.

---

## Key Decisions & Staging for Next Steps

Decisões encaminhadas nesta iteração (em validação final):
1. **Stack do `.exe` (Recomendação adotada no `AGENTS.md`):** **C# / .NET Framework 4.8 (`net48`)** — gera `.exe` único abaixo de 1 MB, roda nativamente no Windows 10/11 e Windows Server sem instalar runtime, aceita manifesto UAC embutido e evita os falsos positivos do `ps2exe`.
2. **Assinatura de Código (Code Signing):** Build preparado para suportar `signtool.exe` (via certificado interno AD CS/GPO em *Trusted Publishers* quando disponível), permitindo rodar sem assinatura caso ainda não haja PKI configurada.
3. **Estratégia de Logs (Ajuste para Área de Trabalho):** O usuário solicitou salvar o log na Área de Trabalho. **Risco identificado:** em elevação UAC com conta separada de admin, `%USERPROFILE%\Desktop` aponta para o perfil do **técnico admin** (não do usuário) e pode colidir com redirecionamento de OneDrive. **Mitigação definida:** manter gravação primária garantida em `C:\ProgramData\Brasfort\TecnicoBrasfort\Logs\` e copiar/exportar um resumo ou arquivo de log para a Área de Trabalho Pública (`C:\Users\Public\Desktop\TecnicoBrasfort_Logs\`) — ou Área de Trabalho do usuário interativo — com fallback silencioso se houver bloqueio.
4. **Matriz de SO:** Windows 10/11 (com compatibilidade natural no .NET 4.8 para Windows Server 2016/2019/2022).
