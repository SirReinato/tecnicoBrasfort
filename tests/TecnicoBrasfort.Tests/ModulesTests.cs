using System;
using System.Collections.Generic;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;

namespace TecnicoBrasfort.Tests
{
    public static class ModulesTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("NetworkModule_ResetNetwork_RunsCommandsInCorrectOrder", NetworkModule_ResetNetwork_RunsCommandsInCorrectOrder);
            run("MaintenanceModule_BootSchedule_UsesChkntfsWithoutStdinDeadlock", MaintenanceModule_BootSchedule_UsesChkntfsWithoutStdinDeadlock);
            run("DriversUpdatesModule_BackupDrivers_RejectsWhenFreeSpaceBelow5Gb", DriversUpdatesModule_BackupDrivers_RejectsWhenFreeSpaceBelow5Gb);
            run("DriversUpdatesModule_BackupDrivers_RunsPnputilWhenSpaceSufficient", DriversUpdatesModule_BackupDrivers_RunsPnputilWhenSpaceSufficient);
            run("ActiveDirectoryModule_TryParseW32TmOffsetSeconds_ParsesOffsets", ActiveDirectoryModule_TryParseW32TmOffsetSeconds_ParsesOffsets);
        }

        private static EnvironmentInfo CreateTestEnv()
        {
            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "WS-TEST-01";
            env.ElevatedAdmin = @"CORP\tech.admin";
            env.InteractiveUser = @"CORP\user.final";
            env.InteractiveUserSid = "S-1-5-21-1000";
            return env;
        }

        private static void NetworkModule_ResetNetwork_RunsCommandsInCorrectOrder()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            FakeHostEnvironment host = new FakeHostEnvironment();
            EnvironmentInfo env = CreateTestEnv();
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            NetworkModule net = new NetworkModule(runner, logger, env, cfg);
            net.ResetNetwork();

            Assert.Equal(5, runner.Invocations.Count);
            Assert.Equal("netsh.exe", runner.Invocations[0].FileName);
            Assert.Equal("winsock", runner.Invocations[0].Args[0]);
            Assert.Equal("netsh.exe", runner.Invocations[1].FileName);
            Assert.Equal("int", runner.Invocations[1].Args[0]);
            Assert.Equal("ipconfig.exe", runner.Invocations[2].FileName);
            Assert.Equal("/release", runner.Invocations[2].Args[0]);
            Assert.Equal("ipconfig.exe", runner.Invocations[3].FileName);
            Assert.Equal("/flushdns", runner.Invocations[3].Args[0]);
            Assert.Equal("ipconfig.exe", runner.Invocations[4].FileName);
            Assert.Equal("/renew", runner.Invocations[4].Args[0]);
        }

        private static void MaintenanceModule_BootSchedule_UsesChkntfsWithoutStdinDeadlock()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            FakeHostEnvironment host = new FakeHostEnvironment();
            EnvironmentInfo env = CreateTestEnv();
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            MaintenanceModule maint = new MaintenanceModule(runner, host, logger, env);
            maint.CheckDiskC(true);

            Assert.Equal(1, runner.Invocations.Count);
            Assert.Equal("chkntfs.exe", runner.Invocations[0].FileName);
            Assert.Equal("/c", runner.Invocations[0].Args[0]);
            Assert.Equal("C:", runner.Invocations[0].Args[1]);
        }

        private static void DriversUpdatesModule_BackupDrivers_RejectsWhenFreeSpaceBelow5Gb()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.FreeSpaceBytes = 2L * 1024L * 1024L * 1024L;
            EnvironmentInfo env = CreateTestEnv();
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            DriversUpdatesModule drv = new DriversUpdatesModule(runner, host, logger, env);
            drv.BackupDrivers(@"C:\Temp\Drivers");

            Assert.Equal(0, runner.Invocations.Count);
        }

        private static void DriversUpdatesModule_BackupDrivers_RunsPnputilWhenSpaceSufficient()
        {
            FakeProcessRunner runner = new FakeProcessRunner();
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.FreeSpaceBytes = 10L * 1024L * 1024L * 1024L;
            EnvironmentInfo env = CreateTestEnv();
            AppConfig cfg = new AppConfig();
            AuditLogger logger = new AuditLogger(host, env, @"C:\LogsP", @"C:\LogsS");

            DriversUpdatesModule drv = new DriversUpdatesModule(runner, host, logger, env);
            drv.BackupDrivers(@"C:\Temp\Drivers");

            Assert.Equal(1, runner.Invocations.Count);
            Assert.Equal("pnputil.exe", runner.Invocations[0].FileName);
            Assert.Equal("/export-driver", runner.Invocations[0].Args[0]);
        }

        private static void ActiveDirectoryModule_TryParseW32TmOffsetSeconds_ParsesOffsets()
        {
            double offset;
            bool ok1 = ActiveDirectoryModule.TryParseW32TmOffsetSeconds("10:42:00, +00.0123456s\r\n", out offset);
            Assert.True(ok1, "Deve parsear offset positivo.");
            Assert.True(Math.Abs(offset - 0.0123456) < 0.0001, "Valor positivo esperado.");

            bool ok2 = ActiveDirectoryModule.TryParseW32TmOffsetSeconds("10:42:01, -315.50s\r\n", out offset);
            Assert.True(ok2, "Deve parsear offset negativo.");
            Assert.True(Math.Abs(offset) > ActiveDirectoryModule.MaxKerberosSkewSeconds, "Deve detectar desvio > 300s.");
        }
    }
}
