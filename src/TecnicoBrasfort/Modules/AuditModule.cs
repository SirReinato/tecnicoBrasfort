using System;
using System.Collections.Generic;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Modules
{
    public class AuditModule
    {
        private const int PageSize = 20;
        private readonly AuditLogger _logger;

        public AuditModule(AuditLogger logger)
        {
            _logger = logger;
        }

        public void ShowHostLogs()
        {
            IList<string> lines = _logger.ReadLogsForCurrentHost();
            if (lines.Count == 0)
            {
                Console.WriteLine("Nenhum registro de acoes encontrado para este computador em ProgramData.");
                return;
            }

            Console.WriteLine(string.Format("Exibindo {0} registro(s) de acoes deste PC (mais recentes por arquivo):", lines.Count));
            int shown = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                Console.WriteLine(lines[i]);
                shown++;

                if (shown % PageSize == 0 && i < lines.Count - 1)
                {
                    Console.Write("Pressione ENTER para continuar ou 'Q' para voltar: ");
                    string input = Console.ReadLine();
                    if (input != null && input.Trim().Equals("Q", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
        }
    }
}
