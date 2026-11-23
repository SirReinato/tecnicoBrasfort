using System;
using System.Collections.Generic;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Tests
{
    public static class CoreTests
    {
        public static void Register(Action<string, Action> run)
        {
            run("ArgumentEscaper_EscapesSpacesAndQuotes", ArgumentEscaper_EscapesSpacesAndQuotes);
            run("ArgumentEscaper_RejectsControlChars", ArgumentEscaper_RejectsControlChars);
            run("ConfigLoader_ReturnsGenericDefaultsWhenMissing", ConfigLoader_ReturnsGenericDefaultsWhenMissing);
            run("ConfigLoader_ParsesValidJsonAndNormalizes", ConfigLoader_ParsesValidJsonAndNormalizes);
            run("AuditLogger_SanitizesTechAdminAndHandlesSecondaryFailure", AuditLogger_SanitizesTechAdminAndHandlesSecondaryFailure);
            run("AuditLogger_SaveTicketSummary_WritesPrimaryAndNotifiesSecondaryError", AuditLogger_SaveTicketSummary_WritesPrimaryAndNotifiesSecondaryError);
            run("HostEnvironment_FormatsPrinterConnectionName", HostEnvironment_FormatsPrinterConnectionName);
        }

        private static void ArgumentEscaper_EscapesSpacesAndQuotes()
        {
            string joined = ArgumentEscaper.JoinArguments(new string[] { "/online", @"C:\Meus Documentos\log.txt", "say \"hi\"" });
            Assert.Equal("/online \"C:\\Meus Documentos\\log.txt\" \"say \\\"hi\\\"\"", joined);
        }

        private static void ArgumentEscaper_RejectsControlChars()
        {
            Assert.Throws<ArgumentException>(delegate() { ArgumentEscaper.Escape("abc\nwhoami"); }, "Deve rejeitar LF.");
            Assert.Throws<ArgumentException>(delegate() { ArgumentEscaper.Escape("abc\rwhoami"); }, "Deve rejeitar CR.");
            Assert.Throws<ArgumentException>(delegate() { ArgumentEscaper.Escape("abc\0whoami"); }, "Deve rejeitar NUL.");
        }

        private static void ConfigLoader_ReturnsGenericDefaultsWhenMissing()
        {
            FakeHostEnvironment host = new FakeHostEnvironment();
            AppConfig cfg = ConfigLoader.Load(host, @"C:\Inexistente");
            Assert.Equal("corp.example.local", cfg.DomainFqdn);
            Assert.Equal("https://mail.example.local/owa", cfg.ExchangeCheckUrl);
            Assert.Equal(2, cfg.DnsTestTargets.Length);
        }

        private static void ConfigLoader_ParsesValidJsonAndNormalizes()
        {
            string json = "{\"DomainFqdn\":\"ad.exemplo.local\",\"DnsTestTargets\":[\"10.0.0.1\"],\"ExchangeCheckUrl\":\"\",\"LogDirectoryOverride\":\"D:\\\\Logs\"}";
            AppConfig cfg = ConfigLoader.ParseJson(json);
            Assert.Equal("ad.exemplo.local", cfg.DomainFqdn);
            Assert.Equal(1, cfg.DnsTestTargets.Length);
            Assert.Equal("10.0.0.1", cfg.DnsTestTargets[0]);
            Assert.Equal("https://mail.example.local/owa", cfg.ExchangeCheckUrl);
            Assert.Equal(@"D:\Logs", cfg.LogDirectoryOverride);
        }

        private static void AuditLogger_SanitizesTechAdminAndHandlesSecondaryFailure()
        {
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.ThrowOnDirectoryPrefix = @"C:\BlockedDesktop";

            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "PC:01*TEST";
            env.ElevatedAdmin = @"BRASFORT\tech:admin";
            env.InteractiveUser = @"BRASFORT\usuario";

            AuditLogger logger = new AuditLogger(host, env, @"C:\PrimaryLogs", @"C:\BlockedDesktop\Logs");
            ActionLogEntry entry = new ActionLogEntry();
            entry.Hostname = env.Hostname;
            entry.TechAdmin = env.ElevatedAdmin;
            entry.InteractiveUser = env.InteractiveUser;
            entry.ModuleName = "Rede";
            entry.ActionName = "FlushDNS";
            entry.ExitCode = 0;
            entry.Summary = "OK";

            logger.Log(entry);

            IList<string> saved = logger.ReadLogsForCurrentHost();
            Assert.Equal(1, saved.Count);
            Assert.True(saved[0].Contains("FlushDNS"), "Log primario deve conter a acao mesmo quando destino secundario falhar.");
            Assert.Equal("tech_admin", AuditLogger.SanitizeFileSegment(AuditLogger.ExtractSamAccountName(@"BRASFORT\tech:admin"), "admin"));
        }

        private static void HostEnvironment_FormatsPrinterConnectionName()
        {
            Assert.Equal(@"\\print01\HP_Financeiro", HostEnvironmentAdapter.FormatPrinterConnectionName(",,print01,HP_Financeiro"));
        }

        private static void AuditLogger_SaveTicketSummary_WritesPrimaryAndNotifiesSecondaryError()
        {
            FakeHostEnvironment host = new FakeHostEnvironment();
            host.ThrowOnDirectoryPrefix = @"C:\BlockedDesktop";
            FakeExecutionObserver observer = new FakeExecutionObserver();

            EnvironmentInfo env = new EnvironmentInfo();
            env.Hostname = "PC-FECHAMENTO";
            env.ElevatedAdmin = @"CORP\admin";

            AuditLogger logger = new AuditLogger(host, env, @"C:\PrimaryLogs", @"C:\BlockedDesktop\Logs");
            logger.Observer = observer;

            DateTime stamp = new DateTime(2026, 11, 23, 14, 30, 0);
            string savedPath = logger.SaveTicketSummary("RESUMO TESTE", stamp);

            Assert.True(savedPath.Contains("Fechamento_PC-FECHAMENTO_2026-11-23_143000.txt"), "Caminho primario esperado.");
            Assert.True(host.Files.ContainsKey(savedPath), "Arquivo primario deve ser gravado.");
            Assert.Equal("RESUMO TESTE", host.Files[savedPath]);
            Assert.Equal(1, observer.Errors.Count);
        }
    }
}
