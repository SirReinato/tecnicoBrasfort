using System;
using System.Collections.Generic;

namespace TecnicoBrasfort.Core
{
    public static class ActionCatalog
    {
        private static readonly List<ActionDefinition> AllActionsList = BuildActions();
        private static readonly List<ComboDefinition> AllCombosList = BuildCombos();

        public static IList<ActionDefinition> GetBatchableActions()
        {
            List<ActionDefinition> list = new List<ActionDefinition>();
            for (int i = 0; i < AllActionsList.Count; i++)
            {
                if (AllActionsList[i].IsBatchable)
                {
                    list.Add(AllActionsList[i]);
                }
            }
            return list;
        }

        public static IList<ActionDefinition> GetQuickTools()
        {
            List<ActionDefinition> list = new List<ActionDefinition>();
            for (int i = 0; i < AllActionsList.Count; i++)
            {
                if (!AllActionsList[i].IsBatchable)
                {
                    list.Add(AllActionsList[i]);
                }
            }
            return list;
        }

        public static IList<ComboDefinition> GetCombos()
        {
            return AllCombosList.AsReadOnly();
        }

        public static ActionDefinition FindActionById(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return null;
            }

            for (int i = 0; i < AllActionsList.Count; i++)
            {
                if (string.Equals(AllActionsList[i].Id, actionId, StringComparison.OrdinalIgnoreCase))
                {
                    return AllActionsList[i];
                }
            }

            return null;
        }

        public static ComboDefinition FindComboById(string comboId)
        {
            if (string.IsNullOrEmpty(comboId))
            {
                return null;
            }

            for (int i = 0; i < AllCombosList.Count; i++)
            {
                if (string.Equals(AllCombosList[i].Id, comboId, StringComparison.OrdinalIgnoreCase))
                {
                    return AllCombosList[i];
                }
            }

            return null;
        }

        private static List<ActionDefinition> BuildActions()
        {
            List<ActionDefinition> list = new List<ActionDefinition>();

            // 1. Manutencao (7 acoes em lote)
            list.Add(CreateAction(
                "maint_temp",
                "Limpeza de Temporarios",
                "Manutencao",
                "Remove arquivos temporarios do usuario ativo e de C:\\Windows\\Temp sem interromper programas em uso.",
                "del /q /f /s \"%TEMP%\\*\" & del /q /f /s \"C:\\Windows\\Temp\\*\"",
                true, true, false, false, false, "~10s"));

            list.Add(CreateAction(
                "maint_winsxs",
                "Limpeza de Componentes WinSxS",
                "Manutencao",
                "Compacta e remove versoes obsoletas de atualizacoes do Windows no repositorio WinSxS.",
                "dism.exe /Online /Cleanup-Image /StartComponentCleanup",
                false, true, false, false, false, "~2-5 min"));

            list.Add(CreateAction(
                "maint_sfc",
                "Verificacao de Arquivos (SFC)",
                "Manutencao",
                "Verifica e repara arquivos protegidos do sistema operacional Windows.",
                "sfc.exe /scannow",
                false, true, false, false, false, "~3-10 min"));

            list.Add(CreateAction(
                "maint_dism",
                "Reparo de Imagem (DISM RestoreHealth)",
                "Manutencao",
                "Verifica e restaura a integridade da imagem do Windows antes de rodar o SFC.",
                "dism.exe /Online /Cleanup-Image /RestoreHealth",
                false, true, false, false, false, "~5-15 min"));

            list.Add(CreateAction(
                "disk_check_ro",
                "Verificar Disco C: (Somente Leitura)",
                "Manutencao",
                "Analisa o sistema de arquivos da unidade C: em modo somente leitura sem agendar reinicializacao.",
                "chkdsk.exe C:",
                false, true, false, false, false, "~1-3 min"));

            list.Add(CreateAction(
                "disk_schedule_boot",
                "Agendar Reparo de Disco no Proximo Boot",
                "Manutencao",
                "Agenda a verificacao e reparo do volume C: na proxima inicializacao do Windows.",
                "chkntfs.exe /c C:",
                false, true, true, false, true, "~5s"));

            list.Add(CreateAction(
                "maint_disk_health",
                "Saude Fisica dos Discos (SMART)",
                "Manutencao",
                "Consulta via WMI o status SMART, modelo e capacidade dos discos fisicos instalados.",
                "wmic diskdrive get Model,Status,MediaType,Size",
                true, true, false, false, false, "~5s"));

            // 2. Rede (4 acoes em lote + 1 ferramenta rapida)
            list.Add(CreateAction(
                "net_reset",
                "Reset Completo de Rede (Winsock + IP + DNS)",
                "Rede",
                "Redefine catalogo Winsock, pilha TCP/IP, renova DHCP e limpa cache DNS. Derruba acesso remoto temporariamente.",
                "netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /flushdns & ipconfig /renew",
                false, true, true, true, true, "~20s"));

            list.Add(CreateAction(
                "net_info",
                "Configuracoes de Rede (ipconfig /all)",
                "Rede",
                "Exibe adaptadores, enderecos IP, gateway, servidores DNS e sufixos configurados.",
                "ipconfig.exe /all",
                false, true, false, false, false, "~3s"));

            list.Add(CreateAction(
                "net_connectivity",
                "Testes de Conectividade (Gateway/DNS/AD)",
                "Rede",
                "Executa testes ICMP e resolucao DNS para gateway padrao, DNS publico e dominio configurado.",
                "ping 1.1.1.1 & ping 8.8.8.8 & nslookup <DomainFqdn>",
                true, true, false, false, false, "~15s"));

            list.Add(CreateAction(
                "net_flush_dns",
                "Limpar Cache DNS (ipconfig /flushdns)",
                "Rede",
                "Esvazia o cache de resolucao de nomes DNS do Windows sem derrubar conexoes ativas.",
                "ipconfig.exe /flushdns",
                false, true, false, false, false, "~3s"));

            list.Add(CreateAction(
                "net_connections",
                "Abrir Conexoes de Rede (ncpa.cpl)",
                "Rede",
                "Abre a janela classica de adaptadores de rede do Painel de Controle.",
                "ncpa.cpl",
                false, false, false, false, false, "Imediato"));

            // 3. Impressao (2 acoes em lote)
            list.Add(CreateAction(
                "print_clear_queue",
                "Limpar Fila de Impressao",
                "Impressao",
                "Para o servico Spooler, remove trabalhos travados na pasta PRINTERS e reinicia o Spooler.",
                "net stop spooler & del /q /f \"%SystemRoot%\\System32\\spool\\PRINTERS\\*\" & net start spooler",
                true, true, true, false, false, "~10s"));

            list.Add(CreateAction(
                "print_list",
                "Listar Impressoras Instaladas",
                "Impressao",
                "Lista impressoras locais e conexoes mapeadas no perfil do usuario ativo.",
                "wmic printer get Name,DriverName,PortName,PrinterStatus",
                true, true, false, false, false, "~5s"));

            // 4. Diagnostico (2 acoes em lote + 1 ferramenta rapida)
            list.Add(CreateAction(
                "diag_sysinfo",
                "Resumo do Sistema (SO, Hardware, Uptime)",
                "Diagnostico",
                "Coleta versao do Windows, fabricante/modelo, RAM, discos livres e tempo de atividade.",
                "systeminfo.exe",
                true, true, false, false, false, "~5s"));

            list.Add(CreateAction(
                "diag_processes",
                "Top 15 Processos (Memoria)",
                "Diagnostico",
                "Lista os 15 processos com maior consumo de memoria no momento.",
                "tasklist.exe /v",
                true, true, false, false, false, "~5s"));

            list.Add(CreateAction(
                "diag_resmon",
                "Abrir Monitor de Recursos (resmon)",
                "Diagnostico",
                "Abre o Monitor de Recursos do Windows para inspecao em tempo real de CPU, memoria, disco e rede.",
                "resmon.exe",
                false, false, false, false, false, "Imediato"));

            // 5. Drivers e Updates (2 acoes em lote + 2 ferramentas rapidas)
            list.Add(CreateAction(
                "drv_errors",
                "Dispositivos com Erro no Gerenciador",
                "Drivers e Updates",
                "Consulta via WMI dispositivos com codigo de erro no Gerenciador de Dispositivos.",
                "pnputil.exe /enum-devices /problem",
                true, true, false, false, false, "~5s"));

            list.Add(CreateAction(
                "drv_search_updates",
                "Buscar Atualizacoes Pendentes (Windows Update)",
                "Drivers e Updates",
                "Consulta atualizacoes pendentes via API nativa do Windows Update Agent (WUA).",
                "UsoClient.exe StartScan",
                true, true, false, false, false, "~30-60s"));

            list.Add(CreateAction(
                "drv_backup",
                "Backup de Drivers (pnputil)",
                "Drivers e Updates",
                "Exporta drivers de terceiros instalados para uma pasta de destino apos validar espaco livre >= 5 GB.",
                "pnputil.exe /export-driver * <destino>",
                false, false, false, false, false, "~1-3 min"));

            list.Add(CreateAction(
                "drv_wu_settings",
                "Abrir Configuracoes do Windows Update",
                "Drivers e Updates",
                "Abre a tela de configuracoes do Windows Update no Windows.",
                "ms-settings:windowsupdate",
                false, false, false, false, false, "Imediato"));

            // 6. Active Directory e Exchange (3 acoes em lote)
            list.Add(CreateAction(
                "ad_gpupdate",
                "Atualizar Politicas de Grupo (gpupdate)",
                "AD e Exchange",
                "Forca a atualizacao das diretivas de grupo de computador com gpupdate /Target:Computer /Force.",
                "gpupdate.exe /Target:Computer /Force",
                false, true, false, false, false, "~15-30s"));

            list.Add(CreateAction(
                "ad_gpresult",
                "Politicas Aplicadas (gpresult /r)",
                "AD e Exchange",
                "Exibe o resumo das diretivas de grupo aplicadas ao computador.",
                "gpresult.exe /R /SCOPE COMPUTER",
                false, true, false, false, false, "~10s"));

            list.Add(CreateAction(
                "ad_diagnostics",
                "Diagnostico de Dominio, Hora e Exchange",
                "AD e Exchange",
                "Valida canal seguro do AD (nltest), sincronismo NTP (w32tm) e disponibilidade HTTPS do Exchange.",
                "nltest /sc_verify:<DomainFqdn> & w32tm /query /status",
                true, true, false, false, false, "~15s"));

            // 7. Auditoria (1 ferramenta rapida)
            list.Add(CreateAction(
                "audit_logs",
                "Visualizar Logs Locais",
                "Auditoria",
                "Exibe os ultimos registros de auditoria salvos para este computador.",
                "type C:\\ProgramData\\Brasfort\\TecnicoBrasfort\\Logs\\*.log",
                true, false, false, false, false, "Imediato"));

            return list;
        }

        private static List<ComboDefinition> BuildCombos()
        {
            List<ComboDefinition> combos = new List<ComboDefinition>();

            combos.Add(CreateCombo(
                "combo_limpeza",
                "1. Limpeza Logica / Travamentos",
                "Remove temporarios, repara imagem (DISM), verifica arquivos (SFC) e limpa WinSxS.",
                new string[] { "maint_temp", "maint_dism", "maint_sfc", "maint_winsxs" }));

            combos.Add(CreateCombo(
                "combo_rede_remoto",
                "2. Diagnostico de Rede (Seguro para Remoto)",
                "Coleta ipconfig, limpa cache DNS e testa conectividade sem derrubar acesso remoto.",
                new string[] { "net_info", "net_flush_dns", "net_connectivity" }));

            combos.Add(CreateCombo(
                "combo_rede_completo",
                "3. Reparo de Rede Completo (Presencial)",
                "Executa reset completo de Winsock/IP/DNS e valida conectividade. Requer reinicializacao.",
                new string[] { "net_reset", "net_connectivity" }));

            combos.Add(CreateCombo(
                "combo_ad_exchange",
                "4. Revisao AD / GPO / Exchange",
                "Forca gpupdate, gera gpresult do computador e diagnostica AD, NTP e Exchange.",
                new string[] { "ad_gpupdate", "ad_gpresult", "ad_diagnostics" }));

            combos.Add(CreateCombo(
                "combo_checkup",
                "5. Check-up Geral (Somente Leitura)",
                "Levantamento completo do equipamento sem alteracoes: sistema, discos, drivers, rede e AD.",
                new string[] { "diag_sysinfo", "maint_disk_health", "drv_errors", "net_connectivity", "ad_diagnostics" }));

            return combos;
        }

        private static ActionDefinition CreateAction(
            string id,
            string name,
            string category,
            string description,
            string commandText,
            bool isNativeApiEquivalent,
            bool isBatchable,
            bool isDestructive,
            bool dropsRemoteAccess,
            bool requiresReboot,
            string estimatedDuration)
        {
            ActionDefinition def = new ActionDefinition();
            def.Id = id;
            def.Name = name;
            def.Category = category;
            def.Description = description;
            def.CommandText = commandText;
            def.IsNativeApiEquivalent = isNativeApiEquivalent;
            def.IsBatchable = isBatchable;
            def.IsDestructive = isDestructive;
            def.DropsRemoteAccess = dropsRemoteAccess;
            def.RequiresReboot = requiresReboot;
            def.EstimatedDuration = estimatedDuration;
            return def;
        }

        private static ComboDefinition CreateCombo(
            string id,
            string name,
            string description,
            string[] actionIds)
        {
            ComboDefinition combo = new ComboDefinition();
            combo.Id = id;
            combo.Name = name;
            combo.Description = description;
            combo.ActionIds = actionIds;
            return combo;
        }
    }
}
