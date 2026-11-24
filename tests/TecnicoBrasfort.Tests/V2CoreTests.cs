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
            run("ElevationPolicy_Evaluate_CoversAllBranches", ElevationPolicy_Evaluate_CoversAllBranches);
            run("ElevationPolicy_ParseCredentials_HandlesAllFormats", ElevationPolicy_ParseCredentials_HandlesAllFormats);
            run("ElevationPolicy_Helpers_DrivePrefixRetryArgsAndErrors", ElevationPolicy_Helpers_DrivePrefixRetryArgsAndErrors);
            run("AccessGate_ShouldPromptForPassword_EnforcesFailClosedAndBypass", AccessGate_ShouldPromptForPassword_EnforcesFailClosedAndBypass);
            run("UnattendedSessionState_TransitionsAndGuards_EnforceIrreversibility", UnattendedSessionState_TransitionsAndGuards_EnforceIrreversibility);
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

        private static void ElevationPolicy_Evaluate_CoversAllBranches()
        {
            UacPolicyState luaEnabled = new UacPolicyState();
            Assert.True(luaEnabled.EnableLua, "Default EnableLua deve ser true.");
            Assert.Equal(3, luaEnabled.ConsentPromptBehaviorUser);

            Assert.Equal(
                ElevationDecision.AlreadyElevated,
                ElevationPolicy.Evaluate(true, luaEnabled, true, true));

            Assert.Equal(
                ElevationDecision.BlockedLoop,
                ElevationPolicy.Evaluate(false, luaEnabled, false, true));

            Assert.Equal(
                ElevationDecision.PromptCredentials,
                ElevationPolicy.Evaluate(false, luaEnabled, true, false));

            UacPolicyState autoDeny = new UacPolicyState();
            autoDeny.EnableLua = true;
            autoDeny.ConsentPromptBehaviorUser = 0;
            Assert.Equal(
                ElevationDecision.BlockedConsentDeny,
                ElevationPolicy.Evaluate(false, autoDeny, false, false));

            Assert.Equal(
                ElevationDecision.RelaunchRunAs,
                ElevationPolicy.Evaluate(false, luaEnabled, false, false));

            UacPolicyState luaDisabled = new UacPolicyState();
            luaDisabled.EnableLua = false;
            Assert.Equal(
                ElevationDecision.PromptCredentials,
                ElevationPolicy.Evaluate(false, luaDisabled, false, false));
        }

        private static void ElevationPolicy_ParseCredentials_HandlesAllFormats()
        {
            ParsedCredentials domUser = ElevationPolicy.ParseCredentials(@"CORP\tech.ti", "DEFAULTDOM", "PC-01");
            Assert.Equal("CORP", domUser.Domain);
            Assert.Equal("tech.ti", domUser.UserName);

            ParsedCredentials localUser = ElevationPolicy.ParseCredentials(@".\AdminLocal", "DEFAULTDOM", "PC-01");
            Assert.Equal("PC-01", localUser.Domain);
            Assert.Equal("AdminLocal", localUser.UserName);

            ParsedCredentials upnUser = ElevationPolicy.ParseCredentials("tech@corp.example.local", "DEFAULTDOM", "PC-01");
            Assert.True(upnUser.Domain == null, "UPN exige Domain null.");
            Assert.Equal("tech@corp.example.local", upnUser.UserName);

            ParsedCredentials bareUser = ElevationPolicy.ParseCredentials("tech.ti", "CORP", "PC-01");
            Assert.Equal("CORP", bareUser.Domain);
            Assert.Equal("tech.ti", bareUser.UserName);

            Assert.Equal("", ElevationPolicy.ParseCredentials("", "CORP", "PC-01").UserName);
            Assert.Equal("", ElevationPolicy.ParseCredentials(@"CORP\", "CORP", "PC-01").UserName);
            Assert.Equal("", ElevationPolicy.ParseCredentials(@".\", "CORP", "PC-01").UserName);
            Assert.Equal("", ElevationPolicy.ParseCredentials("@corp.local", "CORP", "PC-01").UserName);
        }

        private static void ElevationPolicy_Helpers_DrivePrefixRetryArgsAndErrors()
        {
            Assert.Equal("Z:", ElevationPolicy.ExtractDriveLetterPrefix(@"Z:\Ferramentas\TecnicoBrasfort.exe"));
            Assert.Equal("", ElevationPolicy.ExtractDriveLetterPrefix(@"\\servidor\share\TecnicoBrasfort.exe"));
            Assert.Equal("", ElevationPolicy.ExtractDriveLetterPrefix(""));

            Assert.True(ElevationPolicy.HasRetryFlag(new string[] { "--ELEVATED-CRED" }, ElevationPolicy.CredRetryFlag), "Case-insensitive retry flag.");
            Assert.Equal("--elevated-runas", ElevationPolicy.BuildRetryArguments(ElevationPolicy.RunAsRetryFlag));

            Assert.True(ElevationPolicy.FormatElevationWin32Error(1058, "").Contains("seclogon"), "Erro 1058 deve citar seclogon.");
            Assert.True(ElevationPolicy.FormatElevationWin32Error(1326, "").Contains("incorretos"), "Erro 1326 deve indicar credencial invalida.");
            Assert.True(ElevationPolicy.FormatElevationWin32Error(5, @"C:\Users\u\Desktop\app.exe").Contains("Win32 5"), "Erro 5 deve citar caminho.");
        }

        private static void AccessGate_ShouldPromptForPassword_EnforcesFailClosedAndBypass()
        {
            string salt;
            string hash;
            AccessGate.GenerateHash("SenhaTeste@2026", 100000, out salt, out hash);

            ConfigLoadResult validBypass = new ConfigLoadResult();
            validBypass.FileFound = true;
            validBypass.ParseSucceeded = true;
            validBypass.Config.RequireAccessPassword = true;
            validBypass.Config.AccessSaltBase64 = salt;
            validBypass.Config.AccessHashBase64 = hash;
            validBypass.Config.AccessIterations = 100000;
            validBypass.Config.SkipAccessGateWhenElevated = true;

            Assert.True(!AccessGate.ShouldPromptForPassword(validBypass, true), "Elevado + SkipAccessGateWhenElevated=true deve dispensar dialog.");
            Assert.True(AccessGate.ShouldPromptForPassword(validBypass, false), "Nao elevado deve exigir senha.");

            validBypass.Config.SkipAccessGateWhenElevated = false;
            Assert.True(AccessGate.ShouldPromptForPassword(validBypass, true), "SkipAccessGateWhenElevated=false deve exigir senha.");

            ConfigLoadResult missingHash = new ConfigLoadResult();
            missingHash.FileFound = true;
            missingHash.ParseSucceeded = true;
            missingHash.Config.RequireAccessPassword = true;
            missingHash.Config.SkipAccessGateWhenElevated = true;
            Assert.True(AccessGate.ShouldPromptForPassword(missingHash, true), "Hash ausente deve permanecer fail-closed.");

            ConfigLoadResult corrupt = new ConfigLoadResult();
            corrupt.FileFound = false;
            corrupt.ParseSucceeded = false;
            corrupt.Config.SkipAccessGateWhenElevated = true;
            Assert.True(AccessGate.ShouldPromptForPassword(corrupt, true), "Config ausente deve permanecer fail-closed.");
        }

        private static void UnattendedSessionState_TransitionsAndGuards_EnforceIrreversibility()
        {
            UnattendedSessionState state = new UnattendedSessionState();
            Assert.True(!state.IsRunning, "Estado inicial nao deve estar em execucao.");
            Assert.True(!state.IsUnattendedMode, "Estado inicial nao deve estar em modo ausente.");
            Assert.True(state.ShouldShowOwnerOnRestore(), "Modo normal deve exibir owner ao restaurar.");
            Assert.True(!state.TryActivateUnattended(), "Nao deve ativar modo ausente antes de Start().");

            state.Start();
            Assert.True(state.IsRunning, "Start() deve marcar IsRunning=true.");
            Assert.True(state.TryActivateUnattended(), "Deve permitir ativar modo ausente durante execucao.");
            Assert.True(state.IsUnattendedMode, "IsUnattendedMode deve ficar true.");
            Assert.True(!state.TryActivateUnattended(), "Segunda ativacao deve retornar false (idempotente).");
            Assert.True(!state.ShouldShowOwnerOnRestore(), "Modo ausente nao deve reexibir owner.");

            state.Complete();
            Assert.True(!state.IsRunning, "Complete() deve marcar IsRunning=false.");
            Assert.True(state.IsUnattendedMode, "Complete() deve preservar IsUnattendedMode=true.");
            Assert.True(!state.ShouldShowOwnerOnRestore(), "Modo ausente deve permanecer irreversivel apos Complete().");
            Assert.True(!state.TryActivateUnattended(), "Nao deve ativar via TryActivateUnattended apos Complete().");

            UnattendedSessionState modalRaceState = new UnattendedSessionState();
            modalRaceState.Start();
            modalRaceState.Complete();
            modalRaceState.ActivateUnattendedAfterConfirmation();
            Assert.True(modalRaceState.IsUnattendedMode, "Confirmacao modal deve ativar mesmo se fila concluiu durante o dialogo.");
            Assert.True(!modalRaceState.ShouldShowOwnerOnRestore(), "Owner nao deve reaparecer apos confirmacao modal.");
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
