using System;
using System.Windows;
using System.Windows.Threading;
using DentalBraceInspection.Services;

namespace DentalBraceInspection
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局异常兜底, 避免产线软件因未处理异常直接退出
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            // 配置 HALCON 运行时路径 (必须在任何 HalconDotNet 调用之前)
            HalconEnvironment.EnsureInitialized();

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("程序发生未处理异常:\n" + e.Exception.Message,
                "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show("程序发生严重错误:\n" + (ex?.Message ?? "未知错误"),
                "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
