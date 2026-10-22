using System;
using System.Diagnostics;
using System.IO;
using HalconDotNet;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Services
{
    /// <summary>
    /// 将 牙套.hdev 中的 Halcon 算法封装为可复用的 C# 检测服务。
    ///
    /// 算法流程:
    ///   read_image -> rgb1_to_gray -> median_image -> threshold
    ///   -> closing_rectangle1 -> connection -> select_shape(area 200..16000)
    ///   -> count_obj  => TotalCount  (Number : 工件总数)
    ///
    ///   fill_up -> connection -> select_shape(area/width/height)
    ///   -> select_shape(area 7000..10000) -> count_obj
    ///   => GoodCount (Number1 : 有引脚良品数)
    ///
    ///   for each 良品区域: opening_circle(20) -> difference
    ///   -> connection -> select_shape_std(max_area, 70) => 引脚区域
    /// </summary>
    public sealed class HalconInspectionService
    {
        /// <summary>执行一次完整检测。</summary>
        public InspectionResult Run(string imagePath, InspectionParameters p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("找不到待检测图像: " + imagePath, imagePath);

            var result = new InspectionResult { Timestamp = DateTime.Now, ImagePath = imagePath };
            var sw = Stopwatch.StartNew();

            HObject image = null, gray = null, median = null, region = null, closing = null;
            HObject connected = null, selected = null, fillUp = null, connected8 = null;
            HObject selected12 = null, selected13 = null, emptyObj = null;

            try
            {
                // ---------- 1. 读图 & 预处理 ----------
                HOperatorSet.ReadImage(out image, imagePath);

                HOperatorSet.CountChannels(image, out HTuple channels);
                if (channels.Length > 0 && channels[0].I >= 3)
                    HOperatorSet.Rgb1ToGray(image, out gray);
                else
                    HOperatorSet.CopyImage(image, out gray);

                HOperatorSet.MedianImage(gray, out median, "circle", p.MedianRadius, "mirrored");
                HOperatorSet.Threshold(median, out region, p.ThresholdMin, p.ThresholdMax);

                // ---------- 2. 工件总数 Number ----------
                HOperatorSet.ClosingRectangle1(region, out closing, p.ClosingWidth, p.ClosingHeight);
                HOperatorSet.Connection(closing, out connected);
                HOperatorSet.SelectShape(connected, out selected, "area", "and",
                    p.TotalAreaMin, p.TotalAreaMax);
                HOperatorSet.CountObj(selected, out HTuple number);
                result.TotalCount = number[0].I;

                // ---------- 3. 有引脚良品数 Number1 ----------
                HOperatorSet.FillUp(region, out fillUp);
                HOperatorSet.Connection(fillUp, out connected8);
                HOperatorSet.SelectShape(connected8, out selected12,
                    new HTuple("area", "width", "height"), "and",
                    new HTuple(p.GoodAreaMin, p.MinWidth, p.MinHeight),
                    new HTuple(p.GoodAreaMax, p.MaxWidth, p.MaxHeight));
                HOperatorSet.SelectShape(selected12, out selected13, "area", "and",
                    p.FinalAreaMin, p.FinalAreaMax);
                HOperatorSet.CountObj(selected13, out HTuple number1);
                result.GoodCount = number1[0].I;

                // ---------- 4. 逐区域提取引脚 ----------
                HOperatorSet.GenEmptyObj(out emptyObj);
                for (int i = 1; i <= result.GoodCount; i++)
                {
                    HOperatorSet.SelectObj(selected13, out HObject obj, i);
                    HOperatorSet.OpeningCircle(obj, out HObject opening, p.OpeningRadius);
                    HOperatorSet.Difference(obj, opening, out HObject diff);
                    HOperatorSet.Connection(diff, out HObject conn9);
                    HOperatorSet.SelectShapeStd(conn9, out HObject sel14, "max_area", p.PinArea);
                    HOperatorSet.ConcatObj(emptyObj, sel14, out HObject concat);

                    emptyObj.Dispose();
                    emptyObj = concat;

                    obj.Dispose();
                    opening.Dispose();
                    diff.Dispose();
                    conn9.Dispose();
                    sel14.Dispose();
                }

                HOperatorSet.CountObj(emptyObj, out HTuple pinCount);
                result.PinCount = pinCount[0].I;

                // 移交给结果对象负责释放
                result.TotalRegions = selected; selected = null;
                result.PinRegions = emptyObj; emptyObj = null;
            }
            finally
            {
                SafeDispose(image);
                SafeDispose(gray);
                SafeDispose(median);
                SafeDispose(region);
                SafeDispose(closing);
                SafeDispose(connected);
                SafeDispose(selected);
                SafeDispose(fillUp);
                SafeDispose(connected8);
                SafeDispose(selected12);
                SafeDispose(selected13);
                SafeDispose(emptyObj);
            }

            sw.Stop();
            result.ElapsedMs = sw.Elapsed.TotalMilliseconds;

            // ---------- 5. OK / NG 判定 ----------
            result.IsOk = result.TotalCount > 0 && result.NgCount <= p.MaxNgAllowed;
            result.Message = result.TotalCount == 0
                ? "未检测到工件"
                : (result.IsOk
                    ? "全部工件均含引脚"
                    : string.Format("检测到 {0} 个缺引脚不良", result.NgCount));

            return result;
        }

        private static void SafeDispose(HObject obj)
        {
            if (obj == null) return;
            try { obj.Dispose(); } catch { /* ignore */ }
        }
    }
}
