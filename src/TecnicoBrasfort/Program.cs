using System;
using System.Threading;
using System.Windows.Forms;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;
using TecnicoBrasfort.UI;

namespace TecnicoBrasfort
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            try
            {
                HostEnvironmentAdapter host = new HostEnvironmentAdapter();
                ConfigLoadResult loadResult = ConfigLoader.LoadWithStatus(host, AppDomain.CurrentDomain.BaseDirectory);

                string accessError;
                if (!AccessGate.IsConfigurationValid(loadResult, out accessError))
                {
                    MessageBox.Show(
                        accessError + "\n\nConfigure o hash de acesso no arquivo config.json usando scripts\\New-AccessPasswordHash.ps1.",
                        "Tecnico Brasfort - Acesso Bloqueado (Fail-Closed)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }

                if (loadResult.Config.RequireAccessPassword)
                {
                    using (AccessGateDialog gate = new AccessGateDialog(loadResult.Config))
                    {
                        if (gate.ShowDialog() != DialogResult.OK)
                        {
                            return 1;
                        }
                    }
                }

                EnvironmentInfo env = EnvironmentContext.Detect();
                ProcessRunner runner = new ProcessRunner();
                AuditLogger logger = new AuditLogger(host, env, loadResult.Config);

                MaintenanceModule maintenance = new MaintenanceModule(runner, host, logger, env);
                NetworkModule network = new NetworkModule(runner, logger, env, loadResult.Config);
                PrintingModule printing = new PrintingModule(runner, host, logger, env);
                DiagnosticsModule diagnostics = new DiagnosticsModule(runner, logger, env);
                DriversUpdatesModule drivers = new DriversUpdatesModule(runner, host, logger, env);
                ActiveDirectoryModule ad = new ActiveDirectoryModule(runner, logger, env, loadResult.Config);
                AuditModule audit = new AuditModule(logger);

                BatchExecutionEngine engine = new BatchExecutionEngine(
                    maintenance,
                    network,
                    printing,
                    diagnostics,
                    drivers,
                    ad,
                    audit,
                    host,
                    env,
                    logger);

                using (MainForm mainForm = new MainForm(env, engine, network, diagnostics, drivers, audit))
                {
                    Application.Run(mainForm);
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ocorreu uma falha inesperada ao iniciar o aplicativo:\n\n" + ex.Message,
                    "Tecnico Brasfort - Erro Fatal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(
                "Erro na interface:\n\n" + (e.Exception != null ? e.Exception.Message : "Erro desconhecido."),
                "Tecnico Brasfort - Erro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                "Erro critico nao tratado:\n\n" + (ex != null ? ex.Message : "Erro desconhecido."),
                "Tecnico Brasfort - Erro Critico",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
