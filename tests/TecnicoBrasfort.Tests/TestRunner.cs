using System;
using System.Collections.Generic;
using System.Threading;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.Tests
{
    public static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(string.Format("Assert.Equal falhou. Esperado: '{0}', Atual: '{1}'", expected, actual));
            }
        }

        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assert.True falhou: " + message);
            }
        }

        public static void False(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception("Assert.False falhou: " + message);
            }
        }

        public static void Throws<TException>(Action action, string message) where TException : Exception
        {
            bool caught = false;
            try
            {
                action();
            }
            catch (TException)
            {
                caught = true;
            }
            if (!caught)
            {
                throw new Exception("Assert.Throws<" + typeof(TException).Name + "> falhou: " + message);
            }
        }
    }

    public class RecordedInvocation
    {
        public string FileName { get; set; }
        public string[] Args { get; set; }
        public int TimeoutMs { get; set; }
        public bool UseUnicodeOutput { get; set; }
    }

    public class FakeProcessRunner : IProcessRunner
    {
        public readonly List<RecordedInvocation> Invocations = new List<RecordedInvocation>();
        public int NextExitCode = 0;
        public string NextStdout = "";

        public ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput)
        {
            return Run(fileName, args, timeoutMs, onOutput, onError, useUnicodeOutput, CancellationToken.None);
        }

        public ProcessResult Run(
            string fileName,
            string[] args,
            int timeoutMs,
            Action<string> onOutput,
            Action<string> onError,
            bool useUnicodeOutput,
            CancellationToken cancellationToken)
        {
            RecordedInvocation inv = new RecordedInvocation();
            inv.FileName = fileName;
            inv.Args = args;
            inv.TimeoutMs = timeoutMs;
            inv.UseUnicodeOutput = useUnicodeOutput;
            Invocations.Add(inv);

            ProcessResult res = new ProcessResult();
            if (cancellationToken.IsCancellationRequested)
            {
                res.ExitCode = -1;
                res.Cancelled = true;
                res.Summary = "Fake cancelled";
                return res;
            }
            res.ExitCode = NextExitCode;
            res.StandardOutput = NextStdout;
            res.Summary = "Fake result";
            return res;
        }
    }

    public class FakeHostEnvironment : IHostEnvironment
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, IList<string>> PrinterConnections = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
        public long FreeSpaceBytes = 10L * 1024L * 1024L * 1024L;
        public string ThrowOnDirectoryPrefix = "";
        public bool PendingSystemReboot = false;

        public bool FileExists(string path)
        {
            return Files.ContainsKey(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directories.Contains(path);
        }

        public void CreateDirectory(string path)
        {
            if (!string.IsNullOrEmpty(ThrowOnDirectoryPrefix) && path.StartsWith(ThrowOnDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Acesso negado simulado.");
            }
            Directories.Add(path);
        }

        public string ReadAllText(string path)
        {
            return Files[path];
        }

        public void AppendAllText(string path, string content)
        {
            if (Files.ContainsKey(path))
            {
                Files[path] = Files[path] + content;
            }
            else
            {
                Files[path] = content;
            }
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            List<string> matches = new List<string>();
            foreach (string key in Files.Keys)
            {
                if (key.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(key);
                }
            }
            return matches;
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return new string[0];
        }

        public bool TryDeleteFile(string path)
        {
            return Files.Remove(path);
        }

        public readonly List<string> RestoredServiceProfiles = new List<string>();
        public readonly List<string> MovedPaths = new List<string>();

        public bool TryMoveFile(string sourcePath, string destinationPath)
        {
            string content;
            if (!Files.TryGetValue(sourcePath, out content))
            {
                return false;
            }
            Files.Remove(sourcePath);
            Files[destinationPath] = content;
            MovedPaths.Add(sourcePath + " -> " + destinationPath);
            return true;
        }

        public bool TryMoveDirectory(string sourcePath, string destinationPath)
        {
            if (!Directories.Contains(sourcePath))
            {
                return false;
            }
            Directories.Remove(sourcePath);
            Directories.Add(destinationPath);
            MovedPaths.Add(sourcePath + " -> " + destinationPath);
            return true;
        }

        public IList<string> EnsureNativeServiceProfilesPresent()
        {
            return new List<string>(RestoredServiceProfiles);
        }

        public long GetAvailableFreeSpaceBytes(string path)
        {
            return FreeSpaceBytes;
        }

        public IList<string> GetUserPrinterConnections(string userSid)
        {
            if (PrinterConnections.ContainsKey(userSid))
            {
                return PrinterConnections[userSid];
            }
            return new List<string>();
        }

        public bool HasPendingSystemReboot()
        {
            return PendingSystemReboot;
        }
    }

    public class FakeExecutionObserver : IExecutionObserver
    {
        public readonly List<string> Outputs = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<ActionDefinition> StartedActions = new List<ActionDefinition>();
        public readonly List<ActionExecutionResult> CompletedResults = new List<ActionExecutionResult>();

        public void OnOutput(string line)
        {
            Outputs.Add(line);
        }

        public void OnError(string line)
        {
            Errors.Add(line);
        }

        public void OnStepStarted(int stepIndex, int totalSteps, ActionDefinition action)
        {
            StartedActions.Add(action);
        }

        public void OnStepCompleted(int stepIndex, int totalSteps, ActionExecutionResult result)
        {
            CompletedResults.Add(result);
        }
    }

    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("=== TecnicoBrasfort.Tests (C# 5.0 Native Runner) ===");

            CoreTests.Register(RunTest);
            ModulesTests.Register(RunTest);
            V2CoreTests.Register(RunTest);

            Console.WriteLine(string.Format("\nResumo: {0} passou(aram), {1} falhou(aram).", _passed, _failed));
            return _failed == 0 ? 0 : 1;
        }

        private static void RunTest(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name + " -> " + ex.Message);
            }
        }
    }
}
