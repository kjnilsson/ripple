using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FubuCore;
using FubuCore.Util;
using ripple.Model;

namespace ripple
{
    public static class RippleFileSystem
    {
        private static Func<string> _currentDir;
        private static Func<string, bool> _stopAt;

        static RippleFileSystem()
        {
            Live();
        }

        public static void Live()
        {
            _currentDir = () => Environment.CurrentDirectory;
            _stopAt = dir => false;
        }

        public static void StubCurrentDirectory(string directory)
        {
            _currentDir = () => directory;
        }

        public static void StopTraversingAt(string directory)
        {
            _stopAt = dir => dir.ToFullPath() == directory.ToFullPath();
        }

        public static string FindSolutionDirectory()
        {
            return findSolutionDir(_currentDir());
        }

        public static bool IsSolutionDirectory()
        {
            return findSolutionDir(_currentDir(), false).IsNotEmpty();
        }

        public static bool IsCodeDirectory()
        {
            if (IsSolutionDirectory()) return false;

            // We only traverse one level here
            var directory = new DirectoryInfo(_currentDir());
            var children = directory.GetDirectories();

            return children.Any(x => File.Exists(Path.Combine(x.FullName, SolutionFiles.ConfigFile)));
        }

        private static string findSolutionDir(string path, bool shouldThrow = true)
        {
            if (_stopAt(path)) return null;

            var files = new FileSystem();
            if (files.FileExists(path, SolutionFiles.ConfigFile))
            {
                return path;
            }

            var parent = new DirectoryInfo(path).Parent;
            if (parent == null)
            {
                if (shouldThrow) RippleAssert.Fail("Not a ripple repository (or any of the parent directories)");
                return null;
            }

            return findSolutionDir(parent.FullName);
        }

        public static string FindCodeDirectory()
        {
            if (IsCodeDirectory())
            {
                return _currentDir().ToFullPath();
            }

            var slnDir = FindSolutionDirectory();
            if (slnDir == null) return null;

            var parent = new DirectoryInfo(slnDir).Parent;
            if (parent == null) return null;

            return parent.FullName;
        }

        public static string RippleLogsDirectory()
        {
            var location = FindSolutionDirectory();
            return location.AppendPath("logs");
        }

        public static void CleanWithTracing(this IFileSystem system, string directory)
        {
            RippleLog.Info("Cleaning contents of directory " + directory);
            system.CleanDirectory(directory);
        }

        public static string ParentDirectory(this string path)
        {
            return Path.GetDirectoryName(path);
        }

        public static string ToLogPath(this string filename)
        {
            return RippleLogsDirectory().AppendPath(filename);
        }

        public static void OpenLogFile(this IFileSystem fileSystem, string filename)
        {
            if (!filename.EndsWith(".log"))
            {
                filename += ".log";
            }

            var path = filename.ToLogPath();

            if (fileSystem.FileExists(path))
            {
                fileSystem.LaunchEditor(path);
            }
            else
            {
                Console.WriteLine("File {0} does not exist", path);
            }
        }

        public static void WriteLogFile(this IFileSystem fileSystem, string filename, string contents)
        {
            var logsDirectory = RippleLogsDirectory();
            fileSystem.CreateDirectory(logsDirectory);
            fileSystem.WriteStringToFile(logsDirectory.AppendPath(filename), contents);
        }

        private static readonly object LogLock = new object();

        public static void AppendToLogFile(this IFileSystem fileSystem, string filename, string contents)
        {
            lock (LogLock)
            {
                var logsDirectory = RippleLogsDirectory();
                fileSystem.CreateDirectory(logsDirectory);

                fileSystem.AppendStringToFile(logsDirectory.AppendPath(filename), contents);
            }
        }

        public static string LocationOfRunner(string file)
        {
            var folder = FindSolutionDirectory();
            var system = new FileSystem();

            if (system.FileExists(folder.AppendPath(file)))
            {
                return folder.AppendPath(file).ToFullPath();
            }

            return FindCodeDirectory().AppendPath("ripple", file);
        }

        public static string RakeRunnerFile()
        {
            return LocationOfRunner("run-rake.cmd");
        }

        public static string LocalNugetDirectory()
        {
            return FindCodeDirectory().AppendPath("nugets");
        }

        public static string RippleExeLocation()
        {
            return Assembly.GetExecutingAssembly().Location;
        }
    }
}