using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class MaintenanceModule
    {
        private readonly IProcessRunner _runner;
        private readonly IHostEnvironment _host;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;

        public MaintenanceModule(IProcessRunner runner, IHostEnvironment host, AuditLogger logger, EnvironmentInfo env)
        {
            _runner = runner;
            _host = host;
            _logger = logger;
            _env = env;
        }

        public void CleanTempFiles()
        {
            CleanTempFiles(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult CleanTempFiles(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Iniciando limpeza de arquivos temporarios...");

            int deleted = 0;
            int skipped = 0;
            bool cancelled = false;

            List<string> targetDirs = new List<string>();
            targetDirs.Add(@"C:\Windows\Temp");

            if (_host.DirectoryExists(@"C:\Users"))
            {
                foreach (string userDir in _host.GetDirectories(@"C:\Users"))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    string userTemp = Path.Combine(userDir, @"AppData\Local\Temp");
                    if (_host.DirectoryExists(userTemp))
                    {
                        targetDirs.Add(userTemp);
                    }
                }
            }

            for (int i = 0; i < targetDirs.Count && !cancelled; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                string dir = targetDirs[i];
                if (!_host.DirectoryExists(dir))
                {
                    continue;
                }

                try
                {
                    foreach (string file in _host.GetFiles(dir, "*"))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancelled = true;
                            break;
                        }

                        if (_host.TryDeleteFile(file))
                        {
                            deleted++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                catch (Exception)
                {
                    skipped++;
                }
            }

            sw.Stop();
            string summary = cancelled
                ? string.Format("Limpeza cancelada: {0} arquivo(s) removido(s), {1} ignorado(s).", deleted, skipped)
                : string.Format("Limpeza concluida: {0} arquivo(s) removido(s), {1} em uso/ignorado(s).", deleted, skipped);
            obs.OnOutput(summary);

            int exitCode = cancelled ? -1 : 0;
            LogAction("Limpeza de arquivos temporarios", exitCode, summary);
            return BuildResult("maint_temp", "Limpeza de Temporarios", exitCode, cancelled, false, sw.Elapsed, summary);
        }

        public void CleanComponentStore()
        {
            CleanComponentStore(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult CleanComponentStore(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Executando limpeza de disco avancada (WinSxS)...");

            ProcessResult res = _runner.Run(
                "Dism.exe",
                new string[] { "/Online", "/Cleanup-Image", "/StartComponentCleanup" },
                ProcessRunner.LongOperationTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            obs.OnOutput(res.Summary);
            LogAction("Limpeza de disco avancada (DISM)", res.ExitCode, res.Summary);
            return BuildResult("maint_winsxs", "Limpeza de Componentes WinSxS", res.ExitCode, res.Cancelled, res.ExitCode == 3010, res.Duration, res.Summary);
        }

        public void RunSfc()
        {
            RunSfc(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult RunSfc(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Executando verificacao e reparo de arquivos do Windows (SFC /scannow)...");

            ProcessResult res = _runner.Run(
                "sfc.exe",
                new string[] { "/scannow" },
                ProcessRunner.LongOperationTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                true,
                cancellationToken);

            obs.OnOutput(res.Summary);
            LogAction("SFC /scannow", res.ExitCode, res.Summary);
            return BuildResult("maint_sfc", "Verificacao de Arquivos (SFC)", res.ExitCode, res.Cancelled, res.ExitCode == 3010, res.Duration, res.Summary);
        }

        public void RunDismRestoreHealth()
        {
            RunDismRestoreHealth(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult RunDismRestoreHealth(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Executando reparo da imagem do Windows (DISM /RestoreHealth)...");

            ProcessResult res = _runner.Run(
                "DISM.exe",
                new string[] { "/Online", "/Cleanup-Image", "/RestoreHealth" },
                ProcessRunner.LongOperationTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            obs.OnOutput(res.Summary);
            LogAction("DISM /RestoreHealth", res.ExitCode, res.Summary);
            return BuildResult("maint_dism", "Reparo de Imagem (DISM RestoreHealth)", res.ExitCode, res.Cancelled, res.ExitCode == 3010, res.Duration, res.Summary);
        }

        public void CheckDiskC(bool scheduleBootRepair)
        {
            CheckDiskC(scheduleBootRepair, NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult CheckDiskC(bool scheduleBootRepair, IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;

            if (scheduleBootRepair)
            {
                obs.OnOutput("Agendando verificacao do disco C: para a proxima reinicializacao (chkntfs /c C:)...");
                ProcessResult sched = _runner.Run(
                    "chkntfs.exe",
                    new string[] { "/c", "C:" },
                    ProcessRunner.DefaultTimeoutMs,
                    CreateOutputCallback(obs),
                    CreateErrorCallback(obs),
                    false,
                    cancellationToken);

                string summary = !sched.Cancelled && sched.ExitCode == 0
                    ? "Verificacao de disco C: agendada para o proximo boot."
                    : sched.Summary;
                obs.OnOutput(summary);
                LogAction("Agendar verificacao disco C:", sched.ExitCode, summary);

                bool rebootNeeded = !sched.Cancelled && (sched.ExitCode == 0 || sched.ExitCode == 3010);
                return BuildResult("disk_schedule_boot", "Agendar Reparo de Disco no Proximo Boot", sched.ExitCode, sched.Cancelled, rebootNeeded, sched.Duration, summary);
            }

            obs.OnOutput("Executando verificacao somente leitura do disco C: (chkdsk C:)...");
            ProcessResult res = _runner.Run(
                "chkdsk.exe",
                new string[] { "C:" },
                ProcessRunner.LongOperationTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            obs.OnOutput(res.Summary);
            LogAction("Verificar disco C: (somente leitura)", res.ExitCode, res.Summary);
            return BuildResult("disk_check_ro", "Verificar Disco C: (Somente Leitura)", res.ExitCode, res.Cancelled, res.ExitCode == 3010, res.Duration, res.Summary);
        }

        public void CheckDiskHealth()
        {
            CheckDiskHealth(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult CheckDiskHealth(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Consultando saude dos discos (WMI / SMART)...");
            int driveCount = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model, Size, Status FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            sw.Stop();
                            return BuildResult("maint_disk_health", "Saude Fisica dos Discos (SMART)", -1, true, false, sw.Elapsed, "Consulta cancelada.");
                        }

                        driveCount++;
                        string model = obj["Model"] != null ? obj["Model"].ToString() : "Desconhecido";
                        string status = obj["Status"] != null ? obj["Status"].ToString() : "N/A";
                        long sizeBytes = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : 0;
                        double sizeGb = sizeBytes / (1024.0 * 1024.0 * 1024.0);
                        obs.OnOutput(string.Format(" - Disco: {0} | {1:0.0} GB | Status: {2}", model, sizeGb, status));
                    }
                }
            }
            catch (Exception ex)
            {
                obs.OnError("Erro ao consultar Win32_DiskDrive: " + ex.Message);
            }

            bool smartExposed = false;
            try
            {
                using (ManagementObjectSearcher smartSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, PredictFailure, Reason FROM MSStorageDriver_FailurePredictStatus"))
                {
                    foreach (ManagementObject obj in smartSearcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            sw.Stop();
                            return BuildResult("maint_disk_health", "Saude Fisica dos Discos (SMART)", -1, true, false, sw.Elapsed, "Consulta cancelada.");
                        }

                        smartExposed = true;
                        string name = obj["InstanceName"] != null ? obj["InstanceName"].ToString() : "Disco";
                        bool predict = obj["PredictFailure"] != null && Convert.ToBoolean(obj["PredictFailure"]);
                        obs.OnOutput(string.Format(" - SMART [{0}]: {1}", name, predict ? "ALERTA DE FALHA PREVISTA" : "OK"));
                    }
                }
            }
            catch (Exception)
            {
            }

            if (!smartExposed)
            {
                obs.OnOutput("[AVISO] Telemetria SMART nao exposta via WMI (comum em controladoras RAID ou adaptadores USB).");
            }

            sw.Stop();
            string summary = string.Format("{0} disco(s) verificado(s); SMART WMI={1}.", driveCount, smartExposed ? "Disponivel" : "Indisponivel");
            LogAction("Saude do disco (HD/SSD)", 0, summary);
            return BuildResult("maint_disk_health", "Saude Fisica dos Discos (SMART)", 0, false, false, sw.Elapsed, summary);
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "Manutencao";
            entry.ActionName = actionName;
            entry.ExitCode = exitCode;
            entry.Summary = summary;
            _logger.Log(entry);
        }

        private static Action<string> CreateOutputCallback(IExecutionObserver observer)
        {
            return delegate(string line) { observer.OnOutput(line); };
        }

        private static Action<string> CreateErrorCallback(IExecutionObserver observer)
        {
            return delegate(string line) { observer.OnError("[STDERR] " + line); };
        }

        private static ActionExecutionResult BuildResult(
            string actionId,
            string actionName,
            int exitCode,
            bool cancelled,
            bool rebootRequired,
            TimeSpan duration,
            string summary)
        {
            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = actionId;
            result.ActionName = actionName;
            result.ExitCode = exitCode;
            result.Cancelled = cancelled;
            result.Success = !cancelled && (exitCode == 0 || exitCode == 3010);
            result.RebootRequired = rebootRequired;
            result.Duration = duration;
            result.Summary = summary;
            return result;
        }
    }
}
