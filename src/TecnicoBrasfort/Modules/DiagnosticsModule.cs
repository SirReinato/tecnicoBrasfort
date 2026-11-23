using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class DiagnosticsModule
    {
        private readonly IProcessRunner _runner;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;

        public DiagnosticsModule(IProcessRunner runner, AuditLogger logger, EnvironmentInfo env)
        {
            _runner = runner;
            _logger = logger;
            _env = env;
        }

        public void ShowSystemInfo()
        {
            ShowSystemInfo(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ShowSystemInfo(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Coletando informacoes do sistema...");

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return BuildCancelled("diag_sysinfo", "Resumo do Sistema (SO, Hardware, Uptime)", sw.Elapsed);
                }

                using (ManagementObjectSearcher os = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in os.Get())
                    {
                        obs.OnOutput(string.Format(" - SO: {0} | Versao: {1} | Arq: {2}",
                            obj["Caption"], obj["Version"], obj["OSArchitecture"]));
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return BuildCancelled("diag_sysinfo", "Resumo do Sistema (SO, Hardware, Uptime)", sw.Elapsed);
                }

                using (ManagementObjectSearcher cs = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory, Domain FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in cs.Get())
                    {
                        long memBytes = obj["TotalPhysicalMemory"] != null ? Convert.ToInt64(obj["TotalPhysicalMemory"]) : 0;
                        double memGb = memBytes / (1024.0 * 1024.0 * 1024.0);
                        obs.OnOutput(string.Format(" - Hardware: {0} {1} | RAM: {2:0.0} GB | Dominio: {3}",
                            obj["Manufacturer"], obj["Model"], memGb, obj["Domain"]));
                    }
                }

                using (ManagementObjectSearcher bios = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in bios.Get())
                    {
                        obs.OnOutput(string.Format(" - Serial (BIOS): {0}", obj["SerialNumber"]));
                    }
                }

                using (ManagementObjectSearcher disks = new ManagementObjectSearcher("SELECT DeviceID, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3"))
                {
                    foreach (ManagementObject obj in disks.Get())
                    {
                        long free = obj["FreeSpace"] != null ? Convert.ToInt64(obj["FreeSpace"]) : 0;
                        long total = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : 0;
                        obs.OnOutput(string.Format(" - Volume {0} Livre: {1:0.0} GB / {2:0.0} GB",
                            obj["DeviceID"],
                            free / (1024.0 * 1024.0 * 1024.0),
                            total / (1024.0 * 1024.0 * 1024.0)));
                    }
                }
            }
            catch (Exception ex)
            {
                obs.OnError("Erro ao coletar informacoes WMI: " + ex.Message);
            }

            sw.Stop();
            LogAction("Informacoes do sistema", 0, "Inventario WMI exibido.");

            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = "diag_sysinfo";
            result.ActionName = "Resumo do Sistema (SO, Hardware, Uptime)";
            result.ExitCode = 0;
            result.Success = true;
            result.Duration = sw.Elapsed;
            result.Summary = "Inventario WMI exibido.";
            return result;
        }

        public void ShowProcessMonitor(bool openResourceMonitor)
        {
            ShowProcessMonitor(openResourceMonitor, NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ShowProcessMonitor(bool openResourceMonitor, IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildCancelled(
                    openResourceMonitor ? "diag_resmon" : "diag_processes",
                    openResourceMonitor ? "Abrir Monitor de Recursos (resmon)" : "Top 15 Processos (Memoria)",
                    sw.Elapsed);
            }

            obs.OnOutput("Top 15 processos por consumo de memoria (WorkingSet64):");

            Process[] all = Process.GetProcesses();
            List<Process> list = new List<Process>(all);
            list.Sort(delegate(Process a, Process b)
            {
                long memA = SafeGetWorkingSet(a);
                long memB = SafeGetWorkingSet(b);
                return memB.CompareTo(memA);
            });

            int limit = list.Count < 15 ? list.Count : 15;
            for (int i = 0; i < limit; i++)
            {
                Process p = list[i];
                double mb = SafeGetWorkingSet(p) / (1024.0 * 1024.0);
                obs.OnOutput(string.Format(" {0,2}. {1,-28} PID={2,-6} RAM={3,7:0.0} MB", i + 1, p.ProcessName, p.Id, mb));
            }

            int exitCode = 0;
            bool cancelled = false;
            if (openResourceMonitor)
            {
                obs.OnOutput("Abrindo Monitor de Recursos (resmon.exe)...");
                ProcessResult res = _runner.Run(
                    "cmd.exe",
                    new string[] { "/c", "start", "", "resmon.exe" },
                    ProcessRunner.DefaultTimeoutMs,
                    null,
                    null,
                    false,
                    cancellationToken);
                exitCode = res.ExitCode;
                cancelled = res.Cancelled;
            }

            sw.Stop();
            string summary = string.Format("Top {0} processos exibidos.", limit);
            LogAction("Monitor de recursos e processos", exitCode, summary);

            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = openResourceMonitor ? "diag_resmon" : "diag_processes";
            result.ActionName = openResourceMonitor ? "Abrir Monitor de Recursos (resmon)" : "Top 15 Processos (Memoria)";
            result.ExitCode = exitCode;
            result.Cancelled = cancelled;
            result.Success = !cancelled && exitCode == 0;
            result.Duration = sw.Elapsed;
            result.Summary = summary;
            return result;
        }

        private static long SafeGetWorkingSet(Process p)
        {
            try
            {
                return p.WorkingSet64;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "Diagnostico";
            entry.ActionName = actionName;
            entry.ExitCode = exitCode;
            entry.Summary = summary;
            _logger.Log(entry);
        }

        private static ActionExecutionResult BuildCancelled(string actionId, string actionName, TimeSpan duration)
        {
            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = actionId;
            result.ActionName = actionName;
            result.ExitCode = -1;
            result.Cancelled = true;
            result.Success = false;
            result.Duration = duration;
            result.Summary = "Execucao cancelada pelo tecnico.";
            return result;
        }
    }
}
