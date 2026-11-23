using System;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public static class TicketSummaryBuilder
    {
        public static string Build(EnvironmentInfo env, BatchExecutionReport report)
        {
            StringBuilder sb = new StringBuilder();
            string hostname = env != null && !string.IsNullOrEmpty(env.Hostname) ? env.Hostname : "UNKNOWN-PC";
            string interactiveUser = env != null && !string.IsNullOrEmpty(env.InteractiveUser) ? env.InteractiveUser : "(desconhecido)";
            string techAdmin = env != null && !string.IsNullOrEmpty(env.ElevatedAdmin) ? env.ElevatedAdmin : "(desconhecido)";

            DateTime startedAt = report != null ? report.StartedAt : DateTime.Now;
            DateTime finishedAt = report != null ? report.FinishedAt : startedAt;
            TimeSpan totalDuration = finishedAt - startedAt;
            if (totalDuration < TimeSpan.Zero)
            {
                totalDuration = TimeSpan.Zero;
            }

            bool rebootRequired = report != null && report.RebootRequired;

            sb.AppendLine("=== FECHAMENTO DE CHAMADO - TECNICO BRASFORT ===");
            sb.AppendLine(string.Format("Computador: {0}", hostname));
            sb.AppendLine(string.Format("Usuario: {0}", interactiveUser));
            sb.AppendLine(string.Format("Tecnico (Admin): {0}", techAdmin));
            sb.AppendLine(string.Format("Inicio: {0:dd/MM/yyyy HH:mm:ss}", startedAt));
            sb.AppendLine(string.Format("Termino: {0:dd/MM/yyyy HH:mm:ss}", finishedAt));
            sb.AppendLine(string.Format("Duracao total: {0:0.0}s", totalDuration.TotalSeconds));
            sb.AppendLine(string.Format("Reinicializacao necessaria: {0}", rebootRequired ? "SIM" : "NAO"));
            sb.AppendLine();
            sb.AppendLine("Acoes executadas:");

            if (report != null && report.Results != null && report.Results.Count > 0)
            {
                for (int i = 0; i < report.Results.Count; i++)
                {
                    ActionExecutionResult item = report.Results[i];
                    string statusTag = item.Cancelled ? "CANCELADO" : (item.Success ? "OK" : "FALHA");
                    sb.AppendLine(string.Format(
                        "- [{0}] {1} (Exit: {2}, {3:0.0}s) - {4}",
                        statusTag,
                        item.ActionName,
                        item.ExitCode,
                        item.Duration.TotalSeconds,
                        item.Summary));
                }
            }
            else
            {
                sb.AppendLine("- Nenhuma acao executada.");
            }

            return sb.ToString();
        }
    }
}
