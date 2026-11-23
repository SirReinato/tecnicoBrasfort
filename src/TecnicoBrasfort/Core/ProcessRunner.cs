using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TecnicoBrasfort.Core
{
    public class ProcessRunner : IProcessRunner
    {
        public const int DefaultTimeoutMs = 60000;
        public const int LongOperationTimeoutMs = 2700000;

        public ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Nome do executavel nao pode ser vazio.", "fileName");
            }

            int effectiveTimeout = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;
            string arguments = ArgumentEscaper.JoinArguments(args);
            Encoding encoding = useUnicodeOutput
                ? Encoding.Unicode
                : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = encoding;
            startInfo.StandardErrorEncoding = encoding;

            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();
            Stopwatch sw = Stopwatch.StartNew();

            using (ManualResetEvent outputDone = new ManualResetEvent(false))
            using (ManualResetEvent errorDone = new ManualResetEvent(false))
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                    {
                        outputDone.Set();
                        return;
                    }
                    string clean = e.Data.Replace("\0", "");
                    lock (stdoutBuilder)
                    {
                        stdoutBuilder.AppendLine(clean);
                    }
                    if (onOutput != null)
                    {
                        onOutput(clean);
                    }
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                    {
                        errorDone.Set();
                        return;
                    }
                    string clean = e.Data.Replace("\0", "");
                    lock (stderrBuilder)
                    {
                        stderrBuilder.AppendLine(clean);
                    }
                    if (onError != null)
                    {
                        onError(clean);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = process.WaitForExit(effectiveTimeout);
                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    outputDone.WaitOne(5000);
                    errorDone.WaitOne(5000);
                }

                sw.Stop();

                ProcessResult result = new ProcessResult();
                result.TimedOut = !exited;
                result.ExitCode = exited ? process.ExitCode : -1;
                result.Duration = sw.Elapsed;
                result.StandardOutput = stdoutBuilder.ToString();
                result.StandardError = stderrBuilder.ToString();
                result.Summary = exited
                    ? string.Format("Concluido com codigo {0} em {1:0.0}s.", result.ExitCode, result.Duration.TotalSeconds)
                    : string.Format("Tempo limite excedido ({0} ms).", effectiveTimeout);
                return result;
            }
        }
    }
}
