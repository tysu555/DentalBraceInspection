using System;
using HalconDotNet;

namespace DentalBraceInspection.Models
{
    /// <summary>
    /// 一次检测的结果。包含计数值以及供 Halcon 窗口显示的 HObject 区域。
    /// </summary>
    public sealed class InspectionResult : IDisposable
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string ImagePath { get; set; }

        /// <summary>工件总数 (Halcon: Number)</summary>
        public int TotalCount { get; set; }

        /// <summary>有引脚 / 良品个数 (Halcon: Number1)</summary>
        public int GoodCount { get; set; }

        /// <summary>实际提取到的引脚数量</summary>
        public int PinCount { get; set; }

        /// <summary>不良数 = 总数 - 良品数</summary>
        public int NgCount
        {
            get { return Math.Max(0, TotalCount - GoodCount); }
        }

        /// <summary>OK / NG 判定结果</summary>
        public bool IsOk { get; set; }

        /// <summary>单次检测耗时 (ms)</summary>
        public double ElapsedMs { get; set; }

        /// <summary>附加信息 (异常 / 提示)</summary>
        public string Message { get; set; }

        // ---- 供 Halcon 显示的区域 (由服务创建, UI 负责释放) ----
        public HObject TotalRegions { get; set; }
        public HObject PinRegions { get; set; }

        public string ResultText
        {
            get { return IsOk ? "OK" : "NG"; }
        }

        public void Dispose()
        {
            SafeDispose(TotalRegions);
            SafeDispose(PinRegions);
            TotalRegions = null;
            PinRegions = null;
        }

        private static void SafeDispose(HObject obj)
        {
            if (obj == null) return;
            try { obj.Dispose(); } catch { /* ignore */ }
        }
    }
}
