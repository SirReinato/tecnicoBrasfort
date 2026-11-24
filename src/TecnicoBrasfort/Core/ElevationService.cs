using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace TecnicoBrasfort.Core
{
    public static class ElevationService
    {
        private const string UacPolicySubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        private const int Logon32LogonInteractive = 2;
        private const int Logon32ProviderDefault = 0;
        private const int TokenElevationTypeInfoClass = 18;
        private const int TokenElevationTypeLimited = 3;
        private const int ErrorMoreData = 234;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LogonUserW(
            string lpszUsername,
            string lpszDomain,
            IntPtr lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            int tokenInformationClass,
            out int tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetGetConnectionW(
            string lpLocalName,
            StringBuilder lpRemoteName,
            ref int lpnLength);

        public static bool IsCurrentProcessAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (identity == null)
                    {
                        return false;
                    }
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static UacPolicyState ReadUacPolicy()
        {
            UacPolicyState state = new UacPolicyState();
            try
            {
                using (RegistryKey hklm = HostEnvironmentAdapter.OpenLocalMachineKey())
                {
                    if (hklm == null)
                    {
                        return state;
                    }

                    using (RegistryKey key = hklm.OpenSubKey(UacPolicySubKey))
                    {
                        if (key == null)
                        {
                            return state;
                        }

                        object luaObj = key.GetValue("EnableLUA");
                        if (luaObj != null)
                        {
                            state.EnableLua = Convert.ToInt32(luaObj) != 0;
                        }

                        object consentObj = key.GetValue("ConsentPromptBehaviorUser");
                        if (consentObj != null)
                        {
                            state.ConsentPromptBehaviorUser = Convert.ToInt32(consentObj);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return state;
        }

        public static string ResolveExecutablePath(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return "";
            }

            string drivePrefix = ElevationPolicy.ExtractDriveLetterPrefix(exePath);
            if (string.IsNullOrEmpty(drivePrefix))
            {
                return exePath;
            }

            try
            {
                int capacity = 260;
                StringBuilder sb = new StringBuilder(capacity);
                int rc = WNetGetConnectionW(drivePrefix, sb, ref capacity);
                if (rc == ErrorMoreData && capacity > 0)
                {
                    sb = new StringBuilder(capacity);
                    rc = WNetGetConnectionW(drivePrefix, sb, ref capacity);
                }

                if (rc == 0 && sb.Length > 0)
                {
                    string uncRoot = sb.ToString().TrimEnd('\\');
                    string remainder = exePath.Substring(2);
                    return uncRoot + remainder;
                }
            }
            catch (Exception)
            {
            }

            return exePath;
        }

        public static bool ValidateAdminCredentials(
            string domain,
            string userName,
            SecureString password,
            out bool isSplitTokenAdmin,
            out string errorMessage)
        {
            isSplitTokenAdmin = false;
            errorMessage = "";

            if (string.IsNullOrEmpty(userName) || password == null || password.Length == 0)
            {
                errorMessage = "Informe usuario e senha administrativos.";
                return false;
            }

            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr passwordBstr = IntPtr.Zero;

            try
            {
                passwordBstr = Marshal.SecureStringToBSTR(password);
                string effectiveDomain = string.IsNullOrEmpty(domain) ? null : domain;

                bool ok = LogonUserW(
                    userName,
                    effectiveDomain,
                    passwordBstr,
                    Logon32LogonInteractive,
                    Logon32ProviderDefault,
                    out tokenHandle);

                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = ElevationPolicy.FormatElevationWin32Error(err, "");
                    return false;
                }

                using (WindowsIdentity identity = new WindowsIdentity(tokenHandle))
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        return true;
                    }
                }

                int elevationType;
                int returnLength;
                if (GetTokenInformation(
                    tokenHandle,
                    TokenElevationTypeInfoClass,
                    out elevationType,
                    sizeof(int),
                    out returnLength) && elevationType == TokenElevationTypeLimited)
                {
                    isSplitTokenAdmin = true;
                    return false;
                }

                errorMessage = "A conta foi autenticada, mas nao pertence ao grupo local Administrators. Verifique se a GPO de Workstation Administrators foi aplicada nesta estacao.";
                return false;
            }
            finally
            {
                if (passwordBstr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(passwordBstr);
                }
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }

        public static bool RelaunchWithRunAs(
            string exePath,
            out bool cancelledByUser,
            out string errorMessage)
        {
            cancelledByUser = false;
            errorMessage = "";

            string targetPath = ResolveExecutablePath(exePath);
            Environment.SetEnvironmentVariable("__COMPAT_LAYER", null);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = targetPath;
            psi.Arguments = ElevationPolicy.BuildRetryArguments(ElevationPolicy.RunAsRetryFlag);
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WorkingDirectory = Environment.SystemDirectory;

            try
            {
                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.Dispose();
                }
                return true;
            }
            catch (Win32Exception wex)
            {
                if (wex.NativeErrorCode == 1223)
                {
                    cancelledByUser = true;
                    return false;
                }
                errorMessage = ElevationPolicy.FormatElevationWin32Error(wex.NativeErrorCode, targetPath);
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Falha ao solicitar elevacao UAC: " + ex.Message;
                return false;
            }
        }

        public static bool RelaunchWithCredentials(
            string exePath,
            string domain,
            string userName,
            SecureString password,
            out bool requiresRunAsFallback,
            out string errorMessage)
        {
            requiresRunAsFallback = false;
            errorMessage = "";

            string targetPath = ResolveExecutablePath(exePath);
            PreHydrateConfigFile(targetPath);
            Environment.SetEnvironmentVariable("__COMPAT_LAYER", null);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = targetPath;
            psi.Arguments = ElevationPolicy.BuildRetryArguments(ElevationPolicy.CredRetryFlag);
            psi.UseShellExecute = false;
            psi.LoadUserProfile = false;
            psi.Domain = string.IsNullOrEmpty(domain) ? "" : domain;
            psi.UserName = userName;
            psi.Password = password;
            psi.WorkingDirectory = Environment.SystemDirectory;

            try
            {
                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.Dispose();
                }
                return true;
            }
            catch (Win32Exception wex)
            {
                if (wex.NativeErrorCode == 740)
                {
                    requiresRunAsFallback = true;
                }
                errorMessage = ElevationPolicy.FormatElevationWin32Error(wex.NativeErrorCode, targetPath);
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Falha ao iniciar processo elevado: " + ex.Message;
                return false;
            }
        }

        public static void EnsureProcessTempDirectoryExists()
        {
            try
            {
                string currentTemp = Path.GetTempPath();
                if (!Directory.Exists(currentTemp))
                {
                    string fallbackTemp = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "Temp");
                    if (Directory.Exists(fallbackTemp))
                    {
                        Environment.SetEnvironmentVariable("TEMP", fallbackTemp, EnvironmentVariableTarget.Process);
                        Environment.SetEnvironmentVariable("TMP", fallbackTemp, EnvironmentVariableTarget.Process);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void PreHydrateConfigFile(string exePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(dir))
                {
                    return;
                }
                string cfg = Path.Combine(dir, "config.json");
                if (File.Exists(cfg))
                {
                    using (FileStream fs = File.Open(cfg, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.ReadByte();
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
