using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TecnicoBrasfort.Core
{
    [DataContract]
    public class AppConfig
    {
        [DataMember]
        public string DomainFqdn { get; set; }

        [DataMember]
        public string[] DnsTestTargets { get; set; }

        [DataMember]
        public string ExchangeCheckUrl { get; set; }

        [DataMember]
        public string LogDirectoryOverride { get; set; }

        [DataMember]
        public bool RequireAccessPassword { get; set; }

        [DataMember]
        public string AccessSaltBase64 { get; set; }

        [DataMember]
        public string AccessHashBase64 { get; set; }

        [DataMember]
        public int AccessIterations { get; set; }

        [DataMember]
        public bool SkipAccessGateWhenElevated { get; set; }

        public AppConfig()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            DomainFqdn = "corp.example.local";
            DnsTestTargets = new string[] { "1.1.1.1", "8.8.8.8" };
            ExchangeCheckUrl = "https://mail.example.local/owa";
            LogDirectoryOverride = "";
            RequireAccessPassword = true;
            AccessSaltBase64 = "";
            AccessHashBase64 = "";
            AccessIterations = 100000;
            SkipAccessGateWhenElevated = false;
        }
    }

    public class ConfigLoadResult
    {
        public AppConfig Config { get; set; }
        public bool FileFound { get; set; }
        public bool ParseSucceeded { get; set; }
        public string ErrorMessage { get; set; }

        public ConfigLoadResult()
        {
            Config = new AppConfig();
            ErrorMessage = "";
        }
    }

    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public TimeSpan Duration { get; set; }
        public bool TimedOut { get; set; }
        public bool Cancelled { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public string Summary { get; set; }

        public ProcessResult()
        {
            StandardOutput = "";
            StandardError = "";
            Summary = "";
        }
    }

    public class ActionLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Hostname { get; set; }
        public string TechAdmin { get; set; }
        public string InteractiveUser { get; set; }
        public string ModuleName { get; set; }
        public string ActionName { get; set; }
        public int ExitCode { get; set; }
        public string Summary { get; set; }

        public ActionLogEntry()
        {
            Timestamp = DateTime.Now;
            Hostname = "";
            TechAdmin = "";
            InteractiveUser = "";
            ModuleName = "";
            ActionName = "";
            Summary = "";
        }
    }

    public class EnvironmentInfo
    {
        public string Hostname { get; set; }
        public string OsVersion { get; set; }
        public string ElevatedAdmin { get; set; }
        public string InteractiveUser { get; set; }
        public string InteractiveUserSid { get; set; }
        public string OriginDirectory { get; set; }
        public bool IsUncOrigin { get; set; }

        public EnvironmentInfo()
        {
            Hostname = "";
            OsVersion = "";
            ElevatedAdmin = "";
            InteractiveUser = "";
            InteractiveUserSid = "";
            OriginDirectory = "";
        }
    }

    public class ActionDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string CommandText { get; set; }
        public bool IsNativeApiEquivalent { get; set; }
        public bool IsBatchable { get; set; }
        public bool IsDestructive { get; set; }
        public bool DropsRemoteAccess { get; set; }
        public bool RequiresReboot { get; set; }
        public string EstimatedDuration { get; set; }

        public ActionDefinition()
        {
            Id = "";
            Name = "";
            Category = "";
            Description = "";
            CommandText = "";
            EstimatedDuration = "";
        }
    }

    public class ComboDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] ActionIds { get; set; }

        public ComboDefinition()
        {
            Id = "";
            Name = "";
            Description = "";
            ActionIds = new string[0];
        }
    }

    public class ActionExecutionResult
    {
        public string ActionId { get; set; }
        public string ActionName { get; set; }
        public int ExitCode { get; set; }
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public bool RebootRequired { get; set; }
        public TimeSpan Duration { get; set; }
        public string Summary { get; set; }

        public ActionExecutionResult()
        {
            ActionId = "";
            ActionName = "";
            Summary = "";
        }
    }

    public class BatchExecutionReport
    {
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public List<ActionExecutionResult> Results { get; set; }
        public bool RebootRequired { get; set; }
        public string TicketSummaryText { get; set; }
        public string SavedSummaryPath { get; set; }

        public BatchExecutionReport()
        {
            Results = new List<ActionExecutionResult>();
            TicketSummaryText = "";
            SavedSummaryPath = "";
        }
    }
}
