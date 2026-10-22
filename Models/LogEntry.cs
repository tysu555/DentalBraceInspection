using System;

namespace DentalBraceInspection.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 运行日志条目。
    /// </summary>
    public sealed class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public LogLevel Level { get; set; }

        public string Message { get; set; }

        public string TimeText
        {
            get { return Timestamp.ToString("HH:mm:ss"); }
        }

        public string LevelText
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Success: return "成功";
                    case LogLevel.Warning: return "警告";
                    case LogLevel.Error: return "错误";
                    default: return "信息";
                }
            }
        }
    }
}
