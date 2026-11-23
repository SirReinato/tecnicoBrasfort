using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class NetworkModule
    {
        public const int TcpTimeoutMs = 3000;

        private readonly IProcessRunner _runner;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;
        private readonly AppConfig _config;

        public NetworkModule(IProcessRunner runner, AuditLogger logger, EnvironmentInfo env, AppConfig config)
        {
            _runner = runner;
            _logger = logger;
            _env = env;
            _config = config;
        }

        public void ResetNetwork()
        {
            ResetNetwork(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ResetNetwork(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Executando reset de rede (Winsock, IP, Release, FlushDNS, Renew)...");

            string[][] commands = new string[][]
            {
                new string[] { "netsh.exe", "winsock", "reset" },
                new string[] { "netsh.exe", "int", "ip", "reset" },
                new string[] { "ipconfig.exe", "/release" },
                new string[] { "ipconfig.exe", "/flushdns" },
                new string[] { "ipconfig.exe", "/renew" }
            };

            int failures = 0;
            bool cancelled = false;

            for (int i = 0; i < commands.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                string exe = commands[i][0];
                string[] args = new string[commands[i].Length - 1];
                Array.Copy(commands[i], 1, args, 0, args.Length);

                obs.OnOutput(string.Format(">> {0} {1}", exe, string.Join(" ", args)));
                ProcessResult res = _runner.Run(
                    exe,
                    args,
                    ProcessRunner.DefaultTimeoutMs,
                    CreateOutputCallback(obs),
                    CreateErrorCallback(obs),
                    false,
                    cancellationToken);

                if (res.Cancelled)
                {
                    cancelled = true;
                    break;
                }

                if (res.ExitCode != 0)
                {
                    failures++;
                }
            }

            sw.Stop();
            int exitCode = cancelled ? -1 : (failures == 0 ? 0 : 1);
            string summary = cancelled
                ? "Reset de rede cancelado pelo tecnico."
                : string.Format("Reset de rede finalizado com {0} falha(s). Reinicializacao recomendada.", failures);
            obs.OnOutput(summary);
            LogAction("Reset de rede (IP, Winsock e DNS)", exitCode, summary);

            return BuildResult("net_reset", "Reset Completo de Rede (Winsock + IP + DNS)", exitCode, cancelled, !cancelled, sw.Elapsed, summary);
        }

        public void ShowNetworkInfo()
        {
            ShowNetworkInfo(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ShowNetworkInfo(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Obtendo informacoes de rede (ipconfig /all)...");
            ProcessResult res = _runner.Run(
                "ipconfig.exe",
                new string[] { "/all" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            LogAction("Informacoes de rede", res.ExitCode, res.Summary);
            return BuildResult("net_info", "Configuracoes de Rede (ipconfig /all)", res.ExitCode, res.Cancelled, false, res.Duration, res.Summary);
        }

        public void RunConnectivityTests()
        {
            RunConnectivityTests(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult RunConnectivityTests(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput("Executando testes de conectividade (timeout TCP 3000 ms)...");

            int passed = 0;
            int failed = 0;
            bool cancelled = false;

            List<string> gateways = GetDefaultGateways();
            for (int i = 0; i < gateways.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }
                if (TestPing(gateways[i], "Gateway Padrao", obs)) passed++; else failed++;
            }

            if (!cancelled && _config.DnsTestTargets != null)
            {
                for (int i = 0; i < _config.DnsTestTargets.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }
                    if (TestPing(_config.DnsTestTargets[i], "Alvo DNS/Externo", obs)) passed++; else failed++;
                }
            }

            int[] adPorts = new int[] { 53, 88, 389, 445 };
            for (int i = 0; i < adPorts.Length && !cancelled; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }
                if (TestTcpPort(_config.DomainFqdn, adPorts[i], "Porta AD", obs)) passed++; else failed++;
            }

            if (!cancelled)
            {
                string exchangeHost = ExtractHost(_config.ExchangeCheckUrl);
                if (!string.IsNullOrEmpty(exchangeHost))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                    }
                    else if (TestTcpPort(exchangeHost, 443, "Exchange HTTPS", obs))
                    {
                        passed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            sw.Stop();
            int exitCode = cancelled ? -1 : (failed == 0 ? 0 : 1);
            string summary = cancelled
                ? string.Format("Conectividade cancelada ({0} OK, {1} falha(s) ate o momento).", passed, failed)
                : string.Format("Conectividade: {0} OK, {1} falha(s).", passed, failed);
            obs.OnOutput(summary);
            LogAction("Testes de conectividade", exitCode, summary);

            return BuildResult("net_connectivity", "Testes de Conectividade (Gateway/DNS/AD)", exitCode, cancelled, false, sw.Elapsed, summary);
        }

        public void FlushDns()
        {
            FlushDns(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult FlushDns(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Limpando cache de DNS...");
            ProcessResult res = _runner.Run(
                "ipconfig.exe",
                new string[] { "/flushdns" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            obs.OnOutput(res.Summary);
            LogAction("Limpar cache de DNS", res.ExitCode, res.Summary);
            return BuildResult("net_flush_dns", "Limpar Cache DNS (ipconfig /flushdns)", res.ExitCode, res.Cancelled, false, res.Duration, res.Summary);
        }

        public void OpenNetworkConnections()
        {
            OpenNetworkConnections(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult OpenNetworkConnections(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Abrindo Conexoes de Rede (ncpa.cpl)...");
            ProcessResult res = _runner.Run(
                "rundll32.exe",
                new string[] { "shell32.dll,Control_RunDLL", "ncpa.cpl" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            LogAction("Abrir Conexoes de Rede", res.ExitCode, "ncpa.cpl acionado.");
            return BuildResult("net_connections", "Abrir Conexoes de Rede (ncpa.cpl)", res.ExitCode, res.Cancelled, false, res.Duration, "ncpa.cpl acionado.");
        }

        private static bool TestPing(string target, string label, IExecutionObserver observer)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(target, TcpTimeoutMs);
                    bool ok = reply != null && reply.Status == IPStatus.Success;
                    observer.OnOutput(string.Format(" - [{0}] {1}: {2}",
                        label,
                        target,
                        ok ? string.Format("OK ({0} ms)", reply.RoundtripTime) : "FALHA"));
                    return ok;
                }
            }
            catch (Exception ex)
            {
                observer.OnError(string.Format(" - [{0}] {1}: ERRO ({2})", label, target, ex.Message));
                return false;
            }
        }

        private static bool TestTcpPort(string host, int port, string label, IExecutionObserver observer)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(host, port, null, null);
                    bool connected = ar.AsyncWaitHandle.WaitOne(TcpTimeoutMs);
                    if (connected)
                    {
                        client.EndConnect(ar);
                    }
                    observer.OnOutput(string.Format(" - [{0}] {1}:{2} -> {3}", label, host, port, connected ? "OK" : "TIMEOUT/FECHADA"));
                    return connected;
                }
            }
            catch (Exception ex)
            {
                observer.OnError(string.Format(" - [{0}] {1}:{2} -> FALHA ({3})", label, host, port, ex.Message));
                return false;
            }
        }

        private static List<string> GetDefaultGateways()
        {
            List<string> list = new List<string>();
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    if (nics[i].OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }
                    IPInterfaceProperties props = nics[i].GetIPProperties();
                    for (int j = 0; j < props.GatewayAddresses.Count; j++)
                    {
                        string addr = props.GatewayAddresses[j].Address.ToString();
                        if (!string.IsNullOrEmpty(addr) && addr != "0.0.0.0" && !list.Contains(addr))
                        {
                            list.Add(addr);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return list;
        }

        private static string ExtractHost(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "";
            }
            try
            {
                Uri uri = new Uri(url);
                return uri.Host;
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "Rede";
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
