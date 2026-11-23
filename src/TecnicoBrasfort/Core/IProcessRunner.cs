using System;
using System.Threading;

namespace TecnicoBrasfort.Core
{
    public interface IProcessRunner
    {
        ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput);

        ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput,
            CancellationToken cancellationToken);
    }
}
