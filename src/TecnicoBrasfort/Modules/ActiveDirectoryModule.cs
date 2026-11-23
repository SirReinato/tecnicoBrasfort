using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
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
            ForceComputerGpUpdate(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ForceComputerGpUpdate(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Atualizando GPO de computador (gpupdate /target:computer /force)...");
            obs.OnOutput("[NOTA] Em sessao elevada UAC, /target:user afetaria a conta administrativa e nao o usuario final.");

            ProcessResult res = _runner.Run(
                "gpupdate.exe",
                new string[] { "/target:computer", "/force" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            obs.OnOutput(res.Summary);
            LogAction("Atualizar GPO de computador", res.ExitCode, res.Summary);
            return BuildResult("ad_gpupdate", "Atualizar Politicas de Grupo (gpupdate)", res.ExitCode, res.Cancelled, res.ExitCode == 3010, res.Duration, res.Summary);
        }

        public void ShowComputerGpResult()
        {
            ShowComputerGpResult(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ShowComputerGpResult(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            obs.OnOutput("Gerando relatorio de GPO aplicada ao computador (gpresult /r /scope:computer)...");
            ProcessResult res = _runner.Run(
                "gpresult.exe",
                new string[] { "/r", "/scope:computer" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);

            LogAction("Relatorio GPO de computador", res.ExitCode, res.Summary);
            return BuildResult("ad_gpresult", "Politicas Aplicadas (gpresult /r)", res.ExitCode, res.Cancelled, false, res.Duration, res.Summary);
        }

        public void RunDomainAndExchangeDiagnostics()
        {
            RunDomainAndExchangeDiagnostics(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult RunDomainAndExchangeDiagnostics(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();
            obs.OnOutput(string.Format("Executando diagnostico AD/GPO/Exchange para dominio '{0}'...", _config.DomainFqdn));
            int failures = 0;

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }

            obs.OnOutput("--- 1. Canal Seguro e Descoberta de DC (nltest) ---");
            ProcessResult scVerify = _runner.Run(
                "nltest.exe",
                new string[] { "/sc_verify:" + _config.DomainFqdn },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            if (scVerify.Cancelled)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }
            if (scVerify.ExitCode != 0) failures++;

            ProcessResult dsGetDc = _runner.Run(
                "nltest.exe",
                new string[] { "/dsgetdc:" + _config.DomainFqdn },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            if (dsGetDc.Cancelled)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }
            if (dsGetDc.ExitCode != 0) failures++;

            obs.OnOutput("--- 2. Desvio de Relogio contra DC (w32tm /stripchart) ---");
            ProcessResult w32tm = _runner.Run(
                "w32tm.exe",
                new string[] { "/stripchart", "/computer:" + _config.DomainFqdn, "/samples:1", "/dataonly" },
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            if (w32tm.Cancelled)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }

            double offsetSeconds;
            if (TryParseW32TmOffsetSeconds(w32tm.StandardOutput, out offsetSeconds))
            {
                if (Math.Abs(offsetSeconds) > MaxKerberosSkewSeconds)
                {
                    obs.OnError(string.Format(
                        "[ALERTA KERBEROS] Desvio de relogio de {0:0.0}s excede o limite de 300s (5 minutos)!",
                        offsetSeconds));
                    failures++;
                }
                else
                {
                    obs.OnOutput(string.Format("Relogio dentro da tolerancia Kerberos ({0:0.00}s).", offsetSeconds));
                }
            }
            else
            {
                obs.OnOutput("[AVISO] Nao foi possivel extrair o desvio numerico do w32tm.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }

            obs.OnOutput("--- 3. Tiquetes Kerberos da Sessao Atual (klist) ---");
            obs.OnOutput("[NOTA] Em elevacao UAC, klist exibe os tiquetes da conta administrativa em execucao.");
            ProcessResult klistRes = _runner.Run(
                "klist.exe",
                new string[0],
                ProcessRunner.DefaultTimeoutMs,
                CreateOutputCallback(obs),
                CreateErrorCallback(obs),
                false,
                cancellationToken);
            if (klistRes.Cancelled)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }

            obs.OnOutput("--- 4. Resolucao DNS Autodiscover & Exchange HTTPS (TLS 1.2) ---");
            string autodiscoverHost = "autodiscover." + _config.DomainFqdn;
            try
            {
                IPAddress[] addrs = Dns.GetHostAddresses(autodiscoverHost);
                obs.OnOutput(string.Format(" - DNS {0}: {1} registro(s) encontrado(s).", autodiscoverHost, addrs.Length));
            }
            catch (Exception ex)
            {
                obs.OnError(string.Format(" - DNS {0}: FALHA ({1})", autodiscoverHost, ex.Message));
                failures++;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return BuildCancelled(sw.Elapsed);
            }

            if (!CheckExchangeHttps(_config.ExchangeCheckUrl, obs))
            {
                failures++;
            }

            sw.Stop();
            int exitCode = failures == 0 ? 0 : 1;
            string summary = string.Format("Diagnostico AD/Exchange concluido com {0} alerta(s)/falha(s).", failures);
            obs.OnOutput(summary);
            LogAction("Diagnostico AD, Canal Seguro, Relogio e Exchange", exitCode, summary);

            return BuildResult("ad_diagnostics", "Diagnostico de Dominio, Hora e Exchange", exitCode, false, false, sw.Elapsed, summary);
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

        private static bool CheckExchangeHttps(string url, IExecutionObserver observer)
        {
            if (string.IsNullOrEmpty(url))
            {
                observer.OnOutput(" - ExchangeCheckUrl nao configurada.");
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
                    observer.OnOutput(string.Format(" - Exchange HTTPS ({0}): HTTP {1} ({2})",
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
                    observer.OnOutput(string.Format(" - Exchange HTTPS ({0}): HTTP {1} ({2})", url, code, httpResp.StatusDescription));
                    return true;
                }

                if (wex.Status == WebExceptionStatus.TrustFailure || wex.Status == WebExceptionStatus.SecureChannelFailure)
                {
                    observer.OnError(string.Format(" - Exchange HTTPS ({0}): ERRO DE CERTIFICADO SSL/TLS ({1})", url, wex.Message));
                    return false;
                }

                observer.OnError(string.Format(" - Exchange HTTPS ({0}): FALHA DE REDE/TCP 443 ({1}: {2})", url, wex.Status, wex.Message));
                return false;
            }
            catch (Exception ex)
            {
                observer.OnError(string.Format(" - Exchange HTTPS ({0}): ERRO ({1})", url, ex.Message));
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

        private static Action<string> CreateOutputCallback(IExecutionObserver observer)
        {
            return delegate(string line) { observer.OnOutput(line); };
        }

        private static Action<string> CreateErrorCallback(IExecutionObserver observer)
        {
            return delegate(string line) { observer.OnError("[STDERR] " + line); };
        }

        private static ActionExecutionResult BuildCancelled(TimeSpan duration)
        {
            return BuildResult("ad_diagnostics", "Diagnostico de Dominio, Hora e Exchange", -1, true, false, duration, "Diagnostico cancelado pelo tecnico.");
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
