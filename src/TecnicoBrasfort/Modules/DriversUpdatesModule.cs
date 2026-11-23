using System;
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
