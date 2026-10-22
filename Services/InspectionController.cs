using System;
using System.Threading;
using System.Threading.Tasks;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Services
{
    /// <summary>
    /// 检测流程控制器: 负责"设备启动/停止"以及连续取图 -> 调用 Halcon 算法 -> 抛出结果。
    /// </summary>
    public sealed class InspectionController
    {
        private readonly HalconInspectionService _service;
        private readonly Logger _logger;
        private readonly Func<InspectionParameters> _getParameters;
        private readonly object _sync = new object();

        private CancellationTokenSource _cts;
        private Task _loopTask;

        // 手动触发复用的图像源, 保证每次触发向后取图 (而非固定第一张)
        private IImageSource _manualSource;
        private string _manualSourceKey;

        public InspectionController(HalconInspectionService service, Logger logger,
            Func<InspectionParameters> getParameters)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getParameters = getParameters ?? throw new ArgumentNullException(nameof(getParameters));
        }

        /// <summary>每次检测完成时触发 (后台线程)。</summary>
        public event Action<InspectionResult> ResultProduced;

        /// <summary>运行状态变化 (后台线程)。</summary>
        public event Action<bool> RunningChanged;

        /// <summary>状态文本变化 (后台线程)。</summary>
        public event Action<string> StatusChanged;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            lock (_sync)
            {
                if (IsRunning) return;
                _cts = new CancellationTokenSource();
                IsRunning = true;
            }

            var parameters = _getParameters();
            if (parameters == null)
            {
                _logger.Error("未加载到检测参数, 无法启动");
                SetStopped();
                return;
            }

            var source = ImageSourceFactory.Create(parameters);
            if (!source.HasImages)
            {
                _logger.Error("图像源为空, 无法启动: " + source.Description);
                SetStopped();
                return;
            }

            _logger.Success("设备启动, 图像源: " + source.Description);
            RaiseRunning(true);
            RaiseStatus("运行中");

            CancellationToken token = _cts.Token;
            _loopTask = Task.Run(() => Loop(source, token));
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            lock (_sync)
            {
                if (!IsRunning) return;
                cts = _cts;
            }

            _logger.Warn("请求停止设备...");
            RaiseStatus("停止中");
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        /// <summary>手动触发一次检测 (单次模式 / 调试用)。</summary>
        public InspectionResult TriggerOnce()
        {
            InspectionParameters p = _getParameters();
            if (p == null) return null;

            IImageSource source = GetManualSource(p);
            if (!source.HasImages)
            {
                _logger.Error("图像源为空: " + source.Description);
                return null;
            }

            string imagePath = source.Next();
            if (imagePath == null) return null;
            try
            {
                return _service.Run(imagePath, p);
            }
            catch (Exception ex)
            {
                _logger.Error("单次检测异常: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 获取手动触发使用的图像源。仅当图像源相关参数变化时才重建,
        /// 否则复用同一实例, 使连续手动触发能依次切换到下一张图。
        /// </summary>
        private IImageSource GetManualSource(InspectionParameters p)
        {
            string key = string.Join("|",
                p.ImageSourceMode,
                p.ImageFolder,
                p.SingleImagePath,
                p.SearchPattern);

            if (_manualSource == null || _manualSourceKey != key)
            {
                _manualSource = ImageSourceFactory.Create(p);
                _manualSourceKey = key;
            }
            return _manualSource;
        }

        private void Loop(IImageSource source, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    InspectionParameters p = _getParameters();
                    if (p != null && p.CycleDelayMs > 0)
                    {
                        // 单次模式: 只跑一次
                        RunSingleCycle(source, p);

                        if (!p.ContinuousMode) break;

                        if (token.WaitHandle.WaitOne(p.CycleDelayMs)) break;
                    }
                    else
                    {
                        RunSingleCycle(source, p);
                        if (token.WaitHandle.WaitOne(200)) break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检测线程异常: " + ex.Message);
            }
            finally
            {
                SetStopped();
            }
        }

        private void RunSingleCycle(IImageSource source, InspectionParameters p)
        {
            string imagePath = source.Next();
            if (string.IsNullOrEmpty(imagePath)) return;

            try
            {
                InspectionResult result = _service.Run(imagePath, p);
                var handler = ResultProduced;
                if (handler != null) handler(result);
                else result.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error("检测异常 (" + System.IO.Path.GetFileName(imagePath) + "): " + ex.Message);
            }
        }

        private void SetStopped()
        {
            lock (_sync)
            {
                IsRunning = false;
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
            _logger.Warn("设备已停止");
            RaiseRunning(false);
            RaiseStatus("已停止");
        }

        private void RaiseRunning(bool running)
        {
            var handler = RunningChanged;
            if (handler != null) handler(running);
        }

        private void RaiseStatus(string status)
        {
            var handler = StatusChanged;
            if (handler != null) handler(status);
        }
    }
}
