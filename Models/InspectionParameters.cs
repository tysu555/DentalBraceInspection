using System;

namespace DentalBraceInspection.Models
{
    /// <summary>
    /// 检测参数配置 —— 与 Halcon 算法 (牙套.hdev) 一一对应, 支持持久化到 XML。
    /// </summary>
    [Serializable]
    public class InspectionParameters
    {
        // ---------- 图像预处理 ----------
        /// <summary>中值滤波半径 (median_image radius)</summary>
        public int MedianRadius { get; set; } = 3;

        /// <summary>二值化阈值下限 (threshold min)</summary>
        public double ThresholdMin { get; set; } = 0;

        /// <summary>二值化阈值上限 (threshold max)</summary>
        public double ThresholdMax { get; set; } = 125;

        // ---------- 计数工位 (Number = 工件总数) ----------
        /// <summary>闭运算矩形宽 (closing_rectangle1 width)</summary>
        public int ClosingWidth { get; set; } = 40;

        /// <summary>闭运算矩形高 (closing_rectangle1 height)</summary>
        public int ClosingHeight { get; set; } = 40;

        /// <summary>总数筛选面积下限 (select_shape area min)</summary>
        public double TotalAreaMin { get; set; } = 200;

        /// <summary>总数筛选面积上限 (select_shape area max)</summary>
        public double TotalAreaMax { get; set; } = 16000;

        // ---------- 良品工位 (Number1 = 有引脚良品数) ----------
        /// <summary>良品预筛选面积下限</summary>
        public double GoodAreaMin { get; set; } = 150;

        /// <summary>良品预筛选宽度下限</summary>
        public double MinWidth { get; set; } = 50;

        /// <summary>良品预筛选高度下限</summary>
        public double MinHeight { get; set; } = 50;

        /// <summary>良品预筛选面积上限</summary>
        public double GoodAreaMax { get; set; } = 10000;

        /// <summary>良品预筛选宽度上限</summary>
        public double MaxWidth { get; set; } = 150;

        /// <summary>良品预筛选高度上限</summary>
        public double MaxHeight { get; set; } = 150;

        /// <summary>良品终筛选面积下限 (select_shape 7000)</summary>
        public double FinalAreaMin { get; set; } = 7000;

        /// <summary>良品终筛选面积上限 (select_shape 10000)</summary>
        public double FinalAreaMax { get; set; } = 10000;

        /// <summary>引脚提取开运算半径 (opening_circle 20)</summary>
        public double OpeningRadius { get; set; } = 20;

        /// <summary>引脚最大面积 (select_shape_std max_area 70)</summary>
        public double PinArea { get; set; } = 70;

        // ---------- 运行 / 设备 ----------
        /// <summary>图像来源: Folder / Single</summary>
        public string ImageSourceMode { get; set; } = "Folder";

        /// <summary>连续检测图像文件夹 (默认指向生成的测试图)</summary>
        public string ImageFolder { get; set; } = @"C:\Users\32271\Desktop\牙套上位机界面\Image\generated";

        /// <summary>单张图像路径 (默认指向生成的第一张图)</summary>
        public string SingleImagePath { get; set; } = @"C:\Users\32271\Desktop\牙套上位机界面\Image\generated\brace_06.bmp";

        /// <summary>图像搜索通配符</summary>
        public string SearchPattern { get; set; } = "*.jpg;*.jpeg;*.png;*.bmp";

        /// <summary>单次检测节拍 (ms)</summary>
        public int CycleDelayMs { get; set; } = 500;

        /// <summary>允许的 NG 数量 (0 = 全部良品才算 OK)</summary>
        public int MaxNgAllowed { get; set; } = 0;

        /// <summary>连续运行模式, 否则单次触发</summary>
        public bool ContinuousMode { get; set; } = true;

        public InspectionParameters Clone()
        {
            return (InspectionParameters)MemberwiseClone();
        }
    }
}
