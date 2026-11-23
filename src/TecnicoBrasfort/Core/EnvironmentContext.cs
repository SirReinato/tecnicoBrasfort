using System;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;

namespace TecnicoBrasfort.Core
{
    public static class EnvironmentContext
    {
        public static EnvironmentInfo Detect()
        {
            EnvironmentInfo info = new EnvironmentInfo();
            info.Hostname = Environment.MachineName;
            info.OsVersion = DetectOsVersion();
            info.OriginDirectory = AppDomain.CurrentDomain.BaseDirectory;
            info.IsUncOrigin = CheckIsUnc(info.OriginDirectory);

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            info.ElevatedAdmin = identity != null ? identity.Name : Environment.UserName;

            ResolveInteractiveUser(info);
            return info;
        }

        private static bool CheckIsUnc(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                Uri uri = new Uri(path);
                return uri.IsUnc;
            }
            catch (Exception)
            {
                return path.StartsWith("\\\\");
            }
        }

        private static string DetectOsVersion()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string caption = obj["Caption"] != null ? obj["Caption"].ToString().Trim() : "";
                        string version = obj["Version"] != null ? obj["Version"].ToString().Trim() : "";
                        if (!string.IsNullOrEmpty(caption))
                        {
                            return string.Format("{0} ({1})", caption, version);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return Environment.OSVersion.VersionString;
        }

        private static void ResolveInteractiveUser(EnvironmentInfo info)
        {
            int sessionId = Process.GetCurrentProcess().SessionId;

            try
            {
                string query = string.Format(
                    "SELECT ProcessId FROM Win32_Process WHERE Name = 'explorer.exe' AND SessionId = {0}",
                    sessionId);

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject proc in searcher.Get())
                    {
                        ManagementBaseObject ownerParams = proc.InvokeMethod("GetOwner", null, null);
                        string user = ownerParams != null && ownerParams["User"] != null
                            ? ownerParams["User"].ToString()
                            : "";
                        string domain = ownerParams != null && ownerParams["Domain"] != null
                            ? ownerParams["Domain"].ToString()
                            : "";

                        ManagementBaseObject sidParams = proc.InvokeMethod("GetOwnerSid", null, null);
                        string sid = sidParams != null && sidParams["Sid"] != null
                            ? sidParams["Sid"].ToString()
                            : "";

                        if (!string.IsNullOrEmpty(user))
                        {
                            info.InteractiveUser = string.IsNullOrEmpty(domain)
                                ? user
                                : string.Format("{0}\\{1}", domain, user);
                            info.InteractiveUserSid = sid;
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // Fallback secundario para sessao de console fisica
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string userName = obj["UserName"] != null ? obj["UserName"].ToString() : "";
                        if (!string.IsNullOrEmpty(userName))
                        {
                            info.InteractiveUser = userName;
                            info.InteractiveUserSid = TryTranslateToSid(userName);
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            info.InteractiveUser = "(nenhum)";
            info.InteractiveUserSid = "";
        }

        private static string TryTranslateToSid(string accountName)
        {
            try
            {
                NTAccount account = new NTAccount(accountName);
                SecurityIdentifier sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
                return sid.Value;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
