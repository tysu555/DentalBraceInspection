using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HalconDotNet;
using DentalBraceInspection.Models;
using DentalBraceInspection.Services;
using Microsoft.Win32;

namespace DentalBraceInspection
{
    public partial class MainWindow : Window
    {
        // ---------- 路径 ----------
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string ConfigPath = Path.Combine(BaseDir, "Config", "inspection_config.xml");
        private static readonly string LogDir = Path.Combine(BaseDir, "Logs");

        // ---------- 颜色 ----------
        private static readonly SolidColorBrush OkBrush = Freeze(0x22, 0xC5, 0x5E);
        private static readonly SolidColorBrush NgBrush = Freeze(0xEF, 0x44, 0x44);
        private static readonly SolidColorBrush AmberBrush = Freeze(0xF5, 0x9E, 0x0B);
        private static readonly SolidColorBrush IdleBrush = Freeze(0x8A, 0x97, 0xA6);
        private static readonly SolidColorBrush OkBg = Freeze(0x16, 0x30, 0x22, 0xB0);
        private static readonly SolidColorBrush NgBg = Freeze(0x36, 0x1B, 0x1B, 0xB0);
        private static readonly SolidColorBrush IdleBg = Freeze(0x25, 0x2C, 0x35);

        // ---------- 服务 ----------
        private Logger _logger;
        private HalconInspectionService _service;
        private InspectionController _controller;
        private InspectionParameters _parameters;

        // ---------- 数据 ----------
        private readonly ObservableCollection<HistoryRecord> _history = new ObservableCollection<HistoryRecord>();
        private readonly ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();

        private DispatcherTimer _clock;
        private InspectionResult _lastResult;   // 保留用于窗口重绘
        private int _historyIndex;
        private long _totalInspections;
        private long _totalPieces;
        private long _totalGood;

        public MainWindow()
        {
            InitializeComponent();
            HistoryGrid.ItemsSource = _history;
            LogList.ItemsSource = _logs;
        }

        // ==================================================================
        //  生命周期
        // ==================================================================
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger = new Logger(LogDir);
            _logger.EntryWritten += OnLogWritten;

            _parameters = ConfigStore.Load(ConfigPath);
            _service = new HalconInspectionService();
            _controller = new InspectionController(_service, _logger, CurrentParameters);
            _controller.ResultProduced += OnResultProduced;
            _controller.RunningChanged += OnRunningChanged;
            _controller.StatusChanged += OnStatusChanged;

            ConfigPanel.DataContext = _parameters;
            SyncSourceModeToUi();
            _history.Clear();
            CmbSourceMode.SelectionChanged += CmbSourceMode_SelectionChanged;

            StartClock();
            InitializeHalconWindow();

            _logger.Info("系统启动完成, 版本 v1.0.0");
            _logger.Info("配置文件: " + ConfigPath);

            IImageSource source = ImageSourceFactory.Create(_parameters);
            if (source.HasImages)
                _logger.Success("参数加载完成, 图像源: " + source.Description);
            else
                _logger.Warn("图像源中未发现图像, 请在“参数配置”中设置图像文件夹: " + source.Description);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { _controller?.Stop(); } catch { /* ignore */ }
            try { _lastResult?.Dispose(); } catch { /* ignore */ }
            try { ConfigStore.Save(ConfigPath, _parameters); } catch { /* ignore */ }
        }

        private void StartClock()
        {
            UpdateClock();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, a) => UpdateClock();
            _clock.Start();
        }

        private void UpdateClock()
        {
            DateTime now = DateTime.Now;
            TxtClock.Text = now.ToString("HH:mm:ss");
            TxtDate.Text = now.ToString("yyyy-MM-dd  dddd", CultureInfo.GetCultureInfo("zh-CN"));
        }

        private void InitializeHalconWindow()
        {
            try
            {
                HOperatorSet.SetWindowAttr("background_color", "#0E1216");
            }
            catch { /* 可选设置, 失败无妨 */ }

            bool ready = HalconEnvironment.IsReady;
            TxtHalconStatus.Text = ready
                ? "HALCON 就绪 · " + HalconEnvironment.HalconRoot
                : "HALCON 未就绪, 请检查安装/环境变量";
            EllHalcon.Fill = ready ? OkBrush : NgBrush;

            if (!ready)
                _logger.Error("未找到 HALCON 运行时, 请确认已安装并设置 HALCONROOT");
            else
                _logger.Info("HALCON 运行时: " + HalconEnvironment.RuntimeBinPath);
        }

        // ==================================================================
        //  参数
        // ==================================================================
        private InspectionParameters CurrentParameters()
        {
            // 返回快照, 避免后台线程读取到编辑中的半成品
            return _parameters != null ? _parameters.Clone() : new InspectionParameters();
        }

        private void CmbSourceMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_parameters == null) return;
            _parameters.ImageSourceMode = CmbSourceMode.SelectedIndex == 1 ? "Single" : "Folder";
        }

        private void SyncSourceModeToUi()
        {
            CmbSourceMode.SelectedIndex =
                string.Equals(_parameters.ImageSourceMode, "Single", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private void RefreshConfigBinding()
        {
            ConfigPanel.DataContext = null;
            ConfigPanel.DataContext = _parameters;
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigStore.Save(ConfigPath, _parameters);
                _logger.Success("参数已保存: " + ConfigPath);
            }
            catch (Exception ex)
            {
                _logger.Error("保存参数失败: " + ex.Message);
            }
        }

        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigPath))
            {
                _parameters = ConfigStore.Load(ConfigPath);
                RefreshConfigBinding();
                SyncSourceModeToUi();
                _logger.Success("参数已加载: " + ConfigPath);
            }
            else
            {
                _logger.Warn("未找到配置文件, 使用当前参数");
            }
        }

        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            _parameters = new InspectionParameters();
            RefreshConfigBinding();
            SyncSourceModeToUi();
            _logger.Warn("参数已恢复默认值");
        }

        private void BtnApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_controller.IsRunning)
            {
                _controller.Stop();
                _logger.Warn("参数已应用, 为确保生效请重新启动设备");
            }
            else
            {
                _logger.Success("参数已应用");
            }
            TxtSourceDesc.Text = ImageSourceFactory.Create(_parameters).Description;
        }

        // ==================================================================
        //  设备控制
        // ==================================================================
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            TxtSourceDesc.Text = ImageSourceFactory.Create(_parameters).Description;
            _controller.Start();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _controller.Stop();
        }

        private void BtnManual_Click(object sender, RoutedEventArgs e)
        {
            DoManualTrigger();
        }

        private void BtnTriggerShot_Click(object sender, RoutedEventArgs e)
        {
            DoManualTrigger();
        }

        private void DoManualTrigger()
        {
            try
            {
                InspectionResult result = _controller.TriggerOnce();
                if (result != null) DisplayResult(result);
            }
            catch (Exception ex)
            {
                _logger.Error("手动触发失败: " + ex.Message);
            }
        }

        // ==================================================================
        //  图像显示
        // ==================================================================
        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择待检测图像",
                Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|所有文件|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;

            string path = dialog.FileName;
            try
            {
                InspectionResult result = _service.Run(path, CurrentParameters());
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                _logger.Error("打开图像检测失败: " + ex.Message);
                ShowImageOnly(path);
            }
        }

        private void BtnFitWindow_Click(object sender, RoutedEventArgs e)
        {
            RedrawHalconWindow();
        }

        private void ShowImageOnly(string path)
        {
            try
            {
                if (_lastResult != null) { _lastResult.Dispose(); _lastResult = null; }
                HWindow.HalconWindow.ClearWindow();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                HOperatorSet.ReadImage(out HObject image, path);
                FitImage(image);
                HWindow.HalconWindow.DispObj(image);
                image.Dispose();
                TxtImageName.Text = Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                _logger.Error("显示图像失败: " + ex.Message);
            }
        }

        private void RedrawHalconWindow()
        {
            if (_lastResult == null || string.IsNullOrEmpty(_lastResult.ImagePath)) return;
            if (!File.Exists(_lastResult.ImagePath)) return;

            HObject image = null;
            try
            {
                HWindow.HalconWindow.ClearWindow();
                HOperatorSet.ReadImage(out image, _lastResult.ImagePath);
                FitImage(image);
                HWindow.HalconWindow.DispObj(image);

                if (_lastResult.TotalRegions != null)
                {
                    HWindow.HalconWindow.SetColor("yellow");
                    HWindow.HalconWindow.SetDraw("margin");
                    HWindow.HalconWindow.SetLineWidth(2);
                    HWindow.HalconWindow.DispObj(_lastResult.TotalRegions);
                }
                if (_lastResult.PinRegions != null)
                {
                    HWindow.HalconWindow.SetColor("green");
                    HWindow.HalconWindow.SetDraw("fill");
                    HWindow.HalconWindow.DispObj(_lastResult.PinRegions);
                    HWindow.HalconWindow.SetDraw("margin");
                    HWindow.HalconWindow.SetColor("lime green");
                    HWindow.HalconWindow.SetLineWidth(3);
                    HWindow.HalconWindow.DispObj(_lastResult.PinRegions);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("窗口重绘失败: " + ex.Message);
            }
            finally
            {
                if (image != null) image.Dispose();
            }
        }

        private void FitImage(HObject image)
        {
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            HWindow.HalconWindow.SetPart(0, 0, height[0].I - 1, width[0].I - 1);
        }

        // ==================================================================
        //  结果处理
        // ==================================================================
        private void OnResultProduced(InspectionResult result)
        {
            Dispatcher.BeginInvoke(new Action(() => DisplayResult(result)));
        }

        private void DisplayResult(InspectionResult result)
        {
            if (result == null) return;
            try
            {
                // 更新数值显示
                TxtTotal.Text = result.TotalCount.ToString();
                TxtGood.Text = result.GoodCount.ToString();
                TxtNg.Text = result.NgCount.ToString();
                TxtPin.Text = result.PinCount.ToString();
                TxtElapsed.Text = result.ElapsedMs.ToString("F1") + " ms";
                TxtImageName.Text = string.IsNullOrEmpty(result.ImagePath)
                    ? "-" : Path.GetFileName(result.ImagePath);

                // 本次良率
                double yield = result.TotalCount > 0 ? result.GoodCount * 100.0 / result.TotalCount : 0.0;
                TxtYield.Text = result.TotalCount > 0 ? yield.ToString("F1") + "%" : "--";
                TxtYield.Foreground = yield >= 99.999 ? OkBrush : AmberBrush;

                // OK / NG 判定横幅
                if (result.IsOk)
                {
                    TxtVerdict.Text = "OK";
                    TxtVerdict.Foreground = OkBrush;
                    TxtVerdictSub.Text = result.Message ?? "全部工件均含引脚";
                    BrdVerdict.BorderBrush = OkBrush;
                    BrdVerdict.Background = OkBg;
                }
                else
                {
                    TxtVerdict.Text = "NG";
                    TxtVerdict.Foreground = NgBrush;
                    TxtVerdictSub.Text = result.Message ?? "存在缺引脚不良";
                    BrdVerdict.BorderBrush = NgBrush;
                    BrdVerdict.Background = NgBg;
                }

                // 更新显示对象 (释放上一帧)
                if (_lastResult != null) _lastResult.Dispose();
                _lastResult = result;

                RedrawHalconWindow();

                // 历史记录
                _historyIndex++;
                _history.Insert(0, HistoryRecord.FromResult(_historyIndex, result));
                if (_history.Count > 2000) _history.RemoveAt(_history.Count - 1);

                _totalInspections++;
                _totalPieces += result.TotalCount;
                _totalGood += result.GoodCount;
                UpdateSummary();

                // 日志
                if (result.IsOk)
                    _logger.Success(string.Format("检测 OK · 总数 {0} · 良品 {1} · 引脚 {2} · 良率 {3:F1}% · {4:F1}ms",
                        result.TotalCount, result.GoodCount, result.PinCount, yield, result.ElapsedMs));
                else
                    _logger.Warn(string.Format("检测 NG · 总数 {0} · 良品 {1} · 不良 {2} · 良率 {3:F1}% · {4:F1}ms",
                        result.TotalCount, result.GoodCount, result.NgCount, yield, result.ElapsedMs));
            }
            catch (Exception ex)
            {
                _logger.Error("结果刷新异常: " + ex.Message);
                result.Dispose();
            }
        }

        private void UpdateSummary()
        {
            double yield = _totalPieces > 0 ? _totalGood * 100.0 / _totalPieces : 0.0;

            TxtYieldTotal.Text = _totalPieces > 0 ? yield.ToString("F1") + "%" : "--";
            TxtYieldTotal.Foreground = yield >= 99.999 ? OkBrush : AmberBrush;

            TxtSummary.Text = string.Format("累计 {0} 次 · 工件 {1} · 良品 {2} · 良率 {3:F1}%",
                _totalInspections, _totalPieces, _totalGood, yield);
        }

        // ==================================================================
        //  控制器事件
        // ==================================================================
        private void OnRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EllStatus.Fill = running ? OkBrush : IdleBrush;
                BtnStart.IsEnabled = !running;
                BtnStop.IsEnabled = running;
                BtnManual.IsEnabled = !running;
            }));
        }

        private void OnStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtRunStatus.Text = status;
                TxtStatusMsg.Text = "系统状态: " + status;
            }));
        }

        // ==================================================================
        //  日志
        // ==================================================================
        private void OnLogWritten(LogEntry entry)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _logs.Insert(0, entry);
                if (_logs.Count > 1000) _logs.RemoveAt(_logs.Count - 1);
                if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[0]);
            }));
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
        }

        private void BtnOpenLogDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                Process.Start("explorer.exe", "\"" + LogDir + "\"");
            }
            catch (Exception ex)
            {
                _logger.Error("打开日志目录失败: " + ex.Message);
            }
        }

        // ==================================================================
        //  历史数据
        // ==================================================================
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            _historyIndex = 0;
            _totalInspections = 0;
            _totalPieces = 0;
            _totalGood = 0;
            TxtYield.Text = "--";
            UpdateSummary();
            _logger.Info("历史数据已清空");
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_history.Count == 0)
            {
                MessageBox.Show(this, "暂无历史数据可导出。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出历史数据",
                Filter = "CSV 文件|*.csv",
                FileName = "InspectionHistory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("序号,时间,图像,工件总数,良品数,不良数,引脚数,结果,耗时(ms)");
                foreach (HistoryRecord r in _history)
                {
                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                        r.Index, r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        r.ImageName, r.TotalCount, r.GoodCount, r.NgCount, r.PinCount, r.Result, r.ElapsedMs));
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                _logger.Success("历史数据已导出: " + dialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.Error("导出失败: " + ex.Message);
            }
        }

        // ==================================================================
        //  工具
        // ==================================================================
        private static SolidColorBrush Freeze(byte r, byte g, byte b, byte a = 0xFF)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
