using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
            Console.WriteLine("Executando reset de rede (Winsock, IP, Release, FlushDNS, Renew)...");
            string[][] commands = new string[][]
            {
                new string[] { "netsh.exe", "winsock", "reset" },
                new string[] { "netsh.exe", "int", "ip", "reset" },
                new string[] { "ipconfig.exe", "/release" },
                new string[] { "ipconfig.exe", "/flushdns" },
                new string[] { "ipconfig.exe", "/renew" }
            };

            int failures = 0;
            for (int i = 0; i < commands.Length; i++)
            {
                string exe = commands[i][0];
                string[] args = new string[commands[i].Length - 1];
                Array.Copy(commands[i], 1, args, 0, args.Length);

                Console.WriteLine(string.Format(">> {0} {1}", exe, string.Join(" ", args)));
                ProcessResult res = _runner.Run(exe, args, ProcessRunner.DefaultTimeoutMs, PrintLine, PrintError, false);
                if (res.ExitCode != 0)
                {
                    failures++;
                }
            }

            string summary = string.Format("Reset de rede finalizado com {0} falha(s). Reinicializacao recomendada.", failures);
            Console.WriteLine(summary);
            LogAction("Reset de rede (IP, Winsock e DNS)", failures == 0 ? 0 : 1, summary);
        }

        public void ShowNetworkInfo()
        {
            Console.WriteLine("Obtendo informacoes de rede (ipconfig /all)...");
            ProcessResult res = _runner.Run("ipconfig.exe", new string[] { "/all" }, ProcessRunner.DefaultTimeoutMs, PrintLine, PrintError, false);
            LogAction("Informacoes de rede", res.ExitCode, res.Summary);
        }

        public void RunConnectivityTests()
        {
            Console.WriteLine("Executando testes de conectividade (timeout TCP 3000 ms)...");
            int passed = 0;
            int failed = 0;

            List<string> gateways = GetDefaultGateways();
            for (int i = 0; i < gateways.Count; i++)
            {
                if (TestPing(gateways[i], "Gateway Padrao")) passed++; else failed++;
            }

            if (_config.DnsTestTargets != null)
            {
                for (int i = 0; i < _config.DnsTestTargets.Length; i++)
                {
                    if (TestPing(_config.DnsTestTargets[i], "Alvo DNS/Externo")) passed++; else failed++;
                }
            }

            int[] adPorts = new int[] { 53, 88, 389, 445 };
            for (int i = 0; i < adPorts.Length; i++)
            {
                if (TestTcpPort(_config.DomainFqdn, adPorts[i], "Porta AD")) passed++; else failed++;
            }

            string exchangeHost = ExtractHost(_config.ExchangeCheckUrl);
            if (!string.IsNullOrEmpty(exchangeHost))
            {
                if (TestTcpPort(exchangeHost, 443, "Exchange HTTPS")) passed++; else failed++;
            }

            string summary = string.Format("Conectividade: {0} OK, {1} falha(s).", passed, failed);
            Console.WriteLine(summary);
            LogAction("Testes de conectividade", failed == 0 ? 0 : 1, summary);
        }

        public void FlushDns()
        {
            Console.WriteLine("Limpando cache de DNS...");
            ProcessResult res = _runner.Run("ipconfig.exe", new string[] { "/flushdns" }, ProcessRunner.DefaultTimeoutMs, PrintLine, PrintError, false);
            Console.WriteLine(res.Summary);
            LogAction("Limpar cache de DNS", res.ExitCode, res.Summary);
        }

        public void OpenNetworkConnections()
        {
            Console.WriteLine("Abrindo Conexoes de Rede (ncpa.cpl)...");
            ProcessResult res = _runner.Run(
                "rundll32.exe",
                new string[] { "shell32.dll,Control_RunDLL", "ncpa.cpl" },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);
            LogAction("Abrir Conexoes de Rede", res.ExitCode, "ncpa.cpl acionado.");
        }

        private static bool TestPing(string target, string label)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(target, TcpTimeoutMs);
                    bool ok = reply != null && reply.Status == IPStatus.Success;
                    Console.WriteLine(string.Format(" - [{0}] {1}: {2}",
                        label,
                        target,
                        ok ? string.Format("OK ({0} ms)", reply.RoundtripTime) : "FALHA"));
                    return ok;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(" - [{0}] {1}: ERRO ({2})", label, target, ex.Message));
                return false;
            }
        }

        private static bool TestTcpPort(string host, int port, string label)
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
                    Console.WriteLine(string.Format(" - [{0}] {1}:{2} -> {3}", label, host, port, connected ? "OK" : "TIMEOUT/FECHADA"));
                    return connected;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(" - [{0}] {1}:{2} -> FALHA ({3})", label, host, port, ex.Message));
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
