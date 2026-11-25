using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace TecnicoBrasfort.Core
{
    public class HostEnvironmentAdapter : IHostEnvironment
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public void AppendAllText(string path, string content)
        {
            File.AppendAllText(path, content, Encoding.UTF8);
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        public bool TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryMoveFile(string sourcePath, string destinationPath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return false;
                }
                string destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Move(sourcePath, destinationPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryMoveDirectory(string sourcePath, string destinationPath)
        {
            try
            {
                if (!Directory.Exists(sourcePath))
                {
                    return false;
                }
                Directory.Move(sourcePath, destinationPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IList<string> EnsureNativeServiceProfilesPresent()
        {
            List<string> restored = new List<string>();
            EnsureServiceProfile(
                "S-1-5-18",
                @"%systemroot%\system32\config\systemprofile",
                12,
                new byte[] { 1, 1, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0 },
                restored);
            EnsureServiceProfile(
                "S-1-5-19",
                @"%SystemRoot%\ServiceProfiles\LocalService",
                0,
                new byte[] { 1, 1, 0, 0, 0, 0, 0, 5, 19, 0, 0, 0 },
                restored);
            EnsureServiceProfile(
                "S-1-5-20",
                @"%SystemRoot%\ServiceProfiles\NetworkService",
                0,
                new byte[] { 1, 1, 0, 0, 0, 0, 0, 5, 20, 0, 0, 0 },
                restored);
            return restored;
        }

        private static void EnsureServiceProfile(
            string sid,
            string profileImagePath,
            int flags,
            byte[] sidBytes,
            IList<string> restored)
        {
            const string profileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey profileList = baseKey.OpenSubKey(profileListPath, true))
            {
                if (profileList == null)
                {
                    return;
                }

                using (RegistryKey existing = profileList.OpenSubKey(sid, true))
                {
                    if (existing != null)
                    {
                        object currentPath = existing.GetValue("ProfileImagePath", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (currentPath != null && !string.IsNullOrEmpty(Convert.ToString(currentPath)))
                        {
                            return;
                        }

                        existing.SetValue("ProfileImagePath", profileImagePath, RegistryValueKind.ExpandString);
                        existing.SetValue("Flags", flags, RegistryValueKind.DWord);
                        existing.SetValue("State", 0, RegistryValueKind.DWord);
                        existing.SetValue("Sid", sidBytes, RegistryValueKind.Binary);
                        restored.Add(sid);
                        return;
                    }
                }

                using (RegistryKey created = profileList.CreateSubKey(sid))
                {
                    if (created != null)
                    {
                        created.SetValue("ProfileImagePath", profileImagePath, RegistryValueKind.ExpandString);
                        created.SetValue("Flags", flags, RegistryValueKind.DWord);
                        created.SetValue("State", 0, RegistryValueKind.DWord);
                        created.SetValue("Sid", sidBytes, RegistryValueKind.Binary);
                        restored.Add(sid);
                    }
                }
            }
        }

        public long GetAvailableFreeSpaceBytes(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            DriveInfo drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }

        public IList<string> GetUserPrinterConnections(string userSid)
        {
            List<string> printers = new List<string>();
            if (string.IsNullOrEmpty(userSid))
            {
                return printers;
            }

            string subKeyPath = userSid + "\\Printers\\Connections";
            using (RegistryKey key = Registry.Users.OpenSubKey(subKeyPath))
            {
                if (key == null)
                {
                    return printers;
                }

                string[] names = key.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    printers.Add(FormatPrinterConnectionName(names[i]));
                }
            }

            return printers;
        }

        public bool HasPendingSystemReboot()
        {
            try
            {
                using (RegistryKey baseKey = OpenLocalMachineKey())
                {
                    if (baseKey == null)
                    {
                        return false;
                    }

                    using (RegistryKey cbsKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    {
                        if (cbsKey != null)
                        {
                            return true;
                        }
                    }

                    using (RegistryKey wuKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    {
                        if (wuKey != null)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        public static RegistryKey OpenLocalMachineKey()
        {
            try
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            catch (Exception)
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            }
        }

        public static string FormatPrinterConnectionName(string subKeyName)
        {
            if (string.IsNullOrEmpty(subKeyName))
            {
                return "";
            }
            return subKeyName.Replace(',', '\\');
        }
    }
}
