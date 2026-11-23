using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public static class ConfigLoader
    {
        public static AppConfig Load(IHostEnvironment host, string baseDirectory)
        {
            if (host == null || string.IsNullOrEmpty(baseDirectory))
            {
                return new AppConfig();
            }

            string configPath = Path.Combine(baseDirectory, "config.json");
            if (!host.FileExists(configPath))
            {
                return new AppConfig();
            }

            try
            {
                string json = host.ReadAllText(configPath);
                return ParseJson(json);
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        public static AppConfig ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new AppConfig();
            }

            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    AppConfig loaded = serializer.ReadObject(ms) as AppConfig;
                    return Normalize(loaded);
                }
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        private static AppConfig Normalize(AppConfig config)
        {
            if (config == null)
            {
                return new AppConfig();
            }

            AppConfig defaults = new AppConfig();
            if (string.IsNullOrEmpty(config.DomainFqdn))
            {
                config.DomainFqdn = defaults.DomainFqdn;
            }
            if (config.DnsTestTargets == null || config.DnsTestTargets.Length == 0)
            {
                config.DnsTestTargets = defaults.DnsTestTargets;
            }
            if (string.IsNullOrEmpty(config.ExchangeCheckUrl))
            {
                config.ExchangeCheckUrl = defaults.ExchangeCheckUrl;
            }
            if (config.LogDirectoryOverride == null)
            {
                config.LogDirectoryOverride = "";
            }
            return config;
        }
    }
}
