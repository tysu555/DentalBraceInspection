using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Services
{
    /// <summary>图像源抽象 (真实产线中可替换为相机取像)。</summary>
    public interface IImageSource
    {
        bool HasImages { get; }
        int Count { get; }
        string Next();
        void Reset();
        string Description { get; }
    }

    /// <summary>从文件夹循环取图 (模拟产线连续来料)。</summary>
    public sealed class FolderImageSource : IImageSource
    {
        private readonly List<string> _files;
        private int _index;

        public FolderImageSource(string folder, string searchPattern)
        {
            _files = new List<string>();
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                string[] patterns = (searchPattern ?? "")
                    .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToArray();
                if (patterns.Length == 0) patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" };

                foreach (string pattern in patterns)
                {
                    _files.AddRange(Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly));
                }
                // 当前目录无图时, 递归子目录查找 (方便指向数据集根目录)
                if (_files.Count == 0)
                {
                    foreach (string pattern in patterns)
                    {
                        _files.AddRange(Directory.GetFiles(folder, pattern, SearchOption.AllDirectories));
                    }
                }
                _files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            Description = _files.Count + " 张图像 / " + folder;
        }

        public bool HasImages { get { return _files.Count > 0; } }

        public int Count { get { return _files.Count; } }

        public string Description { get; private set; }

        public string Next()
        {
            if (_files.Count == 0) return null;
            string file = _files[_index % _files.Count];
            _index++;
            return file;
        }

        public void Reset()
        {
            _index = 0;
        }
    }

    /// <summary>固定单张图像。</summary>
    public sealed class SingleImageSource : IImageSource
    {
        private readonly string _path;

        public SingleImageSource(string path)
        {
            _path = path;
            Description = path;
        }

        public bool HasImages
        {
            get { return !string.IsNullOrEmpty(_path) && File.Exists(_path); }
        }

        public int Count { get { return HasImages ? 1 : 0; } }

        public string Description { get; private set; }

        public string Next()
        {
            return _path;
        }

        public void Reset() { }
    }

    public static class ImageSourceFactory
    {
        public static IImageSource Create(InspectionParameters p)
        {
            if (p != null && string.Equals(p.ImageSourceMode, "Single", StringComparison.OrdinalIgnoreCase))
                return new SingleImageSource(p.SingleImagePath);

            return new FolderImageSource(p?.ImageFolder, p?.SearchPattern);
        }
    }
}
