using System;
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
            Console.WriteLine("Consultando dispositivos com erro (ConfigManagerErrorCode <> 0)...");
            int count = 0;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        count++;
                        Console.WriteLine(string.Format(" - {0} | Codigo={1} | ID={2}",
                            obj["Name"], obj["ConfigManagerErrorCode"], obj["DeviceID"]));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao consultar dispositivos PnP: " + ex.Message);
            }

            if (count == 0)
            {
                Console.WriteLine("Nenhum dispositivo com erro reportado.");
            }

            LogAction("Dispositivos com erro", 0, string.Format("{0} dispositivo(s) com erro.", count));
        }

        public void BackupDrivers(string customDestination)
        {
            string safeHost = AuditLogger.SanitizeFileSegment(_env.Hostname, "PC");
            string destination = string.IsNullOrEmpty(customDestination)
                ? Path.Combine(@"C:\Temp", "DriverBackup_" + safeHost)
                : customDestination.Trim();

            Console.WriteLine(string.Format("Validando espaco livre para backup em '{0}' (minimo 5 GB)...", destination));

            try
            {
                long freeBytes = _host.GetAvailableFreeSpaceBytes(destination);
                if (freeBytes < MinimumFreeSpaceBytes)
                {
                    double freeGb = freeBytes / (1024.0 * 1024.0 * 1024.0);
                    string msg = string.Format("Espaco insuficiente em '{0}': {1:0.00} GB livres (minimo exigido: 5.00 GB).", destination, freeGb);
                    Console.WriteLine("[ERRO] " + msg);
                    LogAction("Backup dos drivers", 1, msg);
                    return;
                }
            }
            catch (Exception ex)
            {
                string errMsg = "Nao foi possivel verificar espaco livre no destino: " + ex.Message;
                Console.WriteLine("[ERRO] " + errMsg);
                LogAction("Backup dos drivers", 1, errMsg);
                return;
            }

            _host.CreateDirectory(destination);
            Console.WriteLine("Exportando drivers com pnputil...");
            ProcessResult res = _runner.Run(
                "pnputil.exe",
                new string[] { "/export-driver", "*", destination },
                ProcessRunner.LongOperationTimeoutMs,
                PrintLine,
                PrintError,
                false);

            Console.WriteLine(res.Summary);
            LogAction("Backup dos drivers", res.ExitCode, string.Format("Destino: {0} | {1}", destination, res.Summary));
        }

        public void SearchPendingUpdates()
        {
            Console.WriteLine("Buscando atualizacoes pendentes via Windows Update COM (timeout 120s)...");

            Task<string> searchTask = Task<string>.Factory.StartNew(delegate()
            {
                return ExecuteComUpdateSearch();
            });

            int elapsedSec = 0;
            while (!searchTask.Wait(2000))
            {
                elapsedSec += 2;
                Console.Write(string.Format("\rAguardando resposta do Windows Update... {0}s ", elapsedSec));
                if (elapsedSec * 1000 >= UpdateSearchTimeoutMs)
                {
                    Console.WriteLine();
                    string timeoutMsg = "Tempo limite excedido (120s) na consulta ao Windows Update / WSUS.";
                    Console.WriteLine("[AVISO] " + timeoutMsg);
                    LogAction("Atualizacoes / Drivers (Windows Update)", -1, timeoutMsg);
                    return;
                }
            }

            Console.WriteLine();
            string resultSummary = searchTask.Result;
            Console.WriteLine(resultSummary);
            LogAction("Atualizacoes / Drivers (Windows Update)", 0, resultSummary);
        }

        public void OpenWindowsUpdateSettings()
        {
            Console.WriteLine("Abrindo configuracoes do Windows Update...");
            ProcessResult res = _runner.Run(
                "cmd.exe",
                new string[] { "/c", "start", "", "ms-settings:windowsupdate" },
                ProcessRunner.DefaultTimeoutMs,
                null,
                null,
                false);
            LogAction("Procurar atualizacoes do Windows", res.ExitCode, "Tela do Windows Update aberta.");
        }

        private static string ExecuteComUpdateSearch()
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
                    Console.WriteLine(string.Format(" - {0}", title));
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
