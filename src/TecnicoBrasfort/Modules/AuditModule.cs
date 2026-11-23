using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class AuditModule
    {
        private readonly AuditLogger _logger;

        public AuditModule(AuditLogger logger)
        {
            _logger = logger;
        }

        public IList<string> GetHostLogLines()
        {
            return _logger.ReadLogsForCurrentHost();
        }

        public void ShowHostLogs()
        {
            ShowHostLogs(NullExecutionObserver.Instance, CancellationToken.None);
        }

        public ActionExecutionResult ShowHostLogs(IExecutionObserver observer, CancellationToken cancellationToken)
        {
            IExecutionObserver obs = observer ?? NullExecutionObserver.Instance;
            Stopwatch sw = Stopwatch.StartNew();

            IList<string> lines = GetHostLogLines();
            if (lines.Count == 0)
            {
                obs.OnOutput("Nenhum registro de acoes encontrado para este computador em ProgramData.");
                sw.Stop();
                ActionExecutionResult emptyRes = new ActionExecutionResult();
                emptyRes.ActionId = "audit_logs";
                emptyRes.ActionName = "Visualizar Logs Locais";
                emptyRes.ExitCode = 0;
                emptyRes.Success = true;
                emptyRes.Duration = sw.Elapsed;
                emptyRes.Summary = "Nenhum registro encontrado.";
                return emptyRes;
            }

            obs.OnOutput(string.Format("Exibindo {0} registro(s) de acoes deste PC (mais recentes por arquivo):", lines.Count));
            for (int i = 0; i < lines.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    ActionExecutionResult cancelledRes = new ActionExecutionResult();
                    cancelledRes.ActionId = "audit_logs";
                    cancelledRes.ActionName = "Visualizar Logs Locais";
                    cancelledRes.ExitCode = -1;
                    cancelledRes.Cancelled = true;
                    cancelledRes.Success = false;
                    cancelledRes.Duration = sw.Elapsed;
                    cancelledRes.Summary = "Leitura de logs cancelada.";
                    return cancelledRes;
                }
                obs.OnOutput(lines[i]);
            }

            sw.Stop();
            ActionExecutionResult result = new ActionExecutionResult();
            result.ActionId = "audit_logs";
            result.ActionName = "Visualizar Logs Locais";
            result.ExitCode = 0;
            result.Success = true;
            result.Duration = sw.Elapsed;
            result.Summary = string.Format("{0} registro(s) carregado(s).", lines.Count);
            return result;
        }
    }
}
