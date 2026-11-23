using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public class AuditLogger
    {
        public const string DefaultPrimaryDirectory = @"C:\ProgramData\Brasfort\TecnicoBrasfort\Logs";
        public const string DefaultSecondaryDirectory = @"C:\Users\Public\Desktop\TecnicoBrasfort_Logs";

        private readonly IHostEnvironment _host;
        private readonly string _hostname;
        private readonly string _primaryDir;
        private readonly string _secondaryDir;
        private readonly string _sessionSecondaryFileName;

        public AuditLogger(IHostEnvironment host, EnvironmentInfo env, AppConfig config)
            : this(
                host,
                env,
                DefaultPrimaryDirectory,
                config != null && !string.IsNullOrEmpty(config.LogDirectoryOverride)
                    ? config.LogDirectoryOverride
                    : DefaultSecondaryDirectory)
        {
        }

        public AuditLogger(IHostEnvironment host, EnvironmentInfo env, string primaryDir, string secondaryDir)
        {
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }
            if (env == null)
            {
                throw new ArgumentNullException("env");
            }

            _host = host;
            _hostname = SanitizeFileSegment(env.Hostname, "UNKNOWN-PC");
            _primaryDir = string.IsNullOrEmpty(primaryDir) ? DefaultPrimaryDirectory : primaryDir;
            _secondaryDir = string.IsNullOrEmpty(secondaryDir) ? DefaultSecondaryDirectory : secondaryDir;

            string techAdmin = SanitizeFileSegment(ExtractSamAccountName(env.ElevatedAdmin), "admin");
            DateTime now = DateTime.Now;
            _sessionSecondaryFileName = string.Format(
                "{0}_{1:yyyy-MM-dd}_{1:HHmmss}_{2}.log",
                _hostname,
                now,
                techAdmin);
        }

        public void Log(ActionLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            string line = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss}] PC={1} | Admin={2} | Usuario={3} | Modulo={4} | Acao={5} | Exit={6} | {7}{8}",
                entry.Timestamp,
                entry.Hostname,
                entry.TechAdmin,
                entry.InteractiveUser,
                entry.ModuleName,
                entry.ActionName,
                entry.ExitCode,
                entry.Summary,
                Environment.NewLine);

            // Gravacao primaria garantida em ProgramData
            _host.CreateDirectory(_primaryDir);
            string primaryFile = Path.Combine(
                _primaryDir,
                string.Format("{0}_{1:yyyy-MM-dd}.log", _hostname, entry.Timestamp));
            _host.AppendAllText(primaryFile, line);

            // Copia secundaria por sessao (nao bloqueante)
            try
            {
                _host.CreateDirectory(_secondaryDir);
                string secondaryFile = Path.Combine(_secondaryDir, _sessionSecondaryFileName);
                _host.AppendAllText(secondaryFile, line);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(
                    "[AVISO] Nao foi possivel gravar copia secundaria de log em '{0}': {1}",
                    _secondaryDir,
                    ex.Message));
            }
        }

        public IList<string> ReadLogsForCurrentHost()
        {
            List<string> lines = new List<string>();
            if (!_host.DirectoryExists(_primaryDir))
            {
                return lines;
            }

            string pattern = string.Format("{0}_*.log", _hostname);
            List<string> files = new List<string>(_host.GetFiles(_primaryDir, pattern));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            files.Reverse();

            for (int i = 0; i < files.Count; i++)
            {
                string content = _host.ReadAllText(files[i]);
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                string[] fileLines = content.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < fileLines.Length; j++)
                {
                    lines.Add(fileLines[j]);
                }
            }

            return lines;
        }

        public static string ExtractSamAccountName(string account)
        {
            if (string.IsNullOrEmpty(account))
            {
                return "";
            }

            int slashIndex = account.LastIndexOf('\\');
            if (slashIndex >= 0 && slashIndex < account.Length - 1)
            {
                return account.Substring(slashIndex + 1);
            }

            return account;
        }

        public static string SanitizeFileSegment(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isInvalid = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }
                sb.Append(isInvalid ? '_' : c);
            }

            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }
    }
}
