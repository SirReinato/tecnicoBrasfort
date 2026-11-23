using System;

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
    }
}
