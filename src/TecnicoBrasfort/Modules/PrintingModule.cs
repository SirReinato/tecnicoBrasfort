using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class PrintingModule
    {
        private const string SpoolDir = @"C:\Windows\System32\spool\PRINTERS";

        private readonly IProcessRunner _runner;
        private readonly IHostEnvironment _host;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;

        public PrintingModule(IProcessRunner runner, IHostEnvironment host, AuditLogger logger, EnvironmentInfo env)
        {
            _runner = runner;
            _host = host;
            _logger = logger;
            _env = env;
        }

        public void ClearPrintQueue()
        {
            Console.WriteLine("Parando servico Spooler de impressao...");
            int exitCode = 0;

            try
            {
                using (ServiceController sc = new ServiceController("Spooler"))
                {
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                    {
                        sc.Stop();
                    }

                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        try
                        {
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                        catch (System.ServiceProcess.TimeoutException)
                        {
                            KillSpoolerProcess();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AVISO] Falha ao parar Spooler via ServiceController: " + ex.Message);
                KillSpoolerProcess();
            }

            int removed = 0;
            if (_host.DirectoryExists(SpoolDir))
            {
                foreach (string file in _host.GetFiles(SpoolDir, "*"))
                {
                    if (_host.TryDeleteFile(file))
                    {
                        removed++;
                    }
                }
            }

            try
            {
                using (ServiceController sc = new ServiceController("Spooler"))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Console.WriteLine("[ERRO] Nao foi possivel reiniciar o Spooler: " + ex.Message);
            }

            string summary = string.Format("Fila de impressao limpa: {0} arquivo(s) removido(s) de spool\\PRINTERS.", removed);
            Console.WriteLine(summary);
            LogAction("Limpar fila de impressao travada", exitCode, summary);
        }

        public void ListPrinters()
        {
            Console.WriteLine("Impressoras instaladas na maquina (Win32_Printer):");
            int machineCount = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, PortName, Default, Shared FROM Win32_Printer"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        machineCount++;
                        string name = obj["Name"] != null ? obj["Name"].ToString() : "Sem nome";
                        string port = obj["PortName"] != null ? obj["PortName"].ToString() : "";
                        bool isDefault = obj["Default"] != null && Convert.ToBoolean(obj["Default"]);
                        Console.WriteLine(string.Format(" - {0} (Porta: {1}){2}", name, port, isDefault ? " [PADRAO]" : ""));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao consultar Win32_Printer: " + ex.Message);
            }

            Console.WriteLine("Impressoras de rede mapeadas para o usuario interativo:");
            IList<string> userPrinters = _host.GetUserPrinterConnections(_env.InteractiveUserSid);
            if (userPrinters.Count == 0)
            {
                Console.WriteLine(" - Nenhuma conexao de rede encontrada em HKU\\<SID>.");
            }
            else
            {
                for (int i = 0; i < userPrinters.Count; i++)
                {
                    Console.WriteLine(" - " + userPrinters[i]);
                }
            }

            string summary = string.Format("{0} impressora(s) de maquina e {1} conexao(oes) do usuario.", machineCount, userPrinters.Count);
            LogAction("Listar impressoras", 0, summary);
        }

        private void KillSpoolerProcess()
        {
            _runner.Run("taskkill.exe", new string[] { "/F", "/IM", "spoolsv.exe" }, ProcessRunner.DefaultTimeoutMs, null, null, false);
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "Impressao";
            entry.ActionName = actionName;
            entry.ExitCode = exitCode;
            entry.Summary = summary;
            _logger.Log(entry);
        }
    }
}
