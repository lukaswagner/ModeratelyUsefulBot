using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ModeratelyUsefulBot
{
    internal static class Log
    {
        public enum LogLevel
        {
            Verbose, Debug, Info, Warn, Error, Off
        }

        private class LogEntry
        {
            public LogLevel LogLevel;
            public string Tag;
            public string Message;
            public string Time;

            public LogEntry(LogLevel logLevel, string tag, string message)
            {
                LogLevel = logLevel;
                Tag = tag;
                Message = message;
                Time = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            }

            public void LogToConsole()
            {
                if (_changeConsoleColor(LogLevel, out var consoleColor))
                {
                    Console.ForegroundColor = consoleColor;
                }
                Console.WriteLine(_getLogLevelString(LogLevel) + (LogTimes ? "[" + Time + "] " : " ") + Tag.PadRight(TagLength).Substring(0, TagLength) + " : " + Message);
                Console.ResetColor();
            }

            public void LogToFile(StreamWriter writer)
            {
                writer.WriteLine(_getLogLevelString(LogLevel) + " " + Tag.PadRight(TagLength).Substring(0, TagLength) + " : " + Message);
                writer.Flush();
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
                    default:
                        return "[N/A]";
                }
            }

            private static bool _changeConsoleColor(LogLevel logLevel, out ConsoleColor consoleColor)
            {
                consoleColor = ConsoleColor.White;
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

        private static string _tag = "Log";
        private static BlockingCollection<LogEntry> _queue = new BlockingCollection<LogEntry>();
        private static Thread _logLoopThread = new Thread(_logLoop);
        private static bool _runLogLoop = true;
        private static object _logLoopLock = new object();
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static LogLevel _consoleLevel = LogLevel.Off;
        private static LogLevel _fileLevel = LogLevel.Off;
        private static LogLevel _level = LogLevel.Off;
        public static int TagLength = 15;
        public static bool LogTimes = Config.GetDefault("log/logTimes", true);

        private static void _exitHandler(object sender, EventArgs e) => Disable();

        private static void _logLoop()
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            LogEntry entry;

            StreamWriter writer = null;
            if (_fileLevel < LogLevel.Off)
            {
                var filePath = "log/log_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt";
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Directory.Exists)
                {
                    System.IO.Directory.CreateDirectory(fileInfo.DirectoryName);
                }
                writer = new StreamWriter(filePath);
            }

            try
            {
                while (true)
                {
                    lock (_logLoopLock)
                    {
                        if (!_runLogLoop)
                        {
                            return;
                        }
                    }
                    entry = _queue.Take(cancellationToken);
                    if (entry.LogLevel >= _consoleLevel)
                        entry.LogToConsole();
                    if (entry.LogLevel >= _fileLevel)
                        entry.LogToFile(writer);
                }
            }
            catch (OperationCanceledException)
            {
                entry = new LogEntry(LogLevel.Info, _tag, "Stopped output.");
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
        }

        private static void _add(LogLevel logLevel, string tag, string message)
        {
            if (logLevel >= _level)
                _queue.Add(new LogEntry(logLevel, tag, message));
        }

        public static void Enable(LogLevel consoleLevel, LogLevel fileLevel)
        {
            _consoleLevel = consoleLevel;
            _fileLevel = fileLevel;
            _level = fileLevel < consoleLevel ? fileLevel : consoleLevel;
            if (!_logLoopThread.IsAlive && _level < LogLevel.Off)
            {
                _logLoopThread.Start();
                AppDomain.CurrentDomain.ProcessExit += _exitHandler;
            }
        }

        public static void Enable(string consoleLevelString, string fileLevelString = "Off")
        {
            LogLevel consoleLevel;
            try
            {
                consoleLevel = (LogLevel)System.Enum.Parse(typeof(LogLevel), consoleLevelString);
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
                fileLevel = (LogLevel)System.Enum.Parse(typeof(LogLevel), fileLevelString);
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

        public static void Disable()
        {
            _level = LogLevel.Off;
            lock (_logLoopLock)
            {
                _runLogLoop = false;
            }
            _cancellationTokenSource.Cancel();
            _logLoopThread.Join();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static void Verbose(string tag, string message) => _add(LogLevel.Verbose, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static void Debug(string tag, string message) => _add(LogLevel.Debug, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static void Info(string tag, string message) => _add(LogLevel.Info, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static void Warn(string tag, string message) => _add(LogLevel.Warn, tag, message);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static void Error(string tag, string message) => _add(LogLevel.Error, tag, message);
    }
}
