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
