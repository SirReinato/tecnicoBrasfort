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
            return Run(fileName, args, timeoutMs, onOutput, onError, useUnicodeOutput, CancellationToken.None);
        }

        public ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Nome do executavel nao pode ser vazio.", "fileName");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ProcessResult earlyCancelled = new ProcessResult();
                earlyCancelled.ExitCode = -1;
                earlyCancelled.Cancelled = true;
                earlyCancelled.Duration = TimeSpan.Zero;
                earlyCancelled.Summary = "Execucao cancelada antes do inicio.";
                return earlyCancelled;
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

                bool exited = false;
                bool cancelled = false;

                while (true)
                {
                    if (process.WaitForExit(250))
                    {
                        exited = true;
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    if (sw.ElapsedMilliseconds >= effectiveTimeout)
                    {
                        break;
                    }
                }

                if (exited)
                {
                    // Garante drenagem completa dos buffers assincronos antes de aguardar os handles
                    process.WaitForExit();
                    outputDone.WaitOne(5000);
                    errorDone.WaitOne(5000);
                }
                else
                {
                    KillProcessTree(process);
                    try
                    {
                        process.CancelOutputRead();
                    }
                    catch (Exception)
                    {
                    }
                    try
                    {
                        process.CancelErrorRead();
                    }
                    catch (Exception)
                    {
                    }
                }

                sw.Stop();

                ProcessResult result = new ProcessResult();
                result.Cancelled = cancelled;
                result.TimedOut = !exited && !cancelled;
                result.ExitCode = exited ? process.ExitCode : -1;
                result.Duration = sw.Elapsed;
                result.StandardOutput = stdoutBuilder.ToString();
                result.StandardError = stderrBuilder.ToString();

                if (cancelled)
                {
                    result.Summary = "Execucao cancelada pelo tecnico.";
                }
                else if (result.TimedOut)
                {
                    result.Summary = string.Format("Tempo limite excedido ({0} ms).", effectiveTimeout);
                }
                else
                {
                    result.Summary = string.Format("Concluido com codigo {0} em {1:0.0}s.", result.ExitCode, result.Duration.TotalSeconds);
                }

                return result;
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                int pid = process.Id;
                ProcessStartInfo killInfo = new ProcessStartInfo();
                killInfo.FileName = "taskkill.exe";
                killInfo.Arguments = string.Format("/F /T /PID {0}", pid);
                killInfo.UseShellExecute = false;
                killInfo.CreateNoWindow = true;

                using (Process killer = Process.Start(killInfo))
                {
                    if (killer != null)
                    {
                        killer.WaitForExit(5000);
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
