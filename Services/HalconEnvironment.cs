using System;
using System.IO;
using System.Linq;

namespace DentalBraceInspection.Services
{
    /// <summary>
    /// 负责定位 HALCON 运行时并配置环境变量, 保证 native halcon.dll 能被加载。
    /// 必须在第一次调用 HalconDotNet 之前执行。
    /// </summary>
    public static class HalconEnvironment
    {
        private static bool _initialized;

        public static string HalconRoot { get; private set; }

        public static string RuntimeBinPath { get; private set; }

        public static bool IsReady { get; private set; }

        public static bool EnsureInitialized()
        {
            if (_initialized) return IsReady;
            _initialized = true;

            HalconRoot = FindHalconRoot();
            if (HalconRoot == null) return false;

            RuntimeBinPath = Path.Combine(HalconRoot, "bin", "x64-win64");

            // HALCON 原生 DLL 需要位于 PATH 中
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (Directory.Exists(RuntimeBinPath) &&
                currentPath.IndexOf(RuntimeBinPath, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Environment.SetEnvironmentVariable("PATH", RuntimeBinPath + ";" + currentPath);
            }

            Environment.SetEnvironmentVariable("HALCONROOT", HalconRoot);
            Environment.SetEnvironmentVariable("HALCONARCH", "x64-win64");

            IsReady = File.Exists(Path.Combine(RuntimeBinPath, "halcon.dll"));
            return IsReady;
        }

        private static string FindHalconRoot()
        {
            // 1) 环境变量优先
            string env = Environment.GetEnvironmentVariable("HALCONROOT");
            if (!string.IsNullOrEmpty(env) &&
                File.Exists(Path.Combine(env, "bin", "x64-win64", "halcon.dll")))
            {
                return env;
            }

            // 2) 扫描默认安装目录 (取版本号最高者)
            string mvttec = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MVTec");
            if (!Directory.Exists(mvttec)) return null;

            return Directory.GetDirectories(mvttec, "HALCON-*")
                .Where(d => File.Exists(Path.Combine(d, "bin", "x64-win64", "halcon.dll")))
                .OrderByDescending(d => d)
                .FirstOrDefault();
        }
    }
}
