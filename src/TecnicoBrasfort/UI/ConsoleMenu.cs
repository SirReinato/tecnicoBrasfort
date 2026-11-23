using System;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;

namespace TecnicoBrasfort.UI
{
    public class ConsoleMenu
    {
        private readonly EnvironmentInfo _env;
        private readonly MaintenanceModule _maintenance;
        private readonly NetworkModule _network;
        private readonly PrintingModule _printing;
        private readonly DiagnosticsModule _diagnostics;
        private readonly DriversUpdatesModule _drivers;
        private readonly ActiveDirectoryModule _ad;
        private readonly AuditModule _audit;

        public ConsoleMenu(
            EnvironmentInfo env,
            MaintenanceModule maintenance,
            NetworkModule network,
            PrintingModule printing,
            DiagnosticsModule diagnostics,
            DriversUpdatesModule drivers,
            ActiveDirectoryModule ad,
            AuditModule audit)
        {
            _env = env;
            _maintenance = maintenance;
            _network = network;
            _printing = printing;
            _diagnostics = diagnostics;
            _drivers = drivers;
            _ad = ad;
            _audit = audit;
        }

        public void Run()
        {
            try
            {
                Console.Title = "Painel de Suporte TI - Tecnico Brasfort";
            }
            catch (Exception)
            {
            }

            while (true)
            {
                RenderMenu();
                Console.Write("\nEscolha uma opcao: ");
                string choice = Console.ReadLine();
                if (choice == null)
                {
                    break;
                }

                string trimmed = choice.Trim();
                if (trimmed == "0")
                {
                    Console.WriteLine("Encerrando Tecnico Brasfort...");
                    break;
                }

                Console.WriteLine();
                ExecuteChoice(trimmed);
                Console.WriteLine("\nPressione ENTER para voltar ao menu...");
                Console.ReadLine();
            }
        }

        private void RenderMenu()
        {
            try
            {
                Console.Clear();
            }
            catch (Exception)
            {
            }

            Console.WriteLine("========================================================================");
            Console.WriteLine("             TECNICO BRASFORT  |  PAINEL DE SUPORTE DA TI               ");
            Console.WriteLine("========================================================================");
            Console.WriteLine(string.Format("PC: {0}  |  Usuario logado: {1}", _env.Hostname, _env.InteractiveUser));
            Console.WriteLine(string.Format("Admin (UAC): {0}  |  SO: {1}", _env.ElevatedAdmin, _env.OsVersion));
            Console.WriteLine(string.Format("Origem: {0} ({1})",
                _env.OriginDirectory,
                _env.IsUncOrigin ? "UNC - Config em Memoria" : "Local/Pendrive"));

            if (_env.InteractiveUser == "(nenhum)")
            {
                Console.WriteLine("[AVISO] Nenhum usuario interativo detectado nesta sessao.");
            }

            Console.WriteLine();
            Console.WriteLine("--- MANUTENCAO DO SISTEMA ---");
            Console.WriteLine(" [ 1] Limpeza de arquivos temporarios");
            Console.WriteLine(" [ 2] Limpeza de disco avancada (DISM WinSxS)");
            Console.WriteLine(" [ 3] Verificar e reparar arquivos do Windows (SFC)");
            Console.WriteLine(" [ 4] Reparar a imagem do Windows (DISM /RestoreHealth)");
            Console.WriteLine(" [ 5] Verificar o disco C: (leitura / agendar boot)");
            Console.WriteLine(" [ 6] Saude do disco (HD/SSD - SMART)");
            Console.WriteLine();
            Console.WriteLine("--- REDE E INTERNET ---");
            Console.WriteLine(" [ 7] Reset de rede (Winsock, IP e DNS)");
            Console.WriteLine(" [ 8] Informacoes de rede (ipconfig /all)");
            Console.WriteLine(" [ 9] Testes de conectividade (Gateway, DNS, AD e Exchange)");
            Console.WriteLine(" [10] Limpar cache de DNS");
            Console.WriteLine(" [11] Abrir Conexoes de Rede");
            Console.WriteLine();
            Console.WriteLine("--- IMPRESSAO ---");
            Console.WriteLine(" [12] Limpar fila de impressao travada");
            Console.WriteLine(" [13] Listar impressoras (maquina e usuario)");
            Console.WriteLine();
            Console.WriteLine("--- DIAGNOSTICO ---");
            Console.WriteLine(" [14] Informacoes do sistema");
            Console.WriteLine(" [15] Monitor de recursos e processos");
            Console.WriteLine();
            Console.WriteLine("--- DRIVERS E ATUALIZACOES ---");
            Console.WriteLine(" [16] Dispositivos com erro");
            Console.WriteLine(" [17] Buscar atualizacoes pendentes (Windows Update)");
            Console.WriteLine(" [18] Backup dos drivers");
            Console.WriteLine(" [19] Abrir configuracoes do Windows Update");
            Console.WriteLine();
            Console.WriteLine("--- ACTIVE DIRECTORY / GPO / EXCHANGE ---");
            Console.WriteLine(" [20] Atualizar GPO do computador (gpupdate /force)");
            Console.WriteLine(" [21] Relatorio de GPO do computador (gpresult)");
            Console.WriteLine(" [22] Diagnostico AD, Canal Seguro, Relogio e Exchange");
            Console.WriteLine();
            Console.WriteLine("--- TI ---");
            Console.WriteLine(" [23] Ver registro de acoes deste PC");
            Console.WriteLine();
            Console.WriteLine(" [ 0] Sair");
        }

        private void ExecuteChoice(string choice)
        {
            switch (choice)
            {
                case "1":
                    _maintenance.CleanTempFiles();
                    break;
                case "2":
                    _maintenance.CleanComponentStore();
                    break;
                case "3":
                    _maintenance.RunSfc();
                    break;
                case "4":
                    _maintenance.RunDismRestoreHealth();
                    break;
                case "5":
                    _maintenance.CheckDiskC(false);
                    if (ConfirmAction("Deseja agendar verificacao/reparo do disco C: para o proximo boot (chkntfs /c C:)?"))
                    {
                        _maintenance.CheckDiskC(true);
                    }
                    break;
                case "6":
                    _maintenance.CheckDiskHealth();
                    break;
                case "7":
                    Console.WriteLine("[ATENCAO] O Reset de Rede pode derrubar conexoes remotas (RDP/AnyDesk/TeamViewer).");
                    if (ConfirmAction("Confirmar execucao do Reset de Rede?"))
                    {
                        _network.ResetNetwork();
                    }
                    break;
                case "8":
                    _network.ShowNetworkInfo();
                    break;
                case "9":
                    _network.RunConnectivityTests();
                    break;
                case "10":
                    _network.FlushDns();
                    break;
                case "11":
                    _network.OpenNetworkConnections();
                    break;
                case "12":
                    if (ConfirmAction("Confirmar parada do Spooler e limpeza de toda a fila de impressao?"))
                    {
                        _printing.ClearPrintQueue();
                    }
                    break;
                case "13":
                    _printing.ListPrinters();
                    break;
                case "14":
                    _diagnostics.ShowSystemInfo();
                    break;
                case "15":
                    bool openResmon = ConfirmAction("Deseja tambem abrir o Monitor de Recursos (resmon.exe) apos listar os processos?");
                    _diagnostics.ShowProcessMonitor(openResmon);
                    break;
                case "16":
                    _drivers.ListErrorDevices();
                    break;
                case "17":
                    _drivers.SearchPendingUpdates();
                    break;
                case "18":
                    Console.Write("Informe a pasta de destino (ENTER para padrao C:\\Temp\\DriverBackup_<PC>): ");
                    string dest = Console.ReadLine();
                    _drivers.BackupDrivers(dest);
                    break;
                case "19":
                    _drivers.OpenWindowsUpdateSettings();
                    break;
                case "20":
                    _ad.ForceComputerGpUpdate();
                    break;
                case "21":
                    _ad.ShowComputerGpResult();
                    break;
                case "22":
                    _ad.RunDomainAndExchangeDiagnostics();
                    break;
                case "23":
                    _audit.ShowHostLogs();
                    break;
                default:
                    Console.WriteLine("Opcao invalida.");
                    break;
            }
        }

        private static bool ConfirmAction(string prompt)
        {
            Console.Write(prompt + " [S/N]: ");
            string resp = Console.ReadLine();
            if (resp == null)
            {
                return false;
            }
            string clean = resp.Trim();
            return clean.Equals("S", StringComparison.OrdinalIgnoreCase)
                || clean.Equals("SIM", StringComparison.OrdinalIgnoreCase);
        }
    }
}
