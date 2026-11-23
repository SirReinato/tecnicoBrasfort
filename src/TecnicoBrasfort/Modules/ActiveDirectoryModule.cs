using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class ActiveDirectoryModule
    {
        public const double MaxKerberosSkewSeconds = 300.0;

        private readonly IProcessRunner _runner;
        private readonly AuditLogger _logger;
        private readonly EnvironmentInfo _env;
        private readonly AppConfig _config;

        public ActiveDirectoryModule(IProcessRunner runner, AuditLogger logger, EnvironmentInfo env, AppConfig config)
        {
            _runner = runner;
            _logger = logger;
            _env = env;
            _config = config;
        }

        public void ForceComputerGpUpdate()
        {
            Console.WriteLine("Atualizando GPO de computador (gpupdate /target:computer /force)...");
            Console.WriteLine("[NOTA] Em sessao elevada UAC, /target:user afetaria a conta administrativa e nao o usuario final.");

            ProcessResult res = _runner.Run(
                "gpupdate.exe",
                new string[] { "/target:computer", "/force" },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);

            Console.WriteLine(res.Summary);
            LogAction("Atualizar GPO de computador", res.ExitCode, res.Summary);
        }

        public void ShowComputerGpResult()
        {
            Console.WriteLine("Gerando relatorio de GPO aplicada ao computador (gpresult /r /scope:computer)...");
            ProcessResult res = _runner.Run(
                "gpresult.exe",
                new string[] { "/r", "/scope:computer" },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);

            LogAction("Relatorio GPO de computador", res.ExitCode, res.Summary);
        }

        public void RunDomainAndExchangeDiagnostics()
        {
            Console.WriteLine(string.Format("Executando diagnostico AD/GPO/Exchange para dominio '{0}'...", _config.DomainFqdn));
            int failures = 0;

            Console.WriteLine("\n--- 1. Canal Seguro e Descoberta de DC (nltest) ---");
            ProcessResult scVerify = _runner.Run(
                "nltest.exe",
                new string[] { "/sc_verify:" + _config.DomainFqdn },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);
            if (scVerify.ExitCode != 0) failures++;

            ProcessResult dsGetDc = _runner.Run(
                "nltest.exe",
                new string[] { "/dsgetdc:" + _config.DomainFqdn },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);
            if (dsGetDc.ExitCode != 0) failures++;

            Console.WriteLine("\n--- 2. Desvio de Relogio contra DC (w32tm /stripchart) ---");
            ProcessResult w32tm = _runner.Run(
                "w32tm.exe",
                new string[] { "/stripchart", "/computer:" + _config.DomainFqdn, "/samples:1", "/dataonly" },
                ProcessRunner.DefaultTimeoutMs,
                PrintLine,
                PrintError,
                false);

            double offsetSeconds;
            if (TryParseW32TmOffsetSeconds(w32tm.StandardOutput, out offsetSeconds))
            {
                if (Math.Abs(offsetSeconds) > MaxKerberosSkewSeconds)
                {
                    Console.WriteLine(string.Format(
                        "[ALERTA KERBEROS] Desvio de relogio de {0:0.0}s excede o limite de 300s (5 minutos)!",
                        offsetSeconds));
                    failures++;
                }
                else
                {
                    Console.WriteLine(string.Format("Relogio dentro da tolerancia Kerberos ({0:0.00}s).", offsetSeconds));
                }
            }
            else
            {
                Console.WriteLine("[AVISO] Nao foi possivel extrair o desvio numerico do w32tm.");
            }

            Console.WriteLine("\n--- 3. Tiquetes Kerberos da Sessao Atual (klist) ---");
            Console.WriteLine("[NOTA] Em elevacao UAC, klist exibe os tiquetes da conta administrativa em execucao.");
            _runner.Run("klist.exe", new string[0], ProcessRunner.DefaultTimeoutMs, PrintLine, PrintError, false);

            Console.WriteLine("\n--- 4. Resolucao DNS Autodiscover & Exchange HTTPS (TLS 1.2) ---");
            string autodiscoverHost = "autodiscover." + _config.DomainFqdn;
            try
            {
                IPAddress[] addrs = Dns.GetHostAddresses(autodiscoverHost);
                Console.WriteLine(string.Format(" - DNS {0}: {1} registro(s) encontrado(s).", autodiscoverHost, addrs.Length));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(" - DNS {0}: FALHA ({1})", autodiscoverHost, ex.Message));
                failures++;
            }

            if (!CheckExchangeHttps(_config.ExchangeCheckUrl))
            {
                failures++;
            }

            string summary = string.Format("Diagnostico AD/Exchange concluido com {0} alerta(s)/falha(s).", failures);
            Console.WriteLine("\n" + summary);
            LogAction("Diagnostico AD, Canal Seguro, Relogio e Exchange", failures == 0 ? 0 : 1, summary);
        }

        public static bool TryParseW32TmOffsetSeconds(string output, out double offsetSeconds)
        {
            offsetSeconds = 0.0;
            if (string.IsNullOrEmpty(output))
            {
                return false;
            }

            Match match = Regex.Match(output, @",\s*([+-]?\d+(?:[.,]\d+)?)s", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            string raw = match.Groups[1].Value.Replace(',', '.');
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out offsetSeconds);
        }

        private static bool CheckExchangeHttps(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Console.WriteLine(" - ExchangeCheckUrl nao configurada.");
                return false;
            }

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 5000;
                request.AllowAutoRedirect = false;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Console.WriteLine(string.Format(" - Exchange HTTPS ({0}): HTTP {1} ({2})",
                        url, (int)response.StatusCode, response.StatusDescription));
                    return true;
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                if (httpResp != null)
                {
                    int code = (int)httpResp.StatusCode;
                    Console.WriteLine(string.Format(" - Exchange HTTPS ({0}): HTTP {1} ({2})", url, code, httpResp.StatusDescription));
                    return true;
                }

                if (wex.Status == WebExceptionStatus.TrustFailure || wex.Status == WebExceptionStatus.SecureChannelFailure)
                {
                    Console.WriteLine(string.Format(" - Exchange HTTPS ({0}): ERRO DE CERTIFICADO SSL/TLS ({1})", url, wex.Message));
                    return false;
                }

                Console.WriteLine(string.Format(" - Exchange HTTPS ({0}): FALHA DE REDE/TCP 443 ({1}: {2})", url, wex.Status, wex.Message));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(" - Exchange HTTPS ({0}): ERRO ({1})", url, ex.Message));
                return false;
            }
        }

        private void LogAction(string actionName, int exitCode, string summary)
        {
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = _env.Hostname;
            entry.TechAdmin = _env.ElevatedAdmin;
            entry.InteractiveUser = _env.InteractiveUser;
            entry.ModuleName = "AD_GPO_Exchange";
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
