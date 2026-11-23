using System;
using System.Collections.Generic;
using System.Threading;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;

namespace TecnicoBrasfort.Tests
{
    public static class V2CoreTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("AccessGate_GenerateAndVerify_ValidAndWrongPassword", AccessGate_GenerateAndVerify_ValidAndWrongPassword);
            run("AccessGate_FailClosed_WhenConfigMissingOrCorrupted", AccessGate_FailClosed_WhenConfigMissingOrCorrupted);
            run("AccessGate_AllowsLabMode_OnlyWhenExplicitlyDisabledInValidConfig", AccessGate_AllowsLabMode_OnlyWhenExplicitlyDisabledInValidConfig);
            run("ActionCatalog_Has20BatchActions5Combos5QuickToolsAndCombo1Order", ActionCatalog_Has20BatchActions5Combos5QuickToolsAndCombo1Order);
            run("BatchExecutionEngine_ContinuesOnFailureAndAggregatesReboot", BatchExecutionEngine_ContinuesOnFailureAndAggregatesReboot);
            run("BatchExecutionEngine_CancelsCooperatively", BatchExecutionEngine_CancelsCooperatively);
            run("TicketSummaryBuilder_FormatsActionsAndRebootStatus", TicketSummaryBuilder_FormatsActionsAndRebootStatus);
        }

        private static void AccessGate_GenerateAndVerify_ValidAndWrongPassword()
        {
            string salt;
            string hash;
            AccessGate.GenerateHash("SenhaTeste@2026", 100000, out salt, out hash);

            AppConfig cfg = new AppConfig();
            cfg.RequireAccessPassword = true;
            cfg.AccessSaltBase64 = salt;
            cfg.AccessHashBase64 = hash;
            cfg.AccessIterations = 100000;

            ConfigLoadResult loadResult = new ConfigLoadResult();
            loadResult.FileFound = true;
            loadResult.ParseSucceeded = true;
            loadResult.Config = cfg;

            string error;
            Assert.True(AccessGate.IsConfigurationValid(loadResult, out error), "Configuracao valida deve ser aceita: " + error);
            Assert.True(AccessGate.VerifyPassword("SenhaTeste@2026", cfg), "Senha correta deve autenticar.");
            Assert.True(!AccessGate.VerifyPassword("SenhaErrada", cfg), "Senha incorreta deve falhar.");
        }

        private static void AccessGate_FailClosed_WhenConfigMissingOrCorrupted()
        {
            FakeHostEnvironment host = new FakeHostEnvironment();
            ConfigLoadResult missing = ConfigLoader.LoadWithStatus(host, @"C:\SemConfig");
            string error;
            Assert.True(!missing.FileFound, "Arquivo ausente nao deve marcar FileFound.");
            Assert.True(!AccessGate.IsConfigurationValid(missing, out error), "Sem config.json deve bloquear (fail-closed).");

            host.Files[@"C:\Corrupt\config.json"] = "{ invalid json";
            ConfigLoadResult corrupt = ConfigLoader.LoadWithStatus(host, @"C:\Corrupt");
            Assert.True(corrupt.FileFound, "Arquivo encontrado.");
            Assert.True(!corrupt.ParseSucceeded, "JSON invalido deve falhar parse.");
            Assert.True(!AccessGate.IsConfigurationValid(corrupt, out error), "JSON corrompido deve bloquear (fail-closed).");
        }

        private static void AccessGate_AllowsLabMode_OnlyWhenExplicitlyDisabledInValidConfig()
        {
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.Files[@"C:\Lab\config.json"] = "{\"RequireAccessPassword\":false}";
            ConfigLoadResult lab = ConfigLoader.LoadWithStatus(host, @"C:\Lab");

            string error;
            Assert.True(lab.FileFound && lab.ParseSucceeded, "JSON de laboratorio valido.");
            Assert.True(AccessGate.IsConfigurationValid(lab, out error), "Modo laboratorio explicito deve ser aceito.");
            Assert.True(AccessGate.VerifyPassword("", lab.Config), "Sem exigencia de senha deve liberar.");
        }

        private static void ActionCatalog_Has20BatchActions5Combos5QuickToolsAndCombo1Order()
        {
            IList<ActionDefinition> batch = ActionCatalog.GetBatchableActions();
            IList<ActionDefinition> quick = ActionCatalog.GetQuickTools();
            IList<ComboDefinition> combos = ActionCatalog.GetCombos();

            Assert.Equal(20, batch.Count);
            Assert.Equal(5, quick.Count);
            Assert.Equal(5, combos.Count);

            for (int i = 0; i < batch.Count; i++)
            {
                Assert.True(!string.IsNullOrEmpty(batch[i].Description), "Acao deve ter descricao [?].");
                Assert.True(!string.IsNullOrEmpty(batch[i].CommandText), "Acao deve ter comando [?].");
            }

            ComboDefinition combo1 = ActionCatalog.FindComboById("combo_limpeza");
            Assert.True(combo1 != null, "Combo 1 deve existir.");
            Assert.Equal(4, combo1.ActionIds.Length);
            Assert.Equal("maint_temp", combo1.ActionIds[0]);
            Assert.Equal("maint_dism", combo1.ActionIds[1]);
            Assert.Equal("maint_sfc", combo1.ActionIds[2]);
            Assert.Equal("maint_winsxs", combo1.ActionIds[3]);
        }

        private static void BatchExecutionEngine_ContinuesOnFailureAndAggregatesReboot()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            runner.NextExitCode = 1; // Simula falha em comandos
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.PendingSystemReboot = true;

            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "PC-BATCH";
            env.ElevatedAdmin = @"CORP\tech";
            env.InteractiveUser = @"CORP\user";
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            BatchExecutionEngine engine = CreateEngine(runner, host, logger, env, cfg);
            List<ActionDefinition> queue = new List<ActionDefinition>();
            queue.Add(ActionCatalog.FindActionById("maint_dism"));
            queue.Add(ActionCatalog.FindActionById("net_flush_dns"));

            FakeExecutionObserver observer = new FakeExecutionObserver();
            BatchExecutionReport report = engine.Execute(queue, observer, CancellationToken.None);

            Assert.Equal(2, report.Results.Count);
            Assert.True(!report.Results[0].Success, "Primeira acao falhou (exit 1).");
            Assert.True(!report.Results[1].Success, "Segunda acao executou mesmo apos falha da primeira.");
            Assert.True(report.RebootRequired, "PendingSystemReboot deve propagar para o relatorio.");
            Assert.True(host.Files.ContainsKey(report.SavedSummaryPath), "Resumo de fechamento deve ser salvo em disco.");
        }

        private static void BatchExecutionEngine_CancelsCooperatively()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            FakeHostEnvironment host = new FakeHostEnvironment();
            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "PC-CANCEL";
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            BatchExecutionEngine engine = CreateEngine(runner, host, logger, env, cfg);
            List<ActionDefinition> queue = new List<ActionDefinition>();
            queue.Add(ActionCatalog.FindActionById("net_flush_dns"));
            queue.Add(ActionCatalog.FindActionById("maint_sfc"));

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();
                BatchExecutionReport report = engine.Execute(queue, NullExecutionObserver.Instance, cts.Token);
                Assert.Equal(0, report.Results.Count);
            }
        }

        private static void TicketSummaryBuilder_FormatsActionsAndRebootStatus()
        {
            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "WS-FIN-02";
            env.InteractiveUser = @"BRASFORT\maria";
            env.ElevatedAdmin = @"BRASFORT\tech";

            BatchExecutionReport report = new BatchExecutionReport();
            report.StartedAt = new DateTime(2026, 11, 23, 14, 0, 0);
            report.FinishedAt = new DateTime(2026, 11, 23, 14, 2, 0);
            report.RebootRequired = true;

            ActionExecutionResult r1 = new ActionExecutionResult();
            r1.ActionId = "maint_temp";
            r1.ActionName = "Limpeza de Temporarios";
            r1.Success = true;
            r1.ExitCode = 0;
            r1.Duration = TimeSpan.FromSeconds(5);
            r1.Summary = "OK";
            report.Results.Add(r1);

            string summary = TicketSummaryBuilder.Build(env, report);
            Assert.True(summary.Contains("Computador: WS-FIN-02"), "Deve conter hostname.");
            Assert.True(summary.Contains("Reinicializacao necessaria: SIM"), "Deve indicar reinicializacao.");
            Assert.True(summary.Contains("[OK] Limpeza de Temporarios"), "Deve listar acao concluida.");
        }

        private static BatchExecutionEngine CreateEngine(
            IProcessRunner runner,
            IHostEnvironment host,
            AuditLogger logger,
            EnvironmentInfo env,
            AppConfig cfg)
        {
            return new BatchExecutionEngine(
                new MaintenanceModule(runner, host, logger, env),
                new NetworkModule(runner, logger, env, cfg),
                new PrintingModule(runner, host, logger, env),
                new DiagnosticsModule(runner, logger, env),
                new DriversUpdatesModule(runner, host, logger, env),
                new ActiveDirectoryModule(runner, logger, env, cfg),
                new AuditModule(logger),
                host,
                env,
                logger);
        }
    }
}
