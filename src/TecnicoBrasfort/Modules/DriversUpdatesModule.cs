using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class DriversUpdatesModule
    {
        public const long MinimumFreeSpaceBytes = 5L * 1024L * 1024L * 1024L;
        public const int UpdateSearchTimeoutMs = 120000;

        private readonly IProcessRunner _runner;
        private readonly IHostEnvironment _host;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;

        public DriversUpdatesModule(IProcessRunner runner, IHostEnvironment host, AuditLogger logger, EnvironmentInfo env)
        {
            _runner = runner;
            _host = host;
            _logger = logger;
            _env = env;
        }

        public void ListErrorDevices()
        {
            ListErrorDevices(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ListErrorDevices(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Consultando dispositivos com erro (ConfigManagerErrorCode <> 0)...");
            int count = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            sw.Stop();
                            return BuildResult("drv_errors", "Dispositivos com Erro no Gerenciador", -1, true, sw.Elapsed, "Consulta cancelada.");
                        }

                        count++;
                        obs.OnOutput(string.Format(" - {0} | Codigo={1} | ID={2}",
                            obj["Name"], obj["ConfigManagerErrorCode"], obj["DeviceID"]));
                    }
                }
            }
            catch (Exception ex)
            {
                obs.OnError("Erro ao consultar dispositivos PnP: " + ex.Message);
            }

            if (count == 0)
            {
                obs.OnOutput("Nenhum dispositivo com erro reportado.");
            }

            sw.Stop();
            string summary = string.Format("{0} dispositivo(s) com erro.", count);
            LogAction("Dispositivos com erro", 0, summary);
            return BuildResult("drv_errors", "Dispositivos com Erro no Gerenciador", 0, false, sw.Elapsed, summary);
        }

        public void BackupDrivers(string customDestination)
        {
            BackupDrivers(customDestination, NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult BackupDrivers(string customDestination, IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();

            string safeHost = AuditLogger.SanitizeFileSegment(_env.Hostname, "PC");
            string destination = string.IsNullOrEmpty(customDestination)
                ? Path.Combine(@"C:\Temp", "DriverBackup_" + safeHost)
                : customDestination.Trim();

            obs.OnOutput(string.Format("Validando espaco livre para backup em '{0}' (minimo 5 GB)...", destination));

            try
            {
                long freeBytes = _host.GetAvailableFreeSpaceBytes(destination);
                if (freeBytes < MinimumFreeSpaceBytes)
                {
                    double freeGb = freeBytes / (1024.0 * 1024.0 * 1024.0);
                    string msg = string.Format("Espaco insuficiente em '{0}': {1:0.00} GB livres (minimo exigido: 5.00 GB).", destination, freeGb);
                    obs.OnError("[ERRO] " + msg);
                    LogAction("Backup dos drivers", 1, msg);
                    sw.Stop();
                    return BuildResult("drv_backup", "Backup de Drivers (pnputil)", 1, false, sw.Elapsed, msg);
                }
            }
            catch (Exception ex)
            {
                string errMsg = "Nao foi possivel verificar espaco livre no destino: " + ex.Message;
                obs.OnError("[ERRO] " + errMsg);
                LogAction("Backup dos drivers", 1, errMsg);
                sw.Stop();
                return BuildResult("drv_backup", "Backup de Drivers (pnputil)", 1, false, sw.Elapsed, errMsg);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildResult("drv_backup", "Backup de Drivers (pnputil)", -1, true, sw.Elapsed, "Backup cancelado.");
            }

            _host.CreateDirectory(destination);
            obs.OnOutput("Exportando drivers com pnputil...");
            ProcessResult res = _runner.Run(
                "pnputil.exe",
                new string[] { "/export-driver", "*", destination },
                ProcessRunner.LongOperationTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            sw.Stop();
            obs.OnOutput(res.Summary);
            LogAction("Backup dos drivers", res.ExitCode, string.Format("Destino: {0} | {1}", destination, res.Summary));
            return BuildResult("drv_backup", "Backup de Drivers (pnputil)", res.ExitCode, res.Cancelled, res.Duration, res.Summary);
        }

        public void SearchPendingUpdates()
        {
            SearchPendingUpdates(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult SearchPendingUpdates(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Buscando atualizacoes pendentes via Windows Update COM (timeout 120s)...");

            Task<string> searchTask = Task<string>.Factory.StartNew(delegate()
            {
                return ExecuteComUpdateSearch(obs);
            });

            int elapsedSec = 0;
            while (!searchTask.Wait(2000))
            {
                elapsedSec += 2;
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    string cancelMsg = "Consulta ao Windows Update cancelada pelo tecnico.";
                    obs.OnOutput(cancelMsg);
                    LogAction("Atualizacoes / Drivers (Windows Update)", -1, cancelMsg);
                    return BuildResult("drv_search_updates", "Buscar Atualizacoes Pendentes (Windows Update)", -1, true, sw.Elapsed, cancelMsg);
                }

                obs.OnOutput(string.Format("Aguardando resposta do Windows Update... {0}s", elapsedSec));
                if (elapsedSec * 1000 >= UpdateSearchTimeoutMs)
                {
                    sw.Stop();
                    string timeoutMsg = "Tempo limite excedido (120s) na consulta ao Windows Update / WSUS.";
                    obs.OnError("[AVISO] " + timeoutMsg);
                    LogAction("Atualizacoes / Drivers (Windows Update)", -1, timeoutMsg);
                    return BuildResult("drv_search_updates", "Buscar Atualizacoes Pendentes (Windows Update)", -1, false, sw.Elapsed, timeoutMsg);
                }
            }

            sw.Stop();
            string resultSummary = searchTask.Result;
            obs.OnOutput(resultSummary);
            LogAction("Atualizacoes / Drivers (Windows Update)", 0, resultSummary);
            return BuildResult("drv_search_updates", "Buscar Atualizacoes Pendentes (Windows Update)", 0, false, sw.Elapsed, resultSummary);
        }

        public void OpenWindowsUpdateSettings()
        {
            OpenWindowsUpdateSettings(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult OpenWindowsUpdateSettings(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Abrindo configuracoes do Windows Update...");
            ProcessResult res = _runner.Run(
                "cmd.exe",
                new string[] { "/c", "start", "", "ms-settings:windowsupdate" },
                ProcessRunner.DefaultTimeoutMs,
                null,
                null,
                false,
                cancellationToken);
            LogAction("Procurar atualizacoes do Windows", res.ExitCode, "Tela do Windows Update aberta.");
            return BuildResult("drv_wu_settings", "Abrir Configuracoes do Windows Update", res.ExitCode, res.Cancelled, res.Duration, "Tela do Windows Update aberta.");
        }

        public ActionExecutionResult RepairWindowsUpdateAndStore(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildResult("drv_wu_repair", "Reparar Windows Update, Store e Perfis de Servico", -1, true, sw.Elapsed, "Execucao cancelada.");
            }

            // 1. Verificar e restaurar perfis de servico nativos (S-1-5-18, S-1-5-19, S-1-5-20)
            obs.OnOutput("1/5 Verificando perfis de servico nativos (S-1-5-18, S-1-5-19, S-1-5-20) em ProfileList...");
            IList<string> restoredSids = _host.EnsureNativeServiceProfilesPresent();
            EnsureProfileDirectory(@"C:\Windows\System32\config\systemprofile");
            EnsureProfileDirectory(@"C:\Windows\ServiceProfiles\LocalService");
            EnsureProfileDirectory(@"C:\Windows\ServiceProfiles\NetworkService");

            if (restoredSids.Count > 0)
            {
                obs.OnOutput("Perfis restaurados no Registro: " + string.Join(", ", new List<string>(restoredSids).ToArray()));
            }
            else
            {
                obs.OnOutput("Perfis nativos S-1-5-18/19/20 ja estavam presentes em ProfileList.");
            }

            // 2. Parar servicos dependentes antes de limpar caches
            string[] coreServices = new string[] { "wuauserv", "bits", "DoSvc", "CDPSvc", "UsoSvc", "InstallService" };
            obs.OnOutput("2/5 Parando servicos de atualizacao e Store...");
            for (int i = 0; i < coreServices.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return BuildResult("drv_wu_repair", "Reparar Windows Update, Store e Perfis de Servico", -1, true, sw.Elapsed, "Execucao cancelada.");
                }
                _runner.Run("sc.exe", new string[] { "stop", coreServices[i] }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
            }

            // 3. Backup e limpeza dos caches de download
            obs.OnOutput("3/5 Fazendo backup e limpando caches de DoSvc e SoftwareDistribution\\Download...");
            int cachesCleaned = 0;
            if (BackupAndRecreateCache(@"C:\Windows\SoftwareDistribution\Download", stamp, obs)) cachesCleaned++;
            if (BackupAndRecreateCache(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache", stamp, obs)) cachesCleaned++;
            if (BackupAndRecreateCache(@"C:\ProgramData\Microsoft\Network\Downloader", stamp, obs)) cachesCleaned++;

            // 4. Iniciar e validar os 6 servicos principais
            string[] startOrder = new string[] { "DoSvc", "CDPSvc", "bits", "wuauserv", "UsoSvc", "InstallService" };
            List<string> failedServices = new List<string>();
            obs.OnOutput("4/5 Iniciando e verificando servicos (DoSvc, CDPSvc, bits, wuauserv, UsoSvc, InstallService)...");

            for (int i = 0; i < startOrder.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return BuildResult("drv_wu_repair", "Reparar Windows Update, Store e Perfis de Servico", -1, true, sw.Elapsed, "Execucao cancelada.");
                }

                string svc = startOrder[i];
                _runner.Run("sc.exe", new string[] { "start", svc }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
                if (IsServiceRunning(svc, cancellationToken))
                {
                    obs.OnOutput(string.Format(" - [OK] {0}: RUNNING", svc));
                }
                else
                {
                    failedServices.Add(svc);
                    obs.OnError(string.Format(" - [FALHA] {0}: nao atingiu estado RUNNING", svc));
                }
            }

            // 5. Recuperacao condicional do WSearch (Windows.db)
            obs.OnOutput("5/5 Verificando Windows Search (WSearch)...");
            string wsearchStatus = RecoverWSearchConditionally(stamp, obs, cancellationToken);

            sw.Stop();
            int exitCode = failedServices.Count == 0 ? 0 : 1;
            string summary = string.Format(
                "Perfis restaurados: {0} | Caches limpos: {1} | Servicos OK: {2}/{3} | {4}",
                restoredSids.Count,
                cachesCleaned,
                startOrder.Length - failedServices.Count,
                startOrder.Length,
                wsearchStatus);
            if (failedServices.Count > 0)
            {
                summary += " | Falhas: " + string.Join(", ", failedServices.ToArray());
            }

            obs.OnOutput(summary);
            LogAction("Reparar Windows Update, Store e Perfis de Servico", exitCode, summary);
            return BuildResult("drv_wu_repair", "Reparar Windows Update, Store e Perfis de Servico", exitCode, false, sw.Elapsed, summary);
        }

        private void EnsureProfileDirectory(string path)
        {
            if (!_host.DirectoryExists(path))
            {
                _host.CreateDirectory(path);
            }
        }

        private bool BackupAndRecreateCache(string path, string stamp, IExecutionObserver obs)
        {
            if (!_host.DirectoryExists(path))
            {
                return false;
            }

            string backupPath = path + ".bak_" + stamp;
            if (_host.TryMoveDirectory(path, backupPath))
            {
                _host.CreateDirectory(path);
                obs.OnOutput(string.Format(" - Cache movido para backup: {0}", backupPath));
                return true;
            }

            obs.OnOutput(string.Format(" - Aviso: nao foi possivel mover '{0}' (arquivos em uso).", path));
            return false;
        }

        private bool IsServiceRunning(string serviceName, CancellationToken cancellationToken)
        {
            ProcessResult res = _runner.Run(
                "sc.exe",
                new string[] { "query", serviceName },
                ProcessRunner.DefaultTimeoutMs,
                null,
                null,
                false,
                cancellationToken);

            string output = res != null && res.StandardOutput != null ? res.StandardOutput : "";
            return output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string RecoverWSearchConditionally(string stamp, IExecutionObserver obs, CancellationToken cancellationToken)
        {
            ProcessResult queryRes = _runner.Run(
                "sc.exe",
                new string[] { "query", "WSearch" },
                ProcessRunner.DefaultTimeoutMs,
                null,
                null,
                false,
                cancellationToken);

            string initialOutput = queryRes != null && queryRes.StandardOutput != null ? queryRes.StandardOutput : "";
            if (queryRes != null && queryRes.ExitCode != 0 && string.IsNullOrEmpty(initialOutput))
            {
                obs.OnOutput(" - WSearch nao encontrado/indisponivel neste sistema.");
                return "WSearch: indisponivel";
            }

            if (initialOutput.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                obs.OnOutput(" - [OK] WSearch ja esta RUNNING; indice Windows.db preservado.");
                return "WSearch: OK (indice preservado)";
            }

            _runner.Run("sc.exe", new string[] { "start", "WSearch" }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
            if (IsServiceRunning("WSearch", cancellationToken))
            {
                obs.OnOutput(" - [OK] WSearch iniciado normalmente; indice Windows.db preservado.");
                return "WSearch: iniciado (indice preservado)";
            }

            const string windowsDbPath = @"C:\ProgramData\Microsoft\Search\Data\Applications\Windows\Windows.db";
            if (_host.FileExists(windowsDbPath))
            {
                _runner.Run("sc.exe", new string[] { "stop", "WSearch" }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
                string dbBackupPath = windowsDbPath + ".bak_" + stamp;
                if (_host.TryMoveFile(windowsDbPath, dbBackupPath))
                {
                    obs.OnOutput(" - Banco corrompido movido para backup: " + dbBackupPath);
                    _runner.Run("sc.exe", new string[] { "start", "WSearch" }, ProcessRunner.DefaultTimeoutMs, null, null, false, cancellationToken);
                    bool recovered = IsServiceRunning("WSearch", cancellationToken);
                    obs.OnOutput(recovered
                        ? " - [OK] WSearch iniciado apos recriacao do indice."
                        : " - [AVISO] WSearch ainda falhou ao iniciar apos backup do Windows.db.");
                    return recovered ? "WSearch: recuperado (Windows.db recriado)" : "WSearch: Windows.db movido, aguardando reboot";
                }
            }

            obs.OnOutput(" - [AVISO] WSearch parado e sem Windows.db acessivel para backup.");
            return "WSearch: parado";
        }

        private static string ExecuteComUpdateSearch(IExecutionObserver observer)
        {
            try
            {
                Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null)
                {
                    return "Objeto COM Microsoft.Update.Session indisponivel neste sistema.";
                }

                object session = Activator.CreateInstance(sessionType);
                object searcher = sessionType.InvokeMember("CreateUpdateSearcher", BindingFlags.InvokeMethod, null, session, null);
                object searchResult = searcher.GetType().InvokeMember("Search", BindingFlags.InvokeMethod, null, searcher, new object[] { "IsInstalled=0" });
                object updates = searchResult.GetType().InvokeMember("Updates", BindingFlags.GetProperty, null, searchResult, null);
                int count = Convert.ToInt32(updates.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, updates, null));

                if (count == 0)
                {
                    return "Nenhuma atualizacao pendente encontrada.";
                }

                int maxToShow = count < 15 ? count : 15;
                for (int i = 0; i < maxToShow; i++)
                {
                    object item = updates.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, updates, new object[] { i });
                    string title = Convert.ToString(item.GetType().InvokeMember("Title", BindingFlags.GetProperty, null, item, null));
                    observer.OnOutput(string.Format(" - {0}", title));
                }

                return string.Format("{0} atualizacao(oes) pendente(s) encontrada(s).", count);
            }
            catch (TargetInvocationException tie)
            {
                COMException comEx = tie.InnerException as COMException;
                if (comEx != null)
                {
                    return TranslateComError(comEx);
                }
                return "Erro na consulta Windows Update: " + (tie.InnerException != null ? tie.InnerException.Message : tie.Message);
            }
            catch (COMException comEx)
            {
                return TranslateComError(comEx);
            }
            catch (Exception ex)
            {
                return "Erro na consulta Windows Update: " + ex.Message;
            }
        }

        private static string TranslateComError(COMException ex)
        {
            uint code = (uint)ex.ErrorCode;
            if (code == 0x8024402C || code == 0x80072EE7)
            {
                return string.Format("Falha de rede/DNS ao alcancar servidor Windows Update/WSUS (0x{0:X8}).", code);
            }
            if (code == 0x80240438)
            {
                return string.Format("Proxy ou servidor WSUS inacessivel (0x{0:X8}).", code);
            }
            return string.Format("Erro COM do Windows Update (0x{0:X8}): {1}", code, ex.Message);
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "DriversEAtualizacoes";
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
            TimeSpan duration,
            string summary)
        {
            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = actionId;
            result.ActionName = actionName;
            result.ExitCode = exitCode;
            result.Cancelled = cancelled;
            result.Success = !cancelled && (exitCode == 0 || exitCode == 3010);
            result.RebootRequired = exitCode == 3010;
            result.Duration = duration;
            result.Summary = summary;
            return result;
        }
    }
}
