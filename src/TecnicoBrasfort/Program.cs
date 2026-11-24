using System;
using System.Reflection;
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
                string exePath = Assembly.GetEntryAssembly() != null
                    ? Assembly.GetEntryAssembly().Location
                    : Application.ExecutablePath;

                bool isElevated = ElevationService.IsCurrentProcessAdministrator();
                UacPolicyState uacPolicy = ElevationService.ReadUacPolicy();
                bool hasRunAsRetry = ElevationPolicy.HasRetryFlag(args, ElevationPolicy.RunAsRetryFlag);
                bool hasCredRetry = ElevationPolicy.HasRetryFlag(args, ElevationPolicy.CredRetryFlag);

                ElevationDecision decision = ElevationPolicy.Evaluate(
                    isElevated,
                    uacPolicy,
                    hasRunAsRetry,
                    hasCredRetry);

                if (decision == ElevationDecision.BlockedConsentDeny)
                {
                    MessageBox.Show(
                        "A politica de seguranca desta estacao (UAC Auto-Deny / ConsentPromptBehaviorUser = 0) bloqueia solicitacoes de elevacao para contas padrao.\n\nExecute o Tecnico Brasfort em uma sessao administrativa.",
                        "Tecnico Brasfort - Elevacao Bloqueada por GPO",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }

                if (decision == ElevationDecision.BlockedLoop)
                {
                    MessageBox.Show(
                        "O processo foi relancado, mas ainda nao possui privilegio de Administrador local.\n\nVerifique se a conta informada pertence ao grupo local Administrators (ou Workstation Administrators via GPO).",
                        "Tecnico Brasfort - Privilegio Insuficiente",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }

                if (decision == ElevationDecision.RelaunchRunAs)
                {
                    bool cancelledByUser;
                    string runAsError;
                    if (ElevationService.RelaunchWithRunAs(exePath, out cancelledByUser, out runAsError))
                    {
                        return 0;
                    }
                    if (!cancelledByUser)
                    {
                        MessageBox.Show(runAsError, "Tecnico Brasfort - Erro de Elevacao", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return 1;
                }

                if (decision == ElevationDecision.PromptCredentials)
                {
                    using (ElevationCredentialDialog credDlg = new ElevationCredentialDialog(exePath))
                    {
                        return credDlg.ShowDialog() == DialogResult.OK ? 0 : 1;
                    }
                }

                ElevationService.EnsureProcessTempDirectoryExists();

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

                if (AccessGate.ShouldPromptForPassword(loadResult, isElevated))
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
