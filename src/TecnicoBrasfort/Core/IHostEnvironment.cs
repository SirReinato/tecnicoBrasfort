using System.Collections.Generic;

namespace TecnicoBrasfort.Core
{
    public interface IHostEnvironment
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void AppendAllText(string path, string content);
        IEnumerable<string> GetFiles(string path, string searchPattern);
        IEnumerable<string> GetDirectories(string path);
        bool TryDeleteFile(string path);
        long GetAvailableFreeSpaceBytes(string path);
        IList<string> GetUserPrinterConnections(string userSid);
        bool HasPendingSystemReboot();
    }
}
