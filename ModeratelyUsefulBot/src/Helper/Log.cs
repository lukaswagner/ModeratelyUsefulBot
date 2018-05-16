using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot
{
    internal static class Log
    {
        internal enum LogLevel
        {
            Verbose, Debug, Info, Warn, Error, Off
        }

        private class LogEntry
        {
            internal readonly LogLevel LogLevel;
            private readonly string _tag;
            private readonly string _message;
            private readonly string _time;

            internal LogEntry(LogLevel logLevel, string tag, string message)
            {
                LogLevel = logLevel;
                _tag = tag;
                _message = message;
                _time = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            }

            internal void LogToConsole()
            {
                if (_changeConsoleColor(LogLevel, out var consoleColor))
                {
                    Console.ForegroundColor = consoleColor;
                }
                Console.WriteLine(_getLogString());
                Console.ResetColor();
            }

            internal void LogToFile(StreamWriter writer)
            {
                writer.WriteLine(_getLogString());
                writer.Flush();
            }

            private string _getLogString()
            {
                return _getLogLevelString(LogLevel) + (LogTimes ? "[" + _time + "] " : " ") + _tag.PadRight(TagLength).Substring(0, TagLength) + " : " + _message;
            }

            private static string _getLogLevelString(LogLevel logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.Verbose:
                        return "[Vrb]";
                    case LogLevel.Debug:
                        return "[Dbg]";
                    case LogLevel.Info:
                        return "[Inf]";
                    case LogLevel.Warn:
                        return "[Wrn]";
                    case LogLevel.Error:
                        return "[Err]";
                    case LogLevel.Off:
                        return "[Off]";
                    default:
                        return "[N/A]";
                }
            }

            private static bool _changeConsoleColor(LogLevel logLevel, out ConsoleColor consoleColor)
            {
                consoleColor = ConsoleColor.White;
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (logLevel)
                {
                    case LogLevel.Warn:
                        consoleColor = ConsoleColor.Yellow;
                        return true;
                    case LogLevel.Error:
                        consoleColor = ConsoleColor.Red;
                        return true;
                    default:
                        return false;
                }
            }
        }

        private const string Tag = "Log";
        private static readonly BlockingCollection<LogEntry> Queue = new BlockingCollection<LogEntry>();
        private static readonly Thread LogLoopThread = new Thread(_logLoop);
        private static bool _runLogLoop = true;
        private static readonly object LogLoopLock = new object();
        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static LogLevel _consoleLevel = LogLevel.Off;
        private static LogLevel _fileLevel = LogLevel.Off;
        private static LogLevel _level = LogLevel.Off;
        internal static int TagLength = 15;
        internal static bool LogTimes = Config.GetDefault("log/logTimes", true);
        internal static string FilePath { get; private set; }

        private static void _exitHandler(object sender, EventArgs e) => Disable();

        private static void _logLoop()
        {
            var cancellationToken = CancellationTokenSource.Token;
            LogEntry entry;

            FileStream fileStream = null;
            StreamWriter writer = null;
            if (_fileLevel < LogLevel.Off)
            {
                FilePath = "log/log_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt";
                var fileInfo = new FileInfo(FilePath);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    Directory.CreateDirectory(fileInfo.DirectoryName);
                }
                fileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                writer = new StreamWriter(fileStream);
            }

            try
            {
                while (true)
                {
                    lock (LogLoopLock)
                    {
                        if (!_runLogLoop)
                        {
                            return;
                        }
                    }
                    entry = Queue.Take(cancellationToken);
                    if (entry.LogLevel >= _consoleLevel)
                        entry.LogToConsole();
                    if (entry.LogLevel >= _fileLevel)
                        entry.LogToFile(writer);
                }
            }
            catch (OperationCanceledException)
            {
                entry = new LogEntry(LogLevel.Info, Tag, "Stopped output.");
                if (entry.LogLevel >= _consoleLevel)
                    entry.LogToConsole();
                if (entry.LogLevel >= _fileLevel)
                    entry.LogToFile(writer);
            }

            if (writer != null)
            {
                writer.Close();
                writer.Dispose();
            }

            if (fileStream == null)
                return;

            fileStream.Close();
            fileStream.Dispose();
        }

        private static void _add(LogLevel logLevel, string tag, string message)
        {
            if (logLevel >= _level)
                Queue.Add(new LogEntry(logLevel, tag, message));
        }

        internal static void Enable(LogLevel consoleLevel, LogLevel fileLevel)
        {
            _consoleLevel = consoleLevel;
            _fileLevel = fileLevel;
            _level = fileLevel < consoleLevel ? fileLevel : consoleLevel;
            if (LogLoopThread.IsAlive || _level >= LogLevel.Off)
                return;
            LogLoopThread.Start();
            AppDomain.CurrentDomain.ProcessExit += _exitHandler;
        }

        internal static void Enable(string consoleLevelString, string fileLevelString = "Off")
        {
            LogLevel consoleLevel;
            try
            {
                consoleLevel = (LogLevel)Enum.Parse(typeof(LogLevel), consoleLevelString);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException
                    || ex is ArgumentException
                    || ex is OverflowException)
                    consoleLevel = LogLevel.Info;
                else
                    throw;
            }

            LogLevel fileLevel;
            try
            {
                fileLevel = (LogLevel)Enum.Parse(typeof(LogLevel), fileLevelString);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException
                    || ex is ArgumentException
                    || ex is OverflowException)
                    fileLevel = LogLevel.Info;
                else
                    throw;
            }

            Enable(consoleLevel, fileLevel);
        }

        internal static void Disable()
        {
            _level = LogLevel.Off;
            lock (LogLoopLock)
            {
                _runLogLoop = false;
            }
            CancellationTokenSource.Cancel();
            LogLoopThread.Join();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static void Verbose(string tag, string message) => _add(LogLevel.Verbose, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static void Debug(string tag, string message) => _add(LogLevel.Debug, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static void Info(string tag, string message) => _add(LogLevel.Info, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static void Warn(string tag, string message) => _add(LogLevel.Warn, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static void Error(string tag, string message) => _add(LogLevel.Error, tag, message);
    }
}
