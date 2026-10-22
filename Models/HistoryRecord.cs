using System;

namespace DentalBraceInspection.Models
{
    /// <summary>
    /// 历史数据表格的一行记录 (不含 Halcon 对象, 可安全长期保存)。
    /// </summary>
    public sealed class HistoryRecord
    {
        public int Index { get; set; }

        public DateTime Timestamp { get; set; }

        public string ImageName { get; set; }

        /// <summary>工件总数 (Number)</summary>
        public int TotalCount { get; set; }

        /// <summary>有引脚良品数 (Number1)</summary>
        public int GoodCount { get; set; }

        /// <summary>不良数</summary>
        public int NgCount { get; set; }

        /// <summary>引脚数</summary>
        public int PinCount { get; set; }

        /// <summary>OK / NG</summary>
        public string Result { get; set; }

        public double ElapsedMs { get; set; }

        public string TimeText
        {
            get { return Timestamp.ToString("HH:mm:ss.fff"); }
        }

        public static HistoryRecord FromResult(int index, InspectionResult result)
        {
            return new HistoryRecord
            {
                Index = index,
                Timestamp = result.Timestamp,
                ImageName = string.IsNullOrEmpty(result.ImagePath)
                    ? "" : System.IO.Path.GetFileName(result.ImagePath),
                TotalCount = result.TotalCount,
                GoodCount = result.GoodCount,
                NgCount = result.NgCount,
                PinCount = result.PinCount,
                Result = result.ResultText,
                ElapsedMs = Math.Round(result.ElapsedMs, 1)
            };
        }
    }
}
