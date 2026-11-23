using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Threading;
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
            ClearPrintQueue(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ClearPrintQueue(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Parando servico Spooler de impressao...");
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
                            KillSpoolerProcess(cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                obs.OnError("[AVISO] Falha ao parar Spooler via ServiceController: " + ex.Message);
                KillSpoolerProcess(cancellationToken);
            }

            int removed = 0;
            bool cancelled = cancellationToken.IsCancellationRequested;
            if (!cancelled && _host.DirectoryExists(SpoolDir))
            {
                foreach (string file in _host.GetFiles(SpoolDir, "*"))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }
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
                obs.OnError("[ERRO] Nao foi possivel reiniciar o Spooler: " + ex.Message);
            }

            sw.Stop();
            if (cancelled)
            {
                exitCode = -1;
            }
            string summary = cancelled
                ? string.Format("Limpeza da fila cancelada ({0} arquivo(s) removido(s)).", removed)
                : string.Format("Fila de impressao limpa: {0} arquivo(s) removido(s) de spool\\PRINTERS.", removed);
            obs.OnOutput(summary);
            LogAction("Limpar fila de impressao travada", exitCode, summary);

            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = "print_clear_queue";
            result.ActionName = "Limpar Fila de Impressao";
            result.ExitCode = exitCode;
            result.Cancelled = cancelled;
            result.Success = !cancelled && exitCode == 0;
            result.RebootRequired = false;
            result.Duration = sw.Elapsed;
            result.Summary = summary;
            return result;
        }

        public void ListPrinters()
        {
            ListPrinters(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ListPrinters(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Impressoras instaladas na maquina (Win32_Printer):");
            int machineCount = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, PortName, Default, Shared FROM Win32_Printer"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            sw.Stop();
                            ActionExecutionResult cancelledRes = new ActionExecutionResult();
                            cancelledRes.ActionId = "print_list";
                            cancelledRes.ActionName = "Listar Impressoras Instaladas";
                            cancelledRes.ExitCode = -1;
                            cancelledRes.Cancelled = true;
                            cancelledRes.Success = false;
                            cancelledRes.Duration = sw.Elapsed;
                            cancelledRes.Summary = "Listagem cancelada.";
                            return cancelledRes;
                        }

                        machineCount++;
                        string name = obj["Name"] != null ? obj["Name"].ToString() : "Sem nome";
                        string port = obj["PortName"] != null ? obj["PortName"].ToString() : "";
                        bool isDefault = obj["Default"] != null && Convert.ToBoolean(obj["Default"]);
                        obs.OnOutput(string.Format(" - {0} (Porta: {1}){2}", name, port, isDefault ? " [PADRAO]" : ""));
                    }
                }
            }
            catch (Exception ex)
            {
                obs.OnError("Erro ao consultar Win32_Printer: " + ex.Message);
            }

            obs.OnOutput("Impressoras de rede mapeadas para o usuario interativo:");
            IList<string> userPrinters = _host.GetUserPrinterConnections(_env.InteractiveUserSid);
            if (userPrinters.Count == 0)
            {
                obs.OnOutput(" - Nenhuma conexao de rede encontrada em HKU\\<SID>.");
            }
            else
            {
                for (int i = 0; i < userPrinters.Count; i++)
                {
                    obs.OnOutput(" - " + userPrinters[i]);
                }
            }

            sw.Stop();
            string summary = string.Format("{0} impressora(s) de maquina e {1} conexao(oes) do usuario.", machineCount, userPrinters.Count);
            LogAction("Listar impressoras", 0, summary);

            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = "print_list";
            result.ActionName = "Listar Impressoras Instaladas";
            result.ExitCode = 0;
            result.Cancelled = false;
            result.Success = true;
            result.RebootRequired = false;
            result.Duration = sw.Elapsed;
            result.Summary = summary;
            return result;
        }

        private void KillSpoolerProcess(CancellationToken cancellationToken)
        {
            _runner.Run("taskkill.exe", new string[] { "/F", "/IM", "spoolsv.exe" }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
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
