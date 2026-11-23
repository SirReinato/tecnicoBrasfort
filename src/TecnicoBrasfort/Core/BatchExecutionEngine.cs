using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TecnicoBrasfort.Modules;

namespace TecnicoBrasfort.Core
{
    public class BatchExecutionEngine
    {
        private readonly MaintenanceModule _maintenance;
        private readonly NetworkModule _network;
        private readonly PrintingModule _printing;
        private readonly DiagnosticsModule _diagnostics;
        private readonly DriversUpdatesModule _driversUpdates;
        private readonly ActiveDirectoryModule _activeDirectory;
        private readonly AuditModule _audit;
        private readonly IHostEnvironment _host;
        private readonly EnvironmentInfo _env;
        private readonly AuditLogger _logger;

        public BatchExecutionEngine(
            MaintenanceModule maintenance,
            NetworkModule network,
            PrintingModule printing,
            DiagnosticsModule diagnostics,
            DriversUpdatesModule driversUpdates,
            ActiveDirectoryModule activeDirectory,
            AuditModule audit,
            IHostEnvironment host,
            EnvironmentInfo env,
            AuditLogger logger)
        {
            if (maintenance == null) throw new ArgumentNullException("maintenance");
            if (network == null) throw new ArgumentNullException("network");
            if (printing == null) throw new ArgumentNullException("printing");
            if (diagnostics == null) throw new ArgumentNullException("diagnostics");
            if (driversUpdates == null) throw new ArgumentNullException("driversUpdates");
            if (activeDirectory == null) throw new ArgumentNullException("activeDirectory");
            if (audit == null) throw new ArgumentNullException("audit");
            if (host == null) throw new ArgumentNullException("host");
            if (env == null) throw new ArgumentNullException("env");
            if (logger == null) throw new ArgumentNullException("logger");

            _maintenance = maintenance;
            _network = network;
            _printing = printing;
            _diagnostics = diagnostics;
            _driversUpdates = driversUpdates;
            _activeDirectory = activeDirectory;
            _audit = audit;
            _host = host;
            _env = env;
            _logger = logger;
        }

        public BatchExecutionReport Execute(
            IList<ActionDefinition> actions,
            IExecutionObserver observer,
            CancellationToken cancellationToken)
        {
            IExecutionObserver effectiveObserver = observer ?? NullExecutionObserver.Instance;
            BatchExecutionReport report = new BatchExecutionReport();
            report.StartedAt = DateTime.Now;

            IExecutionObserver previousObserver = _logger.Observer;
            _logger.Observer = effectiveObserver;

            bool anyRebootRequired = false;

            try
            {
                int totalSteps = actions != null ? actions.Count : 0;
                for (int i = 0; i < totalSteps; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    ActionDefinition action = actions[i];
                    if (action == null)
                    {
                        continue;
                    }

                    effectiveObserver.OnStepStarted(i + 1, totalSteps, action);

                    ActionExecutionResult stepResult = ExecuteSingleSafely(action, effectiveObserver, cancellationToken);
                    report.Results.Add(stepResult);

                    if (stepResult.RebootRequired ||
                        stepResult.ExitCode == 3010 ||
                        (stepResult.Success && action.RequiresReboot))
                    {
                        anyRebootRequired = true;
                    }

                    effectiveObserver.OnStepCompleted(i + 1, totalSteps, stepResult);

                    if (stepResult.Cancelled || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            finally
            {
                report.FinishedAt = DateTime.Now;
                report.RebootRequired = anyRebootRequired || _host.HasPendingSystemReboot();
                report.TicketSummaryText = TicketSummaryBuilder.Build(_env, report);
                report.SavedSummaryPath = _logger.SaveTicketSummary(report.TicketSummaryText, report.FinishedAt);
                _logger.Observer = previousObserver;
            }

            return report;
        }

        private ActionExecutionResult ExecuteSingleSafely(
            ActionDefinition action,
            IExecutionObserver observer,
            CancellationToken cancellationToken)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                ActionExecutionResult result = DispatchAction(action, observer, cancellationToken);
                sw.Stop();
                if (result.Duration == TimeSpan.Zero)
                {
                    result.Duration = sw.Elapsed;
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                ActionExecutionResult cancelled = new ActionExecutionResult();
                cancelled.ActionId = action.Id;
                cancelled.ActionName = action.Name;
                cancelled.ExitCode = -1;
                cancelled.Success = false;
                cancelled.Cancelled = true;
                cancelled.Duration = sw.Elapsed;
                cancelled.Summary = "Execucao cancelada.";
                return cancelled;
            }
            catch (Exception ex)
            {
                sw.Stop();
                observer.OnError(string.Format("[ERRO] Falha em '{0}': {1}", action.Name, ex.Message));

                ActionExecutionResult failed = new ActionExecutionResult();
                failed.ActionId = action.Id;
                failed.ActionName = action.Name;
                failed.ExitCode = -1;
                failed.Success = false;
                failed.Cancelled = false;
                failed.Duration = sw.Elapsed;
                failed.Summary = ex.Message;
                return failed;
            }
        }

        private ActionExecutionResult DispatchAction(
            ActionDefinition action,
            IExecutionObserver observer,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            switch (action.Id)
            {
                case "maint_temp":
                    _maintenance.CleanTempFiles();
                    break;
                case "maint_winsxs":
                    _maintenance.CleanComponentStore();
                    break;
                case "maint_sfc":
                    _maintenance.RunSfc();
                    break;
                case "maint_dism":
                    _maintenance.RunDismRestoreHealth();
                    break;
                case "disk_check_ro":
                    _maintenance.CheckDiskC(false);
                    break;
                case "disk_schedule_boot":
                    _maintenance.CheckDiskC(true);
                    break;
                case "maint_disk_health":
                    _maintenance.CheckDiskHealth();
                    break;
                case "net_reset":
                    _network.ResetNetwork();
                    break;
                case "net_info":
                    _network.ShowNetworkInfo();
                    break;
                case "net_connectivity":
                    _network.RunConnectivityTests();
                    break;
                case "net_flush_dns":
                    _network.FlushDns();
                    break;
                case "net_connections":
                    _network.OpenNetworkConnections();
                    break;
                case "print_clear_queue":
                    _printing.ClearPrintQueue();
                    break;
                case "print_list":
                    _printing.ListPrinters();
                    break;
                case "diag_sysinfo":
                    _diagnostics.ShowSystemInfo();
                    break;
                case "diag_processes":
                    _diagnostics.ShowProcessMonitor(false);
                    break;
                case "diag_resmon":
                    _diagnostics.ShowProcessMonitor(true);
                    break;
                case "drv_errors":
                    _driversUpdates.ListErrorDevices();
                    break;
                case "drv_search_updates":
                    _driversUpdates.SearchPendingUpdates();
                    break;
                case "drv_backup":
                    _driversUpdates.BackupDrivers("");
                    break;
                case "drv_wu_settings":
                    _driversUpdates.OpenWindowsUpdateSettings();
                    break;
                case "ad_gpupdate":
                    _activeDirectory.ForceComputerGpUpdate();
                    break;
                case "ad_gpresult":
                    _activeDirectory.ShowComputerGpResult();
                    break;
                case "ad_diagnostics":
                    _activeDirectory.RunDomainAndExchangeDiagnostics();
                    break;
                case "audit_logs":
                    _audit.ShowHostLogs();
                    break;
                default:
                    throw new InvalidOperationException("Acao desconhecida: " + action.Id);
            }

            ActionExecutionResult ok = new ActionExecutionResult();
            ok.ActionId = action.Id;
            ok.ActionName = action.Name;
            ok.ExitCode = 0;
            ok.Success = true;
            ok.Cancelled = false;
            ok.RebootRequired = action.RequiresReboot;
            ok.Summary = "Concluido.";
            return ok;
        }
    }
}
