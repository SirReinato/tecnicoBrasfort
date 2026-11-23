using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
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
            Console.WriteLine("Iniciando limpeza de arquivos temporarios...");
            int deleted = 0;
            int skipped = 0;

            List<string> targetDirs = new List<string>();
            targetDirs.Add(@"C:\Windows\Temp");

            if (_host.DirectoryExists(@"C:\Users"))
            {
                foreach (string userDir in _host.GetDirectories(@"C:\Users"))
                {
                    string userTemp = Path.Combine(userDir, @"AppData\Local\Temp");
                    if (_host.DirectoryExists(userTemp))
                    {
                        targetDirs.Add(userTemp);
                    }
                }
            }

            for (int i = 0; i < targetDirs.Count; i++)
            {
                string dir = targetDirs[i];
                if (!_host.DirectoryExists(dir))
                {
                    continue;
                }

                try
                {
                    foreach (string file in _host.GetFiles(dir, "*"))
                    {
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

            string summary = string.Format("Limpeza concluida: {0} arquivo(s) removido(s), {1} em uso/ignorado(s).", deleted, skipped);
            Console.WriteLine(summary);
            LogAction("Limpeza de arquivos temporarios", 0, summary);
        }

        public void CleanComponentStore()
        {
            Console.WriteLine("Executando limpeza de disco avancada (WinSxS)...");
            ProcessResult res = _runner.Run(
                "Dism.exe",
                new string[] { "/Online", "/Cleanup-Image", "/StartComponentCleanup" },
                ProcessRunner.LongOperationTimeoutMs,
                PrintLine,
                PrintError,
                false);

            Console.WriteLine(res.Summary);
            LogAction("Limpeza de disco avancada (DISM)", res.ExitCode, res.Summary);
        }

        public void RunSfc()
        {
            Console.WriteLine("Executando verificacao e reparo de arquivos do Windows (SFC /scannow)...");
            ProcessResult res = _runner.Run(
                "sfc.exe",
                new string[] { "/scannow" },
                ProcessRunner.LongOperationTimeoutMs,
                PrintLine,
                PrintError,
                true);

            Console.WriteLine(res.Summary);
            LogAction("SFC /scannow", res.ExitCode, res.Summary);
        }

        public void RunDismRestoreHealth()
        {
            Console.WriteLine("Executando reparo da imagem do Windows (DISM /RestoreHealth)...");
            ProcessResult res = _runner.Run(
                "DISM.exe",
                new string[] { "/Online", "/Cleanup-Image", "/RestoreHealth" },
                ProcessRunner.LongOperationTimeoutMs,
                PrintLine,
                PrintError,
                false);

            Console.WriteLine(res.Summary);
            LogAction("DISM /RestoreHealth", res.ExitCode, res.Summary);
        }

        public void CheckDiskC(bool scheduleBootRepair)
        {
            if (scheduleBootRepair)
            {
                Console.WriteLine("Agendando verificacao do disco C: para a proxima reinicializacao (chkntfs /c C:)...");
                ProcessResult sched = _runner.Run(
                    "chkntfs.exe",
                    new string[] { "/c", "C:" },
                    ProcessRunner.DefaultTimeoutMs,
                    PrintLine,
                    PrintError,
                    false);

                string summary = sched.ExitCode == 0
                    ? "Verificacao de disco C: agendada para o proximo boot."
                    : sched.Summary;
                Console.WriteLine(summary);
                LogAction("Agendar verificacao disco C:", sched.ExitCode, summary);
                return;
            }

            Console.WriteLine("Executando verificacao somente leitura do disco C: (chkdsk C:)...");
            ProcessResult res = _runner.Run(
                "chkdsk.exe",
                new string[] { "C:" },
                ProcessRunner.LongOperationTimeoutMs,
                PrintLine,
                PrintError,
                false);

            Console.WriteLine(res.Summary);
            LogAction("Verificar disco C: (somente leitura)", res.ExitCode, res.Summary);
        }

        public void CheckDiskHealth()
        {
            Console.WriteLine("Consultando saude dos discos (WMI / SMART)...");
            int driveCount = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model, Size, Status FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        driveCount++;
                        string model = obj["Model"] != null ? obj["Model"].ToString() : "Desconhecido";
                        string status = obj["Status"] != null ? obj["Status"].ToString() : "N/A";
                        long sizeBytes = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : 0;
                        double sizeGb = sizeBytes / (1024.0 * 1024.0 * 1024.0);
                        Console.WriteLine(string.Format(" - Disco: {0} | {1:0.0} GB | Status: {2}", model, sizeGb, status));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao consultar Win32_DiskDrive: " + ex.Message);
            }

            bool smartExposed = false;
            try
            {
                using (ManagementObjectSearcher smartSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, PredictFailure, Reason FROM MSStorageDriver_FailurePredictStatus"))
                {
                    foreach (ManagementObject obj in smartSearcher.Get())
                    {
                        smartExposed = true;
                        string name = obj["InstanceName"] != null ? obj["InstanceName"].ToString() : "Disco";
                        bool predict = obj["PredictFailure"] != null && Convert.ToBoolean(obj["PredictFailure"]);
                        Console.WriteLine(string.Format(" - SMART [{0}]: {1}", name, predict ? "ALERTA DE FALHA PREVISTA" : "OK"));
                    }
                }
            }
            catch (Exception)
            {
            }

            if (!smartExposed)
            {
                Console.WriteLine("[AVISO] Telemetria SMART nao exposta via WMI (comum em controladoras RAID ou adaptadores USB).");
            }

            string summary = string.Format("{0} disco(s) verificado(s); SMART WMI={1}.", driveCount, smartExposed ? "Disponivel" : "Indisponivel");
            LogAction("Saude do disco (HD/SSD)", 0, summary);
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

        private static void PrintLine(string line)
        {
            Console.WriteLine(line);
        }

        private static void PrintError(string line)
        {
            Console.WriteLine("[STDERR] " + line);
        }
    }
}
