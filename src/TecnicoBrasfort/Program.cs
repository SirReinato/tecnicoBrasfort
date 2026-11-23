using System;
using System.Text;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;
using TecnicoBrasfort.UI;

namespace TecnicoBrasfort
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (Exception)
            {
            }

            try
            {
                HostEnvironmentAdapter host = new HostEnvironmentAdapter();
                AppConfig config = ConfigLoader.Load(host, AppDomain.CurrentDomain.BaseDirectory);
                EnvironmentInfo env = EnvironmentContext.Detect();
                ProcessRunner runner = new ProcessRunner();
                AuditLogger logger = new AuditLogger(host, env, config);

                MaintenanceModule maintenance = new MaintenanceModule(runner, host, logger, env);
                NetworkModule network = new NetworkModule(runner, logger, env, config);
                PrintingModule printing = new PrintingModule(runner, host, logger, env);
                DiagnosticsModule diagnostics = new DiagnosticsModule(runner, logger, env);
                DriversUpdatesModule drivers = new DriversUpdatesModule(runner, host, logger, env);
                ActiveDirectoryModule ad = new ActiveDirectoryModule(runner, logger, env, config);
                AuditModule audit = new AuditModule(logger);

                ConsoleMenu menu = new ConsoleMenu(
                    env,
                    maintenance,
                    network,
                    printing,
                    diagnostics,
                    drivers,
                    ad,
                    audit);

                menu.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[ERRO FATAL] Ocorreu uma falha inesperada:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Pressione ENTER para sair...");
                Console.ReadLine();
                return 1;
            }
        }
    }
}
