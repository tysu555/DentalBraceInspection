using System;
using System.IO;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Services
{
    /// <summary>
    /// 运行日志: 写入按天滚动的文本文件, 同时通过事件通知 UI。
    /// </summary>
    public sealed class Logger
    {
        private readonly object _sync = new object();
        private readonly string _logDirectory;

        public event Action<LogEntry> EntryWritten;

        public Logger(string logDirectory)
        {
            _logDirectory = logDirectory;
            try { Directory.CreateDirectory(_logDirectory); } catch { /* ignore */ }
        }

        public void Info(string message) { Write(LogLevel.Info, message); }
        public void Success(string message) { Write(LogLevel.Success, message); }
        public void Warn(string message) { Write(LogLevel.Warning, message); }
        public void Error(string message) { Write(LogLevel.Error, message); }

        public void Write(LogLevel level, string message)
        {
            var entry = new LogEntry { Timestamp = DateTime.Now, Level = level, Message = message };

            lock (_sync)
            {
                try
                {
                    string file = Path.Combine(_logDirectory, entry.Timestamp.ToString("yyyy-MM-dd") + ".log");
                    string line = string.Format("[{0}] [{1}] {2}",
                        entry.Timestamp.ToString("HH:mm:ss.fff"), entry.LevelText, message);
                    File.AppendAllText(file, line + Environment.NewLine);
                }
                catch { /* 日志写盘失败不应影响产线 */ }
            }

            var handler = EntryWritten;
            if (handler != null) handler(entry);
        }
    }
}
