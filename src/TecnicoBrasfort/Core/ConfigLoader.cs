using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public static class ConfigLoader
    {
        public static ConfigLoadResult LoadWithStatus(IHostEnvironment host, string baseDirectory)
        {
            ConfigLoadResult result = new ConfigLoadResult();
            if (host == null || string.IsNullOrEmpty(baseDirectory))
            {
                result.FileFound = false;
                result.ParseSucceeded = false;
                result.ErrorMessage = "Diretorio base nao informado.";
                return result;
            }

            string configPath = Path.Combine(baseDirectory, "config.json");
            if (!host.FileExists(configPath))
            {
                result.FileFound = false;
                result.ParseSucceeded = false;
                result.ErrorMessage = "Arquivo config.json nao encontrado ao lado do executavel.";
                return result;
            }

            result.FileFound = true;

            try
            {
                string json = host.ReadAllText(configPath);
                if (string.IsNullOrEmpty(json))
                {
                    result.ParseSucceeded = false;
                    result.ErrorMessage = "Arquivo config.json esta vazio.";
                    return result;
                }

                AppConfig loaded = DeserializeJson(json);
                if (loaded == null)
                {
                    result.ParseSucceeded = false;
                    result.ErrorMessage = "Arquivo config.json nao contem configuracao valida.";
                    return result;
                }

                result.Config = Normalize(loaded);
                result.ParseSucceeded = true;
                result.ErrorMessage = "";
                return result;
            }
            catch (Exception ex)
            {
                result.Config = new AppConfig();
                result.ParseSucceeded = false;
                result.ErrorMessage = "Falha ao ler/interpretar config.json: " + ex.Message;
                return result;
            }
        }

        public static AppConfig Load(IHostEnvironment host, string baseDirectory)
        {
            return LoadWithStatus(host, baseDirectory).Config;
        }

        public static AppConfig ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new AppConfig();
            }

            try
            {
                AppConfig loaded = DeserializeJson(json);
                return Normalize(loaded);
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        private static AppConfig DeserializeJson(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                return serializer.ReadObject(ms) as AppConfig;
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
            if (config.AccessSaltBase64 == null)
            {
                config.AccessSaltBase64 = "";
            }
            if (config.AccessHashBase64 == null)
            {
                config.AccessHashBase64 = "";
            }
            return config;
        }
    }
}
