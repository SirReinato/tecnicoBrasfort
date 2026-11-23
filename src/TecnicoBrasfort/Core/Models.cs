using System;
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

        public AppConfig()
        {
            DomainFqdn = "corp.example.local";
            DnsTestTargets = new string[] { "1.1.1.1", "8.8.8.8" };
            ExchangeCheckUrl = "https://mail.example.local/owa";
            LogDirectoryOverride = "";
        }
    }

    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public TimeSpan Duration { get; set; }
        public bool TimedOut { get; set; }
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
}
